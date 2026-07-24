using System.Windows;
using System.Windows.Controls;

namespace Emberport.Controls;

public partial class TitleBar : UserControl
{
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    public TitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private Window? HostWindow => Window.GetWindow(this);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (HostWindow is not null)
        {
            HostWindow.StateChanged += (_, _) => SyncMaximizeGlyph();
            SyncMaximizeGlyph();
        }
    }

    // Keeps the maximize/restore icon in sync with the actual window state,
    // including changes made by Windows Snap or keyboard shortcuts.
    private void SyncMaximizeGlyph()
    {
        var isMaximized = HostWindow?.WindowState == WindowState.Maximized;
        MaximizeButton.Content = isMaximized ? RestoreGlyph : MaximizeGlyph;
        MaximizeButton.ToolTip = isMaximized ? "Restore" : "Maximize";
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (HostWindow is not null)
        {
            HostWindow.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (HostWindow is null)
        {
            return;
        }

        HostWindow.WindowState = HostWindow.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => HostWindow?.Close();
}