using System.Text.Json;

namespace ImageClipboard;

internal sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImageClipboard",
        "settings.json");

    public bool UsePrimaryMonitor { get; set; } = true;

    public string MonitorFingerprint { get; set; } = string.Empty;

    [Obsolete("Used only for migration from older versions.")]
    public string MonitorDeviceName { get; set; } = string.Empty;

    public bool AutoStart { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public PreviewCorner PreviewCorner { get; set; } = PreviewCorner.BottomRight;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    settings.MigrateLegacyMonitorSelection();
                    return settings;
                }
            }
        }
        catch
        {
            // Ignore corrupted settings and fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public Screen GetSelectedScreen()
    {
        if (UsePrimaryMonitor)
        {
            return MonitorHelper.GetPrimaryScreen();
        }

        var screen = MonitorHelper.FindByFingerprint(MonitorFingerprint);
        return screen ?? MonitorHelper.GetPrimaryScreen();
    }

    private void MigrateLegacyMonitorSelection()
    {
#pragma warning disable CS0618
        if (string.IsNullOrWhiteSpace(MonitorDeviceName))
        {
            return;
        }

        var legacyScreen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == MonitorDeviceName);
        if (legacyScreen is not null)
        {
            UsePrimaryMonitor = false;
            MonitorFingerprint = MonitorHelper.GetFingerprint(legacyScreen);
        }
        else
        {
            UsePrimaryMonitor = true;
            MonitorFingerprint = string.Empty;
        }

        MonitorDeviceName = string.Empty;
#pragma warning restore CS0618
    }
}
