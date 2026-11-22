using System;
using System.Collections.Generic;
using EdS.Data.Corruption;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot V2.42.1 : lecture stricte de la corruption (<c>kind: "corruption"</c>, <c>data/v0/world/corruption.json</c>). La forme
    /// seulement ; le reste va au validateur unique <see cref="CorruptionValidator"/>.
    /// </summary>
    public static class CorruptionJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "corruption.json");

        public static CorruptionLoadResult ReadFile(string path, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return CorruptionValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), zone, items, report);
        }

        public static CorruptionLoadResult Read(string json, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return CorruptionValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), zone, items, report);
        }

        internal static CorruptionDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new CorruptionDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new CorruptionData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["zone"] = (v, p) => d.Zone = input.Text(v, p),
                    ["source"] = (v, p) => d.Source = ReadArea(input, v, p),
                    ["spreadDays"] = (v, p) => d.SpreadDays = input.Int32(v, p),
                    ["growDays"] = (v, p) => d.GrowDays = input.Int32(v, p),
                    ["maxStage"] = (v, p) => d.MaxStage = input.Int32(v, p),
                    ["respiteDays"] = (v, p) => d.RespiteDays = input.Int32(v, p),
                    ["purifyItem"] = (v, p) => d.PurifyItem = input.Text(v, p),
                    ["purifyXp"] = (v, p) => d.PurifyXp = input.Int64(v, p),
                    ["strengthPercent"] = (v, p) => d.StrengthPercent = input.Int32(v, p),
                    ["purifiedFact"] = (v, p) => d.PurifiedFact = input.Text(v, p),
                    ["areas"] = (v, p) => d.Areas = input.ReadLimitedList(v, p, CorruptionValidator.MaxAreas,
                        n => $"{n} terres, au plus {CorruptionValidator.MaxAreas}.", (e, ip) => ReadArea(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static CorruptionAreaData? ReadArea(JsonInput input, JsonElement element, string path)
        {
            var x = new CorruptionAreaData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["place"] = (v, p) => x.Place = input.Text(v, p),
                ["position"] = (v, p) => x.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["radius"] = (v, p) => x.Radius = input.Decimal(v, p),
                ["altar"] = (v, p) => x.Altar = ScenarioJsonReader.ReadPoint(input, v, p),
                ["from"] = (v, p) => x.From = input.ReadLimitedList<string>(v, p, CorruptionValidator.MaxFrom,
                    n => $"{n} terres d'origine, au plus {CorruptionValidator.MaxFrom}.", (e, ip) => input.Text(e, ip)),
            }) ? x : null;
        }
    }
}
