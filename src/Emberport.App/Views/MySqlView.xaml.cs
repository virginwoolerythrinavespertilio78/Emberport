using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class MySqlView : UserControl
{
    private static readonly Brush RunningFill = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x22));
    private static readonly Brush RunningText = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush StoppedFill = new SolidColorBrush(Color.FromRgb(0x2A, 0x1D, 0x1D));
    private static readonly Brush StoppedText = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    public MySqlView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Reload();

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ServiceLauncher.Rescan();
        Reload();
    }

    private void Reload()
    {
        var mysql = ServiceLauncher.Find(ServiceKind.MySql);
        var running = ServiceRuntime.Current.For(ServiceKind.MySql).IsRunning;

        StateText.Text = running ? "RUNNING" : "STOPPED";
        StateText.Foreground = running ? RunningText : StoppedText;
        StateBadge.Background = running ? RunningFill : StoppedFill;

        VersionText.Text = mysql is null
            ? "Not detected. Drop a MySQL build into the bin folder."
            : $"{mysql.Version}  ·  {mysql.DirectoryPath}";

        ConnectionText.Text = $"127.0.0.1:{AppSettings.Current.MySqlPort}  ·  user root  ·  no password";
        DataText.Text = MySqlConfigurator.DataDirectory;
        ConfigText.Text = MySqlConfigurator.ConfigFilePath;
    }

    private void OnOpenPhpMyAdminClick(object sender, RoutedEventArgs e)
    {
        if (!ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning)
        {
            StatusText.Text = "Apache serves phpMyAdmin, so start it from the dashboard first.";
            return;
        }

        var url = $"http://localhost:{AppSettings.Current.ApachePort}/phpmyadmin";

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        var path = MySqlConfigurator.ConfigFilePath;

        if (!File.Exists(path))
        {
            StatusText.Text = "my.ini is written the first time MySQL starts.";
            return;
        }

        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void OnOpenDataClick(object sender, RoutedEventArgs e)
    {
        var directory = MySqlConfigurator.DataDirectory;

        if (!Directory.Exists(directory))
        {
            StatusText.Text = "The data folder is created the first time MySQL starts.";
            return;
        }

        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void OnOpenLogClick(object sender, RoutedEventArgs e)
    {
        var path = MySqlConfigurator.ErrorLogPath;

        if (!File.Exists(path))
        {
            StatusText.Text = "No error log yet. It appears after the first start.";
            return;
        }

        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}