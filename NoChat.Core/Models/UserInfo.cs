using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoChat.Core.Models;

/// <summary>
/// 局域网内用户信息（用于发现与显示）
/// </summary>
public sealed class UserInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = "";
    private string _displayName = "";
    private string _machineName = "";
    private string _ipAddress = "";
    private int _chatPort;
    private int _filePort;
    private bool _isOnline = true;
    private DateTime _lastSeen;

    public string Id { get => _id; set { _id = value ?? ""; Raise(); } }
    public string DisplayName { get => _displayName; set { _displayName = value ?? ""; Raise(); } }
    public string MachineName { get => _machineName; set { _machineName = value ?? ""; Raise(); } }
    public string IpAddress { get => _ipAddress; set { _ipAddress = value ?? ""; Raise(); } }
    public int ChatPort { get => _chatPort; set { _chatPort = value; Raise(); } }
    public int FilePort { get => _filePort; set { _filePort = value; Raise(); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; Raise(); } }
    public DateTime LastSeen { get => _lastSeen; set { _lastSeen = value; Raise(); } }
}
