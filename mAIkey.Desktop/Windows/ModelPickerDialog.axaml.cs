using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>Kies een ander model na een modelfout (port van Views/ModelPickerDialog).</summary>
public partial class ModelPickerDialog : Window
{
    public string? SelectedModel { get; private set; }

    // Minimale, altijd-beschikbare set voor als de catalogus (nog) niet gecachet is.
    private static readonly AIModel[] _fallbackModels =
    {
        new AIModel { Id = "gpt-4o-mini",           Name = "GPT-4o Mini" },
        new AIModel { Id = "gemini-2.5-flash-lite", Name = "Gemini 2.5 Flash Lite" },
    };

    public ModelPickerDialog() : this("") { }

    public ModelPickerDialog(string failedModel)
    {
        InitializeComponent();

        HeaderLabel.Text = L.T("ModelPicker_Header");
        SubtitleBlock.Text = L.Tf("ModelPicker_Subtitle", failedModel);
        BodyLabel.Text = L.T("ModelPicker_Body");
        CancelButton.Content = L.T("ModelPicker_Cancel");
        RetryButton.Content = L.T("ModelPicker_Retry");

        var models = ModelCatalogCache.Models.Count > 0
            ? ModelCatalogCache.Models.ToArray()
            : _fallbackModels;
        ModelComboBox.ItemsSource = models;
        ModelComboBox.SelectedItem = models.FirstOrDefault(m => m.Id != failedModel) ?? models.FirstOrDefault();

        Header.PointerPressed += (_, e) => BeginMoveDrag(e);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Cancel_Click(null, null);
            if (e.Key == Key.Enter) Retry_Click(null, null);
        };
    }

    private void Cancel_Click(object? sender, RoutedEventArgs? e)
    {
        SelectedModel = null;
        Close();
    }

    private void Retry_Click(object? sender, RoutedEventArgs? e)
    {
        if (ModelComboBox.SelectedItem is AIModel item)
            SelectedModel = item.Id;
        Close();
    }

    /// <summary>Toon de kiezer en geef het gekozen model terug (null = geannuleerd).</summary>
    public static Task<string?> PickAsync(string failedModel, Window? owner = null) =>
        Ui.RunOnUi(async () =>
        {
            var d = new ModelPickerDialog(failedModel);
            await d.ShowModalAsync(owner);
            return d.SelectedModel;
        });
}
