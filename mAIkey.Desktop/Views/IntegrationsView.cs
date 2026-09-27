using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;
using Projektanker.Icons.Avalonia;

namespace mAIkey.Desktop.Views;

/// <summary>
/// Integraties (port van Views/IntegrationsView): 11 vaste kaarten met merklogo, status,
/// details en Koppelen/Bewerken/Ontkoppelen, plus de datagestuurde "Meer integraties".
/// </summary>
public class IntegrationsView : UserControl
{
    private readonly ApiClient _api;
    private readonly StackPanel _loading;
    private readonly StackPanel _content;
    private readonly UniformGrid _grid = new() { Columns = 3, VerticalAlignment = VerticalAlignment.Top };
    private readonly UniformGrid _moreGrid = new() { Columns = 3, VerticalAlignment = VerticalAlignment.Top };
    private Integration[]? _integrations;

    private sealed record Def(string Type, string Name, string DescKey, string LogoColor, string LogoPath,
        string DetailIcon, Func<Integration, string> Detail, string ConfiguredKey, string DeleteConfirmKey, string DeletedKey);

    private static readonly Def[] Defs =
    {
        new("jira", "Jira", "Integrations_JiraDesc", "#0052CC",
            "M11.571 11.513H0a5.218 5.218 0 0 0 5.232 5.215h2.13v2.057A5.215 5.215 0 0 0 12.575 24V12.518a1.005 1.005 0 0 0-1.005-1.005zm5.723-5.756H5.736a5.215 5.215 0 0 0 5.215 5.214h2.129v2.058a5.218 5.218 0 0 0 5.215 5.214V6.758a1.001 1.001 0 0 0-1.001-1.001zM23.013 0H11.455a5.215 5.215 0 0 0 5.215 5.215h2.129v2.057A5.215 5.215 0 0 0 24 12.483V1.005A1.001 1.001 0 0 0 23.013 0Z",
            "mdi-web", i => string.Join("\n", new[] { i.Config?.JiraUrl, i.Config?.Email }.Where(s => !string.IsNullOrEmpty(s))),
            "Integrations_JiraConfigured", "Integrations_JiraDeleteConfirm", "Integrations_JiraDeleted"),
        new("github", "GitHub", "Integrations_GHDesc", "#24292F",
            "M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12",
            "mdi-source-repository", i => i.Config?.DefaultRepo ?? "",
            "Integrations_GitHubConfigured", "Integrations_GitHubDeleteConfirm", "Integrations_GitHubDeleted"),
        new("slack", "Slack", "Integrations_SlackDesc", "#ECB22E",
            "M6 15a2 2 0 0 1-2 2a2 2 0 0 1-2-2a2 2 0 0 1 2-2h2zm1 0a2 2 0 0 1 2-2a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2a2 2 0 0 1-2-2zm2-8a2 2 0 0 1-2-2a2 2 0 0 1 2-2a2 2 0 0 1 2 2v2zm0 1a2 2 0 0 1 2 2a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2a2 2 0 0 1 2-2zm8 2a2 2 0 0 1 2-2a2 2 0 0 1 2 2a2 2 0 0 1-2 2h-2zm-1 0a2 2 0 0 1-2 2a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2a2 2 0 0 1 2 2zm-2 8a2 2 0 0 1 2 2a2 2 0 0 1-2 2a2 2 0 0 1-2-2v-2zm0-1a2 2 0 0 1-2-2a2 2 0 0 1 2-2h5a2 2 0 0 1 2 2a2 2 0 0 1-2 2z",
            "mdi-pound", i => i.Config?.DefaultChannel ?? L.T("Integrations_WebhookConnected"),
            "Integrations_SlackConfigured", "Integrations_SlackDeleteConfirm", "Integrations_SlackDeleted"),
        new("gmail", "Gmail", "Integrations_GmailDesc", "#EA4335",
            "M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z",
            "mdi-email-outline", i => i.Config?.Email ?? "",
            "Integrations_GmailConfigured", "Integrations_GmailDeleteConfirm", "Integrations_GmailDeleted"),
        new("google_calendar", "Google Calendar", "Integrations_CalendarDesc", "#4285F4",
            "M19 3h-1V1h-2v2H8V1H6v2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V8h14v11zM9 10H7v2h2v-2zm4 0h-2v2h2v-2zm4 0h-2v2h2v-2z",
            "mdi-email-outline", i => i.Config?.Email ?? "",
            "Integrations_CalendarConfigured", "Integrations_CalendarDeleteConfirm", "Integrations_CalendarDeleted"),
        new("teams", "Microsoft Teams", "Integrations_TeamsDesc", "#6264A7",
            "M20.625 3.375H7.875C6.84 3.375 6 4.215 6 5.25v1.125H3.375C2.34 6.375 1.5 7.215 1.5 8.25v9c0 1.035.84 1.875 1.875 1.875h5.25v2.25l3-2.25h2.25c1.035 0 1.875-.84 1.875-1.875v-1.5h1.5l3 2.25v-2.25h.375c1.035 0 1.875-.84 1.875-1.875v-9c0-1.035-.84-1.875-1.875-1.875zM12.75 18H11.7l-2.325 1.744V18H3.375a.375.375 0 0 1-.375-.375v-9a.375.375 0 0 1 .375-.375H12.75a.375.375 0 0 1 .375.375v9a.375.375 0 0 1-.375.375zM21 14.625a.375.375 0 0 1-.375.375H19.5v1.744L17.175 15H14.625v-6.75a1.875 1.875 0 0 0-1.875-1.875H7.5V5.25a.375.375 0 0 1 .375-.375h12.75a.375.375 0 0 1 .375.375v9.375z",
            "mdi-pound", i => i.Config?.DefaultChannel ?? L.T("Integrations_WebhookConnected"),
            "Integrations_TeamsConfigured", "Integrations_TeamsDeleteConfirm", "Integrations_TeamsDeleted"),
        new("zapier", "Zapier / Make", "Integrations_ZapierDesc", "#FF4A00",
            "M12 2L4 6.5v5c0 4.9 3.4 9.5 8 10.5 4.6-1 8-5.6 8-10.5v-5L12 2zm0 2.2l6 3.4v4.9c0 3.8-2.6 7.4-6 8.3-3.4-.9-6-4.5-6-8.3V7.6l6-3.4zm-1 5.3v2h2v-2h-2zm0 3.5v5h2v-5h-2z",
            "mdi-webhook", i => i.Config?.WebhookName ?? L.T("Integrations_WebhookConnected"),
            "Integrations_ZapierConfigured", "Integrations_ZapierDeleteConfirm", "Integrations_ZapierDeleted"),
        new("google_tasks", "Google Tasks", "Integrations_GTasksDesc", "#4285F4",
            "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1.46 14.54l-4.04-4.04 1.41-1.41 2.63 2.63 5.54-5.54 1.41 1.41-6.95 6.95z",
            "mdi-format-list-checks", i => i.Config?.DefaultTaskListName ?? i.Config?.Email ?? L.T("Integrations_WebhookConnected"),
            "Integrations_GTasksConfigured", "Integrations_GTasksDeleteConfirm", "Integrations_GTasksDeleted"),
        new("todoist", "Todoist", "Integrations_TodoistDesc", "#E44332",
            "M19.5 3h-15A1.5 1.5 0 0 0 3 4.5v15A1.5 1.5 0 0 0 4.5 21h15a1.5 1.5 0 0 0 1.5-1.5v-15A1.5 1.5 0 0 0 19.5 3zm-9.24 13.06l-4.02-2.32.9-1.56 3.12 1.8 6.72-3.88.9 1.56-7.62 4.4zm0-4.25L6.24 9.49l.9-1.56 3.12 1.8 6.72-3.88.9 1.56-7.62 4.4z",
            "mdi-folder-outline", i => i.Config?.DefaultProjectName ?? "Inbox",
            "Integrations_TodoistConfigured", "Integrations_TodoistDeleteConfirm", "Integrations_TodoistDeleted"),
        new("trello", "Trello", "Integrations_TrelloDesc", "#0079BF",
            "M19.5 3h-15A1.5 1.5 0 0 0 3 4.5v15A1.5 1.5 0 0 0 4.5 21h15a1.5 1.5 0 0 0 1.5-1.5v-15A1.5 1.5 0 0 0 19.5 3zm-9.06 12.66a.72.72 0 0 1-.72.72H6.34a.72.72 0 0 1-.72-.72V6.34a.72.72 0 0 1 .72-.72h3.38a.72.72 0 0 1 .72.72zm7.92-4.5a.72.72 0 0 1-.72.72h-3.38a.72.72 0 0 1-.72-.72V6.34a.72.72 0 0 1 .72-.72h3.38a.72.72 0 0 1 .72.72z",
            "mdi-view-column", i => i.Config?.DefaultBoardName is { } b && i.Config?.DefaultListName is { } l ? $"{b} → {l}" : i.Config?.DefaultBoardName ?? L.T("Integrations_WebhookConnected"),
            "Integrations_TrelloConfigured", "Integrations_TrelloDeleteConfirm", "Integrations_TrelloDeleted"),
        new("asana", "Asana", "Integrations_AsanaDesc", "#F06A6A",
            "M18.78 12.65c-1.78 0-3.22 1.44-3.22 3.22s1.44 3.22 3.22 3.22S22 17.65 22 15.87s-1.44-3.22-3.22-3.22zm-13.56 0C3.44 12.65 2 14.09 2 15.87s1.44 3.22 3.22 3.22 3.22-1.44 3.22-3.22-1.44-3.22-3.22-3.22zM15.22 7.91c0 1.78-1.44 3.22-3.22 3.22S8.78 9.69 8.78 7.91 10.22 4.69 12 4.69s3.22 1.44 3.22 3.22z",
            "mdi-folder-outline", i => i.Config?.DefaultProjectName ?? L.T("Integrations_WebhookConnected"),
            "Integrations_AsanaConfigured", "Integrations_AsanaDeleteConfirm", "Integrations_AsanaDeleted"),
    };

