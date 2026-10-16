using System;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Formule de dégâts préliminaire, fonction pure et déterministe :
    /// <code>
    /// dégâts = arrondi( (puissance × coefficient) × Π(1 + bonus de catégorie) × critique × constante / (constante + défense effective) × (1 − réduction) )
    /// </code>
    /// Ordre de résolution : puissance → coefficient → bonus/multiplicateurs → critique → défense → garde ou parade (lot 6) → arrondi final.
    /// Chaque étape intermédiaire est arrondie à 4 décimales (au plus proche) ; seul l'arrondi final produit un entier.
    /// </summary>
    public static class DamageCalculator
    {
        public static DamageBreakdown Compute(DamageInput input, DamageRules rules)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (rules is null) throw new ArgumentNullException(nameof(rules));

            var b = new DamageBreakdown
            {
                Power = input.Attacker.GetPower(input.PowerType),
            };
            b.AfterCoefficient = b.Power * input.SkillCoefficient;

            b.CompetenceMultiplier = CategoryMultiplier(input.Bonuses.Competence, rules);
            b.TalentMultiplier = CategoryMultiplier(input.Bonuses.Talent, rules);
            b.AlterationMultiplier = CategoryMultiplier(input.Bonuses.Alteration, rules);
            b.BonusMultiplier = b.CompetenceMultiplier * b.TalentMultiplier * b.AlterationMultiplier;
            b.AfterBonuses = b.AfterCoefficient * b.BonusMultiplier;

            b.IsCritical = input.IsCritical;
            b.CriticalMultiplier = input.IsCritical ? input.Attacker.CriticalMultiplier : Fixed.One;
            b.AfterCritical = b.AfterBonuses * b.CriticalMultiplier;

            b.EffectiveDefense = input.Defender.GetEffectiveDefense(input.DamageType);
            b.DefenseConstant = input.DamageType == DamageType.Physique ? rules.ArmorConstant : rules.MagicResistanceConstant;
            if (b.DefenseConstant <= 0) throw new InvalidOperationException("La constante de défense doit être strictement positive.");
            long denominator = checked(b.DefenseConstant * Fixed.Scale + b.EffectiveDefense.Raw);
            long constantRaw = checked(b.DefenseConstant * Fixed.Scale);
            b.DefenseFactor = Fixed.FromRaw(IntMath.MulDiv(Fixed.Scale, constantRaw, denominator, RoundingMode.HalfAwayFromZero));
            b.AfterDefense = Fixed.FromRaw(IntMath.MulDiv(b.AfterCritical.Raw, constantRaw, denominator, RoundingMode.HalfAwayFromZero));

            b.Mitigation = input.Mitigation;
            b.AfterMitigation = input.Mitigation == Fixed.Zero ? b.AfterDefense : b.AfterDefense * (Fixed.One - input.Mitigation);

            long rounded = b.AfterMitigation.RoundToInt(rules.FinalRounding);
            b.FinalDamage = b.AfterMitigation > Fixed.Zero ? Math.Max(rules.MinimumDamage, rounded) : 0;
            return b;
        }

        /// <summary>Multiplicateur d'une catégorie : 1 + somme des bonus, borné entre les limites des règles (jamais négatif).</summary>
        public static Fixed CategoryMultiplier(Fixed bonusSum, DamageRules rules) =>
            Fixed.Clamp(Fixed.One + bonusSum, rules.MinCategoryMultiplier, rules.MaxCategoryMultiplier);
    }
}
