using System;
using System.Runtime.InteropServices;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Controleert (en vraagt) de macOS Toegankelijkheids-toestemming. Die is nodig
/// om Cmd+C/Cmd+V in andere apps te simuleren (het pakken van de selectie en het
/// terugplakken van het resultaat). De hotkey-registratie zelf heeft dit NIET nodig.
/// </summary>
public static class MacAccessibility
{
    private const string AppServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint kCFStringEncodingUTF8 = 0x08000100;

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator, IntPtr[] keys, IntPtr[] values, long numValues,
        IntPtr keyCallBacks, IntPtr valueCallBacks);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);

    private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>Is de app al vertrouwd (Toegankelijkheid aan)?</summary>
    public static bool IsTrusted()
    {
        if (!IsMac) return true;
        try { return AXIsProcessTrusted(); }
        catch { return false; }
    }

    /// <summary>
    /// Controleert de toestemming en toont, indien nog niet verleend, de
    /// systeemprompt (voegt mAIkey toe aan de Toegankelijkheids-lijst en biedt een
    /// knop naar de instellingen). Retourneert true als al vertrouwd.
    /// </summary>
    public static bool EnsureTrusted()
    {
        if (!IsMac) return true;
        try
        {
            if (AXIsProcessTrusted()) return true;

            // options = { kAXTrustedCheckOptionPrompt: true }
            IntPtr key = CFStringCreateWithCString(IntPtr.Zero, "AXTrustedCheckOptionPrompt", kCFStringEncodingUTF8);
            IntPtr val = GetCFBooleanTrue();
            IntPtr dict = CFDictionaryCreate(IntPtr.Zero, new[] { key }, new[] { val }, 1, IntPtr.Zero, IntPtr.Zero);

            bool trusted = AXIsProcessTrustedWithOptions(dict);

            if (dict != IntPtr.Zero) CFRelease(dict);
            if (key != IntPtr.Zero) CFRelease(key);
            return trusted;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetCFBooleanTrue()
    {
        var lib = NativeLibrary.Load(CoreFoundation);
        return Marshal.ReadIntPtr(NativeLibrary.GetExport(lib, "kCFBooleanTrue"));
    }
}