using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Emberport.Models;

namespace Emberport.Services;

public sealed record ApacheCheck(bool IsHealthy, string Output, string? ConfigPath);

/// <summary>Runs Apache's own validator so a broken start is explained, not silent.</summary>
public static class ApacheDoctor
{
    public static string? ConfigPath()
    {
        var apache = ServiceLauncher.Find(ServiceKind.Apache);

        if (apache is null)
        {
            return null;
        }

        return Path.Combine(apache.DirectoryPath, "conf", "emberport.conf");
    }

    public static string? LogsDirectory()
    {
        var apache = ServiceLauncher.Find(ServiceKind.Apache);

        if (apache is null)
        {
            return null;
        }

        return Path.Combine(apache.DirectoryPath, "logs");
    }

    public static ApacheCheck Check()
    {
        var apache = ServiceLauncher.Find(ServiceKind.Apache);

        if (apache is null)
        {
            return new ApacheCheck(false, "No Apache build was detected in the bin folder.", null);
        }

        var executable = Path.Combine(apache.DirectoryPath, "bin", "httpd.exe");
        var config = Path.Combine(apache.DirectoryPath, "conf", "emberport.conf");

        if (!File.Exists(executable))
        {
            return new ApacheCheck(false, $"httpd.exe was not found at {executable}", config);
        }

        if (!File.Exists(config))
        {
            return new ApacheCheck(
                false,
                "The Emberport configuration has not been generated yet. Start Apache once from the dashboard.",
                config);
        }

        try
        {
            var info = new ProcessStartInfo(executable)
            {
                // A relative path would resolve against ServerRoot, so it stays absolute.
                Arguments = $"-f \"{config}\" -t",
                WorkingDirectory = apache.DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(info);

            if (process is null)
            {
                return new ApacheCheck(false, "Apache could not be started for the check.", config);
            }

            var text = new StringBuilder();

            text.Append(process.StandardOutput.ReadToEnd());
            text.Append(process.StandardError.ReadToEnd());

            if (!process.WaitForExit(20_000))
            {
                return new ApacheCheck(false, "The check did not finish in time.", config);
            }

            var output = text.ToString().Trim();

            if (output.Length == 0)
            {
                output = "Apache returned no output.";
            }

            return new ApacheCheck(process.ExitCode == 0, output, config);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ApacheCheck(false, exception.Message, config);
        }
    }
}