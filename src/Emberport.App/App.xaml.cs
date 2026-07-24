using System.Windows;
using Emberport.Services;

namespace Emberport;

public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        // Never leave a server running after the window is gone.
        ServiceRuntime.Current.StopAll();
        base.OnExit(e);
    }
}