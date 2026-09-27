#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using mAIkey.Desktop.Windows;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Alleen Debug: MAIKEY_UI_SNAPSHOT=&lt;map&gt; (samen met MAIKEY_UI_PREVIEW=1) rendert elk scherm naar
/// een PNG en sluit daarna af. Bedoeld om de UI te controleren zonder Mac of login.
/// </summary>
public static class UiSnapshot
{
    public static string? Directory => Environment.GetEnvironmentVariable("MAIKEY_UI_SNAPSHOT");

    public static async Task RunAsync(MainWindow main)
    {
        var dir = Directory!;
        System.IO.Directory.CreateDirectory(dir);
        await Task.Delay(1500);

        var pages = new List<(string, Action)>
        {
            ("01-dashboard", main.NavigateToDashboard),
            ("02-hotkeys", () => main.NavigateToHotkeyEditor()),
            ("03-styles", main.NavigateToStyleLibrary),
            ("04-style-editor", () => main.NavigateToStyleEditor()),
            ("05-templates", main.NavigateToTemplates),
            ("06-integrations", main.NavigateToIntegrations),
            ("07-koppelingen", main.NavigateToKoppelingen),
            ("08-cloudsync", main.NavigateToCloudSync),
            ("09-settings", main.NavigateToSettings),
            ("10-assistant-settings", main.NavigateToAssistant),
        };
        foreach (var (name, go) in pages)
        {
            try { go(); await Task.Delay(1200); Save(main, Path.Combine(dir, name + ".png")); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(dir, name + ".error.txt"), ex.ToString()); }
        }

        var windows = new List<(string, Func<Window>)>
        {
            ("20-login", () => new LoginWindow(App.Api, App.Config)),
            ("21-register", () => new RegisterWindow(App.Api, App.Config)),
            ("22-forgot", () => new ForgotPasswordWindow(App.Api)),
            ("23-assistant", () => new AssistantWindow(App.Api, App.Config, "Voorbeeldtekst als context")),
            ("24-bridgehub", () => new BridgeHubWindow(App.Api, App.Config)),
            ("25-zendesk", () => new ZendeskTicketWindow(App.Api)),
            ("26-jira-search", () => new JiraSearchEditWindow(App.Api)),
            ("27-input-prompt", () => new InputPromptWindow()),
            ("28-feedback", () => new FeedbackWindow("Hoi Jan, bedankt voor je mail.", "Beste Jan,\n\nHartelijk dank voor je bericht.", new HotkeyConfig { Name = "Formeel" }, App.Api, App.Config)),
            ("29-result", () => new ResultWindow("## Resultaat\n\nDit is **markdown** met een lijst:\n\n- punt één\n- punt twee\n\n| Kolom | Waarde |\n|---|---|\n| a | 1 |", App.Api)),
        };
        foreach (var (name, make) in windows)
        {
            try
            {
                var w = make();
                w.Show();
                await Task.Delay(1500);
                Save(w, Path.Combine(dir, name + ".png"));
                w.Close();
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(dir, name + ".error.txt"), ex.ToString()); }
        }

        await Task.Delay(300);
        App.Quit();
    }

    private static void Save(Window w, string path)
    {
        var size = new PixelSize((int)Math.Max(1, w.Bounds.Width), (int)Math.Max(1, w.Bounds.Height));
        using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(w);
        bmp.Save(path);
    }
}
#endif
