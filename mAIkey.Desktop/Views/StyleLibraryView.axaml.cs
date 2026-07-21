using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Views;

public partial class StyleLibraryView : UserControl
{
    public StyleLibraryView()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateStyles();
    }

    private void PopulateStyles()
    {
        StylesPanel.Children.Clear();
        var styles = App.Config.WritingStyles;

        if (styles.Length == 0)
        {
            StylesPanel.Children.Add(new TextBlock
            {
                Text = "Nog geen stijlen. Train hierboven je eerste stijl.",
                Classes = { "muted" }
            });
            return;
        }

        foreach (var s in styles)
            StylesPanel.Children.Add(BuildCard(s));
    }

    private Control BuildCard(WritingStyle s)
    {
        var title = new TextBlock { Text = s.Name };
        title.Classes.Add("CardTitle");

        var snippet = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(s.StyleProfile) ? "(geen profiel)" : s.StyleProfile,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        snippet.Classes.Add("muted");

        var delBtn = new Button { Content = "Verwijderen", HorizontalAlignment = HorizontalAlignment.Left };
        delBtn.Classes.Add("danger");
        delBtn.Click += (_, _) =>
        {
            App.Config.DeleteWritingStyle(s.Id);
            PopulateStyles();
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(title);
        stack.Children.Add(snippet);
        stack.Children.Add(delBtn);

        var card = new Border { Child = stack };
        card.Classes.Add("card");
        return card;
    }

    private async void Generate_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        var raw = ExamplesBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(raw))
        {
            Status("Vul een naam en minstens één voorbeeldtekst in.", error: true);
            return;
        }

        // Splits op lege regels tot losse voorbeelden.
        var examples = raw.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(x => x.Trim())
                          .Where(x => x.Length > 0)
                          .ToArray();
        if (examples.Length == 0)
            examples = new[] { raw };

        GenerateBtn.IsEnabled = false;
        Status("Stijlprofiel genereren…");

        try
        {
            var resp = await App.Api.GenerateStyleProfileAsync(examples);
            var profile = resp?.StyleProfile;
            if (string.IsNullOrWhiteSpace(profile))
            {
                Status("Kon geen stijlprofiel genereren. Probeer meer/langere voorbeelden.", error: true);
                return;
            }

            App.Config.AddWritingStyle(new WritingStyle
            {
                Name = name,
                StyleProfile = profile,
                TextExamples = examples
            });

            NameBox.Text = "";
            ExamplesBox.Text = "";
            Status($"Stijl '{name}' opgeslagen.");
            PopulateStyles();
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
        }
        finally
        {
            GenerateBtn.IsEnabled = true;
        }
    }

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(error ? "#EF4444" : "#F5A524"));
    }
}