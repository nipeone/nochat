using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NoChat.App.Logging;

namespace NoChat.App.Firewall;

/// <summary>
/// 在 Windows 上为 NoChat 添加防火墙规则，便于局域网发现（需管理员权限，会触发 UAC）。
/// </summary>
public static class WindowsFirewallHelper
{
    private const string RuleName = "NoChat (LAN Chat)";

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 尝试为当前程序添加防火墙入站/出站规则。会请求管理员权限（UAC）。
    /// </summary>
    /// <param name="resultMessage">成功或失败时的提示文案</param>
    /// <returns>是否已请求执行（不代表用户已同意 UAC）</returns>
    public static bool TryAddFirewallRules(out string resultMessage)
    {
        resultMessage = "";
        if (!IsWindows)
        {
            resultMessage = "当前系统不是 Windows，无需配置防火墙。";
            return false;
        }

        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(exePath))
        {
            resultMessage = "无法获取程序路径。";
            return false;
        }

        // 一条 UAC：用 cmd /c 连续执行入站+出站两条规则
        var ruleName = "NoChat (LAN Chat)";
        var programQuoted = "\"" + exePath.Replace("\"", "\"\"") + "\"";
        var args = $"/c netsh advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program={programQuoted} enable=yes & netsh advfirewall firewall add rule name=\"{ruleName}\" dir=out action=allow program={programQuoted} enable=yes";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var p = Process.Start(psi);
            if (p == null)
            {
                resultMessage = "无法启动防火墙配置（用户可能取消了 UAC）。";
                return false;
            }
            p.WaitForExit(20000);

            resultMessage = "已请求添加防火墙规则。若已通过 UAC 授权，NoChat 现在应能正常进行局域网发现。";
            AppLogger.Info($"[防火墙] 已执行 netsh 添加规则，程序路径: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[防火墙] 添加规则失败", ex);
            resultMessage = $"添加失败: {ex.Message}。请手动在 Windows 防火墙中允许 NoChat。";
            return false;
        }
    }
}
