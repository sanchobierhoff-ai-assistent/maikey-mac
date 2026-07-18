using System.Runtime.InteropServices;
using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform;

public static class PlatformServiceFactory
{
    public static PlatformServices Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new PlatformServices
            {
                HotkeyService = new macOS.MacHotkeyService(),
                ClipboardService = new macOS.MacClipboardService(),
                DeviceIdentifier = new macOS.MacDeviceIdentifier(),
                TokenProtection = new macOS.MacTokenProtection(),
                SingleInstance = new macOS.MacSingleInstance(),
                TrayService = new macOS.MacTrayService(),
                AutoStartService = new macOS.MacAutoStartService()
            };
        }

        // Fallback for non-macOS (stub services for development on Windows)
        return new PlatformServices
        {
            HotkeyService = new StubHotkeyService(),
            ClipboardService = new StubClipboardService(),
            DeviceIdentifier = new StubDeviceIdentifier(),
            TokenProtection = new StubTokenProtection(),
            SingleInstance = new StubSingleInstance(),
            TrayService = new StubTrayService(),
            AutoStartService = new StubAutoStartService()
        };
    }
}

public class PlatformServices
{
    public IHotkeyService HotkeyService { get; set; } = null!;
    public IClipboardService ClipboardService { get; set; } = null!;
    public IDeviceIdentifier DeviceIdentifier { get; set; } = null!;
    public ITokenProtection TokenProtection { get; set; } = null!;
    public ISingleInstanceService SingleInstance { get; set; } = null!;
    public ISystemTrayService TrayService { get; set; } = null!;
    public IAutoStartService AutoStartService { get; set; } = null!;
}

// Stub implementations for development/testing on non-macOS platforms
internal class StubHotkeyService : IHotkeyService
{
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
    public bool RegisterHotkey(int id, HotkeyModifiers modifiers, int key) => true;
    public bool UnregisterHotkey(int id) => true;
    public void UnregisterAll() { }
    public void Initialize(object windowHandle) { }
    public void Dispose() { }
}

internal class StubClipboardService : IClipboardService
{
    public Task<string?> GetSelectedTextAsync() => Task.FromResult<string?>(null);
    public Task SetTextAsync(string text) => Task.CompletedTask;
    public Task ReplaceSelectedTextAsync(string newText) => Task.CompletedTask;
    public bool ContainsImage() => false;
    public Task<List<string>> GetImagesAsBase64Async() => Task.FromResult(new List<string>());
    public IntPtr GetForegroundWindow() => IntPtr.Zero;
    public void SetForegroundWindow(IntPtr handle) { }
}

internal class StubDeviceIdentifier : IDeviceIdentifier
{
    public string GetMachineId() => Environment.MachineName;
}

internal class StubTokenProtection : ITokenProtection
{
    public string? Encrypt(string? plaintext) => plaintext;
    public string? Decrypt(string? ciphertext) => ciphertext;
}

internal class StubSingleInstance : ISingleInstanceService
{
    public event EventHandler? ShowRequested;
    public bool TryAcquire() => true;
    public void Dispose() { }
}

internal class StubTrayService : ISystemTrayService
{
    public event EventHandler? Activated;
    public event EventHandler? ExitRequested;
    public void Show(string tooltip) { }
    public void Hide() { }
    public void Dispose() { }
}

internal class StubAutoStartService : IAutoStartService
{
    public void SetAutoStart(bool enabled) { }
    public bool IsAutoStartEnabled() => false;
}
