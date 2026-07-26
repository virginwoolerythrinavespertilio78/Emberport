using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emberport.Services;

public sealed record PhpExtension(string Name, bool IsEnabled);

/// <summary>Toggles extension lines in a php.ini without touching anything else.</summary>
public static partial class PhpIniEditor
{
    public static IReadOnlyList<PhpExtension> Read(string iniPath)
    {
        if (!File.Exists(iniPath))
        {
            return [];
        }

        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(iniPath))
        {
            var match = ExtensionLine().Match(line);

            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups[3].Value;
            var enabled = !match.Groups[2].Success;

            // One active line anywhere in the file is enough to load the extension.
            states[name] = states.TryGetValue(name, out var current) ? current || enabled : enabled;
        }

        return states
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new PhpExtension(pair.Key, pair.Value))
            .ToList();
    }

    public static void SetEnabled(string iniPath, string name, bool enabled)
    {
        if (!File.Exists(iniPath))
        {
            return;
        }

        var lines = File.ReadAllLines(iniPath).ToList();
        var found = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var match = ExtensionLine().Match(lines[index]);

            if (!match.Success
                || !string.Equals(match.Groups[3].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found = true;

            // Every duplicate has to change, otherwise a stale line wins.
            var indent = match.Groups[1].Value;
            lines[index] = enabled ? $"{indent}extension={name}" : $"{indent};extension={name}";
        }

        if (!found && enabled)
        {
            lines.Add($"extension={name}");
        }

        File.WriteAllLines(iniPath, lines);
    }

    [GeneratedRegex(@"^(\s*)(;\s*)?extension\s*=\s*""?([A-Za-z0-9_]+)(?:\.dll)?""?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ExtensionLine();
}