using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

// Review-vensters per integratie — ports van de gelijknamige Windows-vensters
// (Views/*ReviewWindow.xaml). Zelfde velden, validatie en meldingen.

public class SlackMessageReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _message;

    public SlackMessageReviewWindow(ApiClient api, SlackMessageDraft draft)
        : base(Loc.T("SlackReview_Eyebrow", "SLACK · BERICHT REVIEW"), Loc.T("SlackReview_Heading", "Bericht versturen"),
               Loc.T("SlackReview_Subtitle", "Controleer en pas het bericht aan voor je het verstuurt."),
               L.T("SlackReview_SendBtn"), 620, 460, Loc.T("SlackReview_Title", "Slack Bericht Versturen"))
    {
        _api = api;
        if (!string.IsNullOrWhiteSpace(draft.SuggestedChannel))
            AddInfo(L.Tf("SlackReview_ChannelSuggestion", draft.SuggestedChannel));
        _message = AddField(Loc.T("SlackReview_Message", "Bericht"), draft.Message, true, 120);
        AddHint("Slack markdown: *vet* · _cursief_ · `code`", mono: true);
    }

    protected override string BusyText => L.T("SlackReview_Sending");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var message = _message.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(message)) { await Validation(L.T("SlackReview_MessageRequired"), _message); return false; }
        try
        {
            if (await _api.SendSlackMessageAsync(message))
            {
                await Info(L.T("Common_Success"), L.T("SlackReview_SendOk"));
                return true;
            }
            await Error(L.T("SlackReview_SendFail"));
        }
        catch (Exception ex) { await Error(L.Tf("SlackReview_SendError", ex.Message)); }
        return false;
    }
}

public class TeamsMessageReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _message;

    public TeamsMessageReviewWindow(ApiClient api, TeamsMessageDraft draft)
        : base(Loc.T("TeamsReview_Eyebrow", "TEAMS · BERICHT REVIEW"), Loc.T("TeamsReview_Heading", "Bericht versturen"),
               Loc.T("TeamsReview_Subtitle", "Controleer en pas het bericht aan voor je het verstuurt."),
               L.T("TeamsReview_SendBtn"), 620, 460, Loc.T("TeamsReview_Title", "Teams Bericht Versturen"))
    {
        _api = api;
        if (!string.IsNullOrWhiteSpace(draft.SuggestedChannel))
            AddInfo(L.Tf("TeamsReview_ChannelSuggestion", draft.SuggestedChannel));
        _message = AddField(Loc.T("TeamsReview_Message", "Bericht"), draft.Message, true, 120);
        AddHint("Markdown: **vet** · _cursief_ · `code`", mono: true);
    }

    protected override string BusyText => L.T("TeamsReview_Sending");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var message = _message.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(message)) { await Validation(L.T("TeamsReview_MessageRequired"), _message); return false; }
        try
        {
            if (await _api.SendTeamsMessageAsync(message))
            {
                await Info(L.T("Common_Success"), L.T("TeamsReview_SendOk"));
                return true;
            }
            await Error(L.T("TeamsReview_SendFail"));
        }
        catch (Exception ex) { await Error(L.Tf("TeamsReview_SendError", ex.Message)); }
        return false;
    }
}

