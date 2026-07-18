namespace mAIkey.Core.Interfaces;

/// <summary>
/// Ensures only one instance of the app runs at a time.
/// Windows: Mutex + EventWaitHandle
/// macOS: File lock (/tmp/maikey.lock)
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>
    /// Try to acquire the single-instance lock.
    /// Returns true if this is the first instance.
    /// Returns false if another instance is already running (signals it to come to front).
    /// </summary>
    bool TryAcquire();

    /// <summary>
    /// Fired when another instance signals this instance to show its window.
    /// </summary>
    event EventHandler? ShowRequested;
}
