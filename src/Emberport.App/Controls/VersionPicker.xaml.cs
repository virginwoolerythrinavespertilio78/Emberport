using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Controls;

public partial class VersionPicker : UserControl
{
    // Guards the Checked event while the list is being rebuilt.
    private bool _isLoading;

    public VersionPicker()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        IsVisibleChanged += OnVisibleChanged;
    }

    /// <summary>Which service this picker manages. Set it in XAML.</summary>
    public ServiceKind Kind { get; set; } = ServiceKind.Apache;

    // A cached page must never show a stale list, so every appearance rescans.
    private void OnLoaded(object sender, RoutedEventArgs e) => Rescan();

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Rescan();
        }
    }

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        Rescan();
        StatusText.Text = "Rescanned the bin folder.";
    }

    private void Rescan()
    {
        ServiceLauncher.Rescan();
        Reload();
    }

    private void Reload()
    {
        _isLoading = true;

        try
        {
            var label = Kind.ToString();
            var running = ServiceRuntime.Current.For(Kind).IsRunning;

            HeaderText.Text = $"{label} versions";
            Subtitle.Text = running
                ? $"{label} is running. Choosing another build restarts it right away."
                : $"Choose the build {label} should run. It is used the next time you start it.";

            RestartButton.IsEnabled = running;

            RuleOne.Text = running
                ? "Selecting a version stops the running process and starts the new build with your current settings."
                : "Selecting a version only records your choice. Start the service from the dashboard when you are ready.";
            RuleTwo.Text = $"The choice is saved in config\\settings.json, so {label} keeps using it after you close Emberport.";
            RuleThree.Text = Kind == ServiceKind.MySql
                ? "The data folder stays where it is. Moving to an older MySQL build than the one that created it can refuse to start, so keep a backup before downgrading."
                : "If you delete the folder of the selected build, Emberport falls back to another detected version instead of failing to start.";

            var directory = Path.Combine(AppPaths.BinariesRoot, label.ToLowerInvariant());

            FolderHint.Text = directory;
            AddHint.Text = $"Extract a portable {label} build into its own subfolder here, then press Rescan. Nothing is installed system wide.";

            var available = ServiceSelection.Available(Kind, ServiceLauncher.Installations);
            var active = ServiceSelection.Current.Resolve(Kind, ServiceLauncher.Installations);

            var items = new List<VersionItem>();

            foreach (var installation in available)
            {
                var isActive = active is not null && SamePath(active.DirectoryPath, installation.DirectoryPath);

                items.Add(new VersionItem(
                    installation,
                    isActive,
                    isActive ? Visibility.Visible : Visibility.Collapsed));
            }

            VersionList.ItemsSource = items;

            EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyText.Text = $"No {label} build was found yet. Extract one into the folder below and press Rescan.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnVersionChecked(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not FrameworkElement { DataContext: VersionItem item })
        {
            return;
        }

        if (item.IsActive)
        {
            return;
        }

        // The folder may have been deleted since the list was drawn.
        if (!Directory.Exists(item.DirectoryPath))
        {
            StatusText.Text = "That build is no longer on disk. The list has been refreshed.";
            Rescan();
            return;
        }

        var running = ServiceRuntime.Current.For(Kind).IsRunning;

        if (running)
        {
            var answer = MessageBox.Show(
                $"{Kind} is running and has to restart to use {item.Version}. Restart now?",
                "Switch version",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                Reload();
                return;
            }
        }

        ServiceSelection.Current.Set(Kind, item.Version);

        try
        {
            if (running)
            {
                ServiceLauncher.RestartIfRunning(Kind);
                StatusText.Text = $"Now running {Kind} {item.Version}.";
            }
            else
            {
                StatusText.Text = $"{Kind} {item.Version} selected. It is used on the next start.";
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                $"Could not restart {Kind}",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Reload();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        if (!ServiceRuntime.Current.For(Kind).IsRunning)
        {
            StatusText.Text = $"{Kind} is not running.";
            Reload();
            return;
        }

        try
        {
            ServiceLauncher.RestartIfRunning(Kind);
            StatusText.Text = $"{Kind} restarted.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                $"Could not restart {Kind}",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Reload();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VersionItem item })
        {
            return;
        }

        if (!Directory.Exists(item.DirectoryPath))
        {
            StatusText.Text = "That folder no longer exists. The list has been refreshed.";
            Rescan();
            return;
        }

        Process.Start(new ProcessStartInfo(item.DirectoryPath) { UseShellExecute = true });
    }

    private void OnOpenBinFolderClick(object sender, RoutedEventArgs e)
    {
        var directory = Path.Combine(AppPaths.BinariesRoot, Kind.ToString().ToLowerInvariant());

        Directory.CreateDirectory(directory);

        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        var url = Kind switch
        {
            ServiceKind.Apache => "https://www.apachelounge.com/download/",
            ServiceKind.MySql => "https://dev.mysql.com/downloads/mysql/",
            ServiceKind.Redis => "https://github.com/tporadowski/redis/releases",
            _ => "https://windows.php.net/download",
        };

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private sealed record VersionItem(
        BinaryInstallation Installation,
        bool IsActive,
        Visibility ActiveBadge)
    {
        public string Version => Installation.Version;

        public string DirectoryPath => Installation.DirectoryPath;

        // The folder name carries the build tag, such as Win64-VS18 or winx64.
        public string Detail => Path.GetFileName(Path.TrimEndingDirectorySeparator(Installation.DirectoryPath));
    }
}