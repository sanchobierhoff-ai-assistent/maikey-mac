using System.Runtime.InteropServices;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// macOS global hotkey registration via de Carbon-API RegisterEventHotKey.
///
/// In tegenstelling tot de vorige CGEventTap-implementatie luistert dit NIET naar
/// alle toetsaanslagen, maar registreert het specifieke modifier+toets-combinaties
/// bij het systeem. Daardoor is er GEEN Accessibility-toestemming nodig en werken
/// sneltoetsen direct na installatie — net als op Windows (RegisterHotKey).
///
/// Beperking: elke hotkey vereist minstens één modifier-toets (Cmd/Ctrl/Alt/Shift).
/// Dat past op het bestaande model (HotkeyModifiers + key is altijd modifier+toets).
/// </summary>
public class MacHotkeyService : IHotkeyService
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint eventClass;
        public uint eventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint signature;
        public uint id;
    }

    private delegate int EventHandlerProc(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData);

    [DllImport(Carbon)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(
        IntPtr inTarget, EventHandlerProc inHandler, uint inNumTypes,
        EventTypeSpec[] inList, IntPtr inUserData, out IntPtr outRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(
        uint inHotKeyCode, uint inHotKeyModifiers, EventHotKeyID inHotKeyID,
        IntPtr inTarget, uint inOptions, out IntPtr outRef);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(IntPtr inHotKey);

    [DllImport(Carbon)]
    private static extern int GetEventParameter(
        IntPtr inEvent, uint inName, uint inType, IntPtr outActualType,
        uint inBufferSize, IntPtr outActualSize, out EventHotKeyID outData);

    // Carbon event-constanten
    private const uint kEventClassKeyboard = 0x6B657962;  // 'keyb'
    private const uint kEventHotKeyPressed = 5;
    private const uint kEventParamDirectObject = 0x2D2D2D2D; // '----'
    private const uint typeEventHotKeyID = 0x686B6964;       // 'hkid'
    private const uint kHotKeySignature = 0x6D41496B;        // 'mAIk'

    // Carbon modifier-vlaggen
    private const uint cmdKey = 0x0100;
    private const uint shiftKey = 0x0200;
    private const uint optionKey = 0x0800;
    private const uint controlKey = 0x1000;

    private readonly Dictionary<int, IntPtr> _hotkeyRefs = new();
    private EventHandlerProc? _handlerDelegate;   // levend houden tegen GC
    private IntPtr _eventHandlerRef;
    private bool _handlerInstalled;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(object windowHandle)
    {
        if (_handlerInstalled) return;

        _handlerDelegate = HandleHotKeyEvent;
        var spec = new[]
        {
            new EventTypeSpec { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed }
        };

        int status = InstallEventHandler(
            GetApplicationEventTarget(), _handlerDelegate, 1, spec, IntPtr.Zero, out _eventHandlerRef);

        _handlerInstalled = status == 0;
        if (!_handlerInstalled)
            Console.Error.WriteLine($"⚠️ mAIkey: InstallEventHandler mislukt (status {status}).");
    }

    private int HandleHotKeyEvent(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData)
    {
        int status = GetEventParameter(
            inEvent, kEventParamDirectObject, typeEventHotKeyID,
            IntPtr.Zero, (uint)Marshal.SizeOf<EventHotKeyID>(), IntPtr.Zero, out EventHotKeyID hkId);

        if (status == 0 && hkId.signature == kHotKeySignature)
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs((int)hkId.id));

        return 0; // noErr
    }

    public bool RegisterHotkey(int id, HotkeyModifiers modifiers, int key)
    {
        if (!_handlerInstalled)
            Initialize(IntPtr.Zero);

        uint macKeyCode = WpfKeyToMacKeyCode(key);
        if (macKeyCode == uint.MaxValue)
            return false; // niet-ondersteunde toets

        uint macMods = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl)) macMods |= controlKey;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) macMods |= optionKey;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) macMods |= shiftKey;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) macMods |= cmdKey;

        // Carbon vereist minstens één modifier
        if (macMods == 0) return false;

        UnregisterHotkey(id); // vervang bestaande registratie met dit id

        var hkId = new EventHotKeyID { signature = kHotKeySignature, id = (uint)id };
        int status = RegisterEventHotKey(
            macKeyCode, macMods, hkId, GetApplicationEventTarget(), 0, out IntPtr hkRef);

        if (status != 0 || hkRef == IntPtr.Zero)
            return false;

        _hotkeyRefs[id] = hkRef;
        return true;
    }

    public bool UnregisterHotkey(int id)
    {
        if (_hotkeyRefs.TryGetValue(id, out IntPtr hkRef))
        {
            UnregisterEventHotKey(hkRef);
            _hotkeyRefs.Remove(id);
            return true;
        }
        return false;
    }

    public void UnregisterAll()
    {
        foreach (var hkRef in _hotkeyRefs.Values)
            UnregisterEventHotKey(hkRef);
        _hotkeyRefs.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
    }

    /// <summary>
    /// Zet een toetswaarde uit de config om naar de macOS Carbon-toetscode (kVK_*).
    /// De config gebruikt het gedeelde WPF/Avalonia Key-formaat (zie HotkeyKeys), zodat
    /// sneltoetsen via Cloud Sync uitwisselbaar zijn met Windows. Retourneert uint.MaxValue
    /// als de toets niet ondersteund wordt.
    /// </summary>
    private static uint WpfKeyToMacKeyCode(int key)
    {
        if (key >= HotkeyKeys.A && key <= HotkeyKeys.Z)
            return LetterKeyCodes[key - HotkeyKeys.A];
        if (key >= HotkeyKeys.D0 && key <= HotkeyKeys.D9)
            return DigitKeyCodes[key - HotkeyKeys.D0];
        if (key >= HotkeyKeys.F1 && key <= HotkeyKeys.F12)
            return FunctionKeyCodes[key - HotkeyKeys.F1];
        if (key >= HotkeyKeys.NumPad0 && key <= HotkeyKeys.NumPad9)
            return KeypadKeyCodes[key - HotkeyKeys.NumPad0];

        return key switch
        {
            HotkeyKeys.Space => 49,
            HotkeyKeys.Enter => 36,
            HotkeyKeys.Escape => 53,
            HotkeyKeys.Tab => 48,
            HotkeyKeys.Back => 51,
            HotkeyKeys.Left => 123,
            HotkeyKeys.Right => 124,
            HotkeyKeys.Down => 125,
            HotkeyKeys.Up => 126,
            HotkeyKeys.OemComma => 43,
            HotkeyKeys.OemPeriod => 47,
            HotkeyKeys.OemQuestion => 44,
            HotkeyKeys.OemSemicolon => 41,
            HotkeyKeys.OemQuotes => 39,
            HotkeyKeys.OemOpenBrackets => 33,
            HotkeyKeys.OemCloseBrackets => 30,
            HotkeyKeys.OemPipe => 42,
            HotkeyKeys.OemTilde => 50,
            HotkeyKeys.OemMinus => 27,
            HotkeyKeys.OemPlus => 24,
            _ => uint.MaxValue
        };
    }

    // kVK_ANSI_A..Z, geïndexeerd op (key - A)
    private static readonly uint[] LetterKeyCodes =
    {
        0,  11, 8,  2,  14, 3,  5,  4,  34, 38, 40, 37, 46, // A-M
        45, 31, 35, 12, 15, 1,  17, 32, 9,  13, 7,  16, 6   // N-Z
    };

    // kVK_ANSI_0..9, geïndexeerd op (key - D0)
    private static readonly uint[] DigitKeyCodes =
    {
        29, 18, 19, 20, 21, 23, 22, 26, 28, 25 // 0-9
    };

    // kVK_F1..F12, geïndexeerd op (key - F1)
    private static readonly uint[] FunctionKeyCodes =
    {
        122, 120, 99, 118, 96, 97, 98, 100, 101, 109, 103, 111 // F1-F12
    };

    // kVK_ANSI_Keypad0..9
    private static readonly uint[] KeypadKeyCodes =
    {
        82, 83, 84, 85, 86, 87, 88, 89, 91, 92 // 0-9
    };
}
