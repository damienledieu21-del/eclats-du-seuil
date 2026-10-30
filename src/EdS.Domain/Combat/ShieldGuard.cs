using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot V1.2 (plan des Vallons solo, « boucliers qui changent la garde ») : la garde propre d'un bouclier porté. Elle remplace, pour
    /// son porteur, les valeurs de base des règles (<c>combat.guard.damageReduction</c>, <c>combat.guard.costPerBlock</c>,
    /// <c>combat.parry.windowMs</c>) ; les modificateurs des talents et des passifs s'y ajoutent ensuite comme avant
    /// (<see cref="CombatantRules"/>). Un personnage sans bouclier (scénarios d'entraînement, mannequin) garde les valeurs des règles : ses
    /// combats ne changent pas. Bornes de jeu dans les règles (<c>combat.shield</c>), vérifiées par une seule fonction,
    /// <see cref="Check"/>, appelée par le catalogue des objets et par la validation du combat. Immuable.
    /// </summary>
    public sealed class ShieldGuard : IEquatable<ShieldGuard>
    {
        /// <summary>Part des dégâts bloquables retirée pendant la garde (0,6 = −60 %), avant les modificateurs.</summary>
        public Fixed DamageReduction { get; }

        /// <summary>Endurance payée par impact bloqué, avant les modificateurs.</summary>
        public int CostPerBlock { get; }

        /// <summary>Fenêtre d'une parade parfaite, en ms (multiple du tick), avant les modificateurs.</summary>
        public long ParryWindowMs { get; }

        public ShieldGuard(Fixed damageReduction, int costPerBlock, long parryWindowMs)
        {
            DamageReduction = damageReduction;
            CostPerBlock = costPerBlock;
            ParryWindowMs = parryWindowMs;
        }

        /// <summary>Identifiants des trois valeurs, dans l'ordre des fichiers et du journal.</summary>
        public const string DamageReductionId = "damageReduction", CostPerBlockId = "costPerBlock", ParryWindowMsId = "parryWindowMs";

        /// <summary>
        /// Incohérences de cette garde avec les règles <paramref name="rules"/> : part bloquée dans
        /// [<see cref="CombatRules.ShieldMinDamageReduction"/>, <see cref="CombatRules.ShieldMaxDamageReduction"/>], coût d'un blocage dans
        /// [<see cref="CombatRules.ShieldMinCostPerBlock"/>, <see cref="CombatRules.ShieldMaxCostPerBlock"/>], fenêtre de parade à au plus
        /// <see cref="CombatRules.ShieldParryWindowShiftMs"/> de celle des règles (« un peu plus large ou plus étroite »), multiple du tick et
        /// d'au moins un tick. Chemins : <paramref name="path"/> suivi de l'identifiant de la valeur. Vide si la garde est jouable.
        /// </summary>
        public IReadOnlyList<RuleViolation> Check(CombatRules rules, string path)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (path is null) throw new ArgumentNullException(nameof(path));
            var errors = new List<RuleViolation>();
            if (DamageReduction < rules.ShieldMinDamageReduction || DamageReduction > rules.ShieldMaxDamageReduction)
                errors.Add(new RuleViolation(RuleViolationKind.InvalidValue,
                    $"part bloquée {DamageReduction} hors de [{rules.ShieldMinDamageReduction}, {rules.ShieldMaxDamageReduction}] (combat.shield des règles).",
                    path + "." + DamageReductionId));
            if (CostPerBlock < rules.ShieldMinCostPerBlock || CostPerBlock > rules.ShieldMaxCostPerBlock)
                errors.Add(new RuleViolation(RuleViolationKind.InvalidValue,
                    $"coût d'un blocage {CostPerBlock} hors de [{rules.ShieldMinCostPerBlock}, {rules.ShieldMaxCostPerBlock}] (combat.shield des règles).",
                    path + "." + CostPerBlockId));
            long low = rules.ParryWindowMs - rules.ShieldParryWindowShiftMs, high = rules.ParryWindowMs + rules.ShieldParryWindowShiftMs;
            if (ParryWindowMs < Math.Max(low, rules.TickMilliseconds) || ParryWindowMs > high)
                errors.Add(new RuleViolation(RuleViolationKind.InvalidValue,
                    $"fenêtre de parade {ParryWindowMs} ms hors de [{Math.Max(low, rules.TickMilliseconds)}, {high}] ms (combat.parry.windowMs ± combat.shield.parryWindowShiftMs).",
                    path + "." + ParryWindowMsId));
            else if (rules.TickMilliseconds > 0 && ParryWindowMs % rules.TickMilliseconds != 0)
                errors.Add(new RuleViolation(RuleViolationKind.InvalidValue,
                    $"{ParryWindowMs} ms n'est pas un multiple de la durée du tick ({rules.TickMilliseconds} ms).", path + "." + ParryWindowMsId));
            return errors;
        }

        public bool Equals(ShieldGuard? other) =>
            other is not null && DamageReduction == other.DamageReduction && CostPerBlock == other.CostPerBlock && ParryWindowMs == other.ParryWindowMs;

        public override bool Equals(object? obj) => Equals(obj as ShieldGuard);

        public override int GetHashCode() => unchecked((DamageReduction.GetHashCode() * 31 + CostPerBlock) * 31 + ParryWindowMs.GetHashCode());

        public override string ToString() => $"garde {DamageReduction}, blocage {CostPerBlock}, parade {ParryWindowMs} ms";
    }
}
