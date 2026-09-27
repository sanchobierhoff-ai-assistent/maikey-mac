using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Beeldhulpjes: comprimeren naar JPEG (max 1280px, zoals ClipboardHelper op Windows),
/// thumbnails maken en een schermopname maken met de ingebouwde macOS-tool.
/// </summary>
public static class ImageHelper
{
    public static readonly string[] ImageExtensions = { "png", "jpg", "jpeg", "gif", "bmp", "webp", "heic", "tiff" };

    /// <summary>Comprimeer ruwe afbeeldingsbytes naar een JPEG-data-URI (max 1280px).</summary>
    public static string? ToJpegDataUri(byte[] bytes, int maxSize = 1280, int quality = 85)
    {
        try
        {
            using var src = SKBitmap.Decode(bytes);
            if (src == null) return null;

            SKBitmap bmp = src;
            SKBitmap? resized = null;
            if (src.Width > maxSize || src.Height > maxSize)
            {
                double scale = Math.Min((double)maxSize / src.Width, (double)maxSize / src.Height);
                var info = new SKImageInfo((int)(src.Width * scale), (int)(src.Height * scale));
                resized = src.Resize(info, SKFilterQuality.Medium);
                if (resized != null) bmp = resized;
            }

            // JPEG heeft geen transparantie: teken op een witte achtergrond.
            using var surface = SKSurface.Create(new SKImageInfo(bmp.Width, bmp.Height));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.DrawBitmap(bmp, 0, 0);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            resized?.Dispose();
            return "data:image/jpeg;base64," + Convert.ToBase64String(data.ToArray());
        }
        catch { return null; }
    }

    /// <summary>Lees een afbeeldingsbestand en comprimeer het naar een data-URI.</summary>
    public static string? FileToDataUri(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (!ImageExtensions.Contains(ext)) return null;
            return ToJpegDataUri(File.ReadAllBytes(path));
        }
        catch { return null; }
    }

    /// <summary>Kleine thumbnail uit een data-URI (voor bijlage-voorbeelden).</summary>
    public static Bitmap? ThumbFromDataUri(string dataUri, int width = 128)
    {
        try
        {
            var comma = dataUri.IndexOf(',');
            var b64 = comma >= 0 ? dataUri[(comma + 1)..] : dataUri;
            using var ms = new MemoryStream(Convert.FromBase64String(b64));
            return Bitmap.DecodeToWidth(ms, width);
        }
        catch { return null; }
    }

    public static byte[]? DataUriToBytes(string dataUri)
    {
        try
        {
            var comma = dataUri.IndexOf(',');
            return Convert.FromBase64String(comma >= 0 ? dataUri[(comma + 1)..] : dataUri);
        }
        catch { return null; }
    }

    /// <summary>
    /// Laat de gebruiker een schermgebied selecteren met de ingebouwde macOS-schermopname
    /// (zelfde kruisdraad als Cmd+Shift+4; Esc annuleert). Geeft een JPEG-data-URI terug,
    /// of null bij annuleren. Vereist eenmalig de macOS-toestemming "Schermopname".
    /// </summary>
    public static async Task<string?> CaptureScreenRegionAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"maikey-capture-{Guid.NewGuid():N}.png");
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                ArgumentList = { "-i", "-x", "-t", "png", file },
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p == null) return null;
            await p.WaitForExitAsync();

            if (!File.Exists(file) || new FileInfo(file).Length == 0) return null; // geannuleerd
            var bytes = await File.ReadAllBytesAsync(file);
            return ToJpegDataUri(bytes, 1600);
        }
        catch { return null; }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
