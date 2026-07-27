using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

public sealed record HealthFinding(string Title, string Detail, bool IsProblem);

/// <summary>
/// Answers the question a silent "Stopped" badge never answers: what is wrong.
/// </summary>
public static class ServiceTroubleshooter
{
    public static IReadOnlyList<HealthFinding> Inspect(ServiceKind kind)
    {
        var findings = new List<HealthFinding>();
        var installation = ServiceLauncher.Find(kind);

        if (installation is null)
        {
            findings.Add(new HealthFinding(
                "No build detected",
                $"Extract a portable {kind} build into bin\\{kind.ToString().ToLowerInvariant()} and press Rescan.",
                true));

            return findings;
        }

        findings.Add(new HealthFinding(
            "Build detected",
            $"{installation.Version} at {installation.DirectoryPath}",
            false));

        CheckExecutable(kind, installation, findings);
        CheckStrayProcess(kind, findings);
        CheckPort(kind, findings);
        CheckApachePhpPairing(kind, findings);
        CheckMySqlStorage(kind, findings);
        CheckLog(kind, findings);

        return findings;
    }

    private static void CheckExecutable(
        ServiceKind kind,
        BinaryInstallation installation,
        List<HealthFinding> findings)
    {
        var executable = ExecutablePath(kind, installation);

        if (executable is null)
        {
            return;
        }

        findings.Add(File.Exists(executable)
            ? new HealthFinding("Executable found", executable, false)
            : new HealthFinding("Executable missing", $"Expected {executable}. The build may be incomplete.", true));
    }

    private static void CheckStrayProcess(ServiceKind kind, List<HealthFinding> findings)
    {
        var name = ProcessName(kind);

        if (name is null)
        {
            return;
        }

        var count = Process.GetProcessesByName(name).Length;
        var managed = ServiceRuntime.Current.For(kind).IsRunning;

        if (count == 0)
        {
            findings.Add(new HealthFinding($"No stray {name}.exe", "Nothing from an earlier run is left behind.", false));
            return;
        }

        if (managed && count == 1)
        {
            findings.Add(new HealthFinding($"{name}.exe is running", "Started by Emberport.", false));
            return;
        }

        // An orphan keeps the data folder and the port locked, so the new one dies instantly.
        findings.Add(new HealthFinding(
            $"{count} {name}.exe process(es) already running",
            managed
                ? $"An extra copy is alive. Close it, or run: taskkill /IM {name}.exe /F"
                : $"Emberport did not start these. They lock the port and the data folder. Run: taskkill /IM {name}.exe /F",
            true));
    }

    private static void CheckPort(ServiceKind kind, List<HealthFinding> findings)
    {
        var port = ServiceLauncher.PortFor(kind);
        var running = ServiceRuntime.Current.For(kind).IsRunning;
        var busy = PortInspector.IsInUse(port);

        if (!busy)
        {
            findings.Add(new HealthFinding($"Port {port} is free", "Nothing else is listening on it.", false));
            return;
        }

        if (running)
        {
            findings.Add(new HealthFinding($"Port {port} is in use", $"{kind} itself is listening on it.", false));
            return;
        }

        var owner = PortInspector.DescribeOwner(port);

        findings.Add(new HealthFinding(
            $"Port {port} is taken",
            string.IsNullOrWhiteSpace(owner)
                ? "Another program holds it. Close it or change the port in Settings."
                : $"Held by {owner}. Close it or change the port in Settings.",
            true));
    }

    private static void CheckApachePhpPairing(ServiceKind kind, List<HealthFinding> findings)
    {
        if (kind != ServiceKind.Apache)
        {
            return;
        }

        var php = ServiceLauncher.Find(ServiceKind.Php);

        if (php is null)
        {
            findings.Add(new HealthFinding(
                "No PHP build selected",
                "Apache starts, but .php files will not run. Add a build on the PHP page.",
                true));

            return;
        }

        // Only a thread safe build ships the Apache module.
        if (PhpBuildInfo.IsThreadSafe(php))
        {
            findings.Add(new HealthFinding("PHP build is thread safe", $"{php.Version} can load into Apache.", false));
        }
        else
        {
            findings.Add(new HealthFinding(
                "PHP build is non thread safe",
                $"{php.Version} has no Apache module. Download the thread safe (TS) build instead.",
                true));
        }
    }

