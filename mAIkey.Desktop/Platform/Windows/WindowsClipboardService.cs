using System.Runtime.InteropServices;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Windows clipboard operations and keyboard simulation via user32.dll.
/// Ported from frontend/Services/ClipboardHelper.cs.
/// </summary>
public class WindowsClipboardService : IClipboardService
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public async Task<string?> GetSelectedTextAsync()
    {
        // Simulate Ctrl+C
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        await Task.Delay(100);

        // Read clipboard — requires STA thread on Windows
        string? text = null;
        var thread = new Thread(() =>
        {
            text = System.Windows.Forms.Clipboard.GetText();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return text;
    }

    public Task SetTextAsync(string text)
    {
        var thread = new Thread(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return Task.CompletedTask;
    }

    public async Task ReplaceSelectedTextAsync(string newText)
    {
        await SetTextAsync(newText);
        await Task.Delay(50);

        // Simulate Ctrl+V
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public bool ContainsImage()
    {
        bool result = false;
        var thread = new Thread(() =>
        {
            result = System.Windows.Forms.Clipboard.ContainsImage();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    public Task<List<string>> GetImagesAsBase64Async()
    {
        // TODO: Port image extraction from ClipboardHelper.cs
        return Task.FromResult(new List<string>());
    }

    IntPtr IClipboardService.GetForegroundWindow() => GetForegroundWindow();

    void IClipboardService.SetForegroundWindow(IntPtr handle) => SetForegroundWindow(handle);
}
