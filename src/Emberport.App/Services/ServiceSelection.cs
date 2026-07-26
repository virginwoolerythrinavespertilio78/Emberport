using System;
using System.Collections.Generic;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>
/// Remembers which build of each service should be used, and heals itself when a
/// version is deleted from disk so a stale setting can never break a launch.
/// </summary>
public sealed class ServiceSelection
{
    public static ServiceSelection Current { get; } = new();

    private ServiceSelection()
    {
    }

    public string? VersionFor(ServiceKind kind) => kind switch
    {
        // PHP already has an owner, so this stays a single source of truth.
        ServiceKind.Php => PhpSelection.Current.Version,
        ServiceKind.Apache => AppSettings.Current.ApacheVersion,
        ServiceKind.MySql => AppSettings.Current.MySqlVersion,
        ServiceKind.Redis => AppSettings.Current.RedisVersion,
        _ => null,
    };

    public void Set(ServiceKind kind, string? version)
    {
        if (kind == ServiceKind.Php)
        {
            PhpSelection.Current.Version = version;
            return;
        }

        switch (kind)
        {
            case ServiceKind.Apache:
                AppSettings.Current.ApacheVersion = version;
                break;
            case ServiceKind.MySql:
                AppSettings.Current.MySqlVersion = version;
                break;
            case ServiceKind.Redis:
                AppSettings.Current.RedisVersion = version;
                break;
            default:
                return;
        }

        AppSettings.Save();
    }

    /// <summary>Only the builds that still exist on disk, newest first as scanned.</summary>
    public static List<BinaryInstallation> Available(
        ServiceKind kind,
        IEnumerable<BinaryInstallation> installations)
    {
        var found = new List<BinaryInstallation>();

        foreach (var installation in installations)
        {
            if (installation.Kind == kind && Directory.Exists(installation.DirectoryPath))
            {
                found.Add(installation);
            }
        }

        return found;
    }

    public BinaryInstallation? Resolve(ServiceKind kind, IEnumerable<BinaryInstallation> installations)
    {
        var candidates = Available(kind, installations);

        if (candidates.Count == 0)
        {
            return null;
        }

        var wanted = VersionFor(kind);

        if (!string.IsNullOrWhiteSpace(wanted))
        {
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.Version, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            // The chosen build is gone, so the setting is dropped instead of failing.
            Set(kind, null);
        }

        return candidates[0];
    }
}