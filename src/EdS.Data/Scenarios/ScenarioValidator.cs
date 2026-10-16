using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Stats;
using EdS.Domain.Combat;

namespace EdS.Data.Scenarios
{
    /// <summary>Résultat d'un chargement de scénario : un combat prêt à simuler, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class ScenarioLoadResult
    {
        public ValidationReport Report { get; }
        public CombatSetup? Setup { get; }
        public bool IsValid => Setup != null;

        internal ScenarioLoadResult(ValidationReport report, CombatSetup? setup)
        {
            Report = report;
            Setup = setup;
        }
    }

    /// <summary>
    /// Validateur commun des scénarios : en-tête, présence des champs, références aux classes chargées, puis
    /// conversion en <see cref="CombatSetup"/> dont <see cref="CombatSetup.Validate"/> vérifie la cohérence
    /// (durées, identifiants, builds). Les règles et les classes viennent des données chargées.
    /// </summary>
    public static class ScenarioValidator
    {
        public const string Kind = "scenario";
        public const int CurrentSchemaVersion = 1;

        public static IReadOnlyList<string> CombatantTypes { get; } = new[] { "character", "dummy", "assailant" };

        /// <summary>Champs propres à chaque type de combattant ; un champ d'un autre type est refusé.</summary>
        private static readonly Dictionary<string, string[]> TypeFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["character"] = new[] { "class", "build" },
            ["dummy"] = new[] { "armor", "magicResistance" },
            ["assailant"] = new[] { "armor", "magicResistance", "target", "firstAttackMs", "intervalMs", "attacks" },
        };

        private static readonly Dictionary<string, AttributeKind> AttributeIds =
            ((AttributeKind[])Enum.GetValues(typeof(AttributeKind))).ToDictionary(Rules.RulesCatalog.Id, k => k, StringComparer.Ordinal);

