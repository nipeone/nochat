using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NoChat.App.Settings;

namespace NoChat.App.Windows;

public partial class CloseChoiceWindow : Window
{
    private CloseChoice _choice = CloseChoice.None;
    private bool _remember;

    public CloseChoiceWindow()
    {
        InitializeComponent();
    }

    public CloseChoice Choice => _choice;
    public bool RememberChoice => CheckRemember?.IsChecked == true;

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _choice = RadioExit?.IsChecked == true ? CloseChoice.Exit : CloseChoice.MinimizeToTray;
        _remember = CheckRemember?.IsChecked == true;
        if (_remember && _choice != CloseChoice.None)
            AppSettings.SavedCloseChoice = _choice;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _choice = CloseChoice.None;
        Close();
    }
}
