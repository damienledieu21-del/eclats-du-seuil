using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EdS.Data.Json.Text;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;

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
                ["armor"] = Track("armor", (v, p) => c.Armor = input.Decimal(v, p)),
                ["magicResistance"] = Track("magicResistance", (v, p) => c.MagicResistance = input.Decimal(v, p)),
                ["statusResistance"] = Track("statusResistance", (v, p) => c.StatusResistance = input.Decimal(v, p)),
                ["target"] = Track("target", (v, p) => c.Target = input.NullableText(v, p)),
                ["firstAttackMs"] = Track("firstAttackMs", (v, p) => c.FirstAttackMs = input.Int64(v, p)),
                ["intervalMs"] = Track("intervalMs", (v, p) => c.IntervalMs = input.Int64(v, p)),
                ["attacks"] = Track("attacks", (v, p) => c.Attacks = input.ReadList(v, p, (e, ip) => ReadAttack(input, e, ip))),
                ["triggers"] = Track("triggers", (v, p) => c.Triggers = TriggerJsonReader.ReadList(input, v, p)),
            });
            return ok ? c : null;
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
        private static List<KeyValuePair<string, int?>>? ReadIntegerMap(JsonInput input, JsonElement element, string path)
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
            });
            return ok ? c : null;
        }
    }
}
