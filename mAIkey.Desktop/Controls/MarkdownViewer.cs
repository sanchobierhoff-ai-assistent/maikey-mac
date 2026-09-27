using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MTable = Markdig.Extensions.Tables.Table;
using MTableRow = Markdig.Extensions.Tables.TableRow;
using MTableCell = Markdig.Extensions.Tables.TableCell;
using MBlock = Markdig.Syntax.Block;
using AInline = Avalonia.Controls.Documents.Inline;

namespace mAIkey.Desktop.Controls;

/// <summary>
/// Rendert Markdown (koppen, lijsten, tabellen, code, citaten, links, vet/cursief) met
/// Avalonia-controls in het mAIkey-thema. Port van Controls/MarkdownViewer.cs (WPF).
/// Tekst is selecteerbaar zodat je stukken kunt kopiëren.
/// </summary>
public class MarkdownViewer : ContentControl
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public double BaseFontSize { get; set; } = 13;

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((v, _) => v.Rebuild());
    }

    private void Rebuild()
    {
        try
        {
            var panel = new StackPanel();
            var doc = Markdig.Markdown.Parse(Markdown ?? "", Pipeline);
            foreach (var block in doc)
            {
                var el = RenderBlock(block);
                if (el != null) panel.Children.Add(el);
            }
            Content = panel;
        }
        catch
        {
            Content = Themed(new SelectableTextBlock { Text = Markdown ?? "", TextWrapping = TextWrapping.Wrap }, "Text1");
        }
    }

    private T Themed<T>(T control, string fgKey) where T : TextBlock
    {
        control.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fgKey));
        return control;
    }

    private void BindRes(AvaloniaObject target, AvaloniaProperty prop, string key) =>
        target.Bind(prop, this.GetResourceObservable(key));

    private FontFamily MonoFont() =>
        this.TryFindResource("FontMono", out var f) && f is FontFamily ff ? ff : new FontFamily("Menlo");

    // ---- Block-niveau ----------------------------------------------------------------

    private Control? RenderBlock(MBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                var tb = Para(h.Inline);
                tb.FontWeight = FontWeight.Bold;
                tb.FontSize = h.Level <= 1 ? BaseFontSize + 4 : h.Level == 2 ? BaseFontSize + 2.5 : BaseFontSize + 1;
                tb.Margin = new Thickness(0, 8, 0, 4);
                return tb;
            }
            case ParagraphBlock p:
            {
                var tb = Para(p.Inline);
                tb.Margin = new Thickness(0, 2, 0, 6);
                return tb;
            }
            case ListBlock list: return RenderList(list);
            case QuoteBlock quote: return RenderQuote(quote);
            case FencedCodeBlock fenced: return RenderCode(GetCodeText(fenced));
            case CodeBlock code: return RenderCode(GetCodeText(code));
            case MTable table: return RenderTable(table);
            case ThematicBreakBlock:
            {
                var sep = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 8) };
                BindRes(sep, Border.BackgroundProperty, "Border1");
                return sep;
            }
            default: return null;
        }
    }

    private SelectableTextBlock Para(ContainerInline? inline)
    {
        var tb = Themed(new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = BaseFontSize }, "Text1");
        tb.Inlines = new InlineCollection();
        if (inline != null)
            foreach (var run in BuildInlines(inline))
                tb.Inlines.Add(run);
        return tb;
    }

    private Control RenderList(ListBlock list)
    {
        var sp = new StackPanel { Margin = new Thickness(4, 2, 0, 6) };
        int index = 1;
        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start)) index = start;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var bullet = Themed(new TextBlock
            {
                Text = list.IsOrdered ? $"{index}." : "•",
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = BaseFontSize,
                VerticalAlignment = VerticalAlignment.Top
            }, "Text3");
            row.Children.Add(bullet);

            var content = new StackPanel();
            foreach (var b in item)
            {
                var el = RenderBlock(b);
                if (el is TextBlock t) t.Margin = new Thickness(0);
                if (el != null) content.Children.Add(el);
            }
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            sp.Children.Add(row);
            index++;
        }
        return sp;
    }

    private Control RenderQuote(QuoteBlock quote)
    {
        var inner = new StackPanel();
        foreach (var b in quote)
        {
            var el = RenderBlock(b);
            if (el != null) inner.Children.Add(el);
        }
        var bar = new Border { Width = 3, Margin = new Thickness(0, 0, 10, 0) };
        BindRes(bar, Border.BackgroundProperty, "Accent");
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 6), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(bar);
        Grid.SetColumn(inner, 1);
        grid.Children.Add(inner);
        return grid;
    }

    private Control RenderCode(string text)
    {
        var box = Themed(new SelectableTextBlock
        {
            Text = text.TrimEnd('\n', '\r'),
            FontFamily = MonoFont(),
            FontSize = BaseFontSize - 0.5,
            TextWrapping = TextWrapping.NoWrap,
        }, "Text1");
        var scroller = new ScrollViewer
        {
            Content = box,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 6),
            BorderThickness = new Thickness(1),
            Child = scroller
        };
        BindRes(border, Border.BackgroundProperty, "Bg3");
        BindRes(border, Border.BorderBrushProperty, "Border1");
        return border;
    }

    private Control RenderTable(MTable table)
    {
        var rows = table.OfType<MTableRow>().ToList();
        int cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        if (cols == 0) return new StackPanel();

        var grid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int r = 0; r < rows.Count; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            bool header = row.IsHeader;
            var cells = row.OfType<MTableCell>().ToList();
            for (int c = 0; c < cols; c++)
            {
                var cellText = c < cells.Count ? Para(FirstInline(cells[c])) : new SelectableTextBlock();
                if (header) cellText.FontWeight = FontWeight.Bold;
                cellText.Margin = new Thickness(0);

                var cellBorder = new Border { Child = cellText, BorderThickness = new Thickness(0.5), Padding = new Thickness(8, 5, 8, 5) };
                BindRes(cellBorder, Border.BorderBrushProperty, "Border1");
                if (header) BindRes(cellBorder, Border.BackgroundProperty, "Bg3");
                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        var wrap = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                Content = grid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            },
            Margin = new Thickness(0, 2, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        BindRes(wrap, Border.BorderBrushProperty, "Border1");
        return wrap;
    }

    private static ContainerInline? FirstInline(MTableCell cell) =>
        cell.OfType<ParagraphBlock>().FirstOrDefault()?.Inline;

    private static string GetCodeText(LeafBlock block)
    {
        if (block.Lines.Lines == null) return "";
        var sb = new System.Text.StringBuilder();
        var lines = block.Lines.Lines;
        for (int i = 0; i < block.Lines.Count; i++)
            sb.AppendLine(lines[i].Slice.ToString());
        return sb.ToString();
    }

    // ---- Inline-niveau ---------------------------------------------------------------

    private IEnumerable<AInline> BuildInlines(ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    yield return new Run(lit.Content.ToString());
                    break;
                case EmphasisInline em:
                {
                    Span span;
                    if (em.DelimiterChar == '~') span = new Span { TextDecorations = TextDecorations.Strikethrough };
                    else if (em.DelimiterCount >= 2) span = new Bold();
                    else span = new Italic();
                    foreach (var ch in BuildInlines(em)) span.Inlines.Add(ch);
                    yield return span;
                    break;
                }
                case CodeInline code:
                {
                    var run = new Run(code.Content) { FontFamily = MonoFont() };
                    BindRes(run, TextElement.BackgroundProperty, "Bg3");
                    yield return run;
                    break;
                }
                case LinkInline link when !link.IsImage:
                {
                    var text = string.Concat(FlattenText(link));
                    yield return MakeLink(string.IsNullOrEmpty(text) ? link.Url ?? "" : text, link.Url);
                    break;
                }
                case LinkInline img when img.IsImage:
                {
                    var alt = string.Concat(FlattenText(img));
                    yield return new Run(string.IsNullOrWhiteSpace(alt) ? "🖼" : $"🖼 {alt}");
                    break;
                }
                case AutolinkInline auto:
                    yield return MakeLink(auto.Url, auto.Url);
                    break;
                case LineBreakInline lb:
                    if (lb.IsHard) yield return new LineBreak();
                    else yield return new Run(" ");
                    break;
                case HtmlInline html:
                    if (html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase)) yield return new LineBreak();
                    break;
                case ContainerInline cont:
                    foreach (var ch in BuildInlines(cont)) yield return ch;
                    break;
                default:
                    var s = inline.ToString();
                    if (!string.IsNullOrEmpty(s)) yield return new Run(s);
                    break;
            }
        }
    }

    private static IEnumerable<string> FlattenText(ContainerInline container)
    {
        foreach (var i in container)
        {
            if (i is LiteralInline l) yield return l.Content.ToString();
            else if (i is CodeInline c) yield return c.Content;
            else if (i is ContainerInline ci) foreach (var s in FlattenText(ci)) yield return s;
        }
    }

    private AInline MakeLink(string text, string? url)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = BaseFontSize,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Accent"));
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var u)
            && (u.Scheme == "http" || u.Scheme == "https" || u.Scheme == "mailto"))
        {
            tb.PointerPressed += (_, e) =>
            {
                Ui.OpenUrl(u.AbsoluteUri);
                e.Handled = true;
            };
            ToolTip.SetTip(tb, u.AbsoluteUri);
        }
        return new InlineUIContainer { Child = tb, BaselineAlignment = BaselineAlignment.TextBottom };
    }
}
