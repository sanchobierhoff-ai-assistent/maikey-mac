using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace mAIkey.Desktop.Windows;

public partial class InputPromptWindow : Window
{
    private string? _result;

    public InputPromptWindow(string heading)
    {
        InitializeComponent();
        if (!string.IsNullOrEmpty(heading)) Heading.Text = heading;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        _result = InputBox.Text;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }

    private Task<string?> ShowAndGetAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        Closed += (_, _) => tcs.TrySetResult(_result);
        Show();
        Activate();
        InputBox.Focus();
        return tcs.Task;
    }

    /// <summary>
    /// Toont het invoervenster (op de UI-thread) en geeft de ingevoerde tekst terug
    /// (of null bij annuleren). Veilig aan te roepen vanaf elke thread.
    /// </summary>
    public static Task<string?> PromptAsync(string heading) =>
        Dispatcher.UIThread.InvokeAsync(() => new InputPromptWindow(heading).ShowAndGetAsync());
}
