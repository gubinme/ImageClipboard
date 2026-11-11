using System.Diagnostics;

namespace ImageClipboard;

internal static class ImageFileHelper
{
    private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "ImageClipboard");

    public static string GetOrCreateFilePath(ClipboardImage clipboardImage, List<string> trackedPaths)
    {
        if (clipboardImage.SourcePath is not null && File.Exists(clipboardImage.SourcePath))
        {
            return clipboardImage.SourcePath;
        }

        var folder = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, SanitizeFileName(clipboardImage.SuggestedFileName));
        if (clipboardImage.OriginalBytes is { Length: > 0 })
        {
            File.WriteAllBytes(path, clipboardImage.OriginalBytes);
        }
        else
        {
            SaveFallbackImage(clipboardImage.Image, path, clipboardImage.DragExtension);
        }

        trackedPaths.Add(path);
        return path;
    }

    public static void OpenWithDefaultViewer(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "image.png" : fileName;
    }

    private static void SaveFallbackImage(Image image, string path, string extension)
    {
        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) && ImageAnimator.CanAnimate(image))
        {
            image.Save(path, System.Drawing.Imaging.ImageFormat.Gif);
            return;
        }

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jfif", StringComparison.OrdinalIgnoreCase))
        {
            image.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
            return;
        }

        if (extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            image.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
            return;
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            using var pngStream = new MemoryStream();
            image.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
            pngStream.Position = 0;
            using var decoded = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pngStream);
            using var output = File.Create(path);
            decoded.Save(output, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder());
            return;
        }

        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
