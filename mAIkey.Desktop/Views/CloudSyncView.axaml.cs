using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Views;

public partial class CloudSyncView : UserControl
{
    private readonly ConfigService _config;
    private readonly ApiClient _apiClient;

    // Checkboxes voor export
    private readonly List<(CheckBox Cb, HotkeyConfig Hotkey)> _exportHotkeyBoxes = new();
    private readonly List<(CheckBox Cb, WritingStyle Style)> _exportStyleBoxes = new();

    // Import data + checkboxes
    private CloudExportData? _importData;
    private readonly List<(CheckBox Cb, HotkeyConfig Hotkey)> _importHotkeyBoxes = new();
    private readonly List<(CheckBox Cb, WritingStyle Style)> _importStyleBoxes = new();

    public CloudSyncView() : this(App.Config, App.Api) { }

    public CloudSyncView(ConfigService config, ApiClient apiClient)
    {
        InitializeComponent();
        _config = config;
        _apiClient = apiClient;
        ApplyLocalization();
        BuildExportSection();
        LoadPreviousExportInfo();
    }

    private void ApplyLocalization()
    {
        CloudSyncTitle.Text       = L.T("CloudSync_Title");
        CloudSyncSubtitle.Text    = L.T("CloudSync_Subtitle");
        ExportCardHeader.Text     = L.T("CloudSync_ExportHeader");
        ExportHotkeysLabel.Text   = L.T("CloudSync_HotkeysLabel");
        ExportStylesLabel.Text    = L.T("CloudSync_StylesLabel");
        ExportEmptyText.Text      = L.T("CloudSync_EmptyExport");
        SelectAllExportBtn.Content = L.T("CloudSync_SelectAll");
        ExportButton.Content      = L.T("CloudSync_ExportBtn");
        ImportCardHeader.Text     = L.T("CloudSync_ImportHeader");
        ImportCheckHint.Text      = L.T("CloudSync_ImportCheckHint");
        CheckExportBtn.Content    = L.T("CloudSync_CheckBtn");
        ImportNotFoundText.Text   = L.T("CloudSync_NotFound");
        ImportHotkeysLabel.Text   = L.T("CloudSync_ImportHotkeys");
        ImportStylesLabel.Text    = L.T("CloudSync_ImportStyles");
        SelectAllImportBtn.Content = L.T("CloudSync_SelectAll");
        ImportButton.Content      = L.T("CloudSync_ImportBtn");
        ImportLoadingText.Text    = L.T("CloudSync_Loading");
    }

    // ============================================
    // EXPORTEREN
    // ============================================

    private void BuildExportSection()
    {
        var hotkeys = _config.Hotkeys;
        var styles = _config.WritingStyles;

        if (hotkeys.Length == 0 && styles.Length == 0)
        {
            ExportEmptyState.IsVisible = true;
            ExportButton.IsEnabled = false;
            return;
        }

        foreach (var hk in hotkeys)
        {
            var cb = MakeCheckBox(hk.Name, true);
            _exportHotkeyBoxes.Add((cb, hk));
            ExportHotkeysPanel.Children.Add(cb);
        }

        foreach (var st in styles)
        {
            var cb = MakeCheckBox(st.Name, true);
            _exportStyleBoxes.Add((cb, st));
            ExportStylesPanel.Children.Add(cb);
        }
    }

    private async void LoadPreviousExportInfo()
    {
        try
        {
            var response = await _apiClient.GetExportAsync();
            if (response?.Success == true && response.Export != null)
            {
                var dt = ParseDate(response.Export.ExportedAt);
                var hCount = response.Export.Hotkeys?.Length ?? 0;
                var sCount = response.Export.Styles?.Length ?? 0;
                PreviousExportText.Text = $"Vorige export: {dt} — {hCount} hotkey{(hCount == 1 ? "" : "s")}, {sCount} stijl{(sCount == 1 ? "" : "en")}";
                PreviousExportBorder.IsVisible = true;
            }
        }
        catch { /* stil falen is prima */ }
    }

    private void SelectAllExport_Click(object? sender, RoutedEventArgs e)
    {
        var allChecked = _exportHotkeyBoxes.All(x => x.Cb.IsChecked == true)
                      && _exportStyleBoxes.All(x => x.Cb.IsChecked == true);

        foreach (var (cb, _) in _exportHotkeyBoxes) cb.IsChecked = !allChecked;
        foreach (var (cb, _) in _exportStyleBoxes)  cb.IsChecked = !allChecked;
    }

