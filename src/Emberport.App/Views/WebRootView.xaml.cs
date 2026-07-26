using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emberport.Models;
using Emberport.Services;
using Microsoft.Win32;

namespace Emberport.Views;

public partial class WebRootView : UserControl
{
    public WebRootView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // The page is cached, so the path and the binaries are re-read every visit.
    private void OnLoaded(object sender, RoutedEventArgs e) => Reload();

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        ServiceLauncher.Rescan();
        Reload();
    }

    private void Reload()
    {
        var root = AppPaths.WwwRoot;
        var isDefault = string.IsNullOrWhiteSpace(AppSettings.Current.DocumentRoot);

        RootPathText.Text = root;
        RootBadge.Text = isDefault ? "Bundled folder" : "Custom folder";
        ResetRootButton.IsEnabled = !isDefault;

        var missing = !Directory.Exists(root);

        if (isDefault && missing)
        {
            // The bundled folder is ours to recreate; a custom one is not.
            Directory.CreateDirectory(root);
            missing = false;
        }

        RootWarning.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        RootWarning.Text = missing
            ? "This folder no longer exists. Apache will refuse to start until you pick another one."
            : string.Empty;

        var entries = TerminalLauncher.Entries();

        PathList.ItemsSource = entries;
        PathEmpty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenTerminalClick(object sender, RoutedEventArgs e)
    {
        try
        {
            TerminalLauncher.Open(AppPaths.WwwRoot);
            RootStatus.Text = "Terminal opened in the web root.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not open the terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnChangeRootClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the folder Apache should serve",
            InitialDirectory = Directory.Exists(AppPaths.WwwRoot) ? AppPaths.WwwRoot : AppPaths.WorkspaceRoot,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ApplyRoot(dialog.FolderName);
    }

    private void OnResetRootClick(object sender, RoutedEventArgs e) => ApplyRoot(null);

    private void ApplyRoot(string? path)
    {
        if (path is not null)
        {
            if (!Directory.Exists(path))
            {
                RootStatus.Text = "That folder does not exist.";
                return;
            }

            // Storing null keeps the setting portable when the default is chosen.
            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(path),
                    Path.TrimEndingDirectorySeparator(AppPaths.DefaultWwwRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                path = null;
            }
        }

        AppSettings.Current.DocumentRoot = path;
        AppSettings.Save();

        var restarted = false;

        try
        {
            // Apache reads the document root at boot, so the change needs a restart.
            if (ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning)
            {
                ServiceLauncher.RestartIfRunning(ServiceKind.Apache);
                restarted = true;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not restart Apache",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RootStatus.Text = restarted
            ? "Web root updated and Apache restarted."
            : "Web root updated. Start Apache to serve from it.";

        Reload();
    }

    private void OnOpenRootClick(object sender, RoutedEventArgs e)
    {
        var root = AppPaths.WwwRoot;

        if (!Directory.Exists(root))
        {
            RootStatus.Text = "That folder does not exist any more.";
            return;
        }

        Process.Start(new ProcessStartInfo(root) { UseShellExecute = true });
    }
}