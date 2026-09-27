namespace mAIkey.Core.Interfaces;

/// <summary>
/// Klembord + toetsenbordsimulatie van het platform (macOS: NSPasteboard + CGEvent).
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Simuleer Cmd+C in de app die vooraan staat. Retourneert true als het klembord
    /// daardoor veranderde (er was dus een selectie), false als er niets gekopieerd werd.
    /// </summary>
    Task<bool> CopySelectionAsync();

    /// <summary>Kopieer de selectie en geef alleen de tekst terug (null = geen selectie).</summary>
    Task<string?> GetSelectedTextAsync();

    /// <summary>Platte tekst op het klembord (of null).</summary>
    string? GetText();

    /// <summary>HTML op het klembord (of null) — bevat bij o.a. Mail/Safari/Word ook afbeeldingen.</summary>
    string? GetHtml();

    /// <summary>Afbeelding op het klembord als PNG-bytes (of null).</summary>
    byte[]? GetImagePng();

    /// <summary>Zet tekst op het klembord.</summary>
    Task SetTextAsync(string text);

    /// <summary>Zet tekst op het klembord en simuleer Cmd+V (vervangt de selectie).</summary>
    Task ReplaceSelectedTextAsync(string newText);

    /// <summary>Simuleer alleen Cmd+V.</summary>
    Task PasteAsync();

    /// <summary>Onthoud/herstel de app die vooraan stond (focus teruggeven na een dialoog).</summary>
    IntPtr GetForegroundWindow();
    void SetForegroundWindow(IntPtr handle);
}
