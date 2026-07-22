using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

public partial class JiraTicketReviewWindow : Window
{
    private readonly string _projectKey;
    private readonly string _issueTypeId;

    public JiraTicketReviewWindow(string projectKey, string issueTypeId, string summary, string description)
    {
        InitializeComponent();
        _projectKey = projectKey;
        _issueTypeId = issueTypeId;
        SummaryBox.Text = summary;
        DescriptionBox.Text = description;
    }

    private async void Create_Click(object? sender, RoutedEventArgs e)
    {
        var summary = SummaryBox.Text?.Trim();
        if (string.IsNullOrEmpty(summary))
        {
            Status("Vul een samenvatting in.", error: true);
            return;
        }

        CreateBtn.IsEnabled = false;
        Status("Ticket aanmaken…");

        try
        {
            var ticket = await App.Api.CreateJiraTicketAsync(
                _projectKey, _issueTypeId, summary, DescriptionBox.Text ?? "");

            if (ticket != null)
            {
                Status($"Aangemaakt: {ticket.Key}");
                Close(true);
            }
            else
            {
                Status("Aanmaken mislukt.", error: true);
                CreateBtn.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
            CreateBtn.IsEnabled = true;
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