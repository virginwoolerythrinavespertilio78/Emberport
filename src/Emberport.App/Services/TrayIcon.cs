using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Emberport.Controls;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Emberport.Services;

/// <summary>
/// Keeps Emberport alive in the notification area. Closing or minimising the window
/// hides it instead of shutting the servers down. The menu itself is a WPF window.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly Lazy<TrayIcon> Instance = new(() => new TrayIcon());

    private Forms.NotifyIcon? _icon;
    private Window? _window;
    private bool _exiting;
    private bool _hintShown;

    private TrayIcon()
    {
    }

    public static TrayIcon Current => Instance.Value;

    /// <summary>Takes over the close and minimise behaviour of the main window.</summary>
    public void Attach(Window window)
    {
        if (_window is not null)
        {
            return;
        }

        _window = window;

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = AppInfo.Signature,
            Visible = true,
        };

        _icon.MouseUp += OnIconMouseUp;
        _icon.DoubleClick += (_, _) => ShowMainWindow();

        window.Closing += OnWindowClosing;
        window.StateChanged += OnWindowStateChanged;
    }

    private void OnIconMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Right)
        {
            TrayMenu.Toggle();
        }
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var stream = Application.GetResourceStream(new Uri("Assets/logo.ico", UriKind.Relative))?.Stream;

            if (stream is not null)
            {
                using (stream)
                {
                    return new Drawing.Icon(stream);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            // Falls through to the system icon below.
        }

        return Drawing.SystemIcons.Application;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window is { WindowState: WindowState.Minimized })
        {
            // Restore the state first so the window comes back at its normal size.
            _window.WindowState = WindowState.Normal;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        _window?.Hide();

        if (_hintShown || _icon is null)
        {
            return;
        }

        _hintShown = true;

        _icon.BalloonTipTitle = "Emberport is still running";
        _icon.BalloonTipText = "Your servers keep running. Double click the icon to open the window, or right click for Exit.";
        _icon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _icon.ShowBalloonTip(4000);
    }

    /// <summary>Brings the main window back from the notification area.</summary>
    public void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Focus();
    }

    /// <summary>Really quits, which lets App.OnExit stop every server.</summary>
    public void RequestExit()
    {
        _exiting = true;

        if (_icon is not null)
        {
            _icon.Visible = false;
        }

        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnWindowClosing;
            _window.StateChanged -= OnWindowStateChanged;
            _window = null;
        }

        if (_icon is not null)
        {
            _icon.MouseUp -= OnIconMouseUp;
            _icon.Dispose();
            _icon = null;
        }
    }
}