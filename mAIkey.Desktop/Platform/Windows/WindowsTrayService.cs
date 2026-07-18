using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Windows system tray icon using Windows.Forms NotifyIcon.
/// Ported from frontend/MainWindow.xaml.cs tray setup.
/// </summary>
public class WindowsTrayService : ISystemTrayService
{
    // TODO: Implement using System.Windows.Forms.NotifyIcon
    // This requires UseWindowsForms=true in the .csproj (Windows only)

    public event EventHandler? Activated;
    public event EventHandler? ExitRequested;

    public void Show(string tooltip)
    {
        // TODO: Create NotifyIcon with context menu
    }

    public void Hide()
    {
        // TODO: Hide NotifyIcon
    }

    public void Dispose() { }
}
