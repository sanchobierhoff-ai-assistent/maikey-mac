using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Windows;

public partial class GitHubConfigWindow : Window
{
    public GitHubConfigWindow()
    {
        InitializeComponent();
    }

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        var token = TokenBox.Text?.Trim() ?? "";
        if (token == "")
        {
            Status("Vul een token in.", error: true);
            return;
        }

        TestBtn.IsEnabled = false;
        Status("Verbinding testen…");

        try
        {
            bool ok = await App.Api.TestGitHubConnectionAsync(token);
            if (!ok)
            {
                Status("Verbinding mislukt. Controleer het token.", error: true);
                return;
            }

            var repos = await App.Api.GetGitHubReposAsync(token);
            RepoCombo.ItemsSource = repos ?? Array.Empty<GitHubRepo>();
            if (repos is { Length: > 0 }) RepoCombo.SelectedIndex = 0;

            RepoSection.IsVisible = true;
            SaveBtn.IsEnabled = true;
            Status("Verbonden. Kies een standaard-repository.");
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
        var token = TokenBox.Text?.Trim() ?? "";
        var repo = RepoCombo.SelectedItem as GitHubRepo;

        SaveBtn.IsEnabled = false;
        Status("Opslaan…");

        try
        {
            bool ok = await App.Api.SaveGitHubIntegrationAsync(
                token, defaultRepo: repo?.FullName,
                defaultLabels: string.IsNullOrWhiteSpace(LabelsBox.Text) ? null : LabelsBox.Text.Trim());

            if (ok)
            {
                Status("GitHub opgeslagen.");
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