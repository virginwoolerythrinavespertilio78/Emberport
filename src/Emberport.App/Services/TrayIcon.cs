using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Emberport.Services;

/// <summary>
/// Keeps Emberport alive in the notification area. Closing or minimising the window
/// hides it instead of shutting the servers down.
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
            ContextMenuStrip = BuildMenu(),
        };

        _icon.DoubleClick += (_, _) => ShowWindow();

        window.Closing += OnWindowClosing;
        window.StateChanged += OnWindowStateChanged;
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

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Drawing.Color.FromArgb(0x16, 0x16, 0x1A),
            ForeColor = Drawing.Color.FromArgb(0xF2, 0xF2, 0xF5),
            Font = new Drawing.Font("Segoe UI", 9f),
            Renderer = new Forms.ToolStripProfessionalRenderer(new DarkColors()),
        };

        var open = new Forms.ToolStripMenuItem("Open Emberport", null, (_, _) => ShowWindow())
        {
            Font = new Drawing.Font("Segoe UI", 9f, Drawing.FontStyle.Bold),
        };

        menu.Items.Add(open);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Open localhost", null, (_, _) => OpenUrl($"http://localhost:{AppSettings.Current.ApachePort}")));
        menu.Items.Add(new Forms.ToolStripMenuItem("Open phpMyAdmin", null, (_, _) => OpenUrl($"http://localhost:{AppSettings.Current.ApachePort}/phpmyadmin")));
        menu.Items.Add(new Forms.ToolStripMenuItem("Open web root", null, (_, _) => OpenFolder(AppPaths.WwwRoot)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit Emberport", null, (_, _) => Exit()));

        return menu;
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

    private void ShowWindow()
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
    private void Exit()
    {
        _exiting = true;

        if (_icon is not null)
        {
            _icon.Visible = false;
        }

        Application.Current?.Shutdown();
    }

    private static void OpenUrl(string url)
    {
        Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Start(new ProcessStartInfo("explorer.exe", $"\"{path}\""));
        }
    }

    private static void Start(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            // Nothing useful can be shown from the tray, so the click is ignored.
        }
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnWindowClosing;
            _window.StateChanged -= OnWindowStateChanged;
            _window = null;
        }

        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>Dark palette so the tray menu matches the application.</summary>
    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Surface = Drawing.Color.FromArgb(0x16, 0x16, 0x1A);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0x26, 0x26, 0x2D);
        private static readonly Drawing.Color Line = Drawing.Color.FromArgb(0x33, 0x33, 0x3C);

        public override Drawing.Color ToolStripDropDownBackground => Surface;
        public override Drawing.Color MenuBorder => Line;
        public override Drawing.Color MenuItemBorder => Line;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color MenuItemPressedGradientBegin => Hover;
        public override Drawing.Color MenuItemPressedGradientEnd => Hover;
        public override Drawing.Color ImageMarginGradientBegin => Surface;
        public override Drawing.Color ImageMarginGradientMiddle => Surface;
        public override Drawing.Color ImageMarginGradientEnd => Surface;
        public override Drawing.Color SeparatorDark => Line;
        public override Drawing.Color SeparatorLight => Line;
    }
}