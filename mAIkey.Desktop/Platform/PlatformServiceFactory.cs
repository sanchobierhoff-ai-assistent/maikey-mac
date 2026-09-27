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
                AutoStartService = new macOS.MacAutoStartService()
            };
        }

        // Alleen voor ontwikkelen/compileren buiten macOS: no-op services.
        return new PlatformServices
        {
            HotkeyService = new StubHotkeyService(),
            ClipboardService = new StubClipboardService(),
            DeviceIdentifier = new StubDeviceIdentifier(),
            TokenProtection = new StubTokenProtection(),
            SingleInstance = new StubSingleInstance(),
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
    public IAutoStartService AutoStartService { get; set; } = null!;
}

internal class StubHotkeyService : IHotkeyService
{
#pragma warning disable CS0067
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
#pragma warning restore CS0067
    public bool RegisterHotkey(int id, HotkeyModifiers modifiers, int key) => true;
    public bool UnregisterHotkey(int id) => true;
    public void UnregisterAll() { }
    public void Initialize(object windowHandle) { }
    public void Dispose() { }
}

internal class StubClipboardService : IClipboardService
{
    private string? _text;
    public Task<bool> CopySelectionAsync() => Task.FromResult(false);
    public Task<string?> GetSelectedTextAsync() => Task.FromResult<string?>(null);
    public string? GetText() => _text;
    public string? GetHtml() => null;
    public byte[]? GetImagePng() => null;
    public Task SetTextAsync(string text) { _text = text; return Task.CompletedTask; }
    public Task ReplaceSelectedTextAsync(string newText) { _text = newText; return Task.CompletedTask; }
    public Task PasteAsync() => Task.CompletedTask;
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
#pragma warning disable CS0067
    public event EventHandler? ShowRequested;
#pragma warning restore CS0067
    public bool TryAcquire() => true;
    public void Dispose() { }
}

internal class StubAutoStartService : IAutoStartService
{
    public void SetAutoStart(bool enabled) { }
    public bool IsAutoStartEnabled() => false;
}
