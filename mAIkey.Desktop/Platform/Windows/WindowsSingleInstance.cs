using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.Windows;

/// <summary>
/// Single-instance enforcement using Mutex + EventWaitHandle.
/// Ported from frontend/App.xaml.cs.
/// </summary>
public class WindowsSingleInstance : ISingleInstanceService
{
    private const string MutexName = "mAIkey_SingleInstanceMutex_B8F3E9A1";
    private const string EventName = "mAIkey_ShowWindowEvent_B8F3E9A1";

    private Mutex? _mutex;
    private EventWaitHandle? _eventHandle;
    private bool _disposed;

    public event EventHandler? ShowRequested;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance exists — signal it
            try
            {
                _eventHandle = EventWaitHandle.OpenExisting(EventName);
                _eventHandle.Set();
            }
            catch { }
            return false;
        }

        // First instance — listen for signals
        _eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        Task.Run(() =>
        {
            while (!_disposed)
            {
                try
                {
                    _eventHandle.WaitOne();
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
                catch { break; }
            }
        });

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _eventHandle?.Close();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
