using System.Text.Json.Serialization;

namespace NoChat.Core.Chat;

/// <summary>数据包类型：握手、消息、命令（如撤回）</summary>
public enum ChatPacketType
{
    Hello,
    Message,
    /// <summary>命令包（如撤回），可扩展其他操作</summary>
    Command,
    /// <summary>已废弃，仅兼容旧端，请用 Command+Recall</summary>
    [Obsolete("Use Command with Command=Recall")]
    Recall = 99
}

/// <summary>消息种类：文本、文件、文件夹，供展示与后续扩展</summary>
public enum MessageKind
{
    Text,
    File,
    Folder
}

/// <summary>命令类型：撤回等，可扩展</summary>
public enum ChatCommandType
{
    Recall,
    /// <summary>创建/加入群组</summary>
    GroupCreate,
    /// <summary>退出群组</summary>
    GroupLeave,
    /// <summary>解散群组（仅群主）</summary>
    GroupDisband
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

    /// <summary>消息种类（仅当 type=Message 时有效）</summary>
    [JsonPropertyName("messageKind")]
    public MessageKind MessageKind { get; set; }

    /// <summary>命令类型（仅当 type=Command 时有效），如 Recall</summary>
    [JsonPropertyName("command")]
    public ChatCommandType? Command { get; set; }

    /// <summary>群名称（用于 GroupCreate 命令）</summary>
    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }

    /// <summary>成员ID列表（用于 GroupCreate 命令）</summary>
    [JsonPropertyName("memberIds")]
    public List<string>? MemberIds { get; set; }
}
