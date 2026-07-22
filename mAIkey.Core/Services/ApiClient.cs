using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using mAIkey.Core.Models;

namespace mAIkey.Core.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string Details { get; }
    public bool IsRetryable { get; }

    public ApiException(string message, int statusCode = 0, string details = "", bool isRetryable = false)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details;
        IsRetryable = isRetryable;
    }
}

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private string? _authToken;

    public const string CURRENT_VERSION = "Beta-v1.4-mac";

    public ApiClient(string baseUrl = "https://ai-assistent-backend-production.up.railway.app")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };
        _httpClient.DefaultRequestHeaders.Add("X-Client-Version", CURRENT_VERSION);
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        _authToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public bool HasToken => !string.IsNullOrEmpty(_authToken);

    // ============================================
    // GENERIC HTTP HELPERS
    // ============================================

    private async Task<T> PostAsync<T>(string endpoint, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var isRetryable = statusCode >= 500 || statusCode == 429;

            string errorMsg = statusCode switch
            {
                401 => "Sessie verlopen. Log opnieuw in.",
                402 => ParsePaymentError(responseBody),
                403 => "Geen toegang. Log opnieuw in.",
                429 => "Te veel verzoeken. Probeer het later.",
                503 => "Model tijdelijk niet beschikbaar.",
                _ => $"Serverfout ({statusCode})"
            };

            throw new ApiException(errorMsg, statusCode, responseBody, isRetryable);
        }

        return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private async Task<T> PutAsync<T>(string endpoint, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(endpoint, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new ApiException($"Request failed ({(int)response.StatusCode})",
                (int)response.StatusCode, responseBody);

        return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"Request failed ({(int)response.StatusCode})",
                (int)response.StatusCode, responseBody);
        }

        return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static string ParsePaymentError(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var reason = json.TryGetProperty("reason", out var r) ? r.GetString() : null;
            return reason switch
            {
                "daily_cap_exceeded" => "Daglimiet bereikt. Probeer het morgen.",
                "monthly_requests_exceeded" => "Maandlimiet bereikt.",
                "subscription_expired" => "Abonnement verlopen.",
                "model_blocked" => "Dit model is niet beschikbaar in je abonnement.",
                _ => "Tegoed op. Upgrade je abonnement."
            };
        }
        catch
        {
            return "Tegoed op. Upgrade je abonnement.";
        }
    }

    // ============================================
    // AUTHENTICATION
    // ============================================

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        try
        {
            return await PostAsync<LoginResponse>("/auth/login", new { email, password });
        }
        catch (ApiException ex)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<LoginResponse>(ex.Details,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) return parsed;
            }
            catch { }
            return new LoginResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<RegisterResponse> RegisterAsync(string email, string password, string name, string machineId, string lang = "nl")
    {
        return await PostAsync<RegisterResponse>("/auth/register",
            new { email, password, name, machine_id = machineId, lang });
    }

    public async Task<ApiResponse> ForgotPasswordAsync(string email, string lang = "nl")
    {
        try
        {
            return await PostAsync<ApiResponse>("/auth/forgot-password", new { email, lang });
        }
        catch (ApiException ex)
        {
            return new ApiResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        return await PostAsync<RefreshTokenResponse>("/auth/refresh", new { refreshToken });
    }

    public async Task<UserInfo> GetCurrentUserAsync()
    {
        return await GetAsync<UserInfo>("/auth/me");
    }

    // ============================================
    // AI ANALYZE
    // ============================================

    public async Task<AnalyzeResponse> AnalyzeAsync(
        string text,
        string? model = null,
        string? customPrompt = null,
        string? styleProfile = null,
        string? userInstructions = null,
        string? outputMode = null,
        string? prefixLanguage = null,
        string? consolidatedLessons = null,
        AIParameters? aiParameters = null,
        string[]? imageUrls = null,
        string[]? publicIds = null,
        string? promptId = null)
    {
        var payload = new
        {
            text,
            model,
            promptId,
            customPrompt,
            styleProfile,
            userInstructions,
            outputMode,
            prefixLanguage,
            consolidatedLessons,
            aiParameters,
            imageUrls,
            publicIds
        };
        return await PostAsync<AnalyzeResponse>("/ai/analyze", payload);
    }

    // ============================================
    // SUBSCRIPTION
    // ============================================

    public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync()
    {
        return await GetAsync<SubscriptionStatusResponse>("/subscription/status");
    }

    // ============================================
    // PROMPT TEMPLATES
    // ============================================

    public async Task<RemotePromptTemplatesResponse> GetPromptTemplatesAsync(string lang = "nl")
    {
        return await GetAsync<RemotePromptTemplatesResponse>($"/prompts/templates?lang={lang}");
    }

    // ============================================
    // INTEGRATIES
    // ============================================

    public async Task<Integration[]?> GetIntegrationsAsync()
    {
        var response = await GetAsync<GetIntegrationsResponse>("/integrations");
        return response?.Integrations;
    }

    // ── Jira ──

    public async Task<bool> TestJiraConnectionAsync(string jiraUrl, string email, string apiToken)
    {
        var r = await PostAsync<ApiResponse>("/integrations/jira/test", new { jiraUrl, email, apiToken });
        return r?.Success ?? false;
    }

    public async Task<JiraProject[]?> GetJiraProjectsWithCredentialsAsync(string jiraUrl, string email, string apiToken)
    {
        var r = await PostAsync<GetJiraProjectsResponse>("/integrations/jira/projects",
            new { jiraUrl, email, apiToken });
        return r?.Projects;
    }

    public async Task<JiraIssueType[]?> GetJiraIssueTypesWithCredentialsAsync(
        string jiraUrl, string email, string apiToken, string projectKey)
    {
        var r = await PostAsync<GetJiraIssueTypesResponse>("/integrations/jira/issue-types",
            new { jiraUrl, email, apiToken, projectKey });
        return r?.IssueTypes;
    }

    public async Task<bool> SaveJiraIntegrationAsync(string jiraUrl, string email, string? apiToken,
        string? defaultProject = null, string? defaultIssueType = null,
        string? customPrompt = null, string? model = null)
    {
        var r = await PostAsync<ApiResponse>("/integrations/jira/save",
            new { jiraUrl, email, apiToken, defaultProject, defaultIssueType, customPrompt, model });
        return r?.Success ?? false;
    }

    public async Task<JiraTicket?> CreateJiraTicketAsync(
        string projectKey, string issueTypeId, string summary, string description)
    {
        var r = await PostAsync<CreateJiraTicketResponse>("/integrations/jira/create-ticket",
            new { projectKey, issueTypeId, summary, description });
        return r?.Ticket;
    }

    // ── GitHub ──

    public async Task<bool> TestGitHubConnectionAsync(string token)
    {
        var r = await PostAsync<ApiResponse>("/integrations/github/test", new { token });
        return r?.Success ?? false;
    }

    public async Task<GitHubRepo[]?> GetGitHubReposAsync(string token)
    {
        var r = await PostAsync<GetGitHubReposResponse>("/integrations/github/repos", new { token });
        return r?.Repos;
    }

    public async Task<bool> SaveGitHubIntegrationAsync(string? token,
        string? defaultRepo = null, string? defaultLabels = null)
    {
        var r = await PostAsync<ApiResponse>("/integrations/github/save",
            new { token, defaultRepo, defaultLabels });
        return r?.Success ?? false;
    }

    public async Task<GitHubIssue?> CreateGitHubIssueAsync(string title, string body, string[]? labels = null)
    {
        var r = await PostAsync<CreateGitHubIssueResponse>("/integrations/github/create-issue",
            new { title, body, labels });
        return r?.Issue;
    }

    // ============================================
    // CLOUD SYNC
    // ============================================

    public async Task<ApiResponse> SyncHotkeysToCloudAsync(HotkeyConfig[] hotkeys, WritingStyle[] allStyles)
    {
        var payload = new
        {
            hotkeys = hotkeys.Select(h =>
            {
                string? styleProfile = h.StyleProfile;
                if (styleProfile == null && h.StyleId != null)
                    styleProfile = allStyles.FirstOrDefault(s => s.Id == h.StyleId)?.StyleProfile;

                return new
                {
                    id = h.Id,
                    name = h.Name,
                    enabled = h.Enabled,
                    key = h.Key,
                    modifierKeys = h.ModifierKeys,
                    customPrompt = h.CustomPrompt,
                    model = h.Model,
                    outputMode = h.OutputMode,
                    askForContext = h.AskForContext,
                    includeImages = h.IncludeImages,
                    styleId = h.StyleId,
                    consolidatedLessons = styleProfile,
                    prefixLanguage = h.PrefixLanguage,
                    integrationType = h.IntegrationType,
                    aiParameters = h.CustomAIParameters
                };
            }).ToArray()
        };
        return await PutAsync<ApiResponse>("/hotkeys/desktop", payload);
    }

    // ============================================
    // MODELS
    // ============================================

    public async Task<AvailableModelsResponse> GetAvailableModelsAsync()
    {
        return await GetAsync<AvailableModelsResponse>("/ai/models");
    }

    // ============================================
    // STYLE PROFILE
    // ============================================

    public async Task<GenerateStyleProfileResponse> GenerateStyleProfileAsync(string[] examples, string? usageContext = null)
    {
        return await PostAsync<GenerateStyleProfileResponse>("/ai/generate-style-profile",
            new { examples, usageContext });
    }
}

public class GenerateStyleProfileResponse : ApiResponse
{
    public string? StyleProfile { get; set; }
}
