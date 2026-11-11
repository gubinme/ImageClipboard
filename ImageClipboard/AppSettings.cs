using System.Text.Json;

namespace ImageClipboard;

internal sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImageClipboard",
        "settings.json");

    public string MonitorDeviceName { get; set; } = string.Empty;
    public bool AutoStart { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

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
        if (!string.IsNullOrWhiteSpace(MonitorDeviceName))
        {
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == MonitorDeviceName);
            if (screen is not null)
            {
                return screen;
            }
        }

        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }
}
