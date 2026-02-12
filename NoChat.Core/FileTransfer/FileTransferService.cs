using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NoChat.Core.Models;

namespace NoChat.Core.FileTransfer;

public sealed class FileTransferService : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;
    private readonly int _listenPort;
    private readonly Func<string, string, string, long, Stream, Task>? _onReceiveFile;
    private readonly Func<string, string, string, bool, Stream, Task>? _onReceiveFolder;

    public FileTransferService(
        int listenPort,
        Func<string, string, string, long, Stream, Task>? onReceiveFile = null,
        Func<string, string, string, bool, Stream, Task>? onReceiveFolder = null)
    {
        _listenPort = listenPort;
        _onReceiveFile = onReceiveFile;
        _onReceiveFolder = onReceiveFolder;
        _listener = new TcpListener(IPAddress.Any, listenPort);
    }

    public void Start()
    {
        _listener.Start();
        _acceptTask = AcceptLoop();
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    private async Task AcceptLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleTransfer(client);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* ignore */ }
        }
    }

    private async Task HandleTransfer(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            var senderId = reader.ReadString();
            var senderName = reader.ReadString();
            var isFolder = reader.ReadBoolean();
            var name = reader.ReadString();

            if (isFolder)
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var fileName = reader.ReadString();
                    var fileSize = reader.ReadInt64();
                    await ReadStreamToAction(stream, fileSize, async (s) =>
                    {
                        if (_onReceiveFolder != null)
                            await _onReceiveFolder(senderId, senderName, fileName, true, s);
                    });
                }
                if (_onReceiveFolder != null)
                    await _onReceiveFolder(senderId, senderName, name, true, Stream.Null);
            }
            else
            {
                var fileSize = reader.ReadInt64();
                if (_onReceiveFile != null)
                {
                    await ReadStreamToAction(stream, fileSize, async (s) =>
                    {
                        await _onReceiveFile(senderId, senderName, name, fileSize, s);
                    });
                }
                else
                {
                    await SkipStreamAsync(stream, fileSize);
                }
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task SkipStreamAsync(Stream stream, long length)
    {
        var buf = new byte[81920];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(remaining, buf.Length);
            var read = await stream.ReadAsync(buf.AsMemory(0, toRead));
            if (read == 0) break;
            remaining -= read;
        }
    }

    private static async Task ReadStreamToAction(Stream stream, long length, Func<Stream, Task> action)
    {
        var ms = new MemoryStream((int)length);
        var remaining = length;
        var buf = new byte[81920];
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(remaining, buf.Length);
            var read = await stream.ReadAsync(buf.AsMemory(0, toRead));
            if (read == 0) break;
            ms.Write(buf, 0, read);
            remaining -= read;
        }
        ms.Position = 0;
        await action(ms);
    }

    /// <summary>
    /// 发送文件给好友
    /// </summary>
    public async Task SendFileAsync(UserInfo toUser, string filePath, string senderId, string senderName, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        using var client = new TcpClient();
        await client.ConnectAsync(toUser.IpAddress, toUser.FilePort, ct);
        using var stream = client.GetStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(senderId);
        writer.Write(senderName);
        writer.Write(false); // isFolder
        writer.Write(fileName);
        writer.Write(fileSize);
        await using (var fs = File.OpenRead(filePath))
            await fs.CopyToAsync(stream, ct);
    }

    /// <summary>
    /// 发送文件夹给好友（先发送文件夹内文件列表和每个文件内容）
    /// </summary>
    public async Task SendFolderAsync(UserInfo toUser, string folderPath, string senderId, string senderName, CancellationToken ct = default)
    {
        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).ToList();
        using var client = new TcpClient();
        await client.ConnectAsync(toUser.IpAddress, toUser.FilePort, ct);
        using var stream = client.GetStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(senderId);
        writer.Write(senderName);
        writer.Write(true); // isFolder
        writer.Write(folderName);
        writer.Write(files.Count);
        var baseLen = folderPath.Length + 1;
        foreach (var fullPath in files)
        {
            var relativePath = fullPath.Substring(baseLen);
            writer.Write(relativePath);
            var fileSize = new FileInfo(fullPath).Length;
            writer.Write(fileSize);
            await using (var fs = File.OpenRead(fullPath))
                await fs.CopyToAsync(stream, ct);
        }
    }

    public void Dispose()
    {
        Stop();
        _listener.Server?.Dispose();
        _cts.Dispose();
    }
}
