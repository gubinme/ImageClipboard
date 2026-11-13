using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ImageClipboard;

internal static class IconFactory
{
    private static Icon? _trayIcon;
    private static Icon? _trayIconDisabled;
    private static Image? _logoImage;

    public static Icon TrayIcon => _trayIcon ??= CreateTrayIcon(enabled: true);

    public static Icon TrayIconDisabled => _trayIconDisabled ??= CreateTrayIcon(enabled: false);

    public static Image LogoImage => _logoImage ??= LoadLogoBitmap();

    public static Image GetLogo(int size)
    {
        return ResizeLogo(LogoImage, size, enabled: true);
    }

    private static Icon CreateTrayIcon(bool enabled)
    {
        using var bitmap = ResizeLogo(LogoImage, 32, enabled);
        return CreateIconFromBitmap(bitmap);
    }

    public static void SaveApplicationIcon(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        using var logo = LoadLogoBitmap();
        var frames = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var bitmap = ResizeLogo(logo, size, enabled: true);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            frames.Add(stream.ToArray());
        }

        WritePngIco(path, frames);
    }

    private static Bitmap LoadLogoBitmap()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("Logo resource stream is null.");
            using var source = new Bitmap(stream);
            return new Bitmap(source);
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (File.Exists(fallback))
        {
            using var source = new Bitmap(fallback);
            return new Bitmap(source);
        }

        throw new FileNotFoundException("Logo asset not found.", "logo.png");
    }

    private static Bitmap ResizeLogo(Image source, int size, bool enabled)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;

            var padding = Math.Max(1, size / 16);
            var dest = new Rectangle(padding, padding, size - (padding * 2), size - (padding * 2));
            graphics.DrawImage(source, dest);
        }

        if (!enabled)
        {
            ApplyDisabledLook(bitmap);
        }

        return bitmap;
    }

    private static void ApplyDisabledLook(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = Math.Abs(data.Stride) * bitmap.Height;
            var buffer = new byte[bytes];
            Marshal.Copy(data.Scan0, buffer, 0, bytes);

            for (var i = 0; i < buffer.Length; i += 4)
            {
                var b = buffer[i];
                var g = buffer[i + 1];
                var r = buffer[i + 2];
                var a = buffer[i + 3];
                if (a == 0)
                {
                    continue;
                }

                var gray = (byte)((r * 0.299) + (g * 0.587) + (b * 0.114));
                buffer[i] = gray;
                buffer[i + 1] = gray;
                buffer[i + 2] = gray;
                buffer[i + 3] = (byte)(a * 0.55);
            }

            Marshal.Copy(buffer, 0, data.Scan0, bytes);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePngIco(string path, IReadOnlyList<byte[]> pngFrames)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngFrames.Count);

        var offset = 6 + (16 * pngFrames.Count);
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };

        for (var i = 0; i < pngFrames.Count; i++)
        {
            var size = sizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(pngFrames[i].Length);
            writer.Write(offset);
            offset += pngFrames[i].Length;
        }

        foreach (var frame in pngFrames)
        {
            writer.Write(frame);
        }
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
