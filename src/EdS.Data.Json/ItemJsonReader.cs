using System;
using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.3 : lecture stricte du catalogue des objets (<c>kind: "items"</c>, <c>data/v0/world/items.json</c>). Elle ne vérifie que
    /// la forme ; le reste va au validateur unique <see cref="ItemValidator"/>, contre les données du jeu.
    /// </summary>
    public static class ItemJsonReader
    {
        /// <summary>Chemin du catalogue dans un dossier de données (<c>data/v0</c>).</summary>
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "items.json");

        public static ItemCatalogLoadResult ReadFile(string path, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), data, report);
        }

        public static ItemCatalogLoadResult Read(string json, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), data, report);
        }

        private static ItemCatalogLoadResult Read(byte[]? json, GameData data, ValidationReport report)
        {
            var document = new ItemCatalogDocument();
            if (json is null) return ItemValidator.Validate(document, data, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"));
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return ItemValidator.Validate(document, data, report);
        }

        internal static ItemCatalogData? ReadData(JsonInput input, JsonElement element, string path)
        {
            var d = new ItemCatalogData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["slots"] = (v, p) => d.Slots = input.ReadLimitedList(v, p, ItemValidator.MaxSlots, n => $"{n} emplacements, au plus {ItemValidator.MaxSlots}.",
                    (e, ip) => input.Text(e, ip)),
                ["inventorySize"] = (v, p) => d.InventorySize = input.Int32(v, p),
                ["items"] = (v, p) => d.Items = input.ReadLimitedList(v, p, ItemValidator.MaxItems, n => $"{n} objets, au plus {ItemValidator.MaxItems}.",
                    (e, ip) => ReadItem(input, e, ip)),
                ["loot"] = (v, p) => d.Loot = input.ReadLimitedList(v, p, ItemValidator.MaxLootTables, n => $"{n} tables, au plus {ItemValidator.MaxLootTables}.",
                    (e, ip) => ReadTable(input, e, ip)),
            });
            return ok ? d : null;
        }

        private static ItemData? ReadItem(JsonInput input, JsonElement element, string path)
        {
            var item = new ItemData();
            Action<JsonElement, string> Track(string name, Action<JsonElement, string> read) => (v, p) =>
            {
                item.Present.Add(name);
                read(v, p);
            };
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => item.Id = input.Text(v, p),
                ["name"] = (v, p) => item.Name = input.Text(v, p),
                ["type"] = (v, p) => item.Type = input.Text(v, p),
                ["rarity"] = (v, p) => item.Rarity = input.Text(v, p),
                ["stack"] = Track("stack", (v, p) => item.Stack = input.Int32(v, p)),
                ["slot"] = Track("slot", (v, p) => item.Slot = input.Text(v, p)),
                ["requiredLevel"] = Track("requiredLevel", (v, p) => item.RequiredLevel = input.Int32(v, p)),
                ["bonus"] = Track("bonus", (v, p) => item.Bonus = ScenarioJsonReader.ReadIntegerMap(input, v, p)),
                ["heal"] = Track("heal", (v, p) => item.Heal = input.Int64(v, p)),               // sous-lot 12.6 (version 2)
                ["cooldownMs"] = Track("cooldownMs", (v, p) => item.CooldownMs = input.Int64(v, p)),
            });
            return ok ? item : null;
        }

        private static LootTableData? ReadTable(JsonInput input, JsonElement element, string path)
        {
            var table = new LootTableData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => table.Id = input.Text(v, p),
                ["drops"] = (v, p) => table.Drops = input.ReadLimitedList(v, p, ItemValidator.MaxDrops, n => $"{n} objets, au plus {ItemValidator.MaxDrops}.",
                    (e, ip) => ReadDrop(input, e, ip)),
            });
            return ok ? table : null;
        }

        private static LootDropData? ReadDrop(JsonInput input, JsonElement element, string path)
        {
            var drop = new LootDropData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["item"] = (v, p) => drop.Item = input.Text(v, p),
                ["count"] = (v, p) => drop.Count = input.Int32(v, p),
                ["chancePercent"] = (v, p) => drop.ChancePercent = input.Int32(v, p),
            });
            return ok ? drop : null;
        }
    }
}
