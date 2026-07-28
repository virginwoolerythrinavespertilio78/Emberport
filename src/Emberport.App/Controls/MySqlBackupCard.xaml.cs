using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Controls;

/// <summary>One click streaming mysqldump backup, with a remembered destination.</summary>
public partial class MySqlBackupCard : UserControl
{
    private static readonly Brush FailureBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x78));

    private string _directory = MySqlBackup.DefaultDirectory;
    private CancellationTokenSource? _cancellation;
    private long _estimate;
    private bool _ready;

    public MySqlBackupCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again on every navigation, so the wiring runs only once.
        if (_ready)
        {
            DescribeLastBackup();
            return;
        }

        _ready = true;

        var stored = AppSettings.Current.BackupDirectory;
        _directory = Resolve(stored, out var fallbackReason);
        FolderText.Text = _directory;

        CompressCheck.IsChecked = AppSettings.Current.BackupCompress;
        CompressCheck.Checked += OnCompressChanged;
        CompressCheck.Unchecked += OnCompressChanged;

        if (fallbackReason is not null)
        {
            // The remembered folder is gone, so repair the setting instead of failing later.
            AppSettings.Current.BackupDirectory = null;
            AppSettings.Save();
            Report(fallbackReason, FailureBrush);
            return;
        }

        DescribeLastBackup();
    }

    private void OnCompressChanged(object sender, RoutedEventArgs e) => Persist();

    private void OnChangeFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose a backup folder",
            InitialDirectory = Directory.Exists(_directory) ? _directory : AppPaths.WorkspaceRoot,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var chosen = dialog.FolderName;
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return;
        }

        _directory = Path.GetFullPath(chosen);
        FolderText.Text = _directory;
        Persist();
        Report("Backup folder saved.", NeutralBrush);
        DescribeLastBackup();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_directory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Report($"Could not open the folder: {exception.Message}", FailureBrush);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        Report("Canceling...", NeutralBrush);
    }

    private async void OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (!ServiceControl.IsRunning(ServiceKind.MySql))
        {
            Report("Start MySQL first. A dump needs a live server.", FailureBrush);
            return;
        }

        var installation = ServiceLauncher.Find(ServiceKind.MySql);
        if (installation is null)
        {
            Report("No MySQL build was found in the bin folder.", FailureBrush);
            return;
        }

        var tool = MySqlBackup.ToolPath(installation);
        if (tool is null)
        {
            Report("mysqldump.exe was not found next to mysqld.exe.", FailureBrush);
            return;
        }

        if (!ConfirmDiskSpace())
        {
            return;
        }

        var options = new MySqlBackupOptions
        {
            OutputDirectory = _directory,
            Compress = CompressCheck.IsChecked == true,
        };

        _cancellation = new CancellationTokenSource();
        BeginRun();

        var progress = new Progress<MySqlBackupProgress>(update =>
        {
            if (_estimate > 0)
            {
                Bar.IsIndeterminate = false;
                Bar.Maximum = _estimate;
                Bar.Value = Math.Min(update.RawBytes, _estimate);
            }

            var speed = update.Elapsed.TotalSeconds > 0.5
                ? MySqlBackup.Human((long)(update.RawBytes / update.Elapsed.TotalSeconds))
                : "-";

            ProgressText.Text = $"{MySqlBackup.Human(update.RawBytes)} dumped · {speed}/s · {Describe(update.Elapsed)}";
        });

        try
        {
            var result = await MySqlBackup.RunAsync(installation, options, progress, _cancellation.Token);
            Present(result);
        }
        catch (Exception exception)
        {
            Report($"The backup failed: {exception.Message}", FailureBrush);
        }
        finally
        {
            EndRun();
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private bool ConfirmDiskSpace()
    {
        _estimate = MySqlBackup.EstimateDataSize();
        if (_estimate <= 0)
        {
            return true;
        }

        // Compressed dumps land far smaller than the data folder, so the guard is proportional.
        var needed = CompressCheck.IsChecked == true ? _estimate / 4 : _estimate;
        var free = MySqlBackup.FreeSpaceFor(_directory);
        if (free < 0 || free >= needed)
        {
            return true;
        }

        Report(
            $"Not enough free space. About {MySqlBackup.Human(needed)} is needed and {MySqlBackup.Human(free)} is free.",
            FailureBrush);
        return false;
    }

    private void BeginRun()
    {
        BackupButton.IsEnabled = false;
        ChangeFolderButton.IsEnabled = false;
        CompressCheck.IsEnabled = false;
        CancelButton.IsEnabled = true;

        Bar.IsIndeterminate = _estimate <= 0;
        Bar.Value = 0;
        Bar.Visibility = Visibility.Visible;
        ProgressText.Text = "Starting mysqldump...";
        ProgressText.Visibility = Visibility.Visible;
        Report("Backup running. You can keep using the app.", NeutralBrush);
    }

    private void EndRun()
    {
        BackupButton.IsEnabled = true;
        ChangeFolderButton.IsEnabled = true;
        CompressCheck.IsEnabled = true;
        CancelButton.IsEnabled = false;

        Bar.Visibility = Visibility.Collapsed;
        ProgressText.Visibility = Visibility.Collapsed;
        Bar.IsIndeterminate = false;
    }

    private void Present(MySqlBackupResult result)
    {
        if (result.Canceled)
        {
            Report("Backup canceled. The partial file was removed.", NeutralBrush);
            return;
        }

        if (!result.Success)
        {
            Report(result.Error ?? "The backup failed.", FailureBrush);
            return;
        }

        var name = result.FilePath is null ? "the backup" : Path.GetFileName(result.FilePath);
        var ratio = result.FileBytes > 0 && result.RawBytes > result.FileBytes
            ? $" · {(double)result.RawBytes / result.FileBytes:0.0}x smaller"
            : string.Empty;

        Report(
            $"{name} · {MySqlBackup.Human(result.FileBytes)} on disk · {MySqlBackup.Human(result.RawBytes)} dumped{ratio} · {Describe(result.Elapsed)}",
            SuccessBrush);
    }

    private void DescribeLastBackup()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                Report("The folder is created the first time you back up.", NeutralBrush);
                return;
            }

            var newest = Directory.GetFiles(_directory, "emberport-*.sql*")
                .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            Report(
                newest is null
                    ? "No backup here yet."
                    : $"Last backup: {newest.Name} · {MySqlBackup.Human(newest.Length)} · {newest.LastWriteTime:yyyy-MM-dd HH:mm}",
                NeutralBrush);
        }
        catch (Exception exception)
        {
            Report($"Could not read the folder: {exception.Message}", FailureBrush);
        }
    }

    private void Persist()
    {
        // The default folder is stored as null so it follows the workspace root.
        var isDefault = string.Equals(
            Path.TrimEndingDirectorySeparator(_directory),
            Path.TrimEndingDirectorySeparator(MySqlBackup.DefaultDirectory),
            StringComparison.OrdinalIgnoreCase);

        AppSettings.Current.BackupDirectory = isDefault ? null : _directory;
        AppSettings.Current.BackupCompress = CompressCheck.IsChecked == true;
        AppSettings.Save();
    }

    private static string Resolve(string? stored, out string? fallbackReason)
    {
        fallbackReason = null;

        if (string.IsNullOrWhiteSpace(stored))
        {
            return MySqlBackup.DefaultDirectory;
        }

        try
        {
            var full = Path.GetFullPath(stored);
            var root = Path.GetPathRoot(full);

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                fallbackReason = $"The saved folder is unavailable ({stored}). Using the default folder.";
                return MySqlBackup.DefaultDirectory;
            }

            return full;
        }
        catch (Exception)
        {
            fallbackReason = "The saved folder was invalid. Using the default folder.";
            return MySqlBackup.DefaultDirectory;
        }
    }

    private void Report(string message, Brush brush)
    {
        StatusText.Text = message;
        StatusText.Foreground = brush;
    }

    private static string Describe(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
            : $"{elapsed.TotalSeconds:0.0}s";
}