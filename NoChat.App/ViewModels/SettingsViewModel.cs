using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Styling;
using NoChat.App.Settings;

namespace NoChat.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private ThemeMode _themeMode;
    private string _accentColor = "Blue";
    private CloseBehavior _closeBehavior;
    private bool _startOnBoot;

    public SettingsViewModel()
    {
        var data = AppSettings.Load();
        _themeMode = data.ThemeMode;
        _accentColor = data.AccentColor;
        _closeBehavior = data.CloseBehavior;
        _startOnBoot = data.StartOnBoot;
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

    public static string[] AccentColorNames => new[] { "Blue", "Green", "Purple", "Orange", "Red", "Pink" };

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
