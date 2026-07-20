using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        foreach (var hk in _config.Hotkeys)
        {
            if (!hk.Enabled || hk.Key == 0) continue;

            int id = _nextId++;
            if (_hotkeys.RegisterHotkey(id, (HotkeyModifiers)hk.ModifierKeys, hk.Key))
                _byId[id] = hk;
        }
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (_busy) return;
        if (!_byId.TryGetValue(e.HotkeyId, out var hk)) return;

        _busy = true;
        try { await RunAsync(hk); }
        catch { /* een fout in de actie mag de app nooit laten crashen */ }
        finally { _busy = false; }
    }

    private async Task RunAsync(HotkeyConfig hk)
    {
        // Toegankelijkheid nodig om de selectie te pakken en terug te plakken.
        if (!MacAccessibility.EnsureTrusted())
            return; // gebruiker moet eerst toestemming geven (systeemprompt is getoond)

        var text = await _clipboard.GetSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var result = await _api.AnalyzeAsync(
            text,
            model: hk.Model,
            customPrompt: hk.CustomPrompt,
            outputMode: hk.OutputMode,
            prefixLanguage: hk.PrefixLanguage);

        if (!result.Success || string.IsNullOrEmpty(result.Output))
            return;

        if (hk.OutputMode == "clipboard")
            await _clipboard.SetTextAsync(result.Output);
        else
            await _clipboard.ReplaceSelectedTextAsync(result.Output);
    }
}