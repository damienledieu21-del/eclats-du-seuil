using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    // Sous-lot 8.1 : modificateurs génériques (PLAN_LOT_8.md, X1 et X2). Un talent ou une compétence passive déclare des
    // modificateurs { clé, valeur par rang, conditions } ; le combat les agrège en règles effectives par combattant
    // (CombatantRules). Ce fichier ne contient que les données et leur validation : l'agrégation est dans ModifierSet, les
    // valeurs résultantes dans CombatantRules, l'exécution dans le moteur (EdS.Simulation).

    /// <summary>Condition d'un modificateur conditionnel (X2), évaluée au calcul des dégâts sur l'état courant du porteur.</summary>
    public enum ModifierCondition
    {
        /// <summary>Porteur en garde : condition tout ou rien.</summary>
        WhileGuarding = 0,

        /// <summary>Valeur multipliée par les Charges de rempart détenues.</summary>
        PerRampartCharge = 1,

        /// <summary>Valeur multipliée par le nombre de paliers complets de Résonance détenue (palier de <see cref="ModifierKeys.ResonanceStep"/>).</summary>
        PerResonanceStep = 2,
    }

    /// <summary>Ensemble de conditions (X2), forme agrégée d'une liste sans doublon.</summary>
    [Flags]
    public enum ModifierConditions
    {
        None = 0,
        WhileGuarding = 1,
        PerRampartCharge = 2,
        PerResonanceStep = 4,
    }

    /// <summary>Règle visée par une clé de modificateur (liste fermée, sans formule libre).</summary>
    public enum ModifierField
    {
        GuardDamageReduction = 0,
        GuardUpkeepPerSecond = 1,
        GuardCostPerBlock = 2,
        GuardNoUpkeepBreak = 3,
        GuardReductionAtZeroEndurance = 4,
        GuardBreakShortenPerCharge = 5,
        ParryWindowMs = 6,
        ParryFailureRecoveryMs = 7,
        RampartChargesMax = 8,
        ResonancePerDirectHit = 9,
        EnduranceMax = 10,
        EnduranceRegenPerSecond = 11,
        MaxHealth = 12,
        StatusResistance = 13,
        DamageDealt = 14,
        DamageTaken = 15,
        StatusDurationDealt = 16,
        StatusDurationTaken = 17,
        SkillCoefficient = 18,
        SkillCooldownMs = 19,

        /// <summary>Sous-lot 8.2 (X4) : puissance des boucliers posés par le porteur (0,04 = +4 %).</summary>
        ShieldPotencyDealt = 20,
    }

    /// <summary>Nature d'une valeur : contrôle de la forme des valeurs déclarées.</summary>
    public enum ModifierUnit
    {
        /// <summary>Fraction ou décimal à 4 décimales (0,04 = 4 %).</summary>
        Decimal = 0,

        /// <summary>Quantité entière (base et perRank entiers).</summary>
        Integer = 1,

        /// <summary>Durée en millisecondes : entière, multiple de la durée du tick (vérifié au combat).</summary>
        Milliseconds = 2,

        /// <summary>Drapeau : valeur 1 exactement, sans perRank.</summary>
        Flag = 3,
    }

    /// <summary>Description d'une clé : identifiant (motif pour une clé paramétrée), nature, borne des valeurs déclarées, condition admise.</summary>
    public sealed class ModifierSpec
    {
        public ModifierField Field { get; }

        /// <summary>Clé telle qu'écrite dans les données ; motif <c>status.&lt;id&gt;.…</c> ou <c>skill.&lt;id&gt;.…</c> pour une clé paramétrée.</summary>
        public string Name { get; }

        public ModifierUnit Unit { get; }

        /// <summary>
        /// Valeur absolue maximale d'une valeur déclarée, à chaque rang possible (rang 1 et rang maximal). Pour une règle à plafond
        /// absolu du moteur (Charges max 10, fenêtre de parade 500 ms), la largeur de ce plafond : un modificateur seul qui le
        /// dépasserait est une erreur de données ; seules les combinaisons de sources sont écrêtées. Les deux clés de réduction de garde
        /// ont ici la borne d'une fraction (1) : leur plafond est un paramètre des règles (<c>combat.guard.maxDamageReduction</c>,
        /// décision du 18/10/2026), vérifié au combat par <see cref="ModifierDefinition.ValidateCaps"/>. Ailleurs, une enveloppe
        /// technique provisoire.
        /// </summary>
        public Fixed MaxMagnitude { get; }

        /// <summary>Seules les clés lues au calcul des dégâts admettent des conditions (X2).</summary>
        public bool Conditional { get; }

        internal ModifierSpec(ModifierField field, string name, ModifierUnit unit, Fixed maxMagnitude, bool conditional = false)
        {
            Field = field;
            Name = name;
            Unit = unit;
            MaxMagnitude = maxMagnitude;
            Conditional = conditional;
        }
    }

    /// <summary>Clé analysée : règle visée et, pour une clé paramétrée, l'identifiant de l'altération ou de la compétence.</summary>
    public readonly struct ModifierKey : IEquatable<ModifierKey>, IComparable<ModifierKey>
    {
        public ModifierField Field { get; }

        /// <summary>Identifiant d'altération (<c>status.&lt;id&gt;.…</c>) ou de compétence (<c>skill.….coefficient</c>) ; null pour une clé scalaire.</summary>
        public string? Parameter { get; }

        public ModifierKey(ModifierField field, string? parameter = null)
        {
            Field = field;
            Parameter = parameter;
        }

        public ModifierSpec Spec => ModifierKeys.Specs[(int)Field];

        public bool Equals(ModifierKey other) => Field == other.Field && string.Equals(Parameter, other.Parameter, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ModifierKey other && Equals(other);
        public override int GetHashCode() => ((int)Field * 397) ^ (Parameter is null ? 0 : StringComparer.Ordinal.GetHashCode(Parameter));

        /// <summary>Ordre total : texte de la clé, ordinal.</summary>
        public int CompareTo(ModifierKey other) => string.CompareOrdinal(ToString(), other.ToString());

        /// <summary>Texte canonique de la clé, tel qu'écrit dans les données et au journal.</summary>
        public override string ToString() => Field switch
        {
            ModifierField.StatusDurationDealt => $"status.{Parameter}.durationDealt",
            ModifierField.StatusDurationTaken => $"status.{Parameter}.durationTaken",
            ModifierField.SkillCoefficient => $"{Parameter}.coefficient",
            ModifierField.SkillCooldownMs => $"{Parameter}.cooldownMs",
            _ => Spec.Name,
        };
    }

    /// <summary>Liste fermée des clés (X1) et analyse du texte d'une clé.</summary>
    public static class ModifierKeys
    {
        /// <summary>Palier de Résonance de la condition <c>perResonanceStep</c> (B12).</summary>
        public const int ResonanceStep = 10;

        private static readonly Fixed Ms = Fixed.FromInt(CombatRules.AbsoluteMaxDurationMs);
        private static readonly Fixed Million = Fixed.FromInt(1_000_000);

        /// <summary>Spécifications, indexées par <see cref="ModifierField"/>.</summary>
        public static IReadOnlyList<ModifierSpec> Specs { get; } = Frozen.List(new[]
        {
            new ModifierSpec(ModifierField.GuardDamageReduction, "guard.damageReduction", ModifierUnit.Decimal, Fixed.One),
            new ModifierSpec(ModifierField.GuardUpkeepPerSecond, "guard.upkeepPerSecond", ModifierUnit.Decimal, Million),
            new ModifierSpec(ModifierField.GuardCostPerBlock, "guard.costPerBlock", ModifierUnit.Integer, Million),
            new ModifierSpec(ModifierField.GuardNoUpkeepBreak, "guard.noUpkeepBreak", ModifierUnit.Flag, Fixed.One),
            new ModifierSpec(ModifierField.GuardReductionAtZeroEndurance, "guard.reductionAtZeroEndurance", ModifierUnit.Decimal, Fixed.One),
            new ModifierSpec(ModifierField.GuardBreakShortenPerCharge, "guard.breakShortenPerCharge", ModifierUnit.Milliseconds, Ms),
            new ModifierSpec(ModifierField.ParryWindowMs, "parry.windowMs", ModifierUnit.Milliseconds, Fixed.FromInt(CombatantRules.MaxParryWindowMs)),
            new ModifierSpec(ModifierField.ParryFailureRecoveryMs, "parry.failureRecoveryMs", ModifierUnit.Milliseconds, Ms),
            new ModifierSpec(ModifierField.RampartChargesMax, "rampartCharges.max", ModifierUnit.Integer, Fixed.FromInt(CombatantRules.MaxRampartCharges)),
            new ModifierSpec(ModifierField.ResonancePerDirectHit, "resonance.perDirectHit", ModifierUnit.Integer, Million),
            new ModifierSpec(ModifierField.EnduranceMax, "endurance.max", ModifierUnit.Integer, Million),
            new ModifierSpec(ModifierField.EnduranceRegenPerSecond, "endurance.regenPerSecond", ModifierUnit.Decimal, Million),
            new ModifierSpec(ModifierField.MaxHealth, "maxHealth", ModifierUnit.Decimal, Fixed.FromInt(10)),
            new ModifierSpec(ModifierField.StatusResistance, "statusResistance", ModifierUnit.Decimal, Million),
            new ModifierSpec(ModifierField.DamageDealt, "damageDealt", ModifierUnit.Decimal, Fixed.FromInt(10), conditional: true),
            new ModifierSpec(ModifierField.DamageTaken, "damageTaken", ModifierUnit.Decimal, Fixed.One, conditional: true),
            new ModifierSpec(ModifierField.StatusDurationDealt, "status.<id>.durationDealt", ModifierUnit.Milliseconds, Ms),
            new ModifierSpec(ModifierField.StatusDurationTaken, "status.<id>.durationTaken", ModifierUnit.Milliseconds, Ms),
            new ModifierSpec(ModifierField.SkillCoefficient, "skill.<id>.coefficient", ModifierUnit.Decimal, SkillCombatDefinition.MaxCoefficient),
            new ModifierSpec(ModifierField.SkillCooldownMs, "skill.<id>.cooldownMs", ModifierUnit.Milliseconds, Ms),
            new ModifierSpec(ModifierField.ShieldPotencyDealt, "shield.potencyDealt", ModifierUnit.Decimal, Fixed.FromInt(10)),
        }, "specs");

        private static readonly Dictionary<string, ModifierField> Scalar =
            Specs.Where(s => !s.Name.Contains("<id>")).ToDictionary(s => s.Name, s => s.Field, StringComparer.Ordinal);

        /// <summary>
        /// Analyse le texte d'une clé : clé scalaire de la liste fermée, <c>status.&lt;id&gt;.durationDealt|durationTaken</c> (identifiant
        /// d'altération, sans point), ou identifiant complet d'une compétence suivi de <c>.coefficient</c> ou <c>.cooldownMs</c>
        /// (<c>skill.bastion.frappe_du_rempart.coefficient</c>). L'existence de la référence est vérifiée ailleurs (classe, catalogue).
        /// </summary>
        public static bool TryParse(string? text, out ModifierKey key)
        {
            key = default;
            if (text is null) return false;
            if (Scalar.TryGetValue(text, out ModifierField field))
            {
                key = new ModifierKey(field);
                return true;
            }
            if (text.StartsWith("status.", StringComparison.Ordinal))
            {
                foreach (var (suffix, f) in new[] { (".durationDealt", ModifierField.StatusDurationDealt), (".durationTaken", ModifierField.StatusDurationTaken) })
                    if (text.EndsWith(suffix, StringComparison.Ordinal) && text.Length > "status.".Length + suffix.Length)
                    {
                        string id = text.Substring("status.".Length, text.Length - "status.".Length - suffix.Length);
                        if (!StatusCatalog.IsValidId(id)) return false;
                        key = new ModifierKey(f, id);
                        return true;
                    }
                return false;
            }
            if (text.StartsWith("skill.", StringComparison.Ordinal))
            {
                foreach (var (suffix, f) in new[] { (".coefficient", ModifierField.SkillCoefficient), (".cooldownMs", ModifierField.SkillCooldownMs) })
                    if (text.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        string id = text.Substring(0, text.Length - suffix.Length);
                        if (!StableId.HasPrefix(id, "skill")) return false;
                        key = new ModifierKey(f, id);
                        return true;
                    }
            }
            return false;
        }

        /// <summary>Identifiants des conditions dans les données et au journal.</summary>
        public static IReadOnlyDictionary<string, ModifierCondition> Conditions { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ModifierCondition>(
            new Dictionary<string, ModifierCondition>(StringComparer.Ordinal)
            {
                ["whileGuarding"] = ModifierCondition.WhileGuarding,
                ["perRampartCharge"] = ModifierCondition.PerRampartCharge,
                ["perResonanceStep"] = ModifierCondition.PerResonanceStep,
            });

        public static string Of(ModifierCondition condition) => condition switch
        {
            ModifierCondition.WhileGuarding => "whileGuarding",
            ModifierCondition.PerRampartCharge => "perRampartCharge",
            ModifierCondition.PerResonanceStep => "perResonanceStep",
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };

        /// <summary>Conditions d'un ensemble, dans l'ordre fixe de l'énumération.</summary>
        public static IEnumerable<ModifierCondition> Of(ModifierConditions set)
        {
            foreach (ModifierCondition c in new[] { ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge, ModifierCondition.PerResonanceStep })
                if ((set & Flag(c)) != 0) yield return c;
        }

        public static ModifierConditions Flag(ModifierCondition condition) => (ModifierConditions)(1 << (int)condition);
    }

    /// <summary>
    /// Modificateur déclaré par un talent ou une compétence passive (X1, X2) : <c>{ "key", "add", "conditions" }</c>. <see cref="Add"/> est
    /// une valeur par rang (talent) ; sans condition, le modificateur est permanent et s'ajoute à la règle visée à la construction du
    /// combat ; avec conditions, il est lu au calcul des dégâts. Immuable (copie défensive des conditions).
    /// </summary>
    public sealed class ModifierDefinition
    {
        /// <summary>Plafond de modificateurs par talent ou par compétence (enveloppe technique provisoire, comme les déclencheurs).</summary>
        public const int MaxPerOwner = 8;

        /// <summary>Texte de la clé, tel que déclaré (analysé par <see cref="ModifierKeys.TryParse"/>).</summary>
        public string Key { get; }

        public RankedValue Add { get; }

        /// <summary>Conditions (X2), dans l'ordre déclaré ; vide = permanent (X1).</summary>
        public IReadOnlyList<ModifierCondition> Conditions { get; }

        public ModifierDefinition(string key, RankedValue add, IEnumerable<ModifierCondition>? conditions = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Add = add;
            Conditions = Frozen.ListOrEmpty(conditions, nameof(conditions));
        }

        /// <summary>Ensemble des conditions déclarées.</summary>
        public ModifierConditions ConditionSet => Conditions.Aggregate(ModifierConditions.None, (s, c) => s | ModifierKeys.Flag(c));

        /// <summary>
        /// Contrôles d'une liste de modificateurs qui ne dépendent ni du catalogue des altérations ni de la durée du tick : nombre, clé
        /// connue, doublon (même clé et mêmes conditions), conditions admises, valeurs bornées à chaque rang et non nulles à tous les rangs,
        /// entiers, drapeaux, référence d'une compétence de la classe. Chemins de la forme <c>{path}[i].add</c>. <paramref name="maxRank"/>
        /// vaut le rang maximal d'un talent, 1 ailleurs ; <paramref name="ranked"/> autorise les valeurs par rang (talents à plusieurs rangs
        /// seulement : un perRank sans effet est refusé). Au plus une erreur par valeur. Un rang maximal déclaré inférieur à 1 est déjà
        /// refusé par la classe : le refus « perRank sans effet », qui suppose un seul rang déclaré, ne s'applique pas ; une valeur avec
        /// perRank est alors examinée aux rangs 1 et 2 (le rang 2 existe pour tout rang maximal qui admet un perRank), sans perRank au
        /// rang 1. Aucune erreur n'est inventée, et une valeur fausse pour tout rang maximal légal est signalée dès le premier passage.
        /// </summary>
        public static void ValidateList(IReadOnlyList<ModifierDefinition> modifiers, string path, int maxRank, bool ranked,
            Func<string, SkillDefinition?> skillOf, List<RuleViolation> errors)
        {
            if (modifiers.Count > MaxPerOwner)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{modifiers.Count} modificateurs, au plus {MaxPerOwner} par définition.", path));
            int topRank = Math.Max(1, Math.Min(maxRank, TalentDefinition.MaxRankLimit));
            bool ranksDeclared = maxRank >= 1;
            var seen = new Dictionary<(ModifierKey, ModifierConditions), int>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition m = modifiers[i];
                string p = $"{path}[{i}]";
                if (!ModifierKeys.TryParse(m.Key, out ModifierKey key))
                {
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference,
                        $"clé inconnue « {m.Key} » ; clés possibles : {string.Join(", ", ModifierKeys.Specs.Select(s => s.Name))}.", p + ".key"));
                    continue;
                }
                ModifierSpec spec = key.Spec;
                if ((key.Field == ModifierField.SkillCoefficient || key.Field == ModifierField.SkillCooldownMs)
                    && !(skillOf(key.Parameter!) is { Combat: not null }))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference,
                        $"« {key.Parameter} » ne désigne aucune compétence de la classe qui se lance (bloc combat).", p + ".key"));

                bool conditionsValid = ValidateConditions(m, spec, p, errors);
                if (conditionsValid && seen.TryGetValue((key, m.ConditionSet), out int first))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId,
                        $"modificateur « {m.Key} »{(m.Conditions.Count > 0 ? " avec les mêmes conditions" : string.Empty)} déjà déclaré ({path}[{first}]).", p + ".key"));
                else if (conditionsValid)
                    seen[(key, m.ConditionSet)] = i;

                ValidateValue(m.Add, spec, p + ".add", topRank, ranked, ranksDeclared, errors);
            }
        }

        private static bool ValidateConditions(ModifierDefinition m, ModifierSpec spec, string p, List<RuleViolation> errors)
        {
            if (m.Conditions.Count == 0) return true;
            int before = errors.Count;
            if (!spec.Conditional)
            {
                errors.Add(new RuleViolation($"« {m.Key} » ne peut pas être conditionnelle : seules damageDealt et damageTaken, lues au calcul des dégâts, le peuvent.", p + ".conditions"));
                return false;
            }
            var set = ModifierConditions.None;
            for (int k = 0; k < m.Conditions.Count; k++)
            {
                ModifierCondition c = m.Conditions[k];
                if (!Enum.IsDefined(typeof(ModifierCondition), c))
                    errors.Add(new RuleViolation("condition inconnue.", $"{p}.conditions[{k}]"));
                else if ((set & ModifierKeys.Flag(c)) != 0)
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"condition « {ModifierKeys.Of(c)} » déjà citée.", $"{p}.conditions[{k}]"));
                else
                    set |= ModifierKeys.Flag(c);
            }
            if ((set & ModifierConditions.PerRampartCharge) != 0 && (set & ModifierConditions.PerResonanceStep) != 0)
                errors.Add(new RuleViolation("au plus une condition multiplicative (perRampartCharge ou perResonanceStep).", p + ".conditions"));
            return errors.Count == before;
        }

        private static void ValidateValue(RankedValue value, ModifierSpec spec, string p, int topRank, bool ranked, bool ranksDeclared,
            List<RuleViolation> errors)
        {
            // Une seule erreur par valeur : un perRank interdit n'est pas examiné plus loin (drapeau, entier).
            if (!ranked && value.PerRank != Fixed.Zero)
            {
                errors.Add(new RuleViolation("seul un talent a des valeurs par rang (perRank).", p));
                return;
            }
            if (ranked && ranksDeclared && topRank == 1 && value.PerRank != Fixed.Zero)
            {
                errors.Add(new RuleViolation("perRank sans effet sur un talent à un seul rang.", p));
                return;
            }
            if (spec.Unit == ModifierUnit.Flag)
            {
                if (value.Base != Fixed.One || value.PerRank != Fixed.Zero)
                    errors.Add(new RuleViolation($"« {spec.Name} » est un drapeau : valeur 1, sans perRank.", p));
                return;
            }
            int rank = !ranked ? 1 : ranksDeclared ? topRank : value.PerRank != Fixed.Zero ? 2 : 1;
            Fixed low, high;
            try
            {
                low = value.At(1);
                high = value.At(rank);
            }
            catch (OverflowException)
            {
                errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, "valeur par rang hors des limites numériques.", p));
                return;
            }
            Fixed max = spec.MaxMagnitude;
            if (low < -max || low > max || high < -max || high > max)
                errors.Add(new RuleViolation($"valeur hors de [{-max}, {max}] à un rang possible (1 à {rank}).", p));
            else if (low == Fixed.Zero && high == Fixed.Zero)
                errors.Add(new RuleViolation("modificateur sans effet : valeur nulle à chaque rang.", p));
            else if (spec.Field == ModifierField.DamageTaken && (low > Fixed.Zero || high > Fixed.Zero))
                errors.Add(new RuleViolation("damageTaken ne fait que réduire les dégâts subis : valeur négative ou nulle à chaque rang.", p));
            else if (spec.Unit != ModifierUnit.Decimal && (value.Base.Raw % Fixed.Scale != 0 || value.PerRank.Raw % Fixed.Scale != 0))
                errors.Add(new RuleViolation(spec.Unit == ModifierUnit.Milliseconds
                    ? "durée entière en millisecondes attendue (base et perRank)."
                    : "quantité entière attendue (base et perRank).", p));
        }

        /// <summary>
        /// Décision du 18/10/2026 : les plafonds de réduction sont des paramètres des règles, connus seulement au combat. Comme une règle à
        /// plafond absolu, une valeur déclarée de <c>guard.damageReduction</c> ou <c>guard.reductionAtZeroEndurance</c> est bornée à
        /// chaque rang possible (rang 1 et rang maximal) par la largeur du plafond des règles (<paramref name="guardReductionCap"/>) : un
        /// modificateur seul qui le dépasse est une erreur de données ; seules les combinaisons de sources sont écrêtées. Appelé après
        /// <see cref="ValidateList"/>, sur une liste qu'elle n'a pas refusée (une valeur déjà hors de [−1, 1] n'est pas signalée deux fois).
        /// </summary>
        public static void ValidateCaps(IReadOnlyList<ModifierDefinition> modifiers, string path, int maxRank, bool ranked, Fixed guardReductionCap,
            List<RuleViolation> errors)
        {
            int topRank = Math.Max(1, Math.Min(maxRank, TalentDefinition.MaxRankLimit));
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition m = modifiers[i];
                if (!ModifierKeys.TryParse(m.Key, out ModifierKey key)) continue;
                if (key.Field != ModifierField.GuardDamageReduction && key.Field != ModifierField.GuardReductionAtZeroEndurance) continue;
                // Mêmes rangs examinés que ValidateValue : rang maximal déclaré, ou rang 2 si un perRank existe sans rang valide.
                int rank = !ranked ? 1 : maxRank >= 1 ? topRank : m.Add.PerRank != Fixed.Zero ? 2 : 1;
                Fixed low, high;
                try
                {
                    low = m.Add.At(1);
                    high = m.Add.At(rank);
                }
                catch (OverflowException)
                {
                    continue;   // déjà signalé par ValidateList
                }
                Fixed max = guardReductionCap;
                if (low < -max || low > max || high < -max || high > max)
                    errors.Add(new RuleViolation(
                        $"valeur hors de [{-max}, {max}] à un rang possible (1 à {rank}) : plafond de la réduction de garde des règles (combat.guard.maxDamageReduction).",
                        $"{path}[{i}].add"));
            }
        }

        /// <summary>
        /// Contrôles qui dépendent du catalogue des altérations et de la durée du tick, connus seulement au combat : une clé
        /// <c>status.&lt;id&gt;</c> désigne une altération du catalogue ; <c>durationDealt</c>, une altération applicable directement (ni
        /// immunité, ni rupture de garde) ; une durée est un multiple du tick (base et perRank), et de la période pour un DOT.
        /// </summary>
        public static void ValidateReferences(IReadOnlyList<ModifierDefinition> modifiers, string path, StatusCatalog catalog, string guardBreakStatus,
            long tickMs, List<RuleViolation> errors)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition m = modifiers[i];
                string p = $"{path}[{i}]";
                if (!ModifierKeys.TryParse(m.Key, out ModifierKey key)) continue;
                StatusDefinition? status = null;
                if (key.Field == ModifierField.StatusDurationDealt || key.Field == ModifierField.StatusDurationTaken)
                {
                    if (!catalog.TryGet(key.Parameter!, out status))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {key.Parameter} ».", p + ".key"));
                        continue;
                    }
                    if (key.Field == ModifierField.StatusDurationDealt && (status.Category == StatusCategory.Immunity || status.Id == guardBreakStatus))
                        errors.Add(new RuleViolation($"« {status.Id} » n'est jamais appliquée directement (immunité ou rupture de garde) : durationDealt sans objet.", p + ".key"));
                }
                if (key.Spec.Unit != ModifierUnit.Milliseconds) continue;
                long step = status is { IsDot: true } ? status.PeriodMs : tickMs;
                if (step > 0 && (value(m.Add.Base) % step != 0 || value(m.Add.PerRank) % step != 0))
                    errors.Add(new RuleViolation(status is { IsDot: true }
                        ? $"durée non multiple de la période de « {status.Id} » ({step} ms), base et perRank."
                        : $"durée non multiple de la durée du tick ({step} ms), base et perRank.", p + ".add"));
            }

            static long value(Fixed f) => f.Raw / Fixed.Scale;
        }
    }
}
