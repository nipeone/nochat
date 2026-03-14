using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoChat.Core.Models;

/// <summary>
/// 群组会话信息
/// </summary>
public sealed class GroupSession : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = "";
    private string _name = "";
    private DateTime _createdAt;
    private DateTime _lastMessageTime;
    private int _unreadCount;

    public string Id { get => _id; set { _id = value ?? ""; Raise(); } }
    public string Name { get => _name; set { _name = value ?? ""; Raise(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; Raise(); } }
    public DateTime LastMessageTime { get => _lastMessageTime; set { _lastMessageTime = value; Raise(); } }

    /// <summary>群成员列表</summary>
    public List<UserInfo> Members { get; } = new();

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            _unreadCount = value;
            Raise();
            Raise(nameof(HasUnread));
        }
    }

    public bool HasUnread => _unreadCount > 0;

    public void ClearUnread()
    {
        UnreadCount = 0;
    }

    public GroupSession()
    {
        _createdAt = DateTime.UtcNow;
        _lastMessageTime = DateTime.UtcNow;
    }

    public GroupSession(string id, string name) : this()
    {
        _id = id;
        _name = name;
    }
}
