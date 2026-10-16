using System;
using System.Collections.Generic;
using EdS.Domain.Collections;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Talent permanent d'un arbre. Chaque rang coûte un point et compte dans les points investis de la branche.
    /// Les effets de combat arriveront avec le moteur (lot 5 et suivants).
    /// </summary>
    public sealed class TalentDefinition
    {
        /// <summary>Rang maximal autorisé pour un talent (correctif 6.1, M03) : garde les sommes de rangs loin des limites numériques.</summary>
        public const int MaxRankLimit = 100;

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
            Prerequisites = Frozen.ListOrEmpty(prerequisites, nameof(prerequisites));
        }
    }
}