    public IntegrationsView() : this(App.Api) { }

    public IntegrationsView(ApiClient api)
    {
        _api = api;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));

        // Kop
        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 28) };
        var titles = new StackPanel();
        titles.Children.Add(Label(L.T("Integrations_Eyebrow"), 11, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6)));
        titles.Children.Add(Label(L.T("Integrations_Title"), 28, "Text1", FontWeight.SemiBold));
        titles.Children.Add(Label(L.T("Integrations_Subtitle"), 13, "Text3", FontWeight.Normal, new Thickness(0, 4, 0, 0)));
        DockPanel.SetDock(titles, Dock.Left);
        var request = new Button { Padding = new Thickness(14, 8), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Bottom };
        request.Classes.Add("GhostButton");
        var rq = new StackPanel { Orientation = Orientation.Horizontal };
        rq.Children.Add(new Icon { Value = "mdi-plus", FontSize = 13, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
        rq.Children.Add(new TextBlock { Text = L.T("Integrations_RequestBtn"), VerticalAlignment = VerticalAlignment.Center });
        request.Content = rq;
        request.Click += async (_, _) => await MkDialog.ShowInfo(L.T("Integrations_RequestTitle"), L.T("Integrations_RequestDesc"), Owner);
        DockPanel.SetDock(request, Dock.Right);
        header.Children.Add(titles);
        header.Children.Add(request);

        _loading = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0), IsVisible = false };
        _loading.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 120, Margin = new Thickness(0, 0, 0, 12) });
        _loading.Children.Add(Label(L.T("Integrations_Loading"), 13, "Text3", FontWeight.Normal));

        _content = new StackPanel();
        _content.Children.Add(_grid);
        _content.Children.Add(Label(L.T("Integrations_MoreSection"), 18, "Text1", FontWeight.SemiBold, new Thickness(2, 18, 0, 4)));
        _content.Children.Add(Label(L.T("Integrations_MoreSectionSub"), 12.5, "Text3", FontWeight.Normal, new Thickness(2, 0, 0, 14)));
        _content.Children.Add(_moreGrid);

        var root = new StackPanel { Margin = new Thickness(32, 28, 32, 40) };
        root.Children.Add(header);
        root.Children.Add(_loading);
        root.Children.Add(_content);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        Render();
        AttachedToVisualTree += async (_, _) => await LoadIntegrationsAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private TextBlock Label(string text, double size, string fg, FontWeight weight, Thickness? margin = null)
    {
        var tb = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fg));
        return tb;
    }

    private async Task LoadIntegrationsAsync()
    {
        try
        {
            _loading.IsVisible = true;
            _content.IsVisible = false;

            var status = await _api.GetSubscriptionStatusAsync();
            if (status.Success && !status.Integrations)
            {
                if (await MkDialog.ShowUpgrade(L.T("Integrations_UpgradeTitle"), L.T("Integrations_UpgradeDesc"), Owner))
                    Ui.Main?.OpenPricingPage();
                return;
            }
            _integrations = await _api.GetIntegrationsAsync();
        }
        catch (ApiException ex)
        {
            string msg = ex.Details?.Contains("table_missing") == true ? L.T("Integrations_DbNotConfigured")
                : ex.Details?.Contains("integrations_blocked") == true ? L.T("Integrations_PlusRequired")
                : $"{L.T("Integrations_TempUnavailable")}\n{ex.Message}";
            await MkDialog.ShowError(L.T("Integrations_ErrorTitle"), msg, Owner);
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Error_General_Title"), $"{L.T("Integrations_LoadError")}\n{ex.Message}", Owner);
        }
        finally
        {
            _loading.IsVisible = false;
            _content.IsVisible = true;
            Render();
        }
    }

    private Integration? Active(string type) => _integrations?.FirstOrDefault(i => i.IntegrationType == type && i.IsActive);

    // ═══ Kaarten ═══

    private void Render()
    {
        _grid.Children.Clear();
        foreach (var def in Defs) _grid.Children.Add(BuildCard(def));

        _moreGrid.Children.Clear();
        foreach (var desc in IntegrationCatalog.All) _moreGrid.Children.Add(BuildGenericCard(desc));
    }

    private Control StatusRow(bool connected)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
        var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        if (connected) dot.Bind(Shape.FillProperty, this.GetResourceObservable("Success"));
        else { dot.Bind(Shape.StrokeProperty, this.GetResourceObservable("Text3")); dot.StrokeThickness = 1.5; }
        row.Children.Add(dot);
        row.Children.Add(Label(connected ? L.T("Integrations_Connected") : L.T("Integrations_Available"), 12, connected ? "Text2" : "Text3", FontWeight.Medium));
        return row;
    }

    private Border CardShell(Control logo, Control status, string name, string desc, Control? details, IEnumerable<Button> left, Button right)
    {
        var head = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(logo, Dock.Left);
        DockPanel.SetDock(status, Dock.Right);
        head.Children.Add(logo);
        head.Children.Add(status);

        var text = new StackPanel();
        text.Children.Add(Label(name, 16, "Text1", FontWeight.SemiBold, new Thickness(0, 0, 0, 4)));
        text.Children.Add(Label(desc, 13, "Text2", FontWeight.Normal));

        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 16, 0, 0) };
        DockPanel.SetDock(right, Dock.Right);
        footer.Children.Add(right);
        foreach (var b in left)
        {
            DockPanel.SetDock(b, Dock.Left);
            b.Margin = new Thickness(0, 0, 8, 0);
            footer.Children.Add(b);
        }

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(22, 22, 22, 18) };
        Grid.SetRow(text, 1);
        grid.Children.Add(head);
        grid.Children.Add(text);
        if (details != null) { Grid.SetRow(details, 2); grid.Children.Add(details); }
        Grid.SetRow(footer, 4);
        grid.Children.Add(footer);

        var card = new Border
        {
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 20, 20),
            MinHeight = 230, BoxShadow = BoxShadows.Parse("0 3 14 0 #123A2A10"), Child = grid
        };
        card.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg2"));
        card.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));
        return card;
    }

    private Button Btn(string text, bool primary, Func<Task> onClick)
    {
        var b = new Button { Content = text, Height = 34, Padding = new Thickness(primary ? 16 : 14, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 12.5 };
        b.Classes.Add(primary ? "AccentButton" : "GhostButton");
        b.Click += async (_, _) => await onClick();
        return b;
    }

    private Border BuildCard(Def def)
    {
        var integ = Active(def.Type);
        bool connected = integ != null;

        var logo = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(13), Background = Brushes.White, BorderThickness = new Thickness(1),
            Child = new Viewbox
            {
                Width = 26, Height = 26,
                Child = new Canvas { Width = 24, Height = 24, Children = { new Avalonia.Controls.Shapes.Path { Fill = new SolidColorBrush(Color.Parse(def.LogoColor)), Data = Geometry.Parse(def.LogoPath) } } }
            }
        };
        logo.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));

        Control? details = null;
        if (connected)
        {
            var lines = def.Detail(integ!).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            for (int i = 0; i < lines.Length; i++)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var ic = new Icon { Value = def.Type == "jira" && i == 1 ? "mdi-email-outline" : def.DetailIcon, FontSize = 13, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                ic.Bind(Icon.ForegroundProperty, this.GetResourceObservable("Text3"));
                DockPanel.SetDock(ic, Dock.Left);
                var tb = Label(lines[i], 12.5, "Text2", FontWeight.Normal);
                tb.TextWrapping = TextWrapping.NoWrap;
                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                row.Children.Add(ic);
                row.Children.Add(tb);
                panel.Children.Add(row);
            }
            details = panel;
        }

        var left = new List<Button>();
        if (connected) left.Add(Btn(L.T("Integrations_Disconnect"), false, () => DeleteAsync(def, integ!)));
        if (def.Type == "jira" && connected)
            left.Add(Btn(L.T("Integrations_JiraSearchEdit"), false, async () => await new JiraSearchEditWindow(_api).ShowModalAsync(Owner)));
        var action = Btn(connected ? L.T("Common_Edit") : L.T("Integrations_Connect"), true, () => ConfigureAsync(def, integ));

        return CardShell(logo, StatusRow(connected), def.Name, L.T(def.DescKey), details, left, action);
    }

    private Border BuildGenericCard(IntegrationDescriptor desc)
    {
        var integ = Active(desc.Type);
        bool connected = integ != null;
        var logo = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(13),
            Background = new SolidColorBrush(Color.Parse(desc.LogoColor)),
            Child = new TextBlock { Text = desc.LogoGlyph, Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        var status = Label(connected ? L.T("Integrations_Connected") : L.T("Integrations_Available"), 11.5, connected ? "Accent" : "Text3", FontWeight.SemiBold);
        status.VerticalAlignment = VerticalAlignment.Center;

        var left = new List<Button>();
        if (connected)
            left.Add(Btn(L.T("Integrations_Disconnect"), false, async () =>
            {
                if (!await MkDialog.ShowConfirm(L.T("Integrations_Disconnect"), L.Tf("Integrations_DisconnectConfirm", desc.Name), Owner)) return;
                try
                {
                    await _api.DeleteIntegrationAsync(integ!.Id);
                    await LoadIntegrationsAsync();
                }
                catch (Exception ex) { await MkDialog.ShowError(L.T("Error_General_Title"), $"{L.T("Integrations_DeleteError")}\n{ex.Message}", Owner); }
            }));
        var action = Btn(connected ? L.T("Common_Edit") : L.T("Integrations_Connect"), true, async () =>
        {
            var win = new GenericTokenConfigWindow(_api, desc);
            await win.ShowModalAsync(Owner);
            if (win.Confirmed)
            {
                await LoadIntegrationsAsync();
                await MkDialog.ShowInfo(L.T("Common_Success"), L.Tf("Integrations_GenericConfigured", desc.Name), Owner);
            }
        });

        var card = CardShell(logo, status, desc.Name, desc.Description, null, left, action);
        card.MinHeight = 200;
        card.CornerRadius = new CornerRadius(16);
        return card;
    }

    // ═══ Acties ═══

    private async Task<bool> CheckTierAsync()
    {
        try
        {
            var status = await _api.GetSubscriptionStatusAsync();
            if (status.Success && !status.Integrations)
            {
                if (await MkDialog.ShowUpgrade(L.T("Integrations_UpgradeTitle"), L.T("Integrations_UpgradeDesc"), Owner))
                    Ui.Main?.OpenPricingPage();
                return false;
            }
        }
        catch { }
        return true;
    }

    private async Task ConfigureAsync(Def def, Integration? integ)
    {
        if (!await CheckTierAsync()) return;

        // Google-integraties: niet verbonden → direct OAuth; verbonden → instellingen.
        if (def.Type is "gmail" or "google_calendar" or "google_tasks" && integ == null)
        {
            await StartGoogleOAuthDirectAsync(def);
            return;
        }

        IConfigWindow window = def.Type switch
        {
            "jira" => new JiraConfigWindow(_api, integ),
            "github" => new GitHubConfigWindow(_api, integ),
            "slack" => new SlackConfigWindow(_api, integ),
            "teams" => new TeamsConfigWindow(_api, integ),
            "zapier" => new ZapierConfigWindow(_api, integ),
            "todoist" => new TodoistConfigWindow(_api, integ),
            "trello" => new TrelloConfigWindow(_api, integ),
            "asana" => new AsanaConfigWindow(_api, integ),
            "gmail" => new GmailConfigWindow(_api, integ),
            "google_calendar" => new CalendarConfigWindow(_api, integ),
            _ => new GoogleTasksConfigWindow(_api, integ),
        };
        await ((Window)window).ShowModalAsync(Owner);
        if (window.Confirmed)
        {
            await LoadIntegrationsAsync();
            await MkDialog.ShowInfo(L.T("Common_Success"), L.T(def.ConfiguredKey), Owner);
        }
    }

    private async Task DeleteAsync(Def def, Integration integ)
    {
        if (!await MkDialog.ShowConfirm(L.T("Integrations_ConfirmDeleteTitle"), L.T(def.DeleteConfirmKey), Owner)) return;
        try
        {
            if (await _api.DeleteIntegrationAsync(integ.Id))
            {
                await MkDialog.ShowInfo(L.T("Common_Success"), L.T(def.DeletedKey), Owner);
                await LoadIntegrationsAsync();
            }
            else await MkDialog.ShowError(L.T("Error_General_Title"), L.T("Integrations_DeleteFailed"), Owner);
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Error_General_Title"), $"{L.T("Integrations_DeleteError")}\n{ex.Message}", Owner);
        }
    }

    /// <summary>Google OAuth zonder tussenvenster: browser openen en elke 2 s de status pollen.</summary>
    private async Task StartGoogleOAuthDirectAsync(Def def)
    {
        try
        {
            var url = await _api.StartGoogleOAuthAsync(def.Type);
            if (string.IsNullOrEmpty(url))
            {
                await MkDialog.ShowError(L.T("Common_Error"), L.T("GmailConfig_AuthUrlError"), Owner);
                return;
            }
            Ui.OpenUrl(url);

            var started = DateTime.UtcNow;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += async (_, _) =>
            {
                if ((DateTime.UtcNow - started).TotalMinutes > 10) { timer.Stop(); return; }
                try
                {
                    var (ok, _) = await _api.GetGoogleOAuthStatusAsync(def.Type);
                    if (!ok) return;
                    timer.Stop();
                    if (def.Type == "gmail") await _api.SaveGmailConfigAsync(null, null);
                    else if (def.Type == "google_tasks") await _api.SaveGoogleTasksConfigAsync(null, null);
                    else await _api.SaveCalendarConfigAsync(null, null, null);
                    await LoadIntegrationsAsync();
                    await MkDialog.ShowInfo(L.T("Common_Success"), L.T(def.ConfiguredKey), Owner);
                    App.ShowMainWindow();
                }
                catch { /* blijven pollen */ }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), ex.Message, Owner);
        }
    }
}
