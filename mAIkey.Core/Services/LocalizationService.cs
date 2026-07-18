using System.Reflection;
using System.Text;

namespace mAIkey.Core.Services;

public static class L
{
    private static Dictionary<string, string> _strings = new();

    public static string CurrentLanguage { get; private set; } = "nl";

    public static void Apply(string languageCode, Assembly? assembly = null)
    {
        var lang = languageCode switch
        {
            "en" => "en",
            "de" => "de",
            _ => "nl"
        };
        CurrentLanguage = lang;

        var asm = assembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        // Try multiple resource name patterns
        var patterns = new[]
        {
            $"mAIkey.Desktop.Resources.Localization.{lang}.txt",
            $"ai_assistant_wpf.Resources.Localization.{lang}.txt",
            $"mAIkey.Core.Resources.Localization.{lang}.txt"
        };

        foreach (var resourceName in patterns)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                _strings = ParseStream(stream);
                return;
            }
        }

        // Try to find any matching resource
        var names = asm.GetManifestResourceNames();
        var match = names.FirstOrDefault(n => n.Contains($"{lang}.txt") && n.Contains("Localization"));
        if (match != null)
        {
            using var stream = asm.GetManifestResourceStream(match);
            if (stream != null)
            {
                _strings = ParseStream(stream);
                return;
            }
        }
    }

    private static Dictionary<string, string> ParseStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith('#') && line.Contains('='))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim().Length > 0)
            .ToDictionary(
                parts => parts[0].Trim(),
                parts => parts[1].Trim().Replace("\\n", "\n"),
                StringComparer.Ordinal);
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    public static string Tf(string key, params object[] args) =>
        string.Format(T(key), args);
}
