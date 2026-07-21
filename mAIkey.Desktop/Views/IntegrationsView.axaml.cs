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
            IntegrationsPanel.Children.Add(BuildRow(name, connected.Contains(type)));
    }

    private Control BuildRow(string name, bool isConnected)
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
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse(isConnected ? "#10B981" : "#9A9AA3"))
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        grid.Children.Add(title);
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);

        var card = new Border { Child = grid };
        card.Classes.Add("card");
        card.Padding = new Avalonia.Thickness(16, 14);
        return card;
    }
}