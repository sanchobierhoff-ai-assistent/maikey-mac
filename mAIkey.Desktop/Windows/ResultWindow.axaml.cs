using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace mAIkey.Desktop.Windows;

public partial class ResultWindow : Window
{
    public string ResultText { get; private set; } = "";
    public bool UseResult { get; private set; } = false;

    public ResultWindow()
    {
        InitializeComponent();

        ResultTitleBar.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        ResultCloseBtn.PointerEntered += (s, e) => ResultCloseBtn.Background =
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E81123"));
        ResultCloseBtn.PointerExited += (s, e) => ResultCloseBtn.Background =
            Avalonia.Media.Brushes.Transparent;
    }

    public ResultWindow(string resultText) : this()
    {
        ResultText = resultText;
        ResultTextBox.Text = resultText;
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e) => Close();

    private async void CopyBtn_Click(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(ResultTextBox.Text ?? "");
    }

    private void UseBtn_Click(object? sender, RoutedEventArgs e)
    {
        UseResult = true;
        ResultText = ResultTextBox.Text ?? "";
        Close();
    }
}
