using System;
using System.Collections.Generic;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Talent permanent d'un arbre. Chaque rang coûte un point et compte dans les points investis de la branche.
    /// Les effets de combat arriveront avec le moteur (lot 5 et suivants).
    /// </summary>
    public sealed class TalentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxRank { get; }

        /// <summary>Talent majeur : exige le seuil de points dans sa branche et un seul rang.</summary>
        public bool IsMajor { get; }

        /// <summary>Talent intermédiaire de test, à remplacer par le vrai contenu.</summary>
        public bool IsPlaceholder { get; }

        public IReadOnlyList<TalentRequirement> Prerequisites { get; }

        public TalentDefinition(string id, string name, int maxRank, bool isMajor = false,
            IReadOnlyList<TalentRequirement>? prerequisites = null, bool isPlaceholder = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MaxRank = maxRank;
            IsMajor = isMajor;
            IsPlaceholder = isPlaceholder;
            Prerequisites = prerequisites ?? Array.Empty<TalentRequirement>();
        }
    }
}
