using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mAIkey.Core.Models;

namespace mAIkey.Desktop.Windows;

public partial class CalendarReviewWindow : Window
{
    public CalendarReviewWindow(CalendarEventDraft draft)
    {
        InitializeComponent();
        TitleBox.Text = draft.Title;
        StartBox.Text = draft.StartDateTime;
        EndBox.Text = draft.EndDateTime;
        LocationBox.Text = draft.Location;
        AttendeesBox.Text = draft.Attendees;
        DescriptionBox.Text = draft.Description;
    }

    private async void Create_Click(object? sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim();
        var start = StartBox.Text?.Trim();
        var end = EndBox.Text?.Trim();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
        {
            Status("Vul titel, begin en eind in.", error: true);
            return;
        }

        CreateBtn.IsEnabled = false;
        Status("Afspraak aanmaken…");

        try
        {
            var ev = await App.Api.CreateCalendarEventAsync(
                title, start, end,
                description: DescriptionBox.Text,
                attendees: string.IsNullOrWhiteSpace(AttendeesBox.Text) ? null : AttendeesBox.Text.Trim(),
                location: string.IsNullOrWhiteSpace(LocationBox.Text) ? null : LocationBox.Text.Trim());

            if (ev != null)
            {
                Status("Afspraak aangemaakt.");
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
