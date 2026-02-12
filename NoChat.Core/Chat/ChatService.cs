using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NoChat.Core.Models;

namespace NoChat.Core.Chat;

public sealed class ChatService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, TcpClient> _outgoingConnections = new();
    private readonly ConcurrentDictionary<string, (string name, StreamWriter writer)> _connectionWriters = new();
    private readonly object _sync = new();
    private Task? _acceptTask;
    private Func<string, string, ChatMessage, Task>? _onMessage;
    private Func<string, string, Task>? _onRecall;

    public int ListenPort { get; }
    public string LocalUserId { get; }
    public string LocalDisplayName { get; }

    public ChatService(string localUserId, string localDisplayName, int listenPort)
    {
        LocalUserId = localUserId;
        LocalDisplayName = localDisplayName;
        ListenPort = listenPort;
        _listener = new TcpListener(IPAddress.Any, listenPort);
    }

    public void SetMessageHandler(Func<string, string, ChatMessage, Task> onMessage)
        => _onMessage = onMessage;

    public void SetRecallHandler(Func<string, string, Task> onRecall)
        => _onRecall = onRecall;

    public void Start()
    {
        _listener?.Start();
        _acceptTask = AcceptLoop();
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener?.Stop();
        foreach (var c in _outgoingConnections.Values)
            try { c.Close(); } catch { }
        _outgoingConnections.Clear();
        _connectionWriters.Clear();
    }

    private async Task AcceptLoop()
    {
        if (_listener == null) return;
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleIncomingConnection(client);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* ignore */ }
        }
    }

    private async Task HandleIncomingConnection(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            string? remoteUserId = null;
            string? remoteName = null;

            while (!_cts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token);
                if (line == null) break;
                var packet = JsonSerializer.Deserialize<ChatPacket>(line, JsonOptions);
                if (packet == null) continue;

                switch (packet.Type)
                {
                    case ChatPacketType.Hello:
                        remoteUserId = packet.UserId;
                        remoteName = packet.DisplayName ?? "";
                        break;
                    case ChatPacketType.Message when remoteUserId != null && packet.MessageId != null:
                        var msg = new ChatMessage
                        {
                            Id = packet.MessageId,
                            SenderId = remoteUserId,
                            SenderName = remoteName ?? "",
                            Content = packet.Content ?? "",
                            SentAt = packet.SentAt.HasValue
                                ? DateTimeOffset.FromUnixTimeMilliseconds(packet.SentAt.Value).UtcDateTime
                                : DateTime.UtcNow,
                            IsRecalled = false,
                            SessionId = packet.SessionId,
                            IsGroup = packet.IsGroup
                        };
                        if (_onMessage != null)
                            await _onMessage(remoteUserId, remoteName ?? "", msg);
                        break;
                    case ChatPacketType.Recall when remoteUserId != null && packet.MessageId != null:
                        if (_onRecall != null)
                            await _onRecall(remoteUserId, packet.MessageId);
                        break;
                }
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// 发送私聊消息
    /// </summary>
    public async Task SendMessageAsync(UserInfo toUser, string content, CancellationToken ct = default)
    {
        var msgId = Guid.NewGuid().ToString("N");
        var packet = new ChatPacket
        {
            Type = ChatPacketType.Hello,
            UserId = LocalUserId,
            DisplayName = LocalDisplayName
        };
        var writer = await GetOrCreateConnection(toUser, ct);
        await SendPacketAsync(writer, new ChatPacket
        {
            Type = ChatPacketType.Message,
            UserId = LocalUserId,
            MessageId = msgId,
            Content = content,
            SessionId = toUser.Id,
            IsGroup = false,
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, ct);
    }

    /// <summary>
    /// 发送群聊消息（向多个成员发送同一条消息）
    /// </summary>
    public async Task SendGroupMessageAsync(string groupId, IEnumerable<UserInfo> members, string content, CancellationToken ct = default)
    {
        var msgId = Guid.NewGuid().ToString("N");
        var packet = new ChatPacket
        {
            Type = ChatPacketType.Message,
            UserId = LocalUserId,
            MessageId = msgId,
            Content = content,
            SessionId = groupId,
            IsGroup = true,
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        foreach (var user in members)
        {
            try
            {
                var writer = await GetOrCreateConnection(user, ct);
                await SendPacketAsync(writer, packet, ct);
            }
            catch
            {
                // skip failed member
            }
        }
    }

    /// <summary>
    /// 撤回消息（通知对方）
    /// </summary>
    public async Task RecallMessageAsync(UserInfo toUser, string messageId, CancellationToken ct = default)
    {
        var writer = await GetOrCreateConnection(toUser, ct);
        await SendPacketAsync(writer, new ChatPacket
        {
            Type = ChatPacketType.Recall,
            MessageId = messageId
        }, ct);
    }

    private async Task<StreamWriter> GetOrCreateConnection(UserInfo toUser, CancellationToken ct)
    {
        if (_connectionWriters.TryGetValue(toUser.Id, out var existing))
            return existing.writer;

        var key = toUser.Id;
        var client = new TcpClient();
        await client.ConnectAsync(toUser.IpAddress, toUser.ChatPort, ct);
        _outgoingConnections[key] = client;
        var stream = client.GetStream();
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        var reader = new StreamReader(stream, Encoding.UTF8);

        await SendPacketAsync(writer, new ChatPacket
        {
            Type = ChatPacketType.Hello,
            UserId = LocalUserId,
            DisplayName = LocalDisplayName
        }, ct);

        _connectionWriters[key] = (toUser.DisplayName, writer);
        _ = ReadIncomingFromOutgoing(client, reader, key);
        return writer;
    }

    private async Task ReadIncomingFromOutgoing(TcpClient client, StreamReader reader, string remoteId)
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token);
                if (line == null) break;
                var packet = JsonSerializer.Deserialize<ChatPacket>(line, JsonOptions);
                if (packet?.Type == ChatPacketType.Message && packet.MessageId != null)
                {
                    var msg = new ChatMessage
                    {
                        Id = packet.MessageId,
                        SenderId = remoteId,
                        SenderName = packet.DisplayName ?? "",
                        Content = packet.Content ?? "",
                        SentAt = packet.SentAt.HasValue
                            ? DateTimeOffset.FromUnixTimeMilliseconds(packet.SentAt.Value).UtcDateTime
                            : DateTime.UtcNow,
                        SessionId = packet.SessionId,
                        IsGroup = packet.IsGroup
                    };
                    if (_onMessage != null)
                        await _onMessage(remoteId, msg.SenderName, msg);
                }
                if (packet?.Type == ChatPacketType.Recall && packet.MessageId != null && _onRecall != null)
                    await _onRecall(remoteId, packet.MessageId);
            }
        }
        catch { /* disconnect */ }
        finally
        {
            _outgoingConnections.TryRemove(remoteId, out _);
            _connectionWriters.TryRemove(remoteId, out _);
            try { client.Dispose(); } catch { }
        }
    }

    private static async Task SendPacketAsync(StreamWriter writer, ChatPacket packet, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(packet, JsonOptions) + "\n";
        await writer.WriteAsync(line.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
