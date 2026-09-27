using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Render/uitlees-logica voor Jira "verplichte/aangepaste velden" (createmeta) — port van
/// Views/JiraDynamicFieldsHelper. Ondersteunt select/multi-select/number/date/text-array/string.
/// </summary>
internal static class JiraDynamicFields
{
    public static void Render(Panel target, Control resourceHost, JiraFieldMeta[] fields, Dictionary<string, Control> controls)
    {
        target.Children.Clear();
        controls.Clear();

        foreach (var field in fields)
        {
            var label = new TextBlock
            {
                Text = field.Name + (field.Required ? " *" : ""),
                FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6)
            };
            label.Bind(TextBlock.ForegroundProperty, resourceHost.GetResourceObservable("Text3"));
            target.Children.Add(label);

            Control control;
            switch (field.FieldType)
            {
                case "single-select":
                    var combo = new ComboBox { Height = 38, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 16) };
                    if (field.AllowedValues != null)
                    {
                        combo.ItemsSource = field.AllowedValues;
                        if (field.AllowedValues.Length == 1) combo.SelectedIndex = 0;
                    }
                    control = combo;
                    break;

                case "multi-select":
                    var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
                    if (field.AllowedValues != null)
                    {
                        bool preselect = field.AllowedValues.Length == 1;
                        foreach (var option in field.AllowedValues)
                            panel.Children.Add(new CheckBox { Content = option.Name, Tag = option.Id, IsChecked = preselect, FontSize = 12, Margin = new Thickness(0, 2) });
                    }
                    control = panel;
                    break;

                case "date":
                    control = new CalendarDatePicker { Height = 38, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 16) };
                    break;

                case "text-array":
                    var arrayBox = new TextBox { Height = 38, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
                    target.Children.Add(arrayBox);
                    var hint = new TextBlock { Text = "Comma-separated values", FontSize = 11, Margin = new Thickness(0, 0, 0, 16) };
                    hint.Bind(TextBlock.ForegroundProperty, resourceHost.GetResourceObservable("Text3"));
                    target.Children.Add(hint);
                    controls[field.Key] = arrayBox;
                    continue;

                default: // string / number
                    control = new TextBox { Height = 38, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    break;
            }

            target.Children.Add(control);
            controls[field.Key] = control;
        }
    }

    public static object? ExtractValue(Control control, JiraFieldMeta field)
    {
        switch (field.FieldType)
        {
            case "single-select":
                return control is ComboBox { SelectedItem: JiraFieldOption opt }
                    ? new Dictionary<string, string> { ["id"] = opt.Id } : null;
            case "multi-select":
                if (control is StackPanel panel)
                {
                    var sel = panel.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true)
                        .Select(cb => new Dictionary<string, string> { ["id"] = cb.Tag?.ToString() ?? "" }).ToList();
                    return sel.Count > 0 ? sel : null;
                }
                return null;
            case "number":
                return control is TextBox nb && double.TryParse(nb.Text?.Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num) ? num : null;
            case "date":
                return control is CalendarDatePicker { SelectedDate: { } d } ? d.ToString("yyyy-MM-dd") : null;
            case "text-array":
                return control is TextBox ab && !string.IsNullOrWhiteSpace(ab.Text)
                    ? ab.Text.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToArray() : null;
            default:
                return control is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text) ? tb.Text.Trim() : null;
        }
    }

    public static Dictionary<string, object>? BuildPayload(JiraFieldMeta[]? fields, Dictionary<string, Control> controls)
    {
        if (fields == null || fields.Length == 0) return null;
        var result = new Dictionary<string, object>();
        foreach (var f in fields)
            if (controls.TryGetValue(f.Key, out var c) && ExtractValue(c, f) is { } v)
                result[f.Key] = v;
        return result.Count > 0 ? result : null;
    }

    public static List<string> MissingRequired(JiraFieldMeta[]? fields, Dictionary<string, Control> controls)
    {
        var missing = new List<string>();
        if (fields == null) return missing;
        foreach (var f in fields.Where(f => f.Required))
            if (controls.TryGetValue(f.Key, out var c) && ExtractValue(c, f) == null)
                missing.Add(f.Name);
        return missing;
    }
}

