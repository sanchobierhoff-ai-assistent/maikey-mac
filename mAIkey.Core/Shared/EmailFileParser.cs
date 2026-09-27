using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MsgReader;
using MsgReader.Outlook;

namespace mAIkey.Core.Services
{
    public class EmailFileParser
    {
        public static List<ParsedEmail> ParseEmailFiles(string[] filePaths)
        {
            var emails = new List<ParsedEmail>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    var email = ParseEmailFile(filePath);
                    if (email != null)
                    {
                        emails.Add(email);
                    }
                }
                catch (Exception ex)
                {
                    // Skip files that fail to parse
                    System.Diagnostics.Debug.WriteLine($"Failed to parse {filePath}: {ex.Message}");
                }
            }

            return emails;
        }

        private static ParsedEmail? ParseEmailFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".eml":
                    return ParseEmlFile(filePath);
                case ".msg":
                    return ParseMsgFile(filePath);
                default:
                    return null;
            }
        }

        private static ParsedEmail? ParseEmlFile(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var message = MimeMessage.Load(stream);

                var subject = message.Subject ?? "";
                var from = message.From.ToString() ?? "";
                var date = message.Date.ToString("yyyy-MM-dd HH:mm") ?? "";

                // Extract body text
                var bodyText = ExtractTextFromMimeMessage(message);

                if (string.IsNullOrWhiteSpace(bodyText))
                    return null;

                return new ParsedEmail
                {
                    Subject = subject,
                    From = from,
                    Date = date,
                    Body = bodyText,
                    FileName = Path.GetFileName(filePath)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing EML file {filePath}: {ex.Message}");
                return null;
            }
        }

        private static ParsedEmail? ParseMsgFile(string filePath)
        {
            try
            {
                using var msgStream = File.OpenRead(filePath);
                using var msg = new MsgReader.Outlook.Storage.Message(msgStream);

                var subject = msg.Subject ?? "";
                var from = msg.Sender?.Email ?? msg.Sender?.DisplayName ?? "";
                var date = msg.SentOn?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var bodyText = msg.BodyText ?? msg.BodyHtml ?? "";

                // Clean HTML if present
                if (!string.IsNullOrWhiteSpace(msg.BodyHtml) && string.IsNullOrWhiteSpace(msg.BodyText))
                {
                    bodyText = StripHtml(msg.BodyHtml);
                }

                if (string.IsNullOrWhiteSpace(bodyText))
                    return null;

                return new ParsedEmail
                {
                    Subject = subject,
                    From = from,
                    Date = date,
                    Body = bodyText.Trim(),
                    FileName = Path.GetFileName(filePath)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing MSG file {filePath}: {ex.Message}");
                return null;
            }
        }

        private static string ExtractTextFromMimeMessage(MimeMessage message)
        {
            var text = new StringBuilder();

            // Try to get text/plain part first
            var textPart = message.TextBody;
            if (!string.IsNullOrWhiteSpace(textPart))
            {
                return textPart.Trim();
            }

            // Fall back to HTML part
            var htmlPart = message.HtmlBody;
            if (!string.IsNullOrWhiteSpace(htmlPart))
            {
                return StripHtml(htmlPart).Trim();
            }

            // If no body parts, try to extract from body
            if (message.Body is TextPart textBodyPart)
            {
                return textBodyPart.Text?.Trim() ?? "";
            }

            if (message.Body is Multipart multipart)
            {
                foreach (var part in multipart)
                {
                    if (part is TextPart tp)
                    {
                        if (tp.IsPlain)
                            return tp.Text?.Trim() ?? "";
                    }
                }

                // Fall back to first text part (even if HTML)
                foreach (var part in multipart)
                {
                    if (part is TextPart tp)
                    {
                        var content = tp.Text ?? "";
                        if (tp.IsHtml)
                            content = StripHtml(content);
                        return content.Trim();
                    }
                }
            }

            return "";
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            // Remove script and style tags with content
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove HTML tags
            html = Regex.Replace(html, @"<[^>]+>", " ");

            // Decode HTML entities
            html = System.Net.WebUtility.HtmlDecode(html);

            // Clean up whitespace
            html = Regex.Replace(html, @"\s+", " ");
            html = html.Trim();

            return html;
        }
    }

    public class ParsedEmail
    {
        public string Subject { get; set; } = "";
        public string From { get; set; } = "";
        public string Date { get; set; } = "";
        public string Body { get; set; } = "";
        public string FileName { get; set; } = "";

        public string GetPreview(int maxLength = 200)
        {
            if (Body.Length <= maxLength)
                return Body;

            return Body.Substring(0, maxLength) + "...";
        }

        public string GetDisplayInfo()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Subject))
                parts.Add($"Subject: {Subject}");
            if (!string.IsNullOrWhiteSpace(From))
                parts.Add($"From: {From}");
            if (!string.IsNullOrWhiteSpace(Date))
                parts.Add($"Date: {Date}");

            return string.Join(" | ", parts);
        }
    }
}
