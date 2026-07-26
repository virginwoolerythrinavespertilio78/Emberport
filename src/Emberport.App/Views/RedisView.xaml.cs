using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class RedisView : UserControl
{
    private static readonly Brush RunningFill = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x22));
    private static readonly Brush RunningText = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush StoppedFill = new SolidColorBrush(Color.FromRgb(0x2A, 0x1D, 0x1D));
    private static readonly Brush StoppedText = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    public RedisView()
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
        var redis = ServiceLauncher.Find(ServiceKind.Redis);
        var running = ServiceRuntime.Current.For(ServiceKind.Redis).IsRunning;

        StateText.Text = running ? "RUNNING" : "STOPPED";
        StateText.Foreground = running ? RunningText : StoppedText;
        StateBadge.Background = running ? RunningFill : StoppedFill;

        VersionText.Text = redis is null
            ? "Not detected. Drop a Redis build into the bin folder."
            : $"{redis.Version}  ·  {redis.DirectoryPath}";

        ConnectionText.Text = $"127.0.0.1:{AppSettings.Current.RedisPort}";
    }

    private void OnOpenCliClick(object sender, RoutedEventArgs e)
    {
        var redis = ServiceLauncher.Find(ServiceKind.Redis);

        if (redis is null || !Directory.Exists(redis.DirectoryPath))
        {
            StatusText.Text = "No Redis build was found. Add one below and press Rescan.";
            return;
        }

        try
        {
            // The shared launcher already puts redis-cli on the PATH.
            TerminalLauncher.Open(redis.DirectoryPath);
            StatusText.Text = "A terminal opened in the Redis folder.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not open the terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var redis = ServiceLauncher.Find(ServiceKind.Redis);

        if (redis is null || !Directory.Exists(redis.DirectoryPath))
        {
            StatusText.Text = "No Redis build was found. Add one below and press Rescan.";
            return;
        }

        Process.Start(new ProcessStartInfo(redis.DirectoryPath) { UseShellExecute = true });
    }
}