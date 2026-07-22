using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Generiek review-venster voor integraties: titel + inhoud nakijken en dan
/// versturen via de meegegeven create-functie (retourneert een resultaatlabel of null).
/// </summary>
public partial class IntegrationReviewWindow : Window
{
    private readonly Func<string, string, Task<string?>> _create;

    private readonly bool _showTitle;

    public IntegrationReviewWindow(
        string eyebrow, string heading, string titleLabel, string createButton,
        string title, string body, Func<string, string, Task<string?>> create,
        bool showTitle = true)
    {
        InitializeComponent();
        _create = create;
        _showTitle = showTitle;
        Eyebrow.Text = eyebrow;
        Heading.Text = heading;
        TitleLabel.Text = titleLabel;
        CreateBtn.Content = createButton;
        TitleBox.Text = title;
        BodyBox.Text = body;
        TitleSection.IsVisible = showTitle;
    }

    private async void Create_Click(object? sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim() ?? "";
        if (_showTitle && string.IsNullOrEmpty(title))
        {
            Status("Vul een titel in.", error: true);
            return;
        }

        CreateBtn.IsEnabled = false;
        Status("Versturen…");

        try
        {
            var result = await _create(title, BodyBox.Text ?? "");
            if (result != null)
            {
                Status("Gelukt: " + result);
                Close(true);
            }
            else
            {
                Status("Mislukt.", error: true);
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