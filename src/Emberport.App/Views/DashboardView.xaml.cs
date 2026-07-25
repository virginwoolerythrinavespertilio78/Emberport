using Emberport.Controls;
using Emberport.Models;
using Emberport.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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

                    PrepareMySqlStorage(installation, configPath);

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

    // The very first launch has to build the system tables, which blocks the UI.
    private static void PrepareMySqlStorage(BinaryInstallation installation, string configPath)
    {
        if (MySqlConfigurator.IsInitialized())
        {
            MySqlConfigurator.EnsureInitialized(installation, configPath);
            return;
        }

        MessageBox.Show(
            """
        MySQL needs to be prepared before it can run for the first time.

        Emberport will now create the database storage in the data folder.
        This happens only once and can take up to a minute.

        The window may stop responding while this runs. That is expected,
        please do not close Emberport until it finishes.
        """,
            "Preparing MySQL",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            MySqlConfigurator.EnsureInitialized(installation, configPath);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MessageBox.Show(
            "MySQL storage is ready. The server is starting now.",
            "Preparing MySQL",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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