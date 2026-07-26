using System;
using System.Collections.Generic;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Produces an Emberport-owned httpd.conf so the shipped file stays untouched.</summary>
public static class ApacheConfigurator
{
    public const int DefaultPort = 80;

    public static string Prepare(BinaryInstallation apache, BinaryInstallation? php, int port)
    {
        var templatePath = Path.Combine(apache.DirectoryPath, "conf", "httpd.conf");

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Apache is missing its default configuration.", templatePath);
        }

        var serverRoot = ToApachePath(apache.DirectoryPath);

        // The served folder is a setting, so it may live on any drive.
        // Nothing is ever written into it here; that folder belongs to the user.
        var documentRoot = ToApachePath(AppPaths.WwwRoot);

        var rewritten = new List<string>();

        foreach (var line in File.ReadAllLines(templatePath))
        {
            rewritten.Add(Rewrite(line, serverRoot, documentRoot, port));
        }

        // Apache honours the last matching directive, so appending here always wins
        // regardless of how the shipped configuration is formatted.
        rewritten.Add(string.Empty);
        rewritten.Add("# --- Emberport ---------------------------------------------------------");
        rewritten.Add($"DocumentRoot \"{documentRoot}\"");
        rewritten.Add($"<Directory \"{documentRoot}\">");
        rewritten.Add("    Options Indexes FollowSymLinks");
        rewritten.Add("    AllowOverride All");
        rewritten.Add("    Require all granted");
        rewritten.Add("</Directory>");
        rewritten.Add("DirectoryIndex index.php index.html index.htm");

        AppendPhp(rewritten, php);
        AppendPhpMyAdmin(rewritten);

        var outputPath = Path.Combine(apache.DirectoryPath, "conf", "emberport.conf");
        File.WriteAllLines(outputPath, rewritten);

        return outputPath;
    }

    private static string Rewrite(string line, string serverRoot, string documentRoot, int port)
    {
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("Define SRVROOT", StringComparison.OrdinalIgnoreCase))
        {
            return $"Define SRVROOT \"{serverRoot}\"";
        }

        if (trimmed.StartsWith("ServerRoot", StringComparison.OrdinalIgnoreCase))
        {
            return $"ServerRoot \"{serverRoot}\"";
        }

        if (trimmed.StartsWith("Listen", StringComparison.OrdinalIgnoreCase))
        {
            return $"Listen {port}";
        }

        // Covers both the active directive and the commented sample line.
        if (trimmed.StartsWith("ServerName", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("#ServerName", StringComparison.OrdinalIgnoreCase))
        {
            return $"ServerName localhost:{port}";
        }

        if (trimmed.StartsWith("DocumentRoot", StringComparison.OrdinalIgnoreCase))
        {
            return $"DocumentRoot \"{documentRoot}\"";
        }

        if (trimmed.StartsWith("<Directory \"${SRVROOT}/htdocs\">", StringComparison.OrdinalIgnoreCase))
        {
            return $"<Directory \"{documentRoot}\">";
        }

        return line;
    }

    private static void AppendPhp(List<string> lines, BinaryInstallation? php)
    {
        if (php is null)
        {
            return;
        }

        var module = PhpConfigurator.FindApacheModule(php);

        if (module is null)
        {
            // A non thread safe build has no Apache module; it can only run as CGI.
            return;
        }

        lines.Add(string.Empty);
        lines.Add($"# PHP {php.Version}");
        lines.Add($"LoadModule php_module \"{ToApachePath(module)}\"");
        lines.Add("AddHandler application/x-httpd-php .php");
        lines.Add($"PHPIniDir \"{ToApachePath(php.DirectoryPath)}\"");
    }

    private static void AppendPhpMyAdmin(List<string> lines)
    {
        var path = Path.Combine(AppPaths.ToolsRoot, "phpmyadmin");

        if (!Directory.Exists(path))
        {
            return;
        }

        var alias = ToApachePath(path);

        lines.Add(string.Empty);
        lines.Add("# phpMyAdmin");
        lines.Add($"Alias /phpmyadmin \"{alias}\"");
        lines.Add($"<Directory \"{alias}\">");
        lines.Add("    Options Indexes FollowSymLinks");
        lines.Add("    AllowOverride All");
        lines.Add("    Require local");
        lines.Add("</Directory>");
    }

    /// <summary>Apache expects forward slashes even on Windows.</summary>
    private static string ToApachePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}