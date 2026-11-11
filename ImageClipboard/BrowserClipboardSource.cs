using System.Text.RegularExpressions;

namespace ImageClipboard;

internal static class BrowserClipboardSource
{
    private static readonly Regex SourceUrlRegex = new(
        @"SourceURL:(?<url>.+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ImgSrcRegex = new(
        @"<img[^>]+src\s*=\s*[""'](?<url>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? TryGetSuggestedFileName()
    {
        var url = TryGetImageUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var fileName = GetFileNameFromUrl(url);
        return fileName.Equals("image.png", StringComparison.OrdinalIgnoreCase) ? null : fileName;
    }

    public static string? TryGetImageUrl()
    {
        try
        {
            if (!Clipboard.ContainsData(DataFormats.Html) && !Clipboard.ContainsText())
            {
                return null;
            }

            var html = Clipboard.ContainsData(DataFormats.Html)
                ? Clipboard.GetData(DataFormats.Html) as string
                : null;

            html ??= Clipboard.ContainsText(TextDataFormat.Html)
                ? Clipboard.GetText(TextDataFormat.Html)
                : null;

            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var sourceUrl = SourceUrlRegex.Match(html).Groups["url"].Value.Trim();
            var imgSrc = ImgSrcRegex.Match(html).Groups["url"].Value.Trim();
            var url = ResolveUrl(imgSrc, sourceUrl);
            if (string.IsNullOrWhiteSpace(url) && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) && IsLikelyImageUrl(sourceUri))
            {
                url = sourceUrl;
            }

            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch
        {
            return null;
        }
    }

    public static string GetFileNameFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "image.png";
        }

        var name = Path.GetFileName(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? "image.png" : Uri.UnescapeDataString(name);
    }

    private static string? ResolveUrl(string imgSrc, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(imgSrc))
        {
            return null;
        }

        if (Uri.TryCreate(imgSrc, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, imgSrc, out var resolved))
        {
            return resolved.ToString();
        }

        return null;
    }

    private static bool IsLikelyImageUrl(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }
}
