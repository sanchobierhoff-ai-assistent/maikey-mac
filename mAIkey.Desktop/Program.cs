using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using Velopack;

namespace mAIkey.Desktop;

class Program
{
    // Pad naar een crashlog op het Bureaublad, zodat een opstartfout zichtbaar
    // is zonder Terminal. Valt terug op de home-map als het Bureaublad ontbreekt.
    private static readonly string CrashLog = Path.Combine(
        GetDesktopOrHome(), "maikey-crash.log");

    [STAThread]
    public static void Main(string[] args)
    {
        // Vang álle niet-afgehandelde fouten en schrijf ze naar het logbestand.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrash(e.ExceptionObject as Exception);

        try
        {
            // Velopack moet als allereerste draaien — vóór Avalonia.
            VelopackApp.Build().Run();

            IconProvider.Current.Register<MaterialDesignIconProvider>();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Systeem-fonts als terugval, zodat een ontbrekend/niet-ladend
            // Geist-lettertype de app niet laat crashen.
            .With(new FontManagerOptions
            {
                FontFallbacks = new[]
                {
                    new FontFallback { FontFamily = new FontFamily("Helvetica Neue") },
                    new FontFallback { FontFamily = new FontFamily("Arial") },
                    new FontFallback { FontFamily = new FontFamily("sans-serif") }
                }
            })
            .LogToTrace();

    private static void WriteCrash(Exception? ex)
    {
        try
        {
            File.WriteAllText(CrashLog,
                $"mAIkey crash — {DateTime.Now}\n\n{ex?.ToString() ?? "onbekende fout"}");
        }
        catch { /* logging mag nooit zelf de boel opblazen */ }
    }

    private static string GetDesktopOrHome()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
            return desktop;
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}