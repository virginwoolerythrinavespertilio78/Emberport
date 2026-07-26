using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Emberport.Services;

public sealed record PhpExtension(string Name, bool IsEnabled);

/// <summary>
/// The single owner of extension lines in php.ini. Nothing else may append them,
/// otherwise PHP reports the same module twice.
/// </summary>
public static partial class PhpIniEditor
{
    private const string Marker = "; emberport-defaults";

    private static readonly string[] Defaults =
    [
        "curl", "exif", "fileinfo", "gd", "intl", "mbstring",
        "mysqli", "openssl", "pdo_mysql", "pdo_sqlite", "zip",
    ];

    public static IReadOnlyList<PhpExtension> Read(string iniPath)
    {
        var found = new List<PhpExtension>();

        if (!File.Exists(iniPath))
        {
            return found;
        }

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(iniPath))
        {
            var match = ExtensionLine().Match(line);

            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups[3].Value;
            var enabled = !match.Groups[2].Success;

            if (seen.TryGetValue(name, out var index))
            {
                // A single enabled line is enough for PHP to load the module.
                if (enabled)
                {
                    found[index] = found[index] with { IsEnabled = true };
                }

                continue;
            }

            seen[name] = found.Count;
            found.Add(new PhpExtension(name, enabled));
        }

        found.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        return found;
    }

    public static void SetEnabled(string iniPath, string name, bool enabled)
    {
        if (!File.Exists(iniPath))
        {
            throw new FileNotFoundException("php.ini was not found.", iniPath);
        }

        var lines = new List<string>(File.ReadAllLines(iniPath));
        var applied = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var match = ExtensionLine().Match(lines[index]);

            if (!match.Success || !string.Equals(match.Groups[3].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lines[index] = Compose(match.Groups[1].Value, match.Groups[3].Value, enabled);
            applied = true;
        }

        if (!applied && enabled)
        {
            lines.Add(Compose(string.Empty, name, true));
        }

        File.WriteAllLines(iniPath, Collapse(lines));
    }

    /// <summary>
    /// Turns the recommended extensions on the first time a php.ini is managed,
    /// then never overrides the user again.
    /// </summary>
    public static void EnsureDefaults(string iniPath)
    {
        if (!File.Exists(iniPath))
        {
            return;
        }

        var lines = new List<string>(File.ReadAllLines(iniPath));

        foreach (var line in lines)
        {
            if (line.StartsWith(Marker, StringComparison.OrdinalIgnoreCase))
            {
                // Already seeded, so only heal duplicates left behind by older builds.
                Deduplicate(iniPath);
                return;
            }
        }

        lines.Add(string.Empty);
        lines.Add($"{Marker} applied");

        File.WriteAllLines(iniPath, lines);

        foreach (var name in Defaults)
        {
            SetEnabled(iniPath, name, true);
        }
    }

    public static void Deduplicate(string iniPath)
    {
        if (!File.Exists(iniPath))
        {
            return;
        }

        var lines = new List<string>(File.ReadAllLines(iniPath));
        var collapsed = Collapse(lines);

        if (collapsed.Count != lines.Count)
        {
            File.WriteAllLines(iniPath, collapsed);
        }
    }

    // Keeps the first mention of each extension and drops every later copy.
    private static List<string> Collapse(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        var kept = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = ExtensionLine().Match(line);

            if (!match.Success)
            {
                result.Add(line);
                continue;
            }

            var name = match.Groups[3].Value;
            var enabled = !match.Groups[2].Success;

            if (!kept.TryGetValue(name, out var index))
            {
                kept[name] = result.Count;
                result.Add(line);
                continue;
            }

            // An enabled copy wins, so the module stays loaded exactly once.
            if (enabled)
            {
                result[index] = Compose(string.Empty, name, true);
            }
        }

        return result;
    }

    private static string Compose(string indent, string name, bool enabled) =>
        enabled ? $"{indent}extension={name}" : $"{indent};extension={name}";

    [GeneratedRegex(@"^(\s*)(;\s*)?extension\s*=\s*""?([A-Za-z0-9_]+)(?:\.dll)?""?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ExtensionLine();
}