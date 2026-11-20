using System;
using System.Collections.Generic;
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

        public static SeasonLoadResult ReadFile(string path, ZoneDefinition zone)
        {
            var report = new ValidationReport();
            return SeasonValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), zone, report);
        }

        public static SeasonLoadResult Read(string json, ZoneDefinition zone)
        {
            var report = new ValidationReport();
            return SeasonValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), zone, report);
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
                    ["mistEchoes"] = (v, p) => d.MistEchoes = ReadMistEchoes(input, v, p),
                    ["passages"] = (v, p) => d.Passages = input.ReadLimitedList(v, p, SeasonValidator.MaxPassages,
                        n => $"{n} passages, au plus {SeasonValidator.MaxPassages}.", (e, ip) => ReadPassage(input, e, ip)),
                    ["locks"] = (v, p) => d.Locks = input.ReadLimitedList(v, p, SeasonValidator.MaxLocks,
                        n => $"{n} verrous, au plus {SeasonValidator.MaxLocks}.", (e, ip) => ReadLock(input, e, ip)),
                    ["vine"] = (v, p) => d.Vine = ReadVine(input, v, p),   // schéma 2 (V2.44.2)
                    ["snow"] = (v, p) => d.Snow = ReadSnow(input, v, p),
                    ["creatures"] = (v, p) => d.Creatures = input.ReadLimitedList(v, p, SeasonValidator.MaxCreatures,
                        n => $"{n} créatures de saison, au plus {SeasonValidator.MaxCreatures}.", (e, ip) =>
                        {
                            var c = new SeasonCreatureData();
                            return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                            {
                                ["creature"] = (w, q) => c.Creature = input.Text(w, q),
                                ["seasons"] = (w, q) => c.Seasons = ReadTexts(input, w, q, 4),
                            }) ? c : null;
                        }),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
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

        private static AnnualDateData? ReadDate(JsonInput input, JsonElement element, string path)
        {
            var x = new AnnualDateData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["month"] = (v, p) => x.Month = input.Int32(v, p),
                ["day"] = (v, p) => x.Day = input.Int32(v, p),
            }) ? x : null;
        }

        private static List<string?>? ReadTexts(JsonInput input, JsonElement element, string path, int max) =>
            input.ReadLimitedList(element, path, max, n => $"{n} éléments, au plus {max}.", (e, ip) => input.Text(e, ip));

        private static VineData? ReadVine(JsonInput input, JsonElement element, string path)
        {
            var x = new VineData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["green"] = (v, p) => x.Green = ReadDate(input, v, p),
                ["ripe"] = (v, p) => x.Ripe = ReadDate(input, v, p),
                ["harvested"] = (v, p) => x.Harvested = ReadDate(input, v, p),
                ["rows"] = (v, p) => x.Rows = input.Text(v, p),
                ["nodes"] = (v, p) => x.Nodes = ReadTexts(input, v, p, SeasonValidator.MaxVineNodes),
            }) ? x : null;
        }

        private static SnowData? ReadSnow(JsonInput input, JsonElement element, string path)
        {
            var x = new SnowData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["lowland"] = (v, p) =>
                {
                    var span = new SnowSpanData();
                    x.Lowland = input.ReadFields(v, p, new Dictionary<string, Action<JsonElement, string>>
                    {
                        ["from"] = (w, q) => span.From = ReadDate(input, w, q),
                        ["to"] = (w, q) => span.To = ReadDate(input, w, q),
                    }) ? span : null;
                },
                ["pass"] = (v, p) =>
                {
                    var pass = new SnowPassData();
                    x.Pass = input.ReadFields(v, p, new Dictionary<string, Action<JsonElement, string>>
                    {
                        ["from"] = (w, q) => pass.From = ReadDate(input, w, q),
                        ["to"] = (w, q) => pass.To = ReadDate(input, w, q),
                        ["altitude"] = (w, q) => pass.Altitude = input.Decimal(w, q),
                        ["places"] = (w, q) => pass.Places = ReadTexts(input, w, q, SeasonValidator.MaxPassPlaces),
                    }) ? pass : null;
                },
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
