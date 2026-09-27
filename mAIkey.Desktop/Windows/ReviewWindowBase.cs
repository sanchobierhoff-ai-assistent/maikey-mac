using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Gemeenschappelijke opmaak van de integratie-review-vensters van de Windows-app
/// (eyebrow · titel · subtitel, formuliervelden, Annuleren links en de hoofdactie rechts).
/// Subklassen voegen velden toe via de Add*-helpers en implementeren <see cref="OnPrimaryAsync"/>.
/// </summary>
public abstract class ReviewWindowBase : Window
{
    protected readonly StackPanel Form = new();
    protected readonly Button PrimaryButton;
    protected readonly Button CancelButton;
    protected readonly StackPanel ButtonsRight = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly string _primaryText;

    /// <summary>True als de actie geslaagd is (WPF: DialogResult == true).</summary>
    public bool Confirmed { get; protected set; }

    protected ReviewWindowBase(string eyebrow, string title, string subtitle, string primaryText,
                               double width = 620, double height = 560, string? windowTitle = null)
    {
        Title = windowTitle ?? title;
        Width = width;
        Height = height;
        MinHeight = Math.Min(height, 360);
        MinWidth = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));
        _primaryText = primaryText;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        header.Children.Add(Text(eyebrow, 11, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6), 0.85));
        header.Children.Add(Text(title, 20, "Text1", FontWeight.SemiBold));
        if (!string.IsNullOrEmpty(subtitle))
            header.Children.Add(Text(subtitle, 12.5, "Text3", FontWeight.Normal, new Thickness(0, 6, 0, 0)));

        CancelButton = new Button { Content = L.T("Common_Cancel"), Height = 40, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center };
        CancelButton.Classes.Add("GhostButton");
        CancelButton.Click += (_, _) => { Confirmed = false; Close(); };

        PrimaryButton = new Button { Content = primaryText, Height = 40, Padding = new Thickness(20, 0), VerticalContentAlignment = VerticalAlignment.Center };
        PrimaryButton.Classes.Add("AccentButton");
        PrimaryButton.Click += async (_, _) => await RunPrimaryAsync();
        ButtonsRight.Children.Add(PrimaryButton);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(CancelButton);
        CancelButton.HorizontalAlignment = HorizontalAlignment.Left;
        footer.Children.Add(ButtonsRight);

        var root = new DockPanel { Margin = new Thickness(28) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = Form, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Confirmed = false; Close(); } };
    }

    /// <summary>Hoofdactie (aanmaken/versturen). Retourneer true om het venster als geslaagd te sluiten.</summary>
    protected abstract Task<bool> OnPrimaryAsync();

    /// <summary>Tekst op de hoofdknop terwijl de actie loopt (bv. "Aanmaken…").</summary>
    protected virtual string BusyText => "…";

    private async Task RunPrimaryAsync()
    {
        PrimaryButton.IsEnabled = false;
        PrimaryButton.Content = BusyText;
        try
        {
            if (await OnPrimaryAsync())
            {
                Confirmed = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), ex.Message, this);
        }
        finally
        {
            PrimaryButton.IsEnabled = true;
            PrimaryButton.Content = _primaryText;
        }
    }

    // ─── Opbouw-helpers ─────────────────────────────────────────────────

    protected TextBlock Text(string text, double size, string fgKey, FontWeight weight = FontWeight.Normal,
                             Thickness? margin = null, double opacity = 1)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0), Opacity = opacity
        };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fgKey));
        return tb;
    }

    protected TextBlock AddLabel(string text, Panel? parent = null)
    {
        var tb = Text(text, 12, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6));
        (parent ?? Form).Children.Add(tb);
        return tb;
    }

    protected TextBlock AddHint(string text, Panel? parent = null, bool mono = false)
    {
        var tb = Text(text, 11, "Text3", FontWeight.Normal, new Thickness(0, -8, 0, 14));
        if (mono && this.TryFindResource("FontMono", out var f) && f is FontFamily ff) tb.FontFamily = ff;
        (parent ?? Form).Children.Add(tb);
        return tb;
    }

    protected TextBlock AddInfo(string text)
    {
        var tb = Text(text, 11.5, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 12));
        Form.Children.Add(tb);
        return tb;
    }

    protected TextBox NewTextBox(string? value, bool multiline = false, double minHeight = 0, bool mono = false)
    {
        var tb = new TextBox
        {
            Text = value ?? "",
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Padding = new Thickness(12, 10),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 14),
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        if (minHeight > 0) tb.MinHeight = minHeight;
        if (multiline) tb.MaxHeight = Math.Max(minHeight * 3, 320);
        if (mono && this.TryFindResource("FontMono", out var f) && f is FontFamily ff) tb.FontFamily = ff;
        return tb;
    }

    protected TextBox AddField(string label, string? value, bool multiline = false, double minHeight = 0, bool mono = false)
    {
        AddLabel(label);
        var tb = NewTextBox(value, multiline, minHeight, mono);
        Form.Children.Add(tb);
        return tb;
    }

    protected ComboBox AddCombo(string label, IEnumerable<ComboItem> items, Panel? parent = null)
    {
        AddLabel(label, parent);
        var cb = new ComboBox
        {
            ItemsSource = new List<ComboItem>(items),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 40,
            Margin = new Thickness(0, 0, 0, 14)
        };
        (parent ?? Form).Children.Add(cb);
        return cb;
    }

    /// <summary>Twee velden naast elkaar.</summary>
    protected (StackPanel Left, StackPanel Right) AddTwoColumns()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        var l = new StackPanel();
        var r = new StackPanel();
        Grid.SetColumn(r, 2);
        grid.Children.Add(l);
        grid.Children.Add(r);
        Form.Children.Add(grid);
        return (l, r);
    }

    protected Task Validation(string message, Control? focus = null)
    {
        focus?.Focus();
        return MkDialog.ShowError(L.T("Common_Validation"), message, this);
    }

    protected Task Info(string title, string message) => MkDialog.ShowInfo(title, message, this);
    protected Task Error(string message) => MkDialog.ShowError(L.T("Common_Error"), message, this);

    protected Task CopyToClipboard(string text) => Ui.SetClipboardTextAsync(text, this);
}

/// <summary>Item voor een ComboBox met een weergavetekst en een waarde.</summary>
public class ComboItem
{
    public string Text { get; }
    public object? Value { get; }
    public ComboItem(string text, object? value) { Text = text; Value = value; }
    public override string ToString() => Text;
}
