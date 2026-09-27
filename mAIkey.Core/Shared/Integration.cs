using System;

namespace mAIkey.Core.Models
{
    public class Integration
    {
        public string Id { get; set; } = "";
        public string IntegrationType { get; set; } = ""; // "jira", "github", etc.
        public IntegrationConfig Config { get; set; } = new IntegrationConfig();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class IntegrationConfig
    {
        // Jira specific
        public string? JiraUrl { get; set; }
        public string? Email { get; set; }
        public string? DefaultProject { get; set; }
        public string? DefaultIssueType { get; set; }
        public string? CustomPrompt { get; set; } // User's custom ticket generation prompt
        public string? Model { get; set; }        // AI model to use for this integration

        // GitHub specific
        public string? DefaultRepo { get; set; }    // "owner/repo"
        public string? DefaultLabels { get; set; }  // comma-separated

        // Slack / Teams specific
        public string? DefaultChannel { get; set; } // "#general"

        // Zapier / Make specific
        public string? WebhookName { get; set; }    // User-friendly name

        // Todoist / Asana specific
        public string? DefaultProjectId { get; set; }
        public string? DefaultProjectName { get; set; }

        // Trello specific
        public string? DefaultBoardId { get; set; }
        public string? DefaultBoardName { get; set; }
        public string? DefaultListId { get; set; }
        public string? DefaultListName { get; set; }

        // Asana specific
        public string? DefaultWorkspaceId { get; set; }
        public string? DefaultWorkspaceName { get; set; }

        // Gmail specific
        public string? GmailEmail { get; set; }

        // Google Calendar specific
        public string? DefaultCalendarId { get; set; }
        public string? DefaultCalendarName { get; set; }

        // Google Tasks specific
        public string? DefaultTaskListId { get; set; }
        public string? DefaultTaskListName { get; set; }
    }

    public class JiraProject
    {
        public string Id { get; set; } = "";
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string ProjectTypeKey { get; set; } = "";

        public override string ToString() => Name;
    }

    public class JiraIssueType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
        public override string ToString() => Name;
    }

    public class JiraUser
    {
        public string AccountId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? EmailAddress { get; set; }
        public string? AvatarUrl { get; set; }

        public override string ToString() => DisplayName;
    }

    public class JiraTicketDraft
    {
        public string Summary { get; set; } = "";
        public string Description { get; set; } = "";
        public string IssueType { get; set; } = "Task";
        public string Priority { get; set; } = "Medium";
        public string[] Labels { get; set; } = Array.Empty<string>();
        public string? AssigneeAccountId { get; set; }
        public string? AssigneeDisplayName { get; set; }
        public string[]? ImageUrls { get; set; }

        // Per-image relevance judged by the AI (aligned to ImageUrls order)
        public JiraImageRelevance[]? ImageRelevance { get; set; }

        // Multiple-ticket / Epic support
        public bool Multiple { get; set; }
        public JiraEpicSuggestion? EpicSuggestion { get; set; }
        public JiraTicketDraft[]? Tickets { get; set; }
    }

    public class JiraImageRelevance
    {
        public int Index { get; set; }
        public string? Url { get; set; }
        public bool Relevant { get; set; } = true;
        public string? Reason { get; set; }
    }

