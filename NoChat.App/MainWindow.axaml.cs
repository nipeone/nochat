using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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

    /// <summary>主窗口被显示时调用（如从托盘菜单“显示主窗口”），用于恢复“窗口可见”状态以便后续新消息能正确触发闪烁</summary>
    public void NotifyWindowShown()
    {
        _vm?.SetWindowVisible(true);
    }

    /// <summary>显示窗口并切换到未读会话（双击托盘时调用），若有未读则选中对应好友并显示消息。保证在 UI 线程执行。</summary>
    public void ShowAndSelectUnreadChat()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = _vm;
            if (vm == null)
            {
                Show();
                Activate();
                return;
            }
            // 先切回聊天视图（若当前在设置页），否则右侧仍是设置、消息区不可见
            SetNavHighlight(true);
            if (SettingsPanel != null) { SettingsPanel.Content = null; SettingsPanel.IsVisible = false; }
            if (ChatPanel != null) ChatPanel.IsVisible = true;

            var senderId = vm.LastUnreadSenderId;
        if (!string.IsNullOrEmpty(senderId))
        {
            var item = vm.Friends.FirstOrDefault(f => f.UserInfo.Id == senderId);
            if (item != null)
            {
                vm.SelectPrivateChat(item.UserInfo);
                if (FriendList != null) FriendList.SelectedItem = item;
                if (ChatTitle != null) ChatTitle.Text = item.DisplayNameForList;
                if (ChatSubtitle != null) ChatSubtitle.Text = "在线";
                if (ChatPartnerInitial != null) ChatPartnerInitial.Text = (item.DisplayNameForList.Length > 0 ? char.ToUpperInvariant(item.DisplayNameForList[0]) : '?').ToString();
            }
        }

            _vm?.SetWindowVisible(true);
            Show();
            Activate();

            // 列表更新后再滚动到底部，便于看到最新消息
            if (MessageScroll != null && vm.CurrentMessages.Count > 0)
                Dispatcher.UIThread.Post(() => MessageScroll.ScrollToEnd(), DispatcherPriority.Loaded);
        });
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
                    AppSettings.SaveClosePreference(choice);
            }
        }

        if (choice == CloseChoice.Exit)
            RequestExit();
        else if (choice == CloseChoice.MinimizeToTray)
        {
            _vm?.SetWindowVisible(false);
            Hide();
        }
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

        void ScrollChatToEnd()
        {
            if (MessageScroll != null)
                MessageScroll.ScrollToEnd();
        }
        void OnCurrentMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
                Dispatcher.UIThread.Post(ScrollChatToEnd, DispatcherPriority.Loaded);
        }
        _vm.CurrentMessages.CollectionChanged += OnCurrentMessagesChanged;

        _vm.OnError += msg => Dispatcher.UIThread.Post(() =>
        {
            if (ChatTitle != null) ChatTitle.Text = $"错误: {msg}";
        });
        _vm.OnUnreadMessage += (senderId, senderName, preview) =>
        {
            var app = Application.Current as App;
            app?.ShowTrayNotification($"来自 {senderName}", preview);
            app?.StartTrayBlink();
        };
        _vm.OnSessionViewed += () =>
        {
            (Application.Current as App)?.StopTrayBlink();
        };
        _vm.SetFileRequestHandler(async (senderId, senderName, fileName, size) =>
        {
            var tcs = new TaskCompletionSource<bool?>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var w = new Windows.ConfirmReceiveWindow();
                w.SetMessage(senderName, fileName, size, false);
                await w.ShowDialog(this);
                tcs.TrySetResult(w.Result);
            });
            return await tcs.Task == true;
        });
        _vm.OnReceiveFileRequest += async (senderId, senderName, fileName, size, stream) =>
        {
            string? savePath = null;
            await Dispatcher.UIThread.InvokeAsync(async () => { savePath = await PromptSaveFileAsync(fileName); });
            if (savePath != null && stream != null)
            {
                await using var fs = File.Create(savePath);
                await stream.CopyToAsync(fs);
                _vm?.AddReceivedFileMessage(senderId, senderName, fileName);
            }
        };
        _vm.SetFolderStartHandler(async (senderId, senderName, folderName, fileCount) =>
        {
            var confirmTcs = new TaskCompletionSource<bool?>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var w = new Windows.ConfirmReceiveWindow();
                w.SetMessage(senderName, folderName, fileCount, true);
                await w.ShowDialog(this);
                confirmTcs.TrySetResult(w.Result);
            });
            if (await confirmTcs.Task != true)
                return (false, null);
            var dirTcs = new TaskCompletionSource<string?>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dir = await PromptSaveFolderAsync();
                dirTcs.TrySetResult(dir);
            });
            var dir = await dirTcs.Task;
            return (dir != null, dir);
        });
        _vm.SetFolderFileHandler(async (senderId, senderName, relativePath, folderName, stream, saveDir) =>
        {
            var baseDir = Path.Combine(saveDir, folderName);
            var fullPath = Path.Combine(baseDir, relativePath);
            var dirPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dirPath)) Directory.CreateDirectory(dirPath);
            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs);
        });
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
        (Application.Current as App)?.ApplyAppBrushes(isDark);
    }

    /// <summary>同步侧边栏深色模式开关与当前主题（设置页切换主题后调用）</summary>
    public void SyncDarkModeToggle()
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
        if (e.AddedItems[0] is FriendItemViewModel item)
        {
            _vm.SelectPrivateChat(item.UserInfo);
            (Application.Current as App)?.StopTrayBlink();
            if (ChatTitle != null) ChatTitle.Text = item.DisplayNameForList;
            if (ChatSubtitle != null) ChatSubtitle.Text = "在线";
            if (ChatPartnerInitial != null) ChatPartnerInitial.Text = (item.DisplayNameForList.Length > 0 ? char.ToUpperInvariant(item.DisplayNameForList[0]) : '?').ToString();
        }
    }

    private async void OnRecallMessageClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        MessageDisplayItem? item = null;
        if (sender is Avalonia.Controls.MenuItem menuItem)
        {
            var ctx = menuItem.Parent as Avalonia.Controls.ContextMenu;
            var target = ctx?.PlacementTarget as Avalonia.Controls.Control;
            item = target?.DataContext as MessageDisplayItem;
        }
        else if (sender is Avalonia.Controls.Button btn)
            item = btn.DataContext as MessageDisplayItem;
        if (item != null)
            await _vm.RecallMessageAsync(item);
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
        var item = FriendList?.SelectedItem as FriendItemViewModel;
        if (item == null) return;
        var friend = item.UserInfo;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, Title = "选择要发送的文件" });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                return;
            if (Directory.Exists(path))
            {
                ShowErrorDialog("请选择文件，不要选择文件夹。");
                return;
            }
            if (!File.Exists(path))
            {
                ShowErrorDialog("所选路径不是有效文件。");
                return;
            }
            await _vm.SendFileAsync(friend, path);
        }
        catch (Exception ex)
        {
            Logging.AppLogger.Error("选择或发送文件时出错", ex);
            ShowErrorDialog($"发送文件失败：{ex.Message}");
        }
    }

    private async void OnEditRemarkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        var ctx = menuItem.Parent as ContextMenu;
        var target = ctx?.PlacementTarget as Control;
        var item = target?.DataContext as FriendItemViewModel;
        if (item == null) return;
        var dialog = new EditRemarkWindow();
        dialog.SetPrompt(item.UserInfo.DisplayName);
        dialog.SetCurrentRemark(item.CurrentRemark);
        await dialog.ShowDialog(this);
        if (dialog.ResultRemark != null)
            item.SetRemark(dialog.ResultRemark);
    }

    private async void OnSendFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var item = FriendList?.SelectedItem as FriendItemViewModel;
        if (item == null) return;
        var friend = item.UserInfo;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false, Title = "选择要发送的文件夹" });
            if (folders.Count == 0) return;
            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                return;
            if (!Directory.Exists(path))
            {
                ShowErrorDialog("请选择文件夹。");
                return;
            }
            await _vm.SendFolderAsync(friend, path);
        }
        catch (Exception ex)
        {
            Logging.AppLogger.Error("选择或发送文件夹时出错", ex);
            ShowErrorDialog($"发送文件夹失败：{ex.Message}");
        }
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
