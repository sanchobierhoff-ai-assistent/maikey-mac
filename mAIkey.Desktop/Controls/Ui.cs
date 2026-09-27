using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;

namespace mAIkey.Desktop.Controls;

/// <summary>
/// Kleine UI-hulpjes die de WPF-patronen van de Windows-app nabootsen
/// (Application.Current.Windows, modale ShowDialog zonder owner, FindResource).
/// </summary>
public static class Ui
{
    public static IClassicDesktopStyleApplicationLifetime? Desktop =>
        Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    /// <summary>Het actieve (of anders het hoofd-)venster, voor het koppelen van dialogen.</summary>
    public static Window? ActiveWindow()
    {
        var d = Desktop;
        if (d == null) return null;
        return d.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible)
               ?? (d.MainWindow is { IsVisible: true } mw ? mw : null);
    }

    public static MainWindow? Main => Desktop?.MainWindow as MainWindow;

    /// <summary>
    /// Toon een venster modaal en wacht tot het sluit. Zonder (zichtbare) owner — bv. bij een
    /// sneltoets terwijl mAIkey op de achtergrond staat — wordt het als los, bovenliggend
    /// venster getoond (Avalonia's ShowDialog vereist namelijk een owner).
    /// </summary>
    public static Task ShowModalAsync(this Window window, Window? owner)
    {
        if (owner != null && owner.IsVisible && owner != window)
            return window.ShowDialog(owner);

        var tcs = new TaskCompletionSource<bool>();
        window.Closed += (_, _) => tcs.TrySetResult(true);
        window.Topmost = true;
        window.Show();
        window.Activate();
        return tcs.Task;
    }

    /// <summary>Voer uit op de UI-thread (ook als we er al op zitten).</summary>
    public static Task RunOnUi(Func<Task> action) =>
        Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action);

    public static Task<T> RunOnUi<T>(Func<Task<T>> action) =>
        Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action);

    public static IBrush Brush(string resourceKey, string fallback = "#F5A524")
    {
        var app = Application.Current;
        if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var res) && res is IBrush b)
            return b;
        return new SolidColorBrush(Color.Parse(fallback));
    }

    /// <summary>Open een URL in de standaardbrowser.</summary>
    /// <summary>Wissel een knop tussen stijlklassen (WPF: Style = FindResource("AccentButton")).</summary>
    public static void SetKind(Button button, string styleClass)
    {
        foreach (var c in new[] { "AccentButton", "GhostButton", "IconButton", "danger", "MkSecondaryButton" })
            button.Classes.Remove(c);
        button.Classes.Add(styleClass);
    }

    public static void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", url);
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>Zet tekst op het klembord via Avalonia.</summary>
    public static async Task SetClipboardTextAsync(string text, TopLevel? top = null)
    {
        top ??= ActiveWindow() ?? Desktop?.MainWindow;
        var cb = top?.Clipboard;
        if (cb != null) await cb.SetTextAsync(text);
    }
}

/// <summary>
/// Sneltoets-vangen zoals in de Windows-app, maar opgeslagen in het gedeelde formaat
/// (WPF/Avalonia Key-getallen; modifiers Alt=1, Ctrl=2, Shift=4, Cmd=8).
/// </summary>
public static class HotkeyCapture
{
    public static bool IsModifierKey(Avalonia.Input.Key key) =>
        key is Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl
            or Avalonia.Input.Key.LeftAlt or Avalonia.Input.Key.RightAlt
            or Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift
            or Avalonia.Input.Key.LWin or Avalonia.Input.Key.RWin
            or Avalonia.Input.Key.System or Avalonia.Input.Key.None;

    public static int ToWpfModifiers(Avalonia.Input.KeyModifiers m) =>
        (m.HasFlag(Avalonia.Input.KeyModifiers.Alt) ? 1 : 0) | (m.HasFlag(Avalonia.Input.KeyModifiers.Control) ? 2 : 0) |
        (m.HasFlag(Avalonia.Input.KeyModifiers.Shift) ? 4 : 0) | (m.HasFlag(Avalonia.Input.KeyModifiers.Meta) ? 8 : 0);

    /// <summary>
    /// Vangt een combinatie uit een KeyDown. Geeft false bij alleen een modifier of zonder modifier
    /// (dan wordt <paramref name="needsModifier"/> true, zodat de aanroeper kan uitleggen).
    /// </summary>
    public static bool TryCapture(Avalonia.Input.KeyEventArgs e, out int modifiers, out int key, out bool needsModifier)
    {
        e.Handled = true;
        modifiers = ToWpfModifiers(e.KeyModifiers);
        key = (int)e.Key;
        needsModifier = false;
        if (IsModifierKey(e.Key)) return false;
        if (modifiers == 0) { needsModifier = true; return false; }
        return true;
    }
}
