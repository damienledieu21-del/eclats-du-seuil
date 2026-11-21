using System;
using System.Collections.Generic;
using EdS.Data.Caravans;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot V2.41.1 : lecture stricte des caravanes et de leurs attaques (<c>kind: "caravans"</c>, <c>data/v0/world/caravans.json</c>).
    /// La forme seulement ; le reste va au validateur unique <see cref="CaravanValidator"/>.
    /// </summary>
    public static class CaravanJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "caravans.json");

        public static CaravanLoadResult ReadFile(string path, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return CaravanValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), zone, items, report);
        }

        public static CaravanLoadResult Read(string json, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return CaravanValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), zone, items, report);
        }

        internal static CaravanDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new CaravanDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new CaravanData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["caravans"] = (v, p) => d.Caravans = input.ReadLimitedList(v, p, CaravanValidator.MaxCaravans,
                        n => $"{n} caravanes, au plus {CaravanValidator.MaxCaravans}.", (e, ip) => ReadCaravan(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static CaravanEntryData? ReadCaravan(JsonInput input, JsonElement element, string path)
        {
            var x = new CaravanEntryData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["start"] = (v, p) => x.Start = input.Text(v, p),
                ["everyDays"] = (v, p) => x.EveryDays = input.Int32(v, p),
                ["firstDay"] = (v, p) => x.FirstDay = input.Int32(v, p),
                ["metersPerMinute"] = (v, p) => x.MetersPerMinute = input.Decimal(v, p),
                ["wagons"] = (v, p) => x.Wagons = input.Int32(v, p),
                ["guards"] = (v, p) => x.Guards = input.Int32(v, p),
                ["route"] = (v, p) => x.Route = input.ReadLimitedList(v, p, CaravanValidator.MaxRoute,
                    n => $"{n} points, au plus {CaravanValidator.MaxRoute}.", (e, ip) => ScenarioJsonReader.ReadPoint(input, e, ip)),
                ["attack"] = (v, p) => x.Attack = ZoneJsonReader.ReadEvent(input, v, p, caravan: true),
            }) ? x : null;
        }
    }
}
