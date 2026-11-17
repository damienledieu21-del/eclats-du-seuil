using System;
using System.Collections.Generic;
using EdS.Data.Feasts;
using EdS.Data.Json.Text;
using EdS.Data.Story;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Les 12 fêtes d'Orsédia (sous-lot F1) : lecture stricte du catalogue des fêtes (<c>kind: "feasts"</c>, <c>data/v0/world/feasts.json</c>).
    /// La forme seulement ; le reste va au validateur unique <see cref="FeastValidator"/>.
    /// </summary>
    public static class FeastJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "feasts.json");

        public static FeastLoadResult ReadFile(string path, StoryBook story, TextCatalog texts)
        {
            var report = new ValidationReport();
            return FeastValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), story, texts, report);
        }

        public static FeastLoadResult Read(string json, StoryBook story, TextCatalog texts)
        {
            var report = new ValidationReport();
            return FeastValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), story, texts, report);
        }

        internal static FeastDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new FeastDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new FeastData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["wallet"] = (v, p) => d.Wallet = ReadWallet(input, v, p),
                    ["maxDishes"] = (v, p) => d.MaxDishes = input.Int32(v, p),
                    ["merchant"] = (v, p) => d.Merchant = input.Text(v, p),
                    ["feasts"] = (v, p) => d.Feasts = input.ReadLimitedList(v, p, FeastValidator.FeastCount,
                        n => $"{n} fêtes, au plus {FeastValidator.FeastCount}.", (e, ip) => ReadFeast(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static FeastWalletData? ReadWallet(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastWalletData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["cap"] = (v, p) => x.Cap = input.Int32(v, p),
            }) ? x : null;
        }

        private static FeastEntryData? ReadFeast(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastEntryData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["number"] = (v, p) => x.Number = input.Int32(v, p),
                ["festival"] = (v, p) => x.Festival = input.Text(v, p),
                ["motto"] = (v, p) => x.Motto = input.Text(v, p),
                ["title"] = (v, p) => x.Title = ReadTitle(input, v, p),
                ["activities"] = (v, p) => x.Activities = input.ReadLimitedList(v, p, FeastValidator.MaxActivities,
                    n => $"{n} activités, au plus {FeastValidator.MaxActivities}.", (e, ip) => ReadActivity(input, e, ip)),
                ["rewardAfter"] = (v, p) => x.RewardAfter = input.Int32(v, p),
                ["refs"] = (v, p) => x.Refs = input.ReadLimitedList(v, p, FeastValidator.RefsPerFeast,
                    n => $"{n} références, {FeastValidator.RefsPerFeast} attendues.", (e, ip) => ReadRef(input, e, ip)),
            }) ? x : null;
        }

        private static FeastTitleData? ReadTitle(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastTitleData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
            }) ? x : null;
        }

        private static FeastActivityData? ReadActivity(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastActivityData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["coins"] = (v, p) => x.Coins = input.Int32(v, p),
                ["host"] = (v, p) => x.Host = input.Text(v, p),
                ["contest"] = (v, p) => x.Contest = input.Text(v, p),
            }) ? x : null;
        }

        private static FeastRefData? ReadRef(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastRefData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["code"] = (v, p) => x.Code = input.Text(v, p),
                ["category"] = (v, p) => x.Category = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["size"] = (v, p) => x.Size = ReadSize(input, v, p),
                ["image"] = (v, p) => x.Image = input.Text(v, p),
                ["icon"] = (v, p) => x.Icon = input.Text(v, p),
                ["rarity"] = (v, p) => x.Rarity = input.Text(v, p),
                ["price"] = (v, p) => x.Price = input.Int32(v, p),
            }) ? x : null;
        }

        private static FeastSizeData? ReadSize(JsonInput input, JsonElement element, string path)
        {
            var x = new FeastSizeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["x"] = (v, p) => x.X = input.Decimal(v, p),
                ["y"] = (v, p) => x.Y = input.Decimal(v, p),
                ["z"] = (v, p) => x.Z = input.Decimal(v, p),
            }) ? x : null;
        }
    }
}
