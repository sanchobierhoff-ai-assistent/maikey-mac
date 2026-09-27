using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

/// <summary>Stijl aanmaken/bewerken (port van Views/StyleEditorView).</summary>
public partial class StyleEditorView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;
    private readonly WritingStyle? _existingStyle;
    private List<string> _textExamples = new();
    private readonly ObservableCollection<TextExampleViewModel> _exampleItems = new();

    public event EventHandler? NavigateBack;
    public event EventHandler? StyleSaved;

    public StyleEditorView() : this(App.Config, App.Api, null) { }

    public StyleEditorView(ConfigService config, ApiClient apiClient, WritingStyle? existingStyle)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;
        _existingStyle = existingStyle;
        TextExamplesList.ItemsSource = _exampleItems;
        ApplyLocalization();

        if (_existingStyle != null)
        {
            WindowTitle.Text = L.T("StyleEditor_EditStyle");
            LoadExistingStyle();
            CancelBtn.Content = L.T("StyleEditor_Delete");
        }
        else WindowTitle.Text = L.T("StyleEditor_NewStyle");
        UpdateExamplesView();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void ApplyLocalization()
    {
        CancelBtn.Content = L.T("StyleEditor_Cancel");
        SaveBtn.Content = L.T("StyleEditor_Save");
        BasicInfoHeader.Text = L.T("StyleEditor_BasicInfo");
        NameLabel.Text = L.T("StyleEditor_NameLabel");
        UsageLabel.Text = L.T("StyleEditor_UsageLabel");
        ExamplesHeader.Text = L.T("StyleEditor_Examples");
        AddExampleBtn.Content = L.T("StyleEditor_AddExample");
        ExamplesHint.Text = L.T("StyleEditor_ExamplesHint");
        NoExamplesText.Text = L.T("StyleEditor_NoExamples");
        WriteStyleHeader.Text = L.T("StyleEditor_WriteStyle");
        GenerateStyleBtn.Content = L.T("StyleEditor_GenerateBtn");
        OptimizeStyleButton.Content = L.T("StyleEditor_OptimizeBtn");
        StyleProfileHint.Text = L.T("StyleEditor_StyleHint");
    }

    private void LoadExistingStyle()
    {
        if (_existingStyle == null) return;
        NameTextBox.Text = _existingStyle.Name;
        UsageContextTextBox.Text = _existingStyle.UsageContext ?? _existingStyle.Description ?? "";
        StyleProfileTextBox.Text = _existingStyle.StyleProfile ?? "";
        if (!string.IsNullOrWhiteSpace(_existingStyle.StyleProfile)) OptimizeStyleButton.IsEnabled = true;
        if (_existingStyle.TextExamples != null) _textExamples = _existingStyle.TextExamples.ToList();
        UpdateExamplesView();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            await MkDialog.ShowError(L.T("Common_Validation"), L.T("StyleEditor_ValidationDesc"), Owner);
            return;
        }
        var profile = string.IsNullOrWhiteSpace(StyleProfileTextBox.Text) ? null : StyleProfileTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(profile))
        {
            await MkDialog.ShowError(L.T("StyleEditor_ProfileRequired"), L.T("StyleEditor_ProfileRequiredDesc"), Owner);
            return;
        }
        var usage = UsageContextTextBox.Text?.Trim() ?? "";

        if (_existingStyle != null)
        {
            _existingStyle.Name = NameTextBox.Text!.Trim();
            _existingStyle.Description = usage;
            _existingStyle.UsageContext = usage;
            _existingStyle.StyleProfile = profile;
            _existingStyle.TextExamples = _textExamples.ToArray();
            _existingStyle.Modified = DateTime.Now;
            _config.UpdateWritingStyle(_existingStyle);
        }
        else
        {
            _config.AddWritingStyle(new WritingStyle
            {
                Id = Guid.NewGuid().ToString(),
                Name = NameTextBox.Text!.Trim(),
                Description = usage,
                UsageContext = usage,
                StyleProfile = profile,
                TextExamples = _textExamples.ToArray(),
                Created = DateTime.Now,
                Modified = DateTime.Now
            });
        }
        StyleSaved?.Invoke(this, EventArgs.Empty);
        NavigateBack?.Invoke(this, EventArgs.Empty);
    }

    private async void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (_existingStyle != null)
        {
            if (await MkDialog.ShowConfirm(L.T("StyleEditor_ConfirmDelete"),
                    $"{L.T("StyleEditor_ConfirmDeleteMsg").Replace("?", "")} '{_existingStyle.Name}'?", Owner))
            {
                _config.DeleteWritingStyle(_existingStyle.Id);
                StyleSaved?.Invoke(this, EventArgs.Empty);
                NavigateBack?.Invoke(this, EventArgs.Empty);
            }
        }
        else NavigateBack?.Invoke(this, EventArgs.Empty);
    }

    private async void GenerateStyleProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_textExamples.Count < 3)
        {
            await MkDialog.ShowError(L.T("StyleEditor_TooFewTitle"), L.Tf("StyleEditor_TooFewDesc", _textExamples.Count), Owner);
            return;
        }
        var usage = UsageContextTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(usage))
        {
            await MkDialog.ShowError(L.T("StyleEditor_UsageRequired"), L.T("StyleEditor_UsageRequiredDesc"), Owner);
            UsageContextTextBox.Focus();
            return;
        }

        GenerateStyleBtn.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);
        try
        {
            var profile = await _apiClient.GenerateStyleProfileAsync(_textExamples.ToArray(), usage);
            Cursor = Cursor.Default;
            if (!string.IsNullOrWhiteSpace(profile))
            {
                StyleProfileTextBox.Text = profile;
                OptimizeStyleButton.IsEnabled = true;
                await MkDialog.ShowInfo(L.T("Common_Success"), L.Tf("StyleEditor_GenerateOk", usage), Owner);
            }
            else await MkDialog.ShowError(L.T("Common_Error"), L.T("StyleEditor_GenerateFail"), Owner);
        }
        catch (Exception ex)
        {
            Cursor = Cursor.Default;
            await MkDialog.ShowError(L.T("Common_Error"), L.Tf("StyleEditor_GenerateError", ex.Message), Owner);
        }
        finally { GenerateStyleBtn.IsEnabled = true; }
    }

    private async void OptimizeStyleProfile_Click(object? sender, RoutedEventArgs e)
    {
        var current = StyleProfileTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(current))
        {
            await MkDialog.ShowError(L.T("StyleEditor_NoStyle"), L.T("StyleEditor_NoStyleDesc"), Owner);
            return;
        }
        var usage = UsageContextTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(usage))
        {
            await MkDialog.ShowError(L.T("StyleEditor_UsageRequired"), L.T("StyleEditor_UsageRequiredOptDesc"), Owner);
            UsageContextTextBox.Focus();
            return;
        }

        var dialog = new TextInputDialog(L.T("OptimizeDlg_Title"), L.T("OptimizeDlg_Header"), L.T("OptimizeDlg_Desc"),
            L.T("OptimizeDlg_OptimizeBtn"), L.T("OptimizeDlg_Cancel"), 600, 450);
        await dialog.ShowModalAsync(Owner);
        if (!dialog.Confirmed) return;

        OptimizeStyleButton.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);
        try
        {
            var result = await _apiClient.OptimizeStyleProfileAsync(current, usage, dialog.Text);
            Cursor = Cursor.Default;
            if (result != null && !string.IsNullOrWhiteSpace(result.OptimizedStyleProfile))
            {
                StyleProfileTextBox.Text = result.OptimizedStyleProfile;
                await MkDialog.ShowInfo(L.T("StyleEditor_OptimizeTitle"), L.Tf("StyleEditor_OptimizeOk", result.Changes ?? ""), Owner);
            }
            else await MkDialog.ShowError(L.T("Common_Error"), L.T("StyleEditor_OptimizeFail"), Owner);
        }
        catch (Exception ex)
        {
            Cursor = Cursor.Default;
            await MkDialog.ShowError(L.T("Common_Error"), L.Tf("StyleEditor_OptimizeError", ex.Message), Owner);
        }
        finally { OptimizeStyleButton.IsEnabled = true; }
    }

    private async void AddManualExample_Click(object? sender, RoutedEventArgs e)
    {
        if (_textExamples.Count >= 15)
        {
            await MkDialog.ShowError(L.T("StyleEditor_MaxExamples"), L.T("StyleEditor_MaxExamplesDesc"), Owner);
            return;
        }
        var dialog = new TextInputDialog(L.T("AddExample_Title"), L.T("AddExample_Label"), L.T("AddExample_Hint"),
            L.T("AddExample_AddBtn"), L.T("AddExample_Cancel"), 650, 450, headerSize: 13);
        await dialog.ShowModalAsync(Owner);
        if (!dialog.Confirmed) return;

        var text = dialog.Text.Trim();
        if (text.Length < 50)
        {
            await MkDialog.ShowError(L.T("StyleEditor_ExampleTooShort"), L.T("StyleEditor_ExampleTooShortDesc"), Owner);
            return;
        }
        _textExamples.Add(text);
        UpdateExamplesView();
    }

    private void RemoveTextExample_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index }) return;
        if (index >= 0 && index < _textExamples.Count)
        {
            _textExamples.RemoveAt(index);
            UpdateExamplesView();
        }
    }

    private void UpdateExamplesView()
    {
        _exampleItems.Clear();
        for (int i = 0; i < _textExamples.Count; i++)
        {
            var t = _textExamples[i];
            _exampleItems.Add(new TextExampleViewModel { Index = i, Preview = t.Length > 120 ? t[..120] + "..." : t });
        }
        bool has = _exampleItems.Count > 0;
        TextExamplesScrollViewer.IsVisible = has;
        VoorbeeldenEmptyState.IsVisible = !has;
    }

    public class TextExampleViewModel
    {
        public int Index { get; set; }
        public string Preview { get; set; } = "";
    }
}

