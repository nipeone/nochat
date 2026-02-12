using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NoChat.Core.Models;

namespace NoChat.Core.Discovery;

/// <summary>
/// 局域网好友自动发现与上线广播（UDP 子网定向广播 + 全局广播，兼容 WiFi）
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private const int DiscoveryPort = 25565;
    private const int BroadcastIntervalMs = 3000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly UdpClient _broadcastClient = new();
    private readonly UdpClient _listenClient = new();
    private readonly List<IPEndPoint> _broadcastEndpoints = new();
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _listenTask;
    private Task? _offlineCheckTask;

    public UserInfo LocalUser { get; }

    public event Action<UserInfo>? UserDiscovered;
    public event Action<string>? UserOffline;

    private readonly Dictionary<string, (UserInfo user, DateTime lastSeen)> _knownUsers = new();
    private readonly TimeSpan _offlineThreshold = TimeSpan.FromSeconds(12);
    private readonly object _sync = new();

    public DiscoveryService(string displayName, int chatPort, int filePort)
    {
        _broadcastClient.EnableBroadcast = true;
        _listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

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
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _broadcastTask = BroadcastLoop(token);
        _listenTask = ListenLoop(token);
        _offlineCheckTask = CheckOfflineLoop(token);
    }

    public void Stop()
    {
        try
        {
            _listenClient.Client?.Close();
            _broadcastClient.Client?.Close();
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
            await Task.Delay(BroadcastIntervalMs, ct);
        }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listenClient.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var packet = JsonSerializer.Deserialize<DiscoveryPacket>(json, JsonOptions);
                if (packet == null || packet.UserId == LocalUser.Id) continue;

                var remoteIp = result.RemoteEndPoint?.Address?.ToString() ?? packet.IpAddress;
                var user = new UserInfo
                {
                    Id = packet.UserId,
                    DisplayName = packet.DisplayName,
                    MachineName = packet.MachineName,
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
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* ignore */ }
        }
    }

    private async Task CheckOfflineLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct);
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
    }
}
