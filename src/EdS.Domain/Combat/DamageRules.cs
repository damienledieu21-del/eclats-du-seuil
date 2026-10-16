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

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (ArmorConstant <= 0) errors.Add("ArmorConstant doit être strictement positive.");
            if (MagicResistanceConstant <= 0) errors.Add("MagicResistanceConstant doit être strictement positive.");
            if (MinCategoryMultiplier < Fixed.Zero) errors.Add("MinCategoryMultiplier ne peut pas être négatif : le multiplicateur final ne doit jamais l'être.");
            if (MaxCategoryMultiplier < MinCategoryMultiplier) errors.Add("MaxCategoryMultiplier doit être supérieur ou égal à MinCategoryMultiplier.");
            if (MinimumDamage < 0) errors.Add("MinimumDamage ne peut pas être négatif.");
            return errors;
        }
    }
}
