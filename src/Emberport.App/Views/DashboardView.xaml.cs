using Emberport.Controls;
using Emberport.Models;
using Emberport.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

    private static string SiteUrl
    {
        get
        {
            var port = AppSettings.Current.ApachePort;

            return port == 80 ? "http://localhost" : $"http://localhost:{port}";
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => LoadServices();

    private void LoadServices()
    {
        var installations = _scanner.Scan(AppPaths.BinariesRoot);
        _installations = installations;
        ServiceLauncher.SetInstallations(_installations);

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
                Attach(card, kind);
            }

            ServiceList.Items.Add(card);
        }

        var phpCount = installations.Count(item => item.Kind == ServiceKind.Php);

        SummaryLabel.Text = phpCount == 0
            ? $"No PHP versions found in {AppPaths.BinariesRoot}"
            : $"{phpCount} PHP version(s) available · workspace at {AppPaths.WorkspaceRoot}";

        SiteUrlText.Text = $"{SiteUrl}  ·  {SiteUrl}/phpmyadmin";

        UpdateActivePhp();
    }

    private void Attach(ServiceCard card, ServiceKind kind)
    {
        var process = ServiceRuntime.Current.For(kind);

        card.Status = process.IsRunning ? ServiceStatus.Running : ServiceStatus.Stopped;

        card.StartRequested += (_, _) =>
        {
            card.Status = ServiceStatus.Starting;
            card.Status = ServiceControl.Start(kind) ? ServiceStatus.Running : ServiceStatus.Stopped;
        };

        card.StopRequested += (_, _) =>
        {
            ServiceControl.Stop(kind);
            card.Status = ServiceStatus.Stopped;
        };

        _monitored.Add(new MonitoredService(card, process));
    }

    private void OnOpenSiteClick(object sender, RoutedEventArgs e) => OpenInBrowser(SiteUrl);

    private void OnOpenPhpMyAdminClick(object sender, RoutedEventArgs e) =>
        OpenInBrowser($"{SiteUrl}/phpmyadmin");

    private void OnOpenWwwClick(object sender, RoutedEventArgs e)
    {
        var folder = AppPaths.WwwRoot;

        // The folder may not exist yet if Apache has never been started.
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    // A browser tab against a dead server only shows a confusing error page.
    private void OpenInBrowser(string url)
    {
        if (!ServiceControl.IsRunning(ServiceKind.Apache))
        {
            MessageBox.Show(
                "Apache is not running. Start it from the dashboard first.",
                "Apache is stopped",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void UpdateActivePhp()
    {
        var php = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        ActivePhpText.Text = php is null ? "Not found" : php.Version;
    }

    // Polling keeps the UI honest when a service dies on its own, or when the tray starts it.
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

        UpdateActivePhp();
    }

    private sealed record MonitoredService(ServiceCard Card, ManagedProcess Process);
}