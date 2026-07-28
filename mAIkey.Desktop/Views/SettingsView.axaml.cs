using Avalonia.Controls;
using Avalonia.Interactivity;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigService _config;
    private bool _loading;

    public SettingsView()
    {
        InitializeComponent();
        _config = App.Config;
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object? sender, RoutedEventArgs e)
    {
        _loading = true;

        // Taal
        foreach (ComboBoxItem item in LanguageComboBox.Items)
            if (item.Tag?.ToString() == _config.InterfaceLanguage)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        LanguageComboBox.SelectionChanged += (s, e) =>
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                var lang = item.Tag?.ToString() ?? "nl";
                _config.InterfaceLanguage = lang;
                L.Apply(lang);
            }
        };

        // Thema (radio)
        bool isLight = string.Equals(_config.Theme, "Light", System.StringComparison.OrdinalIgnoreCase);
        ThemeLightRadio.IsChecked = isLight;
        ThemeDarkRadio.IsChecked = !isLight;
        ThemeDarkRadio.IsCheckedChanged += (s, e) => { if (ThemeDarkRadio.IsChecked == true) SetTheme("Dark"); };
        ThemeLightRadio.IsCheckedChanged += (s, e) => { if (ThemeLightRadio.IsChecked == true) SetTheme("Light"); };

        // Gedrag + venster
        ShowIndicatorCheck.IsChecked = _config.ShowAiIndicator;
        SoundCheck.IsChecked = _config.SoundOnComplete;
        MinimizeToTrayCheck.IsChecked = _config.MinimizeToTray;

        ShowIndicatorCheck.IsCheckedChanged += (s, e) => _config.ShowAiIndicator = ShowIndicatorCheck.IsChecked ?? true;
        SoundCheck.IsCheckedChanged += (s, e) => _config.SoundOnComplete = SoundCheck.IsChecked ?? false;
        MinimizeToTrayCheck.IsCheckedChanged += (s, e) => _config.MinimizeToTray = MinimizeToTrayCheck.IsChecked ?? true;

        // Contentlimieten
        MaxImagesText.Text = _config.MaxImages.ToString();
        MaxCharsBox.Text = _config.MaxCharacters.ToString();
        MaxCharsBox.LostFocus += (s, e) =>
        {
            if (int.TryParse(MaxCharsBox.Text, out var val) && val > 0)
                _config.MaxCharacters = val;
        };

        // Account
        AccountEmailRun.Text = _config.UserEmail ?? "onbekend";
        AccountTierRun.Text = (_config.SubscriptionTier ?? "free").ToUpperInvariant();

        _loading = false;
    }

    private void SetTheme(string theme)
    {
        if (_loading) return;
        _config.Theme = theme;
        App.ApplyTheme(theme);
    }

    private void ImgMinus_Click(object? sender, RoutedEventArgs e)
    {
        if (_config.MaxImages > 0)
        {
            _config.MaxImages -= 1;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }

    private void ImgPlus_Click(object? sender, RoutedEventArgs e)
    {
        if (_config.MaxImages < 10)
        {
            _config.MaxImages += 1;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }
}
