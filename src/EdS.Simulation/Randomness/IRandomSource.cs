namespace EdS.Simulation.Randomness
{
    /// <summary>
    /// Source d'aléa injectée dans le moteur. Le moteur n'utilise jamais System.Random,
    /// dont l'algorithme dépend de la version de .NET : seul ce contrat est autorisé.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Prochain entier 32 bits uniformément réparti.</summary>
        uint NextUInt();

        /// <summary>Entier uniforme dans [0, <paramref name="bound"/>), sans biais.</summary>
        uint NextBounded(uint bound);

        /// <summary>Vrai avec une probabilité de <paramref name="chanceBasisPoints"/> / 10 000 (500 = 5 %).</summary>
        bool RollBasisPoints(int chanceBasisPoints);
    }
}
