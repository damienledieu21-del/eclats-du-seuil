using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot V1.3 (plan des Vallons solo, « effets d'équipement ») : effets que l'équipement porté donne en combat, par une source
    /// (<see cref="Source"/>) : un objet nommé (<c>item.lame_du_seuil</c>) ou un palier d'ensemble atteint (<c>set.garde_de_la_tour.4</c>).
    /// Des modificateurs (ceux des talents, sur une liste de clés admises) et des déclencheurs d'origine <see cref="TriggerOrigin.Equipment"/>
    /// (réservée depuis le lot 8). Ils s'ajoutent à ceux du build du personnage (<see cref="CharacterSetup.ActiveModifiers"/>,
    /// <see cref="CharacterSetup.ActiveTriggers"/>) : la validation, le précontrôle numérique et le moteur les traitent comme eux. Comme le
    /// reste de l'équipement, ils valent dès l'entrée suivante dans un combat. Bornes de jeu dans les règles (<c>combat.equipment</c>),
    /// vérifiées par une seule fonction, <see cref="Check"/>, appelée par le catalogue des objets et par la validation du combat. Immuable.
    /// </summary>
    public sealed class EquipmentEffect
    {
        /// <summary>Modificateurs d'une source, au plus.</summary>
        public const int MaxModifiers = 4;

        /// <summary>Déclencheurs d'une source, au plus.</summary>
        public const int MaxTriggers = 2;

        /// <summary>
        /// Sources d'effets d'un personnage, au plus : les 16 places du combat et des paliers d'ensemble (trois par ensemble). Enveloppe
        /// technique provisoire, très au-delà de ce que l'équipement porté peut donner.
        /// </summary>
        public const int MaxPerCharacter = 40;

        private static readonly Regex SourcePattern = new Regex(@"^(item|set)(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de la source : « item.… » (objet nommé) ou « set.….&lt;pièces&gt; » (palier d'ensemble).</summary>
        public string Source { get; }

        public IReadOnlyList<ModifierDefinition> Modifiers { get; }
        public IReadOnlyList<TriggerDefinition> Triggers { get; }

        public EquipmentEffect(string source, IEnumerable<ModifierDefinition>? modifiers = null, IEnumerable<TriggerDefinition>? triggers = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Modifiers = Frozen.ListOrEmpty(modifiers, nameof(modifiers));
            Triggers = Frozen.ListOrEmpty(triggers, nameof(triggers));
        }

        /// <summary>Identifiant de source valide : « item.… » ou « set.… », minuscules, chiffres, « _ » et « . », 200 caractères au plus.</summary>
        public static bool IsSourceId(string? id) => id != null && id.Length <= 200 && SourcePattern.IsMatch(id);

        /// <summary>
        /// Clés de modificateur admises pour l'équipement : dégâts infligés et subis, PV maximaux (pourcentage), régénération d'endurance,
        /// allongement d'une altération posée. Les règles de garde, de parade, de Charges et des compétences restent aux talents et aux
        /// boucliers (garde propre, sous-lot V1.2).
        /// </summary>
        public static IReadOnlyList<ModifierField> AllowedFields { get; } = Frozen.List(new[]
        {
            ModifierField.DamageDealt, ModifierField.DamageTaken, ModifierField.MaxHealth, ModifierField.EnduranceRegenPerSecond,
            ModifierField.StatusDurationDealt,
        }, nameof(AllowedFields));

        /// <summary>Conditions admises : tout ou rien seulement (en garde, cible étourdie) ; pas de condition multiplicative.</summary>
        public const ModifierConditions AllowedConditions = ModifierConditions.WhileGuarding | ModifierConditions.TargetStunned;

        /// <summary>
        /// Incohérences de cet effet avec les règles <paramref name="rules"/> : identifiant de source, au moins un modificateur ou un
        /// déclencheur, nombres bornés ; modificateurs de clés admises (<see cref="AllowedFields"/>), de conditions admises
        /// (<see cref="AllowedConditions"/>), bien formés (<see cref="ModifierDefinition.ValidateList"/>, sans rang), de références connues
        /// (<see cref="ModifierDefinition.ValidateReferences"/>) et de valeurs positives bornées par <c>combat.equipment</c> (réduction pour
        /// <c>damageTaken</c>) ; déclencheurs bien formés (<see cref="TriggerDefinition.ValidateList"/>, sans rang), de références connues
        /// (<see cref="TriggerDefinition.ValidateReferences"/>), de recharge interne d'au moins
        /// <see cref="CombatRules.EquipmentMinTriggerCooldownMs"/>, de frappes et de gains bornés. Chemins : <paramref name="path"/> suivi
        /// de <c>.id</c>, <c>.modifiers[i]…</c>, <c>.triggers[i]…</c>. Vide si l'effet est jouable.
        /// </summary>
        public IReadOnlyList<RuleViolation> Check(CombatRules rules, string path)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (path is null) throw new ArgumentNullException(nameof(path));
            var errors = new List<RuleViolation>();
            if (!IsSourceId(Source))
                errors.Add(new RuleViolation($"source « {Source} » invalide : « item.… » ou « set.… » (minuscules, chiffres, « _ » et « . »).", path + ".id"));
            if (Modifiers.Count == 0 && Triggers.Count == 0)
                errors.Add(new RuleViolation("un effet d'équipement porte au moins un modificateur ou un déclencheur.", path));
            if (Modifiers.Count > MaxModifiers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{Modifiers.Count} modificateurs, au plus {MaxModifiers} par source.", path + ".modifiers"));
            if (Triggers.Count > MaxTriggers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{Triggers.Count} déclencheurs, au plus {MaxTriggers} par source.", path + ".triggers"));
            if (errors.Count > 0) return errors;
            CheckModifiers(rules, path + ".modifiers", errors);
            CheckTriggers(rules, path + ".triggers", errors);
            return errors;
        }

        private void CheckModifiers(CombatRules rules, string path, List<RuleViolation> errors)
        {
            if (Modifiers.Count == 0) return;
            bool admitted = true;
            for (int i = 0; i < Modifiers.Count; i++)
            {
                ModifierDefinition m = Modifiers[i];
                if (!ModifierKeys.TryParse(m.Key, out ModifierKey key) || !AllowedFields.Contains(key.Field))
                {
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference,
                        $"clé « {m.Key} » non admise pour l'équipement ; clés admises : {string.Join(", ", AllowedFields.Select(f => ModifierKeys.Specs[(int)f].Name))}.",
                        $"{path}[{i}].key"));
                    admitted = false;
                }
                else if ((m.ConditionSet & ~AllowedConditions) != 0)
                {
                    errors.Add(new RuleViolation("conditions admises pour l'équipement : whileGuarding et targetStunned (pas de condition multiplicative).",
                        $"{path}[{i}].conditions"));
                    admitted = false;
                }
            }
            if (!admitted) return;
            int before = errors.Count;
            // Forme générale des modificateurs (doublons, conditions, valeurs non nulles et entières selon la clé), sans rang ni compétence.
            ModifierDefinition.ValidateList(Modifiers, path, 1, false, _ => null, errors);
            if (errors.Count > before) return;
            ModifierDefinition.ValidateReferences(Modifiers, path, rules.Statuses, rules.GuardBreakStatus, rules.TickMilliseconds, errors);
            for (int i = 0; i < Modifiers.Count; i++)
            {
                ModifierDefinition m = Modifiers[i];
                ModifierKeys.TryParse(m.Key, out ModifierKey key);
                Fixed value = m.Add.Base;
                string p = $"{path}[{i}].add";
                void Positive(Fixed max, string rule)
                {
                    if (value <= Fixed.Zero || value > max)
                        errors.Add(new RuleViolation($"valeur hors de ]0, {max}] pour l'équipement (combat.equipment.{rule} des règles).", p));
                }
                switch (key.Field)
                {
                    case ModifierField.DamageDealt: Positive(rules.EquipmentMaxDamageDealt, "maxDamageDealt"); break;
                    case ModifierField.DamageTaken:
                        if (value >= Fixed.Zero || value < -rules.EquipmentMaxDamageTaken)
                            errors.Add(new RuleViolation($"réduction hors de [−{rules.EquipmentMaxDamageTaken}, 0[ pour l'équipement (combat.equipment.maxDamageTaken des règles).", p));
                        break;
                    case ModifierField.MaxHealth: Positive(rules.EquipmentMaxHealthPercent, "maxHealthPercent"); break;
                    case ModifierField.EnduranceRegenPerSecond: Positive(rules.EquipmentMaxEnduranceRegen, "maxEnduranceRegen"); break;
                    case ModifierField.StatusDurationDealt: Positive(Fixed.FromInt(rules.EquipmentMaxStatusDurationMs), "maxStatusDurationMs"); break;
                }
            }
        }

        private void CheckTriggers(CombatRules rules, string path, List<RuleViolation> errors)
        {
            if (Triggers.Count == 0) return;
            int before = errors.Count;
            TriggerDefinition.ValidateList(Triggers, path, 1, false, errors);
            if (errors.Count > before) return;
            TriggerDefinition.ValidateReferences(Triggers, path, rules.Statuses, rules.GuardBreakStatus, rules.TickMilliseconds,
                rules.AreaStrikeMinCooldownMs, errors);
            for (int i = 0; i < Triggers.Count; i++)
            {
                TriggerDefinition t = Triggers[i];
                string p = $"{path}[{i}]";
                if (t.CooldownMs < rules.EquipmentMinTriggerCooldownMs)
                    errors.Add(new RuleViolation($"recharge interne de {t.CooldownMs} ms : au moins {rules.EquipmentMinTriggerCooldownMs} ms pour l'équipement " +
                        "(combat.equipment.minTriggerCooldownMs des règles).", p + ".cooldownMs"));
                for (int k = 0; k < t.Effects.Count; k++)
                {
                    TriggerEffect e = t.Effects[k];
                    string ep = $"{p}.effects[{k}]";
                    if (e.Kind == TriggerEffectKind.Strike && e.Coefficient is { } c && c.Base > rules.EquipmentMaxStrikeCoefficient)
                        errors.Add(new RuleViolation($"coefficient {c.Base} au-dessus de {rules.EquipmentMaxStrikeCoefficient} pour l'équipement " +
                            "(combat.equipment.maxStrikeCoefficient des règles).", ep + ".coefficient"));
                    if (e.Kind == TriggerEffectKind.GainResource && e.Amount is { } a && a.Base > Fixed.FromInt(rules.EquipmentMaxGain))
                        errors.Add(new RuleViolation($"gain {a.Base} au-dessus de {rules.EquipmentMaxGain} pour l'équipement (combat.equipment.maxGain des règles).",
                            ep + ".amount"));
                }
            }
        }

        /// <summary>
        /// Incohérences d'une liste d'effets portés par un personnage : au plus <see cref="MaxPerCharacter"/> sources, chacune une seule fois,
        /// chacune jouable (<see cref="Check"/>, chemins <c>{path}[k]…</c>).
        /// </summary>
        public static IReadOnlyList<RuleViolation> CheckList(IReadOnlyList<EquipmentEffect> effects, CombatRules rules, string path)
        {
            if (effects is null) throw new ArgumentNullException(nameof(effects));
            var errors = new List<RuleViolation>();
            if (effects.Count > MaxPerCharacter)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{effects.Count} sources d'effets, au plus {MaxPerCharacter}.", path));
                return errors;
            }
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int k = 0; k < effects.Count; k++)
            {
                string p = $"{path}[{k}]";
                if (seen.TryGetValue(effects[k].Source, out int first))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"source « {effects[k].Source} » déjà donnée ({path}[{first}]).", p + ".id"));
                else seen[effects[k].Source] = k;
                errors.AddRange(effects[k].Check(rules, p));
            }
            return errors;
        }
    }
}
