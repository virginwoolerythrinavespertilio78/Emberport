using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Emberport.Services;

namespace Emberport;

public partial class App : Application
{
    /// <summary>True when Windows started us at sign in, so the window stays hidden.</summary>
    public static bool StartedInTray { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        StartedInTray = e.Args.Any(argument =>
            string.Equals(argument, StartupRegistration.TrayArgument, StringComparison.OrdinalIgnoreCase));

        WorkspaceSeeder.Seed();
        // A previous run may have been killed before OnExit could stop the servers.
        OrphanReaper.Sweep();

        if (StartedInTray)
        {
            // StartupUri creates the window after this method returns. Normal priority runs
            // before the first render, so the window never flashes on screen.
            Dispatcher.BeginInvoke(new Action(() => MainWindow?.Hide()), DispatcherPriority.Normal);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TrayIcon.Current.Dispose();
        // Never leave a server running after the window is gone.
        ServiceRuntime.Current.StopAll();
        base.OnExit(e);
    }
}