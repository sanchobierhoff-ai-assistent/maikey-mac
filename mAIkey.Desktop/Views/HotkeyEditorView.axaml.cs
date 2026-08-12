using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
    private List<AvailableModel> _models = new();

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

    /// <summary>Haalt een thema-kleur op (past zich aan donker/licht aan).
    /// Zoekt met de actieve themavariant en valt terug op app-resources, zodat
    /// in code opgebouwde controls nooit een onzichtbare (transparante) kleur krijgen.</summary>
    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private void HotkeyEditorView_Loaded(object? sender, RoutedEventArgs e)
    {
        PopulateHotkeyList();
        LoadModels();
        LoadStyles();
        _ = LoadTemplatesAsync();
    }

    // ═══ TEMPLATE-PICKER ═══

    private bool _applyingTemplate;

    private async System.Threading.Tasks.Task LoadTemplatesAsync()
    {
        try
        {
            var resp = await _api.GetPromptTemplatesAsync(_config.InterfaceLanguage);
            var templates = resp?.Templates;
            if (templates == null) return;

            TemplatePickerCombo.Items.Clear();
            foreach (var t in templates.OrderBy(t => t.SortOrder))
                TemplatePickerCombo.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t });
        }
        catch { /* templates zijn optioneel */ }
    }

    private void TemplatePicker_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_applyingTemplate) return;
        if (TemplatePickerCombo.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not RemotePromptTemplate t) return;

        // Vul de prompt + kies model/output op basis van het template.
        PromptBox.Text = t.CustomPrompt;

        if (!string.IsNullOrEmpty(t.Model)) SelectModelInComboBox(t.Model);

        foreach (ComboBoxItem oi in OutputModeComboBox.Items)
            if (oi.Tag?.ToString() == t.OutputMode) { OutputModeComboBox.SelectedItem = oi; break; }
    }

    // ═══ AI-HULP ═══

    private async void Optimize_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var win = new Windows.AiChatWindow("optimizer", PromptBox.Text);
        if (await win.ShowDialog<bool>(owner) && !string.IsNullOrWhiteSpace(win.Result))
            PromptBox.Text = win.Result;
    }

    private async void AiBuilder_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var win = new Windows.AiChatWindow("builder", null);
        if (!await win.ShowDialog<bool>(owner)) return;

        var cfg = win.ResultConfig;
        if (cfg != null)
        {
            if (!string.IsNullOrWhiteSpace(cfg.CustomPrompt)) PromptBox.Text = cfg.CustomPrompt;
            if (!string.IsNullOrWhiteSpace(cfg.Model)) SelectModelInComboBox(cfg.Model!);
            if (!string.IsNullOrWhiteSpace(cfg.OutputMode)) SelectOutputMode(cfg.OutputMode!);
            if (!string.IsNullOrWhiteSpace(cfg.Name) &&
                (string.IsNullOrWhiteSpace(HotkeyNameBox.Text) || HotkeyNameBox.Text == "Nieuwe mAIkey"))
                HotkeyNameBox.Text = cfg.Name;
        }
        else if (!string.IsNullOrWhiteSpace(win.Result))
        {
            PromptBox.Text = win.Result;
        }
    }

    private void SelectOutputMode(string mode)
    {
        foreach (ComboBoxItem item in OutputModeComboBox.Items)
            if (item.Tag?.ToString() == mode) { OutputModeComboBox.SelectedItem = item; return; }
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

        // Toon de lege-lijst-hint alleen als er geen mAIkeys zijn.
        if (ListEmptyHint != null)
            ListEmptyHint.IsVisible = hotkeys.Length == 0;
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
            Foreground = TB("Text1")
        };

        var comboText = new TextBlock
        {
            Text = FormatHotkey(hk),
            FontSize = 10,
            Foreground = TB("Text3")
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
                border.Background = TB("BackgroundHover");
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
        listItem.Background = TB("BackgroundHover");

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
        IncludeImagesCheck.IsChecked = hk.IncludeImages;
        ScreenCaptureCheck.IsChecked = hk.UseScreenCapture;
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

    private void Instructions_Click(object? sender, RoutedEventArgs e)
        => (TopLevel.GetTopLevel(this) as MainWindow)?.StartHotkeyTour();

    /// <summary>Voor de rondleiding: zorg dat er een (voorbeeld-)mAIkey geselecteerd is zodat de editor zichtbaar is.</summary>
    public void TourAddDemo()
    {
        if (_selectedHotkey == null) AddHotkey_Click(null, null!);
    }

    private void AddHotkey_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ListError != null) ListError.IsVisible = false;

            var newHk = new HotkeyConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Nieuwe mAIkey",
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
        catch (Exception ex)
        {
            if (ListError != null)
            {
                ListError.Text = "Aanmaken mislukt: " + ex.Message;
                ListError.IsVisible = true;
            }
        }
    }

    private void SaveHotkey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null) return;

        try
        {
            if (ListError != null) ListError.IsVisible = false;

            _selectedHotkey.Name = string.IsNullOrWhiteSpace(HotkeyNameBox.Text) ? "Naamloos" : HotkeyNameBox.Text;
            _selectedHotkey.ModifierKeys = _recordedModifiers;
            _selectedHotkey.Key = _recordedKey;
            _selectedHotkey.CustomPrompt = PromptBox.Text;
            _selectedHotkey.AskForContext = AskContextCheck.IsChecked ?? false;
            _selectedHotkey.IncludeImages = IncludeImagesCheck.IsChecked ?? true;
            _selectedHotkey.UseScreenCapture = ScreenCaptureCheck.IsChecked ?? false;
            _selectedHotkey.UseInputInsteadOfSelection = UseInputCheck.IsChecked ?? false;

            // Model
            if (ModelComboBox.SelectedItem is AvailableModel modelItem)
                _selectedHotkey.Model = modelItem.Id;

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
            if (idx >= 0) hotkeys[idx] = _selectedHotkey;
            else hotkeys.Add(_selectedHotkey);
            _config.Hotkeys = hotkeys.ToArray();

            // Hotkeys opnieuw aanmelden bij het systeem — mag het opslaan nooit blokkeren
            // (op Windows/preview kan de hotkeydienst een stub zijn die een fout gooit).
            try { App.Hotkeys?.RegisterAll(); } catch { /* genegeerd */ }

            PopulateHotkeyList();
            ReselectById(_selectedHotkey.Id);
            ShowSaved();
        }
        catch (Exception ex)
        {
            if (ListError != null)
            {
                ListError.Text = "Opslaan mislukt: " + ex.Message;
                ListError.IsVisible = true;
            }
        }
    }

    /// <summary>Herselecteer (highlight) het lijstitem met dit id na een verversing.</summary>
    private void ReselectById(string id)
    {
        foreach (var child in HotkeyListPanel.Children)
        {
            if (child is Border b && (b.Tag as string) == id)
            {
                if (_selectedListItem != null) _selectedListItem.Background = Brushes.Transparent;
                _selectedListItem = b;
                b.Background = TB("BackgroundHover");
                break;
            }
        }
    }

    /// <summary>Toont kort een "Opgeslagen"-bevestiging naast de opslaan-knop.</summary>
    private async void ShowSaved()
    {
        if (SaveStatus == null) return;
        SaveStatus.IsVisible = true;
        try { await System.Threading.Tasks.Task.Delay(2000); } catch { }
        SaveStatus.IsVisible = false;
    }

    private void DeleteHotkey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedHotkey == null) return;

        var hotkeys = _config.Hotkeys.Where(h => h.Id != _selectedHotkey.Id).ToArray();
        _config.Hotkeys = hotkeys;
        try { App.Hotkeys?.RegisterAll(); } catch { /* afmelden mag verwijderen niet blokkeren */ }

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
        // Eén gedeelde sjabloon voor zowel de lijstitems als de geselecteerde (dichtgeklapte) weergave,
        // zodat ze er gegarandeerd identiek uitzien.
        ModelComboBox.ItemTemplate = new FuncDataTemplate<AvailableModel>((m, _) => BuildModelContent(m), true);

        // Standaardmodellen (worden vervangen zodra de API-lijst binnen is)
        _models = new List<AvailableModel>
        {
            new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Provider = "OpenAI",    Speed = 4, Intelligence = 2, Usage = 1 },
            new() { Id = "gpt-4.1-mini", Name = "GPT-4.1 Mini", Provider = "OpenAI",   Speed = 4, Intelligence = 3, Usage = 2 },
            new() { Id = "claude-haiku-4-5", Name = "Claude Haiku 4.5", Provider = "Anthropic", Speed = 4, Intelligence = 3, Usage = 2 },
        };
        ModelComboBox.ItemsSource = _models;
        ModelComboBox.SelectedIndex = 0;

        // Try to load from API
        try
        {
            var response = await _api.GetAvailableModelsAsync();
            if (response.Success && response.Models?.Length > 0)
            {
                var selectedId = (ModelComboBox.SelectedItem as AvailableModel)?.Id;
                _models = response.Models.ToList();
                ModelComboBox.ItemsSource = _models;
                if (!string.IsNullOrEmpty(selectedId)) SelectModelInComboBox(selectedId);
                if (ModelComboBox.SelectedItem == null && _models.Count > 0) ModelComboBox.SelectedIndex = 0;
            }
        }
        catch { /* gebruik standaardmodellen */ }
    }

    /// <summary>Rijke model-rij: naam + provider links, meters (snelheid/slimheid/verbruik) rechts.
    /// Gebruikt als sjabloon voor zowel de dropdown-items als de geselecteerde weergave.</summary>
    private Control BuildModelContent(AvailableModel m)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 34 };

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        left.Children.Add(new TextBlock { Text = m.Name, FontSize = 13, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(m.Provider))
            left.Children.Add(new TextBlock { Text = m.Provider, FontSize = 10, Foreground = TB("Text3"), VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(left);

        var meters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        meters.Children.Add(BuildMeter("SNELHEID", m.Speed, Color.Parse("#F59E0B")));   // oranje
        meters.Children.Add(BuildMeter("SLIMHEID", m.Intelligence, Color.Parse("#A78BFA"))); // paars
        meters.Children.Add(BuildMeter("VERBRUIK", m.Usage, Color.Parse("#10B981")));  // groen
        Grid.SetColumn(meters, 1);
        grid.Children.Add(meters);

        return grid;
    }

    /// <summary>Eén meter met vaste breedte (voor nette kolom-uitlijning): gekleurd label
    /// bovenop + 5 segmentjes (gevuld tot de waarde, 1–5), zoals de Windows-versie.</summary>
    private Control BuildMeter(string label, int value, Color color)
    {
        var stack = new StackPanel { Spacing = 3, Width = 58, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = 8, FontWeight = FontWeight.Bold, LetterSpacing = 0.5,
            Foreground = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Center
        });
        var segs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var empty = TB("BorderStrong");
        var filled = new SolidColorBrush(color);
        for (int i = 1; i <= 5; i++)
            segs.Children.Add(new Border
            {
                Width = 7, Height = 4, CornerRadius = new CornerRadius(2),
                Background = i <= value ? filled : empty
            });
        stack.Children.Add(segs);
        return stack;
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
        var match = _models.FirstOrDefault(m => m.Id == modelId);
        if (match != null) { ModelComboBox.SelectedItem = match; return; }
        if (_models.Count > 0) ModelComboBox.SelectedIndex = 0;
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
