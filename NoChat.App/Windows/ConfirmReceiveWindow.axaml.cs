using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoChat.App.Windows;

public partial class ConfirmReceiveWindow : Window
{
    public bool? Result { get; private set; }

    public ConfirmReceiveWindow()
    {
        InitializeComponent();
    }

    public void SetMessage(string senderName, string fileName, long sizeOrFileCount, bool isFolder)
    {
        if (MessageText == null) return;
        MessageText.Text = isFolder
            ? $"{senderName} 想发送文件夹「{fileName}」（共 {sizeOrFileCount} 个文件）给你，是否接收？"
            : $"{senderName} 想发送文件「{fileName}」({FormatSize(sizeOrFileCount)}) 给你，是否接收？";
    }

    private static string FormatSize(long size)
        => size < 1024 ? $"{size} B" : size < 1024 * 1024 ? $"{size / 1024.0:F1} KB" : $"{size / (1024.0 * 1024):F1} MB";

    private void OnAcceptClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnRejectClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
