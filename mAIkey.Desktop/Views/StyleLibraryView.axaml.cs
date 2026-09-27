using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Views;

/// <summary>Stijlbibliotheek met inline editor (port van Views/StyleLibraryView).</summary>
public partial class StyleLibraryView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;
    private string? _selectedStyleId;

    public StyleLibraryView() : this(App.Config, App.Api) { }

    public StyleLibraryView(ConfigService config, ApiClient apiClient)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;
        ApplyLocalization();
        LoadStyles();
    }

    private void ApplyLocalization()
    {
        PageEyebrow.Text = L.T("StyleLib_Eyebrow");
        PageTitle.Text = L.T("StyleLib_Title");
        EmptyStateTitle.Text = L.T("StyleLib_NoStyles");
        EmptyStateSub.Text = L.T("StyleLib_NoStylesSub");
        RightPlaceholderText.Text = L.T("StyleLib_SelectPrompt");
        NewStyleBtn.Content = L.T("StyleLib_NewStyle");
        ZendeskStyleBtn.Content = L.T("StyleLib_ZendeskStyle");
    }

    private void LoadStyles()
    {
        var styles = _config.GetWritingStyles();
        if (styles.Length == 0)
        {
            EmptyState.IsVisible = true;
            StylesListView.IsVisible = false;
            ClearEditor();
            return;
        }

        var vms = styles.Select(s => new StyleViewModel
        {
            Id = s.Id,
            Name = s.Name,
            ShortDescription = string.IsNullOrWhiteSpace(s.Description) ? L.T("StyleLib_NoDescription") : s.Description
        }).ToList();
        StylesListView.ItemsSource = vms;
        EmptyState.IsVisible = false;
        StylesListView.IsVisible = true;

        if (_selectedStyleId != null)
            StylesListView.SelectedItem = vms.FirstOrDefault(s => s.Id == _selectedStyleId);
    }

    private void StylesListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (StylesListView.SelectedItem is StyleViewModel vm)
        {
            _selectedStyleId = vm.Id;
            var style = _config.GetStyleById(vm.Id);
            if (style == null) ClearEditor();
            else OpenEditor(style);
        }
        else ClearEditor();
    }

    /// <summary>Start een nieuwe stijl (gebruikt door de tour).</summary>
    public void TourStartNew() => OpenEditor(null);

    private void OpenEditor(WritingStyle? style)
    {
        var editor = new StyleEditorView(_config, _apiClient, style);
        editor.NavigateBack += (_, _) =>
        {
            StylesListView.SelectedItem = null;
            _selectedStyleId = null;
            ClearEditor();
        };
        editor.StyleSaved += (_, _) => LoadStyles();
        StyleEditorContainer.Content = editor;
        RightPlaceholder.IsVisible = false;
    }

    private void ClearEditor()
    {
        StyleEditorContainer.Content = null;
        RightPlaceholder.IsVisible = true;
    }

    private async System.Threading.Tasks.Task<bool> AtStyleLimitAsync()
    {
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success && status.MaxStyleProfiles.HasValue && _config.GetWritingStyles().Length >= status.MaxStyleProfiles.Value)
            {
                await MkDialog.ShowInfo(L.T("StyleLib_LimitTitle"),
                    L.Tf("StyleLib_LimitDesc", status.Tier, status.MaxStyleProfiles.Value), TopLevel.GetTopLevel(this) as Window);
                return true;
            }
        }
        catch { }
        return false;
    }

    private async void NewStyle_Click(object? sender, RoutedEventArgs e)
    {
        if (await AtStyleLimitAsync()) return;
        StylesListView.SelectedItem = null;
        _selectedStyleId = null;
        OpenEditor(null);
    }

    /// <summary>
    /// Leid een schrijfstijl af uit de eigen publieke Zendesk-reacties: reacties ophalen →
    /// ter controle tonen → StyleProfile genereren → als stijl opslaan.
    /// </summary>
    private async void ZendeskStyle_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (await AtStyleLimitAsync()) return;

        ZendeskStyleBtn.IsEnabled = false;
        try
        {
            var fetched = await _apiClient.DeriveZendeskRepliesAsync(12);
            if (!fetched.Success || fetched.Replies == null || fetched.Replies.Count < 3)
            {
                await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"), fetched.Error ?? L.T("StyleLib_ZendeskStyleTooFew"), owner);
                return;
            }

            var preview = string.Join("\n\n", fetched.Replies.Take(3)
                .Select(r => "• " + (r.Length > 160 ? r[..160] + "…" : r)));
            if (!await MkDialog.ShowConfirm(L.T("StyleLib_ZendeskStyle"),
                    L.Tf("StyleLib_ZendeskStyleConfirm", fetched.Replies.Count) + "\n\n" + preview, owner))
                return;

            var generated = await _apiClient.DeriveZendeskStyleAsync(fetched.Replies, "Klantantwoorden in Zendesk");
            if (!generated.Success || string.IsNullOrWhiteSpace(generated.StyleProfile))
            {
                await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"), generated.Error ?? L.T("StyleLib_ZendeskStyleFailed"), owner);
                return;
            }

            var style = new WritingStyle
            {
                Name = L.T("StyleLib_ZendeskStyleName"),
                UsageContext = "Klantantwoorden in Zendesk",
                Description = "Klantantwoorden in Zendesk",
                StyleProfile = generated.StyleProfile!.Trim(),
                TextExamples = fetched.Replies.ToArray()
            };
            _config.AddWritingStyle(style);
            _selectedStyleId = style.Id;
            LoadStyles();
            await MkDialog.ShowInfo(L.T("StyleLib_ZendeskStyle"), L.T("StyleLib_ZendeskStyleDone"), owner);
        }
        catch (Exception ex)
        {
            await MkDialog.ShowError(L.T("StyleLib_ZendeskStyle"), ex.Message, owner);
        }
        finally { ZendeskStyleBtn.IsEnabled = true; }
    }

    public class StyleViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ShortDescription { get; set; } = "";
    }
}
