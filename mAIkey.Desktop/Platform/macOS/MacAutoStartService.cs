using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// macOS auto-start at login using LaunchAgent plist files.
/// Creates/removes ~/Library/LaunchAgents/nl.maikey.app.plist
/// </summary>
public class MacAutoStartService : IAutoStartService
{
    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "nl.maikey.app.plist");

    public void SetAutoStart(bool enabled)
    {
        if (enabled)
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            string plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>nl.maikey.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            File.WriteAllText(PlistPath, plistContent);
        }
        else
        {
            try { File.Delete(PlistPath); } catch { }
        }
    }

    public bool IsAutoStartEnabled()
    {
        return File.Exists(PlistPath);
    }
}
