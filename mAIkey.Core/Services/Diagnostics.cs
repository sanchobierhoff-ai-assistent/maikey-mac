using System.Text;
using mAIkey.Core.Interfaces;

namespace mAIkey.Core.Services;

/// <summary>
/// Lokaal diagnoselog voor de Mac-app: ~/Library/Logs/mAIkey/mAIkey.log.
/// Bevat alleen korte statusregels (nooit geselecteerde tekst of AI-antwoorden),
/// zodat een probleem op een Mac terug te zien is zonder dat er gebruikersdata
/// op schijf belandt. Het bestand wordt afgekapt boven 1 MB.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();

    public static string LogDirectory { get; } = ResolveLogDirectory();
    public static string LogFilePath => Path.Combine(LogDirectory, "mAIkey.log");

    private static string ResolveLogDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) home = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Logs", "mAIkey")
            : Path.Combine(home, ".mAIkey", "logs");
    }

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(LogDirectory);
                var path = LogFilePath;
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}\n", Encoding.UTF8);
            }
        }
        catch { /* loggen mag nooit de app breken */ }
    }

    public static void LogFormat(string format, params object[] args) => Log(string.Format(format, args));
}

/// <summary>
/// Zelfde API als de Windows-LoggingService. Productie-Windows logt niets; de Mac
/// schrijft alleen een korte foutregel (endpoint + status, zonder payload) naar het
/// diagnoselog, zodat fouten op afstand te herleiden zijn.
/// </summary>
public class LoggingService
{
    public string GetLogDirectory() => Logger.LogDirectory;

    public Task LogApiRequestAsync(
        string requestType, string operationName, string model,
        string systemPrompt, string userInput, string aiResponse,
        int inputTokens, int outputTokens, double costEuros, int durationMs,
        bool success, string? errorMessage = null, object? additionalContext = null,
        System.Text.Json.JsonElement? debugInfo = null)
        => Task.CompletedTask;

    public Task LogHotkeyUsageAsync(string hotkeyName, string input, string output, string model, int tokens, double costEuros)
        => Task.CompletedTask;

    public Task LogAIBuilderAsync(string conversationHistory, object resultingHotkey)
        => Task.CompletedTask;

    public Task LogApiErrorAsync(string endpoint, string error, int? statusCode, object? requestPayload, string? stackTrace = null)
    {
        var shortError = error.Length > 300 ? error[..300] + "…" : error;
        Logger.Log($"API-fout {endpoint} ({statusCode?.ToString() ?? "-"}): {shortError}");
        return Task.CompletedTask;
    }

    public Task LogIntegrationErrorAsync(string integrationType, string operation, string error, object? details = null, string? stackTrace = null)
    {
        Logger.Log($"Integratie-fout {integrationType}/{operation}: {error}");
        return Task.CompletedTask;
    }

    public Task LogExceptionAsync(string category, Exception exception, object? context = null)
    {
        Logger.Log($"Exception [{category}]: {exception.GetType().Name}: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string category, string message, object? details = null)
        => Task.CompletedTask;

    public Task LogInfoAsync(string category, string message, object? details = null)
        => Task.CompletedTask;
}

/// <summary>
/// Apparaat-ID voor registratie (anti-account-hopping). Het platform zet de
/// <see cref="Provider"/> bij het opstarten (macOS: IOPlatformUUID).
/// </summary>
public static class DeviceIdentifier
{
    public static IDeviceIdentifier? Provider { get; set; }

    public static string GetMachineId()
    {
        try
        {
            var id = Provider?.GetMachineId();
            if (!string.IsNullOrEmpty(id)) return id;
        }
        catch { }
        return "mac-" + Environment.MachineName;
    }
}
