using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Controleert de macOS Toegankelijkheids-toestemming. Die is nodig om Cmd+C/Cmd+V
/// in andere apps te simuleren (de selectie pakken en het resultaat terugplakken).
/// De hotkey-registratie zelf heeft dit NIET nodig.
///
/// We gebruiken bewust alleen de parameterloze AXIsProcessTrusted() (een simpele
/// bool-aanroep) en openen zo nodig het instellingen-paneel. De variant met opties
/// (AXIsProcessTrustedWithOptions) vereist een handmatig opgebouwde CFDictionary en
/// crashte in de praktijk — die vermijden we.
/// </summary>
public static class MacAccessibility
{
    private const string AppServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrusted();

    private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static bool _settingsOpened;

    /// <summary>Is de app al vertrouwd (Toegankelijkheid aan)?</summary>
    public static bool IsTrusted()
    {
        if (!IsMac) return true;
        try { return AXIsProcessTrusted(); }
        catch { return false; }
    }

    /// <summary>
    /// True als de toestemming er is. Zo niet, dan wordt (één keer) het
    /// Toegankelijkheids-paneel geopend zodat de gebruiker mAIkey kan aanzetten.
    /// </summary>
    public static bool EnsureTrusted()
    {
        if (IsTrusted()) return true;

        if (!_settingsOpened)
        {
            _settingsOpened = true;
            OpenAccessibilitySettings();
        }
        return false;
    }

    private static void OpenAccessibilitySettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                UseShellExecute = false
            });
        }
        catch { /* nooit crashen op het openen van instellingen */ }
    }
}