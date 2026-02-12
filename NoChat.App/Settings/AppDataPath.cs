using System;
using System.IO;

namespace NoChat.App.Settings;

/// <summary>
/// 应用用户数据目录。单文件/trimmed 发布时优先用环境变量 LOCALAPPDATA（避免 GetFolderPath 被裁剪导致异常），
/// 保证在任意发布模式下都能创建并写入 nochat-settings.json。
/// Windows: %LocalAppData%\NoChat，例如 C:\Users\用户名\AppData\Local\NoChat
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
                        appData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                    if (string.IsNullOrWhiteSpace(appData))
                    {
                        try
                        {
                            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        }
                        catch
                        {
                            appData = null;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(appData))
                        appData = Path.GetTempPath();
                    var basePath = appData!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
