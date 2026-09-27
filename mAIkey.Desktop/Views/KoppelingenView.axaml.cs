using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

/// <summary>
/// Overzichts-/instellingenpagina voor Koppelingen (Zendesk ⇄ Jira): opent de hub,
/// en beheert de instelbare sneltoets, het brug-model en de schrijfstijl.
/// </summary>
public partial class KoppelingenView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private bool _loading;

    public KoppelingenView() : this(App.Config, App.Api) { }

    public KoppelingenView(ConfigService config, ApiClient api)
    {
        InitializeComponent();
        _config = config;
        _api = api;
        HotkeyBox.AddHandler(KeyDownEvent, Hotkey_PreviewKeyDown, RoutingStrategies.Tunnel);
        LoadState();
        LoadInboxStateAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    /// <summary>De werklijst-opt-in + merkfilter staan server-side (zodat de achtergrond-poll ze kent).</summary>
    private async void LoadInboxStateAsync()
    {
        try
        {
            var settingsTask = _api.GetInboxSettingsAsync();
            var brandsTask = _api.GetZendeskBrandsAsync();
            await Task.WhenAll(settingsTask, brandsTask);
            var settings = settingsTask.Result;
            var brands = brandsTask.Result;

            _loading = true;
            InboxEnabledCheck.IsChecked = settings.Enabled;

            // Merk-dropdown: "Alle merken" (null) + opgehaalde merken.
            var items = new List<BrandOption> { new BrandOption("Alle merken", null, null) };
            BrandOption? selected = null;
            foreach (var b in brands)
            {
                var opt = new BrandOption(b.Name, b.Id, b.Name);
                items.Add(opt);
                if (b.Id == settings.BrandId) selected = opt;
            }
            // Bewaard merk dat niet (meer) in de lijst zit → toch tonen zodat de keuze zichtbaar blijft.
            if (selected == null && !string.IsNullOrWhiteSpace(settings.BrandId))
            {
                var opt = new BrandOption(settings.BrandName ?? settings.BrandId!, settings.BrandId, settings.BrandName);
                items.Add(opt);
                selected = opt;
            }
            BrandCombo.ItemsSource = items;
            BrandCombo.SelectedItem = selected ?? items[0];
            BrandHint.Text = brands.Count == 0
                ? "Geen merken opgehaald (Zendesk-account zonder meerdere merken, of nog niet gekoppeld)."
                : "Merken worden automatisch uit Zendesk opgehaald. 'Alle merken' = geen filter.";
            _loading = false;
        }
        catch { _loading = false; }
    }

    private async void BrandCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (BrandCombo.SelectedItem is not BrandOption opt) return;
        var ok = await _api.SetInboxBrandAsync(opt.Id, opt.Name);
        if (!ok)
            await MkDialog.ShowError(L.T("Nav_Koppelingen"),
                "Kon het merkfilter niet opslaan. Controleer je verbinding en probeer opnieuw.", Owner);
    }

    private async void InboxEnabled_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = InboxEnabledCheck.IsChecked == true;
        InboxEnabledCheck.IsEnabled = false;
        var ok = await _api.SetInboxEnabledAsync(enabled);
        InboxEnabledCheck.IsEnabled = true;
        if (!ok)
        {
            _loading = true;
            InboxEnabledCheck.IsChecked = !enabled; // terugdraaien bij fout
            _loading = false;
            await MkDialog.ShowError(L.T("Nav_Koppelingen"),
                "Kon de werklijst-instelling niet opslaan. Controleer je verbinding en probeer opnieuw.", Owner);
        }
    }

    private void LoadState()
    {
        _loading = true;

        HotkeyEnabledCheck.IsChecked = _config.KoppelingHotkeyEnabled;
        AutoBacklinkCheck.IsChecked = _config.KoppelingAutoBacklink;
        JiraPromptBox.Text = _config.KoppelingJiraPrompt ?? "";
        HotkeyBox.Text = HotkeyKeys.Format(_config.KoppelingHotkeyModifiers, _config.KoppelingHotkeyKey);

        // Model-opties (Value null = sterke server-default).
        var models = new List<ModelOption>
        {
            new ModelOption("Automatisch (sterk model)", null),
            new ModelOption("Claude Sonnet 5 — beste stijl & context", "claude-sonnet-5"),
            new ModelOption("GPT-5.6 Terra — sterk, 1M context", "gpt-5.6-terra"),
            new ModelOption("GPT-5.6 Sol — top-tier", "gpt-5.6-sol"),
            new ModelOption("GPT-4.1", "gpt-4.1"),
        };
        ModelCombo.ItemsSource = models;
        ModelCombo.SelectedItem = models.Find(m => m.Value == _config.KoppelingModel) ?? models[0];

        // Stijl-opties uit de bibliotheek.
        var styles = new List<StyleOption> { new StyleOption("Geen stijl", null) };
        StyleOption? selected = null;
        foreach (var s in _config.WritingStyles)
        {
            var opt = new StyleOption(string.IsNullOrWhiteSpace(s.Name) ? "(naamloos)" : s.Name, s.Id);
            styles.Add(opt);
            if (s.Id == _config.KoppelingStyleId) selected = opt;
        }
        StyleCombo.ItemsSource = styles;
        StyleCombo.SelectedItem = selected ?? styles[0];

        _loading = false;
    }

    private void OpenHubButton_Click(object? sender, RoutedEventArgs e)
    {
        var hub = new BridgeHubWindow(_api, _config);
        if (Owner != null) hub.Show(Owner); else hub.Show();
        hub.Activate();
    }

    private void ManageZendesk_Click(object? sender, RoutedEventArgs e)
    {
        var win = new ZendeskTicketWindow(_api);
        if (Owner != null) win.Show(Owner); else win.Show();
        win.Activate();
    }

    private void HotkeyEnabled_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.KoppelingHotkeyEnabled = HotkeyEnabledCheck.IsChecked == true;
        Ui.Main?.ReloadHotkeys();
    }

    private void AutoBacklink_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.KoppelingAutoBacklink = AutoBacklinkCheck.IsChecked == true;
    }

    private void JiraPrompt_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var v = (JiraPromptBox.Text ?? "").Trim();
        _config.KoppelingJiraPrompt = v.Length == 0 ? null : v;
    }

    /// <summary>
    /// Maak een schrijfstijl uit de eigen recente publieke Zendesk-reacties en stel die
    /// meteen in als brug-stijl (hergebruikt de flow van de Stijlbibliotheek).
    /// </summary>
    private async void ZendeskStyle_Click(object? sender, RoutedEventArgs e)
    {
        var owner = Owner;
        ZendeskStyleBtn.IsEnabled = false;
        try
        {
            // Tier-limiet op aantal stijlen (fail-open bij fout).
            try
            {
                var status = await _api.GetSubscriptionStatusAsync();
                if (status.Success && status.MaxStyleProfiles.HasValue &&
                    _config.GetWritingStyles().Length >= status.MaxStyleProfiles.Value)
                {
                    await MkDialog.ShowInfo(L.T("StyleLib_LimitTitle"),
                        L.Tf("StyleLib_LimitDesc", status.Tier, status.MaxStyleProfiles.Value), owner);
                    return;
                }
            }
            catch { /* fail-open */ }

            // 1) Eigen recente publieke reacties ophalen.
            var fetched = await _api.DeriveZendeskRepliesAsync(12);
            if (!fetched.Success || fetched.Replies == null || fetched.Replies.Count < 3)
            {
                await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"),
                    fetched.Error ?? L.T("StyleLib_ZendeskStyleTooFew"), owner);
                return;
            }

            // 2) Ter controle tonen vóór genereren.
            var preview = string.Join("\n\n", fetched.Replies
                .Take(3).Select(r => "• " + (r.Length > 160 ? r.Substring(0, 160) + "…" : r)));
            if (!await MkDialog.ShowConfirm(L.T("StyleLib_ZendeskStyle"),
                    L.Tf("StyleLib_ZendeskStyleConfirm", fetched.Replies.Count) + "\n\n" + preview, owner))
                return;

            // 3) StyleProfile genereren.
            var generated = await _api.DeriveZendeskStyleAsync(fetched.Replies, "Klantantwoorden in Zendesk");
            if (!generated.Success || string.IsNullOrWhiteSpace(generated.StyleProfile))
            {
                await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"),
                    generated.Error ?? L.T("StyleLib_ZendeskStyleFailed"), owner);
                return;
            }

            // 4) Opslaan als stijl + meteen instellen als brug-stijl.
            var style = new WritingStyle
            {
                Name = L.T("StyleLib_ZendeskStyleName"),
                UsageContext = "Klantantwoorden in Zendesk",
                Description = "Klantantwoorden in Zendesk",
                StyleProfile = generated.StyleProfile!.Trim(),
                TextExamples = fetched.Replies.ToArray()
            };
            _config.AddWritingStyle(style);
            _config.KoppelingStyleId = style.Id;

            LoadState();  // ververst StyleCombo + selecteert de nieuwe stijl
            await MkDialog.ShowInfo(L.T("StyleLib_ZendeskStyle"), L.T("StyleLib_ZendeskStyleDone"), owner);
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"), ex.Message, owner);
        }
        finally
        {
            ZendeskStyleBtn.IsEnabled = true;
        }
    }

    private void ModelCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ModelCombo.SelectedItem is ModelOption opt) _config.KoppelingModel = opt.Value;
    }

    private void StyleCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (StyleCombo.SelectedItem is StyleOption opt) _config.KoppelingStyleId = opt.Value;
    }

    // ── Sneltoets vangen ──

    private async void Hotkey_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (HotkeyCapture.TryCapture(e, out var modifiers, out var key, out var needsModifier))
        {
            _config.KoppelingHotkeyModifiers = modifiers;
            _config.KoppelingHotkeyKey = key;
            HotkeyBox.Text = HotkeyKeys.Format(modifiers, key);
            Ui.Main?.ReloadHotkeys();
        }
        else if (needsModifier)
        {
            await MkDialog.ShowInfo(L.T("Nav_Koppelingen"), L.T("AssistantSettings_ModifierRequired"), Owner);
        }
    }

    private sealed class ModelOption
    {
        public string Label { get; }
        public string? Value { get; }
        public ModelOption(string label, string? value) { Label = label; Value = value; }
        public override string ToString() => Label;
    }

    private sealed class StyleOption
    {
        public string Label { get; }
        public string? Value { get; }
        public StyleOption(string label, string? value) { Label = label; Value = value; }
        public override string ToString() => Label;
    }

    private sealed class BrandOption
    {
        public string Label { get; }
        public string? Id { get; }
        public string? Name { get; }
        public BrandOption(string label, string? id, string? name) { Label = label; Id = id; Name = name; }
        public override string ToString() => Label;
    }
}
