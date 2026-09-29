using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Views;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop;

/// <summary>
/// Hoofdvenster: zijbalk-navigatie + content (port van frontend/MainWindow). De
/// sneltoets-afhandeling zit op de Mac in Services/HotkeyRuntime.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private Button? _activeNavButton;
    private bool _reallyClose;
    private DateTime _lastCreditsRefresh = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _config = App.Config;
        _api = App.Api;

        UpdateProfileLabel();

        TitleBarArea.PointerPressed += TitleBar_PointerPressed;
        ContentTitleBar.PointerPressed += TitleBar_PointerPressed;

        _activeNavButton = NavDashboard;
        NavigateToDashboard();

        Opened += (_, _) =>
        {
            _ = UpdateInboxBadgeAsync();
            var t = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            t.Tick += async (_, _) => await UpdateInboxBadgeAsync();
            t.Start();
        };

        // Abonnement verversen als de gebruiker terugkomt (max 1× per 30 s), zoals Windows.
        Activated += async (_, _) =>
        {
            if ((DateTime.UtcNow - _lastCreditsRefresh).TotalSeconds < 30) return;
            _lastCreditsRefresh = DateTime.UtcNow;
            if (App.Hotkeys != null) await App.Hotkeys.RefreshSubscriptionAsync();
            if (ContentArea.Content is DashboardView dash) dash.ForceRefreshStats();
        };

        // Sluiten = naar de achtergrond (menubalk), tenzij "minimaliseer naar tray" uit staat.
        Closing += (_, e) =>
        {
            if (_reallyClose) return;
            e.Cancel = true;
            if (_config.MinimizeToTray) Hide();
            else App.Quit();
        };
    }

    public void CloseForReal()
    {
        _reallyClose = true;
        Close();
    }

    public void UpdateProfileLabel()
    {
        if (!string.IsNullOrEmpty(_config.UserEmail)) UserEmailText.Text = _config.UserEmail;
    }

    // ═══ NAVIGATIE ═══

    private void SetActiveNav(Button button)
    {
        _activeNavButton?.Classes.Remove("active");
        button.Classes.Add("active");
        _activeNavButton = button;
    }

    private void NavigateTo(Button nav, Control view)
    {
        SetActiveNav(nav);
        ContentArea.Content = view;
    }

    public void NavigateToDashboard() => NavigateTo(NavDashboard, new DashboardView(_config, _api));

    public void NavigateToHotkeyEditor(string? hotkeyIdToSelect = null)
    {
        var view = new HotkeyEditorView(_config, _api, hotkeyIdToSelect);
        view.NavigateBack += (_, _) =>
        {
            NavigateToDashboard();
            ReloadHotkeys();
        };
        NavigateTo(NavHotkeys, view);
    }

    public void NavigateToHotkeys() => NavigateToHotkeyEditor();

    public void NavigateToHotkeysWithDemo()
    {
        var demo = _config.Hotkeys.FirstOrDefault(h => h.Id == Services.HotkeyRuntime.DemoHotkeyId) ?? _config.Hotkeys.FirstOrDefault();
        NavigateToHotkeyEditor(demo?.Id);
    }

    public void NavigateToStyleLibrary() => NavigateTo(NavStyles, new StyleLibraryView(_config, _api));
    public void NavigateToStyleLibraryPublic() => NavigateToStyleLibrary();

    public void NavigateToStyleEditor(WritingStyle? existingStyle = null)
    {
        var editor = new StyleEditorView(_config, _api, existingStyle);
        editor.NavigateBack += (_, _) => NavigateToStyleLibrary();
        NavigateTo(NavStyles, editor);
    }

    public void NavigateToTemplates()
    {
        var view = new PromptTemplatesView(_config, _api);
        view.NavigateBack += (_, e) =>
        {
            NavigateToHotkeyEditor(e.HotkeyId);
            ReloadHotkeys();
        };
        NavigateTo(NavTemplates, view);
    }
    public void NavigateToTemplatesPublic() => NavigateToTemplates();

    public void NavigateToIntegrations() => NavigateTo(NavIntegrations, new IntegrationsView(_api));
    public void NavigateToKoppelingen() => NavigateTo(NavKoppelingen, new KoppelingenView(_config, _api));
    public void NavigateToCloudSync() => NavigateTo(NavCloudSync, new CloudSyncView(_config, _api));
    public void NavigateToSettings() => NavigateTo(NavSettings, new SettingsView(_config));
    public void NavigateToAssistant() => NavigateTo(NavAssistant, new AssistantSettingsView(_config, _api));

    /// <summary>Herregistreert alle globale sneltoetsen (na een wijziging).</summary>
    public void ReloadHotkeys() => App.Hotkeys?.RegisterAll();

    public void OpenPricingPage() => App.Hotkeys?.OpenPricingPage();

    private void NavDashboard_Click(object? s, RoutedEventArgs e) => NavigateToDashboard();
    private void NavHotkeys_Click(object? s, RoutedEventArgs e) => NavigateToHotkeyEditor();
    private void NavStyles_Click(object? s, RoutedEventArgs e) => NavigateToStyleLibrary();
    private void NavTemplates_Click(object? s, RoutedEventArgs e) => NavigateToTemplates();
    private void NavIntegrations_Click(object? s, RoutedEventArgs e) => NavigateToIntegrations();
    private void NavKoppelingen_Click(object? s, RoutedEventArgs e) => NavigateToKoppelingen();
    private void NavCloudSync_Click(object? s, RoutedEventArgs e) => NavigateToCloudSync();
    private void NavSettings_Click(object? s, RoutedEventArgs e) => NavigateToSettings();
    private void NavAssistant_Click(object? s, RoutedEventArgs e) => NavigateToAssistant();

    /// <summary>Profiel opent de accountpagina op de website (zoals Windows).</summary>
    private void NavProfile_Click(object? s, RoutedEventArgs e) => Ui.OpenUrl("https://maikey.nl/account.html");

    private void Logout_Click(object? s, RoutedEventArgs e) => App.Logout();

    /// <summary>Badge op het Koppelingen-menu met het aantal openstaande werklijst-tickets.</summary>
    public async Task UpdateInboxBadgeAsync()
    {
        try
        {
            var res = await _api.ListInboxAsync();
            int count = res?.Success == true ? res.Items.Count : 0;
            InboxBadgeText.Text = count > 99 ? "99+" : count.ToString();
            InboxBadge.IsVisible = count > 0;
        }
        catch { /* badge mag nooit de app breken */ }
    }

    // ═══ ONBOARDING-TOUR ═══

    private sealed record TourStep(string? Nav, string? Target, string TitleKey, string BodyKey);

    private TourOverlay? _tour;
    private int _tourStep;
    private string _tourId = "";
    private List<TourStep> _steps = new();

    private static readonly Dictionary<string, string[]> TargetAliases = new()
    {
        ["NavStyleLibrary"] = new[] { "NavStyles" },
        ["NavPrompts"] = new[] { "NavTemplates" },
    };

    public void StartTour(string tourId)
    {
        var steps = tourId switch
        {
            "hotkey" => new List<TourStep>
            {
                new(null, "NavHotkeys", "Tour_Hotkey_1_Title", "Tour_Hotkey_1_Body"),
                new("hotkeys", "AddHotkeyBtn", "Tour_Hotkey_2_Title", "Tour_Hotkey_2_Body"),
                new("hotkeys_demo", "NameTextBox", "Tour_Hotkey_3_Title", "Tour_Hotkey_3_Body"),
                new(null, "HotkeyTextBox", "Tour_Hotkey_4_Title", "Tour_Hotkey_4_Body_Mac"),
                new(null, "CustomPromptTextBox", "Tour_Hotkey_5_Title", "Tour_Hotkey_5_Body"),
                new(null, "ModelComboBox", "Tour_Hotkey_6_Title", "Tour_Hotkey_6_Body"),
                new(null, "StyleComboBox", "Tour_Hotkey_7_Title", "Tour_Hotkey_7_Body"),
                new(null, "CreateWithAIButton", "Tour_Hotkey_8_Title", "Tour_Hotkey_8_Body"),
                new(null, "OptimizeButton", "Tour_Hotkey_9_Title", "Tour_Hotkey_9_Body"),
                new(null, "OutputModeComboBox", "Tour_Hotkey_10_Title", "Tour_Hotkey_10_Body"),
                new(null, "AskForContextCheckbox", "Tour_Hotkey_11_Title", "Tour_Hotkey_11_Body"),
                new(null, "IncludeImagesCheckbox", "Tour_Hotkey_12_Title", "Tour_Hotkey_12_Body"),
                new(null, "UseInputInsteadOfSelectionCheckbox", "Tour_Hotkey_13_Title", "Tour_Hotkey_13_Body"),
                new(null, "UseCustomAIParamsCheckbox", "Tour_Hotkey_14_Title", "Tour_Hotkey_14_Body"),
                new(null, "SaveHotkeyBtn", "Tour_Hotkey_15_Title", "Tour_Hotkey_15_Body"),
            },
            "style" or "style_new" => new List<TourStep>
            {
                new("styles", "NewStyleBtn", "Tour_StyleNew_1_Title", "Tour_StyleNew_1_Body"),
                new("styles_new", "NameTextBox", "Tour_StyleNew_2_Title", "Tour_StyleNew_2_Body"),
                new(null, "UsageContextTextBox", "Tour_StyleNew_3_Title", "Tour_StyleNew_3_Body"),
                new(null, "AddExampleBtn", "Tour_StyleNew_4_Title", "Tour_StyleNew_4_Body"),
                new(null, "GenerateStyleBtn", "Tour_StyleNew_5_Title", "Tour_StyleNew_5_Body"),
                new(null, "SaveBtn", "Tour_StyleNew_6_Title", "Tour_StyleNew_6_Body"),
            },
            "style_edit" => new List<TourStep>
            {
                new("styles", "StylesListView", "Tour_StyleEdit_1_Title", "Tour_StyleEdit_1_Body"),
                new(null, "AddExampleBtn", "Tour_StyleEdit_2_Title", "Tour_StyleEdit_2_Body"),
                new(null, "GenerateStyleBtn", "Tour_StyleEdit_3_Title", "Tour_StyleEdit_3_Body"),
                new(null, "SaveBtn", "Tour_StyleEdit_4_Title", "Tour_StyleEdit_4_Body"),
            },
            "template" => new List<TourStep>
            {
                new(null, "NavPrompts", "Tour_Template_1_Title", "Tour_Template_1_Body"),
                new("templates", "Template1", "Tour_Template_2_Title", "Tour_Template_2_Body"),
            },
            "pin" => new List<TourStep> { new(null, null, "Tour_Pin_Title_Mac", "Tour_Pin_Body_Mac") },
            _ => new List<TourStep>()
        };
        if (steps.Count == 0) return;

        _tourId = tourId;
        _steps = steps;
        _tourStep = 0;
        if (_tour == null)
        {
            _tour = new TourOverlay
            {
                OnNext = () => { _tourStep++; if (_tourStep >= _steps.Count) EndTour(true); else ShowTourStep(); },
                OnPrev = () => { if (_tourStep > 0) { _tourStep--; ShowTourStep(); } },
                OnClose = () => EndTour(false)
            };
            Grid.SetColumnSpan(_tour, 2);
            RootGrid.Children.Add(_tour);
        }
        ShowTourStep();
    }

    private async void ShowTourStep()
    {
        if (_tour == null) return;
        var step = _steps[_tourStep];

        if (step.Nav != null)
        {
            NavigateForTour(step.Nav);
            await Task.Delay(180);
        }
        else await Task.Delay(30);
        if (_tour == null) return;

        var target = ResolveTarget(step.Target);
        if (target != null)
        {
            CenterInScrollViewer(target);
            await Task.Delay(150);
            if (_tour == null) return;
        }

        Rect? spot = null;
        if (target != null && target.IsVisible)
        {
            var tl = target.TranslatePoint(new Point(0, 0), _tour);
            if (tl.HasValue) spot = new Rect(tl.Value, target.Bounds.Size);
        }
        _tour.ShowStep(spot, L.T(step.TitleKey), L.T(step.BodyKey), _tourStep, _steps.Count);
    }

    private static void CenterInScrollViewer(Control target)
    {
        var sv = target.FindAncestorOfType<ScrollViewer>();
        if (sv == null) return;
        var pos = target.TranslatePoint(new Point(0, 0), sv);
        if (!pos.HasValue) return;
        var center = sv.Offset.Y + pos.Value.Y + target.Bounds.Height / 2;
        var newY = Math.Max(0, Math.Min(center - sv.Viewport.Height / 2, Math.Max(0, sv.Extent.Height - sv.Viewport.Height)));
        sv.Offset = new Vector(sv.Offset.X, newY);
    }

    private Control? ResolveTarget(string? name)
    {
        if (name == null) return null;
        var candidates = TargetAliases.TryGetValue(name, out var a) ? a.Prepend(name) : new[] { name };
        foreach (var n in candidates)
        {
            var found = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == n && c.IsVisible);
            if (found != null) return found;
        }
        return null;
    }

    private void NavigateForTour(string nav)
    {
        switch (nav)
        {
            case "hotkeys":
            case "hotkeys_demo":
                if (ContentArea.Content is not HotkeyEditorView)
                {
                    if (nav == "hotkeys_demo") NavigateToHotkeysWithDemo();
                    else NavigateToHotkeyEditor();
                }
                else if (nav == "hotkeys_demo" && ContentArea.Content is HotkeyEditorView hv)
                    hv.SelectHotkey(Services.HotkeyRuntime.DemoHotkeyId);
                break;
            case "styles":
                if (ContentArea.Content is not StyleLibraryView) NavigateToStyleLibrary();
                break;
            case "styles_new":
                if (ContentArea.Content is not StyleEditorView) NavigateToStyleEditor();
                break;
            case "templates":
                if (ContentArea.Content is not PromptTemplatesView) NavigateToTemplates();
                break;
        }
    }

    private void EndTour(bool completed)
    {
        if (_tour != null)
        {
            RootGrid.Children.Remove(_tour);
            _tour = null;
        }
        if (completed && !string.IsNullOrEmpty(_tourId))
            _config.SetOnboardingStepDone(_tourId == "style_new" || _tourId == "style_edit" ? "style" : _tourId);
        RefreshDashboardOnboarding();
    }

    public void RefreshDashboardOnboarding()
    {
        if (ContentArea.Content is DashboardView dashboard) dashboard.RefreshOnboarding();
    }

    // ═══ VENSTER ═══

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                BeginMoveDrag(e);
        }
    }
}
