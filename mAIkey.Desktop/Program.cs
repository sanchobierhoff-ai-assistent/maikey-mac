using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using Velopack;

namespace mAIkey.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack moet als allereerste draaien — vóór Avalonia — zodat
        // install/update/uninstall-hooks snel afgehandeld worden. Bij een
        // normale start doet dit niets en gaat de app gewoon verder.
        VelopackApp.Build().Run();

        IconProvider.Current.Register<MaterialDesignIconProvider>();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
