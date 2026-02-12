using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NoChat.App;

/// <summary>
/// 单例进程：若已有实例在运行，则通知其显示窗口并退出当前进程。
/// </summary>
public static class SingleInstanceGuard
{
    private const string MutexName = "Global\\NoChat.SingleInstance.Mutex";
    private const int NotifyPort = 25560;
    private const int NotifyRetryMs = 300;
    private const int NotifyRetryCount = 5;

    private static Mutex? _mutex;

    /// <summary>
    /// 若已有实例在运行则通知其显示主窗口并返回 false（调用方应退出）；
    /// 否则占用互斥体并返回 true，可继续启动应用。
    /// </summary>
    public static bool TryAcquireOrNotifyExisting()
    {
        bool createdNew;
        try
        {
            _mutex = new Mutex(true, MutexName, out createdNew);
        }
        catch
        {
            return true; // 无法创建 Mutex 时仍允许启动
        }

        if (createdNew)
            return true;

        // 已有实例在运行，通知其显示窗口
        NotifyExistingToShow();
        return false;
    }

    private static void NotifyExistingToShow()
    {
        for (int i = 0; i < NotifyRetryCount; i++)
        {
            try
            {
                using var client = new TcpClient();
                client.ConnectAsync(IPAddress.Loopback, NotifyPort).Wait(500);
                var stream = client.GetStream();
                stream.Write(Encoding.UTF8.GetBytes("show"));
                stream.Flush();
                return;
            }
            catch
            {
                Thread.Sleep(NotifyRetryMs);
            }
        }
    }

    /// <summary>
    /// 在首个实例中启动本地监听，收到连接后执行 onShow（应在 UI 线程中显示主窗口）。
    /// </summary>
    public static void StartNotifyListener(Action onShow)
    {
        var listener = new TcpListener(IPAddress.Loopback, NotifyPort);
        listener.Start();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                while (true)
                {
                    using var client = listener.AcceptTcpClient();
                    try { client.Client?.Shutdown(SocketShutdown.Both); } catch { }
                    Avalonia.Threading.Dispatcher.UIThread.Post(onShow);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception) { /* ignore */ }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        });
    }
}
