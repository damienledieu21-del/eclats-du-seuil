using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EdS.Data.Json.Text;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Data.Json
{
    /// <summary>
    /// Adaptateur System.Text.Json pour les scénarios (<c>kind: "scenario"</c>). Il ne vérifie que la forme ;
    /// tout le reste est délégué au validateur commun <see cref="ScenarioValidator"/>.
    /// </summary>
    public static class ScenarioJsonReader
    {
        public static ScenarioLoadResult ReadFile(string path, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), data, report);
        }

        public static ScenarioLoadResult Read(string json, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), data, report);
        }

        private static ScenarioLoadResult Read(byte[]? json, GameData data, ValidationReport report)
        {
            if (json is null) return ScenarioValidator.Validate(new ScenarioDocument(), data, report);
            var document = new ScenarioDocument();
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data", data.Rules.Combat));
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return ScenarioValidator.Validate(document, data, report);
        }

        /// <summary>
        /// Lit le contenu d'un scénario ; sert aussi pour le scénario recopié dans l'en-tête d'un journal. Lot 7.1 : les
        /// tableaux de combattants et de commandes sont comparés aux limites configurées <b>avant</b> la construction de
        /// leurs éléments ; un tableau trop long donne une seule erreur, avec le même message que l'admission du domaine.
        /// </summary>
        internal static ScenarioData? ReadData(JsonInput input, JsonElement element, string path, CombatRules limits)
        {
            var data = new ScenarioData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["seed"] = (v, p) => data.Seed = input.Int64(v, p),
                ["durationMs"] = (v, p) => data.DurationMs = input.Int64(v, p),
                ["zone"] = (v, p) => data.Zone = input.Boolean(v, p),   // sous-lot 12.1 (schéma 10)
                ["space"] = (v, p) =>   // sous-lot 11.4 (schéma 5)
                {
                    data.Present.Add("space");
                    data.Space = ReadSpace(input, v, p);
                },
                ["combatants"] = (v, p) => data.Combatants = input.ReadLimitedList(v, p, limits.MaxCombatants,
                    n => $"{n} combattants, au plus {limits.MaxCombatants} (combat.limits.maxCombatants).", (e, ip) => ReadCombatant(input, e, ip)),
                ["commands"] = (v, p) => data.Commands = input.ReadLimitedList(v, p, limits.MaxCommands,
                    n => $"{n} commandes, au plus {limits.MaxCommands} (combat.limits.maxCommands).", (e, ip) => ReadCommand(input, e, ip)),
            });
            return ok ? data : null;
        }

        private static CombatantData? ReadCombatant(JsonInput input, JsonElement element, string path)
        {
            var c = new CombatantData();
            Action<JsonElement, string> Track(string name, Action<JsonElement, string> read) => (v, p) =>
            {
                c.Present.Add(name);
                read(v, p);
            };
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => c.Id = input.Text(v, p),
                ["type"] = (v, p) => c.Type = input.Text(v, p),
                ["team"] = (v, p) => c.Team = input.Text(v, p),
                ["initiative"] = (v, p) => c.Initiative = input.Int32(v, p),
                ["class"] = Track("class", (v, p) => c.Class = input.Text(v, p)),
                ["build"] = Track("build", (v, p) => c.Build = ReadBuild(input, v, p)),
                ["gear"] = Track("gear", (v, p) => c.Gear = ReadIntegerMap(input, v, p)),   // sous-lot 12.3 (schéma 11)
                ["armor"] = Track("armor", (v, p) => c.Armor = input.Decimal(v, p)),
                ["magicResistance"] = Track("magicResistance", (v, p) => c.MagicResistance = input.Decimal(v, p)),
                ["statusResistance"] = Track("statusResistance", (v, p) => c.StatusResistance = input.Decimal(v, p)),
                ["health"] = Track("health", (v, p) => c.Health = input.Int64(v, p)),   // sous-lot 11.1 (schéma 4)
                ["target"] = Track("target", (v, p) => c.Target = input.NullableText(v, p)),
                ["firstAttackMs"] = Track("firstAttackMs", (v, p) => c.FirstAttackMs = input.Int64(v, p)),
                ["intervalMs"] = Track("intervalMs", (v, p) => c.IntervalMs = input.Int64(v, p)),
                ["attacks"] = Track("attacks", (v, p) => c.Attacks = input.ReadList(v, p, (e, ip) => ReadAttack(input, e, ip))),
                ["triggers"] = Track("triggers", (v, p) => c.Triggers = TriggerJsonReader.ReadList(input, v, p)),
                // Sous-lot 11.4 (schéma 5) : place de départ.
                ["position"] = Track("position", (v, p) => c.Position = ReadPoint(input, v, p)),
                ["facing"] = Track("facing", (v, p) => c.Facing = ReadPoint(input, v, p)),
                ["radius"] = Track("radius", (v, p) => c.Radius = input.Decimal(v, p)),
                ["speed"] = Track("speed", (v, p) => c.Speed = input.Decimal(v, p)),
                ["arrivalMs"] = Track("arrivalMs", (v, p) => c.ArrivalMs = input.Int64(v, p)),   // sous-lot 11.4d (schéma 7)
                ["behavior"] = Track("behavior", (v, p) => c.Behavior = ReadBehavior(input, v, p)),   // sous-lot 11.5 (schéma 8)
            });
            return ok ? c : null;
        }

        /// <summary>Sous-lot 11.5 : comportement d'un assaillant.</summary>
        private static BehaviorData? ReadBehavior(JsonInput input, JsonElement element, string path)
        {
            var b = new BehaviorData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["maxRange"] = (v, p) => b.MaxRange = input.Decimal(v, p),
                ["minRange"] = (v, p) => b.MinRange = input.Decimal(v, p),
                ["aggroRange"] = (v, p) => b.AggroRange = input.Decimal(v, p),
                ["encircle"] = (v, p) => b.Encircle = input.Boolean(v, p),
                ["pack"] = (v, p) => b.Pack = input.Boolean(v, p),   // sous-lot 11.9 (schéma 9)
            });
            return ok ? b : null;
        }

        /// <summary>Sous-lot 11.4 : point ou direction, tableau d'exactement deux nombres <c>[x, z]</c>.</summary>
        private static PointData? ReadPoint(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Array, path, "tableau [x, z]")) return null;
            IReadOnlyList<JsonElement> items = element.EnumerateArray();
            if (items.Count != 2)
            {
                input.Report.Add(path, ValidationCode.WrongType, $"tableau de deux nombres [x, z] attendu ({items.Count} valeur(s)).", input.LineOf(path));
                return null;
            }
            // Une coordonnée invalide est signalée sur le point lui-même : un seul message, pas de « champ absent » en plus.
            var point = new PointData { X = input.Decimal(items[0], path), Z = input.Decimal(items[1], path) };
            return point.X is null || point.Z is null ? null : point;
        }

        private static SpaceData? ReadSpace(JsonInput input, JsonElement element, string path)
        {
            var space = new SpaceData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["bounds"] = (v, p) => space.Bounds = ReadBounds(input, v, p),
                ["obstacles"] = (v, p) => space.Obstacles = input.ReadLimitedList(v, p, SpaceLimits.MaxObstacles,
                    n => $"{n} obstacles, au plus {SpaceLimits.MaxObstacles}.", (e, ip) => ReadObstacle(input, e, ip)),
            });
            return ok ? space : null;
        }

        private static BoundsData? ReadBounds(JsonInput input, JsonElement element, string path)
        {
            var bounds = new BoundsData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["min"] = (v, p) => bounds.Min = ReadPoint(input, v, p),
                ["max"] = (v, p) => bounds.Max = ReadPoint(input, v, p),
            });
            return ok ? bounds : null;
        }

        private static ObstacleData? ReadObstacle(JsonInput input, JsonElement element, string path)
        {
            var obstacle = new ObstacleData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => obstacle.Id = input.Text(v, p),
                ["points"] = (v, p) => obstacle.Points = input.ReadLimitedList(v, p, SpaceLimits.MaxVertices,
                    n => $"{n} sommets, au plus {SpaceLimits.MaxVertices}.", (e, ip) => ReadPoint(input, e, ip)),
                ["low"] = (v, p) => obstacle.Low = input.Boolean(v, p),   // sous-lot 11.9 (schéma 9)
            });
            return ok ? obstacle : null;
        }

        internal static BuildData? ReadBuild(JsonInput input, JsonElement element, string path)
        {
            var b = new BuildData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["level"] = (v, p) => b.Level = input.Int32(v, p),
                ["attributes"] = (v, p) => b.Attributes = ReadIntegerMap(input, v, p),
                ["talents"] = (v, p) => b.Talents = ReadIntegerMap(input, v, p),
                ["activeSkills"] = (v, p) => b.ActiveSkills = input.ReadList(v, p, (e, ip) => input.Text(e, ip)),
                ["ultimate"] = (v, p) => b.Ultimate = input.NullableText(v, p),
                ["passiveSkills"] = (v, p) => b.PassiveSkills = input.ReadList(v, p, (e, ip) => input.Text(e, ip)),
            });
            return ok ? b : null;
        }

        /// <summary>Objet « identifiant → entier » dont les clés sont libres (attributs, talents) ; ordre du fichier conservé.</summary>
        internal static List<KeyValuePair<string, int?>>? ReadIntegerMap(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Object, path, "objet")) return null;
            var map = new List<KeyValuePair<string, int?>>();
            foreach (JsonProperty property in input.UniqueProperties(element, path))
                map.Add(new KeyValuePair<string, int?>(property.Name, input.Int32(property.Value, path + "." + property.Name)));
            return map;
        }

        private static AttackData? ReadAttack(JsonInput input, JsonElement element, string path)
        {
            var a = new AttackData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => a.Id = input.Text(v, p),
                ["rawDamage"] = (v, p) => a.RawDamage = input.Int64(v, p),
                ["damageType"] = (v, p) => a.DamageType = input.Text(v, p),
                ["targeting"] = (v, p) => a.Targeting = input.Text(v, p),
                ["unblockable"] = (v, p) => a.Unblockable = input.Boolean(v, p),
                ["applies"] = (v, p) => a.Applies = input.ReadList(v, p, (e, ip) => ClassJsonReader.ReadApplication(input, e, ip)),
                // Sous-lot 11.4c (schéma 6) : forme, annonce, dimensions et défenses.
                ["shape"] = (v, p) => a.Shape = input.Text(v, p),
                ["telegraphMs"] = (v, p) => a.TelegraphMs = input.Int64(v, p),
                ["range"] = (v, p) => a.Range = input.Decimal(v, p),
                ["radius"] = (v, p) => a.Radius = input.Decimal(v, p),
                ["angle"] = (v, p) => a.Angle = input.Int32(v, p),
                ["width"] = (v, p) => a.Width = input.Decimal(v, p),
                ["center"] = (v, p) => a.Center = input.Text(v, p),
                ["persistentMs"] = (v, p) => a.PersistentMs = input.Int64(v, p),
                ["tickMs"] = (v, p) => a.TickMs = input.Int64(v, p),
                ["guardable"] = (v, p) => a.Guardable = input.Boolean(v, p),
                ["parryable"] = (v, p) => a.Parryable = input.Boolean(v, p),
                ["charge"] = (v, p) => a.Charge = input.Boolean(v, p),   // sous-lot 11.5 (schéma 8)
                ["push"] = (v, p) => a.Push = input.Decimal(v, p),   // sous-lot 11.9 (schéma 9)
                ["holdPosition"] = (v, p) => a.HoldPosition = input.Boolean(v, p),
            });
            return ok ? a : null;
        }

        private static CommandData? ReadCommand(JsonInput input, JsonElement element, string path)
        {
            var c = new CommandData();
            Action<JsonElement, string> Track(string name, Action<JsonElement, string> read) => (v, p) =>
            {
                c.Present.Add(name);
                read(v, p);
            };
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["atMs"] = (v, p) => c.AtMs = input.Int64(v, p),
                ["actor"] = (v, p) => c.Actor = input.Text(v, p),
                ["skill"] = Track("skill", (v, p) => c.Skill = input.Text(v, p)),
                ["action"] = Track("action", (v, p) => c.Action = input.Text(v, p)),
                ["target"] = Track("target", (v, p) => c.Target = input.NullableText(v, p)),
                ["direction"] = Track("direction", (v, p) => c.Direction = ReadPoint(input, v, p)),   // sous-lot 11.4
                ["backward"] = Track("backward", (v, p) => c.Backward = input.Boolean(v, p)),       // sous-lot 11.9 (schéma 9)
            });
            return ok ? c : null;
        }
    }
}
