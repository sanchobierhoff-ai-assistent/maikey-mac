using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Jira-ticket nakijken en aanmaken (port van Views/JiraTicketReviewWindow): project,
/// issue-type, prioriteit, toegewezene, labels, verplichte/aangepaste velden (createmeta)
/// en een afbeeldingengalerij met AI-relevantie.
/// </summary>
public class JiraTicketReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly JiraTicketDraft _draft;
    private readonly JiraProject[] _projects;
    private readonly string? _defaultProject, _defaultIssueType, _defaultAssignee;

    private readonly TextBox _summary, _description, _labels;
    private readonly TextBlock _charCount;
    private readonly ComboBox _project, _issueType, _priority, _assignee;
    private readonly StackPanel _dynamicPanel = new() { Margin = new Thickness(0, 0, 0, 16) };
    private readonly Dictionary<string, Control> _dynamicControls = new();
    private JiraFieldMeta[]? _dynamicFields;
    private readonly JiraImageGallery _gallery;
    private bool _suppressIssueTypeChanged;

    public JiraTicket? CreatedTicket { get; private set; }

    public JiraTicketReviewWindow(ApiClient api, JiraTicketDraft draft, JiraProject[] projects,
        string customPrompt = "", string? defaultProject = null, string? defaultIssueType = null,
        string? defaultAssignee = null, string[]? imageUrls = null)
        : base(L.T("JiraReview_Header"), L.T("JiraReview_Title"), L.T("JiraReview_Subtitle"),
               L.T("JiraReview_Create"), 800, 760, L.T("JiraReview_Title"))
    {
        _api = api;
        _draft = draft;
        _projects = projects;
        _defaultProject = defaultProject;
        _defaultIssueType = defaultIssueType;
        _defaultAssignee = defaultAssignee;

        AddLabel(L.T("JiraReview_Summary"));
        _summary = NewTextBox(draft.Summary);
        _summary.MaxLength = 255;
        _summary.Margin = new Thickness(0, 0, 0, 4);
        Form.Children.Add(_summary);
        _charCount = Text("", 11, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 16));
        _charCount.HorizontalAlignment = HorizontalAlignment.Right;
        Form.Children.Add(_charCount);
        _summary.TextChanged += (_, _) => UpdateCharCount();
        UpdateCharCount();

        _description = AddField(L.T("JiraReview_Description"), draft.Description, true, 180, mono: true);

        var (left, right) = AddTwoColumns();
        _project = AddCombo(Loc.T("JiraReview_Project", "Project"), Array.Empty<ComboItem>(), left);
        _issueType = AddCombo("Issue Type", Array.Empty<ComboItem>(), right);

        _priority = AddCombo("Priority", new[] { "Highest", "High", "Medium", "Low" }.Select(p => new ComboItem(p, p)));
        _priority.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "Highest", "High", "Medium", "Low" }, draft.Priority is "Highest" or "High" or "Medium" or "Low" ? draft.Priority : "Medium"));

        _assignee = AddCombo(L.T("JiraReview_Assignee"), Array.Empty<ComboItem>());
        _assignee.Margin = new Thickness(0, 0, 0, 4);
        Form.Children.Add(Text(L.T("JiraReview_AssigneeHint"), 11, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 16)));

        _labels = AddField(L.T("JiraReview_Labels"), draft.Labels is { Length: > 0 } ? string.Join(", ", draft.Labels) : "");
        _labels.Margin = new Thickness(0, 0, 0, 4);
        Form.Children.Add(Text(L.T("JiraReview_LabelsPlaceholder"), 11, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 16)));

        Form.Children.Add(_dynamicPanel);

        _gallery = new JiraImageGallery(api, this, L.T("JiraReview_ImagesHint"));
        Form.Children.Add(_gallery);
        foreach (var url in imageUrls ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            var rel = draft.ImageRelevance?.FirstOrDefault(r => r.Url == url);
            _gallery.Images.Add(new JiraReviewImage(url, rel?.Relevant ?? true, rel?.Reason));
        }
        _gallery.Refresh();

        var note = new Border
        {
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10),
            Child = Text(L.T("JiraReview_AiNote"), 11.5, "Text3")
        };
        note.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg3"));
        note.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));
        Form.Children.Add(note);

        _project.ItemsSource = projects;
        _project.SelectionChanged += async (_, _) => await OnProjectChangedAsync();
        _issueType.SelectionChanged += async (_, _) =>
        {
            if (_suppressIssueTypeChanged) return;
            if (_issueType.SelectedItem is JiraIssueType t && _project.SelectedItem is JiraProject p)
                await LoadDynamicFields(p.Key, t.Id);
        };

        Opened += async (_, _) =>
        {
            if (projects.Length == 0) return;
            var def = !string.IsNullOrEmpty(_defaultProject) ? projects.FirstOrDefault(p => p.Key == _defaultProject) : null;
            _project.SelectedItem = def ?? projects[0];
            await Task.CompletedTask;
        };
    }

    private void UpdateCharCount()
    {
        var count = _summary.Text?.Length ?? 0;
        _charCount.Text = L.Tf("JiraReview_CharCount", count);
        if (count > 80) _charCount.Foreground = Brushes.Red;
        else _charCount.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text3"));
    }

    private async Task OnProjectChangedAsync()
    {
        if (_project.SelectedItem is not JiraProject p) return;
        _suppressIssueTypeChanged = true;
        await LoadIssueTypesForProject(p.Key);
        _suppressIssueTypeChanged = false;
        if (_issueType.SelectedItem is JiraIssueType t)
            await LoadDynamicFields(p.Key, t.Id);
    }

    private async Task LoadIssueTypesForProject(string projectKey)
    {
        try
        {
            var types = await _api.GetJiraIssueTypesAsync(projectKey);
            if (types != null && types.Length > 0)
            {
                _issueType.ItemsSource = types;
                var target = _defaultIssueType ?? _draft.IssueType;
                _issueType.SelectedItem = types.FirstOrDefault(t => t.Name == target || t.Id == target) ?? types[0];
            }
            await LoadAssignableUsers(projectKey);
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), L.Tf("JiraConfig_TypesLoadError", ex.Message), this);
        }
    }

    private async Task LoadAssignableUsers(string projectKey)
    {
        var list = new List<JiraUser> { new() { AccountId = "", DisplayName = L.T("JiraReview_Unassigned") } };
        try
        {
            var users = await _api.GetJiraAssignableUsersAsync(projectKey);
            if (users != null) list.AddRange(users);
        }
        catch (Exception ex) { Logger.Log($"[JIRA] assignees laden mislukt: {ex.Message}"); }

        _assignee.ItemsSource = list;
        var target = _defaultAssignee ?? _draft.AssigneeAccountId;
        _assignee.SelectedItem = (!string.IsNullOrEmpty(target) ? list.FirstOrDefault(u => u.AccountId == target) : null) ?? list[0];
    }

    private async Task LoadDynamicFields(string projectKey, string issueTypeId)
    {
        _dynamicPanel.Children.Clear();
        _dynamicControls.Clear();
        _dynamicFields = null;
        try
        {
            var fields = await _api.GetJiraCreateMetaAsync(projectKey, issueTypeId);
            if (fields == null || fields.Length == 0) return;
            _dynamicFields = fields;
            JiraDynamicFields.Render(_dynamicPanel, this, fields, _dynamicControls);
        }
        catch (Exception ex) { Logger.Log($"[JIRA] createmeta mislukt: {ex.Message}"); }
    }

    protected override string BusyText => L.T("JiraReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var summary = _summary.Text?.Trim() ?? "";
        var project = _project.SelectedItem as JiraProject;
        var issueTypeId = (_issueType.SelectedItem as JiraIssueType)?.Id;

        if (string.IsNullOrWhiteSpace(summary)) { await Validation(L.T("JiraReview_SummaryRequired"), _summary); return false; }
        if (project == null) { await Validation(L.T("JiraReview_SelectProject")); return false; }
        if (string.IsNullOrWhiteSpace(issueTypeId)) { await Validation(L.T("JiraReview_SelectIssueType")); return false; }

        var missing = JiraDynamicFields.MissingRequired(_dynamicFields, _dynamicControls);
        if (missing.Count > 0) { await Validation($"'{missing[0]}' is required."); return false; }

        var labelsText = _labels.Text?.Trim() ?? "";
        string[]? labels = string.IsNullOrWhiteSpace(labelsText) ? null
            : labelsText.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        var assignee = _assignee.SelectedItem as JiraUser;
        var images = _gallery.Images.Where(i => i.Include).Select(i => i.Url).ToArray();

        try
        {
            var ticket = await _api.CreateJiraTicketAsync(project.Key, issueTypeId!, summary, _description.Text?.Trim() ?? "",
                (_priority.SelectedItem as ComboItem)?.Value as string,
                labels,
                string.IsNullOrEmpty(assignee?.AccountId) ? null : assignee!.AccountId,
                JiraDynamicFields.BuildPayload(_dynamicFields, _dynamicControls),
                images.Length > 0 ? images : null);

            if (ticket != null)
            {
                CreatedTicket = ticket;
                await CopyToClipboard(ticket.Url);
                await Info(L.T("Common_Success"), L.Tf("JiraReview_CreateOk", ticket.Key, ticket.Url));
                return true;
            }
            await Error(L.T("JiraReview_CreateFail"));
        }
        catch (ApiException ex)
        {
            await Error(L.Tf("JiraReview_CreateError", BackendError(ex), "API"));
        }
        catch (Exception ex)
        {
            await Error(L.Tf("JiraReview_CreateError", ex.Message, ex.GetType().Name));
        }
        return false;
    }

    internal static string BackendError(ApiException ex)
    {
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(ex.Details);
            if (parsed.TryGetProperty("error", out var e) && e.GetString() is string s) return s;
        }
        catch { }
        return ex.Message;
    }
}

