using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Emberport.Controls;

public partial class WelcomeOverlay : UserControl
{
    private const string RepositoryUrl = "https://github.com/hojjatjh";

    private Window? _host;

    public WelcomeOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _host = Window.GetWindow(this);

        if (_host is not null)
        {
            _host.PreviewKeyDown += OnHostKeyDown;
        }

        ((Storyboard)Resources["ShowStoryboard"]).Begin(this);
    }

    private void OnHostKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter)
        {
            Dismiss();
        }
    }

    private void OnStarClick(object sender, RoutedEventArgs e)
    {
        // UseShellExecute lets Windows resolve the default browser.
        Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
        Dismiss();
    }

    private void OnContinueClick(object sender, RoutedEventArgs e) => Dismiss();

    private void Dismiss()
    {
        if (_host is not null)
        {
            _host.PreviewKeyDown -= OnHostKeyDown;
            _host = null;
        }

        ((Storyboard)Resources["HideStoryboard"]).Begin(this);
    }

    private void OnHideCompleted(object? sender, EventArgs e) =>
        Visibility = Visibility.Collapsed;
}