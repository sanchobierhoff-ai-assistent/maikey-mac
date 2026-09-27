using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace mAIkey.Desktop.Controls;

/// <summary>5-delige meter (port van Controls/SegmentMeter).</summary>
public class SegmentMeter : StackPanel
{
    private static readonly IBrush Inactive = new SolidColorBrush(Color.FromArgb(0x2E, 0, 0, 0));

    public SegmentMeter(int value, IBrush color)
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        for (int i = 0; i < 5; i++)
            Children.Add(new Border
            {
                Width = 8, Height = 6, CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 0, i < 4 ? 3 : 0, 0),
                Background = i < value ? color : Inactive
            });
    }
}

/// <summary>
/// Modelkiezer zoals in de Windows-hotkey-editor: gesloten staat toont naam + drie meters
/// (snelheid, intelligentie, verbruik); de lijst is gegroepeerd per provider, met
/// kolomkoppen en per model een korte beschrijving.
/// </summary>
public class ModelPicker : UserControl
{
    private static readonly IBrush PanelBg = new SolidColorBrush(Color.Parse("#FBF5E4"));
    private static readonly IBrush PanelBorder = new SolidColorBrush(Color.Parse("#D4C29A"));
    private static readonly IBrush HeaderStrip = new SolidColorBrush(Color.Parse("#F5EBD3"));
    private static readonly IBrush GroupHeader = new SolidColorBrush(Color.Parse("#9A8A5E"));
    private static readonly IBrush DescText = new SolidColorBrush(Color.Parse("#6B5D38"));
    private static readonly IBrush SelectedBg = new SolidColorBrush(Color.Parse("#B9D9EA"));
    private static readonly IBrush HoverBg = new SolidColorBrush(Color.Parse("#22B9D9EA"));
    private static readonly IBrush NameText = new SolidColorBrush(Color.Parse("#2C2820"));
    public static readonly IBrush SpeedColor = new SolidColorBrush(Color.Parse("#F97316"));
    public static readonly IBrush QualityColor = new SolidColorBrush(Color.Parse("#8B7DD8"));
    public static readonly IBrush UsageColor = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush SpeedHeader = new SolidColorBrush(Color.Parse("#C25C0A"));
    private static readonly IBrush QualityHeader = new SolidColorBrush(Color.Parse("#5D4FB3"));
    private static readonly IBrush UsageHeader = new SolidColorBrush(Color.Parse("#1F8A3E"));

    private static readonly string[] ProviderOrder = { "Auto", "OpenAI", "Google", "Groq", "Anthropic", "Moonshot" };

    private readonly Button _button;
    private readonly Popup _popup;
    private readonly StackPanel _list = new();
    private List<AIModel> _models = new();
    private string? _selectedId;

    /// <summary>Wordt aangeroepen als de gebruiker een ander model kiest.</summary>
    public event EventHandler? SelectionChanged;

    public string? SelectedModelId
    {
        get => _selectedId;
        set
        {
            _selectedId = value;
            RenderSelected();
        }
    }

    public AIModel? SelectedModel => _models.FirstOrDefault(m => m.Id == _selectedId);

    public IReadOnlyList<AIModel> Models => _models;

