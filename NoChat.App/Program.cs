using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using NoChat.App.Logging;

namespace NoChat.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--write-icon-to" && i + 1 < args.Length)
            {
                var path = args[i + 1];
                if (path.Contains("Assets") && !Path.IsPathRooted(path))
                    path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", path));
                NoChat.App.Assets.IconHelper.WriteIconToFile(path);
                return;
            }
        }

        AppLogger.Init();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLogger.Error("未处理的异常（应用即将退出）", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error("未观察的 Task 异常", e.Exception);
        };

        if (!SingleInstanceGuard.TryAcquireOrNotifyExisting())
        {
            Environment.Exit(0);
            return;
        }
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLogger.Error("启动失败", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
