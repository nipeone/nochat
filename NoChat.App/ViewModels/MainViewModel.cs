using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using NoChat.App.Logging;
using NoChat.Core.Chat;
using NoChat.Core.Discovery;
using NoChat.Core.FileTransfer;
using NoChat.Core.Models;

namespace NoChat.App.ViewModels;

public sealed class MainViewModel : IDisposable, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly DiscoveryService _discovery;
    private readonly ChatService _chat;
    private readonly FileTransferService _fileTransfer;
    private string? _currentChatUserId;
    private string? _currentChatGroupId;
    private readonly Dictionary<string, List<ChatMessage>> _privateMessages = new();
    private readonly Dictionary<string, List<ChatMessage>> _groupMessages = new();
    private string _saveFolder = "";

    public ObservableCollection<UserInfo> Friends { get; } = new();
    public ObservableCollection<MessageDisplayItem> CurrentMessages { get; } = new();
    public ObservableCollection<object> Sessions { get; } = new(); // 私聊 UserInfo 或 GroupSession

    public string MyName => _discovery.LocalUser.DisplayName;

    private string _inputText = "";
    public string InputText { get => _inputText; set { _inputText = value ?? ""; RaisePropertyChanged(); } }

    private string _displayName = "";
    public string DisplayName { get => _displayName; set { _displayName = value ?? ""; RaisePropertyChanged(); } }

    public event Action<string>? OnError;
    public event Action<string, string, string, long, Stream>? OnReceiveFileRequest;
    public event Action<string, string, string, string, bool, Stream>? OnReceiveFolderRequest;
    private Func<string, string, string, long, Task<bool>>? _fileRequestHandler;
    public void SetFileRequestHandler(Func<string, string, string, long, Task<bool>> handler) => _fileRequestHandler = handler;

    public MainViewModel()
    {
        DiscoveryService.Log = msg => AppLogger.Info("[发现] " + msg);

        var chatPort = 25566;
        var filePort = 25567;
        DisplayName = Environment.MachineName;
        _discovery = new DiscoveryService(DisplayName, chatPort, filePort);
        _chat = new ChatService(
            _discovery.LocalUser.Id,
            _discovery.LocalUser.DisplayName,
            chatPort);
        _fileTransfer = new FileTransferService(filePort, OnFileRequest, OnFileReceived, OnFolderReceived);

        _discovery.UserDiscovered += OnUserDiscovered;
        _discovery.UserOffline += OnUserOffline;
        _chat.SetMessageHandler(OnMessageReceived);
        _chat.SetRecallHandler(OnRecallReceived);

        _discovery.Start();
        _chat.Start();
        _fileTransfer.Start();
    }

    public void SetSaveFolder(string path) => _saveFolder = path;

    private static List<ChatMessage> GetOrAddList(Dictionary<string, List<ChatMessage>> dict, string key)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = new List<ChatMessage>();
            dict[key] = list;
        }
        return list;
    }

    private void OnUserDiscovered(UserInfo user)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Friends.FirstOrDefault(f =>
                f.IpAddress == user.IpAddress && f.ChatPort == user.ChatPort);
            if (existing != null)
            {
                var oldId = existing.Id;
                existing.Id = user.Id;
                existing.DisplayName = user.DisplayName;
                existing.MachineName = user.MachineName;
                existing.IpAddress = user.IpAddress;
                existing.ChatPort = user.ChatPort;
                existing.FilePort = user.FilePort;
                existing.IsOnline = true;
                existing.LastSeen = user.LastSeen;
                if (oldId != user.Id)
                {
                    if (_currentChatUserId == oldId)
                        _currentChatUserId = user.Id;
                    if (_privateMessages.TryGetValue(oldId, out var oldList))
                    {
                        if (!_privateMessages.ContainsKey(user.Id))
                            _privateMessages[user.Id] = new List<ChatMessage>();
                        _privateMessages[user.Id].AddRange(oldList);
                        _privateMessages.Remove(oldId);
                    }
                }
            }
            else
            {
                Friends.Add(user);
            }
            _ = _chat.EnsureConnectionAsync(user);
        });
    }

    private void OnUserOffline(string userId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var u = Friends.FirstOrDefault(f => f.Id == userId);
            if (u != null)
            {
                u.IsOnline = false;
                u.LastSeen = DateTime.UtcNow;
            }
        });
    }

    private Task OnMessageReceived(string senderId, string senderName, ChatMessage msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var list = msg.IsGroup
                ? GetOrAddList(_groupMessages, msg.SessionId ?? "")
                : GetOrAddList(_privateMessages, msg.SessionId ?? senderId);
            list.Add(msg);
            if ((msg.IsGroup && _currentChatGroupId == msg.SessionId) ||
                (!msg.IsGroup && _currentChatUserId == senderId))
            {
                CurrentMessages.Add(new MessageDisplayItem(msg, false));
            }
        });
        return Task.CompletedTask;
    }

    private Task OnRecallReceived(string fromUserId, string messageId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var list in _privateMessages.Values.Concat(_groupMessages.Values))
            {
                var m = list.FirstOrDefault(x => x.Id == messageId);
                if (m != null) { m.IsRecalled = true; break; }
            }
            var idx = -1;
            for (var i = 0; i < CurrentMessages.Count; i++)
            {
                if (CurrentMessages[i].Message.Id == messageId) { idx = i; break; }
            }
            if (idx >= 0)
            {
                var item = CurrentMessages[idx];
                item.Message.IsRecalled = true;
                CurrentMessages.RemoveAt(idx);
                CurrentMessages.Insert(idx, new MessageDisplayItem(item.Message, item.IsFromMe));
            }
        });
        return Task.CompletedTask;
    }

    private async Task<bool> OnFileRequest(string senderId, string senderName, string fileName, long size)
    {
        return await (_fileRequestHandler?.Invoke(senderId, senderName, fileName, size) ?? Task.FromResult(false));
    }

    private Task OnFileReceived(string senderId, string senderName, string fileName, long size, Stream stream)
    {
        OnReceiveFileRequest?.Invoke(senderId, senderName, fileName, size, stream);
        return Task.CompletedTask;
    }

    private Task OnFolderReceived(string senderId, string senderName, string path, string folderName, bool isFolder, Stream stream)
    {
        if (stream == Stream.Null)
            return Task.CompletedTask;
        OnReceiveFolderRequest?.Invoke(senderId, senderName, path, folderName, isFolder, stream);
        return Task.CompletedTask;
    }

    public void SelectPrivateChat(UserInfo user)
    {
        _currentChatUserId = user.Id;
        _currentChatGroupId = null;
        CurrentMessages.Clear();
        var list = _privateMessages.TryGetValue(user.Id, out var lst) ? lst : null;
        if (list != null)
            foreach (var m in list)
                CurrentMessages.Add(new MessageDisplayItem(m, m.SenderId == _discovery.LocalUser.Id));
        _ = _chat.EnsureConnectionAsync(user);
    }

    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;
        var text = InputText;
        InputText = ""; // 清空输入并通知 UI
        if (_currentChatUserId != null)
        {
            var user = Friends.FirstOrDefault(f => f.Id == _currentChatUserId);
            if (user == null) return;
            try
            {
                await _chat.SendMessageAsync(user, text);
                var msg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SenderId = _discovery.LocalUser.Id,
                    SenderName = MyName,
                    Content = text,
                    SentAt = DateTime.UtcNow,
                    SessionId = user.Id,
                    IsGroup = false
                };
                GetOrAddList(_privateMessages, user.Id).Add(msg);
                CurrentMessages.Add(new MessageDisplayItem(msg, true));
            }
            catch (Exception ex) { OnError?.Invoke(ex.Message); }
        }
    }

    public async Task RecallMessageAsync(MessageDisplayItem item)
    {
        var msg = item.Message;
        if (msg.SenderId != _discovery.LocalUser.Id) return;
        if (msg.IsGroup) return;
        var user = Friends.FirstOrDefault(f => f.Id == _currentChatUserId);
        if (user == null) return;
        try
        {
            await _chat.RecallMessageAsync(user, msg.Id);
            msg.IsRecalled = true;
            CurrentMessages.Remove(item);
            CurrentMessages.Add(item);
        }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
    }

    public async Task SendFileAsync(UserInfo user, string filePath)
    {
        try
        {
            await _fileTransfer.SendFileAsync(user, filePath, _discovery.LocalUser.Id, MyName);
            var fileName = System.IO.Path.GetFileName(filePath);
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                SenderId = _discovery.LocalUser.Id,
                SenderName = MyName,
                Content = $"[文件] {fileName}",
                SentAt = DateTime.UtcNow,
                SessionId = user.Id,
                IsGroup = false
            };
            GetOrAddList(_privateMessages, user.Id).Add(msg);
            if (_currentChatUserId == user.Id)
                CurrentMessages.Add(new MessageDisplayItem(msg, true));
        }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
    }

    public async Task SendFolderAsync(UserInfo user, string folderPath)
    {
        try
        {
            await _fileTransfer.SendFolderAsync(user, folderPath, _discovery.LocalUser.Id, MyName);
            var folderName = System.IO.Path.GetFileName(folderPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                SenderId = _discovery.LocalUser.Id,
                SenderName = MyName,
                Content = $"[文件夹] {folderName}",
                SentAt = DateTime.UtcNow,
                SessionId = user.Id,
                IsGroup = false
            };
            GetOrAddList(_privateMessages, user.Id).Add(msg);
            if (_currentChatUserId == user.Id)
                CurrentMessages.Add(new MessageDisplayItem(msg, true));
        }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
    }

    public void AddReceivedFileMessage(string senderId, string senderName, string fileName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                SenderId = senderId,
                SenderName = senderName,
                Content = $"[收到文件] {fileName}",
                SentAt = DateTime.UtcNow,
                SessionId = senderId,
                IsGroup = false
            };
            GetOrAddList(_privateMessages, senderId).Add(msg);
            if (_currentChatUserId == senderId)
                CurrentMessages.Add(new MessageDisplayItem(msg, false));
        });
    }

    public void AddReceivedFolderMessage(string senderId, string senderName, string folderName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var content = string.IsNullOrEmpty(folderName) ? "[收到文件夹]" : $"[收到文件夹] {folderName}";
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                SenderId = senderId,
                SenderName = senderName,
                Content = content,
                SentAt = DateTime.UtcNow,
                SessionId = senderId,
                IsGroup = false
            };
            GetOrAddList(_privateMessages, senderId).Add(msg);
            if (_currentChatUserId == senderId)
                CurrentMessages.Add(new MessageDisplayItem(msg, false));
        });
    }

    public void Dispose()
    {
        _discovery.Dispose();
        _chat.Dispose();
        _fileTransfer.Dispose();
    }
}
