using System;
using System.Collections.Generic;
using EdS.Data.Feasts;
using EdS.Data.Homes;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Rebuilds;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1) : lecture stricte du catalogue de la maison (<c>kind: "home"</c>, <c>data/v0/world/home.json</c>).
    /// La forme seulement ; le reste va au validateur unique <see cref="HomeValidator"/>.
    /// </summary>
    public static class HomeJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "home.json");

        public static HomeLoadResult ReadFile(string path, IReadOnlyList<ZoneDefinition> zones, ItemCatalog items, FeastBook? feasts)
        {
            var report = new ValidationReport();
            return HomeValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), zones, items, feasts, report);
        }

        public static HomeLoadResult Read(string json, IReadOnlyList<ZoneDefinition> zones, ItemCatalog items, FeastBook? feasts)
        {
            var report = new ValidationReport();
            return HomeValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), zones, items, feasts, report);
        }

        internal static HomeDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new HomeDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new HomeData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["interior"] = (v, p) => d.Interior = input.Text(v, p),
                    ["door"] = (v, p) => d.Door = input.Text(v, p),
                    ["price"] = (v, p) => d.Price = input.Int64(v, p),
                    ["base"] = (v, p) =>
                    {
                        var t = new HomeTierData();
                        if (input.ReadFields(v, p, TierFields(input, t))) d.Base = t;
                    },
                    ["stages"] = (v, p) => d.Stages = input.ReadLimitedList(v, p, HomeValidator.MaxStages,
                        n => $"{n} paliers, au plus {HomeValidator.MaxStages}.", (e, ip) => ReadStage(input, e, ip)),
                    ["furniture"] = (v, p) => d.Furniture = input.ReadLimitedList(v, p, HomeValidator.MaxFurniture,
                        n => $"{n} meubles, au plus {HomeValidator.MaxFurniture}.", (e, ip) => ReadPiece(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static Dictionary<string, Action<JsonElement, string>> TierFields(JsonInput input, HomeTierData t) =>
            new Dictionary<string, Action<JsonElement, string>>
            {
                ["floor"] = (v, p) => t.Floor = ReadSize(input, v, p),
                ["chest"] = (v, p) => t.Chest = input.Int32(v, p),
                ["pieces"] = (v, p) => t.Pieces = input.Int32(v, p),
            };

        private static HomeSizeData? ReadSize(JsonInput input, JsonElement element, string path)
        {
            var s = new HomeSizeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["width"] = (v, p) => s.Width = input.Decimal(v, p),
                ["depth"] = (v, p) => s.Depth = input.Decimal(v, p),
            }) ? s : null;
        }

        private static HomeStageData? ReadStage(JsonInput input, JsonElement element, string path)
        {
            var x = new HomeStageData();
            Dictionary<string, Action<JsonElement, string>> fields = TierFields(input, x);
            fields["id"] = (v, p) => x.Id = input.Text(v, p);
            fields["needs"] = (v, p) => x.Needs = input.ReadLimitedList(v, p, HomeValidator.MaxNeeds,
                n => $"{n} besoins, au plus {HomeValidator.MaxNeeds}.", (e, ip) => RebuildJsonReader.ReadNeed(input, e, ip));
            return input.ReadFields(element, path, fields) ? x : null;
        }

        private static HomePieceData? ReadPiece(JsonInput input, JsonElement element, string path)
        {
            var x = new HomePieceData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["feast"] = (v, p) => x.Feast = input.Text(v, p),
                ["size"] = (v, p) => x.Size = ReadSize(input, v, p),
                ["use"] = (v, p) => x.Use = input.Text(v, p),
            }) ? x : null;
        }
    }
}
