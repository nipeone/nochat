using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoChat.App.Firewall;
using NoChat.App.Logging;
using NoChat.App.Settings;
using MainWindow = NoChat.App.MainWindow;

namespace NoChat.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        try
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LoadFromSettings();
                if (NetworkSection != null)
                    NetworkSection.IsVisible = WindowsFirewallHelper.IsWindows;
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsView 初始化失败", ex);
            throw;
        }
    }

    private void OnFirewallClick(object? sender, RoutedEventArgs e)
    {
        if (BtnFirewall != null) BtnFirewall.IsEnabled = false;
        var ok = WindowsFirewallHelper.TryAddFirewallRules(out var msg);
        if (FirewallResult != null)
        {
            FirewallResult.Text = msg;
            FirewallResult.IsVisible = true;
        }
        if (BtnFirewall != null) BtnFirewall.IsEnabled = true;
    }

    private void LoadFromSettings()
    {
        try
        {
            var data = AppSettings.Load();
            if (RadioThemeSystem != null) RadioThemeSystem.IsChecked = data.ThemeMode == ThemeMode.System;
            if (RadioThemeLight != null) RadioThemeLight.IsChecked = data.ThemeMode == ThemeMode.Light;
            if (RadioThemeDark != null) RadioThemeDark.IsChecked = data.ThemeMode == ThemeMode.Dark;
            if (CloseBehaviorCombo != null)
                CloseBehaviorCombo.SelectedIndex = data.CloseBehavior switch
                {
                    CloseBehavior.AskMe => 0,
                    CloseBehavior.MinimizeToTray => 1,
                    CloseBehavior.ExitProgram => 2,
                    _ => 0
                };
            if (StartOnBootSwitch != null)
                StartOnBootSwitch.IsChecked = data.StartOnBoot;
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsView LoadFromSettings 失败", ex);
        }
    }

    private void OnThemeSystem(object? sender, RoutedEventArgs e) => ApplyTheme(ThemeMode.System);
    private void OnThemeLight(object? sender, RoutedEventArgs e) => ApplyTheme(ThemeMode.Light);
    private void OnThemeDark(object? sender, RoutedEventArgs e) => ApplyTheme(ThemeMode.Dark);

    private void ApplyTheme(ThemeMode mode)
    {
        AppSettings.ThemeMode = mode;
        var app = Avalonia.Application.Current;
        if (app == null) return;
        app.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => Avalonia.Styling.ThemeVariant.Light,
            ThemeMode.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
        (app as App)?.ApplyAppBrushes(mode == ThemeMode.Dark);
        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is MainWindow main)
            main.SyncDarkModeToggle();
    }

    private void OnAccentBlue(object? sender, PointerPressedEventArgs e) => SetAccent("Blue");
    private void OnAccentGreen(object? sender, PointerPressedEventArgs e) => SetAccent("Green");
    private void OnAccentPurple(object? sender, PointerPressedEventArgs e) => SetAccent("Purple");
    private void OnAccentOrange(object? sender, PointerPressedEventArgs e) => SetAccent("Orange");
    private void OnAccentRed(object? sender, PointerPressedEventArgs e) => SetAccent("Red");
    private void OnAccentPink(object? sender, PointerPressedEventArgs e) => SetAccent("Pink");

    private void SetAccent(string name)
    {
        AppSettings.AccentColor = name;
    }

    private void OnCloseBehaviorChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CloseBehaviorCombo?.SelectedIndex is not int i) return;
        var behavior = i switch
        {
            1 => CloseBehavior.MinimizeToTray,
            2 => CloseBehavior.ExitProgram,
            _ => CloseBehavior.AskMe
        };
        AppSettings.CloseBehavior = behavior;
    }

    private void OnStartOnBootChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (StartOnBootSwitch?.IsChecked is bool b)
            AppSettings.StartOnBoot = b;
    }
}
