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
    private bool _isLoadingExtensions;

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
                isActive,
                PhpBuildInfo.IsThreadSafe(installation)));
        }

        VersionList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PhpFolderPath.Text = AppPaths.PhpRoot;

        LoadExtensions(active);
    }

    private void LoadExtensions(BinaryInstallation? active)
    {
        _isLoadingExtensions = true;

        try
        {
            if (active is null)
            {
                ExtensionList.ItemsSource = null;
                ExtensionSubtitle.Text = "Select a PHP version first.";

                return;
            }

            var iniPath = IniPathFor(active);
            var extensions = PhpIniEditor.Read(iniPath);

            ExtensionList.ItemsSource = extensions;

            ExtensionSubtitle.Text = extensions.Count == 0
                ? $"No extension lines were found in {iniPath}. Start Apache once so Emberport can create the file."
                : $"Editing php.ini of PHP {active.Version}. Apache must be restarted for changes to take effect.";

            ExtensionStatus.Text = string.Empty;
        }
        finally
        {
            _isLoadingExtensions = false;
        }
    }

    private void OnExtensionToggled(object sender, RoutedEventArgs e)
    {
        // Populating the list raises Checked for every enabled item.
        if (_isLoadingExtensions || sender is not CheckBox { DataContext: PhpExtension extension } box)
        {
            return;
        }

        var active = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        if (active is null)
        {
            return;
        }

        var enabled = box.IsChecked == true;

        try
        {
            PhpIniEditor.SetEnabled(IniPathFor(active), extension.Name, enabled);

            ExtensionStatus.Text = enabled
                ? $"{extension.Name} enabled · restart Apache to apply."
                : $"{extension.Name} disabled · restart Apache to apply.";
        }
        catch (Exception exception)
        {
            box.IsChecked = !enabled;

            MessageBox.Show(
                exception.Message,
                "Could not update php.ini",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnRestartApacheClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ServiceLauncher.RestartIfRunning(ServiceKind.Apache);

            ExtensionStatus.Text = ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning
                ? "Apache restarted with the current php.ini."
                : "Apache is not running, so nothing to restart.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not restart Apache",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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

        if (!PhpBuildInfo.IsThreadSafe(item.Installation))
        {
            MessageBox.Show(
                "This is a Non Thread Safe build. It has no Apache module, so Apache would fail "
                + "to start. Download the x64 Thread Safe archive of the same version instead.",
                "Incompatible PHP build",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

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

    private static string IniPathFor(BinaryInstallation installation) =>
        Path.Combine(installation.DirectoryPath, "php.ini");

    // Rescanning creates new objects, so identity has to come from the path.
    private static bool SamePath(BinaryInstallation left, BinaryInstallation right) =>
        string.Equals(left.ExecutablePath, right.ExecutablePath, StringComparison.OrdinalIgnoreCase);

    private sealed record PhpVersionItem(
        BinaryInstallation Installation,
        string Title,
        string Location,
        bool IsActive,
        bool IsThreadSafe)
    {
        public Visibility ActiveBadge => IsActive ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IncompatibleBadge => IsThreadSafe ? Visibility.Collapsed : Visibility.Visible;

        public Visibility SwitchAction =>
            !IsActive && IsThreadSafe ? Visibility.Visible : Visibility.Collapsed;
    }
}