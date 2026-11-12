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
        /// Version 7 (sous-lot 11.4d) : <c>arrivalMs</c> facultatif sur un combattant (arrivée en cours de combat) et action <c>leave</c>
        /// (départ, sans compétence ni cible). Version 8 (sous-lot 11.5) : <c>behavior</c> facultatif sur un assaillant (<c>maxRange</c>
        /// obligatoire, <c>minRange</c>, <c>aggroRange</c>, <c>encircle</c>) et <c>charge</c> facultatif sur une attaque. Version 9 (sous-lot
        /// 11.9, essai du 22 octobre 2026) : <c>low</c> facultatif sur un obstacle (bas : arrête la marche, pas la vue), <c>push</c> (poussée
        /// d'une bande) et <c>holdPosition</c> (arrêt pendant l'annonce) facultatifs sur une attaque, <c>pack</c> facultatif sur un comportement
        /// (entraîné par sa meute), <c>backward</c> facultatif sur un <c>move</c> (marche à reculons). Version 10 (sous-lot 12.1) :
        /// <c>zone</c> facultatif (simulation d'une zone du monde, sans fin quand une équipe tombe, durée jusqu'à 24 h). Version 11 (sous-lot 12.3) :
        /// <c>gear</c> facultatif sur un personnage (bonus de l'équipement porté, entiers de 0 à 10⁶). Version 12 (sous-lot 12.6) :
        /// <c>consumables</c> facultatif sur un personnage (objets de soin : <c>id</c>, <c>heal</c>, <c>charges</c>, <c>cooldownMs</c>) et action
        /// <c>consume</c> avec son <c>item</c>. Version 13 (sous-lot V1.2) : <c>shield</c> facultatif sur un personnage (garde du bouclier porté :
        /// <c>damageReduction</c>, <c>costPerBlock</c>, <c>parryWindowMs</c>, toutes obligatoires, bornées par <c>combat.shield</c> des
        /// règles). Version 14 (sous-lot V1.3) : <c>equipment</c> facultatif sur un personnage (effets de l'équipement porté : <c>id</c> de la
        /// source « item.… » ou « set.… », <c>modifiers</c> et <c>triggers</c>, bornés par <c>combat.equipment</c> des règles). Version 15 (sous-lot
        /// V1.8d) : action <c>gear</c> (« bonus modifié » d'un personnage) avec son <c>gear</c> (nouveau bonus d'équipement total, mêmes champs et
        /// bornes que celui d'un personnage). Version 16 (sous-lot V2.4, animaux paisibles) : <c>flee</c> facultatif sur un comportement (bête qui
        /// fuit, à <c>minRange</c> de sa cible). Version 17 (sous-lot V2.4.1, bêtes qui se promènent) : <c>wander</c> facultatif sur un comportement
        /// (<c>style</c>, <c>radius</c>, <c>pace</c>, <c>legMs</c>, <c>pauseMs</c> obligatoires ; <c>group</c>, <c>lead</c>, <c>spacing</c>,
        /// <c>points</c> facultatifs). Version 18 (sous-lot V2.26, secteurs de simulation) : <c>carried</c> facultatif sur un personnage ou un
        /// assaillant qui arrive en route (état emporté d'un autre secteur) ; coordonnées jusqu'à ±4 096 m et 256 obstacles ; une zone compte ses
        /// places (combattants présents au départ au plus <c>combat.limits.maxCombatants</c>, identités au plus 1 024). Une version antérieure
        /// est refusée sans migration : le fichier se met à jour à la main.
        /// </summary>
        public const int CurrentSchemaVersion = 18;

        public static IReadOnlyList<string> CombatantTypes { get; } = Array.AsReadOnly(new[] { "character", "dummy", "assailant" });

        /// <summary>Champs propres à chaque type de combattant ; un champ d'un autre type est refusé.</summary>
        private static readonly Dictionary<string, string[]> TypeFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["character"] = new[] { "class", "build", "gear", "consumables", "shield", "equipment", "carried" },
            ["dummy"] = new[] { "armor", "magicResistance", "statusResistance" },
            ["assailant"] = new[] { "health", "armor", "magicResistance", "statusResistance", "target", "firstAttackMs", "intervalMs", "attacks", "triggers", "speed", "behavior", "carried" },
        };

        /// <summary>Le champ propre à un type est-il renseigné dans l'objet (indépendamment du lecteur JSON) ? Une cible nulle vaut absente.</summary>
        private static bool IsSet(CombatantData c, string field) => field switch
        {
            "class" => c.Class != null,
            "build" => c.Build != null,
            "gear" => c.Gear != null,
            "consumables" => c.Consumables != null,
            "shield" => c.Shield != null,
            "equipment" => c.Equipment != null,
            "armor" => c.Armor != null,
            "magicResistance" => c.MagicResistance != null,
            "statusResistance" => c.StatusResistance != null,
            "health" => c.Health != null,
            "target" => c.Target != null,
            "firstAttackMs" => c.FirstAttackMs != null,
            "intervalMs" => c.IntervalMs != null,
            "attacks" => c.Attacks != null,
            "triggers" => c.Triggers != null,
            "speed" => c.Speed != null,
            "behavior" => c.Behavior != null,
            "carried" => c.Carried != null,   // sous-lot V2.26 (schéma 18)
            _ => false,
        };

        /// <summary>Sous-lot 11.4 : champs de la place de départ, réservés aux combats positionnés (bloc <c>space</c>).</summary>
        private static readonly string[] PlacementFields = { "position", "facing", "radius", "speed" };

        /// <summary>Sous-lot V2.26 (schéma 18) : état emporté du fichier (bornes vérifiées par la validation du combat, avec chemin).</summary>
        private static CarriedState? Carried(CarriedData? c) => c is null ? null : new CarriedState(c.Health, c.Endurance, c.EnduranceRegenMs ?? 0,
            c.Resonance ?? 0, c.RampartCharges ?? 0, c.GlobalCooldownMs ?? 0,
            c.Cooldowns?.Select(k => new KeyValuePair<string, long>(k.Key, k.Value ?? 0)), c.ConsumableCooldownMs ?? 0);

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

        /// <summary>Sous-lot 12.3 : bonus d'équipement d'un personnage de scénario (champs absents à 0).</summary>
        private static GearBonus Gear(List<KeyValuePair<string, int?>>? gear)
        {
            if (gear is null) return GearBonus.None;
            var values = new int[GearBonus.Fields.Count];
            foreach (KeyValuePair<string, int?> entry in gear) values[GearBonus.FieldIndex(entry.Key)] = entry.Value!.Value;
            return GearBonus.FromValues(values);
        }

        /// <summary>Sous-lot V2.4.1 : errance d'une bête (champs obligatoires et manière déjà vérifiés ; circuit borné).</summary>
        private static WanderBehavior BuildWander(WanderData w) =>
            new WanderBehavior(WanderBehavior.ParseStyle(w.Style)!.Value, w.Radius!.Value, w.Pace!.Value, w.LegMs!.Value, w.PauseMs![0], w.PauseMs[1],
                w.Group, w.Lead ?? false, w.Spacing, (int)(w.Points ?? 4));

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
                    combatants.Add(new CharacterSetup(c.Id!, c.Team!, loaded.Definition, build, c.Initiative!.Value, placement, c.ArrivalMs, Gear(c.Gear),
                        c.Consumables?.Select(x => new ConsumableSetup(x!.Id!, x.Heal!.Value, x.Charges!.Value, x.CooldownMs!.Value)),
                        c.Shield is { } shield ? new ShieldGuard(shield.DamageReduction!.Value, shield.CostPerBlock!.Value, shield.ParryWindowMs!.Value) : null,
                        c.Equipment?.Select(e => Equipment.EquipmentEffectShape.Build(e!)), Carried(c.Carried)));
                }
                else if (c.Type == "assailant")
                {
                    var attacks = c.Attacks!.Select(a => new AssailantAttack(a!.Id!, a.RawDamage!.Value, ClassValidator.DamageTypes[a.DamageType!],
                        ClassValidator.Targetings[a.Targeting!], a.Unblockable!.Value, ClassValidator.Applications(a.Applies!),
                        a.Shape is null ? null : AttackShapeIds.Shapes[a.Shape], a.TelegraphMs ?? 0, a.Range, a.Radius, a.Angle, a.Width,
                        a.Center is null ? ShapeCenter.Target : AttackShapeIds.Centers[a.Center], a.PersistentMs ?? 0, a.TickMs ?? 0, a.Guardable, a.Parryable,
                        a.Charge ?? false, a.Push, a.HoldPosition ?? true));
                    combatants.Add(new AssailantSetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Target,
                        c.FirstAttackMs!.Value, c.IntervalMs!.Value, attacks, c.Initiative!.Value, c.StatusResistance ?? Fixed.Zero,
                        Triggers.TriggerShape.Build(c.Triggers), c.Health, placement, c.ArrivalMs,
                        c.Behavior is { } b ? new AssailantBehavior(b.MaxRange!.Value, b.MinRange, b.AggroRange, b.Encircle ?? false, b.Pack ?? true, b.Flee ?? false,
                            b.Wander is { } w ? BuildWander(w) : null) : null, Carried(c.Carried)));
                }
                else
                {
                    combatants.Add(new DummySetup(c.Id!, c.Team!, c.Armor!.Value, c.MagicResistance!.Value, c.Initiative!.Value, c.StatusResistance ?? Fixed.Zero,
                        placement, c.ArrivalMs));
                }
            }
            var commands = scenario.Commands!.Select(c => c!.Action == CombatActionIds.Consume
                ? CombatCommand.Use(c.AtMs!.Value, c.Actor!, c.Item!)   // sous-lot 12.6
                : c.Action == CombatActionIds.Gear
                ? CombatCommand.SetGear(c.AtMs!.Value, c.Actor!, Gear(c.Gear))   // sous-lot V1.8d
                : !CombatActionIds.TryParseNamed(c.Action, out CombatAction action)
                ? new CombatCommand(c.AtMs!.Value, c.Actor!, c.Skill!, c.Target)
                : action == CombatAction.Move
                    ? new CombatCommand(c.AtMs!.Value, c.Actor!, Point(c.Direction!), c.Backward == true)
                    : action == CombatAction.Select   // sous-lot 11.4c
                        ? CombatCommand.SelectTarget(c.AtMs!.Value, c.Actor!, c.Target)
                        : new CombatCommand(c.AtMs!.Value, c.Actor!, action));
            CombatSpace? space = scenario.Space is { } s
                ? new CombatSpace(Point(s.Bounds!.Min!), Point(s.Bounds.Max!),
                    (s.Obstacles ?? new List<ObstacleData?>()).Select(o => new SpaceObstacle(o!.Id!, o.Points!.Select(p => Point(p!)), o.Low ?? false)))
                : null;
            var identity = new CombatDataIdentity(data.RulesIdentity, usedClasses.Values.Select(GameData.IdentityOf));
            var rules = data.Rules;
            return new CombatSetup(scenario.Seed!.Value, scenario.DurationMs!.Value, rules.Stats, rules.Damage, rules.Progression, rules.Combat,
                combatants, commands, identity, space, scenario.Zone ?? false,
                scenario.Truces?.Select(t => new TeamTruce(t![0]!, t[1]!)));   // sous-lot V2.4 (schéma 16)
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

            /// <summary>Sous-lot V2.4.1 (schéma 17) : champs obligatoires de l'errance et manière connue.</summary>
            private void CheckWander(WanderData w, string path)
            {
                if (Present(w.Style, path + ".style", "manière d'errer : graze, hop, patrol ou root") && WanderBehavior.ParseStyle(w.Style) is null
                    && !_report.HasErrorAtOrAbove(path + ".style"))
                    _report.Add(path + ".style", ValidationCode.InvalidValue, $"manière inconnue « {w.Style} » (graze, hop, patrol ou root).");
                Present(w.Radius, path + ".radius", "rayon de l'aire d'errance, en mètres");
                Present(w.Pace, path + ".pace", "allure, fraction de la vitesse");
                Present(w.LegMs, path + ".legMs", "durée d'une étape, en millisecondes");
                Present(w.PauseMs, path + ".pauseMs", "pause [min, max] au début d'une étape, en millisecondes");
                if (w.Points is long points && (points < WanderBehavior.MinPoints || points > WanderBehavior.MaxPoints) && !_report.HasErrorAtOrAbove(path + ".points"))
                    _report.Add(path + ".points", ValidationCode.OutOfRange, $"circuit hors de [{WanderBehavior.MinPoints}, {WanderBehavior.MaxPoints}] points.");
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
                // Sous-lot V2.4 (schéma 16) : chaque trêve est une paire de deux équipes.
                if (scenario.Truces != null)
                    for (int i = 0; i < scenario.Truces.Count; i++)
                    {
                        string p = $"{root}.truces[{i}]";
                        if (!Present(scenario.Truces[i], p, "trêve : paire [équipe, équipe]")) continue;
                        if (scenario.Truces[i]!.Count != 2) _report.Add(p, ValidationCode.InvalidValue, $"{scenario.Truces[i]!.Count} équipe(s), une trêve en lie deux.");
                        else for (int k = 0; k < 2; k++) Present(scenario.Truces[i]![k], $"{p}[{k}]", "équipe");
                    }
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
                // Revue Codex de la PR #44 : un champ d'un autre type est refusé qu'il vienne du lecteur JSON (Present) ou d'un objet public renseigné.
                foreach (string field in TypeFields.Values.SelectMany(f => f).Distinct().Where(f => !own.Contains(f) && (c.Present.Contains(f) || IsSet(c, f))))
                    _report.Add($"{path}.{field}", ValidationCode.InvalidValue,
                        $"champ réservé aux types {string.Join(", ", TypeFields.Where(t => t.Value.Contains(field)).Select(t => t.Key))}.");

                switch (c.Type)
                {
                    case "character":
                        if (Present(c.Class, path + ".class", "identifiant de classe") && !_data.TryGetClass(c.Class!, out _))
                            _report.Add(path + ".class", ValidationCode.UnknownReference,
                                $"classe inconnue « {c.Class} » ; classes chargées : {string.Join(", ", _data.Classes.Select(x => x.Definition.Id))}.");
                        if (Present(c.Build, path + ".build", "build du personnage")) Builds.BuildShape.Check(c.Build!, path + ".build", _report);
                        // Sous-lot 12.6 : objets de soin, tous les champs présents ; leurs bornes sont vérifiées par la validation du combat.
                        if (c.Consumables is { } consumables)
                            Items(consumables, path + ".consumables", "objets de soin", (x, p) =>
                            {
                                Present(x.Id, p + ".id", "identifiant d'objet « item.… »");
                                Present(x.Heal, p + ".heal", "PV rendus");
                                Present(x.Charges, p + ".charges", "nombre de charges");
                                Present(x.CooldownMs, p + ".cooldownMs", "recharge partagée en ms");
                            });
                        // Sous-lot V1.2 : garde du bouclier, trois valeurs présentes ; leurs bornes (combat.shield des règles) sont vérifiées par la
                        // validation du combat.
                        if (c.Shield is { } shield)
                        {
                            Present(shield.DamageReduction, path + ".shield." + ShieldGuard.DamageReductionId, "part des dégâts bloquée, de 0 à 1");
                            Present(shield.CostPerBlock, path + ".shield." + ShieldGuard.CostPerBlockId, "endurance d'un blocage");
                            Present(shield.ParryWindowMs, path + ".shield." + ShieldGuard.ParryWindowMsId, "fenêtre de parade en ms");
                        }
                        // Sous-lot V1.3 : effets de l'équipement, forme seulement ; leurs bornes (combat.equipment des règles) et leurs références
                        // sont vérifiées par la validation du combat (EquipmentEffect.CheckList).
                        if (c.Equipment is { } equipment)
                            Items(equipment, path + ".equipment", "effets de l'équipement porté", (e, p) => Equipment.EquipmentEffectShape.Check(e, p, withId: true, _report));
                        // Sous-lot 12.3 : bonus d'équipement par champ connu ; leurs bornes sont vérifiées par la validation du combat.
                        // Revue Codex de la PR #44 : un scénario construit sans le lecteur JSON (objets publics) est contrôlé de même : valeur
                        // présente, un seul bonus par nom.
                        if (c.Gear is { } gear) CheckGear(gear, path + ".gear");
                        break;
                    case "dummy":
                        Present(c.Armor, path + ".armor", "nombre décimal");
                        Present(c.MagicResistance, path + ".magicResistance", "nombre décimal");
                        break;
                    default:
                        Present(c.Armor, path + ".armor", "nombre décimal");
                        Present(c.MagicResistance, path + ".magicResistance", "nombre décimal");
                        // Revue Codex de la PR #33 : avec un comportement, la cible est facultative (il prend le plus proche).
                        if (!c.Present.Contains("target") && c.Behavior is null)
                            Present(null, path + ".target", "identifiant de la cible, ou null si toutes les attaques sont de zone");
                        Present(c.FirstAttackMs, path + ".firstAttackMs", "instant de la première attaque, en millisecondes");
                        Present(c.IntervalMs, path + ".intervalMs", "intervalle entre deux attaques, en millisecondes");
                        Items(c.Attacks, path + ".attacks", "liste des attaques, jouées dans l'ordre puis en boucle", CheckAttack);
                        if (c.Behavior is { } behavior) Present(behavior.MaxRange, path + ".behavior.maxRange", "distance maximale à la cible, en mètres");   // sous-lot 11.5
                        if (c.Behavior?.Wander is { } wander) CheckWander(wander, path + ".behavior.wander");   // sous-lot V2.4.1
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

            /// <summary>
            /// Sous-lot 12.3 : bonus d'équipement par champ connu ; leurs bornes sont vérifiées par la validation du combat. Revue Codex de la
            /// PR #44 : valeur présente, un seul bonus par nom. Sous-lot V1.8d : le même contrôle pour le bonus d'un bonus modifié.
            /// </summary>
            private void CheckGear(List<KeyValuePair<string, int?>> gear, string at)
            {
                var seenGear = new HashSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, int?> entry in gear)
                    if (GearBonus.FieldIndex(entry.Key) < 0)
                        _report.Add($"{at}.{entry.Key}", ValidationCode.UnknownReference,
                            $"bonus d'équipement inconnu « {entry.Key} » ; bonus possibles : {string.Join(", ", GearBonus.Fields)}.");
                    else if (!seenGear.Add(entry.Key))
                        _report.Add($"{at}.{entry.Key}", ValidationCode.DuplicateId, $"bonus « {entry.Key} » donné deux fois.");
                    else if (entry.Value is null)
                        _report.Add($"{at}.{entry.Key}", ValidationCode.MissingField, "valeur du bonus attendue (entier).");
            }

            private void CheckCommand(CommandData c, string path)
            {
                Present(c.AtMs, path + ".atMs", "instant en millisecondes");
                Present(c.Actor, path + ".actor", "identifiant du combattant");
                string actions = string.Join(", ", CombatActionIds.Named.Concat(new[] { CombatActionIds.Consume, CombatActionIds.Gear }));
                if (!c.Present.Contains("action"))
                {
                    Present(c.Skill, path + ".skill", "identifiant de compétence, ou champ action (" + actions + ")");
                    if (c.Present.Contains("item"))
                        _report.Add(path + ".item", ValidationCode.InvalidValue, "seul un soin (action consume) nomme un objet.");
                    if (c.Present.Contains("gear"))
                        _report.Add(path + ".gear", ValidationCode.InvalidValue, "seul un bonus modifié (action gear) porte un bonus d'équipement.");
                    if (c.Present.Contains("direction"))
                        _report.Add(path + ".direction", ValidationCode.InvalidValue, "seul un déplacement (action move) a une direction.");
                    if (c.Present.Contains("backward"))
                        _report.Add(path + ".backward", ValidationCode.InvalidValue, "seul un déplacement (action move) se fait à reculons.");
                    return;
                }
                CombatAction action = default;
                // Sous-lot 12.6 : un soin nomme son objet, et rien d'autre.
                if (c.Action == CombatActionIds.Consume)
                {
                    Present(c.Item, path + ".item", "objet de soin emporté « item.… »");
                    foreach (string field in new[] { "skill", "target", "direction", "backward", "gear" }.Where(c.Present.Contains))
                        _report.Add($"{path}.{field}", ValidationCode.InvalidValue, "un soin ne prend que son objet (item).");
                    return;
                }
                // Sous-lot V1.8d : un bonus modifié porte son bonus (champs connus, une fois chacun, valeur entière), et rien d'autre ; ses bornes
                // et la nature de l'acteur (un personnage) sont jugées par la validation du combat.
                if (c.Action == CombatActionIds.Gear)
                {
                    if (Present(c.Gear, path + ".gear", "bonus d'équipement { champ: entier }")) CheckGear(c.Gear!, path + ".gear");
                    foreach (string field in new[] { "skill", "target", "direction", "backward", "item" }.Where(c.Present.Contains))
                        _report.Add($"{path}.{field}", ValidationCode.InvalidValue, "un bonus modifié ne prend que son bonus (gear).");
                    return;
                }
                if (c.Present.Contains("gear"))
                    _report.Add(path + ".gear", ValidationCode.InvalidValue, "seul un bonus modifié (action gear) porte un bonus d'équipement.");
                if (c.Present.Contains("item"))
                    _report.Add(path + ".item", ValidationCode.InvalidValue, "seul un soin (action consume) nomme un objet.");
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
                            : "une garde, une parade, un déplacement ou un départ ne prend ni compétence ni cible.");
                // Sous-lot 11.4 : une direction pour un déplacement, et seulement pour lui.
                if (c.Action is null) return;
                if (action == CombatAction.Move) Present(c.Direction, path + ".direction", "direction de marche [x, z], composantes dans [−1, 1]");
                else if (c.Present.Contains("direction"))
                    _report.Add(path + ".direction", ValidationCode.InvalidValue, "seul un déplacement (action move) a une direction.");
                // Sous-lot 11.9 : la marche à reculons, seulement pour un déplacement.
                if (action != CombatAction.Move && c.Present.Contains("backward"))
                    _report.Add(path + ".backward", ValidationCode.InvalidValue, "seul un déplacement (action move) se fait à reculons.");
            }
        }
    }
}
