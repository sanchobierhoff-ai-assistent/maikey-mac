using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Controls;

/// <summary>
/// Afbeeldingen toevoegen aan een invoerveld: plakken (Cmd+V), bestand kiezen (📎)
/// en slepen. Gedeeld door het context-venster, de assistent en de koppelingen-chat.
/// </summary>
public static class ImageInput
{
    /// <summary>
    /// Laat Cmd+V in <paramref name="textBox"/> een klembord-afbeelding als bijlage toevoegen
    /// (tekst plakken werkt gewoon zoals altijd).
    /// </summary>
    public static void EnableImagePaste(TextBox textBox, Action<string> add)
    {
        textBox.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            bool cmd = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (!cmd || e.Key != Key.V) return;

            var clip = App.Platform.ClipboardService;
            var png = clip.GetImagePng();
            if (png == null || !string.IsNullOrEmpty(clip.GetText())) return;

            var uri = ImageHelper.ToJpegDataUri(png);
            if (uri != null)
            {
                add(uri);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>Open de bestandskiezer voor afbeeldingen.</summary>
    public static async Task PickImagesAsync(Control owner, Action<string> add)
    {
        var top = TopLevel.GetTopLevel(owner);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(L.T("Common_Images"))
                {
                    Patterns = ImageHelper.ImageExtensions.Select(e => "*." + e).ToArray()
                }
            }
        });
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (path == null) continue;
            var uri = ImageHelper.FileToDataUri(path);
            if (uri != null) add(uri);
        }
    }

    /// <summary>Accepteer gesleepte afbeeldingsbestanden op <paramref name="target"/>.</summary>
    public static void EnableDrop(Control target, Action<string> add)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        });
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.Data.GetFiles();
            if (files == null) return;
            foreach (var f in files)
            {
                var path = f.TryGetLocalPath();
                if (path == null) continue;
                var uri = ImageHelper.FileToDataUri(path);
                if (uri != null) add(uri);
            }
        });
    }
}
