using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Emberport.Models;

namespace Emberport.Services;

public sealed record MySqlBackupOptions
{
    public required string OutputDirectory { get; init; }

    public bool Compress { get; init; } = true;
}

/// <summary>Raw dump bytes produced so far, which is what progress is measured against.</summary>
public sealed record MySqlBackupProgress(long RawBytes, TimeSpan Elapsed);

public sealed record MySqlBackupResult(
    bool Success,
    bool Canceled,
    string? FilePath,
    long FileBytes,
    long RawBytes,
    TimeSpan Elapsed,
    string? Error);

/// <summary>
/// Streams a mysqldump straight to disk. Memory use stays flat no matter how large
/// the database is, and the dump can be cancelled at any point.
/// </summary>
public static class MySqlBackup
{
    private const int BufferSize = 1 << 20;

    public static string DefaultDirectory => Path.Combine(AppPaths.WorkspaceRoot, "backups");

    /// <summary>mysqldump.exe lives next to mysqld.exe in every official build.</summary>
    public static string? ToolPath(BinaryInstallation installation)
    {
        var folder = Path.GetDirectoryName(installation.ExecutablePath);

        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var path = Path.Combine(folder, "mysqldump.exe");

        return File.Exists(path) ? path : null;
    }

    /// <summary>Rough size of the live data, used for the progress bar and the disk space check.</summary>
    public static long EstimateDataSize()
    {
        try
        {
            var directory = new DirectoryInfo(MySqlConfigurator.DataDirectory);

            if (!directory.Exists)
            {
                return 0;
            }

            long total = 0;

            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    total += file.Length;
                }
                catch (IOException)
                {
                    // A file held open by the server is not worth failing over.
                }
            }

            return total;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public static long FreeSpaceFor(string directory)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(directory));

            return string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public static async Task<MySqlBackupResult> RunAsync(
        BinaryInstallation installation,
        MySqlBackupOptions options,
        IProgress<MySqlBackupProgress>? progress,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var tool = ToolPath(installation);

        if (tool is null)
        {
            return Failure("mysqldump.exe was not found next to mysqld.exe in this MySQL build.", watch.Elapsed);
        }

        string partPath;
        string finalPath;

        try
        {
            Directory.CreateDirectory(options.OutputDirectory);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            var name = options.Compress ? $"emberport-{stamp}.sql.gz" : $"emberport-{stamp}.sql";

            finalPath = Path.Combine(options.OutputDirectory, name);
            partPath = finalPath + ".part";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure($"The backup folder could not be prepared. {exception.Message}", watch.Elapsed);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = tool,
            Arguments = BuildArguments(AppSettings.Current.MySqlPort),
            WorkingDirectory = Path.GetDirectoryName(tool) ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };

        long rawBytes = 0;
        var canceled = false;

        try
        {
            process.Start();

            // The dump must never outlive Emberport either.
            ProcessJob.Current.Assign(process);
        }
        catch (Exception exception)
        {
            return Failure($"mysqldump could not be started. {exception.Message}", watch.Elapsed);
        }

        // Killing the tool is the only way to interrupt a pipe read that is already waiting.
        await using var cancelHook = token.Register(() => TryKill(process));

        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await using (var file = new FileStream(
                partPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                // Compressing inline means no temporary uncompressed copy ever touches the disk.
                Stream sink = options.Compress
                    ? new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true)
                    : file;

                try
                {
                    var buffer = new byte[BufferSize];
                    var source = process.StandardOutput.BaseStream;
                    var lastReport = 0L;

                    int read;

                    while ((read = await source.ReadAsync(buffer, token)) > 0)
                    {
                        await sink.WriteAsync(buffer.AsMemory(0, read), token);

                        rawBytes += read;

                        if (watch.ElapsedMilliseconds - lastReport >= 250)
                        {
                            lastReport = watch.ElapsedMilliseconds;
                            progress?.Report(new MySqlBackupProgress(rawBytes, watch.Elapsed));
                        }
                    }
                }
                finally
                {
                    if (options.Compress)
                    {
                        await sink.DisposeAsync();
                    }
                }

                await file.FlushAsync(CancellationToken.None);
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TryKill(process);
            TryDelete(partPath);

            return Failure(exception.Message, watch.Elapsed);
        }

        watch.Stop();

        var error = (await errorTask).Trim();

        if (canceled)
        {
            TryDelete(partPath);

            return new MySqlBackupResult(false, true, null, 0, rawBytes, watch.Elapsed, null);
        }

        if (process.ExitCode != 0)
        {
            TryDelete(partPath);

            return Failure(
                string.IsNullOrWhiteSpace(error) ? $"mysqldump exited with code {process.ExitCode}." : error,
                watch.Elapsed);
        }

        if (rawBytes == 0)
        {
            TryDelete(partPath);

            return Failure("mysqldump produced no data.", watch.Elapsed);
        }

        try
        {
            File.Move(partPath, finalPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure($"The finished backup could not be renamed. {exception.Message}", watch.Elapsed);
        }

        long fileBytes;

        try
        {
            fileBytes = new FileInfo(finalPath).Length;
        }
        catch (IOException)
        {
            fileBytes = 0;
        }

        return new MySqlBackupResult(true, false, finalPath, fileBytes, rawBytes, watch.Elapsed, null);
    }

    public static string Human(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {units[unit]}");
    }

    private static string BuildArguments(int port) => string.Join(' ',
        "--host=127.0.0.1",
        $"--port={port}",
        "--user=root",
        "--all-databases",
        // A consistent snapshot of InnoDB without blocking anyone else.
        "--single-transaction",
        "--skip-lock-tables",
        // Row by row, so a huge table never has to fit in memory.
        "--quick",
        "--routines",
        "--events",
        "--triggers",
        "--hex-blob",
        "--default-character-set=utf8mb4");

    private static MySqlBackupResult Failure(string error, TimeSpan elapsed) =>
        new(false, false, null, 0, 0, elapsed, error);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or SystemException)
        {
            // Already gone.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover .part file is harmless.
        }
    }
}