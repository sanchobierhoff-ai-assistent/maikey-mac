using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Views;

public partial class PromptTemplatesView : UserControl
{
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
            var templates = resp?.Templates ?? new();

            if (templates.Count == 0)
            {
                StatusText.Text = "Geen templates gevonden.";
                return;
            }

            StatusText.IsVisible = false;
            TemplatesPanel.Children.Clear();

            // Groepeer op categorie, gesorteerd op sort_order.
            var groups = templates
                .OrderBy(t => t.SortOrder)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Algemeen" : t.Category);

            foreach (var group in groups)
            {
                TemplatesPanel.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Classes = { "eyebrow" },
                    Margin = new Avalonia.Thickness(4, 12, 0, 0)
                });

                foreach (var t in group)
                    TemplatesPanel.Children.Add(BuildCard(t));
            }
        }
        catch (Exception ex)
        {
            StatusText.IsVisible = true;
            StatusText.Text = "Kon templates niet laden: " + ex.Message;
        }
    }

    private Control BuildCard(RemotePromptTemplate t)
    {
        var title = new TextBlock { Text = t.Name, FontSize = 15, FontWeight = FontWeight.SemiBold };
        title.Classes.Add("CardTitle");

        var desc = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(t.Description) ? t.CustomPrompt : t.Description,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        desc.Classes.Add("muted");

        var useBtn = new Button { Content = "Gebruik als hotkey", HorizontalAlignment = HorizontalAlignment.Left };
        useBtn.Classes.Add("accent");
        useBtn.Click += (_, _) => UseTemplate(t);

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(title);
        stack.Children.Add(desc);
        stack.Children.Add(useBtn);

        var card = new Border { Child = stack };
        card.Classes.Add("card");
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
            Key = 0,          // gebruiker kiest de toets in de hotkey-editor
            ModifierKeys = 0
        };

        App.Config.Hotkeys = App.Config.Hotkeys.Append(hk).ToArray();
        App.Hotkeys?.RegisterAll();

        StatusText.IsVisible = true;
        StatusText.Foreground = new SolidColorBrush(Color.Parse("#F5A524"));
        StatusText.Text = $"'{t.Name}' toegevoegd als hotkey. Ga naar Hotkeys om een toets te kiezen.";
    }
}