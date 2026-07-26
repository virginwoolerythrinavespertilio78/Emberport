using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Creates a usable php.ini the first time a PHP version is used.</summary>
public static class PhpConfigurator
{
    private static readonly string[] DefaultExtensions =
    [
        "curl",
        "exif",
        "fileinfo",
        "gd",
        "intl",
        "mbstring",
        "mysqli",
        "openssl",
        "pdo_mysql",
        "pdo_sqlite",
        "zip",
    ];

    public static string GetIniPath(BinaryInstallation php) =>
        Path.Combine(php.DirectoryPath, "php.ini");

    /// <summary>Locates the Apache module shipped with a thread safe PHP build.</summary>
    public static string? FindApacheModule(BinaryInstallation php) =>
        Directory
            .EnumerateFiles(php.DirectoryPath, "php*apache2_4.dll")
            .FirstOrDefault();

    public static void EnsureConfigured(BinaryInstallation php)
    {
        var iniPath = GetIniPath(php);

        // Never touch an existing file; the user may have customised it.
        if (File.Exists(iniPath))
        {
            return;
        }

        var template = new[] { "php.ini-development", "php.ini-production" }
            .Select(name => Path.Combine(php.DirectoryPath, name))
            .FirstOrDefault(File.Exists);

        var lines = template is null
            ? new List<string>()
            : File.ReadAllLines(template).ToList();

        AppendOverrides(lines, php);

        File.WriteAllLines(iniPath, lines);
        // Extensions have a single owner, so seed once and heal older duplicates.
        PhpIniEditor.EnsureDefaults(iniPath);
    }

    // PHP keeps the last value it reads, so appending always wins over the template.
    private static void AppendOverrides(List<string> lines, BinaryInstallation php)
    {
        var extensionDir = Path
            .Combine(php.DirectoryPath, "ext")
            .Replace('\\', '/');

        lines.Add(string.Empty);
        lines.Add("; --- Emberport -------------------------------------------------------");
        lines.Add($"extension_dir = \"{extensionDir}\"");
        lines.Add(string.Empty);

        lines.Add(string.Empty);
        lines.Add("date.timezone = UTC");
        lines.Add("memory_limit = 512M");
        lines.Add("max_execution_time = 300");
        lines.Add("upload_max_filesize = 128M");
        lines.Add("post_max_size = 128M");
        lines.Add("display_errors = On");
        lines.Add("error_reporting = E_ALL");

    }
}