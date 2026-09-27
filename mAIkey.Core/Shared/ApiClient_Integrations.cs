using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using mAIkey.Core.Models;

namespace mAIkey.Core.Services
{
    // Partial class to separate integration-specific API methods
    public partial class ApiClient
    {
        // ============================================
        // JIRA INTEGRATION METHODS
        // ============================================

        public async Task<Integration[]?> GetIntegrationsAsync()
        {
            var response = await GetAsync<GetIntegrationsResponse>("/integrations");
            return response?.Integrations;
        }

        public async Task<bool> TestJiraConnectionAsync(string jiraUrl, string email, string apiToken)
        {
            var payload = new { jiraUrl, email, apiToken };
            var response = await PostAsync<TestJiraResponse>("/integrations/jira/test", payload);
            return response?.Success ?? false;
        }

        public async Task<JiraProject[]?> GetJiraProjectsAsync()
        {
            var response = await GetAsync<GetJiraProjectsResponse>("/integrations/jira/projects");
            return response?.Projects;
        }

        public async Task<JiraProject[]?> GetJiraProjectsWithCredentialsAsync(string jiraUrl, string email, string apiToken)
        {
            var payload = new { jiraUrl, email, apiToken };
            var response = await PostAsync<GetJiraProjectsResponse>("/integrations/jira/projects", payload);
            return response?.Projects;
        }

        public async Task<JiraIssueType[]?> GetJiraIssueTypesAsync(string projectKey)
        {
            var response = await GetAsync<GetJiraIssueTypesResponse>($"/integrations/jira/issue-types?projectKey={projectKey}");
            return response?.IssueTypes;
        }

        public async Task<JiraIssueType[]?> GetJiraIssueTypesWithCredentialsAsync(string jiraUrl, string email, string apiToken, string projectKey)
        {
            var payload = new { jiraUrl, email, apiToken, projectKey };
            var response = await PostAsync<GetJiraIssueTypesResponse>("/integrations/jira/issue-types", payload);
            return response?.IssueTypes;
        }

        public async Task<JiraUser[]?> GetJiraAssignableUsersAsync(string projectKey)
        {
            var response = await GetAsync<GetJiraUsersResponse>($"/integrations/jira/users?projectKey={projectKey}");
            return response?.Users;
        }

        public async Task<JiraFieldMeta[]?> GetJiraCreateMetaAsync(string projectKey, string issueTypeId)
        {
            var response = await GetAsync<GetJiraCreateMetaResponse>(
                $"/integrations/jira/createmeta?projectKey={projectKey}&issueTypeId={issueTypeId}");
            return response?.Fields;
        }

        public async Task<bool> SaveJiraIntegrationAsync(string jiraUrl, string email, string? apiToken,
            string? defaultProject = null, string? defaultIssueType = null, string? customPrompt = null, string? model = null)
        {
            var payload = new
            {
                jiraUrl,
                email,
                apiToken,
                defaultProject,
                defaultIssueType,
                customPrompt,
                model
            };

            var response = await PostAsync<SaveJiraResponse>("/integrations/jira/save", payload);
            return response?.Success ?? false;
        }

        public async Task<JiraTicketDraft?> GenerateJiraTicketAsync(string text, string? customPrompt = null, string? model = null, string[]? imageUrls = null)
        {
            var payload = new { text, customPrompt, model, imageUrls };
            var response = await PostAsync<GenerateJiraTicketResponse>("/ai/generate-jira-ticket", payload);
            return response?.Ticket;
        }

        public async Task<JiraTicket?> CreateJiraTicketAsync(string projectKey, string issueTypeId,
            string summary, string description, string? priority = null, string[]? labels = null,
            string? assigneeAccountId = null, Dictionary<string, object>? dynamicFields = null,
            string[]? imageUrls = null)
        {
            var payload = new Dictionary<string, object?>
            {
                ["projectKey"] = projectKey,
                ["issueTypeId"] = issueTypeId,
                ["summary"] = summary,
                ["description"] = description,
                ["priority"] = priority,
                ["labels"] = labels,
                ["assigneeAccountId"] = assigneeAccountId,
                ["dynamicFields"] = dynamicFields,
                ["imageUrls"] = imageUrls
            };

            var response = await PostAsync<CreateJiraTicketResponse>("/integrations/jira/create-ticket", payload);
            return response?.Ticket;
        }

