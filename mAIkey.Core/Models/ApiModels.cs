using System.Text.Json.Serialization;

namespace mAIkey.Core.Models;

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LoginResponse : ApiResponse
{
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Tier { get; set; }
    public string? ErrorType { get; set; }
}

public class RegisterResponse : ApiResponse
{
    public string? UserId { get; set; }
}

public class RefreshTokenResponse : ApiResponse
{
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
}

public class AnalyzeResponse : ApiResponse
{
    // De backend stuurt het AI-resultaat in het veld "response".
    [JsonPropertyName("response")]
    public string? Output { get; set; }
    public int? TokensUsed { get; set; }
    public string? Model { get; set; }
}

public class RemoteTemplateVariable
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Placeholder { get; set; } = "";
}

public class RemoteIntegrationAction
{
    public string Action { get; set; } = "";
    public bool ShowReviewWindow { get; set; } = true;
}

public class RemotePromptTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string CustomPrompt { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string OutputMode { get; set; } = "clipboard";
    public float Temperature { get; set; } = 0.5f;
    public int MaxTokens { get; set; } = 8000;
    public bool RequiresContext { get; set; }
    public bool RequiresStyleWarning { get; set; }
    public List<RemoteTemplateVariable> TemplateVariables { get; set; } = new();
    public bool IncludeImages { get; set; } = true;
    public int SortOrder { get; set; }
    public string? IntegrationType { get; set; }
    public RemoteIntegrationAction? IntegrationAction { get; set; }
    public bool UseScreenCapture { get; set; }
}

public class RemotePromptTemplatesResponse
{
    public List<RemotePromptTemplate> Templates { get; set; } = new();
    public int Count { get; set; }
    public string? UpdatedAt { get; set; }
}

public class IntegrationConfig
{
    public string? DefaultProject { get; set; }
    public string? DefaultIssueType { get; set; }
    public string? DefaultRepo { get; set; }
    public string? DefaultLabels { get; set; }
}

public class Integration
{
    public string Id { get; set; } = "";
    public string IntegrationType { get; set; } = "";
    public bool IsActive { get; set; }
    public IntegrationConfig? Config { get; set; }
}

public class JiraTicket
{
    public string Key { get; set; } = "";
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
}

public class CreateJiraTicketResponse : ApiResponse
{
    public JiraTicket? Ticket { get; set; }
}

public class GitHubRepo
{
    public string FullName { get; set; } = "";
    public string Name { get; set; } = "";
    public override string ToString() => FullName;
}

public class GitHubIssue
{
    public int Number { get; set; }
    public string HtmlUrl { get; set; } = "";
    public string Title { get; set; } = "";
}

public class GetGitHubReposResponse : ApiResponse
{
    public GitHubRepo[]? Repos { get; set; }
}

public class CreateGitHubIssueResponse : ApiResponse
{
    public GitHubIssue? Issue { get; set; }
}

public class StartGoogleOAuthResponse : ApiResponse
{
    public string? Url { get; set; }
}

public class GoogleOAuthStatusResponse : ApiResponse
{
    public bool IsConnected { get; set; }
    public string? Email { get; set; }
}

public class GetIntegrationsResponse : ApiResponse
{
    public Integration[]? Integrations { get; set; }
}

public class JiraProject
{
    public string Id { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

public class JiraIssueType
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public override string ToString() => Name;
}

public class GetJiraProjectsResponse : ApiResponse
{
    public JiraProject[]? Projects { get; set; }
}

public class GetJiraIssueTypesResponse : ApiResponse
{
    public JiraIssueType[]? IssueTypes { get; set; }
}

public class SubscriptionStatusResponse : ApiResponse
{
    public string? Tier { get; set; }
    public int RequestsUsed { get; set; }
    public int MaxMonthlyRequests { get; set; }
    public int MaxHotkeys { get; set; }
    public int DailyUsed { get; set; }
    public int? DailyCap { get; set; }
    public string? MonthlyResetDate { get; set; }
}

public class AvailableModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public int Speed { get; set; }
    public int Intelligence { get; set; }
    public int Usage { get; set; }
    public string? MinTier { get; set; }
}

public class AvailableModelsResponse : ApiResponse
{
    public AvailableModel[]? Models { get; set; }
}

public class UserInfo : ApiResponse
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Tier { get; set; }
}

public class TrainingExample
{
    public string? Input { get; set; }
    public string? Output { get; set; }
}

public class OrderedContentBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
}
