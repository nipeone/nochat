using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NoChat.App.Settings;

/// <summary>
/// 应用数据（群组、聊天记录等）的根结构
/// </summary>
public class AppData
{
    [JsonPropertyName("groups")]
    public List<GroupData> Groups { get; set; } = new();

    [JsonPropertyName("privateMessages")]
    public Dictionary<string, List<MessageData>> PrivateMessages { get; set; } = new();

    [JsonPropertyName("groupMessages")]
    public Dictionary<string, List<MessageData>> GroupMessages { get; set; } = new();

    [JsonPropertyName("friendRemarks")]
    public Dictionary<string, string> FriendRemarks { get; set; } = new();
}

/// <summary>
/// 群组数据（仅保存基本信息，成员通过 UserInfo.Id 关联）
/// </summary>
public class GroupData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("memberIds")]
    public List<string> MemberIds { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 消息数据（用于持久化）
/// </summary>
public class MessageData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = "";

    [JsonPropertyName("senderName")]
    public string SenderName { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("isGroup")]
    public bool IsGroup { get; set; }

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("isRecalled")]
    public bool IsRecalled { get; set; }
}
