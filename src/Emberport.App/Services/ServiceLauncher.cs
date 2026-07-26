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

    // The selected build wins; otherwise the first detected one is used.
    public static BinaryInstallation? Find(ServiceKind kind) =>
        ServiceSelection.Current.Resolve(kind, Installations);

    public static IReadOnlyList<BinaryInstallation> PhpVersions() =>
        _installations
            .Where(installation => installation.Kind == ServiceKind.Php)
            .OrderByDescending(installation => installation.Version)
            .ToList();

    /// <summary>The port a service will bind to on its next start.</summary>
    public static int PortFor(ServiceKind kind) => kind switch
    {
        ServiceKind.Apache => AppSettings.Current.ApachePort,
        ServiceKind.MySql => AppSettings.Current.MySqlPort,
        ServiceKind.Redis => AppSettings.Current.RedisPort,
        _ => 0,
    };

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

                    PhpMyAdminConfigurator.EnsureConfigured(PortFor(ServiceKind.MySql));

                    var configPath = ApacheConfigurator.Prepare(installation, php, PortFor(ServiceKind.Apache));

                    return new ProcessLaunchRequest
                    {
                        ExecutablePath = installation.ExecutablePath,
                        Arguments = $"-f \"{configPath}\"",
                    };
                }

            case ServiceKind.MySql:
                {
                    var configPath = MySqlConfigurator.EnsureConfigured(installation, PortFor(ServiceKind.MySql));
                    MySqlConfigurator.EnsureInitialized(installation, configPath);

                    return new ProcessLaunchRequest
                    {
                        ExecutablePath = installation.ExecutablePath,
                        Arguments = $"--defaults-file=\"{configPath}\" --console",
                        WorkingDirectory = installation.DirectoryPath,
                    };
                }

            case ServiceKind.Redis:
                {
                    // Redis has no config file here, so the port is passed on the command line.
                    return new ProcessLaunchRequest
                    {
                        ExecutablePath = installation.ExecutablePath,
                        Arguments = $"--port {PortFor(ServiceKind.Redis)}",
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