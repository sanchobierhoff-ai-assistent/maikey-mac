namespace mAIkey.Core.Interfaces;

/// <summary>
/// Abstracts global hotkey registration across platforms.
/// Windows: RegisterHotKey/UnregisterHotKey (user32.dll)
/// macOS: CGEventTapCreate (Core Graphics)
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Register a global hotkey that works system-wide.
    /// </summary>
    /// <param name="id">Unique identifier for this hotkey</param>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt/Option, Shift, Win/Cmd)</param>
    /// <param name="key">The main key</param>
    /// <returns>True if registration succeeded</returns>
    bool RegisterHotkey(int id, HotkeyModifiers modifiers, int key);

    /// <summary>
    /// Unregister a previously registered hotkey.
    /// </summary>
    bool UnregisterHotkey(int id);

    /// <summary>
    /// Unregister all hotkeys.
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Fired when a registered hotkey is pressed.
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>
    /// Initialize the hotkey service with the main window handle.
    /// </summary>
    void Initialize(object windowHandle);
}

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8 // Win on Windows, Cmd on macOS
}

public class HotkeyPressedEventArgs : EventArgs
{
    public int HotkeyId { get; }
    public HotkeyPressedEventArgs(int hotkeyId) => HotkeyId = hotkeyId;
}
