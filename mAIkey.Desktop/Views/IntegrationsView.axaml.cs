using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Services;
using Projektanker.Icons.Avalonia;

namespace mAIkey.Desktop.Views;

public partial class IntegrationsView : UserControl
{
    // type, naam, omschrijving-KEY (via L.T), MDI-icoon, merkkleur
    private static readonly (string Type, string Name, string DescKey, string Icon, string Color)[] Supported =
    {
        ("jira",      "Jira",             "Integrations_JiraDesc",     "mdi-jira",                          "#2684FF"),
        ("github",    "GitHub",           "Integrations_GitHubDesc",   "mdi-github",                        "#8B949E"),
        ("slack",     "Slack",            "Integrations_SlackDesc",    "mdi-slack",                         "#E01E5A"),
        ("teams",     "Microsoft Teams",  "Integrations_TeamsDesc",    "mdi-microsoft-teams",               "#6264A7"),
        ("trello",    "Trello",           "Integrations_TrelloDesc",   "mdi-trello",                        "#0079BF"),
        ("asana",     "Asana",            "Integrations_AsanaDesc",    "mdi-checkbox-marked-circle",        "#F06A6A"),
        ("todoist",   "Todoist",          "Integrations_TodoistDesc",  "mdi-checkbox-marked-circle-outline","#E44332"),
        ("gmail",     "Gmail",            "Integrations_GmailDesc",    "mdi-gmail",                         "#EA4335"),
        ("gcalendar", "Google Agenda",    "Integrations_CalendarDesc", "mdi-calendar-month",                "#4285F4"),
        ("gtasks",    "Google Taken",     "Integrations_GTasksDesc",   "mdi-format-list-checks",            "#4285F4"),
        ("zapier",    "Zapier / Make",    "Integrations_ZapierDesc",   "mdi-lightning-bolt",                "#FF4A00"),
    };

