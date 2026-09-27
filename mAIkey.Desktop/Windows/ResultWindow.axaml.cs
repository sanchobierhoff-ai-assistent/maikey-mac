using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Resultaatvenster voor de output-modus "venster" (port van Views/ResultWindow): toont het
/// AI-antwoord (met opmaak) en laat vervolgvragen stellen via /ai/chat.
/// </summary>
public partial class ResultWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly List<ChatMessage> _history = new();

    public ResultWindow() : this("", App.Api) { }

    public ResultWindow(string initialResponse, ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        TitleText.Text = L.T("Result_Title");
        ThinkingText.Text = L.T("Result_Thinking");
        SendBtn.Content = L.T("Common_Send");
        FollowUpBox.Watermark = L.T("Result_FollowUpPlaceholder");

        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        FollowUpBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                await SendFollowUp();
            }
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        AppendMessage("assistant", initialResponse);
        _history.Add(new ChatMessage { Role = "assistant", Content = initialResponse });
    }

    private void AppendMessage(string role, string content)
    {
        bool isAssistant = role == "assistant";

        var bubble = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = isAssistant ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
        };
        bubble.Bind(Border.BackgroundProperty, bubble.GetResourceObservable(isAssistant ? "Bg2" : "Bg3"));
        bubble.Bind(Border.BorderBrushProperty, bubble.GetResourceObservable("Border1"));

        var label = new TextBlock
        {
            Text = isAssistant ? "AI" : L.T("Result_You"),
            FontSize = 10.5,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable(isAssistant ? "Accent" : "Text3"));

        Control body = isAssistant
            ? new MarkdownViewer { Markdown = content }
            : new SelectableTextBlock { Text = content, FontSize = 13, TextWrapping = TextWrapping.Wrap };

        var inner = new StackPanel();
        inner.Children.Add(label);
        inner.Children.Add(body);
        bubble.Child = inner;

        ConversationPanel.Children.Add(bubble);
        ConversationScroll.ScrollToEnd();
    }

    private async void SendFollowUp_Click(object? sender, RoutedEventArgs e) => await SendFollowUp();

    private async System.Threading.Tasks.Task SendFollowUp()
    {
        var text = FollowUpBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        FollowUpBox.Text = "";
        AppendMessage("user", text);
        _history.Add(new ChatMessage { Role = "user", Content = text });

        LoadingRow.IsVisible = true;
        try
        {
            var result = await _apiClient.ChatAsync(_history);
            if (!string.IsNullOrEmpty(result))
            {
                _history.Add(new ChatMessage { Role = "assistant", Content = result });
                AppendMessage("assistant", result);
            }
            else AppendMessage("assistant", L.T("Error_ChatFailed_Body"));
        }
        catch (ApiException ex) when (ex.ErrorType == "NETWORK_ERROR")
        {
            AppendMessage("assistant", L.T("Error_NoInternet_Title") + " — " + L.T("Error_NoInternet_Body"));
        }
        catch
        {
            AppendMessage("assistant", L.T("Error_ChatFailed_Body"));
        }
        finally
        {
            LoadingRow.IsVisible = false;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
