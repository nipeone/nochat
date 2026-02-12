using Avalonia;
using System;

namespace NoChat.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (!SingleInstanceGuard.TryAcquireOrNotifyExisting())
        {
            Environment.Exit(0);
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
