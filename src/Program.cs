using System.Threading;
using Velopack;

namespace Chromata;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\Chromata_SingleInstance";

    // Rooted for the process lifetime so the single-instance mutex is never finalized
    // (and released) while the tray is running.
    private static Mutex? _instanceMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        // Must run before any UI or the mutex: when the installer/updater relaunches us with its
        // hook arguments, this handles install/update/uninstall and exits. A normal launch is a no-op.
        VelopackApp.Build().Run();

        // Single resident instance per desktop session — a second launch just exits.
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
            return;

        var app = new App();
        app.InitializeComponent();
        app.Run();

        GC.KeepAlive(_instanceMutex);
    }
}
