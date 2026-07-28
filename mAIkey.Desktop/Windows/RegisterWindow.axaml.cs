using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace mAIkey.Desktop.Windows;

public partial class RegisterWindow : Window
{
    public RegisterWindow()
    {
        InitializeComponent();
    }

    private async void Register_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        var email = EmailBox.Text?.Trim();
        var password = PasswordBox.Text;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Vul naam, e-mail en wachtwoord in.");
            return;
        }
        if (password.Length < 8)
        {
            ShowError("Wachtwoord moet minstens 8 tekens zijn.");
            return;
        }

        RegisterBtn.IsEnabled = false;
        ErrorBorder.IsVisible = false;

        try
        {
            var machineId = $"{Environment.MachineName}_{Environment.UserName}";
            var result = await App.Api.RegisterAsync(email, password, name, machineId, App.Config.InterfaceLanguage);

            if (result.Success)
                Close(true); // terug naar login; account bestaat nu
            else
                ShowError(result.Error ?? "Registreren mislukt.");
        }
        catch (Exception ex)
        {
            ShowError("Verbindingsfout: " + ex.Message);
        }
        finally
        {
            RegisterBtn.IsEnabled = true;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.IsVisible = true;
    }
}
