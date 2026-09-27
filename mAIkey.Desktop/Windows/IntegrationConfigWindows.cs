using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using Projektanker.Icons.Avalonia;

namespace mAIkey.Desktop.Windows;

public interface IConfigWindow
{
    bool Confirmed { get; }
}

/// <summary>
/// Gemeenschappelijke opmaak van de integratie-configuratievensters van Windows
/// (Views/*ConfigWindow): kop, velden, "Test verbinding" + status, keuzelijsten en Opslaan.
/// </summary>
public abstract class ConfigWindowBase : Window, IConfigWindow
{
    protected readonly StackPanel Form = new();
    protected readonly Button SaveButton;
    protected readonly Button CancelButton;
    private readonly StackPanel _statusPanel = new() { Orientation = Orientation.Horizontal, IsVisible = false, Margin = new Thickness(0, 0, 0, 16), Spacing = 8 };
    private readonly Projektanker.Icons.Avalonia.Icon _statusIcon = new() { FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _statusText = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    protected bool IsConnected;

    public bool Confirmed { get; protected set; }

    protected ConfigWindowBase(string eyebrow, string title, string subtitle, string windowTitle, double width = 560, double height = 640)
    {
        Title = windowTitle;
        Width = width; Height = height; MinWidth = 460; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        header.Children.Add(Text(eyebrow, 11, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6), 0.85));
        header.Children.Add(Text(title, 20, "Text1", FontWeight.SemiBold));
        header.Children.Add(Text(subtitle, 12.5, "Text3", FontWeight.Normal, new Thickness(0, 6, 0, 0)));

        _statusPanel.Children.Add(_statusIcon);
        _statusPanel.Children.Add(_statusText);

        CancelButton = new Button { Content = L.T("Common_Cancel"), Height = 40, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        CancelButton.Classes.Add("GhostButton");
        CancelButton.Click += (_, _) => { Confirmed = false; Close(); };
        SaveButton = new Button { Content = L.T("Common_Save"), Height = 40, Padding = new Thickness(20, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = false };
        SaveButton.Classes.Add("AccentButton");
        SaveButton.Click += async (_, _) => await SaveInternalAsync();

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(CancelButton);
        footer.Children.Add(SaveButton);

        var root = new DockPanel { Margin = new Thickness(28) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = Form, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    protected abstract Task<bool> SaveAsync();
    protected virtual string SavingText => L.T("Common_Saving").StartsWith("[") ? "…" : L.T("Common_Saving");

    private async Task SaveInternalAsync()
    {
        var old = SaveButton.Content;
        SaveButton.IsEnabled = false;
        SaveButton.Content = SavingText;
        try
        {
            if (await SaveAsync()) { Confirmed = true; Close(); }
        }
        catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), ex.Message, this); }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Content = old;
        }
    }

