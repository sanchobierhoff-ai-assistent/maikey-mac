using System.Collections.Generic;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Eén invulveld voor een generieke (token-gebaseerde) integratie.
    /// </summary>
    public class IntegrationField
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public bool Secret { get; set; }          // true → PasswordBox
        public string? HelpUrl { get; set; }
        public string? Placeholder { get; set; }
    }

    /// <summary>
    /// Beschrijft een token-gebaseerde integratie voor het generieke koppel-venster.
    /// Nieuwe integratie toevoegen = hier één descriptor + backend lees/schrijf-functies + tools.
    /// </summary>
    public class IntegrationDescriptor
    {
        public string Type { get; set; } = "";            // moet matchen met integrations-extra.js
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LogoGlyph { get; set; } = "";       // korte letter/emoji voor het logo-vierkant
        public string LogoColor { get; set; } = "#666666";
        public List<IntegrationField> Fields { get; set; } = new();
        public string? OptionsLabel { get; set; }         // niet-null → toon dropdown (bv. "Standaard database")
        public string? OptionsConfigKey { get; set; }     // config-sleutel die de agent-tools lezen (bv. "defaultDatabaseId")
    }

    /// <summary>
    /// De catalogus van generieke integraties die in "Meer integraties" verschijnen.
    /// </summary>
    public static class IntegrationCatalog
    {
        public static readonly List<IntegrationDescriptor> All = new()
        {
            new IntegrationDescriptor
            {
                Type = "notion", Name = "Notion", LogoGlyph = "N", LogoColor = "#111111",
                Description = "Zoek pagina's & databases en maak notities aan.",
                Fields = new()
                {
                    new IntegrationField { Key = "token", Label = "Integratie-token", Secret = true,
                        HelpUrl = "https://www.notion.so/my-integrations",
                        Placeholder = "secret_..." }
                },
                OptionsLabel = "Standaard database", OptionsConfigKey = "defaultDatabaseId"
            },
            new IntegrationDescriptor
            {
                Type = "linear", Name = "Linear", LogoGlyph = "L", LogoColor = "#5E6AD2",
                Description = "Zoek issues en maak nieuwe issues aan.",
                Fields = new()
                {
                    new IntegrationField { Key = "token", Label = "API-sleutel", Secret = true,
                        HelpUrl = "https://linear.app/settings/api",
                        Placeholder = "lin_api_..." }
                },
                OptionsLabel = "Standaard team", OptionsConfigKey = "defaultTeamId"
            },
            new IntegrationDescriptor
            {
                Type = "airtable", Name = "Airtable", LogoGlyph = "A", LogoColor = "#FCB400",
                Description = "Lees records en voeg records toe aan een tabel.",
                Fields = new()
                {
                    new IntegrationField { Key = "token", Label = "Personal access token", Secret = true,
                        HelpUrl = "https://airtable.com/create/tokens", Placeholder = "pat..." },
                    new IntegrationField { Key = "baseId", Label = "Base ID", Secret = false, Placeholder = "app..." },
                    new IntegrationField { Key = "tableName", Label = "Tabelnaam", Secret = false, Placeholder = "Tabel 1" }
                }
            },
            new IntegrationDescriptor
            {
                Type = "hubspot", Name = "HubSpot", LogoGlyph = "H", LogoColor = "#FF7A59",
                Description = "Zoek contacten en maak nieuwe contacten aan.",
                Fields = new()
                {
                    new IntegrationField { Key = "token", Label = "Private App-token", Secret = true,
                        HelpUrl = "https://developers.hubspot.com/docs/api/private-apps",
                        Placeholder = "pat-..." }
                }
            },
            new IntegrationDescriptor
            {
                Type = "zendesk", Name = "Zendesk", LogoGlyph = "Z", LogoColor = "#03363D",
                Description = "Zoek/lees tickets (incl. reacties) en reageer of maak tickets aan.",
                Fields = new()
                {
                    new IntegrationField { Key = "subdomain", Label = "Subdomein", Secret = false, Placeholder = "jouwbedrijf" },
                    new IntegrationField { Key = "email", Label = "E-mailadres", Secret = false, Placeholder = "jij@bedrijf.nl" },
                    new IntegrationField { Key = "token", Label = "API-token", Secret = true,
                        HelpUrl = "https://support.zendesk.com/hc/en-us/articles/4408889192858" }
                }
            }
        };
    }
}
