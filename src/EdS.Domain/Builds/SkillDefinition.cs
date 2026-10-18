using System;
using System.Collections.Generic;
using EdS.Domain.Collections;

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
    /// <see cref="Combat"/> décrit ce que fait la compétence quand on la lance ; null tant qu'elle n'est pas implémentée.
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public SkillKind Kind { get; }
        public int MinLevel { get; }
        public IReadOnlyList<TalentRequirement> TalentRequirements { get; }
        public bool IsPlaceholder { get; }

        /// <summary>Dégâts, coût et recharge ; null si la compétence ne peut pas encore être lancée.</summary>
        public SkillCombatDefinition? Combat { get; }

        /// <summary>
        /// Déclencheurs permanents tant que la compétence est équipée (lot 7.3, Q2) : passif, active ou ultime. Les effets du
        /// lancer restent des conséquences directes de <see cref="Combat"/>.
        /// </summary>
        public IReadOnlyList<EdS.Domain.Combat.TriggerDefinition> Triggers { get; }

        /// <summary>
        /// Modificateurs (sous-lot 8.1, X1 et X2) d'une compétence passive, actifs tant qu'elle est équipée ; une compétence active ou
        /// ultime n'en porte pas (refusé par <see cref="ClassDefinition.Validate"/>).
        /// </summary>
        public IReadOnlyList<EdS.Domain.Combat.ModifierDefinition> Modifiers { get; }

        public SkillDefinition(string id, string name, SkillKind kind, int minLevel = 1,
            IReadOnlyList<TalentRequirement>? talentRequirements = null, bool isPlaceholder = false,
            SkillCombatDefinition? combat = null, IEnumerable<EdS.Domain.Combat.TriggerDefinition>? triggers = null,
            IEnumerable<EdS.Domain.Combat.ModifierDefinition>? modifiers = null)
        {
            Triggers = Frozen.ListOrEmpty(triggers, nameof(triggers));
            Modifiers = Frozen.ListOrEmpty(modifiers, nameof(modifiers));
            Combat = combat;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            MinLevel = minLevel;
            TalentRequirements = Frozen.ListOrEmpty(talentRequirements, nameof(talentRequirements));
            IsPlaceholder = isPlaceholder;
        }
    }
}
