using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
        NavigateTo(Placeholder("Stijl Library"));
    }

    private void NavTemplates_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavTemplates);
        NavigateTo(Placeholder("Templates"));
    }

    private void NavIntegrations_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavIntegrations);
        NavigateTo(Placeholder("Integraties"));
    }

    private void NavCloudSync_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(NavCloudSync);
        NavigateTo(Placeholder("Cloud Sync"));
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
