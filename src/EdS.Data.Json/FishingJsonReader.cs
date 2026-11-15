using System;
using System.Collections.Generic;
using EdS.Data.Fishing;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Pêche et carnet : lecture stricte de la pêche (<c>kind: "fishing"</c>, <c>data/v0/world/fishing.json</c>). La forme seulement ; le reste
    /// va au validateur unique <see cref="FishingValidator"/>.
    /// </summary>
    public static class FishingJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "fishing.json");

        public static FishingLoadResult ReadFile(string path, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return FishingValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), texts, zone, items, report);
        }

        public static FishingLoadResult Read(string json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return FishingValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), texts, zone, items, report);
        }

        internal static FishingDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new FishingDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new FishingData();
                List<string?>? Texts(JsonElement v, string p, int max) => input.ReadLimitedList(v, p, max, n => $"{n} entrées, au plus {max}.", (e, ip) => input.Text(e, ip));
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["biteMinMs"] = (v, p) => d.BiteMinMs = input.Int64(v, p),
                    ["biteMaxMs"] = (v, p) => d.BiteMaxMs = input.Int64(v, p),
                    ["windowMs"] = (v, p) => d.WindowMs = input.Int64(v, p),
                    ["range"] = (v, p) => d.Range = input.Decimal(v, p),
                    ["spots"] = (v, p) => d.Spots = input.ReadLimitedList(v, p, FishingValidator.MaxSpots, n => $"{n} postes, au plus {FishingValidator.MaxSpots}.", (e, ip) =>
                    {
                        var s = new FishingSpotData();
                        return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["id"] = (v2, p2) => s.Id = input.Text(v2, p2),
                            ["name"] = (v2, p2) => s.Name = input.Text(v2, p2),
                            ["position"] = (v2, p2) => s.Position = ScenarioJsonReader.ReadPoint(input, v2, p2),
                            ["fish"] = (v2, p2) => s.Fish = Texts(v2, p2, FishingValidator.MaxFish),
                        }) ? s : null;
                    }),
                    ["fish"] = (v, p) => d.Fish = input.ReadLimitedList(v, p, FishingValidator.MaxFish, n => $"{n} espèces, au plus {FishingValidator.MaxFish}.", (e, ip) =>
                    {
                        var f = new FishData();
                        return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["item"] = (v2, p2) => f.Item = input.Text(v2, p2),
                            ["weight"] = (v2, p2) => f.Weight = input.Int32(v2, p2),
                            ["minCm"] = (v2, p2) => f.MinCm = input.Int32(v2, p2),
                            ["maxCm"] = (v2, p2) => f.MaxCm = input.Int32(v2, p2),
                            ["seasons"] = (v2, p2) => f.Seasons = Texts(v2, p2, 4),
                            ["phases"] = (v2, p2) => f.Phases = Texts(v2, p2, 4),
                        }) ? f : null;
                    }),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }
    }
}
