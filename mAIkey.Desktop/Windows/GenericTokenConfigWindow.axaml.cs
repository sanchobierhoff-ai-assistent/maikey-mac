using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Herbruikbaar config-venster voor token/webhook-integraties met 1 of 2 velden
/// (bijv. Todoist, Zapier = 1 veld; Trello = key + token).
/// </summary>
public partial class GenericTokenConfigWindow : Window
{
    private readonly Func<string, string, Task<bool>> _test;
    private readonly Func<string, string, Task<bool>> _save;

    public GenericTokenConfigWindow(
        string name, string subtitle,
        string field1Label, string? field2Label,
        Func<string, string, Task<bool>> test,
        Func<string, string, Task<bool>> save)
    {
        InitializeComponent();
        Heading.Text = name;
        Sub.Text = subtitle;
        Field1Label.Text = field1Label;
        _test = test;
        _save = save;

        if (field2Label != null)
        {
            Field2Label.Text = field2Label;
            Field2Section.IsVisible = true;
        }
    }

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        var f1 = Field1Box.Text?.Trim() ?? "";
        var f2 = Field2Box.Text?.Trim() ?? "";
        if (f1 == "")
        {
            Status("Vul de gegevens in.", error: true);
            return;
        }

        TestBtn.IsEnabled = false;
        Status("Verbinding testen…");

        try
        {
            bool ok = await _test(f1, f2);
            if (ok)
            {
                SaveBtn.IsEnabled = true;
                Status("Verbonden.");
            }
            else
            {
                Status("Verbinding mislukt. Controleer de gegevens.", error: true);
            }
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
        SaveBtn.IsEnabled = false;
        Status("Opslaan…");

        try
        {
            bool ok = await _save(Field1Box.Text?.Trim() ?? "", Field2Box.Text?.Trim() ?? "");
            if (ok)
            {
                Status("Opgeslagen.");
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
