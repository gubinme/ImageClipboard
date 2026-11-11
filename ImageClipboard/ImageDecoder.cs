using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace ImageClipboard;

internal sealed class DecodedImage : IDisposable
{
    public DecodedImage(Image image, Stream? lifetimeStream, string extension)
    {
        Image = image;
        LifetimeStream = lifetimeStream;
        Extension = extension;
    }

    public Image Image { get; }

    public Stream? LifetimeStream { get; }

    public string Extension { get; }

    public void Dispose()
    {
        Image.Dispose();
        LifetimeStream?.Dispose();
    }
}

internal static class ImageDecoder
{
    private static readonly HashSet<string> GdiPreferredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif", ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".ico", ".tif", ".tiff"
    };

    public static DecodedImage LoadFromBytes(byte[] bytes, string? extensionHint = null)
    {
        var stream = new MemoryStream(bytes, writable: false);
        var extension = DetectExtension(stream) ?? NormalizeExtension(extensionHint);

        if (extension is not null && GdiPreferredExtensions.Contains(extension))
        {
            stream.Position = 0;
            try
            {
                var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new DecodedImage(image, stream, extension);
            }
            catch
            {
                stream.Position = 0;
            }
        }

        stream.Position = 0;
        try
        {
            using var sharpImage = SixLaborsImage.Load(stream);
            if (sharpImage.Frames.Count > 1)
            {
                var gifStream = new MemoryStream();
                sharpImage.Save(gifStream, new GifEncoder());
                gifStream.Position = 0;
                stream.Dispose();

                var animated = Image.FromStream(gifStream, useEmbeddedColorManagement: false, validateImageData: false);
                return new DecodedImage(animated, gifStream, ".gif");
            }

            using var frame = sharpImage.CloneAs<Rgba32>();
            var bitmap = ToBitmap(frame);
            stream.Dispose();
            return new DecodedImage(bitmap, lifetimeStream: null, extension ?? ".png");
        }
        catch
        {
            stream.Position = 0;
            try
            {
                var fallback = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new DecodedImage(fallback, stream, extension ?? ".png");
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }

    public static string CombineFileName(string? suggestedFileName, string actualExtension)
    {
        actualExtension = NormalizeExtension(actualExtension) ?? ".png";
        var name = string.IsNullOrWhiteSpace(suggestedFileName)
            ? "image"
            : Path.GetFileNameWithoutExtension(suggestedFileName);

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "image";
        }

        return name + actualExtension;
    }

    public static string GetMimeType(string extension)
    {
        extension = NormalizeExtension(extension) ?? ".png";
        return extension switch
        {
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            _ => "image/png"
        };
    }

    public static string? DetectExtension(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return DetectExtension(stream);
    }

    public static string? DetectExtension(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        var position = stream.Position;
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        stream.Position = position;

        if (read >= 3 && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
        {
            return ".gif";
        }

        if (read >= 8 &&
            header[0] == 0x89 &&
            header[1] == (byte)'P' &&
            header[2] == (byte)'N' &&
            header[3] == (byte)'G')
        {
            return ".png";
        }

        if (read >= 2 && header[0] == 0xFF && header[1] == 0xD8)
        {
            return ".jpg";
        }

        if (read >= 12 &&
            header[0] == (byte)'R' &&
            header[1] == (byte)'I' &&
            header[2] == (byte)'F' &&
            header[3] == (byte)'F' &&
            header[8] == (byte)'W' &&
            header[9] == (byte)'E' &&
            header[10] == (byte)'B' &&
            header[11] == (byte)'P')
        {
            return ".webp";
        }

        if (read >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M')
        {
            return ".bmp";
        }

        return null;
    }

    private static Bitmap ToBitmap(SixLabors.ImageSharp.Image<Rgba32> source)
    {
        using var memoryStream = new MemoryStream();
        source.Save(memoryStream, new PngEncoder());
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        extension = extension.Trim();
        if (!extension.StartsWith('.'))
        {
            extension = $".{extension}";
        }

        return extension.ToLowerInvariant();
    }
}
