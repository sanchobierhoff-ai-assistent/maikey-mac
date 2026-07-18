using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// Gets a unique device identifier on macOS using the hardware UUID
/// from IOKit (equivalent to Windows Registry MachineGuid).
/// </summary>
public class MacDeviceIdentifier : IDeviceIdentifier
{
    public string GetMachineId()
    {
        try
        {
            // Get hardware UUID via ioreg command
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/ioreg",
                    Arguments = "-rd1 -c IOPlatformExpertDevice",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse IOPlatformUUID from output
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("IOPlatformUUID"))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                    {
                        return parts[1].Trim().Trim('"').Trim();
                    }
                }
            }
        }
        catch { }

        // Fallback: hash machine name + user name (same as Windows fallback)
        return GenerateFallbackId();
    }

    private static string GenerateFallbackId()
    {
        string input = $"{Environment.MachineName}_{Environment.UserName}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..32];
    }
}
