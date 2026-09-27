using System.Runtime.InteropServices;
using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// macOS-klembord via NSPasteboard (ObjC-runtime) en Cmd+C/Cmd+V-simulatie via CGEvent.
/// De toetssimulatie vereist macOS Toegankelijkheids-toestemming.
/// </summary>
public class MacClipboardService : IClipboardService
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ObjC = "/usr/lib/libobjc.dylib";

    [DllImport(CoreGraphics)] private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort keycode, bool keyDown);
    [DllImport(CoreGraphics)] private static extern void CGEventSetFlags(IntPtr eventRef, ulong flags);
    [DllImport(CoreGraphics)] private static extern void CGEventPost(int tap, IntPtr eventRef);
    [DllImport(CoreGraphics)] private static extern ulong CGEventSourceFlagsState(int stateId);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr cf);

    [DllImport(ObjC)] private static extern IntPtr objc_getClass(string className);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName(string selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, ulong arg1, IntPtr arg2);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendBytes(IntPtr receiver, IntPtr selector, byte[] bytes, ulong length);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern long SendLong(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern bool SendBool(IntPtr receiver, IntPtr selector, ulong arg1);

    private const ushort kVK_C = 0x08;
    private const ushort kVK_V = 0x09;
    private const ulong kCGEventFlagMaskCommand = 0x100000;
    private const ulong ModifierMask = 0x00FF0000; // shift/ctrl/alt/cmd/fn
    private const int kCGHIDEventTap = 0;
    private const int kCGEventSourceStateHIDSystemState = 1;

    private static IntPtr Sel(string name) => sel_registerName(name);

    // ─────────────────────────────── Selectie kopiëren ───────────────────────────────

    public async Task<bool> CopySelectionAsync()
    {
        await WaitForModifierReleaseAsync();

        long before = ChangeCount();
        await SimulateKeyComboAsync(kVK_C, kCGEventFlagMaskCommand);

        // Wacht tot het klembord verandert (max ~600 ms); trage apps (Word, Electron) hebben tijd nodig.
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(30);
            if (ChangeCount() != before) { await Task.Delay(40); return true; }
        }
        return false;
    }

    public async Task<string?> GetSelectedTextAsync()
    {
        string? previous = GetText();
        bool copied = await CopySelectionAsync();
        if (!copied) return null;

        string? selected = GetText();

        // Herstel de vorige klembordtekst zodat de gebruiker er niets van merkt.
        if (previous != null) SetPasteboardString(previous);
        return selected;
    }

    /// <summary>
    /// Wacht tot de gebruiker de sneltoets-modifiers heeft losgelaten, anders ontvangt de
    /// doel-app bv. Ctrl+Option+Cmd+C in plaats van Cmd+C.
    /// </summary>
    private static async Task WaitForModifierReleaseAsync()
    {
        for (int i = 0; i < 25; i++)
        {
            try
            {
                if ((CGEventSourceFlagsState(kCGEventSourceStateHIDSystemState) & ModifierMask) == 0) return;
            }
            catch { return; }
            await Task.Delay(20);
        }
    }

    // ─────────────────────────────── Lezen ───────────────────────────────

    public string? GetText() => GetPasteboardString("public.utf8-plain-text");

    public string? GetHtml() => GetPasteboardString("public.html");

    public byte[]? GetImagePng()
    {
        try
        {
            var pb = GetGeneralPasteboard();
            var data = Send(pb, Sel("dataForType:"), NSString("public.png"));
            if (data == IntPtr.Zero)
            {
                var tiff = Send(pb, Sel("dataForType:"), NSString("public.tiff"));
                if (tiff == IntPtr.Zero) return null;

                // TIFF → PNG via NSBitmapImageRep (NSBitmapImageFileTypePNG = 4)
                var rep = Send(objc_getClass("NSBitmapImageRep"), Sel("imageRepWithData:"), tiff);
                if (rep == IntPtr.Zero) return null;
                var props = Send(objc_getClass("NSDictionary"), Sel("dictionary"));
                data = Send(rep, Sel("representationUsingType:properties:"), 4UL, props);
                if (data == IntPtr.Zero) return null;
            }
            return NSDataToBytes(data);
        }
        catch { return null; }
    }

    // ─────────────────────────────── Schrijven / plakken ───────────────────────────────

    public Task SetTextAsync(string text)
    {
        SetPasteboardString(text);
        return Task.CompletedTask;
    }

    public async Task ReplaceSelectedTextAsync(string newText)
    {
        SetPasteboardString(newText);
        await Task.Delay(50);
        await WaitForModifierReleaseAsync();
        await SimulateKeyComboAsync(kVK_V, kCGEventFlagMaskCommand);
    }

    public async Task PasteAsync()
    {
        await WaitForModifierReleaseAsync();
        await SimulateKeyComboAsync(kVK_V, kCGEventFlagMaskCommand);
    }

    // ─────────────────────────────── Focus ───────────────────────────────

    public IntPtr GetForegroundWindow()
    {
        try
        {
            var workspace = Send(objc_getClass("NSWorkspace"), Sel("sharedWorkspace"));
            var app = Send(workspace, Sel("frontmostApplication"));
            if (app != IntPtr.Zero) Send(app, Sel("retain"));
            return app;
        }
        catch { return IntPtr.Zero; }
    }

    public void SetForegroundWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        try
        {
            // NSApplicationActivateIgnoringOtherApps = 1 << 1
            SendBool(handle, Sel("activateWithOptions:"), 2);
        }
        catch { }
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    private static async Task SimulateKeyComboAsync(ushort keycode, ulong modifierFlags)
    {
        IntPtr keyDown = CGEventCreateKeyboardEvent(IntPtr.Zero, keycode, true);
        CGEventSetFlags(keyDown, modifierFlags);
        CGEventPost(kCGHIDEventTap, keyDown);
        CFRelease(keyDown);

        await Task.Delay(30);

        IntPtr keyUp = CGEventCreateKeyboardEvent(IntPtr.Zero, keycode, false);
        CGEventSetFlags(keyUp, modifierFlags);
        CGEventPost(kCGHIDEventTap, keyUp);
        CFRelease(keyUp);
    }

    private static IntPtr GetGeneralPasteboard() =>
        Send(objc_getClass("NSPasteboard"), Sel("generalPasteboard"));

    private static long ChangeCount()
    {
        try { return SendLong(GetGeneralPasteboard(), Sel("changeCount")); }
        catch { return 0; }
    }

    private static string? GetPasteboardString(string type)
    {
        try
        {
            var nsString = Send(GetGeneralPasteboard(), Sel("stringForType:"), NSString(type));
            if (nsString == IntPtr.Zero) return null;
            var utf8 = Send(nsString, Sel("UTF8String"));
            return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
        }
        catch { return null; }
    }

    private static void SetPasteboardString(string text)
    {
        var pb = GetGeneralPasteboard();
        Send(pb, Sel("clearContents"));
        Send(pb, Sel("setString:forType:"), NSString(text), NSString("public.utf8-plain-text"));
    }

    /// <summary>Maak een NSString uit een .NET-string (UTF-8, zodat é/ë/emoji heel blijven).</summary>
    private static IntPtr NSString(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        var alloc = Send(objc_getClass("NSString"), Sel("alloc"));
        // initWithBytes:length:encoding: (NSUTF8StringEncoding = 4)
        var result = InitWithBytes(alloc, Sel("initWithBytes:length:encoding:"), bytes, (ulong)bytes.Length, 4);
        if (result != IntPtr.Zero) Send(result, Sel("autorelease"));
        return result;
    }

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr InitWithBytes(IntPtr receiver, IntPtr selector, byte[] bytes, ulong length, ulong encoding);

    private static byte[]? NSDataToBytes(IntPtr data)
    {
        long length = SendLong(data, Sel("length"));
        if (length <= 0) return null;
        var ptr = Send(data, Sel("bytes"));
        if (ptr == IntPtr.Zero) return null;
        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, (int)length);
        return bytes;
    }
}
