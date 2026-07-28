using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Services;

namespace Emberport.Views;

public partial class SettingsView : UserControl
{
    private static readonly Brush FreeBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush BusyBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApachePortBox.Text = AppSettings.Current.ApachePort.ToString(CultureInfo.InvariantCulture);
        MySqlPortBox.Text = AppSettings.Current.MySqlPort.ToString(CultureInfo.InvariantCulture);
        RedisPortBox.Text = AppSettings.Current.RedisPort.ToString(CultureInfo.InvariantCulture);
        SettingsFilePath.Text = AppSettings.FilePath;

        RefreshPortStatus();
        RefreshStartup();
    }

    private void OnRecheckClick(object sender, RoutedEventArgs e)
    {
        RefreshPortStatus();
        RefreshStartup();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPort(ApachePortBox.Text, "Apache", out var apache)
            || !TryReadPort(MySqlPortBox.Text, "MySQL", out var mySql)
            || !TryReadPort(RedisPortBox.Text, "Redis", out var redis))
        {
            return;
        }

        if (apache == mySql || apache == redis || mySql == redis)
        {
            MessageBox.Show(
                "Each service needs its own port.",
                "Duplicate port",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        AppSettings.Current.ApachePort = apache;
        AppSettings.Current.MySqlPort = mySql;
        AppSettings.Current.RedisPort = redis;
        AppSettings.Save();

        SaveStatus.Text = "Saved · restart a running service to apply its new port.";
        RefreshPortStatus();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        ApachePortBox.Text = "80";
        MySqlPortBox.Text = "3306";
        RedisPortBox.Text = "6379";

        SaveStatus.Text = "Defaults filled in · press Save to store them.";
        RefreshPortStatus();
    }

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        var folder = AppPaths.ConfigRoot;

        // The folder is created lazily, so it may not exist on a fresh copy.
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    // Windows owns this setting, not the JSON file, so the registry is always the source of truth.
    private void OnStartupClick(object sender, RoutedEventArgs e)
    {
        var wanted = StartupCheck.IsChecked == true;

        if (wanted)
        {
            StartupRegistration.Enable();
        }
        else
        {
            StartupRegistration.Disable();
        }

        var actual = StartupRegistration.IsEnabled;

        if (actual != wanted)
        {
            MessageBox.Show(
                "Windows did not allow this change. Your security software may be protecting the startup list.",
                "Could not change the startup setting",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshStartup();
    }

    private void RefreshStartup()
    {
        var enabled = StartupRegistration.IsEnabled;

        StartupCheck.IsChecked = enabled;

        StartupStatus.Text = enabled
            ? $"Registered for {Environment.UserName} · launches hidden in the notification area."
            : "Not registered · Emberport only runs when you open it yourself.";
    }

    private void RefreshPortStatus()
    {
        Describe(ApachePortBox.Text, ApachePortStatus);
        Describe(MySqlPortBox.Text, MySqlPortStatus);
        Describe(RedisPortBox.Text, RedisPortStatus);
    }

    private static void Describe(string text, TextBlock target)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            target.Text = "Not a valid port";
            target.Foreground = BusyBrush;

            return;
        }

        if (!PortInspector.IsInUse(port))
        {
            target.Text = "Available";
            target.Foreground = FreeBrush;

            return;
        }

        var owner = PortInspector.DescribeOwner(port);

        // A port held by our own service is expected, so the wording stays neutral.
        target.Text = owner is null ? "In use" : $"In use by {owner}";
        target.Foreground = BusyBrush;
    }

    private static bool TryReadPort(string text, string label, out int port)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
            && port is >= 1 and <= 65535)
        {
            return true;
        }

        MessageBox.Show(
            $"The {label} port must be a number between 1 and 65535.",
            "Invalid port",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }
}