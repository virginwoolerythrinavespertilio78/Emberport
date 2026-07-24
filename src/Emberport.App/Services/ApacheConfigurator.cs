using System.Collections.Generic;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Produces an Emberport-owned httpd.conf so the shipped file stays untouched.</summary>
public static class ApacheConfigurator
{
    public const int DefaultPort = 80;

    public static string Prepare(BinaryInstallation installation, int port)
    {
        var templatePath = Path.Combine(installation.DirectoryPath, "conf", "httpd.conf");

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Apache is missing its default configuration.", templatePath);
        }

        var serverRoot = ToApachePath(installation.DirectoryPath);
        var documentRoot = EnsureDocumentRoot();

        var rewritten = new List<string>();

        foreach (var line in File.ReadAllLines(templatePath))
        {
            rewritten.Add(Rewrite(line, serverRoot, documentRoot, port));
        }

        var outputPath = Path.Combine(installation.DirectoryPath, "conf", "emberport.conf");
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

    private static string EnsureDocumentRoot()
    {
        Directory.CreateDirectory(AppPaths.WwwRoot);

        var landingPage = Path.Combine(AppPaths.WwwRoot, "index.html");

        if (!File.Exists(landingPage))
        {
            File.WriteAllText(landingPage, LandingPage);
        }

        return ToApachePath(AppPaths.WwwRoot);
    }

    // Apache only understands forward slashes, even on Windows.
    private static string ToApachePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    private const string LandingPage = """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>Emberport</title>
            <style>
                body {
                    margin: 0;
                    height: 100vh;
                    display: grid;
                    place-items: center;
                    background: #0E0E10;
                    color: #F2F2F5;
                    font-family: "Segoe UI", sans-serif;
                }
                h1 { margin: 0; font-size: 42px; letter-spacing: -1px; }
                p { color: #A1A1AA; }
                span { color: #FF6B1A; }
            </style>
        </head>
        <body>
            <div>
                <h1>Ember<span>port</span></h1>
                <p>Your local server is running.</p>
            </div>
        </body>
        </html>
        """;
}