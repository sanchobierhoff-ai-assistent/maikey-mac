using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Koppelingen-hub (Zendesk ⇄ Jira) — chat-gedreven. Links kies je een Zendesk-ticket,
/// rechts een Jira-issue (beide doorzoekbaar). Op basis van je selectie verschijnen in het
/// midden acties (Maak Jira-ticket / Reactie naar klant / Opmerking voor dev / Analyseer / Koppel).
/// Onderin typ je extra context; Verstuur toont EERST een voorstel in de chat (met opmaak/tabellen),
/// plaatsen doe je met een aparte knop. Gesprekken worden bewaard (Geschiedenis).
/// </summary>
public partial class BridgeHubWindow : Window
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;

    private BridgeSourceItem? _selectedZendesk;
    private BridgeSourceItem? _selectedJira;
    private string? _activeAction;   // null = freeform; anders make_jira/zreply/reply/dev_comment/analyze/summarize/couple
    private string? _sessionId;      // brug-sessie voor geschiedenis (Fase 3)
    private Func<List<BridgeClarification>?, Task>? _pendingClarify;
    private bool _suppressListEvents;
    private string _tab = "werklijst";   // "werklijst" (Zendesk-first) | "gekoppeld"
    private bool _jiraPickerOpen;        // gebruiker koppelt handmatig een Jira → Jira-paneel tonen

    public BridgeHubWindow() : this(App.Api, App.Config) { }

    public BridgeHubWindow(ApiClient api, ConfigService config)
    {
        InitializeComponent();
        InputBox.AddHandler(KeyDownEvent, InputBox_PreviewKeyDown, RoutingStrategies.Tunnel);
        KeyDown += (_, e) => { if (e.Key == Key.Escape && HistoryOverlay.IsVisible) HistoryOverlay.IsVisible = false; };
        _api = api;
        _config = config;
        GoToIntegrationsButton.Content = L.T("Bridge_GoToIntegrations") + " →";

        Opened += async (s, e) =>
        {
            await RefreshConnectionStatusAsync();
            await LoadZendeskListAsync(string.Empty);
            await LoadInboxAsync(forceRefresh: false);
            UpdateOptionChips();
        };
    }

    private string? Model => string.IsNullOrWhiteSpace(_config.KoppelingModel) ? null : _config.KoppelingModel;

    private string? StyleInstructions
    {
        get
        {
            var id = _config.KoppelingStyleId;
            if (string.IsNullOrWhiteSpace(id)) return null;
            var style = _config.GetStyleById(id);
            return string.IsNullOrWhiteSpace(style?.StyleProfile) ? null : style!.StyleProfile;
        }
    }

    // Combineer de opgeslagen schrijfstijl met de door de gebruiker getypte extra aanwijzing.
    private string? CombineStyle(string? typed)
    {
        var style = StyleInstructions;
        if (string.IsNullOrWhiteSpace(typed)) return style;
        if (string.IsNullOrWhiteSpace(style)) return typed;
        return style + "\n\nExtra aanwijzing van de gebruiker: " + typed;
    }

    // ── Verbindingsstatus ─────────────────────────────────────────────────────
    private async Task RefreshConnectionStatusAsync()
    {
        try
        {
            var integrations = await _api.GetIntegrationsAsync();
            var types = new HashSet<string>(
                (integrations ?? Array.Empty<Integration>())
                    .Select(i => (i.IntegrationType ?? "").ToLowerInvariant()));
            bool zen = types.Contains("zendesk");
            bool jira = types.Contains("jira");

            var okBrush = Ui.Brush("Accent");
            var offBrush = Ui.Brush("Text3");
            ConnZendesk.Text = zen ? "Zendesk ✓" : "Zendesk ✗";
            ConnZendesk.Foreground = zen ? okBrush : offBrush;
            ConnJira.Text = jira ? "Jira ✓" : "Jira ✗";
            ConnJira.Foreground = jira ? okBrush : offBrush;
            ConnFixButton.IsVisible = !((zen && jira));
        }
        catch { ConnZendesk.Text = "Zendesk ?"; ConnJira.Text = "Jira ?"; }
    }

    // ── Bron-lijsten ──────────────────────────────────────────────────────────
    private async void ZenSearch_Click(object? sender, RoutedEventArgs e) => await LoadZendeskListAsync(ZenSearchBox.Text ?? "");
    private async void ZenSearch_KeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) await LoadZendeskListAsync(ZenSearchBox.Text ?? ""); }
    private async void JiraSearch_Click(object? sender, RoutedEventArgs e) => await LoadJiraListAsync(JiraSearchBox.Text ?? "");
    private async void JiraSearch_KeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) await LoadJiraListAsync(JiraSearchBox.Text ?? ""); }

    private async Task LoadZendeskListAsync(string query)
    {
        SetBusy(true, "Zendesk-tickets ophalen…");
        var res = await _api.ListBridgeSourcesAsync("zendesk", query);
        SetBusy(false, null);
        FillList(ZenList, res, "Kon Zendesk-tickets niet ophalen. Koppel eerst Zendesk bij Integraties.");
    }

    private async Task LoadJiraListAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) { SetStatus("Zoek een Jira-issue op sleutel, link of naam.", true); return; }
        SetBusy(true, "Jira-issues zoeken…");
        var res = await _api.ListBridgeSourcesAsync("jira", query);
        SetBusy(false, null);
        FillList(JiraList, res, "Kon Jira niet doorzoeken. Koppel eerst Jira bij Integraties.");
    }

    private void FillList(ListBox list, BridgeSourcesResult res, string fallbackError)
    {
        _suppressListEvents = true;
        list.Items.Clear();
        _suppressListEvents = false;
        if (!res.Success)
        {
            SetStatus(res.Error ?? fallbackError, true, res.ErrorType);
            return;
        }
        foreach (var item in res.Items) list.Items.Add(item);
        if (res.Items.Count == 0) SetStatus("Niets gevonden.", false);
    }

    private async void ZenList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressListEvents) return;
        if (ZenList.SelectedItem is not BridgeSourceItem it) return;
        _selectedZendesk = it;
        ShowChip(ZenSelectedChip, ZenSelectedText, $"#{it.Id} · {it.Title}");
        UpdateOptionChips();
        // Auto: gekoppeld Jira-issue voorstellen als er nog geen gekozen is.
        if (_selectedJira == null) await AutoLinkCounterpartAsync("zendesk", it.Id);
    }

    private async void JiraList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressListEvents) return;
        if (JiraList.SelectedItem is not BridgeSourceItem it) return;
        _selectedJira = it;
        ShowChip(JiraSelectedChip, JiraSelectedText, $"{it.Id} · {it.Title}");
        SyncJiraPanel();
        UpdateOptionChips();
        if (_selectedZendesk == null) await AutoLinkCounterpartAsync("jira", it.Id);
    }

    /// <summary>Zoek automatisch de al gekoppelde tegenpartij (Jira↔Zendesk) en selecteer die.</summary>
    private async Task AutoLinkCounterpartAsync(string type, string id)
    {
        try
        {
            var res = await _api.ListLinkedCounterpartsAsync(type, id);
            if (!res.Success || res.Items.Count == 0) return;
            var first = res.Items[0];
            if (type == "zendesk")
            {
                _selectedJira = first;
                ShowChip(JiraSelectedChip, JiraSelectedText, $"{first.Id} · {first.Title}");
                SyncJiraPanel();
                SetStatus("Gekoppeld Jira-issue automatisch geselecteerd.", false);
            }
            else
            {
                _selectedZendesk = first;
                ShowChip(ZenSelectedChip, ZenSelectedText, $"#{first.Id} · {first.Title}");
                SetStatus("Gekoppeld Zendesk-ticket automatisch geselecteerd.", false);
            }
            UpdateOptionChips();
        }
        catch { /* stil */ }
    }

    private static void ShowChip(Border chip, TextBlock text, string label)
    {
        text.Text = label;
        chip.IsVisible = true;
    }

    // ── Jira-paneel tonen/verbergen (Zendesk-first: standaard verborgen) ─────────
    private void SetJiraPanelVisible(bool show)
    {
        JiraPanel.IsVisible = show;
        var cols = SourcesGrid.ColumnDefinitions;
        cols[2].Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        cols[1].Width = show ? new GridLength(12) : new GridLength(0);
        cols[0].Width = new GridLength(1, GridUnitType.Star);
    }

    /// <summary>Toon het Jira-paneel zodra er een Jira gekozen is óf de gebruiker handmatig wil koppelen.</summary>
    private void SyncJiraPanel() => SetJiraPanelVisible(_selectedJira != null || _jiraPickerOpen);

    // ── Tabs: Werklijst (Zendesk-first) ⇄ Gekoppeld ─────────────────────────────
    private void TabWerklijst_Click(object? sender, RoutedEventArgs e) => SetTab("werklijst");
    private void TabGekoppeld_Click(object? sender, RoutedEventArgs e) => SetTab("gekoppeld");

    private void SetTab(string tab)
    {
        _tab = tab;
        bool werk = tab == "werklijst";
        SourcesGrid.IsVisible = werk;
        CoupledPanel.IsVisible = !(werk);
        Ui.SetKind(TabWerklijst, werk ? "AccentButton" : "GhostButton");
        Ui.SetKind(TabGekoppeld, werk ? "GhostButton" : "AccentButton");
        if (!werk) _ = LoadCoupledAsync();
    }

    // ── Tab "Gekoppeld": bestaande koppelingen openen/verwijderen ───────────────
    private sealed class CoupledPair { public string ZendeskId = ""; public string JiraKey = ""; public string? JiraUrl; }

    private void CoupledRefresh_Click(object? sender, RoutedEventArgs e) => _ = LoadCoupledAsync();

    private async Task LoadCoupledAsync()
    {
        try
        {
            var res = await _api.ListBridgeLinksAsync();
            CoupledList.Children.Clear();
            var links = res.Success ? res.Links : new List<BridgeLink>();
            if (links.Count == 0) { CoupledEmptyHint.IsVisible = true; return; }
            CoupledEmptyHint.IsVisible = false;

            foreach (var link in links)
            {
                // Normaliseer naar (zendeskId, jiraKey) ongeacht de koppelrichting.
                string zid, jkey;
                if (string.Equals(link.SourceType, "zendesk", StringComparison.OrdinalIgnoreCase))
                { zid = link.SourceId ?? ""; jkey = link.TargetKey ?? ""; }
                else { zid = link.TargetKey ?? ""; jkey = link.SourceId ?? ""; }
                if (string.IsNullOrEmpty(zid) || string.IsNullOrEmpty(jkey)) continue;

                var pair = new CoupledPair { ZendeskId = zid, JiraKey = jkey, JiraUrl = link.TargetUrl };
                var row = new Border
                {
                    Background = Ui.Brush("Bg1"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 6, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var dock = new DockPanel { LastChildFill = true };

                var del = new Button
                {
                    Content = "✕", Classes = { "GhostButton" },
                    Padding = new Thickness(6, 0, 6, 0), Height = 24, MinWidth = 24, FontSize = 11,
                    Tag = link.Id
                };
                ToolTip.SetTip(del, "Koppeling verwijderen");
                del.Click += CoupledDelete_Click;
                DockPanel.SetDock(del, Dock.Right);
                dock.Children.Add(del);

                var open = new Button
                {
                    Content = "Open →", Classes = { "GhostButton" },
                    Padding = new Thickness(12, 3, 12, 3), Height = 24, FontSize = 11,
                    Tag = pair, Margin = new Thickness(8, 0, 6, 0)
                };
                open.Click += CoupledOpen_Click;
                DockPanel.SetDock(open, Dock.Right);
                dock.Children.Add(open);

                dock.Children.Add(new TextBlock
                {
                    Text = $"Zendesk #{zid}   ⇄   {jkey}",
                    Foreground = Ui.Brush("Text1"), FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
                });

                row.Child = dock;
                CoupledList.Children.Add(row);
            }
        }
        catch { /* lijst mag de hub nooit breken */ }
    }

    private async void CoupledDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        b.IsEnabled = false;
        var r = await _api.DeleteBridgeLinkAsync(id);
        if (r.Success) await LoadCoupledAsync();
        else b.IsEnabled = true;
    }

    private void CoupledOpen_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not CoupledPair pair) return;
        OpenCoupled(pair);
    }

    /// <summary>Open een gekoppeld paar in de werklijst-weergave als koppel-gesprek (beide kanten geselecteerd).</summary>
    private void OpenCoupled(CoupledPair pair)
    {
        _selectedZendesk = new BridgeSourceItem { Id = pair.ZendeskId, Title = $"#{pair.ZendeskId}" };
        _selectedJira = new BridgeSourceItem { Id = pair.JiraKey, Title = pair.JiraKey, Url = pair.JiraUrl };
        ShowChip(ZenSelectedChip, ZenSelectedText, $"#{pair.ZendeskId}");
        ShowChip(JiraSelectedChip, JiraSelectedText, $"{pair.JiraKey}");
        _jiraPickerOpen = false;
        SetTab("werklijst");
        SyncJiraPanel();
        UpdateOptionChips();

        // Fris gesprek voor dit paar; de sessie krijgt zijn meta uit de selecties (EnsureSessionAsync).
        _sessionId = null;
        _pendingClarify = null;
        ChatPanel.Children.Clear();
        ChatPanel.Children.Add(ChatEmptyHint);
        ChatEmptyHint.IsVisible = false;
        AddSystemLine($"Koppeling geopend: Zendesk #{pair.ZendeskId} ⇄ {pair.JiraKey}. Kies een actie hieronder.");
    }

    // ── Statuswijziging (koppel-gesprek) ────────────────────────────────────────
    private async Task LoadStatusOptionsAsync()
    {
        if (ZenStatusCombo.Items.Count == 0)
            foreach (var s in new[] { "open", "pending", "hold", "solved", "closed" }) ZenStatusCombo.Items.Add(s);

        JiraStatusCombo.Items.Clear();
        if (_selectedJira != null)
        {
            var trans = await _api.GetJiraTransitionsAsync(_selectedJira.Id);
            foreach (var t in trans) JiraStatusCombo.Items.Add(t); // toont JiraTransition.Name
            JiraStatusApply.IsEnabled = trans.Count > 0;
            if (trans.Count == 0) JiraStatusCombo.Items.Add("(geen transities beschikbaar)");
        }
    }

    private async void ApplyZendeskStatus_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedZendesk == null || ZenStatusCombo.SelectedItem is not string st)
        { SetStatus("Kies een Zendesk-status.", true); return; }
        SetBusy(true, "Zendesk-status wijzigen…");
        var r = await _api.UpdateZendeskTicketAsync(_selectedZendesk.Id, status: st);
        SetBusy(false, null);
        var msg = r.Success ? $"✅ Zendesk-status → {st}." : $"⚠ Zendesk-status wijzigen mislukte: {r.Error}";
        AddSystemLine(msg); await PersistSystemAsync(msg);
    }

    private async void ApplyJiraStatus_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedJira == null || JiraStatusCombo.SelectedItem is not JiraTransition t)
        { SetStatus("Kies een Jira-status.", true); return; }
        SetBusy(true, "Jira-status wijzigen…");
        var r = await _api.TransitionJiraIssueAsync(_selectedJira.Id, t.Name);
        SetBusy(false, null);
        var msg = r.Success ? $"✅ Jira-status → {t.Name}." : $"⚠ Jira-status wijzigen mislukte: {r.Error}";
        AddSystemLine(msg); await PersistSystemAsync(msg);
    }

    // ── Werklijst / inbox ───────────────────────────────────────────────────────
    // Openstaande Zendesk-tickets komen (opt-in) naar de gebruiker toe. Klikken op een
    // item selecteert het als Zendesk-bron; de rest van de flow blijft ongewijzigd.

    /// <summary>Laad de werklijst. forceRefresh=true triggert een verse server-poll.</summary>
    private async Task LoadInboxAsync(bool forceRefresh)
    {
        try
        {
            var res = forceRefresh ? await _api.RefreshInboxAsync() : await _api.ListInboxAsync();
            if (!res.Success)
            {
                // Alleen bij een expliciete actie de fout tonen (niet stilletjes bij openen).
                if (forceRefresh) SetStatus(res.Error ?? "Kon de werklijst niet laden.", true, res.ErrorType);
                RenderInbox(new List<BridgeInboxItem>(), res.Success);
                return;
            }
            RenderInbox(res.Items, true);
            if (forceRefresh)
                SetStatus(res.Items.Count == 0 ? "Geen openstaande tickets in de werklijst." : $"{res.Items.Count} ticket(s) in de werklijst.", false);
        }
        catch { /* werklijst mag de hub nooit breken */ }
    }

    private void RenderInbox(List<BridgeInboxItem> items, bool succeeded)
    {
        ZenInboxPanel.Children.Clear();

        if (items.Count == 0)
        {
            InboxContainer.IsVisible = false;
            InboxHint.IsVisible = true;
            InboxHint.Text = succeeded
                ? "Geen openstaande tickets. Zet de werklijst aan bij Koppelingen-instellingen, of klik ⟳ Werklijst."
                : "Werklijst niet beschikbaar. Controleer je Zendesk-koppeling bij Integraties.";
            return;
        }

        InboxHint.IsVisible = false;
        InboxContainer.IsVisible = true;

        foreach (var item in items)
        {
            var row = new Border
            {
                Background = Ui.Brush("Bg2"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 6, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = item
            };

            var dock = new DockPanel { LastChildFill = true };

            // Wegklikken (rechts).
            var dismiss = new Button
            {
                Content = "✕",
                Classes = { "GhostButton" },
                Padding = new Thickness(6, 0, 6, 0),
                Height = 22,
                MinWidth = 22,
                FontSize = 11,
                Tag = item.Id
            };
            ToolTip.SetTip(dismiss, "Verberg uit de werklijst");
            dismiss.Click += InboxDismiss_Click;
            DockPanel.SetDock(dismiss, Dock.Right);
            dock.Children.Add(dismiss);

            // Triage-prioriteitsstip (links).
            var prio = new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Background = TriageBrush(item.TriagePriority)
            };
            DockPanel.SetDock(prio, Dock.Left);
            dock.Children.Add(prio);

            // Onderwerp + label.
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = $"#{item.SourceId} · {item.DisplaySubject}",
                Foreground = Ui.Brush("Text1"),
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.TriageCategory)) meta.Add(item.TriageCategory!);
            if (!string.IsNullOrWhiteSpace(item.Status)) meta.Add(item.Status!);
            if (meta.Count > 0)
                textStack.Children.Add(new TextBlock
                {
                    Text = string.Join("  ·  ", meta),
                    Foreground = Ui.Brush("Text3"),
                    FontSize = 10.5,
                    Margin = new Thickness(0, 1, 0, 0)
                });
            dock.Children.Add(textStack);

            row.Child = dock;
            row.PointerReleased += InboxRow_Click;
            ZenInboxPanel.Children.Add(row);
        }
    }

    private IBrush TriageBrush(string? priority) => (priority ?? "").ToLowerInvariant() switch
    {
        "urgent" => new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)),
        "high"   => new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)),
        "low"    => Ui.Brush("Text3"),
        _        => Ui.Brush("Accent"),   // normal / onbekend
    };

    private async void InboxRow_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Border b || b.Tag is not BridgeInboxItem item) return;
        var it = new BridgeSourceItem
        {
            Id = item.SourceId ?? "",
            Title = item.DisplaySubject,
            Status = item.Status
        };
        _selectedZendesk = it;
        ShowChip(ZenSelectedChip, ZenSelectedText, $"#{it.Id} · {it.Title}");
        UpdateOptionChips();
        if (_selectedJira == null) await AutoLinkCounterpartAsync("zendesk", it.Id);
    }

    private async void InboxDismiss_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true; // niet doorbubbelen naar de rij-klik
        if (sender is not Button btn || btn.Tag is not string id) return;
        btn.IsEnabled = false;
        var r = await _api.DismissInboxItemAsync(id);
        if (r.Success)
        {
            // Rij weghalen zonder de hele lijst te herladen.
            if (btn.Parent is DockPanel dp && dp.Parent is Border row && row.Parent is Panel panel)
            {
                panel.Children.Remove(row);
                if (panel.Children.Count == 0) RenderInbox(new List<BridgeInboxItem>(), true);
            }
        }
        else { btn.IsEnabled = true; }
    }

    private async void InboxRefresh_Click(object? sender, RoutedEventArgs e)
    {
        InboxRefreshButton.IsEnabled = false;
        SetBusy(true, "Werklijst bijwerken…");
        await LoadInboxAsync(forceRefresh: true);
        SetBusy(false, null);
        InboxRefreshButton.IsEnabled = true;
    }

    private void ZenClear_Click(object? sender, RoutedEventArgs e)
    {
        _selectedZendesk = null;
        ZenSelectedChip.IsVisible = false;
        _suppressListEvents = true; ZenList.SelectedItem = null; _suppressListEvents = false;
        UpdateOptionChips();
    }

    private void JiraClear_Click(object? sender, RoutedEventArgs e)
    {
        _selectedJira = null;
        _jiraPickerOpen = false;
        JiraSelectedChip.IsVisible = false;
        _suppressListEvents = true; JiraList.SelectedItem = null; _suppressListEvents = false;
        StatusPanel.IsVisible = false;
        SyncJiraPanel();
        UpdateOptionChips();
    }

    // ── Dynamische opties (Zendesk-first) ────────────────────────────────────────
    private void UpdateOptionChips()
    {
        OptionsPanel.Children.Clear();
        bool z = _selectedZendesk != null, j = _selectedJira != null;

        var chips = new List<(string label, string key)>();
        if (z && !j)
        {
            // Alleen een Zendesk-ticket gekozen (nog niet gekoppeld).
            chips.Add(("Vat samen", "summarize"));
            chips.Add(("Maak reactie", "zreply"));
            chips.Add(("Maak Jira-ticket", "make_jira"));
            chips.Add(("Koppel bestaand Jira-ticket", "link_existing"));
        }
        if (z && j)
        {
            // Gekoppeld paar → volledig koppel-gesprek.
            chips.Add(("Analyseer beide", "analyze"));
            chips.Add(("Reactie naar klant", "reply"));
            chips.Add(("Opmerking voor dev-team", "dev_comment"));
            chips.Add(("Statuswijziging", "status"));
            chips.Add(("Koppel deze twee", "couple"));
        }

        // Speciale (niet-verzend) chips resetten het actieve verzend-type niet.
        if (_activeAction != null && chips.All(c => c.key != _activeAction)) _activeAction = null;

        foreach (var (label, key) in chips)
        {
            bool active = _activeAction == key;
            var btn = new Button
            {
                Content = label,
                Tag = key,
                Classes = { active ? "AccentButton" : "GhostButton" },
                Padding = new Thickness(14, 7, 14, 7),
                Height = 32,
                Margin = new Thickness(0, 0, 8, 8)
            };
            btn.Click += Chip_Click;
            OptionsPanel.Children.Add(btn);
        }

        UpdateOptionsHint();
    }

    private void Chip_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string key) return;

        // Speciale chips: geen verzend-actie, maar een directe UI-handeling.
        if (key == "link_existing")
        {
            _jiraPickerOpen = true;
            SyncJiraPanel();
            SetStatus("Zoek rechts een Jira-issue, kies het, en koppel met 'Koppel deze twee'.", false);
            JiraSearchBox.Focus();
            return;
        }
        if (key == "status")
        {
            var show = !StatusPanel.IsVisible;
            StatusPanel.IsVisible = show;
            if (show) _ = LoadStatusOptionsAsync();
            return;
        }

        _activeAction = (_activeAction == key) ? null : key;
        UpdateOptionChips();
    }

    private void UpdateOptionsHint()
    {
        OptionsHint.Text = _activeAction switch
        {
            "summarize" => "Verstuur → vat dit Zendesk-ticket samen.",
            "zreply" => "Verstuur → stelt een klantreactie op o.b.v. dit Zendesk-gesprek.",
            "make_jira" => "Verstuur → stelt een Jira-ticket op; daarna kun je het in Jira aanmaken (koppelt automatisch).",
            "reply" => "Verstuur → stelt een klantreactie op o.b.v. beide draden; typ eventueel extra aanwijzingen.",
            "dev_comment" => "Verstuur → stelt een opmerking voor het dev-team op.",
            "analyze" => "Verstuur → analyseert beide tickets en vat ze samen.",
            "couple" => "Verstuur → koppelt het gekozen Zendesk-ticket aan het Jira-issue.",
            _ => "Geen actie gekozen: Verstuur doet precies wat je typt op het gekozen ticket."
        };
    }

    // ── Chat ────────────────────────────────────────────────────────────────────
    private Border AddUserBubble(string text)
    {
        ChatEmptyHint.IsVisible = false;
        var tb = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.Brush("Bg0"),
            FontSize = 13.5
        };
        var border = new Border
        {
            Background = Ui.Brush("Accent"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(60, 0, 0, 10),
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = tb
        };
        ChatPanel.Children.Add(border);
        ScrollChatToEnd();
        return border;
    }

    /// <summary>Assistent-bubbel met markdown-rendering; retourneert de inhoud-StackPanel zodat je knoppen kunt toevoegen.</summary>
    private StackPanel AddAssistantBubble(string markdown)
    {
        ChatEmptyHint.IsVisible = false;
        var content = new StackPanel();
        content.Children.Add(new MarkdownViewer { Markdown = markdown });
        var border = new Border
        {
            Background = Ui.Brush("Bg3"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 60, 10),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = content
        };
        ChatPanel.Children.Add(border);
        ScrollChatToEnd();
        return content;
    }

    private void AddSystemLine(string text)
    {
        ChatEmptyHint.IsVisible = false;
        ChatPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.Brush("Text3"),
            FontSize = 12,
            FontStyle = FontStyle.Italic,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });
        ScrollChatToEnd();
    }

    private void ScrollChatToEnd() => Dispatcher.UIThread.Post(() => ChatScroller.ScrollToEnd());

    private Button ActionButton(string label, EventHandler<RoutedEventArgs> onClick, bool accent = false)
    {
        var b = new Button
        {
            Content = label,
            Classes = { accent ? "AccentButton" : "GhostButton" },
            Padding = new Thickness(14, 6, 14, 6),
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0)
        };
        b.Click += onClick;
        return b;
    }

    // ── Verzenden ────────────────────────────────────────────────────────────────
    private void InputBox_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            Send_Click(sender, e);
        }
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var typed = (InputBox.Text ?? "").Trim();

        // Lopende verduidelijking? Behandel dit bericht als antwoord.
        if (_pendingClarify != null)
        {
            if (typed.Length == 0) { SetStatus("Typ je antwoord op de vraag.", true); return; }
            AddUserBubble(typed);
            await PersistUserAsync(typed);
            InputBox.Text = "";
            var cont = _pendingClarify; _pendingClarify = null;
            await cont(new List<BridgeClarification> { new BridgeClarification { Question = "Antwoorden op de vragen", Answer = typed } });
            return;
        }

        var action = _activeAction;

        // Validatie per actie.
        if ((action == "make_jira" || action == "summarize" || action == "zreply") && _selectedZendesk == null)
        { SetStatus("Kies eerst een Zendesk-ticket.", true); return; }
        if ((action == "reply" || action == "dev_comment" || action == "analyze" || action == "couple")
            && (_selectedZendesk == null || _selectedJira == null))
        { SetStatus("Kies zowel een Zendesk-ticket als een Jira-issue.", true); return; }
        if (action == null && _selectedZendesk == null && _selectedJira == null)
        { SetStatus("Selecteer een ticket en/of typ een instructie.", true); return; }
        if (action == null && typed.Length == 0)
        { SetStatus("Typ een instructie, of kies een actie hierboven.", true); return; }

        // Koppelen is een directe actie (geen voorstel).
        if (action == "couple") { AddUserBubble(typed.Length > 0 ? typed : "Koppel deze twee."); InputBox.Text = ""; await RunCoupleAsync(); return; }

        AddUserBubble(typed.Length > 0 ? typed : DefaultPrompt(action));
        await PersistUserAsync(typed.Length > 0 ? typed : DefaultPrompt(action));
        InputBox.Text = "";
        await RunActionAsync(action, typed, null);
    }

    private static string DefaultPrompt(string? action) => action switch
    {
        "make_jira" => "Maak een Jira-ticket van dit Zendesk-ticket.",
        "summarize" => "Vat dit Zendesk-ticket samen.",
        "zreply" => "Schrijf een reactie naar de klant.",
        "reply" => "Schrijf een reactie naar de klant.",
        "dev_comment" => "Schrijf een opmerking voor het dev-team.",
        "analyze" => "Analyseer beide tickets en vat ze samen.",
        _ => "Voer dit uit op het gekozen ticket."
    };

    private async Task RunActionAsync(string? action, string typed, List<BridgeClarification>? clar)
    {
        await EnsureSessionAsync();
        SetBusy(true, "mAIkey leest de tickets en stelt een voorstel op…");
        BridgePrepareResult res;

        switch (action)
        {
            case "make_jira":
            {
                // Vaste Jira-prompt (instelling) + wat je nu typte → samen als instructie voor het ticket.
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_config.KoppelingJiraPrompt)) parts.Add(_config.KoppelingJiraPrompt!.Trim());
                if (typed.Length > 0) parts.Add(typed);
                var jiraInstruction = parts.Count > 0 ? string.Join("\n\n", parts) : null;
                res = await _api.PrepareBridgeAsync("zendesk-to-jira", _selectedZendesk!.Id, Model, instruction: jiraInstruction);
                break;
            }
            case "zreply":
                // Klantantwoord o.b.v. alleen de Zendesk-thread (nog geen gekoppelde Jira).
                res = await _api.PrepareBridgeAsync("zendesk-reply", _selectedZendesk!.Id, Model,
                    CombineStyle(typed), clarifications: clar);
                break;
            case "summarize":
            {
                var instr = "Vat dit Zendesk-ticket bondig samen: de kern van de klantvraag, wat er al besproken is, "
                          + "en eventuele open punten. Gebruik waar nuttig een korte tabel." + (typed.Length > 0 ? " " + typed : "");
                res = await _api.PrepareBridgeAsync("freeform", _selectedZendesk!.Id, Model, StyleInstructions,
                    _selectedZendesk!.Id, clar, instr, "zendesk");
                break;
            }
            case "reply":
                res = await _api.PrepareBridgeAsync("jira-to-zendesk", _selectedJira!.Id, Model,
                    CombineStyle(typed), _selectedZendesk!.Id, clar);
                break;
            case "dev_comment":
                res = await _api.PrepareBridgeAsync("zendesk-to-dev", _selectedZendesk!.Id, Model,
                    CombineStyle(typed), null, clar, jiraKey: _selectedJira?.Id);
                break;
            case "analyze":
            {
                var instr = "Analyseer beide tickets: vat de klantvraag (Zendesk) en de huidige status in development (Jira) samen, "
                          + "en noem open punten. Gebruik waar nuttig een korte tabel." + (typed.Length > 0 ? " " + typed : "");
                var srcType = _selectedZendesk != null ? "zendesk" : "jira";
                var srcId = _selectedZendesk?.Id ?? _selectedJira!.Id;
                res = await _api.PrepareBridgeAsync("freeform", srcId, Model, StyleInstructions,
                    _selectedZendesk?.Id, clar, instr, srcType, jiraKey: _selectedJira?.Id);
                break;
            }
            default: // freeform
            {
                var srcType = _selectedZendesk != null ? "zendesk" : "jira";
                var srcId = _selectedZendesk?.Id ?? _selectedJira!.Id;
                res = await _api.PrepareBridgeAsync("freeform", srcId, Model, StyleInstructions,
                    _selectedZendesk?.Id, clar, typed, srcType, jiraKey: _selectedJira?.Id);
                break;
            }
        }

        SetBusy(false, null);
        if (!res.Success) { AddSystemLine("⚠ " + (res.Error ?? "Genereren mislukt.")); SetStatus(res.Error ?? "Genereren mislukt.", true, res.ErrorType); return; }

        // Niet-fatale waarschuwingen (bv. een gekozen ticket dat toch niet gelezen kon worden).
        if (res.Warnings != null)
            foreach (var w in res.Warnings) AddSystemLine("⚠ " + w);

        var g = res.Generated;
        if (g != null && g.NeedsClarification)
        {
            var q = "**mAIkey heeft nog wat info nodig:**\n\n" + string.Join("\n", (g.Questions ?? new List<string>()).Select(x => "- " + x));
            AddAssistantBubble(q);
            await PersistAssistantAsync(q, action);
            _pendingClarify = (c) => RunActionAsync(action, typed, c);
            SetStatus("Beantwoord de vraag in de chat en druk op Verstuur.", false);
            return;
        }

        // Voorstel tonen + plaats-knoppen (review-first).
        switch (action)
        {
            case "make_jira":
                ShowJiraProposal(g);
                break;
            case "zreply":
                ShowTextProposal(g?.Reply, action);
                break;
            case "reply":
                ShowTextProposal(g?.Reply, action);
                break;
            case "dev_comment":
                ShowTextProposal(g?.Text, action, res.Source?.JiraKey);
                break;
            case "summarize":
            case "analyze":
                var a = g?.Text ?? "(geen resultaat)";
                AddAssistantBubble(a);
                await PersistAssistantAsync(a, action);
                break;
            default: // freeform
                ShowTextProposal(g?.Text, action, res.Source?.JiraKey);
                break;
        }
        SetStatus("", false);
    }

    // ── Voorstellen + plaatsen ──────────────────────────────────────────────────
    private async void ShowTextProposal(string? text, string? action, string? jiraKeyFromSource = null)
    {
        var body = text ?? "(leeg)";
        var content = AddAssistantBubble(body);
        await PersistAssistantAsync(body, action);

        var jiraKey = jiraKeyFromSource ?? _selectedJira?.Id;
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };

        if (action == "zreply")
        {
            row.Children.Add(ActionButton("Plaats publiek →", (s, e) => PlaceZendeskReply(body, true), accent: true));
            row.Children.Add(ActionButton("Plaats intern", (s, e) => PlaceZendeskReply(body, false)));
        }
        else if (action == "reply")
        {
            row.Children.Add(ActionButton("Plaats publiek →", (s, e) => PlaceReply(body, true), accent: true));
            row.Children.Add(ActionButton("Plaats intern", (s, e) => PlaceReply(body, false)));
        }
        else if (action == "dev_comment")
        {
            if (!string.IsNullOrEmpty(jiraKey)) row.Children.Add(ActionButton("Naar Jira →", (s, e) => PlaceDevComment(body, jiraKey, "jira"), accent: true));
            row.Children.Add(ActionButton("Interne Zendesk-notitie", (s, e) => PlaceDevComment(body, jiraKey, "zendesk")));
            if (!string.IsNullOrEmpty(jiraKey)) row.Children.Add(ActionButton("Beide", (s, e) => PlaceDevComment(body, jiraKey, "both")));
        }
        else // freeform
        {
            row.Children.Add(ActionButton("Plaats publiek →", (s, e) => PlaceFreeform(body, jiraKey, "zendesk_public"), accent: true));
            row.Children.Add(ActionButton("Interne notitie", (s, e) => PlaceFreeform(body, jiraKey, "zendesk_internal")));
            if (!string.IsNullOrEmpty(jiraKey)) row.Children.Add(ActionButton("Jira-comment", (s, e) => PlaceFreeform(body, jiraKey, "jira_comment")));
        }

        content.Children.Add(row);
    }

    private void ShowJiraProposal(BridgeGenerated? g)
    {
        var md = $"**{g?.Summary ?? "(zonder titel)"}**\n\n{g?.Description ?? ""}";
        if (!string.IsNullOrWhiteSpace(g?.Priority) || (g?.Labels?.Count ?? 0) > 0)
            md += $"\n\n_Type: {g?.IssueType ?? "Task"} · Prioriteit: {g?.Priority ?? "Medium"}"
                + ((g?.Labels?.Count ?? 0) > 0 ? " · Labels: " + string.Join(", ", g!.Labels!) : "") + "_";
        var content = AddAssistantBubble(md);
        _ = PersistAssistantAsync(md, "make_jira");

        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(ActionButton("Aanmaken in Jira →", (s, e) => CreateJiraFromProposal(g), accent: true));
        content.Children.Add(row);
    }

    private async void PlaceReply(string text, bool isPublic)
    {
        var payload = new { direction = "jira-to-zendesk", sourceKey = _selectedJira?.Id, zendeskTicketId = _selectedZendesk?.Id, reply = text, @public = isPublic };
        await CommitAndReportAsync(payload, isPublic ? "Reactie publiek geplaatst bij de klant." : "Reactie als interne notitie geplaatst.");
    }

    private async void PlaceZendeskReply(string text, bool isPublic)
    {
        // Klantantwoord zonder Jira (nog niet gekoppeld ticket).
        var payload = new { direction = "zendesk-reply", zendeskTicketId = _selectedZendesk?.Id, reply = text, @public = isPublic };
        await CommitAndReportAsync(payload, isPublic ? "Reactie publiek geplaatst bij de klant." : "Reactie als interne notitie geplaatst.");
    }

    private async void PlaceDevComment(string text, string? jiraKey, string destination)
    {
        if ((destination == "jira" || destination == "both") && string.IsNullOrEmpty(jiraKey))
        { SetStatus("Geen gekoppeld Jira-issue voor een Jira-comment.", true); return; }
        var payload = new { direction = "dev-comment", jiraKey, zendeskTicketId = _selectedZendesk?.Id, comment = text, destination };
        await CommitAndReportAsync(payload, "Opmerking geplaatst.");
    }

    private async void PlaceFreeform(string text, string? jiraKey, string destination)
    {
        if (destination == "jira_comment" && string.IsNullOrEmpty(jiraKey))
        { SetStatus("Geen gekoppeld Jira-issue voor een Jira-comment.", true); return; }
        var payload = new { direction = "freeform", zendeskTicketId = _selectedZendesk?.Id, jiraKey, text, destination };
        await CommitAndReportAsync(payload, "Geplaatst.");
    }

    private async Task CommitAndReportAsync(object payload, string successMsg)
    {
        SetBusy(true, "Doorzetten…");
        var res = await _api.CommitBridgeAsync(payload);
        SetBusy(false, null);
        if (!res.Success) { AddSystemLine("⚠ " + (res.Error ?? "Doorzetten mislukt.")); SetStatus(res.Error ?? "Doorzetten mislukt.", true, res.ErrorType); return; }
        var msg = "✅ " + successMsg + WarnSuffix(res.Warnings);
        AddSystemLine(msg);
        await PersistSystemAsync(msg);
        SetStatus("", false);
    }

    private async void CreateJiraFromProposal(BridgeGenerated? g)
    {
        var projects = await _api.GetJiraProjectsAsync();
        if (projects == null || projects.Length == 0)
        { SetStatus("Geen Jira-projecten gevonden. Koppel eerst Jira bij Integraties.", true); return; }

        var draft = new JiraTicketDraft
        {
            Summary = g?.Summary ?? "",
            Description = g?.Description ?? "",
            IssueType = string.IsNullOrWhiteSpace(g?.IssueType) ? "Task" : g!.IssueType!,
            Priority = string.IsNullOrWhiteSpace(g?.Priority) ? "Medium" : g!.Priority!,
            Labels = g?.Labels?.ToArray() ?? Array.Empty<string>()
        };

        var review = new JiraTicketReviewWindow(_api, draft, projects);
        await review.ShowModalAsync(this);
        if (review.CreatedTicket != null)
        {
            var jira = review.CreatedTicket;
            SetBusy(true, _config.KoppelingAutoBacklink ? "Jira-link terugkoppelen in Zendesk…" : "Koppeling opslaan…");
            var link = await _api.LinkBridgeAsync(_selectedZendesk!.Id, jira.Key, jira.Url, backlink: _config.KoppelingAutoBacklink);
            SetBusy(false, null);
            _selectedJira = new BridgeSourceItem { Id = jira.Key, Title = draft.Summary, Url = jira.Url };
            ShowChip(JiraSelectedChip, JiraSelectedText, $"{jira.Key} · {draft.Summary}");
            UpdateOptionChips();
            var msg = link.Success
                ? $"✅ {jira.Key} aangemaakt en gekoppeld aan Zendesk #{_selectedZendesk!.Id}." + WarnSuffix(link.Warnings)
                : $"{jira.Key} is aangemaakt, maar terugkoppelen mislukte: {link.Error}";
            AddSystemLine(msg);
            await PersistSystemAsync(msg);
        }
    }

    private async Task RunCoupleAsync()
    {
        if (_selectedZendesk == null || _selectedJira == null) { SetStatus("Kies zowel Zendesk als Jira.", true); return; }
        SetBusy(true, "Koppeling opslaan…");
        var res = await _api.LinkBridgeAsync(_selectedZendesk.Id, _selectedJira.Id, _selectedJira.Url, backlink: _config.KoppelingAutoBacklink);
        SetBusy(false, null);
        if (!res.Success) { AddSystemLine("⚠ " + (res.Error ?? "Koppelen mislukt.")); SetStatus(res.Error ?? "Koppelen mislukt.", true, res.ErrorType); return; }
        var msg = $"✅ Zendesk #{_selectedZendesk.Id} gekoppeld aan {_selectedJira.Id}." + WarnSuffix(res.Warnings);
        AddSystemLine(msg);
        await PersistSystemAsync(msg);
    }

    // ── Geschiedenis / sessies (Fase 3 vult de persistentie in) ──────────────────
    private async void NewChat_Click(object? sender, RoutedEventArgs e)
    {
        _sessionId = null;
        _pendingClarify = null;
        ChatPanel.Children.Clear();
        ChatPanel.Children.Add(ChatEmptyHint);
        ChatEmptyHint.IsVisible = true;
        SetStatus("Nieuw gesprek.", false);
        await Task.CompletedTask;
    }

    private async void History_Click(object? sender, RoutedEventArgs e)
    {
        HistoryOverlay.IsVisible = true;
        await LoadHistoryAsync();
    }

    private void CloseHistory_Click(object? sender, RoutedEventArgs e) => HistoryOverlay.IsVisible = false;

    // Persistentie — brug-sessies (backend, migratie 033). Faalt stil zodat de chat nooit breekt.
    private async Task EnsureSessionAsync()
    {
        if (_sessionId != null) return;
        string title = _selectedZendesk != null ? $"#{_selectedZendesk.Id} · {_selectedZendesk.Title}"
                     : _selectedJira != null ? $"{_selectedJira.Id} · {_selectedJira.Title}"
                     : "Koppelingen-gesprek";
        var meta = new
        {
            zendeskId = _selectedZendesk?.Id,
            zendeskTitle = _selectedZendesk?.Title,
            zendeskUrl = _selectedZendesk?.Url,
            jiraId = _selectedJira?.Id,
            jiraTitle = _selectedJira?.Title,
            jiraUrl = _selectedJira?.Url
        };
        _sessionId = await _api.CreateBridgeSessionAsync(title, meta);
    }

    private async Task PersistUserAsync(string text)
    {
        await EnsureSessionAsync();
        if (_sessionId != null) await _api.AppendBridgeMessageAsync(_sessionId, "user", new { text });
    }

    private async Task PersistAssistantAsync(string text, string? action)
    {
        await EnsureSessionAsync();
        if (_sessionId != null) await _api.AppendBridgeMessageAsync(_sessionId, "assistant", new { text, action });
    }

    private async Task PersistSystemAsync(string text)
    {
        await EnsureSessionAsync();
        if (_sessionId != null) await _api.AppendBridgeMessageAsync(_sessionId, "system", new { text });
    }

    private async Task LoadHistoryAsync()
    {
        HistoryList.Children.Clear();
        HistoryEmpty.IsVisible = false;
        var resp = await _api.ListBridgeSessionsAsync();
        if (resp.Sessions.Count == 0) { HistoryEmpty.IsVisible = true; return; }
        foreach (var s in resp.Sessions) HistoryList.Children.Add(BuildHistoryRow(s));
    }

    private Control BuildHistoryRow(SessionSummary s)
    {
        var when = "";
        if (DateTime.TryParse(s.UpdatedAt, out var dt)) when = dt.ToLocalTime().ToString("dd-MM HH:mm");
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var del = new Button
        {
            Content = "✕",
            Classes = { "GhostButton" },
            Padding = new Thickness(8, 2, 8, 2), Height = 28, MinWidth = 28,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
        };
        del.Click += async (snd, ev) =>
        {
            if (await _api.DeleteBridgeSessionAsync(s.Id))
            {
                HistoryList.Children.Remove(row);
                if (_sessionId == s.Id) NewChat_Click(this, new RoutedEventArgs());
                if (HistoryList.Children.Count == 0) HistoryEmpty.IsVisible = true;
            }
        };
        DockPanel.SetDock(del, Dock.Right);
        row.Children.Add(del);

        var open = new Button
        {
            Content = new TextBlock
            {
                Text = (string.IsNullOrWhiteSpace(s.Title) ? "Gesprek" : s.Title) + (when.Length > 0 ? $"   ·   {when}" : ""),
                TextTrimming = TextTrimming.CharacterEllipsis
            },
            Classes = { "GhostButton" },
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 7, 12, 7),
            Height = 36
        };
        open.Click += async (snd, ev) => await OpenSessionAsync(s.Id);
        row.Children.Add(open);

        return row;
    }

    private async Task OpenSessionAsync(string id)
    {
        HistoryOverlay.IsVisible = false;
        SetBusy(true, "Gesprek laden…");
        var resp = await _api.GetBridgeSessionAsync(id);
        SetBusy(false, null);

        _sessionId = id;
        _pendingClarify = null;
        _selectedZendesk = null; _selectedJira = null;
        ZenSelectedChip.IsVisible = false;
        JiraSelectedChip.IsVisible = false;

        ChatPanel.Children.Clear();
        ChatPanel.Children.Add(ChatEmptyHint);
        ChatEmptyHint.IsVisible = false;

        foreach (var m in resp.Messages)
        {
            var text = GetJson(m.Content, "text");
            switch (m.Role)
            {
                case "meta": RestoreSelection(m.Content); break;
                case "user": if (!string.IsNullOrEmpty(text)) AddUserBubble(text!); break;
                case "assistant": if (!string.IsNullOrWhiteSpace(text)) AddAssistantBubble(text!); break;
                case "system": if (!string.IsNullOrEmpty(text)) AddSystemLine(text!); break;
            }
        }
        UpdateOptionChips();
    }

    private void RestoreSelection(JsonElement meta)
    {
        var zId = GetJson(meta, "zendeskId");
        if (!string.IsNullOrEmpty(zId))
        {
            _selectedZendesk = new BridgeSourceItem { Id = zId!, Title = GetJson(meta, "zendeskTitle") ?? "", Url = GetJson(meta, "zendeskUrl") };
            ShowChip(ZenSelectedChip, ZenSelectedText, $"#{_selectedZendesk.Id} · {_selectedZendesk.Title}");
        }
        var jId = GetJson(meta, "jiraId");
        if (!string.IsNullOrEmpty(jId))
        {
            _selectedJira = new BridgeSourceItem { Id = jId!, Title = GetJson(meta, "jiraTitle") ?? "", Url = GetJson(meta, "jiraUrl") };
            ShowChip(JiraSelectedChip, JiraSelectedText, $"{_selectedJira.Id} · {_selectedJira.Title}");
        }
    }

    private static string? GetJson(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
           ? v.GetString() : null;

    // ── Helpers ──────────────────────────────────────────────────────────────────
    private static string WarnSuffix(List<string>? warnings) =>
        (warnings != null && warnings.Count > 0) ? "  ⚠ " + string.Join(" ", warnings) : "";

    private void SetStatus(string text, bool isError, string? errorType = null)
    {
        StatusText.Text = text;
        StatusText.Foreground = isError ? Ui.Brush("Danger") : Ui.Brush("Text3");
        bool showGoto = errorType == "INTEGRATION_NOT_FOUND" || errorType == "AUTH_FAILED";
        GoToIntegrationsButton.IsVisible = showGoto;
    }

    private void GoToIntegrationsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw) { mw.NavigateToIntegrations(); mw.Activate(); Close(); }
    }

    private void SetBusy(bool busy, string? message)
    {
        SendButton.IsEnabled = !busy;
        if (message != null) SetStatus(message, false);
    }
}
