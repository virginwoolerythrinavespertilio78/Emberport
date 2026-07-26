using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class ApacheView : UserControl
{
    private static readonly Brush RunningFill = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x22));
    private static readonly Brush RunningText = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush StoppedFill = new SolidColorBrush(Color.FromRgb(0x2A, 0x1D, 0x1D));
    private static readonly Brush StoppedText = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    public ApacheView()
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
        var apache = ServiceLauncher.Find(ServiceKind.Apache);
        var running = ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning;

        StateText.Text = running ? "RUNNING" : "STOPPED";
        StateText.Foreground = running ? RunningText : StoppedText;
        StateBadge.Background = running ? RunningFill : StoppedFill;

        VersionText.Text = apache is null
            ? "Not detected. Drop an Apache build into the bin folder."
            : $"{apache.Version}  ·  {apache.DirectoryPath}";

        PortText.Text = $"http://localhost:{AppSettings.Current.ApachePort}";
        RootText.Text = AppPaths.WwwRoot;
        ConfigText.Text = ApacheDoctor.ConfigPath() ?? "Not available yet.";
    }

    private void OnTestClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Running the syntax check...";

        var check = ApacheDoctor.Check();

        ResultBox.Text = check.Output;
        StatusText.Text = check.IsHealthy
            ? "Configuration is valid."
            : "Apache reported a problem. The details are below.";

        Reload();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        if (!ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning)
        {
            StatusText.Text = "Apache is not running. Start it from the dashboard.";
            return;
        }

        try
        {
            ServiceLauncher.RestartIfRunning(ServiceKind.Apache);
            StatusText.Text = "Apache restarted with the current configuration.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not restart Apache",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Reload();
    }

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        var path = ApacheDoctor.ConfigPath();

        if (path is null || !File.Exists(path))
        {
            StatusText.Text = "The configuration is generated the first time Apache starts.";
            return;
        }

        // The file is regenerated on every start, so it opens read-only in spirit.
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        var directory = ApacheDoctor.LogsDirectory();

        if (directory is null || !Directory.Exists(directory))
        {
            StatusText.Text = "No logs folder yet. Start Apache once.";
            return;
        }

        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
}