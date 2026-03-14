using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Styling;
using Avalonia.Threading;
using NoChat.App.Settings;
using NoChat.App.Update;

namespace NoChat.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private ThemeMode _themeMode;
    private string _accentColor = "Blue";
    private CloseBehavior _closeBehavior;
    private bool _startOnBoot;
    private bool _checkUpdateOnStartup = true;
    private bool _autoCheckUpdate = true;
    private string _currentVersion = "";
    private string _updateStatus = "";
    private bool _isCheckingUpdate;
    private bool _hasUpdate;
    private string? _latestVersion;
    private string? _downloadUrl;
    private bool _isDownloading;
    private double _downloadProgress;

    public SettingsViewModel()
    {
        var data = AppSettings.Load();
        _themeMode = data.ThemeMode;
        _accentColor = data.AccentColor;
        _closeBehavior = data.CloseBehavior;
        _startOnBoot = data.StartOnBoot;
        _checkUpdateOnStartup = data.CheckUpdateOnStartup;
        _autoCheckUpdate = data.AutoCheckUpdate;
        _currentVersion = UpdateService.GetCurrentVersion();
    }

    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set { _themeMode = value; Raise(); ApplyTheme(); AppSettings.ThemeMode = value; }
    }

    public string AccentColor
    {
        get => _accentColor;
        set { _accentColor = value ?? "Blue"; Raise(); AppSettings.AccentColor = _accentColor; }
    }

    public CloseBehavior CloseBehavior
    {
        get => _closeBehavior;
        set { _closeBehavior = value; Raise(); AppSettings.CloseBehavior = value; }
    }

    public bool StartOnBoot
    {
        get => _startOnBoot;
        set { _startOnBoot = value; Raise(); AppSettings.StartOnBoot = value; ApplyStartOnBoot(); }
    }

    public bool CheckUpdateOnStartup
    {
        get => _checkUpdateOnStartup;
        set { _checkUpdateOnStartup = value; Raise(); AppSettings.CheckUpdateOnStartup = value; }
    }

    public bool AutoCheckUpdate
    {
        get => _autoCheckUpdate;
        set { _autoCheckUpdate = value; Raise(); AppSettings.AutoCheckUpdate = value; }
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        private set { _currentVersion = value; Raise(); }
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        private set { _updateStatus = value; Raise(); }
    }

    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        private set { _isCheckingUpdate = value; Raise(); }
    }

    public bool HasUpdate
    {
        get => _hasUpdate;
        private set { _hasUpdate = value; Raise(); }
    }

    public string? LatestVersion
    {
        get => _latestVersion;
        private set { _latestVersion = value; Raise(); }
    }

    public string? DownloadUrl
    {
        get => _downloadUrl;
        private set { _downloadUrl = value; Raise(); }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set { _isDownloading = value; Raise(); }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        private set { _downloadProgress = value; Raise(); }
    }

    public static string[] AccentColorNames => new[] { "Blue", "Green", "Purple", "Orange", "Red", "Pink" };

    public async Task CheckForUpdateAsync()
    {
        if (IsCheckingUpdate) return;

        IsCheckingUpdate = true;
        UpdateStatus = "正在检查更新...";
        HasUpdate = false;

        try
        {
            var result = await UpdateService.CheckForUpdateAsync();

            if (result.HasUpdate)
            {
                HasUpdate = true;
                LatestVersion = result.LatestVersion;
                DownloadUrl = result.DownloadUrl;
                UpdateStatus = $"发现新版本: v{result.LatestVersion}";
                AppSettings.LastCheckedVersion = result.LatestVersion;
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                UpdateStatus = $"检查更新失败: {result.ErrorMessage}";
            }
            else
            {
                UpdateStatus = $"已是最新版本: v{result.CurrentVersion}";
            }
        }
        catch
        {
            UpdateStatus = "检查更新失败";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    public async Task DownloadAndInstallAsync()
    {
        if (string.IsNullOrEmpty(DownloadUrl) || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        UpdateStatus = "正在下载更新...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p * 100;
            });

            var installerPath = await UpdateService.DownloadUpdateAsync(DownloadUrl, progress);

            if (string.IsNullOrEmpty(installerPath))
            {
                UpdateStatus = "下载失败";
                return;
            }

            UpdateStatus = "正在启动安装程序...";

            var success = UpdateService.InstallUpdate(installerPath);
            if (success)
            {
                UpdateStatus = "正在安装，请等待...";
                // 退出当前应用，让安装程序完成更新
                await Task.Delay(2000);
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            else
            {
                UpdateStatus = "启动安装程序失败";
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"更新失败: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void ApplyTheme()
    {
        if (Avalonia.Application.Current == null) return;
        var variant = _themeMode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        Avalonia.Application.Current.RequestedThemeVariant = variant;
    }

    private void ApplyStartOnBoot()
    {
        try
        {
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcut = Path.Combine(startupFolder, "NoChat.lnk");
                if (_startOnBoot)
                {
                    var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(exePath)) return;
                    CreateShortcut(shortcut, exePath);
                }
                else if (File.Exists(shortcut))
                    File.Delete(shortcut);
            }
#endif
        }
        catch { /* ignore */ }
    }

#if WINDOWS
    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        try
        {
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            var link = shell!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            link!.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, link, new object[] { targetPath });
            link.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);
        }
        catch { /* ignore */ }
    }
#endif
}