/// <summary>
/// Meerdere Jira-tickets (optioneel onder één Epic) nakijken en aanmaken — port van
/// Views/JiraMultiTicketReviewWindow.
/// </summary>
public class JiraMultiTicketReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly JiraTicketDraft _draft;
    private readonly ComboBox _project;
    private readonly CheckBox _epicCheck;
    private readonly TextBox _epicTitle;
    private readonly StackPanel _requiredSection = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 16) };
    private readonly StackPanel _requiredPanel = new();
    private readonly Dictionary<string, Control> _requiredControls = new();
    private JiraFieldMeta[]? _requiredFields;
    private string? _loadedFieldsForProject;
    private readonly List<TicketRow> _tickets = new();
    private readonly JiraImageGallery _gallery;

    public bool AnyCreated { get; private set; }

    private sealed class TicketRow
    {
        public TextBox Summary = null!;
        public TextBox Description = null!;
        public ComboBox IssueType = null!;
        public ComboBox Priority = null!;
        public TextBox Labels = null!;
    }

    public JiraMultiTicketReviewWindow(ApiClient api, JiraTicketDraft draft, JiraProject[] projects,
        string? defaultProject = null, string[]? imageUrls = null)
        : base(L.T("JiraMulti_Header"), L.T("JiraMulti_Title"), L.T("JiraMulti_Subtitle"),
               L.T("JiraMulti_Create"), 840, 800, L.T("JiraMulti_Title"))
    {
        _api = api;
        _draft = draft;

        _project = AddCombo(L.T("JiraReview_Project"), Array.Empty<ComboItem>());
        _project.ItemsSource = projects;

        _epicCheck = new CheckBox { Content = L.T("JiraMulti_GroupEpic"), Margin = new Thickness(0, 0, 0, 6) };
        _epicTitle = NewTextBox(draft.EpicSuggestion?.Summary ?? "");
        _epicTitle.IsEnabled = false;
        _epicCheck.IsCheckedChanged += (_, _) => _epicTitle.IsEnabled = _epicCheck.IsChecked == true;
        Form.Children.Add(_epicCheck);
        Form.Children.Add(_epicTitle);

        _requiredSection.Children.Add(Text(L.T("JiraMulti_RequiredFields"), 12, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 4)));
        _requiredSection.Children.Add(Text(L.T("JiraMulti_RequiredFieldsHint"), 11, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 8)));
        _requiredSection.Children.Add(_requiredPanel);
        Form.Children.Add(_requiredSection);

        var source = draft.Tickets is { Length: > 0 } ? draft.Tickets : new[] { draft };
        int n = 1;
        foreach (var t in source) Form.Children.Add(BuildTicketCard(n++, t));

        _gallery = new JiraImageGallery(api, this, L.T("JiraMulti_ImagesHint"),
            () => Enumerable.Range(1, _tickets.Count).Select(i => L.Tf("JiraMulti_TicketN", i)).ToList());
        foreach (var url in imageUrls ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            var rel = draft.ImageRelevance?.FirstOrDefault(r => r.Url == url);
            _gallery.Images.Add(new JiraReviewImage(url, rel?.Relevant ?? true, rel?.Reason));
        }
        _gallery.Refresh();
        Form.Children.Add(_gallery);

        _project.SelectionChanged += async (_, _) => await LoadRequiredFieldsAsync();
        Opened += (_, _) =>
        {
            if (projects.Length == 0) return;
            var def = !string.IsNullOrEmpty(defaultProject) ? projects.FirstOrDefault(p => p.Key == defaultProject) : null;
            _project.SelectedItem = def ?? projects[0];
        };
    }

    private Control BuildTicketCard(int number, JiraTicketDraft t)
    {
        var row = new TicketRow();
        var stack = new StackPanel();
        stack.Children.Add(Text(L.Tf("JiraMulti_TicketN", number), 13, "Text1", FontWeight.SemiBold, new Thickness(0, 0, 0, 10)));

        row.Summary = NewTextBox(t.Summary);
        stack.Children.Add(row.Summary);
        row.Description = NewTextBox(t.Description, true, 90);
        stack.Children.Add(row.Description);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,2*") };
        row.IssueType = new ComboBox { ItemsSource = new[] { "Bug", "Task", "Story" }, SelectedItem = NormalizeIssueType(t.IssueType), HorizontalAlignment = HorizontalAlignment.Stretch, Height = 38 };
        row.Priority = new ComboBox { ItemsSource = new[] { "Highest", "High", "Medium", "Low" }, SelectedItem = NormalizePriority(t.Priority), HorizontalAlignment = HorizontalAlignment.Stretch, Height = 38 };
        row.Labels = NewTextBox(t.Labels != null ? string.Join(", ", t.Labels) : "");
        row.Labels.Margin = new Thickness(0);
        row.Labels.Watermark = L.T("JiraReview_LabelsPlaceholder");
        Grid.SetColumn(row.Priority, 2);
        Grid.SetColumn(row.Labels, 4);
        grid.Children.Add(row.IssueType);
        grid.Children.Add(row.Priority);
        grid.Children.Add(row.Labels);
        stack.Children.Add(grid);

        _tickets.Add(row);

        var card = new Border
        {
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 12), Child = stack
        };
        card.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg2"));
        card.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));
        return card;
    }

    private async Task LoadRequiredFieldsAsync()
    {
        if (_project.SelectedItem is not JiraProject project) return;
        if (_loadedFieldsForProject == project.Key) return;
        _loadedFieldsForProject = project.Key;

        _requiredFields = null;
        _requiredControls.Clear();
        _requiredPanel.Children.Clear();
        _requiredSection.IsVisible = false;
        try
        {
            var types = await _api.GetJiraIssueTypesAsync(project.Key);
            if (types == null || types.Length == 0) return;
            var wanted = _tickets.Select(t => t.IssueType.SelectedItem as string).Concat(new[] { "Epic" })
                .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim().ToLowerInvariant()).ToHashSet();
            var ids = types.Where(it => wanted.Contains(it.Name.Trim().ToLowerInvariant())).Select(it => it.Id).Distinct();

            var union = new List<JiraFieldMeta>();
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                var fields = await _api.GetJiraCreateMetaAsync(project.Key, id);
                if (fields == null) continue;
                foreach (var f in fields) if (f != null && seen.Add(f.Key)) union.Add(f);
            }
            if (union.Count == 0) return;
            _requiredFields = union.ToArray();
            JiraDynamicFields.Render(_requiredPanel, this, _requiredFields, _requiredControls);
            _requiredSection.IsVisible = true;
        }
        catch (Exception ex) { Logger.Log($"[JIRA] verplichte velden laden mislukt: {ex.Message}"); }
    }

    protected override string BusyText => L.T("JiraReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        if (_project.SelectedItem is not JiraProject project) { await Validation(L.T("JiraReview_SelectProject")); return false; }
        if (_tickets.Any(t => string.IsNullOrWhiteSpace(t.Summary.Text))) { await Validation(L.T("JiraReview_SummaryRequired")); return false; }

        bool createEpic = _epicCheck.IsChecked == true;
        if (createEpic && string.IsNullOrWhiteSpace(_epicTitle.Text)) { await Validation(L.T("JiraMulti_EpicTitleRequired"), _epicTitle); return false; }

        Dictionary<string, object>? dynamicFields = null;
        if (_requiredFields is { Length: > 0 })
        {
            var missing = JiraDynamicFields.MissingRequired(_requiredFields, _requiredControls);
            if (missing.Count > 0) { await Validation(L.Tf("JiraMulti_RequiredFieldsMissing", string.Join(", ", missing))); return false; }
            dynamicFields = JiraDynamicFields.BuildPayload(_requiredFields, _requiredControls);
        }

        var payload = _tickets.Select((t, idx) => (object)new Dictionary<string, object?>
        {
            ["summary"] = t.Summary.Text!.Trim(),
            ["description"] = t.Description.Text ?? "",
            ["issueType"] = t.IssueType.SelectedItem as string ?? "Task",
            ["priority"] = t.Priority.SelectedItem as string ?? "Medium",
            ["labels"] = SplitLabels(t.Labels.Text),
            ["imageUrls"] = _gallery.Images.Where(i => i.Include && i.TargetTicketIndex == idx).Select(i => i.Url).ToArray()
        }).ToList();

        object? epic = createEpic
            ? new Dictionary<string, object?> { ["summary"] = _epicTitle.Text!.Trim(), ["description"] = _draft.EpicSuggestion?.Description ?? "" }
            : null;

        try
        {
            var result = await _api.CreateJiraTicketsAsync(project.Key, createEpic, epic, payload, dynamicFields);
            if (result?.Tickets == null) { await Error(L.T("JiraReview_CreateFail")); return false; }

            var created = result.Tickets.Where(t => !string.IsNullOrEmpty(t.Key)).Select(t => t.Key!).ToList();
            var failed = result.Tickets.Count(t => string.IsNullOrEmpty(t.Key));
            AnyCreated = created.Count > 0;

            var lines = new System.Text.StringBuilder();
            if (result.Epic != null) lines.AppendLine($"Epic: {result.Epic.Key}");
            foreach (var k in created) lines.AppendLine($"✅ {k}");
            if (failed > 0) lines.AppendLine(L.Tf("JiraMulti_SomeFailed", failed));

            var firstUrl = result.Tickets.FirstOrDefault(t => !string.IsNullOrEmpty(t.Url))?.Url;
            if (!string.IsNullOrEmpty(firstUrl)) await CopyToClipboard(firstUrl);

            await Info(L.T("Common_Success"), lines.ToString().Trim());
            return AnyCreated;
        }
        catch (ApiException ex) { await Error(L.Tf("JiraReview_CreateError", JiraTicketReviewWindow.BackendError(ex), "API")); }
        catch (Exception ex) { await Error(L.Tf("JiraReview_CreateError", ex.Message, ex.GetType().Name)); }
        return false;
    }

    private static string NormalizePriority(string? p) => (p ?? "").Trim().ToLowerInvariant() switch
    {
        "critical" or "highest" => "Highest",
        "high" => "High",
        "low" => "Low",
        _ => "Medium"
    };

    private static string NormalizeIssueType(string? t) => (t ?? "").Trim().ToLowerInvariant() switch
    {
        "bug" => "Bug",
        "story" => "Story",
        _ => "Task"
    };

    private static string[] SplitLabels(string? labels) =>
        string.IsNullOrWhiteSpace(labels) ? Array.Empty<string>()
            : labels.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
}
