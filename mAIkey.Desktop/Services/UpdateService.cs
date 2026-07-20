using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using mAIkey.Core.Services;
using Velopack;
using Velopack.Sources;

namespace mAIkey.Desktop.Services;

/// <summary>
/// Update-info die aan de UI wordt teruggegeven. Houdt intern de Velopack
/// UpdateManager + gevonden release vast, zodat DownloadAndApply die kan gebruiken.
/// </summary>
public class UpdateInfo
{
    public string LatestVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool ForceUpdate { get; set; }

    internal UpdateManager Manager { get; set; } = null!;
    internal Velopack.UpdateInfo VeloUpdate { get; set; } = null!;
}

/// <summary>
/// Auto-update voor de macOS-app via Velopack met GitHub Releases als bron —
/// dezelfde feed en aanpak als de Windows-app (frontend/Services/UpdateService.cs).
///
/// De macOS-releases staan als assets in dezelfde releases-repo, maar in het
/// aparte kanaal "osx" zodat de Mac-app geen Windows-pakketten binnenhaalt.
/// </summary>
public static class UpdateService
{
    // Releases-repo voor de desktop-app (gedeeld met de Windows-app).
    private const string GithubRepoUrl = "https://github.com/sanchobierhoff-ai-assistent/maikey-mac";

    // Kanaal per processor, zodat een Intel-Mac geen Apple-Silicon-pakket haalt (en andersom).
    private static string Channel =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

    /// <summary>
    /// Check of er een nieuwere macOS-versie beschikbaar is via Velopack.
    /// Retourneert UpdateInfo als er een update is, anders null. Faalt stil bij
    /// netwerkfouten of wanneer de app niet via Velopack is geïnstalleerd
    /// (bijv. een losse debug-build of `dotnet run`).
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(string baseUrl)
    {
        try
        {
            var mgr = new UpdateManager(
                new GithubSource(GithubRepoUrl, accessToken: null, prerelease: false),
                new UpdateOptions { ExplicitChannel = Channel });

            // Niet via Velopack geïnstalleerd (dev-build) → geen auto-update.
            if (!mgr.IsInstalled)
                return null;

            var velo = await mgr.CheckForUpdatesAsync();
            if (velo == null)
                return null; // al up-to-date

            bool force = await GetForceUpdateFlagAsync(baseUrl);

            var notes = velo.TargetFullRelease?.NotesMarkdown
                        ?? velo.TargetFullRelease?.NotesHTML
                        ?? "";

            return new UpdateInfo
            {
                LatestVersion = velo.TargetFullRelease?.Version?.ToString() ?? "",
                ReleaseNotes = notes,
                ForceUpdate = force,
                Manager = mgr,
                VeloUpdate = velo
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloadt de update (delta indien mogelijk), installeert en herstart de app.
    /// Keert normaal gesproken niet terug — Velopack herstart het proces.
    /// </summary>
    public static async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? progress = null)
    {
        await info.Manager.DownloadUpdatesAsync(info.VeloUpdate, progress);
        info.Manager.ApplyUpdatesAndRestart(info.VeloUpdate);
    }

    /// <summary>
    /// Vraagt de eigen backend of deze update verplicht is (force_update),
    /// zodat MINIMUM_CLIENT_VERSION in Railway blijft werken. Faalt stil naar false.
    /// </summary>
    private static async Task<bool> GetForceUpdateFlagAsync(string baseUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.Add("X-Client-Version", ConfigService.CURRENT_VERSION);

            var response = await client.GetAsync($"{baseUrl}/version");
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return root.TryGetProperty("force_update", out var fu) && fu.GetBoolean();
        }
        catch
        {
            return false;
        }
    }
}
