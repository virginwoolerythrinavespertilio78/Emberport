using System.Windows;
using Emberport.Services;

namespace Emberport;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        WorkspaceSeeder.Seed();
        // A previous run may have been killed before OnExit could stop the servers.
        OrphanReaper.Sweep();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Never leave a server running after the window is gone.
        ServiceRuntime.Current.StopAll();
        base.OnExit(e);
    }
}