using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Gedeelde chat-opmaak van de AI-builder en AI-optimizer (zelfde kleuren als Windows):
/// kop, berichtenlijst, invoer + versturen en actieknoppen onderaan.
/// </summary>
public abstract class AiChatWindowBase : Window
{
    protected readonly StackPanel MessagesPanel = new();
    protected readonly ScrollViewer ChatScroll;
    protected readonly TextBox MessageBox;
    protected readonly Button SendButton;
    protected readonly StackPanel ActionButtons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, IsVisible = false, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
    protected readonly StackPanel TopArea = new();
    private Border? _typing;
    private DispatcherTimer? _typingTimer;

    protected static readonly IBrush AiBubble = new SolidColorBrush(Color.Parse("#27272A"));
    protected static readonly IBrush AiText = new SolidColorBrush(Color.Parse("#E4E4E7"));
    protected static readonly IBrush UserBubble = new SolidColorBrush(Color.Parse("#10B981"));

    protected AiChatWindowBase(string eyebrow, string title, string subtitle)
    {
        Title = title;
        Width = 720; Height = 700; MinWidth = 560; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("Bg1"));

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(Label(eyebrow, 11, "Text3", FontWeight.SemiBold, 0.85));
        header.Children.Add(Label(title, 20, "Text1", FontWeight.SemiBold));
        header.Children.Add(Label(subtitle, 12.5, "Text3", FontWeight.Normal));

        ChatScroll = new ScrollViewer { Content = MessagesPanel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var chatBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Child = ChatScroll };
        chatBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg2"));
        chatBorder.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));

        MessageBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44, MaxHeight = 120, Padding = new Thickness(12, 10) };
        MessageBox.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                await OnSendAsync();
            }
        }, RoutingStrategies.Tunnel);
        SendButton = new Button { Content = L.T("Common_Send"), Height = 44, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        SendButton.Classes.Add("AccentButton");
        SendButton.Click += async (_, _) => await OnSendAsync();

        var input = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetColumn(SendButton, 2);
        input.Children.Add(MessageBox);
        input.Children.Add(SendButton);

        var bottom = new StackPanel();
        bottom.Children.Add(input);
        bottom.Children.Add(ActionButtons);

        var root = new DockPanel { Margin = new Thickness(24) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(TopArea, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(TopArea);
        root.Children.Add(bottom);
        root.Children.Add(chatBorder);
        Content = root;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Opened += (_, _) => MessageBox.Focus();
    }

    protected abstract Task OnSendAsync();

    protected TextBlock Label(string text, double size, string fg, FontWeight weight, double opacity = 1)
    {
        var tb = new TextBlock { Text = text, FontSize = size, FontWeight = weight, Opacity = opacity, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fg));
        return tb;
    }

    protected Button ActionButton(string text, bool primary, Action onClick)
    {
        var b = new Button { Content = text, Height = 40, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        b.Classes.Add(primary ? "AccentButton" : "GhostButton");
        b.Click += (_, _) => onClick();
        return b;
    }

    protected void AddUserMessage(string message) => AddBubble(new SelectableTextBlock { Text = message, Foreground = Brushes.White, FontSize = 14, TextWrapping = TextWrapping.Wrap }, UserBubble, true);

    protected void AddAIMessage(string message) => AddBubble(new SelectableTextBlock { Text = message, Foreground = AiText, FontSize = 14, TextWrapping = TextWrapping.Wrap }, AiBubble, false);

    protected void AddSystemMessage(string message)
    {
        MessagesPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3B82F6")),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(15, 8), Margin = new Thickness(40, 5),
            HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 600,
            Child = new TextBlock { Text = message, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }
        });
        ChatScroll.ScrollToEnd();
    }

    protected void AddCard(Control card)
    {
        MessagesPanel.Children.Add(card);
        ChatScroll.ScrollToEnd();
    }

    private void AddBubble(Control content, IBrush bg, bool user)
    {
        MessagesPanel.Children.Add(new Border
        {
            Background = bg,
            CornerRadius = user ? new CornerRadius(12, 12, 2, 12) : new CornerRadius(12, 12, 12, 2),
            Padding = new Thickness(15, 10),
            Margin = user ? new Thickness(60, 5, 0, 5) : new Thickness(0, 5, 60, 5),
            HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 500,
            Child = content
        });
        ChatScroll.ScrollToEnd();
    }

    protected void ShowTyping()
    {
        int dots = 0;
        var tb = new TextBlock { Text = ".", Foreground = new SolidColorBrush(Color.Parse("#A1A1AA")), FontSize = 18, FontWeight = FontWeight.Bold };
        _typing = new Border { Background = AiBubble, CornerRadius = new CornerRadius(12, 12, 12, 2), Padding = new Thickness(15, 10), Margin = new Thickness(0, 5, 60, 5), HorizontalAlignment = HorizontalAlignment.Left, Child = tb };
        MessagesPanel.Children.Add(_typing);
        ChatScroll.ScrollToEnd();
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _typingTimer.Tick += (_, _) => { dots = (dots + 1) % 3; tb.Text = new string('.', dots + 1); };
        _typingTimer.Start();
    }

    protected void HideTyping()
    {
        _typingTimer?.Stop();
        _typingTimer = null;
        if (_typing != null) MessagesPanel.Children.Remove(_typing);
        _typing = null;
    }

    protected void SetInputEnabled(bool enabled)
    {
        MessageBox.IsEnabled = enabled;
        SendButton.IsEnabled = enabled;
        if (enabled) MessageBox.Focus();
    }
}

