using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

/// <summary>Sneltoets-editor (port van Views/HotkeyEditorView).</summary>
public partial class HotkeyEditorView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;
    private readonly ObservableCollection<HotkeyViewModel> _hotkeyViewModels = new();
    private HotkeyConfig? _selectedHotkey;
    private bool _isLoadingHotkey;
    private HashSet<string>? _connectedIntegrationTypes;
    private List<RemotePromptTemplate>? _templatePickerTemplates;
    private bool _templatePickerLoading;
    private List<string> _highUsageModels = new();
    private bool _isFreeTierForWarning;

    private static readonly string[] HotkeyColors = { "#6B5BA6", "#3A6EA5", "#3A8F7A", "#A98A3F" };

    public event EventHandler? NavigateBack;

    public HotkeyEditorView() : this(App.Config, App.Api) { }

    public HotkeyEditorView(ConfigService config, ApiClient apiClient, string? hotkeyIdToSelect = null)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;

        for (int i = 0; i < _config.Hotkeys.Length; i++)
            _hotkeyViewModels.Add(NewViewModel(_config.Hotkeys[i], i));
        HotkeyListBox.ItemsSource = _hotkeyViewModels;

        ApplyLocalization();

        HotkeyTextBox.AddHandler(KeyDownEvent, HotkeyTextBox_KeyDown, RoutingStrategies.Tunnel);
        MaxTokensValueBox.AddHandler(TextInputEvent, (_, e) =>
        {
            if (e.Text != null && !e.Text.All(char.IsDigit)) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        ModelComboBox.SelectionChanged += ModelComboBox_SelectionChanged;

        _isLoadingHotkey = true;
        ModelComboBox.SetModels(ModelPicker.FallbackModels);
        LoadStyles();
        _isLoadingHotkey = false;

        AttachedToVisualTree += async (_, _) =>
        {
            _ = LoadConnectedIntegrationsAsync();
            await LoadAvailableModelsAsync();
        };

        if (!string.IsNullOrEmpty(hotkeyIdToSelect))
        {
            var idx = _hotkeyViewModels.ToList().FindIndex(vm => vm.Config.Id == hotkeyIdToSelect);
            HotkeyListBox.SelectedIndex = idx >= 0 ? idx : (_hotkeyViewModels.Count > 0 ? 0 : -1);
        }
        else if (_hotkeyViewModels.Count > 0) HotkeyListBox.SelectedIndex = 0;

        UpdateEmptyState();
    }

    private static HotkeyViewModel NewViewModel(HotkeyConfig h, int index) => new()
    {
        Config = h,
        Description = h.Description,
        Name = h.Name,
        Enabled = h.Enabled,
        Frozen = h.FrozenByDowngrade,
        ColorBrush = new SolidColorBrush(Color.Parse(HotkeyColors[index % HotkeyColors.Length]))
    };

    /// <summary>Selecteer een mAIkey op id (gebruikt door de tour).</summary>
    public void SelectHotkey(string id)
    {
        var idx = _hotkeyViewModels.ToList().FindIndex(vm => vm.Config.Id == id);
        if (idx >= 0) HotkeyListBox.SelectedIndex = idx;
    }

    public void TourAddDemo() => SelectHotkey(Services.HotkeyRuntime.DemoHotkeyId);

    private void UpdateEmptyState()
    {
        bool isEmpty = _hotkeyViewModels.Count == 0;
        EmptyStatePanel.IsVisible = isEmpty;
        EditorScrollViewer.IsVisible = !isEmpty;
        SaveHotkeyBtn.IsVisible = !isEmpty;
        if (isEmpty)
        {
            DeleteButton.IsVisible = false;
            TourBtn.IsVisible = false;
        }
    }

    private void ApplyLocalization()
    {
        HotkeyListHeader.Text = L.T("HotkeyEditor_Title");
        AddHotkeyBtn.Content = L.T("HotkeyEditor_NewHotkey");
        TemplatePickerHeader.Text = L.T("HotkeyEditor_TemplatePickerHeader");
        TemplatePickerSearchBox.Watermark = L.T("HotkeyEditor_TemplatePickerPlaceholder");
        BackButton.Content = L.T("HotkeyEditor_Back");
        DeleteButton.Content = L.T("HotkeyEditor_Delete");
        SaveHotkeyBtn.Content = L.T("HotkeyEditor_Save");
        ToolTip.SetTip(TourBtn, L.T("HotkeyEditor_TourTooltip"));
        TourBtnLabel.Text = L.T("HotkeyEditor_TourLabel");
        EmptyStateTitle.Text = L.T("HotkeyEditor_EmptyTitle");
        EmptyStateSub.Text = L.T("HotkeyEditor_EmptySub");
        EmptyStateTourBtn.Content = L.T("HotkeyEditor_EmptyTourLink");
        FrozenBannerTitle.Text = L.T("HotkeyEditor_FrozenTitle");
        FrozenBannerBody.Text = L.T("HotkeyEditor_FrozenBody");
        TriggerSectionHeader.Text = L.T("HotkeyEditor_TriggerSection");
        KeyComboLabel.Text = L.T("HotkeyEditor_KeyCombo");
        KeyComboHintLabel.Text = Loc.T("HotkeyEditor_KeyComboHint_Mac", L.T("HotkeyEditor_KeyComboHint"));
        PromptSectionHeader.Text = L.T("HotkeyEditor_PromptSection");
        PromptLabel.Text = L.T("HotkeyEditor_PromptLabel");
        PromptHintLabel.Text = L.T("HotkeyEditor_PromptHint");
        AIModelLabel.Text = L.T("HotkeyEditor_AIModel");
        WriteStyleLabel.Text = L.T("HotkeyEditor_WriteStyle");
        StyleHintText.Text = L.T("HotkeyEditor_StyleHint");
        AddStyleLinkText.Text = L.T("HotkeyEditor_StyleLinkText");
        PersonalContextLabel.Text = L.T("HotkeyEditor_PersonalContext");
        PersonalContextHint.Text = Loc.T("HotkeyEditor_PersonalContextHint", "Deze waarden worden automatisch meegestuurd bij elke uitvoering.");
        BehaviorSectionHeader.Text = L.T("HotkeyEditor_BehaviorSection");
        OutputModeLabel.Text = L.T("HotkeyEditor_OutputMode");
        GHSectionHeader.Text = L.T("HotkeyEditor_GHSection");
        GHRepoLabel.Text = Loc.T("HotkeyEditor_GHRepoLabel", "Repository (optioneel, overschrijft integratie-instelling)");
        GHLabelsLabel.Text = Loc.T("HotkeyEditor_GHLabelsLabel", "Labels (optioneel, kommagescheiden)");
        SlackSectionHeader.Text = L.T("HotkeyEditor_SlackSection");
        SlackPanelHint.Text = Loc.T("HotkeyEditor_SlackPanelHint", "Berichten worden verstuurd naar het geconfigureerde Slack-kanaal uit de integratie-instellingen.");
        GmailSectionHeader.Text = L.T("HotkeyEditor_GmailSection");
        GmailToLabel.Text = Loc.T("HotkeyEditor_GmailTo", "Aan (optioneel — overschrijft AI-suggestie)");
        GmailSubjectLabel.Text = Loc.T("HotkeyEditor_GmailSubject", "Onderwerp (optioneel — overschrijft AI-suggestie)");
        GmailCcLabel.Text = Loc.T("HotkeyEditor_GmailCc", "CC (optioneel)");
        CalSectionHeader.Text = L.T("HotkeyEditor_CalSection");
        CalPanelHint.Text = Loc.T("HotkeyEditor_CalPanelHint", "Gebruikt de standaard agenda uit je Google Agenda-integratie.");
        AskForContextCheckbox.Content = L.T("HotkeyEditor_AskContext");
        IncludeImagesCheckbox.Content = L.T("HotkeyEditor_SendImages");
        UseScreenCaptureCheckbox.Content = L.T("Editor_UseScreenCapture");
        UseInputInsteadOfSelectionCheckbox.Content = L.T("HotkeyEditor_UseInput");
        IncludeAssistantContextCheckbox.Content = L.T("HotkeyEditor_IncludeAssistantContext");
        UseCustomAIParamsCheckbox.Content = L.T("HotkeyEditor_CustomParams");
        ToolTip.SetTip(TemperatureInfoIcon, L.T("AIParam_Temperature_Tooltip"));
        ToolTip.SetTip(MaxTokensInfoIcon, L.T("AIParam_MaxTokens_Tooltip"));
        ToolTip.SetTip(TopPInfoIcon, L.T("AIParam_TopP_Tooltip"));
        ToolTip.SetTip(FrequencyPenaltyInfoIcon, L.T("AIParam_FrequencyPenalty_Tooltip"));
        ToolTip.SetTip(PresencePenaltyInfoIcon, L.T("AIParam_PresencePenalty_Tooltip"));
        CreateWithAIButton.Content = L.T("HotkeyEditor_AIBuilder");
        OptimizeButton.Content = L.T("HotkeyEditor_AIOptimizer");
        RebuildOutputModeComboBox();
    }

    // ═══ Output-modus ═══

    private static readonly (string tag, string key, string? integration)[] AllOutputModes =
    {
        ("window", "HotkeyEditor_OutWindow", null),
        ("replace", "HotkeyEditor_OutReplace", null),
        ("append", "HotkeyEditor_OutAppend", null),
        ("clipboard", "HotkeyEditor_OutCopy", null),
        ("jira_review", "HotkeyEditor_OutJiraReview", "jira"),
        ("jira_direct", "HotkeyEditor_OutJira", "jira"),
        ("github_review", "HotkeyEditor_OutGitHubReview", "github"),
        ("github_direct", "HotkeyEditor_OutGitHub", "github"),
        ("slack_review", "HotkeyEditor_OutSlackReview", "slack"),
        ("slack_direct", "HotkeyEditor_OutSlack", "slack"),
        ("teams_review", "HotkeyEditor_OutTeamsReview", "teams"),
        ("teams_direct", "HotkeyEditor_OutTeams", "teams"),
        ("zapier_review", "HotkeyEditor_OutZapierReview", "zapier"),
        ("zapier_direct", "HotkeyEditor_OutZapier", "zapier"),
        ("todoist_review", "HotkeyEditor_OutTodoistReview", "todoist"),
        ("todoist_direct", "HotkeyEditor_OutTodoist", "todoist"),
        ("trello_review", "HotkeyEditor_OutTrelloReview", "trello"),
        ("trello_direct", "HotkeyEditor_OutTrello", "trello"),
        ("asana_review", "HotkeyEditor_OutAsanaReview", "asana"),
        ("asana_direct", "HotkeyEditor_OutAsana", "asana"),
        ("gmail_review", "HotkeyEditor_OutEmailReview", "gmail"),
        ("calendar_review", "HotkeyEditor_OutCalendarReview", "google_calendar"),
        ("gtasks_review", "HotkeyEditor_OutGTasksReview", "google_tasks"),
        ("gtasks_direct", "HotkeyEditor_OutGTasks", "google_tasks"),
    };

    private static readonly Dictionary<string, string> OrphanKeys = new()
    {
        ["gmail_send"] = "HotkeyEditor_OutEmail",
        ["calendar_direct"] = "HotkeyEditor_OutCalendar",
    };

    private string? SelectedOutputTag => (OutputModeComboBox.SelectedItem as ComboItem)?.Value as string;

    private void RebuildOutputModeComboBox(string? preserveTag = null)
    {
        var currentTag = preserveTag ?? SelectedOutputTag;
        bool showAll = _connectedIntegrationTypes == null;

        var items = AllOutputModes
            .Where(m => m.integration == null || showAll || _connectedIntegrationTypes!.Contains(m.integration))
            .Select(m => new ComboItem(L.T(m.key), m.tag))
            .ToList();

        if (!string.IsNullOrEmpty(currentTag) && items.All(i => (string?)i.Value != currentTag))
        {
            var key = AllOutputModes.FirstOrDefault(m => m.tag == currentTag).key
                      ?? (OrphanKeys.TryGetValue(currentTag, out var k) ? k : null);
            items.Add(new ComboItem(key != null ? L.T(key) : currentTag, currentTag));
        }

        OutputModeComboBox.ItemsSource = items;
        var idx = items.FindIndex(i => (string?)i.Value == currentTag);
        OutputModeComboBox.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private async Task LoadConnectedIntegrationsAsync()
    {
        try
        {
            var integrations = await _apiClient.GetIntegrationsAsync();
            if (integrations != null)
                _connectedIntegrationTypes = new HashSet<string>(integrations.Where(i => i.IsActive).Select(i => i.IntegrationType));
        }
        catch { }
        finally
        {
            var wasLoading = _isLoadingHotkey;
            _isLoadingHotkey = true;
            RebuildOutputModeComboBox();
            _isLoadingHotkey = wasLoading;
        }
    }

    private void SelectComboByTag(string tag)
    {
        if (OutputModeComboBox.ItemsSource is not List<ComboItem> items) return;
        var idx = items.FindIndex(i => (string?)i.Value == tag);
        if (idx < 0)
        {
            RebuildOutputModeComboBox(tag);
            return;
        }
        OutputModeComboBox.SelectedIndex = idx;
    }

    // ═══ Modellen ═══

    private async Task LoadAvailableModelsAsync()
    {
        try
        {
            var response = await _apiClient.GetAvailableModelsAsync();
            if (response.Success && response.Models is { Length: > 0 })
            {
                ModelCatalogCache.Update(response.Models);
                var keep = ModelComboBox.SelectedModelId ?? _selectedHotkey?.Model;
                ModelComboBox.SetModels(response.Models);
                ModelComboBox.SelectedModelId = keep ?? "gpt-4o-mini";
            }

            try
            {
                var status = await _apiClient.GetSubscriptionStatusAsync();
                if (status?.Success == true)
                {
                    _highUsageModels = status.HighUsageModels ?? new List<string>();
                    _isFreeTierForWarning = string.Equals(status.TierId, "free", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            UpdateModelUsageWarning(ModelComboBox.SelectedModelId);
        }
        catch { /* fallback-modellen staan al */ }
    }

    private void UpdateModelUsageWarning(string? modelId)
    {
        bool show = _isFreeTierForWarning && !string.IsNullOrEmpty(modelId) && _highUsageModels.Contains(modelId);
        if (show) ModelUsageWarning.Text = L.T("HotkeyEditor_HighUsageModel");
        ModelUsageWarning.IsVisible = show;
    }

    private void ModelComboBox_SelectionChanged(object? sender, EventArgs e)
    {
        var id = ModelComboBox.SelectedModelId;
        UpdateParameterVisibilityForModel(id);
        UpdateModelUsageWarning(id);
        if (!_isLoadingHotkey && _selectedHotkey != null) SaveEditorToHotkey();
    }

    private void UpdateParameterVisibilityForModel(string? modelId)
    {
        if (modelId == null) return;
        bool isGpt5 = modelId.StartsWith("gpt-5");
        bool isClaude = modelId.StartsWith("claude-");
        bool isGemini = modelId.StartsWith("gemini-");
        bool isSmart = modelId is "smart" or "smart-openai";

        if (isSmart || isGemini)
        {
            TemperatureRow.IsVisible = false;
            MaxTokensRow.IsVisible = true;
            MaxTokensLabel.Text = "Max Tokens";
            TopPRow.IsVisible = FrequencyPenaltyRow.IsVisible = PresencePenaltyRow.IsVisible = false;
            return;
        }
        TemperatureRow.IsVisible = !isGpt5;
        MaxTokensRow.IsVisible = true;
        MaxTokensLabel.Text = isGpt5 ? "Max Output Tokens" : "Max Tokens";
        TopPRow.IsVisible = FrequencyPenaltyRow.IsVisible = PresencePenaltyRow.IsVisible = !isGpt5 && !isClaude;
    }

    // ═══ Selectie / laden / opslaan ═══

    private void HotkeyListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HotkeyListBox.SelectedItem is HotkeyViewModel vm)
        {
            _selectedHotkey = vm.Config;
            LoadHotkeyToEditor(vm.Config);
            DeleteButton.IsVisible = true;
            TourBtn.IsVisible = true;
        }
        else
        {
            DeleteButton.IsVisible = false;
            TourBtn.IsVisible = false;
        }
    }

    private void LoadHotkeyToEditor(HotkeyConfig hotkey)
    {
        _isLoadingHotkey = true;
        NameTextBox.Text = hotkey.Name;
        PrefixButtonText.Text = hotkey.PrefixLanguage == "EN" ? "The selected text is" : "De geselecteerde tekst is";
        CustomPromptTextBox.Text = hotkey.CustomPrompt ?? "";
        HotkeyTextBox.Text = HotkeyKeys.Format(hotkey.ModifierKeys, hotkey.Key);

        string tag = hotkey.IntegrationType switch
        {
            "jira" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "jira_review" : "jira_direct",
            "github" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "github_review" : "github_direct",
            "slack" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "slack_review" : "slack_direct",
            "teams" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "teams_review" : "teams_direct",
            "zapier" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "zapier_review" : "zapier_direct",
            "todoist" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "todoist_review" : "todoist_direct",
            "trello" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "trello_review" : "trello_direct",
            "asana" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "asana_review" : "asana_direct",
            "gmail" => "gmail_review",
            "google_calendar" => "calendar_review",
            "google_tasks" => hotkey.IntegrationAction?.ShowReviewWindow != false ? "gtasks_review" : "gtasks_direct",
            _ => hotkey.OutputMode == "prompt" ? "window" : (hotkey.OutputMode ?? "replace")
        };
        SelectComboByTag(tag);

        GitHubRepoBox.Text = hotkey.IntegrationAction?.DefaultRepo ?? "";
        GitHubLabelsBox.Text = hotkey.IntegrationAction?.DefaultLabels ?? "";
        GmailToBox.Text = hotkey.IntegrationAction?.DefaultTo ?? "";
        GmailSubjectBox.Text = hotkey.IntegrationAction?.DefaultSubject ?? "";
        GmailCcBox.Text = hotkey.IntegrationAction?.DefaultCc ?? "";
        UpdatePanelVisibility();

        AskForContextCheckbox.IsChecked = hotkey.AskForContext || hotkey.OutputMode == "prompt";
        IncludeImagesCheckbox.IsChecked = hotkey.IncludeImages;
        IncludeAssistantContextCheckbox.IsChecked = hotkey.IncludeAssistantContext;
        UseScreenCaptureCheckbox.IsChecked = hotkey.UseScreenCapture;
        UseInputInsteadOfSelectionCheckbox.IsChecked = hotkey.UseInputInsteadOfSelection;

        if (!string.IsNullOrEmpty(hotkey.Model))
        {
            ModelComboBox.SelectedModelId = hotkey.Model;
            UpdateParameterVisibilityForModel(hotkey.Model);
        }

        SelectStyle(hotkey.StyleId);

        FrozenBanner.IsVisible = hotkey.FrozenByDowngrade;
        SaveHotkeyBtn.IsEnabled = !hotkey.FrozenByDowngrade;

        LoadTemplateVariablesPanel(hotkey);

        var p = hotkey.CustomAIParameters;
        UseCustomAIParamsCheckbox.IsChecked = p != null;
        AIParametersPanel.IsVisible = p != null;
        TemperatureSlider.Value = p?.Temperature ?? 0.7;
        MaxTokensSlider.Value = p?.MaxTokens ?? 8000;
        TopPSlider.Value = p?.TopP ?? 1.0;
        FrequencyPenaltySlider.Value = p?.FrequencyPenalty ?? 0.0;
        PresencePenaltySlider.Value = p?.PresencePenalty ?? 0.0;
        UpdateParamTexts();

        UpdateModelUsageWarning(ModelComboBox.SelectedModelId);
        _isLoadingHotkey = false;
    }

    private void UpdateParamTexts()
    {
        TemperatureValueText.Text = TemperatureSlider.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        MaxTokensValueBox.Text = ((int)MaxTokensSlider.Value).ToString();
        TopPValueText.Text = TopPSlider.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        FrequencyPenaltyValueText.Text = FrequencyPenaltySlider.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        PresencePenaltyValueText.Text = PresencePenaltySlider.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SaveEditorToHotkey()
    {
        if (_selectedHotkey == null) return;
        var h = _selectedHotkey;
        h.Name = NameTextBox.Text ?? "";
        h.CustomPrompt = CustomPromptTextBox.Text ?? "";
        h.Description = HotkeyTextBox.Text ?? "";

        var tag = SelectedOutputTag;
        if (tag == null)
        {
            h.OutputMode = "replace";
            h.IntegrationType = null;
            h.IntegrationAction = null;
        }
        else
        {
            string? Opt(TextBox t) => string.IsNullOrWhiteSpace(t.Text) ? null : t.Text.Trim();
            (string? type, string action) = tag switch
            {
                "jira_review" or "jira_direct" => ("jira", "create-ticket"),
                "github_review" or "github_direct" => ("github", "create-issue"),
                "slack_review" or "slack_direct" => ("slack", "send-message"),
                "teams_review" or "teams_direct" => ("teams", "send-message"),
                "zapier_review" or "zapier_direct" => ("zapier", "send-data"),
                "todoist_review" or "todoist_direct" => ("todoist", "create-task"),
                "trello_review" or "trello_direct" => ("trello", "create-card"),
                "asana_review" or "asana_direct" => ("asana", "create-task"),
                "gmail_review" or "gmail_send" => ("gmail", "send"),
                "calendar_review" or "calendar_direct" => ("google_calendar", "create-event"),
                "gtasks_review" or "gtasks_direct" => ("google_tasks", "create-task"),
                _ => ((string?)null, "")
            };

            if (type == null)
            {
                h.IntegrationType = null;
                h.IntegrationAction = null;
                h.OutputMode = tag;
            }
            else
            {
                h.IntegrationType = type;
                h.IntegrationAction = new IntegrationAction
                {
                    Action = action,
                    ShowReviewWindow = tag.EndsWith("_review"),
                    DefaultRepo = type == "github" ? Opt(GitHubRepoBox) : null,
                    DefaultLabels = type == "github" ? Opt(GitHubLabelsBox) : null,
                    DefaultTo = type == "gmail" ? Opt(GmailToBox) : null,
                    DefaultSubject = type == "gmail" ? Opt(GmailSubjectBox) : null,
                    DefaultCc = type == "gmail" ? Opt(GmailCcBox) : null,
                };
                h.OutputMode = "replace";
            }
        }

        h.AskForContext = AskForContextCheckbox.IsChecked == true;
        h.IncludeImages = IncludeImagesCheckbox.IsChecked ?? true;
        h.IncludeAssistantContext = IncludeAssistantContextCheckbox.IsChecked == true;
        h.UseScreenCapture = UseScreenCaptureCheckbox.IsChecked == true;
        h.UseInputInsteadOfSelection = UseInputInsteadOfSelectionCheckbox.IsChecked == true;
        if (ModelComboBox.SelectedModelId != null) h.Model = ModelComboBox.SelectedModelId;
        h.StyleId = (StyleComboBox.SelectedItem as StyleOption)?.Id;

        h.CustomAIParameters = UseCustomAIParamsCheckbox.IsChecked == true
            ? new AIParameters
            {
                Temperature = TemperatureSlider.Value,
                MaxTokens = (int)MaxTokensSlider.Value,
                TopP = TopPSlider.Value == 1.0 ? null : TopPSlider.Value,
                FrequencyPenalty = FrequencyPenaltySlider.Value == 0.0 ? null : FrequencyPenaltySlider.Value,
                PresencePenalty = PresencePenaltySlider.Value == 0.0 ? null : PresencePenaltySlider.Value
            }
            : null;

        if (HotkeyListBox.SelectedItem is HotkeyViewModel vm)
        {
            vm.Description = h.Description;
            vm.Name = h.Name;
        }
    }

    private void CustomPromptTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_selectedHotkey != null) _selectedHotkey.CustomPrompt = CustomPromptTextBox.Text;
    }

    private void PrefixDropdownButton_Click(object? sender, RoutedEventArgs e) => PrefixLanguagePopup.IsOpen = true;

    private void PrefixLanguageOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string language })
        {
            PrefixButtonText.Text = language == "EN" ? "The selected text is" : "De geselecteerde tekst is";
            if (_selectedHotkey != null) _selectedHotkey.PrefixLanguage = language;
            PrefixLanguagePopup.IsOpen = false;
        }
    }

    // ═══ Template kiezen ═══

    private async void TemplatePickerSearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = TemplatePickerSearchBox.Text?.Trim() ?? "";
        if (query.Length == 0)
        {
            TemplatePickerResultsBorder.IsVisible = false;
            return;
        }

        if (_templatePickerTemplates == null && !_templatePickerLoading)
        {
            _templatePickerLoading = true;
            try
            {
                var response = await _apiClient.GetPromptTemplatesAsync(_config.InterfaceLanguage);
                _templatePickerTemplates = response?.Templates ?? new List<RemotePromptTemplate>();
            }
            catch { _templatePickerTemplates = new List<RemotePromptTemplate>(); }
            finally { _templatePickerLoading = false; }
        }

        query = TemplatePickerSearchBox.Text?.Trim() ?? "";
        if (query.Length == 0) return;
        var q = query.ToLowerInvariant();
        var matches = (_templatePickerTemplates ?? new List<RemotePromptTemplate>())
            .Where(t => $"{t.Name} {t.Description} {t.Category}".ToLowerInvariant().Contains(q))
            .Take(8).ToList();
        TemplatePickerResultsList.ItemsSource = matches;
        TemplatePickerResultsBorder.IsVisible = matches.Count > 0;
    }

    private async void TemplatePickerResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not RemotePromptTemplate tpl) return;
        TemplatePickerResultsBorder.IsVisible = false;
        TemplatePickerResultsList.ItemsSource = null;
        TemplatePickerSearchBox.Text = "";
        if (_selectedHotkey == null) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (!string.IsNullOrWhiteSpace(_selectedHotkey.CustomPrompt) &&
            !await MkDialog.ShowConfirm(L.T("HotkeyEditor_TemplateOverwriteTitle"), L.T("HotkeyEditor_TemplateOverwriteDesc"), owner))
            return;

        var data = await TemplateApplyHelper.BuildAsync(tpl, _apiClient, _config, owner);
        if (data == null) return;

        var h = _selectedHotkey;
        h.Name = data.Name;
        h.CustomPrompt = data.Prompt;
        h.OutputMode = data.OutputMode == "prompt" ? "window" : data.OutputMode;
        h.Model = data.Model;
        h.AskForContext = data.AskForContext || data.OutputMode == "prompt";
        h.IncludeImages = data.IncludeImages;
        h.TemplateVariables = data.TemplateVariables;
        h.StyleId = data.StyleId;
        h.IntegrationType = data.IntegrationType;
        h.IntegrationAction = data.IntegrationAction;
        h.UseScreenCapture = data.UseScreenCapture;
        h.CustomAIParameters = new AIParameters { Temperature = data.Temperature, MaxTokens = 8000 };

        LoadHotkeyToEditor(h);
        if (HotkeyListBox.SelectedItem is HotkeyViewModel vm) vm.Name = h.Name;
    }

    // ═══ Toevoegen / verwijderen / opslaan ═══

    private async void AddHotkey_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success && status.MaxHotkeys.HasValue && _hotkeyViewModels.Count(vm => !vm.Frozen) >= status.MaxHotkeys.Value)
            {
                if (await MkDialog.ShowUpgrade(Loc.T("HotkeyEditor_TooManyTitle", "Te veel mAIkeys"),
                        Loc.Tf("HotkeyEditor_TooManyBody", "Je {0}-abonnement staat maximaal {1} mAIkeys toe.\n\nUpgrade om meer mAIkeys aan te maken.", status.Tier, status.MaxHotkeys.Value), owner))
                    Ui.Main?.OpenPricingPage();
                return;
            }
        }
        catch { }

        // Zoek een vrije combinatie: ⌃ + cijfer, dan ⌥ + cijfer, dan ⇧ + cijfer (zoals Windows).
        int? foundKey = null;
        int foundMods = 2;
        foreach (var mods in new[] { 2, 1, 4 })
        {
            var occupied = _hotkeyViewModels.Where(vm => vm.Config.Enabled && vm.Config.ModifierKeys == mods).Select(vm => vm.Config.Key).ToHashSet();
            var free = Enumerable.Range(0, 10).Select(i => HotkeyKeys.D0 + i).FirstOrDefault(k => !occupied.Contains(k), -1);
            if (free >= 0) { foundKey = free; foundMods = mods; break; }
        }

        if (foundKey == null)
        {
            await MkDialog.ShowError(L.T("HotkeyEditor_NoCombosTitle"), L.T("HotkeyEditor_NoCombosDesc"), owner);
            return;
        }

        var newHotkey = new HotkeyConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = L.T("HotkeyEditor_NewHotkeyName"),
            Description = HotkeyKeys.Format(foundMods, foundKey.Value),
            ModifierKeys = foundMods,
            Key = foundKey.Value,
            Enabled = true,
            OutputMode = "replace"
        };
        var vmNew = NewViewModel(newHotkey, _hotkeyViewModels.Count);
        _hotkeyViewModels.Add(vmNew);
        UpdateEmptyState();
        HotkeyListBox.SelectedItem = vmNew;
    }

    private void TourBtn_Click(object? sender, RoutedEventArgs e) => Ui.Main?.StartTour("hotkey");

    private void EmptyStateTourBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_hotkeyViewModels.Count == 0)
        {
            var demo = new HotkeyConfig
            {
                Id = Services.HotkeyRuntime.DemoHotkeyId,
                Name = L.CurrentLanguage switch { "en" => "Spelling & Grammar", "de" => "Rechtschreibung & Grammatik", _ => "Spelling & Grammatica" },
                Description = L.CurrentLanguage switch
                {
                    "en" => "Correct spelling and grammar of the selected text",
                    "de" => "Rechtschreibung und Grammatik des markierten Textes korrigieren",
                    _ => "Verbeter spelling en grammatica van de geselecteerde tekst"
                },
                CustomPrompt = L.CurrentLanguage switch
                {
                    "en" => "A text that may contain spelling or grammar mistakes. Check and correct the spelling and grammar. Preserve the original writing style and meaning as much as possible. Return only the corrected text, without any explanation.",
                    "de" => "Ein Text, der möglicherweise Rechtschreib- oder Grammatikfehler enthält. Prüfe und korrigiere Rechtschreibung und Grammatik. Behalte den ursprünglichen Schreibstil und die Bedeutung so weit wie möglich bei. Gib nur den korrigierten Text zurück, ohne Erklärung.",
                    _ => "Een tekst die mogelijk spellings- of grammaticale fouten bevat. Controleer en verbeter de spelling en grammatica. Behoud de oorspronkelijke schrijfstijl en betekenis zo veel mogelijk. Geef alleen de verbeterde tekst terug, zonder uitleg."
                },
                Model = "gpt-4o-mini",
                OutputMode = "replace",
                ModifierKeys = 2,
                Key = HotkeyKeys.D0 + 1,
                Enabled = true,
                IncludeImages = true,
            };
            _hotkeyViewModels.Add(NewViewModel(demo, 0));
            _config.Hotkeys = _hotkeyViewModels.Select(vm => vm.Config).ToArray();
            _config.HasShownDemoHotkey = true;
            UpdateEmptyState();
            HotkeyListBox.SelectedIndex = 0;
        }
        Ui.Main?.StartTour("hotkey");
    }

    private async void DeleteHotkey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (!await MkDialog.ShowConfirm(L.T("HotkeyEditor_ConfirmDelete"),
                $"{L.T("HotkeyEditor_ConfirmDeleteMsg").Replace("?", "")} '{_selectedHotkey.Name}'?", owner))
            return;

        var vm = _hotkeyViewModels.FirstOrDefault(v => v.Config == _selectedHotkey);
        if (vm != null) _hotkeyViewModels.Remove(vm);
        _config.Hotkeys = _hotkeyViewModels.Select(v => v.Config).ToArray();
        _selectedHotkey = null;
        if (_hotkeyViewModels.Count > 0) HotkeyListBox.SelectedIndex = 0;
        UpdateEmptyState();
        Ui.Main?.ReloadHotkeys();
    }

    private async void SaveChanges_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null || _selectedHotkey.FrozenByDowngrade) return;
        var owner = TopLevel.GetTopLevel(this) as Window;

        if (_selectedHotkey.ModifierKeys == 0 || _selectedHotkey.Key == 0)
        {
            await MkDialog.ShowError(L.T("HotkeyEditor_ComboRequired"), L.T("HotkeyEditor_ComboRequiredDesc"), owner);
            return;
        }

        SaveEditorToHotkey();

        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (_selectedHotkey.IncludeImages && status.Success && !status.ImageAnalysis)
            {
                await MkDialog.ShowInfo(L.T("HotkeyEditor_ImagesUnavailTitle"), L.T("HotkeyEditor_ImagesUnavailDesc"), owner);
                _selectedHotkey.IncludeImages = false;
                IncludeImagesCheckbox.IsChecked = false;
            }

            var chosen = _selectedHotkey.Model ?? "";
            bool allowed = status.AllowedModels == null || status.AllowedModels.Count == 0
                           || status.AllowedModels.Contains("*") || status.AllowedModels.Contains(chosen);
            if (status.Success && !string.IsNullOrEmpty(chosen) && !allowed)
            {
                await MkDialog.ShowInfo(L.T("HotkeyEditor_ModelUnavailTitle"), L.Tf("HotkeyEditor_ModelUnavailDesc", chosen, status.Tier), owner);
                _selectedHotkey.Model = "gpt-4o-mini";
                ModelComboBox.SelectedModelId = "gpt-4o-mini";
            }
        }
        catch { }

        _config.Hotkeys = _hotkeyViewModels.Select(vm => vm.Config).ToArray();
        await MkDialog.ShowInfo(L.T("HotkeyEditor_Save"), L.T("HotkeyEditor_SavedOk"), owner);
        NavigateBack?.Invoke(this, EventArgs.Empty);
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => NavigateBack?.Invoke(this, EventArgs.Empty);

    // ═══ Stijlen ═══

    private void LoadStyles()
    {
        var options = new List<StyleOption> { new() { Id = null, Name = L.T("HotkeyEditor_NoStyle") } };
        options.AddRange(_config.GetWritingStyles().Select(s => new StyleOption
        {
            Id = s.Id,
            Name = $"{s.Name} ({s.TextExamples?.Length ?? 0} voorbeelden)"
        }));
        StyleComboBox.ItemsSource = options;
        StyleComboBox.SelectedIndex = 0;
    }

    private void SelectStyle(string? styleId)
    {
        if (StyleComboBox.ItemsSource is not List<StyleOption> options) return;
        var idx = string.IsNullOrEmpty(styleId) ? 0 : options.FindIndex(o => o.Id == styleId);
        StyleComboBox.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void StyleComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoadingHotkey && _selectedHotkey != null)
            _selectedHotkey.StyleId = (StyleComboBox.SelectedItem as StyleOption)?.Id;
    }

    private async void AddStyleLink_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            bool atLimit = status?.Success == true && status.MaxStyleProfiles.HasValue
                           && _config.GetWritingStyles().Length >= status.MaxStyleProfiles.Value;
            if (atLimit)
                await MkDialog.ShowInfo(L.T("StyleTour_LimitTitle"), L.T("StyleTour_LimitBody"), TopLevel.GetTopLevel(this) as Window);
            Ui.Main?.StartTour(atLimit ? "style_edit" : "style_new");
        }
        catch { Ui.Main?.StartTour("style_new"); }
    }

    // ═══ Template-variabelen ═══

    private void LoadTemplateVariablesPanel(HotkeyConfig hotkey)
    {
        TemplateVariablesFieldsPanel.Children.Clear();
        if (hotkey.TemplateVariables == null || hotkey.TemplateVariables.Count == 0)
        {
            TemplateVariablesPanel.IsVisible = false;
            return;
        }
        TemplateVariablesPanel.IsVisible = true;
        foreach (var kv in hotkey.TemplateVariables.ToList())
        {
            var label = new TextBlock { Text = kv.Key, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };
            label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextSecondary"));
            var box = new TextBox { Text = kv.Value, Height = 38, FontSize = 13, Padding = new Thickness(10, 0), Margin = new Thickness(0, 0, 0, 12), VerticalContentAlignment = VerticalAlignment.Center, Tag = kv.Key };
            box.TextChanged += (_, _) =>
            {
                if (_selectedHotkey?.TemplateVariables != null && box.Tag is string key)
                    _selectedHotkey.TemplateVariables[key] = box.Text ?? "";
            };
            TemplateVariablesFieldsPanel.Children.Add(label);
            TemplateVariablesFieldsPanel.Children.Add(box);
        }
    }

    // ═══ Checkboxes / AI-parameters ═══

    private void AskForContext_Changed(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey != null) _selectedHotkey.AskForContext = AskForContextCheckbox.IsChecked == true;
    }

    private void UseInputInsteadOfSelection_Changed(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey != null) _selectedHotkey.UseInputInsteadOfSelection = UseInputInsteadOfSelectionCheckbox.IsChecked == true;
    }

    private void UseScreenCapture_Changed(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey != null) _selectedHotkey.UseScreenCapture = UseScreenCaptureCheckbox.IsChecked == true;
    }

    private void UseCustomAIParams_Changed(object? sender, RoutedEventArgs e)
    {
        AIParametersPanel.IsVisible = UseCustomAIParamsCheckbox.IsChecked == true;
        if (!_isLoadingHotkey) SaveEditorToHotkey();
    }

    private void AIParameter_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_selectedHotkey == null) return;
        UpdateParamTexts();
        if (!_isLoadingHotkey) SaveEditorToHotkey();
    }

    private void CommitValue(TextBox box, Slider slider, string format)
    {
        var input = (box.Text ?? "").Replace(',', '.');
        if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            v = Math.Max(slider.Minimum, Math.Min(slider.Maximum, format == "0" ? Math.Round(v) : Math.Round(v, 2)));
            slider.Value = v;
        }
        UpdateParamTexts();
        if (_selectedHotkey != null) SaveEditorToHotkey();
    }

    private void MaxTokensValueBox_LostFocus(object? sender, RoutedEventArgs e) => CommitValue(MaxTokensValueBox, MaxTokensSlider, "0");
    private void TemperatureValueBox_LostFocus(object? sender, RoutedEventArgs e) => CommitValue(TemperatureValueText, TemperatureSlider, "F2");
    private void TopPValueBox_LostFocus(object? sender, RoutedEventArgs e) => CommitValue(TopPValueText, TopPSlider, "F2");
    private void FrequencyPenaltyValueBox_LostFocus(object? sender, RoutedEventArgs e) => CommitValue(FrequencyPenaltyValueText, FrequencyPenaltySlider, "F2");
    private void PresencePenaltyValueBox_LostFocus(object? sender, RoutedEventArgs e) => CommitValue(PresencePenaltyValueText, PresencePenaltySlider, "F2");

    // ═══ AI-hulp ═══

    private void ApplyAiConfig(AIHotkeyConfig config, bool applyImageOptions)
    {
        NameTextBox.Text = string.IsNullOrEmpty(config.Name) ? NameTextBox.Text : config.Name;
        CustomPromptTextBox.Text = config.CustomPrompt ?? "";
        if (!string.IsNullOrEmpty(config.Model))
        {
            ModelComboBox.SelectedModelId = config.Model;
            UpdateParameterVisibilityForModel(config.Model);
        }
        if (!string.IsNullOrEmpty(config.OutputMode))
        {
            if (config.OutputMode == "prompt") AskForContextCheckbox.IsChecked = true;
            SelectComboByTag(config.OutputMode == "prompt" ? "window" : config.OutputMode);
        }
        if (!string.IsNullOrEmpty(config.StyleId)) SelectStyle(config.StyleId);
        if (applyImageOptions)
        {
            IncludeImagesCheckbox.IsChecked = config.IncludeImages;
            UseScreenCaptureCheckbox.IsChecked = config.UseScreenCapture;
            UseInputInsteadOfSelectionCheckbox.IsChecked = config.UseInputInsteadOfSelection;
        }
        if (config.CustomAIParameters is { } p)
        {
            UseCustomAIParamsCheckbox.IsChecked = true;
            AIParametersPanel.IsVisible = true;
            if (p.Temperature.HasValue) TemperatureSlider.Value = p.Temperature.Value;
            if (p.MaxTokens.HasValue) MaxTokensSlider.Value = p.MaxTokens.Value;
            if (p.TopP.HasValue) TopPSlider.Value = p.TopP.Value;
            if (p.FrequencyPenalty.HasValue) FrequencyPenaltySlider.Value = p.FrequencyPenalty.Value;
            if (p.PresencePenalty.HasValue) PresencePenaltySlider.Value = p.PresencePenalty.Value;
        }
    }

    private async void CreateWithAI_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        try
        {
            var builder = new AIHotkeyBuilderWindow(_apiClient);
            await builder.ShowModalAsync(owner);
            if (builder.GeneratedConfig != null)
            {
                ApplyAiConfig(builder.GeneratedConfig, true);
                await MkDialog.ShowInfo(L.T("Common_Success"), L.T("HotkeyEditor_AIBuilderApplied"), owner);
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Error_General_Title"), $"{L.T("HotkeyEditor_AIBuilderError")}\n\n{ex.Message}", owner);
        }
    }

    private async void OptimizePrompt_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        try
        {
            if (_selectedHotkey == null)
            {
                await MkDialog.ShowError(L.T("HotkeyEditor_NoHotkeySelected"), L.T("HotkeyEditor_NoHotkeySelectedDesc"), owner);
                return;
            }
            SaveEditorToHotkey();

            if ((_selectedHotkey.RecentUsage == null || _selectedHotkey.RecentUsage.Count == 0) &&
                !await MkDialog.ShowConfirm(L.T("HotkeyEditor_NoUsageTitle"), L.T("HotkeyEditor_NoUsageDesc"), owner))
                return;

            var optimizer = new AIPromptOptimizerWindow(_apiClient, _selectedHotkey);
            await optimizer.ShowModalAsync(owner);
            if (optimizer.OptimizedConfig != null)
            {
                ApplyAiConfig(optimizer.OptimizedConfig, false);
                await MkDialog.ShowInfo(L.T("Common_Success"), L.T("HotkeyEditor_OptimizeApplied"), owner);
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Error_General_Title"), $"{L.T("HotkeyEditor_OptimizeError")}\n\n{ex.Message}", owner);
        }
    }

    // ═══ Toetscombinatie vastleggen ═══

    private void HotkeyTextBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(HotkeyTextBox.Text))
            HotkeyTextBox.Watermark = L.T("HotkeyEditor_KeyComboPlaceholder");
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System;

    /// <summary>Avalonia-modifiers → WPF-ModifierKeys-waarden (Alt=1, Ctrl=2, Shift=4, Cmd=8).</summary>
    private static int ToWpfModifiers(KeyModifiers m) =>
        (m.HasFlag(KeyModifiers.Alt) ? 1 : 0) | (m.HasFlag(KeyModifiers.Control) ? 2 : 0) |
        (m.HasFlag(KeyModifiers.Shift) ? 4 : 0) | (m.HasFlag(KeyModifiers.Meta) ? 8 : 0);

    private async void HotkeyTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key;
        if (IsModifierKey(key) || key == Key.None) return;

        int mods = ToWpfModifiers(e.KeyModifiers);
        int keyCode = (int)key; // Avalonia Key == WPF Key (zelfde getallen)
        var owner = TopLevel.GetTopLevel(this) as Window;

        if (mods == 0)
        {
            await MkDialog.ShowInfo(L.T("HotkeyEditor_ModifierRequired"), L.T("HotkeyEditor_ModifierRequiredDesc"), owner);
            return;
        }

        var duplicate = _hotkeyViewModels.FirstOrDefault(vm => vm.Config.Id != _selectedHotkey?.Id && vm.Config.Enabled
                                                                && vm.Config.Key == keyCode && vm.Config.ModifierKeys == mods);
        string combo = HotkeyKeys.Format(mods, keyCode);
        if (duplicate != null)
        {
            await MkDialog.ShowError(L.T("HotkeyEditor_DuplicateTitle"), L.Tf("HotkeyEditor_DuplicateDesc", combo, duplicate.Config.Name), owner);
            return;
        }

        if (IsSystemShortcut(mods, key) &&
            !await MkDialog.ShowConfirm(L.T("HotkeyEditor_SysShortcutTitle"), L.Tf("HotkeyEditor_SysShortcutDesc", combo), owner))
            return;

        if (_selectedHotkey != null)
        {
            _selectedHotkey.ModifierKeys = mods;
            _selectedHotkey.Key = keyCode;
            _selectedHotkey.Description = combo;
        }
        HotkeyTextBox.Text = combo;
    }

    /// <summary>Veelgebruikte macOS-systeemcombinaties waarvoor we waarschuwen.</summary>
    private static bool IsSystemShortcut(int mods, Key key) =>
        (mods == 8 && key is Key.Q or Key.W or Key.Tab or Key.Space or Key.H or Key.M or Key.C or Key.V or Key.X or Key.Z or Key.A)
        || (mods == (8 | 4) && key is Key.D3 or Key.D4 or Key.D5)
        || (mods == (8 | 1) && key == Key.Escape)
        || (mods == 2 && key is Key.Up or Key.Down or Key.Left or Key.Right);

    // ═══ Panelen ═══

    private void OutputModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdatePanelVisibility();

    private void UpdatePanelVisibility()
    {
        var tag = SelectedOutputTag ?? "";
        PromptPanel.IsVisible = true;
        AIOptionsPanel.IsVisible = true;
        GitHubPanel.IsVisible = tag.StartsWith("github");
        SlackPanel.IsVisible = tag.StartsWith("slack");
        GmailPanel.IsVisible = tag.StartsWith("gmail");
        CalendarPanel.IsVisible = tag.StartsWith("calendar");
    }

    public class StyleOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public class HotkeyViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = "";
        private string _description = "";
        public HotkeyConfig Config { get; set; } = null!;
        public string Name { get => _name; set { _name = value; OnChanged(nameof(Name)); } }
        public string Description { get => _description; set { _description = value; OnChanged(nameof(Description)); } }
        public bool Enabled { get; set; }
        public bool Frozen { get; set; }
        public string FrozenLabel => Loc.T("HotkeyEditor_FrozenBadge", "Bevroren");
        public IBrush ColorBrush { get; set; } = Brushes.Purple;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string p) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(p));
    }
}
