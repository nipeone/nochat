using System.Text.Json.Serialization;

namespace NoChat.App.Settings;

/// <summary>
/// 用于 Trimmed/AOT 发布的 JSON 序列化上下文（源生成，不依赖反射）。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettingsData))]
[JsonSerializable(typeof(ThemeMode))]
[JsonSerializable(typeof(CloseBehavior))]
[JsonSerializable(typeof(CloseChoice))]
[JsonSerializable(typeof(NoChat.App.Update.UpdateInfo))]
[JsonSerializable(typeof(NoChat.App.Update.UpdateAsset))]
[JsonSerializable(typeof(AppData))]
[JsonSerializable(typeof(GroupData))]
[JsonSerializable(typeof(MessageData))]
public partial class AppSettingsJsonContext : JsonSerializerContext
{
}
