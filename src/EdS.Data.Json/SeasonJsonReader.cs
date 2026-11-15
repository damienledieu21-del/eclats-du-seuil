using System;
using System.Collections.Generic;
using EdS.Data.Crafting;
using EdS.Data.Json.Text;
using EdS.Data.Seasons;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;

namespace EdS.Data.Json
{
    /// <summary>
    /// Saisons : lecture stricte du contenu saisonnier (<c>kind: "seasons"</c>, <c>data/v0/world/seasons.json</c>). La forme seulement ; le
    /// reste va au validateur unique <see cref="SeasonValidator"/>.
    /// </summary>
    public static class SeasonJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "seasons.json");

        public static SeasonLoadResult ReadFile(string path, CraftingBook crafting, ZoneDefinition zone)
        {
            var report = new ValidationReport();
            return SeasonValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), crafting, zone, report);
        }

        public static SeasonLoadResult Read(string json, CraftingBook crafting, ZoneDefinition zone)
        {
            var report = new ValidationReport();
            return SeasonValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), crafting, zone, report);
        }

        /// <summary>Document lu sans validation (tests : document retouché en objets publics, puis validé).</summary>
        internal static SeasonDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new SeasonDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new SeasonData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["nodes"] = (v, p) => d.Nodes = input.ReadLimitedList(v, p, SeasonValidator.MaxSeasonalNodes,
                        n => $"{n} nœuds saisonniers, au plus {SeasonValidator.MaxSeasonalNodes}.", (e, ip) => ReadNode(input, e, ip)),
                    ["mistEchoes"] = (v, p) => d.MistEchoes = ReadMistEchoes(input, v, p),
                    ["passages"] = (v, p) => d.Passages = input.ReadLimitedList(v, p, SeasonValidator.MaxPassages,
                        n => $"{n} passages, au plus {SeasonValidator.MaxPassages}.", (e, ip) => ReadPassage(input, e, ip)),
                    ["locks"] = (v, p) => d.Locks = input.ReadLimitedList(v, p, SeasonValidator.MaxLocks,
                        n => $"{n} verrous, au plus {SeasonValidator.MaxLocks}.", (e, ip) => ReadLock(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static SeasonalNodeData? ReadNode(JsonInput input, JsonElement element, string path)
        {
            var x = new SeasonalNodeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["node"] = (v, p) => x.Node = input.Text(v, p),
                ["seasons"] = (v, p) => x.Seasons = input.ReadLimitedList(v, p, 4, n => $"{n} saisons, au plus 4.", (e, ip) => input.Text(e, ip)),
            }) ? x : null;
        }

        private static MistEchoesData? ReadMistEchoes(JsonInput input, JsonElement element, string path)
        {
            var x = new MistEchoesData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["presence"] = (v, p) => x.Presence = ReadPresence(input, v, p),
                ["maxPerVolume"] = (v, p) => x.MaxPerVolume = input.Int32(v, p),
            }) ? x : null;
        }

        private static Dictionary<string, Fixed?>? ReadPresence(JsonInput input, JsonElement element, string path)
        {
            var x = new Dictionary<string, Fixed?>(StringComparer.Ordinal);
            Action<JsonElement, string> Season(string id) => (v, p) => x[id] = input.Decimal(v, p);
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["spring"] = Season("spring"),
                ["summer"] = Season("summer"),
                ["autumn"] = Season("autumn"),
                ["winter"] = Season("winter"),
            }) ? x : null;
        }

        private static SeasonPassageData? ReadPassage(JsonInput input, JsonElement element, string path)
        {
            var x = new SeasonPassageData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["requires"] = (v, p) => x.Requires = input.Text(v, p),
            }) ? x : null;
        }

        private static SeasonLockData? ReadLock(JsonInput input, JsonElement element, string path)
        {
            var x = new SeasonLockData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["passage"] = (v, p) => x.Passage = input.Text(v, p),
                ["anchor"] = (v, p) => x.Anchor = ScenarioJsonReader.ReadPoint(input, v, p),
            }) ? x : null;
        }
    }
}
