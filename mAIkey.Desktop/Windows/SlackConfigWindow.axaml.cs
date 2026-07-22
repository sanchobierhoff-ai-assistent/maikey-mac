using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

public partial class SlackConfigWindow : Window
{
    public SlackConfigWindow()
    {
        InitializeComponent();
    }

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        var webhook = WebhookBox.Text?.Trim() ?? "";
        if (webhook == "")
        {
            Status("Vul een webhook-URL in.", error: true);
            return;
        }

        TestBtn.IsEnabled = false;
        Status("Verbinding testen…");

        try
        {
            bool ok = await App.Api.TestSlackConnectionAsync(webhook);
            if (ok)
            {
                SaveBtn.IsEnabled = true;
                Status("Verbonden. (Er is een testbericht naar je Slack gestuurd.)");
            }
            else
            {
                Status("Verbinding mislukt. Controleer de webhook-URL.", error: true);
            }
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
        }
        finally
        {
            TestBtn.IsEnabled = true;
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        SaveBtn.IsEnabled = false;
        Status("Opslaan…");

        try
        {
            bool ok = await App.Api.SaveSlackIntegrationAsync(
                WebhookBox.Text?.Trim(),
                string.IsNullOrWhiteSpace(ChannelBox.Text) ? null : ChannelBox.Text.Trim());

            if (ok)
            {
                Status("Slack opgeslagen.");
                Close(true);
            }
            else
            {
                Status("Opslaan mislukt.", error: true);
                SaveBtn.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
            SaveBtn.IsEnabled = true;
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