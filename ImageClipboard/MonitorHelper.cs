namespace ImageClipboard;

internal static class MonitorHelper
{
    public static IReadOnlyList<Screen> GetOrderedScreens()
    {
        return Screen.AllScreens
            .OrderBy(s => s.Bounds.X)
            .ThenBy(s => s.Bounds.Y)
            .ThenBy(s => s.Bounds.Width)
            .ThenBy(s => s.Bounds.Height)
            .ToArray();
    }

    public static Screen GetPrimaryScreen()
    {
        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    public static string GetFingerprint(Screen screen)
    {
        var bounds = screen.Bounds;
        return $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
    }

    public static string GetDisplayLabel(Screen screen, int index)
    {
        var primaryMark = screen.Primary ? " (основной в Windows)" : string.Empty;
        return $"Монитор {index}{primaryMark} — {screen.Bounds.Width}×{screen.Bounds.Height}";
    }

    public static Screen? FindByFingerprint(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        return GetOrderedScreens().FirstOrDefault(s => GetFingerprint(s) == fingerprint);
    }
}
