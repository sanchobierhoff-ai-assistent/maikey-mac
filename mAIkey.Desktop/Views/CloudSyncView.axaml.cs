using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Views;

public partial class CloudSyncView : UserControl
{
    public CloudSyncView()
    {
        InitializeComponent();
        Loaded += (_, _) => Populate();
    }

    private void Populate()
    {
        HotkeysPanel.Children.Clear();
        StylesPanel.Children.Clear();

        foreach (var hk in App.Config.Hotkeys)
            HotkeysPanel.Children.Add(new CheckBox { Content = hk.Name, IsChecked = true, Tag = hk, FontSize = 13 });
        if (App.Config.Hotkeys.Length == 0)
            HotkeysPanel.Children.Add(new TextBlock { Text = "Geen mAIkeys.", Classes = { "muted" } });

        foreach (var s in App.Config.WritingStyles)
            StylesPanel.Children.Add(new CheckBox { Content = s.Name, IsChecked = true, Tag = s, FontSize = 13 });
        if (App.Config.WritingStyles.Length == 0)
            StylesPanel.Children.Add(new TextBlock { Text = "Geen schrijfstijlen.", Classes = { "muted" } });
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var cb in HotkeysPanel.Children.OfType<CheckBox>()) cb.IsChecked = true;
        foreach (var cb in StylesPanel.Children.OfType<CheckBox>()) cb.IsChecked = true;
    }

    private async void Sync_Click(object? sender, RoutedEventArgs e)
    {
        var hotkeys = HotkeysPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true).Select(cb => (HotkeyConfig)cb.Tag!).ToArray();
        var styles = StylesPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true).Select(cb => (WritingStyle)cb.Tag!).ToArray();

        if (hotkeys.Length == 0)
        {
            Status("Selecteer minstens één mAIkey.", error: true);
            return;
        }

        SyncBtn.IsEnabled = false;
        Status("Exporteren…");

        try
        {
            var result = await App.Api.SyncHotkeysToCloudAsync(hotkeys, styles);
            if (result.Success)
            {
                LastExportText.Text = $"Laatste export: {DateTime.Now:dd-MM HH:mm} — {hotkeys.Length} hotkeys, {styles.Length} stijlen";
                Status("Geëxporteerd naar de cloud.");
            }
            else
                Status("Exporteren mislukt: " + (result.Error ?? "onbekende fout"), error: true);
        }
        catch (Exception ex)
        {
            Status("Fout: " + ex.Message, error: true);
        }
        finally
        {
            SyncBtn.IsEnabled = true;
        }
    }

    private void ImportCheck_Click(object? sender, RoutedEventArgs e)
    {
        ImportStatusText.IsVisible = true;
        ImportStatusText.Text = "Importeren vanaf de cloud komt binnenkort naar de Mac-app.";
    }

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(error ? "#EF4444" : "#F5A524"));
    }
}
