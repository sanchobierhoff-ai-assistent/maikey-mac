using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using mAIkey.Desktop.Controls;

namespace mAIkey.Desktop.Windows;

/// <summary>
/// Randloos kaartvenster (zoals MkDialog/TemplateSetupDialog op Windows) met een sleepbare
/// kop, een inhoudspaneel en Annuleren/Hoofdactie onderaan.
/// </summary>
public abstract class CardDialog : Window
{
    protected readonly StackPanel Body = new();
    protected readonly Button PrimaryBtn;
    protected readonly Button CancelBtn;
    public bool Confirmed { get; protected set; }

    protected CardDialog(string eyebrow, string title, string description, string primary, double width = 480)
    {
        Width = width;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18), Background = Brushes.Transparent };
        header.Children.Add(Label(eyebrow, 11, "Text3", FontWeight.SemiBold, new Thickness(0, 0, 0, 6)));
        header.Children.Add(Label(title, 18, "Text1", FontWeight.SemiBold, new Thickness(0, 0, 0, 6)));
        if (!string.IsNullOrEmpty(description)) header.Children.Add(Label(description, 12.5, "Text3", FontWeight.Normal));
        header.PointerPressed += (_, e) => BeginMoveDrag(e);

        CancelBtn = new Button { Content = L.T("Common_Cancel"), Height = 38, Padding = new Thickness(18, 0), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        CancelBtn.Classes.Add("GhostButton");
        CancelBtn.Click += (_, _) => { Confirmed = false; OnCancel(); Close(); };
        PrimaryBtn = new Button { Content = primary, Height = 38, Padding = new Thickness(22, 0), VerticalContentAlignment = VerticalAlignment.Center };
        PrimaryBtn.Classes.Add("AccentButton");
        PrimaryBtn.Click += (_, _) => { if (OnSubmit()) { Confirmed = true; Close(); } };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(CancelBtn);
        buttons.Children.Add(PrimaryBtn);

        var stack = new StackPanel { Margin = new Thickness(28) };
        stack.Children.Add(header);
        stack.Children.Add(Body);
        stack.Children.Add(buttons);

        var shell = new Border
        {
            Margin = new Thickness(24), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 8 28 0 #8C000000"), Child = stack
        };
        shell.Bind(Border.BackgroundProperty, this.GetResourceObservable("Bg1"));
        shell.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Border1"));
        Content = shell;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Confirmed = false; OnCancel(); Close(); } };
    }

    protected virtual bool OnSubmit() => true;
    protected virtual void OnCancel() { }

    protected TextBlock Label(string text, double size, string fg, FontWeight weight, Thickness? margin = null)
    {
        var tb = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
        tb.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(fg));
        return tb;
    }
}

/// <summary>Invulvelden voor template-variabelen (port van Views/TemplateSetupDialog).</summary>
public class TemplateSetupDialog : CardDialog
{
    private readonly TemplateVariableField[] _fields;
    private readonly Dictionary<string, TextBox> _boxes = new();
    public Dictionary<string, string>? Result { get; private set; }

    public TemplateSetupDialog(string templateName, TemplateVariableField[] fields)
        : base(L.T("TemplateSetup_Header"), L.Tf("TemplateSetup_TitleFmt", templateName), L.T("TemplateSetup_Desc"), L.T("TemplateSetup_AddBtn"))
    {
        Title = L.T("TemplateSetup_Title");
        _fields = fields;
        foreach (var f in fields)
        {
            Body.Children.Add(Label(f.Label, 13, "Text2", FontWeight.Normal, new Thickness(0, 0, 0, 6)));
            var box = new TextBox { Height = 40, Padding = new Thickness(12, 0), Margin = new Thickness(0, 0, 0, 16), VerticalContentAlignment = VerticalAlignment.Center, Watermark = f.Placeholder };
            _boxes[f.Key] = box;
            Body.Children.Add(box);
        }
        Opened += (_, _) => _boxes.Values.FirstOrDefault()?.Focus();
    }

    protected override bool OnSubmit()
    {
        var result = new Dictionary<string, string>();
        foreach (var f in _fields)
        {
            var v = _boxes[f.Key].Text;
            if (!string.IsNullOrWhiteSpace(v)) result[f.Key] = v.Trim();
        }
        Result = result.Count > 0 ? result : null;
        return true;
    }
}

/// <summary>Naam + schrijfstijl voor e-mailtemplates (port van Views/EmailTemplateSetupDialog).</summary>
public class EmailTemplateSetupDialog : CardDialog
{
    private const string NoStyleKey = "__none__";
    private readonly WritingStyle[] _styles;
    private readonly TextBox _naam;
    private readonly ComboBox _styleCombo;
    private readonly TextBlock _styleWarning;

