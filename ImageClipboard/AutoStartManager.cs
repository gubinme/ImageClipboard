using Microsoft.Win32;

namespace ImageClipboard;

internal static class AutoStartManager
{
    private const string AppName = "ImageClipboard";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    public static void SyncWithSettings(AppSettings settings)
    {
        var enabledInRegistry = IsEnabled();
        if (settings.AutoStart != enabledInRegistry)
        {
            SetEnabled(settings.AutoStart);
        }
    }
}
