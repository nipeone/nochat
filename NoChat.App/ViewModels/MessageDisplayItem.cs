using NoChat.Core.Models;

namespace NoChat.App.ViewModels;

public sealed class MessageDisplayItem
{
    public ChatMessage Message { get; }
    public bool IsFromMe { get; }

    /// <summary>仅本人发送且未撤回的消息可撤回</summary>
    public bool CanRecall => IsFromMe && !Message.IsRecalled;

    public MessageDisplayItem(ChatMessage message, bool isFromMe)
    {
        Message = message;
        IsFromMe = isFromMe;
    }
}
