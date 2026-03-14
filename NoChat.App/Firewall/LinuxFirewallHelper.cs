using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NoChat.App.Logging;

namespace NoChat.App.Firewall;

/// <summary>
/// 在 Linux 上为 NoChat 添加防火墙规则，便于局域网发现（需要 sudo 权限）。
/// 支持 ufw (Ubuntu, Debian, Mint) 和 firewalld (Fedora, RHEL, CentOS)。
/// </summary>
public static class LinuxFirewallHelper
{
    // NoChat 使用的端口
    private const int DiscoveryPort = 25565;
    private const int MulticastPort = 25569;
    private const int ChatPort = 25566;
    private const int FileTransferPort = 25567;

    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// 尝试为 NoChat 添加防火墙规则（需要 sudo 权限）。
    /// </summary>
    /// <param name="resultMessage">成功或失败时的提示文案</param>
    /// <returns>是否成功添加规则</returns>
    public static bool TryAddFirewallRules(out string resultMessage)
    {
        resultMessage = "";

        if (!IsLinux)
        {
            resultMessage = "当前系统不是 Linux";
            return false;
        }

        try
        {
            // 检测防火墙类型
            var firewallType = DetectFirewallType();

            return firewallType switch
            {
                FirewallType.Ufw => AddUfwRules(out resultMessage),
                FirewallType.Firewalld => AddFirewalldRules(out resultMessage),
                FirewallType.None => AddIptablesRules(out resultMessage),
                _ => throw new InvalidOperationException($"不支持的防火墙类型: {firewallType}")
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("[防火墙] 添加规则失败", ex);
            resultMessage = $"添加失败: {ex.Message}";
            return false;
        }
    }

    private enum FirewallType
    {
        None,
        Ufw,
        Firewalld
    }

    private static FirewallType DetectFirewallType()
    {
        // 检测 ufw
        var ufwResult = RunCommand("which", "ufw");
        if (ufwResult.ExitCode == 0)
        {
            var statusResult = RunCommand("ufw", "status");
            if (statusResult.ExitCode == 0 && statusResult.Output.Contains("Status: active"))
            {
                AppLogger.Info("[防火墙] 检测到 ufw 防火墙");
                return FirewallType.Ufw;
            }
        }

        // 检测 firewalld
        var firewalldResult = RunCommand("which", "firewall-cmd");
        if (firewalldResult.ExitCode == 0)
        {
            var statusResult = RunCommand("firewall-cmd", "--state");
            if (statusResult.ExitCode == 0 && statusResult.Output.Contains("running"))
            {
                AppLogger.Info("[防火墙] 检测到 firewalld 防火墙");
                return FirewallType.Firewalld;
            }
        }

        AppLogger.Info("[防火墙] 未检测到活动的防火墙，使用 iptables");
        return FirewallType.None;
    }

    private static bool AddUfwRules(out string resultMessage)
    {
        // 尝试永久开放端口（需要 sudo 权限）
        var ports = $"{DiscoveryPort},{MulticastPort},{ChatPort},{FileTransferPort}";

        // ufw allow 命令 - 尝试 pkexec
        var allowResult = RunCommand("pkexec", $"ufw allow {DiscoveryPort}/{MulticastPort}/{ChatPort}/{FileTransferPort}");

        if (allowResult.ExitCode == 0)
        {
            AppLogger.Info($"[防火墙] ufw 已开放端口");
            resultMessage = "已通过 ufw 开放端口。请确保在同一局域网内可以发现对方。";
            return true;
        }

        // pkexec 失败，提供手动命令
        AppLogger.Error($"[防火墙] ufw 命令失败: {allowResult.Output}");
        resultMessage = "无法自动添加防火墙规则。\n\n请打开终端运行以下命令：\n" +
            $"  sudo ufw allow {DiscoveryPort}/udp\n" +
            $"  sudo ufw allow {MulticastPort}/udp\n" +
            $"  sudo ufw allow {ChatPort}/tcp\n" +
            $"  sudo ufw allow {FileTransferPort}/tcp\n\n" +
            "运行后重启 NoChat 即可发现其他设备。";
        return false;
    }

    private static bool AddFirewalldRules(out string resultMessage)
    {
        var ports = $"{DiscoveryPort}/udp,{MulticastPort}/udp,{ChatPort}/tcp,{FileTransferPort}/tcp";

        var result = RunCommand("pkexec", $"firewall-cmd --permanent --add-port={ports}");
        if (result.ExitCode != 0)
        {
            result = RunCommand("sudo", $"firewall-cmd --permanent --add-port={ports}");
        }

        if (result.ExitCode == 0)
        {
            // 重新加载防火墙规则
            RunCommand("pkexec", "firewall-cmd --reload");

            AppLogger.Info($"[防火墙] firewalld 已开放端口: {ports}");
            resultMessage = "已通过 firewalld 开放端口 " + ports.Replace(",", ", ") + "。请确保在同一局域网内可以发现对方。";
            return true;
        }

        resultMessage = "无法自动添加防火墙规则。\n\n请打开终端运行以下命令：\n" +
            $"  sudo firewall-cmd --permanent --add-port={DiscoveryPort}/udp\n" +
            $"  sudo firewall-cmd --permanent --add-port={MulticastPort}/udp\n" +
            $"  sudo firewall-cmd --permanent --add-port={ChatPort}/tcp\n" +
            $"  sudo firewall-cmd --permanent --add-port={FileTransferPort}/tcp\n" +
            "  sudo firewall-cmd --reload\n\n" +
            "运行后重启 NoChat 即可发现其他设备。";
        return false;
    }

    private static bool AddIptablesRules(out string resultMessage)
    {
        var iptablesCmds = new[]
        {
            $"iptables -I INPUT -p udp --dport {DiscoveryPort} -j ACCEPT",
            $"iptables -I INPUT -p udp --dport {MulticastPort} -j ACCEPT",
            $"iptables -I INPUT -p tcp --dport {ChatPort} -j ACCEPT",
            $"iptables -I INPUT -p tcp --dport {FileTransferPort} -j ACCEPT"
        };

        var allSuccess = true;
        foreach (var cmd in iptablesCmds)
        {
            var parts = cmd.Split(' ', 2);
            var result = RunCommand("sudo", parts[1]);
            if (result.ExitCode != 0)
            {
                allSuccess = false;
            }
        }

        if (allSuccess)
        {
            AppLogger.Info("[防火墙] iptables 已添加规则");
            resultMessage = $"已添加 iptables 规则开放端口 {DiscoveryPort},{MulticastPort},{ChatPort},{FileTransferPort}。请确保在同一局域网内可以发现对方。";
            return true;
        }

        resultMessage = "无法自动添加 iptables 规则。\n\n请打开终端运行以下命令：\n" +
            $"  sudo iptables -I INPUT -p udp --dport {DiscoveryPort} -j ACCEPT\n" +
            $"  sudo iptables -I INPUT -p udp --dport {MulticastPort} -j ACCEPT\n" +
            $"  sudo iptables -I INPUT -p tcp --dport {ChatPort} -j ACCEPT\n" +
            $"  sudo iptables -I INPUT -p tcp --dport {FileTransferPort} -j ACCEPT\n\n" +
            "运行后重启 NoChat 即可发现其他设备。";
        return false;
    }

    private static (int ExitCode, string Output) RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (-1, "无法启动进程");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            var combinedOutput = string.IsNullOrEmpty(output) ? error : output;
            AppLogger.Info($"[防火墙] 命令: {fileName} {arguments}, 退出码: {process.ExitCode}");

            return (process.ExitCode, combinedOutput);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[防火墙] 执行命令失败: {fileName} {arguments}", ex);
            return (-1, ex.Message);
        }
    }
}
