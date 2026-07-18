using mAIkey.Core.Interfaces;

namespace mAIkey.Desktop.Platform.macOS;

/// <summary>
/// Single-instance enforcement on macOS using a file lock.
/// If the lock file exists and the process is still running, signals it.
/// </summary>
public class MacSingleInstance : ISingleInstanceService
{
    private const string LockFileName = "/tmp/maikey.lock";
    private FileStream? _lockFile;
    private bool _disposed;

    public event EventHandler? ShowRequested;

    public bool TryAcquire()
    {
        try
        {
            // Try to create/open the lock file exclusively
            _lockFile = new FileStream(LockFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // Write our PID so others can signal us
            var writer = new StreamWriter(_lockFile);
            writer.Write(Environment.ProcessId);
            writer.Flush();

            return true;
        }
        catch (IOException)
        {
            // Lock file is held by another instance
            // Try to read PID and signal the existing instance
            try
            {
                string pidStr = File.ReadAllText(LockFileName).Trim();
                if (int.TryParse(pidStr, out int pid))
                {
                    // Send SIGUSR1 to the existing process to show window
                    var process = System.Diagnostics.Process.GetProcessById(pid);
                    if (process != null && !process.HasExited)
                    {
                        // Use kill -USR1 to signal
                        System.Diagnostics.Process.Start("kill", $"-USR1 {pid}");
                    }
                }
            }
            catch { }

            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lockFile?.Dispose();

        try { File.Delete(LockFileName); } catch { }
    }
}
