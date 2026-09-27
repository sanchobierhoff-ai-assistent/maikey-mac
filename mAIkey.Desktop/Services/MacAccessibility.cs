using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Services;

/// <summary>
/// macOS-toestemmingen die mAIkey nodig heeft:
///  • Toegankelijkheid — om Cmd+C/Cmd+V in andere apps te simuleren (selectie pakken en
///    het resultaat terugplakken). De sneltoetsen zelf werken zonder deze toestemming.
///  • Schermopname — alleen voor de screenshot-functie.
/// Gebruikt de eenvoudige CoreGraphics-aanroepen (CGPreflight…/CGRequest…), die ook de
/// systeemvraag tonen; de variant met een CFDictionary crashte eerder in de praktijk.
/// </summary>
public static class MacAccessibility
{
    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(AppServices)] private static extern bool AXIsProcessTrusted();
    [DllImport(CoreGraphics)] private static extern bool CGRequestPostEventAccess();
    [DllImport(CoreGraphics)] private static extern bool CGPreflightScreenCaptureAccess();
    [DllImport(CoreGraphics)] private static extern bool CGRequestScreenCaptureAccess();

    private static bool IsMac => OperatingSystem.IsMacOS();
    private static bool _requested;

    /// <summary>Heeft mAIkey Toegankelijkheids-toestemming?</summary>
    public static bool IsTrusted()
    {
        if (!IsMac) return true;
        try { return AXIsProcessTrusted(); }
        catch { return false; }
    }

    /// <summary>
    /// True als de toestemming er is. Zo niet, dan vraagt macOS (één keer per sessie) om
    /// mAIkey toe te voegen aan Toegankelijkheid.
    /// </summary>
    public static bool EnsureTrusted()
    {
        if (IsTrusted()) return true;
        if (!_requested)
        {
            _requested = true;
            try { CGRequestPostEventAccess(); } catch { }
        }
        return false;
    }

    /// <summary>Leg uit waarom de toestemming nodig is en open desgewenst de instellingen.</summary>
    public static async Task ExplainAsync()
    {
        if (await MkDialog.ShowConfirm(
                L.T("MacPerm_Accessibility_Title"),
                L.T("MacPerm_Accessibility_Body"),
                null,
                L.T("MacPerm_OpenSettings"),
                L.T("Common_Close")))
            OpenAccessibilitySettings();
    }

    public static void OpenAccessibilitySettings() =>
        Open("x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility");

    public static bool HasScreenCapture()
    {
        if (!IsMac) return true;
        try { return CGPreflightScreenCaptureAccess(); }
        catch { return true; }
    }

    public static void RequestScreenCapture()
    {
        if (!IsMac) return;
        try { CGRequestScreenCaptureAccess(); } catch { }
    }

    /// <summary>Vraag (eenmalig) om Schermopname-toestemming en leg uit waar die staat als hij ontbreekt.</summary>
    public static async Task EnsureScreenCaptureAsync(Avalonia.Controls.Window? owner = null)
    {
        if (HasScreenCapture()) return;
        RequestScreenCapture();
        if (HasScreenCapture()) return;
        if (await MkDialog.ShowConfirm(
                Loc.T("MacPerm_Screen_Title", "Toestemming voor schermopnames"),
                Loc.T("MacPerm_Screen_Body", "Om screenshots aan de assistent toe te voegen heeft mAIkey toestemming nodig voor Schermopname. Zet mAIkey aan in Systeeminstellingen → Privacy en beveiliging → Schermopname en start mAIkey daarna opnieuw."),
                owner, L.T("MacPerm_OpenSettings"), L.T("Common_Close")))
            OpenScreenCaptureSettings();
    }

    public static void OpenScreenCaptureSettings() =>
        Open("x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "open", Arguments = url, UseShellExecute = false }); }
        catch { /* nooit crashen op het openen van instellingen */ }
    }
}
