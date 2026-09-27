using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Windows;

public partial class ForgotPasswordWindow : Window
{
    private readonly ApiClient _apiClient;
    private string _email = "";

    public ForgotPasswordWindow() : this(App.Api) { }

    public ForgotPasswordWindow(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        ApplyLocalization();

        NewPasswordBox.TextChanged += (s, e) => NewPasswordBox_PasswordChanged(s, e);
    }

    private void ApplyLocalization()
    {
        this.Title = L.T("ForgotPw_Title");
        Step1Header.Text = L.T("ForgotPw_Title");
        Step1Sub.Text = L.T("ForgotPw_Step1Sub");
        Step1EmailLabel.Text = L.T("ForgotPw_EmailLabel");
        SendCodeButton.Content = L.T("ForgotPw_SendCode");
        Step1CancelButton.Content = L.T("AIBuilder_Cancel");
        Step2Header.Text = L.T("ForgotPw_Step2Header");
        CodeLabel.Text = L.T("ForgotPw_CodeReceived");
        NewPasswordLabel.Text = L.T("ForgotPw_NewPassword");
        ConfirmPasswordLabel.Text = L.T("ForgotPw_ConfirmPassword");
        ResetButton.Content = L.T("ForgotPw_ResetBtn");
        Step3Header.Text = L.T("ForgotPw_SuccessTitle");
        Step3Sub.Text = L.T("ForgotPw_SuccessSub");
        ToLoginButton.Content = L.T("ForgotPw_ToLogin");
    }

    private void TitleBar_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    // ─── STAP 1: Email invoeren ───────────────────────────────────────────

    private void Step1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SendCodeButton_Click(sender, e);
    }

    private async void SendCodeButton_Click(object? sender, RoutedEventArgs e)
    {
        _email = EmailTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(_email))
        {
            ShowStep1Error(L.T("ForgotPw_ErrEmail"));
            return;
        }

        ShowLoading(L.T("ForgotPw_Sending"));

        try
        {
            var response = await _apiClient.ForgotPasswordAsync(_email, L.CurrentLanguage);

            HideLoading();

            if (response.Success)
            {
                Step2EmailLabel.Text = L.Tf("ForgotPw_Step2EmailSent", _email);
                GoToStep2();
            }
            else
            {
                ShowStep1Error(response.Error ?? L.T("Auth_ErrGeneral"));
            }
        }
        catch
        {
            HideLoading();
            ShowStep1Error(L.T("Auth_ErrNetwork"));
        }
    }

    // ─── STAP 2: Code + nieuw wachtwoord ─────────────────────────────────

    private void Step2_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ResetButton_Click(sender, e);
    }

    private void NewPasswordBox_PasswordChanged(object? sender, EventArgs e)
    {
        UpdatePasswordRequirements((NewPasswordBox.Text ?? ""));
    }

    private async void ResetButton_Click(object? sender, RoutedEventArgs e)
    {
        var code = CodeTextBox.Text?.Trim() ?? "";
        var newPassword = (NewPasswordBox.Text ?? "");
        var confirmPassword = (ConfirmPasswordBox.Text ?? "");

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
        {
            ShowStep2Error(L.T("ForgotPw_ErrCodeRequired"));
            return;
        }

        if (!IsPasswordValid(newPassword))
        {
            ShowStep2Error(L.T("Auth_ErrPasswordWeak"));
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowStep2Error(L.T("Auth_ErrPasswordMismatch"));
            return;
        }

        ShowLoading(L.T("ForgotPw_Resetting"));

        try
        {
            var response = await _apiClient.ResetPasswordAsync(_email, code, newPassword);

            HideLoading();

            if (response.Success)
            {
                GoToStep3();
            }
            else
            {
                ShowStep2Error(GetResetErrorMessage(response.ErrorType, response.Error));
            }
        }
        catch
        {
            HideLoading();
            ShowStep2Error(L.T("Auth_ErrNetwork"));
        }
    }

    private void BackToStep1_Click(object? sender, RoutedEventArgs e)
    {
        GoToStep1();
    }

    // ─── Navigatie ────────────────────────────────────────────────────────

    private void GoToStep1()
    {
        Step1Panel.IsVisible = true;
        Step2Panel.IsVisible = false;
        Step3Panel.IsVisible = false;
        HideStep1Error();
    }

    private void GoToStep2()
    {
        Step1Panel.IsVisible = false;
        Step2Panel.IsVisible = true;
        Step3Panel.IsVisible = false;
        CodeTextBox.Focus();
    }

    private void GoToStep3()
    {
        Step1Panel.IsVisible = false;
        Step2Panel.IsVisible = false;
        Step3Panel.IsVisible = true;
    }

    // ─── UI helpers ───────────────────────────────────────────────────────

    private void ShowLoading(string text = "Even geduld...")
    {
        LoadingText.Text = text;
        LoadingOverlay.IsVisible = true;
    }

    private void HideLoading()
    {
        LoadingOverlay.IsVisible = false;
    }

    private void ShowStep1Error(string message)
    {
        Step1ErrorText.Text = message;
        Step1ErrorText.IsVisible = true;
    }

    private void HideStep1Error()
    {
        Step1ErrorText.IsVisible = false;
    }

    private void ShowStep2Error(string message)
    {
        Step2ErrorText.Text = message;
        Step2ErrorText.IsVisible = true;
    }

    private void UpdatePasswordRequirements(string password)
    {
        SetReq(ReqLength,    password.Length >= 8,     L.T("ForgotPw_ReqLength"));
        SetReq(ReqUppercase, System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"), L.T("ForgotPw_ReqUpper"));
        SetReq(ReqLowercase, System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"), L.T("ForgotPw_ReqLower"));
        SetReq(ReqDigit,     System.Text.RegularExpressions.Regex.IsMatch(password, @"\d"),    L.T("ForgotPw_ReqDigit"));
        SetReq(ReqSpecial,   System.Text.RegularExpressions.Regex.IsMatch(password, @"[^A-Za-z0-9]"), L.T("ForgotPw_ReqSpecial"));
    }

    private static void SetReq(TextBlock tb, bool met, string label)
    {
        tb.Text = (met ? "✓ " : "○ ") + label;
        tb.Foreground = new SolidColorBrush(met
            ? Color.FromRgb(34, 197, 94)
            : Color.FromRgb(156, 163, 175));
    }

    private static bool IsPasswordValid(string password)
    {
        if (password.Length < 8) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"\d")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[^A-Za-z0-9]")) return false;
        return true;
    }

    private static string GetResetErrorMessage(string? errorType, string? fallback)
    {
        return errorType switch
        {
            "INVALID_RESET_CODE" => L.T("ForgotPw_ErrInvalidCode"),
            "RESET_CODE_EXPIRED" => L.T("ForgotPw_ErrCodeExpired"),
            _ => fallback ?? L.T("Auth_ErrGeneral")
        };
    }
}
