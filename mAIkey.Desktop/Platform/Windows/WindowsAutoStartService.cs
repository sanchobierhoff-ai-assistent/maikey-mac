using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Windows auto-start at login via Registry Run key.
/// Ported from frontend/Services/UpdateService.cs.
/// </summary>
public class WindowsAutoStartService : IAutoStartService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "mAIkey";

    public void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch { }
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }
}
