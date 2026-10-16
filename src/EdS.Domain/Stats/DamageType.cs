namespace EdS.Domain.Stats
{
    /// <summary>
    /// Type de dégâts, qui choisit la défense opposée : physique → armure, magique → résistance magique.
    /// Indépendant du <see cref="PowerType"/> : une compétence agile inflige par défaut des dégâts physiques.
    /// </summary>
    public enum DamageType
    {
        Physique = 0,
        Magique = 1,
    }
}