    private async void ExportToCloud_Click(object? sender, RoutedEventArgs e)
    {
        // Tiercheck
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success && !status.CloudSync)
            {
                ShowExportStatus("Cloud sync is niet beschikbaar in het Gratis-abonnement. Upgrade naar Starter of hoger op maikey.nl/prijzen.", false);
                return;
            }
        }
        catch { /* Fail-open */ }

        var selectedHotkeys = _exportHotkeyBoxes
            .Where(x => x.Cb.IsChecked == true)
            .Select(x => x.Hotkey)
            .ToArray();

        var selectedStyles = _exportStyleBoxes
            .Where(x => x.Cb.IsChecked == true)
            .Select(x => x.Style)
            .ToArray();

        if (selectedHotkeys.Length == 0 && selectedStyles.Length == 0)
        {
            ShowExportStatus("Selecteer minimaal één hotkey of stijl.", false);
            return;
        }

        ExportButton.IsEnabled = false;
        ExportButton.Content = L.T("CloudSync_Loading");

        try
        {
            var result = await _apiClient.SaveExportAsync(selectedHotkeys, selectedStyles);

            if (result?.Success == true)
            {
                var dt = ParseDate(result.ExportedAt);
                ShowExportStatus($"✓ Geëxporteerd op {dt} — {result.HotkeyCount} hotkeys, {result.StyleCount} stijlen", true);
                PreviousExportText.Text = $"Vorige export: {dt} — {result.HotkeyCount} hotkey{(result.HotkeyCount == 1 ? "" : "s")}, {result.StyleCount} stijl{(result.StyleCount == 1 ? "" : "en")}";
                PreviousExportBorder.IsVisible = true;

                // Sync hotkeys ook naar /hotkeys/desktop zodat de mobiele app ze automatisch kan laden
                // (inclusief ingebedde stijlprofielen voor de gekoppelde WritingStyles)
                try
                {
                    await _apiClient.SyncHotkeysToCloudAsync(selectedHotkeys, _config.WritingStyles);
                }
                catch { /* Stil falen — export is al geslaagd */ }
            }
            else
            {
                ShowExportStatus($"Export mislukt: {result?.Error ?? "onbekende fout"}", false);
            }
        }
        catch (Exception ex)
        {
            ShowExportStatus($"Export mislukt: {ex.Message}", false);
        }
        finally
        {
            ExportButton.IsEnabled = true;
            ExportButton.Content = L.T("CloudSync_ExportBtn");
        }
    }

    private void ShowExportStatus(string message, bool success)
    {
        ExportStatusText.Text = message;
        ExportStatusText.Foreground = success
            ? new SolidColorBrush(Color.Parse("#10B981"))
            : new SolidColorBrush(Color.Parse("#EF4444"));
        ExportStatusText.IsVisible = true;
    }

    // ============================================
    // IMPORTEREN
    // ============================================

    private async void CheckExport_Click(object? sender, RoutedEventArgs e)
    {
        // Tiercheck
        try
        {
            var status = await _apiClient.GetSubscriptionStatusAsync();
            if (status.Success && !status.CloudSync)
            {
                ShowImportStatus("Cloud sync is niet beschikbaar in het Gratis-abonnement. Upgrade naar Starter of hoger op maikey.nl/prijzen.", false);
                return;
            }
        }
        catch { /* Fail-open */ }

        ImportInitialPanel.IsVisible = false;
        ImportLoadingPanel.IsVisible = true;

        try
        {
            var response = await _apiClient.GetExportAsync();

            if (response?.Success == true && response.Export != null)
            {
                _importData = response.Export;
                BuildImportSection(response.Export);
                ImportFoundPanel.IsVisible = true;

                var dt = ParseDate(response.Export.ExportedAt);
                var hCount = response.Export.Hotkeys?.Length ?? 0;
                var sCount = response.Export.Styles?.Length ?? 0;
                ImportExportInfoText.Text = $"Export gevonden — geëxporteerd op {dt} · {hCount} hotkey{(hCount == 1 ? "" : "s")}, {sCount} stijl{(sCount == 1 ? "" : "en")}";
            }
            else
            {
                ImportNotFoundPanel.IsVisible = true;
            }
        }
        catch
        {
            ImportNotFoundPanel.IsVisible = true;
        }
        finally
        {
            ImportLoadingPanel.IsVisible = false;
        }
    }

    private void BuildImportSection(CloudExportData export)
    {
        _importHotkeyBoxes.Clear();
        _importStyleBoxes.Clear();
        ImportHotkeysPanel.Children.Clear();
        ImportStylesPanel.Children.Clear();

        foreach (var hk in export.Hotkeys ?? Array.Empty<HotkeyConfig>())
        {
            var existing = _config.Hotkeys.Any(h => h.Id == hk.Id);
            var label = existing ? $"{hk.Name}  (al aanwezig – wordt overschreven)" : hk.Name;
            var cb = MakeCheckBox(label, true);
            _importHotkeyBoxes.Add((cb, hk));
            ImportHotkeysPanel.Children.Add(cb);
        }

        foreach (var st in export.Styles ?? Array.Empty<WritingStyle>())
        {
            var existing = _config.WritingStyles.Any(s => s.Id == st.Id);
            var label = existing ? $"{st.Name}  (al aanwezig – wordt overschreven)" : st.Name;
            var cb = MakeCheckBox(label, true);
            _importStyleBoxes.Add((cb, st));
            ImportStylesPanel.Children.Add(cb);
        }
    }

    private void SelectAllImport_Click(object? sender, RoutedEventArgs e)
    {
        var allChecked = _importHotkeyBoxes.All(x => x.Cb.IsChecked == true)
                      && _importStyleBoxes.All(x => x.Cb.IsChecked == true);

        foreach (var (cb, _) in _importHotkeyBoxes) cb.IsChecked = !allChecked;
        foreach (var (cb, _) in _importStyleBoxes)  cb.IsChecked = !allChecked;
    }

    private void ImportSelected_Click(object? sender, RoutedEventArgs e)
    {
        var selectedHotkeys = _importHotkeyBoxes
            .Where(x => x.Cb.IsChecked == true)
            .Select(x => x.Hotkey)
            .ToList();

        var selectedStyles = _importStyleBoxes
            .Where(x => x.Cb.IsChecked == true)
            .Select(x => x.Style)
            .ToList();

        if (selectedHotkeys.Count == 0 && selectedStyles.Count == 0)
        {
            ShowImportStatus("Selecteer minimaal één item om te importeren.", false);
            return;
        }

        ImportButton.IsEnabled = false;

        try
        {
            // Importeer hotkeys (overschrijf bij zelfde ID, anders toevoegen)
            var currentHotkeys = _config.Hotkeys.ToList();
            foreach (var hk in selectedHotkeys)
            {
                var idx = currentHotkeys.FindIndex(h => h.Id == hk.Id);
                if (idx >= 0)
                    currentHotkeys[idx] = hk;
                else
                    currentHotkeys.Add(hk);
            }
            _config.Hotkeys = currentHotkeys.ToArray();

            // Importeer stijlen (overschrijf bij zelfde ID, anders toevoegen)
            var currentStyles = _config.WritingStyles.ToList();
            foreach (var st in selectedStyles)
            {
                var idx = currentStyles.FindIndex(s => s.Id == st.Id);
                if (idx >= 0)
                    currentStyles[idx] = st;
                else
                    currentStyles.Add(st);
            }
            _config.WritingStyles = currentStyles.ToArray();
            Ui.Main?.ReloadHotkeys();

            ShowImportStatus($"✓ {selectedHotkeys.Count} hotkey{(selectedHotkeys.Count == 1 ? "" : "s")} en {selectedStyles.Count} stijl{(selectedStyles.Count == 1 ? "" : "en")} geïmporteerd.", true);
        }
        catch (Exception ex)
        {
            ShowImportStatus($"Importeren mislukt: {ex.Message}", false);
        }
        finally
        {
            ImportButton.IsEnabled = true;
        }
    }

    private void ShowImportStatus(string message, bool success)
    {
        ImportStatusText.Text = message;
        ImportStatusText.Foreground = success
            ? new SolidColorBrush(Color.Parse("#10B981"))
            : new SolidColorBrush(Color.Parse("#EF4444"));
        ImportStatusText.IsVisible = true;
    }

    // ============================================
    // HELPERS
    // ============================================

    private CheckBox MakeCheckBox(string label, bool isChecked)
    {
        var cb = new CheckBox
        {
            Content = label,
            IsChecked = isChecked,
            Margin = new Thickness(0, 0, 0, 8),
            FontSize = 13
        };
        cb.Foreground = Ui.Brush("Text1");
        return cb;
    }

    private static string ParseDate(string? iso)
    {
        if (DateTime.TryParse(iso, out var dt))
            return dt.ToString("dd MMM yyyy, HH:mm");
        return iso ?? "onbekend";
    }
}
