using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

public partial class GmailReviewWindow : Window
{
    public GmailReviewWindow(string subject, string body)
    {
        InitializeComponent();
        SubjectBox.Text = subject;
        BodyBox.Text = body;
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var to = ToBox.Text?.Trim();
        if (string.IsNullOrEmpty(to))
        {
            Status("Vul een ontvanger in.", error: true);
            return;
        }

        SendBtn.IsEnabled = false;
        Status("Versturen…");

        try
        {
            bool ok = await App.Api.SendGmailAsync(
                to, SubjectBox.Text ?? "", BodyBox.Text ?? "",
                string.IsNullOrWhiteSpace(CcBox.Text) ? null : CcBox.Text.Trim());

            if (ok)
            {
                Status("Verzonden.");
                Close(true);
            }
            else
            {
                Status("Versturen mislukt.", error: true);
                SendBtn.IsEnabled = true;
            }
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