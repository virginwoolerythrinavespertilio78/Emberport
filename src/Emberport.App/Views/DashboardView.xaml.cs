using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Emberport.Controls;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class DashboardView : UserControl
{
    private static readonly ServiceKind[] ManagedServices =
        [ServiceKind.Apache, ServiceKind.MySql, ServiceKind.Redis];

    private static readonly Dictionary<ServiceKind, string> Glyphs = new()
    {
        [ServiceKind.Apache] = "\uE774",
        [ServiceKind.MySql] = "\uE8F1",
        [ServiceKind.Redis] = "\uE945",
    };

    private readonly IBinaryScanner _scanner = new BinaryScanner();
    private readonly List<MonitoredService> _monitored = [];
    private readonly DispatcherTimer _statusTimer;
    private IReadOnlyList<BinaryInstallation> _installations = [];

    public DashboardView()
    {
        InitializeComponent();
        LoadServices();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += OnStatusTick;
        _statusTimer.Start();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => LoadServices();

    private void LoadServices()
    {
        var installations = _scanner.Scan(AppPaths.BinariesRoot);
        _installations = installations;

        ServiceList.Items.Clear();
        _monitored.Clear();

        foreach (var kind in ManagedServices)
        {
            // The scanner returns newest versions first, so First wins.
            var installation = installations.FirstOrDefault(item => item.Kind == kind);

            var card = new ServiceCard
            {
                Margin = new Thickness(0, 0, 0, 12),
                Title = kind.ToDisplayName(),
                Glyph = Glyphs[kind],
                IsDetected = installation is not null,
                Version = installation?.Version ?? "—",
                Detail = installation?.ExecutablePath ?? $"Place the binaries in {AppPaths.BinariesRoot}",
            };

            if (installation is not null)
            {
                Attach(card, kind, installation);
            }

            ServiceList.Items.Add(card);
        }

        var phpCount = installations.Count(item => item.Kind == ServiceKind.Php);

        SummaryLabel.Text = phpCount == 0
            ? $"No PHP versions found in {AppPaths.BinariesRoot}"
            : $"{phpCount} PHP version(s) available · workspace at {AppPaths.WorkspaceRoot}";
    }

    private void Attach(ServiceCard card, ServiceKind kind, BinaryInstallation installation)
    {
        var process = ServiceRuntime.Current.For(kind);

        card.Status = process.IsRunning ? ServiceStatus.Running : ServiceStatus.Stopped;

        card.StartRequested += (_, _) =>
        {
            card.Status = ServiceStatus.Starting;

            try
            {
                process.Start(CreateLaunchRequest(kind, installation));
                card.Status = ServiceStatus.Running;
            }
            catch (Exception exception)
            {
                card.Status = ServiceStatus.Faulted;
                MessageBox.Show(exception.Message, $"Could not start {kind.ToDisplayName()}");
            }
        };

        card.StopRequested += (_, _) =>
        {
            process.Stop();
            card.Status = ServiceStatus.Stopped;
        };

        _monitored.Add(new MonitoredService(card, process));
    }

    private ProcessLaunchRequest CreateLaunchRequest(ServiceKind kind, BinaryInstallation installation)
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

    // Polling keeps the UI honest when a service dies on its own.
    private void OnStatusTick(object? sender, EventArgs e)
    {
        foreach (var (card, process) in _monitored)
        {
            if (process.IsRunning)
            {
                card.Status = ServiceStatus.Running;
            }
            else if (card.Status == ServiceStatus.Running)
            {
                card.Status = ServiceStatus.Stopped;
            }
        }
    }

    private sealed record MonitoredService(ServiceCard Card, ManagedProcess Process);
}