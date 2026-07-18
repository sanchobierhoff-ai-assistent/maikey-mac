namespace mAIkey.Core.Interfaces;

/// <summary>
/// Abstracts clipboard operations and keyboard simulation across platforms.
/// Windows: keybd_event (user32.dll), Forms.Clipboard
/// macOS: CGEventCreateKeyboardEvent, NSPasteboard
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Simulate Ctrl+C (Windows) / Cmd+C (macOS) to copy selected text.
    /// </summary>
    Task<string?> GetSelectedTextAsync();

    /// <summary>
    /// Set text on the clipboard.
    /// </summary>
    Task SetTextAsync(string text);

    /// <summary>
    /// Simulate Ctrl+V (Windows) / Cmd+V (macOS) to paste text.
    /// Replaces the currently selected text with the given text.
    /// </summary>
    Task ReplaceSelectedTextAsync(string newText);

    /// <summary>
    /// Check if the clipboard contains an image.
    /// </summary>
    bool ContainsImage();

    /// <summary>
    /// Get images from clipboard as base64-encoded JPEG strings.
    /// </summary>
    Task<List<string>> GetImagesAsBase64Async();

    /// <summary>
    /// Save and restore the current foreground window handle.
    /// Used to return focus after showing mAIkey dialogs.
    /// </summary>
    IntPtr GetForegroundWindow();
    void SetForegroundWindow(IntPtr handle);
}
