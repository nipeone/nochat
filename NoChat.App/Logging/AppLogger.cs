using System;
using System.IO;

namespace NoChat.App.Logging;

/// <summary>
/// 简单文件日志，便于用户排查崩溃与发现失败原因。
/// 日志目录：Data/nochat.log
/// </summary>
public static class AppLogger
{
    private static readonly object Lock = new();
    private static string? _logPath;
    private static bool _initialized;

    public static void Init()
    {
        lock (Lock)
        {
            if (_initialized) return;
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Data");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "nochat.log");
                _initialized = true;
                Info("NoChat 启动，日志已启用。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppLogger.Init failed: {ex}");
            }
        }
    }

    public static string? LogFilePath
    {
        get { lock (Lock) return _logPath; }
    }

    private static void Write(string level, string message, Exception? ex = null)
    {
        lock (Lock)
        {
            if (!_initialized || string.IsNullOrEmpty(_logPath)) return;
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(_logPath, line + Environment.NewLine);
                if (ex != null)
                {
                    File.AppendAllText(_logPath, ex.ToString() + Environment.NewLine);
                }
            }
            catch { /* 写日志失败时静默 */ }
        }
    }

    public static void Debug(string message) => Write("DEBUG", message);
    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);
}
