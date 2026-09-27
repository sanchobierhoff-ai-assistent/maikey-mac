using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using mAIkey.Core.Interfaces;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Platform;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Globale sneltoetsen en de volledige mAIkey-actie — port van de hotkey-logica in
/// frontend/MainWindow.xaml.cs (OnHotkeyPressed, HandleIntegrationHotkey, RetryWithModel,
/// assistent-/screenshot-/koppelingen-sneltoets, abonnements-bevriezing).
/// </summary>
public class HotkeyRuntime
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;
    private readonly IHotkeyService _hotkeys;
    private readonly IClipboardService _clipboard;
    private readonly LoadingIndicatorService _loading;
    private readonly Dictionary<int, Func<Task>> _actions = new();
    private int _nextId = 1;
    private bool _busy;
    private bool _started;
    private bool _warningShownThisSession;

    private AssistantWindow? _assistantWindow;
    private BridgeHubWindow? _bridgeHubWindow;

    public const string DemoHotkeyId = "maikey-demo-spelling-grammar";

    public HotkeyRuntime(ConfigService config, ApiClient api, PlatformServices platform)
    {
        _config = config;
        _api = api;
        _hotkeys = platform.HotkeyService;
        _clipboard = platform.ClipboardService;
        _loading = new LoadingIndicatorService(config);
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        SetupDefaultHotkeys();
        _hotkeys.Initialize(IntPtr.Zero);
        _hotkeys.HotkeyPressed += (_, e) =>
        {
            if (_actions.TryGetValue(e.HotkeyId, out var action))
                Dispatcher.UIThread.Post(async () =>
                {
                    try { await action(); }
                    catch (Exception ex) { Logger.Log("Sneltoets-fout: " + ex.Message); }
                });
        };
        RegisterAll();

        // Sneltoetsen elke 5 minuten opnieuw registreren (na slaapstand e.d.), zoals Windows.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        timer.Tick += (_, _) => RegisterAll();
        timer.Start();
    }

    // ═══════════════════════════ REGISTRATIE ═══════════════════════════

    private void SetupDefaultHotkeys()
    {
        // Demo-mAIkey alleen bij de allereerste installatie (verwijderen is permanent).
        if (_config.Hotkeys.Length == 0 && !_config.HasShownDemoHotkey)
        {
            _config.HasShownDemoHotkey = true;
            _config.Hotkeys = new[]
            {
                new HotkeyConfig
                {
                    Id = DemoHotkeyId,
                    Name = L.CurrentLanguage switch { "en" => "Spelling & Grammar", "de" => "Rechtschreibung & Grammatik", _ => "Spelling & Grammatica" },
                    Description = L.CurrentLanguage switch
                    {
                        "en" => "Correct spelling and grammar of the selected text",
                        "de" => "Rechtschreibung und Grammatik des markierten Textes korrigieren",
                        _ => "Verbeter spelling en grammatica van de geselecteerde tekst"
                    },
                    CustomPrompt = L.CurrentLanguage switch
                    {
                        "en" => "A text that may contain spelling or grammar mistakes. Check and correct the spelling and grammar. Preserve the original writing style and meaning as much as possible. Return only the corrected text, without any explanation.",
                        "de" => "Ein Text, der möglicherweise Rechtschreib- oder Grammatikfehler enthält. Prüfe und korrigiere Rechtschreibung und Grammatik. Behalte den ursprünglichen Schreibstil und die Bedeutung so weit wie möglich bei. Gib nur den korrigierten Text zurück, ohne Erklärung.",
                        _ => "Een tekst die mogelijk spellings- of grammaticale fouten bevat. Controleer en verbeter de spelling en grammatica. Behoud de oorspronkelijke schrijfstijl en betekenis zo veel mogelijk. Geef alleen de verbeterde tekst terug, zonder uitleg."
                    },
                    Model = "gpt-4o-mini",
                    OutputMode = "replace",
                    ModifierKeys = 2,          // Ctrl (⌃)
                    Key = HotkeyKeys.D0 + 1,   // 1
                    Enabled = true,
                    IncludeImages = true,
                }
            };
        }
    }

    /// <summary>Registreer alle sneltoetsen opnieuw (na een wijziging).</summary>
    public void RegisterAll()
    {
        _hotkeys.UnregisterAll();
        _actions.Clear();
        _nextId = 1;

        var hotkeys = _config.Hotkeys;
        bool changed = false;
        foreach (var hk in hotkeys)
        {
            if (!hk.Enabled || hk.FrozenByDowngrade || hk.Key == 0) continue;
            var captured = hk;
            RegisterOrRecover(hk.Name, hk.ModifierKeys, hk.Key, () => OnHotkeyPressed(captured),
                (m, k) => { captured.ModifierKeys = m; captured.Key = k; changed = true; });
        }
        if (changed) _config.Hotkeys = hotkeys;

        if (_config.AssistantEnabled)
        {
            RegisterOrRecover(L.T("Assistant_Title"), _config.AssistantHotkeyModifiers, _config.AssistantHotkeyKey,
                OnAssistantHotkeyPressed, (m, k) => { _config.AssistantHotkeyModifiers = m; _config.AssistantHotkeyKey = k; });

            if (_config.ScreenshotHotkeyEnabled)
                RegisterOrRecover(L.T("AssistantSettings_ScreenshotHotkey"), _config.ScreenshotHotkeyModifiers, _config.ScreenshotHotkeyKey,
                    OnAssistantScreenshotHotkeyPressed, (m, k) => { _config.ScreenshotHotkeyModifiers = m; _config.ScreenshotHotkeyKey = k; });
        }

        if (_config.KoppelingHotkeyEnabled)
            RegisterOrRecover(L.T("Nav_Koppelingen"), _config.KoppelingHotkeyModifiers, _config.KoppelingHotkeyKey,
                OnKoppelingHotkeyPressed, (m, k) => { _config.KoppelingHotkeyModifiers = m; _config.KoppelingHotkeyKey = k; });
    }

    private bool TryRegister(int mods, int key, Func<Task> action)
    {
        int id = _nextId++;
        if (!_hotkeys.RegisterHotkey(id, (HotkeyModifiers)mods, key)) return false;
        _actions[id] = action;
        return true;
    }

    /// <summary>
    /// Registreer één sneltoets. Is de combinatie al bezet, kies dan automatisch een vrije,
    /// sla die op en meld dat één keer (zoals RegisterOrRecover op Windows).
    /// </summary>
    private void RegisterOrRecover(string displayName, int mods, int key, Func<Task> action, Action<int, int> persist)
    {
        if (TryRegister(mods, key, action)) return;

        foreach (var fm in new[] { 3, 6, 5, 7 })               // ⌃⌥, ⌃⇧, ⌥⇧, ⌃⌥⇧
            foreach (var fk in Enumerable.Range(HotkeyKeys.D0 + 1, 9).Concat(Enumerable.Range(HotkeyKeys.F1, 12)))
            {
                if (fm == mods && fk == key) continue;
                if (_config.Hotkeys.Any(h => h.ModifierKeys == fm && h.Key == fk)) continue;
                if (!TryRegister(fm, fk, action)) continue;

                persist(fm, fk);
                _ = MkDialog.ShowInfo("mAIkey", L.Tf("Info_HotkeyReassigned", displayName,
                    HotkeyKeys.Format(mods, key), HotkeyKeys.Format(fm, fk)));
                return;
            }

        Logger.Log($"Sneltoets '{displayName}' kon niet geregistreerd worden (geen vrije combinatie)");
    }

    // ═══════════════════════════ ASSISTENT / KOPPELINGEN ═══════════════════════════

    public Task OnKoppelingHotkeyPressed()
    {
        if (_bridgeHubWindow != null)
        {
            _bridgeHubWindow.Activate();
            return Task.CompletedTask;
        }
        _bridgeHubWindow = new BridgeHubWindow(_api, _config);
        _bridgeHubWindow.Closed += (_, _) =>
        {
            _bridgeHubWindow = null;
            _ = Ui.Main?.UpdateInboxBadgeAsync();
        };
        _bridgeHubWindow.Show();
        _bridgeHubWindow.Activate();
        return Task.CompletedTask;
    }

    public async Task OnAssistantHotkeyPressed()
    {
        if (_assistantWindow != null)
        {
            _assistantWindow.Activate();
            return;
        }

        // Best-effort: de huidige selectie als context meegeven (vereist Toegankelijkheid).
        string? context = null;
        if (MacAccessibility.IsTrusted())
        {
            try { context = await _clipboard.GetSelectedTextAsync(); } catch { }
        }

        OpenAssistant(string.IsNullOrWhiteSpace(context) ? null : context);
    }

    private AssistantWindow OpenAssistant(string? context)
    {
        _assistantWindow = new AssistantWindow(_api, _config, context);
        _assistantWindow.Closed += (_, _) => _assistantWindow = null;
        _assistantWindow.Show();
        _assistantWindow.Activate();
        return _assistantWindow;
    }

    public async Task OnAssistantScreenshotHotkeyPressed()
    {
        var shot = await ImageHelper.CaptureScreenRegionAsync();
        if (string.IsNullOrEmpty(shot)) return;
        var win = _assistantWindow ?? OpenAssistant(null);
        win.Activate();
        win.AddPendingImage(shot);
    }

    // ═══════════════════════════ TRANSFORM-SNELTOETS ═══════════════════════════

    private async Task OnHotkeyPressed(HotkeyConfig hotkey)
    {
        if (_busy) { Logger.Log("sneltoets genegeerd (bezig)"); return; }
        _busy = true;
        try { await RunHotkeyAsync(hotkey); }
        finally { _busy = false; }
    }

    private async Task RunHotkeyAsync(HotkeyConfig hotkey)
    {
        // App die vooraan stond onthouden — daar plakken we straks terug.
        IntPtr originalApp = _clipboard.GetForegroundWindow();

        string? retryText = null;
        string[]? retryImageUrls = null, retryPublicIds = null;
        OrderedContentBlock[]? retryOrdered = null;
        string retryPrompt = "", retryInstructions = "", retryOutputMode = "window";
        string? retryStyleProfile = null;
        TrainingExample[]? retryTraining = null;

        try
        {
            Logger.Log($"sneltoets '{hotkey.Name}' (modus {hotkey.OutputMode})");

            // 0. Limiet vooraf controleren (fail-open)
            try
            {
                var status = await _api.GetSubscriptionStatusAsync();
                if (status.Success && status.UsageExceeded)
                {
                    await ShowUsageLimitReached(status.DaysUntilReset);
                    return;
                }
            }
            catch { }

            var imageUrls = new List<string>();
            var publicIds = new List<string>();
            OrderedContentBlock[]? orderedContent = null;
            string text;

            if (hotkey.UseScreenCapture)
            {
                // 1a. Schermopname
                var shot = await ImageHelper.CaptureScreenRegionAsync();
                if (string.IsNullOrEmpty(shot)) return;

                _loading.Show(L.T("ScreenCapture_Processing"));
                var resp = await new ImageUploadService(_api).UploadImageAsync(shot);
                if (resp == null || !resp.Success)
                {
                    _loading.Hide();
                    await ShowError("Screenshot upload mislukt. Probeer opnieuw.");
                    return;
                }
                imageUrls.Add(resp.ImageUrl);
                publicIds.Add(resp.PublicId);
                text = "[Screenshot - no text]";
            }
            else if (hotkey.UseInputInsteadOfSelection)
            {
                // 1b. Handmatige invoer
                var input = await new InputPromptWindow(
                    customHeader: Loc.T("InputPrompt_ManualHeader", "INVOERVELD"),
                    customSubtitle: Loc.T("InputPrompt_ManualSubtitle", "Wat wil je verwerken?"),
                    customHint: Loc.T("InputPrompt_ManualHint", "Typ de tekst die je wilt verwerken. Druk Esc om te annuleren."),
                    showSkipButton: false).ShowAndWaitAsync();
                if (!input.Confirmed || string.IsNullOrWhiteSpace(input.UserPrompt)) return;
                text = input.UserPrompt;
                _loading.Show(L.T("Loading_Working"));
            }
            else
            {
                // 1c. Selectie kopiëren (Cmd+C) — vereist Toegankelijkheid
                if (!MacAccessibility.EnsureTrusted())
                {
                    await MacAccessibility.ExplainAsync();
                    return;
                }

                bool copied = await _clipboard.CopySelectionAsync();
                _loading.Show(L.T("Loading_FetchingContent"));

                RichClipboardContent rich;
                if (copied)
                {
                    var png = _clipboard.GetImagePng();
                    rich = RichClipboard.Build(_clipboard.GetText(), _clipboard.GetHtml(),
                        png != null ? ImageHelper.ToJpegDataUri(png) : null);
                }
                else rich = new RichClipboardContent();

                text = rich.Text ?? "";

                if (rich.Images.Count > 0 && hotkey.IncludeImages)
                {
                    bool overImages = rich.Images.Count > _config.MaxImages;
                    bool overChars = text.Length > _config.MaxCharacters;
                    if (overImages || overChars)
                    {
                        _loading.Hide();
                        if (!await ConfirmContentLimit(rich.Images.Count, text.Length)) return;
                        _loading.Show(L.T("Loading_FetchingContent"));
                    }

                    var uploader = new ImageUploadService(_api);
                    int total = rich.Images.Count, idx = 0;

                    async Task<string?> Upload(ClipboardImage img)
                    {
                        idx++;
                        _loading.Show($"{Loc.T("Loading_UploadingImages", "Uploaden naar cloud")} ({idx}/{total})...");
                        if (img.IsPublicUrl && !string.IsNullOrEmpty(img.PublicUrl)) return img.PublicUrl;
                        if (string.IsNullOrEmpty(img.Base64Data)) return null;
                        try
                        {
                            var r = await uploader.UploadImageAsync(img.Base64Data);
                            if (r != null && r.Success) { publicIds.Add(r.PublicId); return r.ImageUrl; }
                        }
                        catch (Exception ex) { Logger.Log("afbeelding uploaden mislukt: " + ex.Message); }
                        return null;
                    }

                    if (rich.HasInterleavedContent)
                    {
                        var blocks = new List<OrderedContentBlock>();
                        foreach (var block in rich.OrderedContent)
                        {
                            if (block.Type == "text") blocks.Add(new OrderedContentBlock { Type = "text", Text = block.Text });
                            else if (block.Image != null && await Upload(block.Image) is { } url)
                            {
                                imageUrls.Add(url);
                                blocks.Add(new OrderedContentBlock { Type = "image", ImageUrl = url });
                            }
                        }
                        orderedContent = blocks.ToArray();
                    }
                    else
                    {
                        foreach (var img in rich.Images)
                            if (await Upload(img) is { } url) imageUrls.Add(url);
                    }
                }

                if (string.IsNullOrWhiteSpace(text) && imageUrls.Count == 0)
                {
                    _loading.Hide();
                    await ShowError(rich.Images.Count > 0 && !hotkey.IncludeImages
                        ? L.T("Error_ImagesDisabled") : L.T("Error_NoContent"));
                    return;
                }
                if (string.IsNullOrWhiteSpace(text) && imageUrls.Count > 0)
                    text = "[Image only - no text]";
            }

            // 2. Extra context vragen (AskForContext of legacy "prompt"-modus)
            string userInstructions = "";
            bool askForContext = hotkey.AskForContext || hotkey.OutputMode == "prompt";
            if (askForContext)
            {
                _loading.Hide();
                var ctx = await new InputPromptWindow().ShowAndWaitAsync();
                if (!ctx.Confirmed) return;
                userInstructions = ctx.UserPrompt;

                var ctxImages = ctx.PendingImagesBase64;
                if (ctxImages.Count > 0)
                {
                    _loading.Show(L.T("InputPrompt_UploadingImages"));
                    var uploader = new ImageUploadService(_api);
                    foreach (var b64 in ctxImages)
                    {
                        var r = await uploader.UploadImageAsync(b64);
                        if (r != null && r.Success && !string.IsNullOrEmpty(r.ImageUrl))
                        {
                            imageUrls.Add(r.ImageUrl);
                            if (!string.IsNullOrEmpty(r.PublicId)) publicIds.Add(r.PublicId);
                        }
                    }
                    if (string.IsNullOrWhiteSpace(text) && imageUrls.Count > 0) text = "[Image only - no text]";
                }
            }

            // 3. Integratie-sneltoets (Jira, GitHub, …)
            if (!string.IsNullOrEmpty(hotkey.IntegrationType))
            {
                await HandleIntegrationHotkey(hotkey, text, userInstructions, imageUrls);
                return;
            }

            // 4. Gewone sneltoets
            string promptToUse = hotkey.PromptId ?? hotkey.CustomPrompt ?? "";
            string effectiveOutputMode = hotkey.OutputMode == "prompt" ? "window" : hotkey.OutputMode;

            _loading.Show(imageUrls.Count > 0
                ? (imageUrls.Count == 1 ? Loc.T("Loading_AnalyzingImage", "AI analyseert tekst en afbeelding...")
                                        : Loc.Tf("Loading_AnalyzingImages", "AI analyseert tekst en {0} afbeeldingen...", imageUrls.Count))
                : L.T("Loading_Working"));

            var trainingExamples = hotkey.TrainingExamples?.Select(te => new TrainingExample
            {
                Input = te.Input,
                ExpectedOutput = te.ExpectedOutput ?? ""
            }).ToArray();

            string? styleProfile = null;
            if (!string.IsNullOrEmpty(hotkey.StyleId))
                styleProfile = _config.GetStyleById(hotkey.StyleId)?.StyleProfile;

            // Template-variabelen (persoonlijke context) → stijlprofiel + {{placeholders}}
            if (hotkey.TemplateVariables is { Count: > 0 })
            {
                var profileContext = string.Join("\n", hotkey.TemplateVariables.Select(kv => $"{kv.Key}: {kv.Value}"));
                styleProfile = string.IsNullOrEmpty(styleProfile) ? profileContext : profileContext + "\n\n" + styleProfile;
                if (!string.IsNullOrEmpty(promptToUse))
                    foreach (var kv in hotkey.TemplateVariables)
                        promptToUse = promptToUse.Replace($"{{{{{kv.Key}}}}}", kv.Value ?? "");
            }

            retryText = text;
            retryImageUrls = imageUrls.Count > 0 ? imageUrls.ToArray() : null;
            retryPublicIds = publicIds.Count > 0 ? publicIds.ToArray() : null;
            retryOrdered = orderedContent;
            retryPrompt = promptToUse;
            retryInstructions = userInstructions;
            retryStyleProfile = styleProfile;
            retryOutputMode = effectiveOutputMode;
            retryTraining = trainingExamples;

            var result = await Analyze(hotkey, hotkey.Model ?? "gpt-4o-mini", text, retryImageUrls, retryPublicIds,
                orderedContent, promptToUse, userInstructions, styleProfile, effectiveOutputMode, trainingExamples);

            // Melding bij fallback-provider of Smart-model
            if (!string.IsNullOrEmpty(result.FallbackModel))
            {
                _loading.Show(L.Tf("Error_FallbackUsed_Body", FriendlyModelName(hotkey.Model ?? ""), FriendlyModelName(result.FallbackModel)));
                await Task.Delay(3000);
            }
            else if (hotkey.Model == "smart" && !string.IsNullOrEmpty(result.ModelUsed))
            {
                _loading.Show($"Smart: {FriendlyModelName(result.ModelUsed)}");
                await Task.Delay(2000);
            }
            _loading.Hide();

            if (_config.SoundOnComplete) Notifier.PlayCompleteSound();

            if (!result.Success || string.IsNullOrEmpty(result.Response))
            {
                Logger.Log("API-resultaat zonder antwoord: " + result.Error);
                await ShowError(L.T("Error_General_Body"));
                return;
            }

            // 5. Output
            if (effectiveOutputMode == "window" && askForContext)
            {
                var fb = new FeedbackWindow(text, result.Response, hotkey, _api, _config, userInstructions);
                await fb.ShowModalAsync(null);
                if (fb.WasCopied) Notifier.Show(L.T("Notif_Copied"));
            }
            else switch (effectiveOutputMode)
            {
                case "clipboard":
                    await _clipboard.SetTextAsync(result.Response);
                    Notifier.Show(L.T("Notif_Copied"));
                    break;
                case "window":
                    ShowResultWindow(result.Response);
                    break;
                default: // replace
                    _clipboard.SetForegroundWindow(originalApp);
                    await Task.Delay(150);
                    await _clipboard.ReplaceSelectedTextAsync(result.Response);
                    break;
            }

            await Task.Delay(500);
            _ = RefreshSubscriptionAsync();
        }
        catch (ApiException ex)
        {
            _loading.Hide();
            Logger.Log($"ApiException: {ex.ErrorType} {ex.Message}");

            if (ex.IsUpgradeError)
            {
                if (await MkDialog.ShowUpgrade(Loc.T("Dialog_LimitReached_Title", "Limiet bereikt"), ex.Message))
                    OpenPricingPage();
                return;
            }

            switch (ex.ErrorType)
            {
                case "NETWORK_ERROR":
                    await MkDialog.ShowError(L.T("Error_NoInternet_Title"), L.T("Error_NoInternet_Body"));
                    break;
                case "TIMEOUT_ERROR":
                    await MkDialog.ShowError(L.T("Error_Timeout_Title"), L.T("Error_Timeout_Body"));
                    break;
                case "MODEL_UNAVAILABLE":
                    if (retryText != null && await ModelPickerDialog.PickAsync(hotkey.Model ?? "") is { } picked)
                    {
                        await RetryWithModelAsync(picked, hotkey, retryText, retryImageUrls, retryPublicIds, retryOrdered,
                            retryPrompt, retryInstructions, retryStyleProfile, retryTraining);
                        return;
                    }
                    if (retryText == null)
                        await MkDialog.ShowError(L.T("Error_ModelUnavailable_Title"), L.T("Error_ModelUnavailable_Body"));
                    break;
                default:
                    bool retryable = ex.StatusCode is System.Net.HttpStatusCode.InternalServerError
                        or System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.ServiceUnavailable
                        or System.Net.HttpStatusCode.GatewayTimeout;
                    if (retryable && retryText != null && await ModelPickerDialog.PickAsync(hotkey.Model ?? "gpt-4o-mini") is { } m)
                    {
                        await RetryWithModelAsync(m, hotkey, retryText, retryImageUrls, retryPublicIds, retryOrdered,
                            retryPrompt, retryInstructions, retryStyleProfile, retryTraining);
                        return;
                    }
                    await ShowError(ex.Message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _loading.Hide();
            Logger.Log($"Sneltoets-fout: {ex.GetType().Name}: {ex.Message}");
            await ShowError(L.T("Error_General_Body"));
        }
    }

    private Task<AnalyzeResponse> Analyze(HotkeyConfig hotkey, string model, string text, string[]? imageUrls,
        string[]? publicIds, OrderedContentBlock[]? ordered, string prompt, string instructions,
        string? styleProfile, string outputMode, TrainingExample[]? training) =>
        _api.AnalyzeAsync(
            text: text,
            imageUrls: imageUrls,
            publicIds: publicIds,
            orderedContent: ordered,
            promptId: null,
            trainingExamples: training,
            model: model,
            customPrompt: prompt,
            stylePrompt: hotkey.StylePrompt,
            styleExamples: hotkey.StyleExamples,
            trainingPairs: null,
            feedbackHistory: null,
            consolidatedLessons: hotkey.ConsolidatedLessons,
            userInstructions: instructions,
            aiParameters: hotkey.CustomAIParameters,
            textExamples: null,
            styleProfile: styleProfile,
            outputMode: outputMode,
            prefixLanguage: hotkey.PrefixLanguage,
            includeAssistantContext: hotkey.IncludeAssistantContext);

    private async Task RetryWithModelAsync(string model, HotkeyConfig hotkey, string text, string[]? imageUrls,
        string[]? publicIds, OrderedContentBlock[]? ordered, string prompt, string instructions,
        string? styleProfile, TrainingExample[]? training)
    {
        _loading.Show(L.T("Loading_Retrying"));
        try
        {
            // Altijd het resultaatvenster bij een nieuwe poging (zoals Windows).
            var result = await Analyze(hotkey, model, text, imageUrls, publicIds, ordered, prompt, instructions, styleProfile, "window", training);
            _loading.Hide();
            if (result.Success && !string.IsNullOrEmpty(result.Response)) ShowResultWindow(result.Response);
            else await ShowError(L.T("Error_General_Body"));
        }
        catch (ApiException ex)
        {
            _loading.Hide();
            await ShowError(ex.ErrorType switch
            {
                "NETWORK_ERROR" => L.T("Error_NoInternet_Title") + "\n\n" + L.T("Error_NoInternet_Body"),
                "TIMEOUT_ERROR" => L.T("Error_Timeout_Title") + "\n\n" + L.T("Error_Timeout_Body"),
                "MODEL_UNAVAILABLE" => L.T("Error_ModelUnavailable_Title") + "\n\n" + L.T("Error_ModelUnavailable_Body"),
                _ => ex.IsUpgradeError ? ex.Message : L.T("Error_General_Body")
            });
        }
        catch
        {
            _loading.Hide();
            await ShowError(L.T("Error_General_Body"));
        }
    }

    private void ShowResultWindow(string result)
    {
        var win = new ResultWindow(result, _api);
        win.Show();
        win.Activate();
    }

    private async Task<bool> ConfirmContentLimit(int imageCount, int charCount)
    {
        bool overImages = imageCount > _config.MaxImages;
        bool overChars = charCount > _config.MaxCharacters;
        var body = $"{L.T("ContentLimit_Desc")}\n\n" +
                   $"{L.T("ContentLimit_Images")}: {imageCount} (limit: {_config.MaxImages})\n" +
                   $"{L.T("ContentLimit_Chars")}: {charCount:N0} (limit: {_config.MaxCharacters:N0})\n\n" +
                   (overImages && overChars ? "Many images and long text significantly increase token usage and costs."
                    : overImages ? "Multiple images significantly increase token usage."
                    : "Long text increases token usage.");
        return await MkDialog.ShowConfirm(L.T("ContentLimit_Subtitle"), body, null, L.T("ContentLimit_Continue"));
    }

    // ═══════════════════════════ INTEGRATIES ═══════════════════════════

    private async Task HandleIntegrationHotkey(HotkeyConfig hotkey, string selectedText, string userInstructions = "",
        List<string>? imageUrls = null, string? modelOverride = null)
    {
        string? extraContext = !string.IsNullOrWhiteSpace(userInstructions) ? userInstructions : null;
        var type = hotkey.IntegrationType!;

        string? WithContext(string? prompt) =>
            extraContext == null ? prompt
            : string.IsNullOrWhiteSpace(prompt) ? extraContext
            : $"{prompt}\n\nExtra context van gebruiker: {extraContext}";

        string? PickModel(Integration i) =>
            modelOverride ?? (!string.IsNullOrWhiteSpace(hotkey.Model) ? hotkey.Model : i.Config?.Model);

        bool review = hotkey.IntegrationAction?.ShowReviewWindow != false;

        try
        {
            var integrations = await _api.GetIntegrationsAsync();
            var integration = integrations?.FirstOrDefault(i => i.IntegrationType == type && i.IsActive);
            if (integration == null)
            {
                await ShowError(L.T(type switch
                {
                    "jira" => "Error_Integration_NotConfig_Jira",
                    "github" => "Error_Integration_NotConfig_GitHub",
                    "slack" => "Error_Integration_NotConfig_Slack",
                    "teams" => "Error_Integration_NotConfig_Teams",
                    "zapier" => "Error_Integration_NotConfig_Zapier",
                    "todoist" => "Error_Integration_NotConfig_Todoist",
                    "trello" => "Error_Integration_NotConfig_Trello",
                    "asana" => "Error_Integration_NotConfig_Asana",
                    "google_tasks" => "Error_Integration_NotConfig_GTasks",
                    "gmail" => "Error_Integration_NotConfig_Gmail",
                    "google_calendar" => "Error_Integration_NotConfig_Calendar",
                    _ => "Error_Integration_Unsupported"
                }));
                return;
            }

            // Zapier gebruikt alleen de prompt van de sneltoets (niet die van de integratie).
            var customPrompt = WithContext(!string.IsNullOrWhiteSpace(hotkey.CustomPrompt)
                ? hotkey.CustomPrompt
                : type == "zapier" ? null : integration.Config?.CustomPrompt);
            var model = PickModel(integration);
            string[] SplitLabels(string? s) => string.IsNullOrWhiteSpace(s) ? Array.Empty<string>()
                : s.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

            try
            {
                switch (type)
                {
                    case "jira":
                    {
                        _loading.Show(L.T("Loading_JiraTicket"));
                        var imgs = imageUrls?.Count > 0 ? imageUrls.ToArray() : null;
                        var draft = await _api.GenerateJiraTicketAsync(selectedText, customPrompt, model, imgs);
                        if (draft == null) { await DraftFailed(); return; }
                        var projects = await _api.GetJiraProjectsAsync();
                        if (projects == null || projects.Length == 0) { _loading.Hide(); await ShowError(L.T("Error_IntegrationFailed_Body")); return; }
                        _loading.Hide();

                        if (review)
                        {
                            if (draft.Multiple && draft.Tickets is { Length: > 1 })
                            {
                                var w = new JiraMultiTicketReviewWindow(_api, draft, projects, integration.Config?.DefaultProject, draft.ImageUrls ?? imgs);
                                await w.ShowModalAsync(null);
                                if (w.AnyCreated) Notifier.Show(L.T("JiraMulti_CreatedTray"));
                            }
                            else
                            {
                                var w = new JiraTicketReviewWindow(_api, draft, projects, customPrompt ?? "",
                                    integration.Config?.DefaultProject, integration.Config?.DefaultIssueType,
                                    hotkey.IntegrationAction?.DefaultAssignee, draft.ImageUrls ?? imgs);
                                await w.ShowModalAsync(null);
                                if (w.CreatedTicket != null) Notifier.Show(L.Tf("Tray_JiraCreated", w.CreatedTicket.Key));
                            }
                        }
                        else
                        {
                            var projectKey = integration.Config?.DefaultProject ?? projects.FirstOrDefault()?.Key;
                            var issueType = integration.Config?.DefaultIssueType ?? draft.IssueType;
                            if (projectKey != null)
                            {
                                var ticket = await _api.CreateJiraTicketAsync(projectKey, issueType, draft.Summary, draft.Description,
                                    draft.Priority, draft.Labels, imageUrls: draft.ImageUrls ?? imgs);
                                if (ticket != null)
                                {
                                    await _clipboard.SetTextAsync(ticket.Url);
                                    Notifier.Show(L.Tf("Tray_JiraCreatedUrl", ticket.Key));
                                }
                            }
                        }
                        break;
                    }
                    case "github":
                    {
                        _loading.Show(L.T("Loading_GitHubIssue"));
                        var draft = await _api.GenerateGitHubIssueAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        var labelsOverride = hotkey.IntegrationAction?.DefaultLabels ?? integration.Config?.DefaultLabels;
                        if (review)
                        {
                            var w = new GitHubIssueReviewWindow(_api, draft, hotkey.IntegrationAction?.DefaultRepo ?? integration.Config?.DefaultRepo, labelsOverride);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_GitHubCreated"));
                        }
                        else
                        {
                            var labels = !string.IsNullOrWhiteSpace(labelsOverride) ? SplitLabels(labelsOverride) : draft.Labels ?? Array.Empty<string>();
                            var issue = await _api.CreateGitHubIssueAsync(draft.Title, draft.Body, labels, null);
                            if (issue != null) Notifier.Show(L.Tf("Tray_GitHubCreatedNum", issue.Number));
                            else await ShowError(L.T("Error_Integration_SendFailed"));
                        }
                        break;
                    }
                    case "slack":
                    {
                        _loading.Show(L.T("Loading_SlackMessage"));
                        var draft = await _api.GenerateSlackMessageAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new SlackMessageReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_SlackSent"));
                        }
                        else if (await _api.SendSlackMessageAsync(draft.Message)) Notifier.Show(L.T("Tray_SlackSent"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "teams":
                    {
                        _loading.Show(L.T("Loading_TeamsMessage"));
                        var draft = await _api.GenerateTeamsMessageAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new TeamsMessageReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_TeamsSent"));
                        }
                        else if (await _api.SendTeamsMessageAsync(draft.Message)) Notifier.Show(L.T("Tray_TeamsSent"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "zapier":
                    {
                        _loading.Show(L.T("Loading_ZapierPayload"));
                        var draft = await _api.GenerateZapierPayloadAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new ZapierPayloadReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_ZapierSent"));
                        }
                        else if (await _api.SendZapierDataAsync(System.Text.Json.JsonSerializer.Serialize(draft))) Notifier.Show(L.T("Tray_ZapierSent"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "todoist":
                    {
                        _loading.Show(L.T("Loading_TodoistTask"));
                        var draft = await _api.GenerateTodoistTaskAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        var labelsOverride = hotkey.IntegrationAction?.DefaultLabels ?? integration.Config?.DefaultLabels;
                        if (review)
                        {
                            var w = new TodoistTaskReviewWindow(_api, draft, labelsOverride);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_TodoistCreated"));
                        }
                        else
                        {
                            var labels = !string.IsNullOrWhiteSpace(labelsOverride) ? SplitLabels(labelsOverride) : draft.Labels ?? Array.Empty<string>();
                            var task = await _api.CreateTodoistTaskAsync(draft.Content, draft.Description, draft.DueString, draft.Priority, labels);
                            if (task != null) Notifier.Show(L.T("Tray_TodoistCreated"));
                            else await ShowError(L.T("Error_Integration_SendFailed"));
                        }
                        break;
                    }
                    case "trello":
                    {
                        _loading.Show(L.T("Loading_TrelloCard"));
                        var draft = await _api.GenerateTrelloCardAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new TrelloCardReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_TrelloCreated"));
                        }
                        else if (await _api.CreateTrelloCardAsync(draft.Name, draft.Desc, string.IsNullOrWhiteSpace(draft.Due) ? null : draft.Due) != null)
                            Notifier.Show(L.T("Tray_TrelloCreated"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "asana":
                    {
                        _loading.Show(L.T("Loading_AsanaTask"));
                        var draft = await _api.GenerateAsanaTaskAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new AsanaTaskReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_AsanaCreated"));
                        }
                        else if (await _api.CreateAsanaTaskAsync(draft.Name, draft.Notes, string.IsNullOrWhiteSpace(draft.DueOn) ? null : draft.DueOn) != null)
                            Notifier.Show(L.T("Tray_AsanaCreated"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "google_tasks":
                    {
                        _loading.Show(L.T("Loading_GTask"));
                        var draft = await _api.GenerateGoogleTaskAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new GoogleTaskReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_GTaskCreated"));
                        }
                        else if (await _api.CreateGoogleTaskAsync(draft.Title, draft.Notes, string.IsNullOrWhiteSpace(draft.DueOn) ? null : draft.DueOn) != null)
                            Notifier.Show(L.T("Tray_GTaskCreated"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "gmail":
                    {
                        _loading.Show(L.T("Loading_Email"));
                        var draft = await _api.GenerateGmailDraftAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        if (!string.IsNullOrEmpty(hotkey.IntegrationAction?.DefaultTo)) draft.To = hotkey.IntegrationAction!.DefaultTo!;
                        if (!string.IsNullOrEmpty(hotkey.IntegrationAction?.DefaultSubject)) draft.Subject = hotkey.IntegrationAction!.DefaultSubject!;
                        if (!string.IsNullOrEmpty(hotkey.IntegrationAction?.DefaultCc)) draft.Cc = hotkey.IntegrationAction!.DefaultCc!;
                        _loading.Hide();
                        if (review)
                        {
                            var w = new GmailReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_EmailSentOrDraft"));
                        }
                        else if (await _api.SendGmailAsync(draft.To, draft.Subject, draft.Body, draft.Cc)) Notifier.Show(L.T("Tray_EmailSent"));
                        else await ShowError(L.T("Error_Integration_SendFailed"));
                        break;
                    }
                    case "google_calendar":
                    {
                        _loading.Show(L.T("Loading_Calendar"));
                        var draft = await _api.GenerateCalendarEventAsync(selectedText, customPrompt, model);
                        if (draft == null) { await DraftFailed(); return; }
                        _loading.Hide();
                        if (review)
                        {
                            var w = new CalendarEventReviewWindow(_api, draft);
                            await w.ShowModalAsync(null);
                            if (w.Confirmed) Notifier.Show(L.T("Tray_CalendarCreated"));
                        }
                        else
                        {
                            var calendarId = hotkey.IntegrationAction?.DefaultCalendarId ?? integration.Config?.DefaultCalendarId;
                            var ev = await _api.CreateCalendarEventAsync(draft.Title, draft.StartDateTime, draft.EndDateTime,
                                draft.Description, draft.Attendees, draft.Location, calendarId);
                            if (ev != null) Notifier.Show(L.T("Tray_CalendarCreated"));
                            else await ShowError(L.T("Error_Integration_SendFailed"));
                        }
                        break;
                    }
                    default:
                        await ShowError(L.T("Error_Integration_Unsupported"));
                        break;
                }
            }
            finally { _loading.Hide(); }
        }
        catch (ApiException ex)
        {
            _loading.Hide();
            bool modelFailed = ex.ErrorType == "MODEL_UNAVAILABLE" || (ex.Details?.Contains("could not generate a valid") ?? false);
            if (modelFailed)
            {
                var current = modelOverride ?? (!string.IsNullOrWhiteSpace(hotkey.Model) ? hotkey.Model! : "gpt-4o-mini");
                var picked = await ModelPickerDialog.PickAsync(current);
                if (!string.IsNullOrWhiteSpace(picked))
                    await HandleIntegrationHotkey(hotkey, selectedText, userInstructions, imageUrls, picked);
                return;
            }
            await ShowError(ex.ErrorType switch
            {
                "NETWORK_ERROR" => L.T("Error_NoInternet_Title") + "\n\n" + L.T("Error_NoInternet_Body"),
                _ => ex.IsUpgradeError ? ex.Message : L.T("Error_IntegrationFailed_Body")
            });
        }
        catch (Exception ex)
        {
            _loading.Hide();
            Logger.Log("Integratie-fout: " + ex.Message);
            await ShowError(L.T("Error_IntegrationFailed_Body"));
        }
    }

    private Task DraftFailed()
    {
        _loading.Hide();
        return ShowError(L.T("Error_Integration_DraftFailed"));
    }

    // ═══════════════════════════ ABONNEMENT ═══════════════════════════

    /// <summary>
    /// Abonnementsstatus ophalen: sneltoetsen boven het limiet bevriezen/ontdooien en
    /// eenmalig per sessie waarschuwen bij 80% verbruik (zoals RefreshCreditsAsync op Windows).
    /// </summary>
    public async Task RefreshSubscriptionAsync()
    {
        try
        {
            var status = await _api.GetSubscriptionStatusAsync();
            if (!status.Success) return;
            ApplySubscriptionFreeze(status);
            if (status.UsageWarning && !status.UsageExceeded && !_warningShownThisSession)
            {
                _warningShownThisSession = true;
                await ShowUsageWarning(status.DaysUntilReset);
            }
        }
        catch (Exception ex) { Logger.Log("abonnementsstatus mislukt: " + ex.Message); }
    }

    private void ApplySubscriptionFreeze(SubscriptionStatusResponse status)
    {
        var hotkeys = _config.Hotkeys;
        if (hotkeys.Length == 0) return;
        bool changed = false;

        if (status.MaxHotkeys.HasValue)
        {
            int limit = status.MaxHotkeys.Value;
            var active = hotkeys.Where(h => !h.FrozenByDowngrade).ToList();
            for (int i = limit; i < active.Count; i++) { active[i].FrozenByDowngrade = true; changed = true; }

            var frozen = hotkeys.Where(h => h.FrozenByDowngrade).ToList();
            int room = limit - Math.Min(limit, active.Count);
            for (int i = 0; i < Math.Min(room, frozen.Count); i++) { frozen[i].FrozenByDowngrade = false; changed = true; }
        }
        else
        {
            foreach (var h in hotkeys.Where(h => h.FrozenByDowngrade)) { h.FrozenByDowngrade = false; changed = true; }
        }

        if (changed)
        {
            _config.SaveConfig();
            RegisterAll();
        }
    }

    private async Task ShowUsageWarning(int days)
    {
        var dag = days != 1 ? "dagen" : "dag";
        var msg = $"Je hebt 80% van je maandlimiet gebruikt.\n\nJe limiet wordt gereset over {days} {dag}.\n\nOverweeg een upgrade als je meer mAIkeys per maand nodig hebt.";
        if (await MkDialog.ShowUpgrade(Loc.T("Dialog_AlmostLimit_Title", "Bijna op limiet"), msg)) OpenPricingPage();
    }

    private async Task ShowUsageLimitReached(int days)
    {
        var dag = days != 1 ? "dagen" : "dag";
        var msg = $"Je hebt je maandlimiet bereikt.\n\nJe kunt over {days} {dag} weer mAIkeys gebruiken.\n\nOf upgrade naar een hoger abonnement voor meer mAIkeys.";
        if (await MkDialog.ShowUpgrade(Loc.T("Dialog_LimitReached_Title", "Limiet bereikt"), msg)) OpenPricingPage();
    }

    public void OpenPricingPage()
    {
        const string url = "https://maikey.nl/";
        var token = _config.AuthToken;
        Ui.OpenUrl(!string.IsNullOrWhiteSpace(token)
            ? url + "?autotoken=" + Uri.EscapeDataString(token) + "#prijzen"
            : url + "#prijzen");
    }

    // ═══════════════════════════ HULP ═══════════════════════════

    private static Task ShowError(string message) =>
        MkDialog.ShowError($"mAIkey — {L.T("Common_Error")}", message);

    private static string FriendlyModelName(string id) => id switch
    {
        "gpt-4o-mini" => "GPT-4o Mini",
        "gpt-4o" => "GPT-4o",
        "claude-haiku-4-5" => "Claude Haiku",
        "claude-sonnet-4-6" => "Claude Sonnet",
        "llama-3.3-70b-versatile" => "Llama 3.3 70B",
        "llama-3.1-8b-instant" => "Llama 3.1 8B",
        _ => ModelCatalogCache.Models.FirstOrDefault(m => m.Id == id)?.Name ?? id
    };
}
