using System.Text.RegularExpressions;

namespace mAIkey.Core.Services;

/// <summary>Eén geordend blok uit de selectie (tekst of afbeelding).</summary>
public class ContentBlock
{
    public string Type { get; set; } = "text"; // "text" of "image"
    public string? Text { get; set; }
    public ClipboardImage? Image { get; set; }
}

/// <summary>Resultaat van het uitlezen van de selectie: tekst + afbeeldingen.</summary>
public class RichClipboardContent
{
    public string Text { get; set; } = "";
    public List<ClipboardImage> Images { get; set; } = new();
    public List<ContentBlock> OrderedContent { get; set; } = new();
    public bool HasInterleavedContent =>
        OrderedContent.Any(b => b.Type == "image") && OrderedContent.Any(b => b.Type == "text");
}

/// <summary>Afbeelding uit de selectie: base64-data-URI of een publieke URL.</summary>
public class ClipboardImage
{
    public bool IsPublicUrl { get; set; }
    public string PublicUrl { get; set; } = "";
    public string Base64Data { get; set; } = "";
}

/// <summary>
/// Platform-onafhankelijke logica uit frontend/Services/ClipboardHelper.cs: haalt tekst en
/// afbeeldingen (base64 of publieke URL, in documentvolgorde) uit HTML en herkent
/// losse afbeeldings-URL's.
/// </summary>
public static class RichClipboard
{
    /// <summary>
    /// Bouw de rijke inhoud op uit wat er op het klembord staat. Platte tekst heeft de
    /// voorkeur (behoudt regelafbrekingen); HTML wordt alleen gebruikt als die afbeeldingen bevat.
    /// </summary>
    public static RichClipboardContent Build(string? text, string? html, string? imageDataUri)
    {
        var result = new RichClipboardContent();

        if (!string.IsNullOrEmpty(html) && Regex.IsMatch(html, @"<img\b", RegexOptions.IgnoreCase))
        {
            var fromHtml = ExtractFromHtml(html);
            if (fromHtml.Images.Count > 0)
            {
                result = fromHtml;
                // Gebruik de nette platte tekst als die er is (HTML-strippen verliest regelafbrekingen).
                if (!string.IsNullOrWhiteSpace(text) && !fromHtml.HasInterleavedContent)
                    result.Text = text!;
            }
        }

        if (string.IsNullOrEmpty(result.Text) && result.OrderedContent.Count == 0)
        {
            result.Text = text ?? (html != null ? StripHtmlTags(html) : "");
            if (!string.IsNullOrEmpty(result.Text) && IsPublicImageUrl(result.Text.Trim()))
            {
                result.Images.Add(new ClipboardImage { PublicUrl = result.Text.Trim(), IsPublicUrl = true });
                result.Text = "";
            }
        }

        if (result.Images.Count == 0 && !string.IsNullOrEmpty(imageDataUri))
            result.Images.Add(new ClipboardImage { Base64Data = imageDataUri!, IsPublicUrl = false });

        return result;
    }

    public static RichClipboardContent ExtractFromHtml(string html)
    {
        var result = new RichClipboardContent();
        var imgMatches = Regex.Matches(html, @"<img\b[^>]*>", RegexOptions.IgnoreCase);
        if (imgMatches.Count == 0)
        {
            result.Text = StripHtmlTags(html);
            return result;
        }

        int cursor = 0;
        foreach (Match m in imgMatches)
        {
            if (m.Index > cursor)
            {
                var segment = StripHtmlTags(html.Substring(cursor, m.Index - cursor));
                if (!string.IsNullOrWhiteSpace(segment))
                    result.OrderedContent.Add(new ContentBlock { Type = "text", Text = segment.Trim() });
            }

            var img = ExtractImageFromImgTag(m.Value);
            if (img != null)
            {
                result.OrderedContent.Add(new ContentBlock { Type = "image", Image = img });
                result.Images.Add(img);
            }
            cursor = m.Index + m.Length;
        }

        if (cursor < html.Length)
        {
            var trailing = StripHtmlTags(html.Substring(cursor));
            if (!string.IsNullOrWhiteSpace(trailing))
                result.OrderedContent.Add(new ContentBlock { Type = "text", Text = trailing.Trim() });
        }

        result.Text = string.Join(" ", result.OrderedContent
            .Where(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text))
            .Select(b => b.Text)).Trim();
        return result;
    }

    private static ClipboardImage? ExtractImageFromImgTag(string imgTag)
    {
        var b64 = Regex.Match(imgTag, @"src=[""']data:image/([^;]+);base64,([A-Za-z0-9+/=]+)[""']", RegexOptions.IgnoreCase);
        if (b64.Success)
            return new ClipboardImage { Base64Data = $"data:image/{b64.Groups[1].Value};base64,{b64.Groups[2].Value}" };

        var url = Regex.Match(imgTag, @"src=[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase);
        if (url.Success)
        {
            // Sla trackingpixels / spacer-GIFs over
            if (Regex.IsMatch(imgTag, @"(?:width|height)\s*=\s*[""']?([012])\s*(?:px)?[""']?", RegexOptions.IgnoreCase))
                return null;
            return new ClipboardImage { PublicUrl = url.Groups[1].Value, IsPublicUrl = true };
        }
        return null;
    }

    public static string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"\s+", " ");
        return html.Trim();
    }

    public static bool IsPublicImageUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var path = uri.AbsolutePath.ToLowerInvariant();
        string[] ext = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
        if (ext.Any(e => path.EndsWith(e))) return true;

        var host = uri.Host.ToLowerInvariant();
        string[] hosts = { "imgur.com", "i.imgur.com", "cloudinary.com", "res.cloudinary.com",
            "images.unsplash.com", "unsplash.com", "cdn.discordapp.com", "media.discordapp.net",
            "pbs.twimg.com", "i.redd.it" };
        return hosts.Any(h => host.Contains(h));
    }
}
