using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

public enum MkDialogMode { Info, Error, Confirm, Upgrade, Update }

/// <summary>
/// mAIkey-dialoog (port van Views/MkDialog.xaml). Alle helpers zijn async omdat
/// Avalonia geen blokkerende ShowDialog kent.
/// </summary>
public partial class MkDialog : Window
{
    public bool Confirmed { get; private set; }

    // Bij een verplichte update mag het venster niet worden weggeklikt.
    private bool _blockDismiss;

    public MkDialog() : this("", "", MkDialogMode.Info) { }

    public MkDialog(string title, string body, MkDialogMode mode)
    {
        InitializeComponent();

        DialogTitle.Text = title;
        DialogBody.Text = body;

        switch (mode)
        {
            case MkDialogMode.Error:
                DialogIcon.Value = "mdi-alert-circle";
                DialogIcon.Foreground = Ui.Brush("Danger", "#EF4444");
                OkBtn.Content = L.T("Common_Close");
                break;
            case MkDialogMode.Confirm:
                DialogIcon.Value = "mdi-help-circle";
                CancelBtn.IsVisible = true;
                CancelBtn.Content = L.T("Common_Cancel");
                OkBtn.Content = L.T("Common_Confirm");
                break;
            case MkDialogMode.Upgrade:
                DialogIcon.Value = "mdi-arrow-up-circle";
                CancelBtn.IsVisible = true;
                CancelBtn.Content = L.T("Common_Close");
                OkBtn.Content = L.T("Dialog_ViewPricing");
                break;
            case MkDialogMode.Update:
                DialogIcon.Value = "mdi-download";
                CancelBtn.IsVisible = true;
                CancelBtn.Content = L.T("Update_LaterBtn");
                OkBtn.Content = L.T("Update_NowBtn");
                break;
            default:
                OkBtn.Content = "OK";
                break;
        }

        KeyDown += (_, e) =>
        {
            if (_blockDismiss) return;
            if (e.Key == Key.Escape) { Confirmed = false; Close(); }
            if (e.Key == Key.Enter && mode != MkDialogMode.Confirm) { Confirmed = true; Close(); }
        };
        Opened += (_, _) => OkBtn.Focus();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) { Confirmed = true; Close(); }
    private void Cancel_Click(object? sender, RoutedEventArgs e) { Confirmed = false; Close(); }

    // ─── Static helpers ───────────────────────────────────────────────

    private static Task<bool> ShowAsync(string title, string body, MkDialogMode mode, Window? owner,
        System.Action<MkDialog>? configure = null) =>
        Ui.RunOnUi(async () =>
        {
            var d = new MkDialog(title, body, mode);
            configure?.Invoke(d);
            await d.ShowModalAsync(owner ?? Ui.ActiveWindow());
            return d.Confirmed;
        });

    public static Task ShowInfo(string title, string body, Window? owner = null) =>
        ShowAsync(title, body, MkDialogMode.Info, owner);

    public static Task ShowError(string title, string body, Window? owner = null) =>
        ShowAsync(title, body, MkDialogMode.Error, owner);

    public static Task<bool> ShowConfirm(string title, string body, Window? owner = null,
        string? okText = null, string? cancelText = null) =>
        ShowAsync(title, body, MkDialogMode.Confirm, owner, d =>
        {
            if (okText != null) d.OkBtn.Content = okText;
            if (cancelText != null) d.CancelBtn.Content = cancelText;
        });

    public static Task<bool> ShowUpgrade(string title, string body, Window? owner = null) =>
        ShowAsync(title, body, MkDialogMode.Upgrade, owner);

    /// <summary>
    /// Update-melding. Bij force=true is er alleen "Update nu" en kan het venster niet
    /// worden weggeklikt. Retourneert true bij "Update nu".
    /// </summary>
    public static Task<bool> ShowUpdate(string title, string body, bool force = false,
        Window? owner = null, string? okText = null, string? laterText = null) =>
        ShowAsync(title, body, MkDialogMode.Update, owner, d =>
        {
            if (okText != null) d.OkBtn.Content = okText;
            if (force)
            {
                d._blockDismiss = true;
                d.CancelBtn.IsVisible = false;
            }
            else if (laterText != null)
            {
                d.CancelBtn.Content = laterText;
            }
        });
}
