using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Merchants;
using EdS.Data.Story;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot V1.1 : lecture stricte des marchands (<c>kind: "merchants"</c>, <c>data/v0/world/merchants.json</c> : valeur de base des objets,
    /// marchands et leurs offres). La forme seulement ; le reste va au validateur unique <see cref="MerchantValidator"/>.
    /// </summary>
    public static class MerchantJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "merchants.json");

        public static MerchantsLoadResult ReadFile(string path, TextCatalog texts, StoryBook story, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), texts, story, items, report);
        }

        public static MerchantsLoadResult Read(string json, TextCatalog texts, StoryBook story, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), texts, story, items, report);
        }

        private static MerchantsLoadResult Read(byte[]? json, TextCatalog texts, StoryBook story, ItemCatalog items, ValidationReport report) =>
            MerchantValidator.Validate(ReadDocument(json, report), texts, story, items, report);

        /// <summary>Document lu sans validation (tests : document retouché en objets publics, puis validé).</summary>
        internal static MerchantsDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new MerchantsDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new MerchantsData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["values"] = (v, p) => d.Values = input.ReadLimitedList(v, p, MerchantValidator.MaxValues,
                        n => $"{n} valeurs, au plus {MerchantValidator.MaxValues}.", (e, ip) => ReadValue(input, e, ip)),
                    ["merchants"] = (v, p) => d.Merchants = input.ReadLimitedList(v, p, MerchantValidator.MaxMerchants,
                        n => $"{n} marchands, au plus {MerchantValidator.MaxMerchants}.", (e, ip) => ReadMerchant(input, e, ip)),
                    ["prices"] = (v, p) => d.Prices = input.ReadLimitedList(v, p, MerchantValidator.MaxRules,   // sous-lot V2.10 (schéma 5)
                        n => $"{n} règles, au plus {MerchantValidator.MaxRules}.", (e, ip) => ReadPrice(input, e, ip)),
                    ["shocks"] = (v, p) => d.Shocks = input.ReadLimitedList(v, p, MerchantValidator.MaxRules,
                        n => $"{n} contrecoups, au plus {MerchantValidator.MaxRules}.", (e, ip) => ReadShock(input, e, ip)),
                    ["market"] = (v, p) => d.Market = ReadMarket(input, v, p),   // sous-lot V2.14 (schéma 6)
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static ItemValueData? ReadValue(JsonInput input, JsonElement element, string path)
        {
            var x = new ItemValueData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["value"] = (v, p) => x.Value = input.Int64(v, p),
            }) ? x : null;
        }

        private static MerchantData? ReadMerchant(JsonInput input, JsonElement element, string path)
        {
            var x = new MerchantData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["npc"] = (v, p) => x.Npc = input.Text(v, p),
                ["greeting"] = (v, p) => x.Greeting = input.Text(v, p),
                ["sellPercent"] = (v, p) => x.SellPercent = input.Int32(v, p),
                ["closed"] = (v, p) => x.Closed = input.ReadLimitedList(v, p, 4, n => $"{n} moments, au plus 4.", (e, ip) => input.Text(e, ip)),
                ["daily"] = (v, p) => x.Daily = input.Int32(v, p),
                ["rank"] = (v, p) => x.Rank = input.Text(v, p),   // sous-lot V1.7 (schéma 2)
                ["fact"] = (v, p) => x.Fact = input.Text(v, p),   // sous-lot V1.9.3b (schéma 3)
                ["faction"] = (v, p) => x.Faction = input.Text(v, p),   // sous-lot V2.7 (schéma 4)
                ["standing"] = (v, p) => x.Standing = input.Text(v, p),
                ["restock"] = (v, p) => x.Restock = input.Int32(v, p),   // sous-lot V2.10 (schéma 5)
                ["resale"] = (v, p) => x.Resale = input.Int32(v, p),
                ["festivals"] = (v, p) => x.Festivals = Names(input, v, p),
                ["shop"] = (v, p) => x.Shop = ReadShop(input, v, p),   // sous-lot V2.14 (schéma 6)
                ["bakes"] = (v, p) => x.Bakes = input.ReadLimitedList(v, p, MerchantValidator.MaxBakes,
                    n => $"{n} fournées, au plus {MerchantValidator.MaxBakes}.", (e, ip) => ReadBake(input, e, ip)),
                ["soldOut"] = (v, p) => x.SoldOut = input.Text(v, p),
                ["offers"] = (v, p) => x.Offers = input.ReadLimitedList(v, p, MerchantValidator.MaxOffers,
                    n => $"{n} offres, au plus {MerchantValidator.MaxOffers}.", (e, ip) => ReadOffer(input, e, ip)),
            }) ? x : null;
        }

        private static OfferData? ReadOffer(JsonInput input, JsonElement element, string path)
        {
            var x = new OfferData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["price"] = (v, p) => x.Price = input.Int64(v, p),
                ["always"] = (v, p) => x.Always = input.Boolean(v, p),
                ["rank"] = (v, p) => x.Rank = input.Text(v, p),   // sous-lot V1.7 (schéma 2)
                ["stock"] = (v, p) => x.Stock = input.Int32(v, p),   // sous-lot V2.10 (schéma 5)
                ["fresh"] = (v, p) => x.Fresh = input.Boolean(v, p),   // sous-lot V2.14 (schéma 6)
                ["door"] = (v, p) => x.Door = input.Boolean(v, p),
            }) ? x : null;
        }

        /// <summary>Sous-lot V2.14 (schéma 6) : la boutique d'un marchand.</summary>
        private static ShopData? ReadShop(JsonInput input, JsonElement element, string path)
        {
            var x = new ShopData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["kind"] = (v, p) => x.Kind = input.Text(v, p),
                ["sign"] = (v, p) => x.Sign = input.Text(v, p),
                ["building"] = (v, p) => x.Building = input.Text(v, p),
                ["open"] = (v, p) => x.Open = input.Text(v, p),
                ["close"] = (v, p) => x.Close = input.Text(v, p),
                ["rest"] = (v, p) => x.Rest = Days(input, v, p),
                ["door"] = (v, p) => x.Door = input.Text(v, p),
                ["clerk"] = (v, p) => x.Clerk = input.Text(v, p),
            }) ? x : null;
        }

        /// <summary>Jours de la semaine (entiers), au plus 7 ; un jour mal typé est lu null (signalé par le validateur et le lecteur).</summary>
        private static List<int?>? Days(JsonInput input, JsonElement element, string path)
        {
            List<object?>? boxed = input.ReadLimitedList(element, path, 7, n => $"{n} jours, au plus 7.", (e, ip) => input.Int32(e, ip) is int n ? (object)n : null);
            return boxed?.Select(b => b is int n ? n : (int?)null).ToList();
        }

        private static BakeData? ReadBake(JsonInput input, JsonElement element, string path)
        {
            var x = new BakeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["at"] = (v, p) => x.At = input.Text(v, p),
                ["percent"] = (v, p) => x.Percent = input.Int32(v, p),
            }) ? x : null;
        }

        /// <summary>Sous-lot V2.14 (schéma 6) : le grand jour de marché.</summary>
        private static MarketDayData? ReadMarket(JsonInput input, JsonElement element, string path)
        {
            var x = new MarketDayData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["festivals"] = (v, p) => x.Festivals = Names(input, v, p),
                ["setup"] = (v, p) => x.Setup = input.Text(v, p),
                ["teardown"] = (v, p) => x.Teardown = input.Text(v, p),
                ["crowd"] = (v, p) => x.Crowd = input.Int32(v, p),
                ["cries"] = (v, p) => x.Cries = input.ReadLimitedList(v, p, MerchantValidator.MaxCries,
                    n => $"{n} criées, au plus {MerchantValidator.MaxCries}.", (e, ip) => input.Text(e, ip)),
                ["stalls"] = (v, p) => x.Stalls = input.ReadLimitedList(v, p, MerchantValidator.MaxStalls,
                    n => $"{n} étals, au plus {MerchantValidator.MaxStalls}.", (e, ip) => ReadStall(input, e, ip)),
                ["haggle"] = (v, p) => x.Haggle = ReadHaggle(input, v, p),
            }) ? x : null;
        }

        private static StallData? ReadStall(JsonInput input, JsonElement element, string path)
        {
            var x = new StallData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["merchant"] = (v, p) => x.Merchant = input.Text(v, p),
                ["kind"] = (v, p) => x.Kind = input.Text(v, p),
                ["position"] = (v, p) => x.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["sign"] = (v, p) => x.Sign = input.Text(v, p),
            }) ? x : null;
        }

        private static HaggleData? ReadHaggle(JsonInput input, JsonElement element, string path)
        {
            var x = new HaggleData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["standing"] = (v, p) => x.Standing = input.Text(v, p),
                ["percent"] = (v, p) => x.Percent = input.Int32(v, p),
                ["chance"] = (v, p) => x.Chance = input.Int32(v, p),
                ["bonus"] = (v, p) => x.Bonus = input.Int32(v, p),
            }) ? x : null;
        }

        private static List<string?>? Names(JsonInput input, JsonElement element, string path) =>
            input.ReadLimitedList(element, path, MerchantValidator.MaxNamed, n => $"{n} noms, au plus {MerchantValidator.MaxNamed}.", (e, ip) => input.Text(e, ip));

        private static PriceRuleData? ReadPrice(JsonInput input, JsonElement element, string path)
        {
            var x = new PriceRuleData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["season"] = (v, p) => x.Season = input.Text(v, p),
                ["weather"] = (v, p) => x.Weather = input.Text(v, p),
                ["items"] = (v, p) => x.Items = Names(input, v, p),
                ["merchants"] = (v, p) => x.Merchants = Names(input, v, p),
                ["percent"] = (v, p) => x.Percent = input.Int32(v, p),
                ["market"] = (v, p) => x.Market = input.Boolean(v, p),   // sous-lot V2.14 (schéma 6)
            }) ? x : null;
        }

        private static ShockData? ReadShock(JsonInput input, JsonElement element, string path)
        {
            var x = new ShockData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["event"] = (v, p) => x.Event = input.Text(v, p),
                ["outcome"] = (v, p) => x.Outcome = input.Text(v, p),
                ["days"] = (v, p) => x.Days = input.Int32(v, p),
                ["fact"] = (v, p) => x.Fact = input.Text(v, p),
                ["notFact"] = (v, p) => x.NotFact = input.Text(v, p),
                ["items"] = (v, p) => x.Items = Names(input, v, p),
                ["merchants"] = (v, p) => x.Merchants = Names(input, v, p),
                ["stockPercent"] = (v, p) => x.StockPercent = input.Int32(v, p),
                ["pricePercent"] = (v, p) => x.PricePercent = input.Int32(v, p),
            }) ? x : null;
        }
    }
}
