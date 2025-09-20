using System;
using System.Threading;
using Avalonia;
using Velopack;

namespace VRCFaceTracking.Avalonia.Desktop;

internal sealed class Program
{
    private static readonly Mutex Mutex = new(false, "vrcfacetracking-avalonia-unique-id");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        if (!Mutex.WaitOne(TimeSpan.FromSeconds(2),
                false)) return 75; // Exit code for BSD's EX_TEMPFAIL, invite the user to try again later

        VelopackApp.Build().Run();

        try
        {
            var builder = BuildAvaloniaApp();
            return builder.StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Mutex.ReleaseMutex();
            Mutex.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
