using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using mAIkey.Desktop.Controls;
using mAIkey.Desktop.Windows;
using mAIkey.Desktop.Services;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Command bar van de Assistant-modus: conversationele, geheugen-gedreven,
/// tool-gebruikende assistent. Bereikbaar via een eigen globale sneltoets,
/// naast de gewone transform-sneltoetsen (die onaangeroerd blijven).
/// </summary>
public partial class AssistantWindow : Window
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;
    private readonly string? _model;   // null = backend-standaard (goedkoop, tool-capabel)

    private string? _sessionId;
    private string? _context;          // geselecteerde tekst als optionele context
    private bool _contextActive;
    private bool _busy;

    // Bijlagen (afbeeldingen) die bij het volgende bericht worden meegestuurd.
    private readonly System.Collections.ObjectModel.ObservableCollection<PendingImage> _attachments = new();

    // Bijlagen (documenten): geëxtraheerde tekst reist mee als context.
    private readonly System.Collections.ObjectModel.ObservableCollection<PendingDocument> _documents = new();

    private sealed class PendingImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Base64 { get; set; } = "";   // data:image/...;base64,....
        public Bitmap? Thumb { get; set; }
    }

    private sealed class PendingDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public bool Truncated { get; set; }
    }

    public AssistantWindow() : this(App.Api, App.Config) { }

    public AssistantWindow(ApiClient api, ConfigService config, string? initialContext = null, string? model = null)
    {
        InitializeComponent();
        _api = api;
        _config = config;
        // Expliciet meegegeven model wint; anders de keuze uit Instellingen (null = backend-standaard).
        _model = string.IsNullOrWhiteSpace(model) ? config.AssistantModel : model;

        ApplyLocalization();
        LoadMascot();
        AttachmentsPanel.ItemsSource = _attachments;
        DocAttachmentsPanel.ItemsSource = _documents;

        if (!string.IsNullOrWhiteSpace(initialContext))
        {
            _context = initialContext!.Trim();
            _contextActive = true;
            ContextChip.IsVisible = true;
            var preview = _context.Length > 120 ? _context.Substring(0, 120) + "…" : _context;
            ContextChipText.Text = L.Tf("Assistant_ContextLabel", preview.Replace("\r", " ").Replace("\n", " "));
        }

        Opened += (s, e) =>
        {
            Activate();
            Dispatcher.UIThread.Post(() => InputBox.Focus(), DispatcherPriority.Input);
            ShowWelcome();
        };

        // Afbeelding plakken (Cmd+V) → bijlage; tekst plakken gaat gewoon door.
        ImageInput.EnableImagePaste(InputBox, AddPendingImage);

        // Slepen & neerzetten van afbeeldingen en documenten.
        DragDrop.SetAllowDrop(AssistantShell, true);
        AssistantShell.AddHandler(DragDrop.DragOverEvent, Shell_DragOver);
        AssistantShell.AddHandler(DragDrop.DropEvent, Shell_Drop);

        // Esc sluit ook als de focus niet in het invoerveld staat.
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; if (OverlayPanel.IsVisible) HideOverlay(); else Close(); } };

        InputBox.AddHandler(KeyDownEvent, (s, e) =>
        {
            if (e.Key == Key.Enter
                && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
                && !e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Enter verstuurt; Shift+Enter (en Ctrl+Enter) laten een nieuwe regel toe.
                e.Handled = true;
                SendMessage();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }, RoutingStrategies.Tunnel);
    }

    // ============================================
    // BIJLAGEN (afbeeldingen)
    // ============================================

    /// <summary>Voegt een afbeelding (base64 data-URI) toe als bijlage. Extern aanroepbaar (screenshot).</summary>
    public void AddPendingImage(string base64DataUri)
    {
        if (string.IsNullOrWhiteSpace(base64DataUri)) return;
        _attachments.Add(new PendingImage { Base64 = base64DataUri, Thumb = ImageHelper.ThumbFromDataUri(base64DataUri) });
        AttachmentsPanel.IsVisible = true;
        InputBox.Focus();
    }

    private async void Attach_Click(object? sender, RoutedEventArgs e)
    {
        var patterns = ImageHelper.ImageExtensions
            .Concat(new[] { "pdf", "docx", "txt", "md", "csv", "json", "log", "eml", "msg" })
            .Select(x => "*." + x).ToArray();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Afbeeldingen en documenten") { Patterns = patterns } }
        });
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (path != null) RouteFile(path);
        }
    }

    /// <summary>Bepaalt of een gesleept/gekozen bestand een afbeelding of een document is en voegt het toe.</summary>
    private void RouteFile(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (ImageHelper.ImageExtensions.Contains(ext)) AddImageFromFile(path);
        else if (DocumentTextExtractor.IsSupported(path)) AddPendingDocument(path);
        // andere types negeren we stil
    }

    private void AddImageFromFile(string path)
    {
        try
        {
            var uri = ImageHelper.FileToDataUri(path);
            if (uri != null) AddPendingImage(uri);
        }
        catch { /* sla een onleesbaar bestand over */ }
    }

    private async void AddPendingDocument(string path)
    {
        var res = DocumentTextExtractor.Extract(path);
        if (!res.Ok)
        {
            var msg = res.Error == "unsupported"
                ? L.T("Assistant_DocumentUnsupported")
                : L.T("Assistant_DocumentReadFailed");
            await MkDialog.ShowInfo(L.T("Assistant_Title"), msg, this);
            return;
        }
        _documents.Add(new PendingDocument { Name = res.FileName, Text = res.Text, Truncated = res.Truncated });
        DocAttachmentsPanel.IsVisible = true;
        if (res.Truncated) SetStatus(L.T("Assistant_DocumentTruncated"));
        InputBox.Focus();
    }

    private void RemoveAttachment_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var item = _attachments.FirstOrDefault(a => a.Id == id);
            if (item != null) _attachments.Remove(item);
            if (_attachments.Count == 0) AttachmentsPanel.IsVisible = false;
        }
    }

    private void RemoveDocument_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var item = _documents.FirstOrDefault(d => d.Id == id);
            if (item != null) _documents.Remove(item);
            if (_documents.Count == 0) DocAttachmentsPanel.IsVisible = false;
        }
    }

    // ============================================
    // SLEPEN & NEERZETTEN (afbeeldingen + documenten)
    // ============================================

    private void Shell_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Shell_Drop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files == null) return;
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (path != null) RouteFile(path);
        }
    }

    /// <summary>Laadt de mascotte (transparant, uitgesneden) in de header.</summary>
    private void LoadMascot()
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://mAIkey.Desktop/Resources/maikey-loading-mark.png"));
            MascotImage.Source = new Bitmap(stream);
        }
        catch { /* ook geen logo → laat leeg */ }
    }

    private void ApplyLocalization()
    {
        TitleLabel.Text = L.T("Assistant_Title");
        SendBtn.Content = L.T("Assistant_Ask");
        HintLabel.Text = L.T("Assistant_InputHint");
        ToolTip.SetTip(AttachBtn, L.T("Assistant_Attach"));
        ToolTip.SetTip(NewChatBtn, L.T("Assistant_NewChat"));
        ToolTip.SetTip(HistoryBtn, L.T("Assistant_HistoryTitle"));
        ToolTip.SetTip(MemoryBtn, L.T("Assistant_MemoryTitle"));
        HintLabel.Text = HintLabel.Text?.Replace("Ctrl", "Cmd");
    }

    private void ShowWelcome()
    {
        AddAssistantBubble(L.T("Assistant_Welcome"));
    }

    // ============================================
    // BERICHTEN VERSTUREN
    // ============================================

    private void Send_Click(object? sender, RoutedEventArgs e) => SendMessage();

    private async void SendMessage()
    {
        if (_busy) return;
        var text = InputBox.Text?.Trim() ?? "";
        bool hasAttachments = _attachments.Count > 0;
        bool hasDocuments = _documents.Count > 0;
        if (string.IsNullOrEmpty(text) && !hasAttachments && !hasDocuments) return;

        _busy = true;
        SendBtn.IsEnabled = false;
        InputBox.Text = "";

        // Bouw de context: geselecteerde tekst + geëxtraheerde documenttekst.
        var context = BuildContextForSend();

        if (!string.IsNullOrEmpty(text)) AddUserBubble(text);
        else if (hasDocuments) AddUserBubble(L.T("Assistant_DocumentOnly"));
        else AddUserBubble(L.T("Assistant_ImageOnly"));
        if (hasAttachments) AddUserThumbnails();
        if (hasDocuments) AddUserDocChips();

        // Context (selectie + documenten) wordt eenmalig meegestuurd, daarna weg
        _contextActive = false;
        ContextChip.IsVisible = false;
        _documents.Clear();
        DocAttachmentsPanel.IsVisible = false;

        SetStatus(L.T("Assistant_Thinking"));

        string[]? imageUrls = null;
        string[]? publicIds = null;
        try
        {
            // Bijlagen uploaden (indien aanwezig)
            if (hasAttachments)
            {
                SetStatus(L.T("Assistant_Uploading"));
                var svc = new ImageUploadService(_api);
                var urls = new System.Collections.Generic.List<string>();
                var pids = new System.Collections.Generic.List<string>();
                foreach (var a in System.Linq.Enumerable.ToList(_attachments))
                {
                    var resp = await svc.UploadImageAsync(a.Base64);
                    if (resp != null && resp.Success) { urls.Add(resp.ImageUrl); pids.Add(resp.PublicId); }
                }
                imageUrls = urls.ToArray();
                publicIds = pids.ToArray();
                _attachments.Clear();
                AttachmentsPanel.IsVisible = false;
            }

            await _api.SendAgentStreamAsync(
                text, _sessionId, _model, context,
                onStatus: stage => Dispatcher.UIThread.Post(() => SetStatusStage(stage)),
                onResult: res => Dispatcher.UIThread.Post(() => HandleAgentResult(res)),
                onError: err => Dispatcher.UIThread.Post(() => ShowError(err)),
                imageUrls: imageUrls, publicIds: publicIds);
        }
        catch (Exception)
        {
            // Streaming faalde → val terug op de niet-streamende variant
            try
            {
                var res = await _api.SendAgentAsync(text, _sessionId, _model, context, imageUrls, publicIds);
                HandleAgentResult(res);
            }
            catch (ApiException ex) { ShowError(ex.Message); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
        finally
        {
            _busy = false;
            SendBtn.IsEnabled = true;
            SetStatus("");
            InputBox.Focus();
        }
    }

    /// <summary>Toont de meegestuurde afbeeldingen als kleine thumbnails in de gebruikersbubbel.</summary>
    private void AddUserThumbnails()
    {
        var wrap = new WrapPanel { Margin = new Thickness(60, 0, 8, 6), HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var a in _attachments)
        {
            if (a.Thumb == null) continue;
            wrap.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Margin = new Thickness(6, 0, 0, 0),
                ClipToBounds = true, Background = Ui.Brush("Bg2"),
                Child = new Image { Source = a.Thumb, Stretch = Stretch.UniformToFill }
            });
        }
        if (wrap.Children.Count > 0) { MessagesPanel.Children.Add(wrap); ScrollToBottom(); }
    }

    /// <summary>Combineert de geselecteerde tekst en de geëxtraheerde documenttekst tot één context-string.</summary>
    private string? BuildContextForSend()
    {
        var sb = new StringBuilder();
        if (_contextActive && !string.IsNullOrWhiteSpace(_context)) sb.Append(_context);
        foreach (var d in _documents)
        {
            if (string.IsNullOrWhiteSpace(d.Text)) continue;
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append($"[Document: {d.Name}]\n{d.Text}");
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>Toont de meegestuurde documenten als kleine chips in de gebruikersbubbel.</summary>
    private void AddUserDocChips()
    {
        var wrap = new WrapPanel { Margin = new Thickness(60, 0, 8, 6), HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var d in _documents)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 5, 8, 5),
                Background = Ui.Brush("Bg2"),
                BorderBrush = Ui.Brush("Border1"),
                BorderThickness = new Thickness(1)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = "\U0001F4C4", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = d.Name, FontSize = 12, MaxWidth = 200, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Ui.Brush("Text2"), VerticalAlignment = VerticalAlignment.Center });
            chip.Child = sp;
            wrap.Children.Add(chip);
        }
        if (wrap.Children.Count > 0) { MessagesPanel.Children.Add(wrap); ScrollToBottom(); }
    }

    private void HandleAgentResult(AgentResponse res)
    {
        if (!string.IsNullOrEmpty(res.SessionId)) _sessionId = res.SessionId;

        if (res.Type == "confirm" && res.Action != null)
        {
            if (!string.IsNullOrWhiteSpace(res.AssistantText)) AddAssistantBubble(res.AssistantText!);
            ShowConfirmCard(res.Action);
        }
        else
        {
            AddAssistantBubble(res.Response ?? "");
        }

        if (res.Remembered) AddRememberedChip();
    }

    /// <summary>Subtiel signaal dat de assistent iets in het geheugen heeft opgeslagen.</summary>
    private void AddRememberedChip()
    {
        MessagesPanel.Children.Add(new TextBlock
        {
            Text = "\U0001F9E0 " + L.T("Assistant_Remembered"),
            FontSize = 10.5,
            Foreground = Ui.Brush("Text3"),
            Margin = new Thickness(12, 0, 60, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        ScrollToBottom();
    }

    // ============================================
    // BEVESTIGINGSKAART (human-in-the-loop)
    // ============================================

    private void ShowConfirmCard(AgentAction action)
    {
        var card = new Border
        {
            Background = Ui.Brush("Bg2"),
            BorderBrush = Ui.Brush("Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(40, 6, 8, 10)
        };

        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text = action.ActionLabel ?? action.ToolName,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = Ui.Brush("Text1"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(new TextBlock
        {
            Text = FormatArgs(action.Args),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.Brush("Text2"),
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Jira heeft project + issue-type nodig die het model niet kan weten.
        TextBox? jiraProject = null;
        TextBox? jiraIssueType = null;
        if (action.IntegrationType == "jira")
        {
            jiraProject = AddLabeledField(stack, L.T("Assistant_JiraProjectKey"));
            jiraIssueType = AddLabeledField(stack, L.T("Assistant_JiraIssueType"));
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var cancelBtn = new Button
        {
            Content = L.T("Assistant_Cancel"),
            Height = 34,
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Classes = { "GhostButton" }
        };
        var execBtn = new Button
        {
            Content = L.T("Assistant_Execute"),
            Height = 34,
            Padding = new Thickness(20, 0, 20, 0),
            Classes = { "AccentButton" }
        };

        cancelBtn.Click += async (s, e) =>
        {
            buttons.IsEnabled = false;
            await CancelActionAsync(action, card);
        };
        execBtn.Click += async (s, e) =>
        {
            if (action.IntegrationType == "jira" &&
                (string.IsNullOrWhiteSpace(jiraProject?.Text) || string.IsNullOrWhiteSpace(jiraIssueType?.Text)))
            {
                await MkDialog.ShowInfo(L.T("Assistant_Title"), L.T("Assistant_JiraFieldsRequired"), this);
                return;
            }
            buttons.IsEnabled = false;
            await ExecuteActionAsync(action, card, jiraProject?.Text, jiraIssueType?.Text);
        };

        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(execBtn);
        stack.Children.Add(buttons);

        card.Child = stack;
        MessagesPanel.Children.Add(card);
        ScrollToBottom();
    }

    private TextBox AddLabeledField(StackPanel parent, string label)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = Ui.Brush("Text3"),
            Margin = new Thickness(0, 2, 0, 2)
        });
        var tb = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 8),
            Background = Ui.Brush("Bg1"),
            Foreground = Ui.Brush("Text1"),
            BorderBrush = Ui.Brush("Border1"),
            BorderThickness = new Thickness(1)
        };
        parent.Children.Add(tb);
        return tb;
    }

    private async Task ExecuteActionAsync(AgentAction action, Border card, string? jiraProject, string? jiraIssueType)
    {
        if (_busy) return;
        _busy = true;
        SetStatus(L.T("Assistant_Executing"));
        try
        {
            var args = ArgsToDict(action.Args);
            if (action.IntegrationType == "jira")
            {
                args["projectKey"] = jiraProject;
                args["issueTypeId"] = jiraIssueType;
            }

            var res = await _api.ConfirmAgentActionAsync(_sessionId ?? "", action.ToolName, action.ToolCallId, args, _model, false);
            MessagesPanel.Children.Remove(card);
            HandleAgentResult(res);
        }
        catch (ApiException ex) { ShowError(ex.Message); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { _busy = false; SetStatus(""); }
    }

    private async Task CancelActionAsync(AgentAction action, Border card)
    {
        if (_busy) return;
        _busy = true;
        SetStatus("");
        try
        {
            var res = await _api.ConfirmAgentActionAsync(_sessionId ?? "", action.ToolName, action.ToolCallId, new { }, _model, true);
            MessagesPanel.Children.Remove(card);
            AddAssistantBubble(L.T("Assistant_Cancelled"));
            HandleAgentResult(res);
        }
        catch (Exception)
        {
            // Zelfs als de backend-follow-up faalt: kaart weg + korte notitie
            MessagesPanel.Children.Remove(card);
            AddAssistantBubble(L.T("Assistant_Cancelled"));
        }
        finally { _busy = false; }
    }

    // ============================================
    // ARGS-HELPERS
    // ============================================

    private static Dictionary<string, object?> ArgsToDict(JsonElement args)
    {
        var d = new Dictionary<string, object?>();
        if (args.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in args.EnumerateObject())
                d[p.Name] = p.Value; // JsonElement serialiseert terug naar hetzelfde JSON
        }
        return d;
    }

    private static string FormatArgs(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return "";
        var sb = new StringBuilder();
        foreach (var p in args.EnumerateObject())
        {
            string value;
            switch (p.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    var items = new List<string>();
                    foreach (var it in p.Value.EnumerateArray())
                        items.Add(it.ValueKind == JsonValueKind.String ? it.GetString() ?? "" : it.ToString());
                    value = string.Join(", ", items);
                    break;
                case JsonValueKind.String:
                    value = p.Value.GetString() ?? "";
                    break;
                default:
                    value = p.Value.ToString();
                    break;
            }
            if (string.IsNullOrWhiteSpace(value)) continue;
            sb.Append("• ").Append(p.Name).Append(": ").Append(value).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    // ============================================
    // UI-HELPERS
    // ============================================

    private void AddUserBubble(string text)
    {
        AddBubble(text, isUser: true);
    }

    private void AddAssistantBubble(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        AddBubble(text, isUser: false);
    }

    private void AddBubble(string text, bool isUser)
    {
        var bubble = new Border
        {
            Background = isUser ? Ui.Brush("Accent") : Ui.Brush("Bg2"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = isUser ? new Thickness(60, 6, 8, 6) : new Thickness(8, 6, 60, 6),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 520
        };

        if (isUser)
        {
            bubble.Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                Foreground = Brushes.Black
            };
        }
        else
        {
            // Assistent-bubbel: markdown (koppen, lijsten, **vet**, tabellen, links), selecteerbaar.
            bubble.Child = new MarkdownViewer { Markdown = text };
        }

        MessagesPanel.Children.Add(bubble);
        ScrollToBottom();
    }

    private void ShowError(string message)
    {
        var bubble = new Border
        {
            Background = Ui.Brush("Bg2"),
            BorderBrush = Brushes.IndianRed,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(8, 6, 60, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 520
        };
        bubble.Child = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = Brushes.IndianRed
        };
        MessagesPanel.Children.Add(bubble);
        ScrollToBottom();
    }

    private void SetStatus(string text) => StatusLabel.Text = text;

    private void SetStatusStage(string stage)
    {
        // stage: "thinking" | "tool:<naam>" | "confirm:<naam>"
        if (string.IsNullOrEmpty(stage)) { SetStatus(""); return; }
        if (stage.StartsWith("tool:"))
            SetStatus(L.Tf("Assistant_ToolRunning", stage.Substring(5)));
        else if (stage.StartsWith("confirm:"))
            SetStatus(L.T("Assistant_Thinking"));
        else
            SetStatus(L.T("Assistant_Thinking"));
    }

    private void ScrollToBottom() => Dispatcher.UIThread.Post(() => MessagesScroller.ScrollToEnd(), DispatcherPriority.Background);

    // ============================================
    // VENSTER
    // ============================================

    private void Header_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void ClearContext_Click(object? sender, RoutedEventArgs e)
    {
        _contextActive = false;
        ContextChip.IsVisible = false;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    // ============================================
    // NIEUW GESPREK / GESCHIEDENIS / GEHEUGEN (overlay)
    // ============================================

    private void NewChat_Click(object? sender, RoutedEventArgs e)
    {
        _sessionId = null;
        _attachments.Clear();
        _documents.Clear();
        AttachmentsPanel.IsVisible = false;
        DocAttachmentsPanel.IsVisible = false;
        MessagesPanel.Children.Clear();
        ShowWelcome();
        InputBox.Focus();
    }

    private void ShowOverlay(string title, bool showAddBox)
    {
        OverlayTitle.Text = title;
        OverlayList.Children.Clear();
        OverlayAddInput.Text = "";
        OverlayAddBox.IsVisible = showAddBox;
        OverlayPanel.IsVisible = true;
    }

    private void HideOverlay() => OverlayPanel.IsVisible = false;

    private void OverlayClose_Click(object? sender, RoutedEventArgs e) => HideOverlay();

    private void OverlayBackdrop_Click(object? sender, PointerPressedEventArgs e) => HideOverlay();

    // Klik binnen de kaart mag de overlay niet sluiten.
    private void OverlayCard_Click(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private TextBlock OverlayMessage(string text) => new TextBlock
    {
        Text = text,
        FontSize = 12.5,
        Foreground = Ui.Brush("Text3"),
        Margin = new Thickness(2, 8, 2, 8),
        TextWrapping = TextWrapping.Wrap
    };

    // ---- Geschiedenis ----

    private async void History_Click(object? sender, RoutedEventArgs e)
    {
        ShowOverlay(L.T("Assistant_HistoryTitle"), false);
        OverlayList.Children.Add(OverlayMessage(L.T("Assistant_Loading")));
        try
        {
            var resp = await _api.GetSessionsAsync();
            OverlayList.Children.Clear();
            if (resp?.Sessions == null || resp.Sessions.Count == 0)
            {
                OverlayList.Children.Add(OverlayMessage(L.T("Assistant_HistoryEmpty")));
                return;
            }
            foreach (var s in resp.Sessions) OverlayList.Children.Add(BuildSessionRow(s));
        }
        catch (Exception ex)
        {
            OverlayList.Children.Clear();
            OverlayList.Children.Add(OverlayMessage(ex.Message));
        }
    }

    private Border BuildSessionRow(SessionSummary s)
    {
        var row = new Border
        {
            Background = Ui.Brush("Bg2"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(s.Title) ? L.T("Assistant_UntitledChat") : s.Title!,
            FontSize = 13,
            Foreground = Ui.Brush("Text1"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var when = FormatDate(s.UpdatedAt ?? s.CreatedAt);
        if (!string.IsNullOrEmpty(when))
            info.Children.Add(new TextBlock { Text = when, FontSize = 11, Foreground = Ui.Brush("Text3"), Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var del = new Button
        {
            Content = "\U0001F5D1",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "GhostButton" }
        };
        ToolTip.SetTip(del, L.T("Assistant_Delete"));
        del.Click += async (snd, ev) =>
        {
            try { await _api.DeleteSessionAsync(s.Id); } catch { /* stil */ }
            OverlayList.Children.Remove(row);
            if (_sessionId == s.Id) NewChat_Click(snd, ev);
        };
        Grid.SetColumn(del, 1);
        grid.Children.Add(del);

        row.Child = grid;
        row.PointerReleased += (snd, ev) => OpenSession(s.Id);
        return row;
    }

    private async void OpenSession(string id)
    {
        HideOverlay();
        MessagesPanel.Children.Clear();
        _sessionId = id;
        SetStatus(L.T("Assistant_Loading"));
        try
        {
            var resp = await _api.GetSessionAsync(id);
            MessagesPanel.Children.Clear();
            foreach (var m in resp.Messages) RenderStoredMessage(m.Role, m.Content);
            if (MessagesPanel.Children.Count == 0) ShowWelcome();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetStatus("");
            ScrollToBottom();
            InputBox.Focus();
        }
    }

    /// <summary>Herbouwt een opgeslagen bericht (jsonb content) als bubbel(s).</summary>
    private void RenderStoredMessage(string role, JsonElement content)
    {
        var text = GetJsonString(content, "text");
        if (role == "user")
        {
            var urls = GetJsonStringArray(content, "imageUrls");
            var hasContext = !string.IsNullOrEmpty(GetJsonString(content, "context"));
            if (!string.IsNullOrEmpty(text)) AddUserBubble(text!);
            else if (urls.Count > 0) AddUserBubble(L.T("Assistant_ImageOnly"));
            else if (hasContext) AddUserBubble(L.T("Assistant_DocumentOnly"));
            if (urls.Count > 0) AddUserThumbnailsFromUrls(urls);
        }
        else if (role == "assistant")
        {
            if (!string.IsNullOrWhiteSpace(text)) AddAssistantBubble(text!);
        }
        // role == "tool": interne tool-resultaten tonen we niet.
    }

    private void AddUserThumbnailsFromUrls(IEnumerable<string> urls)
    {
        var wrap = new WrapPanel { Margin = new Thickness(60, 0, 8, 6), HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var url in urls)
        {
            var img = new Image { Stretch = Stretch.UniformToFill };
            _ = LoadRemoteThumbAsync(img, url);
            wrap.Children.Add(new Border
            {
                Width = 72,
                Height = 72,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(6, 0, 0, 0),
                ClipToBounds = true,
                Background = Ui.Brush("Bg2"),
                Child = img
            });
        }
        if (wrap.Children.Count > 0) { MessagesPanel.Children.Add(wrap); ScrollToBottom(); }
    }

    private static readonly System.Net.Http.HttpClient ThumbHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static async Task LoadRemoteThumbAsync(Image target, string url)
    {
        try
        {
            var bytes = await ThumbHttp.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            target.Source = Bitmap.DecodeToWidth(ms, 128);
        }
        catch { /* thumbnail is optioneel */ }
    }

    // ---- Geheugen ----

    private async void Memory_Click(object? sender, RoutedEventArgs e)
    {
        ShowOverlay(L.T("Assistant_MemoryTitle"), true);
        OverlayAddInput.Watermark = L.T("Assistant_MemoryAddHint");
        await LoadMemoryAsync();
    }

    private async Task LoadMemoryAsync()
    {
        OverlayList.Children.Clear();
        OverlayList.Children.Add(OverlayMessage(L.T("Assistant_Loading")));
        try
        {
            var resp = await _api.GetMemoryAsync();
            OverlayList.Children.Clear();
            if (resp?.Memory == null || resp.Memory.Count == 0)
            {
                OverlayList.Children.Add(OverlayMessage(L.T("Assistant_MemoryEmpty")));
                return;
            }
            foreach (var m in resp.Memory) OverlayList.Children.Add(BuildMemoryRow(m));
        }
        catch (Exception ex)
        {
            OverlayList.Children.Clear();
            OverlayList.Children.Add(OverlayMessage(ex.Message));
        }
    }

    private Border BuildMemoryRow(MemoryItem m)
    {
        var row = new Border
        {
            Background = Ui.Brush("Bg2"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txt = new TextBlock
        {
            Text = m.Content,
            FontSize = 12.5,
            Foreground = Ui.Brush("Text1"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(txt, 0);
        grid.Children.Add(txt);

        var del = new Button
        {
            Content = "\U0001F5D1",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "GhostButton" }
        };
        ToolTip.SetTip(del, L.T("Assistant_Delete"));
        del.Click += async (snd, ev) =>
        {
            try { await _api.DeleteMemoryAsync(m.Id); } catch { /* stil */ }
            OverlayList.Children.Remove(row);
        };
        Grid.SetColumn(del, 1);
        grid.Children.Add(del);

        row.Child = grid;
        return row;
    }

    private async void MemoryAdd_Click(object? sender, RoutedEventArgs e)
    {
        var content = OverlayAddInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(content)) return;
        OverlayAddInput.Text = "";
        try
        {
            await _api.AddMemoryAsync(content);
            await LoadMemoryAsync();
        }
        catch (Exception ex)
        {
            await MkDialog.ShowInfo(L.T("Assistant_MemoryTitle"), ex.Message, this);
        }
    }

    // ---- helpers ----

    private static string? GetJsonString(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static List<string> GetJsonStringArray(JsonElement el, string prop)
    {
        var list = new List<string>();
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in v.EnumerateArray())
                if (it.ValueKind == JsonValueKind.String) list.Add(it.GetString()!);
        }
        return list;
    }

    private static string FormatDate(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        return DateTimeOffset.TryParse(iso, out var dto)
            ? dto.LocalDateTime.ToString("dd MMM yyyy HH:mm")
            : "";
    }
}
