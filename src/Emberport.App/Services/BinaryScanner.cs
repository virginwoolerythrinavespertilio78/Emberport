using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Emberport.Models;

namespace Emberport.Services;

public interface IBinaryScanner
{
    IReadOnlyList<BinaryInstallation> Scan(string binariesRootPath);
}

/// <summary>
/// Discovers service installations by convention:
/// {root}\{service}\{any-folder-with-a-version}\...\{executable}
/// </summary>
public sealed partial class BinaryScanner : IBinaryScanner
{
    private static readonly Dictionary<ServiceKind, Probe> Probes = new()
    {
        [ServiceKind.Php] = new Probe("php", "php.exe"),
        [ServiceKind.Apache] = new Probe("apache", "httpd.exe"),
        [ServiceKind.MySql] = new Probe("mysql", "mysqld.exe"),
        [ServiceKind.Redis] = new Probe("redis", "redis-server.exe"),
    };

    public IReadOnlyList<BinaryInstallation> Scan(string binariesRootPath)
    {
        if (string.IsNullOrWhiteSpace(binariesRootPath) || !Directory.Exists(binariesRootPath))
        {
            return [];
        }

        var discovered = new List<BinaryInstallation>();

        foreach (var (kind, probe) in Probes)
        {
            var serviceRoot = Path.Combine(binariesRootPath, probe.FolderName);
            if (!Directory.Exists(serviceRoot))
            {
                continue;
            }

            foreach (var candidate in SafeEnumerateDirectories(serviceRoot))
            {
                var executable = FindExecutable(candidate, probe.ExecutableName);
                if (executable is null)
                {
                    continue;
                }

                discovered.Add(new BinaryInstallation
                {
                    Kind = kind,
                    Version = ExtractVersion(Path.GetFileName(candidate)),
                    DirectoryPath = candidate,
                    ExecutablePath = executable,
                });
            }
        }

        return discovered
            .OrderBy(item => item.Kind)
            .ThenByDescending(item => ToComparableVersion(item.Version))
            .ToList();
    }

    // Matches the first dotted version group, e.g. "redis-x64-5.0.14.1" -> "5.0.14.1".
    // The dot is required so architecture tokens like "x64" or "Win32" are never matched.
    [GeneratedRegex(@"\d+\.\d+(?:\.\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    private static string ExtractVersion(string folderName)
    {
        var match = VersionPattern().Match(folderName);
        return match.Success ? match.Value : folderName;
    }

    private static Version ToComparableVersion(string rawVersion) =>
        Version.TryParse(rawVersion, out var parsed) ? parsed : new Version(0, 0);

    // Executables sit either at the root of the folder or inside a nested "bin" folder,
    // depending on how each project packages its Windows build.
    private static string? FindExecutable(string directory, string executableName)
    {
        var atRoot = Path.Combine(directory, executableName);
        if (File.Exists(atRoot))
        {
            return atRoot;
        }

        var inBinFolder = Path.Combine(directory, "bin", executableName);
        if (File.Exists(inBinFolder))
        {
            return inBinFolder;
        }

        try
        {
            return Directory
                .EnumerateFiles(directory, executableName, SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private sealed record Probe(string FolderName, string ExecutableName);
}