using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
using Avalonia.Threading;
using NoChat.App.Settings;
using NoChat.App.ViewModels;
using NoChat.App.Windows;
using NoChat.Core.Models;

namespace NoChat.App;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private string? _currentReceiveFolder;
    private bool _isExiting;
    private bool _friendListExpanded = true;

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            using var stream = NoChat.App.Assets.IconHelper.CreateAppIconStream();
            Icon = new WindowIcon(stream);
        }
        catch { /* 图标可选 */ }
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>
    /// 由托盘菜单「退出」或用户选择「退出程序」时调用，直接关闭窗口并退出应用。
    /// </summary>
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
        var saved = CloseBehaviorSettings.Load();
        CloseChoice choice;

        if (saved.HasValue)
        {
            choice = saved.Value;
        }
        else
        {
            var dialog = new CloseChoiceWindow();
            await dialog.ShowDialog(this);
            choice = dialog.Choice;
            if (choice != CloseChoice.None)
                CloseBehaviorSettings.Save(choice);
        }

        if (choice == CloseChoice.Exit)
            RequestExit();
        else if (choice == CloseChoice.MinimizeToTray)
            Hide();
    }

    private void OnFriendListToggleClick(object? sender, RoutedEventArgs e)
    {
        _friendListExpanded = !_friendListExpanded;
        if (MainGrid.ColumnDefinitions.Count >= 2)
            MainGrid.ColumnDefinitions[1] = new ColumnDefinition(_friendListExpanded ? new GridLength(244) : new GridLength(0));
        if (FriendListToggleBtn != null)
        {
            FriendListToggleBtn.Content = _friendListExpanded ? "◀" : "▶";
            ToolTip.SetTip(FriendListToggleBtn, _friendListExpanded ? "收起好友列表" : "展开好友列表");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _vm = new MainViewModel();
        _vm.DisplayName = Environment.MachineName;
        _vm.OnError += msg => Dispatcher.UIThread.Post(() =>
        {
            if (ChatTitle != null)
                ChatTitle.Text = $"错误: {msg}";
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
            if (stream == Stream.Null)
            {
                _currentReceiveFolder = null;
                return;
            }
            Dispatcher.UIThread.Post(async () =>
            {
                var dir = _currentReceiveFolder;
                if (dir == null)
                {
                    dir = await PromptSaveFolderAsync();
                    _currentReceiveFolder = dir;
                }
                if (dir != null)
                {
                    var fullPath = Path.Combine(dir, path);
                    var dirPath = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dirPath))
                        Directory.CreateDirectory(dirPath);
                    await using var fs = File.Create(fullPath);
                    await stream.CopyToAsync(fs);
                }
            });
        };
        DataContext = _vm;
    }

    private void OnThemeToggle(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark)
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        else
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    private void OnFriendSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is UserInfo user)
        {
            _vm.SelectPrivateChat(user);
            if (ChatTitle != null)
                ChatTitle.Text = $"与 {user.DisplayName} 聊天";
        }
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
        if (_vm != null)
            await _vm.SendMessageAsync();
    }

    private async void OnSendFileClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var friend = FriendList?.SelectedItem as UserInfo;
        if (friend == null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择要发送的文件"
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await _vm.SendFileAsync(friend, path);
    }

    private async void OnSendFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var friend = FriendList?.SelectedItem as UserInfo;
        if (friend == null) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择要发送的文件夹"
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            await _vm.SendFolderAsync(friend, path);
    }

    private async Task<string?> PromptSaveFileAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            Title = "保存文件到"
        });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PromptSaveFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择保存文件夹"
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
