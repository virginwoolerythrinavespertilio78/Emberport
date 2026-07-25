using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace Emberport.Services;

/// <summary>Answers whether a TCP port is free and, if not, who is holding it.</summary>
public static class PortInspector
{
    public static bool IsInUse(int port) =>
        IPGlobalProperties
            .GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == port);

    /// <summary>Returns something like "httpd (PID 1234)", or null when the owner is unknown.</summary>
    public static string? DescribeOwner(int port)
    {
        var pid = FindListenerPid(port);

        if (pid is null)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(pid.Value);

            return $"{process.ProcessName} (PID {pid.Value})";
        }
        catch (ArgumentException)
        {
            // The process died between the scan and the lookup.
            return $"PID {pid.Value}";
        }
    }

    // netstat is the only way to map a listening port to a process without admin rights.
    private static int? FindListenerPid(int port)
    {
        try
        {
            var startInfo = new ProcessStartInfo("netstat", "-ano -p tcp")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var netstat = Process.Start(startInfo);

            if (netstat is null)
            {
                return null;
            }

            var output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit(4000);

            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 5)
                {
                    continue;
                }

                var separator = parts[1].LastIndexOf(':');

                if (separator < 0
                    || !int.TryParse(parts[1][(separator + 1)..], out var listening)
                    || listening != port)
                {
                    continue;
                }

                if (int.TryParse(parts[^1], out var pid))
                {
                    return pid;
                }
            }
        }
        catch (Exception)
        {
            // Diagnostics must never take the app down.
        }

        return null;
    }
}