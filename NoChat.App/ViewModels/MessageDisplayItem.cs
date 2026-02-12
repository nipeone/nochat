using NoChat.Core.Models;

namespace NoChat.App.ViewModels;

public sealed class MessageDisplayItem
{
    public ChatMessage Message { get; }
    public bool IsFromMe { get; }

    public MessageDisplayItem(ChatMessage message, bool isFromMe)
    {
        Message = message;
        IsFromMe = isFromMe;
    }
}
