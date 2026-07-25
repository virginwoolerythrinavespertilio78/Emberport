using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Emberport.Models;
using Emberport.Services;
using System.Diagnostics;
using System.IO;

namespace Emberport.Views;

public partial class PhpView : UserControl
{
    public PhpView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Reload();

    private void Reload()
    {
        var versions = ServiceLauncher.PhpVersions();
        var active = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        var items = new List<PhpVersionItem>();

        foreach (var installation in versions)
        {
            items.Add(new PhpVersionItem(
                installation,
                $"PHP {installation.Version}",
                installation.DirectoryPath,
                ReferenceEquals(installation, active)));
        }

        VersionList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PhpFolderPath.Text = AppPaths.PhpRoot;
    }

    private void OnUseVersionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PhpVersionItem item })
        {
            return;
        }

        PhpSelection.Current.Version = item.Installation.Version;
        PhpConfigurator.EnsureConfigured(item.Installation);

        // Apache loads the PHP module at boot, so the change only lands after a restart.
        ServiceLauncher.RestartIfRunning(ServiceKind.Apache);

        Reload();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = AppPaths.PhpRoot;

        // The folder may not exist yet on a fresh copy of Emberport.
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://windows.php.net/download")
        {
            UseShellExecute = true,
        });

    private sealed record PhpVersionItem(
        BinaryInstallation Installation,
        string Title,
        string Location,
        bool IsActive)
    {
        public Visibility ActiveBadge => IsActive ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SwitchAction => IsActive ? Visibility.Collapsed : Visibility.Visible;
    }
}