using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ImageClipboard;

internal sealed class ClipboardImage : IDisposable
{
    public ClipboardImage(
        Image image,
        string? sourcePath = null,
        byte[]? originalBytes = null,
        string? suggestedFileName = null,
        Stream? lifetimeStream = null)
    {
        Image = image;
        SourcePath = sourcePath;
        OriginalBytes = originalBytes;
        SuggestedFileName = suggestedFileName ?? InferFileName(sourcePath, image);
        DragExtension = Path.GetExtension(SuggestedFileName);
        _lifetimeStream = lifetimeStream;
    }

    private readonly Stream? _lifetimeStream;

    public Image Image { get; }

    public string? SourcePath { get; }

    public byte[]? OriginalBytes { get; }

    public string SuggestedFileName { get; }

    public string DragExtension { get; }

    public void Dispose()
    {
        Image.Dispose();
        _lifetimeStream?.Dispose();
    }

    private static string InferFileName(string? sourcePath, Image image)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            return Path.GetFileName(sourcePath);
        }

        if (ImageAnimator.CanAnimate(image))
        {
            return "image.gif";
        }

        return "image.png";
    }
}

internal static class ClipboardImageHelper
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".avif", ".heic", ".heif"
    };

    private static readonly string[] PreferredFormatOrder =
    [
        "GIF",
        "image/gif",
        "WEBP",
        "WebP",
        "image/webp",
        "PNG",
        "image/png",
        "JFIF",
        "image/jpeg",
        "JPEG",
        "image/jpg",
        "AVIF",
        "image/avif",
        "TIFF",
        "image/tiff",
        "DeviceIndependentBitmap",
        "CF_DIB",
        DataFormats.Bitmap,
        "CF_BITMAP"
    ];

    public static string GetImageSignature(Image image)
    {
        using var pngStream = new MemoryStream();
        image.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
        var hash = SHA256.HashData(pngStream.ToArray());
        return $"image:{Convert.ToHexString(hash)}";
    }

    public static string? GetClipboardSignature()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count == 1)
                {
                    var path = files[0]!;
                    if (IsImageFile(path) && File.Exists(path))
                    {
                        var info = new FileInfo(path);
                        return $"file:{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                    }
                }
            }

            var dataObject = Clipboard.GetDataObject();
            if (dataObject is null)
            {
                return null;
            }

            foreach (var format in GetCandidateFormats(dataObject))
            {
                var payload = dataObject.GetData(format);
                var hash = HashPayload(payload);
                if (hash is not null)
                {
                    return $"{format}:{hash}";
                }
            }
        }
        catch (ExternalException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    public static ClipboardImage? TryGetImage()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var imageFromFile = TryLoadFromFileDrop(Clipboard.GetFileDropList());
                if (imageFromFile is not null)
                {
                    return imageFromFile;
                }
            }

            var dataObject = Clipboard.GetDataObject();
            if (dataObject is not null)
            {
                foreach (var format in GetCandidateFormats(dataObject))
                {
                    var imageFromFormat = TryLoadFromFormat(dataObject, format);
                    if (imageFromFormat is not null)
                    {
                        return imageFromFormat;
                    }
                }
            }

            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image is not null)
                {
                    return new ClipboardImage(
                        CloneImage(image),
                        suggestedFileName: ResolveSuggestedFileName(".png"));
                }
            }
        }
        catch (ExternalException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateFormats(IDataObject dataObject)
    {
        var formats = new List<string>();

        foreach (var format in PreferredFormatOrder)
        {
            if (dataObject.GetDataPresent(format))
            {
                formats.Add(format);
            }
        }

        foreach (var format in dataObject.GetFormats())
        {
            if (IsLikelyImageFormat(format) && !formats.Contains(format, StringComparer.OrdinalIgnoreCase))
            {
                formats.Add(format);
            }
        }

        return formats;
    }

    private static bool IsLikelyImageFormat(string format)
    {
        if (format.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return format.Equals("PNG", StringComparison.OrdinalIgnoreCase)
            || format.Equals("GIF", StringComparison.OrdinalIgnoreCase)
            || format.Equals("WEBP", StringComparison.OrdinalIgnoreCase)
            || format.Equals("JFIF", StringComparison.OrdinalIgnoreCase)
            || format.Equals("JPEG", StringComparison.OrdinalIgnoreCase);
    }

    private static ClipboardImage? TryLoadFromFileDrop(StringCollection files)
    {
        if (files.Count != 1)
        {
            return null;
        }

        var path = files[0]!;
        if (!IsImageFile(path) || !File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        var decoded = ImageDecoder.LoadFromBytes(bytes, Path.GetExtension(path));
        return new ClipboardImage(
            decoded.Image,
            sourcePath: path,
            originalBytes: bytes,
            suggestedFileName: Path.GetFileName(path),
            lifetimeStream: decoded.LifetimeStream);
    }

    private static ClipboardImage? TryLoadFromFormat(IDataObject dataObject, string format)
    {
        var data = dataObject.GetData(format);
        if (data is null)
        {
            return null;
        }

        var extension = FormatToExtension(format);

        switch (data)
        {
            case Image image:
                return new ClipboardImage(
                    CloneImage(image),
                    suggestedFileName: ResolveSuggestedFileName(extension));
            case byte[] bytes when bytes.Length > 0:
            {
                var copy = bytes.ToArray();
                var decoded = ImageDecoder.LoadFromBytes(copy, extension);
                return new ClipboardImage(
                    decoded.Image,
                    originalBytes: copy,
                    suggestedFileName: ResolveSuggestedFileName(ImageDecoder.DetectExtension(copy) ?? extension),
                    lifetimeStream: decoded.LifetimeStream);
            }
            case MemoryStream stream when stream.Length > 0:
            {
                var copy = stream.ToArray();
                var decoded = ImageDecoder.LoadFromBytes(copy, extension);
                return new ClipboardImage(
                    decoded.Image,
                    originalBytes: copy,
                    suggestedFileName: ResolveSuggestedFileName(ImageDecoder.DetectExtension(copy) ?? extension),
                    lifetimeStream: decoded.LifetimeStream);
            }
            case Stream genericStream when genericStream.CanRead:
            {
                using var copy = new MemoryStream();
                genericStream.CopyTo(copy);
                if (copy.Length == 0)
                {
                    return null;
                }

                var bytes = copy.ToArray();
                var decoded = ImageDecoder.LoadFromBytes(bytes, extension);
                return new ClipboardImage(
                    decoded.Image,
                    originalBytes: bytes,
                    suggestedFileName: ResolveSuggestedFileName(ImageDecoder.DetectExtension(bytes) ?? extension),
                    lifetimeStream: decoded.LifetimeStream);
            }
            default:
                return null;
        }
    }

    private static string ResolveSuggestedFileName(string? actualExtension)
    {
        actualExtension = string.IsNullOrWhiteSpace(actualExtension) ? ".png" : actualExtension;
        if (!actualExtension.StartsWith('.'))
        {
            actualExtension = $".{actualExtension}";
        }

        actualExtension = actualExtension.ToLowerInvariant();

        var htmlName = BrowserClipboardSource.TryGetSuggestedFileName();
        if (string.IsNullOrWhiteSpace(htmlName))
        {
            return $"image{actualExtension}";
        }

        var baseName = Path.GetFileNameWithoutExtension(htmlName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return $"image{actualExtension}";
        }

        return baseName + actualExtension;
    }

    private static string? FormatToExtension(string format)
    {
        if (format.Contains("webp", StringComparison.OrdinalIgnoreCase))
        {
            return ".webp";
        }

        if (format.Contains("gif", StringComparison.OrdinalIgnoreCase))
        {
            return ".gif";
        }

        if (format.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("jpg", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("JFIF", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        if (format.Contains("png", StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        if (format.Contains("bmp", StringComparison.OrdinalIgnoreCase))
        {
            return ".bmp";
        }

        if (format.Contains("tif", StringComparison.OrdinalIgnoreCase))
        {
            return ".tif";
        }

        if (format.Contains("avif", StringComparison.OrdinalIgnoreCase))
        {
            return ".avif";
        }

        return null;
    }

    private static bool IsImageFile(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    private static string? HashPayload(object? payload)
    {
        byte[]? bytes = payload switch
        {
            byte[] raw => raw,
            MemoryStream stream => stream.ToArray(),
            Stream genericStream => ReadAllBytes(genericStream),
            Image image => ImageToPngBytes(image),
            _ => null
        };

        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static byte[]? ReadAllBytes(Stream stream)
    {
        if (!stream.CanRead)
        {
            return null;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[]? ImageToPngBytes(Image image)
    {
        using var pngStream = new MemoryStream();
        image.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
        return pngStream.ToArray();
    }

    private static Image CloneImage(Image source)
    {
        if (ImageAnimator.CanAnimate(source))
        {
            return (Image)source.Clone();
        }

        return new Bitmap(source);
    }
}
