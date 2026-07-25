using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class PhpView : UserControl
{
    public PhpView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // The page instance is cached, so the folder is re-read every time it is shown.
    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void OnRescanClick(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        ServiceLauncher.Rescan();
        Reload();
    }

    private void Reload()
    {
        var versions = ServiceLauncher.PhpVersions();
        var active = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        var items = new List<PhpVersionItem>();

        foreach (var installation in versions)
        {
            var isActive = active is not null && SamePath(active, installation);

            items.Add(new PhpVersionItem(
                installation,
                $"PHP {installation.Version}",
                installation.DirectoryPath,
                isActive));
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

        // The folder can disappear between two scans, so never trust the cached entry.
        if (!File.Exists(item.Installation.ExecutablePath))
        {
            MessageBox.Show(
                "That PHP build is no longer on disk. The list has been refreshed.",
                "PHP",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Refresh();
            return;
        }

        try
        {
            PhpSelection.Current.Version = item.Installation.Version;
            PhpConfigurator.EnsureConfigured(item.Installation);

            // Apache loads the PHP module at boot, so the change only lands after a restart.
            ServiceLauncher.RestartIfRunning(ServiceKind.Apache);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not switch PHP version",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

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

    // Rescanning creates new objects, so identity has to come from the path.
    private static bool SamePath(BinaryInstallation left, BinaryInstallation right) =>
        string.Equals(left.ExecutablePath, right.ExecutablePath, StringComparison.OrdinalIgnoreCase);

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