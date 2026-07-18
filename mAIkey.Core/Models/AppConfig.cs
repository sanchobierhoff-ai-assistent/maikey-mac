using System.Text.Json.Serialization;

namespace mAIkey.Core.Models;

public class AppConfig
{
    public string? AuthToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserEmail { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? SubscriptionTier { get; set; } = "free";
    public HotkeyConfig[]? Hotkeys { get; set; }
    public WritingStyle[]? WritingStyles { get; set; }
    public string? AppVersion { get; set; }
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public int MaxImages { get; set; } = 3;
    public int MaxCharacters { get; set; } = 1000;
    public string? ApiBaseUrl { get; set; } = "https://ai-assistent-backend-production.up.railway.app";
    public List<string> OnboardingCompletedIds { get; set; } = new();
    public DateTime? OnboardingCompletedAt { get; set; } = null;
    public bool HasShownDemoHotkey { get; set; } = false;
    public bool ShowAiIndicator { get; set; } = true;
    public bool SoundOnComplete { get; set; } = false;
    public string InterfaceLanguage { get; set; } = "en";
    public string Theme { get; set; } = "Dark";
}

public class HotkeyConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int ModifierKeys { get; set; }
    public int Key { get; set; }
    public string? PromptId { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Model { get; set; } = "gpt-4o-mini";
    public string OutputMode { get; set; } = "replace";
    public bool AskForContext { get; set; } = false;
    [JsonPropertyName("useInputInsteadOfSelection")]
    public bool UseInputInsteadOfSelection { get; set; } = false;
    public bool IncludeImages { get; set; } = true;
    [JsonPropertyName("useScreenCapture")]
    public bool UseScreenCapture { get; set; } = false;
    public string? PrefixLanguage { get; set; } = "NL";
    public string? StyleId { get; set; }
    public string? CustomPrompt { get; set; }
    public bool FrozenByDowngrade { get; set; } = false;
    public Dictionary<string, string>? TemplateVariables { get; set; }
    public AIParameters? CustomAIParameters { get; set; }
    public List<UsageExample>? RecentUsage { get; set; }
    public string? IntegrationType { get; set; }
    public IntegrationAction? IntegrationAction { get; set; }
    public string? ConsolidatedLessons { get; set; }
    public string? StyleProfile { get; set; }
}

public class IntegrationAction
{
    public string Action { get; set; } = "";
    public bool ShowReviewWindow { get; set; } = true;
    public string? DefaultAssignee { get; set; }
    public string? DefaultProject { get; set; }
    public string? DefaultRepo { get; set; }
    public string? DefaultLabels { get; set; }
    public string? DefaultTo { get; set; }
    public string? DefaultSubject { get; set; }
    public string? DefaultCc { get; set; }
    public string? DefaultCalendarId { get; set; }
}

public class AIParameters
{
    public double? Temperature { get; set; } = 0.7;
    public int? MaxTokens { get; set; } = 8000;
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
}

public class UsageExample
{
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public string Task { get; set; } = "";
    public string Context { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class WritingStyle
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? UsageContext { get; set; }
    public string? StyleProfile { get; set; }
    public string[]? TextExamples { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
    public StyleMetadata? Metadata { get; set; }
}

public class StyleMetadata
{
    public string? Formality { get; set; }
    public string? Language { get; set; }
}
