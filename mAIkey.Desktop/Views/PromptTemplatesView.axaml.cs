using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Views;

public partial class PromptTemplatesView : UserControl
{
    private List<RemotePromptTemplate> _all = new();

    public PromptTemplatesView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
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

    private void Render(string? query)
    {
        CategoriesPanel.Children.Clear();
        bool searching = !string.IsNullOrEmpty(query);

        IEnumerable<RemotePromptTemplate> items = _all.OrderBy(t => t.SortOrder);
        if (searching)
        {
            var q = query!.ToLowerInvariant();
            items = items.Where(t =>
                (t.Name + " " + t.Description + " " + t.Category).ToLowerInvariant().Contains(q));

            // Bij zoeken: platte lijst met tegels.
            foreach (var t in items)
                CategoriesPanel.Children.Add(BuildCard(t));
            return;
        }

        // Anders: per categorie een uitklap-sectie.
        foreach (var group in items.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Overig" : t.Category))
        {
            var inner = new StackPanel { Margin = new Avalonia.Thickness(12, 4, 0, 12) };
            foreach (var t in group)
                inner.Children.Add(BuildCard(t));

            CategoriesPanel.Children.Add(new Expander
            {
                Header = $"{IconFor(group.Key)}  {group.Key}",
                IsExpanded = false,
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                Content = inner
            });
        }
    }

    private static string IconFor(string category) => category.ToUpperInvariant() switch
    {
        "PRODUCTIVITEIT" or "PRODUCTIVITY" => "⚡",
        "COMMUNICATIE" or "COMMUNICATION" => "💬",
        "ONTWIKKELING" or "DEVELOPMENT" => "💻",
        "CREATIEF" or "CREATIVE" => "🎨",
        "ANALYSE" or "ANALYSIS" => "🔍",
        "INTEGRATIES" or "INTEGRATIONS" => "🔗",
        _ => "📁"
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

        var btn = new Button { Content = "Toevoegen", Height = 30, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12 };
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
        App.Hotkeys?.RegisterAll();

        StatusText.IsVisible = true;
        StatusText.Foreground = new SolidColorBrush(Color.Parse("#F5A524"));
        StatusText.Text = $"'{t.Name}' toegevoegd. Ga naar Hotkeys om een toets te kiezen.";
    }
}