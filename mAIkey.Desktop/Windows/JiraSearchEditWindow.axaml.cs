using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace mAIkey.Desktop.Windows;

public partial class JiraSearchEditWindow : Window
{
    private readonly ApiClient _apiClient;
    private JiraIssueSummary? _selected;
    private List<JiraIssueSummary> _results = new();

    public JiraSearchEditWindow() : this(App.Api!) { }

    public JiraSearchEditWindow(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        ApplyLocalization();
        _ = LoadProjects();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void ApplyLocalization()
    {
        Title = L.T("JiraSearch_Title");
        HeaderText.Text = L.T("JiraSearch_Header");
        TitleText.Text = L.T("JiraSearch_Title");
        SearchTextBox.Watermark = L.T("JiraSearch_Placeholder");
        SearchBtn.Content = L.T("JiraSearch_SearchBtn");
        NoResultsText.Text = L.T("JiraSearch_NoResults");
        EditSummaryLabel.Text = L.T("JiraReview_Summary");
        EditDescLabel.Text = L.T("JiraReview_Description");
        SaveBtn.Content = L.T("JiraSearch_Save");
        CommentLabel.Text = L.T("JiraSearch_CommentLabel");
        CommentBtn.Content = L.T("JiraSearch_CommentBtn");
    }

    private async Task LoadProjects()
    {
        try
        {
            var projects = await _apiClient.GetJiraProjectsAsync();
            var list = new List<JiraProject> { new JiraProject { Key = "", Name = L.T("JiraSearch_AllProjects") } };
            if (projects != null) list.AddRange(projects);
            ProjectFilterComboBox.ItemsSource = list;
            ProjectFilterComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"[JIRA SEARCH] Load projects failed: {ex.Message}");
        }
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Search_Click(sender, e);
    }

    private async void Search_Click(object? sender, RoutedEventArgs e)
    {
        var query = (SearchTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            await MkDialog.ShowError(L.T("Common_Validation"), L.T("JiraSearch_QueryRequired"), this);
            return;
        }

        var projectKey = (ProjectFilterComboBox.SelectedItem as JiraProject)?.Key;
        if (string.IsNullOrEmpty(projectKey)) projectKey = null;

        SearchBtn.IsEnabled = false;
        SearchBtn.Content = L.T("JiraSearch_Searching");
        try
        {
            var issues = await _apiClient.SearchJiraIssuesAsync(query, projectKey);
            _results = issues?.ToList() ?? new List<JiraIssueSummary>();
            ResultsListBox.ItemsSource = _results;
            NoResultsText.IsVisible = _results.Count == 0;
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), ExtractError(ex), this);
        }
        finally
        {
            SearchBtn.IsEnabled = true;
            SearchBtn.Content = L.T("JiraSearch_SearchBtn");
        }
    }

    private async void ResultsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = ResultsListBox.SelectedItem as JiraIssueSummary;
        EditorPanel.IsEnabled = false;
        if (_selected == null) return;

        EditKeyText.Text = _selected.Key;
        EditSummaryTextBox.Text = _selected.Summary;
        EditDescTextBox.Text = L.T("JiraSearch_LoadingDesc");
        CommentTextBox.Text = "";

        try
        {
            var detail = await _apiClient.GetJiraIssueAsync(_selected.Key);
            if (detail != null)
            {
                EditSummaryTextBox.Text = detail.Summary;
                EditDescTextBox.Text = detail.Description;
            }
            else
            {
                EditDescTextBox.Text = "";
            }
        }
        catch (Exception ex)
        {
            EditDescTextBox.Text = "";
            Logger.Log($"[JIRA SEARCH] Load issue failed: {ex.Message}");
        }
        finally
        {
            EditorPanel.IsEnabled = true;
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var summary = (EditSummaryTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            await MkDialog.ShowError(L.T("Common_Validation"), L.T("JiraReview_SummaryRequired"), this);
            return;
        }

        SaveBtn.IsEnabled = false;
        SaveBtn.Content = L.T("JiraReview_Creating");
        try
        {
            var ok = await _apiClient.UpdateJiraIssueAsync(_selected.Key, summary, EditDescTextBox.Text ?? "");
            if (ok)
            {
                _selected.Summary = summary;
                var keep = _selected;
                ResultsListBox.ItemsSource = null;
                ResultsListBox.ItemsSource = _results;
                ResultsListBox.SelectedItem = keep;
                await MkDialog.ShowInfo(L.T("Common_Success"), L.Tf("JiraSearch_Saved", keep.Key), this);
            }
            else
            {
                await MkDialog.ShowError(L.T("Common_Error"), L.T("JiraSearch_SaveFail"), this);
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), ExtractError(ex), this);
        }
        finally
        {
            SaveBtn.IsEnabled = true;
            SaveBtn.Content = L.T("JiraSearch_Save");
        }
    }

    private async void Comment_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var comment = (CommentTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(comment))
        {
            await MkDialog.ShowError(L.T("Common_Validation"), L.T("JiraSearch_CommentRequired"), this);
            return;
        }

        CommentBtn.IsEnabled = false;
        try
        {
            var ok = await _apiClient.AddJiraCommentAsync(_selected.Key, comment);
            if (ok)
            {
                CommentTextBox.Text = "";
                await MkDialog.ShowInfo(L.T("Common_Success"), L.Tf("JiraSearch_CommentAdded", _selected.Key), this);
            }
            else
            {
                await MkDialog.ShowError(L.T("Common_Error"), L.T("JiraSearch_CommentFail"), this);
            }
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("Common_Error"), ExtractError(ex), this);
        }
        finally
        {
            CommentBtn.IsEnabled = true;
        }
    }

    private static string ExtractError(Exception ex)
    {
        if (ex is ApiException apiEx)
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(apiEx.Details);
                if (parsed.TryGetProperty("error", out var errProp) && errProp.GetString() is string backendError)
                    return backendError;
            }
            catch { }
        }
        return ex.Message;
    }
}
