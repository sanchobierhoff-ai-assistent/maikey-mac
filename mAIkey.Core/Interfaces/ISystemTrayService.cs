namespace mAIkey.Core.Interfaces;

/// <summary>
/// System tray / menu bar icon functionality.
/// Windows: NotifyIcon (Windows.Forms)
/// macOS: NSStatusItem (AppKit)
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>
    /// Show the tray/menu bar icon.
    /// </summary>
    void Show(string tooltip);

    /// <summary>
    /// Hide the tray/menu bar icon.
    /// </summary>
    void Hide();

    /// <summary>
    /// Fired when the user double-clicks the tray icon (Windows)
    /// or clicks the menu bar icon (macOS).
    /// </summary>
    event EventHandler? Activated;

    /// <summary>
    /// Fired when the user selects "Exit" from the tray context menu.
    /// </summary>
    event EventHandler? ExitRequested;
}
