using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Domain.Stats
{
    /// <summary>
    /// Paramètres d'équilibrage des statistiques. Valeurs par défaut : arbitrages D1/D3/E1 du 16/10/2026
    /// et coefficients du cahier des charges, <b>provisoires et non équilibrés</b>.
    /// </summary>
    public sealed class StatRules
    {
        public static readonly StatRules Provisional = new StatRules();

        // Valeurs de base, avant attributs, équipement et effets.
        public Fixed BasePhysicalPower { get; init; } = Fixed.Zero;
        public Fixed BaseAgilePower { get; init; } = Fixed.Zero;
        public Fixed BaseMagicPower { get; init; } = Fixed.Zero;
        public Fixed BaseArmor { get; init; } = Fixed.Zero;
        public Fixed BaseMagicResistance { get; init; } = Fixed.Zero;
        public Fixed BaseMaxHealth { get; init; } = Fixed.FromInt(100);
        public Fixed BaseMaxEndurance { get; init; } = Fixed.FromInt(100);
        public Fixed BaseStatusResistance { get; init; } = Fixed.Zero;

        // Apport par point d'attribut.
        public Fixed PhysicalPowerPerForce { get; init; } = Fixed.FromInt(2);
        public Fixed AgilePowerPerDexterite { get; init; } = Fixed.FromInt(2);
        public Fixed MagicPowerPerIntelligence { get; init; } = Fixed.FromInt(2);
        public Fixed MaxHealthPerVitalite { get; init; } = Fixed.FromInt(10);
        public Fixed StatusResistancePerVolonte { get; init; } = Fixed.FromInt(2);
        public Fixed MaxEndurancePerEndurance { get; init; } = Fixed.FromInt(2);

        /// <summary>
        /// Constante de la résistance aux altérations (A23, lot 7.2) : la durée d'un contrôle est multipliée par
        /// K / (K + résistance effective). 1000 provisoire ; au plus 10⁹ comme les constantes de défense.
        /// </summary>
        public long StatusResistanceConstant { get; init; } = 1000;

        // Critiques.
        public int BaseCriticalChanceBasisPoints { get; init; } = 500;
        public int MinCriticalChanceBasisPoints { get; init; } = 0;
        public int MaxCriticalChanceBasisPoints { get; init; } = 6000;
        public Fixed BaseCriticalMultiplier { get; init; } = Fixed.Parse("1.5");
        public Fixed MinCriticalMultiplier { get; init; } = Fixed.One;
        public Fixed MaxCriticalMultiplier { get; init; } = Fixed.Parse("2.5");

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            void NonNegative(Fixed value, string name)
            {
                if (value < Fixed.Zero) errors.Add(new RuleViolation($"ne peut pas être négatif ({value}).", name));
            }

            NonNegative(BasePhysicalPower, nameof(BasePhysicalPower));
            NonNegative(BaseAgilePower, nameof(BaseAgilePower));
            NonNegative(BaseMagicPower, nameof(BaseMagicPower));
            NonNegative(BaseArmor, nameof(BaseArmor));
            NonNegative(BaseMagicResistance, nameof(BaseMagicResistance));
            NonNegative(BaseMaxHealth, nameof(BaseMaxHealth));
            NonNegative(BaseMaxEndurance, nameof(BaseMaxEndurance));
            NonNegative(BaseStatusResistance, nameof(BaseStatusResistance));
            NonNegative(PhysicalPowerPerForce, nameof(PhysicalPowerPerForce));
            NonNegative(AgilePowerPerDexterite, nameof(AgilePowerPerDexterite));
            NonNegative(MagicPowerPerIntelligence, nameof(MagicPowerPerIntelligence));
            NonNegative(MaxHealthPerVitalite, nameof(MaxHealthPerVitalite));
            NonNegative(StatusResistancePerVolonte, nameof(StatusResistancePerVolonte));
            NonNegative(MaxEndurancePerEndurance, nameof(MaxEndurancePerEndurance));

            if (StatusResistanceConstant <= 0)
                errors.Add(new RuleViolation("doit être strictement positive.", nameof(StatusResistanceConstant)));
            else if (StatusResistanceConstant > 1_000_000_000)
                errors.Add(new RuleViolation("ne peut pas dépasser 1000000000 (limite technique provisoire).", nameof(StatusResistanceConstant)));

            if (MinCriticalChanceBasisPoints < 0)
                errors.Add(new RuleViolation("la chance critique minimale ne peut pas être négative.", nameof(MinCriticalChanceBasisPoints)));
            if (MaxCriticalChanceBasisPoints > 10_000)
                errors.Add(new RuleViolation("la chance critique maximale ne peut pas dépasser 10 000 points de base (100 %).", nameof(MaxCriticalChanceBasisPoints)));
            if (MinCriticalChanceBasisPoints > MaxCriticalChanceBasisPoints)
                errors.Add(new RuleViolation("le minimum doit être inférieur ou égal au maximum.", nameof(MinCriticalChanceBasisPoints), nameof(MaxCriticalChanceBasisPoints)));
            else if (BaseCriticalChanceBasisPoints < MinCriticalChanceBasisPoints || BaseCriticalChanceBasisPoints > MaxCriticalChanceBasisPoints)
                errors.Add(new RuleViolation($"la chance critique de base doit être comprise entre {MinCriticalChanceBasisPoints} et {MaxCriticalChanceBasisPoints}.", nameof(BaseCriticalChanceBasisPoints)));

            if (MinCriticalMultiplier < Fixed.One)
                errors.Add(new RuleViolation("le multiplicateur critique minimal doit valoir au moins 1 : un critique ne réduit jamais les dégâts.", nameof(MinCriticalMultiplier)));
            if (MinCriticalMultiplier > MaxCriticalMultiplier)
                errors.Add(new RuleViolation("le minimum doit être inférieur ou égal au maximum.", nameof(MinCriticalMultiplier), nameof(MaxCriticalMultiplier)));
            else if (BaseCriticalMultiplier < MinCriticalMultiplier || BaseCriticalMultiplier > MaxCriticalMultiplier)
                errors.Add(new RuleViolation($"le multiplicateur critique de base doit être compris entre {MinCriticalMultiplier} et {MaxCriticalMultiplier}.", nameof(BaseCriticalMultiplier)));
            return errors;
        }
    }
}
