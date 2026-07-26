using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emberport.Services;
using Microsoft.Win32;

namespace Emberport.Views;

public partial class TerminalView : UserControl
{
    // Null means "follow the web root", so changing the root keeps this in sync.
    private string? _folder;

    public TerminalView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Reload();

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        ServiceLauncher.Rescan();
        Reload();
    }

    private string CurrentFolder() => _folder ?? AppPaths.WwwRoot;

    private void Reload()
    {
        var folder = CurrentFolder();

        FolderText.Text = folder;
        FolderBadge.Text = _folder is null ? "Following the web root" : "Custom folder";
        ResetFolderButton.IsEnabled = _folder is not null;

        if (!Directory.Exists(folder))
        {
            FolderBadge.Text = "This folder no longer exists. The shell will open in the Emberport folder.";
        }

        var entries = TerminalLauncher.Entries();

        PathList.ItemsSource = entries;
        PathEmpty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var php = PhpSelection.Current.Resolve(ServiceLauncher.Installations);

        PathSubtitle.Text = php is null
            ? "No PHP build is selected yet, so php will not resolve in the shell."
            : $"php resolves to version {php.Version}, the build Apache is configured with.";
    }

    private void OnOpenTerminalClick(object sender, RoutedEventArgs e)
    {
        try
        {
            TerminalLauncher.Open(CurrentFolder());
            StatusText.Text = $"Terminal opened in {CurrentFolder()}";
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

    private void OnChooseFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the folder the shell should start in",
            InitialDirectory = Directory.Exists(CurrentFolder()) ? CurrentFolder() : AppPaths.WorkspaceRoot,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _folder = dialog.FolderName;
        StatusText.Text = string.Empty;

        Reload();
    }

    private void OnResetFolderClick(object sender, RoutedEventArgs e)
    {
        _folder = null;
        StatusText.Text = string.Empty;

        Reload();
    }
}