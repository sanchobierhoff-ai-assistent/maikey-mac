using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace mAIkey.Desktop.Views;

public partial class IntegrationsView : UserControl
{
    // Ondersteunde integraties (type -> weergavenaam), zoals de Windows-versie.
    private static readonly (string Type, string Name)[] Supported =
    {
        ("jira", "Jira"),
        ("github", "GitHub"),
        ("slack", "Slack"),
        ("teams", "Microsoft Teams"),
        ("trello", "Trello"),
        ("asana", "Asana"),
        ("todoist", "Todoist"),
        ("gmail", "Gmail"),
        ("gcalendar", "Google Calendar"),
        ("gtasks", "Google Tasks"),
        ("zapier", "Zapier / Make"),
    };

    public IntegrationsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        System.Collections.Generic.HashSet<string> connected = new(StringComparer.OrdinalIgnoreCase);
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
            StatusText.Text = "Kon verbindingsstatus niet laden (offline?). Integraties worden wel getoond.";
        }

        IntegrationsPanel.Children.Clear();
        foreach (var (type, name) in Supported)
            IntegrationsPanel.Children.Add(BuildRow(type, name, connected.Contains(type)));
    }

    private Control BuildRow(string type, string name, bool isConnected)
    {
        var title = new TextBlock
        {
            Text = name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var status = new TextBlock
        {
            Text = isConnected ? "Verbonden" : "Niet verbonden",
            FontSize = 12,
            Margin = new Avalonia.Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse(isConnected ? "#10B981" : "#9A9AA3"))
        };

        var configBtn = new Button
        {
            Content = isConnected ? "Wijzigen" : "Configureren",
            VerticalAlignment = VerticalAlignment.Center
        };
        configBtn.Classes.Add("ghost");

        // Geport: Jira en GitHub. De rest volgt.
        if (type == "jira")
            configBtn.Click += async (_, _) => await OpenConfig(new Windows.JiraConfigWindow());
        else if (type == "github")
            configBtn.Click += async (_, _) => await OpenConfig(new Windows.GitHubConfigWindow());
        else
        {
            configBtn.IsEnabled = false;
            configBtn.Content = "Binnenkort";
        }

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(status);
        right.Children.Add(configBtn);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        var card = new Border { Child = grid };
        card.Classes.Add("tile");
        return card;
    }

    private async System.Threading.Tasks.Task OpenConfig(Window dialog)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var saved = await dialog.ShowDialog<bool>(owner);
        if (saved)
            await LoadAsync(); // status verversen
    }
}