using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Klein zwevend "mAIkey verwerkt…"-venster dat tijdens een hotkey-actie wordt
/// getoond. Let op: dit venster activeert kort de app; de aanroeper heractiveert
/// daarom de oorspronkelijke app vóór het plakken (anders belandt Cmd+V hier).
/// </summary>
public class ProcessingIndicator
{
    private Window? _window;

    public void Show()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_window != null) return;

            var dot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new Avalonia.CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#F5A524")),
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = "mAIkey verwerkt…",
                Foreground = new SolidColorBrush(Color.Parse("#F2F2F5")),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(dot);
            row.Children.Add(label);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#18181C")),
                BorderBrush = new SolidColorBrush(Color.Parse("#F5A524")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(12),
                Padding = new Avalonia.Thickness(18, 12),
                Child = row
            };

            _window = new Window
            {
                SystemDecorations = SystemDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = card
            };
            _window.Show();
        });
    }

    public void Hide()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _window?.Close();
            _window = null;
        });
    }
}