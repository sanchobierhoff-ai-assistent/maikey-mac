using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// HTTP client voor communicatie met de Node.js backend
    /// </summary>
    public partial class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private string? _authToken;
        private readonly LoggingService _loggingService;

        /// <summary>
        /// Gedeelde deserialisatie-opties: case-insensitive + een tolerante string-converter.
        /// De converter leest óók een JSON-getal/bool als string, zodat een numeriek id (bv. een
        /// Zendesk-ticketnummer) in een string-veld de hele response niet meer laat crashen.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new TolerantStringConverter() }
        };

        // Technische foutcodes waarvoor PostAsync zelf al een nette, gelokaliseerde boodschap
        // op de ApiException zet (bv. "mAIkey is currently processing many requests..." voor een
        // rate limit). De rauwe backend-body bevat voor deze gevallen vaak alleen een technische
        // code (bv. {"error":"RATE_LIMIT"}) die bedoeld is voor de client, niet om letterlijk aan
        // de gebruiker te tonen — dus die JSON niet blindelings herparsen als het antwoordmodel.
        private static readonly HashSet<string> TechnicalErrorTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "RATE_LIMIT", "TIMEOUT_ERROR", "NETWORK_ERROR", "UPDATE_REQUIRED", "MODEL_UNAVAILABLE"
        };

        private static bool IsTechnicalError(ApiException ex) =>
            !string.IsNullOrEmpty(ex.ErrorType) && TechnicalErrorTypes.Contains(ex.ErrorType);

        /// <summary>
        /// Zet anonieme C#-types (bv. <c>new { email, password }</c>) om naar een Dictionary
        /// vóór serialisatie. NOODZAKELIJK in de geobfusceerde release-build: System.Text.Json's
        /// reflectie-serializer bouwt voor ELK type ook metadata voor het "constructor-met-
        /// parameters-die-de-properties-matchen"-patroon (nodig voor records/immutable types) —
        /// en een anoniem type heeft ALTIJD zo'n constructor. Obfuscar strip de parameternamen
        /// van die constructor uit de gecompileerde IL, waardoor die metadata-opbouw een
        /// ArgumentNullException gooit (".NET probeert een parameternaam als dictionary-sleutel
        /// te gebruiken, maar die is null") — bij ELKE aanroep, voor ELK endpoint. Dictionaries
        /// kennen dat "parameterized constructor"-patroon niet, dus dit omzeilt de bug volledig
        /// zonder de vorm van de verstuurde JSON te veranderen.
        ///
        /// Detectie is bewust STRUCTUREEL (geen publieke parameterloze constructor) in plaats van
        /// op typenaam/CompilerGeneratedAttribute: Obfuscar hernoemt anonieme types ÓÓK (ze zijn
        /// intern, dus niet beschermd door KeepPublicApi), waardoor een naam-check als
        /// "bevat 'AnonymousType'" in de geobfusceerde build altijd false teruggeeft.
        /// </summary>
        private static bool NeedsDictionaryConversion(Type type)
        {
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal))
                return false;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return false; // arrays/lijsten/dictionaries: apart afgehandeld door de aanroeper
            return type.GetConstructor(Type.EmptyTypes) == null;
        }

        private static object? ToJsonSafePayload(object? value)
        {
            if (value == null) return null;
            var type = value.GetType();

            if (NeedsDictionaryConversion(type))
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in type.GetProperties())
                    dict[prop.Name] = ToJsonSafePayload(prop.GetValue(value));
                return dict;
            }

            // Strings zijn zelf ook IEnumerable<char> — niet als lijst behandelen. Dictionaries
            // moeten een JSON-object blijven, niet omgezet worden naar een array van entries.
            if (value is string || value is System.Collections.IDictionary)
                return value;

            if (value is System.Collections.IEnumerable enumerable)
            {
                var items = enumerable.Cast<object?>().ToList();
                // Alleen ombouwen als er daadwerkelijk problematische elementen in zitten (bv.
                // messages.Select(m => new { role, content })) — anders het origineel
                // ongemoeid laten, om bestaande, al-werkende paden niet te raken.
                bool hasProblemItem = items.Any(i => i != null && NeedsDictionaryConversion(i.GetType()));
                return hasProblemItem ? items.Select(ToJsonSafePayload).ToList() : value;
            }

            return value;
        }

        private sealed class TolerantStringConverter : JsonConverter<string>
        {
            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String: return reader.GetString();
                    case JsonTokenType.Null: return null;
                    case JsonTokenType.Number:
                        return reader.TryGetInt64(out var l)
                            ? l.ToString(CultureInfo.InvariantCulture)
                            : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
                    case JsonTokenType.True: return "true";
                    case JsonTokenType.False: return "false";
                    default: reader.Skip(); return null;
                }
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
                => writer.WriteStringValue(value);
        }

        public ApiClient(string baseUrl = "https://ai-assistent-backend-production.up.railway.app")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(120) // Increased from 30 to 120 seconds
            };
            _loggingService = new LoggingService();
            _httpClient.DefaultRequestHeaders.Add("X-Client-Version", ConfigService.CURRENT_VERSION);
        }

        /// <summary>
        /// Geeft een nette gebruikersvriendelijke foutmelding voor 403 responses.
        /// Onderscheidt tier-blokkering van echte auth-fouten.
        /// </summary>
        private static string GetForbiddenMessage(string responseBody)
        {
            if (responseBody.Contains("cloud_sync_blocked"))
                return L.T("Error_403_CloudSync");
            if (responseBody.Contains("integrations_blocked"))
                return L.T("Error_403_Integrations");
            if (responseBody.Contains("image_analysis_blocked"))
                return L.T("Error_403_ImageAnalysis");
            return L.T("Error_SessionExpired");
        }

        /// <summary>
        /// Geeft een contextspecifieke foutmelding voor 402 responses (limiet bereikt).
        /// Parst de reden uit de JSON-response van de backend.
        /// </summary>
        private static string Get402Message(string responseBody)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
                var reason        = json.TryGetProperty("reason", out var r) ? r.GetString() : null;
                var days          = json.TryGetProperty("days_until_reset", out var d) && d.ValueKind == JsonValueKind.Number ? (int?)(int)d.GetDouble() : null;
                var dailyCap      = json.TryGetProperty("daily_cap", out var dc) && dc.ValueKind == JsonValueKind.Number ? (int?)(int)dc.GetDouble() : null;
                var dailyUsed     = json.TryGetProperty("daily_used", out var du) && du.ValueKind == JsonValueKind.Number ? (int?)(int)Math.Round(du.GetDouble()) : null;
                var reqUsed       = json.TryGetProperty("requests_used", out var ru) && ru.ValueKind == JsonValueKind.Number ? (int?)(int)ru.GetDouble() : null;
                var maxReq        = json.TryGetProperty("max_monthly_requests", out var mr) && mr.ValueKind == JsonValueKind.Number ? (int?)(int)mr.GetDouble() : null;
                var dailyResetUtc = json.TryGetProperty("daily_reset_utc", out var dru) ? dru.GetString() : null;
                var monthlyReset  = json.TryGetProperty("monthly_reset_date", out var mrd) ? mrd.GetString() : null;

                string daysStr = days.HasValue
                    ? L.Tf("Error_402_MonthlyDays", days)
                    : "";

                string dagResetZin = !string.IsNullOrEmpty(dailyResetUtc)
                    ? L.Tf("Error_402_DailyReset", ToLocalDate(dailyResetUtc))
                    : L.T("Error_402_DailyResetTomorrow");

                string maandResetZin = !string.IsNullOrEmpty(monthlyReset)
                    ? L.Tf("Error_402_MonthlyResetOn", ToLocalDate(monthlyReset), daysStr)
                    : L.T("Error_402_MonthlyResetSoon");

                if (reason == "daily_cap_exceeded" && dailyCap.HasValue)
                    return L.Tf("Error_402_DailyCap", dagResetZin);

                if (reason == "monthly_requests_exceeded" && maxReq.HasValue)
                    return L.Tf("Error_402_MonthlyUsed", reqUsed, maxReq, maandResetZin);

                if (reason == "subscription_expired")
                    return L.T("Error_402_SubExpired");

                if (reason == "model_blocked")
                    return L.T("Error_ModelBlocked_Body");

                // credits_exhausted of onbekende reden
                return L.Tf("Error_402_Default", maandResetZin);
            }
            catch
            {
                return L.T("Error_402_Fallback");
            }
        }

        private static string ToLocalDate(string isoUtc)
        {
            try
            {
                var utc = DateTimeOffset.Parse(isoUtc).UtcDateTime;
                var local = utc.ToLocalTime();
                var culture = L.CurrentLanguage switch
                {
                    "en" => new System.Globalization.CultureInfo("en-GB"),
                    "de" => new System.Globalization.CultureInfo("de-DE"),
                    _    => new System.Globalization.CultureInfo("nl-NL")
                };
                return local.ToString("dddd d MMMM HH:mm", culture);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Set authentication token voor protected endpoints
        /// </summary>
        public void SetAuthToken(string token)
        {
            _authToken = token;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Clear authentication token
        /// </summary>
        public void ClearAuthToken()
        {
            _authToken = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// Ververs de X-Client-LocalTime header met de huidige lokale tijd (ISO 8601 + offset).
        /// De backend gebruikt dit om begroetingen ("Goedemorgen/-middag/-avond") bij het juiste
        /// dagdeel te laten passen. Per verzoek verversen zodat de tijd niet verstalt tijdens een
        /// lang draaiende sessie.
        /// </summary>
        private void SetLocalTimeHeader()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Remove("X-Client-LocalTime");
                _httpClient.DefaultRequestHeaders.Add("X-Client-LocalTime", DateTimeOffset.Now.ToString("o"));
            }
            catch { /* header zetten mag nooit een request breken */ }
        }

        // ============================================
        // AUTHENTICATION
        // ============================================

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            try
            {
                var payload = new { email, password };
                return await PostAsync<LoginResponse>("/auth/login", payload);
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new LoginResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                // Parse error body to get errorType (e.g. EMAIL_NOT_VERIFIED, WRONG_PASSWORD)
                try
                {
                    var parsed = JsonSerializer.Deserialize<LoginResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new LoginResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<ApiResponse> ResendVerificationAsync(string email, string lang = "nl")
        {
            try
            {
                var payload = new { email, lang };
                return await PostAsync<ApiResponse>("/auth/resend-verification", payload);
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new ApiResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                try
                {
                    var parsed = JsonSerializer.Deserialize<ApiResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new ApiResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<ApiResponse> ForgotPasswordAsync(string email, string lang = "nl")
        {
            try
            {
                var payload = new { email, lang };
                return await PostAsync<ApiResponse>("/auth/forgot-password", payload);
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new ApiResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                try
                {
                    var parsed = JsonSerializer.Deserialize<ApiResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new ApiResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<ApiResponse> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                var payload = new { email, code, newPassword };
                return await PostAsync<ApiResponse>("/auth/reset-password", payload);
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new ApiResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                try
                {
                    var parsed = JsonSerializer.Deserialize<ApiResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new ApiResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<RegisterResponse> RegisterAsync(string email, string password, string name, string lang = "nl")
        {
            // Get unique machine ID for anti-account-hopping
            var machineId = DeviceIdentifier.GetMachineId();

            var payload = new { email, password, name, machine_id = machineId, lang };
            return await PostAsync<RegisterResponse>("/auth/register", payload);
        }

        public async Task<UserInfo> GetCurrentUserAsync()
        {
            return await GetAsync<UserInfo>("/auth/me");
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            var payload = new { refreshToken };
            return await PostAsync<RefreshTokenResponse>("/auth/refresh", payload);
        }

        // ============================================
        // GOOGLE LOGIN ("Inloggen met Google")
        // ============================================

        /// <summary>
        /// Start de Google-login flow. Retourneert de authorization-URL (open in browser)
        /// en een sessionId dat vervolgens gepollt wordt via PollGoogleLoginAsync.
        /// </summary>
        public async Task<OAuthStartResponse> StartGoogleLoginAsync()
        {
            return await StartOAuthLoginAsync("/auth/google/start");
        }

        /// <summary>
        /// Pollt de Google-login sessie. Zolang de gebruiker nog bezig is retourneert dit
        /// Success=false met Status="pending". Bij succes bevat het de tokens + user (net als login).
        /// </summary>
        public async Task<LoginResponse> PollGoogleLoginAsync(string sessionId)
        {
            return await PollOAuthLoginAsync("/auth/google/session", sessionId);
        }

        public async Task<OAuthStartResponse> StartMicrosoftLoginAsync()
        {
            return await StartOAuthLoginAsync("/auth/microsoft/start");
        }

        public async Task<LoginResponse> PollMicrosoftLoginAsync(string sessionId)
        {
            return await PollOAuthLoginAsync("/auth/microsoft/session", sessionId);
        }

        // Gedeelde OAuth-login helpers (Google/Microsoft delen dezelfde vorm)
        private async Task<OAuthStartResponse> StartOAuthLoginAsync(string endpoint)
        {
            try
            {
                return await PostAsync<OAuthStartResponse>(endpoint, new { });
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new OAuthStartResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                try
                {
                    var parsed = JsonSerializer.Deserialize<OAuthStartResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new OAuthStartResponse { Success = false, Error = ex.Message };
            }
        }

        private async Task<LoginResponse> PollOAuthLoginAsync(string endpoint, string sessionId)
        {
            try
            {
                var payload = new { sessionId };
                return await PostAsync<LoginResponse>(endpoint, payload);
            }
            catch (ApiException ex)
            {
                if (IsTechnicalError(ex))
                    return new LoginResponse { Success = false, Error = ex.Message, ErrorType = ex.ErrorType };

                try
                {
                    var parsed = JsonSerializer.Deserialize<LoginResponse>(ex.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) return parsed;
                }
                catch { }
                return new LoginResponse { Success = false, Error = ex.Message };
            }
        }

        // ============================================
        // AI ANALYZE
        // ============================================

        public async Task<AnalyzeResponse> AnalyzeAsync(
            string text,
            string[]? imageUrls = null, // Cloudinary image URLs (null if no images)
            string[]? publicIds = null, // Cloudinary public IDs for deletion (null if no images)
            OrderedContentBlock[]? orderedContent = null, // Interleaved text+image blocks (overrides imageUrls when present)
            string? promptId = null,
            TrainingExample[]? trainingExamples = null, // DEPRECATED: Only kept for old configs
            string? model = null,
            string? customPrompt = null,
            string? stylePrompt = null, // DEPRECATED: Only kept for old configs
            string[]? styleExamples = null, // DEPRECATED: Only kept for old configs
            Services.TrainingPair[]? trainingPairs = null, // DEPRECATED: No longer sent (only StyleProfile is used)
            Services.FeedbackExample[]? feedbackHistory = null, // DEPRECATED: Removed
            string? consolidatedLessons = null,
            string? userInstructions = null, // Extra context from user (used with prompt mode)
            Services.AIParameters? aiParameters = null, // Custom AI parameters
            string[]? textExamples = null, // DEPRECATED: No longer sent (only StyleProfile is used)
            string? styleProfile = null, // ACTIVE: Universal writing style from Style Profile (THE ONLY style guidance sent)
            string? outputMode = null, // Output mode: replace/clipboard/window/prompt (determines which orchestrator prompt to use)
            string? prefixLanguage = null, // Prefix language: NL or EN (for auto-prefix)
            bool includeAssistantContext = false) // Stuur achtergrondprofiel + geheugen uit de mAI Assistent mee
        {
            var payload = new
            {
                text,
                imageUrls, // Array of Cloudinary image URLs (null if no images)
                publicIds, // Array of Cloudinary public IDs for deletion (null if no images)
                orderedContent, // Interleaved text+image blocks (null if not interleaved)
                promptId,
                trainingExamples,
                model,
                customPrompt,
                stylePrompt,
                styleExamples,
                trainingPairs,
                consolidatedLessons,
                userInstructions,
                aiParameters,
                textExamples,
                styleProfile,
                outputMode,
                prefixLanguage,
                includeAssistantContext
            };
            return await PostAsync<AnalyzeResponse>("/ai/analyze", payload);
        }

        public async Task<TrainStyleResponse> TrainStyleAsync(string exampleTexts)
        {
            var payload = new
            {
                exampleTexts,
                lang = L.CurrentLanguage
            };
            return await PostAsync<TrainStyleResponse>("/ai/train-style", payload);
        }

        public async Task<TrainStyleResponse> RetrainWithFeedbackAsync(string originalTrainingInput, string[] styleExamples, StyleFeedbackItem[] feedbackItems)
        {
            var payload = new
            {
                originalTrainingInput,
                styleExamples,
                feedbackItems
            };
            return await PostAsync<TrainStyleResponse>("/ai/retrain-with-feedback", payload);
        }

        public async Task<AvailableModelsResponse> GetAvailableModelsAsync()
        {
            return await GetAsync<AvailableModelsResponse>($"/ai/models?lang={L.CurrentLanguage}");
        }

        /// <summary>
        /// Vraagt de backend welke geconfigureerde model-ID's zijn uitgefaseerd en naar
        /// welk vervangend model ze wijzen (Supabase replacement_model_id).
        /// </summary>
        public async Task<ResolveModelsResponse> ResolveModelsAsync(string[] models)
        {
            return await PostAsync<ResolveModelsResponse>("/ai/models/resolve", new { models });
        }

        public async Task<ConsolidateResponse> ConsolidateFeedbackAsync(Services.FeedbackExample[] feedbackExamples)
        {
            var payload = new
            {
                feedbackExamples
            };
            return await PostAsync<ConsolidateResponse>("/ai/consolidate-feedback", payload);
        }

        /// <summary>
        /// AI Hotkey Builder - Interactive chat to create optimized hotkey configurations
        /// </summary>
        public async Task<BuildHotkeyResponse> BuildHotkeyAsync(System.Collections.Generic.List<ChatMessage> conversationHistory, string userMessage)
        {
            var payload = new
            {
                conversationHistory,
                userMessage,
                lang = L.CurrentLanguage
            };
            return await PostAsync<BuildHotkeyResponse>("/ai/build-hotkey", payload);
        }

        /// <summary>
        /// AI Prompt Optimizer - Analyzes hotkey usage and suggests optimizations
        /// </summary>
        public async Task<OptimizePromptResponse> OptimizePromptAsync(
            System.Collections.Generic.List<ChatMessage> conversationHistory,
            string userMessage,
            HotkeyConfig currentHotkey)
        {
            // Take last 5 usage examples (full text, no trimming)
            var recentUsage = currentHotkey.RecentUsage?.TakeLast(5).ToArray();

            var payload = new
            {
                conversationHistory,
                userMessage,
                currentConfig = new
                {
                    name = currentHotkey.Name,
                    customPrompt = currentHotkey.CustomPrompt,
                    model = currentHotkey.Model,
                    outputMode = currentHotkey.OutputMode,
                    styleId = currentHotkey.StyleId,
                    aiParameters = currentHotkey.CustomAIParameters
                },
                recentUsage,
                lang = L.CurrentLanguage
            };
            return await PostAsync<OptimizePromptResponse>("/ai/optimize-prompt", payload);
        }

        /// <summary>
        /// Iterate Prompt Mode - Conversational feedback loop for prompt mode hotkeys
        /// </summary>
        public async Task<AnalyzeResponse> IteratePromptModeAsync(
            string originalInput,
            string initialUserInstructions,
            string previousResponse,
            ConversationMessage[] conversationHistory,
            HotkeyConfig hotkey)
        {
            var payload = new
            {
                originalInput,
                initialUserInstructions,
                previousResponse,
                conversationHistory,
                model = hotkey.Model,
                customPrompt = hotkey.CustomPrompt,
                trainingPairs = hotkey.TrainingPairs,
                feedbackHistory = hotkey.FeedbackHistory?.TakeLast(20).ToArray(),
                consolidatedLessons = hotkey.ConsolidatedLessons,
                aiParameters = hotkey.CustomAIParameters
            };
            return await PostAsync<AnalyzeResponse>("/ai/iterate-prompt", payload);
        }

        /// <summary>
        /// Generate Style Profile - AI analyzes examples and creates style description
        /// </summary>
        public async Task<string?> GenerateStyleProfileAsync(string[] examples, string? usageContext = null)
        {
            var payload = new { examples, usageContext };
            var response = await PostAsync<GenerateStyleProfileResponse>("/ai/generate-style-profile", payload);
            return response?.StyleProfile;
        }

public async Task<OptimizeStyleProfileResponse?> OptimizeStyleProfileAsync(
            string currentStyleProfile,
            string usageContext,
            string? userFeedback = null)
        {
            var payload = new
            {
                currentStyleProfile,
                usageContext,
                userFeedback
            };
            var response = await PostAsync<OptimizeStyleProfileResponse>("/ai/optimize-style-profile", payload);
            return response;
        }

        // ============================================
        // USAGE STATS & SUBSCRIPTION
        // ============================================

        public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync()
        {
            return await GetAsync<SubscriptionStatusResponse>("/subscription/status");
        }

        /// <summary>
        /// Upload image to Cloudinary via backend (credentials nooit in frontend)
        /// </summary>
        public async Task<ImageUploadResponse> UploadImageAsync(string base64Image)
        {
            var payload = new { base64Image };
            return await PostAsync<ImageUploadResponse>("/ai/upload-image", payload);
        }

        /// <summary>
        /// Create Mollie checkout session for tier upgrade
        /// </summary>
        public async Task<CheckoutResponse> CreateCheckoutAsync(string tier)
        {
            var payload = new { tier };
            return await PostAsync<CheckoutResponse>("/subscription/upgrade", payload);
        }

        /// <summary>
        /// Get payment history
        /// </summary>
        public async Task<PaymentHistoryResponse> GetPaymentHistoryAsync()
        {
            return await GetAsync<PaymentHistoryResponse>("/subscription/payments");
        }

        /// <summary>
        /// Cancel subscription (downgrade at end of period)
        /// </summary>
        public async Task<CancelSubscriptionResponse> CancelSubscriptionAsync()
        {
            return await PostAsync<CancelSubscriptionResponse>("/subscription/cancel", new { });
        }

        public async Task<DailyStatsResponse> GetDailyStatsAsync()
        {
            return await GetAsync<DailyStatsResponse>("/usage/daily");
        }

        public async Task<MonthlyStatsResponse> GetMonthlyStatsAsync()
        {
            return await GetAsync<MonthlyStatsResponse>("/usage/monthly");
        }

        // ============================================
        // FEEDBACK
        // ============================================

        public async Task<FeedbackResponse> SubmitFeedbackAsync(string originalText, string aiResponse, string correctedText, string feedbackType)
        {
            var payload = new
            {
                originalText,
                aiResponse,
                correctedText,
                feedbackType
            };
            return await PostAsync<FeedbackResponse>("/feedback/submit", payload);
        }

        public async Task<StyleProfile> GetStyleProfileAsync()
        {
            return await GetAsync<StyleProfile>("/feedback/profile");
        }

        // ============================================
        // EXPORT / IMPORT
        // ============================================

        public async Task<SaveExportResponse> SaveExportAsync(HotkeyConfig[] hotkeys, WritingStyle[] styles)
        {
            var payload = new { hotkeys, styles };
            return await PostAsync<SaveExportResponse>("/export/save", payload);
        }

        public async Task<GetExportResponse> GetExportAsync()
        {
            return await GetAsync<GetExportResponse>("/export/get");
        }

        /// <summary>
        /// Synct de desktop-hotkeys naar de cloud zodat de mobiele app ze automatisch kan ophalen.
        /// Het stijlprofiel van de gekoppelde WritingStyle wordt ingebed als consolidatedLessons,
        /// zodat de mobiele app de juiste schrijfstijl kan toepassen zonder extra fetch.
        /// </summary>
        public async Task<ApiResponse> SyncHotkeysToCloudAsync(HotkeyConfig[] hotkeys, WritingStyle[] allStyles)
        {
            var payload = new
            {
                hotkeys = hotkeys.Select(h =>
                {
                    // Embed het stijlprofiel als de hotkey een StyleId heeft
                    string? embeddedStyleProfile = h.ConsolidatedLessons;
                    if (embeddedStyleProfile == null && h.StyleId != null)
                    {
                        var style = allStyles.FirstOrDefault(s => s.Id == h.StyleId);
                        embeddedStyleProfile = style?.StyleProfile;
                    }

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
                        consolidatedLessons = embeddedStyleProfile,
                        prefixLanguage = h.PrefixLanguage,
                        prependText = h.PrependText,
                        appendText = h.AppendText,
                        integrationType = h.IntegrationType,
                        aiParameters = h.CustomAIParameters
                    };
                }).ToArray()
            };
            return await PutAsync<ApiResponse>("/hotkeys/desktop", payload);
        }

        // ============================================
        // PROMPT TEMPLATES
        // ============================================

        public async Task<RemotePromptTemplatesResponse> GetPromptTemplatesAsync(string lang = "nl")
        {
            return await GetAsync<RemotePromptTemplatesResponse>($"/prompts/templates?lang={lang}");
        }

        /// <summary>
        /// Zoekt één sjabloon op zijn deelbare code (voor "Voeg toe met code").
        /// Geeft null terug als er geen sjabloon met die code bestaat (HTTP 404).
        /// Gebruikt een directe request zodat een niet-bestaande code (normale
        /// gebruikersactie) niet in het foutenlog belandt.
        /// </summary>
        public async Task<RemotePromptTemplate?> GetTemplateByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            SetLocalTimeHeader();
            var encoded = Uri.EscapeDataString(code.Trim());
            var response = await _httpClient.GetAsync($"/prompts/templates/code/{encoded}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                await _loggingService.LogApiErrorAsync(
                    endpoint: "/prompts/templates/code",
                    error: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} | Body: {content}",
                    statusCode: (int)response.StatusCode,
                    requestPayload: null,
                    stackTrace: null);
                throw new ApiException(L.T("Error_General_Body"), content, statusCode: response.StatusCode);
            }

            var parsed = JsonSerializer.Deserialize<RemotePromptTemplateResponse>(content, JsonOpts);
            return parsed?.Template;
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        public async Task<T> GetAsync<T>(string endpoint)
        {
            SetLocalTimeHeader();
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();

                // If status is not success, log and throw regardless of body content
                if (!response.IsSuccessStatusCode)
                {
                    // 401/403/402 are normal auth/payment flow - don't pollute error log
                    bool isExpectedAuthError = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        || response.StatusCode == System.Net.HttpStatusCode.Forbidden
                        || (int)response.StatusCode == 402;

                    if (!isExpectedAuthError)
                    {
                        await _loggingService.LogApiErrorAsync(
                            endpoint: endpoint,
                            error: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} | Body: {content}",
                            statusCode: (int)response.StatusCode,
                            requestPayload: null,
                            stackTrace: null
                        );
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new ApiException(GetForbiddenMessage(content), content);
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)402)
                    {
                        throw new ApiException(Get402Message(content), content, isUpgradeError: true);
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)426)
                    {
                        // Update required — client version too old
                        string downloadUrl = "https://maikey.nl";
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(content);
                            if (parsed.TryGetProperty("download_url", out var du))
                                downloadUrl = du.GetString() ?? downloadUrl;
                        }
                        catch { }
                        throw new ApiException(
                            L.Tf("Update_Outdated", downloadUrl),
                            content, errorType: "UPDATE_REQUIRED");
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        int? retryAfterSeconds = null;
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(content);
                            var errorType = parsed.TryGetProperty("errorType", out var et) ? et.GetString() : null;
                            if (errorType == "DAILY_LIMIT_REACHED")
                            {
                                var used  = parsed.TryGetProperty("dailyUsage", out var u) && u.ValueKind == JsonValueKind.Number ? (int?)u.GetInt32() : null;
                                var limit = parsed.TryGetProperty("dailyLimit", out var l) && l.ValueKind == JsonValueKind.Number ? (int?)l.GetInt32() : null;
                                string msg = used.HasValue && limit.HasValue
                                    ? L.Tf("Error_429_DayLimit", used, limit)
                                    : L.T("Error_429_DayLimitSimple");
                                throw new ApiException(msg, content, isUpgradeError: true);
                            }
                            if (parsed.TryGetProperty("retryAfterSeconds", out var ras) && ras.ValueKind == JsonValueKind.Number)
                                retryAfterSeconds = ras.GetInt32();
                        }
                        catch (ApiException) { throw; }
                        catch { }
                        throw new ApiException(L.T("Error_429_ServerBusy"), content, errorType: "RATE_LIMIT", retryAfterSeconds: retryAfterSeconds);
                    }
                    else if ((int)response.StatusCode == 503)
                    {
                        throw new ApiException(L.T("Error_ModelUnavailable_Body"), content, errorType: "MODEL_UNAVAILABLE");
                    }
                    throw new ApiException(L.T("Error_General_Body"), content, statusCode: response.StatusCode);
                }

                // Status is success — deserialize
                try
                {
                    var result = JsonSerializer.Deserialize<T>(content, JsonOpts);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (JsonException) { /* fall through to deserialize error below */ }

                // Couldn't deserialize a 2xx response
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Failed to deserialize response | Body: {content}",
                    statusCode: (int)response.StatusCode,
                    requestPayload: null,
                    stackTrace: null
                );
                throw new ApiException(L.T("Error_General_Body"), content);
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Request timeout: {ex.Message}",
                    statusCode: null,
                    requestPayload: null,
                    stackTrace: ex.StackTrace
                );
                throw new ApiException(L.T("Error_Timeout_Body"), ex.Message, errorType: "TIMEOUT_ERROR");
            }
            catch (HttpRequestException ex)
            {
                // Log network error
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Network error: {ex.Message}",
                    statusCode: null,
                    requestPayload: null,
                    stackTrace: ex.StackTrace
                );
                throw new ApiException(L.T("Error_NoInternet_Body"), ex.Message, errorType: "NETWORK_ERROR");
            }
        }

        private async Task<T?> DeleteAsync<T>(string endpoint)
        {
            SetLocalTimeHeader();
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Log the API error
                    await _loggingService.LogApiErrorAsync(
                        endpoint: endpoint,
                        error: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} | Body: {responseContent}",
                        statusCode: (int)response.StatusCode,
                        requestPayload: null,
                        stackTrace: null
                    );

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new ApiException(GetForbiddenMessage(responseContent), responseContent);
                    }
                    throw new ApiException($"API Error: {response.StatusCode}", responseContent);
                }

                var result = JsonSerializer.Deserialize<T>(responseContent, JsonOpts);

                return result;
            }
            catch (HttpRequestException ex)
            {
                // Log network error
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Network error: {ex.Message}",
                    statusCode: null,
                    requestPayload: null,
                    stackTrace: ex.StackTrace
                );
                throw new ApiException(L.T("Error_NoInternet_Body"), ex.Message, errorType: "NETWORK_ERROR");
            }
        }

        private async Task<T> PutAsync<T>(string endpoint, object payload)
        {
            SetLocalTimeHeader();
            var json = JsonSerializer.Serialize(ToJsonSafePayload(payload), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = content };
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ApiException(L.T("Error_General_Body"), responseContent, statusCode: response.StatusCode);

            var result = JsonSerializer.Deserialize<T>(responseContent, JsonOpts);
            return result ?? throw new ApiException(L.T("Error_General_Body"), responseContent);
        }

        private async Task<T> PostAsync<T>(string endpoint, object payload)
        {
            SetLocalTimeHeader();
            try
            {
                // Use camelCase for JSON property names (REST API best practice)
                var json = JsonSerializer.Serialize(ToJsonSafePayload(payload), new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });

                System.Diagnostics.Debug.WriteLine($"[ApiClient] POST {endpoint}, payload length: {json.Length} chars");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // If status is not success, log and throw regardless of body content
                if (!response.IsSuccessStatusCode)
                {
                    await _loggingService.LogApiErrorAsync(
                        endpoint: endpoint,
                        error: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} | Body: {responseContent}",
                        statusCode: (int)response.StatusCode,
                        requestPayload: payload,
                        stackTrace: null
                    );

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new ApiException(GetForbiddenMessage(responseContent), responseContent);
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)402)
                    {
                        throw new ApiException(Get402Message(responseContent), responseContent, isUpgradeError: true);
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)426)
                    {
                        string downloadUrl = "https://maikey.nl";
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            if (parsed.TryGetProperty("download_url", out var du))
                                downloadUrl = du.GetString() ?? downloadUrl;
                        }
                        catch { }
                        throw new ApiException(
                            L.Tf("Update_Outdated", downloadUrl),
                            responseContent, errorType: "UPDATE_REQUIRED");
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        int? retryAfterSeconds = null;
                        try
                        {
                            var parsed429 = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            var et429 = parsed429.TryGetProperty("errorType", out var etProp) ? etProp.GetString() : null;
                            var err429 = parsed429.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                            if (err429 == "integration_limit_reached")
                            {
                                var cap = parsed429.TryGetProperty("daily_cap", out var dc) && dc.ValueKind == JsonValueKind.Number ? (int?)dc.GetInt32() : null;
                                string msg = cap.HasValue
                                    ? L.Tf("Error_IntegrationLimit", cap)
                                    : L.T("Error_IntegrationLimit_Simple");
                                throw new ApiException(msg, responseContent, isUpgradeError: true);
                            }
                            if (et429 == "DAILY_LIMIT_REACHED")
                            {
                                var used  = parsed429.TryGetProperty("dailyUsage", out var u) && u.ValueKind == JsonValueKind.Number ? (int?)u.GetInt32() : null;
                                var limit = parsed429.TryGetProperty("dailyLimit", out var l) && l.ValueKind == JsonValueKind.Number ? (int?)l.GetInt32() : null;
                                string msg = used.HasValue && limit.HasValue
                                    ? L.Tf("Error_429_DayLimit", used, limit)
                                    : L.T("Error_429_DayLimitSimple");
                                throw new ApiException(msg, responseContent, isUpgradeError: true);
                            }
                            // Read retryAfterSeconds sent by our rate limiter
                            if (parsed429.TryGetProperty("retryAfterSeconds", out var ras) && ras.ValueKind == JsonValueKind.Number)
                                retryAfterSeconds = ras.GetInt32();
                        }
                        catch (ApiException) { throw; }
                        catch { }
                        throw new ApiException(L.T("Error_429_ServerBusy"), responseContent, errorType: "RATE_LIMIT", retryAfterSeconds: retryAfterSeconds);
                    }
                    else if ((int)response.StatusCode == 503)
                    {
                        throw new ApiException(L.T("Error_ModelUnavailable_Body"), responseContent, errorType: "MODEL_UNAVAILABLE");
                    }
                    // Parse errorType from body so UI can show specific localized messages
                    string? parsedErrorType = null;
                    try {
                        var parsedBody = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (parsedBody.TryGetProperty("errorType", out var etProp))
                            parsedErrorType = etProp.GetString();
                        else if (parsedBody.TryGetProperty("error", out var eProp))
                            parsedErrorType = eProp.GetString();
                    } catch { }
                    throw new ApiException(L.T("Error_General_Body"), responseContent,
                        errorType: parsedErrorType, statusCode: response.StatusCode);
                }

                // Status is success — deserialize
                try
                {
                    var result = JsonSerializer.Deserialize<T>(responseContent, JsonOpts);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (JsonException) { /* fall through to deserialize error below */ }

                // Couldn't deserialize a 2xx response
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: "Failed to deserialize response",
                    statusCode: (int)response.StatusCode,
                    requestPayload: payload,
                    stackTrace: null
                );
                throw new ApiException(L.T("Error_General_Body"), responseContent);
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Request timeout: {ex.Message}",
                    statusCode: null,
                    requestPayload: payload,
                    stackTrace: ex.StackTrace
                );
                throw new ApiException(L.T("Error_Timeout_Body"), ex.Message, errorType: "TIMEOUT_ERROR");
            }
            catch (HttpRequestException ex)
            {
                // Log network error with request payload
                await _loggingService.LogApiErrorAsync(
                    endpoint: endpoint,
                    error: $"Network error: {ex.Message}",
                    statusCode: null,
                    requestPayload: payload,
                    stackTrace: ex.StackTrace
                );
                throw new ApiException(L.T("Error_NoInternet_Body"), ex.Message, errorType: "NETWORK_ERROR");
            }
        }

        // ============================================
        // CHAT (conversational follow-up)
        // ============================================

        public async Task<string> ChatAsync(List<ChatMessage> messages)
        {
            var payload = new { messages = messages.Select(m => new { role = m.Role, content = m.Content }) };
            var response = await PostAsync<ChatResponse>("/ai/chat", payload);
            return response.Result ?? "";
        }

        // ============================================
        // ANNOUNCEMENTS (public, no auth needed)
        // ============================================

        public async Task<List<Announcement>> GetAnnouncementsAsync(string lang = "nl")
        {
            try
            {
                var response = await _httpClient.GetAsync($"/announcements?lang={lang}");
                var content = await response.Content.ReadAsStringAsync();
                var parsed = System.Text.Json.JsonSerializer.Deserialize<AnnouncementsResponse>(
                    content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return parsed?.Announcements ?? new List<Announcement>();
            }
            catch
            {
                return new List<Announcement>();
            }
        }
    }

    // ============================================
    // API RESPONSE MODELS
    // ============================================

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfo? User { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public string? Status { get; set; }  // Google-login poll: "pending" zolang gebruiker nog bezig is
    }

    public class OAuthStartResponse
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? SessionId { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserId { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfo? User { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public bool RequiresVerification { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
    }

    public class RefreshTokenResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfo? User { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
    }

    public class UserInfo
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Name { get; set; }
        public string Subscription { get; set; } = "free";
        public string? Tier { get; set; }  // Backend sends "tier" instead of "subscription"
    }

    public class SubscriptionStatusResponse
    {
        public bool Success { get; set; }

        // NEW: Simplified response - NO CREDITS EXPOSED
        [System.Text.Json.Serialization.JsonPropertyName("tier")]
        public string Tier { get; set; } = "Gratis"; // "Gratis", "Starter", "Plus", "Pro"

        [System.Text.Json.Serialization.JsonPropertyName("tier_id")]
        public string TierId { get; set; } = "free"; // "free", "starter", "plus", "pro"

        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public decimal Price { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("days_until_reset")]
        public int DaysUntilReset { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reset_date")]
        public string ResetDate { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("usage_warning")]
        public bool UsageWarning { get; set; } // 80% gebruikt

        [System.Text.Json.Serialization.JsonPropertyName("usage_exceeded")]
        public bool UsageExceeded { get; set; } // 100% gebruikt

        // Feature flags per tier
        [System.Text.Json.Serialization.JsonPropertyName("max_hotkeys")]
        public int? MaxHotkeys { get; set; } // null = onbeperkt (Pro)

        [System.Text.Json.Serialization.JsonPropertyName("max_style_profiles")]
        public int? MaxStyleProfiles { get; set; } // null = onbeperkt (Pro)

        [System.Text.Json.Serialization.JsonPropertyName("image_analysis")]
        public bool ImageAnalysis { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("cloud_sync")]
        public bool CloudSync { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("integrations")]
        public bool Integrations { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("integration_actions_per_day")]
        public int? IntegrationActionsPerDay { get; set; } // null = onbeperkt; gratis = 3

        [System.Text.Json.Serialization.JsonPropertyName("high_usage_models")]
        public List<string> HighUsageModels { get; set; } = new List<string>(); // modellen met "verbruikt sneller"-melding

        [System.Text.Json.Serialization.JsonPropertyName("allowed_models")]
        public List<string> AllowedModels { get; set; } = new List<string>(); // toegestane modellen ("*" = alle)

        [System.Text.Json.Serialization.JsonPropertyName("max_monthly_requests")]
        public int? MaxMonthlyRequests { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requests_used_this_month")]
        public int? RequestsUsedThisMonth { get; set; }

        public string? Error { get; set; }

        // DEPRECATED - Keep for backwards compatibility during migration
        [Obsolete("Use new tier properties instead")]
        public SubscriptionInfo? Subscription { get; set; }
    }

    // DEPRECATED - Only kept for backwards compatibility
    [Obsolete("Use SubscriptionStatusResponse properties directly")]
    public class SubscriptionInfo
    {
        public string Tier { get; set; } = "free";
        public string Tier_Name { get; set; } = "Free";
        public decimal Credits_Remaining { get; set; }
        public int Credits_Limit { get; set; }
        public double Credits_Percentage { get; set; }
        public DateTime? Reset_Date { get; set; }
        public DateTime? Expires_At { get; set; }
        public bool Is_Active { get; set; }
        public int Period_Days { get; set; }
    }

    // ============================================
    // PAYMENT & CHECKOUT RESPONSES
    // ============================================

    public class ImageUploadResponse
    {
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("publicId")]
        public string PublicId { get; set; } = "";

        public string? Error { get; set; }
    }

    public class CheckoutResponse
    {
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("checkout_url")]
        public string CheckoutUrl { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("payment_id")]
        public string? PaymentId { get; set; }

        public bool Mock { get; set; } // True if mock payment (dev mode)
        public string? Error { get; set; }
    }

    public class PaymentHistoryResponse
    {
        public bool Success { get; set; }
        public PaymentRecord[]? Payments { get; set; }
        public int Count { get; set; }
        public string? Error { get; set; }
    }

    public class PaymentRecord
    {
        public string Id { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Tier { get; set; } = "";
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("payment_method")]
        public string? PaymentMethod { get; set; }
    }

    public class CancelSubscriptionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        public string? Error { get; set; }
    }

    // ============================================
    // AI RESPONSES
    // ============================================

    public class AnalyzeResponse
    {
        public bool Success { get; set; }
        public string? Response { get; set; }
        public UsageInfo? Usage { get; set; }
        public string? Model { get; set; }
        public string? Error { get; set; }
        public string? FallbackModel { get; set; } // Ingevuld als een fallback provider werd gebruikt
        public string? ModelUsed { get; set; } // Werkelijk gebruikt model (kan afwijken bij cascade of fallback)
        public System.Text.Json.JsonElement? Debug { get; set; } // ⚠️ TESTFASE ONLY - Complete OpenAI request details
    }

    public class UsageInfo
    {
        public int Tokens { get; set; }
        public double Cost { get; set; }
        public int RemainingToday { get; set; }
    }

    public class TrainingExample
    {
        public string Input { get; set; } = "";
        public string ExpectedOutput { get; set; } = "";
    }

    /// <summary>
    /// A single block in an interleaved text/image sequence
    /// </summary>
    public class OrderedContentBlock
    {
        public string Type { get; set; } = "text"; // "text" or "image"
        public string? Text { get; set; }
        public string? ImageUrl { get; set; } // Cloudinary URL (only for type="image")
    }

    public class DailyStatsResponse
    {
        public bool Success { get; set; }
        public DailyStats? Today { get; set; }
        public LimitInfo? Limits { get; set; }
        public string? Subscription { get; set; }
    }

    public class DailyStats
    {
        public int Requests { get; set; }
        public int Tokens { get; set; }
        public double Cost { get; set; }
    }

    public class LimitInfo
    {
        public int DailyRequests { get; set; }
        public int Remaining { get; set; }
    }

    public class MonthlyStatsResponse
    {
        public bool Success { get; set; }
        public MonthStats? Stats { get; set; }
    }

    public class MonthStats
    {
        public int Requests { get; set; }
        public int Tokens { get; set; }
        public double Cost { get; set; }
    }

    public class FeedbackResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class StyleProfile
    {
        public int FormalityScore { get; set; }
        public int VerbosityScore { get; set; }
        public bool EmojiUsage { get; set; }
        public string[]? TypicalGreetings { get; set; }
        public string[]? TypicalClosings { get; set; }
        public string? Tone { get; set; }
    }

    public class AvailableModelsResponse
    {
        public bool Success { get; set; }
        public AIModel[]? Models { get; set; }
    }

    public class ResolveModelsResponse
    {
        public bool Success { get; set; }
        public ModelResolution[]? Resolutions { get; set; }
    }

    public class ModelResolution
    {
        public string Id { get; set; } = "";
        public bool Changed { get; set; }
        public string? FromName { get; set; }
        public string? ReplacementId { get; set; }
        public string? ReplacementName { get; set; }
    }

    public class AIModel
    {
        public string Id          { get; set; } = "";
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Provider    { get; set; } = "";  // "Auto", "Google", "OpenAI", "Groq", "Anthropic", "Moonshot"
        public int SpeedLevel     { get; set; }        // 1–5  (1=traag,  5=razendsnel)
        public int QualityLevel   { get; set; }        // 1–5  (1=basis,  5=frontier)
        public int UsageLevel     { get; set; }        // 1–5  (1=zuinig, 5=duur)
    }

    public class TrainStyleResponse
    {
        public bool Success { get; set; }
        public string? StylePrompt { get; set; }
        public string[]? SelectedExamples { get; set; }
        public string? Improvements { get; set; }
        public int ExampleTextLength { get; set; }
        public int StylePromptLength { get; set; }
        public string? Error { get; set; }
    }

    public class ConsolidateResponse
    {
        public bool Success { get; set; }
        public string? ConsolidatedLessons { get; set; }
        public string? Error { get; set; }
    }

    public class GenerateStyleProfileResponse
    {
        public bool Success { get; set; }
        public string? StyleProfile { get; set; }
        public string? Error { get; set; }
    }

    public class OptimizeStyleProfileResponse
    {
        public bool Success { get; set; }
        public string? OptimizedStyleProfile { get; set; }
        public string? Changes { get; set; }
        public string? Error { get; set; }
    }

    public class SaveExportResponse
    {
        public bool Success { get; set; }
        public string? ExportedAt { get; set; }
        public int HotkeyCount { get; set; }
        public int StyleCount { get; set; }
        public string? Error { get; set; }
    }

    public class GetExportResponse
    {
        public bool Success { get; set; }
        public CloudExportData? Export { get; set; }
        public string? Error { get; set; }
    }

    public class CloudExportData
    {
        public string? ExportedAt { get; set; }
        public HotkeyConfig[]? Hotkeys { get; set; }
        public WritingStyle[]? Styles { get; set; }
    }

    // ============================================
    // PROMPT TEMPLATES MODELS
    // ============================================

    public class RemoteTemplateVariable
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Placeholder { get; set; } = "";
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
        public string? ShareCode { get; set; }
    }

    public class RemoteIntegrationAction
    {
        public string Action { get; set; } = "";
        public bool ShowReviewWindow { get; set; } = true;
    }

    public class RemotePromptTemplatesResponse
    {
        public List<RemotePromptTemplate> Templates { get; set; } = new();
        public int Count { get; set; }
        public string? UpdatedAt { get; set; }
    }

    public class RemotePromptTemplateResponse
    {
        public RemotePromptTemplate? Template { get; set; }
    }

    public class ApiException : Exception
    {
        public string Details { get; }
        public bool IsUpgradeError { get; }
        public System.Net.HttpStatusCode? StatusCode { get; }
        /// <summary>
        /// Machine-readable type: "NETWORK_ERROR", "TIMEOUT_ERROR", "MODEL_UNAVAILABLE", etc.
        /// Used to show the right localized message in the UI.
        /// </summary>
        public string? ErrorType { get; }
        /// <summary>Seconds to wait before retrying (filled from RATE_LIMIT responses).</summary>
        public int? RetryAfterSeconds { get; }

        public ApiException(string message, string details, bool isUpgradeError = false,
                            System.Net.HttpStatusCode? statusCode = null, string? errorType = null,
                            int? retryAfterSeconds = null) : base(message)
        {
            Details = details;
            IsUpgradeError = isUpgradeError;
            StatusCode = statusCode;
            ErrorType = errorType;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }

    public class ChatMessage
    {
        public string Role    { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class ChatResponse
    {
        public bool    Success { get; set; }
        public string? Result  { get; set; }
        public string? Error   { get; set; }
    }

    public class Announcement
    {
        public string   Id          { get; set; } = "";
        public string   Type        { get; set; } = "";
        public string   Title       { get; set; } = "";
        public string   Body        { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }
    }

    public class AnnouncementsResponse
    {
        public bool              Success       { get; set; }
        public List<Announcement>? Announcements { get; set; }
    }
}
