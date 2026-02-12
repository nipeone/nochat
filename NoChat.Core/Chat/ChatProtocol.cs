using System.Text.Json.Serialization;

namespace NoChat.Core.Chat;

public enum ChatPacketType
{
    Hello,
    Message,
    Recall
}

public sealed class ChatPacket
{
    [JsonPropertyName("type")]
    public ChatPacketType Type { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("isGroup")]
    public bool IsGroup { get; set; }

    [JsonPropertyName("sentAt")]
    public long? SentAt { get; set; }
}
