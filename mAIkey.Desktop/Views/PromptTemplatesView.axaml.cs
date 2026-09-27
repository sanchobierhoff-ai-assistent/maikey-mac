using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

public class HotkeyNavigationEventArgs : EventArgs
{
    public string HotkeyId { get; }
    public HotkeyNavigationEventArgs(string hotkeyId) => HotkeyId = hotkeyId;
}

/// <summary>
/// Templatebibliotheek (port van Views/PromptTemplatesView): templates uit de backend (met
/// lokale cache), toevoegen met deelbare code, zoeken, en een ingebouwde terugvalset.
/// </summary>
public partial class PromptTemplatesView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;
    private readonly Dictionary<Border, string> _search = new();

    private static readonly string CachePath = Path.Combine(
        string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "mAIkey", "prompt_templates_cache.json");

    public event EventHandler<HotkeyNavigationEventArgs>? NavigateBack;

    public PromptTemplatesView() : this(App.Config, App.Api) { }

    public PromptTemplatesView(ConfigService config, ApiClient apiClient)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;

        PageTitle.Text = L.T("Templates_Title");
        PageSubtitle.Text = L.T("Templates_Subtitle");
        CodeLabel.Text = L.T("Templates_AddByCodeLabel");
        CodeBox.Watermark = L.T("Templates_AddByCodePlaceholder");
        AddByCodeBtn.Content = L.T("Templates_AddByCodeBtn");
        SearchLabel.Text = L.T("Templates_SearchLabel");
        SearchBox.Watermark = L.T("Templates_SearchPlaceholder");

        ShowTemplates(BuiltInTemplates());
        AttachedToVisualTree += async (_, _) => await LoadRemoteTemplatesAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    // ═══ Laden ═══

    private async Task LoadRemoteTemplatesAsync()
    {
        try
        {
            var response = await _apiClient.GetPromptTemplatesAsync(_config.InterfaceLanguage);
            if (response?.Templates?.Count > 0)
            {
                _ = SaveCacheAsync(response.Templates);
                ShowTemplates(response.Templates);
                return;
            }
        }
        catch { }

        var cached = await LoadCacheAsync();
        if (cached?.Count > 0) ShowTemplates(cached);
    }

    private static async Task SaveCacheAsync(List<RemotePromptTemplate> templates)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(templates));
        }
        catch { }
    }

    private static async Task<List<RemotePromptTemplate>?> LoadCacheAsync()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            return JsonSerializer.Deserialize<List<RemotePromptTemplate>>(await File.ReadAllTextAsync(CachePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    // ═══ Opbouw ═══

    private void ShowTemplates(List<RemotePromptTemplate> templates)
    {
        TemplatesContainer.Children.Clear();
        _search.Clear();

        foreach (var group in templates.GroupBy(t => t.Category))
        {
            var icon = group.Key switch
            {
                "PRODUCTIVITEIT" or "PRODUCTIVITY" => "⚡",
                "COMMUNICATIE" or "COMMUNICATION" => "💬",
                "ONTWIKKELING" or "DEVELOPMENT" => "💻",
                "CREATIEF" or "CREATIVE" => "🎨",
                "ANALYSE" or "ANALYSIS" => "🔍",
                "INTEGRATIES" or "INTEGRATIONS" => "🔗",
                _ => "📁"
            };

            var inner = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };
            int i = 0;
            foreach (var tpl in group)
            {
                var card = BuildTemplateCard(tpl);
                // Eerste kaart krijgt de naam "Template1" voor de onboarding-tour.
                if (TemplatesContainer.Children.Count == 0 && i == 0) card.Name = "Template1";
                _search[card] = $"{tpl.Name} {tpl.Description} {tpl.Category} {tpl.ShareCode}".ToLowerInvariant();
                inner.Children.Add(card);
                i++;
            }

            var header = new TextBlock { Text = $"{icon}  {group.Key}", FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            header.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text3"));
            var expander = new Expander
            {
                IsExpanded = TemplatesContainer.Children.Count == 0,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Header = header,
                Content = inner
            };
            expander.Classes.Add("MkExpander");
            TemplatesContainer.Children.Add(expander);
        }
        ApplySearch();
    }

    private Border BuildTemplateCard(RemotePromptTemplate tpl)
    {
        string output = tpl.OutputMode switch
        {
            "replace" => L.T("Templates_OutReplace"),
            "clipboard" => L.T("Templates_OutClipboard"),
            "window" or "prompt" => L.T("Templates_OutWindow"),
            _ => tpl.OutputMode
        };

        TextBlock Meta(string text, bool accent, double left = 0)
        {
            var tb = new TextBlock { Text = text, FontSize = 11, FontWeight = accent ? FontWeight.SemiBold : FontWeight.Normal, Margin = new Thickness(left, 0, 0, 0) };
            tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(accent ? "Accent" : "Text3"));
            return tb;
        }

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        meta.Children.Add(Meta("Output: ", false));
        meta.Children.Add(Meta(output, true));
        meta.Children.Add(Meta("  •  Model: ", false, 10));
        meta.Children.Add(Meta(tpl.Model, true));
        if (!string.IsNullOrWhiteSpace(tpl.ShareCode))
        {
            meta.Children.Add(Meta($"  •  {L.T("Templates_CardCodeLabel")}: ", false, 10));
            meta.Children.Add(Meta(tpl.ShareCode!, true));
        }

        var btn = new Button { Content = L.T("Templates_AddBtn"), Height = 30, Padding = new Thickness(12, 0), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center };
        btn.Classes.Add("GhostButton");
        btn.Click += async (_, _) => await AddTemplateAsync(tpl);

        var inner = new StackPanel();
        var title = new TextBlock { Text = tpl.Name };
        title.Classes.Add("TplTitle");
        inner.Children.Add(title);
        if (!string.IsNullOrWhiteSpace(tpl.Description))
        {
            var desc = new TextBlock { Text = tpl.Description };
            desc.Classes.Add("TplDesc");
            inner.Children.Add(desc);
        }
        inner.Children.Add(meta);
        inner.Children.Add(btn);

        var card = new Border { Child = inner };
        card.Classes.Add("TplCard");
        return card;
    }

    // ═══ Zoeken ═══

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        foreach (var expander in TemplatesContainer.Children.OfType<Expander>())
        {
            if (expander.Content is not StackPanel inner) continue;
            bool any = false;
            foreach (var card in inner.Children.OfType<Border>())
            {
                bool match = q.Length == 0 || (_search.TryGetValue(card, out var kw) && kw.Contains(q));
                card.IsVisible = match;
                any |= match;
            }
            expander.IsVisible = any;
            if (any && q.Length > 0) expander.IsExpanded = true;
        }
    }

    // ═══ Toevoegen ═══

    private void CodeBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = AddByCodeAsync();
        }
    }

    private void AddByCodeBtn_Click(object? sender, RoutedEventArgs e) => _ = AddByCodeAsync();

    private async Task AddByCodeAsync()
    {
        var code = CodeBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return;
        AddByCodeBtn.IsEnabled = false;
        try
        {
            RemotePromptTemplate? tpl;
            try { tpl = await _apiClient.GetTemplateByCodeAsync(code); }
            catch
            {
                await MkDialog.ShowError(L.T("Templates_CodeNotFoundTitle"), L.T("Templates_CodeErrorDesc"), Owner);
                return;
            }
            if (tpl == null)
            {
                await MkDialog.ShowInfo(L.T("Templates_CodeNotFoundTitle"), L.Tf("Templates_CodeNotFoundDesc", code), Owner);
                return;
            }
            CodeBox.Text = "";
            await AddTemplateAsync(tpl);
        }
        finally { AddByCodeBtn.IsEnabled = true; }
    }

    private async Task AddTemplateAsync(RemotePromptTemplate tpl)
    {
        // Ingebouwde "Vertaal"-template vraagt eerst de doeltaal (zoals Windows).
        if (tpl.Id == "builtin-translate")
        {
            var dialog = await new InputPromptWindow(
                customHeader: Loc.T("Templates_TranslateHeader", "Vertaal naar…"),
                customSubtitle: Loc.T("Templates_TranslateSubtitle", "Naar welke taal moet de tekst vertaald worden?"),
                customHint: Loc.T("Templates_TranslateHint", "Bijv. Engels, Frans, Duits..."),
                showSkipButton: false).ShowAndWaitAsync();
            if (!dialog.Confirmed || string.IsNullOrWhiteSpace(dialog.UserPrompt)) return;
            var target = dialog.UserPrompt.Trim();
            tpl = new RemotePromptTemplate
            {
                Name = $"Vertaal → {target}",
                CustomPrompt = $"een tekst in een vreemde taal. Detecteer de brontaal automatisch en vertaal deze naar {target}. Behoud de originele betekenis en toon.",
                OutputMode = "replace", Model = "gpt-5-nano", Temperature = 0.3f
            };
        }

        var data = await TemplateApplyHelper.BuildAsync(tpl, _apiClient, _config, Owner);
        if (data == null) return;
        await AddFromApplyDataAsync(data);
    }

    /// <summary>
    /// Nieuwe mAIkey uit een template: tier-limiet checken, een vrije ⌃[1-9]-combinatie zoeken,
    /// opslaan en naar de editor gaan met de nieuwe mAIkey geselecteerd.
    /// </summary>
    private async Task AddFromApplyDataAsync(TemplateApplyData data)
    {
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success && status.MaxHotkeys.HasValue &&
                _config.Hotkeys.Count(h => !h.FrozenByDowngrade) >= status.MaxHotkeys.Value)
            {
                await MkDialog.ShowInfo(L.T("Templates_HotkeyLimitTitle"), L.Tf("Templates_HotkeyLimitDesc", status.Tier, status.MaxHotkeys.Value), Owner);
                return;
            }
        }
        catch { }

        int? next = null;
        for (int i = 1; i <= 9; i++)
        {
            int keyCode = HotkeyKeys.D0 + i;
            if (!_config.Hotkeys.Any(h => h.ModifierKeys == 2 && h.Key == keyCode)) { next = i; break; }
        }
        if (next == null)
        {
            await MkDialog.ShowError(L.T("Templates_NoSlotsTitle"), L.T("Templates_NoSlotsDesc"), Owner);
            return;
        }

        var hotkey = new HotkeyConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = data.Name,
            Description = HotkeyKeys.Format(2, HotkeyKeys.D0 + next.Value),
            ModifierKeys = 2,
            Key = HotkeyKeys.D0 + next.Value,
            CustomPrompt = data.Prompt,
            Model = data.Model,
            OutputMode = data.OutputMode == "prompt" ? "window" : data.OutputMode,
            AskForContext = data.AskForContext || data.OutputMode == "prompt",
            Enabled = true,
            CustomAIParameters = new AIParameters { Temperature = data.Temperature, MaxTokens = 8000 },
            TemplateVariables = data.TemplateVariables,
            IncludeImages = data.IncludeImages,
            StyleId = data.StyleId,
            UseScreenCapture = data.UseScreenCapture,
            IntegrationType = data.IntegrationType,
            IntegrationAction = data.IntegrationAction
        };
        _config.Hotkeys = _config.Hotkeys.Append(hotkey).ToArray();
        NavigateBack?.Invoke(this, new HotkeyNavigationEventArgs(hotkey.Id));
    }

    // ═══ Ingebouwde terugvalset (zelfde als de Windows-app zonder verbinding) ═══

    private static RemotePromptTemplate T(string cat, string name, string desc, string prompt, string mode, string model,
        double temp, params (string key, string label, string ph)[] vars) => new()
    {
        Category = cat, Name = name, Description = desc, CustomPrompt = prompt, OutputMode = mode, Model = model,
        Temperature = (float)temp,
        TemplateVariables = vars.Select(v => new RemoteTemplateVariable { Key = v.key, Label = v.label, Placeholder = v.ph }).ToList()
    };

    private static List<RemotePromptTemplate> BuiltInTemplates() => new()
    {
        new RemotePromptTemplate { Id = "builtin-translate", Category = "PRODUCTIVITEIT", Name = "Vertaal", Description = "Vertaalt geselecteerde tekst naar de door jou gekozen taal. Detecteert de brontaal automatisch.", OutputMode = "replace", Model = "gpt-5-nano" },
        T("PRODUCTIVITEIT", "Spelling & Grammatica", "Corrigeert spelling- en grammaticafouten. Behoudt de originele stijl.", "een tekst die mogelijk fouten bevat. Corrigeer alle spelling- en grammaticafouten en behoud de originele stijl en betekenis.", "replace", "gpt-5-nano", 0.2),
        T("PRODUCTIVITEIT", "Vat Samen", "Maakt een beknopte samenvatting met de belangrijkste punten.", "een lange tekst. Maak hiervan een beknopte samenvatting en behoud de belangrijkste punten.", "window", "gpt-5-nano", 0.5),
        T("PRODUCTIVITEIT", "Maak Bullet Points", "Zet tekst om naar duidelijke bullet points.", "een tekst die gestructureerd moet worden. Zet deze om naar duidelijke bullet points.", "replace", "gpt-5-nano", 0.4),
        T("PRODUCTIVITEIT", "¶  Bullets → Alinea's", "Zet bullet points om naar vloeiende alinea's met goede overgangen.", "een lijst met bullet points. Zet deze om naar vloeiende alinea's met goede overgangen.", "replace", "gpt-5-mini", 0.6),
        T("PRODUCTIVITEIT", "Verbeter Leesbaarheid", "Maakt zinnen korter en duidelijker waar nodig.", "een tekst die moeilijk leesbaar is. Verbeter de leesbaarheid en maak zinnen korter en duidelijker waar nodig.", "replace", "gpt-5-mini", 0.5),
        T("COMMUNICATIE", "LinkedIn Post", "Maakt een boeiende LinkedIn post met korte alinea's en hashtags.", "informatie voor een LinkedIn post. Maak hiervan een pakkende post met korte alinea's. Voeg alleen hashtags toe als ze direct relevant zijn. Verzin geen feiten, statistieken of voordelen die niet in de input staan.", "window", "gpt-5-mini", 0.8,
            ("naam", "Jouw naam", "Jan de Vries"), ("bedrijf", "Bedrijf / organisatie", "Maikey BV"), ("sector", "Sector / branche", "Software / SaaS")),
        T("COMMUNICATIE", "😊  Formeel → Informeel", "Herschrijft tekst in een informele, toegankelijke toon.", "een formele tekst. Herschrijf deze in een informele, toegankelijke toon en behoud de kernboodschap.", "replace", "gpt-5-mini", 0.5),
        T("COMMUNICATIE", "👔  Informeel → Formeel", "Herschrijft tekst in een professionele, zakelijke toon.", "een informele tekst. Herschrijf deze in een professionele, zakelijke toon.", "replace", "gpt-5-mini", 0.4,
            ("naam", "Jouw naam (voor ondertekening)", "Jan de Vries")),
        T("COMMUNICATIE", "Support E-mail", "Schrijft een behulpzame support reactie.", "een klantvraag of probleem. Schrijf hierop een empathische support reactie. Gebruik alleen de informatie die beschikbaar is. Verzin geen oplossingen, deadlines of beloften die niet gegeven zijn.", "prompt", "gpt-5-mini", 0.6,
            ("naam", "Jouw naam", "Jan de Vries"), ("bedrijf", "Bedrijfsnaam (optioneel)", "Acme BV")),
        T("ONTWIKKELING", "Code Uitleggen", "Legt code uit in begrijpelijke taal met alle belangrijke details.", "code. Leg in begrijpelijke taal uit wat deze code doet, hoe het werkt, en welke belangrijke details relevant zijn.", "window", "gpt-5-mini", 0.3),
        T("ONTWIKKELING", "Code Documentatie", "Voegt duidelijke commentaar toe aan code.", "code. Voeg hieraan duidelijke commentaar toe. Schrijf commentaar in dezelfde taal als de bestaande code of de omringende tekst.", "replace", "gpt-4o-mini", 0.3),
        T("ONTWIKKELING", "Code Schrijven", "Schrijft of wijzigt code gebaseerd op een beschrijving.", "een beschrijving of bestaande code. Schrijf of wijzig code op basis van de input. Lever altijd volledige, direct uitvoerbare code. Laat geen regels weg.", "replace", "gpt-5-mini", 0.2),
        T("CREATIEF", "Brainstorm Ideeën", "Genereert creatieve, originele ideeën gebaseerd op je tekst.", "een onderwerp of concept. Genereer hierover creatieve ideeën.", "window", "gpt-5", 0.9),
        T("CREATIEF", "Maak Catchy Titel", "Genereert pakkende titels voor je tekst.", "content die een titel nodig heeft. Genereer meerdere pakkende titels hiervoor.", "window", "gpt-5-mini", 0.8),
        T("CREATIEF", "Sentiment Check", "Analyseert de toon van de tekst en geeft een korte uitleg.", "een tekst waarvan de toon geanalyseerd moet worden. Bepaal of deze positief, negatief of neutraal is en geef een korte uitleg.", "window", "gpt-5-nano", 0.3),
        T("CREATIEF", "Actiepunten Extraheren", "Haalt alle actiepunten en taken uit de tekst.", "een tekst met mogelijke actiepunten. Haal alle actiepunten en taken hieruit.", "window", "gpt-5-nano", 0.2),
    };
}
