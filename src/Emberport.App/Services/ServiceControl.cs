using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>
/// The single owner of starting and stopping a server. The dashboard and the tray
/// flyout both go through here so their behaviour can never drift apart.
/// </summary>
public static class ServiceControl
{
    public static bool IsRunning(ServiceKind kind) => ServiceRuntime.Current.For(kind).IsRunning;

    /// <summary>Starts a server. Returns false when it could not be started.</summary>
    public static bool Start(ServiceKind kind)
    {
        var installation = Resolve(kind);

        if (installation is null)
        {
            MessageBox.Show(
                $"No {kind.ToDisplayName()} build was found in {AppPaths.BinariesRoot}.",
                $"{kind.ToDisplayName()} is not available",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        if (!EnsurePortIsFree(kind))
        {
            return false;
        }

        try
        {
            if (kind == ServiceKind.MySql && !MySqlConfigurator.IsInitialized())
            {
                PrepareMySqlStorage(installation);
            }

            ServiceRuntime.Current.For(kind).Start(ServiceLauncher.CreateLaunchRequest(kind, installation));

            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                $"Could not start {kind.ToDisplayName()}",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    public static void Stop(ServiceKind kind) => ServiceRuntime.Current.For(kind).Stop();

    /// <summary>Starts a stopped server, stops a running one.</summary>
    public static bool Toggle(ServiceKind kind)
    {
        if (IsRunning(kind))
        {
            Stop(kind);

            return false;
        }

        return Start(kind);
    }

    /// <summary>The build the user selected, falling back to a fresh scan when nothing is known yet.</summary>
    private static BinaryInstallation? Resolve(ServiceKind kind)
    {
        if (ServiceLauncher.Installations.Count == 0)
        {
            ServiceLauncher.Rescan();
        }

        return ServiceLauncher.Find(kind);
    }

    // A busy port makes the service exit instantly, which looks like a silent failure.
    private static bool EnsurePortIsFree(ServiceKind kind)
    {
        var port = ServiceLauncher.PortFor(kind);

        if (port == 0 || !PortInspector.IsInUse(port))
        {
            return true;
        }

        var owner = PortInspector.DescribeOwner(port) ?? "an unknown process";

        MessageBox.Show(
            $"Port {port} is already in use by {owner}.\n\n"
            + $"Close that program, or change the {kind.ToDisplayName()} port, and try again.",
            "Port is not available",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    // The very first launch has to build the system tables, which blocks the UI.
    private static void PrepareMySqlStorage(BinaryInstallation installation)
    {
        var configPath = MySqlConfigurator.EnsureConfigured(installation, MySqlConfigurator.DefaultPort);

        MessageBox.Show(
            """
            MySQL needs to be prepared before it can run for the first time.

            Emberport will now create the database storage in the data folder.
            This happens only once and can take up to a minute.

            The window may stop responding while this runs. That is expected,
            please do not close Emberport until it finishes.
            """,
            "Preparing MySQL",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            MySqlConfigurator.EnsureInitialized(installation, configPath);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }
}