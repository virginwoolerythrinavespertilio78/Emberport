using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Emberport.Services;
using Forms = System.Windows.Forms;

namespace Emberport.Controls;

/// <summary>A themed replacement for the default notification area menu.</summary>
public partial class TrayMenu : Window
{
    private static TrayMenu? _open;
    private static DateTime _closedAt = DateTime.MinValue;

    private bool _closing;

    private TrayMenu()
    {
        InitializeComponent();
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
        PlaceNearTray();
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

    private void OnDeactivated(object sender, EventArgs e) => Dismiss();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
        }
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