/// <summary>"Maak mAIkey met AI" (port van Views/AIHotkeyBuilderWindow).</summary>
public class AIHotkeyBuilderWindow : AiChatWindowBase
{
    private readonly ApiClient _api;
    private List<ChatMessage> _history = new();
    private AIHotkeyConfig? _candidate;
    public AIHotkeyConfig? GeneratedConfig { get; private set; }

    private static Dictionary<string, string> ModeLabels => new()
    {
        { "replace", L.T("AIBuilder_ModeReplace") },
        { "clipboard", L.T("AIBuilder_ModeClipboard") },
        { "window", L.T("AIBuilder_ModeWindow") },
        { "prompt", L.T("AIBuilder_ModePrompt") }
    };

    public AIHotkeyBuilderWindow(ApiClient api)
        : base(L.T("AIBuilder_Header"), L.T("AIBuilder_WindowTitle"), L.T("AIBuilder_Desc"))
    {
        _api = api;
        var hint = Label(L.T("AIBuilder_RefineHint"), 11.5, "Text3", FontWeight.Normal);
        hint.VerticalAlignment = VerticalAlignment.Center;
        hint.Margin = new Thickness(0, 0, 8, 0);
        ActionButtons.Children.Add(hint);
        ActionButtons.Children.Add(ActionButton(L.T("Common_Cancel"), false, () => { GeneratedConfig = null; Close(); }));
        ActionButtons.Children.Add(ActionButton(L.T("AIBuilder_Create"), true, () => { GeneratedConfig = _candidate; if (_candidate != null) Close(); }));
        AddAIMessage(L.T("AIBuilder_Welcome"));
    }

    protected override async Task OnSendAsync()
    {
        var text = MessageBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        SetInputEnabled(false);
        AddUserMessage(text);
        MessageBox.Text = "";
        ShowTyping();
        try
        {
            var response = await _api.BuildHotkeyAsync(_history, text);
            HideTyping();
            if (response == null || !response.Success)
            {
                AddAIMessage(L.Tf("AIBuilder_Error", response?.Error ?? "Unknown error"));
                return;
            }
            _history = response.ConversationHistory ?? new List<ChatMessage>();
            AddAIMessage(string.IsNullOrWhiteSpace(response.Message) ? L.T("AIBuilder_NoResponse") : response.Message!);

            if (response.Config != null)
            {
                _candidate = response.Config;
                ShowConfigPreview(response.Config);
                ActionButtons.IsVisible = true;
            }
            _ = App.Hotkeys?.RefreshSubscriptionAsync();
        }
        catch (Exception ex)
        {
            HideTyping();
            AddAIMessage(L.Tf("AIBuilder_Error", ex.Message));
        }
        finally { SetInputEnabled(true); }
    }

    private void ShowConfigPreview(AIHotkeyConfig config)
    {
        var mode = ModeLabels.TryGetValue(config.OutputMode ?? "", out var label) ? label : config.OutputMode ?? "onbekend";
        var model = config.Model switch
        {
            "gpt-4o-mini" => "gpt-4o-mini — Fast & affordable, perfect for daily use",
            "gpt-4o" => "gpt-4o — Powerful, for complex text and long documents",
            "gpt-5-mini" => "gpt-5-mini — Reasoning model, slow (10-15 sec), only for complex tasks",
            "gpt-5" => "gpt-5 — Reasoning model, slow, for deep analysis and code",
            _ => config.Model ?? "default"
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = L.T("AIBuilder_ProposedTitle").StartsWith("[") ? "Proposed mAIkey" : L.T("AIBuilder_ProposedTitle"), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = UserBubble, Margin = new Thickness(0, 0, 0, 6) });
        void Row(string l, string v)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };
            row.Children.Add(new TextBlock { Text = l + ": ", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#A1A1AA")), MinWidth = 90 });
            row.Children.Add(new TextBlock { Text = v, FontSize = 13, Foreground = AiText, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
            stack.Children.Add(row);
        }
        Row("Name", config.Name ?? "-");
        Row("What it does", config.CustomPrompt ?? "-");
        Row("Mode", mode);
        Row("Model", model);
        AddCard(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A2A1A")), BorderBrush = UserBubble, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10), Margin = new Thickness(0, 8, 60, 4),
            HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 520, Child = stack
        });
    }

}

