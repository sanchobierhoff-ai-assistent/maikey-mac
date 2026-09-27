using System.Collections.Generic;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Generiek koppel-framework: test/options/save voor token-gebaseerde integraties
    /// (Notion, Linear, Airtable, HubSpot, Zendesk). Verwijderen via DeleteIntegrationAsync.
    /// </summary>
    public partial class ApiClient
    {
        public async Task<GenericResult> TestGenericIntegrationAsync(string type, Dictionary<string, string> credentials)
        {
            try
            {
                var r = await PostAsync<GenericApiResponse>("/integrations/generic/test", new { type, credentials });
                return new GenericResult { Success = r?.Success == true, Error = r?.Error };
            }
            catch (ApiException ex) { return new GenericResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (System.Exception ex) { return new GenericResult { Success = false, Error = ex.Message }; }
        }

        public async Task<GenericOptionsResult> GetGenericOptionsAsync(string type, Dictionary<string, string> credentials)
        {
            try
            {
                var r = await PostAsync<GenericOptionsResponse>("/integrations/generic/options", new { type, credentials });
                return new GenericOptionsResult { Success = r?.Success == true, Options = r?.Options ?? new() };
            }
            catch (ApiException ex) { return new GenericOptionsResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (System.Exception ex) { return new GenericOptionsResult { Success = false, Error = ex.Message }; }
        }

        public async Task<GenericResult> SaveGenericIntegrationAsync(string type, Dictionary<string, string> credentials, Dictionary<string, string?> config)
        {
            try
            {
                var r = await PostAsync<GenericApiResponse>("/integrations/generic/save", new { type, credentials, config });
                return new GenericResult { Success = r?.Success == true, Error = r?.Error };
            }
            catch (ApiException ex) { return new GenericResult { Success = false, Error = SafeError(ex.Details) }; }
            catch (System.Exception ex) { return new GenericResult { Success = false, Error = ex.Message }; }
        }
    }

    public class GenericResult { public bool Success { get; set; } public string? Error { get; set; } }

    public class GenericOption
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public class GenericOptionsResult
    {
        public bool Success { get; set; }
        public List<GenericOption> Options { get; set; } = new();
        public string? Error { get; set; }
    }

    public class GenericApiResponse { public bool Success { get; set; } public string? Error { get; set; } }

    public class GenericOptionsResponse
    {
        public bool Success { get; set; }
        public List<GenericOption> Options { get; set; } = new();
    }
}
