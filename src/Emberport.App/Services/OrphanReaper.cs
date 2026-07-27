using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>
/// A hard kill of Emberport skips OnExit, so servers survive and lock their ports
/// and data folders. This clears anything left over from a previous run.
/// </summary>
public static class OrphanReaper
{
    private static readonly string[] Names = ["mysqld", "httpd", "redis-server"];

    /// <summary>Kills leftover servers that were started from this workspace.</summary>
    public static int Sweep()
    {
        var removed = 0;

        foreach (var name in Names)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (!BelongsToWorkspace(process))
                    {
                        continue;
                    }

                    if (Stop(name, process))
                    {
                        removed++;
                    }
                }
            }
        }

        return removed;
    }

    /// <summary>True when the executable lives inside this workspace's bin folder.</summary>
    private static bool BelongsToWorkspace(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return path.StartsWith(AppPaths.BinariesRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // A process owned by another user cannot be inspected, so it is left alone.
            return false;
        }
    }

    private static bool Stop(string name, Process process)
    {
        if (name == "mysqld" && ShutdownMySql())
        {
            if (process.WaitForExit(15_000))
            {
                return true;
            }
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);

            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Asks MySQL to close its files properly instead of tearing it down.</summary>
    private static bool ShutdownMySql()
    {
        var mysql = ServiceLauncher.Find(ServiceKind.MySql);

        if (mysql is null)
        {
            return false;
        }

        var admin = Path.Combine(mysql.DirectoryPath, "bin", "mysqladmin.exe");

        if (!File.Exists(admin))
        {
            return false;
        }

        try
        {
            var info = new ProcessStartInfo(admin)
            {
                Arguments = $"-u root -h 127.0.0.1 -P {AppSettings.Current.MySqlPort} shutdown",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var client = Process.Start(info);

            if (client is null)
            {
                return false;
            }

            client.WaitForExit(15_000);

            return client.HasExited && client.ExitCode == 0;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Names of leftover servers, for showing the user what was found.</summary>
    public static IReadOnlyList<string> Describe()
    {
        var found = new List<string>();

        foreach (var name in Names)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (BelongsToWorkspace(process))
                    {
                        found.Add($"{name}.exe (pid {process.Id})");
                    }
                }
            }
        }

        return found;
    }
}