public class ZapierPayloadReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _payload;

    public ZapierPayloadReviewWindow(ApiClient api, ZapierPayloadDraft draft)
        : base(Loc.T("ZapierReview_Eyebrow", "ZAPIER / MAKE · DATA REVIEW"), Loc.T("ZapierReview_Heading", "Data versturen naar webhook"),
               Loc.T("ZapierReview_Subtitle", "Controleer de gestructureerde data voor je het naar je webhook stuurt."),
               L.T("ZapierReview_SendBtn"), 680, 620, Loc.T("ZapierReview_Title", "Zapier/Make Data Versturen"))
    {
        _api = api;
        var summary = Text($"{Loc.T("ZapierReview_TitleLabel", "Titel:")} {draft.Title}\n" +
                           $"{Loc.T("ZapierReview_Category", "Categorie:")} {draft.Category}\n" +
                           $"{Loc.T("ZapierReview_Priority", "Prioriteit:")} {draft.Priority}", 12.5, "Text2",
                           FontWeight.Normal, new Avalonia.Thickness(0, 0, 0, 14));
        Form.Children.Add(summary);
        _payload = AddField(Loc.T("ZapierReview_Payload", "JSON Payload"),
            JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }), true, 220, mono: true);
        AddHint(Loc.T("ZapierReview_PayloadHint", "Je kunt de JSON aanpassen voor verzending"));
    }

    protected override string BusyText => L.T("ZapierReview_Sending");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var json = _payload.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(json)) { await Validation(L.T("ZapierReview_PayloadRequired"), _payload); return false; }
        try { JsonSerializer.Deserialize<object>(json); }
        catch { await Validation(L.T("ZapierReview_InvalidJson"), _payload); return false; }
        try
        {
            if (await _api.SendZapierDataAsync(json))
            {
                await Info(L.T("Common_Success"), L.T("ZapierReview_SendOk"));
                return true;
            }
            await Error(L.T("ZapierReview_SendFail"));
        }
        catch (Exception ex) { await Error(L.Tf("ZapierReview_SendError", ex.Message)); }
        return false;
    }
}

