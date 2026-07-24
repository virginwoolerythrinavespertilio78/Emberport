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
        "fileinfo",
        "gd",
        "intl",
        "mbstring",
        "exif",
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

        if (template is null)
        {
            return;
        }

        var lines = File.ReadAllLines(template)
            .Select(EnableKnownDirective)
            .ToList();

        File.WriteAllLines(iniPath, lines);
    }

    private static string EnableKnownDirective(string line)
    {
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith(";extension_dir = \"ext\"", StringComparison.Ordinal))
        {
            return "extension_dir = \"ext\"";
        }

        foreach (var extension in DefaultExtensions)
        {
            if (trimmed.Equals($";extension={extension}", StringComparison.OrdinalIgnoreCase))
            {
                return $"extension={extension}";
            }
        }

        return line;
    }
}