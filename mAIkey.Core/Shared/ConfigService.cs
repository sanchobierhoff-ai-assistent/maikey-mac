using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using mAIkey.Core.Interfaces;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Configuration service - opslag van settings, credentials, hotkeys.
    /// Mac-port van frontend/Services/ConfigService.cs: zelfde datamodel (zodat Cloud Sync
    /// en export/import 1-op-1 werken), maar met een Mac-configmap en de Mac-tokenversleuteling.
    /// </summary>
    public class ConfigService
    {
        // Houd major.minor gelijk aan de Windows-app: de backend-versiegate
        // (MINIMUM_CLIENT_VERSION) vergelijkt alleen de eerste twee getallen.
        public const string CURRENT_VERSION = "v1.16-mac";

        private static readonly string ConfigDirectory = ResolveConfigDirectory();

        private static readonly string ConfigFile = Path.Combine(ConfigDirectory, "config.json");

        /// <summary>
        /// Bepaalt een schrijfbare configmap (~/.config/mAIkey). Op macOS geeft
        /// GetFolderPath(ApplicationData) bij een Finder-start soms een lege string,
        /// daarom vallen we terug op de home-map en als laatste redmiddel op de temp-map.
        /// </summary>
        private static string ResolveConfigDirectory()
        {
            // Nooit de map van de Windows-app (%APPDATA%\mAIkey) gebruiken: een test-build van
            // deze Mac-code op Windows zou anders de echte Windows-config migreren/overschrijven.
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mAIkey-mac-dev");

            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home))
                    home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
                if (string.IsNullOrEmpty(home))
                    home = Path.GetTempPath();
                baseDir = Path.Combine(home, ".config");
            }
            return Path.Combine(baseDir, "mAIkey");
        }

        private readonly ITokenProtection? _tokenProtection;
        private AppConfig _config;

        private string? EncryptToken(string? plaintext) =>
            _tokenProtection != null ? _tokenProtection.Encrypt(plaintext) : plaintext;

        private string? DecryptToken(string? ciphertext) =>
            _tokenProtection != null ? _tokenProtection.Decrypt(ciphertext) : ciphertext;

        public ConfigService(ITokenProtection? tokenProtection = null)
        {
            _tokenProtection = tokenProtection;

            // Zorg dat directory bestaat (nooit crashen — desnoods zonder opslag)
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                    Directory.CreateDirectory(ConfigDirectory);
            }
            catch { }

            // Laad config
            _config = LoadConfig();

            CheckAndClearOldVersion();
        }

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AppConfig();

                    // Ontsleutel tokens eerst — migraties hieronder slaan de config
                    // (opnieuw versleuteld) op en mogen geen al-versleutelde waarde zien.
                    config.AuthToken = DecryptToken(config.AuthToken);
                    config.RefreshToken = DecryptToken(config.RefreshToken);
                    _config = config;

                    // Auto-migreer oude TrainingPairs naar nieuwe WritingStyles
                    MigrateOldTrainingPairs(config);

                    // Oudere Mac-builds sloegen Windows virtual-key-codes op; de Windows-app
                    // (en Cloud Sync) gebruikt de WPF/Avalonia Key-enum. Eenmalig omzetten.
                    MigrateLegacyMacKeyCodes(config);

                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }

            return new AppConfig { HotkeyKeyFormat = HotkeyKeys.FormatWpf };
        }

        private void MigrateLegacyMacKeyCodes(AppConfig config)
        {
            if (config.HotkeyKeyFormat == HotkeyKeys.FormatWpf) return;

            if (config.Hotkeys != null)
                foreach (var hk in config.Hotkeys)
                    hk.Key = HotkeyKeys.FromWindowsVirtualKey(hk.Key);

            config.HotkeyKeyFormat = HotkeyKeys.FormatWpf;
            SaveConfig();
        }

        private void MigrateOldTrainingPairs(AppConfig config)
        {
            if (config.Hotkeys == null || config.Hotkeys.Length == 0) return;

            var stylesToCreate = new List<WritingStyle>();
            var migratedCount = 0;

#pragma warning disable CS0618 // TrainingPairs is obsolete
            foreach (var hotkey in config.Hotkeys)
            {
                // Skip als al een StyleId heeft (al gemigreerd)
                if (hotkey.StyleId != null) continue;

                // Skip als geen TrainingPairs heeft (niets te migreren)
                if (hotkey.TrainingPairs == null || hotkey.TrainingPairs.Length == 0) continue;

                // Maak automatisch een stijl aan voor deze hotkey
                var newStyle = new WritingStyle
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"{hotkey.Name} Stijl",
                    Description = $"Automatisch gemigreerd van hotkey '{hotkey.Name}' op {DateTime.Now:dd-MM-yyyy}",
                    TextExamples = null,
                    Created = DateTime.Now,
                    Modified = DateTime.Now,
                    Metadata = new StyleMetadata
                    {
                        Formality = "Not set",
                        Language = "Not set"
                    }
                };

                stylesToCreate.Add(newStyle);

                // Link hotkey aan nieuwe stijl
                hotkey.StyleId = newStyle.Id;

                migratedCount++;
            }
