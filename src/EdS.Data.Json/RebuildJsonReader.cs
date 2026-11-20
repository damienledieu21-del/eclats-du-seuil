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

        /// <summary>
        /// Revue Codex de la PR #221 : faits que le catalogue des chantiers fait apprendre (<c>fact.rebuild.&lt;lieu&gt;.started</c>, un par
        /// palier, <c>.done</c>), d'après la forme du fichier seulement, pour que l'histoire et les marchands, validés avant lui, puissent les
        /// attendre. Fichier absent ou illisible : aucun (le catalogue est validé à part, avec ses erreurs).
        /// </summary>
        public static IReadOnlyList<string> DeclaredFacts(string path)
        {
            if (!System.IO.File.Exists(path)) return Array.Empty<string>();
            var report = new ValidationReport();
            RebuildDocument d = ReadDocument(JsonInput.ReadFile(path, report), report);
            var facts = new List<string>();
            foreach (RebuildSiteData? site in d.Data?.Sites ?? new List<RebuildSiteData?>())
            {
                if (!(site?.Id is string id) || !EdS.Data.Saves.SavedRebuild.IsLocalId(id)) continue;
                facts.Add($"fact.rebuild.{id}.started");
                foreach (RebuildStageData? st in site.Stages ?? new List<RebuildStageData?>())
                    if (st?.Id is string s && EdS.Data.Saves.SavedRebuild.IsLocalId(s)) facts.Add($"fact.rebuild.{id}.{s}");
                facts.Add($"fact.rebuild.{id}.done");
            }
            return facts;
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
                ["rebuilt"] = (v, p) =>   // V2.43.2b : ce que le lieu apporte une fois reconstruit
                {
                    var rb = new RebuildRebuiltData();
                    if (input.ReadFields(v, p, new Dictionary<string, Action<JsonElement, string>>
                    {
                        ["npcs"] = (e, ep) => rb.Npcs = input.ReadLimitedList(e, ep, RebuildValidator.MaxRebuiltNpcs,
                            n => $"{n} PNJ, au plus {RebuildValidator.MaxRebuiltNpcs}.", (q, qp) => input.Text(q, qp)),
                        ["safePoints"] = (e, ep) => rb.SafePoints = input.ReadLimitedList(e, ep, RebuildValidator.MaxRebuiltNpcs,
                            n => $"{n} points sûrs, au plus {RebuildValidator.MaxRebuiltNpcs}.", (q, qp) => input.Text(q, qp)),
                        ["doors"] = (e, ep) => rb.Doors = input.ReadLimitedList(e, ep, RebuildValidator.MaxRebuiltNpcs,   // V2.43.4 (version 2)
                            n => $"{n} portes, au plus {RebuildValidator.MaxRebuiltNpcs}.", (q, qp) => input.Text(q, qp)),
                    })) x.Rebuilt = rb;
                },
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
                ["reach"] = (v, p) => x.Reach = ScenarioJsonReader.ReadPoint(input, v, p),   // V2.43.2b
            }) ? x : null;
        }
    }
}
