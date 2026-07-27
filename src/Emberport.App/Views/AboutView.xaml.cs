using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Emberport.Services;

namespace Emberport.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SignatureText.Text = $"Built by {AppInfo.Author}. A local development environment for Windows: Apache, PHP, MySQL and Redis, managed from one window.";
        VersionBadgeText.Text = AppInfo.DisplayVersion;
        RepositoryText.Text = AppInfo.RepositoryUrl;
        SystemList.ItemsSource = Rows();
        StatusText.Text = "Nothing here is sent anywhere. Copying is a manual action.";
    }

    private static List<InfoRow> Rows()
    {
        return
        [
            new InfoRow("Application", AppInfo.Signature),
            new InfoRow("Operating system", Environment.OSVersion.VersionString),
            new InfoRow("Runtime", RuntimeInformation.FrameworkDescription),
            new InfoRow("Architecture", $"{RuntimeInformation.OSArchitecture} operating system, {RuntimeInformation.ProcessArchitecture} process"),
            new InfoRow("Administrator", IsElevated() ? "Yes" : "No, some ports may be refused"),
            new InfoRow("Machine", Environment.MachineName),
            new InfoRow("Workspace", AppPaths.WorkspaceRoot),
            new InfoRow("Document root", Describe(AppPaths.WwwRoot)),
            new InfoRow("Binaries", Describe(AppPaths.BinariesRoot)),
            new InfoRow("Ports", $"Apache {AppSettings.Current.ApachePort}, MySQL {AppSettings.Current.MySqlPort}, Redis {AppSettings.Current.RedisPort}"),
        ];
    }

    /// <summary>Shows a path together with whether it actually exists.</summary>
    private static string Describe(string path)
    {
        return Directory.Exists(path) ? path : $"{path} (missing)";
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();

            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var builder = new StringBuilder();

        foreach (var row in Rows())
        {
            builder.AppendLine($"{row.Label}: {row.Value}");
        }

        try
        {
            Clipboard.SetText(builder.ToString());
            StatusText.Text = "Diagnostics copied to the clipboard.";
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            StatusText.Text = "The clipboard is busy. Try again in a moment.";
        }
    }

    private void OnStarClick(object sender, RoutedEventArgs e) => Open(AppInfo.RepositoryUrl);

    private void OnIssueClick(object sender, RoutedEventArgs e) => Open($"{AppInfo.RepositoryUrl}/issues/new");

    private void OnRepositoryClick(object sender, RoutedEventArgs e) => Open(AppInfo.RepositoryUrl);

    private void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            StatusText.Text = "Opened in your browser.";
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            StatusText.Text = $"Could not open the browser. Visit {url} manually.";
        }
    }

    private sealed record InfoRow(string Label, string Value);
}