using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Paramètres de la formule de dégâts. Valeurs par défaut : arbitrages D3/D4 et A9,
    /// <b>provisoires et non équilibrées</b>.
    /// </summary>
    public sealed class DamageRules
    {
        public static readonly DamageRules Provisional = new DamageRules();

        /// <summary>Constante de réduction de l'armure : facteur = constante / (constante + armure effective).</summary>
        public long ArmorConstant { get; init; } = 1000;

        /// <summary>Constante de réduction de la résistance magique, réglable séparément.</summary>
        public long MagicResistanceConstant { get; init; } = 1000;

        /// <summary>Plancher du multiplicateur d'une catégorie de bonus (1 + somme). Jamais négatif.</summary>
        public Fixed MinCategoryMultiplier { get; init; } = Fixed.Zero;

        /// <summary>Plafond du multiplicateur d'une catégorie de bonus, garde-fou contre les cumuls extrêmes.</summary>
        public Fixed MaxCategoryMultiplier { get; init; } = Fixed.FromInt(10);

        /// <summary>Arrondi final des dégâts (A9 : au plus proche, moitié loin de zéro).</summary>
        public RoundingMode FinalRounding { get; init; } = RoundingMode.HalfAwayFromZero;

        /// <summary>
        /// Dégâts minimum d'un coup qui inflige quelque chose (A9). Ne s'applique pas si le montant avant arrondi est nul
        /// (multiplicateur ramené à 0 par des malus, coefficient nul) : une immunité totale reste possible.
        /// </summary>
        public long MinimumDamage { get; init; } = 1;

        /// <summary>
        /// Plafond des constantes de défense (10⁹), enveloppe technique provisoire de pré-alpha (correctif 6.1.1, CA-M01 A) :
        /// avec une défense effective d'au plus <see cref="CombatantStats.MaxDefense"/>, le dénominateur
        /// constante × 10 000 + défense brute reste sous 2 × 10¹³, très loin de la limite d'un entier 64 bits.
        /// </summary>
        public const long MaxDefenseConstant = 1_000_000_000;

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            foreach (var (value, name) in new[] { (ArmorConstant, nameof(ArmorConstant)), (MagicResistanceConstant, nameof(MagicResistanceConstant)) })
            {
                if (value <= 0) errors.Add(new RuleViolation("doit être strictement positive.", name));
                else if (value > MaxDefenseConstant) errors.Add(new RuleViolation($"ne peut pas dépasser {MaxDefenseConstant} (limite technique provisoire).", name));
            }
            if (MinCategoryMultiplier < Fixed.Zero)
                errors.Add(new RuleViolation("ne peut pas être négatif : le multiplicateur final ne doit jamais l'être.", nameof(MinCategoryMultiplier)));
            if (MaxCategoryMultiplier < MinCategoryMultiplier)
                errors.Add(new RuleViolation("le maximum doit être supérieur ou égal au minimum.", nameof(MinCategoryMultiplier), nameof(MaxCategoryMultiplier)));
            if (!Enum.IsDefined(typeof(RoundingMode), FinalRounding))
                errors.Add(new RuleViolation("mode d'arrondi inconnu.", nameof(FinalRounding)));
            if (MinimumDamage < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", nameof(MinimumDamage)));
            return errors;
        }
    }
}
