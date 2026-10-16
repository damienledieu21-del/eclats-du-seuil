using System;
using EdS.Domain.Stats;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Combat
{
    /// <summary>
    /// Tirage critique : seul endroit où l'aléa entre dans le calcul des dégâts.
    /// Consomme toujours exactement un tirage, même à 0 % ou 100 %, pour que la suite aléatoire
    /// ne dépende pas des statistiques des combattants.
    /// </summary>
    public static class CriticalRoll
    {
        public static bool Roll(IRandomSource random, DerivedStats attacker)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (attacker is null) throw new ArgumentNullException(nameof(attacker));
            return random.RollBasisPoints(attacker.CriticalChanceBasisPoints);
        }
    }
}
