using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace mAIkey.Desktop.Views;

/// <summary>
/// In-venster onboarding-overlay (spotlight + uitlegballon), zoals de Windows-tour.
/// Wordt over het hoofdvenster gelegd; licht een doel-element uit met een "gat"
/// in de donkere laag en toont een kaartje met titel/uitleg + navigatie.
/// </summary>
public class TourOverlay : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly Avalonia.Controls.Shapes.Path _mask = new()
    {
        Fill = new SolidColorBrush(Color.Parse("#B0060612")),
        IsHitTestVisible = true
    };
    private readonly Border _tooltip;
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _body = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 0, 0, 18) };
    private readonly TextBlock _counter = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _prev;
    private readonly Button _next;
    private readonly Button _close;

    private Rect? _spot;

    public Action? OnNext;
    public Action? OnPrev;
    public Action? OnClose;

    public TourOverlay()
    {
        IsHitTestVisible = true;

        _close = new Button
        {
            Content = "✕", Width = 24, Height = 24, Padding = new Thickness(0), FontSize = 12,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _close.Click += (_, _) => OnClose?.Invoke();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetColumn(_counter, 1);
        Grid.SetColumn(_close, 2);
        header.Children.Add(new Panel());
        header.Children.Add(_counter);
        header.Children.Add(_close);
        _counter.Margin = new Thickness(0, 0, 8, 0);

        _prev = new Button { Content = "← Vorige", Height = 32, Padding = new Thickness(12, 0), FontSize = 12, IsVisible = false };
        _prev.Classes.Add("ghost");
        _prev.Click += (_, _) => OnPrev?.Invoke();

        _next = new Button { Content = "Volgende →", Height = 32, Padding = new Thickness(14, 0), FontSize = 12 };
        _next.Classes.Add("accent");
        _next.Click += (_, _) => OnNext?.Invoke();

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(_next, 2);
        buttons.Children.Add(_prev);
        buttons.Children.Add(_next);

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(_title);
        stack.Children.Add(_body);
        stack.Children.Add(buttons);

        _tooltip = new Border
        {
            Width = 320,
            Background = TB("Bg2"),
            BorderBrush = TB("Border1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22, 20),
            BoxShadow = BoxShadows.Parse("0 8 32 0 #66000000"),
            Child = stack
        };

        _canvas.Children.Add(_mask);
        _canvas.Children.Add(_tooltip);
        Content = _canvas;

        // Klik op de donkere laag doet niets (blokkeert de app eronder).
        _mask.PointerPressed += (_, e) => e.Handled = true;

        SizeChanged += (_, _) => Relayout();
        // Kleuren pas ophalen als de overlay in de visuele boom zit (anders zijn ze grijs/onzichtbaar).
        Loaded += (_, _) => ApplyThemeColors();
    }

    private void ApplyThemeColors()
    {
        _title.Foreground = TB("Text1");
        _body.Foreground = TB("Text2");
        _counter.Foreground = TB("Text3");
        _counter.FontFamily = TB2("FontMono") as FontFamily ?? FontFamily.Default;
        _close.Foreground = TB("Text3");
        _tooltip.Background = TB("Bg2");
        _tooltip.BorderBrush = TB("Border1");
    }

    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private object? TB2(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v)) return v;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2)) return v2;
        return null;
    }

    public void ShowStep(Rect? spotlight, string title, string body, int index, int total)
    {
        _spot = spotlight;
        _title.Text = title;
        _body.Text = body;
        _counter.Text = $"{index + 1} / {total}";
        _prev.IsVisible = index > 0;
        _next.Content = index >= total - 1 ? "Klaar" : "Volgende →";
        Relayout();
    }

    private void Relayout()
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        _canvas.Width = w;
        _canvas.Height = h;

        // Donkere laag met een "gat" op de spotlight (even-odd fill).
        var full = new RectangleGeometry(new Rect(0, 0, w, h));
        if (_spot is { } s)
        {
            var hole = new RectangleGeometry(s.Inflate(6));
            _mask.Data = new GeometryGroup { FillRule = FillRule.EvenOdd, Children = { full, hole } };
        }
        else
        {
            _mask.Data = full;
        }

        // Kaartje meten en positioneren.
        _tooltip.Measure(new Size(320, double.PositiveInfinity));
        var th = _tooltip.DesiredSize.Height;
        const double tw = 320;

        double left, top;
        if (_spot is { } sp)
        {
            left = Math.Clamp(sp.X, 16, Math.Max(16, w - tw - 16));
            // onder de spotlight indien plek, anders erboven
            if (sp.Bottom + 14 + th < h - 12) top = sp.Bottom + 14;
            else top = Math.Max(12, sp.Y - th - 14);
        }
        else
        {
            left = (w - tw) / 2;
            top = (h - th) / 2;
        }

        Canvas.SetLeft(_tooltip, left);
        Canvas.SetTop(_tooltip, top);
    }
}
