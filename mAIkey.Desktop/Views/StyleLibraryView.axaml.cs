using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Views;

public partial class StyleLibraryView : UserControl
{
    private string? _editingId;
    private List<string> _examples = new();
    private Border? _selectedRow;

    public StyleLibraryView()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateStyles();
    }

    /// <summary>Thema-kleur ophalen (past zich aan donker/taupe aan).</summary>
    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    // ═══ LIJST ═══

    private void PopulateStyles()
    {
        StylesListPanel.Children.Clear();
        _selectedRow = null;
        var styles = App.Config.WritingStyles;

        if (styles.Length == 0)
        {
            StylesListPanel.Children.Add(new TextBlock
            {
                Text = "Nog geen stijlen aangemaakt.",
                FontSize = 12,
                Foreground = TB("Text3"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 16, 20, 0)
            });
            return;
        }

        foreach (var s in styles)
            StylesListPanel.Children.Add(BuildListRow(s));
    }

    private Border BuildListRow(WritingStyle s)
    {
        var name = new TextBlock
        {
            Text = s.Name, FontWeight = FontWeight.SemiBold, FontSize = 13,
            Foreground = TB("Text1"), TextTrimming = TextTrimming.CharacterEllipsis
        };
        var descText = !string.IsNullOrWhiteSpace(s.UsageContext) ? s.UsageContext
                     : !string.IsNullOrWhiteSpace(s.StyleProfile) ? s.StyleProfile
                     : "(geen omschrijving)";
        var desc = new TextBlock
        {
            Text = descText!.Replace("\n", " ").Trim(), FontSize = 11.5,
            Foreground = TB("Text3"), TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 0)
        };

        var row = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Padding = new Thickness(13, 10, 16, 10),
            MinHeight = 52,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = s.Id,
            Child = new StackPanel { Children = { name, desc } }
        };

        row.PointerEntered += (_, _) => { if (row != _selectedRow) row.Background = TB("Bg2"); };
        row.PointerExited += (_, _) => { if (row != _selectedRow) row.Background = Brushes.Transparent; };
        row.PointerReleased += (_, _) => SelectStyle(s, row);
        return row;
    }

    private void HighlightRow(Border? row)
    {
        if (_selectedRow != null)
        {
            _selectedRow.Background = Brushes.Transparent;
            _selectedRow.BorderBrush = Brushes.Transparent;
        }
        _selectedRow = row;
        if (row != null)
        {
            row.Background = TB("AccentSoft");
            row.BorderBrush = TB("Accent");
        }
    }

    private Border? FindRow(string id) =>
        StylesListPanel.Children.OfType<Border>().FirstOrDefault(b => (b.Tag as string) == id);

    // ═══ EDITOR ═══

    /// <summary>Voor de rondleiding: open de nieuwe-stijl-editor als die nog niet open is.</summary>
    public void TourStartNew()
    {
        if (!EditorPanel.IsVisible) NewStyle_Click(null, null!);
    }

    private void NewStyle_Click(object? sender, RoutedEventArgs e)
    {
        _editingId = null;
        _examples = new List<string>();
        NameBox.Text = "";
        UsageBox.Text = "";
        ProfileBox.Text = "";
        EditorTitle.Text = "Nieuwe stijl";
        DeleteStyleBtn.IsVisible = false;
        StatusText.IsVisible = false;
        RenderExamples();
        ShowEditor();
        HighlightRow(null);
    }

    private void SelectStyle(WritingStyle s, Border row)
    {
        _editingId = s.Id;
        _examples = s.TextExamples?.ToList() ?? new List<string>();
        NameBox.Text = s.Name;
        UsageBox.Text = s.UsageContext ?? "";
        ProfileBox.Text = s.StyleProfile ?? "";
        EditorTitle.Text = s.Name;
        DeleteStyleBtn.IsVisible = true;
        StatusText.IsVisible = false;
        RenderExamples();
        ShowEditor();
        HighlightRow(row);
    }

    private void ShowEditor()
    {
        RightPlaceholder.IsVisible = false;
        EditorPanel.IsVisible = true;
    }

    private void RenderExamples()
    {
        ExamplesPanel.Children.Clear();
        ExamplesEmpty.IsVisible = _examples.Count == 0;

        for (int i = 0; i < _examples.Count; i++)
        {
            int index = i;
            var preview = new TextBlock
            {
                Text = _examples[i], FontSize = 12.5, Foreground = TB("Text1"),
                TextWrapping = TextWrapping.Wrap, MaxLines = 3,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 32, 0)
            };
            var remove = new Button
            {
                Content = "✕", FontSize = 13, Padding = new Thickness(4),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = TB("Text3"), Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
            };
            remove.Click += (_, _) => { _examples.RemoveAt(index); RenderExamples(); };

            var grid = new Grid();
            grid.Children.Add(preview);
            grid.Children.Add(remove);

            ExamplesPanel.Children.Add(new Border
            {
                Background = TB("Bg3"), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12), Child = grid
            });
        }
    }

    private async void AddExample_Click(object? sender, RoutedEventArgs e)
    {
        var text = await Windows.InputPromptWindow.PromptAsync("Plak een voorbeeldtekst die jij zelf hebt geschreven:");
        if (string.IsNullOrWhiteSpace(text)) return;
        _examples.Add(text.Trim());
        RenderExamples();
    }

    private async void Generate_Click(object? sender, RoutedEventArgs e)
    {
        if (_examples.Count == 0)
        {
            Status("Voeg eerst één of meer voorbeeldteksten toe.", error: true);
            return;
        }

        GenerateBtn.IsEnabled = false;
        Status("Stijlprofiel genereren…");
        try
        {
            var resp = await App.Api.GenerateStyleProfileAsync(_examples.ToArray());
            var profile = resp?.StyleProfile;
            if (string.IsNullOrWhiteSpace(profile))
            {
                Status("Kon geen stijlprofiel genereren. Probeer meer/langere voorbeelden.", error: true);
                return;
            }
            ProfileBox.Text = profile;
            Status("Stijlprofiel gegenereerd. Vergeet niet op te slaan.");
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

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Status("Geef de stijl een naam.", error: true);
            return;
        }

        if (_editingId == null)
        {
            var style = new WritingStyle
            {
                Name = name,
                UsageContext = UsageBox.Text?.Trim(),
                StyleProfile = ProfileBox.Text?.Trim(),
                TextExamples = _examples.ToArray()
            };
            App.Config.AddWritingStyle(style);
            _editingId = style.Id;
        }
        else
        {
            var style = App.Config.GetStyleById(_editingId) ?? new WritingStyle { Id = _editingId };
            style.Name = name;
            style.UsageContext = UsageBox.Text?.Trim();
            style.StyleProfile = ProfileBox.Text?.Trim();
            style.TextExamples = _examples.ToArray();
            App.Config.UpdateWritingStyle(style);
        }

        EditorTitle.Text = name;
        DeleteStyleBtn.IsVisible = true;
        PopulateStyles();
        HighlightRow(FindRow(_editingId!));
        Status("Opgeslagen.");
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _editingId = null;
        EditorPanel.IsVisible = false;
        RightPlaceholder.IsVisible = true;
        HighlightRow(null);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_editingId == null) return;
        App.Config.DeleteWritingStyle(_editingId);
        _editingId = null;
        EditorPanel.IsVisible = false;
        RightPlaceholder.IsVisible = true;
        PopulateStyles();
    }

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = error ? TB("Error") : TB("Accent");
    }
}
