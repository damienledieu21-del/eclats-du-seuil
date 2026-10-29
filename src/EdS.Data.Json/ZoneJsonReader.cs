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
    /// <summary>Intérieurs : zones reliées par leurs portes (<see cref="ZoneJsonReader.ReadLinked"/>).</summary>
    public sealed class WorldZonesLoadResult
    {
        /// <summary>Zone dont <see cref="Report"/> est le rapport : celle dont le fichier est refusé, ou dont une porte est en cause.</summary>
        public string Zone { get; }

        public ValidationReport Report { get; }

        /// <summary>Zones lues, celle de départ en premier ; vide si une zone est refusée ou si les portes ne se répondent pas.</summary>
        public IReadOnlyList<ZoneDefinition> Zones { get; }

        public bool IsValid => Zones.Count > 0;

        public WorldZonesLoadResult(string zone, ValidationReport report, IReadOnlyList<ZoneDefinition> zones)
        {
            Zone = zone ?? throw new ArgumentNullException(nameof(zone));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        }
    }

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
                ["roads"] = (v, p) => d.Roads = input.ReadLimitedList(v, p, ZoneValidator.MaxRoads, n => $"{n} routes, au plus {ZoneValidator.MaxRoads}.",
                    (e, ip) => ReadRoad(input, e, ip)),   // Vallons agrandis (schéma 3)
                ["indoor"] = (v, p) => d.Indoor = input.Boolean(v, p),   // intérieurs (schéma 4)
                ["doors"] = (v, p) => d.Doors = input.ReadLimitedList(v, p, ZoneValidator.MaxDoors, n => $"{n} portes, au plus {ZoneValidator.MaxDoors}.",
                    (e, ip) => ReadDoor(input, e, ip)),
            });
            return ok ? d : null;
        }

        private static DoorData? ReadDoor(JsonInput input, JsonElement element, string path)
        {
            var door = new DoorData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => door.Id = input.Text(v, p),
                ["name"] = (v, p) => door.Name = input.Text(v, p),
                ["position"] = (v, p) => door.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["to"] = (v, p) => door.To = input.Text(v, p),
                ["toDoor"] = (v, p) => door.ToDoor = input.Text(v, p),
                ["building"] = (v, p) => door.Building = input.NullableText(v, p),
            });
            return ok ? door : null;
        }

        /// <summary>
        /// Intérieurs : lit la zone <paramref name="start"/> et toutes celles où mènent ses portes, de proche en proche (dans l'ordre des
        /// portes), puis vérifie que les portes se répondent (<see cref="ZoneValidator.CheckDoors"/>). La première zone rendue est
        /// <paramref name="start"/>. Une zone refusée, ou des portes qui ne se répondent pas : <see cref="WorldZonesLoadResult.Zones"/> vide
        /// et le premier rapport en faute.
        /// </summary>
        public static WorldZonesLoadResult ReadLinked(string dataDirectory, string start, string scenariosDirectory, GameData data, ItemCatalog items)
        {
            if (dataDirectory is null) throw new ArgumentNullException(nameof(dataDirectory));
            if (start is null) throw new ArgumentNullException(nameof(start));
            var zones = new List<ZoneDefinition>();
            var queued = new List<string> { start };
            // Porte par laquelle chaque zone reliée a été trouvée (zone, rang de la porte, porte) : une zone introuvable est signalée sur elle.
            var via = new Dictionary<string, (string Zone, int Index, string Door)>(StringComparer.Ordinal);
            for (int i = 0; i < queued.Count; i++)
            {
                if (zones.Count >= ZoneValidator.MaxLinkedZones)
                {
                    var limit = new ValidationReport();
                    limit.Add("$", ValidationCode.LimitExceeded, $"plus de {ZoneValidator.MaxLinkedZones} zones reliées par des portes.");
                    return new WorldZonesLoadResult(queued[i], limit, Array.Empty<ZoneDefinition>());
                }
                string file = PathIn(dataDirectory, queued[i]);
                if (via.TryGetValue(queued[i], out var from) && !System.IO.File.Exists(file))
                {
                    var absent = new ValidationReport();
                    absent.Add($"$.data.doors[{from.Index}].to", ValidationCode.UnknownReference,
                        $"{from.Zone}, porte « {from.Door} » : zone « {queued[i]} » introuvable (fichier {System.IO.Path.GetFileName(file)} absent).");
                    return new WorldZonesLoadResult(from.Zone, absent, Array.Empty<ZoneDefinition>());
                }
                ZoneLoadResult loaded = ReadFile(file, scenariosDirectory, data, items);
                if (loaded.Zone is null) return new WorldZonesLoadResult(queued[i], loaded.Report, Array.Empty<ZoneDefinition>());
                if (loaded.Zone.Id != queued[i])
                {
                    var wrong = new ValidationReport();
                    wrong.Add("$.data.id", ValidationCode.InvalidValue, $"le fichier de « {queued[i]} » décrit « {loaded.Zone.Id} ».");
                    return new WorldZonesLoadResult(queued[i], wrong, Array.Empty<ZoneDefinition>());
                }
                zones.Add(loaded.Zone);
                for (int d = 0; d < loaded.Zone.Doors.Count; d++)
                {
                    Door door = loaded.Zone.Doors[d];
                    if (queued.Contains(door.To)) continue;
                    queued.Add(door.To);
                    via.Add(door.To, (loaded.Zone.Id, d, door.Id));
                }
            }
            ValidationReport links = ZoneValidator.CheckDoors(zones);
            if (links.IsValid) return new WorldZonesLoadResult(start, links, zones);
            // Relecture indépendante : le rapport est celui de la première zone dont une porte est en cause (chemins de son fichier).
            foreach (ZoneDefinition zone in zones)
            {
                ValidationReport own = ZoneValidator.CheckDoorsOf(zone, zones);
                if (!own.IsValid) return new WorldZonesLoadResult(zone.Id, own, Array.Empty<ZoneDefinition>());
            }
            return new WorldZonesLoadResult(start, links, Array.Empty<ZoneDefinition>());
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

        private static RoadData? ReadRoad(JsonInput input, JsonElement element, string path)
        {
            var road = new RoadData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => road.Id = input.Text(v, p),
                ["kind"] = (v, p) => road.Kind = input.Text(v, p),
                ["width"] = (v, p) => road.Width = input.Decimal(v, p),
                ["points"] = (v, p) => road.Points = input.ReadLimitedList(v, p, ZoneValidator.MaxRoadPoints,
                    n => $"{n} points, au plus {ZoneValidator.MaxRoadPoints}.", (e, ip) => ScenarioJsonReader.ReadPoint(input, e, ip)),
            });
            return ok ? road : null;
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
