using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Conversationeel feedbackvenster (port van Views/FeedbackWindow): toont het resultaat van een
/// "venster + extra context"-sneltoets en laat het iteratief bijsturen via /ai/iterate-prompt.
/// "Kopiëren en sluiten" slaat de correctie op als FeedbackExample (leert de sneltoets bij).
/// </summary>
public class FeedbackWindow : Window
{
    private readonly string _originalInput;
    private readonly string _initialUserInstructions;
    private readonly List<ConversationMessage> _conversationHistory = new();
    private readonly HotkeyConfig _hotkey;
    private readonly ApiClient _apiClient;
    private readonly ConfigService _configService;
    private readonly string? _firstResponse;
    private string _lastAIResponse;

    private readonly StackPanel _messagesPanel = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBox _messageBox;
    private readonly Button _sendButton;
    private Border? _typingIndicator;
    private DispatcherTimer? _typingTimer;

    public string FinalResponse => _lastAIResponse;
    public bool WasCopied { get; private set; }

    public FeedbackWindow(string originalInput, string initialResponse, HotkeyConfig hotkey,
                          ApiClient apiClient, ConfigService configService, string? initialUserInstructions = null)
    {
        _originalInput = originalInput;
        _initialUserInstructions = initialUserInstructions ?? "";
        _hotkey = hotkey;
        _apiClient = apiClient;
        _configService = configService;
        _firstResponse = initialResponse;
        _lastAIResponse = initialResponse;

        Title = L.T("FeedbackWindow_Title");
        Width = 720; Height = 640; MinWidth = 520; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(Label(L.T("FeedbackWindow_Header"), 11, "Text3", FontWeight.SemiBold, 0.85));
        header.Children.Add(Label(L.T("FeedbackWindow_Subtitle"), 20, "Text1", FontWeight.SemiBold));
        header.Children.Add(Label(L.T("FeedbackWindow_Desc"), 12.5, "Text3", FontWeight.Normal));

        _scroll = new ScrollViewer { Content = _messagesPanel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var chatBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Child = _scroll };
        chatBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg2"));
        chatBorder.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));

        _messageBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44, MaxHeight = 120, Padding = new Thickness(12, 10) };
        _messageBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                Send();
            }
        }, RoutingStrategies.Tunnel);

        _sendButton = new Button { Content = L.T("Common_Send"), Height = 44, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        _sendButton.Classes.Add("GhostButton");
        _sendButton.Click += (_, _) => Send();

        var copyButton = new Button { Content = L.T("FeedbackWindow_CopyClose"), Height = 44, Padding = new Thickness(20, 0), VerticalContentAlignment = VerticalAlignment.Center };
        copyButton.Classes.Add("AccentButton");
        copyButton.Click += async (_, _) => await CopyAndDoneAsync();

        var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto,8,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetColumn(_sendButton, 2);
        Grid.SetColumn(copyButton, 4);
        inputRow.Children.Add(_messageBox);
        inputRow.Children.Add(_sendButton);
        inputRow.Children.Add(copyButton);

        var root = new DockPanel { Margin = new Thickness(24) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(inputRow, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(inputRow);
        root.Children.Add(chatBorder);
        Content = root;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Opened += (_, _) => _messageBox.Focus();

        AddAIMessage(initialResponse);
    }

    private TextBlock Label(string text, double size, string fg, FontWeight weight, double opacity = 1)
    {
        var tb = new TextBlock { Text = text, FontSize = size, FontWeight = weight, Opacity = opacity, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fg));
        return tb;
    }

    private async void Send()
    {
        var userMessage = _messageBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(userMessage)) return;

        _messageBox.IsEnabled = false;
        _sendButton.IsEnabled = false;
        AddUserMessage(userMessage);
        ShowTyping();
        _conversationHistory.Add(new ConversationMessage { Role = "user", Content = userMessage });
        _messageBox.Text = "";

        try
        {
            var result = await _apiClient.IteratePromptModeAsync(_originalInput, _initialUserInstructions,
                _lastAIResponse, _conversationHistory.ToArray(), _hotkey);
            HideTyping();
            if (result.Success && !string.IsNullOrEmpty(result.Response))
            {
                _lastAIResponse = result.Response;
                AddAIMessage(result.Response);
            }
            else
            {
                await MkDialog.ShowError(L.T("Common_Error"), L.Tf("Feedback_ApiError", result.Error ?? L.T("Feedback_UnknownError")), this);
            }
        }
        catch (Exception ex)
        {
            HideTyping();
            await MkDialog.ShowError(L.T("Common_Error"), L.Tf("Feedback_SendError", ex.Message), this);
        }
        finally
        {
            _messageBox.IsEnabled = true;
            _sendButton.IsEnabled = true;
            _messageBox.Focus();
            _scroll.ScrollToEnd();
        }
    }

    private void ShowTyping()
    {
        int dots = 0;
        var text = new TextBlock { Text = L.T("Feedback_Typing"), FontStyle = FontStyle.Italic, FontSize = 14 };
        text.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Text2"));
        _typingIndicator = Bubble(text, false);
        _messagesPanel.Children.Add(_typingIndicator);
        _scroll.ScrollToEnd();
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _typingTimer.Tick += (_, _) =>
        {
            dots = (dots + 1) % 4;
            text.Text = L.T("Feedback_Typing") + new string('.', dots);
        };
        _typingTimer.Start();
    }

    private void HideTyping()
    {
        _typingTimer?.Stop();
        _typingTimer = null;
        if (_typingIndicator != null) _messagesPanel.Children.Remove(_typingIndicator);
        _typingIndicator = null;
    }

    private Border Bubble(Control child, bool user)
    {
        var b = new Border
        {
            CornerRadius = user ? new CornerRadius(12, 12, 2, 12) : new CornerRadius(12, 12, 12, 2),
            Padding = new Thickness(15, 10),
            Margin = user ? new Thickness(60, 5, 0, 5) : new Thickness(0, 5, 60, 5),
            HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 560,
            Child = child
        };
        if (user) b.Background = new SolidColorBrush(Color.Parse("#10B981"));
        else b.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg3"));
        return b;
    }

    private void AddUserMessage(string message) =>
        _messagesPanel.Children.Add(Bubble(new SelectableTextBlock { Text = message, Foreground = Brushes.White, FontSize = 14, TextWrapping = TextWrapping.Wrap }, true));

    private void AddAIMessage(string message)
    {
        _messagesPanel.Children.Add(Bubble(new MarkdownViewer { Markdown = message, BaseFontSize = 14 }, false));
        _scroll.ScrollToEnd();
    }

    private async System.Threading.Tasks.Task CopyAndDoneAsync()
    {
        if (string.IsNullOrEmpty(_lastAIResponse))
        {
            await MkDialog.ShowError(L.T("Common_Error"), L.T("Feedback_NoCopyError"), this);
            return;
        }

        if (_conversationHistory.Any(m => m.Role == "user") && _firstResponse != null)
            SaveFeedbackExample();

        try
        {
            await App.Platform.ClipboardService.SetTextAsync(_lastAIResponse);
            WasCopied = true;
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), L.Tf("Feedback_CopyError", ex.Message), this);
            return;
        }

        await CheckAndConsolidateAsync();
        Close();
    }

    private void SaveFeedbackExample()
    {
        var feedback = _conversationHistory.Where(m => m.Role == "user").Select(m => m.Content).ToList();
        if (feedback.Count == 0 || _firstResponse == null) return;

        var list = _hotkey.FeedbackHistory?.ToList() ?? new List<FeedbackExample>();
        list.Add(new FeedbackExample
        {
            OriginalInput = _originalInput,
            AIResponse = _firstResponse,
            UserFeedback = string.Join("; ", feedback),
            CorrectedResponse = _lastAIResponse,
            Timestamp = DateTime.Now
        });
        _hotkey.FeedbackHistory = list.ToArray();
        SaveHotkey();
    }

    private void SaveHotkey()
    {
        var hotkeys = _configService.Hotkeys.ToList();
        var index = hotkeys.FindIndex(h => h.Id == _hotkey.Id);
        if (index >= 0)
        {
            hotkeys[index] = _hotkey;
            _configService.Hotkeys = hotkeys.ToArray();
        }
    }

    private async System.Threading.Tasks.Task CheckAndConsolidateAsync()
    {
        if (_hotkey.FeedbackHistory == null || _hotkey.FeedbackHistory.Length <= 50) return;
        try
        {
            var toConsolidate = _hotkey.FeedbackHistory.Take(30).ToArray();
            await MkDialog.ShowInfo(L.T("Feedback_ConsolidateTitle"), L.Tf("Feedback_ConsolidateMsg", _hotkey.FeedbackHistory.Length), this);
            var result = await _apiClient.ConsolidateFeedbackAsync(toConsolidate);
            if (result.Success && !string.IsNullOrEmpty(result.ConsolidatedLessons))
            {
                _hotkey.ConsolidatedLessons = string.IsNullOrEmpty(_hotkey.ConsolidatedLessons)
                    ? result.ConsolidatedLessons
                    : _hotkey.ConsolidatedLessons + "\n\n--- ADDITIONAL LESSONS ---\n\n" + result.ConsolidatedLessons;
                _hotkey.FeedbackHistory = _hotkey.FeedbackHistory.Skip(30).ToArray();
                _hotkey.LastConsolidation = DateTime.Now;
                SaveHotkey();
                await MkDialog.ShowInfo(L.T("Feedback_ConsolidateDoneTitle"),
                    L.Tf("Feedback_ConsolidateDoneMsg", toConsolidate.Length, _hotkey.FeedbackHistory.Length), this);
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Feedback_ConsolidateErrorTitle"), L.Tf("Feedback_ConsolidateError", ex.Message), this);
        }
    }
}
