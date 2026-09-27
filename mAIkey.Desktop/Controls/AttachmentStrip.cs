using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Controls;

/// <summary>
/// Rij met afbeelding-bijlagen (thumbnail + ✕), gebruikt in het context-venster en de
/// assistent. Houdt de base64-data-URI's bij; wordt onzichtbaar als hij leeg is.
/// </summary>
public class AttachmentStrip : WrapPanel
{
    private readonly List<(string Id, string DataUri)> _items = new();

    public event EventHandler? Changed;

    public IReadOnlyList<string> Images => _items.Select(i => i.DataUri).ToList();
    public int Count => _items.Count;

    public AttachmentStrip()
    {
        Orientation = Orientation.Horizontal;
        IsVisible = false;
    }

    public void Add(string dataUri)
    {
        if (string.IsNullOrWhiteSpace(dataUri)) return;
        var id = Guid.NewGuid().ToString("N");
        _items.Add((id, dataUri));

        var remove = new Button
        {
            Content = "✕",
            Width = 18, Height = 18, Padding = new Thickness(0), FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(9),
        };
        remove.Classes.Add("GhostButton");

        var tile = new Border
        {
            Width = 64, Height = 64,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 8, 8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Tag = id,
            Child = new Grid
            {
                Children =
                {
                    new Image { Source = ImageHelper.ThumbFromDataUri(dataUri), Stretch = Stretch.UniformToFill },
                    remove
                }
            }
        };
        tile.Bind(Border.BorderBrushProperty, tile.GetResourceObservable("Border1"));
        tile.Bind(Border.BackgroundProperty, tile.GetResourceObservable("Bg2"));

        remove.Click += (_, _) =>
        {
            _items.RemoveAll(i => i.Id == id);
            Children.Remove(tile);
            IsVisible = _items.Count > 0;
            Changed?.Invoke(this, EventArgs.Empty);
        };

        Children.Add(tile);
        IsVisible = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _items.Clear();
        Children.Clear();
        IsVisible = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
