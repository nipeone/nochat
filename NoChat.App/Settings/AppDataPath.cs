using System;
using System.IO;

namespace NoChat.App.Settings;

/// <summary>
/// 应用用户数据目录。
/// - Windows: %LocalAppData%\NoChat (例如 C:\Users\用户名\AppData\Local\NoChat)
/// - Linux: ~/.local/share/NoChat
/// - macOS: ~/Library/Application Support/NoChat
/// </summary>
public static class AppDataPath
{
    private static string? _root;
    private static readonly object Lock = new();

    /// <summary>
    /// 获取并确保存在 NoChat 用户数据根目录（用于存放设置、日志等）。
    /// 任意异常或不可写时回退到临时目录，确保 getter 从不抛出。
    /// </summary>
    public static string Root
    {
        get
        {
            if (_root != null) return _root;
            lock (Lock)
            {
                if (_root != null) return _root;
                try
                {
                    string? appData = null;

                    if (OperatingSystem.IsWindows())
                    {
                        // Windows: 优先使用环境变量，避免 GetFolderPath 被裁剪
                        appData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                        if (string.IsNullOrWhiteSpace(appData))
                        {
                            try { appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
                            catch { appData = null; }
                        }
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        // Linux: XDG_DATA_HOME 或 ~/.local/share
                        appData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                        if (string.IsNullOrWhiteSpace(appData))
                        {
                            var home = Environment.GetEnvironmentVariable("HOME");
                            if (!string.IsNullOrWhiteSpace(home))
                                appData = Path.Combine(home, ".local", "share");
                            else
                            {
                                try { appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
                                catch { appData = null; }
                            }
                        }
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        // macOS: ~/Library/Application Support
                        var home = Environment.GetEnvironmentVariable("HOME");
                        if (!string.IsNullOrWhiteSpace(home))
                            appData = Path.Combine(home, "Library", "Application Support");
                        else
                        {
                            try { appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
                            catch { appData = null; }
                        }
                    }

                    // 回退到临时目录
                    if (string.IsNullOrWhiteSpace(appData))
                        appData = Path.GetTempPath();

                    var basePath = appData.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var root = Path.Combine(basePath, "NoChat");
                    if (!Directory.Exists(root))
                        Directory.CreateDirectory(root);
                    _root = root;
                    return _root;
                }
                catch
                {
                    try
                    {
                        var fallback = Path.Combine(Path.GetTempPath(), "NoChat");
                        if (!Directory.Exists(fallback))
                            Directory.CreateDirectory(fallback);
                        _root = fallback;
                        return _root;
                    }
                    catch
                    {
                        _root = Path.GetTempPath();
                        return _root;
                    }
                }
            }
        }
    }
}
