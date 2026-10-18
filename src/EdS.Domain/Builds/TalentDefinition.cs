using System;
using System.Collections.Generic;
using EdS.Domain.Collections;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Talent permanent d'un arbre. Chaque rang coûte un point et compte dans les points investis de la branche.
    /// Depuis le lot 7.3, ses effets de combat sont des déclencheurs génériques (<see cref="Triggers"/>).
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

        /// <summary>Déclencheurs permanents (lot 7.3), actifs dès le rang 1 ; leurs valeurs peuvent dépendre du rang.</summary>
        public IReadOnlyList<Combat.TriggerDefinition> Triggers { get; }

        /// <summary>Modificateurs (sous-lot 8.1, X1 et X2), actifs dès le rang 1 ; leurs valeurs dépendent du rang.</summary>
        public IReadOnlyList<Combat.ModifierDefinition> Modifiers { get; }

        public TalentDefinition(string id, string name, int maxRank, bool isMajor = false,
            IReadOnlyList<TalentRequirement>? prerequisites = null, bool isPlaceholder = false,
            IEnumerable<Combat.TriggerDefinition>? triggers = null, IEnumerable<Combat.ModifierDefinition>? modifiers = null)
        {
            Triggers = Frozen.ListOrEmpty(triggers, nameof(triggers));
            Modifiers = Frozen.ListOrEmpty(modifiers, nameof(modifiers));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MaxRank = maxRank;
            IsMajor = isMajor;
            IsPlaceholder = isPlaceholder;
            Prerequisites = Frozen.ListOrEmpty(prerequisites, nameof(prerequisites));
        }
    }
}
