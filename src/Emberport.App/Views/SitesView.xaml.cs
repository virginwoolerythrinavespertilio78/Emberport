using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Emberport.Models;
using Emberport.Services;
using Microsoft.Win32;

namespace Emberport.Views;

public partial class SitesView : UserControl
{
    public SitesView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // The page is cached, so folders added outside the app still show up.
    private void OnLoaded(object sender, RoutedEventArgs e) => Reload();

    private void OnRescanClick(object sender, RoutedEventArgs e) => Reload();

    private void Reload()
    {
        var root = AppPaths.WwwRoot;
        var isDefault = string.IsNullOrWhiteSpace(AppSettings.Current.DocumentRoot);

        RootPathText.Text = root;
        ResetRootButton.IsEnabled = !isDefault;

        var missing = !Directory.Exists(root);

        RootWarning.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        RootWarning.Text = missing
            ? "This folder no longer exists. Apache will refuse to start until you pick another one."
            : string.Empty;

        if (isDefault && missing)
        {
            // The bundled folder is ours to recreate; a custom one is not.
            Directory.CreateDirectory(root);
            RootWarning.Visibility = Visibility.Collapsed;
        }

        var items = new List<SiteItem>();

        foreach (var site in SiteScanner.Scan())
        {
            items.Add(new SiteItem(site, $"{site.Url}   ·   {site.FileCount} item(s)"));
        }

        SiteList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            ? "Server root updated and Apache restarted."
            : "Server root updated. Start Apache to serve from it.";

        Reload();
    }

    private void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnCreateClick(sender, e);
        }
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var typed = NameBox.Text;

        if (string.IsNullOrWhiteSpace(typed))
        {
            CreateStatus.Text = "Type a name first.";
            return;
        }

        if (!Directory.Exists(AppPaths.WwwRoot))
        {
            CreateStatus.Text = "Pick a valid server root first.";
            return;
        }

        try
        {
            var site = SiteScanner.Create(typed);

            NameBox.Clear();
            CreateStatus.Text = $"Created {site.Name} · {site.Url}";

            Reload();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            CreateStatus.Text = exception.Message;
        }
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

    private void OnOpenSiteFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SiteItem item })
        {
            return;
        }

        if (!Directory.Exists(item.Site.DirectoryPath))
        {
            Reload();
            return;
        }

        Process.Start(new ProcessStartInfo(item.Site.DirectoryPath) { UseShellExecute = true });
    }

    private void OnOpenSiteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SiteItem item })
        {
            return;
        }

        // A URL without Apache running just shows a browser error page.
        if (!ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning)
        {
            MessageBox.Show(
                "Apache is not running. Start it from the dashboard first.",
                "Sites",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        Process.Start(new ProcessStartInfo(item.Site.Url) { UseShellExecute = true });
    }

    private sealed record SiteItem(Site Site, string Detail)
    {
        public string Name => Site.Name;

        public Visibility MissingIndexBadge => Site.HasIndex ? Visibility.Collapsed : Visibility.Visible;
    }
}