        public static ScenarioLoadResult Validate(ScenarioDocument document, GameData data, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (data is null) throw new ArgumentNullException(nameof(data));
            report ??= new ValidationReport();
            HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report);
            return ValidateData(document.Data, data, "$.data", report);
        }

        /// <summary>Valide le contenu d'un scénario situé à <paramref name="root"/> (« $.data » dans un fichier, « $.scenario » dans un journal).</summary>
        public static ScenarioLoadResult ValidateData(ScenarioData? scenario, GameData data, string root, ValidationReport report)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (report is null) throw new ArgumentNullException(nameof(report));
            new ShapeChecker(report, data).Check(scenario, root);
            if (!report.IsValid) return new ScenarioLoadResult(report, null);

            CombatSetup setup = Build(scenario!, data);
            foreach (RuleViolation violation in setup.Validate())
            {
                ValidationCode code = violation.Kind switch
                {
                    RuleViolationKind.UnknownReference => ValidationCode.UnknownReference,
                    RuleViolationKind.DuplicateId => ValidationCode.DuplicateId,
                    _ => ValidationCode.InvalidValue,
                };
                foreach (string field in violation.Fields) report.Add(root + "." + field, code, violation.Message);
            }
            return new ScenarioLoadResult(report, report.IsValid ? setup : null);
        }

        private static CombatSetup Build(ScenarioData scenario, GameData data)
        {
            var combatants = new List<CombatantSetup>();
            var usedClasses = new SortedDictionary<string, LoadedClass>(StringComparer.Ordinal);
            foreach (CombatantData c in scenario.Combatants!.Select(c => c!))
            {
                if (c.Type == "character")
                {
                    data.TryGetClass(c.Class!, out LoadedClass loaded);
                    usedClasses[loaded.Definition.Id] = loaded;
                    BuildData b = c.Build!;
                    var values = b.Attributes!.ToDictionary(a => AttributeIds[a.Key], a => a.Value!.Value);
                    var attributes = new Attributes(values[AttributeKind.Force], values[AttributeKind.Dexterite], values[AttributeKind.Intelligence],
                        values[AttributeKind.Vitalite], values[AttributeKind.Volonte], values[AttributeKind.Endurance]);
                    var talents = b.Talents!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal);
                    var build = new CharacterBuild(loaded.Definition.Id, b.Level!.Value, attributes, talents,
                        b.ActiveSkills!.Select(s => s!).ToArray(), b.Ultimate, b.PassiveSkills!.Select(s => s!).ToArray());
                    combatants.Add(new CharacterSetup(c.Id!, c.Team!, loaded.Definition, build, c.Initiative!.Value));
                }
                else if (c.Type == "assailant")
                {
                    var attacks = c.Attacks!.Select(a => new AssailantAttack(a!.Id!, a.RawDamage!.Value, ClassValidator.DamageTypes[a.DamageType!],
                        ClassValidator.Targetings[a.Targeting!], a.Unblockable!.Value, a.AppliesFragility!.Value));
                    combatants.Add(new AssailantSetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Target,
                        c.FirstAttackMs!.Value, c.IntervalMs!.Value, attacks, c.Initiative!.Value));
                }
                else
                {
                    combatants.Add(new DummySetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Initiative!.Value));
                }
            }
            var commands = scenario.Commands!.Select(c => CombatActionIds.TryParseDefensive(c!.Action, out CombatAction action)
                ? new CombatCommand(c.AtMs!.Value, c.Actor!, action)
                : new CombatCommand(c.AtMs!.Value, c.Actor!, c.Skill!, c.Target));
            var identity = new CombatDataIdentity(data.RulesIdentity, usedClasses.Values.Select(GameData.IdentityOf));
            var rules = data.Rules;
            return new CombatSetup(scenario.Seed!.Value, scenario.DurationMs!.Value, rules.Stats, rules.Damage, rules.Progression, rules.Combat,
                combatants, commands, identity);
        }

        private sealed class ShapeChecker
        {
            private readonly ValidationReport _report;
            private readonly GameData _data;

            public ShapeChecker(ValidationReport report, GameData data)
            {
                _report = report;
                _data = data;
            }

            private bool Present(object? value, string path, string description)
            {
                if (value != null) return true;
                if (!_report.HasErrorAtOrAbove(path))
                    _report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
                return false;
            }

            private void Items<T>(List<T?>? list, string path, string description, Action<T, string> check) where T : class
            {
                if (!Present(list, path, description)) return;
                for (int i = 0; i < list!.Count; i++)
                {
                    string itemPath = $"{path}[{i}]";
                    if (list[i] is { } item) check(item, itemPath);
                    else if (!_report.HasErrorAtOrAbove(itemPath)) _report.Add(itemPath, ValidationCode.WrongType, "valeur attendue.");
                }
            }

            public void Check(ScenarioData? scenario, string root)
            {
                if (!Present(scenario, root, "objet décrivant le scénario")) return;
                Present(scenario!.Seed, root + ".seed", "entier positif ou nul");
                Present(scenario.DurationMs, root + ".durationMs", "durée en millisecondes");
                Items(scenario.Combatants, root + ".combatants", "liste des combattants", CheckCombatant);
                Items(scenario.Commands, root + ".commands", "liste des commandes, éventuellement vide", CheckCommand);
            }

            private void CheckCombatant(CombatantData c, string path)
            {
                Present(c.Id, path + ".id", "identifiant du combattant");
                Present(c.Team, path + ".team", "équipe");
                Present(c.Initiative, path + ".initiative", "entier");
                if (!Present(c.Type, path + ".type", "character, dummy ou assailant")) return;
                if (!TypeFields.TryGetValue(c.Type!, out string[] own))
                {
                    _report.Add(path + ".type", ValidationCode.UnknownReference,
                        $"type de combattant inconnu « {c.Type} » ; valeurs possibles : {string.Join(", ", CombatantTypes)}.");
                    return;
                }
                foreach (string field in TypeFields.Values.SelectMany(f => f).Distinct().Where(f => !own.Contains(f) && c.Present.Contains(f)))
                    _report.Add($"{path}.{field}", ValidationCode.InvalidValue,
                        $"champ réservé aux types {string.Join(", ", TypeFields.Where(t => t.Value.Contains(field)).Select(t => t.Key))}.");

                switch (c.Type)
                {
                    case "character":
                        if (Present(c.Class, path + ".class", "identifiant de classe") && !_data.TryGetClass(c.Class!, out _))
                            _report.Add(path + ".class", ValidationCode.UnknownReference,
                                $"classe inconnue « {c.Class} » ; classes chargées : {string.Join(", ", _data.Classes.Select(x => x.Definition.Id))}.");
                        if (Present(c.Build, path + ".build", "build du personnage")) CheckBuild(c.Build!, path + ".build");
                        break;
                    case "dummy":
                        Present(c.Armor, path + ".armor", "nombre décimal");
                        Present(c.MagicResistance, path + ".magicResistance", "nombre décimal");
                        break;
                    default:
                        Present(c.Armor, path + ".armor", "nombre décimal");
                        Present(c.MagicResistance, path + ".magicResistance", "nombre décimal");
                        if (!c.Present.Contains("target")) Present(null, path + ".target", "identifiant de la cible, ou null si toutes les attaques sont de zone");
                        Present(c.FirstAttackMs, path + ".firstAttackMs", "instant de la première attaque, en millisecondes");
                        Present(c.IntervalMs, path + ".intervalMs", "intervalle entre deux attaques, en millisecondes");
                        Items(c.Attacks, path + ".attacks", "liste des attaques, jouées dans l'ordre puis en boucle", CheckAttack);
                        break;
                }
            }

            private void CheckAttack(AttackData a, string path)
            {
                Present(a.Id, path + ".id", "identifiant de l'attaque");
                Present(a.RawDamage, path + ".rawDamage", "dégâts bruts entiers, avant défense");
                if (Present(a.DamageType, path + ".damageType", "physique ou magique") && !ClassValidator.DamageTypes.ContainsKey(a.DamageType!))
                    _report.Add(path + ".damageType", ValidationCode.UnknownReference,
                        $"valeur inconnue « {a.DamageType} » ; valeurs possibles : {string.Join(", ", ClassValidator.DamageTypes.Keys)}.");
                if (Present(a.Targeting, path + ".targeting", "enemy ou allEnemies") && !ClassValidator.Targetings.ContainsKey(a.Targeting!))
                    _report.Add(path + ".targeting", ValidationCode.UnknownReference,
                        $"valeur inconnue « {a.Targeting} » ; valeurs possibles : {string.Join(", ", ClassValidator.Targetings.Keys)}.");
                Present(a.Unblockable, path + ".unblockable", "booléen");
                Present(a.AppliesFragility, path + ".appliesFragility", "booléen");
            }

            private void CheckBuild(BuildData b, string path)
            {
                Present(b.Level, path + ".level", "niveau");
                if (Present(b.Attributes, path + ".attributes", "les six attributs"))
                {
                    foreach (var attribute in b.Attributes!)
                    {
                        string attributePath = path + ".attributes." + attribute.Key;
                        if (!AttributeIds.ContainsKey(attribute.Key))
                            _report.Add(attributePath, ValidationCode.UnknownReference,
                                $"attribut inconnu « {attribute.Key} » ; valeurs possibles : {string.Join(", ", AttributeIds.Keys)}.");
                        else if (attribute.Value is int value && value < 0)
                            _report.Add(attributePath, ValidationCode.InvalidValue, "un attribut ne peut pas être négatif.");
                        else
                            Present(attribute.Value, attributePath, "entier");
                    }
                    foreach (string id in AttributeIds.Keys.Where(id => b.Attributes.All(a => a.Key != id)))
                        Present(null, path + ".attributes." + id, "entier positif ou nul");
                }
                if (Present(b.Talents, path + ".talents", "rangs par talent, éventuellement vide"))
                    foreach (var talent in b.Talents!) Present(talent.Value, path + ".talents." + talent.Key, "rang");
                Items(b.ActiveSkills, path + ".activeSkills", "liste, éventuellement vide", (_, __) => { });
                Items(b.PassiveSkills, path + ".passiveSkills", "liste, éventuellement vide", (_, __) => { });
            }

            private void CheckCommand(CommandData c, string path)
            {
                Present(c.AtMs, path + ".atMs", "instant en millisecondes");
                Present(c.Actor, path + ".actor", "identifiant du combattant");
                if (!c.Present.Contains("action"))
                {
                    Present(c.Skill, path + ".skill", "identifiant de compétence, ou champ action (" + string.Join(", ", CombatActionIds.Defensive) + ")");
                    return;
                }
                if (Present(c.Action, path + ".action", string.Join(", ", CombatActionIds.Defensive)) && !CombatActionIds.TryParseDefensive(c.Action, out _))
                    _report.Add(path + ".action", ValidationCode.UnknownReference,
                        $"action inconnue « {c.Action} » ; valeurs possibles : {string.Join(", ", CombatActionIds.Defensive)}.");
                foreach (string field in new[] { "skill", "target" }.Where(c.Present.Contains))
                    _report.Add($"{path}.{field}", ValidationCode.InvalidValue, "une garde ou une parade ne prend ni compétence ni cible.");
            }
        }
    }
}
