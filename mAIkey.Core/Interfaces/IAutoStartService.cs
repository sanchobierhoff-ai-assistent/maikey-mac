namespace mAIkey.Core.Interfaces;

/// <summary>
/// Enable/disable app launch at login.
/// Windows: Registry HKCU\...\Run
/// macOS: LaunchAgent plist in ~/Library/LaunchAgents/
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// Enable or disable starting the app at login.
    /// </summary>
    void SetAutoStart(bool enabled);

    /// <summary>
    /// Check if auto-start is currently enabled.
    /// </summary>
    bool IsAutoStartEnabled();
}
