using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Emberport.Services;
using Emberport.Views;

namespace Emberport;

public partial class MainWindow : Window
{
    private const string DefaultPageKey = "dashboard";

    private readonly Dictionary<string, Func<UserControl>> _pageFactories;
    private readonly Dictionary<string, UserControl> _pageCache = new();

    public MainWindow()
    {
        InitializeComponent();

        _pageFactories = new Dictionary<string, Func<UserControl>>
        {
            ["dashboard"] = () => new DashboardView(),

            ["apache"] = () => new ApacheView(),

            ["mysql"] = () => new MySqlView(),

            ["redis"] = () => new RedisView(),

            ["php"] = () => new PhpView(),

            ["sites"] = () => new WebRootView(),

            ["terminal"] = () => new TerminalView(),

            ["logs"] = () => new LogsView(),

            ["settings"] = () => new SettingsView(),

            ["about"] = () => new AboutView(),
        };

        NavigationSidebar.PageSelected += OnPageSelected;
        NavigateTo(DefaultPageKey);

        // Closing and minimising now hide the window instead of quitting.
        TrayIcon.Current.Attach(this);
    }

    private static PlaceholderView Placeholder(string glyph, string title, string description) =>
        new() { Glyph = glyph, Title = title, Description = description };

    private void OnPageSelected(object? sender, string pageKey) => NavigateTo(pageKey);

    // Views are created once and reused, so switching pages keeps their state.
    private void NavigateTo(string pageKey)
    {
        if (!_pageFactories.TryGetValue(pageKey, out var factory))
        {
            return;
        }

        if (!_pageCache.TryGetValue(pageKey, out var view))
        {
            view = factory();
            _pageCache[pageKey] = view;
        }

        PageHost.Content = view;
    }

    // The overlay asks for support, so it only appears every few days.
    private void Welcome_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement overlay)
        {
            return;
        }

        if (!WelcomeSchedule.ShouldShow())
        {
            overlay.Visibility = Visibility.Collapsed;
            return;
        }

        WelcomeSchedule.MarkShown();
    }
}