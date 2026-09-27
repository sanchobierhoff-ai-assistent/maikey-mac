using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Venster voor extra context/instructies of handmatige invoer (port van
/// Views/InputPromptWindow). Ondersteunt afbeeldingen plakken/kiezen/slepen.
/// </summary>
public partial class InputPromptWindow : Window
{
    public string UserPrompt { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; }

    /// <summary>De geplakte/toegevoegde afbeeldingen (base64-data-URI's).</summary>
    public List<string> PendingImagesBase64 => AttachmentsPanel.Images.ToList();

    public InputPromptWindow() : this(null) { }

    public InputPromptWindow(string? customHeader = null, string? customSubtitle = null,
                             string? customHint = null, bool showSkipButton = true)
    {
        InitializeComponent();

        Title = L.T("InputPrompt_Title");
        HeaderLabel.Text = customHeader ?? L.T("InputPrompt_Header");
        HeaderSubtitle.Text = customSubtitle ?? L.T("InputPrompt_Question");
        HeaderHint.Text = customHint ?? L.T("InputPrompt_Placeholder");
        CancelInputBtn.Content = L.T("Common_Cancel");
        SkipButton.Content = L.T("Common_Skip");
        SubmitInputBtn.Content = L.T("Common_Send");
        SkipButton.IsVisible = showSkipButton;

        Header.PointerPressed += (_, e) => BeginMoveDrag(e);

        ImageInput.EnableImagePaste(PromptTextBox, AttachmentsPanel.Add);
        ImageInput.EnableDrop(Shell, AttachmentsPanel.Add);

        // Enter = versturen, Shift+Enter = nieuwe regel, Esc = annuleren
        PromptTextBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                Submit_Click(null, null);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancel_Click(null, null);
            }
        }, RoutingStrategies.Tunnel);

        Opened += (_, _) =>
        {
            Activate();
            Dispatcher.UIThread.Post(() => PromptTextBox.Focus(), DispatcherPriority.Input);
        };
    }

    private async void AttachImage_Click(object? sender, RoutedEventArgs e) =>
        await ImageInput.PickImagesAsync(this, AttachmentsPanel.Add);

    private void Cancel_Click(object? sender, RoutedEventArgs? e)
    {
        Confirmed = false;
        Close();
    }

    private void Skip_Click(object? sender, RoutedEventArgs e)
    {
        UserPrompt = string.Empty;
        Confirmed = true;
        Close();
    }

    private async void Submit_Click(object? sender, RoutedEventArgs? e)
    {
        UserPrompt = PromptTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(UserPrompt) && AttachmentsPanel.Count == 0)
        {
            await MkDialog.ShowInfo("Tip", L.T("InputPrompt_EmptyHint"), this);
            return;
        }
        Confirmed = true;
        Close();
    }

    /// <summary>Toon het venster en wacht op de gebruiker. Retourneert het venster (check Confirmed).</summary>
    public Task<InputPromptWindow> ShowAndWaitAsync() =>
        Ui.RunOnUi(async () =>
        {
            await this.ShowModalAsync(null);
            return this;
        });
}
