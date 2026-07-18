using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mAIkey.Core.Services;

namespace mAIkey.Desktop.Windows;

public partial class LoginWindow : Window
{
    public event EventHandler? LoginSucceeded;

    public LoginWindow()
    {
        InitializeComponent();

        LoginTitleBar.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        // Enter key triggers login
        PasswordBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Return)
                LoginBtn_Click(null, null!);
        };
        EmailBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Return)
                PasswordBox.Focus();
        };

        // Close button hover
        LoginCloseBtn.PointerEntered += (s, e) => LoginCloseBtn.Background =
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E81123"));
        LoginCloseBtn.PointerExited += (s, e) => LoginCloseBtn.Background =
            Avalonia.Media.Brushes.Transparent;
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void LoginBtn_Click(object? sender, RoutedEventArgs e)
    {
        var email = EmailBox?.Text?.Trim();
        var password = PasswordBox?.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Vul e-mailadres en wachtwoord in.");
            return;
        }

        SetLoading(true);

        try
        {
            var result = await App.Api.LoginAsync(email, password);

            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                // Save credentials
                App.Config.AuthToken = result.Token;
                App.Config.RefreshToken = result.RefreshToken;
                App.Config.UserEmail = result.Email ?? email;
                App.Config.UserName = result.Name;
                App.Config.UserId = result.UserId;
                App.Config.SubscriptionTier = result.Tier ?? "free";

                // Set token on API client
                App.Api.SetAuthToken(result.Token);

                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowError(result.Error ?? "Inloggen mislukt.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Verbindingsfout: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.IsVisible = true;
    }

    private void SetLoading(bool loading)
    {
        LoginBtn.IsEnabled = !loading;
        LoadingPanel.IsVisible = loading;
        EmailBox.IsEnabled = !loading;
        PasswordBox.IsEnabled = !loading;
    }
}
