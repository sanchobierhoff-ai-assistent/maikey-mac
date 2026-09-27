using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Laatst bekende modellenlijst uit de Supabase-catalogus (/ai/models).
    /// Wordt gevuld door de plekken die de lijst tóch al ophalen (hotkey-editor,
    /// assistent-instellingen, opstart) zodat de fallback-model-kiezer
    /// (ModelPickerDialog) actuele modellen kan tonen i.p.v. een hardcoded lijst —
    /// ook op het moment dat er net een call is mislukt.
    /// </summary>
    public static class ModelCatalogCache
    {
        private static AIModel[] _models = Array.Empty<AIModel>();

        public static IReadOnlyList<AIModel> Models => _models;

        /// <summary>Werk de cache bij met een verse lijst (lege/null-lijsten worden genegeerd).</summary>
        public static void Update(AIModel[]? models)
        {
            if (models != null && models.Length > 0) _models = models;
        }

        /// <summary>
        /// Zorgt dat de cache gevuld is; haalt eenmalig op via de API als dat nog niet
        /// gebeurd is. Bij een netwerkfout blijft de cache leeg en valt de caller terug.
        /// </summary>
        public static async Task<IReadOnlyList<AIModel>> EnsureLoadedAsync(ApiClient api)
        {
            if (_models.Length > 0) return _models;
            try
            {
                var resp = await api.GetAvailableModelsAsync();
                if (resp.Success && resp.Models != null && resp.Models.Length > 0)
                    _models = resp.Models;
            }
            catch { /* offline — laat leeg, caller valt terug op een minimale set */ }
            return _models;
        }
    }
}