public class TodoistTaskReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _content, _description, _due, _labels;
    private readonly ComboBox _priority;
    public TodoistTask? CreatedTask { get; private set; }

    public TodoistTaskReviewWindow(ApiClient api, TodoistTaskDraft draft, string? labelsOverride = null)
        : base(Loc.T("TodoistReview_Eyebrow", "TODOIST · TAAK REVIEW"), Loc.T("TodoistReview_Heading", "Taak aanmaken"),
               Loc.T("TodoistReview_Subtitle", "Controleer en pas de AI-gegenereerde taak aan voor je hem aanmaakt."),
               L.T("TodoistReview_CreateBtn"), 640, 640, Loc.T("TodoistReview_Title", "Todoist Taak Aanmaken"))
    {
        _api = api;
        _content = AddField(Loc.T("TodoistReview_Content", "Taak"), draft.Content);
        _description = AddField(Loc.T("TodoistReview_Description", "Beschrijving (optioneel)"), draft.Description, true, 90);

        var (left, right) = AddTwoColumns();
        AddLabel(Loc.T("TodoistReview_Due", "Deadline (optioneel)"), left);
        _due = NewTextBox(draft.DueString);
        left.Children.Add(_due);
        _priority = AddCombo(Loc.T("TodoistReview_Priority", "Prioriteit"), new[]
        {
            new ComboItem(L.T("TodoistPriority_Urgent"), 4),
            new ComboItem(L.T("TodoistPriority_High"), 3),
            new ComboItem(L.T("TodoistPriority_Medium"), 2),
            new ComboItem(L.T("TodoistPriority_Normal"), 1),
        }, right);
        var p = draft.Priority is >= 1 and <= 4 ? draft.Priority : 1;
        _priority.SelectedIndex = 4 - p;
        AddHint(Loc.T("TodoistReview_DueHint", "Deadline in gewone taal, bijv: morgen 15:00, vrijdag, 12 augustus"));

        _labels = AddField(Loc.T("TodoistReview_Labels", "Labels (kommagescheiden)"),
            !string.IsNullOrWhiteSpace(labelsOverride) ? labelsOverride : string.Join(", ", draft.Labels ?? Array.Empty<string>()));
        AddHint(Loc.T("TodoistReview_LabelsHint", "Bijv: werk, klant"));
    }

    protected override string BusyText => L.T("TodoistReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var content = _content.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(content)) { await Validation(L.T("TodoistReview_ContentRequired"), _content); return false; }
        var priority = (_priority.SelectedItem as ComboItem)?.Value as int? ?? 1;
        var raw = _labels.Text?.Trim() ?? "";
        var labels = string.IsNullOrWhiteSpace(raw) ? Array.Empty<string>()
            : raw.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        try
        {
            var task = await _api.CreateTodoistTaskAsync(content, _description.Text?.Trim(), _due.Text?.Trim(), priority, labels);
            if (task != null)
            {
                CreatedTask = task;
                await CopyToClipboard(task.Url);
                await Info(L.T("TodoistReview_CreateTitle"), L.Tf("TodoistReview_CreateOk", task.Url));
                return true;
            }
            await Error(L.T("TodoistReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("TodoistReview_CreateError", ex.Message)); }
        return false;
    }
}

public class TrelloCardReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _name, _desc, _due;
    public TrelloCard? CreatedCard { get; private set; }

    public TrelloCardReviewWindow(ApiClient api, TrelloCardDraft draft)
        : base(Loc.T("TrelloReview_Eyebrow", "TRELLO · KAART REVIEW"), Loc.T("TrelloReview_Heading", "Kaart aanmaken"),
               Loc.T("TrelloReview_Subtitle", "Controleer en pas de AI-gegenereerde kaart aan voor je hem aanmaakt."),
               L.T("TrelloReview_CreateBtn"), 640, 580, Loc.T("TrelloReview_Title", "Trello Kaart Aanmaken"))
    {
        _api = api;
        _name = AddField(Loc.T("TrelloReview_Name", "Kaartnaam"), draft.Name);
        _desc = AddField(Loc.T("TrelloReview_Desc", "Beschrijving (markdown, optioneel)"), draft.Desc, true, 120);
        _due = AddField(Loc.T("TrelloReview_Due", "Deadline (optioneel)"), draft.Due);
        AddHint(Loc.T("TrelloReview_DueHint", "Formaat: JJJJ-MM-DD of JJJJ-MM-DDTUU:MM (leeg = geen deadline)"));
    }

    protected override string BusyText => L.T("TrelloReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var name = _name.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) { await Validation(L.T("TrelloReview_NameRequired"), _name); return false; }
        var due = _due.Text?.Trim();
        try
        {
            var card = await _api.CreateTrelloCardAsync(name, _desc.Text?.Trim(), string.IsNullOrWhiteSpace(due) ? null : due);
            if (card != null)
            {
                CreatedCard = card;
                await CopyToClipboard(card.ShortUrl);
                await Info(L.T("TrelloReview_CreateTitle"), L.Tf("TrelloReview_CreateOk", card.ShortUrl));
                return true;
            }
            await Error(L.T("TrelloReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("TrelloReview_CreateError", ex.Message)); }
        return false;
    }
}

public class AsanaTaskReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _name, _notes, _due;
    public AsanaTask? CreatedTask { get; private set; }

    public AsanaTaskReviewWindow(ApiClient api, AsanaTaskDraft draft)
        : base(Loc.T("AsanaReview_Eyebrow", "ASANA · TAAK REVIEW"), Loc.T("AsanaReview_Heading", "Taak aanmaken"),
               Loc.T("AsanaReview_Subtitle", "Controleer en pas de AI-gegenereerde taak aan voor je hem aanmaakt."),
               L.T("AsanaReview_CreateBtn"), 640, 580, Loc.T("AsanaReview_Title", "Asana Taak Aanmaken"))
    {
        _api = api;
        _name = AddField(Loc.T("AsanaReview_Name", "Taaknaam"), draft.Name);
        _notes = AddField(Loc.T("AsanaReview_Notes", "Notities (optioneel)"), draft.Notes, true, 120);
        _due = AddField(Loc.T("AsanaReview_Due", "Deadline (optioneel)"), draft.DueOn);
        AddHint(Loc.T("AsanaReview_DueHint", "Formaat: JJJJ-MM-DD (leeg = geen deadline)"));
    }

    protected override string BusyText => L.T("AsanaReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var name = _name.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) { await Validation(L.T("AsanaReview_NameRequired"), _name); return false; }
        var due = _due.Text?.Trim();
        try
        {
            var task = await _api.CreateAsanaTaskAsync(name, _notes.Text?.Trim(), string.IsNullOrWhiteSpace(due) ? null : due);
            if (task != null)
            {
                CreatedTask = task;
                await CopyToClipboard(task.PermalinkUrl);
                await Info(L.T("AsanaReview_CreateTitle"), L.Tf("AsanaReview_CreateOk", task.PermalinkUrl));
                return true;
            }
            await Error(L.T("AsanaReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("AsanaReview_CreateError", ex.Message)); }
        return false;
    }
}

public class GoogleTaskReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _title, _notes, _due;
    public GoogleTask? CreatedTask { get; private set; }

    public GoogleTaskReviewWindow(ApiClient api, GoogleTaskDraft draft)
        : base(Loc.T("GTaskReview_Eyebrow", "GOOGLE TASKS · TAAK REVIEW"), Loc.T("GTaskReview_Heading", "Taak aanmaken"),
               Loc.T("GTaskReview_Subtitle", "Controleer en pas de AI-gegenereerde taak aan voor je hem aanmaakt."),
               L.T("GTaskReview_CreateBtn"), 640, 580, Loc.T("GTaskReview_Title", "Google Tasks Taak Aanmaken"))
    {
        _api = api;
        _title = AddField(Loc.T("GTaskReview_Name", "Taaknaam"), draft.Title);
        _notes = AddField(Loc.T("GTaskReview_Notes", "Notities (optioneel)"), draft.Notes, true, 120);
        _due = AddField(Loc.T("GTaskReview_Due", "Deadline (optioneel)"), draft.DueOn);
        AddHint(Loc.T("GTaskReview_DueHint", "Formaat: JJJJ-MM-DD (leeg = geen deadline; Google Tasks kent geen tijdstip)"));
    }

    protected override string BusyText => L.T("GTaskReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var title = _title.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) { await Validation(L.T("GTaskReview_TitleRequired"), _title); return false; }
        var due = _due.Text?.Trim();
        try
        {
            var task = await _api.CreateGoogleTaskAsync(title, _notes.Text?.Trim(), string.IsNullOrWhiteSpace(due) ? null : due);
            if (task != null)
            {
                CreatedTask = task;
                await Info(L.T("GTaskReview_CreateTitle"), L.Tf("GTaskReview_CreateOk", task.Title));
                return true;
            }
            await Error(L.T("GTaskReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("GTaskReview_CreateError", ex.Message)); }
        return false;
    }
}

public class GitHubIssueReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _title, _body, _labels;
    private readonly TextBlock _count;
    public GitHubIssue? CreatedIssue { get; private set; }

    public GitHubIssueReviewWindow(ApiClient api, GitHubIssueDraft draft, string? repoOverride = null, string? labelsOverride = null)
        : base(Loc.T("GitHubReview_Eyebrow", "GITHUB · ISSUE REVIEW"), Loc.T("GitHubReview_Heading", "Issue aanmaken"),
               Loc.T("GitHubReview_Subtitle", "Controleer en pas het AI-gegenereerde issue aan voor je het aanmaakt."),
               L.T("GitHubReview_CreateBtn"), 680, 640, Loc.T("GitHubReview_Title", "GitHub Issue Aanmaken"))
    {
        _api = api;
        var labelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titleLabel = AddLabel(Loc.T("GitHubReview_TitleLabel", "Titel"));
        Form.Children.Remove(titleLabel);
        _count = Text("", 11, "Text3");
        Grid.SetColumn(_count, 1);
        labelRow.Children.Add(titleLabel);
        labelRow.Children.Add(_count);
        Form.Children.Add(labelRow);
        _title = NewTextBox(draft.Title);
        Form.Children.Add(_title);

        _body = AddField(Loc.T("GitHubReview_Body", "Beschrijving (GitHub-markdown)"), draft.Body, true, 180);
        _labels = AddField(Loc.T("GitHubReview_Labels", "Labels (kommagescheiden)"),
            !string.IsNullOrWhiteSpace(labelsOverride) ? labelsOverride : string.Join(", ", draft.Labels ?? Array.Empty<string>()));
        AddHint(Loc.T("GitHubReview_LabelsHint", "Bijv: bug, enhancement, frontend"));

        UpdateCount();
        _title.TextChanged += (_, _) => UpdateCount();
    }

    private void UpdateCount()
    {
        var length = _title.Text?.Length ?? 0;
        _count.Text = $"{length}/80";
        if (length > 70) _count.Foreground = Brushes.Orange;
        else _count.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text3"));
    }

    protected override string BusyText => L.T("GitHubReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var title = _title.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) { await Validation(L.T("GitHubReview_TitleRequired"), _title); return false; }
        var raw = _labels.Text?.Trim() ?? "";
        var labels = string.IsNullOrWhiteSpace(raw) ? Array.Empty<string>()
            : raw.Split(',').Select(l => l.Trim().ToLower().Replace(" ", "-")).Where(l => l.Length > 0).ToArray();
        try
        {
            var issue = await _api.CreateGitHubIssueAsync(title, _body.Text?.Trim() ?? "", labels);
            if (issue != null)
            {
                CreatedIssue = issue;
                await CopyToClipboard(issue.HtmlUrl);
                await Info(L.T("GitHubReview_CreateTitle"), L.Tf("GitHubReview_CreateOk", issue.Number, issue.HtmlUrl));
                return true;
            }
            await Error(L.T("GitHubReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("GitHubReview_CreateError", ex.Message)); }
        return false;
    }
}

public class GmailReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _to, _cc, _subject, _body;
    private readonly Button _saveDraft;

    public GmailReviewWindow(ApiClient api, GmailDraft draft)
        : base(L.T("GmailReview_Eyebrow"), L.T("GmailReview_Title"), L.T("GmailReview_Subtitle"),
               L.T("GmailReview_SendBtn"), 680, 680, L.T("GmailReview_WindowTitle"))
    {
        _api = api;
        var (left, right) = AddTwoColumns();
        AddLabel(L.T("GmailReview_To"), left);
        _to = NewTextBox(draft.To);
        left.Children.Add(_to);
        AddLabel(L.T("GmailReview_Cc"), right);
        _cc = NewTextBox(draft.Cc);
        right.Children.Add(_cc);
        _subject = AddField(L.T("GmailReview_Subject"), draft.Subject);
        _body = AddField(L.T("GmailReview_Body"), draft.Body, true, 200);

        _saveDraft = new Button
        {
            Content = L.T("GmailReview_SaveDraftBtn"), Height = 40,
            Padding = new Avalonia.Thickness(16, 0), VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _saveDraft.Classes.Add("GhostButton");
        _saveDraft.Click += async (_, _) => await SaveDraftAsync();
        ButtonsRight.Children.Insert(0, _saveDraft);
    }

    protected override string BusyText => L.T("GmailReview_Sending");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var to = _to.Text?.Trim() ?? "";
        var subject = _subject.Text?.Trim() ?? "";
        var cc = _cc.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(to)) { await Validation(L.T("GmailReview_RecipientRequired"), _to); return false; }
        if (string.IsNullOrWhiteSpace(subject)) { await Validation(L.T("GmailReview_SubjectRequired"), _subject); return false; }
        try
        {
            if (await _api.SendGmailAsync(to, subject, _body.Text?.Trim() ?? "", string.IsNullOrEmpty(cc) ? null : cc))
            {
                await Info(L.T("Common_Success"), L.T("GmailReview_SendOk"));
                return true;
            }
            await Error(L.T("GmailReview_SendFail"));
        }
        catch (Exception ex) { await Error(L.Tf("GmailReview_SendError", ex.Message)); }
        return false;
    }

    private async Task SaveDraftAsync()
    {
        var subject = _subject.Text?.Trim() ?? "";
        var cc = _cc.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(subject)) { await Validation(L.T("GmailReview_SubjectRequired"), _subject); return; }

        _saveDraft.IsEnabled = false;
        _saveDraft.Content = L.T("GmailReview_Saving");
        try
        {
            if (await _api.CreateGmailDraftAsync(_to.Text?.Trim() ?? "", subject, _body.Text?.Trim() ?? "", string.IsNullOrEmpty(cc) ? null : cc))
            {
                await Info(L.T("Common_Success"), L.T("GmailReview_DraftOk"));
                Confirmed = true;
                Close();
            }
            else await Error(L.T("GmailReview_DraftFail"));
        }
        catch (Exception ex) { await Error(L.Tf("GmailReview_DraftError", ex.Message)); }
        finally
        {
            _saveDraft.IsEnabled = true;
            _saveDraft.Content = L.T("GmailReview_SaveDraftBtn");
        }
    }
}

public class CalendarEventReviewWindow : ReviewWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _title, _start, _end, _description, _attendees, _location;

    public CalendarEventReviewWindow(ApiClient api, CalendarEventDraft draft)
        : base(Loc.T("CalReview_Eyebrow", "AGENDA · REVIEW"), L.T("CalReview_Heading"), L.T("CalReview_Subtitle"),
               L.T("CalReview_CreateBtn"), 680, 700, L.T("CalReview_Title"))
    {
        _api = api;
        CancelButton.Content = L.T("CalReview_Cancel");
        _title = AddField(L.T("CalReview_EventTitle"), draft.Title);

        var (left, right) = AddTwoColumns();
        AddLabel(L.T("CalReview_Start"), left);
        _start = NewTextBox(draft.StartDateTime);
        left.Children.Add(_start);
        AddLabel(L.T("CalReview_End"), right);
        _end = NewTextBox(draft.EndDateTime);
        right.Children.Add(_end);
        AddHint(Loc.T("CalReview_TimeHint", "Formaat: 2026-05-05T14:00:00"));

        _description = AddField(L.T("CalReview_Description"), draft.Description, true, 90);
        _attendees = AddField(L.T("CalReview_Attendees"), draft.Attendees);
        AddHint(L.T("CalReview_AttendeesHint"));
        _location = AddField(L.T("CalReview_Location"), draft.Location);
    }

    protected override string BusyText => L.T("CalReview_Creating");

    protected override async Task<bool> OnPrimaryAsync()
    {
        var title = _title.Text?.Trim() ?? "";
        var start = _start.Text?.Trim() ?? "";
        var end = _end.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) { await Validation(L.T("CalReview_TitleRequired"), _title); return false; }
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end)) { await Validation(L.T("CalReview_TimeRequired")); return false; }

        string? Opt(TextBox t) => string.IsNullOrWhiteSpace(t.Text) ? null : t.Text.Trim();
        try
        {
            var ev = await _api.CreateCalendarEventAsync(title, start, end, Opt(_description), Opt(_attendees), Opt(_location));
            if (ev != null)
            {
                if (await MkDialog.ShowConfirm(L.T("Common_Success"), L.Tf("CalReview_CreateOk", title), this)
                    && !string.IsNullOrEmpty(ev.HtmlLink))
                    Ui.OpenUrl(ev.HtmlLink);
                return true;
            }
            await Error(L.T("CalReview_CreateFail"));
        }
        catch (Exception ex) { await Error(L.Tf("CalReview_CreateError", ex.Message)); }
        return false;
    }
}
