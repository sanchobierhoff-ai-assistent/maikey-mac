using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using mAIkey.Core.Services;

namespace mAIkey.Desktop;

public partial class App : Application
{
    public static ConfigService Config { get; private set; } = null!;
    public static ApiClient Api { get; private set; } = null!;
    public static Services.HotkeyRuntime? Hotkeys { get; private set; }

    /// <summary>Zet het app-thema op donker (standaard) of taupe/licht.</summary>
    public static void ApplyTheme(string? theme)
    {
        if (Current != null)
            Current.RequestedThemeVariant =
                string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
    }

    private static void StartHotkeys()
    {
        try
        {
            Hotkeys ??= new Services.HotkeyRuntime(Config, Api);
            Hotkeys.Start();
        }
        catch { /* hotkeys mogen de app nooit blokkeren */ }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Initialize services
        Config = new ConfigService();
        Api = new ApiClient(Config.ApiBaseUrl);

        // Apply localization
        L.Apply(Config.InterfaceLanguage);

        // Pas het opgeslagen thema toe (donker is de standaard).
        ApplyTheme(Config.Theme);

        // Restore auth token if available
        if (!string.IsNullOrEmpty(Config.AuthToken))
            Api.SetAuthToken(Config.AuthToken);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (string.IsNullOrEmpty(Config.AuthToken))
            {
                var login = new Windows.LoginWindow();
                login.LoginSucceeded += (s, e) =>
                {
                    desktop.MainWindow = new MainWindow();
                    desktop.MainWindow.Show();
                    login.Close();
                    StartHotkeys();
                };
                desktop.MainWindow = login;
            }
            else
            {
                desktop.MainWindow = new MainWindow();
                StartHotkeys();
            }
        }

        // Auto-update op de achtergrond (stil; doet niets bij een losse dev-build).
        _ = CheckForUpdatesAsync();

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Controleert bij het opstarten op een nieuwere macOS-release en past die
    /// automatisch toe (download + herstart). Faalt volledig stil zodat de app
    /// altijd normaal blijft starten.
    /// </summary>
    private static async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        try
        {
            var info = await Services.UpdateService.CheckForUpdateAsync(Config.ApiBaseUrl);
            if (info != null)
                await Services.UpdateService.DownloadAndApplyAsync(info);
        }
        catch
        {
            // Nooit de app-start blokkeren op een update-fout.
        }
    }
}
