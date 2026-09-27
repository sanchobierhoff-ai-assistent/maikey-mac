using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Laadindicator rechtsonder in beeld (port van Views/LoadingIndicatorWindow). Het
/// venster activeert zichzelf niet (ShowActivated=False), zodat de app waarin de
/// gebruiker werkt focus houdt en Cmd+V daar terechtkomt.
/// </summary>
public partial class LoadingIndicatorWindow : Window
{
    private DispatcherTimer? _timer;
    private int _elapsedSeconds;

    public event EventHandler? Cancelled;

    public LoadingIndicatorWindow()
    {
        InitializeComponent();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Cancel_Click(null, null);
        };

        Opened += (_, _) =>
        {
            PositionBottomRight();
            _elapsedSeconds = 0;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                _elapsedSeconds++;
                TimerText.Text = $"{_elapsedSeconds}s";
            };
            _timer.Start();
        };
        Closed += (_, _) => _timer?.Stop();
    }

    private void PositionBottomRight()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen == null) return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var w = (int)(Bounds.Width * scale);
        var h = (int)(Bounds.Height * scale);
        if (w <= 0) w = (int)(392 * scale);
        if (h <= 0) h = (int)(260 * scale);
        Position = new PixelPoint(area.Right - w - (int)(12 * scale), area.Bottom - h - (int)(12 * scale));
    }

    public void SetMessage(string message) =>
        Dispatcher.UIThread.Post(() =>
        {
            MetaText.Text = message;
            PositionBottomRight();
        });

    public void ShowDone() =>
        Dispatcher.UIThread.Post(() =>
        {
            SetFinalState(DoneIcon, L.T("Loading_Done"), "#0E9C6E");
            DispatcherTimer.RunOnce(FadeOutAndClose, TimeSpan.FromSeconds(1.5));
        });

    public void ShowError(string message) =>
        Dispatcher.UIThread.Post(() =>
        {
            SetFinalState(ErrorIcon, L.T("Common_Error"), "#D33A3A");
            MetaText.Text = message;
        });

    private void SetFinalState(Control icon, string status, string color)
    {
        _timer?.Stop();
        TrackRing.IsVisible = false;
        ArcPath.IsVisible = false;
        LogoMark.IsVisible = false;
        icon.IsVisible = true;
        StatusText.Text = status;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(color));
        TimerText.IsVisible = false;
    }

    public void FadeOutAndClose() =>
        Dispatcher.UIThread.Post(async () =>
        {
            _timer?.Stop();
            for (double o = 1; o > 0; o -= 0.2)
            {
                MainBorder.Opacity = o;
                await System.Threading.Tasks.Task.Delay(30);
            }
            Close();
        });

    private void Cancel_Click(object? sender, RoutedEventArgs? e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        FadeOutAndClose();
    }
}
