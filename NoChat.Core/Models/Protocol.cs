using System.Text.Json.Serialization;

namespace NoChat.Core.Models;

/// <summary>
/// 发现协议：UDP 广播包
/// </summary>
public sealed class DiscoveryPacket
{
    [JsonPropertyName("id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("machine")]
    public string MachineName { get; set; } = "";

    [JsonPropertyName("ip")]
    public string IpAddress { get; set; } = "";

    [JsonPropertyName("chatPort")]
    public int ChatPort { get; set; }

    [JsonPropertyName("filePort")]
    public int FilePort { get; set; }

    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }
}
