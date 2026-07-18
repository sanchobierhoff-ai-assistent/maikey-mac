using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// macOS menu bar status item (NSStatusItem).
/// Placeholder implementation — full native implementation requires
/// ObjC runtime calls or a helper library like MonoMac/Xamarin.Mac.
/// For the proof of concept, the app runs without a tray icon on macOS.
/// </summary>
public class MacTrayService : ISystemTrayService
{
    public event EventHandler? Activated;
    public event EventHandler? ExitRequested;

    public void Show(string tooltip)
    {
        // TODO: Implement NSStatusItem via ObjC runtime P/Invoke
        // For now, macOS app runs as a normal window app
        Console.WriteLine($"[MacTray] Would show menu bar icon: {tooltip}");
    }

    public void Hide()
    {
        Console.WriteLine("[MacTray] Would hide menu bar icon");
    }

    public void Dispose() { }
}