    public IntegrationsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
        L.Changed += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) => L.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => _ = LoadAsync();

    /// <summary>Thema-kleur ophalen (past zich aan donker/taupe aan).</summary>
    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private async Task LoadAsync()
    {
        var map = new System.Collections.Generic.Dictionary<string, mAIkey.Core.Models.Integration>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var integrations = await App.Api.GetIntegrationsAsync();
            if (integrations != null)
                foreach (var i in integrations.Where(i => i.IsActive))
                    map[i.IntegrationType] = i;
            StatusText.IsVisible = false;
        }
        catch
        {
            StatusText.Text = "Verbindingsstatus kon niet laden (offline?). Koppelingen worden wel getoond.";
        }

        IntegrationsPanel.Children.Clear();
        foreach (var m in Supported)
            IntegrationsPanel.Children.Add(BuildCard(m, map.TryGetValue(m.Type, out var it) ? it : null));
    }

    private static string? DetailFor(string type, mAIkey.Core.Models.IntegrationConfig? c)
    {
        if (c == null) return null;
        return type switch
        {
            "jira" => c.JiraUrl ?? c.Email,
            "github" => c.DefaultRepo,
            "slack" or "teams" => c.DefaultChannel,
            "gmail" => c.GmailEmail,
            "trello" => c.DefaultBoardName,
            "zapier" => c.WebhookName,
            _ => null
        };
    }

    private Control BuildCard((string Type, string Name, string DescKey, string Icon, string Color) m, mAIkey.Core.Models.Integration? integration)
    {
        bool isConnected = integration != null;
        var brand = new SolidColorBrush(Color.Parse(m.Color));

        // Logo-tegel (wit, afgerond, merkicoon)
        var logo = new Border
        {
            Width = 46, Height = 46, CornerRadius = new Avalonia.CornerRadius(13),
            Background = Brushes.White,
            Child = new Icon { Value = m.Icon, FontSize = 26, Foreground = brand,
                               HorizontalAlignment = HorizontalAlignment.Center,
                               VerticalAlignment = VerticalAlignment.Center }
        };

        // Statusbadge
        var dot = new Ellipse
        {
            Width = 7, Height = 7, Margin = new Avalonia.Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isConnected) dot.Fill = TB("Success");
        else { dot.Stroke = TB("Text3"); dot.StrokeThickness = 1.5; dot.Fill = Brushes.Transparent; }

        var statusText = new TextBlock
        {
            Text = L.T(isConnected ? "Integrations_Connected" : "Integrations_Available"),
            FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center,
            Foreground = isConnected ? TB("Text2") : TB("Text3")
        };
        var badge = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        badge.Children.Add(dot);
        badge.Children.Add(statusText);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Avalonia.Thickness(0, 0, 0, 14) };
        header.Children.Add(logo);
        Grid.SetColumn(badge, 1);
        badge.HorizontalAlignment = HorizontalAlignment.Right;
        header.Children.Add(badge);

        var name = new TextBlock { Text = m.Name, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Avalonia.Thickness(0, 0, 0, 4) };
        var desc = new TextBlock { Text = L.T(m.DescKey), FontSize = 13, TextWrapping = TextWrapping.Wrap, Height = 38 };
        desc.Classes.Add("muted");

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(name);
        stack.Children.Add(desc);

        // Details-regel bij verbonden koppeling
        var detail = DetailFor(m.Type, integration?.Config);
        if (isConnected && !string.IsNullOrEmpty(detail))
        {
            var d = new TextBlock
            {
                Text = detail, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
                Foreground = TB("Text2")
            };
            stack.Children.Add(d);
        }

        var btn = new Button
        {
            Content = isConnected ? "Wijzigen" : L.T("Integrations_Connect"),
            Height = 32, FontSize = 12, VerticalAlignment = VerticalAlignment.Center
        };
        btn.Classes.Add(isConnected ? "ghost" : "accent");
        WireConfig(btn, m.Type);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Avalonia.Thickness(0, 14, 0, 0)
        };
        actions.Children.Add(btn);
        if (isConnected)
        {
            var disc = new Button { Content = L.T("Integrations_Disconnect"), Height = 32, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            disc.Classes.Add("danger");
            disc.Click += async (_, _) =>
            {
                disc.IsEnabled = false;
                if (await App.Api.DeleteIntegrationAsync(integration!.Id))
                    await LoadAsync();
                else disc.IsEnabled = true;
            };
            actions.Children.Add(disc);
        }
        stack.Children.Add(actions);

        var card = new Border
        {
            Child = stack,
            Width = 250,
            Margin = new Avalonia.Thickness(0, 0, 14, 14),
            Padding = new Avalonia.Thickness(20)
        };
        card.Classes.Add("card");
        return card;
    }

    private void WireConfig(Button btn, string type)
    {
        switch (type)
        {
            case "jira": btn.Click += async (_, _) => await OpenConfig(new Windows.JiraConfigWindow()); break;
            case "github": btn.Click += async (_, _) => await OpenConfig(new Windows.GitHubConfigWindow()); break;
            case "slack": btn.Click += async (_, _) => await OpenConfig(new Windows.SlackConfigWindow()); break;
            case "gmail": btn.Click += async (_, _) => await OpenConfig(new Windows.GoogleConfigWindow("gmail", "Gmail")); break;
            case "gcalendar": btn.Click += async (_, _) => await OpenConfig(new Windows.GoogleConfigWindow("gcalendar", "Google Agenda")); break;
            case "gtasks": btn.Click += async (_, _) => await OpenConfig(new Windows.GoogleConfigWindow("gtasks", "Google Taken")); break;
            case "todoist": btn.Click += async (_, _) => await OpenConfig(new Windows.GenericTokenConfigWindow(
                "Todoist", "Verbind met je Todoist API-token (Instellingen → Integraties → API-token).", "API-token", null,
                (t, _) => App.Api.TestTodoistConnectionAsync(t), (t, _) => App.Api.SaveTodoistIntegrationAsync(t))); break;
            case "trello": btn.Click += async (_, _) => await OpenConfig(new Windows.GenericTokenConfigWindow(
                "Trello", "Verbind met je Trello API-key en token (trello.com/app-key).", "API-key", "Token",
                (k, t) => App.Api.TestTrelloConnectionAsync(k, t), (k, t) => App.Api.SaveTrelloIntegrationAsync(k, t))); break;
            case "zapier": btn.Click += async (_, _) => await OpenConfig(new Windows.GenericTokenConfigWindow(
                "Zapier / Make", "Verbind via een webhook-URL uit je Zap of Make-scenario.", "Webhook-URL", null,
                (u, _) => App.Api.TestZapierConnectionAsync(u), (u, _) => App.Api.SaveZapierIntegrationAsync(u))); break;
            case "teams": btn.Click += async (_, _) => await OpenConfig(new Windows.GenericTokenConfigWindow(
                "Microsoft Teams", "Verbind via een Incoming Webhook-URL van je Teams-kanaal.", "Webhook-URL", null,
                (u, _) => App.Api.TestTeamsConnectionAsync(u), (u, _) => App.Api.SaveTeamsIntegrationAsync(u))); break;
            case "asana": btn.Click += async (_, _) => await OpenConfig(new Windows.GenericTokenConfigWindow(
                "Asana", "Verbind met een Personal Access Token (Asana → Settings → Apps → PAT).", "Personal Access Token", null,
                (t, _) => App.Api.TestAsanaConnectionAsync(t), (t, _) => App.Api.SaveAsanaIntegrationAsync(t))); break;
        }
    }

    private async Task OpenConfig(Window dialog)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var saved = await dialog.ShowDialog<bool>(owner);
        if (saved) await LoadAsync();
    }
}
