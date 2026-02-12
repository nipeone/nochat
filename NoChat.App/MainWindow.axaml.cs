using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using NoChat.App.Logging;
using NoChat.App.Settings;
using NoChat.App.ViewModels;
using NoChat.App.Views;
using NoChat.App.Windows;
using NoChat.Core.Models;

namespace NoChat.App;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private string? _currentReceiveFolder;
    private bool _isExiting;
    private bool _navBusy;

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            using var stream = NoChat.App.Assets.IconHelper.CreateAppIconStream();
            Icon = new WindowIcon(stream);
        }
        catch { }
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public void RequestExit()
    {
        _isExiting = true;
        Close();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            _vm?.Dispose();
            return;
        }

        e.Cancel = true;
        var behavior = AppSettings.CloseBehavior;
        CloseChoice choice;

        if (behavior == CloseBehavior.MinimizeToTray)
            choice = CloseChoice.MinimizeToTray;
        else if (behavior == CloseBehavior.ExitProgram)
            choice = CloseChoice.Exit;
        else
        {
            var saved = AppSettings.SavedCloseChoice;
            if (saved.HasValue)
                choice = saved.Value;
            else
            {
                var dialog = new CloseChoiceWindow();
                await dialog.ShowDialog(this);
                choice = dialog.Choice;
                if (dialog.RememberChoice && choice != CloseChoice.None)
                    AppSettings.SavedCloseChoice = choice;
            }
        }

        if (choice == CloseChoice.Exit)
            RequestExit();
        else if (choice == CloseChoice.MinimizeToTray)
            Hide();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyThemeFromSettings();
        SyncDarkModeToggle();
        _vm = new MainViewModel();
        _vm.DisplayName = Environment.MachineName;
        DataContext = _vm;
        SetNavHighlight(true);
        if (UserInitial != null)
            UserInitial.Text = (_vm.MyName.Length > 0 ? char.ToUpperInvariant(_vm.MyName[0]) : 'N').ToString();
        _vm.OnError += msg => Dispatcher.UIThread.Post(() =>
        {
            if (ChatTitle != null) ChatTitle.Text = $"错误: {msg}";
        });
        _vm.OnReceiveFileRequest += (senderId, senderName, fileName, size, stream) =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var path = await PromptSaveFileAsync(fileName);
                if (path != null && stream != null)
                {
                    await using var fs = File.Create(path);
                    await stream.CopyToAsync(fs);
                }
            });
        };
        _vm.OnReceiveFolderRequest += (senderId, senderName, path, isFolder, stream) =>
        {
            if (stream == Stream.Null) { _currentReceiveFolder = null; return; }
            Dispatcher.UIThread.Post(async () =>
            {
                var dir = _currentReceiveFolder ?? await PromptSaveFolderAsync();
                _currentReceiveFolder = dir;
                if (dir != null)
                {
                    var fullPath = Path.Combine(dir, path);
                    var dirPath = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dirPath)) Directory.CreateDirectory(dirPath);
                    await using var fs = File.Create(fullPath);
                    await stream.CopyToAsync(fs);
                }
            });
        };
    }

    private void ApplyThemeFromSettings()
    {
        var mode = AppSettings.ThemeMode;
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        ApplyBrushes(Application.Current.RequestedThemeVariant == ThemeVariant.Dark);
    }

    private void ApplyBrushes(bool isDark)
    {
        if (Application.Current?.Resources == null) return;
        var r = Application.Current.Resources;
        if (isDark)
        {
            r["SidebarBrush"] = new SolidColorBrush(Color.Parse("#161B26"));
            r["ContentBrush"] = new SolidColorBrush(Color.Parse("#0F1117"));
            r["HeaderBrush"] = new SolidColorBrush(Color.Parse("#161B26"));
            r["BorderBrush"] = new SolidColorBrush(Color.Parse("#252B38"));
        }
        else
        {
            r["SidebarBrush"] = new SolidColorBrush(Color.Parse("#E8EEF5"));
            r["ContentBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
            r["HeaderBrush"] = new SolidColorBrush(Color.Parse("#F0F4F8"));
            r["BorderBrush"] = new SolidColorBrush(Color.Parse("#D0D7DE"));
        }
        var accent = AppSettings.AccentColor switch
        {
            "Green" => Color.Parse("#50C878"),
            "Purple" => Color.Parse("#9B59B6"),
            "Orange" => Color.Parse("#E67E22"),
            "Red" => Color.Parse("#E74C3C"),
            "Pink" => Color.Parse("#E91E63"),
            _ => Color.Parse("#5B8DEE")
        };
        r["AccentBrush"] = new SolidColorBrush(accent);
        r["NavSelectedBrush"] = new SolidColorBrush(Color.FromArgb(0x28, accent.R, accent.G, accent.B));
    }

    private void SyncDarkModeToggle()
    {
        if (DarkModeToggle == null) return;
        var isDark = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
        DarkModeToggle.IsChecked = isDark;
    }

    private void OnDarkModeToggled(object? sender, RoutedEventArgs e)
    {
        if (DarkModeToggle?.IsChecked != true)
        {
            AppSettings.ThemeMode = ThemeMode.Light;
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        }
        else
        {
            AppSettings.ThemeMode = ThemeMode.Dark;
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }
        ApplyBrushes(Application.Current!.RequestedThemeVariant == ThemeVariant.Dark);
        SetNavHighlight(SettingsPanel?.IsVisible != true);
    }

    private void SetNavHighlight(bool chatsActive)
    {
        var brush = Application.Current?.Resources["NavSelectedBrush"] as Avalonia.Media.IBrush;
        if (NavChats != null) NavChats.Background = chatsActive ? brush : null;
        if (NavSettings != null) NavSettings.Background = !chatsActive ? brush : null;
        if (NavGroups != null) NavGroups.Background = null;
    }

    private void OnNavChats(object? sender, RoutedEventArgs e)
    {
        if (_navBusy) return;
        _navBusy = true;
        try
        {
            SetNavHighlight(true);
            if (ChatPanel != null) ChatPanel.IsVisible = true;
            if (SettingsPanel != null) { SettingsPanel.Content = null; SettingsPanel.IsVisible = false; }
        }
        catch (Exception ex)
        {
            AppLogger.Error("导航到聊天时发生错误", ex);
        }
        finally
        {
            _navBusy = false;
        }
    }

    private void OnNavGroups(object? sender, RoutedEventArgs e)
    {
        if (_navBusy) return;
        _navBusy = true;
        try
        {
            SetNavHighlight(true);
            if (ChatPanel != null) ChatPanel.IsVisible = true;
            if (SettingsPanel != null) { SettingsPanel.Content = null; SettingsPanel.IsVisible = false; }
        }
        catch (Exception ex)
        {
            AppLogger.Error("导航到群组时发生错误", ex);
        }
        finally
        {
            _navBusy = false;
        }
    }

    private void OnNavSettings(object? sender, RoutedEventArgs e)
    {
        if (_navBusy) return;
        _navBusy = true;
        try
        {
            SetNavHighlight(false);
            if (ChatPanel != null) ChatPanel.IsVisible = false;
            if (SettingsPanel != null)
            {
                SettingsPanel.Content = new SettingsView();
                SettingsPanel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("打开设置界面时发生错误", ex);
            if (ChatPanel != null) ChatPanel.IsVisible = true;
            ShowErrorDialog($"无法打开设置：{ex.Message} 详见日志：{AppLogger.LogFilePath}");
        }
        finally
        {
            _navBusy = false;
        }
    }

    private void ShowErrorDialog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ChatTitle != null)
                ChatTitle.Text = message.Length > 80 ? message.Substring(0, 80) + "…" : message;
            if (ChatPanel != null) ChatPanel.IsVisible = true;
        });
    }

    private void OnFriendSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is UserInfo user)
        {
            _vm.SelectPrivateChat(user);
            if (ChatTitle != null) ChatTitle.Text = user.DisplayName;
            if (ChatSubtitle != null) ChatSubtitle.Text = "在线";
            if (ChatPartnerInitial != null) ChatPartnerInitial.Text = (user.DisplayName.Length > 0 ? char.ToUpperInvariant(user.DisplayName[0]) : '?').ToString();
        }
    }

    private void OnAddManualHostClick(object? sender, RoutedEventArgs e)
    {
        _vm?.AddManualHost();
    }

    private void OnInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && _vm != null)
        {
            _ = _vm.SendMessageAsync();
            e.Handled = true;
        }
    }

    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (_vm != null) await _vm.SendMessageAsync();
    }

    private async void OnSendFileClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var friend = FriendList?.SelectedItem as UserInfo;
        if (friend == null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, Title = "选择要发送的文件" });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await _vm.SendFileAsync(friend, path);
    }

    private async void OnSendFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var friend = FriendList?.SelectedItem as UserInfo;
        if (friend == null) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false, Title = "选择要发送的文件夹" });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            await _vm.SendFolderAsync(friend, path);
    }

    private async Task<string?> PromptSaveFileAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = suggestedName, Title = "保存文件到" });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PromptSaveFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "选择保存文件夹" });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
