using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Models;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Views;

public partial class HotkeyEditorView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private HotkeyConfig? _selectedHotkey;
    private Border? _selectedListItem;
    private int _recordedModifiers;
    private int _recordedKey;

    public HotkeyEditorView()
    {
        InitializeComponent();
        _config = App.Config;
        _api = App.Api;

        // Slider value changed events
        TempSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Value")
                TempValueText.Text = TempSlider.Value.ToString("F1");
        };
        TokensSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Value")
                TokensValueText.Text = ((int)TokensSlider.Value).ToString();
        };

        Loaded += HotkeyEditorView_Loaded;
    }

    private void HotkeyEditorView_Loaded(object? sender, RoutedEventArgs e)
    {
        PopulateHotkeyList();
        LoadModels();
        LoadStyles();
    }

    // ═══ HOTKEY LIST ═══

    private void PopulateHotkeyList()
    {
        HotkeyListPanel.Children.Clear();
        var hotkeys = _config.Hotkeys;

        foreach (var hk in hotkeys)
        {
            var item = CreateHotkeyListItem(hk);
            HotkeyListPanel.Children.Add(item);
        }
    }

    private Border CreateHotkeyListItem(HotkeyConfig hk)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 1),
            Cursor = new Cursor(StandardCursorType.Hand),
            MinHeight = 48,
            Tag = hk.Id
        };

        var stack = new StackPanel { Spacing = 2 };

        var nameText = new TextBlock
        {
            Text = hk.Name,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#F2F2F5"))
        };

        var comboText = new TextBlock
        {
            Text = FormatHotkey(hk),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#5E5E66"))
        };

        if (hk.FrozenByDowngrade)
        {
            var frozen = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#EF444418")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 1),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            frozen.Child = new TextBlock
            {
                Text = "Bevroren",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#EF4444"))
            };
            stack.Children.Add(frozen);
        }

        stack.Children.Add(nameText);
        stack.Children.Add(comboText);
        border.Child = stack;

        border.PointerEntered += (s, e) =>
        {
            if (border != _selectedListItem)
                border.Background = new SolidColorBrush(Color.Parse("#18181C"));
        };
        border.PointerExited += (s, e) =>
        {
            if (border != _selectedListItem)
                border.Background = Brushes.Transparent;
        };
        border.PointerReleased += (s, e) => SelectHotkey(hk, border);

        return border;
    }

    private void SelectHotkey(HotkeyConfig hk, Border listItem)
    {
        // Deselect previous
        if (_selectedListItem != null)
            _selectedListItem.Background = Brushes.Transparent;

        _selectedHotkey = hk;
        _selectedListItem = listItem;
        listItem.Background = new SolidColorBrush(Color.Parse("#18181C"));

        // Show editor
        EmptyEditorState.IsVisible = false;
        EditorPanel.IsVisible = true;

        // Populate fields
        HotkeyNameBox.Text = hk.Name;
        _recordedModifiers = hk.ModifierKeys;
        _recordedKey = hk.Key;
        HotkeyComboBox.Text = FormatHotkey(hk);
        PromptBox.Text = hk.CustomPrompt;
        AskContextCheck.IsChecked = hk.AskForContext;
        UseInputCheck.IsChecked = hk.UseInputInsteadOfSelection;

        // Model
        SelectModelInComboBox(hk.Model ?? "gpt-4o-mini");

        // Style
        SelectStyleInComboBox(hk.StyleId);

        // Output mode
        foreach (ComboBoxItem item in OutputModeComboBox.Items)
        {
            if (item.Tag?.ToString() == hk.OutputMode)
            {
                OutputModeComboBox.SelectedItem = item;
                break;
            }
        }

        // AI parameters
        if (hk.CustomAIParameters != null)
        {
            TempSlider.Value = hk.CustomAIParameters.Temperature ?? 0.7;
            TokensSlider.Value = hk.CustomAIParameters.MaxTokens ?? 8000;
        }
        else
        {
            TempSlider.Value = 0.7;
            TokensSlider.Value = 8000;
        }
    }

    // ═══ ADD / SAVE / DELETE ═══

    private void AddHotkey_Click(object? sender, RoutedEventArgs e)
    {
        var newHk = new HotkeyConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Nieuwe hotkey",
            CustomPrompt = "Verbeter de volgende tekst.",
            Model = "gpt-4o-mini",
            OutputMode = "replace",
            Enabled = true
        };

        var hotkeys = _config.Hotkeys.ToList();
        hotkeys.Add(newHk);
        _config.Hotkeys = hotkeys.ToArray();

        PopulateHotkeyList();

        // Select the new hotkey
        var lastItem = HotkeyListPanel.Children.LastOrDefault() as Border;
        if (lastItem != null)
            SelectHotkey(newHk, lastItem);
    }

    private void SaveHotkey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null) return;

        _selectedHotkey.Name = HotkeyNameBox.Text ?? "Naamloos";
        _selectedHotkey.ModifierKeys = _recordedModifiers;
        _selectedHotkey.Key = _recordedKey;
        _selectedHotkey.CustomPrompt = PromptBox.Text;
        _selectedHotkey.AskForContext = AskContextCheck.IsChecked ?? false;
        _selectedHotkey.UseInputInsteadOfSelection = UseInputCheck.IsChecked ?? false;

        // Model
        if (ModelComboBox.SelectedItem is ComboBoxItem modelItem)
            _selectedHotkey.Model = modelItem.Tag?.ToString();

        // Style
        if (StyleComboBox.SelectedItem is ComboBoxItem styleItem)
            _selectedHotkey.StyleId = styleItem.Tag?.ToString();

        // Output mode
        if (OutputModeComboBox.SelectedItem is ComboBoxItem modeItem)
            _selectedHotkey.OutputMode = modeItem.Tag?.ToString() ?? "replace";

        // AI parameters
        _selectedHotkey.CustomAIParameters = new AIParameters
        {
            Temperature = TempSlider.Value,
            MaxTokens = (int)TokensSlider.Value
        };

        // Save all hotkeys
        var hotkeys = _config.Hotkeys.ToList();
        var idx = hotkeys.FindIndex(h => h.Id == _selectedHotkey.Id);
        if (idx >= 0)
            hotkeys[idx] = _selectedHotkey;
        _config.Hotkeys = hotkeys.ToArray();

        PopulateHotkeyList();
    }

    private void DeleteHotkey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null) return;

        var hotkeys = _config.Hotkeys.Where(h => h.Id != _selectedHotkey.Id).ToArray();
        _config.Hotkeys = hotkeys;

        _selectedHotkey = null;
        _selectedListItem = null;
        EmptyEditorState.IsVisible = true;
        EditorPanel.IsVisible = false;

        PopulateHotkeyList();
    }

    // ═══ HOTKEY RECORDING ═══

    private void HotkeyCombo_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Record modifiers
        int mods = 0;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) mods |= 2;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) mods |= 1;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) mods |= 4;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) mods |= 8;

        // Skip if only modifier pressed
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin)
            return;

        // Zet de Avalonia-toets om naar een Windows virtual-key-code. Zo staat de
        // config in hetzelfde formaat als de Windows-app en kan de macOS-hotkeydienst
        // hem correct naar een macOS-toetscode vertalen.
        int vk = AvaloniaKeyToVk(e.Key);
        if (vk == 0) return; // niet-ondersteunde toets, negeren

        _recordedModifiers = mods;
        _recordedKey = vk;

        // Display
        var parts = new List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Cmd");
        parts.Add(VkToDisplay(vk));

        HotkeyComboBox.Text = string.Join(" + ", parts);
    }

    /// <summary>
    /// Avalonia Key -> Windows virtual-key-code. Gebruikt de (aaneengesloten,
    /// geordende) enum-reeksen zodat het niet afhangt van absolute enum-waarden.
    /// Retourneert 0 voor niet-ondersteunde toetsen.
    /// </summary>
    private static int AvaloniaKeyToVk(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return 0x41 + (key - Key.A);
        if (key >= Key.D0 && key <= Key.D9) return 0x30 + (key - Key.D0);
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return 0x30 + (key - Key.NumPad0);
        if (key >= Key.F1 && key <= Key.F12) return 0x70 + (key - Key.F1);

        return key switch
        {
            Key.Space => 0x20,
            Key.Enter => 0x0D,
            Key.Escape => 0x1B,
            Key.Tab => 0x09,
            Key.Back => 0x08,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemSemicolon => 0xBA,
            Key.OemMinus => 0xBD,
            Key.OemPlus => 0xBB,
            Key.OemTilde => 0xC0,
            Key.OemOpenBrackets => 0xDB,
            Key.OemCloseBrackets => 0xDD,
            Key.OemPipe => 0xDC,
            _ => 0
        };
    }

    /// <summary>Windows virtual-key-code -> leesbare weergave (identiek aan het dashboard).</summary>
    private static string VkToDisplay(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        _ => $"Key{vk}"
    };

    // ═══ MODELS & STYLES ═══

    private async void LoadModels()
    {
        // Default models
        ModelComboBox.Items.Clear();
        var defaults = new[] {
            ("gpt-4o-mini", "GPT-4o Mini (snel)"),
            ("gpt-4o", "GPT-4o (slim)"),
            ("claude-3-haiku", "Claude 3 Haiku (snel)"),
            ("claude-3-sonnet", "Claude 3 Sonnet (slim)")
        };
        foreach (var (id, name) in defaults)
            ModelComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });

        ModelComboBox.SelectedIndex = 0;

        // Try to load from API
        try
        {
            var response = await _api.GetAvailableModelsAsync();
            if (response.Success && response.Models?.Length > 0)
            {
                ModelComboBox.Items.Clear();
                foreach (var m in response.Models)
                    ModelComboBox.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id });
                ModelComboBox.SelectedIndex = 0;
            }
        }
        catch { /* use defaults */ }
    }

    private void LoadStyles()
    {
        StyleComboBox.Items.Clear();
        StyleComboBox.Items.Add(new ComboBoxItem { Content = "Geen stijl", Tag = "" });

        foreach (var style in _config.WritingStyles)
            StyleComboBox.Items.Add(new ComboBoxItem { Content = style.Name, Tag = style.Id });

        StyleComboBox.SelectedIndex = 0;
    }

    private void SelectModelInComboBox(string modelId)
    {
        foreach (ComboBoxItem item in ModelComboBox.Items)
        {
            if (item.Tag?.ToString() == modelId)
            {
                ModelComboBox.SelectedItem = item;
                return;
            }
        }
        if (ModelComboBox.Items.Count > 0)
            ModelComboBox.SelectedIndex = 0;
    }

    private void SelectStyleInComboBox(string? styleId)
    {
        if (string.IsNullOrEmpty(styleId))
        {
            StyleComboBox.SelectedIndex = 0;
            return;
        }
        foreach (ComboBoxItem item in StyleComboBox.Items)
        {
            if (item.Tag?.ToString() == styleId)
            {
                StyleComboBox.SelectedItem = item;
                return;
            }
        }
        StyleComboBox.SelectedIndex = 0;
    }

    // ═══ HELPERS ═══

    private static string FormatHotkey(HotkeyConfig hk)
    {
        var parts = new List<string>();
        var mods = hk.ModifierKeys;
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Cmd");

        var keyStr = hk.Key switch
        {
            >= 48 and <= 57 => ((char)hk.Key).ToString(),
            >= 65 and <= 90 => ((char)hk.Key).ToString(),
            >= 112 and <= 123 => $"F{hk.Key - 111}",
            _ => hk.Key > 0 ? $"Key{hk.Key}" : "..."
        };
        parts.Add(keyStr);
        return string.Join(" + ", parts);
    }
}
