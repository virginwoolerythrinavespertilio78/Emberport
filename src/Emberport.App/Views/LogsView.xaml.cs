using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class LogsView : UserControl
{
    // Reading a whole access.log would freeze the UI, so only the tail is shown.
    private const int TailBytes = 200_000;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    private string _source = "apache-error";

    // Resolved once per refresh so the buttons always act on what is on screen.
    private string? _currentPath;

    public LogsView()
    {
        InitializeComponent();

        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();

        if (AutoRefreshToggle.IsChecked == true)
        {
            _timer.Start();
        }
    }

    // The page stays alive in the cache, so the timer must not survive navigation.
    private void OnUnloaded(object sender, RoutedEventArgs e) => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ServiceLauncher.Rescan();
        Refresh();
    }

    private void OnAutoRefreshChanged(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsChecked == true)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnSourceChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        _source = tag;

        // Checked fires during InitializeComponent, before the fields exist.
        if (IsLoaded)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        _currentPath = ResolveTarget();

        if (_currentPath is null)
        {
            LogPathText.Text = "No log file found.";
            SetText(Diagnose());

            return;
        }

        var info = new FileInfo(_currentPath);

        // Showing the size makes an empty file obvious instead of looking broken.
        LogPathText.Text = $"{_currentPath}   ·   {info.Length:N0} bytes   ·   {info.LastWriteTime:HH:mm:ss}";

        try
        {
            var text = Tail(_currentPath);

            SetText(text.Length == 0
                ? $"The file exists but contains no text.{Environment.NewLine}{_currentPath}"
                : text);
        }
        catch (IOException exception)
        {
            SetText($"Could not read the log file.{Environment.NewLine}{_currentPath}{Environment.NewLine}{exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            SetText($"Access denied.{Environment.NewLine}{_currentPath}{Environment.NewLine}{exception.Message}");
        }
    }

    private string? ResolveTarget()
    {
        var logsDirectory = ApacheLogsDirectory();

        if (logsDirectory is null)
        {
            return null;
        }

        var wanted = _source == "apache-access" ? "access" : "error";
        var files = Files(logsDirectory);

        var match = files
            .Where(path => Path.GetFileName(path).Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (match is not null)
        {
            return match;
        }

        // Better to show the newest log than to show nothing at all.
        return files
            .Where(path => !path.EndsWith(".pid", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    // Directory.GetFiles ignores no attributes, unlike the EnumerationOptions overloads.
    private static IReadOnlyList<string> Files(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return Directory.GetFiles(directory);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? ApacheLogsDirectory()
    {
        var root = ApacheRoot();

        if (root is null)
        {
            return null;
        }

        var logs = Path.Combine(root, "logs");

        return Directory.Exists(logs) ? logs : null;
    }

    private static string? ApacheRoot()
    {
        // Opening this page first would otherwise leave the installation list empty.
        if (ServiceLauncher.Installations.Count == 0)
        {
            ServiceLauncher.Rescan();
        }

        var apache = ServiceLauncher.Find(ServiceKind.Apache);

        if (apache is null)
        {
            return null;
        }

        var directory = apache.DirectoryPath;

        // The scanner may point at the server root or at the bin folder inside it.
        return string.Equals(Path.GetFileName(directory), "bin", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(directory)?.FullName ?? directory
            : directory;
    }

    private string CurrentFolder() => _currentPath is not null
        ? Path.GetDirectoryName(_currentPath) ?? DefaultFolder()
        : DefaultFolder();

    private static string DefaultFolder() =>
        ApacheLogsDirectory() ?? ApacheRoot() ?? AppPaths.WorkspaceRoot;

    private static string Diagnose()
    {
        var directory = DefaultFolder();
        var files = Files(directory);

        var header = "Folder:" + Environment.NewLine
            + "  " + directory + Environment.NewLine
            + "  exists: " + Directory.Exists(directory) + Environment.NewLine
            + Environment.NewLine;

        if (files.Count == 0)
        {
            return header + "Apache has not written a log yet. Start it once and press Refresh.";
        }

        var listing = files.Select(path =>
        {
            var info = new FileInfo(path);

            return $"  {info.Name}  ·  {info.Length:N0} bytes";
        });

        return header
            + $"Contents ({files.Count}):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, listing);
    }

    private void SetText(string text)
    {
        if (string.Equals(LogBox.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        LogBox.Text = text;
        LogBox.ScrollToEnd();
    }

    // FileShare.ReadWrite is what lets us read a file the service still holds open.
    private static string Tail(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length > TailBytes)
        {
            stream.Seek(-TailBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = CurrentFolder();

        Directory.CreateDirectory(folder);

        // Selecting the file is friendlier than dropping the user in the folder.
        if (_currentPath is not null && File.Exists(_currentPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_currentPath}\"")
            {
                UseShellExecute = true,
            });

            return;
        }

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_currentPath is null || !File.Exists(_currentPath))
        {
            MessageBox.Show(
                "There is no log file to clear on this tab yet.",
                "Clear log",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var path = _currentPath;

        var answer = MessageBox.Show(
            $"Empty this log file?\r\n\r\n{path}",
            "Clear log",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            // Truncating instead of deleting keeps the handle the service already owns valid.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            stream.SetLength(0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                ServiceRuntime.Current.For(ServiceKind.Apache).IsRunning
                    ? "Apache is running and keeps this file locked.\r\n\r\n"
                      + "Stop Apache from the dashboard, clear the log, then start it again."
                    : $"Windows refused to write to the file.\r\n\r\n{exception.Message}",
                "Could not clear the log",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // The tail is cached, so force the box to redraw from the empty file.
        LogBox.Clear();
        Refresh();
    }
}