/// <summary>Eén afbeelding in de Jira-review-galerij (port van Views/JiraReviewImage).</summary>
public class JiraReviewImage
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string Url { get; }
    public bool Relevant { get; }
    public string Reason { get; }
    public bool Include { get; set; }
    public int TargetTicketIndex { get; set; }

    public JiraReviewImage(string url, bool relevant = true, string? reason = null)
    {
        Url = url;
        Relevant = relevant;
        Reason = reason ?? "";
        Include = relevant;
    }

    public async Task<Bitmap?> LoadThumbAsync()
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(Url);
            using var ms = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(ms, 200);
        }
        catch { return null; }
    }

    /// <summary>Kies een afbeelding, upload hem via de API en geef de URL terug (of null).</summary>
    public static async Task<string?> PickAndUploadAsync(ApiClient api, Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = Loc.T("JiraReview_PickImage", "Afbeelding kiezen"),
            FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.heic", "*.gif", "*.webp" } } }
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return null;
        try
        {
            var dataUrl = ImageHelper.FileToDataUri(path);
            if (dataUrl == null) return null;
            var resp = await api.UploadImageAsync(dataUrl);
            if (resp != null && resp.Success && !string.IsNullOrEmpty(resp.ImageUrl)) return resp.ImageUrl;
        }
        catch (Exception ex) { Logger.Log($"[JIRA] Add-image upload failed: {ex.Message}"); }
        return null;
    }

    /// <summary>
    /// Bouw een tegel (thumbnail, ✕, "niet-relevant"-badge, "meesturen"-vinkje en optioneel
    /// een ticket-keuze) voor deze afbeelding.
    /// </summary>
    public Control BuildTile(Control resourceHost, Action<JiraReviewImage> onRemove, IList<string>? ticketLabels = null)
    {
        var image = new Image { Height = 90, Stretch = Stretch.UniformToFill };
        _ = LoadThumbAsync().ContinueWith(t =>
        {
            if (t.Result != null) Avalonia.Threading.Dispatcher.UIThread.Post(() => image.Source = t.Result);
        });

        var remove = new Button
        {
            Content = "✕", Width = 22, Height = 22, Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        remove.Classes.Add("GhostButton");
        remove.Click += (_, _) => onRemove(this);

        var grid = new Grid();
        grid.Children.Add(image);
        grid.Children.Add(remove);
        if (!Relevant)
        {
            grid.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#CC000000")),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 1),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2),
                Child = new TextBlock { Text = Loc.T("JiraReview_NotRelevant", "niet-relevant"), FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#FFD08A")) }
            });
        }

        var include = new CheckBox { Content = Loc.T("JiraReview_IncludeImage", "meesturen"), IsChecked = Include, FontSize = 11, Margin = new Thickness(0, 6, 0, 0) };
        if (!string.IsNullOrEmpty(Reason)) ToolTip.SetTip(include, Reason);
        include.IsCheckedChanged += (_, _) => Include = include.IsChecked == true;

        var stack = new StackPanel();
        stack.Children.Add(grid);
        stack.Children.Add(include);

        if (ticketLabels != null && ticketLabels.Count > 1)
        {
            var target = new ComboBox { ItemsSource = ticketLabels, SelectedIndex = Math.Min(TargetTicketIndex, ticketLabels.Count - 1), FontSize = 11, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            target.SelectionChanged += (_, _) => TargetTicketIndex = Math.Max(0, target.SelectedIndex);
            stack.Children.Add(target);
        }

        var tile = new Border
        {
            Width = 150, Margin = new Thickness(0, 0, 10, 10), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(6), Child = stack, ClipToBounds = true
        };
        tile.Bind(Border.BackgroundProperty, resourceHost.GetResourceObservable("Bg3"));
        tile.Bind(Border.BorderBrushProperty, resourceHost.GetResourceObservable("Border1"));
        return tile;
    }
}

/// <summary>Galerij met Jira-afbeeldingen + "Afbeelding toevoegen"-knop.</summary>
internal class JiraImageGallery : StackPanel
{
    private readonly ApiClient _api;
    private readonly Window _owner;
    private readonly WrapPanel _tiles = new();
    private readonly Func<IList<string>?>? _ticketLabels;
    public List<JiraReviewImage> Images { get; } = new();

    public JiraImageGallery(ApiClient api, Window owner, string hint, Func<IList<string>?>? ticketLabels = null)
    {
        _api = api;
        _owner = owner;
        _ticketLabels = ticketLabels;
        Margin = new Thickness(0, 0, 0, 16);

        var label = new TextBlock { Text = L.T("JiraReview_Images"), FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        label.Bind(TextBlock.ForegroundProperty, owner.GetResourceObservable("Text3"));
        var hintTb = new TextBlock { Text = hint, FontSize = 11, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        hintTb.Bind(TextBlock.ForegroundProperty, owner.GetResourceObservable("Text3"));

        var add = new Button { Content = L.T("JiraReview_AddImage"), Height = 32, Padding = new Thickness(12, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
        add.Classes.Add("GhostButton");
        add.Click += async (_, _) =>
        {
            add.IsEnabled = false;
            try
            {
                var url = await JiraReviewImage.PickAndUploadAsync(_api, _owner);
                if (!string.IsNullOrEmpty(url)) Add(new JiraReviewImage(url, true));
            }
            finally { add.IsEnabled = true; }
        };

        Children.Add(label);
        Children.Add(hintTb);
        Children.Add(_tiles);
        Children.Add(add);
        IsVisible = false;
    }

    public void Add(JiraReviewImage img)
    {
        Images.Add(img);
        Refresh();
    }

    public void Refresh()
    {
        _tiles.Children.Clear();
        foreach (var img in Images)
            _tiles.Children.Add(img.BuildTile(_owner, i => { Images.Remove(i); Refresh(); }, _ticketLabels?.Invoke()));
        IsVisible = Images.Count > 0;
    }
}
