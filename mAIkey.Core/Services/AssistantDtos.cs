namespace mAIkey.Core.Services;

// DTO's die in de Windows-app in de views staan (AIHotkeyBuilderWindow,
// AIPromptOptimizerWindow, FeedbackWindow) maar door de ApiClient gebruikt worden.

public class BuildHotkeyResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool IsReady { get; set; }
    public AIHotkeyConfig? Config { get; set; }
    public List<ChatMessage>? ConversationHistory { get; set; }
    public string? Error { get; set; }
}

public class AIHotkeyConfigWrapper
{
    public bool Ready { get; set; }
    public AIHotkeyConfig? Config { get; set; }
    public List<string>? Warnings { get; set; }
}

public class AIHotkeyConfig
{
    public string Name { get; set; } = "";
    public string CustomPrompt { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string OutputMode { get; set; } = "window";
    public string? StyleId { get; set; }
    public bool IncludeImages { get; set; } = true;
    public bool UseScreenCapture { get; set; } = false;
    public bool UseInputInsteadOfSelection { get; set; } = false;
    public CustomAIParams? CustomAIParameters { get; set; }
}

public class CustomAIParams
{
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
}

public class OptimizePromptResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool IsReady { get; set; }
    public List<ChatMessage>? ConversationHistory { get; set; }
    public string? Error { get; set; }
}

public class ConversationMessage
{
    public string Role { get; set; } = ""; // "user" of "assistant"
    public string Content { get; set; } = "";
}
