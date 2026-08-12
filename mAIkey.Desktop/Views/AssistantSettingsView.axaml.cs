using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Views;

public partial class AssistantSettingsView : UserControl
{
    private readonly ConfigService _config;
    private bool _loading;

    public AssistantSettingsView()
    {
        InitializeComponent();
        _config = App.Config;
        Loaded += AssistantSettingsView_Loaded;
    }

    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private async void AssistantSettingsView_Loaded(object? sender, RoutedEventArgs e)
    {
        _loading = true;

        AssistantEnabledCheck.IsChecked = _config.AssistantEnabled;
        ScreenshotEnabledCheck.IsChecked = _config.ScreenshotEnabled;
        AssistantHotkeyBox.Text = _config.AssistantHotkey;
        ScreenshotHotkeyBox.Text = _config.ScreenshotHotkey;

        AssistantEnabledCheck.IsCheckedChanged += (_, _) =>
        { if (!_loading) _config.AssistantEnabled = AssistantEnabledCheck.IsChecked ?? false; };
        ScreenshotEnabledCheck.IsCheckedChanged += (_, _) =>
        { if (!_loading) _config.ScreenshotEnabled = ScreenshotEnabledCheck.IsChecked ?? false; };

        _loading = false;

        await LoadProfileAsync();
        await LoadMemoryAsync();
    }

    // ═══ SNELTOETSEN ═══

    private void AssistantHotkey_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var combo = FormatCombo(e.KeyModifiers, e.Key);
        if (combo == null) return;
        AssistantHotkeyBox.Text = combo;
        _config.AssistantHotkey = combo;
    }

    private void ScreenshotHotkey_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var combo = FormatCombo(e.KeyModifiers, e.Key);
        if (combo == null) return;
        ScreenshotHotkeyBox.Text = combo;
        _config.ScreenshotHotkey = combo;
    }

    private static string? FormatCombo(KeyModifiers mods, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return null;

        var parts = new System.Collections.Generic.List<string>();
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (mods.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (mods.HasFlag(KeyModifiers.Meta)) parts.Add("Cmd");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    // ═══ ACHTERGRONDPROFIEL ═══

    private async Task LoadProfileAsync()
    {
        try
        {
            var resp = await App.Api.GetAssistantProfileAsync();
            if (resp.Success && resp.Profile != null)
            {
                CompanyBox.Text = resp.Profile.Company ?? "";
                RoleBox.Text = resp.Profile.Role ?? "";
                BackgroundBox.Text = resp.Profile.Background ?? "";
            }
        }
        catch { /* offline — laat leeg */ }
    }

    private async void SaveBackground_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var resp = await App.Api.SaveAssistantProfileAsync(
                CompanyBox.Text?.Trim(), RoleBox.Text?.Trim(), BackgroundBox.Text?.Trim());
            BackgroundSavedLabel.Text = resp.Success ? "✓ Opgeslagen" : "Opslaan mislukt";
            _ = ClearLabelSoon();
        }
        catch
        {
            BackgroundSavedLabel.Text = "Opslaan mislukt";
        }
    }

    private async Task ClearLabelSoon()
    {
        try { await Task.Delay(2500); } catch { }
        BackgroundSavedLabel.Text = "";
    }

    // ═══ GEHEUGEN ═══

    private async Task LoadMemoryAsync()
    {
        MemoryPanel.Children.Clear();
        try
        {
            var resp = await App.Api.GetMemoryAsync();
            var items = resp.Memory ?? new();
            MemoryEmpty.IsVisible = items.Count == 0;
            foreach (var m in items)
                MemoryPanel.Children.Add(BuildMemoryRow(m.Id, m.Content));
        }
        catch
        {
            MemoryEmpty.IsVisible = true;
            MemoryEmpty.Text = "Geheugen kon niet laden (offline?).";
        }
    }

    private Control BuildMemoryRow(string id, string content)
    {
        var text = new TextBlock
        {
            Text = content, Foreground = TB("Text1"), FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
        };
        var del = new Button
        {
            Content = "✕", FontSize = 11, Width = 24, Height = 24, Padding = new Thickness(0),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = TB("Text3"), Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        };
        del.Click += async (_, _) =>
        {
            del.IsEnabled = false;
            try { await App.Api.DeleteMemoryAsync(id); } catch { }
            await LoadMemoryAsync();
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(text);
        Grid.SetColumn(del, 1);
        grid.Children.Add(del);

        return new Border
        {
            Background = TB("Bg1"), BorderBrush = TB("Border1"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8), Child = grid
        };
    }

    private async void AddMemory_Click(object? sender, RoutedEventArgs e)
    {
        var content = MemoryAddBox.Text?.Trim();
        if (string.IsNullOrEmpty(content)) return;
        try
        {
            await App.Api.AddMemoryAsync(content);
            MemoryAddBox.Text = "";
            await LoadMemoryAsync();
        }
        catch { /* stil */ }
    }
}
