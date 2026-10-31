using System;
using System.Collections.Generic;
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
            }) ? x : null;
        }
    }
}
