using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoChat.App.Windows;

public partial class EditRemarkWindow : Window
{
    public string? ResultRemark { get; private set; }

    public EditRemarkWindow()
    {
        InitializeComponent();
    }

    public void SetPrompt(string displayName)
    {
        if (PromptText != null)
            PromptText.Text = $"为「{displayName}」设置备注：";
    }

    public void SetCurrentRemark(string? remark)
    {
        if (RemarkBox != null)
            RemarkBox.Text = remark ?? "";
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ResultRemark = RemarkBox?.Text?.Trim();
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
