using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Platform;
using mAIkey.Desktop.Services;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop;

public partial class App : Application
{
    public static ConfigService Config { get; private set; } = null!;
    public static ApiClient Api { get; private set; } = null!;
    public static PlatformServices Platform { get; private set; } = null!;
    public static HotkeyRuntime? Hotkeys { get; private set; }

    private static TokenRefreshService? _tokenRefresh;
    private TrayIcon? _trayIcon;

    /// <summary>Zet het app-thema op taupe/licht (standaard) of donker.</summary>
    public static void ApplyTheme(string? theme)
    {
        if (Current != null)
            Current.RequestedThemeVariant =
                string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Platform = PlatformServiceFactory.Create();
        DeviceIdentifier.Provider = Platform.DeviceIdentifier;

        Config = new ConfigService(Platform.TokenProtection);
        L.Apply(Config.InterfaceLanguage);
        ApplyTheme(Config.Theme);

        Api = new ApiClient(Config.ApiBaseUrl);
        if (!string.IsNullOrEmpty(Config.AuthToken))
            Api.SetAuthToken(Config.AuthToken);

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Logger.Log($"UI-fout: {e.Exception.GetType().Name}: {e.Exception.Message}");
            e.Handled = true;
            _ = MkDialog.ShowError(L.T("App_GlobalErrorTitle"),
                $"{L.T("App_GlobalError")}\n\n{e.Exception.Message}\n\n{L.T("App_GlobalErrorSub")}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Log($"Taak-fout: {e.Exception.GetBaseException().Message}");
            e.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Sluiten van het hoofdvenster = naar de achtergrond (menubalk), niet afsluiten.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (string.IsNullOrEmpty(Config.AuthToken) && !IsUiPreview)
                ShowLogin(desktop);
            else
                ShowMain(desktop);

            SetupTrayIcon(desktop);
            SetupDockReopen();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Alleen in Debug-builds: MAIKEY_UI_PREVIEW=1 opent het hoofdvenster zonder login en zonder
    /// opstartcontroles, om schermen te kunnen bekijken (ook op Windows, met stub-platformdiensten).
    /// </summary>
#if DEBUG
    public static bool IsUiPreview { get; } = Environment.GetEnvironmentVariable("MAIKEY_UI_PREVIEW") == "1";
#else
    public static bool IsUiPreview => false;
#endif

    public static void ShowLogin(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var login = new LoginWindow(Api, Config);
        desktop.MainWindow = login;
        login.Show();
    }

    /// <summary>Na een geslaagde login (e-mail, Google of Microsoft): tokens bewaren, hoofdvenster openen.</summary>
    public static void CompleteLogin(Window from, LoginResponse response)
    {
        Api.SetAuthToken(response.Token);
        Config.AuthToken = response.Token;
        Config.RefreshToken = response.RefreshToken;
        Config.UserEmail = response.User?.Email;
        Config.UserName = response.User?.Name;
        Config.SubscriptionTier = response.User?.Tier ?? response.User?.Subscription ?? "free";
        if (Ui.Desktop is { } desktop) ShowMain(desktop);
        from.Close();
    }

    /// <summary>Wissel tussen login- en registratievenster (houdt de app in leven).</summary>
    public static void SwitchWindow(Window from, Window to)
    {
        if (Ui.Desktop is { } desktop) desktop.MainWindow = to;
        to.Show();
        from.Close();
    }

    public static void ShowMain(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var main = new MainWindow();
        desktop.MainWindow = main;
        main.Show();

        Hotkeys ??= new HotkeyRuntime(Config, Api, Platform);
        Hotkeys.Start();
        Hotkeys.RegisterAll();

        if (!IsUiPreview) _ = StartupChecksAsync(main);
#if DEBUG
        if (IsUiPreview && UiSnapshot.Directory != null) _ = UiSnapshot.RunAsync(main);
#endif
    }

    /// <summary>
    /// Achtergrondtaken na het openen van het hoofdvenster (zoals MainWindow_Loaded op
    /// Windows): update-check, token verversen, abonnement, modellen-cache, uitgefaseerde modellen.
    /// </summary>
    private static async Task StartupChecksAsync(MainWindow main)
    {
        // Eerste start na installatie: meteen de systeemvraag voor Toegankelijkheid tonen,
        // zodat de eerste sneltoets direct werkt (daarna blijft de toestemming staan).
        if (!MacAccessibility.IsTrusted()) MacAccessibility.EnsureTrusted();

        _ = CheckForUpdatesAsync(main);
        await InitializeTokenRefreshAsync();
        if (Hotkeys != null) await Hotkeys.RefreshSubscriptionAsync();
        _ = ModelCatalogCache.EnsureLoadedAsync(Api);
        await ReconcileHotkeyModelsAsync(main);
    }

    private static async Task InitializeTokenRefreshAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(Config.AuthToken) || string.IsNullOrEmpty(Config.RefreshToken)) return;
            _tokenRefresh?.Dispose();
            _tokenRefresh = new TokenRefreshService(Api);
            _tokenRefresh.Initialize(Config.AuthToken, Config.RefreshToken, 30 * 24 * 60 * 60);
            _tokenRefresh.TokenRefreshed += (_, token) => Config.AuthToken = token;
            _tokenRefresh.TokenExpired += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                await MkDialog.ShowInfo(L.T("Dialog_SessionExpired_Title"), L.T("Dialog_SessionExpired_Msg"));
                Logout(askConfirmation: false);
            });
            await _tokenRefresh.ValidateAndRefreshOnStartupAsync();
        }
        catch (Exception ex) { Logger.Log("token verversen mislukt: " + ex.Message); }
    }

    /// <summary>Uitgefaseerde modellen in sneltoetsen omzetten naar hun vervanger en éénmalig melden.</summary>
    private static async Task ReconcileHotkeyModelsAsync(Window owner)
    {
        try
        {
            var hotkeys = Config.Hotkeys;
            var ids = hotkeys.Select(h => h.Model).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).Distinct().ToArray();
            if (ids.Length == 0) return;

            var resp = await Api.ResolveModelsAsync(ids);
            if (resp?.Success != true || resp.Resolutions == null) return;
            var changed = resp.Resolutions.Where(r => r.Changed && !string.IsNullOrEmpty(r.ReplacementId)).ToList();
            if (changed.Count == 0) return;

            var map = changed.ToDictionary(r => r.Id, r => r);
            bool any = false;
            foreach (var h in hotkeys)
                if (h.Model != null && map.TryGetValue(h.Model, out var res)) { h.Model = res.ReplacementId; any = true; }
            if (!any) return;

            Config.Hotkeys = hotkeys;
            Hotkeys?.RegisterAll();

            var lines = changed.Select(r => L.Tf("ModelReplaced_Line", r.FromName ?? r.Id, r.ReplacementName ?? r.ReplacementId ?? "")).Distinct();
            await MkDialog.ShowInfo(L.T("ModelReplaced_Title"), L.T("ModelReplaced_Body") + "\n\n" + string.Join("\n", lines), owner);
        }
        catch { /* optioneel */ }
    }

    /// <summary>Update-check met keuze (zoals CheckForAppUpdateAsync op Windows).</summary>
    private static async Task CheckForUpdatesAsync(Window owner)
    {
        try
        {
            var info = await UpdateService.CheckForUpdateAsync(Config.ApiBaseUrl);
            if (info == null) return;

            var message = L.Tf("Update_NewVersion", info.LatestVersion, ConfigService.CURRENT_VERSION);
            if (!string.IsNullOrEmpty(info.ReleaseNotes)) message += L.Tf("Update_ReleaseNotes", info.ReleaseNotes);

            bool doUpdate;
            if (info.ForceUpdate)
            {
                message += L.T("Update_ForceMessage");
                await MkDialog.ShowUpdate(L.T("Update_Required_Title"), message, true, owner, L.T("Update_NowBtn"));
                doUpdate = true;
            }
            else
            {
                message += L.T("Update_OptionalMessage");
                doUpdate = await MkDialog.ShowUpdate(L.T("Update_Available_Title"), message, false, owner,
                    L.T("Update_NowBtn"), L.T("Update_LaterBtn"));
            }
            if (!doUpdate) return;

            var loading = new LoadingIndicatorWindow();
            loading.Show();
            loading.SetMessage(L.T("Update_Downloading"));
            try
            {
                await UpdateService.DownloadAndApplyAsync(info, pct => loading.SetMessage($"{L.T("Update_Downloading")} {pct}%"));
            }
            catch (Exception ex)
            {
                Logger.Log("update mislukt: " + ex.Message);
                loading.FadeOutAndClose();
                await MkDialog.ShowError(L.T("Update_Failed_Title"), L.Tf("Update_Failed_Error", "https://maikey.nl/mac"), owner);
            }
        }
        catch { /* update-check mag nooit de app blokkeren */ }
    }

    /// <summary>Uitloggen: tokens wissen, sneltoetsen uit, terug naar het loginscherm.</summary>
    public static async void Logout(bool askConfirmation = true)
    {
        if (askConfirmation &&
            !await MkDialog.ShowConfirm(L.T("Dialog_Logout_Title"), L.T("Dialog_Logout_Msg")))
            return;

        _tokenRefresh?.Dispose();
        _tokenRefresh = null;
        Config.ClearAuth();
        Api.ClearAuthToken();
        Platform.HotkeyService.UnregisterAll();

        if (Ui.Desktop is { } desktop)
        {
            var old = desktop.MainWindow;
            ShowLogin(desktop);
            if (old is MainWindow mw) mw.CloseForReal();
        }
    }

    /// <summary>Toon (en activeer) het hoofdvenster — vanuit de menubalk of het Dock.</summary>
    public static void ShowMainWindow()
    {
        if (Ui.Desktop?.MainWindow is not { } w) return;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
    }

    public static void Quit()
    {
        try { Platform.HotkeyService.Dispose(); } catch { }
        Ui.Desktop?.Shutdown();
    }

    // ═══ Menubalk-icoon (Windows: tray-icoon) ═══

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var menu = new NativeMenu();
            var open = new NativeMenuItem(L.T("Tray_Open"));
            open.Click += (_, _) => ShowMainWindow();
            var assistant = new NativeMenuItem(L.T("Assistant_Title"));
            assistant.Click += async (_, _) => { if (Hotkeys != null) await Hotkeys.OnAssistantHotkeyPressed(); };
            var exit = new NativeMenuItem(L.T("Tray_Exit"));
            exit.Click += (_, _) => Quit();
            menu.Items.Add(open);
            menu.Items.Add(assistant);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exit);

            _trayIcon = new TrayIcon
            {
                ToolTipText = L.T("Tray_Title"),
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://mAIkey.Desktop/Resources/maikey-128.png"))),
                Menu = menu,
                IsVisible = true
            };
            _trayIcon.Clicked += (_, _) => ShowMainWindow();
            TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
        }
        catch (Exception ex) { Logger.Log("menubalk-icoon mislukt: " + ex.Message); }
    }

    /// <summary>Klik op het Dock-icoon terwijl het venster verborgen is → venster terug.</summary>
    private void SetupDockReopen()
    {
        try
        {
            if (this.TryGetFeature<IActivatableLifetime>() is { } lifetime)
                lifetime.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen) ShowMainWindow();
                };
        }
        catch { }
    }
}
