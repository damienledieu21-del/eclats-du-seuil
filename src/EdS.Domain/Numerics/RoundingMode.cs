namespace EdS.Domain.Numerics
{
    /// <summary>
    /// Mode d'arrondi explicite. Aucun calcul de combat n'arrondit implicitement.
    /// </summary>
    public enum RoundingMode
    {
        /// <summary>Au plus proche, la moitié s'éloigne de zéro (2,5 → 3 ; -2,5 → -3). Mode par défaut (décision A9).</summary>
        HalfAwayFromZero = 0,

        /// <summary>Au plus proche, la moitié va vers le pair (2,5 → 2 ; 3,5 → 4).</summary>
        HalfToEven = 1,

        /// <summary>Troncature vers zéro.</summary>
        TowardZero = 2,

        /// <summary>Vers moins l'infini.</summary>
        Floor = 3,

        /// <summary>Vers plus l'infini.</summary>
        Ceiling = 4,
    }
}
