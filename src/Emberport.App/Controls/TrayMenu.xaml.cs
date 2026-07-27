using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Emberport.Models;
using Emberport.Services;
using Forms = System.Windows.Forms;

namespace Emberport.Controls;

/// <summary>A themed replacement for the default notification area menu.</summary>
public partial class TrayMenu : Window
{
    private static readonly SolidColorBrush RunningDot = new((Color)ColorConverter.ConvertFromString("#3DD68C"));
    private static readonly SolidColorBrush StoppedDot = new((Color)ColorConverter.ConvertFromString("#5A5A63"));

    private static TrayMenu? _open;
    private static DateTime _closedAt = DateTime.MinValue;

    private readonly DispatcherTimer _timer;

    private bool _closing;
    private bool _busy;

    private TrayMenu()
    {
        InitializeComponent();

        // Keeps the dots honest when a server dies or is toggled from the window.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
    }

    /// <summary>Opens the panel above the notification area, or closes the open one.</summary>
    public static void Toggle()
    {
        if (_open is not null)
        {
            _open.Dismiss();

            return;
        }

        // A right click first deactivates the panel, which closes it. Without this guard
        // the same click would immediately open it again.
        if (DateTime.UtcNow - _closedAt < TimeSpan.FromMilliseconds(300))
        {
            return;
        }

        var menu = new TrayMenu();
        _open = menu;

        menu.Show();
        menu.Activate();
    }

    /// <summary>The single exit path, safe to call more than once.</summary>
    private void Dismiss()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;

        if (ReferenceEquals(_open, this))
        {
            _open = null;
        }

        _closedAt = DateTime.UtcNow;

        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LocalhostLabel.Text = $"Open localhost:{AppSettings.Current.ApachePort}";

        Refresh();
        PlaceNearTray();

        _timer.Start();
    }

    private void OnClosed(object sender, EventArgs e) => _timer.Stop();

    private void Refresh()
    {
        Apply(ServiceKind.Apache, ApacheState, ApacheDot);
        Apply(ServiceKind.MySql, MySqlState, MySqlDot);
        Apply(ServiceKind.Redis, RedisState, RedisDot);
    }

    private static void Apply(ServiceKind kind, TextBlock state, Border dot)
    {
        var running = ServiceControl.IsRunning(kind);

        state.Text = running ? "Running" : "Stopped";
        dot.Background = running ? RunningDot : StoppedDot;
    }

    /// <summary>Anchors the panel to the bottom right of the working area, like a Windows flyout.</summary>
    private void PlaceNearTray()
    {
        var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var scale = Scale();

        Left = (area.Right / scale) - ActualWidth;
        Top = (area.Bottom / scale) - ActualHeight;
    }

    /// <summary>Physical pixels per device independent pixel.</summary>
    private static double Scale()
    {
        var primary = Forms.Screen.PrimaryScreen;

        if (primary is null || SystemParameters.PrimaryScreenWidth <= 0)
        {
            return 1;
        }

        var scale = primary.Bounds.Width / SystemParameters.PrimaryScreenWidth;

        return scale <= 0 ? 1 : scale;
    }

    // Starting a server can raise a dialog, which would otherwise close this panel.
    private void OnDeactivated(object sender, EventArgs e)
    {
        if (!_busy)
        {
            Dismiss();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
        }
    }

    private void OnApacheClick(object sender, RoutedEventArgs e) => Switch(ServiceKind.Apache);

    private void OnMySqlClick(object sender, RoutedEventArgs e) => Switch(ServiceKind.MySql);

    private void OnRedisClick(object sender, RoutedEventArgs e) => Switch(ServiceKind.Redis);

    private void Switch(ServiceKind kind)
    {
        _busy = true;
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            ServiceControl.Toggle(kind);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            _busy = false;
        }

        Refresh();

        // A dialog may have stolen the focus, so the panel takes it back.
        Activate();
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        Dismiss();
        TrayIcon.Current.ShowMainWindow();
    }

    private void OnLocalhostClick(object sender, RoutedEventArgs e)
    {
        Dismiss();
        OpenUrl($"http://localhost:{AppSettings.Current.ApachePort}");
    }

    private void OnPhpMyAdminClick(object sender, RoutedEventArgs e)
    {
        Dismiss();
        OpenUrl($"http://localhost:{AppSettings.Current.ApachePort}/phpmyadmin");
    }

    private void OnWebRootClick(object sender, RoutedEventArgs e)
    {
        Dismiss();

        var path = AppPaths.WwwRoot;

        if (Directory.Exists(path))
        {
            Start(new ProcessStartInfo("explorer.exe", $"\"{path}\""));
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Dismiss();
        TrayIcon.Current.RequestExit();
    }

    private static void OpenUrl(string url)
    {
        Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void Start(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            // A failed shell action is not worth interrupting the user for.
        }
    }
}