    public string? ResultNaam { get; private set; }
    public string? ResultStyleId { get; private set; }
    public bool ShouldStartStyleTour { get; private set; }

    public EmailTemplateSetupDialog(string templateName, string prefillNaam, WritingStyle[] styles, bool showNaam = true)
        : base("TEMPLATE", L.Tf("TemplateSetup_TitleFmt", templateName), L.T("EmailSetup_Desc"), L.T("TemplateSetup_AddBtn"))
    {
        Title = L.T("TemplateSetup_Title");
        _styles = styles;

        var naamPanel = new StackPanel { IsVisible = showNaam };
        naamPanel.Children.Add(Label(L.T("EmailSetup_NaamLabel"), 13, "Text2", FontWeight.Normal, new Thickness(0, 0, 0, 6)));
        _naam = new TextBox { Height = 40, Padding = new Thickness(12, 0), Margin = new Thickness(0, 0, 0, 16), VerticalContentAlignment = VerticalAlignment.Center, Text = prefillNaam, Watermark = L.T("EmailSetup_NaamPlaceholder") };
        naamPanel.Children.Add(_naam);
        Body.Children.Add(naamPanel);

        Body.Children.Add(Label(L.T("EmailSetup_StyleLabel"), 13, "Text2", FontWeight.Normal, new Thickness(0, 0, 0, 4)));
        Body.Children.Add(Label(L.T("EmailSetup_StyleDesc"), 11.5, "Text3", FontWeight.Normal, new Thickness(0, 0, 0, 8)));

        _styleCombo = new ComboBox { Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = styles.Length > 0 };
        _styleWarning = Label(L.T("EmailSetup_StyleWarning"), 11.5, "Warning", FontWeight.Normal, new Thickness(0, 8, 0, 0));
        _styleWarning.IsVisible = false;

        var setupStyle = new Button { Content = L.T("EmailSetup_SetupStyle"), Height = 36, Padding = new Thickness(14, 0), IsVisible = styles.Length == 0, VerticalContentAlignment = VerticalAlignment.Center };
        setupStyle.Classes.Add("GhostButton");
        setupStyle.Click += (_, _) => { ShouldStartStyleTour = true; Confirmed = false; Close(); };

        if (styles.Length > 0)
        {
            var items = styles.Select(s => new ComboItem(s.Name, s.Id)).ToList();
            items.Add(new ComboItem(L.T("EmailSetup_NoStyle"), NoStyleKey));
            _styleCombo.ItemsSource = items;
            _styleCombo.SelectedIndex = 0;
            _styleCombo.SelectionChanged += (_, _) =>
                _styleWarning.IsVisible = (_styleCombo.SelectedItem as ComboItem)?.Value as string == NoStyleKey;
        }

        Body.Children.Add(_styleCombo);
        Body.Children.Add(setupStyle);
        Body.Children.Add(_styleWarning);
    }

    protected override bool OnSubmit()
    {
        var naam = _naam.Text?.Trim();
        ResultNaam = string.IsNullOrWhiteSpace(naam) ? null : naam;
        if (_styles.Length > 0)
        {
            var id = (_styleCombo.SelectedItem as ComboItem)?.Value as string;
            ResultStyleId = id == NoStyleKey ? null : id;
        }
        return true;
    }
}

/// <summary>Resultaat van het toepassen van een template op een mAIkey.</summary>
public sealed class TemplateApplyData
{
    public string Name = "";
    public string Prompt = "";
    public string OutputMode = "replace";
    public string Model = "gpt-4o-mini";
    public double Temperature = 0.7;
    public bool AskForContext;
    public bool IncludeImages = true;
    public Dictionary<string, string>? TemplateVariables;
    public string? StyleId;
    public string? IntegrationType;
    public IntegrationAction? IntegrationAction;
    public bool UseScreenCapture;
}

/// <summary>
/// Zet een template om naar mAIkey-velden (port van Views/TemplateApplyHelper): controleert
/// vereiste integraties en toont de stijl-/naam- en variabelen-dialogen.
/// </summary>
public static class TemplateApplyHelper
{
    private static readonly Dictionary<string, string> IntegrationDisplayNames = new()
    {
        ["jira"] = "Jira", ["github"] = "GitHub", ["slack"] = "Slack", ["teams"] = "Microsoft Teams",
        ["zapier"] = "Zapier/Make", ["todoist"] = "Todoist", ["trello"] = "Trello", ["asana"] = "Asana",
        ["gmail"] = "Gmail", ["google_calendar"] = "Google Agenda"
    };

    private static readonly Dictionary<string, string> Cancelled = new();