    protected TextBlock Text(string text, double size, string fg, FontWeight weight = FontWeight.Normal, Thickness? margin = null, double opacity = 1)
    {
        var tb = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0), Opacity = opacity };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fg));
        return tb;
    }

    /// <summary>Label met optionele hulplink rechts ("Hoe maak ik een token aan?").</summary>
    protected void AddLabel(string label, string? helpText = null, string? helpUrl = null)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        if (helpText != null && helpUrl != null)
        {
            var link = new Button { Classes = { "link" }, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = new Cursor(StandardCursorType.Hand) };
            var lt = Text(helpText, 11.5, "Accent");
            lt.TextDecorations = TextDecorations.Underline;
            link.Content = lt;
            link.Click += (_, _) => Ui.OpenUrl(helpUrl);
            DockPanel.SetDock(link, Dock.Right);
            row.Children.Add(link);
        }
        row.Children.Add(Text(label, 12, "Text3", FontWeight.SemiBold));
        Form.Children.Add(row);
    }

    protected void AddHint(string hint) => Form.Children.Add(Text(hint, 11, "Text3", FontWeight.Normal, new Thickness(0, -10, 0, 16)));

    protected TextBox AddTextBox(string label, string? value = null, string? helpText = null, string? helpUrl = null, bool secret = false, string? watermark = null)
    {
        AddLabel(label, helpText, helpUrl);
        var tb = new TextBox
        {
            Text = value ?? "", Height = 40, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14), Watermark = watermark
        };
        if (secret) tb.PasswordChar = '•';
        Form.Children.Add(tb);
        return tb;
    }

    protected ComboBox AddComboBox(string label, string? displayMember = null)
    {
        AddLabel(label);
        var cb = new ComboBox { Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 14), IsEnabled = false };
        if (displayMember != null) cb.DisplayMemberBinding = new Binding(displayMember);
        Form.Children.Add(cb);
        return cb;
    }

    protected Button AddButton(string text, Func<Task> onClick, bool accent = false)
    {
        var b = new Button { Content = text, Height = 38, Padding = new Thickness(16, 0), Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
        b.Classes.Add(accent ? "AccentButton" : "GhostButton");
        b.Click += async (_, _) => await onClick();
        Form.Children.Add(b);
        return b;
    }

    protected void AddStatusPanel() => Form.Children.Add(_statusPanel);

    protected void ShowStatus(string message, bool success, bool loading = false)
    {
        _statusPanel.IsVisible = true;
        _statusText.Text = message;
        _statusIcon.Value = success ? "mdi-check-circle" : loading ? "mdi-loading" : "mdi-alert-circle";
        var brush = success ? new SolidColorBrush(Color.Parse("#10B981")) : loading ? null : new SolidColorBrush(Color.Parse("#E5484D"));
        if (brush != null) { _statusText.Foreground = brush; _statusIcon.Foreground = brush; }
        else
        {
            _statusText.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text2"));
            _statusIcon.Bind(Projektanker.Icons.Avalonia.Icon.ForegroundProperty, this.GetResourceObservable("Text2"));
        }
    }

    /// <summary>Standaard "Test verbinding"-afhandeling.</summary>
    protected async Task RunTestAsync(Button button, string testingText, string resetText, Func<Task<bool>> test,
        string okText, string failText, Func<Exception, string> errorText, Func<Task>? afterSuccess = null)
    {
        button.IsEnabled = false;
        button.Content = testingText;
        try
        {
            if (await test())
            {
                ShowStatus(okText, true);
                IsConnected = true;
                if (afterSuccess != null) await afterSuccess();
                SaveButton.IsEnabled = true;
            }
            else
            {
                ShowStatus(failText, false);
                IsConnected = false;
            }
        }
        catch (Exception ex)
        {
            ShowStatus(errorText(ex), false);
            IsConnected = false;
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = resetText;
        }
    }

    protected void MarkConnected(string message)
    {
        IsConnected = true;
        SaveButton.IsEnabled = true;
        ShowStatus(message, true);
    }

    protected static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ─────────────────────────────── Jira ───────────────────────────────
public class JiraConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _url, _email, _token;
    private readonly ComboBox _project, _issueType;
    private readonly Button _test;
    private string? _cUrl, _cEmail, _cToken;

    public JiraConfigWindow(ApiClient api, Integration? existing = null)
        : base(L.T("JiraConfig_Header"), L.T("JiraConfig_Subtitle"), L.T("JiraConfig_Desc"), L.T("JiraConfig_Title"), 580, 760)
    {
        _api = api;
        _url = AddTextBox(L.T("JiraConfig_UrlLabel"), existing?.Config?.JiraUrl);
        AddHint(L.T("JiraConfig_UrlPlaceholder"));
        _email = AddTextBox(L.T("JiraConfig_EmailLabel"), existing?.Config?.Email);
        _token = AddTextBox(L.T("JiraConfig_TokenLabel"), null, L.T("JiraConfig_TokenHelp"), "https://id.atlassian.com/manage-profile/security/api-tokens", secret: true);
        _test = AddButton(L.T("JiraConfig_TestBtn"), TestAsync);
        AddStatusPanel();
        _project = AddComboBox(L.T("JiraConfig_DefaultProject"));
        AddHint(L.T("JiraConfig_ProjectHint"));
        _issueType = AddComboBox(L.T("JiraConfig_DefaultType"));
        AddHint(L.T("JiraConfig_TypeHint"));
        _project.SelectionChanged += async (_, _) =>
        {
            if (_project.SelectedItem is JiraProject p) await LoadIssueTypesAsync(p.Key);
        };
        if (existing?.IsActive == true) MarkConnected(L.T("JiraConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var url = _url.Text?.Trim() ?? "";
        var email = _email.Text?.Trim() ?? "";
        var token = _token.Text ?? "";
        if (url == "" || email == "" || token == "") { ShowStatus(L.T("JiraConfig_FillRequired"), false); return; }
        await RunTestAsync(_test, L.T("JiraConfig_Testing"), L.T("JiraConfig_TestBtn"),
            () => _api.TestJiraConnectionAsync(url, email, token),
            L.T("JiraConfig_TestOk"), L.T("JiraConfig_TestFail"), ex => L.Tf("JiraConfig_TestError", ex.Message),
            async () =>
            {
                _cUrl = url; _cEmail = email; _cToken = token;
                try
                {
                    var projects = await _api.GetJiraProjectsWithCredentialsAsync(url, email, token);
                    if (projects is { Length: > 0 })
                    {
                        _project.ItemsSource = projects;
                        _project.IsEnabled = true;
                        _project.SelectedIndex = 0;
                    }
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("JiraConfig_ProjectsLoadError", ex.Message), this); }
            });
    }

    private async Task LoadIssueTypesAsync(string projectKey)
    {
        if (_cUrl == null || _cEmail == null || _cToken == null) return;
        try
        {
            var types = await _api.GetJiraIssueTypesWithCredentialsAsync(_cUrl, _cEmail, _cToken, projectKey);
            if (types is { Length: > 0 })
            {
                _issueType.ItemsSource = types;
                _issueType.SelectedIndex = 0;
                _issueType.IsEnabled = true;
            }
        }
        catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("JiraConfig_TypesLoadError", ex.Message), this); }
    }

    protected override string SavingText => L.T("JiraConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("JiraConfig_ConnectionRequired"), L.T("JiraConfig_ConnectionRequiredDesc"), this); return false; }
        var ok = await _api.SaveJiraIntegrationAsync(_url.Text?.Trim() ?? "", _email.Text?.Trim() ?? "", NullIfEmpty(_token.Text),
            (_project.SelectedItem as JiraProject)?.Key, (_issueType.SelectedItem as JiraIssueType)?.Name, null, null);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("JiraConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("JiraConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────────────────── GitHub ───────────────────────────────
public class GitHubConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _token, _labels;
    private readonly ComboBox _repo;
    private readonly Button _test;

    public GitHubConfigWindow(ApiClient api, Integration? existing = null)
        : base(Loc.T("GitHubConfig_Header", "GITHUB · CONFIGURATIE"), Loc.T("GitHubConfig_Subtitle", "GitHub-integratie instellen"),
               Loc.T("GitHubConfig_Desc", "Verbind je GitHub-account om issues aan te maken via hotkeys."), Loc.T("GitHubConfig_Title", "GitHub Integratie Configureren"), 580, 700)
    {
        _api = api;
        _token = AddTextBox("Personal Access Token (PAT)", null, Loc.T("GitHubConfig_TokenHelp", "Hoe maak ik een PAT aan?"), "https://github.com/settings/tokens/new?scopes=repo", secret: true);
        AddHint(Loc.T("GitHubConfig_ScopeHint", "Vereiste scope: repo (voor private repos) of public_repo (alleen public)"));
        _test = AddButton(Loc.T("GitHubConfig_TestBtn", "Test verbinding"), TestAsync);
        AddStatusPanel();
        _repo = AddComboBox(Loc.T("GitHubConfig_RepoLabel", "Standaard repository"));
        AddHint(Loc.T("GitHubConfig_RepoHint", "Repository waar nieuwe issues worden aangemaakt"));
        _labels = AddTextBox(Loc.T("GitHubConfig_LabelsLabel", "Standaard labels (optioneel)"), existing?.Config?.DefaultLabels);
        AddHint(Loc.T("GitHubConfig_LabelsHint", "Kommagescheiden, bijv: bug, enhancement, maikey"));
        if (existing?.IsActive == true) MarkConnected(L.T("GitHubConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var token = _token.Text ?? "";
        if (string.IsNullOrWhiteSpace(token)) { ShowStatus(L.T("GitHubConfig_FillToken"), false); return; }
        await RunTestAsync(_test, L.T("GitHubConfig_Testing"), L.T("GitHubConfig_TestBtnReset"),
            () => _api.TestGitHubConnectionAsync(token), L.T("GitHubConfig_TestOk"), L.T("GitHubConfig_TestFail"),
            ex => L.Tf("GitHubConfig_TestError", ex.Message),
            async () =>
            {
                try
                {
                    var repos = await _api.GetGitHubReposAsync(token);
                    if (repos is { Length: > 0 })
                    {
                        _repo.ItemsSource = repos;
                        _repo.IsEnabled = true;
                        _repo.SelectedIndex = 0;
                    }
                    else ShowStatus(L.T("GitHubConfig_ConnectedNoRepos"), true);
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("GitHubConfig_ReposLoadError", ex.Message), this); }
            });
    }

    protected override string SavingText => L.T("GitHubConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("GitHubConfig_ConnectionRequired"), L.T("GitHubConfig_ConnectionRequiredDesc"), this); return false; }
        var ok = await _api.SaveGitHubIntegrationAsync(NullIfEmpty(_token.Text), (_repo.SelectedItem as GitHubRepo)?.FullName, NullIfEmpty(_labels.Text), null, null);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("GitHubConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("GitHubConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────────── Slack & Teams (webhook) ───────────────────────
public class WebhookConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly string _prefix;
    private readonly bool _teams;
    private readonly TextBox _url, _channel;
    private readonly Button _test;

    protected WebhookConfigWindow(ApiClient api, Integration? existing, bool teams)
        : base(teams ? Loc.T("TeamsConfig_Header", "TEAMS · CONFIGURATIE") : Loc.T("SlackConfig_Header", "SLACK · CONFIGURATIE"),
               teams ? Loc.T("TeamsConfig_Subtitle", "Teams-integratie instellen") : Loc.T("SlackConfig_Subtitle", "Slack-integratie instellen"),
               teams ? Loc.T("TeamsConfig_Desc", "Verbind je Microsoft Teams-kanaal om berichten te sturen via hotkeys.") : Loc.T("SlackConfig_Desc", "Verbind je Slack-workspace om berichten te sturen via hotkeys."),
               teams ? Loc.T("TeamsConfig_Title", "Microsoft Teams Integratie Configureren") : Loc.T("SlackConfig_Title", "Slack Integratie Configureren"), 580, 620)
    {
        _api = api;
        _teams = teams;
        _prefix = teams ? "TeamsConfig" : "SlackConfig";
        _url = AddTextBox("Incoming Webhook URL", null, Loc.T($"{_prefix}_WebhookHelp", "Hoe maak ik een webhook aan?"),
            teams ? "https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook" : "https://api.slack.com/messaging/webhooks");
        AddHint(teams ? Loc.T("TeamsConfig_WebhookHint", "Teams kanaal → Connectors → Incoming Webhook → URL kopiëren")
                      : Loc.T("SlackConfig_WebhookHint", "Begint met: https://hooks.slack.com/services/..."));
        _test = AddButton(Loc.T($"{_prefix}_TestBtn", "Test verbinding (stuurt testbericht)"), TestAsync);
        AddStatusPanel();
        _channel = AddTextBox(Loc.T($"{_prefix}_ChannelLabel", "Standaard kanaal (optioneel)"), existing?.Config?.DefaultChannel);
        AddHint(Loc.T($"{_prefix}_ChannelHint", "Bijv: #algemeen (ter info, de webhook bepaalt het kanaal)"));
        if (existing?.IsActive == true) MarkConnected(L.T($"{_prefix}_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var url = _url.Text?.Trim() ?? "";
        if (url == "") { ShowStatus(L.T($"{_prefix}_FillWebhook"), false); return; }
        await RunTestAsync(_test, L.T($"{_prefix}_Testing"), L.T($"{_prefix}_TestBtnReset"),
            () => _teams ? _api.TestTeamsConnectionAsync(url) : _api.TestSlackConnectionAsync(url),
            L.T($"{_prefix}_TestOk"), L.T($"{_prefix}_TestFail"), ex => L.Tf($"{_prefix}_TestError", ex.Message));
    }

    protected override string SavingText => L.T($"{_prefix}_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T($"{_prefix}_ConnectionRequired"), L.T($"{_prefix}_ConnectionRequiredDesc"), this); return false; }
        var url = NullIfEmpty(_url.Text);
        var channel = NullIfEmpty(_channel.Text);
        var ok = _teams ? await _api.SaveTeamsIntegrationAsync(url, channel, null, null)
                        : await _api.SaveSlackIntegrationAsync(url, channel, null, null);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T($"{_prefix}_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T($"{_prefix}_SaveFail"), this);
        return false;
    }
}

public class SlackConfigWindow : WebhookConfigWindow
{
    public SlackConfigWindow(ApiClient api, Integration? existing = null) : base(api, existing, false) { }
}

public class TeamsConfigWindow : WebhookConfigWindow
{
    public TeamsConfigWindow(ApiClient api, Integration? existing = null) : base(api, existing, true) { }
}

// ─────────────────────────────── Zapier / Make ───────────────────────────────
public class ZapierConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _url, _name;
    private readonly Button _test;

    public ZapierConfigWindow(ApiClient api, Integration? existing = null)
        : base(Loc.T("ZapierConfig_Header", "ZAPIER / MAKE · CONFIGURATIE"), Loc.T("ZapierConfig_Subtitle", "Webhook-integratie instellen"),
               Loc.T("ZapierConfig_Desc", "Verbind mAIkey met Zapier, Make of andere webhook-platforms om data automatisch door te sturen."),
               Loc.T("ZapierConfig_Title", "Zapier/Make Webhook Configureren"), 580, 620)
    {
        _api = api;
        _url = AddTextBox("Webhook URL", null, "Zapier Webhooks", "https://zapier.com/apps/webhook/integrations");
        var make = new Button { Classes = { "link" }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0, -8, 0, 8), HorizontalAlignment = HorizontalAlignment.Right };
        var mt = Text("Make Webhooks", 11.5, "Accent");
        mt.TextDecorations = TextDecorations.Underline;
        make.Content = mt;
        make.Click += (_, _) => Ui.OpenUrl("https://www.make.com/en/help/tools/webhooks");
        Form.Children.Add(make);
        AddHint(Loc.T("ZapierConfig_WebhookHint", "Plak je Zapier Catch Hook URL of Make Webhook URL"));
        _test = AddButton(Loc.T("ZapierConfig_TestBtn", "Test webhook (stuurt test-payload)"), TestAsync);
        AddStatusPanel();
        _name = AddTextBox(Loc.T("ZapierConfig_NameLabel", "Webhook naam (optioneel)"), existing?.Config?.WebhookName);
        AddHint(Loc.T("ZapierConfig_NameHint", "Bijv: Mijn Trello webhook, Notion sync, etc."));
        if (existing?.IsActive == true) MarkConnected(L.T("ZapierConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var url = _url.Text?.Trim() ?? "";
        if (url == "") { ShowStatus(L.T("ZapierConfig_FillWebhook"), false); return; }
        await RunTestAsync(_test, L.T("ZapierConfig_Testing"), L.T("ZapierConfig_TestBtnReset"),
            () => _api.TestZapierConnectionAsync(url), L.T("ZapierConfig_TestOk"), L.T("ZapierConfig_TestFail"),
            ex => L.Tf("ZapierConfig_TestError", ex.Message));
    }

    protected override string SavingText => L.T("ZapierConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("ZapierConfig_ConnectionRequired"), L.T("ZapierConfig_ConnectionRequiredDesc"), this); return false; }
        var ok = await _api.SaveZapierIntegrationAsync(NullIfEmpty(_url.Text), NullIfEmpty(_name.Text), null);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("ZapierConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("ZapierConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────────────────── Todoist ───────────────────────────────
public class TodoistConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _token, _labels;
    private readonly ComboBox _project;
    private readonly Button _test;
    private readonly string? _existingProjectId;

    public TodoistConfigWindow(ApiClient api, Integration? existing = null)
        : base(Loc.T("TodoistConfig_Header", "TODOIST · CONFIGURATIE"), Loc.T("TodoistConfig_Subtitle", "Todoist-integratie instellen"),
               Loc.T("TodoistConfig_Desc", "Verbind je Todoist-account om taken aan te maken via hotkeys."), Loc.T("TodoistConfig_Title", "Todoist Integratie Configureren"), 580, 700)
    {
        _api = api;
        _existingProjectId = existing?.Config?.DefaultProjectId;
        _token = AddTextBox(Loc.T("TodoistConfig_TokenLabel", "API-token"), null, Loc.T("TodoistConfig_TokenHelp", "Waar vind ik mijn API-token?"), "https://app.todoist.com/app/settings/integrations/developer", secret: true);
        AddHint(Loc.T("TodoistConfig_TokenHint", "Todoist → Instellingen → Integraties → Ontwikkelaar → API-token"));
        _test = AddButton(Loc.T("TodoistConfig_TestBtn", "Test verbinding"), TestAsync);
        AddStatusPanel();
        _project = AddComboBox(Loc.T("TodoistConfig_ProjectLabel", "Standaard project"));
        AddHint(Loc.T("TodoistConfig_ProjectHint", "Project waar nieuwe taken worden aangemaakt (leeg = Inbox)"));
        _labels = AddTextBox(Loc.T("TodoistConfig_LabelsLabel", "Standaard labels (optioneel)"), existing?.Config?.DefaultLabels);
        AddHint(Loc.T("TodoistConfig_LabelsHint", "Kommagescheiden, bijv: werk, maikey"));
        if (existing?.IsActive == true) MarkConnected(L.T("TodoistConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var token = _token.Text ?? "";
        if (string.IsNullOrWhiteSpace(token)) { ShowStatus(L.T("TodoistConfig_FillToken"), false); return; }
        await RunTestAsync(_test, L.T("TodoistConfig_Testing"), L.T("TodoistConfig_TestBtnReset"),
            () => _api.TestTodoistConnectionAsync(token), L.T("TodoistConfig_TestOk"), L.T("TodoistConfig_TestFail"),
            ex => L.Tf("TodoistConfig_TestError", ex.Message),
            async () =>
            {
                try
                {
                    var projects = await _api.GetTodoistProjectsAsync(token);
                    if (projects is { Length: > 0 })
                    {
                        _project.ItemsSource = projects;
                        _project.IsEnabled = true;
                        var idx = !string.IsNullOrEmpty(_existingProjectId) ? Array.FindIndex(projects, p => p.Id == _existingProjectId) : -1;
                        _project.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                    else ShowStatus(L.T("TodoistConfig_ConnectedNoProjects"), true);
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("TodoistConfig_ProjectsLoadError", ex.Message), this); }
            });
    }

    protected override string SavingText => L.T("TodoistConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("TodoistConfig_ConnectionRequired"), L.T("TodoistConfig_ConnectionRequiredDesc"), this); return false; }
        var p = _project.SelectedItem as TodoistProject;
        var ok = await _api.SaveTodoistIntegrationAsync(NullIfEmpty(_token.Text), p?.Id, p?.Name, NullIfEmpty(_labels.Text));
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("TodoistConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("TodoistConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────────────────── Trello ───────────────────────────────
public class TrelloConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _key, _token;
    private readonly ComboBox _board, _list;
    private readonly Button _test;
    private readonly string? _existingBoardId, _existingListId;

    public TrelloConfigWindow(ApiClient api, Integration? existing = null)
        : base(Loc.T("TrelloConfig_Header", "TRELLO · CONFIGURATIE"), Loc.T("TrelloConfig_Subtitle", "Trello-integratie instellen"),
               Loc.T("TrelloConfig_Desc", "Verbind je Trello-account om kaarten aan te maken via hotkeys."), Loc.T("TrelloConfig_Title", "Trello Integratie Configureren"), 580, 760)
    {
        _api = api;
        _existingBoardId = existing?.Config?.DefaultBoardId;
        _existingListId = existing?.Config?.DefaultListId;
        _key = AddTextBox(Loc.T("TrelloConfig_KeyLabel", "API-key"), null, Loc.T("TrelloConfig_KeyHelp", "Waar vind ik mijn API-key en token?"), "https://trello.com/power-ups/admin");
        _token = AddTextBox("Token", null, secret: true);
        AddHint(Loc.T("TrelloConfig_TokenHint", "Maak een Power-Up aan (mag leeg blijven) → API-key kopiëren → via de Token-link een token genereren"));
        _test = AddButton(Loc.T("TrelloConfig_TestBtn", "Test verbinding"), TestAsync);
        AddStatusPanel();
        _board = AddComboBox(Loc.T("TrelloConfig_BoardLabel", "Standaard bord"));
        AddHint(Loc.T("TrelloConfig_BoardHint", "Bord waarop nieuwe kaarten worden aangemaakt"));
        _list = AddComboBox(Loc.T("TrelloConfig_ListLabel", "Standaard lijst"));
        AddHint(Loc.T("TrelloConfig_ListHint", "Lijst (kolom) waarin nieuwe kaarten komen, bijv. 'Te doen'"));
        _board.SelectionChanged += async (_, _) => await LoadListsAsync();
        if (existing?.IsActive == true) MarkConnected(L.T("TrelloConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var key = _key.Text?.Trim() ?? "";
        var token = _token.Text ?? "";
        if (key == "" || string.IsNullOrWhiteSpace(token)) { ShowStatus(L.T("TrelloConfig_FillKeyToken"), false); return; }
        await RunTestAsync(_test, L.T("TrelloConfig_Testing"), L.T("TrelloConfig_TestBtnReset"),
            () => _api.TestTrelloConnectionAsync(key, token), L.T("TrelloConfig_TestOk"), L.T("TrelloConfig_TestFail"),
            ex => L.Tf("TrelloConfig_TestError", ex.Message),
            async () =>
            {
                try
                {
                    var boards = await _api.GetTrelloBoardsAsync(key, token);
                    if (boards is { Length: > 0 })
                    {
                        _board.ItemsSource = boards;
                        _board.IsEnabled = true;
                        var idx = !string.IsNullOrEmpty(_existingBoardId) ? Array.FindIndex(boards, b => b.Id == _existingBoardId) : -1;
                        _board.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                    else ShowStatus(L.T("TrelloConfig_ConnectedNoBoards"), true);
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("TrelloConfig_BoardsLoadError", ex.Message), this); }
            });
    }

    private async Task LoadListsAsync()
    {
        if (_board.SelectedItem is not TrelloBoard board) return;
        var key = _key.Text?.Trim() ?? "";
        var token = _token.Text ?? "";
        if (key == "" || string.IsNullOrWhiteSpace(token)) return;
        _list.IsEnabled = false;
        _list.ItemsSource = null;
        try
        {
            var lists = await _api.GetTrelloListsAsync(key, token, board.Id);
            if (lists is { Length: > 0 })
            {
                _list.ItemsSource = lists;
                _list.IsEnabled = true;
                var idx = !string.IsNullOrEmpty(_existingListId) ? Array.FindIndex(lists, l => l.Id == _existingListId) : -1;
                _list.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }
        catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("TrelloConfig_ListsLoadError", ex.Message), this); }
    }

    protected override string SavingText => L.T("TrelloConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("TrelloConfig_ConnectionRequired"), L.T("TrelloConfig_ConnectionRequiredDesc"), this); return false; }
        string? key = NullIfEmpty(_key.Text), token = NullIfEmpty(_token.Text);
        if ((key == null) != (token == null)) { await MkDialog.ShowError(L.T("Common_Error"), L.T("TrelloConfig_FillKeyToken"), this); return false; }
        var b = _board.SelectedItem as TrelloBoard;
        var l = _list.SelectedItem as TrelloList;
        var ok = await _api.SaveTrelloIntegrationAsync(key, token, b?.Id, b?.Name, l?.Id, l?.Name);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("TrelloConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("TrelloConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────────────────── Asana ───────────────────────────────
public class AsanaConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly TextBox _token;
    private readonly ComboBox _workspace, _project;
    private readonly Button _test;
    private readonly string? _existingWorkspaceId, _existingProjectId;

    public AsanaConfigWindow(ApiClient api, Integration? existing = null)
        : base(Loc.T("AsanaConfig_Header", "ASANA · CONFIGURATIE"), Loc.T("AsanaConfig_Subtitle", "Asana-integratie instellen"),
               Loc.T("AsanaConfig_Desc", "Verbind je Asana-account om taken aan te maken via hotkeys."), Loc.T("AsanaConfig_Title", "Asana Integratie Configureren"), 580, 720)
    {
        _api = api;
        _existingWorkspaceId = existing?.Config?.DefaultWorkspaceId;
        _existingProjectId = existing?.Config?.DefaultProjectId;
        _token = AddTextBox("Personal Access Token", null, Loc.T("AsanaConfig_TokenHelp", "Waar maak ik een token aan?"), "https://app.asana.com/0/my-apps", secret: true);
        AddHint(Loc.T("AsanaConfig_TokenHint", "Asana → Instellingen → Apps → Developer console → Personal access token aanmaken"));
        _test = AddButton(Loc.T("AsanaConfig_TestBtn", "Test verbinding"), TestAsync);
        AddStatusPanel();
        _workspace = AddComboBox("Workspace");
        AddHint(Loc.T("AsanaConfig_WorkspaceHint", "Je Asana-workspace (meestal maar één)"));
        _project = AddComboBox(Loc.T("AsanaConfig_ProjectLabel", "Standaard project"));
        AddHint(Loc.T("AsanaConfig_ProjectHint", "Project waarin nieuwe taken worden aangemaakt"));
        _workspace.SelectionChanged += async (_, _) => await LoadProjectsAsync();
        if (existing?.IsActive == true) MarkConnected(L.T("AsanaConfig_AlreadyConnected"));
    }

    private async Task TestAsync()
    {
        var token = _token.Text ?? "";
        if (string.IsNullOrWhiteSpace(token)) { ShowStatus(L.T("AsanaConfig_FillToken"), false); return; }
        await RunTestAsync(_test, L.T("AsanaConfig_Testing"), L.T("AsanaConfig_TestBtnReset"),
            () => _api.TestAsanaConnectionAsync(token), L.T("AsanaConfig_TestOk"), L.T("AsanaConfig_TestFail"),
            ex => L.Tf("AsanaConfig_TestError", ex.Message),
            async () =>
            {
                try
                {
                    var ws = await _api.GetAsanaWorkspacesAsync(token);
                    if (ws is { Length: > 0 })
                    {
                        _workspace.ItemsSource = ws;
                        _workspace.IsEnabled = true;
                        var idx = !string.IsNullOrEmpty(_existingWorkspaceId) ? Array.FindIndex(ws, w => w.Gid == _existingWorkspaceId) : -1;
                        _workspace.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                    else ShowStatus(L.T("AsanaConfig_ConnectedNoWorkspaces"), true);
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("AsanaConfig_WorkspacesLoadError", ex.Message), this); }
            });
    }

    private async Task LoadProjectsAsync()
    {
        if (_workspace.SelectedItem is not AsanaWorkspace w) return;
        var token = _token.Text ?? "";
        if (string.IsNullOrWhiteSpace(token)) return;
        _project.IsEnabled = false;
        _project.ItemsSource = null;
        try
        {
            var projects = await _api.GetAsanaProjectsAsync(token, w.Gid);
            if (projects is { Length: > 0 })
            {
                _project.ItemsSource = projects;
                _project.IsEnabled = true;
                var idx = !string.IsNullOrEmpty(_existingProjectId) ? Array.FindIndex(projects, p => p.Gid == _existingProjectId) : -1;
                _project.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }
        catch (Exception ex) { await MkDialog.ShowError(L.T("Common_Error"), L.Tf("AsanaConfig_ProjectsLoadError", ex.Message), this); }
    }

    protected override string SavingText => L.T("AsanaConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("AsanaConfig_ConnectionRequired"), L.T("AsanaConfig_ConnectionRequiredDesc"), this); return false; }
        var w = _workspace.SelectedItem as AsanaWorkspace;
        var p = _project.SelectedItem as AsanaProject;
        var ok = await _api.SaveAsanaIntegrationAsync(NullIfEmpty(_token.Text), w?.Gid, w?.Name, p?.Gid, p?.Name);
        if (ok) { await MkDialog.ShowInfo(L.T("Common_Success"), L.T("AsanaConfig_SaveOk"), this); return true; }
        await MkDialog.ShowError(L.T("Common_Error"), L.T("AsanaConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────── Google (Gmail / Agenda / Taken) ───────────────────
public abstract class GoogleConfigWindowBase : ConfigWindowBase
{
    protected readonly ApiClient Api;
    private readonly string _type;
    private readonly string _prefix;
    private readonly Button _connect;
    private DispatcherTimer? _poll;

    protected GoogleConfigWindowBase(ApiClient api, string type, string prefix, string eyebrow, string title, string subtitle, string windowTitle, double height)
        : base(eyebrow, title, subtitle, windowTitle, 560, height)
    {
        Api = api;
        _type = type;
        _prefix = prefix;
        AddLabel(Loc.T($"{prefix}_Step1", "Stap 1: Verbind je Google-account"));
        _connect = AddButton(Loc.T($"{prefix}_ConnectBtn", "Verbinden met Google"), ConnectAsync, accent: true);
        AddStatusPanel();
        Closed += (_, _) => _poll?.Stop();
    }

    protected void SetReconnect() => _connect.Content = Loc.T($"{_prefix}_Reconnect", "Opnieuw verbinden");

    private async Task ConnectAsync()
    {
        _connect.IsEnabled = false;
        _connect.Content = Loc.T($"{_prefix}_Opening", L.T("GmailConfig_OpeningBrowser"));
        try
        {
            var url = await Api.StartGoogleOAuthAsync(_type);
            if (string.IsNullOrEmpty(url))
            {
                await MkDialog.ShowError(L.T("Common_Error"), Loc.T($"{_prefix}_AuthUrlError", L.T("GmailConfig_AuthUrlError")), this);
                return;
            }
            Ui.OpenUrl(url);
            ShowStatus(Loc.T($"{_prefix}_WaitingBrowser", L.T("GmailConfig_WaitingBrowser")), false, loading: true);
            _poll?.Stop();
            _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _poll.Tick += async (_, _) =>
            {
                try
                {
                    var (ok, email) = await Api.GetGoogleOAuthStatusAsync(_type);
                    if (!ok) return;
                    _poll!.Stop();
                    IsConnected = true;
                    await OnConnectedAsync(email);
                    Activate();
                }
                catch { }
            };
            _poll.Start();
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), Loc.Tf($"{_prefix}_AuthError", "{0}", ex.Message), this);
        }
        finally
        {
            _connect.IsEnabled = true;
            SetReconnect();
        }
    }

    protected abstract Task OnConnectedAsync(string? email);
}

public class GmailConfigWindow : GoogleConfigWindowBase, IConfigWindow
{
    public GmailConfigWindow(ApiClient api, Integration? existing = null)
        : base(api, "gmail", "GmailConfig", Loc.T("GmailConfig_Header", "GMAIL · CONFIGURATIE"), Loc.T("GmailConfig_Heading", "Gmail verbinden"),
               Loc.T("GmailConfig_Subtitle", "Verbind je Google-account om e-mails te genereren en versturen via hotkeys."), Loc.T("GmailConfig_Title", "Gmail Integratie Configureren"), 440)
    {
        var email = existing?.Config?.GmailEmail ?? existing?.Config?.Email;
        if (!string.IsNullOrEmpty(email))
        {
            MarkConnected(L.Tf("GmailConfig_ConnectedAs", email));
            SetReconnect();
        }
    }

    protected override Task OnConnectedAsync(string? email)
    {
        SaveButton.IsEnabled = true;
        ShowStatus(L.Tf("GmailConfig_ConnectedAs", email ?? ""), true);
        return Task.CompletedTask;
    }

    protected override string SavingText => L.T("GmailConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("GmailConfig_ConnectionRequired"), L.T("GmailConfig_ConnectionRequiredDesc"), this); return false; }
        if (await Api.SaveGmailConfigAsync(null, null)) return true;
        await MkDialog.ShowError(L.T("Common_Error"), L.T("GmailConfig_SaveFail"), this);
        return false;
    }
}

public class CalendarConfigWindow : GoogleConfigWindowBase, IConfigWindow
{
    private readonly ComboBox _calendar;

    public CalendarConfigWindow(ApiClient api, Integration? existing = null)
        : base(api, "google_calendar", "CalConfig", Loc.T("CalConfig_Header", "GOOGLE AGENDA · CONFIGURATIE"), L.T("CalConfig_Heading"),
               L.T("CalConfig_Subtitle"), L.T("CalConfig_Title"), 560)
    {
        _calendar = AddComboBox(L.T("CalConfig_Step2"), "Summary");
        AddHint(L.T("CalConfig_CalHint"));
        CancelButton.Content = L.T("CalConfig_Cancel");
        SaveButton.Content = L.T("CalConfig_Save");
        var email = existing?.Config?.GmailEmail ?? existing?.Config?.Email;
        if (!string.IsNullOrEmpty(email))
        {
            IsConnected = true;
            ShowStatus($"✅ {L.T("CalConfig_ConnectedAs")} {email}", true);
            SetReconnect();
            _ = LoadCalendarsAsync(existing?.Config?.DefaultCalendarId);
        }
    }

    protected override async Task OnConnectedAsync(string? email)
    {
        ShowStatus($"✅ {L.T("CalConfig_ConnectedAs")} {email}", true);
        await LoadCalendarsAsync(null);
    }

    private async Task LoadCalendarsAsync(string? selectedId)
    {
        try
        {
            _calendar.IsEnabled = false;
            var cals = await Api.GetCalendarListAsync();
            if (cals is { Length: > 0 })
            {
                _calendar.ItemsSource = cals;
                _calendar.IsEnabled = true;
                _calendar.SelectedItem = (!string.IsNullOrEmpty(selectedId) ? cals.FirstOrDefault(c => c.Id == selectedId) : null)
                                         ?? cals.FirstOrDefault(c => c.Primary) ?? cals[0];
            }
        }
        catch { }
        SaveButton.IsEnabled = IsConnected;
    }

    protected override string SavingText => L.T("CalConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("CalConfig_ConnectionRequired"), L.T("CalConfig_ConnectionRequiredDesc"), this); return false; }
        if (await Api.SaveCalendarConfigAsync((_calendar.SelectedItem as GoogleCalendar)?.Id, null)) return true;
        await MkDialog.ShowError(L.T("Common_Error"), L.T("CalConfig_SaveFail"), this);
        return false;
    }
}

public class GoogleTasksConfigWindow : GoogleConfigWindowBase, IConfigWindow
{
    private readonly ComboBox _list;

    public GoogleTasksConfigWindow(ApiClient api, Integration? existing = null)
        : base(api, "google_tasks", "GTasksConfig", Loc.T("GTasksConfig_Header", "GOOGLE TASKS · CONFIGURATIE"), L.T("GTasksConfig_Heading"),
               L.T("GTasksConfig_Subtitle"), L.T("GTasksConfig_Title"), 560)
    {
        _list = AddComboBox(L.T("GTasksConfig_Step2"));
        AddHint(L.T("GTasksConfig_ListHint"));
        CancelButton.Content = L.T("GTasksConfig_Cancel");
        SaveButton.Content = L.T("GTasksConfig_Save");
        var email = existing?.Config?.Email ?? existing?.Config?.GmailEmail;
        if (!string.IsNullOrEmpty(email))
        {
            IsConnected = true;
            ShowStatus($"✅ {L.T("GTasksConfig_ConnectedAs")} {email}", true);
            SetReconnect();
            _ = LoadListsAsync(existing?.Config?.DefaultTaskListId);
        }
    }

    protected override async Task OnConnectedAsync(string? email)
    {
        ShowStatus($"✅ {L.T("GTasksConfig_ConnectedAs")} {email}", true);
        await LoadListsAsync(null);
    }

    private async Task LoadListsAsync(string? selectedId)
    {
        try
        {
            _list.IsEnabled = false;
            var lists = await Api.GetTaskListsAsync();
            if (lists is { Length: > 0 })
            {
                _list.ItemsSource = lists;
                _list.IsEnabled = true;
                _list.SelectedItem = (!string.IsNullOrEmpty(selectedId) ? lists.FirstOrDefault(l => l.Id == selectedId) : null) ?? lists[0];
            }
        }
        catch { }
        SaveButton.IsEnabled = IsConnected;
    }

    protected override string SavingText => L.T("GTasksConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { await MkDialog.ShowError(L.T("GTasksConfig_ConnectionRequired"), L.T("GTasksConfig_ConnectionRequiredDesc"), this); return false; }
        var l = _list.SelectedItem as GoogleTaskList;
        if (await Api.SaveGoogleTasksConfigAsync(l?.Id, l?.Title)) return true;
        await MkDialog.ShowError(L.T("Common_Error"), L.T("GTasksConfig_SaveFail"), this);
        return false;
    }
}

// ─────────────────── Generiek (Notion, Linear, Airtable, HubSpot, Zendesk) ───────────────────
public class GenericTokenConfigWindow : ConfigWindowBase
{
    private readonly ApiClient _api;
    private readonly IntegrationDescriptor _desc;
    private readonly Dictionary<string, TextBox> _inputs = new();
    private readonly StackPanel _optionsPanel = new() { IsVisible = false };
    private readonly ComboBox _options = new() { Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 14) };
    private readonly Button _test;

    public GenericTokenConfigWindow(ApiClient api, IntegrationDescriptor desc)
        : base($"{desc.Name.ToUpperInvariant()} · {L.T("GenericConfig_Eyebrow")}", L.Tf("GenericConfig_Title", desc.Name), desc.Description,
               L.Tf("GenericConfig_Title", desc.Name), 560, 600)
    {
        _api = api;
        _desc = desc;
        foreach (var f in desc.Fields)
            _inputs[f.Key] = AddTextBox(f.Label, null, f.HelpUrl != null ? L.T("GenericConfig_WhereToFind") : null, f.HelpUrl, f.Secret, f.Placeholder);
        _test = AddButton(L.T("GenericConfig_Test"), TestAsync);
        AddStatusPanel();
        if (desc.OptionsLabel != null)
        {
            _optionsPanel.Children.Add(Text(desc.OptionsLabel, 12, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6)));
            _optionsPanel.Children.Add(_options);
            Form.Children.Add(_optionsPanel);
        }
    }

    private Dictionary<string, string> Credentials() =>
        _desc.Fields.ToDictionary(f => f.Key, f => (_inputs[f.Key].Text ?? "").Trim());

    private async Task TestAsync()
    {
        var creds = Credentials();
        var missing = _desc.Fields.FirstOrDefault(f => string.IsNullOrWhiteSpace(creds[f.Key]));
        if (missing != null) { ShowStatus(L.Tf("GenericConfig_FieldRequired", missing.Label), false); return; }

        _test.IsEnabled = false;
        _test.Content = L.T("GenericConfig_Testing");
        try
        {
            var result = await _api.TestGenericIntegrationAsync(_desc.Type, creds);
            if (!result.Success)
            {
                ShowStatus(result.Error ?? L.T("GenericConfig_TestFail"), false);
                IsConnected = false;
                return;
            }
            IsConnected = true;
            ShowStatus(L.T("GenericConfig_TestOk"), true);
            if (_desc.OptionsLabel != null)
            {
                var opts = await _api.GetGenericOptionsAsync(_desc.Type, creds);
                if (opts.Success && opts.Options.Count > 0)
                {
                    _options.ItemsSource = opts.Options;
                    _options.SelectedIndex = 0;
                    _optionsPanel.IsVisible = true;
                }
            }
            SaveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, false);
            IsConnected = false;
        }
        finally
        {
            _test.IsEnabled = true;
            _test.Content = L.T("GenericConfig_Test");
        }
    }

    protected override string SavingText => L.T("GenericConfig_Saving");

    protected override async Task<bool> SaveAsync()
    {
        if (!IsConnected) { ShowStatus(L.T("GenericConfig_TestFirst"), false); return false; }
        var config = new Dictionary<string, string?>();
        if (_desc.OptionsLabel != null && !string.IsNullOrEmpty(_desc.OptionsConfigKey) && _options.SelectedItem is GenericOption opt)
        {
            config[_desc.OptionsConfigKey] = opt.Id;
            config[_desc.OptionsConfigKey + "Name"] = opt.Name;
        }
        var result = await _api.SaveGenericIntegrationAsync(_desc.Type, Credentials(), config);
        if (result.Success) return true;
        ShowStatus(result.Error ?? L.T("GenericConfig_SaveFail"), false);
        return false;
    }
}
