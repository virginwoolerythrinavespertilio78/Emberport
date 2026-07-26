using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Emberport.Models;
using Emberport.Services;

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
        Directory.CreateDirectory(AppPaths.WwwRoot);

        var items = new List<SiteItem>();

        foreach (var site in SiteScanner.Scan())
        {
            items.Add(new SiteItem(site, $"{site.Url}   ·   {site.FileCount} item(s)"));
        }

        SiteList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        Directory.CreateDirectory(AppPaths.WwwRoot);

        Process.Start(new ProcessStartInfo(AppPaths.WwwRoot) { UseShellExecute = true });
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