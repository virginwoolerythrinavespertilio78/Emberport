using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>
/// Opens a shell where the bundled binaries win over anything installed system wide.
/// The PATH is built per process, so the machine's own environment stays untouched.
/// </summary>
public static class TerminalLauncher
{
    public static void Open(string workingDirectory)
    {
        var entries = Entries();

        var info = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            WorkingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : AppPaths.WorkspaceRoot,
            Arguments = entries.Count == 0
                ? "/K title Emberport"
                : "/K title Emberport && php -v",
        };

        info.EnvironmentVariables["PATH"] = BuildPath(entries);
        info.EnvironmentVariables["EMBERPORT_ROOT"] = AppPaths.WorkspaceRoot;

        Process.Start(info);
    }

    /// <summary>The folders that are prepended to PATH, in priority order.</summary>
    public static IReadOnlyList<PathEntry> Entries()
    {
        var entries = new List<PathEntry>();

        var php = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        if (php is not null && Directory.Exists(php.DirectoryPath))
        {
            entries.Add(new PathEntry($"PHP {php.Version}", php.DirectoryPath));
        }

        AddBin(entries, ServiceKind.MySql, "MySQL");
        AddBin(entries, ServiceKind.Apache, "Apache");

        var redis = ServiceLauncher.Find(ServiceKind.Redis);

        if (redis is not null && Directory.Exists(redis.DirectoryPath))
        {
            entries.Add(new PathEntry($"Redis {redis.Version}", redis.DirectoryPath));
        }

        return entries;
    }

    // Apache and MySQL keep their executables one level down.
    private static void AddBin(List<PathEntry> entries, ServiceKind kind, string label)
    {
        var installation = ServiceLauncher.Find(kind);

        if (installation is null)
        {
            return;
        }

        var bin = Path.Combine(installation.DirectoryPath, "bin");

        if (Directory.Exists(bin))
        {
            entries.Add(new PathEntry($"{label} {installation.Version}", bin));
        }
    }

    private static string BuildPath(IReadOnlyList<PathEntry> entries)
    {
        var parts = new List<string>();

        foreach (var entry in entries)
        {
            parts.Add(entry.DirectoryPath);
        }

        var inherited = Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrWhiteSpace(inherited))
        {
            parts.Add(inherited);
        }

        return string.Join(';', parts);
    }

    public sealed record PathEntry(string Label, string DirectoryPath);
}