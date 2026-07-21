using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Views;

public partial class CloudSyncView : UserControl
{
    public CloudSyncView()
    {
        InitializeComponent();
    }

    private async void Sync_Click(object? sender, RoutedEventArgs e)
    {
        SyncBtn.IsEnabled = false;
        Status("Synchroniseren…");

        try
        {
            var result = await App.Api.SyncHotkeysToCloudAsync(
                App.Config.Hotkeys, App.Config.WritingStyles);

            if (result.Success)
                Status($"Gesynchroniseerd om {DateTime.Now:HH:mm}.");
            else
                Status("Synchroniseren mislukt: " + (result.Error ?? "onbekende fout"), error: true);
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

    private void Status(string msg, bool error = false)
    {
        StatusText.IsVisible = true;
        StatusText.Text = msg;
        StatusText.Foreground = new SolidColorBrush(Color.Parse(error ? "#EF4444" : "#F5A524"));
    }
}