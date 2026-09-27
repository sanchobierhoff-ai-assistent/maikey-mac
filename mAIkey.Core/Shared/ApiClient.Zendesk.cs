using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Zendesk-ticketbeheer vanuit de app (Fase B): zoeken, ticket + reacties lezen,
    /// en status/prioriteit/tags/reactie bijwerken. Praat met de directe Zendesk-endpoints
    /// onder /integrations/zendesk (dun laagje over de zendesk-tools in de backend).
    /// </summary>
    public partial class ApiClient
    {
        public async Task<ZendeskSearchResult> SearchZendeskTicketsAsync(string query, string? status = null, string? priority = null)
        {
            try
            {
                var endpoint = $"/integrations/zendesk/search?query={Uri.EscapeDataString(query ?? string.Empty)}";
                if (!string.IsNullOrWhiteSpace(status)) endpoint += $"&status={Uri.EscapeDataString(status)}";
                if (!string.IsNullOrWhiteSpace(priority)) endpoint += $"&priority={Uri.EscapeDataString(priority)}";
                var r = await GetAsync<ZendeskSearchResult>(endpoint);
                return r ?? new ZendeskSearchResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new ZendeskSearchResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new ZendeskSearchResult { Success = false, Error = ex.Message }; }
        }

        public async Task<ZendeskTicketResult> GetZendeskTicketAsync(string id)
        {
            try
            {
                var r = await GetAsync<ZendeskTicketResult>($"/integrations/zendesk/ticket?id={Uri.EscapeDataString(id)}");
                return r ?? new ZendeskTicketResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new ZendeskTicketResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new ZendeskTicketResult { Success = false, Error = ex.Message }; }
        }

        public async Task<ZendeskSimpleResult> UpdateZendeskTicketAsync(string id, string? status = null,
            string? priority = null, List<string>? tags = null, string? comment = null, bool isPublic = true)
        {
            try
            {
                var payload = new
                {
                    id,
                    status,
                    priority,
                    tags,
                    comment,
                    @public = isPublic
                };
                var r = await PostAsync<ZendeskSimpleResult>("/integrations/zendesk/update", payload);
                return r ?? new ZendeskSimpleResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new ZendeskSimpleResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new ZendeskSimpleResult { Success = false, Error = ex.Message }; }
        }
    }

    // ---- DTO's ----------------------------------------------------------------------------

    public class ZendeskTicketSummary
    {
        public long Id { get; set; }
        public string Subject { get; set; } = "";
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Updated { get; set; }
        public string? Url { get; set; }
        public override string ToString() =>
            $"#{Id}  ·  {(string.IsNullOrEmpty(Subject) ? "(zonder onderwerp)" : Subject)}  ·  {Status}";
    }

    public class ZendeskCommentDto
    {
        public string Body { get; set; } = "";
        [JsonPropertyName("public")] public bool Public { get; set; }
        public string? Created { get; set; }
    }

    public class ZendeskTicketDto
    {
        public long Id { get; set; }
        public string Subject { get; set; } = "";
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ZendeskCommentDto> Comments { get; set; } = new();
    }

    public class ZendeskSearchResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<ZendeskTicketSummary> Results { get; set; } = new();
        public int Count { get; set; }
    }

    public class ZendeskTicketResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public ZendeskTicketDto? Ticket { get; set; }
    }

    public class ZendeskSimpleResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
