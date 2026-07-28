using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Projektanker.Icons.Avalonia;

namespace mAIkey.Desktop.Views;

public partial class IntegrationsView : UserControl
{
    // type, naam, omschrijving, MDI-icoon, merkkleur
    private static readonly (string Type, string Name, string Desc, string Icon, string Color)[] Supported =
    {
        ("jira",      "Jira",             "Maak tickets vanuit geselecteerde tekst.", "mdi-jira",                        "#2684FF"),
        ("github",    "GitHub",           "Maak issues vanuit geselecteerde tekst.",  "mdi-github",                      "#8B949E"),
        ("slack",     "Slack",            "Stuur berichten naar je kanalen.",         "mdi-slack",                       "#E01E5A"),
        ("teams",     "Microsoft Teams",  "Stuur berichten naar Teams.",              "mdi-microsoft-teams",             "#6264A7"),
        ("trello",    "Trello",           "Maak kaarten op je borden.",               "mdi-trello",                      "#0079BF"),
        ("asana",     "Asana",            "Maak taken in Asana.",                     "mdi-checkbox-marked-circle",      "#F06A6A"),
        ("todoist",   "Todoist",          "Maak taken in Todoist.",                   "mdi-checkbox-marked-circle-outline","#E44332"),
        ("gmail",     "Gmail",            "Stel e-mails op en verstuur ze.",          "mdi-gmail",                       "#EA4335"),
        ("gcalendar", "Google Agenda",    "Maak afspraken in je agenda.",             "mdi-calendar-month",              "#4285F4"),
        ("gtasks",    "Google Taken",     "Maak taken in Google Tasks.",              "mdi-format-list-checks",          "#4285F4"),
        ("zapier",    "Zapier / Make",    "Stuur data naar je automatiseringen.",     "mdi-lightning-bolt",              "#FF4A00"),
    };

    public IntegrationsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var connected = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var integrations = await App.Api.GetIntegrationsAsync();
            if (integrations != null)
                foreach (var i in integrations.Where(i => i.IsActive))
                    connected.Add(i.IntegrationType);
            StatusText.IsVisible = false;
        }
        catch
        {
            StatusText.Text = "Verbindingsstatus kon niet laden (offline?). Koppelingen worden wel getoond.";
        }

        IntegrationsPanel.Children.Clear();
        foreach (var m in Supported)
            IntegrationsPanel.Children.Add(BuildCard(m, connected.Contains(m.Type)));
    }

    private Control BuildCard((string Type, string Name, string Desc, string Icon, string Color) m, bool isConnected)
    {
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
        if (isConnected) dot.Fill = new SolidColorBrush(Color.Parse("#10B981"));
        else { dot.Stroke = new SolidColorBrush(Color.Parse("#5E5E66")); dot.StrokeThickness = 1.5; dot.Fill = Brushes.Transparent; }

        var statusText = new TextBlock
        {
            Text = isConnected ? "Verbonden" : "Beschikbaar",
            FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse(isConnected ? "#9A9AA3" : "#5E5E66"))
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
        var desc = new TextBlock { Text = m.Desc, FontSize = 13, TextWrapping = TextWrapping.Wrap, Height = 38 };
        desc.Classes.Add("muted");

        var btn = new Button
        {
            Content = isConnected ? "Wijzigen" : "Verbinden",
            Height = 32, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, 14, 0, 0)
        };
        btn.Classes.Add(isConnected ? "ghost" : "accent");
        WireConfig(btn, m.Type);

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(name);
        stack.Children.Add(desc);
        stack.Children.Add(btn);

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
