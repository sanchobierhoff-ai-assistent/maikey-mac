using Avalonia.Threading;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Beheert de laadindicator (port van Services/LoadingIndicatorService). Respecteert de
/// instelling "Toon AI-indicator". Veilig aan te roepen vanaf elke thread.
/// </summary>
public class LoadingIndicatorService
{
    private LoadingIndicatorWindow? _current;
    private readonly ConfigService _config;

    public LoadingIndicatorService(ConfigService config) => _config = config;

    public bool IsShowing => _current != null;

    public void Show(string? message = null)
    {
        if (!_config.ShowAiIndicator) return;
        message ??= L.T("Loading_Working");
        Dispatcher.UIThread.Post(() =>
        {
            if (_current != null)
            {
                _current.SetMessage(message);
                return;
            }
            _current = new LoadingIndicatorWindow();
            _current.SetMessage(message);
            _current.Show();
        });
    }

    public void Hide()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var indicator = _current;
            _current = null;
            indicator?.FadeOutAndClose();
        });
    }
}
