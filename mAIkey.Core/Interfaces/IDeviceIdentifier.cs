namespace mAIkey.Core.Interfaces;

/// <summary>
/// Provides a unique device identifier for account binding.
/// Windows: Registry MachineGuid
/// macOS: IOKit IOPlatformSerialNumber
/// </summary>
public interface IDeviceIdentifier
{
    /// <summary>
    /// Get a stable, unique identifier for this device.
    /// </summary>
    string GetMachineId();
}