    public class JiraEpicSuggestion
    {
        public string Summary { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class JiraTicket
    {
        public string Key { get; set; } = "";
        public string Id { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class JiraIssueSummary
    {
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Status { get; set; } = "";
        public string IssueType { get; set; } = "";
        public string Url { get; set; } = "";

        // Convenience for list display: "ABC-123 · In Progress"
        public string Display => $"{Key}  ·  {Status}";
    }

    public class JiraIssueDetail
    {
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class JiraFieldMeta
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Required { get; set; }
        public string FieldType { get; set; } = "string";
        public JiraFieldOption[]? AllowedValues { get; set; }
    }

    public class JiraFieldOption
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    // ============================================
    // GITHUB MODELS
    // ============================================

    public class GitHubRepo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";  // "owner/repo"
        public bool Private { get; set; }
        public string Owner { get; set; } = "";

        public override string ToString() => FullName;
    }

    public class GitHubIssueDraft
    {
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string[] Labels { get; set; } = Array.Empty<string>();
        public string IssueType { get; set; } = "enhancement";
    }

    public class GitHubIssue
    {
        public int Number { get; set; }
        public string HtmlUrl { get; set; } = "";
        public string Title { get; set; } = "";
    }

    // ============================================
    // TODOIST MODELS
    // ============================================

    public class TodoistProject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    public class TodoistTaskDraft
    {
        public string Content { get; set; } = "";
        public string Description { get; set; } = "";
        public string DueString { get; set; } = "";
        public int Priority { get; set; } = 1;   // Todoist API-schaal: 4=urgent (P1) ... 1=normaal (P4)
        public string[] Labels { get; set; } = Array.Empty<string>();
    }

    public class TodoistTask
    {
        public string Id { get; set; } = "";
        public string Url { get; set; } = "";
    }

    // ============================================
    // TRELLO MODELS
    // ============================================

    public class TrelloBoard
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    public class TrelloList
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    public class TrelloCardDraft
    {
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public string Due { get; set; } = "";    // ISO 8601 of leeg
    }

    public class TrelloCard
    {
        public string Id { get; set; } = "";
        public string ShortUrl { get; set; } = "";
    }

    // ============================================
    // ASANA MODELS
    // ============================================

    public class AsanaWorkspace
    {
        public string Gid { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    public class AsanaProject
    {
        public string Gid { get; set; } = "";
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    public class AsanaTaskDraft
    {
        public string Name { get; set; } = "";
        public string Notes { get; set; } = "";
        public string DueOn { get; set; } = "";  // YYYY-MM-DD of leeg
    }

    public class AsanaTask
    {
        public string Gid { get; set; } = "";
        public string PermalinkUrl { get; set; } = "";
    }

    // ============================================
    // SLACK MODELS
    // ============================================

    public class SlackMessageDraft
    {
        public string Message { get; set; } = "";
        public string? SuggestedChannel { get; set; }
    }

    // ============================================
    // MICROSOFT TEAMS MODELS
    // ============================================

    public class TeamsMessageDraft
    {
        public string Message { get; set; } = "";
        public string? SuggestedChannel { get; set; }
    }

    // ============================================
    // ZAPIER / MAKE MODELS
    // ============================================

    public class ZapierPayloadDraft
    {
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Body { get; set; } = "";
        public string Category { get; set; } = "other";
        public string Priority { get; set; } = "medium";
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string[] ActionItems { get; set; } = Array.Empty<string>();
        public string[] PeopleMentioned { get; set; } = Array.Empty<string>();
        public string[] DatesMentioned { get; set; } = Array.Empty<string>();
    }

    // ============================================
    // GMAIL MODELS
    // ============================================

    public class GmailDraft
    {
        public string To { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string Cc { get; set; } = "";
        public string Tone { get; set; } = "professioneel";
    }

    // ============================================
    // GOOGLE CALENDAR MODELS
    // ============================================

    public class CalendarEventDraft
    {
        public string Title { get; set; } = "";
        public string StartDateTime { get; set; } = "";
        public string EndDateTime { get; set; } = "";
        public string Description { get; set; } = "";
        public string Attendees { get; set; } = "";
        public string Location { get; set; } = "";
    }

    public class GoogleCalendar
    {
        public string Id { get; set; } = "";
        public string Summary { get; set; } = "";
        public bool Primary { get; set; }
        public string? BackgroundColor { get; set; }

        public override string ToString() => Primary ? $"{Summary} (Primair)" : Summary;
    }

    public class CalendarEvent
    {
        public string Id { get; set; } = "";
        public string HtmlLink { get; set; } = "";
        public string Summary { get; set; } = "";
    }

    // ============================================
    // GOOGLE TASKS MODELS
    // ============================================

    public class GoogleTaskList
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";

        public override string ToString() => Title;
    }

    public class GoogleTaskDraft
    {
        public string Title { get; set; } = "";
        public string Notes { get; set; } = "";
        public string DueOn { get; set; } = "";  // YYYY-MM-DD of leeg (Google Tasks kent geen tijd)
    }

    public class GoogleTask
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
