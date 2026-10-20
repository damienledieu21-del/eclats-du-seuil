using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Stats;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

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
        /// <summary>
        /// Version 1 : lots 5 et 6 (assaillant, actions, ajouts compatibles). Version 2 (lot 7.2) : <c>applies</c> remplace
        /// <c>appliesFragility</c> sur les attaques d'assaillant, <c>statusResistance</c> facultatif sur mannequins et assaillants.
        /// Lot 7.3 : <c>triggers</c> facultatif sur un assaillant (ajout compatible, version inchangée). Version 3 (sous-lot 8.3) :
        /// <c>applies[].target</c> sur les attaques et rôle <c>ally</c> des déclencheurs d'assaillant. Version 4 (sous-lot 11.1) : <c>health</c>
        /// facultatif sur un assaillant (PV d'un assaillant mortel ; absent : PV infinis de l'entraînement). Version 5 (sous-lot 11.4a) :
        /// combat positionné, bloc <c>space</c> facultatif (aire <c>bounds</c>, <c>obstacles</c> convexes) ; avec lui, <c>position</c> et
        /// <c>radius</c> obligatoires et <c>facing</c> facultatif sur chaque combattant, <c>speed</c> facultatif sur un assaillant ; actions
        /// <c>move</c> (avec <c>direction</c>) et <c>stop</c>. Version 6 (sous-lot 11.4c) : forme, annonce, dimensions et défenses des attaques
        /// d'assaillant (<c>shape</c>, <c>telegraphMs</c>, <c>range</c>, <c>radius</c>, <c>angle</c>, <c>width</c>, <c>center</c>,
        /// <c>persistentMs</c>, <c>tickMs</c>, <c>guardable</c>, <c>parryable</c>) et action <c>select</c> (avec <c>target</c> facultatif).
        /// Une version antérieure est refusée sans migration : le fichier se met à jour à la main.
        /// </summary>
        public const int CurrentSchemaVersion = 6;

        public static IReadOnlyList<string> CombatantTypes { get; } = Array.AsReadOnly(new[] { "character", "dummy", "assailant" });

        /// <summary>Champs propres à chaque type de combattant ; un champ d'un autre type est refusé.</summary>
        private static readonly Dictionary<string, string[]> TypeFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["character"] = new[] { "class", "build" },
            ["dummy"] = new[] { "armor", "magicResistance", "statusResistance" },
            ["assailant"] = new[] { "health", "armor", "magicResistance", "statusResistance", "target", "firstAttackMs", "intervalMs", "attacks", "triggers", "speed" },
        };

        /// <summary>Sous-lot 11.4 : champs de la place de départ, réservés aux combats positionnés (bloc <c>space</c>).</summary>
        private static readonly string[] PlacementFields = { "position", "facing", "radius", "speed" };

        public static ScenarioLoadResult Validate(ScenarioDocument document, GameData data, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (data is null) throw new ArgumentNullException(nameof(data));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new ScenarioLoadResult(report, null);
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
                    RuleViolationKind.NumericLimit => ValidationCode.OutOfRange,
                    RuleViolationKind.LimitExceeded => ValidationCode.LimitExceeded,
                    _ => ValidationCode.InvalidValue,
                };
                foreach (string field in violation.Fields) report.Add(root + "." + field, code, violation.Message);
            }
            if (report.IsValid)
                foreach (RuleViolation warning in setup.TriggerWarnings())
                    foreach (string field in warning.Fields) report.AddWarning(root + "." + field, warning.Message);
            return new ScenarioLoadResult(report, report.IsValid ? setup : null);
        }

        private static CombatSetup Build(ScenarioData scenario, GameData data)
        {
            var combatants = new List<CombatantSetup>();
            var usedClasses = new SortedDictionary<string, LoadedClass>(StringComparer.Ordinal);
            foreach (CombatantData c in scenario.Combatants!.Select(c => c!))
            {
                // Sous-lot 11.4 : place de départ d'un combat positionné (la forme est vérifiée : position et rayon présents avec space).
                CombatantPlacement? placement = c.Position is { } position
                    ? new CombatantPlacement(Point(position), c.Radius!.Value, c.Facing is { } facing ? Point(facing) : (GroundVector?)null, c.Speed)
                    : null;
                if (c.Type == "character")
                {
                    data.TryGetClass(c.Class!, out LoadedClass loaded);
                    usedClasses[loaded.Definition.Id] = loaded;
                    CharacterBuild build = Builds.BuildShape.ToCharacterBuild(loaded.Definition.Id, c.Build!);
                    combatants.Add(new CharacterSetup(c.Id!, c.Team!, loaded.Definition, build, c.Initiative!.Value, placement));
                }
                else if (c.Type == "assailant")
                {
                    var attacks = c.Attacks!.Select(a => new AssailantAttack(a!.Id!, a.RawDamage!.Value, ClassValidator.DamageTypes[a.DamageType!],
                        ClassValidator.Targetings[a.Targeting!], a.Unblockable!.Value, ClassValidator.Applications(a.Applies!),
                        a.Shape is null ? null : AttackShapeIds.Shapes[a.Shape], a.TelegraphMs ?? 0, a.Range, a.Radius, a.Angle, a.Width,
                        a.Center is null ? ShapeCenter.Target : AttackShapeIds.Centers[a.Center], a.PersistentMs ?? 0, a.TickMs ?? 0, a.Guardable, a.Parryable));
                    combatants.Add(new AssailantSetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Target,
                        c.FirstAttackMs!.Value, c.IntervalMs!.Value, attacks, c.Initiative!.Value, c.StatusResistance ?? Fixed.Zero,
                        Triggers.TriggerShape.Build(c.Triggers), c.Health, placement));
                }
                else
                {
                    combatants.Add(new DummySetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Initiative!.Value, c.StatusResistance ?? Fixed.Zero,
                        placement));
                }
            }
            var commands = scenario.Commands!.Select(c => !CombatActionIds.TryParseNamed(c!.Action, out CombatAction action)
                ? new CombatCommand(c.AtMs!.Value, c.Actor!, c.Skill!, c.Target)
                : action == CombatAction.Move
                    ? new CombatCommand(c.AtMs!.Value, c.Actor!, Point(c.Direction!))
                    : action == CombatAction.Select   // sous-lot 11.4c
                        ? CombatCommand.SelectTarget(c.AtMs!.Value, c.Actor!, c.Target)
                        : new CombatCommand(c.AtMs!.Value, c.Actor!, action));
            CombatSpace? space = scenario.Space is { } s
                ? new CombatSpace(Point(s.Bounds!.Min!), Point(s.Bounds.Max!),
                    (s.Obstacles ?? new List<ObstacleData?>()).Select(o => new SpaceObstacle(o!.Id!, o.Points!.Select(p => Point(p!)))))
                : null;
            var identity = new CombatDataIdentity(data.RulesIdentity, usedClasses.Values.Select(GameData.IdentityOf));
            var rules = data.Rules;
            return new CombatSetup(scenario.Seed!.Value, scenario.DurationMs!.Value, rules.Stats, rules.Damage, rules.Progression, rules.Combat,
                combatants, commands, identity, space);
        }

        private static GroundVector Point(PointData p) => new GroundVector(p.X!.Value, p.Z!.Value);

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

            /// <summary>Sous-lot 11.4 : le scénario a-t-il un bloc <c>space</c> (combat positionné) ?</summary>
            private bool _spatial;

            public void Check(ScenarioData? scenario, string root)
            {
                if (!Present(scenario, root, "objet décrivant le scénario")) return;
                Present(scenario!.Seed, root + ".seed", "entier positif ou nul");
                Present(scenario.DurationMs, root + ".durationMs", "durée en millisecondes");
                _spatial = scenario.Present.Contains("space");
                if (_spatial && Present(scenario.Space, root + ".space", "espace du combat : bounds, obstacles"))
                    CheckSpace(scenario.Space!, root + ".space");
                Items(scenario.Combatants, root + ".combatants", "liste des combattants", CheckCombatant);
                Items(scenario.Commands, root + ".commands", "liste des commandes, éventuellement vide", CheckCommand);
            }

            private void CheckSpace(SpaceData space, string path)
            {
                if (Present(space.Bounds, path + ".bounds", "aire praticable : min et max"))
                {
                    Present(space.Bounds!.Min, path + ".bounds.min", "coin sud-ouest [x, z]");
                    Present(space.Bounds.Max, path + ".bounds.max", "coin nord-est [x, z]");
                }
                if (space.Obstacles is null) return;   // facultatif : aucun obstacle
                Items(space.Obstacles, path + ".obstacles", "liste des obstacles", (o, p) =>
                {
                    Present(o.Id, p + ".id", "identifiant de l'obstacle");
                    Items(o.Points, p + ".points", "sommets [x, z] du polygone convexe", (_, _) => { });
                });
            }

            private void CheckCombatant(CombatantData c, string path)
            {
                // Sous-lot 11.4 : place de départ obligatoire dans un combat positionné, interdite sans espace.
                if (_spatial)
                {
                    Present(c.Position, path + ".position", "centre de l'emprise [x, z], en mètres");
                    Present(c.Radius, path + ".radius", "rayon de l'emprise, en mètres");
                }
                else
                    foreach (string field in PlacementFields.Where(c.Present.Contains))
                        _report.Add($"{path}.{field}", ValidationCode.InvalidValue, "champ réservé aux combats positionnés (bloc space).");
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
                        if (Present(c.Build, path + ".build", "build du personnage")) Builds.BuildShape.Check(c.Build!, path + ".build", _report);
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
                        Triggers.TriggerShape.Check(c.Triggers, path + ".triggers", _report);
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
                // Sous-lot 8.3 : une attaque d'assaillant frappe : enemy ou allEnemies seulement (les ciblages de soutien sont aux compétences).
                if (Present(a.Targeting, path + ".targeting", "enemy ou allEnemies") && !ClassValidator.AttackTargetings.Contains(a.Targeting!))
                    _report.Add(path + ".targeting", ValidationCode.UnknownReference,
                        $"valeur inconnue « {a.Targeting} » ; valeurs possibles : {string.Join(", ", ClassValidator.AttackTargetings)}.");
                Present(a.Unblockable, path + ".unblockable", "booléen");
                ClassValidator.CheckApplications(a.Applies, path + ".applies", _report);
                // Sous-lot 11.4c : identifiants de forme et de centre ; la cohérence est jugée par CombatSetup.Validate.
                if (a.Shape != null && !AttackShapeIds.Shapes.ContainsKey(a.Shape))
                    _report.Add(path + ".shape", ValidationCode.UnknownReference,
                        $"valeur inconnue « {a.Shape} » ; valeurs possibles : {string.Join(", ", AttackShapeIds.Shapes.Keys)}.");
                if (a.Center != null && !AttackShapeIds.Centers.ContainsKey(a.Center))
                    _report.Add(path + ".center", ValidationCode.UnknownReference,
                        $"valeur inconnue « {a.Center} » ; valeurs possibles : {string.Join(", ", AttackShapeIds.Centers.Keys)}.");
            }

            private void CheckCommand(CommandData c, string path)
            {
                Present(c.AtMs, path + ".atMs", "instant en millisecondes");
                Present(c.Actor, path + ".actor", "identifiant du combattant");
                string actions = string.Join(", ", CombatActionIds.Named);
                if (!c.Present.Contains("action"))
                {
                    Present(c.Skill, path + ".skill", "identifiant de compétence, ou champ action (" + actions + ")");
                    if (c.Present.Contains("direction"))
                        _report.Add(path + ".direction", ValidationCode.InvalidValue, "seul un déplacement (action move) a une direction.");
                    return;
                }
                CombatAction action = default;
                if (Present(c.Action, path + ".action", actions) && !CombatActionIds.TryParseNamed(c.Action, out action))
                {
                    _report.Add(path + ".action", ValidationCode.UnknownReference, $"action inconnue « {c.Action} » ; valeurs possibles : {actions}.");
                    return;
                }
                // Sous-lot 11.4c : une sélection prend une cible (ou aucune, pour effacer la sélection), pas de compétence.
                foreach (string field in new[] { "skill", "target" }.Where(c.Present.Contains))
                    if (!(field == "target" && action == CombatAction.Select))
                        _report.Add($"{path}.{field}", ValidationCode.InvalidValue, action == CombatAction.Select
                            ? "une sélection ne nomme pas de compétence."
                            : "une garde, une parade ou un déplacement ne prend ni compétence ni cible.");
                // Sous-lot 11.4 : une direction pour un déplacement, et seulement pour lui.
                if (c.Action is null) return;
                if (action == CombatAction.Move) Present(c.Direction, path + ".direction", "direction de marche [x, z], composantes dans [−1, 1]");
                else if (c.Present.Contains("direction"))
                    _report.Add(path + ".direction", ValidationCode.InvalidValue, "seul un déplacement (action move) a une direction.");
            }
        }
    }
}
