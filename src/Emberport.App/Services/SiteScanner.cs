using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Emberport.Services;

public sealed record Site(string Name, string DirectoryPath, string Url, bool HasIndex, int FileCount);

/// <summary>Treats every folder inside www as a project, the way Laragon does.</summary>
public static class SiteScanner
{
    private static readonly string[] IndexNames = ["index.php", "index.html", "index.htm"];

    public static IReadOnlyList<Site> Scan()
    {
        var root = AppPaths.WwwRoot;

        if (!Directory.Exists(root))
        {
            return [];
        }

        var port = AppSettings.Current.ApachePort;

        try
        {
            return Directory
                .GetDirectories(root)
                .Select(directory => Describe(directory, port))
                .OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static Site Describe(string directory, int port)
    {
        var name = Path.GetFileName(directory);
        var host = port == 80 ? "http://localhost" : $"http://localhost:{port}";

        return new Site(
            name,
            directory,
            $"{host}/{name}/",
            IndexNames.Any(index => File.Exists(Path.Combine(directory, index))),
            CountFiles(directory));
    }

    private static int CountFiles(string directory)
    {
        try
        {
            return Directory.GetFileSystemEntries(directory).Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>Creates the folder plus a starter index.php. Returns the new site.</summary>
    public static Site Create(string name)
    {
        var safe = Sanitize(name);

        if (safe.Length == 0)
        {
            throw new ArgumentException("Use letters, numbers, dashes or underscores.", nameof(name));
        }

        var directory = Path.Combine(AppPaths.WwwRoot, safe);

        if (Directory.Exists(directory))
        {
            throw new IOException($"A folder named {safe} already exists in www.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.php"), Starter(safe));

        return Describe(directory, AppSettings.Current.ApachePort);
    }

    public static string Sanitize(string name) =>
        new(name.Trim()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());

    private static string Starter(string name) =>
        $$"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>{{name}}</title>
            <style>
                body { margin: 0; height: 100vh; display: grid; place-items: center;
                       background: #0e0e10; color: #f2f2f5;
                       font-family: 'Segoe UI', system-ui, sans-serif; }
                h1 { margin: 0; font-size: 34px; letter-spacing: -0.5px; }
                p  { margin: 12px 0 0; color: #a1a1aa; font-size: 14px; }
                span { color: #ff8340; }
            </style>
        </head>
        <body>
            <div>
                <h1>{{name}}</h1>
                <p>Served by <span>Emberport</span> on PHP <?= PHP_VERSION ?></p>
            </div>
        </body>
        </html>

        """;
}