        public async Task<bool> DeleteIntegrationAsync(string integrationId)
        {
            var response = await DeleteAsync<DeleteIntegrationResponse>($"/integrations/{integrationId}");
            return response?.Success ?? false;
        }

        // ---- Jira: search & edit existing issues ----

        public async Task<JiraIssueSummary[]?> SearchJiraIssuesAsync(string query, string? projectKey = null)
        {
            var payload = new { query, projectKey };
            var response = await PostAsync<SearchJiraIssuesResponse>("/integrations/jira/search-issues", payload);
            return response?.Issues;
        }

        public async Task<JiraIssueDetail?> GetJiraIssueAsync(string issueKey)
        {
            var response = await GetAsync<GetJiraIssueResponse>(
                $"/integrations/jira/issue?issueKey={Uri.EscapeDataString(issueKey)}");
            return response?.Issue;
        }

        public async Task<bool> UpdateJiraIssueAsync(string issueKey, string? summary, string? description)
        {
            var payload = new { issueKey, summary, description };
            var response = await PutAsync<UpdateJiraIssueResponse>("/integrations/jira/update-issue", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> AddJiraCommentAsync(string issueKey, string comment)
        {
            var payload = new { issueKey, comment };
            var response = await PostAsync<AddJiraCommentResponse>("/integrations/jira/add-comment", payload);
            return response?.Success ?? false;
        }

        // ---- Jira: create multiple tickets (optionally under an Epic) ----
        // `tickets` items are anonymous objects: { summary, description, issueType, priority, labels, imageUrls }
        public async Task<CreateJiraTicketsResponse?> CreateJiraTicketsAsync(
            string projectKey, bool createEpic, object? epic, System.Collections.Generic.IEnumerable<object> tickets,
            object? dynamicFields = null)
        {
            var payload = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["projectKey"] = projectKey,
                ["createEpic"] = createEpic,
                ["epic"] = epic,
                ["tickets"] = tickets,
                ["dynamicFields"] = dynamicFields // gedeelde verplichte velden (bv. Components) voor epic + tickets
            };
            return await PostAsync<CreateJiraTicketsResponse>("/integrations/jira/create-tickets", payload);
        }

        // Bridge (Zendesk ⇄ Jira) API-methoden staan in ApiClient.Bridge.cs.

        // ============================================
        // GITHUB INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestGitHubConnectionAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<TestGitHubResponse>("/integrations/github/test", payload);
            return response?.Success ?? false;
        }

        public async Task<GitHubRepo[]?> GetGitHubReposAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<GetGitHubReposResponse>("/integrations/github/repos", payload);
            return response?.Repos;
        }

        public async Task<bool> SaveGitHubIntegrationAsync(string? token, string? defaultRepo = null, string? defaultLabels = null, string? customPrompt = null, string? model = null)
        {
            var payload = new { token, defaultRepo, defaultLabels, customPrompt, model };
            var response = await PostAsync<SaveGitHubResponse>("/integrations/github/save", payload);
            return response?.Success ?? false;
        }

