using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Windows;

public partial class JiraConfigWindow : Window
{
    public JiraConfigWindow()
    {
        InitializeComponent();
    }

    private (string url, string email, string token) Credentials() =>
        (UrlBox.Text?.Trim() ?? "", EmailBox.Text?.Trim() ?? "", TokenBox.Text?.Trim() ?? "");

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        var (url, email, token) = Credentials();
        if (url == "" || email == "" || token == "")
        {
            Status("Vul URL, e-mail en API-token in.", error: true);
            return;
        }

        TestBtn.IsEnabled = false;
        Status("Verbinding testen…");

        try
        {
            bool ok = await App.Api.TestJiraConnectionAsync(url, email, token);
            if (!ok)
            {
                Status("Verbinding mislukt. Controleer de gegevens.", error: true);
                return;
            }

            var projects = await App.Api.GetJiraProjectsWithCredentialsAsync(url, email, token);
            ProjectCombo.ItemsSource = projects ?? Array.Empty<JiraProject>();
            if (projects is { Length: > 0 }) ProjectCombo.SelectedIndex = 0;

            ProjectSection.IsVisible = true;
            SaveBtn.IsEnabled = true;
            Status("Verbonden. Kies een standaardproject.");
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

    private async void Project_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (ProjectCombo.SelectedItem is not JiraProject project) return;
        var (url, email, token) = Credentials();

        try
        {
            var types = await App.Api.GetJiraIssueTypesWithCredentialsAsync(url, email, token, project.Key);
            IssueTypeCombo.ItemsSource = types ?? Array.Empty<JiraIssueType>();
            if (types is { Length: > 0 }) IssueTypeCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Status("Kon issue-types niet laden: " + ex.Message, error: true);
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        var (url, email, token) = Credentials();
        var project = ProjectCombo.SelectedItem as JiraProject;
        var issueType = IssueTypeCombo.SelectedItem as JiraIssueType;

        SaveBtn.IsEnabled = false;
        Status("Opslaan…");

        try
        {
            bool ok = await App.Api.SaveJiraIntegrationAsync(
                url, email, token,
                defaultProject: project?.Key,
                defaultIssueType: issueType?.Id);

            if (ok)
            {
                Status("Jira opgeslagen.");
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