/// <summary>"Optimaliseer prompt" (port van Views/AIPromptOptimizerWindow).</summary>
public class AIPromptOptimizerWindow : AiChatWindowBase
{
    private readonly ApiClient _api;
    private readonly HotkeyConfig _hotkey;
    private List<ChatMessage> _history = new();
    private readonly TextBox _feedback;
    private readonly Button _startButton;
    private bool _isReady;
    private bool _initialSent;
    private AIHotkeyConfig? _candidate;

    public AIHotkeyConfig? OptimizedConfig { get; private set; }

    public AIPromptOptimizerWindow(ApiClient api, HotkeyConfig hotkey)
        : base(L.T("AIOptimizer_Header"), L.T("AIOptimizer_WindowTitle"), L.T("AIOptimizer_Desc"))
    {
        _api = api;
        _hotkey = hotkey;

        TopArea.Children.Add(Label(L.T("AIOptimizer_FeedbackLabel"), 12, "Text3", FontWeight.SemiBold));
        TopArea.Children.Add(Label(L.T("AIOptimizer_FeedbackHint"), 11.5, "Text3", FontWeight.Normal));
        _feedback = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, Padding = new Thickness(12, 10),
            Watermark = "E.g.: the output is too long, prompt doesn't give enough detail, needs to be more formal…"
        };
        _feedback.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
            {
                e.Handled = true;
                await StartAnalysisAsync();
            }
        }, RoutingStrategies.Tunnel);
        _startButton = new Button { Content = L.T("AIOptimizer_Start"), Height = 38, Padding = new Thickness(18, 0), Margin = new Thickness(0, 8, 0, 12), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
        _startButton.Classes.Add("AccentButton");
        _startButton.Click += async (_, _) => await StartAnalysisAsync();
        TopArea.Children.Add(_feedback);
        TopArea.Children.Add(_startButton);

        ActionButtons.Children.Add(ActionButton(L.T("Common_Cancel"), false, () => { OptimizedConfig = null; Close(); }));
        ActionButtons.Children.Add(ActionButton(L.T("AIOptimizer_Apply"), true, () => { OptimizedConfig = _candidate; if (_candidate != null) Close(); }));

        Opened += (_, _) => _feedback.Focus();
        SetInputEnabled(false);
    }

    private async Task StartAnalysisAsync()
    {
        if (_initialSent) return;
        _initialSent = true;
        _feedback.IsEnabled = false;
        _startButton.IsEnabled = false;

        var userFeedback = _feedback.Text?.Trim();
        var initial = string.IsNullOrWhiteSpace(userFeedback) ? "Start analyse" : userFeedback!;
        AddSystemMessage(string.IsNullOrWhiteSpace(userFeedback)
            ? "Automatic analysis started — the AI is analysing your mAIkey configuration and usage…"
            : $"Analysis started with your feedback: \"{userFeedback}\"");
        await SendAsync(initial, false);
    }

    protected override async Task OnSendAsync()
    {
        var text = MessageBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        AddUserMessage(text);
        MessageBox.Text = "";
        await SendAsync(text, true);
    }

    private async Task SendAsync(string message, bool _)
    {
        SetInputEnabled(false);
        ShowTyping();
        try
        {
            var response = await _api.OptimizePromptAsync(_history, message, _hotkey);
            HideTyping();
            if (response == null || !response.Success)
            {
                AddAIMessage(L.Tf("AIOptimizer_Error", response?.Error ?? "Unknown error"));
                return;
            }
            _history = response.ConversationHistory ?? new List<ChatMessage>();
            AddAIMessage(string.IsNullOrWhiteSpace(response.Message) ? L.T("AIOptimizer_NoResponse") : response.Message!);
            App.Hotkeys?.RefreshSubscriptionAsync().ConfigureAwait(false);

            if (response.IsReady)
            {
                _isReady = true;
                try
                {
                    var msg = response.Message ?? "";
                    int start = msg.IndexOf('{'), end = msg.LastIndexOf('}') + 1;
                    if (start >= 0 && end > start)
                    {
                        var wrapper = JsonSerializer.Deserialize<AIHotkeyConfigWrapper>(msg[start..end], new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (wrapper?.Config != null)
                        {
                            _candidate = wrapper.Config;
                            ActionButtons.IsVisible = true;
                            return;
                        }
                    }
                }
                catch { }
                AddAIMessage(L.T("AIOptimizer_ConfigError"));
                _isReady = false;
            }
        }
        catch (Exception ex)
        {
            HideTyping();
            AddAIMessage(L.Tf("AIOptimizer_Error", ex.Message));
        }
        finally
        {
            if (!_isReady) SetInputEnabled(true);
        }
    }
}
