using System.Text.Json;
using mAIkey.Core.Interfaces;
using mAIkey.Core.Models;

namespace mAIkey.Core.Services;

public class ConfigService
{
    public const string CURRENT_VERSION = "Beta-v1.4-mac";

    private static readonly string ConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mAIkey");

    private static readonly string ConfigFile = Path.Combine(ConfigDirectory, "config.json");

    private AppConfig _config;
    private readonly ITokenProtection? _tokenProtection;

    public ConfigService(ITokenProtection? tokenProtection = null)
    {
        _tokenProtection = tokenProtection;

        if (!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory);

        _config = LoadConfig();
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

                // Decrypt tokens
                if (_tokenProtection != null)
                {
                    config.AuthToken = _tokenProtection.Decrypt(config.AuthToken);
                    config.RefreshToken = _tokenProtection.Decrypt(config.RefreshToken);
                }

                return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config: {ex.Message}");
        }

        return new AppConfig();
    }

    public void SaveConfig()
    {
        var plainAuth = _config.AuthToken;
        var plainRefresh = _config.RefreshToken;
        try
        {
            if (_tokenProtection != null)
            {
                _config.AuthToken = _tokenProtection.Encrypt(plainAuth);
                _config.RefreshToken = _tokenProtection.Encrypt(plainRefresh);
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
        finally
        {
            _config.AuthToken = plainAuth;
            _config.RefreshToken = plainRefresh;
        }
    }

    // ============================================
    // AUTHENTICATION
    // ============================================

    public string? AuthToken
    {
        get => _config.AuthToken;
        set { _config.AuthToken = value; SaveConfig(); }
    }

    public string? RefreshToken
    {
        get => _config.RefreshToken;
        set { _config.RefreshToken = value; SaveConfig(); }
    }

    public string? UserEmail
    {
        get => _config.UserEmail;
        set { _config.UserEmail = value; SaveConfig(); }
    }

    public string? UserName
    {
        get => _config.UserName;
        set { _config.UserName = value; SaveConfig(); }
    }

    public string? UserId
    {
        get => _config.UserId;
        set { _config.UserId = value; SaveConfig(); }
    }

    public string? SubscriptionTier
    {
        get => _config.SubscriptionTier;
        set { _config.SubscriptionTier = value; SaveConfig(); }
    }

    // ============================================
    // HOTKEYS
    // ============================================

    public HotkeyConfig[] Hotkeys
    {
        get => _config.Hotkeys ?? Array.Empty<HotkeyConfig>();
        set { _config.Hotkeys = value; SaveConfig(); }
    }

    // ============================================
    // WRITING STYLES
    // ============================================

    public WritingStyle[] WritingStyles
    {
        get => _config.WritingStyles ?? Array.Empty<WritingStyle>();
        set { _config.WritingStyles = value; SaveConfig(); }
    }

    public WritingStyle? GetStyleById(string styleId) =>
        _config.WritingStyles?.FirstOrDefault(s => s.Id == styleId);

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
        _config.WritingStyles = _config.WritingStyles.Where(s => s.Id != styleId).ToArray();
        SaveConfig();
    }

    // ============================================
    // SETTINGS
    // ============================================

    public bool MinimizeToTray
    {
        get => _config.MinimizeToTray;
        set { _config.MinimizeToTray = value; SaveConfig(); }
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

    public string InterfaceLanguage
    {
        get => _config.InterfaceLanguage;
        set { _config.InterfaceLanguage = value; SaveConfig(); }
    }

    public string Theme
    {
        get => _config.Theme ?? "Dark";
        set { _config.Theme = value; SaveConfig(); }
    }

    public int MaxImages
    {
        get => _config.MaxImages;
        set { _config.MaxImages = value; SaveConfig(); }
    }

    public int MaxCharacters
    {
        get => _config.MaxCharacters;
        set { _config.MaxCharacters = value; SaveConfig(); }
    }

    public string ApiBaseUrl
    {
        get => _config.ApiBaseUrl ?? "https://ai-assistent-backend-production.up.railway.app";
        set { _config.ApiBaseUrl = value; SaveConfig(); }
    }

    public bool HasShownDemoHotkey
    {
        get => _config.HasShownDemoHotkey;
        set { _config.HasShownDemoHotkey = value; SaveConfig(); }
    }

    public void ClearAuth()
    {
        _config.AuthToken = null;
        _config.RefreshToken = null;
        _config.UserEmail = null;
        _config.UserId = null;
        SaveConfig();
    }

    // ============================================
    // ONBOARDING
    // ============================================

    public bool IsOnboardingDone()
    {
        return _config.OnboardingCompletedAt.HasValue ||
               new[] { "hotkey", "style", "template", "pin" }
                   .All(s => _config.OnboardingCompletedIds.Contains(s));
    }

    public IReadOnlyList<string> GetOnboardingCompletedIds() => _config.OnboardingCompletedIds;

    public void SetOnboardingStepDone(string stepId)
    {
        if (!_config.OnboardingCompletedIds.Contains(stepId))
        {
            _config.OnboardingCompletedIds.Add(stepId);
            SaveConfig();
        }
    }
}
