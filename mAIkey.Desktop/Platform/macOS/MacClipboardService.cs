using System.Runtime.InteropServices;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// macOS clipboard operations using NSPasteboard and CGEvent keyboard simulation.
/// Cmd+C / Cmd+V simulation requires Accessibility permission.
/// </summary>
public class MacClipboardService : IClipboardService
{
    // CoreGraphics keyboard simulation
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort keycode, bool keyDown);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventSetFlags(IntPtr eventRef, ulong flags);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(int tap, IntPtr eventRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    // AppKit for NSPasteboard (via ObjC runtime)
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    // NSWorkspace for foreground app
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector, IntPtr arg1);

    // Constants
    private const ushort kVK_C = 0x08;       // macOS virtual keycode for 'C'
    private const ushort kVK_V = 0x09;       // macOS virtual keycode for 'V'
    private const ulong kCGEventFlagMaskCommand = 0x100000;
    private const int kCGHIDEventTap = 0;

    public async Task<string?> GetSelectedTextAsync()
    {
        // Save current clipboard content
        string? oldClipboard = GetPasteboardString();

        // Simulate Cmd+C
        await SimulateKeyComboAsync(kVK_C, kCGEventFlagMaskCommand);
        await Task.Delay(100); // Wait for clipboard to update

        // Get the selected text
        string? selectedText = GetPasteboardString();

        // Restore old clipboard
        if (oldClipboard != null)
            SetPasteboardString(oldClipboard);

        return selectedText;
    }

    public Task SetTextAsync(string text)
    {
        SetPasteboardString(text);
        return Task.CompletedTask;
    }

    public async Task ReplaceSelectedTextAsync(string newText)
    {
        // Put replacement text on clipboard
        SetPasteboardString(newText);
        await Task.Delay(50);

        // Simulate Cmd+V
        await SimulateKeyComboAsync(kVK_V, kCGEventFlagMaskCommand);
    }

    public bool ContainsImage()
    {
        // Check NSPasteboard for image types
        // Simplified — check for public.tiff or public.png
        var pb = GetGeneralPasteboard();
        var types = objc_msgSend(pb, sel_registerName("types"));
        // For now, return false — image support can be added later
        return false;
    }

    public Task<List<string>> GetImagesAsBase64Async()
    {
        // Image clipboard support — implement in Phase 3
        return Task.FromResult(new List<string>());
    }

    public IntPtr GetForegroundWindow()
    {
        // Return the frontmost application's process identifier
        var workspace = objc_msgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
        var frontApp = objc_msgSend(workspace, sel_registerName("frontmostApplication"));
        return frontApp;
    }

    public void SetForegroundWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        // Activate the app: [app activateWithOptions: NSApplicationActivateIgnoringOtherApps]
        objc_msgSend_bool(handle, sel_registerName("activateWithOptions:"), (IntPtr)2);
    }

    // ---- Private helpers ----

    private async Task SimulateKeyComboAsync(ushort keycode, ulong modifierFlags)
    {
        // Key down with modifier
        IntPtr keyDown = CGEventCreateKeyboardEvent(IntPtr.Zero, keycode, true);
        CGEventSetFlags(keyDown, modifierFlags);
        CGEventPost(kCGHIDEventTap, keyDown);
        CFRelease(keyDown);

        await Task.Delay(30);

        // Key up
        IntPtr keyUp = CGEventCreateKeyboardEvent(IntPtr.Zero, keycode, false);
        CGEventSetFlags(keyUp, 0);
        CGEventPost(kCGHIDEventTap, keyUp);
        CFRelease(keyUp);
    }

    private IntPtr GetGeneralPasteboard()
    {
        var pbClass = objc_getClass("NSPasteboard");
        return objc_msgSend(pbClass, sel_registerName("generalPasteboard"));
    }

    private string? GetPasteboardString()
    {
        var pb = GetGeneralPasteboard();
        var nsString = objc_msgSend(pb, sel_registerName("stringForType:"),
            CreateNSString("public.utf8-plain-text"));

        if (nsString == IntPtr.Zero) return null;

        var utf8 = objc_msgSend(nsString, sel_registerName("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    private void SetPasteboardString(string text)
    {
        var pb = GetGeneralPasteboard();
        objc_msgSend(pb, sel_registerName("clearContents"));

        var nsString = CreateNSString(text);
        var typeString = CreateNSString("public.utf8-plain-text");

        // [pb setString:nsString forType:typeString]
        objc_msgSend(pb, sel_registerName("setString:forType:"), nsString, typeString);
    }

    private IntPtr CreateNSString(string str)
    {
        var nsStringClass = objc_getClass("NSString");
        var utf8 = Marshal.StringToHGlobalAnsi(str);
        var result = objc_msgSend(nsStringClass, sel_registerName("stringWithUTF8String:"), utf8);
        Marshal.FreeHGlobal(utf8);
        return result;
    }
}
