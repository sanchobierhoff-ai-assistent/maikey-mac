using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Core.Models;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Windows;

public partial class AiChatWindow : Window
{
    private readonly string _mode;
    private readonly string? _initialPrompt;
    private List<AiChatMessage> _history = new();
    private string? _lastPrompt;
    private AiHotkeyConfig? _lastConfig;

    /// <summary>De uiteindelijke instructie, gezet bij "Gebruiken".</summary>
    public string? Result { get; private set; }

    /// <summary>Het volledige voorstel (incl. aanbevolen model + modus), gezet bij "Gebruiken".</summary>
    public AiHotkeyConfig? ResultConfig { get; private set; }

    // Parameterloze ctor voor de Avalonia XAML-loader.
    public AiChatWindow() : this("builder", null) { }

    public AiChatWindow(string mode, string? initialPrompt)
    {
        InitializeComponent();
        _mode = mode;
        _initialPrompt = initialPrompt;

        if (mode == "optimizer")
        {
            EyebrowText.Text = L.T("AiChat_OptimizerEyebrow");
            TitleText.Text = L.T("AiChat_OptimizerTitle");
            SubtitleText.Text = L.T("AiChat_OptimizerSubtitle");
            AddMessage(L.T("AiChat_OptimizerGreeting"), isUser: false);
        }
        else
        {
            EyebrowText.Text = L.T("AiChat_BuilderEyebrow");
            TitleText.Text = L.T("AiChat_BuilderTitle");
            SubtitleText.Text = L.T("AiChat_BuilderSubtitle");
            AddMessage(L.T("AiChat_BuilderGreeting"), isUser: false);
        }

        MessageBox.Focus();
    }

    private IBrush TB(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        if (Application.Current is { } app && app.TryFindResource(key, ActualThemeVariant, out var v2) && v2 is IBrush b2) return b2;
        return Brushes.Gray;
    }

    private void AddMessage(string text, bool isUser)
    {
        var bubble = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9),
            MaxWidth = 460,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = isUser ? TB("Accent") : TB("Bg3"),
            BorderBrush = TB("Border1"),
            BorderThickness = new Thickness(isUser ? 0 : 1),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = isUser ? TB("AccentForeground") : TB("Text1")
            }
        };
        MessagesPanel.Children.Add(bubble);
        ChatScroll.ScrollToEnd();
    }

    private void MessageBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Send_Click(null, null!);
        }
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var text = MessageBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        AddMessage(text, isUser: true);
        MessageBox.Text = "";
        SendButton.IsEnabled = false;

        var thinking = new Border
        {
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9),
            HorizontalAlignment = HorizontalAlignment.Left, Background = TB("Bg3"),
            Child = new TextBlock { Text = L.T("AiChat_Thinking"), FontSize = 13, Foreground = TB("Text3") }
        };
        MessagesPanel.Children.Add(thinking);
        ChatScroll.ScrollToEnd();

        try
        {
            OptimizePromptResponse resp;
            if (_mode == "optimizer")
            {
                object currentConfig = new
                {
                    name = "",
                    customPrompt = _lastPrompt ?? _initialPrompt ?? "",
                    model = "gpt-4o-mini",
                    outputMode = "window"
                };
                resp = await App.Api.OptimizePromptAsync(_history, text, currentConfig, App.Config.InterfaceLanguage);
            }
            else
            {
                resp = await App.Api.BuildHotkeyAsync(_history, text, App.Config.InterfaceLanguage);
            }

            MessagesPanel.Children.Remove(thinking);

            if (resp.Success)
            {
                _history = resp.ConversationHistory ?? _history;

                if (!string.IsNullOrWhiteSpace(resp.Message))
                    AddMessage(resp.Message, isUser: false);

                if (resp.Config != null && !string.IsNullOrWhiteSpace(resp.Config.CustomPrompt))
                {
                    _lastPrompt = resp.Config.CustomPrompt;
                    _lastConfig = resp.Config;
                    ShowConfigPreview(resp.Config);
                    ApplyButton.IsEnabled = true;
                }
                else if (_mode == "optimizer" && !string.IsNullOrWhiteSpace(resp.Message))
                {
                    // Optimizer zonder losse config: het bericht ís de verbeterde instructie.
                    _lastPrompt = resp.Message;
                    ApplyButton.IsEnabled = true;
                }
            }
            else
            {
                AddMessage(resp.Error ?? "Er ging iets mis. Probeer het opnieuw.", isUser: false);
            }
        }
        catch (Exception ex)
        {
            MessagesPanel.Children.Remove(thinking);
            AddMessage("Fout: " + ex.Message, isUser: false);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    /// <summary>Toont een nette preview-kaart van de voorgestelde mAIkey (zoals Windows).</summary>
    private void ShowConfigPreview(AiHotkeyConfig config)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = L.T("AiChat_ProposedTitle"), FontSize = 11, FontWeight = FontWeight.Bold,
            Foreground = TB("Success"), Margin = new Thickness(0, 0, 0, 6)
        });
        AddPreviewRow(stack, L.T("AiChat_FieldName"), string.IsNullOrWhiteSpace(config.Name) ? "-" : config.Name!);
        AddPreviewRow(stack, L.T("AiChat_FieldPrompt"), config.CustomPrompt ?? "-");
        AddPreviewRow(stack, L.T("AiChat_FieldMode"), ModeLabel(config.OutputMode));
        AddPreviewRow(stack, L.T("AiChat_FieldModel"), string.IsNullOrWhiteSpace(config.Model) ? "-" : config.Model!);

        MessagesPanel.Children.Add(new Border
        {
            Background = TB("Bg2"), BorderBrush = TB("Success"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 4, 40, 4), HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 520, Child = stack
        });
        ChatScroll.ScrollToEnd();
    }

    private void AddPreviewRow(StackPanel parent, string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), Margin = new Thickness(0, 2) };
        row.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = TB("Text3")
        });
        var v = new TextBlock { Text = value, FontSize = 12.5, Foreground = TB("Text1"), TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        row.Children.Add(v);
        parent.Children.Add(row);
    }

    private static string ModeLabel(string? mode) => mode switch
    {
        "replace" => "Vervang geselecteerde tekst",
        "clipboard" => "Kopieer naar klembord",
        "window" => "Toon in venster",
        _ => mode ?? "-"
    };

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        Result = _lastPrompt;
        ResultConfig = _lastConfig;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
