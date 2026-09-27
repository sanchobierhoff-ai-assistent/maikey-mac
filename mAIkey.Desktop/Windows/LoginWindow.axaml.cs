using System;
using System.Threading.Tasks;
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

public partial class LoginWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly ConfigService _config;

    public LoginWindow() : this(App.Api, App.Config) { }

    public LoginWindow(ApiClient apiClient, ConfigService config)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _config = config;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        this.Title = L.T("Login_Title");
        SubtitleText.Text = L.T("Login_Subtitle");
        EmailLabel.Text = L.T("Login_Email");
        PasswordLabel.Text = L.T("Login_Password");
        LoginButton.Content = L.T("Login_Button");
        ForgotPasswordButton.Content = L.T("Login_ForgotPassword");
        NoAccountText.Text = L.T("Login_NoAccount") + " ";
        RegisterButton.Content = L.T("Login_Register");
        VerifyTitle.Text = "📧 " + L.T("Login_VerifyTitle");
        VerifyMsg.Text = L.T("Login_VerifyMsg");
        ResendVerificationButton.Content = L.T("Login_ResendVerify");
        LoadingText.Text = L.T("Login_LoggingIn");
        OrText.Text = L.T("Login_Or");
        GoogleButtonText.Text = L.T("Login_GoogleButton");
        MicrosoftButtonText.Text = L.T("Login_MicrosoftButton");
        UpdateLangButtons(L.CurrentLanguage);
    }

    private void UpdateLangButtons(string lang)
    {
        LangNlButton.FontWeight = lang == "nl" ? FontWeight.SemiBold : FontWeight.Normal;
        LangNlButton.Foreground = Ui.Brush(lang == "nl" ? "Text1" : "Text3");
        LangEnButton.FontWeight = lang == "en" ? FontWeight.SemiBold : FontWeight.Normal;
        LangEnButton.Foreground = Ui.Brush(lang == "en" ? "Text1" : "Text3");
        LangDeButton.FontWeight = lang == "de" ? FontWeight.SemiBold : FontWeight.Normal;
        LangDeButton.Foreground = Ui.Brush(lang == "de" ? "Text1" : "Text3");
    }

    private void SwitchLanguage(string lang)
    {
        if (L.CurrentLanguage == lang) return;
        _config.InterfaceLanguage = lang;
        L.Apply(lang);
        ApplyLocalization();
    }

    private void LangNl_Click(object? sender, RoutedEventArgs e) => SwitchLanguage("nl");
    private void LangEn_Click(object? sender, RoutedEventArgs e) => SwitchLanguage("en");
    private void LangDe_Click(object? sender, RoutedEventArgs e) => SwitchLanguage("de");

    private void TitleBar_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        App.Quit();
    }

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text?.Trim() ?? "";
        var password = (PasswordBox.Text ?? "");

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError(L.T("Login_ErrEmail"));
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError(L.T("Login_ErrPassword"));
            return;
        }

        ShowLoading(true);
        HideError();

        try
        {
            var response = await _apiClient.LoginAsync(email, password);

            if (response.Success)
            {
                App.CompleteLogin(this, response);
            }
            else
            {
                if (response.ErrorType == "EMAIL_NOT_VERIFIED")
                {
                    ShowError(L.T("Login_VerifyTitle"));
                    ShowVerificationPanel();
                    // Store email for resend button
                    _pendingVerificationEmail = email;
                }
                else
                {
                    HideVerificationPanel();
                    string errorMessage = GetUserFriendlyErrorMessage(response.ErrorType, response.Error);
                    ShowError(errorMessage);
                }
            }
        }
        catch (ApiException apiEx)
        {
            string msg = GetUserFriendlyErrorMessage(apiEx.ErrorType, apiEx.Message);
            ShowError(msg);
            if (apiEx.ErrorType == "RATE_LIMIT")
                StartRateLimitCooldown(apiEx.RetryAfterSeconds ?? 60);
        }
        catch (Exception)
        {
            ShowError(L.T("Error_General_Body"));
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private System.Threading.CancellationTokenSource? _oauthCts;

    private void GoogleLoginButton_Click(object? sender, RoutedEventArgs e)
        => _ = RunSocialLoginAsync(_apiClient.StartGoogleLoginAsync, _apiClient.PollGoogleLoginAsync, L.T("Login_GoogleWaiting"));

    private void MicrosoftLoginButton_Click(object? sender, RoutedEventArgs e)
        => _ = RunSocialLoginAsync(_apiClient.StartMicrosoftLoginAsync, _apiClient.PollMicrosoftLoginAsync, L.T("Login_MicrosoftWaiting"));

    private async Task RunSocialLoginAsync(
        Func<Task<OAuthStartResponse>> start,
        Func<string, Task<LoginResponse>> poll,
        string waitingText)
    {
        HideError();
        HideVerificationPanel();

        _oauthCts?.Cancel();
        _oauthCts = new System.Threading.CancellationTokenSource();

        ShowLoading(true);
        LoadingText.Text = waitingText;
        CancelGoogleButton.Content = L.T("Common_Cancel");
        CancelGoogleButton.IsVisible = true;

        try
        {
            var response = await OAuthLoginHelper.RunAsync(start, poll, _oauthCts.Token);

            if (response.Success)
            {
                App.CompleteLogin(this, response);
            }
            else if (response.ErrorType != "CANCELLED")
            {
                ShowError(GetOAuthErrorMessage(response.ErrorType, response.Error));
            }
        }
        catch (Exception)
        {
            ShowError(L.T("Error_General_Body"));
        }
        finally
        {
            CancelGoogleButton.IsVisible = false;
            LoadingText.Text = L.T("Login_LoggingIn");
            ShowLoading(false);
        }
    }

    private void CancelGoogleButton_Click(object? sender, RoutedEventArgs e)
    {
        _oauthCts?.Cancel();
    }

    private string GetOAuthErrorMessage(string? errorType, string? fallback)
    {
        return errorType switch
        {
            "TIMEOUT"        => L.T("Login_GoogleTimeout"),
            "BROWSER_FAILED" => L.T("Login_GoogleBrowserFailed"),
            _                => fallback ?? L.T("Login_GoogleFailed")
        };
    }

    private string? _pendingVerificationEmail;

    private async void ResendVerificationButton_Click(object? sender, RoutedEventArgs e)
    {
        var email = _pendingVerificationEmail ?? EmailTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(email)) return;

        ResendVerificationButton.IsEnabled = false;
        ResendVerificationButton.Content = L.T("Login_Sending");

        try
        {
            var response = await _apiClient.ResendVerificationAsync(email, _config.InterfaceLanguage ?? "nl");
            if (response.Success)
            {
                ResendVerificationButton.Content = L.T("Login_Sent");
                HideError();
            }
            else
            {
                ShowError(response.Error ?? L.T("Login_ErrUnknown"));
                ResendVerificationButton.IsEnabled = true;
                ResendVerificationButton.Content = L.T("Login_ResendVerify");
            }
        }
        catch
        {
            ShowError(L.T("Login_ErrNetwork"));
            ResendVerificationButton.IsEnabled = true;
            ResendVerificationButton.Content = L.T("Login_ResendVerify");
        }
    }

    private void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        App.SwitchWindow(this, new RegisterWindow(_apiClient, _config));
    }

    private async void ForgotPasswordButton_Click(object? sender, RoutedEventArgs e)
    {
        await new ForgotPasswordWindow(_apiClient).ShowDialog(this);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }

    private void HideError()
    {
        ErrorTextBlock.IsVisible = false;
    }

    private void ShowVerificationPanel()
    {
        VerificationPanel.IsVisible = true;
    }

    private void HideVerificationPanel()
    {
        VerificationPanel.IsVisible = false;
    }

    private void ShowLoading(bool show)
    {
        LoadingOverlay.IsVisible = show;
        LoginButton.IsEnabled = !show;
        GoogleLoginButton.IsEnabled = !show;
        MicrosoftLoginButton.IsEnabled = !show;
        EmailTextBox.IsEnabled = !show;
        PasswordBox.IsEnabled = !show;
    }

    private System.Threading.CancellationTokenSource? _cooldownCts;

    private async void StartRateLimitCooldown(int seconds)
    {
        _cooldownCts?.Cancel();
        _cooldownCts = new System.Threading.CancellationTokenSource();
        var token = _cooldownCts.Token;
        LoginButton.IsEnabled = false;
        for (int i = seconds; i > 0 && !token.IsCancellationRequested; i--)
        {
            LoginButton.Content = L.Tf("Login_RateLimitWait", i);
            try { await Task.Delay(1000, token); } catch { break; }
        }
        if (!token.IsCancellationRequested)
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = L.T("Login_Button");
        }
    }

    private string GetUserFriendlyErrorMessage(string? errorType, string? fallbackMessage)
    {
        return errorType switch
        {
            "USER_NOT_FOUND" => L.T("Auth_ErrUserNotFound"),
            "WRONG_PASSWORD" => L.T("Auth_ErrWrongPassword"),
            "DATABASE_ERROR" => L.T("Auth_ErrServer"),
            "SERVER_ERROR"   => L.T("Auth_ErrServer"),
            _                => fallbackMessage ?? L.T("Auth_ErrGeneral")
        };
    }
}
