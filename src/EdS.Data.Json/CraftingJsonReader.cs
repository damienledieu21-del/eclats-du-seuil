using System;
using System.Collections.Generic;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.6 : lecture stricte des métiers (<c>kind: "crafting"</c>, <c>data/v0/world/crafting.json</c>). La forme seulement ; le reste
    /// va au validateur unique <see cref="CraftingValidator"/>.
    /// </summary>
    public static class CraftingJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "crafting.json");

        public static CraftingLoadResult ReadFile(string path, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), texts, zone, items, report);
        }

        public static CraftingLoadResult Read(string json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), texts, zone, items, report);
        }

        private static CraftingLoadResult Read(byte[]? json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport report) =>
            CraftingValidator.Validate(ReadDocument(json, report), texts, zone, items, report);

        /// <summary>Document lu sans validation (tests : document retouché en objets publics, puis validé).</summary>
        internal static CraftingDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new CraftingDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new CraftingData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["maxLevel"] = (v, p) => d.MaxLevel = input.Int32(v, p),
                    ["xpToLevel"] = (v, p) => d.XpToLevel = ReadLongs(input, v, p),
                    ["professions"] = (v, p) => d.Professions = input.ReadLimitedList(v, p, CraftingValidator.MaxProfessions,
                        n => $"{n} métiers, au plus {CraftingValidator.MaxProfessions}.", (e, ip) => ReadProfession(input, e, ip)),
                    ["recipes"] = (v, p) => d.Recipes = input.ReadLimitedList(v, p, CraftingValidator.MaxRecipes,
                        n => $"{n} recettes, au plus {CraftingValidator.MaxRecipes}.", (e, ip) => ReadRecipe(input, e, ip)),
                    ["nodes"] = (v, p) => d.Nodes = input.ReadLimitedList(v, p, CraftingValidator.MaxNodes,
                        n => $"{n} nœuds, au plus {CraftingValidator.MaxNodes}.", (e, ip) => ReadNode(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static List<long?>? ReadLongs(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Array, path, "liste d'entiers")) return null;
            var list = new List<long?>();
            int i = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (i >= CraftingValidator.MaxLevels) { input.Report.Add(path, ValidationCode.LimitExceeded, $"au plus {CraftingValidator.MaxLevels} niveaux."); return null; }
                list.Add(input.Int64(item, $"{path}[{i++}]"));
            }
            return list;
        }

        private static ProfessionData? ReadProfession(JsonInput input, JsonElement element, string path)
        {
            var x = new ProfessionData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
            }) ? x : null;
        }

        private static StackData? ReadStack(JsonInput input, JsonElement element, string path)
        {
            var x = new StackData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["count"] = (v, p) => x.Count = input.Int32(v, p),
            }) ? x : null;
        }

        private static RecipeData? ReadRecipe(JsonInput input, JsonElement element, string path)
        {
            var x = new RecipeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["profession"] = (v, p) => x.Profession = input.Text(v, p),
                ["requiredLevel"] = (v, p) => x.RequiredLevel = input.Int32(v, p),
                ["place"] = (v, p) => x.Place = input.Text(v, p),
                ["inputs"] = (v, p) => x.Inputs = input.ReadLimitedList(v, p, CraftingValidator.MaxInputs, n => $"{n} entrées, au plus {CraftingValidator.MaxInputs}.",
                    (e, ip) => ReadStack(input, e, ip)),
                ["output"] = (v, p) => x.Output = ReadStack(input, v, p),
                ["xp"] = (v, p) => x.Xp = input.Int64(v, p),
            }) ? x : null;
        }

        private static NodeData? ReadNode(JsonInput input, JsonElement element, string path)
        {
            var x = new NodeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["profession"] = (v, p) => x.Profession = input.Text(v, p),
                ["item"] = (v, p) => x.Item = input.Text(v, p),
                ["count"] = (v, p) => x.Count = input.Int32(v, p),
                ["position"] = (v, p) => x.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["respawnMs"] = (v, p) => x.RespawnMs = input.Int64(v, p),
                ["xp"] = (v, p) => x.Xp = input.Int64(v, p),
            }) ? x : null;
        }
    }
}
