using System.Diagnostics;
using System.IO;

namespace Emberport.Services;

public sealed record ProcessLaunchRequest
{
    public required string ExecutablePath { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }
}

/// <summary>Owns a single external process and keeps its lifetime under control.</summary>
public sealed class ManagedProcess : IDisposable
{
    private readonly object _gate = new();

    private Process? _process;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                try
                {
                    return _process is { HasExited: false };
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>Raised on a background thread for every stdout and stderr line.</summary>
    public event EventHandler<string>? OutputReceived;

    public void Start(ProcessLaunchRequest request)
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            if (!File.Exists(request.ExecutablePath))
            {
                throw new FileNotFoundException("Executable was not found.", request.ExecutablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory
                    ?? Path.GetDirectoryName(request.ExecutablePath)
                    ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, args) => Publish(args.Data);
            process.ErrorDataReceived += (_, args) => Publish(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _process = process;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_process is null)
            {
                return;
            }

            var process = _process;
            _process = null;

            try
            {
                if (!process.HasExited)
                {
                    // Apache and MySQL spawn children, so the whole tree has to go.
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or SystemException)
            {
                // The process already died on its own.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public void Dispose() => Stop();

    private void Publish(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            OutputReceived?.Invoke(this, line);
        }
    }
}