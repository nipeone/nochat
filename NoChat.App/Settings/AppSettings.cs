using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoChat.App.Logging;

namespace NoChat.App.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public enum CloseBehavior
{
    AskMe,
    MinimizeToTray,
    ExitProgram
}

public enum CloseChoice
{
    None,
    MinimizeToTray,
    Exit
}

public static class AppSettings
{
    private const string FileName = "nochat-settings.json";

    private static string GetFilePath()
    {
        return Path.Combine(AppDataPath.Root, FileName);
    }

    public static ThemeMode ThemeMode
    {
        get => Load().ThemeMode;
        set { var s = Load(); s.ThemeMode = value; Save(s); }
    }

    public static string AccentColor
    {
        get => Load().AccentColor;
        set { var s = Load(); s.AccentColor = value; Save(s); }
    }

    public static CloseBehavior CloseBehavior
    {
        get => Load().CloseBehavior;
        set { var s = Load(); s.CloseBehavior = value; Save(s); }
    }

    public static bool StartOnBoot
    {
        get => Load().StartOnBoot;
        set { var s = Load(); s.StartOnBoot = value; Save(s); }
    }

    public static CloseChoice? SavedCloseChoice
    {
        get => Load().SavedCloseChoice;
        set { var s = Load(); s.SavedCloseChoice = value; Save(s); }
    }

    public static bool CheckUpdateOnStartup
    {
        get => Load().CheckUpdateOnStartup;
        set { var s = Load(); s.CheckUpdateOnStartup = value; Save(s); }
    }

    public static bool AutoCheckUpdate
    {
        get => Load().AutoCheckUpdate;
        set { var s = Load(); s.AutoCheckUpdate = value; Save(s); }
    }

    public static string? LastCheckedVersion
    {
        get => Load().LastCheckedVersion;
        set { var s = Load(); s.LastCheckedVersion = value; Save(s); }
    }

    /// <summary>
    /// 保存关闭时的用户选择（勾选“记住我的选择”时调用），同时写入 CloseBehavior 与 SavedCloseChoice，启动时优先用 CloseBehavior。
    /// </summary>
    public static void SaveClosePreference(CloseChoice choice)
    {
        if (choice == CloseChoice.None) return;
        var s = Load();
        s.SavedCloseChoice = choice;
        s.CloseBehavior = choice == CloseChoice.Exit ? CloseBehavior.ExitProgram : CloseBehavior.MinimizeToTray;
        Save(s);
    }

    public static AppSettingsData Load()
    {
        string path;
        try
        {
            path = GetFilePath();
            if (!File.Exists(path))
            {
                var defaults = new AppSettingsData();
                Save(defaults);
                return defaults;
            }
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettingsData);
            return data ?? new AppSettingsData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("[配置] Load 异常", ex);
            AppLogger.Info($"[配置] Load: 使用内存默认值");
            var defaults = new AppSettingsData();
            try
            {
                Save(defaults);
                AppLogger.Info("[配置] Load: 默认值保存成功");
            }
            catch (Exception saveEx)
            {
                AppLogger.Error("[配置] Load: 默认值保存失败", saveEx);
            }
            return defaults;
        }
    }

    public static void Save(AppSettingsData data)
    {
        try
        {
            var path = GetFilePath();
            var json = JsonSerializer.Serialize(data, AppSettingsJsonContext.Default.AppSettingsData);
            AppLogger.Info($"[配置] Save: 路径={path}, 内容长度={json.Length}");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                AppLogger.Info($"[配置] Save: 创建目录 {dir}");
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, json);
            AppLogger.Info("[配置] Save: 写入成功");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[配置] Save 异常", ex);
        }
    }
}

public class AppSettingsData
{
    [JsonPropertyName("themeMode")]
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "Blue";

    [JsonPropertyName("closeBehavior")]
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.AskMe;

    [JsonPropertyName("startOnBoot")]
    public bool StartOnBoot { get; set; }

    [JsonPropertyName("savedCloseChoice")]
    public CloseChoice? SavedCloseChoice { get; set; }

    [JsonPropertyName("checkUpdateOnStartup")]
    public bool CheckUpdateOnStartup { get; set; } = true;

    [JsonPropertyName("autoCheckUpdate")]
    public bool AutoCheckUpdate { get; set; } = true;

    [JsonPropertyName("lastCheckedVersion")]
    public string? LastCheckedVersion { get; set; }
}
