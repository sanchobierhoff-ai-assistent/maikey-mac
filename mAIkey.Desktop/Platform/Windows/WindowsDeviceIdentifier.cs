using System.Security.Cryptography;
using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Gets unique device identifier from Windows Registry MachineGuid.
/// Ported from frontend/Services/DeviceIdentifier.cs.
/// </summary>
public class WindowsDeviceIdentifier : IDeviceIdentifier
{
    public string GetMachineId()
    {
        try
        {
            var machineGuid = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid",
                "")?.ToString();

            if (!string.IsNullOrEmpty(machineGuid))
                return machineGuid;
        }
        catch { }

        // Fallback
        string input = $"{Environment.MachineName}_{Environment.UserName}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..32];
    }
}
