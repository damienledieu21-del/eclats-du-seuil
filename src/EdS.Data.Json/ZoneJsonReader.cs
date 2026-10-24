using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.4 : lecture stricte d'une zone du monde (<c>kind: "zone"</c>, <c>data/v0/world/zones/&lt;nom&gt;.json</c>). Le scénario de
    /// zone qu'elle nomme est lu et validé (dans <c>scenarios/</c>) ; le reste va au validateur unique <see cref="ZoneValidator"/>.
    /// </summary>
    public static class ZoneJsonReader
    {
        /// <summary>Chemin d'une zone dans un dossier de données (<c>data/v0</c>) : <c>world/zones/vallons.json</c> pour « zone.vallons ».</summary>
        public static string PathIn(string dataDirectory, string zoneId) =>
            System.IO.Path.Combine(dataDirectory, "world", "zones", (zoneId ?? throw new ArgumentNullException(nameof(zoneId))).Substring(zoneId.IndexOf('.') + 1) + ".json");

        /// <param name="scenariosDirectory">Dossier des scénarios (<c>scenarios/</c>).</param>
        public static ZoneLoadResult ReadFile(string path, string scenariosDirectory, GameData data, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), scenariosDirectory, data, items, report);
        }

        public static ZoneLoadResult Read(string json, string scenariosDirectory, GameData data, ItemCatalog items)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), scenariosDirectory, data, items, report);
        }

        private static ZoneLoadResult Read(byte[]? json, string scenariosDirectory, GameData data, ItemCatalog items, ValidationReport report)
        {
            if (scenariosDirectory is null) throw new ArgumentNullException(nameof(scenariosDirectory));
            if (data is null) throw new ArgumentNullException(nameof(data));
            var document = new ZoneDocument();
            if (json is null) return ZoneValidator.Validate(document, null, items, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"));
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            CombatSetup? scenario = null;
            if (report.IsValid && document.Data?.Scenario is string name && ZoneValidator.IsScenarioPath(name))
            {
                ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(System.IO.Path.Combine(scenariosDirectory, name + ".json"), data);
                if (loaded.Setup is null)
                    report.Add("$.data.scenario", ValidationCode.InvalidValue,
                        $"scénario de zone « {name} » refusé : {loaded.Report.Errors.FirstOrDefault()?.ToString() ?? "illisible"}");
                scenario = loaded.Setup;
            }
            // Sous-lot 12.7 : scénario des formes du chef.
            CombatSetup? bossScenario = null;
            if (report.IsValid && document.Data?.Boss?.Scenario is string bossName && ZoneValidator.IsScenarioPath(bossName))
            {
                ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(System.IO.Path.Combine(scenariosDirectory, bossName + ".json"), data);
                if (loaded.Setup is null)
                    report.Add("$.data.boss.scenario", ValidationCode.InvalidValue,
                        $"scénario du chef « {bossName} » refusé : {loaded.Report.Errors.FirstOrDefault()?.ToString() ?? "illisible"}");
                bossScenario = loaded.Setup;
            }
            return ZoneValidator.Validate(document, scenario, items, report, bossScenario);
        }

        private static ZoneData? ReadData(JsonInput input, JsonElement element, string path)
        {
            var d = new ZoneData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => d.Id = input.Text(v, p),
                ["name"] = (v, p) => d.Name = input.Text(v, p),
                ["scenario"] = (v, p) => d.Scenario = input.Text(v, p),
                ["entry"] = (v, p) => d.Entry = input.Text(v, p),
                ["safePoints"] = (v, p) => d.SafePoints = input.ReadLimitedList(v, p, ZoneValidator.MaxSafePoints, n => $"{n} points sûrs, au plus {ZoneValidator.MaxSafePoints}.",
                    (e, ip) => ReadSafePoint(input, e, ip)),
                ["places"] = (v, p) => d.Places = input.ReadLimitedList(v, p, ZoneValidator.MaxPlaces, n => $"{n} lieux, au plus {ZoneValidator.MaxPlaces}.",
                    (e, ip) => ReadPlace(input, e, ip)),
                ["populations"] = (v, p) => d.Populations = input.ReadLimitedList(v, p, ZoneValidator.MaxPopulations,
                    n => $"{n} populations, au plus {ZoneValidator.MaxPopulations}.", (e, ip) => ReadPopulation(input, e, ip)),
                ["passages"] = (v, p) => d.Passages = input.ReadLimitedList(v, p, ZoneValidator.MaxPassages, n => $"{n} passages, au plus {ZoneValidator.MaxPassages}.",
                    (e, ip) => ReadPassage(input, e, ip)),
                ["boss"] = (v, p) => d.Boss = ReadBoss(input, v, p),   // sous-lot 12.7 (schéma 2)
            });
            return ok ? d : null;
        }

        private static SafePointData? ReadSafePoint(JsonInput input, JsonElement element, string path)
        {
            var s = new SafePointData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => s.Id = input.Text(v, p),
                ["name"] = (v, p) => s.Name = input.Text(v, p),
                ["position"] = (v, p) => s.Position = ScenarioJsonReader.ReadPoint(input, v, p),
            });
            return ok ? s : null;
        }

        private static PlaceData? ReadPlace(JsonInput input, JsonElement element, string path)
        {
            var place = new PlaceData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => place.Id = input.Text(v, p),
                ["name"] = (v, p) => place.Name = input.Text(v, p),
                ["min"] = (v, p) => place.Min = ScenarioJsonReader.ReadPoint(input, v, p),
                ["max"] = (v, p) => place.Max = ScenarioJsonReader.ReadPoint(input, v, p),
            });
            return ok ? place : null;
        }

        private static PopulationData? ReadPopulation(JsonInput input, JsonElement element, string path)
        {
            var population = new PopulationData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["creature"] = (v, p) => population.Creature = input.Text(v, p),
                ["respawnMs"] = (v, p) => population.RespawnMs = input.Int64(v, p),
                ["leashRange"] = (v, p) => population.LeashRange = input.Decimal(v, p),
                ["loot"] = (v, p) => population.Loot = input.NullableText(v, p),
            });
            return ok ? population : null;
        }

        private static BossData? ReadBoss(JsonInput input, JsonElement element, string path)
        {
            var b = new BossData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => b.Id = input.Text(v, p),
                ["scenario"] = (v, p) => b.Scenario = input.Text(v, p),
                ["forms"] = (v, p) => b.Forms = input.ReadLimitedList(v, p, ZoneValidator.MaxForms, n => $"{n} formes, au plus {ZoneValidator.MaxForms}.",
                    (e, ip) => input.Text(e, ip)),
                ["fact"] = (v, p) => b.Fact = input.Text(v, p),
                ["rewards"] = (v, p) => b.Rewards = input.ReadLimitedList(v, p, ZoneValidator.MaxRewards, n => $"{n} récompenses, au plus {ZoneValidator.MaxRewards}.",
                    (e, ip) =>
                    {
                        var r = new RewardData();
                        return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["item"] = (rv, rp) => r.Item = input.Text(rv, rp),
                            ["count"] = (rv, rp) => r.Count = input.Int32(rv, rp),
                        }) ? r : null;
                    }),
                ["loot"] = (v, p) => b.Loot = input.NullableText(v, p),
            }) ? b : null;
        }

        private static PassageData? ReadPassage(JsonInput input, JsonElement element, string path)
        {
            var passage = new PassageData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => passage.Id = input.Text(v, p),
                ["name"] = (v, p) => passage.Name = input.Text(v, p),
                ["obstacle"] = (v, p) => passage.Obstacle = input.Text(v, p),
            });
            return ok ? passage : null;
        }
    }
}
