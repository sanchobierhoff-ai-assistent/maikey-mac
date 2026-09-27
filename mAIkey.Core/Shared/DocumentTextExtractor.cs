using System;
using System.IO;
using System.Linq;
using System.Text;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Haalt platte tekst uit documenten (PDF/DOCX/tekst) zodat de Assistant-modus
    /// erover kan redeneren. De tekst reist mee als gewone context naar /ai/agent —
    /// géén backend-wijziging nodig. Kostenbewust: output wordt begrensd (truncatie).
    /// </summary>
    public static class DocumentTextExtractor
    {
        /// <summary>Bestandsextensies die we als document kunnen lezen (zonder punt, lowercase).</summary>
        public static readonly string[] SupportedExtensions =
            { "pdf", "docx", "txt", "md", "csv", "json", "log" };

        /// <summary>OpenFileDialog-filter voor documenten.</summary>
        public const string DialogFilter =
            "Documenten|*.pdf;*.docx;*.txt;*.md;*.csv;*.json;*.log";

        // Begrens de geëxtraheerde tekst (~24k tekens ≈ ~6k tokens). Houdt kosten laag.
        private const int MaxChars = 24000;

        public sealed class Result
        {
            public string FileName { get; set; } = "";
            public string Text { get; set; } = "";
            public bool Truncated { get; set; }
            public bool Ok { get; set; }
            public string? Error { get; set; }   // gelokaliseerde-of-onbekende foutreden
        }

        public static bool IsSupported(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            return SupportedExtensions.Contains(ext);
        }

        /// <summary>Extraheert tekst uit één bestand. Werpt nooit — fouten komen via Result.Error.</summary>
        public static Result Extract(string path)
        {
            var result = new Result { FileName = Path.GetFileName(path) };
            try
            {
                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                string raw = ext switch
                {
                    "pdf" => ExtractPdf(path),
                    "docx" => ExtractDocx(path),
                    "txt" or "md" or "csv" or "json" or "log" => File.ReadAllText(path),
                    _ => throw new NotSupportedException(ext)
                };

                raw = (raw ?? "").Trim();
                if (raw.Length > MaxChars)
                {
                    raw = raw.Substring(0, MaxChars);
                    result.Truncated = true;
                }

                result.Text = raw;
                result.Ok = true;
            }
            catch (NotSupportedException)
            {
                result.Ok = false;
                result.Error = "unsupported";
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.Message;
            }
            return result;
        }

        private static string ExtractPdf(string path)
        {
            var sb = new StringBuilder();
            using var doc = UglyToad.PdfPig.PdfDocument.Open(path);
            foreach (var page in doc.GetPages())
            {
                sb.AppendLine(page.Text);
                if (sb.Length > MaxChars) break; // niet meer lezen dan we bewaren
            }
            return sb.ToString();
        }

        private static string ExtractDocx(string path)
        {
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return "";
            var sb = new StringBuilder();
            foreach (var para in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                sb.AppendLine(para.InnerText);
                if (sb.Length > MaxChars) break;
            }
            return sb.ToString();
        }
    }
}
