namespace mAIkey.Core.Services;

/// <summary>
/// Sneltoets-codes zoals de Windows-app ze opslaat: HotkeyConfig.Key is een waarde uit de
/// WPF Key-enum (Avalonia's Key-enum heeft exact dezelfde getallen), ModifierKeys volgt
/// WPF ModifierKeys (Alt=1, Ctrl=2, Shift=4, Win/Cmd=8). Door hetzelfde formaat te
/// gebruiken werken via Cloud Sync of export/import overgezette sneltoetsen op beide
/// platformen.
/// </summary>
public static class HotkeyKeys
{
    public const string FormatWpf = "wpf";

    // Enkele vaste WPF/Avalonia Key-waarden
    public const int Back = 2, Tab = 3, Enter = 6, Escape = 13, Space = 18;
    public const int Left = 23, Up = 24, Right = 25, Down = 26;
    public const int D0 = 34, D9 = 43, A = 44, Z = 69;
    public const int NumPad0 = 74, NumPad9 = 83, F1 = 90, F12 = 101;
    public const int OemSemicolon = 140, OemPlus = 141, OemComma = 142, OemMinus = 143,
                     OemPeriod = 144, OemQuestion = 145, OemTilde = 146,
                     OemOpenBrackets = 149, OemPipe = 150, OemCloseBrackets = 151, OemQuotes = 152;

    /// <summary>Zet een Windows virtual-key-code (oud Mac-formaat) om naar de WPF Key-waarde.</summary>
    public static int FromWindowsVirtualKey(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return A + (vk - 0x41);
        if (vk >= 0x30 && vk <= 0x39) return D0 + (vk - 0x30);
        if (vk >= 0x70 && vk <= 0x7B) return F1 + (vk - 0x70);
        return vk switch
        {
            0x20 => Space, 0x0D => Enter, 0x1B => Escape, 0x09 => Tab, 0x08 => Back,
            0x25 => Left, 0x26 => Up, 0x27 => Right, 0x28 => Down,
            0xBA => OemSemicolon, 0xBB => OemPlus, 0xBC => OemComma, 0xBD => OemMinus,
            0xBE => OemPeriod, 0xBF => OemQuestion, 0xC0 => OemTilde, 0xDB => OemOpenBrackets,
            0xDC => OemPipe, 0xDD => OemCloseBrackets, 0xDE => OemQuotes,
            _ => vk
        };
    }

    /// <summary>Leesbare naam van een toets ("1", "A", "Spatie", "F5").</summary>
    public static string KeyName(int key)
    {
        if (key >= D0 && key <= D9) return ((char)('0' + key - D0)).ToString();
        if (key >= NumPad0 && key <= NumPad9) return ((char)('0' + key - NumPad0)).ToString();
        if (key >= A && key <= Z) return ((char)('A' + key - A)).ToString();
        if (key >= F1 && key <= F1 + 23) return "F" + (key - F1 + 1);
        return key switch
        {
            Space => L.CurrentLanguage == "en" ? "Space" : L.CurrentLanguage == "de" ? "Leertaste" : "Spatie",
            Enter => "↩", Escape => "Esc", Tab => "⇥", Back => "⌫",
            Left => "←", Up => "↑", Right => "→", Down => "↓",
            OemSemicolon => ";", OemPlus => "=", OemComma => ",", OemMinus => "-",
            OemPeriod => ".", OemQuestion => "/", OemTilde => "`", OemOpenBrackets => "[",
            OemPipe => "\\", OemCloseBrackets => "]", OemQuotes => "'",
            0 => "",
            _ => "#" + key
        };
    }

    /// <summary>
    /// Mac-weergave van een combinatie, in de volgorde die macOS zelf gebruikt:
    /// ⌃ (Control) ⌥ (Option) ⇧ (Shift) ⌘ (Command), bv. "⌃⌥1".
    /// </summary>
    public static string Format(int modifiers, int key)
    {
        var s = "";
        if ((modifiers & 2) != 0) s += "⌃";
        if ((modifiers & 1) != 0) s += "⌥";
        if ((modifiers & 4) != 0) s += "⇧";
        if ((modifiers & 8) != 0) s += "⌘";
        return s + KeyName(key);
    }
}
