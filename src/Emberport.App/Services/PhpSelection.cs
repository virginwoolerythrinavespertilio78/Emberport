using System;
using System.Collections.Generic;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>The PHP build Apache is configured to load.</summary>
public sealed class PhpSelection
{
    public static PhpSelection Current { get; } = new();

    private string? _version = AppSettings.Current.PhpVersion;

    private PhpSelection()
    {
    }

    public string? Version
    {
        get => _version;
        set
        {
            if (string.Equals(_version, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _version = value;

            AppSettings.Current.PhpVersion = value;
            AppSettings.Save();
        }
    }

    /// <summary>Returns the chosen build, or the newest one when the choice is gone.</summary>
    public BinaryInstallation? Resolve(IEnumerable<BinaryInstallation> installations)
    {
        var builds = installations
            .Where(item => item.Kind == ServiceKind.Php)
            .ToList();

        if (builds.Count == 0)
        {
            return null;
        }

        var match = builds.FirstOrDefault(item =>
            string.Equals(item.Version, _version, StringComparison.OrdinalIgnoreCase));

        // The scanner already orders newest first, so the head is the sensible fallback.
        return match ?? builds[0];
    }
}