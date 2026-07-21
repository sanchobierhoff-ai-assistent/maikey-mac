using Avalonia.Controls;
using Avalonia.Interactivity;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigService _config;

    public SettingsView()
    {
        InitializeComponent();
        _config = App.Config;

        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object? sender, RoutedEventArgs e)
    {
        // Language
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag?.ToString() == _config.InterfaceLanguage)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
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

        // Thema
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (item.Tag?.ToString() == (_config.Theme ?? "Dark"))
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
        ThemeComboBox.SelectionChanged += (s, e) =>
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item)
            {
                var theme = item.Tag?.ToString() ?? "Dark";
                _config.Theme = theme;
                App.ApplyTheme(theme);
            }
        };

        // Checkboxes
        MinimizeToTrayCheck.IsChecked = _config.MinimizeToTray;
        ShowIndicatorCheck.IsChecked = _config.ShowAiIndicator;
        SoundCheck.IsChecked = _config.SoundOnComplete;

        MinimizeToTrayCheck.IsCheckedChanged += (s, e) =>
            _config.MinimizeToTray = MinimizeToTrayCheck.IsChecked ?? true;
        ShowIndicatorCheck.IsCheckedChanged += (s, e) =>
            _config.ShowAiIndicator = ShowIndicatorCheck.IsChecked ?? true;
        SoundCheck.IsCheckedChanged += (s, e) =>
            _config.SoundOnComplete = SoundCheck.IsChecked ?? false;

        // Content limits
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
    }

    private void ImgMinus_Click(object? sender, RoutedEventArgs e)
    {
        var val = _config.MaxImages;
        if (val > 0)
        {
            _config.MaxImages = val - 1;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }

    private void ImgPlus_Click(object? sender, RoutedEventArgs e)
    {
        var val = _config.MaxImages;
        if (val < 10)
        {
            _config.MaxImages = val + 1;
            MaxImagesText.Text = _config.MaxImages.ToString();
        }
    }
}
