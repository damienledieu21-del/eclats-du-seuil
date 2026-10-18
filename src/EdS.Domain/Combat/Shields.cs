using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot 8.2 (X4) : valeur d'un bouclier d'absorption, fixée à son application. Une seule fonction, partagée par le précontrôle
    /// numérique (<see cref="CombatSetup"/>) et le moteur, si bien qu'un combat accepté ne peut pas déborder en posant un bouclier.
    /// </summary>
    public static class Shields
    {
        /// <summary>
        /// ⌊ PV max de la source × coefficient de l'application × (1 + <c>shield.potencyDealt</c> de la source) ⌋, en virgule fixe
        /// (produits arrondis à 4 décimales), jamais négative. Une source sans PV max (mannequin, assaillant) pose un bouclier nul, refusé
        /// par le moteur (<c>empty</c>). Opérations vérifiées : un dépassement lève <see cref="System.OverflowException"/>.
        /// </summary>
        public static long Value(long? sourceMaxHealth, Fixed coefficient, Fixed potency)
        {
            if (sourceMaxHealth is not long maxHealth || maxHealth <= 0 || coefficient <= Fixed.Zero) return 0;
            Fixed factor = Fixed.Max(Fixed.Zero, Fixed.One + potency);
            Fixed value = Fixed.FromInt(maxHealth) * coefficient * factor;
            return value <= Fixed.Zero ? 0 : value.RoundToInt(RoundingMode.Floor);
        }
    }
}
