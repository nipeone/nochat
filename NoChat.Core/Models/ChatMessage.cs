using NoChat.Core.Chat;

namespace NoChat.Core.Models;

/// <summary>
/// 聊天消息（文本 / 文件 / 文件夹）
/// </summary>
public sealed class ChatMessage
{
    public string Id { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; }
    public bool IsRecalled { get; set; }
    public string? SessionId { get; set; }  // 私聊为对方UserId，群聊为 GroupId
    public bool IsGroup { get; set; }
    /// <summary>消息种类：文本、文件、文件夹</summary>
    public MessageKind Kind { get; set; }
}
