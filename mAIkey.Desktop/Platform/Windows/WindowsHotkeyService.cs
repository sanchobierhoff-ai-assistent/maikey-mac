using System.Runtime.InteropServices;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Windows global hotkey registration via RegisterHotKey/UnregisterHotKey (user32.dll).
/// Ported from frontend/Services/HotkeyManager.cs.
/// </summary>
public class WindowsHotkeyService : IHotkeyService
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private IntPtr _windowHandle;
    private readonly List<int> _registeredIds = new();

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(object windowHandle)
    {
        _windowHandle = (IntPtr)windowHandle;
        // TODO: Hook into Avalonia's window message loop on Windows
        // to receive WM_HOTKEY messages and fire HotkeyPressed
    }

    public bool RegisterHotkey(int id, HotkeyModifiers modifiers, int key)
    {
        uint winMods = MOD_NOREPEAT;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) winMods |= MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl)) winMods |= MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) winMods |= MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) winMods |= MOD_WIN;

        bool result = RegisterHotKey(_windowHandle, id, winMods, (uint)key);
        if (result) _registeredIds.Add(id);
        return result;
    }

    public bool UnregisterHotkey(int id)
    {
        _registeredIds.Remove(id);
        return UnregisterHotKey(_windowHandle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredIds.ToList())
            UnregisterHotKey(_windowHandle, id);
        _registeredIds.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
    }
}
