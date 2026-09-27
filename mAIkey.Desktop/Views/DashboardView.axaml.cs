using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Views;

/// <summary>Dashboard (port van Views/DashboardView).</summary>
public partial class DashboardView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;
    private int _refreshCount;
    private DateTime _refreshWindowStart = DateTime.MinValue;

    public DashboardView() : this(App.Config, App.Api) { }

    public DashboardView(ConfigService config, ApiClient apiClient)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;

        ApplyLocalization();
        LoadHotkeys();
        _ = LoadStatsAsync();
        LoadAnnouncementsAsync();
        UpdateOnboardingUI();
    }

    private void ApplyLocalization()
    {
        PageEyebrow.Text = L.T("Dashboard_Eyebrow");
        NewHotkeyBtnText.Text = L.T("Dashboard_NewHotkey");
        StatHotkeysHeader.Text = L.T("Dashboard_Hotkeys");
        StatHotkeysLabel.Text = L.T("Dashboard_Active");
        StatTodayHeader.Text = L.T("Dashboard_Today");
        StatTodayLabel.Text = L.T("Dashboard_Requests");
        StatStylesHeader.Text = L.T("Dashboard_Styles");
        StatStylesLabel.Text = L.T("Dashboard_StylesLabel");
        StatSubHeader.Text = L.T("Dashboard_Sub");
        YourHotkeysHeader.Text = L.T("Dashboard_YourHotkeys");
        EmptyStateTitle.Text = L.T("Dashboard_NoHotkeys");
        EmptyStateSub.Text = L.T("Dashboard_NoHotkeysSub");
        EmptyStateBtn.Content = L.T("Dashboard_EmptyStateBtn");
        UpgradeSubBtn.Content = L.T("Dashboard_UpgradeBtn");
        ToolTip.SetTip(RefreshSubBtn, L.T("Dashboard_RefreshTooltip"));
        UpgradePlanBtn.Content = L.T("Dashboard_UpgradePlanBtn");
        NewsHeader.Text = L.T("Dashboard_News");
        SetupProgressHeader.Text = L.T("Dashboard_SetupHeader");
        SetupProgressSub.Text = L.T("Dashboard_SetupSub");
        StepTile1Title.Text = L.T("Dashboard_Step1_Title");
        StepTile1Sub.Text = L.T("Dashboard_Step1_Sub");
        StepTile1TutLabel.Text = L.T("Dashboard_TourBtn");
        StepTile2Title.Text = L.T("Dashboard_Step2_Title");
        StepTile2Sub.Text = L.T("Dashboard_Step2_Sub");
        StepTile2TutLabel.Text = L.T("Dashboard_TourBtn");
        StepTile3Title.Text = L.T("Dashboard_Step3_Title");
        StepTile3Sub.Text = L.T("Dashboard_Step3_Sub");
        StepTile3TutLabel.Text = L.T("Dashboard_TourBtn");
        StepTile4Title.Text = Loc.T("Dashboard_Step4_Title_Mac", L.T("Dashboard_Step4_Title"));
        StepTile4Sub.Text = Loc.T("Dashboard_Step4_Sub_Mac", L.T("Dashboard_Step4_Sub"));
        StepTile4TutLabel.Text = L.T("Dashboard_TourBtn");
    }

    public void ForceRefreshStats() => _ = LoadStatsAsync();

    public void RefreshOnboarding()
    {
        UpdateOnboardingUI();
        LoadHotkeys();
    }

    // ── Onboarding ───────────────────────────────────────────────────────

    private void UpdateOnboardingUI()
    {
        bool graduated = _config.IsOnboardingDone();
        var completed = _config.GetOnboardingCompletedIds();
        int pct = (int)(completed.Count / 4.0 * 100);

        if (graduated)
        {
            OnboardingHero.IsVisible = false;
            PageTitle.IsVisible = false;
            return;
        }

        OnboardingHero.IsVisible = true;
        PageTitle.Text = L.T("Dashboard_SetupTitle");
        OnboardingProgressTitle.Text = L.Tf("Dashboard_SetupProgress", pct);
        DrawProgressRing(completed.Count / 4.0);

        var tiles = new (Border tile, Control tutBtn, Control doneInd, string id)[]
        {
            (StepTile1, StepTile1TutorialBtn, StepTile1DoneIndicator, "hotkey"),
            (StepTile2, StepTile2TutorialBtn, StepTile2DoneIndicator, "style"),
            (StepTile3, StepTile3TutorialBtn, StepTile3DoneIndicator, "template"),
            (StepTile4, StepTile4TutorialBtn, StepTile4DoneIndicator, "pin"),
        };

        bool nextFound = false;
        foreach (var (tile, tutBtn, doneInd, id) in tiles)
        {
            bool done = completed.Contains(id);
            tutBtn.IsVisible = !done;
            doneInd.IsVisible = done;
            tile.Opacity = done ? 0.6 : 1.0;
            if (!done && !nextFound)
            {
                nextFound = true;
                var ac = AccentColor();
                tile.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, ac.R, ac.G, ac.B));
            }
            else tile.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));
        }
    }

    private void DrawProgressRing(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        ProgressRingText.Text = $"{(int)(fraction * 100)}%";
        if (fraction <= 0) { ProgressRingArc.Data = null; return; }

        const double cx = 27, cy = 27, r = 22;
        double startRad = -Math.PI / 2;
        var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(startPt, false);
            if (fraction >= 1.0)
            {
                var mid = new Point(cx + r * Math.Cos(startRad + Math.PI), cy + r * Math.Sin(startRad + Math.PI));
                ctx.ArcTo(mid, new Size(r, r), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(startPt, new Size(r, r), 0, false, SweepDirection.Clockwise);
            }
            else
            {
                double sweep = fraction * 360.0;
                double endRad = startRad + sweep * Math.PI / 180.0;
                var endPt = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
                ctx.ArcTo(endPt, new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
            }
            ctx.EndFigure(false);
        }
        ProgressRingArc.Data = geo;
    }

    private void StepTile_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border { Tag: string stepId }) Ui.Main?.StartTour(stepId);
    }

    // ── Stats ───────────────────────────────────────────────────────────

    private async Task LoadStatsAsync()
    {
        var hotkeys = _config.Hotkeys;

        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success)
            {
                StatTierText.Text = status.Tier;
                if (status.UsageExceeded || status.UsageWarning)
                {
                    PlanWarningCard.IsVisible = true;
                    double pctFraction = status.UsageExceeded ? 1.0 : 0.82;
                    PlanWarningPct.Text = status.UsageExceeded ? "100%" : "≥80%";
                    PlanWarningLabel.Text = status.UsageExceeded ? L.T("Dashboard_WarnExceeded") : L.T("Dashboard_WarnAlmost");
                    PlanWarningResetText.Text = status.DaysUntilReset == 0 ? L.T("Dashboard_ResetToday")
                        : status.DaysUntilReset == 1 ? L.T("Dashboard_ResetTomorrow")
                        : L.Tf("Dashboard_ResetDays", status.DaysUntilReset);
                    var col = status.UsageExceeded ? Color.FromRgb(0xEF, 0x44, 0x44) : AccentColor();
                    var brush = new SolidColorBrush(col);
                    PlanWarningLabel.Foreground = brush;
                    PlanWarningPct.Foreground = brush;
                    PlanWarningBar.Background = brush;
                    PlanWarningCard.BorderBrush = new SolidColorBrush(col, 0.35);
                    PlanWarningCard.Background = new SolidColorBrush(col, 0.07);
                    PlanWarningBar.Width = pctFraction * 230;
                }
                else PlanWarningCard.IsVisible = false;
            }
        }
        catch { StatTierText.Text = "—"; }

        try
        {
            var daily = await _apiClient.GetDailyStatsAsync();
            StatTodayValue.Text = daily?.Success == true && daily.Today != null ? daily.Today.Requests.ToString("N0") : "0";
        }
        catch { StatTodayValue.Text = "—"; }

        StatHotkeysValue.Text = hotkeys.Count(h => h.Enabled).ToString();
        StatHotkeysLabel.Text = L.T("Dashboard_Active");
        HotkeyCountBadge.Text = L.Tf("Dashboard_HotkeyCount", hotkeys.Length);
        StatStylesValue.Text = _config.GetWritingStyles().Length.ToString();
    }

    // ── Hotkeys ─────────────────────────────────────────────────────────

    private Color AccentColor()
    {
        if (this.TryFindResource("AccentColor", ActualThemeVariant, out var c) && c is Color col) return col;
        return Color.Parse("#47906B");
    }

    private (IBrush fg, IBrush bg)[] BuildDotPalette()
    {
        IBrush accentFg = this.TryFindResource("AccentForeground", ActualThemeVariant, out var r) && r is IBrush b
            ? b : new SolidColorBrush(Color.Parse("#F5EFE2"));
        return new (IBrush, IBrush)[]
        {
            (new SolidColorBrush(AccentColor()), accentFg),
            (new SolidColorBrush(Color.Parse("#60A5FA")), new SolidColorBrush(Color.Parse("#18222B"))),
            (new SolidColorBrush(Color.Parse("#10B981")), new SolidColorBrush(Color.Parse("#18251F"))),
            (new SolidColorBrush(Color.Parse("#F59E0B")), new SolidColorBrush(Color.Parse("#2B2518"))),
        };
    }

    private void LoadHotkeys()
    {
        var hotkeys = _config.Hotkeys;
        if (hotkeys.Length == 0)
        {
            EmptyState.IsVisible = true;
            HotkeysListView.IsVisible = false;
            return;
        }

        var palette = BuildDotPalette();
        HotkeysListView.ItemsSource = hotkeys.Select((h, i) => new HotkeyViewModel
        {
            HotkeyCombo = HotkeyKeys.Format(h.ModifierKeys, h.Key),
            Name = string.IsNullOrEmpty(h.Name) ? L.T("Dashboard_UnnamedHotkey") : h.Name,
            DotBrush = palette[i % palette.Length].fg,
            DotBrushBg = palette[i % palette.Length].bg,
            HotkeyConfig = h,
        }).ToList();

        EmptyState.IsVisible = false;
        HotkeysListView.IsVisible = true;
    }

    // ── Events ──────────────────────────────────────────────────────────

    private void AddHotkey_Click(object? sender, RoutedEventArgs e) => Ui.Main?.NavigateToHotkeys();

    private void HotkeyItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border { Tag: HotkeyViewModel vm }) Ui.Main?.NavigateToHotkeyEditor(vm.HotkeyConfig?.Id);
    }

    private void HotkeyItem_MouseEnter(object? sender, PointerEventArgs e)
    {
        if (sender is Border b) b.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg3"));
    }

    private void HotkeyItem_MouseLeave(object? sender, PointerEventArgs e)
    {
        if (sender is Border b) b.Background = Brushes.Transparent;
    }

    private void StatHotkeys_Click(object? sender, PointerReleasedEventArgs e) => Ui.Main?.NavigateToHotkeys();

    private void UpgradeButton_Click(object? sender, RoutedEventArgs e) => Ui.Main?.OpenPricingPage();

    private async void RefreshSub_Click(object? sender, RoutedEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _refreshWindowStart).TotalSeconds > 60)
        {
            _refreshCount = 0;
            _refreshWindowStart = now;
        }
        if (++_refreshCount > 5)
        {
            ToolTip.SetTip(RefreshSubBtn, L.T("Dashboard_RefreshCooldown"));
            return;
        }

        RefreshSubBtn.IsEnabled = false;
        try
        {
            await LoadStatsAsync();
            if (App.Hotkeys != null) await App.Hotkeys.RefreshSubscriptionAsync();
        }
        finally
        {
            RefreshSubBtn.IsEnabled = true;
            ToolTip.SetTip(RefreshSubBtn, L.T("Dashboard_RefreshTooltip"));
        }
    }

    // ── Nieuws & tips ───────────────────────────────────────────────────

    private async void LoadAnnouncementsAsync()
    {
        try
        {
            var items = await _apiClient.GetAnnouncementsAsync(_config.InterfaceLanguage);
            NewsItemsControl.ItemsSource = items.Select(a => new AnnouncementViewModel
            {
                Type = a.Type,
                Title = a.Title,
                Body = a.Body,
                RelativeDate = GetRelativeDate(a.PublishedAt),
                TypeForeground = GetTypeBrush(a.Type, "fg"),
                TypeBackground = GetTypeBrush(a.Type, "bg"),
                TypeBorderBrush = GetTypeBrush(a.Type, "border"),
            }).ToList();
        }
        catch { /* leeg laten */ }
    }

    private static string GetRelativeDate(DateTime date)
    {
        var delta = DateTime.UtcNow - date.ToUniversalTime();
        if (delta.TotalDays < 1) return "vandaag";
        if (delta.TotalDays < 2) return "gisteren";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} dagen geleden";
        if (delta.TotalDays < 14) return "1 week geleden";
        if (delta.TotalDays < 30) return $"{(int)(delta.TotalDays / 7)} weken geleden";
        if (delta.TotalDays < 60) return "1 maand geleden";
        return $"{(int)(delta.TotalDays / 30)} maanden geleden";
    }

    private IBrush Res(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var r) && r is IBrush b ? b : Brushes.Gray;

    private IBrush GetTypeBrush(string type, string part) => (type, part) switch
    {
        ("NIEUW", "fg") => Res("Accent"),
        ("NIEUW", "bg") => Res("AccentSoft"),
        ("NIEUW", _) => Res("AccentBorderSubtle"),
        ("UPDATE", "fg") => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
        ("UPDATE", "bg") => new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81)),
        ("UPDATE", _) => new SolidColorBrush(Color.FromArgb(0x33, 0x10, 0xB9, 0x81)),
        (_, "fg") => Res("Text2"),
        (_, "bg") => Res("Bg3"),
        _ => Res("Border1"),
    };

    public class AnnouncementViewModel
    {
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string RelativeDate { get; set; } = "";
        public IBrush TypeForeground { get; set; } = Brushes.White;
        public IBrush TypeBackground { get; set; } = Brushes.Transparent;
        public IBrush TypeBorderBrush { get; set; } = Brushes.Transparent;
    }

    public class HotkeyViewModel
    {
        public string HotkeyCombo { get; set; } = "";
        public string Name { get; set; } = "";
        public IBrush DotBrush { get; set; } = Brushes.Green;
        public IBrush DotBrushBg { get; set; } = Brushes.Beige;
        public HotkeyConfig? HotkeyConfig { get; set; }
    }
}
