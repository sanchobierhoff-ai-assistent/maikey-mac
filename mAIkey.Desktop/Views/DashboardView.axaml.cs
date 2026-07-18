using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Views;

public partial class DashboardView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;

    public DashboardView()
    {
        InitializeComponent();

        _config = App.Config;
        _api = App.Api;

        Loaded += DashboardView_Loaded;
    }

    private async void DashboardView_Loaded(object? sender, RoutedEventArgs e)
    {
        RefreshLocalStats();
        await RefreshSubscriptionStats();
        PopulateHotkeyList();
    }

    private void RefreshLocalStats()
    {
        var hotkeys = _config.Hotkeys;
        var activeCount = hotkeys.Count(h => h.Enabled && !h.FrozenByDowngrade);
        HotkeyCountText.Text = activeCount.ToString();
        StyleCountText.Text = _config.WritingStyles.Length.ToString();

        // Show/hide empty state
        EmptyHotkeyState.IsVisible = hotkeys.Length == 0;
    }

    private async Task RefreshSubscriptionStats()
    {
        try
        {
            var status = await _api.GetSubscriptionStatusAsync();
            if (status.Success)
            {
                TierText.Text = CapitalizeFirst(status.Tier ?? "free");
                TodayCountText.Text = status.DailyUsed.ToString();
                UsageText.Text = $"{status.RequestsUsed} / {status.MaxMonthlyRequests} verzoeken";

                // Show warning at 80%
                if (status.MaxMonthlyRequests > 0)
                {
                    var pct = (double)status.RequestsUsed / status.MaxMonthlyRequests;
                    UsageWarningCard.IsVisible = pct >= 0.8;
                    if (pct >= 0.8)
                        UsageWarningText.Text = $"Je hebt {(int)(pct * 100)}% van je maandlimiet gebruikt.";
                }
            }
        }
        catch
        {
            // Silently fail — offline or token expired
        }
    }

    private void PopulateHotkeyList()
    {
        var hotkeys = _config.Hotkeys;
        if (hotkeys.Length == 0) return;

        EmptyHotkeyState.IsVisible = false;

        // Clear existing items (keep empty state)
        var toRemove = HotkeyListPanel.Children
            .Where(c => c != EmptyHotkeyState).ToList();
        foreach (var c in toRemove)
            HotkeyListPanel.Children.Remove(c);

        var colors = new[] { "#60A5FA", "#10B981", "#A78BFA", "#F5A524", "#EF4444", "#EC4899" };

        for (int i = 0; i < hotkeys.Length; i++)
        {
            var hk = hotkeys[i];
            var color = colors[i % colors.Length];

            var row = new Border
            {
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(12, 10),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                MinHeight = 48
            };

            row.PointerEntered += (s, e) => row.Background = new SolidColorBrush(Color.Parse("#1F1F24"));
            row.PointerExited += (s, e) => row.Background = Brushes.Transparent;

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Color dot
            var dot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new Avalonia.CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse(color)),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(dot, 0);
            content.Children.Add(dot);

            // Name
            var name = new TextBlock
            {
                Text = hk.Name,
                FontSize = 13,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#F2F2F5")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1);
            content.Children.Add(name);

            // Hotkey chip
            var hotkeyText = FormatHotkey(hk);
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1F1F24")),
                BorderBrush = new SolidColorBrush(Color.Parse("#26262C")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                Padding = new Avalonia.Thickness(8, 3),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(8, 0)
            };
            var chipText = new TextBlock
            {
                Text = hotkeyText,
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#9A9AA3"))
            };
            chip.Child = chipText;
            Grid.SetColumn(chip, 2);
            content.Children.Add(chip);

            // Chevron
            var chevron = new TextBlock
            {
                Text = "›",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.Parse("#5E5E66")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(chevron, 3);
            content.Children.Add(chevron);

            row.Child = content;

            // Click placeholder — navigation handled by MainWindow
            row.PointerReleased += (s, e) => { };

            HotkeyListPanel.Children.Add(row);
        }
    }

    private static string FormatHotkey(mAIkey.Core.Models.HotkeyConfig hk)
    {
        var parts = new List<string>();
        var mods = hk.ModifierKeys;
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Cmd");

        // Convert virtual key code to string
        var keyStr = hk.Key switch
        {
            >= 48 and <= 57 => ((char)hk.Key).ToString(),
            >= 65 and <= 90 => ((char)hk.Key).ToString(),
            >= 112 and <= 123 => $"F{hk.Key - 111}",
            _ => $"Key{hk.Key}"
        };
        parts.Add(keyStr);
        return string.Join(" + ", parts);
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
