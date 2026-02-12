using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NoChat.App.Logging;

namespace NoChat.App.Settings;

/// <summary>
/// 好友备注持久化（userId -> 备注名），存于用户数据目录下的 friend-remarks.json。
/// </summary>
public static class FriendRemarks
{
    private const string FileName = "friend-remarks.json";
    private static Dictionary<string, string>? _cache;

    private static string GetFilePath() => Path.Combine(AppDataPath.Root, FileName);

    private static void EnsureLoaded()
    {
        if (_cache != null) return;
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            else
                _cache = new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            AppLogger.Error("[备注] 加载失败", ex);
            _cache = new Dictionary<string, string>();
        }
    }

    public static string? Get(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        EnsureLoaded();
        return _cache!.TryGetValue(userId, out var v) ? v : null;
    }

    public static void Set(string userId, string? remark)
    {
        if (string.IsNullOrEmpty(userId)) return;
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(remark))
            _cache!.Remove(userId);
        else
            _cache![userId] = remark.Trim();
        try
        {
            var path = GetFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(_cache));
        }
        catch (Exception ex)
        {
            AppLogger.Error("[备注] 保存失败", ex);
        }
    }

    /// <summary>返回显示名：有备注用备注，否则用 displayName。</summary>
    public static string GetDisplayName(string userId, string displayName)
    {
        var r = Get(userId);
        return string.IsNullOrWhiteSpace(r) ? displayName : r;
    }
}
