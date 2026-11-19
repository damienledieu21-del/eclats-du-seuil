using System;
using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Rebuilds;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Lieux reconstruits (sous-lot V2.43.1) : lecture stricte du catalogue des chantiers (<c>kind: "rebuilds"</c>,
    /// <c>data/v0/world/rebuilds.json</c>). La forme seulement ; le reste va au validateur unique <see cref="RebuildValidator"/>.
    /// </summary>
    public static class RebuildJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "rebuilds.json");

        public static RebuildLoadResult ReadFile(string path, ZoneDefinition zone, ItemCatalog items, StoryBook story)
        {
            var report = new ValidationReport();
            return RebuildValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), zone, items, story, report);
        }

        public static RebuildLoadResult Read(string json, ZoneDefinition zone, ItemCatalog items, StoryBook story)
        {
            var report = new ValidationReport();
            return RebuildValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), zone, items, story, report);
        }

        internal static RebuildDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new RebuildDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new RebuildData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["sites"] = (v, p) => d.Sites = input.ReadLimitedList(v, p, RebuildValidator.MaxSites,
                        n => $"{n} chantiers, au plus {RebuildValidator.MaxSites}.", (e, ip) => ReadSite(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static RebuildSiteData? ReadSite(JsonInput input, JsonElement element, string path)
        {
            var x = new RebuildSiteData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["place"] = (v, p) => x.Place = input.Text(v, p),
                ["margin"] = (v, p) => x.Margin = input.Decimal(v, p),
                ["stages"] = (v, p) => x.Stages = input.ReadLimitedList(v, p, RebuildValidator.MaxStages,
                    n => $"{n} paliers, au plus {RebuildValidator.MaxStages}.", (e, ip) => ReadStage(input, e, ip)),
            }) ? x : null;
        }

        private static RebuildStageData? ReadStage(JsonInput input, JsonElement element, string path)
        {
            var x = new RebuildStageData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["needs"] = (v, p) => x.Needs = input.ReadLimitedList(v, p, RebuildValidator.MaxNeeds,
                    n => $"{n} besoins, au plus {RebuildValidator.MaxNeeds}.", (e, ip) => ReadNeed(input, e, ip)),
            }) ? x : null;
        }

        private static RebuildNeedData? ReadNeed(JsonInput input, JsonElement element, string path)
        {
            var x = new RebuildNeedData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["coins"] = (v, p) => x.Coins = input.Int64(v, p),
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["count"] = (v, p) => x.Count = input.Int32(v, p),
                ["defeat"] = (v, p) => x.Defeat = input.Int32(v, p),
                ["discover"] = (v, p) => x.Discover = input.Text(v, p),
            }) ? x : null;
        }
    }
}
