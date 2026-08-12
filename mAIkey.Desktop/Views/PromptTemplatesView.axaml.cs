using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;
using mAIkey.Core.Services;
using Projektanker.Icons.Avalonia;

namespace mAIkey.Desktop.Views;

public partial class PromptTemplatesView : UserControl
{
    private List<RemotePromptTemplate> _all = new();

    public PromptTemplatesView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
        L.Changed += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) => L.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (_all.Count > 0) Render(SearchBox.Text?.Trim());
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var resp = await App.Api.GetPromptTemplatesAsync(App.Config.InterfaceLanguage);
            _all = resp?.Templates ?? new();

            if (_all.Count == 0)
            {
                StatusText.Text = "Geen templates gevonden.";
                return;
            }

            StatusText.IsVisible = false;
            Render(null);
        }
        catch (Exception ex)
        {
            StatusText.IsVisible = true;
            StatusText.Text = "Kon templates niet laden: " + ex.Message;
        }
    }

    private void Search_Changed(object? sender, TextChangedEventArgs e) =>
        Render(SearchBox.Text?.Trim());

    /// <summary>Thema-kleur ophalen (past zich aan donker/taupe aan).</summary>
    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private void Render(string? query)
    {
        CategoriesPanel.Children.Clear();
        bool searching = !string.IsNullOrEmpty(query);

        IEnumerable<RemotePromptTemplate> items = _all.OrderBy(t => t.SortOrder);
        if (searching)
        {
            var q = query!.ToLowerInvariant();
            var results = items.Where(t =>
                (t.Name + " " + t.Description + " " + t.Category).ToLowerInvariant().Contains(q)).ToList();

            StatusText.IsVisible = results.Count == 0;
            StatusText.Text = "Geen templates gevonden voor je zoekopdracht.";

            // Bij zoeken: platte lijst met tegels.
            var list = new StackPanel { Spacing = 10 };
            foreach (var t in results)
                list.Children.Add(BuildCard(t));
            CategoriesPanel.Children.Add(list);
            return;
        }

        StatusText.IsVisible = false;

        // Anders: per categorie een uitklapbare sectiekaart (volledige breedte).
        foreach (var group in items.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Overig" : t.Category))
            CategoriesPanel.Children.Add(BuildCategory(group.Key, group.ToList()));
    }

    private Control BuildCategory(string category, List<RemotePromptTemplate> templates)
    {
        var chevron = new Icon
        {
            Value = "mdi-chevron-down", FontSize = 18, Foreground = TB("Text3"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Height = 52
        };
        header.Children.Add(new Icon
        {
            Value = MdiFor(category), FontSize = 17, Foreground = TB("Accent"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(2, 0, 12, 0)
        });
        var title = new TextBlock
        {
            Text = category.ToUpperInvariant(), FontSize = 13, FontWeight = FontWeight.SemiBold,
            Foreground = TB("Text1"), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var count = new TextBlock
        {
            Text = $"{templates.Count}", FontSize = 12, Foreground = TB("Text3"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(count, 2);
        header.Children.Add(count);
        Grid.SetColumn(chevron, 3);
        header.Children.Add(chevron);

        var inner = new StackPanel
        {
            Spacing = 10,
            Margin = new Avalonia.Thickness(0, 4, 0, 4),
            IsVisible = false
        };
        foreach (var t in templates)
            inner.Children.Add(BuildCard(t));

        var headerButton = new Button
        {
            Content = header,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(16, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        headerButton.Click += (_, _) =>
        {
            inner.IsVisible = !inner.IsVisible;
            chevron.Value = inner.IsVisible ? "mdi-chevron-up" : "mdi-chevron-down";
        };

        var body = new StackPanel();
        body.Children.Add(headerButton);
        var innerHost = new Border { Child = inner, Padding = new Avalonia.Thickness(16, 0, 16, 0) };
        body.Children.Add(innerHost);

        var card = new Border
        {
            Child = body,
            Margin = new Avalonia.Thickness(0, 0, 0, 10),
            Padding = new Avalonia.Thickness(0)
        };
        card.Classes.Add("section");
        return card;
    }

    private static string MdiFor(string category) => category.ToUpperInvariant() switch
    {
        "PRODUCTIVITEIT" or "PRODUCTIVITY" => "mdi-lightning-bolt",
        "COMMUNICATIE" or "COMMUNICATION" => "mdi-message-text-outline",
        "ONTWIKKELING" or "DEVELOPMENT" => "mdi-code-tags",
        "CREATIEF" or "CREATIVE" => "mdi-palette-outline",
        "ANALYSE" or "ANALYSIS" => "mdi-magnify",
        "INTEGRATIES" or "INTEGRATIONS" => "mdi-link-variant",
        _ => "mdi-folder-outline"
    };

    private static string OutputLabel(string mode) => mode switch
    {
        "replace" => "Vervangen",
        "clipboard" => "Klembord",
        "window" or "prompt" => "Venster",
        _ => mode
    };

    private Control BuildCard(RemotePromptTemplate t)
    {
        var accent = new SolidColorBrush(Color.Parse("#F5A524"));

        var title = new TextBlock
        {
            Text = t.Name, FontSize = 14, FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 5)
        };

        var desc = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(t.Description) ? t.CustomPrompt : t.Description,
            FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 3,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };
        desc.Classes.Add("muted");

        // Meta: Output • Model
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Avalonia.Thickness(0, 0, 0, 10) };
        meta.Children.Add(new TextBlock { Text = "Output: ", FontSize = 11, Classes = { "dimmed" } });
        meta.Children.Add(new TextBlock { Text = OutputLabel(t.OutputMode), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = accent });
        meta.Children.Add(new TextBlock { Text = "  •  Model: ", FontSize = 11, Classes = { "dimmed" } });
        meta.Children.Add(new TextBlock { Text = t.Model, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = accent });

        var btn = new Button { Content = L.T("Templates_AddBtn"), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, Padding = new Avalonia.Thickness(16, 8) };
        btn.Classes.Add("ghost");
        btn.Click += (_, _) => UseTemplate(t);

        var stack = new StackPanel();
        stack.Children.Add(title);
        stack.Children.Add(desc);
        stack.Children.Add(meta);
        stack.Children.Add(btn);

        var card = new Border { Child = stack };
        card.Classes.Add("tile");
        return card;
    }

    private void UseTemplate(RemotePromptTemplate t)
    {
        var hk = new HotkeyConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = t.Name,
            CustomPrompt = t.CustomPrompt,
            Model = t.Model,
            OutputMode = t.OutputMode,
            PrefixLanguage = App.Config.InterfaceLanguage?.ToUpperInvariant() ?? "NL",
            Enabled = true,
            Key = 0,
            ModifierKeys = 0
        };

        App.Config.Hotkeys = App.Config.Hotkeys.Append(hk).ToArray();
        try { App.Hotkeys?.RegisterAll(); } catch { /* registratie mag toevoegen niet blokkeren */ }

        StatusText.IsVisible = true;
        StatusText.Foreground = TB("Accent");
        StatusText.Text = $"'{t.Name}' toegevoegd. Ga naar Hotkeys om een toets te kiezen.";
    }
}