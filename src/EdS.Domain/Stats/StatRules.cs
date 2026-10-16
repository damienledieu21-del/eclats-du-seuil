using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Domain.Stats
{
    /// <summary>
    /// Paramètres d'équilibrage des statistiques. Valeurs par défaut : arbitrages D1/D3 du 16/10/2026
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
        public Fixed BaseMaxHealth { get; init; } = Fixed.Zero;
        public Fixed BaseMaxEndurance { get; init; } = Fixed.FromInt(100);
        public Fixed BaseStatusResistance { get; init; } = Fixed.Zero;

        // Apport par point d'attribut.
        public Fixed PhysicalPowerPerForce { get; init; } = Fixed.FromInt(2);
        public Fixed AgilePowerPerDexterite { get; init; } = Fixed.FromInt(2);
        public Fixed MagicPowerPerIntelligence { get; init; } = Fixed.FromInt(2);
        public Fixed MaxHealthPerVitalite { get; init; } = Fixed.FromInt(10);
        public Fixed StatusResistancePerVolonte { get; init; } = Fixed.FromInt(2);
        public Fixed MaxEndurancePerEndurance { get; init; } = Fixed.FromInt(2);

        // Critiques.
        public int BaseCriticalChanceBasisPoints { get; init; } = 500;
        public int MinCriticalChanceBasisPoints { get; init; } = 0;
        public int MaxCriticalChanceBasisPoints { get; init; } = 6000;
        public Fixed BaseCriticalMultiplier { get; init; } = Fixed.Parse("1.5");
        public Fixed MinCriticalMultiplier { get; init; } = Fixed.One;
        public Fixed MaxCriticalMultiplier { get; init; } = Fixed.Parse("2.5");

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            void NonNegative(Fixed value, string name)
            {
                if (value < Fixed.Zero) errors.Add($"{name} ne peut pas être négatif ({value}).");
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

            if (MinCriticalChanceBasisPoints < 0 || MaxCriticalChanceBasisPoints > 10_000
                || MinCriticalChanceBasisPoints > MaxCriticalChanceBasisPoints)
                errors.Add("Bornes de chance critique invalides : 0 ≤ min ≤ max ≤ 10 000 attendu.");
            if (BaseCriticalChanceBasisPoints < MinCriticalChanceBasisPoints || BaseCriticalChanceBasisPoints > MaxCriticalChanceBasisPoints)
                errors.Add("La chance critique de base doit être comprise entre ses bornes.");
            if (MinCriticalMultiplier < Fixed.One || MinCriticalMultiplier > MaxCriticalMultiplier)
                errors.Add("Bornes du multiplicateur critique invalides : 1 ≤ min ≤ max attendu (un critique ne réduit jamais les dégâts).");
            if (BaseCriticalMultiplier < MinCriticalMultiplier || BaseCriticalMultiplier > MaxCriticalMultiplier)
                errors.Add("Le multiplicateur critique de base doit être compris entre ses bornes.");
            return errors;
        }
    }
}
