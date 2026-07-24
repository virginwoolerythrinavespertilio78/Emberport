using System.Collections.Generic;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Remembers which PHP version Apache should load.</summary>
public sealed class PhpSelection
{
    public static PhpSelection Current { get; } = new();

    /// <summary>Null means "use the newest version that was detected".</summary>
    public string? Version { get; set; }

    public BinaryInstallation? Resolve(IReadOnlyList<BinaryInstallation> installations)
    {
        var candidates = installations
            .Where(item => item.Kind == ServiceKind.Php)
            .ToList();

        return candidates.FirstOrDefault(item => item.Version == Version)
            ?? candidates.FirstOrDefault();
    }
}