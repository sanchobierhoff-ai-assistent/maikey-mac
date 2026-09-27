using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigService _config;
    private bool _loading;

    public SettingsView() : this(App.Config) { }

    public SettingsView(ConfigService config)
    {
        InitializeComponent();
        _config = config;
        MaxCharsTextBox.AddHandler(TextInputEvent, NumberOnly_PreviewTextInput, RoutingStrategies.Tunnel);
        ApplyLocalization();
        LoadSettings();
        // Toestemmingen kunnen buiten de app om veranderen → bij terugkomen verversen.
        AttachedToVisualTree += (_, _) => RefreshPermissions();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void ApplyLocalization()
    {
        PageEyebrow.Text              = L.T("Settings_Title");
        PageTitle.Text                = L.T("Settings_Heading");
        PageSubtitle.Text             = L.T("Settings_Subtitle");
        CardThemeHeader.Text          = L.T("Settings_Card_Theme");
        ThemeDarkRadio.Content        = L.T("Settings_Theme_Dark");
        ThemeLightRadio.Content       = L.T("Settings_Theme_Light");
        CardBehaviorHeader.Text       = L.T("Settings_Card_Behavior");
        CardLangHeader.Text           = L.T("Settings_Card_Lang");
        AutostartLabel.Text           = Loc.T("Settings_Autostart_Mac", "Starten bij inloggen");
        AutostartSubLabel.Text        = L.T("Settings_AutostartSub");
        ShowIndicatorLabel.Text       = L.T("Settings_ShowIndicator");
        ShowIndicatorSubLabel.Text    = L.T("Settings_ShowIndicatorSub");
        SoundLabel.Text               = L.T("Settings_Sound");
        SoundSubLabel.Text            = L.T("Settings_SoundSub");
        LangInterfaceLabel.Text       = L.T("Settings_LangInterface");
        LangInterfaceSubLabel.Text    = L.T("Settings_LangSubtitle");
        CardContentLimitsHeader.Text  = L.T("Settings_ContentLimits");
        ContentLimitsSub.Text         = L.T("Settings_ContentLimitsSub");
        MaxImagesLabel.Text           = L.T("Settings_MaxImages");
        MaxImagesSubLabel.Text        = L.T("Settings_MaxImagesSub");
        MaxCharsLabel.Text            = L.T("Settings_MaxChars");
        MaxCharsSubLabel.Text         = L.T("Settings_MaxCharsSub");
        CardWindowHeader.Text         = L.T("Settings_WindowCard");
        MinimizeToTrayLabel.Text      = Loc.T("Settings_MinimizeToTray_Mac", "In de menubalk blijven bij sluiten");
        MinimizeToTraySubLabel.Text   = Loc.T("Settings_MinimizeToTraySub_Mac", "Sluiten verbergt het venster; mAIkey blijft actief via het menubalk-icoon");
        CardPermissionsHeader.Text    = Loc.T("Settings_Permissions_Mac", "macOS-toestemmingen");
        PermAccessibilityLabel.Text   = Loc.T("Settings_PermAccessibility_Mac", "Toegankelijkheid");
        PermScreenLabel.Text          = Loc.T("Settings_PermScreen_Mac", "Schermopname");
        CardTourHeader.Text           = L.T("Settings_Card_Tour");
        TourSubLabel.Text             = L.T("Settings_TourSub");
        TourHotkeyLabel.Text          = L.T("Settings_Tour_Hotkey");
        TourHotkeySubLabel.Text       = L.T("Settings_Tour_HotkeySub");
        TourHotkeyStartBtn.Content    = L.T("Settings_Tour_Start");
        TourStyleLabel.Text           = L.T("Settings_Tour_Style");
        TourStyleSubLabel.Text        = L.T("Settings_Tour_StyleSub");
        TourStyleStartBtn.Content     = L.T("Settings_Tour_Start");
        TourTemplateLabel.Text        = L.T("Settings_Tour_Template");
        TourTemplateSubLabel.Text     = L.T("Settings_Tour_TemplateSub");
        TourTemplateStartBtn.Content  = L.T("Settings_Tour_Start");
        TourPinLabel.Text             = Loc.T("Settings_Tour_Pin_Mac", "mAIkey in het Dock houden");
        TourPinSubLabel.Text          = L.T("Settings_Tour_PinSub");
        TourPinStartBtn.Content       = L.T("Settings_Tour_Start");
    }

    private void LoadSettings()
    {
        _loading = true;

        // Gedrag — de echte login-item-status is leidend (gebruiker kan hem ook in Systeeminstellingen wijzigen).
        var autoStart = App.Platform.AutoStartService;
        AutostartCheckBox.IsChecked = autoStart.IsAutoStartEnabled() || _config.StartWithWindows;
        ShowAiIndicatorCheckBox.IsChecked = _config.ShowAiIndicator;
        SoundCheckBox.IsChecked = _config.SoundOnComplete;

        // Taal
        SelectComboByTag(LanguageComboBox, _config.InterfaceLanguage);

        // Contentlimieten
        MaxImagesText.Text = _config.MaxImages.ToString();
        MaxCharsTextBox.Text = _config.MaxCharacters.ToString();

        // Venster
        MinimizeToTrayCheckBox.IsChecked = _config.MinimizeToTray;

        // Thema
        ThemeDarkRadio.IsChecked  = _config.Theme != "Light";
        ThemeLightRadio.IsChecked = _config.Theme == "Light";

        // Login-item bijwerken als de app verplaatst is (bv. na herinstallatie).
        if (_config.StartWithWindows) autoStart.SetAutoStart(true);

        _loading = false;
        RefreshPermissions();
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        var item = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == tag);
        if (item != null) combo.SelectedItem = item;
        else if (combo.ItemCount > 0) combo.SelectedIndex = 0;
    }

    // ── Gedrag ──

    private void Autostart_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool enable = AutostartCheckBox.IsChecked == true;
        _config.StartWithWindows = enable;
        App.Platform.AutoStartService.SetAutoStart(enable);
    }

    // Snelkoppelingen bestaan alleen op Windows (rijen zijn verborgen).
    private void DesktopShortcut_Changed(object? sender, RoutedEventArgs e) { }
    private void StartMenuShortcut_Changed(object? sender, RoutedEventArgs e) { }

    private void ShowAiIndicator_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.ShowAiIndicator = ShowAiIndicatorCheckBox.IsChecked == true;
    }

    private void Sound_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.SoundOnComplete = SoundCheckBox.IsChecked == true;
    }

    // ── Taal ──

    private async void Language_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item)
        {
            var newLang = item.Tag as string ?? "nl";
            if (newLang == _config.InterfaceLanguage) return;
            _config.InterfaceLanguage = newLang;
            await MkDialog.ShowInfo(L.T("Lang_ChangeRestartTitle"), L.T("Lang_ChangeRestart"), Owner);
        }
    }

    // ── Contentlimieten ──

    private void DecreaseMaxImages_Click(object? sender, RoutedEventArgs e)
    {
        if (_config.MaxImages > 1)
        {
            _config.MaxImages--;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }

    private void IncreaseMaxImages_Click(object? sender, RoutedEventArgs e)
    {
        if (_config.MaxImages < 10)
        {
            _config.MaxImages++;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }

    private void MaxCharsTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(MaxCharsTextBox.Text, out int value) && value >= 100)
            _config.MaxCharacters = value;
        else
            MaxCharsTextBox.Text = _config.MaxCharacters.ToString();
    }

    private void NumberOnly_PreviewTextInput(object? sender, TextInputEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    // ── Venster ──

    private void MinimizeToTray_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
    }

    // ── Thema ──

    private void ThemeDark_Checked(object? sender, RoutedEventArgs e)
    {
        if (_loading || ThemeDarkRadio.IsChecked != true) return;
        App.ApplyTheme("Dark");
        _config.Theme = "Dark";
    }

    private void ThemeLight_Checked(object? sender, RoutedEventArgs e)
    {
        if (_loading || ThemeLightRadio.IsChecked != true) return;
        App.ApplyTheme("Light");
        _config.Theme = "Light";
    }

    // ── macOS-toestemmingen ──

    private void RefreshPermissions()
    {
        bool ax = MacAccessibility.IsTrusted();
        PermAccessibilityStatus.Text = ax
            ? Loc.T("Settings_PermGranted_Mac", "✓ Toegestaan — mAIkey kan geselecteerde tekst lezen en het resultaat terugplakken.")
            : Loc.T("Settings_PermAccessibilityMissing_Mac", "Nodig om geselecteerde tekst te kopiëren en het resultaat terug te plakken.");
        PermAccessibilityBtn.Content = ax ? Loc.T("MacPerm_OpenSettings", "Open instellingen") : Loc.T("Settings_PermGrant_Mac", "Toestaan…");

        bool sc = MacAccessibility.HasScreenCapture();
        PermScreenStatus.Text = sc
            ? Loc.T("Settings_PermScreenGranted_Mac", "✓ Toegestaan — screenshots voor de assistent werken.")
            : Loc.T("Settings_PermScreenMissing_Mac", "Alleen nodig voor de screenshot-sneltoets van de assistent.");
        PermScreenBtn.Content = sc ? Loc.T("MacPerm_OpenSettings", "Open instellingen") : Loc.T("Settings_PermGrant_Mac", "Toestaan…");
    }

    private async void PermAccessibility_Click(object? sender, RoutedEventArgs e)
    {
        if (MacAccessibility.IsTrusted()) MacAccessibility.OpenAccessibilitySettings();
        else if (!MacAccessibility.EnsureTrusted()) await MacAccessibility.ExplainAsync();
        RefreshPermissions();
    }

    private async void PermScreen_Click(object? sender, RoutedEventArgs e)
    {
        if (MacAccessibility.HasScreenCapture()) MacAccessibility.OpenScreenCaptureSettings();
        else await MacAccessibility.EnsureScreenCaptureAsync(Owner);
        RefreshPermissions();
    }

    // ── Rondleidingen ──

    private void TourHotkey_Click(object? sender, RoutedEventArgs e) => Ui.Main?.StartTour("hotkey");
    private void TourStyle_Click(object? sender, RoutedEventArgs e) => Ui.Main?.StartTour("style");
    private void TourTemplate_Click(object? sender, RoutedEventArgs e) => Ui.Main?.StartTour("template");
    private void TourPin_Click(object? sender, RoutedEventArgs e) => Ui.Main?.StartTour("pin");
}
