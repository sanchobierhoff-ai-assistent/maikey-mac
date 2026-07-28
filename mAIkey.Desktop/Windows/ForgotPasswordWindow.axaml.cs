using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

public partial class ForgotPasswordWindow : Window
{
    public ForgotPasswordWindow()
    {
        InitializeComponent();
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            Status("Vul een e-mailadres in.", error: true);
            return;
        }

        SendBtn.IsEnabled = false;
        Status("Versturen…");

        try
        {
            await App.Api.ForgotPasswordAsync(email, App.Config.InterfaceLanguage);
            // Bewust altijd dezelfde melding (geen info of het adres bestaat).
            Status("Als dit e-mailadres bestaat, is er een herstellink verstuurd.");
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
            SendBtn.IsEnabled = true;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(error ? "#EF4444" : "#F5A524"));
    }
}
