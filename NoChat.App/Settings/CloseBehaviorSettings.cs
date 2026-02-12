using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NoChat.App.Windows;

namespace NoChat.App.Settings;

public static class CloseBehaviorSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private const string FileName = "nochat-close-behavior.json";

    private static string GetFilePath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    public static CloseChoice? Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<string>(json);
            return value switch
            {
                "Exit" => CloseChoice.Exit,
                "MinimizeToTray" => CloseChoice.MinimizeToTray,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Save(CloseChoice choice)
    {
        try
        {
            var path = GetFilePath();
            var value = choice switch
            {
                CloseChoice.Exit => "Exit",
                CloseChoice.MinimizeToTray => "MinimizeToTray",
                _ => (string?)null
            };
            if (value == null) return;
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }
        catch { /* 忽略写入失败 */ }
    }
}
