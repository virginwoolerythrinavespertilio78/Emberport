using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

            ["apache"] = () => Placeholder("\uE774", "Apache",
                "Configure the web server, manage ports and edit virtual hosts."),

            ["mysql"] = () => Placeholder("\uE8F1", "MySQL",
                "Switch between MySQL versions, manage the port and run backups."),

            ["redis"] = () => Placeholder("\uE945", "Redis",
                "Control the Redis server and inspect its runtime output."),

            ["php"] = () => new PhpView(),

            ["sites"] = () => new SitesView(),

            ["logs"] = () => new LogsView(),

            ["settings"] = () => new SettingsView(),

            ["about"] = () => Placeholder("\uE946", "About",
                "Emberport is built and maintained by Hojjat Jahanpour."),
        };

        NavigationSidebar.PageSelected += OnPageSelected;
        NavigateTo(DefaultPageKey);
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

    private void Welcome_Loaded(object sender, RoutedEventArgs e)
    {

    }
}