    public ModelPicker()
    {
        _button = new Button
        {
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = PanelBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 0, 10, 0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _button.Click += (_, _) => _popup!.IsOpen = !_popup.IsOpen;

        var header = new Border
        {
            Background = HeaderStrip,
            BorderBrush = new SolidColorBrush(Color.Parse("#E7D8B0")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = Columns(new Panel(),
                ColHeader(L.T("Model_ColSpeed"), SpeedHeader, 10),
                ColHeader(L.T("Model_ColIntelligence"), QualityHeader, 10),
                ColHeader(L.T("Model_ColUsage"), UsageHeader, 10), new Thickness(14, 9))
        };

        var scroll = new ScrollViewer { Content = _list, MaxHeight = 500, Margin = new Thickness(0, 4, 0, 8), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(scroll);

        _popup = new Popup
        {
            PlacementTarget = _button,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Background = PanelBg, BorderBrush = PanelBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), BoxShadow = BoxShadows.Parse("0 6 24 0 #403C2810"),
                Child = dock, ClipToBounds = true
            }
        };
        _popup.Opened += (_, _) =>
        {
            if (_popup.Child is Border b) b.MinWidth = Math.Max(_button.Bounds.Width, 460);
        };

        Content = new Panel { Children = { _button, _popup } };
        RenderSelected();
    }

    public void SetModels(IEnumerable<AIModel> models)
    {
        _models = models.OrderBy(m => { var i = Array.IndexOf(ProviderOrder, m.Provider); return i < 0 ? 99 : i; }).ToList();
        if (_selectedId == null || _models.All(m => m.Id != _selectedId))
            _selectedId = _models.Any(m => m.Id == _selectedId) ? _selectedId : _selectedId; // behoud onbekende id
        RenderList();
        RenderSelected();
    }

    private void RenderSelected()
    {
        var m = SelectedModel;
        var name = new TextBlock
        {
            Text = m?.Name ?? _selectedId ?? "—",
            FontWeight = FontWeight.SemiBold, FontSize = 13, Foreground = NameText,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
        };
        Control Meter(string label, IBrush headerBrush, int value, IBrush color)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 8, FontWeight = FontWeight.Bold, Foreground = headerBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3) });
            sp.Children.Add(new SegmentMeter(value, color) { HorizontalAlignment = HorizontalAlignment.Center });
            return sp;
        }
        var grid = m == null
            ? Columns(name, new Panel(), new Panel(), new Panel(), new Thickness(0))
            : Columns(name,
                Meter(L.T("Model_ColSpeed"), SpeedHeader, m.SpeedLevel, SpeedColor),
                Meter(L.T("Model_ColIntelligence"), QualityHeader, m.QualityLevel, QualityColor),
                Meter(L.T("Model_ColUsage"), UsageHeader, m.UsageLevel, UsageColor), new Thickness(0));
        var arrow = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 L8,0 L4,5 Z"), Fill = new SolidColorBrush(Color.Parse("#7A6A45")),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        };
        var outer = new DockPanel();
        DockPanel.SetDock(arrow, Dock.Right);
        outer.Children.Add(arrow);
        outer.Children.Add(grid);
        _button.Content = outer;
    }

    private void RenderList()
    {
        _list.Children.Clear();
        foreach (var group in _models.GroupBy(m => m.Provider))
        {
            _list.Children.Add(new TextBlock { Text = group.Key, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = GroupHeader, Margin = new Thickness(14, 10, 14, 4) });
            foreach (var m in group)
            {
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                info.Children.Add(new TextBlock { Text = m.Name, FontWeight = FontWeight.SemiBold, FontSize = 13, Foreground = NameText, TextTrimming = TextTrimming.CharacterEllipsis });
                if (!string.IsNullOrEmpty(m.Description))
                    info.Children.Add(new TextBlock { Text = m.Description, FontSize = 11, Foreground = DescText, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });

                var row = new Border
                {
                    Padding = new Thickness(14, 8),
                    Background = m.Id == _selectedId ? SelectedBg : Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = Columns(info,
                        new SegmentMeter(m.SpeedLevel, SpeedColor) { HorizontalAlignment = HorizontalAlignment.Center },
                        new SegmentMeter(m.QualityLevel, QualityColor) { HorizontalAlignment = HorizontalAlignment.Center },
                        new SegmentMeter(m.UsageLevel, UsageColor) { HorizontalAlignment = HorizontalAlignment.Center }, new Thickness(0))
                };
                var model = m;
                row.PointerEntered += (_, _) => { if (model.Id != _selectedId) row.Background = HoverBg; };
                row.PointerExited += (_, _) => { if (model.Id != _selectedId) row.Background = Brushes.Transparent; };
                row.PointerReleased += (_, _) =>
                {
                    _popup.IsOpen = false;
                    if (_selectedId == model.Id) return;
                    _selectedId = model.Id;
                    RenderSelected();
                    RenderList();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                };
                _list.Children.Add(row);
            }
        }
    }

    private static Grid Columns(Control first, Control c1, Control c2, Control c3, Thickness margin)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,64,64,64"), Margin = margin };
        Grid.SetColumn(c1, 1); Grid.SetColumn(c2, 2); Grid.SetColumn(c3, 3);
        g.Children.Add(first); g.Children.Add(c1); g.Children.Add(c2); g.Children.Add(c3);
        return g;
    }

    private static TextBlock ColHeader(string text, IBrush fg, double size) =>
        new() { Text = text, FontSize = size, FontWeight = FontWeight.Bold, Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center };

    /// <summary>Standaard-modellen als de API (nog) niet bereikbaar is — gelijk aan de Windows-lijst.</summary>
    public static AIModel[] FallbackModels => new[]
    {
        new AIModel { Id = "gemini-2.5-flash-lite",   Name = "Gemini 2.5 Flash Lite", Provider = "Google",    SpeedLevel = 5, QualityLevel = 2, UsageLevel = 1, Description = "Cheapest option · 1M context" },
        new AIModel { Id = "gemini-2.5-flash",        Name = "Gemini 2.5 Flash",      Provider = "Google",    SpeedLevel = 4, QualityLevel = 3, UsageLevel = 1, Description = "Slightly smarter than Lite, same low price" },
        new AIModel { Id = "gemini-3.1-flash-lite",   Name = "Gemini 3.1 Flash-Lite", Provider = "Google",    SpeedLevel = 5, QualityLevel = 3, UsageLevel = 2, Description = "Faster and smarter than 2.5 Lite" },
        new AIModel { Id = "gemini-3.5-flash",        Name = "Gemini 3.5 Flash",      Provider = "Google",    SpeedLevel = 4, QualityLevel = 4, UsageLevel = 3, Description = "Strongest Gemini · comparable to GPT-4.1" },
        new AIModel { Id = "gpt-4o-mini",             Name = "GPT-4o Mini",           Provider = "OpenAI",    SpeedLevel = 4, QualityLevel = 2, UsageLevel = 1, Description = "Simple tasks · affordable entry model" },
        new AIModel { Id = "gpt-4.1-mini",            Name = "GPT-4.1 Mini",          Provider = "OpenAI",    SpeedLevel = 4, QualityLevel = 3, UsageLevel = 2, Description = "Smarter than 4o Mini, just as fast" },
        new AIModel { Id = "gpt-4.1",                 Name = "GPT-4.1",               Provider = "OpenAI",    SpeedLevel = 4, QualityLevel = 4, UsageLevel = 3, Description = "All-rounder · strong instruction following" },
        new AIModel { Id = "gpt-4o",                  Name = "GPT-4o",                Provider = "OpenAI",    SpeedLevel = 3, QualityLevel = 4, UsageLevel = 3, Description = "Slightly more powerful than 4.1, slightly slower" },
        new AIModel { Id = "o3-mini",                 Name = "o3 Mini",               Provider = "OpenAI",    SpeedLevel = 2, QualityLevel = 4, UsageLevel = 3, Description = "Reasons better than 4o · slower" },
        new AIModel { Id = "o3",                      Name = "o3",                    Provider = "OpenAI",    SpeedLevel = 1, QualityLevel = 5, UsageLevel = 5, Description = "Deep reasoning · most demanding tasks" },
        new AIModel { Id = "gpt-5-nano",              Name = "GPT-5 Nano",            Provider = "OpenAI",    SpeedLevel = 3, QualityLevel = 2, UsageLevel = 1, Description = "Lighter than 4o Mini · short tasks" },
        new AIModel { Id = "gpt-5-mini",              Name = "GPT-5 Mini",            Provider = "OpenAI",    SpeedLevel = 2, QualityLevel = 4, UsageLevel = 3, Description = "Strong reasoning in a mini model" },
        new AIModel { Id = "gpt-5",                   Name = "GPT-5",                 Provider = "OpenAI",    SpeedLevel = 2, QualityLevel = 5, UsageLevel = 5, Description = "Top-tier quality · heavy tasks" },
        new AIModel { Id = "gpt-5-pro",               Name = "GPT-5 Pro",             Provider = "OpenAI",    SpeedLevel = 1, QualityLevel = 5, UsageLevel = 5, Description = "Maximum quality · most expensive option" },
        new AIModel { Id = "gpt-5.6-luna",            Name = "GPT-5.6 Luna",          Provider = "OpenAI",    SpeedLevel = 4, QualityLevel = 3, UsageLevel = 1, Description = "New cheap workhorse · faster than 5 Mini" },
        new AIModel { Id = "gpt-5.6-terra",           Name = "GPT-5.6 Terra",         Provider = "OpenAI",    SpeedLevel = 3, QualityLevel = 4, UsageLevel = 3, Description = "Balanced new model · 1M context" },
        new AIModel { Id = "gpt-5.6-sol",             Name = "GPT-5.6 Sol",           Provider = "OpenAI",    SpeedLevel = 2, QualityLevel = 5, UsageLevel = 5, Description = "Top-tier · heaviest tasks" },
        new AIModel { Id = "llama-3.3-70b-versatile", Name = "Llama 3.3 70B",         Provider = "Groq",      SpeedLevel = 5, QualityLevel = 2, UsageLevel = 1, Description = "Ultra-fast via Groq · less consistent" },
        new AIModel { Id = "llama-3.1-8b-instant",    Name = "Llama 3.1 8B",          Provider = "Groq",      SpeedLevel = 5, QualityLevel = 1, UsageLevel = 1, Description = "Fastest and cheapest · short queries" },
        new AIModel { Id = "claude-haiku-4-5",        Name = "Claude Haiku 4.5",      Provider = "Anthropic", SpeedLevel = 4, QualityLevel = 3, UsageLevel = 2, Description = "Fast and reliable · strong style adherence" },
        new AIModel { Id = "claude-sonnet-5",         Name = "Claude Sonnet 5",       Provider = "Anthropic", SpeedLevel = 3, QualityLevel = 5, UsageLevel = 4, Description = "Near-Opus quality · strong at context and email" },
        new AIModel { Id = "claude-opus-4-8",         Name = "Claude Opus 4.8",       Provider = "Anthropic", SpeedLevel = 2, QualityLevel = 5, UsageLevel = 5, Description = "Most capable · heaviest analyses and long tasks" },
        new AIModel { Id = "kimi-k2.6",               Name = "Kimi K2.6",             Provider = "Moonshot",  SpeedLevel = 2, QualityLevel = 3, UsageLevel = 2, Description = "Accurate and style-consistent · Sonnet alternative" },
        new AIModel { Id = "kimi-k3",                 Name = "Kimi K3",               Provider = "Moonshot",  SpeedLevel = 2, QualityLevel = 5, UsageLevel = 4, Description = "New · multimodal reasoning model, 1M context" },
    };
}