    public static async Task<TemplateApplyData?> BuildAsync(RemotePromptTemplate tpl, ApiClient api, ConfigService config, Window? owner)
    {
        if (!string.IsNullOrEmpty(tpl.IntegrationType))
        {
            bool connected = false;
            try
            {
                var integrations = await api.GetIntegrationsAsync();
                connected = integrations?.Any(i => i.IntegrationType == tpl.IntegrationType && i.IsActive) == true;
            }
            catch { }
            if (!connected)
            {
                var name = IntegrationDisplayNames.TryGetValue(tpl.IntegrationType, out var n) ? n : tpl.IntegrationType;
                await MkDialog.ShowInfo(L.T("Templates_IntegrationRequiredTitle"), L.Tf("Templates_IntegrationRequiredDesc", name), owner);
                return null;
            }
        }

        bool hasNaam = tpl.TemplateVariables?.Any(v => v.Key == "naam") == true;
        IntegrationAction? action = tpl.IntegrationType != null
            ? new IntegrationAction { Action = tpl.IntegrationAction?.Action ?? "", ShowReviewWindow = true }
            : null;

        if (tpl.RequiresStyleWarning || hasNaam)
        {
            var dialog = new EmailTemplateSetupDialog(tpl.Name, config.UserName ?? "", config.GetWritingStyles(), hasNaam);
            await dialog.ShowModalAsync(owner);
            if (!dialog.Confirmed)
            {
                if (dialog.ShouldStartStyleTour) Ui.Main?.StartTour("style_new");
                return null;
            }

            Dictionary<string, string>? prefilled = null;
            if (hasNaam && !string.IsNullOrWhiteSpace(dialog.ResultNaam))
                prefilled = new Dictionary<string, string> { ["naam"] = dialog.ResultNaam! };

            var remaining = tpl.TemplateVariables?.Where(v => v.Key != "naam").ToList();
            var fields = remaining is { Count: > 0 }
                ? remaining.Select(v => new TemplateVariableField { Key = v.Key, Label = v.Label, Placeholder = v.Placeholder }).ToArray()
                : null;

            var vars = await CollectVariablesAsync(tpl.Name, fields, prefilled, owner);
            if (vars == Cancelled) return null;

            return new TemplateApplyData
            {
                Name = tpl.Name, Prompt = tpl.CustomPrompt,
                OutputMode = tpl.OutputMode == "prompt" ? "window" : tpl.OutputMode,
                Model = tpl.Model, Temperature = tpl.Temperature, AskForContext = tpl.RequiresContext,
                IncludeImages = tpl.IncludeImages, TemplateVariables = vars, StyleId = dialog.ResultStyleId,
                IntegrationType = tpl.IntegrationType, IntegrationAction = action, UseScreenCapture = tpl.UseScreenCapture
            };
        }

        Dictionary<string, string>? standardPrefill = null;
        if (hasNaam && !string.IsNullOrWhiteSpace(config.UserName))
            standardPrefill = new Dictionary<string, string> { ["naam"] = config.UserName! };

        var filtered = tpl.TemplateVariables?.Where(v => standardPrefill == null || !standardPrefill.ContainsKey(v.Key)).ToList();
        var standardFields = filtered is { Count: > 0 }
            ? filtered.Select(v => new TemplateVariableField { Key = v.Key, Label = v.Label, Placeholder = v.Placeholder }).ToArray()
            : null;

        var standardVars = await CollectVariablesAsync(tpl.Name, standardFields, standardPrefill, owner);
        if (standardVars == Cancelled) return null;

        return new TemplateApplyData
        {
            Name = tpl.Name, Prompt = tpl.CustomPrompt,
            OutputMode = tpl.OutputMode == "prompt" ? "window" : tpl.OutputMode,
            Model = tpl.Model, Temperature = tpl.Temperature,
            AskForContext = tpl.RequiresContext || tpl.OutputMode == "prompt",
            IncludeImages = tpl.IncludeImages, TemplateVariables = standardVars,
            IntegrationType = tpl.IntegrationType, IntegrationAction = action, UseScreenCapture = tpl.UseScreenCapture
        };
    }

    private static async Task<Dictionary<string, string>?> CollectVariablesAsync(string templateName,
        TemplateVariableField[]? fields, Dictionary<string, string>? prefilled, Window? owner)
    {
        Dictionary<string, string>? variables = null;
        if (fields is { Length: > 0 })
        {
            var dialog = new TemplateSetupDialog(templateName, fields);
            await dialog.ShowModalAsync(owner);
            if (!dialog.Confirmed) return Cancelled;
            variables = dialog.Result;
        }
        if (prefilled is { Count: > 0 })
        {
            variables ??= new Dictionary<string, string>();
            foreach (var kv in prefilled) variables[kv.Key] = kv.Value;
        }
        return variables;
    }
}
