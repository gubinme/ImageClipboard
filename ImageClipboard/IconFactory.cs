using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageClipboard;

internal static class IconFactory
{
    private static Icon? _trayIcon;
    private static Icon? _trayIconDisabled;

    public static Icon TrayIcon => _trayIcon ??= CreateTrayIcon(enabled: true);

    public static Icon TrayIconDisabled => _trayIconDisabled ??= CreateTrayIcon(enabled: false);

    private static Icon CreateTrayIcon(bool enabled)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var backColor = enabled
                ? Color.FromArgb(255, 74, 144, 226)
                : Color.FromArgb(255, 120, 120, 120);

            using (var backgroundBrush = new SolidBrush(backColor))
            {
                graphics.FillEllipse(backgroundBrush, 1, 1, 30, 30);
            }

            using var clipPath = CreateRoundedRect(new RectangleF(8, 6, 16, 20), 2f);
            using (var clipBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            {
                graphics.FillPath(clipBrush, clipPath);
            }

            using (var clipPen = new Pen(Color.FromArgb(200, 74, 144, 226), 1.2f))
            {
                graphics.DrawPath(clipPen, clipPath);
            }

            using (var imageBrush = new SolidBrush(Color.FromArgb(255, 120, 190, 255)))
            {
                graphics.FillRectangle(imageBrush, 10, 10, 12, 8);
            }

            using (var mountainPen = new Pen(Color.FromArgb(255, 74, 144, 226), 1.4f))
            {
                graphics.DrawLine(mountainPen, 10, 18, 14, 14);
                graphics.DrawLine(mountainPen, 14, 14, 18, 18);
                graphics.DrawLine(mountainPen, 18, 18, 22, 12);
            }

            using (var sunBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 80)))
            {
                graphics.FillEllipse(sunBrush, 18, 9, 4, 4);
            }
        }

        return CreateIconFromBitmap(bitmap);
    }

    public static void SaveApplicationIcon(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var icon = CreateTrayIcon(enabled: true);
        icon.Save(stream);
    }

    private static Icon CreateIconFromBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(handle);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