#pragma warning restore CS0618

            // Voeg gemigreerde stijlen toe aan config
            if (stylesToCreate.Count > 0)
            {
                var existingStyles = config.WritingStyles?.ToList() ?? new List<WritingStyle>();
                existingStyles.AddRange(stylesToCreate);
                config.WritingStyles = existingStyles.ToArray();

                Console.WriteLine($"AUTO-MIGRATION: Created {stylesToCreate.Count} writing styles from old hotkey training pairs");

                // Sla gemigreerde config direct op
                SaveConfig();
            }
        }

        /// <summary>
        /// Migrate existing WritingStyles to have Type field (if missing)
        /// Called during config loading for backward compatibility
        /// </summary>
        private void MigrateStyleTypes(AppConfig config)
        {
            // Legacy migration for Type field - no longer needed, kept for compat
        }

        /// <summary>
        /// Werk de versie-tag bij als de app is geüpdatet.
        /// Alle bestaande configuratie (hotkeys, schrijfstijlen, auth) blijft behouden.
        /// </summary>
        private void CheckAndClearOldVersion()
        {
            if (_config.AppVersion == CURRENT_VERSION) return;

            var previousVersion = _config.AppVersion;
            System.Diagnostics.Debug.WriteLine($"VERSION CHANGE: {previousVersion ?? "(none)"} → {CURRENT_VERSION}");

            _config.AppVersion = CURRENT_VERSION;
            SaveConfig();
        }

        public void SaveConfig()
        {
            // Bewaar plaintext in memory — schrijf versleuteld naar schijf
            var plainAuthToken    = _config.AuthToken;
            var plainRefreshToken = _config.RefreshToken;
            try
            {
                _config.AuthToken    = EncryptToken(plainAuthToken);
                _config.RefreshToken = EncryptToken(plainRefreshToken);

                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
            finally
            {
                // Altijd herstellen, ook bij exceptions
                _config.AuthToken    = plainAuthToken;
                _config.RefreshToken = plainRefreshToken;
            }
        }

        // ============================================
        // AUTHENTICATION
        // ============================================

        public string? AuthToken
        {
            get => _config.AuthToken;
            set
            {
                _config.AuthToken = value;
                SaveConfig();
            }
        }

        public string? RefreshToken
        {
            get => _config.RefreshToken;
            set
            {
                _config.RefreshToken = value;
                SaveConfig();
            }
        }

        public string? UserEmail
        {
            get => _config.UserEmail;
            set
            {
                _config.UserEmail = value;
                SaveConfig();
            }
        }

        public string? UserName
        {
            get => _config.UserName;
            set
            {
                _config.UserName = value;
                SaveConfig();
            }
        }

        public string? SubscriptionTier
        {
            get => _config.SubscriptionTier;
            set
            {
                _config.SubscriptionTier = value;
                SaveConfig();
            }
        }

        public string? UserId
        {
            get => _config.UserId;
            set
            {
                _config.UserId = value;
                SaveConfig();
            }
        }

        // ============================================
        // HOTKEYS
        // ============================================

        public HotkeyConfig[] Hotkeys
        {
            get => _config.Hotkeys ?? Array.Empty<HotkeyConfig>();
            set
            {
                _config.Hotkeys = value;
                SaveConfig();
            }
        }

        public bool HasShownDemoHotkey
        {
            get => _config.HasShownDemoHotkey;
            set { _config.HasShownDemoHotkey = value; SaveConfig(); }
        }

        // ============================================
        // SETTINGS
        // ============================================

        public bool StartWithWindows
        {
            get => _config.StartWithWindows;
            set
            {
                _config.StartWithWindows = value;
                SaveConfig();
            }
        }

        public bool MinimizeToTray
        {
            get => _config.MinimizeToTray;
            set
            {
                _config.MinimizeToTray = value;
                SaveConfig();
            }
        }

        public bool ShowAiIndicator
        {
            get => _config.ShowAiIndicator;
            set { _config.ShowAiIndicator = value; SaveConfig(); }
        }

        public bool SoundOnComplete
        {
            get => _config.SoundOnComplete;
            set { _config.SoundOnComplete = value; SaveConfig(); }
        }

        public bool DesktopShortcut
        {
            get => _config.DesktopShortcut;
            set { _config.DesktopShortcut = value; SaveConfig(); }
        }

        public bool StartMenuShortcut
        {
            get => _config.StartMenuShortcut;
            set { _config.StartMenuShortcut = value; SaveConfig(); }
        }

        public string InterfaceLanguage
        {
            get => _config.InterfaceLanguage;
            set { _config.InterfaceLanguage = value; SaveConfig(); }
        }

        public string Theme
        {
            get => _config.Theme ?? "Light";
            set { _config.Theme = value; SaveConfig(); }
        }

        // ── Assistant-modus (command bar) ──
        public bool AssistantEnabled
        {
            get => _config.AssistantEnabled;
            set { _config.AssistantEnabled = value; SaveConfig(); }
        }

        public int AssistantHotkeyModifiers
        {
            get => _config.AssistantHotkeyModifiers;
            set { _config.AssistantHotkeyModifiers = value; SaveConfig(); }
        }

        public int AssistantHotkeyKey
        {
            get => _config.AssistantHotkeyKey;
            set { _config.AssistantHotkeyKey = value; SaveConfig(); }
        }

        public string? AssistantModel
        {
            get => _config.AssistantModel;
            set { _config.AssistantModel = value; SaveConfig(); }
        }

        public bool ScreenshotHotkeyEnabled
        {
            get => _config.ScreenshotHotkeyEnabled;
            set { _config.ScreenshotHotkeyEnabled = value; SaveConfig(); }
        }

        public int ScreenshotHotkeyModifiers
        {
            get => _config.ScreenshotHotkeyModifiers;
            set { _config.ScreenshotHotkeyModifiers = value; SaveConfig(); }
        }

        public int ScreenshotHotkeyKey
        {
            get => _config.ScreenshotHotkeyKey;
            set { _config.ScreenshotHotkeyKey = value; SaveConfig(); }
        }

        // Koppelingen (support ⇄ development)
        public bool KoppelingHotkeyEnabled
        {
            get => _config.KoppelingHotkeyEnabled;
            set { _config.KoppelingHotkeyEnabled = value; SaveConfig(); }
        }

        public int KoppelingHotkeyModifiers
        {
            get => _config.KoppelingHotkeyModifiers;
            set { _config.KoppelingHotkeyModifiers = value; SaveConfig(); }
        }

        public int KoppelingHotkeyKey
        {
            get => _config.KoppelingHotkeyKey;
            set { _config.KoppelingHotkeyKey = value; SaveConfig(); }
        }

        public string? KoppelingModel
        {
            get => _config.KoppelingModel;
            set { _config.KoppelingModel = value; SaveConfig(); }
        }

        public string? KoppelingStyleId
        {
            get => _config.KoppelingStyleId;
            set { _config.KoppelingStyleId = value; SaveConfig(); }
        }

        // Automatisch een interne notitie in Zendesk plaatsen bij het koppelen/aanmaken van een Jira-ticket.
        public bool KoppelingAutoBacklink
        {
            get => _config.KoppelingAutoBacklink;
            set { _config.KoppelingAutoBacklink = value; SaveConfig(); }
        }

        // Vaste instructie die bij het genereren van een Jira-ticket als user_instructions meegaat.
        public string? KoppelingJiraPrompt
        {
            get => _config.KoppelingJiraPrompt;
            set { _config.KoppelingJiraPrompt = value; SaveConfig(); }
        }

        public int MaxImages
        {
            get => _config.MaxImages;
            set
            {
                _config.MaxImages = value;
                SaveConfig();
            }
        }

        public int MaxCharacters
        {
            get => _config.MaxCharacters;
            set
            {
                _config.MaxCharacters = value;
                SaveConfig();
            }
        }

        public string ApiBaseUrl
        {
            // Default to localhost for development
            get => _config.ApiBaseUrl ?? "https://ai-assistent-backend-production.up.railway.app";
            set
            {
                _config.ApiBaseUrl = value;
                SaveConfig();
            }
        }

        public void ClearAuth()
        {
            AuthToken = null;
            RefreshToken = null;
            UserEmail = null;
            UserId = null;
        }

        // ============================================
        // WRITING STYLES MANAGEMENT
        // ============================================

        public WritingStyle[] WritingStyles
        {
            get => _config.WritingStyles ?? Array.Empty<WritingStyle>();
            set
            {
                _config.WritingStyles = value;
                SaveConfig();
            }
        }

        public WritingStyle[] GetWritingStyles()
        {
            return WritingStyles;
        }

        public WritingStyle? GetStyleById(string styleId)
        {
            var style = _config.WritingStyles?.FirstOrDefault(s => s.Id == styleId);
            if (style != null)
            {
                Console.WriteLine($"Found style: '{style.Name}' (Examples: {style.TextExamples?.Length ?? 0})");
            }
            else
            {
                Console.WriteLine($"âŒ Style not found for ID: {styleId}");
                Console.WriteLine($"   Available styles: {string.Join(", ", _config.WritingStyles?.Select(s => $"{s.Name} ({s.Id})") ?? new[] { "NONE" })}");
            }
            return style;
        }

        public void AddWritingStyle(WritingStyle style)
        {
            var styles = _config.WritingStyles?.ToList() ?? new List<WritingStyle>();
            style.Created = DateTime.Now;
            style.Modified = DateTime.Now;
            styles.Add(style);
            _config.WritingStyles = styles.ToArray();
            SaveConfig();
        }

        public void UpdateWritingStyle(WritingStyle style)
        {
            if (_config.WritingStyles == null) return;

            var index = Array.FindIndex(_config.WritingStyles, s => s.Id == style.Id);
            if (index >= 0)
            {
                style.Modified = DateTime.Now;
                _config.WritingStyles[index] = style;
                SaveConfig();
            }
        }

        public void DeleteWritingStyle(string styleId)
        {
            if (_config.WritingStyles == null) return;

            _config.WritingStyles = _config.WritingStyles
                .Where(s => s.Id != styleId)
                .ToArray();
            SaveConfig();
        }

        public TrainingPair[] GetTrainingPairsForHotkey(HotkeyConfig hotkey)
        {
            // Fallback: Oude config met directe TrainingPairs
#pragma warning disable CS0618 // TrainingPairs is obsolete
            if (hotkey.TrainingPairs != null && hotkey.TrainingPairs.Length > 0)
            {
                return hotkey.TrainingPairs;
            }
#pragma warning restore CS0618

            return Array.Empty<TrainingPair>();
        }

        // ============================================
        // ONBOARDING HELPERS
        // ============================================

        private static readonly string[] OnboardingSteps = { "hotkey", "style", "template", "pin" };

        public bool IsOnboardingDone()
        {
            return _config.OnboardingCompletedAt.HasValue ||
                   OnboardingSteps.All(s => _config.OnboardingCompletedIds.Contains(s));
        }

        public IReadOnlyList<string> GetOnboardingCompletedIds() => _config.OnboardingCompletedIds;

        public void SetOnboardingStepDone(string stepId)
        {
            if (!_config.OnboardingCompletedIds.Contains(stepId))
            {
                _config.OnboardingCompletedIds.Add(stepId);
                if (OnboardingSteps.All(s => _config.OnboardingCompletedIds.Contains(s)))
                    _config.OnboardingCompletedAt = DateTime.UtcNow;
                SaveConfig();
            }
        }

        // ============================================
        // DEFAULT CONFIG FOR DISTRIBUTION
        // ============================================

        /// <summary>
        /// Returns a clean default configuration for new users
        /// No authentication, no hotkeys, no writing styles
        /// </summary>
        public static AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                AuthToken = null,
                UserEmail = null,
                UserId = null,
                UserName = null,
                SubscriptionTier = "free",
                Hotkeys = Array.Empty<HotkeyConfig>(),
                WritingStyles = Array.Empty<WritingStyle>(),
                AppVersion = null,
                StartWithWindows = false,
                MinimizeToTray = true,
                MaxImages = 3,
                MaxCharacters = 1000,
                ApiBaseUrl = "https://ai-assistent-backend-production.up.railway.app"
            };
        }
    }

    // ============================================
    // CONFIG MODELS
    // ============================================

    public class AppConfig
    {
        public string? AuthToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserEmail { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? SubscriptionTier { get; set; } = "free";
        public HotkeyConfig[]? Hotkeys { get; set; }

        // NIEUW: Stijl library
        public WritingStyle[]? WritingStyles { get; set; }

        // NIEUW: Versie tracking voor auto-clear bij nieuwe versie
        public string? AppVersion { get; set; }

        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public int MaxImages { get; set; } = 3;
        public int MaxCharacters { get; set; } = 1000;
        public string? ApiBaseUrl { get; set; } = "https://ai-assistent-backend-production.up.railway.app"; // Railway production (NEVER test locally)

        // Onboarding: bijgehouden welke stappen zijn afgerond (one-way switch)
        public List<string> OnboardingCompletedIds { get; set; } = new();
        public DateTime? OnboardingCompletedAt { get; set; } = null;

        // Bijhouden of de demo-mAIkey al eenmalig aangemaakt is (zodat verwijdering permanent is)
        public bool HasShownDemoHotkey { get; set; } = false;

        // App gedrag
        public bool ShowAiIndicator { get; set; } = true;
        public bool SoundOnComplete { get; set; } = false;

        // Snelkoppelingen (Velopack maakt beide standaard bij installatie → default true)
        public bool DesktopShortcut { get; set; } = true;
        public bool StartMenuShortcut { get; set; } = true;

        // Taal & model
        // Eerste installatie: Nederlands/Duits als Windows zo staat ingesteld, anders
        // Nederlands (doelgroep is NL-first — nooit stilzwijgend Engels als default).
        // Alleen relevant zolang er nog geen config.json bestaat; zodra de gebruiker
        // zelf een taal kiest (Instellingen/Login/Register) wordt dat gewoon bewaard.
        public string InterfaceLanguage { get; set; } = DetectDefaultLanguage();

        private static string DetectDefaultLanguage()
        {
            var lang = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
            return lang switch
            {
                "nl" => "nl",
                "de" => "de",
                _ => "nl"
            };
        }

        // Assistant-modus: eigen globale sneltoets (default Ctrl+Alt+Space).
        // ModifierKeys: Alt=1, Ctrl=2, Shift=4, Win=8 → Ctrl+Alt = 3. Key.Space = 18.
        public bool AssistantEnabled { get; set; } = true;
        public int AssistantHotkeyModifiers { get; set; } = 3;
        public int AssistantHotkeyKey { get; set; } = 18;
        public string? AssistantModel { get; set; } = null;   // null = backend-standaard

        // Screenshot-sneltoets voor de assistent (default Ctrl+Shift+Space → mod 6, Key.Space 18).
        public bool ScreenshotHotkeyEnabled { get; set; } = true;
        public int ScreenshotHotkeyModifiers { get; set; } = 6;
        public int ScreenshotHotkeyKey { get; set; } = 18;

        // Koppelingen (support ⇄ development): eigen globale sneltoets die het hub-venster opent.
        // Default Ctrl+Alt+K → mod 3 (Ctrl+Alt), Key.K = 44. Brug-model leeg = sterke server-default.
        public bool KoppelingHotkeyEnabled { get; set; } = true;
        public int KoppelingHotkeyModifiers { get; set; } = 3;
        public int KoppelingHotkeyKey { get; set; } = 44;
        public string? KoppelingModel { get; set; } = null;
        public string? KoppelingStyleId { get; set; } = null;
        public bool KoppelingAutoBacklink { get; set; } = true;   // interne Zendesk-notitie bij koppelen
        public string? KoppelingJiraPrompt { get; set; } = null;  // vaste instructie voor Jira-ticket-generatie

        // Mac: opslagformaat van HotkeyConfig.Key ("wpf" = WPF/Avalonia Key-enum, zoals Windows).
        public string? HotkeyKeyFormat { get; set; }

        // Thema
        public string Theme { get; set; } = "Light";
    }

    public class HotkeyConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int ModifierKeys { get; set; } // Ctrl=2, Alt=1, Shift=4
        public int Key { get; set; } // Virtual key code
        public string? PromptId { get; set; }
        public bool Enabled { get; set; } = true;

        // AI Model selection
        public string? Model { get; set; } = "gpt-4o-mini"; // Selected AI model

        // Output mode
        public string OutputMode { get; set; } = "replace"; // "replace", "clipboard", "window" ("prompt" is legacy → window + AskForContext)

        // Ask for extra context before executing (independent of output mode)
        public bool AskForContext { get; set; } = false;

        // Show an input field instead of using the selected text (user types the input manually)
        [System.Text.Json.Serialization.JsonPropertyName("useInputInsteadOfSelection")]
        public bool UseInputInsteadOfSelection { get; set; } = false;

        // Include Images: Send clipboard images to AI (default: true)
        public bool IncludeImages { get; set; } = true;

        // Screenshot mode: capture screen region instead of clipboard content
        [System.Text.Json.Serialization.JsonPropertyName("useScreenCapture")]
        public bool UseScreenCapture { get; set; } = false;

        // Extra text to prepend/append
        public string? PrependText { get; set; }
        public string? AppendText { get; set; }

        // Prefix language for auto-prefix ("NL" or "EN") — defaults to app UI language
        public string? PrefixLanguage { get; set; } = L.CurrentLanguage == "en" ? "EN" : "NL";

        // ===== STYLE LIBRARY SYSTEM =====

        // NIEUW: Link naar stijl (nullable = "Geen stijl")
        public string? StyleId { get; set; }

        // DEPRECATED: Direct training pairs (gebruik StyleId ipv)
        [Obsolete("Use StyleId to link to WritingStyle instead")]
        public TrainingPair[]? TrainingPairs { get; set; }

        // Feedback history: Collected corrections from user (max 20 recent, rest gets consolidated)
        public FeedbackExample[]? FeedbackHistory { get; set; }

        // Consolidated lessons: Summary of old feedback (auto-generated when > 50 items)
        public string? ConsolidatedLessons { get; set; }
        public DateTime? LastConsolidation { get; set; }

        // ===== DEPRECATED (kept for backward compatibility, will be migrated) =====

        // Training examples for AI (legacy - replaced by TrainingPairs)
        [Obsolete("Use TrainingPairs instead")]
        public mAIkey.Core.Services.TrainingExample[]? TrainingExamples { get; set; }

        // "Train Mijn Stijl" feature - AI-analyzed writing style prompt (legacy)
        [Obsolete("Use TrainingPairs and FeedbackHistory instead")]
        public string? StylePrompt { get; set; }

        [Obsolete("Use TrainingPairs instead")]
        public string[]? StyleExamples { get; set; }

        [Obsolete("Use TrainingPairs instead")]
        public string? StyleTrainingInput { get; set; }

        [Obsolete("Use FeedbackHistory instead")]
        public StyleFeedbackItem[]? StyleFeedback { get; set; }

        // Custom prompt text (if PromptId is null)
        public string? CustomPrompt { get; set; }

        // Bevroren door abonnementsdowngrade — niet uitvoerbaar tot upgrade
        public bool FrozenByDowngrade { get; set; } = false;

        // Stuur het achtergrondprofiel + geheugen uit de mAI Assistent mee als context.
        public bool IncludeAssistantContext { get; set; } = false;

        // Persoonlijke context ingevuld bij aanmaken via template (bijv. naam, afsluiting, bedrijf)
        public Dictionary<string, string>? TemplateVariables { get; set; }

        // ===== AI PARAMETERS (Geavanceerde instellingen) =====
        public AIParameters? CustomAIParameters { get; set; }

        // ===== USAGE TRACKING (Voor prompt optimalisatie) =====
        // Track laatste 10 executions voor optimalisatie (altijd actief)
        public List<UsageExample>? RecentUsage { get; set; }

        // ===== INTEGRATION SYSTEM =====
        // Integration type: "jira", "github", "slack", etc. (null = normal AI hotkey)
        public string? IntegrationType { get; set; }

        // Integration-specific configuration
        public IntegrationAction? IntegrationAction { get; set; }
    }

    /// <summary>
    /// Integration action configuration for hotkeys
    /// </summary>
    public class IntegrationAction
    {
        // For Jira: "create-ticket"
        // For GitHub: "create-issue", "create-pr"
        public string Action { get; set; } = "";

        // Show review window before executing action
        public bool ShowReviewWindow { get; set; } = true;

        // Default assignee for JIRA tickets (accountId)
        public string? DefaultAssignee { get; set; }

        // Default project for JIRA tickets
        public string? DefaultProject { get; set; }

        // GitHub
        public string? DefaultRepo { get; set; }       // "owner/repo" — overschrijft integratie-instelling
        public string? DefaultLabels { get; set; }     // kommagescheiden labels override

        // Gmail
        public string? DefaultTo { get; set; }         // standaard ontvanger
        public string? DefaultSubject { get; set; }    // standaard onderwerp (overschrijft AI)
        public string? DefaultCc { get; set; }         // standaard CC

        // Google Calendar
        public string? DefaultCalendarId { get; set; } // agenda-id override
    }

    /// <summary>
    /// AI parameters for OpenAI API calls
    /// Geavanceerde instellingen per hotkey
    /// </summary>
    public class AIParameters
    {
        /// <summary>
        /// Temperature: Controls randomness/creativity (0.0-2.0)
        /// - 0.0-0.3: Deterministisch, consistent (vertalingen, zakelijke teksten)
        /// - 0.7: Standaard balans (default)
        /// - 1.0-2.0: Creatief, gevarieerd (brainstormen, content creatie)
        /// </summary>
        public double? Temperature { get; set; } = 0.7;

        /// <summary>
        /// Max Tokens: Maximum aantal tokens in de response (2000-16000)
        /// - 2000: ~1500 woorden (kort antwoord)
        /// - 8000: ~6000 woorden (standaard, default)
        /// - 16000: ~12000 woorden (zeer lang, maximum voor meeste modellen)
        /// </summary>
        public int? MaxTokens { get; set; } = 8000;

        /// <summary>
        /// Top P: Nucleus sampling - alternatief voor temperature (0.0-1.0)
        /// - 0.1: Alleen meest waarschijnlijke woorden
        /// - 1.0: Alle mogelijke woorden (default)
        /// Aanbeveling: Gebruik ÓF temperature ÓF top_p, niet beide
        /// </summary>
        public double? TopP { get; set; }

        /// <summary>
        /// Frequency Penalty: Verminder herhaling van woorden (-2.0 tot 2.0)
        /// - 0.0: Geen penalty (default)
        /// - 0.5-1.0: Minder herhaling
        /// - Negatief: Meer herhaling (zelden gebruikt)
        /// </summary>
        public double? FrequencyPenalty { get; set; }

        /// <summary>
        /// Presence Penalty: Stimuleer nieuwe onderwerpen (-2.0 tot 2.0)
        /// - 0.0: Geen penalty (default)
        /// - 0.5-1.0: Meer diverse onderwerpen
        /// - Negatief: Blijf bij hetzelfde onderwerp
        /// </summary>
        public double? PresencePenalty { get; set; }
    }

    /// <summary>
    /// Training pair: kept for backward compat (HotkeyConfig.TrainingPairs)
    /// </summary>
    [Obsolete("Style profiles now use TextExamples on WritingStyle")]
    public class TrainingPair
    {
        public string? Input { get; set; }
        public string? YourResponse { get; set; }
    }

    /// <summary>
    /// Feedback example: Captured when user corrects AI response
    /// Used for iterative learning
    /// </summary>
    public class FeedbackExample
    {
        public string OriginalInput { get; set; } = "";        // User's selected text
        public string AIResponse { get; set; } = "";           // What AI generated (incorrect)
        public string UserFeedback { get; set; } = "";         // What user said was wrong
        public string CorrectedResponse { get; set; } = "";    // Final correct response after iterations
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Usage example: Tracked execution for prompt optimization
    /// Volledige input/output (geen trimming) voor betere analyse
    /// </summary>
    public class UsageExample
    {
        public string Input { get; set; } = "";     // Volledige geselecteerde tekst
        public string Output { get; set; } = "";    // Volledige AI response
        public string Task { get; set; } = "";      // Hotkey custom prompt / taak
        public string Context { get; set; } = "";   // Extra context (prompt mode)
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DEPRECATED: Old feedback system, replaced by FeedbackExample
    /// </summary>
    [Obsolete("Use FeedbackExample instead")]
    public class StyleFeedbackItem
    {
        public string Input { get; set; } = "";
        public string Output { get; set; } = "";
        public string Problem { get; set; } = "";
    }

    /// <summary>
    /// Template variable field: Definitie van een invulveld dat bij het aanmaken van een template-hotkey wordt gevraagd.
    /// </summary>
    public class TemplateVariableField
    {
        public string Key { get; set; } = "";          // Intern sleutelwoord, bijv. "naam"
        public string Label { get; set; } = "";        // Label in dialoog, bijv. "Jouw naam"
        public string Placeholder { get; set; } = "";  // Hint in TextBox, bijv. "Jan de Vries"
    }

    /// <summary>
    /// Writing style: Herbruikbare stijl met voorbeeldmails of stijlvoorbeelden
    /// </summary>
    public class WritingStyle
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";           // "Formeel", "Informeel", "Engels"
        public string Description { get; set; } = "";     // Uitleg over de stijl (legacy, niet meer gebruikt in UI)

        /// <summary>
        /// Usage Context: Waarvoor wil de gebruiker deze stijl gebruiken?
        /// Bijvoorbeeld: "Reageren op klantmails", "LinkedIn posts schrijven", "Jira tickets maken"
        /// Dit helpt de AI om een relevanter StyleProfile te genereren.
        /// </summary>
        public string? UsageContext { get; set; }

        /// <summary>
        /// Style Profile: Algemene beschrijving van je schrijfstijl (toon, zinlengte, woordkeuze, etc.)
        /// Dit wordt toegevoegd aan de system prompt en werkt universeel voor alle situaties.
        /// </summary>
        public string? StyleProfile { get; set; }

        /// <summary>
        /// Text examples: Alleen voorbeeldteksten in een bepaalde stijl (geen input/output structuur)
        /// Gebruikt wanneer Type = "text-examples"
        /// </summary>
        public string[]? TextExamples { get; set; }

        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;

        // Metadata voor filtering/info (optioneel, legacy - niet meer gebruikt in UI)
        public StyleMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Style metadata: Extra informatie over een stijl
    /// </summary>
    public class StyleMetadata
    {
        public string? Formality { get; set; }   // "Formal", "Informal", "Neutral"
        public string? Language { get; set; }     // "NL", "EN", "DE"
    }
}