        public async Task<GitHubIssueDraft?> GenerateGitHubIssueAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateGitHubIssueResponse>("/ai/generate-github-issue", payload);
            return response?.Issue;
        }

        public async Task<GitHubIssue?> CreateGitHubIssueAsync(string title, string body, string[]? labels = null, string[]? assignees = null)
        {
            var payload = new { title, body, labels, assignees };
            var response = await PostAsync<CreateGitHubIssueResponse>("/integrations/github/create-issue", payload);
            return response?.Issue;
        }

        // ============================================
        // TODOIST INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestTodoistConnectionAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<TestTodoistResponse>("/integrations/todoist/test", payload);
            return response?.Success ?? false;
        }

        public async Task<TodoistProject[]?> GetTodoistProjectsAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<GetTodoistProjectsResponse>("/integrations/todoist/projects", payload);
            return response?.Projects;
        }

        public async Task<bool> SaveTodoistIntegrationAsync(string? token, string? defaultProjectId = null,
            string? defaultProjectName = null, string? defaultLabels = null)
        {
            var payload = new { token, defaultProjectId, defaultProjectName, defaultLabels };
            var response = await PostAsync<SaveTodoistResponse>("/integrations/todoist/save", payload);
            return response?.Success ?? false;
        }

        public async Task<TodoistTaskDraft?> GenerateTodoistTaskAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateTodoistTaskResponse>("/ai/generate-todoist-task", payload);
            return response?.Task;
        }

        public async Task<TodoistTask?> CreateTodoistTaskAsync(string content, string? description = null,
            string? dueString = null, int priority = 1, string[]? labels = null)
        {
            var payload = new { content, description, dueString, priority, labels };
            var response = await PostAsync<CreateTodoistTaskResponse>("/integrations/todoist/create-task", payload);
            return response?.Task;
        }

        // ============================================
        // TRELLO INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestTrelloConnectionAsync(string apiKey, string token)
        {
            var payload = new { apiKey, token };
            var response = await PostAsync<TestTrelloResponse>("/integrations/trello/test", payload);
            return response?.Success ?? false;
        }

        public async Task<TrelloBoard[]?> GetTrelloBoardsAsync(string apiKey, string token)
        {
            var payload = new { apiKey, token };
            var response = await PostAsync<GetTrelloBoardsResponse>("/integrations/trello/boards", payload);
            return response?.Boards;
        }

        public async Task<TrelloList[]?> GetTrelloListsAsync(string apiKey, string token, string boardId)
        {
            var payload = new { apiKey, token, boardId };
            var response = await PostAsync<GetTrelloListsResponse>("/integrations/trello/lists", payload);
            return response?.Lists;
        }

        public async Task<bool> SaveTrelloIntegrationAsync(string? apiKey, string? token,
            string? defaultBoardId = null, string? defaultBoardName = null,
            string? defaultListId = null, string? defaultListName = null)
        {
            var payload = new { apiKey, token, defaultBoardId, defaultBoardName, defaultListId, defaultListName };
            var response = await PostAsync<SaveTrelloResponse>("/integrations/trello/save", payload);
            return response?.Success ?? false;
        }

        public async Task<TrelloCardDraft?> GenerateTrelloCardAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateTrelloCardResponse>("/ai/generate-trello-card", payload);
            return response?.Card;
        }

        public async Task<TrelloCard?> CreateTrelloCardAsync(string name, string? desc = null, string? due = null)
        {
            var payload = new { name, desc, due };
            var response = await PostAsync<CreateTrelloCardResponse>("/integrations/trello/create-card", payload);
            return response?.Card;
        }

        // ============================================
        // ASANA INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestAsanaConnectionAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<TestAsanaResponse>("/integrations/asana/test", payload);
            return response?.Success ?? false;
        }

        public async Task<AsanaWorkspace[]?> GetAsanaWorkspacesAsync(string token)
        {
            var payload = new { token };
            var response = await PostAsync<GetAsanaWorkspacesResponse>("/integrations/asana/workspaces", payload);
            return response?.Workspaces;
        }

        public async Task<AsanaProject[]?> GetAsanaProjectsAsync(string token, string workspaceId)
        {
            var payload = new { token, workspaceId };
            var response = await PostAsync<GetAsanaProjectsResponse>("/integrations/asana/projects", payload);
            return response?.Projects;
        }

        public async Task<bool> SaveAsanaIntegrationAsync(string? token,
            string? defaultWorkspaceId = null, string? defaultWorkspaceName = null,
            string? defaultProjectId = null, string? defaultProjectName = null)
        {
            var payload = new { token, defaultWorkspaceId, defaultWorkspaceName, defaultProjectId, defaultProjectName };
            var response = await PostAsync<SaveAsanaResponse>("/integrations/asana/save", payload);
            return response?.Success ?? false;
        }

        public async Task<AsanaTaskDraft?> GenerateAsanaTaskAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateAsanaTaskResponse>("/ai/generate-asana-task", payload);
            return response?.Task;
        }

        public async Task<AsanaTask?> CreateAsanaTaskAsync(string name, string? notes = null, string? dueOn = null)
        {
            var payload = new { name, notes, dueOn };
            var response = await PostAsync<CreateAsanaTaskResponse>("/integrations/asana/create-task", payload);
            return response?.Task;
        }

        // ============================================
        // SLACK INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestSlackConnectionAsync(string webhookUrl)
        {
            var payload = new { webhookUrl };
            var response = await PostAsync<TestSlackResponse>("/integrations/slack/test", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> SaveSlackIntegrationAsync(string? webhookUrl, string? defaultChannel = null, string? customPrompt = null, string? model = null)
        {
            var payload = new { webhookUrl, defaultChannel, customPrompt, model };
            var response = await PostAsync<SaveSlackResponse>("/integrations/slack/save", payload);
            return response?.Success ?? false;
        }

        public async Task<SlackMessageDraft?> GenerateSlackMessageAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateSlackMessageResponse>("/ai/generate-slack-message", payload);
            return response?.Message;
        }

        public async Task<bool> SendSlackMessageAsync(string message)
        {
            var payload = new { message };
            var response = await PostAsync<SendSlackMessageResponse>("/integrations/slack/send", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // MICROSOFT TEAMS INTEGRATION METHODS
        // ============================================

        public async Task<bool> TestTeamsConnectionAsync(string webhookUrl)
        {
            var payload = new { webhookUrl };
            var response = await PostAsync<TestTeamsResponse>("/integrations/teams/test", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> SaveTeamsIntegrationAsync(string? webhookUrl, string? defaultChannel = null, string? customPrompt = null, string? model = null)
        {
            var payload = new { webhookUrl, defaultChannel, customPrompt, model };
            var response = await PostAsync<SaveTeamsResponse>("/integrations/teams/save", payload);
            return response?.Success ?? false;
        }

        public async Task<TeamsMessageDraft?> GenerateTeamsMessageAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateTeamsMessageResponse>("/ai/generate-teams-message", payload);
            return response?.Message;
        }

        public async Task<bool> SendTeamsMessageAsync(string message)
        {
            var payload = new { message };
            var response = await PostAsync<SendTeamsMessageResponse>("/integrations/teams/send", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // ZAPIER / MAKE WEBHOOK METHODS
        // ============================================

        public async Task<bool> TestZapierConnectionAsync(string webhookUrl)
        {
            var payload = new { webhookUrl };
            var response = await PostAsync<TestZapierResponse>("/integrations/zapier/test", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> SaveZapierIntegrationAsync(string? webhookUrl, string? webhookName = null, string? model = null)
        {
            var payload = new { webhookUrl, webhookName, model };
            var response = await PostAsync<SaveZapierResponse>("/integrations/zapier/save", payload);
            return response?.Success ?? false;
        }

        public async Task<ZapierPayloadDraft?> GenerateZapierPayloadAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateZapierPayloadResponse>("/ai/generate-zapier-payload", payload);
            return response?.Payload;
        }

        public async Task<bool> SendZapierDataAsync(string payloadJson)
        {
            var payload = new { payload = System.Text.Json.JsonSerializer.Deserialize<object>(payloadJson) };
            var response = await PostAsync<SendZapierResponse>("/integrations/zapier/send", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // GOOGLE OAUTH METHODS
        // ============================================

        public async Task<string?> StartGoogleOAuthAsync(string integrationType)
        {
            var payload = new { integrationType };
            var response = await PostAsync<StartGoogleOAuthResponse>("/oauth/google/start", payload);
            return response?.Url;
        }

        public async Task<(bool isConnected, string? email)> GetGoogleOAuthStatusAsync(string integrationType)
        {
            var response = await GetAsync<GoogleOAuthStatusResponse>($"/oauth/google/status/{integrationType}");
            return (response?.IsConnected ?? false, response?.Email);
        }

        // ============================================
        // GMAIL INTEGRATION METHODS
        // ============================================

        public async Task<GmailDraft?> GenerateGmailDraftAsync(string text, string? customPrompt = null, string? model = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[GMAIL API] POST /ai/generate-gmail — text: {text?.Length ?? 0} chars, model: {model ?? "null"}");
                var payload = new { text, customPrompt, model };
                var response = await PostAsync<GenerateGmailDraftResponse>("/ai/generate-gmail", payload);
                System.Diagnostics.Debug.WriteLine($"[GMAIL API] Response: {(response == null ? "NULL" : $"Draft={response.Draft != null}")}");
                return response?.Draft;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GMAIL API] ERROR: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SendGmailAsync(string to, string subject, string body, string? cc = null)
        {
            var payload = new { to, subject, body, cc };
            var response = await PostAsync<SendGmailResponse>("/integrations/gmail/send", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> CreateGmailDraftAsync(string to, string subject, string body, string? cc = null)
        {
            var payload = new { to, subject, body, cc };
            var response = await PostAsync<CreateGmailDraftResponse>("/integrations/gmail/draft", payload);
            return response?.Success ?? false;
        }

        public async Task<bool> SaveGmailConfigAsync(string? customPrompt, string? model = null)
        {
            var payload = new { customPrompt, model };
            var response = await PostAsync<SendGmailResponse>("/integrations/gmail/save-config", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // GOOGLE CALENDAR INTEGRATION METHODS
        // ============================================

        public async Task<GoogleCalendar[]?> GetCalendarListAsync()
        {
            var response = await GetAsync<GetCalendarListResponse>("/integrations/calendar/calendars");
            return response?.Calendars;
        }

        public async Task<CalendarEventDraft?> GenerateCalendarEventAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateCalendarEventResponse>("/ai/generate-calendar-event", payload);
            return response?.Event;
        }

        public async Task<CalendarEvent?> CreateCalendarEventAsync(string title, string startDateTime, string endDateTime,
            string? description = null, string? attendees = null, string? location = null, string? calendarId = null)
        {
            var payload = new { title, startDateTime, endDateTime, description, attendees, location, calendarId };
            var response = await PostAsync<CreateCalendarEventResponse>("/integrations/calendar/create-event", payload);
            return response?.Event;
        }

        public async Task<bool> SaveCalendarConfigAsync(string? defaultCalendarId, string? customPrompt, string? model = null)
        {
            var payload = new { defaultCalendarId, customPrompt, model };
            var response = await PostAsync<CreateCalendarEventResponse>("/integrations/calendar/save-config", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // GOOGLE TASKS INTEGRATION METHODS
        // ============================================

        public async Task<GoogleTaskList[]?> GetTaskListsAsync()
        {
            var response = await GetAsync<GetTaskListsResponse>("/integrations/gtasks/tasklists");
            return response?.TaskLists;
        }

        public async Task<GoogleTaskDraft?> GenerateGoogleTaskAsync(string text, string? customPrompt = null, string? model = null)
        {
            var payload = new { text, customPrompt, model };
            var response = await PostAsync<GenerateGoogleTaskResponse>("/ai/generate-google-task", payload);
            return response?.Task;
        }

        public async Task<GoogleTask?> CreateGoogleTaskAsync(string title, string? notes = null, string? dueOn = null)
        {
            var payload = new { title, notes, dueOn };
            var response = await PostAsync<CreateGoogleTaskResponse>("/integrations/gtasks/create-task", payload);
            return response?.Task;
        }

        public async Task<bool> SaveGoogleTasksConfigAsync(string? defaultTaskListId, string? defaultTaskListName)
        {
            var payload = new { defaultTaskListId, defaultTaskListName };
            var response = await PostAsync<SaveGoogleTasksConfigResponse>("/integrations/gtasks/save-config", payload);
            return response?.Success ?? false;
        }

        // ============================================
        // RESPONSE CLASSES
        // ============================================

        public class GetIntegrationsResponse
        {
            public bool Success { get; set; }
            public Integration[]? Integrations { get; set; }
            public string? Error { get; set; }
        }

        public class TestJiraResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GetJiraProjectsResponse
        {
            public bool Success { get; set; }
            public JiraProject[]? Projects { get; set; }
            public string? Error { get; set; }
        }

        public class GetJiraIssueTypesResponse
        {
            public bool Success { get; set; }
            public JiraIssueType[]? IssueTypes { get; set; }
            public string? Error { get; set; }
        }

        public class GetJiraUsersResponse
        {
            public bool Success { get; set; }
            public JiraUser[]? Users { get; set; }
            public string? Error { get; set; }
        }

        public class GetJiraCreateMetaResponse
        {
            public bool Success { get; set; }
            public JiraFieldMeta[]? Fields { get; set; }
            public string? Error { get; set; }
        }

        public class SaveJiraResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateJiraTicketResponse
        {
            public bool Success { get; set; }
            public JiraTicketDraft? Ticket { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateJiraTicketResponse
        {
            public bool Success { get; set; }
            public JiraTicket? Ticket { get; set; }
            public string? Error { get; set; }
        }

        public class DeleteIntegrationResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        // Bridge (Zendesk ⇄ Jira) DTO's staan in ApiClient.Bridge.cs (top-level in Services-namespace).

        public class SearchJiraIssuesResponse
        {
            public bool Success { get; set; }
            public JiraIssueSummary[]? Issues { get; set; }
            public string? Error { get; set; }
        }

        public class GetJiraIssueResponse
        {
            public bool Success { get; set; }
            public JiraIssueDetail? Issue { get; set; }
            public string? Error { get; set; }
        }

        public class UpdateJiraIssueResponse
        {
            public bool Success { get; set; }
            public JiraTicket? Ticket { get; set; }
            public string? Error { get; set; }
        }

        public class AddJiraCommentResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class CreateJiraTicketsResponse
        {
            public bool Success { get; set; }
            public JiraTicket? Epic { get; set; }
            public JiraCreatedTicket[]? Tickets { get; set; }
            public string? Error { get; set; }
        }

        public class JiraCreatedTicket
        {
            public string? Key { get; set; }
            public string? Id { get; set; }
            public string? Url { get; set; }
            public int AttachedImages { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // GITHUB RESPONSE CLASSES
        // ============================================

        public class TestGitHubResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GetGitHubReposResponse
        {
            public bool Success { get; set; }
            public GitHubRepo[]? Repos { get; set; }
            public string? Error { get; set; }
        }

        public class SaveGitHubResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateGitHubIssueResponse
        {
            public bool Success { get; set; }
            public GitHubIssueDraft? Issue { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateGitHubIssueResponse
        {
            public bool Success { get; set; }
            public GitHubIssue? Issue { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // TODOIST RESPONSE CLASSES
        // ============================================

        public class TestTodoistResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GetTodoistProjectsResponse
        {
            public bool Success { get; set; }
            public TodoistProject[]? Projects { get; set; }
            public string? Error { get; set; }
        }

        public class SaveTodoistResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateTodoistTaskResponse
        {
            public bool Success { get; set; }
            public TodoistTaskDraft? Task { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateTodoistTaskResponse
        {
            public bool Success { get; set; }
            public TodoistTask? Task { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // TRELLO RESPONSE CLASSES
        // ============================================

        public class TestTrelloResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GetTrelloBoardsResponse
        {
            public bool Success { get; set; }
            public TrelloBoard[]? Boards { get; set; }
            public string? Error { get; set; }
        }

        public class GetTrelloListsResponse
        {
            public bool Success { get; set; }
            public TrelloList[]? Lists { get; set; }
            public string? Error { get; set; }
        }

        public class SaveTrelloResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateTrelloCardResponse
        {
            public bool Success { get; set; }
            public TrelloCardDraft? Card { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateTrelloCardResponse
        {
            public bool Success { get; set; }
            public TrelloCard? Card { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // ASANA RESPONSE CLASSES
        // ============================================

        public class TestAsanaResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GetAsanaWorkspacesResponse
        {
            public bool Success { get; set; }
            public AsanaWorkspace[]? Workspaces { get; set; }
            public string? Error { get; set; }
        }

        public class GetAsanaProjectsResponse
        {
            public bool Success { get; set; }
            public AsanaProject[]? Projects { get; set; }
            public string? Error { get; set; }
        }

        public class SaveAsanaResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateAsanaTaskResponse
        {
            public bool Success { get; set; }
            public AsanaTaskDraft? Task { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateAsanaTaskResponse
        {
            public bool Success { get; set; }
            public AsanaTask? Task { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // SLACK RESPONSE CLASSES
        // ============================================

        public class TestSlackResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class SaveSlackResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateSlackMessageResponse
        {
            public bool Success { get; set; }
            public SlackMessageDraft? Message { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class SendSlackMessageResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // MICROSOFT TEAMS RESPONSE CLASSES
        // ============================================

        public class TestTeamsResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class SaveTeamsResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateTeamsMessageResponse
        {
            public bool Success { get; set; }
            public TeamsMessageDraft? Message { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class SendTeamsMessageResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // ZAPIER / MAKE RESPONSE CLASSES
        // ============================================

        public class TestZapierResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class SaveZapierResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateZapierPayloadResponse
        {
            public bool Success { get; set; }
            public ZapierPayloadDraft? Payload { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class SendZapierResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // GOOGLE OAUTH RESPONSE CLASSES
        // ============================================

        public class StartGoogleOAuthResponse
        {
            public bool Success { get; set; }
            public string? Url { get; set; }
            public string? Error { get; set; }
        }

        public class GoogleOAuthStatusResponse
        {
            public bool Success { get; set; }
            public bool IsConnected { get; set; }
            public string? Email { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // GMAIL RESPONSE CLASSES
        // ============================================

        public class GenerateGmailDraftResponse
        {
            public bool Success { get; set; }
            public GmailDraft? Draft { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
            public string? ModelUsed { get; set; }
            public bool CascadeUsed { get; set; }
        }

        public class SendGmailResponse
        {
            public bool Success { get; set; }
            public string? MessageId { get; set; }
            public string? Error { get; set; }
        }

        public class CreateGmailDraftResponse
        {
            public bool Success { get; set; }
            public string? DraftId { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // GOOGLE CALENDAR RESPONSE CLASSES
        // ============================================

        public class GetCalendarListResponse
        {
            public bool Success { get; set; }
            public GoogleCalendar[]? Calendars { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateCalendarEventResponse
        {
            public bool Success { get; set; }
            public CalendarEventDraft? Event { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateCalendarEventResponse
        {
            public bool Success { get; set; }
            public CalendarEvent? Event { get; set; }
            public string? Error { get; set; }
        }

        // ============================================
        // GOOGLE TASKS RESPONSE CLASSES
        // ============================================

        public class GetTaskListsResponse
        {
            public bool Success { get; set; }
            public GoogleTaskList[]? TaskLists { get; set; }
            public string? Error { get; set; }
        }

        public class GenerateGoogleTaskResponse
        {
            public bool Success { get; set; }
            public GoogleTaskDraft? Task { get; set; }
            public int TokensUsed { get; set; }
            public string? Error { get; set; }
        }

        public class CreateGoogleTaskResponse
        {
            public bool Success { get; set; }
            public GoogleTask? Task { get; set; }
            public string? Error { get; set; }
        }

        public class SaveGoogleTasksConfigResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }
    }
}
