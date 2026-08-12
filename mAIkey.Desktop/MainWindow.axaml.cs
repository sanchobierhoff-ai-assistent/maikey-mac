using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using mAIkey.Core.Services;

namespace mAIkey.Desktop;

public partial class MainWindow : Window
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private Button? _activeNavButton;

    public MainWindow()
    {
        InitializeComponent();

        _config = App.Config;
        _api = App.Api;

        if (UserEmailText != null)
            UserEmailText.Text = _config.UserEmail ?? "Profiel";

        LogoutBtn.Click += LogoutBtn_Click;

        // Titelbalk slepen
        TitleBarArea.PointerPressed += TitleBar_PointerPressed;
        ContentTitleBar.PointerPressed += TitleBar_PointerPressed;

        // Start op dashboard
        _activeNavButton = NavDashboard;
        NavigateTo(new Views.DashboardView());
    }

    // ═══ NAVIGATIE ═══

    private void SetActiveNav(Button button)
    {
        _activeNavButton?.Classes.Remove("active");
        button.Classes.Add("active");
        _activeNavButton = button;
    }

    private void NavigateTo(Control view)
    {
        ContentArea.Content = view;
    }

    /// <summary>Publiek: open de hotkey-editor (aangeroepen vanuit andere views, bv. Dashboard).</summary>
    public void ShowHotkeyEditor()
    {
        SetActiveNav(NavHotkeys);
        NavigateTo(new Views.HotkeyEditorView());
    }

    private Control Placeholder(string title) => new TextBlock
    {
        Text = title + " — Binnenkort beschikbaar",
        Foreground = new SolidColorBrush(Color.Parse("#9A9AA3")),
        FontSize = 16,
        Margin = new Avalonia.Thickness(24),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private void NavDashboard_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavDashboard);
        NavigateTo(new Views.DashboardView());
    }

    private void NavHotkeys_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavHotkeys);
        NavigateTo(new Views.HotkeyEditorView());
    }

    private void NavStyles_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavStyles);
        NavigateTo(new Views.StyleLibraryView());
    }

    private void NavTemplates_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavTemplates);
        NavigateTo(new Views.PromptTemplatesView());
    }

    private void NavIntegrations_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavIntegrations);
        NavigateTo(new Views.IntegrationsView());
    }

    private void NavCloudSync_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavCloudSync);
        NavigateTo(new Views.CloudSyncView());
    }

    private void NavSettings_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavSettings);
        NavigateTo(new Views.SettingsView());
    }

    private void NavProfile_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavSettings);
        NavigateTo(new Views.SettingsView());
    }

    private void NavAssistant_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavAssistant);
        NavigateTo(new Views.AssistantSettingsView());
    }

    // ═══ ONBOARDING-TOUR ═══

    private sealed record TourStep(string? Nav, string? Target, string TitleKey, string BodyKey);

    private Views.TourOverlay? _tour;
    private int _tourStep;
    private System.Collections.Generic.List<TourStep> _steps = new();

    // Windows element-naam → mogelijke Mac-namen (eerste die in beeld staat wint).
    private static readonly System.Collections.Generic.Dictionary<string, string[]> TargetMap = new()
    {
        ["NavHotkeys"] = new[] { "NavHotkeys" },
        ["NavStyleLibrary"] = new[] { "NavStyles" },
        ["NavPrompts"] = new[] { "NavTemplates" },
        ["AddHotkeyBtn"] = new[] { "AddHotkeyBtn" },
        ["NameTextBox"] = new[] { "HotkeyNameBox", "NameBox" },
        ["HotkeyTextBox"] = new[] { "HotkeyComboBox" },
        ["CustomPromptTextBox"] = new[] { "PromptBox" },
        ["ModelComboBox"] = new[] { "ModelComboBox" },
        ["StyleComboBox"] = new[] { "StyleComboBox" },
        ["CreateWithAIButton"] = new[] { "AiBuilderBtn" },
        ["OptimizeButton"] = new[] { "OptimizeBtn" },
        ["OutputModeComboBox"] = new[] { "OutputModeComboBox" },
        ["AskForContextCheckbox"] = new[] { "AskContextCheck" },
        ["IncludeImagesCheckbox"] = new[] { "IncludeImagesCheck" },
        ["UseInputInsteadOfSelectionCheckbox"] = new[] { "UseInputCheck" },
        ["UseCustomAIParamsCheckbox"] = System.Array.Empty<string>(),
        ["SaveHotkeyBtn"] = new[] { "SaveBtn" },
        ["NewStyleBtn"] = new[] { "NewStyleBtn" },
        ["UsageContextTextBox"] = new[] { "UsageBox" },
        ["AddExampleBtn"] = new[] { "AddExampleBtn" },
        ["GenerateStyleBtn"] = new[] { "GenerateBtn" },
        ["SaveBtn"] = new[] { "StyleSaveBtn" },
        ["Template1"] = System.Array.Empty<string>(),
    };

    public void StartHotkeyTour() => StartTour(new()
    {
        new(null, "NavHotkeys", "Tour_Hotkey_1_Title", "Tour_Hotkey_1_Body"),
        new("hotkeys", "AddHotkeyBtn", "Tour_Hotkey_2_Title", "Tour_Hotkey_2_Body"),
        new("hotkeys_demo", "NameTextBox", "Tour_Hotkey_3_Title", "Tour_Hotkey_3_Body"),
        new(null, "HotkeyTextBox", "Tour_Hotkey_4_Title", "Tour_Hotkey_4_Body"),
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
    });

    public void StartStyleTour() => StartTour(new()
    {
        new("styles", "NewStyleBtn", "Tour_StyleNew_1_Title", "Tour_StyleNew_1_Body"),
        new("styles_new", "NameTextBox", "Tour_StyleNew_2_Title", "Tour_StyleNew_2_Body"),
        new(null, "UsageContextTextBox", "Tour_StyleNew_3_Title", "Tour_StyleNew_3_Body"),
        new(null, "AddExampleBtn", "Tour_StyleNew_4_Title", "Tour_StyleNew_4_Body"),
        new(null, "GenerateStyleBtn", "Tour_StyleNew_5_Title", "Tour_StyleNew_5_Body"),
        new(null, "SaveBtn", "Tour_StyleNew_6_Title", "Tour_StyleNew_6_Body"),
    });

    public void StartTemplateTour() => StartTour(new()
    {
        new(null, "NavPrompts", "Tour_Template_1_Title", "Tour_Template_1_Body"),
        new("templates", "Template1", "Tour_Template_2_Title", "Tour_Template_2_Body"),
    });

    public void StartPinTour() => StartTour(new()
    {
        new(null, null, "Tour_Pin_Title", "Tour_Pin_Body"),
    });

    private void StartTour(System.Collections.Generic.List<TourStep> steps)
    {
        _steps = steps;
        _tourStep = 0;
        if (_tour == null)
        {
            _tour = new Views.TourOverlay
            {
                OnNext = () => { _tourStep++; if (_tourStep >= _steps.Count) EndTour(); else ShowTourStep(); },
                OnPrev = () => { if (_tourStep > 0) { _tourStep--; ShowTourStep(); } },
                OnClose = EndTour
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

        // Navigeer indien nodig en geef de lay-out even tijd.
        if (step.Nav != null)
        {
            NavigateForTour(step.Nav);
            await System.Threading.Tasks.Task.Delay(160);
        }
        else
        {
            await System.Threading.Tasks.Task.Delay(30);
        }
        if (_tour == null) return;

        var target = ResolveTarget(step.Target);
        if (target != null)
        {
            // Centreer het doel in het scroll-gebied (zodat er ruimte is voor de ballon).
            CenterInScrollViewer(target);
            await System.Threading.Tasks.Task.Delay(150);
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

    /// <summary>Scroll het scroll-gebied zo dat het doel verticaal gecentreerd staat.</summary>
    private static void CenterInScrollViewer(Control target)
    {
        var sv = target.FindAncestorOfType<ScrollViewer>();
        if (sv == null) return;
        var pos = target.TranslatePoint(new Point(0, 0), sv);
        if (!pos.HasValue) return;

        var targetCenter = sv.Offset.Y + pos.Value.Y + target.Bounds.Height / 2;
        var newY = targetCenter - sv.Viewport.Height / 2;
        var max = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
        newY = Math.Max(0, Math.Min(newY, max));
        sv.Offset = new Vector(sv.Offset.X, newY);
    }

    private Control? ResolveTarget(string? winName)
    {
        if (winName == null || !TargetMap.TryGetValue(winName, out var candidates)) return null;
        foreach (var mac in candidates)
        {
            var found = this.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => c.Name == mac && c.IsVisible);
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
                if (ContentArea.Content is not Views.HotkeyEditorView hv)
                {
                    hv = new Views.HotkeyEditorView();
                    SetActiveNav(NavHotkeys);
                    NavigateTo(hv);
                }
                if (nav == "hotkeys_demo") hv.TourAddDemo();
                break;
            case "styles":
            case "styles_new":
                if (ContentArea.Content is not Views.StyleLibraryView sv)
                {
                    sv = new Views.StyleLibraryView();
                    SetActiveNav(NavStyles);
                    NavigateTo(sv);
                }
                if (nav == "styles_new") sv.TourStartNew();
                break;
            case "templates":
                if (ContentArea.Content is not Views.PromptTemplatesView)
                {
                    SetActiveNav(NavTemplates);
                    NavigateTo(new Views.PromptTemplatesView());
                }
                break;
        }
    }

    private void EndTour()
    {
        if (_tour != null)
        {
            RootGrid.Children.Remove(_tour);
            _tour = null;
        }
    }

    // ═══ VENSTERKNOPPEN ═══

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeBtn_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeBtn_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // ═══ UITLOGGEN ═══

    private void LogoutBtn_Click(object? sender, RoutedEventArgs e)
    {
        _config.ClearAuth();
        _api.ClearAuthToken();

        var login = new Windows.LoginWindow();
        login.LoginSucceeded += (s, ev) =>
        {
            if (UserEmailText != null)
                UserEmailText.Text = _config.UserEmail ?? "Profiel";
            Show();
            login.Close();
        };
        login.Show();
        Hide();
    }
}
