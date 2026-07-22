using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

public partial class GoogleConfigWindow : Window
{
    private readonly string _type;
    private bool _connected;

    public GoogleConfigWindow(string integrationType, string displayName)
    {
        InitializeComponent();
        _type = integrationType;
        Heading.Text = displayName;
        Sub.Text = $"Verbind {displayName} via je browser met je Google-account.";
    }

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        ConnectBtn.IsEnabled = false;
        Status("Browser openen…");

        try
        {
            var url = await App.Api.StartGoogleOAuthAsync(_type);
            if (string.IsNullOrEmpty(url))
            {
                Status("Kon de inlog-URL niet ophalen.", error: true);
                ConnectBtn.IsEnabled = true;
                return;
            }

            OpenInBrowser(url);
            Status("Log in je browser in… ik wacht op de bevestiging.");

            // Poll de status tot ~2 minuten.
            for (int i = 0; i < 60 && !_connected; i++)
            {
                await System.Threading.Tasks.Task.Delay(2000);
                try
                {
                    var (ok, email) = await App.Api.GetGoogleOAuthStatusAsync(_type);
                    if (ok)
                    {
                        _connected = true;
                        Status($"Verbonden{(string.IsNullOrEmpty(email) ? "" : " als " + email)}.");
                        DoneBtn.Content = "Klaar";
                        return;
                    }
                }
                catch { /* blijf pollen */ }
            }

            if (!_connected)
            {
                Status("Nog niet verbonden. Rond de inlog in je browser af en probeer opnieuw.", error: true);
                ConnectBtn.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
            ConnectBtn.IsEnabled = true;
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e) => Close(_connected);

    private static void OpenInBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo { FileName = "open", Arguments = url, UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(error ? "#EF4444" : "#F5A524"));
    }
}