/// <summary>
/// Eenvoudig venster met één groot tekstveld (Windows: AddTextExampleDialog en
/// OptimizeFeedbackDialog in StyleEditorView.xaml.cs).
/// </summary>
public class TextInputDialog : Window
{
    private readonly TextBox _box;
    public string Text => _box.Text ?? "";
    public bool Confirmed { get; private set; }

    public TextInputDialog(string title, string header, string description, string okText, string cancelText,
                           double width, double height, double headerSize = 18)
    {
        Title = title;
        Width = width;
        Height = height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));

        var head = new TextBlock { Text = header, FontSize = headerSize, FontWeight = headerSize > 14 ? FontWeight.Bold : FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        head.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text1"));
        var desc = new TextBlock { Text = description, FontSize = headerSize > 14 ? 13 : 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        desc.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(headerSize > 14 ? "Text2" : "Text3"));

        _box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 13, Padding = new Thickness(12), VerticalContentAlignment = VerticalAlignment.Top };

        var ok = new Button { Content = okText, Height = 36, Padding = new Thickness(20, 0), Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center };
        ok.Classes.Add("AccentButton");
        ok.Click += (_, _) => { Confirmed = true; Close(); };
        var cancel = new Button { Content = cancelText, Height = 36, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center };
        cancel.Classes.Add("GhostButton");
        cancel.Click += (_, _) => { Confirmed = false; Close(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new DockPanel { Margin = new Thickness(24) };
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(desc, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(head);
        root.Children.Add(desc);
        root.Children.Add(buttons);
        root.Children.Add(_box);
        Content = root;

        Opened += (_, _) => _box.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
