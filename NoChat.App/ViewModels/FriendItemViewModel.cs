using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoChat.App.Settings;
using NoChat.Core.Models;

namespace NoChat.App.ViewModels;

/// <summary>
/// 好友列表项，包装 UserInfo 并支持备注显示与编辑。
/// </summary>
public sealed class FriendItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public UserInfo UserInfo { get; }

    public FriendItemViewModel(UserInfo userInfo)
    {
        UserInfo = userInfo;
        UserInfo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UserInfo.DisplayName) || e.PropertyName == nameof(UserInfo.Id))
                Raise(nameof(DisplayNameForList));
        };
    }

    /// <summary>列表显示名：备注优先，否则为对方 DisplayName。</summary>
    public string DisplayNameForList => FriendRemarks.GetDisplayName(UserInfo.Id, UserInfo.DisplayName);

    public string MachineName => UserInfo.MachineName;
    public bool IsOnline => UserInfo.IsOnline;

    /// <summary>设置备注并刷新显示。</summary>
    public void SetRemark(string? remark)
    {
        FriendRemarks.Set(UserInfo.Id, remark);
        Raise(nameof(DisplayNameForList));
    }

    public string? CurrentRemark => FriendRemarks.Get(UserInfo.Id);
}
