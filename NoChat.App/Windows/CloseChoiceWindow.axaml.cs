using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoChat.App.Windows;

public enum CloseChoice
{
    None,
    MinimizeToTray,
    Exit
}

public partial class CloseChoiceWindow : Window
{
    private CloseChoice _choice = CloseChoice.None;

    public CloseChoiceWindow()
    {
        InitializeComponent();
    }

    public CloseChoice Choice => _choice;

    private void OnMinimizeToTrayClick(object? sender, RoutedEventArgs e)
    {
        _choice = CloseChoice.MinimizeToTray;
        Close();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        _choice = CloseChoice.Exit;
        Close();
    }
}
