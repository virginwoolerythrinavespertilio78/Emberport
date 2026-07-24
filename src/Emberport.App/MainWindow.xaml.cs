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
            ["dashboard"] = () => Placeholder("\uE80F", "Dashboard",
                "A live overview of every service, with one-click start and stop controls."),

            ["apache"] = () => Placeholder("\uE774", "Apache",
                "Configure the web server, manage ports and edit virtual hosts."),

            ["mysql"] = () => Placeholder("\uE8F1", "MySQL",
                "Switch between MySQL versions, manage the port and run backups."),

            ["redis"] = () => Placeholder("\uE945", "Redis",
                "Control the Redis server and inspect its runtime output."),

            ["php"] = () => Placeholder("\uE943", "PHP",
                "Switch the active PHP version and toggle extensions in php.ini."),

            ["sites"] = () => Placeholder("\uE8B7", "Sites",
                "Every folder in your projects directory, served as its own .test domain."),

            ["logs"] = () => Placeholder("\uE7C3", "Logs",
                "Stream error and access logs from all services in one place."),

            ["settings"] = () => Placeholder("\uE713", "Settings",
                "Choose your projects folder, binaries folder and startup behaviour."),

            ["about"] = () => Placeholder("\uE946", "About",
                "Emberport is built and maintained by Hojjat Jahanpour."),
        };

        NavigationSidebar.PageSelected += OnPageSelected;
        NavigateTo(DefaultPageKey);

        DebugScanBinaries();
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

    // TEMPORARY: verifies the scanner output. Removed in the next step.
    private static void DebugScanBinaries()
    {
        var scanner = new BinaryScanner();
        var found = scanner.Scan(@"D:\Emberport\bin");

        var report = found.Count == 0
            ? "No installations found."
            : string.Join(Environment.NewLine, found.Select(item => $"{item.DisplayName}  ->  {item.ExecutablePath}"));

        MessageBox.Show(report, "Binary scan result");
    }
}