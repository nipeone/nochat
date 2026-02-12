using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NoChat.Core.Models;

namespace NoChat.Core.Discovery;

/// <summary>
/// 局域网好友自动发现（UDP 广播 + 多播，兼容 WiFi 与部分路由器对广播的限制）
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private const int DiscoveryPort = 25565;
    private const int MulticastPort = 25569;
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.251");
    private const int BroadcastIntervalMs = 2500;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    /// <summary>由 App 层注入，用于写日志（Core 不引用 App）</summary>
    public static Action<string>? Log;

    private readonly UdpClient _broadcastClient = new();
    private readonly UdpClient _listenClient = new();
    private readonly UdpClient? _multicastSendClient;
    private readonly UdpClient? _multicastListenClient;
    private readonly List<IPEndPoint> _broadcastEndpoints = new();
    private readonly IPEndPoint _multicastEndpoint;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _listenTask;
    private Task? _multicastListenTask;
    private Task? _offlineCheckTask;

    public UserInfo LocalUser { get; }

    public event Action<UserInfo>? UserDiscovered;
    public event Action<string>? UserOffline;

    private readonly Dictionary<string, (UserInfo user, DateTime lastSeen)> _knownUsers = new();
    private readonly TimeSpan _offlineThreshold = TimeSpan.FromSeconds(3);
    private readonly object _sync = new();

    public DiscoveryService(string displayName, int chatPort, int filePort)
    {
        _broadcastClient.EnableBroadcast = true;
        try
        {
            _broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _broadcastClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        }
        catch (Exception ex)
        {
            Log?.Invoke($"广播套接字绑定失败: {ex.Message}");
        }

        _listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        try
        {
            _listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        }
        catch (Exception ex)
        {
            Log?.Invoke($"监听端口 {DiscoveryPort} 绑定失败（可能已被占用）: {ex.Message}");
        }

        var hostName = Environment.MachineName;
        var localIp = GetPreferredLocalIp();
        LocalUser = new UserInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? hostName : displayName,
            MachineName = hostName,
            IpAddress = localIp ?? "0.0.0.0",
            ChatPort = chatPort,
            FilePort = filePort,
            IsOnline = true,
            LastSeen = DateTime.UtcNow
        };

        _broadcastEndpoints.Add(new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
        foreach (var broadcastIp in GetSubnetBroadcastAddresses())
            _broadcastEndpoints.Add(new IPEndPoint(broadcastIp, DiscoveryPort));

        _multicastEndpoint = new IPEndPoint(MulticastAddress, MulticastPort);
        UdpClient? mcSend = null;
        UdpClient? mcListen = null;
        try
        {
            mcSend = new UdpClient();
            mcSend.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mcSend.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            mcSend.JoinMulticastGroup(MulticastAddress);
            _multicastSendClient = mcSend;

            mcListen = new UdpClient();
            mcListen.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mcListen.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
            mcListen.JoinMulticastGroup(MulticastAddress);
            _multicastListenClient = mcListen;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"多播初始化失败（将仅使用广播）: {ex.Message}");
            mcSend?.Dispose();
            mcListen?.Dispose();
            _multicastSendClient = null;
            _multicastListenClient = null;
        }

        Log?.Invoke($"发现服务初始化: 本机 IP={LocalUser.IpAddress}, 广播端口={DiscoveryPort}, 多播端口={MulticastPort}, 聊天={chatPort}, 文件={filePort}. 若无法发现对方请检查: 1) 同一局域网 2) 防火墙允许 UDP {DiscoveryPort}/{MulticastPort}/{chatPort}/{filePort}");
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _broadcastTask = BroadcastLoop(token);
        _listenTask = ListenLoop(token);
        if (_multicastListenClient != null)
            _multicastListenTask = MulticastListenLoop(token);
        _offlineCheckTask = CheckOfflineLoop(token);
        Log?.Invoke("发现服务已启动（广播+多播），正在发送与监听…");
    }

    public void Stop()
    {
        try
        {
            _listenClient.Client?.Close();
            _broadcastClient.Client?.Close();
            _multicastSendClient?.Client?.Close();
            _multicastListenClient?.Client?.Close();
        }
        catch { /* ignore */ }
        _cts?.Cancel();
    }

    private async Task BroadcastLoop(CancellationToken ct)
    {
        var packet = new DiscoveryPacket
        {
            UserId = LocalUser.Id,
            DisplayName = LocalUser.DisplayName,
            MachineName = LocalUser.MachineName,
            IpAddress = LocalUser.IpAddress,
            ChatPort = LocalUser.ChatPort,
            FilePort = LocalUser.FilePort,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var json = JsonSerializer.Serialize(packet, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        while (!ct.IsCancellationRequested)
        {
            foreach (var endpoint in _broadcastEndpoints)
            {
                try { await _broadcastClient.SendAsync(bytes, endpoint); } catch { /* ignore */ }
            }
            if (_multicastSendClient != null)
            {
                try { await _multicastSendClient.SendAsync(bytes, _multicastEndpoint); } catch { /* ignore */ }
            }
            await Task.Delay(BroadcastIntervalMs, ct);
        }
    }

    private async Task MulticastListenLoop(CancellationToken ct)
    {
        if (_multicastListenClient == null) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _multicastListenClient.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                ProcessReceivedPacket(json, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log?.Invoke($"多播接收异常: {ex.Message}"); }
        }
    }

    private void ProcessReceivedPacket(string json, IPEndPoint? remoteEndPoint)
    {
        var packet = JsonSerializer.Deserialize<DiscoveryPacket>(json, JsonOptions);
        if (packet == null || string.IsNullOrEmpty(packet.UserId) || packet.UserId == LocalUser.Id)
            return;

        var remoteIp = remoteEndPoint?.Address?.ToString() ?? packet.IpAddress;
        if (string.IsNullOrEmpty(remoteIp)) remoteIp = packet.IpAddress;
        var user = new UserInfo
        {
            Id = packet.UserId,
            DisplayName = packet.DisplayName ?? "",
            MachineName = packet.MachineName ?? "",
            IpAddress = remoteIp,
            ChatPort = packet.ChatPort,
            FilePort = packet.FilePort,
            IsOnline = true,
            LastSeen = DateTime.UtcNow
        };

        lock (_sync)
            _knownUsers[user.Id] = (user, user.LastSeen);
        UserDiscovered?.Invoke(user);
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listenClient.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                ProcessReceivedPacket(json, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log?.Invoke($"广播接收异常: {ex.Message}"); }
        }
    }

    private async Task CheckOfflineLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            List<string>? toRemove = null;
            lock (_sync)
            {
                var now = DateTime.UtcNow;
                foreach (var kv in _knownUsers.ToList())
                {
                    if (now - kv.Value.lastSeen > _offlineThreshold)
                    {
                        toRemove ??= new List<string>();
                        toRemove.Add(kv.Key);
                    }
                }
                if (toRemove != null)
                    foreach (var id in toRemove)
                        _knownUsers.Remove(id);
            }
            if (toRemove != null)
                foreach (var id in toRemove)
                    UserOffline?.Invoke(id);
        }
    }

    public IReadOnlyList<UserInfo> GetKnownUsers()
    {
        lock (_sync)
            return _knownUsers.Values.Select(x => x.user).ToList();
    }

    private static string? GetPreferredLocalIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                        return unicast.Address.ToString();
                }
            }
        }
        catch { /* ignore */ }

        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString();
        }
        catch { return null; }
    }

    private static List<IPAddress> GetSubnetBroadcastAddresses()
    {
        var list = new List<IPAddress>();
        var seen = new HashSet<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                        continue;

                    var prefixLength = unicast.PrefixLength;
                    if (prefixLength <= 0 || prefixLength >= 32) continue;

                    var broadcast = GetBroadcastAddress(unicast.Address, prefixLength);
                    if (broadcast != null && seen.Add(broadcast.ToString()))
                        list.Add(broadcast);
                }
            }
        }
        catch { /* ignore */ }
        return list;
    }

    private static IPAddress? GetBroadcastAddress(IPAddress address, int prefixLength)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return null;
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return null;
        uint mask = prefixLength >= 32 ? 0xFFFFFFFF : (uint)((0xFFFFFFFFu << (32 - prefixLength)) & 0xFFFFFFFFu);
        uint addrInt = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        uint broadcastInt = addrInt | ~mask;
        var broadcastBytes = new byte[]
        {
            (byte)(broadcastInt >> 24),
            (byte)(broadcastInt >> 16),
            (byte)(broadcastInt >> 8),
            (byte)broadcastInt
        };
        return new IPAddress(broadcastBytes);
    }

    public void Dispose()
    {
        Stop();
        _broadcastClient.Dispose();
        _listenClient.Dispose();
        _multicastSendClient?.Dispose();
        _multicastListenClient?.Dispose();
    }
}
