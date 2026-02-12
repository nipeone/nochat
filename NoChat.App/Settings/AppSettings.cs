using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private const string FileName = "nochat-settings.json";

    private static string GetFilePath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
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

    public static AppSettingsData Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return new AppSettingsData();
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<AppSettingsData>(json);
            return data ?? new AppSettingsData();
        }
        catch { return new AppSettingsData(); }
    }

    public static void Save(AppSettingsData data)
    {
        try
        {
            File.WriteAllText(GetFilePath(), JsonSerializer.Serialize(data, JsonOptions));
        }
        catch { /* ignore */ }
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
}
