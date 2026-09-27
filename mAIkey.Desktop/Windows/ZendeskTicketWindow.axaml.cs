using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Zendesk-ticketbeheer: zoek een ticket, bekijk het verloop en werk
/// status/prioriteit/tags bij of plaats een reactie — rechtstreeks vanuit mAIkey.
/// </summary>
public partial class ZendeskTicketWindow : Window
{
    private readonly ApiClient _api;
    private string? _currentId;

    public ZendeskTicketWindow() : this(App.Api) { }

    public ZendeskTicketWindow(ApiClient api)
    {
        InitializeComponent();
        _api = api;
        Opened += async (_, _) => await SearchAsync(string.Empty);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async void Search_Click(object? sender, RoutedEventArgs e) => await SearchAsync(SearchBox.Text ?? "");

    private async void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SearchAsync(SearchBox.Text ?? "");
    }

    private async Task SearchAsync(string query)
    {
        SetStatus("Zendesk-tickets ophalen…", false);
        var res = await _api.SearchZendeskTicketsAsync(query);
        ResultsList.ItemsSource = null;
        if (!res.Success)
        {
            SetStatus(res.Error ?? "Kon Zendesk-tickets niet ophalen. Koppel eerst Zendesk bij Integraties.", true);
            return;
        }
        ResultsList.ItemsSource = res.Results.ToList();
        SetStatus(res.Results.Count == 0 ? "Geen tickets gevonden." : $"{res.Results.Count} ticket(s).", false);
    }

    private async void ResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is not ZendeskTicketSummary summary) return;
        await LoadTicketAsync(summary.Id.ToString());
    }

    private async Task LoadTicketAsync(string id)
    {
        SetStatus("Ticket laden…", false);
        var res = await _api.GetZendeskTicketAsync(id);
        if (!res.Success || res.Ticket == null)
        {
            SetStatus(res.Error ?? "Kon ticket niet laden.", true);
            return;
        }

        var t = res.Ticket;
        _currentId = t.Id.ToString();
        SubjectText.Text = $"#{t.Id}  ·  {t.Subject}";
        SelectCombo(StatusCombo, t.Status);
        SelectCombo(PriorityCombo, t.Priority);
        TagsBox.Text = string.Join(", ", t.Tags ?? new List<string>());
        CommentsBox.Text = string.Join("\n\n", (t.Comments ?? new List<ZendeskCommentDto>())
            .Select(c => $"[{(c.Public ? "publiek" : "intern")}] {c.Body}"));
        ReplyBox.Text = "";
        DetailPanel.IsEnabled = true;
        SetStatus("Klaar.", false);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentId == null) return;

        var tags = (TagsBox.Text ?? "")
            .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var reply = (ReplyBox.Text ?? "").Trim();

        SaveButton.IsEnabled = false;
        SetStatus("Opslaan…", false);
        var res = await _api.UpdateZendeskTicketAsync(
            _currentId,
            status: ComboValue(StatusCombo),
            priority: ComboValue(PriorityCombo),
            tags: tags,
            comment: reply.Length > 0 ? reply : null,
            isPublic: PublicCheck.IsChecked == true);
        SaveButton.IsEnabled = true;

        if (!res.Success)
        {
            SetStatus(res.Error ?? "Opslaan mislukt.", true);
            return;
        }
        SetStatus("✅ Ticket bijgewerkt.", false);
        await LoadTicketAsync(_currentId);
    }

    // ── Helpers ──

    private static void SelectCombo(ComboBox combo, string? value)
    {
        combo.SelectedItem = string.IsNullOrEmpty(value)
            ? null
            : combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i =>
                string.Equals(i.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ComboValue(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();

    private void SetStatus(string text, bool isError)
    {
        StatusText.Text = text;
        StatusText.Foreground = isError ? Brushes.IndianRed : new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x6B));
    }
}
