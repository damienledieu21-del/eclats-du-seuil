using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.WorldBosses;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Grand Solitaire : lecture stricte des boss du monde (<c>kind: "worldBosses"</c>, <c>data/v0/world/worldbosses.json</c>). La forme
    /// seulement ; le reste va au validateur unique <see cref="WorldBossValidator"/>.
    /// </summary>
    public static class WorldBossJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "worldbosses.json");

        public static WorldBossLoadResult ReadFile(string path, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return WorldBossValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), texts, zone, items, report);
        }

        public static WorldBossLoadResult Read(string json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items)
        {
            var report = new ValidationReport();
            return WorldBossValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), texts, zone, items, report);
        }

        /// <summary>
        /// Schéma 2 (Grand Solitaire, 2e partie) : lit aussi le scénario des formes de chaque boss (<c>scenario</c>, dans
        /// <paramref name="scenariosDirectory"/>), validé par le validateur des scénarios ; un scénario introuvable ou refusé est signalé au
        /// chemin du boss.
        /// </summary>
        public static WorldBossLoadResult ReadFile(string path, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, string scenariosDirectory, GameData data)
        {
            var report = new ValidationReport();
            return WithForms(ReadDocument(JsonInput.ReadFile(path, report), report), texts, zone, items, scenariosDirectory, data, report);
        }

        public static WorldBossLoadResult Read(string json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, string scenariosDirectory, GameData data)
        {
            var report = new ValidationReport();
            return WithForms(ReadDocument(JsonInput.Utf8(json, report), report), texts, zone, items, scenariosDirectory, data, report);
        }

        private static WorldBossLoadResult WithForms(WorldBossDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, string scenariosDirectory,
            GameData data, ValidationReport report)
        {
            if (scenariosDirectory is null) throw new ArgumentNullException(nameof(scenariosDirectory));
            if (data is null) throw new ArgumentNullException(nameof(data));
            var forms = new List<CombatSetup?>();
            List<WorldBossData?>? bosses = document.Data?.Bosses;
            for (int i = 0; bosses != null && i < bosses.Count && i < WorldBossValidator.MaxBosses; i++)
            {
                CombatSetup? setup = null;
                if (bosses[i]?.Scenario is string name)
                {
                    if (!ZoneValidator.IsScenarioPath(name))
                        report.Add($"$.data.bosses[{i}].scenario", ValidationCode.InvalidValue, $"chemin de scénario « {name} » mal formé.");
                    else
                    {
                        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(System.IO.Path.Combine(scenariosDirectory, name + ".json"), data);
                        if (loaded.Setup is null)
                            report.Add($"$.data.bosses[{i}].scenario", ValidationCode.InvalidValue,
                                $"scénario des formes « {name} » refusé : {loaded.Report.Errors.FirstOrDefault()?.ToString() ?? "illisible"}");
                        setup = loaded.Setup;
                    }
                }
                forms.Add(setup);
            }
            return WorldBossValidator.Validate(document, texts, zone, items, forms, report);
        }


        internal static WorldBossDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new WorldBossDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new WorldBossesData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["bosses"] = (v, p) => d.Bosses = input.ReadLimitedList(v, p, WorldBossValidator.MaxBosses,
                        n => $"{n} boss du monde, au plus {WorldBossValidator.MaxBosses}.", (e, ip) => ReadBoss(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static List<T?>? List<T>(JsonInput input, JsonElement v, string p, int max, string what, Func<JsonElement, string, T?> read) where T : class =>
            input.ReadLimitedList(v, p, max, n => $"{n} {what}, au plus {max}.", read);

        private static WorldBossData? ReadBoss(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["scenario"] = (v, p) => x.Scenario = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["radius"] = (v, p) => x.Radius = input.Decimal(v, p),
                ["healthBase"] = (v, p) => x.HealthBase = input.Int64(v, p),
                ["healthPerPlayer"] = (v, p) => x.HealthPerPlayer = input.Int64(v, p),
                ["minPlayers"] = (v, p) => x.MinPlayers = input.Int32(v, p),
                ["maxPlayers"] = (v, p) => x.MaxPlayers = input.Int32(v, p),
                ["windowMinutes"] = (v, p) => x.WindowMinutes = input.Int32(v, p),
                ["chancePercent"] = (v, p) => x.ChancePercent = input.Int32(v, p),
                ["signsMs"] = (v, p) => x.SignsMs = input.Int64(v, p),
                ["limitMs"] = (v, p) => x.LimitMs = input.Int64(v, p),
                ["announceMs"] = (v, p) => x.AnnounceMs = input.Int64(v, p),
                ["objectiveMs"] = (v, p) => x.ObjectiveMs = input.Int64(v, p),
                ["walkSpeed"] = (v, p) => x.WalkSpeed = input.Decimal(v, p),
                ["clearance"] = (v, p) => x.Clearance = input.Decimal(v, p),
                ["phases"] = (v, p) => x.Phases = List(input, v, p, WorldBossValidator.MaxPhases, "phases", (e, ip) => ReadPhase(input, e, ip)),
                ["attacks"] = (v, p) => x.Attacks = List(input, v, p, WorldBossValidator.MaxAttacks, "attaques", (e, ip) => ReadAttack(input, e, ip)),
                ["sites"] = (v, p) => x.Sites = List(input, v, p, WorldBossValidator.MaxSites, "emplacements", (e, ip) => ReadSite(input, e, ip)),
                ["targets"] = (v, p) => x.Targets = List(input, v, p, WorldBossValidator.MaxTargets, "cibles", (e, ip) => ReadTarget(input, e, ip)),
                ["companionsInCombat"] = (v, p) => x.CompanionsInCombat = input.Int32(v, p),
                ["fallback"] = (v, p) => x.Fallback = ScenarioJsonReader.ReadPoint(input, v, p),
                ["loot"] = (v, p) => x.Loot = ReadLoot(input, v, p),
            }) ? x : null;
        }

        private static WorldBossPhaseData? ReadPhase(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossPhaseData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["fromPercent"] = (v, p) => x.FromPercent = input.Int32(v, p),
                ["form"] = (v, p) => x.Form = input.Text(v, p),
                ["attacks"] = (v, p) => x.Attacks = List(input, v, p, WorldBossValidator.MaxAttacks, "attaques", (e, ip) => input.Text(e, ip)),
            }) ? x : null;
        }

        private static WorldBossAttackData? ReadAttack(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossAttackData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["shape"] = (v, p) => x.Shape = input.Text(v, p),
                ["range"] = (v, p) => x.Range = input.Decimal(v, p),
                ["angle"] = (v, p) => x.Angle = input.Int32(v, p),
                ["width"] = (v, p) => x.Width = input.Decimal(v, p),
                ["radius"] = (v, p) => x.Radius = input.Decimal(v, p),
                ["inner"] = (v, p) => x.Inner = input.Decimal(v, p),
                ["outer"] = (v, p) => x.Outer = input.Decimal(v, p),
                ["countMin"] = (v, p) => x.CountMin = input.Int32(v, p),
                ["countMax"] = (v, p) => x.CountMax = input.Int32(v, p),
                ["staggerMs"] = (v, p) => x.StaggerMs = input.Int64(v, p),
                ["anchor"] = (v, p) => x.Anchor = input.Text(v, p),
                ["announceMs"] = (v, p) => x.AnnounceMs = input.Int64(v, p),
                ["lockMs"] = (v, p) => x.LockMs = input.Int64(v, p),
                ["recoveryMs"] = (v, p) => x.RecoveryMs = input.Int64(v, p),
            }) ? x : null;
        }

        private static WorldBossSiteData? ReadSite(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossSiteData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["kind"] = (v, p) => x.Kind = input.Text(v, p),
                ["position"] = (v, p) => x.Position = ScenarioJsonReader.ReadPoint(input, v, p),
            }) ? x : null;
        }

        private static WorldBossTargetData? ReadTarget(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossTargetData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["place"] = (v, p) => x.Place = input.Text(v, p),
                ["route"] = (v, p) => x.Route = List(input, v, p, WorldBossValidator.MaxRoute, "points", (e, ip) => ScenarioJsonReader.ReadPoint(input, e, ip)),
                ["objectives"] = (v, p) => x.Objectives = List(input, v, p, WorldBossValidator.MaxObjectives, "objectifs", (e, ip) => ReadObjective(input, e, ip)),
            }) ? x : null;
        }

        private static WorldBossObjectiveData? ReadObjective(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossObjectiveData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["position"] = (v, p) => x.Position = ScenarioJsonReader.ReadPoint(input, v, p),
            }) ? x : null;
        }

        private static WorldBossLootData? ReadLoot(JsonInput input, JsonElement element, string path)
        {
            var x = new WorldBossLootData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["trophy"] = (v, p) => x.Trophy = input.Text(v, p),
                ["appearance"] = (v, p) => x.Appearance = input.Text(v, p),
                ["mount"] = (v, p) => x.Mount = input.Text(v, p),
                ["mountChanceBasisPoints"] = (v, p) => x.MountChanceBasisPoints = input.Int32(v, p),
            }) ? x : null;
        }
    }
}
