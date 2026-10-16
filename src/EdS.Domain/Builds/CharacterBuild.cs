using System;
using System.Collections.Generic;
using EdS.Domain.Collections;
using EdS.Domain.Stats;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Build d'un personnage : niveau, attributs fournis explicitement (F5), rangs de talents et compétences équipées.
    /// Immuable ; sa validité se vérifie avec <see cref="BuildValidator"/>.
    /// </summary>
    public sealed class CharacterBuild
    {
        public string ClassId { get; }
        public int Level { get; }
        public Attributes Attributes { get; }

        /// <summary>Rang acquis par talent (identifiant → rang).</summary>
        public IReadOnlyDictionary<string, int> TalentRanks { get; }

        /// <summary>Compétences actives, une par emplacement, dans l'ordre des emplacements.</summary>
        public IReadOnlyList<string> ActiveSkills { get; }

        /// <summary>Compétence dans l'emplacement d'ultime, ou null.</summary>
        public string? Ultimate { get; }

        public IReadOnlyList<string> PassiveSkills { get; }

        public CharacterBuild(string classId, int level, Attributes attributes,
            IReadOnlyDictionary<string, int>? talentRanks = null,
            IReadOnlyList<string>? activeSkills = null, string? ultimate = null,
            IReadOnlyList<string>? passiveSkills = null)
        {
            ClassId = classId ?? throw new ArgumentNullException(nameof(classId));
            Level = level;
            Attributes = attributes;
            // Copies figées (M05) : modifier les collections passées ne modifie pas le build.
            TalentRanks = Frozen.Dictionary(talentRanks ?? new Dictionary<string, int>(), nameof(talentRanks));
            ActiveSkills = Frozen.ListOrEmpty(activeSkills, nameof(activeSkills));
            Ultimate = ultimate;
            PassiveSkills = Frozen.ListOrEmpty(passiveSkills, nameof(passiveSkills));
        }
    }
}
