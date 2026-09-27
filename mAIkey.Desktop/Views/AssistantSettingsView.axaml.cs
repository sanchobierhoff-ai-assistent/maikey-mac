using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

/// <summary>
/// Config-only pagina voor de mAI Assistent: aan/uit, instelbare sneltoetsen,
/// achtergrondinformatie en geheugenbeheer. Chatten gebeurt via de command bar-popup.
/// </summary>
public partial class AssistantSettingsView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private bool _loading;

    public AssistantSettingsView() : this(App.Config, App.Api) { }

    public AssistantSettingsView(ConfigService config, ApiClient api)
    {
        InitializeComponent();
        _config = config;
        _api = api;
        AssistantHotkeyBox.AddHandler(KeyDownEvent, AssistantHotkey_PreviewKeyDown, RoutingStrategies.Tunnel);
        ScreenshotHotkeyBox.AddHandler(KeyDownEvent, ScreenshotHotkey_PreviewKeyDown, RoutingStrategies.Tunnel);
        ApplyLocalization();
        LoadSettings();
        LoadModelsAsync();
        _ = LoadProfileAsync();
        _ = LoadMemoryAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    // ── AI-model voor de assistent ──

    private async void LoadModelsAsync()
    {
        try
        {
            var resp = await _api.GetAvailableModelsAsync();
            ModelCatalogCache.Update(resp?.Models);
            _loading = true;
            var items = new List<ModelChoice>();
            var auto = new ModelChoice("Automatisch (aanbevolen)", null);
            items.Add(auto);
            var selected = auto;
            if (resp?.Success == true && resp.Models != null)
            {
                foreach (var m in resp.Models)
                {
                    if (string.IsNullOrEmpty(m.Id) || m.Id == "smart") continue;
                    var choice = new ModelChoice(string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name, m.Id);
                    items.Add(choice);
                    if (m.Id == _config.AssistantModel) selected = choice;
                }
            }
            AssistantModelCombo.ItemsSource = items;
            AssistantModelCombo.SelectedItem = selected;
        }
        catch { /* laat 'Automatisch' staan */ }
        finally { _loading = false; }
    }

    private void AssistantModel_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (AssistantModelCombo.SelectedItem is ModelChoice c) _config.AssistantModel = c.Value;
    }

    private sealed class ModelChoice
    {
        public string Label { get; }
        public string? Value { get; }
        public ModelChoice(string label, string? value) { Label = label; Value = value; }
        public override string ToString() => Label;
    }

    private void ApplyLocalization()
    {
        PageEyebrow.Text            = L.T("Nav_Assistant").ToUpperInvariant();
        PageTitle.Text              = L.T("Nav_Assistant");
        PageSubtitle.Text           = L.T("AssistantSettings_Subtitle");
        CardAssistantHeader.Text    = L.T("AssistantSettings_HotkeysCard");
        AssistantEnabledCheck.Content = L.T("Settings_Assistant_Enable");
        AssistantEnabledDesc.Text   = L.T("AssistantSettings_EnableDesc");
        AssistantHotkeyLabel.Text   = L.T("AssistantSettings_OpenHotkey");
        AssistantHotkeyHint.Text    = L.T("AssistantSettings_HotkeyHint");
        ScreenshotEnabledCheck.Content = L.T("AssistantSettings_ScreenshotEnable");
        ScreenshotEnabledDesc.Text  = L.T("AssistantSettings_ScreenshotDesc");
        ScreenshotHotkeyLabel.Text  = L.T("AssistantSettings_ScreenshotHotkey");
        CardBackgroundHeader.Text   = L.T("AssistantSettings_BackgroundCard");
        BackgroundDesc.Text         = L.T("AssistantSettings_BackgroundDesc");
        CompanyLabel.Text           = L.T("AssistantSettings_Company");
        RoleLabel.Text              = L.T("AssistantSettings_Role");
        BackgroundLabel.Text        = L.T("AssistantSettings_About");
        SaveBackgroundBtn.Content   = L.T("AssistantSettings_Save");
        MemoryTitle.Text            = L.T("Settings_Memory_Title");
        MemoryDesc.Text             = L.T("Settings_Memory_Desc");
        MemoryEmpty.Text            = L.T("Settings_Memory_Empty");
        MemoryAddBtn.Content        = L.T("Settings_Memory_Add");
    }

    private void LoadSettings()
    {
        _loading = true;
        AssistantEnabledCheck.IsChecked = _config.AssistantEnabled;
        ScreenshotEnabledCheck.IsChecked = _config.ScreenshotHotkeyEnabled;
        AssistantHotkeyBox.Text = HotkeyKeys.Format(_config.AssistantHotkeyModifiers, _config.AssistantHotkeyKey);
        ScreenshotHotkeyBox.Text = HotkeyKeys.Format(_config.ScreenshotHotkeyModifiers, _config.ScreenshotHotkeyKey);
        _loading = false;
    }

    // ── Aan/uit ──

    private void AssistantEnabled_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.AssistantEnabled = AssistantEnabledCheck.IsChecked == true;
        Ui.Main?.ReloadHotkeys();
    }

    private async void ScreenshotEnabled_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.ScreenshotHotkeyEnabled = ScreenshotEnabledCheck.IsChecked == true;
        Ui.Main?.ReloadHotkeys();
        // macOS: schermopnames vragen eenmalig toestemming (Privacy → Schermopname).
        if (_config.ScreenshotHotkeyEnabled) await MacAccessibility.EnsureScreenCaptureAsync(Owner);
    }

    // ── Sneltoets vangen ──

    private async void AssistantHotkey_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var (ok, mods, key) = await TryCaptureHotkeyAsync(e);
        if (!ok) return;
        _config.AssistantHotkeyModifiers = mods;
        _config.AssistantHotkeyKey = key;
        AssistantHotkeyBox.Text = HotkeyKeys.Format(mods, key);
        Ui.Main?.ReloadHotkeys();
    }

    private async void ScreenshotHotkey_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var (ok, mods, key) = await TryCaptureHotkeyAsync(e);
        if (!ok) return;
        _config.ScreenshotHotkeyModifiers = mods;
        _config.ScreenshotHotkeyKey = key;
        ScreenshotHotkeyBox.Text = HotkeyKeys.Format(mods, key);
        Ui.Main?.ReloadHotkeys();
    }

    /// <summary>
    /// Vangt een toetscombinatie op: negeert losse modifiers, vereist minstens één modifier,
    /// waarschuwt bij macOS-systeemcombinaties.
    /// </summary>
    private async Task<(bool ok, int mods, int key)> TryCaptureHotkeyAsync(KeyEventArgs e)
    {
        var physical = e.Key;
        if (!HotkeyCapture.TryCapture(e, out var mods, out var key, out var needsModifier))
        {
            if (needsModifier)
                await MkDialog.ShowInfo(L.T("Nav_Assistant"), L.T("AssistantSettings_ModifierRequired"), Owner);
            return (false, 0, 0);
        }

        if (IsSystemShortcut(mods, physical))
        {
            string combo = HotkeyKeys.Format(mods, key);
            if (!await MkDialog.ShowConfirm(L.T("Nav_Assistant"), L.Tf("AssistantSettings_SysShortcut", combo), Owner))
                return (false, 0, 0);
        }
        return (true, mods, key);
    }

    /// <summary>Combinaties die macOS zelf al gebruikt (Cmd = 8, Ctrl = 2, Alt = 1, Shift = 4).</summary>
    private static bool IsSystemShortcut(int mods, Key key)
    {
        if (mods == 8 && key is Key.Q or Key.W or Key.Tab or Key.Space or Key.H or Key.M or Key.C or Key.V or Key.X or Key.Z or Key.A) return true;
        if (mods == 2 && key == Key.Space) return true;
        if (mods == (8 | 1) && key == Key.Escape) return true;
        if (mods == (8 | 4) && key is Key.D3 or Key.D4 or Key.D5) return true;
        return false;
    }

    // ── Achtergrondprofiel ──

    private async Task LoadProfileAsync()
    {
        try
        {
            var res = await _api.GetAssistantProfileAsync();
            var p = res?.Profile;
            if (p != null)
            {
                CompanyBox.Text = p.Company ?? "";
                RoleBox.Text = p.Role ?? "";
                BackgroundBox.Text = p.Background ?? "";
            }
        }
        catch { /* stil: profiel laden is best-effort */ }
    }

    private async void SaveBackground_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            SaveBackgroundBtn.IsEnabled = false;
            await _api.SaveAssistantProfileAsync(CompanyBox.Text?.Trim(), RoleBox.Text?.Trim(), BackgroundBox.Text?.Trim());
            BackgroundSavedLabel.Text = L.T("AssistantSettings_Saved");
        }
        catch
        {
            await MkDialog.ShowInfo(L.T("Nav_Assistant"), L.T("Assistant_GenericError"), Owner);
        }
        finally { SaveBackgroundBtn.IsEnabled = true; }
    }

    // ── Geheugen ──

    private async Task LoadMemoryAsync()
    {
        try
        {
            var res = await _api.GetMemoryAsync();
            var items = res?.Memory ?? new List<MemoryItem>();
            MemoryList.ItemsSource = items;
            MemoryEmpty.IsVisible = items.Count == 0;
        }
        catch
        {
            MemoryEmpty.Text = L.T("Settings_Memory_LoadError");
            MemoryEmpty.IsVisible = true;
        }
    }

    private async void AddMemory_Click(object? sender, RoutedEventArgs e)
    {
        var content = MemoryAddBox.Text?.Trim();
        if (string.IsNullOrEmpty(content)) return;
        try
        {
            await _api.AddMemoryAsync(content, "user");
            MemoryAddBox.Text = "";
            await LoadMemoryAsync();
        }
        catch { await MkDialog.ShowInfo(L.T("Nav_Assistant"), L.T("Assistant_GenericError"), Owner); }
    }

    private async void DeleteMemory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id || string.IsNullOrEmpty(id)) return;
        try
        {
            await _api.DeleteMemoryAsync(id);
            await LoadMemoryAsync();
        }
        catch { await MkDialog.ShowInfo(L.T("Nav_Assistant"), L.T("Assistant_GenericError"), Owner); }
    }
}
