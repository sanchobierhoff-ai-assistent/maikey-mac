using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Assistant-modus: gesprekken (agent), persoonlijk geheugen en sessiegeschiedenis.
    /// Aparte partial zodat de grote ApiClient.cs onaangeroerd blijft.
    /// </summary>
    public partial class ApiClient
    {
        private static readonly JsonSerializerOptions AgentJson = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ============================================
        // AGENT (command bar)
        // ============================================

        /// <summary>Eén beurt (niet-streaming). Retourneert tekstantwoord of voorgestelde actie.</summary>
        public async Task<AgentResponse> SendAgentAsync(string message, string? sessionId, string? model, string? context,
            string[]? imageUrls = null, string[]? publicIds = null)
        {
            var payload = new { message, sessionId, model, context, imageUrls, publicIds };
            return await PostAsync<AgentResponse>("/ai/agent", payload);
        }

        /// <summary>Bevestig (of annuleer) een voorgestelde actie en zet het gesprek voort.</summary>
        public async Task<AgentResponse> ConfirmAgentActionAsync(string sessionId, string toolName, string toolCallId, object args, string? model, bool cancel = false)
        {
            var payload = new { sessionId, toolName, toolCallId, args, model, cancel };
            return await PostAsync<AgentResponse>("/ai/agent/confirm", payload);
        }

        /// <summary>
        /// Streamende variant (SSE). Roept callbacks aan voor voortgang, eindresultaat en fouten.
        /// Valt in de UI terug op SendAgentAsync wanneer streaming faalt.
        /// </summary>
        public async Task SendAgentStreamAsync(
            string message, string? sessionId, string? model, string? context,
            Action<string> onStatus, Action<AgentResponse> onResult, Action<string> onError,
            string[]? imageUrls = null, string[]? publicIds = null,
            CancellationToken ct = default)
        {
            var payload = new { message, sessionId, model, context, imageUrls, publicIds };
            var json = JsonSerializer.Serialize(ToJsonSafePayload(payload), AgentJson);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/ai/agent/stream")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                onError(SafeError(body));
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? currentEvent = null;
            var data = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (ct.IsCancellationRequested) break;

                if (line.Length == 0)
                {
                    DispatchSse(currentEvent, data.ToString(), onStatus, onResult, onError);
                    currentEvent = null;
                    data.Clear();
                    continue;
                }
                if (line.StartsWith("event:")) currentEvent = line.Substring(6).Trim();
                else if (line.StartsWith("data:")) data.Append(line.Substring(5).Trim());
            }
            // Flush any trailing event without a terminating blank line
            if (currentEvent != null) DispatchSse(currentEvent, data.ToString(), onStatus, onResult, onError);
        }

        private static void DispatchSse(string? evt, string data, Action<string> onStatus, Action<AgentResponse> onResult, Action<string> onError)
        {
            if (string.IsNullOrEmpty(evt) || string.IsNullOrEmpty(data)) return;
            try
            {
                switch (evt)
                {
                    case "status":
                        {
                            var el = JsonSerializer.Deserialize<JsonElement>(data);
                            var stage = el.TryGetProperty("stage", out var s) ? s.GetString() : null;
                            var tool = el.TryGetProperty("tool", out var t) ? t.GetString() : null;
                            onStatus(tool != null ? $"{stage}:{tool}" : (stage ?? ""));
                            break;
                        }
                    case "message":
                    case "action":
                        {
                            var res = JsonSerializer.Deserialize<AgentResponse>(data, AgentJson);
                            if (res != null) { res.Success = true; onResult(res); }
                            break;
                        }
                    case "error":
                        {
                            var el = JsonSerializer.Deserialize<JsonElement>(data);
                            onError(el.TryGetProperty("error", out var e) ? (e.GetString() ?? "Fout") : "Fout");
                            break;
                        }
                    case "session":
                    case "done":
                    default:
                        break;
                }
            }
            catch { /* negeer een enkel corrupt SSE-frame */ }
        }

        private static string SafeError(string body)
        {
            try
            {
                var el = JsonSerializer.Deserialize<JsonElement>(body);
                if (el.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) return m.GetString()!;
                if (el.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) return e.GetString()!;
            }
            catch { }
            return L.T("Assistant_GenericError");
        }

        // ============================================
        // GEHEUGEN
        // ============================================

        public async Task<MemoryListResponse> GetMemoryAsync()
            => await GetAsync<MemoryListResponse>("/memory");

        public async Task<ApiResponse> AddMemoryAsync(string content, string category = "general")
            => await PostAsync<ApiResponse>("/memory", new { content, category });

        public async Task<ApiResponse?> DeleteMemoryAsync(string id)
            => await DeleteAsync<ApiResponse>($"/memory/{id}");

        // ============================================
        // SESSIES (gespreksgeschiedenis)
        // ============================================

        public async Task<SessionListResponse> GetSessionsAsync()
            => await GetAsync<SessionListResponse>("/agent/sessions");

        public async Task<SessionDetailResponse> GetSessionAsync(string id)
            => await GetAsync<SessionDetailResponse>($"/agent/sessions/{id}");

        public async Task<ApiResponse?> DeleteSessionAsync(string id)
            => await DeleteAsync<ApiResponse>($"/agent/sessions/{id}");

        // ============================================
        // ACHTERGRONDPROFIEL (mAI Assistent-menu)
        // ============================================

        public async Task<AssistantProfileResponse> GetAssistantProfileAsync()
            => await GetAsync<AssistantProfileResponse>("/assistant/profile");

        public async Task<ApiResponse> SaveAssistantProfileAsync(string? company, string? role, string? background)
            => await PutAsync<ApiResponse>("/assistant/profile", new { company, role, background });
    }

    // ============================================
    // RESPONSE-MODELLEN
    // ============================================

    public class AgentResponse
    {
        public bool Success { get; set; }
        public string? Type { get; set; }          // "message" | "confirm"
        public string? SessionId { get; set; }
        public string? Response { get; set; }       // tekstantwoord (type=message)
        public string? AssistantText { get; set; }  // begeleidende tekst bij een voorgestelde actie
        public string? Model { get; set; }
        public bool Executed { get; set; }          // bij confirm-respons
        public bool Remembered { get; set; }         // assistent sloeg deze beurt iets in het geheugen op
        public AgentAction? Action { get; set; }
        public string? Error { get; set; }
    }

    public class AgentAction
    {
        public string ToolCallId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public string? ActionLabel { get; set; }
        public string? IntegrationType { get; set; }
        public JsonElement Args { get; set; }       // ruwe argumenten (per tool anders)
    }

    public class MemoryItem
    {
        public string Id { get; set; } = "";
        public string? Category { get; set; }
        public string Content { get; set; } = "";
        public string? Source { get; set; }
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
    }

    public class MemoryListResponse
    {
        public bool Success { get; set; }
        public List<MemoryItem> Memory { get; set; } = new();
    }

    public class SessionSummary
    {
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
    }

    public class SessionListResponse
    {
        public bool Success { get; set; }
        public List<SessionSummary> Sessions { get; set; } = new();
    }

    public class SessionMessage
    {
        public string Role { get; set; } = "";
        public JsonElement Content { get; set; }
    }

    public class SessionDetailResponse
    {
        public bool Success { get; set; }
        public SessionSummary? Session { get; set; }
        public List<SessionMessage> Messages { get; set; } = new();
    }

    public class AssistantProfile
    {
        public string? Company { get; set; }
        public string? Role { get; set; }
        public string? Background { get; set; }
    }

    public class AssistantProfileResponse
    {
        public bool Success { get; set; }
        public AssistantProfile? Profile { get; set; }
    }
}
