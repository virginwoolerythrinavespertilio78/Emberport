using System;
using System.Collections.Generic;
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
        CheckPort(kind, findings);
        CheckApachePhpPairing(kind, findings);
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
            tail.Contains("error", StringComparison.OrdinalIgnoreCase)
                || tail.Contains("fatal", StringComparison.OrdinalIgnoreCase)));
    }

    private static string? ExecutablePath(ServiceKind kind, BinaryInstallation installation) => kind switch
    {
        ServiceKind.Apache => Path.Combine(installation.DirectoryPath, "bin", "httpd.exe"),
        ServiceKind.MySql => Path.Combine(installation.DirectoryPath, "bin", "mysqld.exe"),
        ServiceKind.Redis => Path.Combine(installation.DirectoryPath, "redis-server.exe"),
        ServiceKind.Php => Path.Combine(installation.DirectoryPath, "php.exe"),
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