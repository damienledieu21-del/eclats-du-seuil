using System;
using System.Collections.Generic;

namespace EdS.Domain.Builds
{
    /// <summary>Type d'emplacement qu'une compétence peut occuper.</summary>
    public enum SkillKind
    {
        Active = 0,
        Ultimate = 1,
        Passive = 2,
    }

    /// <summary>
    /// Compétence équipable (active, ultime ou passive). Les passifs équipables sont distincts des talents permanents.
    /// Coûts, recharges et effets arriveront avec le moteur.
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public SkillKind Kind { get; }
        public int MinLevel { get; }
        public IReadOnlyList<TalentRequirement> TalentRequirements { get; }
        public bool IsPlaceholder { get; }

        public SkillDefinition(string id, string name, SkillKind kind, int minLevel = 1,
            IReadOnlyList<TalentRequirement>? talentRequirements = null, bool isPlaceholder = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            MinLevel = minLevel;
            TalentRequirements = talentRequirements ?? Array.Empty<TalentRequirement>();
            IsPlaceholder = isPlaceholder;
        }
    }
}
