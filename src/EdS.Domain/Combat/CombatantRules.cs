using System;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot 8.1 : règles effectives d'un combattant, valeurs résultantes des règles de base et de ses modificateurs permanents (X1),
    /// plus l'évaluation de ses modificateurs conditionnels (X2). Construites par une seule fonction (<see cref="For"/>), appelée par la
    /// validation du combat (précontrôle numérique, majorant de file) et par le moteur, si bien qu'un combat accepté ne peut pas diverger
    /// à sa construction. Neutres (règles de base) pour un mannequin ou un assaillant. Immuables.
    /// <para>Chaque valeur résultante = base + somme des modificateurs, <b>écrêtée</b> dans son intervalle de jeu (plafonds et planchers,
    /// jamais une erreur : une combinaison de choix légaux ne rend pas un combat invalide). Quand la base sort déjà de cet intervalle
    /// (règles de test), l'intervalle est élargi jusqu'à elle : sans modificateur, la valeur résultante est toujours la base.</para>
    /// </summary>
    public sealed class CombatantRules
    {
        /// <summary>Plafond de la réduction de garde (B1) ; les règles de base y sont tenues par <see cref="CombatRules.Validate"/>.</summary>
        public static readonly Fixed MaxGuardDamageReduction = CombatRules.MaxGuardDamageReduction;

        /// <summary>Plafond de la réduction totale des dégâts subis (B2), avant l'absorption par un bouclier (8.2).</summary>
        public static readonly Fixed MaxTotalDamageReduction = CombatRules.MaxTotalDamageReduction;

        /// <summary>Fenêtre de parade maximale atteinte par modificateur (500 ms, ramenée au multiple du tick inférieur).</summary>
        public const long MaxParryWindowMs = 500;

        /// <summary>Charges de rempart maximales atteintes par modificateur.</summary>
        public const int MaxRampartCharges = 10;

        /// <summary>
        /// Plancher du pourcentage de PV max résultant (−90 %). Un PV max calculé d'au moins 10 garde au moins 1 PV ; en dessous
        /// (règles de test), le plancher entier peut donner 0, que le moteur traite déjà (mort relevée à la fin du premier tick).
        /// </summary>
        public static readonly Fixed MinMaxHealthPercent = Fixed.FromRatio(-9, 10);

        /// <summary>Plafond du pourcentage de PV max résultant (+1 000 %).</summary>
        public static readonly Fixed MaxMaxHealthPercent = Fixed.FromInt(10);

        private static readonly Fixed MaxInt = Fixed.FromInt(int.MaxValue);

        public ModifierSet Modifiers { get; }
        public Fixed GuardDamageReduction { get; }
        public Fixed GuardUpkeepPerSecond { get; }
        public long GuardCostPerBlock { get; }
        public bool GuardNoUpkeepBreak { get; }
        public Fixed GuardReductionAtZeroEndurance { get; }
        public long GuardBreakShortenPerChargeMs { get; }
        public long ParryWindowMs { get; }
        public long ParryFailureRecoveryMs { get; }
        public int RampartChargesMax { get; }
        public int ResonancePerDirectHit { get; }
        public Fixed EnduranceRegenPerSecond { get; }

        /// <summary>Somme des modificateurs permanents de dégâts infligés (catégorie talent), sans écrêtage (la catégorie borne).</summary>
        public Fixed DamageDealt { get; }

        /// <summary>Somme des modificateurs permanents de dégâts subis ; écrêtée dans [−1, 0] à l'évaluation (<see cref="DamageTakenAt"/>).</summary>
        public Fixed DamageTaken { get; }

        private readonly long _tickMs;

        private CombatantRules(CombatRules rules, ModifierSet m)
        {
            Modifiers = m;
            _tickMs = rules.TickMilliseconds;
            GuardDamageReduction = Clamp(rules.GuardDamageReduction, m.Sum(ModifierField.GuardDamageReduction), Fixed.Zero, MaxGuardDamageReduction);
            GuardUpkeepPerSecond = Clamp(Fixed.FromInt(rules.GuardUpkeepPerSecond), m.Sum(ModifierField.GuardUpkeepPerSecond), Fixed.Zero, MaxInt);
            GuardCostPerBlock = Whole(Clamp(Fixed.FromInt(rules.GuardCostPerBlock), m.Sum(ModifierField.GuardCostPerBlock), Fixed.Zero, MaxInt));
            GuardNoUpkeepBreak = m.Flag(ModifierField.GuardNoUpkeepBreak);
            // Les deux combinaisons inertes du drapeau valent 0, pour que le journal ne les montre pas comme appliquées : la réduction à
            // 0 d'endurance ne sert que si la garde tient sans endurance (noUpkeepBreak) ; le raccourci de rupture, que si la garde peut
            // encore se briser (avec le drapeau, ni l'entretien ni un blocage ne la brisent, et aucune autre rupture n'existe). D'autres
            // valeurs peuvent rester sans effet selon le build (réduction à 0 d'endurance avec un coût de blocage nul, raccourci avec
            // 0 Charge max) : elles sont gardées et journalisées telles quelles. « Ramenée à » : jamais au-dessus de la réduction de
            // garde effective (0 d'endurance ne vaut pas mieux qu'une garde payée).
            GuardReductionAtZeroEndurance = !GuardNoUpkeepBreak ? Fixed.Zero : Fixed.Min(GuardDamageReduction,
                Clamp(Fixed.Zero, m.Sum(ModifierField.GuardReductionAtZeroEndurance), Fixed.Zero, MaxGuardDamageReduction));
            GuardBreakShortenPerChargeMs = GuardNoUpkeepBreak ? 0
                : Whole(Clamp(Fixed.Zero, m.Sum(ModifierField.GuardBreakShortenPerCharge), Fixed.Zero, MaxDuration));
            long windowCap = MaxParryWindowMs - MaxParryWindowMs % _tickMs;
            ParryWindowMs = Whole(Clamp(Fixed.FromInt(rules.ParryWindowMs), m.Sum(ModifierField.ParryWindowMs), Fixed.FromInt(_tickMs), Fixed.FromInt(windowCap)));
            ParryFailureRecoveryMs = Whole(Clamp(Fixed.FromInt(rules.ParryFailureRecoveryMs), m.Sum(ModifierField.ParryFailureRecoveryMs), Fixed.Zero, MaxDuration));
            RampartChargesMax = (int)Whole(Clamp(Fixed.FromInt(rules.RampartChargesMax), m.Sum(ModifierField.RampartChargesMax), Fixed.Zero, Fixed.FromInt(MaxRampartCharges)));
            ResonancePerDirectHit = (int)Whole(Clamp(Fixed.FromInt(rules.ResonancePerDirectHit), m.Sum(ModifierField.ResonancePerDirectHit), Fixed.Zero, MaxInt));
            EnduranceRegenPerSecond = Clamp(Fixed.FromInt(rules.EnduranceRegenPerSecond), m.Sum(ModifierField.EnduranceRegenPerSecond), Fixed.Zero, MaxInt);
            DamageDealt = m.Sum(ModifierField.DamageDealt);
            DamageTaken = m.Sum(ModifierField.DamageTaken);
        }

        private static readonly Fixed MaxDuration = Fixed.FromInt(CombatRules.AbsoluteMaxDurationMs);

        /// <summary>Règles effectives d'un combattant ; <see cref="ModifierSet.Empty"/> donne les règles de base.</summary>
        public static CombatantRules For(CombatRules rules, ModifierSet modifiers) =>
            new CombatantRules(rules ?? throw new ArgumentNullException(nameof(rules)), modifiers ?? throw new ArgumentNullException(nameof(modifiers)));

        /// <summary>base + somme, écrêtée dans [min(bas, base), max(haut, base)].</summary>
        private static Fixed Clamp(Fixed @base, Fixed sum, Fixed low, Fixed high) =>
            sum == Fixed.Zero ? @base : Fixed.Clamp(@base + sum, Fixed.Min(low, @base), Fixed.Max(high, @base));

        private static long Whole(Fixed value) => value.Raw / Fixed.Scale;

        /// <summary>Ajout (ms) aux durées des applications directes de cette altération par ce combattant.</summary>
        public long DurationDealtMs(string status) => Whole(Modifiers.Sum(ModifierField.StatusDurationDealt, status));

        /// <summary>Ajout (ms) aux durées de toute application de cette altération sur ce combattant.</summary>
        public long DurationTakenMs(string status) => Whole(Modifiers.Sum(ModifierField.StatusDurationTaken, status));

        /// <summary>Coefficient effectif d'une compétence : base + modificateurs, écrêté dans [0, <see cref="SkillCombatDefinition.MaxCoefficient"/>].</summary>
        public Fixed Coefficient(SkillDefinition skill, SkillCombatDefinition combat) =>
            Clamp(combat.Coefficient, Modifiers.Sum(ModifierField.SkillCoefficient, skill.Id), Fixed.Zero, SkillCombatDefinition.MaxCoefficient);

        /// <summary>Recharge effective d'une compétence (ms) : base + modificateurs, écrêtée dans [0, 24 h] ; multiple du tick (validé).</summary>
        public long CooldownMs(SkillDefinition skill, SkillCombatDefinition combat) =>
            Whole(Clamp(Fixed.FromInt(combat.CooldownMs), Modifiers.Sum(ModifierField.SkillCooldownMs, skill.Id), Fixed.Zero, MaxDuration));

        /// <summary>
        /// Valeur des modificateurs d'un champ de dégâts sur l'état courant du porteur (X2) : permanent + chaque conditionnel dont les
        /// conditions tiennent, multiplié par les Charges ou les paliers de Résonance. Sans débordement possible : valeurs bornées par la
        /// validation, Charges ≤ max(base, 10), Résonance ≤ 2³¹.
        /// </summary>
        private Fixed Evaluate(ModifierField field, Fixed permanent, bool guarding, long charges, long resonance)
        {
            Fixed total = permanent;
            foreach (ConditionalModifier c in Modifiers.Conditional)
            {
                if (c.Field != field) continue;
                if ((c.Conditions & ModifierConditions.WhileGuarding) != 0 && !guarding) continue;
                long factor = (c.Conditions & ModifierConditions.PerRampartCharge) != 0 ? charges
                    : (c.Conditions & ModifierConditions.PerResonanceStep) != 0 ? resonance / ModifierKeys.ResonanceStep : 1;
                total += c.Value * factor;
            }
            return total;
        }

        /// <summary>Bonus de dégâts infligés (catégorie talent) de l'attaquant dans l'état donné.</summary>
        public Fixed DamageDealtAt(bool guarding, long charges, long resonance) =>
            Evaluate(ModifierField.DamageDealt, DamageDealt, guarding, charges, resonance);

        /// <summary>Modificateur de dégâts subis du défenseur dans l'état donné, écrêté dans [−1, 0] : une réduction seulement.</summary>
        public Fixed DamageTakenAt(bool guarding, long charges, long resonance) =>
            Fixed.Clamp(Evaluate(ModifierField.DamageTaken, DamageTaken, guarding, charges, resonance), -Fixed.One, Fixed.Zero);

        /// <summary>
        /// Majorant du bonus de dégâts infligés sur tout le combat (précontrôle) : permanent + chaque conditionnel positif à son
        /// facteur maximal (Charges max effectives, ⌊Résonance max / palier⌋). Égal à 0 sans modificateur.
        /// </summary>
        public Fixed WorstDamageDealt(int resonanceMax)
        {
            Fixed worst = DamageDealt;
            foreach (ConditionalModifier c in Modifiers.Conditional)
            {
                if (c.Field != ModifierField.DamageDealt || c.Value <= Fixed.Zero) continue;
                long factor = (c.Conditions & ModifierConditions.PerRampartCharge) != 0 ? RampartChargesMax
                    : (c.Conditions & ModifierConditions.PerResonanceStep) != 0 ? Math.Max(0, resonanceMax) / ModifierKeys.ResonanceStep : 1;
                worst += c.Value * factor;
            }
            return worst;
        }

        /// <summary>
        /// Réduction totale d'un impact direct (B2, multiplicative) : r = min(85 %, 1 − (1 − d)(1 + v)), d = réduction de la défense
        /// (garde ≤ 75 %, ou 0), v = modificateur de dégâts subis dans [−1, 0]. Une seule multiplication arrondie. v = 0 donne r = d
        /// exactement. La parade (hors plafond) n'y passe pas : sur un impact paré, la réduction est celle de la parade.
        /// </summary>
        public static Fixed TotalReduction(Fixed defense, Fixed damageTaken)
        {
            if (damageTaken == Fixed.Zero) return defense;
            Fixed reduction = Fixed.One - (Fixed.One - defense) * (Fixed.One + damageTaken);
            return Fixed.Min(reduction, Fixed.Max(MaxTotalDamageReduction, defense));
        }
    }
}
