using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Koppelingen / brug-laag: Zendesk ⇄ Jira in vaste stappen (prepare → review/chat → commit),
    /// plus bestaande koppelingen, bron-dropdowns en het afleiden van een schrijfstijl uit eigen
    /// Zendesk-reacties. Praat met de backend-endpoints onder /integrations/bridge en
    /// /integrations/zendesk/derive-style.
    /// </summary>
    public partial class ApiClient
    {
        /// <summary>
        /// Vertaal een ApiException van een brug-call naar een nette, actiegerichte melding
        /// (NL/EN/DE) op basis van het semantische errorType dat de backend meegeeft. Geeft ook
        /// het errorType terug zodat het venster kan beslissen om een "Ga naar Integraties"-knop
        /// te tonen. Onbekende types vallen terug op de servertekst.
        /// </summary>
        internal string BridgeError(ApiException ex, out string? errorType)
        {
            errorType = ex.ErrorType;

            // Schrijf de VOLLEDIGE details naar 2-errors.log (alleen TEST/DEBUG schrijft echt),
            // zodat je in de testversie exact ziet wat er misging (Zendesk/Jira-tekst + status).
            try { _ = _loggingService.LogIntegrationErrorAsync("bridge", ex.ErrorType ?? "unknown", ex.Message, ex.Details); } catch { }

            switch (ex.ErrorType)
            {
                case "INTEGRATION_NOT_FOUND": return L.T("Bridge_Err_IntegrationMissing");
                case "AUTH_FAILED":           return L.T("Bridge_Err_AuthFailed");
                case "RATE_LIMITED":
                    return ex.RetryAfterSeconds is int s && s > 0
                        ? L.Tf("Bridge_Err_RateLimitedWait", s)
                        : L.T("Bridge_Err_RateLimited");
                case "UPSTREAM_INVALID":      return SafeError(ex.Details); // concrete Jira/Zendesk-tekst
                // Generiek/onbekend: toon de generieke zin MÉT de concrete servertekst erachter,
                // zodat de gebruiker (en het foutenlog) de echte oorzaak ziet i.p.v. alleen "er ging iets mis".
                case "BRIDGE_ERROR":          return AppendDetail(L.T("Bridge_Err_Generic"), ex);
                default:                      return SafeError(ex.Details);
            }
        }

        /// <summary>Voeg de concrete server-/upstream-tekst (uit Details, of anders het HTTP-statusnummer) toe.</summary>
        private static string AppendDetail(string baseMsg, ApiException ex)
        {
            var detail = SafeError(ex.Details);
            var generic = L.T("Assistant_GenericError");
            // Alleen toevoegen als er echt een zinvolle, andere tekst is.
            if (!string.IsNullOrWhiteSpace(detail) && detail != generic && detail != baseMsg)
                return $"{baseMsg}\n({detail})";
            if (ex.StatusCode is System.Net.HttpStatusCode sc)
                return $"{baseMsg} (HTTP {(int)sc})";
            return baseMsg;
        }

        /// <summary>Lees de bron(nen) en laat de AI het doel-item genereren (geen schrijf-actie).</summary>
        public async Task<BridgePrepareResult> PrepareBridgeAsync(
            string direction, string sourceId, string? model = null,
            string? styleInstructions = null, string? zendeskTicketId = null,
            List<BridgeClarification>? clarifications = null,
            string? instruction = null, string? sourceType = null,
            string? jiraKey = null)
        {
            try
            {
                var r = await PostAsync<BridgePrepareResult>("/integrations/bridge/prepare", new
                {
                    direction,
                    sourceId,
                    model,
                    styleInstructions,
                    zendeskTicketId,
                    clarifications,
                    instruction,
                    sourceType,
                    jiraKey
                });
                if (r != null) return r;
                return new BridgePrepareResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgePrepareResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgePrepareResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Voer de koppeling daadwerkelijk uit (maak Jira aan / plaats Zendesk-reactie + terugkoppelen).</summary>
        public async Task<BridgeCommitResult> CommitBridgeAsync(object payload)
        {
            try
            {
                var r = await PostAsync<BridgeCommitResult>("/integrations/bridge/commit", payload);
                if (r != null) return r;
                return new BridgeCommitResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeCommitResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeCommitResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Koppel een al aangemaakt Jira-ticket terug in Zendesk (interne notitie) + bewaar de koppeling.</summary>
        public async Task<BridgeCommitResult> LinkBridgeAsync(
            string zendeskTicketId, string jiraKey, string? jiraUrl = null,
            string? note = null, string? setSourceStatus = null, bool backlink = true)
        {
            try
            {
                var r = await PostAsync<BridgeCommitResult>("/integrations/bridge/link", new
                {
                    zendeskTicketId, jiraKey, jiraUrl, note, setSourceStatus, backlink
                });
                if (r != null) return r;
                return new BridgeCommitResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeCommitResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeCommitResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Bestaande koppelingen van de gebruiker (nieuwste eerst).</summary>
        public async Task<BridgeLinksResult> ListBridgeLinksAsync()
        {
            try
            {
                var r = await GetAsync<BridgeLinksResult>("/integrations/bridge/links");
                if (r != null) return r;
                return new BridgeLinksResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new BridgeLinksResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new BridgeLinksResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Verwijder een koppeling uit de geschiedenis.</summary>
        public async Task<GenericResult> DeleteBridgeLinkAsync(string id)
        {
            try
            {
                var r = await DeleteAsync<GenericApiResponse>($"/integrations/bridge/links/{id}");
                return new GenericResult { Success = r?.Success == true, Error = r?.Error };
            }
            catch (ApiException ex) { return new GenericResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new GenericResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Bron-tickets voor de dropdown. type = "zendesk" | "jira"; lege query op Zendesk = recente tickets.</summary>
        public async Task<BridgeSourcesResult> ListBridgeSourcesAsync(string type, string? query = null)
        {
            try
            {
                var endpoint = $"/integrations/bridge/sources?type={Uri.EscapeDataString(type)}&query={Uri.EscapeDataString(query ?? string.Empty)}";
                var r = await GetAsync<BridgeSourcesResult>(endpoint);
                if (r != null) return r;
                return new BridgeSourcesResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeSourcesResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeSourcesResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Vind bestaande gekoppelde tegenpartij(en) — bv. via een Jira remote link naar Zendesk.</summary>
        public async Task<BridgeSourcesResult> ListLinkedCounterpartsAsync(string type, string id)
        {
            try
            {
                var endpoint = $"/integrations/bridge/linked?type={Uri.EscapeDataString(type)}&id={Uri.EscapeDataString(id)}";
                var r = await GetAsync<BridgeSourcesResult>(endpoint);
                if (r != null) return r;
                return new BridgeSourcesResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeSourcesResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeSourcesResult { Success = false, Error = ex.Message }; }
        }

        // ---- Werklijst / inbox (opt-in achtergrond-poll van openstaande Zendesk-tickets) -----

        /// <summary>De werklijst: openstaande, nog-niet-gekoppelde Zendesk-tickets (metadata + triage).</summary>
        public async Task<BridgeInboxResult> ListInboxAsync()
        {
            try
            {
                var r = await GetAsync<BridgeInboxResult>("/integrations/bridge/inbox");
                if (r != null) return r;
                return new BridgeInboxResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeInboxResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeInboxResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Forceer nu één poll en geef meteen de verse lijst terug (handmatige "Vernieuwen").</summary>
        public async Task<BridgeInboxResult> RefreshInboxAsync()
        {
            try
            {
                var r = await PostAsync<BridgeInboxResult>("/integrations/bridge/inbox/refresh", new { });
                if (r != null) return r;
                return new BridgeInboxResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { var m = BridgeError(ex, out var et); return new BridgeInboxResult { Success = false, Error = m, ErrorType = et }; }
            catch (Exception ex) { return new BridgeInboxResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Klik een inbox-item weg (verdwijnt uit de werklijst).</summary>
        public async Task<GenericResult> DismissInboxItemAsync(string id)
        {
            try
            {
                var r = await PostAsync<GenericApiResponse>($"/integrations/bridge/inbox/{Uri.EscapeDataString(id)}/dismiss", new { });
                return new GenericResult { Success = r?.Success == true, Error = r?.Error };
            }
            catch (ApiException ex) { return new GenericResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new GenericResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Huidige werklijst-instellingen (opt-in + gekozen merk).</summary>
        public async Task<BridgeInboxSettingsResult> GetInboxSettingsAsync()
        {
            try { return await GetAsync<BridgeInboxSettingsResult>("/integrations/bridge/inbox/settings") ?? new BridgeInboxSettingsResult(); }
            catch { return new BridgeInboxSettingsResult(); }
        }

        /// <summary>Zet de achtergrond-poll aan/uit. Bij aanzetten pollt de server meteen één keer.</summary>
        public async Task<bool> SetInboxEnabledAsync(bool enabled)
        {
            try { var r = await PostAsync<BridgeInboxSettingsResult>("/integrations/bridge/inbox/settings", new { enabled }); return r?.Success == true; }
            catch { return false; }
        }

        /// <summary>Stel het merkfilter in (brandId null/"" = alle merken). Bij aan poll de server meteen.</summary>
        public async Task<bool> SetInboxBrandAsync(string? brandId, string? brandName)
        {
            try { var r = await PostAsync<BridgeInboxSettingsResult>("/integrations/bridge/inbox/settings", new { brandId, brandName }); return r?.Success == true; }
            catch { return false; }
        }

        /// <summary>Merken (brands) in het Zendesk-account, voor de filter-dropdown.</summary>
        public async Task<List<ZendeskBrand>> GetZendeskBrandsAsync()
        {
            try
            {
                var r = await GetAsync<ZendeskBrandsResult>("/integrations/zendesk/brands");
                return r?.Success == true ? r.Brands : new List<ZendeskBrand>();
            }
            catch { return new List<ZendeskBrand>(); }
        }

        // ---- Jira-statusovergangen (voor statuswijziging in het koppel-gesprek) ---------------

        /// <summary>Beschikbare Jira-transities voor een issue (naam → toon in dropdown).</summary>
        public async Task<List<JiraTransition>> GetJiraTransitionsAsync(string issueKey)
        {
            try
            {
                var r = await GetAsync<JiraTransitionsResult>($"/integrations/jira/transitions?issueKey={Uri.EscapeDataString(issueKey)}");
                return r?.Success == true ? r.Transitions : new List<JiraTransition>();
            }
            catch { return new List<JiraTransition>(); }
        }

        /// <summary>Zet een Jira-issue door naar een andere status (op naam).</summary>
        public async Task<GenericResult> TransitionJiraIssueAsync(string issueKey, string transitionName)
        {
            try
            {
                var r = await PostAsync<GenericApiResponse>("/integrations/jira/transition", new { issueKey, transitionName });
                return new GenericResult { Success = r?.Success == true, Error = r?.Error };
            }
            catch (ApiException ex) { return new GenericResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new GenericResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Stap 1: haal eigen publieke Zendesk-reacties op als stijl-voorbeelden.</summary>
        public async Task<DeriveStyleResult> DeriveZendeskRepliesAsync(int? limit = null)
        {
            try
            {
                var r = await PostAsync<DeriveStyleResult>("/integrations/zendesk/derive-style", new { limit });
                if (r != null) return r;
                return new DeriveStyleResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new DeriveStyleResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new DeriveStyleResult { Success = false, Error = ex.Message }; }
        }

        /// <summary>Stap 2: genereer een StyleProfile uit (bewerkte) voorbeeldreacties.</summary>
        public async Task<DeriveStyleResult> DeriveZendeskStyleAsync(List<string> examples, string? usageContext = null)
        {
            try
            {
                var r = await PostAsync<DeriveStyleResult>("/integrations/zendesk/derive-style", new { examples, usageContext });
                if (r != null) return r;
                return new DeriveStyleResult { Success = false, Error = "Geen antwoord van de server." };
            }
            catch (ApiException ex) { return new DeriveStyleResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (Exception ex) { return new DeriveStyleResult { Success = false, Error = ex.Message }; }
        }

        // ---- Chat-geschiedenis (brug-sessies) --------------------------------------------
        /// <summary>Maak een nieuwe brug-sessie; meta bewaart de gekozen Zendesk/Jira zodat we die bij heropenen herstellen.</summary>
        public async Task<string?> CreateBridgeSessionAsync(string? title, object? meta)
        {
            try
            {
                var r = await PostAsync<BridgeSessionCreateResponse>("/integrations/bridge/sessions", new { title, meta });
                return r?.Success == true ? r.Id : null;
            }
            catch { return null; }
        }

        /// <summary>Voeg een bericht toe aan een brug-sessie (role: user|assistant|system|meta).</summary>
        public async Task AppendBridgeMessageAsync(string sessionId, string role, object content)
        {
            try { await PostAsync<GenericApiResponse>($"/integrations/bridge/sessions/{sessionId}/messages", new { role, content }); }
            catch { /* geschiedenis mag nooit de flow breken */ }
        }

        /// <summary>Recente brug-gesprekken (nieuwste eerst).</summary>
        public async Task<SessionListResponse> ListBridgeSessionsAsync()
        {
            try { return await GetAsync<SessionListResponse>("/integrations/bridge/sessions") ?? new SessionListResponse(); }
            catch { return new SessionListResponse(); }
        }

        /// <summary>Eén brug-gesprek met alle berichten.</summary>
        public async Task<SessionDetailResponse> GetBridgeSessionAsync(string id)
        {
            try { return await GetAsync<SessionDetailResponse>($"/integrations/bridge/sessions/{id}") ?? new SessionDetailResponse(); }
            catch { return new SessionDetailResponse(); }
        }

        /// <summary>Verwijder een brug-gesprek.</summary>
        public async Task<bool> DeleteBridgeSessionAsync(string id)
        {
            try { var r = await DeleteAsync<GenericApiResponse>($"/integrations/bridge/sessions/{id}"); return r?.Success == true; }
            catch { return false; }
        }
    }

    // ---- DTO's ----------------------------------------------------------------------------

    public class BridgeSessionCreateResponse
    {
        public bool Success { get; set; }
        public string? Id { get; set; }
    }


    public class BridgeClarification
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

    public class BridgePrepareResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        /// <summary>Semantisch fouttype (INTEGRATION_NOT_FOUND, AUTH_FAILED, RATE_LIMITED, …) voor de UI.</summary>
        public string? ErrorType { get; set; }
        /// <summary>Niet-fatale waarschuwingen (bv. een expliciet gekozen ticket dat niet gelezen kon worden).</summary>
        public List<string>? Warnings { get; set; }
        public string? Direction { get; set; }
        public BridgeSource? Source { get; set; }
        public BridgeGenerated? Generated { get; set; }
        public BridgeLink? ExistingLink { get; set; }
    }

    public class BridgeSource
    {
        // Zendesk-bron
        public string? Id { get; set; }
        public string? Subject { get; set; }
        public string? Status { get; set; }
        // Jira-bron
        public string? Key { get; set; }
        public string? Summary { get; set; }
        public string? Url { get; set; }
        // Gekoppeld Jira-issue bij een Zendesk-bron (dev-opmerking/freeform).
        public string? JiraKey { get; set; }
        // Gekoppeld Zendesk-ticket (bij jira-to-zendesk)
        public string? ZendeskTicketId { get; set; }
        public string? ZendeskSubject { get; set; }
        // De volledige, gelabelde brontekst die naar de AI ging (handig ter controle).
        public string? Text { get; set; }
    }

    /// <summary>
    /// Union: bij zendesk→jira bevat dit het gegenereerde Jira-ticket; bij jira→zendesk het
    /// klantantwoord (Reply) óf verduidelijkingsvragen (NeedsClarification + Questions).
    /// </summary>
    public class BridgeGenerated
    {
        // Zendesk → Jira (ticket-velden)
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? IssueType { get; set; }
        public string? Priority { get; set; }
        public List<string>? Labels { get; set; }
        public bool Multiple { get; set; }

        // Jira → Zendesk
        public string? Reply { get; set; }
        public bool NeedsClarification { get; set; }
        public List<string>? Questions { get; set; }

        // Dev-opmerking / vrije instructie
        public string? Text { get; set; }
    }

    public class BridgeLink
    {
        public string? Id { get; set; }
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
        public string? TargetType { get; set; }
        public string? TargetKey { get; set; }
        public string? TargetUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class BridgeLinksResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<BridgeLink> Links { get; set; } = new();
    }

    public class BridgeSourceItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Status { get; set; }
        public string? Url { get; set; }
        public override string ToString() => string.IsNullOrEmpty(Status) ? Title : $"{Title}  ·  {Status}";
    }

    public class BridgeSourcesResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public string? Type { get; set; }
        public List<BridgeSourceItem> Items { get; set; } = new();
    }

    public class BridgeCommitResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public BridgeCommitTarget? Target { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class BridgeCommitTarget
    {
        public string? Type { get; set; }
        public string? Key { get; set; }
        public string? Id { get; set; }
        public string? Url { get; set; }
    }

    public class BridgeInboxItem
    {
        public string Id { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("source_type")]
        public string? SourceType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("source_id")]
        public string? SourceId { get; set; }
        public string? Subject { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("ticket_updated_at")]
        public DateTime? TicketUpdatedAt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("triage_category")]
        public string? TriageCategory { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("triage_priority")]
        public string? TriagePriority { get; set; }

        public string DisplaySubject => string.IsNullOrWhiteSpace(Subject) ? "(zonder onderwerp)" : Subject!;
    }

    public class BridgeInboxResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public List<BridgeInboxItem> Items { get; set; } = new();
        public int Count { get; set; }
    }

    public class BridgeInboxSettingsResult
    {
        public bool Success { get; set; }
        public bool Enabled { get; set; }
        public string? BrandId { get; set; }
        public string? BrandName { get; set; }
        public string? Error { get; set; }
    }

    public class ZendeskBrand
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public class JiraTransition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? To { get; set; }
        public override string ToString() => Name;
    }

    public class JiraTransitionsResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<JiraTransition> Transitions { get; set; } = new();
    }

    public class ZendeskBrandsResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<ZendeskBrand> Brands { get; set; } = new();
    }

    public class DeriveStyleResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<string>? Replies { get; set; }
        // Velden uit generate-style-profile (StyleProfile-tekst e.d.) worden dynamisch elders gelezen.
        public string? StyleProfile { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
