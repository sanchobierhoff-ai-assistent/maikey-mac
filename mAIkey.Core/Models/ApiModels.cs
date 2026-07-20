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
