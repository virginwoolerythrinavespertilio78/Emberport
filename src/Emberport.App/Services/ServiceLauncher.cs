using System.Collections.Generic;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>The single place that knows how to turn a detected binary into a running process.</summary>
public static class ServiceLauncher
{
    private static IReadOnlyList<BinaryInstallation> _installations = [];

    public static IReadOnlyList<BinaryInstallation> Installations => _installations;

    public static void SetInstallations(IReadOnlyList<BinaryInstallation> installations) =>
        _installations = installations;

    /// <summary>Re-reads the bin folder so added or removed builds are picked up.</summary>
    public static IReadOnlyList<BinaryInstallation> Rescan()
    {
        IBinaryScanner scanner = new BinaryScanner();
        _installations = scanner.Scan(AppPaths.BinariesRoot);

        return _installations;
    }

    public static BinaryInstallation? Find(ServiceKind kind) =>
        _installations.FirstOrDefault(installation => installation.Kind == kind);

    public static IReadOnlyList<BinaryInstallation> PhpVersions() =>
        _installations
            .Where(installation => installation.Kind == ServiceKind.Php)
            .OrderByDescending(installation => installation.Version)
            .ToList();

    public static ProcessLaunchRequest CreateLaunchRequest(ServiceKind kind, BinaryInstallation installation)
    {
        switch (kind)
        {
            case ServiceKind.Apache:
                {
                    var php = PhpSelection.Current.Resolve(_installations);

                    if (php is not null)
                    {
                        PhpConfigurator.EnsureConfigured(php);
                    }

                    PhpMyAdminConfigurator.EnsureConfigured(MySqlConfigurator.DefaultPort);

                    var configPath = ApacheConfigurator.Prepare(installation, php, ApacheConfigurator.DefaultPort);

                    return new ProcessLaunchRequest
                    {
                        ExecutablePath = installation.ExecutablePath,
                        Arguments = $"-f \"{configPath}\"",
                    };
                }

            case ServiceKind.MySql:
                {
                    var configPath = MySqlConfigurator.EnsureConfigured(installation, MySqlConfigurator.DefaultPort);
                    MySqlConfigurator.EnsureInitialized(installation, configPath);

                    return new ProcessLaunchRequest
                    {
                        ExecutablePath = installation.ExecutablePath,
                        Arguments = $"--defaults-file=\"{configPath}\" --console",
                        WorkingDirectory = installation.DirectoryPath,
                    };
                }

            default:
                return new ProcessLaunchRequest { ExecutablePath = installation.ExecutablePath };
        }
    }

    /// <summary>Applies a configuration change to a service that is already up.</summary>
    public static void RestartIfRunning(ServiceKind kind)
    {
        var process = ServiceRuntime.Current.For(kind);

        if (!process.IsRunning)
        {
            return;
        }

        var installation = Find(kind);

        if (installation is null)
        {
            return;
        }

        process.Stop();
        process.Start(CreateLaunchRequest(kind, installation));
    }
}