    private static void CheckMySqlStorage(ServiceKind kind, List<HealthFinding> findings)
    {
        if (kind != ServiceKind.MySql)
        {
            return;
        }

        var config = MySqlConfigurator.ConfigFilePath;

        findings.Add(File.Exists(config)
            ? new HealthFinding("Configuration file found", config, false)
            : new HealthFinding("No my.ini yet", $"It is written to {config} on the first start.", false));

        var data = MySqlConfigurator.DataDirectory;

        if (!Directory.Exists(data))
        {
            findings.Add(new HealthFinding(
                "Data folder missing",
                $"{data} will be created and initialized on the next start. The first run takes up to a minute.",
                false));

            return;
        }

        var missing = new List<string>();

        foreach (var entry in new[] { "ibdata1", "mysql", "sys" })
        {
            var candidate = Path.Combine(data, entry);

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                missing.Add(entry);
            }
        }

        findings.Add(missing.Count == 0
            ? new HealthFinding("Data folder looks initialized", data, false)
            : new HealthFinding(
                "Data folder is incomplete",
                $"Missing: {string.Join(", ", missing)}. A failed first run leaves it half written. Delete {data} and start MySQL again.",
                true));

        findings.Add(CanWrite(data)
            ? new HealthFinding("Data folder is writable", "MySQL can create its files.", false)
            : new HealthFinding(
                "Data folder is not writable",
                $"MySQL cannot write to {data}. Run Emberport as administrator, or exclude the folder from your antivirus.",
                true));
    }

    private static bool CanWrite(string directory)
    {
        var probe = Path.Combine(directory, $".emberport-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void CheckLog(ServiceKind kind, List<HealthFinding> findings)
    {
        var path = LogPath(kind);

        if (path is null)
        {
            return;
        }

        if (!File.Exists(path))
        {
            findings.Add(new HealthFinding("No log file yet", $"Expected {path} after the first start.", false));
            return;
        }

        var tail = Tail(path, 12);

        findings.Add(new HealthFinding(
            "Last log lines",
            string.IsNullOrWhiteSpace(tail) ? "The log is empty." : tail,
            tail.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)
                || tail.Contains("fatal", StringComparison.OrdinalIgnoreCase)
                || tail.Contains("aborting", StringComparison.OrdinalIgnoreCase)));
    }

    private static string? ExecutablePath(ServiceKind kind, BinaryInstallation installation) => kind switch
    {
        ServiceKind.Apache => Path.Combine(installation.DirectoryPath, "bin", "httpd.exe"),
        ServiceKind.MySql => Path.Combine(installation.DirectoryPath, "bin", "mysqld.exe"),
        ServiceKind.Redis => Path.Combine(installation.DirectoryPath, "redis-server.exe"),
        ServiceKind.Php => Path.Combine(installation.DirectoryPath, "php.exe"),
        _ => null,
    };

    private static string? ProcessName(ServiceKind kind) => kind switch
    {
        ServiceKind.Apache => "httpd",
        ServiceKind.MySql => "mysqld",
        ServiceKind.Redis => "redis-server",
        _ => null,
    };

    private static string? LogPath(ServiceKind kind) => kind switch
    {
        ServiceKind.Apache => ApacheDoctor.LogsDirectory() is { } directory
            ? Path.Combine(directory, "error.log")
            : null,
        ServiceKind.MySql => MySqlConfigurator.ErrorLogPath,
        _ => null,
    };

    private static string Tail(string path, int lines)
    {
        try
        {
            // The file is held open by the service, so sharing is required.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var buffer = new Queue<string>(lines);

            while (reader.ReadLine() is { } line)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                if (buffer.Count == lines)
                {
                    buffer.Dequeue();
                }

                buffer.Enqueue(line);
            }

            return string.Join(Environment.NewLine, buffer.ToArray().TakeLast(lines));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"The log could not be read: {exception.Message}";
        }
    }
}