using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using mAIkey.Core.Interfaces;
using mAIkey.Core.Models;
using mAIkey.Core.Services;
using mAIkey.Desktop.Platform;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Verbindt de globale sneltoetsen aan de mAIkey-actie. Registreert alle
/// ingeschakelde hotkeys bij het systeem (permissievrij via Carbon) en voert bij
/// indrukken de tekst-omzetting uit: geselecteerde tekst pakken → API → terugplakken.
/// Het pakken/plakken vereist eenmalig macOS Toegankelijkheids-toestemming.
/// </summary>
public class HotkeyRuntime
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private readonly IHotkeyService _hotkeys;
    private readonly IClipboardService _clipboard;

    private readonly ProcessingIndicator _indicator = new();
    private readonly Dictionary<int, HotkeyConfig> _byId = new();
    private int _nextId = 1;
    private bool _busy;
    private bool _started;

    public HotkeyRuntime(ConfigService config, ApiClient api)
    {
        _config = config;
        _api = api;
        var platform = PlatformServiceFactory.Create();
        _hotkeys = platform.HotkeyService;
        _clipboard = platform.ClipboardService;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        _hotkeys.Initialize(IntPtr.Zero);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        RegisterAll();
    }

    /// <summary>Registreer alle ingeschakelde hotkeys opnieuw (na een wijziging).</summary>
    public void RegisterAll()
    {
        _hotkeys.UnregisterAll();
        _byId.Clear();
        _nextId = 1;

        int count = 0;
        foreach (var hk in _config.Hotkeys)
        {
            if (!hk.Enabled || hk.Key == 0)
            {
                Log($"overslaan '{hk.Name}' (enabled={hk.Enabled}, key={hk.Key})");
                continue;
            }

            int id = _nextId++;
            bool ok = _hotkeys.RegisterHotkey(id, (HotkeyModifiers)hk.ModifierKeys, hk.Key);
            if (ok) { _byId[id] = hk; count++; }
            Log($"registreren '{hk.Name}' mods={hk.ModifierKeys} key={hk.Key} -> {(ok ? "OK" : "MISLUKT")}");
        }
        Log($"totaal geregistreerd: {count}");
    }

    private async Task SendToJiraAsync(string content, bool direct)
    {
        Integration? jira = null;
        try
        {
            var integrations = await _api.GetIntegrationsAsync();
            jira = integrations?.FirstOrDefault(i => i.IntegrationType == "jira" && i.IsActive);
        }
        catch (Exception ex) { Log("jira: integraties ophalen mislukt: " + ex.Message); }

        var project = jira?.Config?.DefaultProject;
        var issueType = jira?.Config?.DefaultIssueType;
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(issueType))
        {
            Log("jira: geen standaardproject/issue-type ingesteld — configureer Jira eerst");
            return;
        }

        var summary = content.Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0) ?? "mAIkey ticket";
        if (summary.Length > 200) summary = summary.Substring(0, 200);

        if (direct)
        {
            try
            {
                var ticket = await _api.CreateJiraTicketAsync(project!, issueType!, summary, content);
                Log(ticket != null ? $"jira: ticket {ticket.Key} aangemaakt" : "jira: aanmaken mislukt");
            }
            catch (Exception ex) { Log("jira: aanmaken fout: " + ex.Message); }
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            new Windows.JiraTicketReviewWindow(project!, issueType!, summary, content).Show();
        });
    }

    /// <summary>GitHub-issue aanmaken (server gebruikt de opgeslagen standaard-repo).</summary>
    private async Task SendToGitHubAsync(string content, bool direct)
    {
        var title = content.Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0) ?? "mAIkey issue";
        if (title.Length > 200) title = title.Substring(0, 200);

        if (direct)
        {
            try
            {
                var issue = await _api.CreateGitHubIssueAsync(title, content);
                Log(issue != null ? $"github: issue #{issue.Number} aangemaakt" : "github: aanmaken mislukt");
            }
            catch (Exception ex) { Log("github: aanmaken fout: " + ex.Message); }
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            new Windows.IntegrationReviewWindow(
                "GITHUB", "Issue nakijken", "Titel", "Issue aanmaken", title, content,
                async (t, b) =>
                {
                    var i = await _api.CreateGitHubIssueAsync(t, b);
                    return i != null ? $"#{i.Number}" : null;
                }).Show();
        });
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "maikey-hotkey.log"),
                $"{DateTime.Now:HH:mm:ss}  {msg}\n");
        }
        catch { }
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        Log($"--- hotkey ingedrukt (id={e.HotkeyId}) ---");
        if (_busy) { Log("bezig, genegeerd"); return; }
        if (!_byId.TryGetValue(e.HotkeyId, out var hk)) { Log("onbekend id"); return; }

        _busy = true;
        try { await RunAsync(hk); }
        catch (Exception ex) { Log($"FOUT: {ex.Message}"); }
        finally { _busy = false; }
    }

    private async Task RunAsync(HotkeyConfig hk)
    {
        // Toegankelijkheid nodig om de selectie te pakken en terug te plakken.
        if (!MacAccessibility.EnsureTrusted())
        {
            Log("geen Toegankelijkheids-toestemming -> instellingen geopend, gestopt");
            return;
        }
        Log("toestemming OK");

        // Onthoud de app die vooraan stond, zodat we die na de indicator kunnen
        // heractiveren (anders belandt Cmd+V in het indicator-venster).
        IntPtr targetApp = _clipboard.GetForegroundWindow();

        var text = await _clipboard.GetSelectedTextAsync();
        Log($"geselecteerde tekst: {(text == null ? "null" : text.Length + " tekens")}");
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Integratie-modus? (bijv. jira_review, github_direct)
        string integration = hk.OutputMode switch
        {
            "jira_review" or "jira_direct" => "jira",
            "github_review" or "github_direct" => "github",
            _ => ""
        };
        bool isIntegration = integration.Length > 0;
        bool directSend = hk.OutputMode.EndsWith("_direct");
        // Voor integraties genereert de AI de inhoud als een "window"-antwoord.
        string apiMode = isIntegration ? "window" : hk.OutputMode;

        bool showIndicator = _config.ShowAiIndicator;
        if (showIndicator) _indicator.Show();

        try
        {
            var result = await _api.AnalyzeAsync(
                text,
                model: hk.Model,
                customPrompt: hk.CustomPrompt,
                outputMode: apiMode,
                prefixLanguage: hk.PrefixLanguage,
                promptId: hk.PromptId);
            Log($"API resultaat: success={result.Success} error={result.Error} output={(result.Output?.Length ?? 0)} tekens");

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return;

            // Integratie? Versturen/aanmaken i.p.v. terug te plakken.
            if (isIntegration)
            {
                if (showIndicator) _indicator.Hide();
                if (integration == "jira") await SendToJiraAsync(result.Output, directSend);
                else if (integration == "github") await SendToGitHubAsync(result.Output, directSend);
                Log($"{integration}: {(directSend ? "direct" : "review")}");
                return;
            }

            // Indicator weg + oorspronkelijke app terug naar voren vóór het plakken.
            if (showIndicator)
            {
                _indicator.Hide();
                _clipboard.SetForegroundWindow(targetApp);
                await Task.Delay(150);
            }

            if (hk.OutputMode == "clipboard")
                await _clipboard.SetTextAsync(result.Output);
            else
                await _clipboard.ReplaceSelectedTextAsync(result.Output);

            Log($"klaar (mode={hk.OutputMode})");
        }
        finally
        {
            if (showIndicator) _indicator.Hide();
        }
    }
}