using System;
using System.Diagnostics;
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

public partial class RegisterWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly ConfigService _config;

    public RegisterWindow() : this(App.Api, App.Config) { }

    public RegisterWindow(ApiClient apiClient, ConfigService config)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _config = config;
        ApplyLocalization();

        // Hook up password validation
        PasswordBox.TextChanged += (s, e) => PasswordBox_PasswordChanged(s, e);
    }

    private void ApplyLocalization()
    {
        this.Title = L.T("Register_Title");
        RegSubtitleText.Text = L.T("Register_Subtitle");
        NameLabel.Text = L.T("Register_Name");
        RegEmailLabel.Text = L.T("Register_Email");
        RegPasswordLabel.Text = L.T("Register_Password");
        ReqTitle.Text = L.T("Register_ReqTitle");
        RegisterButton.Content = L.T("Register_Button");
        HaveAccountText.Text = L.T("Register_HaveAccount") + " ";
        LoginLinkButton.Content = L.T("Register_Login");
        SuccessTitle.Text = L.T("Register_SuccessTitle");
        SuccessToLoginButton.Content = L.T("Register_Login");
        EmailExistsForgotButton.Content = L.T("Register_ForgotPwHint");
        OrText.Text = L.T("Login_Or");
        GoogleButtonText.Text = L.T("Register_GoogleButton");
        MicrosoftButtonText.Text = L.T("Register_MicrosoftButton");
        // Initialize requirement texts
        RequirementLength.Text = "○ " + L.T("Register_ReqLength");
        RequirementUppercase.Text = "○ " + L.T("Register_ReqUpper");
        RequirementLowercase.Text = "○ " + L.T("Register_ReqLower");
        RequirementDigit.Text = "○ " + L.T("Register_ReqDigit");
        RequirementSpecial.Text = "○ " + L.T("Register_ReqSpecial");
        TermsText1Run.Text = L.T("Register_TermsText1");
        TermsLinkRun.Text = L.T("Register_TermsLinkText");
        TermsText2Run.Text = L.T("Register_TermsText2");
        PrivacyLinkRun.Text = L.T("Register_PrivacyLinkText");
        TermsText3Run.Text = L.T("Register_TermsText3");
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
            RegisterButton_Click(sender, e);
        }
    }

    private async void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? "";
        var email = EmailTextBox.Text?.Trim() ?? "";
        var password = (PasswordBox.Text ?? "");

        // Validatie
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(L.T("Register_ErrName"));
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError(L.T("Register_ErrEmail"));
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowError(L.T("Register_ErrEmail"));
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError(L.T("Register_ErrPassword"));
            return;
        }

        // Password validation is now done in real-time, but double-check here
        if (!IsPasswordValid(password))
        {
            ShowError(L.T("Register_ErrPassword"));
            return;
        }

        if (TermsCheckBox.IsChecked != true)
        {
            ShowError(L.T("Register_ErrTerms"));
            return;
        }

        // Toon loading
        ShowLoading(true);
        HideError();

        try
        {
            var response = await _apiClient.RegisterAsync(email, password, name, _config.InterfaceLanguage ?? "nl");

            if (response.Success)
            {
                ShowSuccess($"We hebben een verificatiemail gestuurd naar:\n{email}\n\nKlik op de link in de email om je account te activeren.");
            }
            else
            {
                // Show specific error based on errorType
                string errorMessage = GetUserFriendlyErrorMessage(response.ErrorType, response.Error);
                ShowError(errorMessage);
                EmailExistsForgotButton.IsVisible = response.ErrorType == "EMAIL_EXISTS";
            }
        }
        catch (ApiException apiEx)
        {
            string msg = GetUserFriendlyErrorMessage(apiEx.ErrorType, apiEx.Message);
            ShowError(msg);
            EmailExistsForgotButton.IsVisible = apiEx.ErrorType == "EMAIL_EXISTS";
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

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        App.SwitchWindow(this, new LoginWindow(_apiClient, _config));
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

        if (TermsCheckBox.IsChecked != true)
        {
            ShowError(L.T("Register_ErrTerms"));
            return;
        }

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
            LoadingText.Text = L.T("Register_Loading");
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

    private void Hyperlink_RequestNavigate(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is string url) Ui.OpenUrl(url);
        e.Handled = true;
    }

    private async void EmailExistsForgotButton_Click(object? sender, RoutedEventArgs e)
    {
        await new ForgotPasswordWindow(_apiClient).ShowDialog(this);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }

    private void HideError()
    {
        ErrorTextBlock.IsVisible = false;
        EmailExistsForgotButton.IsVisible = false;
    }

    private void ShowLoading(bool show)
    {
        LoadingOverlay.IsVisible = show;
        RegisterButton.IsEnabled = !show;
        GoogleLoginButton.IsEnabled = !show;
        MicrosoftLoginButton.IsEnabled = !show;
        NameTextBox.IsEnabled = !show;
        EmailTextBox.IsEnabled = !show;
        PasswordBox.IsEnabled = !show;
    }

    private void ShowSuccess(string message)
    {
        SuccessMessageTextBlock.Text = message;
        SuccessOverlay.IsVisible = true;
    }

    private void PasswordBox_PasswordChanged(object? sender, EventArgs e)
    {
        var password = (PasswordBox.Text ?? "");
        UpdatePasswordValidationUI(password);
    }

    private void UpdatePasswordValidationUI(string password)
    {
        // Check each requirement
        bool hasMinLength = password.Length >= 8;
        bool hasUppercase = System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]");
        bool hasLowercase = System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]");
        bool hasDigit = System.Text.RegularExpressions.Regex.IsMatch(password, @"\d");
        bool hasSpecial = System.Text.RegularExpressions.Regex.IsMatch(password, @"[^A-Za-z0-9]");

        // Update UI elements (defined in XAML)
        RequirementLength.Foreground = new SolidColorBrush(
            hasMinLength ? Color.FromRgb(34, 197, 94) : Color.FromRgb(156, 163, 175));
        RequirementUppercase.Foreground = new SolidColorBrush(
            hasUppercase ? Color.FromRgb(34, 197, 94) : Color.FromRgb(156, 163, 175));
        RequirementLowercase.Foreground = new SolidColorBrush(
            hasLowercase ? Color.FromRgb(34, 197, 94) : Color.FromRgb(156, 163, 175));
        RequirementDigit.Foreground = new SolidColorBrush(
            hasDigit ? Color.FromRgb(34, 197, 94) : Color.FromRgb(156, 163, 175));
        RequirementSpecial.Foreground = new SolidColorBrush(
            hasSpecial ? Color.FromRgb(34, 197, 94) : Color.FromRgb(156, 163, 175));

        // Update icons
        RequirementLength.Text = (hasMinLength ? "✓ " : "○ ") + L.T("Register_ReqLength");
        RequirementUppercase.Text = (hasUppercase ? "✓ " : "○ ") + L.T("Register_ReqUpper");
        RequirementLowercase.Text = (hasLowercase ? "✓ " : "○ ") + L.T("Register_ReqLower");
        RequirementDigit.Text = (hasDigit ? "✓ " : "○ ") + L.T("Register_ReqDigit");
        RequirementSpecial.Text = (hasSpecial ? "✓ " : "○ ") + L.T("Register_ReqSpecial");
    }

    private bool IsPasswordValid(string password)
    {
        if (password.Length < 8) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"\d")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[^A-Za-z0-9]")) return false;
        return true;
    }

    private System.Threading.CancellationTokenSource? _cooldownCts;

    private async void StartRateLimitCooldown(int seconds)
    {
        _cooldownCts?.Cancel();
        _cooldownCts = new System.Threading.CancellationTokenSource();
        var token = _cooldownCts.Token;
        RegisterButton.IsEnabled = false;
        for (int i = seconds; i > 0 && !token.IsCancellationRequested; i--)
        {
            RegisterButton.Content = L.Tf("Register_RateLimitWait", i);
            try { await Task.Delay(1000, token); } catch { break; }
        }
        if (!token.IsCancellationRequested)
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Content = L.T("Register_Button");
        }
    }

    private string GetUserFriendlyErrorMessage(string? errorType, string? fallbackMessage)
    {
        return errorType switch
        {
            "EMAIL_EXISTS"         => L.T("Auth_ErrEmailExists"),
            "PASSWORD_TOO_WEAK"    => L.T("Auth_ErrPasswordWeak"),
            "DEVICE_LIMIT_REACHED" => L.T("Auth_ErrDeviceLimit"),
            "MISSING_FIELDS"       => L.T("Auth_ErrMissingFields"),
            "MISSING_MACHINE_ID"   => L.T("Auth_ErrMissingFields"),
            "DATABASE_ERROR"       => L.T("Auth_ErrServer"),
            "SERVER_ERROR"         => L.T("Auth_ErrServer"),
            _                      => fallbackMessage ?? L.T("Auth_ErrGeneral")
        };
    }
}
