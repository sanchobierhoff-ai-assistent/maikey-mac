using System;
using System.Diagnostics;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Korte, niet-blokkerende meldingen (Windows: ballon-tip bij het tray-icoon).
/// Op macOS via het Berichtencentrum (osascript "display notification").
/// </summary>
public static class Notifier
{
    public static void Show(string message, string? title = null)
    {
        title ??= L.T("Tray_Title");
        if (!OperatingSystem.IsMacOS()) return;
        try
        {
            var script = $"display notification \"{Escape(message)}\" with title \"{Escape(title)}\"";
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList = { "-e", script },
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { /* melding mag nooit iets breken */ }
    }

    private static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");

    /// <summary>Speel het systeemgeluid af (instelling "Geluid bij voltooien").</summary>
    public static void PlayCompleteSound()
    {
        if (!OperatingSystem.IsMacOS()) return;
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/afplay",
                ArgumentList = { "/System/Library/Sounds/Glass.aiff" },
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }
}
