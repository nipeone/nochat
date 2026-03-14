using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoChat.Core.Models;

namespace NoChat.App.ViewModels;

/// <summary>
/// 群组列表项，包装 GroupSession 并支持显示与交互。
/// </summary>
public sealed class GroupItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public GroupSession Group { get; }

    public string GroupName => Group.Name;
    public int MemberCount => Group.Members.Count;
    public int UnreadCount => Group.UnreadCount;
    public bool HasUnread => Group.HasUnread;

    public GroupItemViewModel(GroupSession group)
    {
        Group = group;
        Group.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GroupSession.Name))
                Raise(nameof(GroupName));
            if (e.PropertyName == nameof(GroupSession.UnreadCount))
                Raise(nameof(UnreadCount));
            if (e.PropertyName == nameof(GroupSession.HasUnread))
                Raise(nameof(HasUnread));
        };
    }

    public void ClearUnread() => Group.ClearUnread();
}
