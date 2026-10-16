using System;

namespace EdS.Domain.Builds
{
    /// <summary>Exige qu'un talent soit acquis au moins à un rang donné.</summary>
    public sealed class TalentRequirement
    {
        public string TalentId { get; }
        public int MinRank { get; }

        public TalentRequirement(string talentId, int minRank = 1)
        {
            TalentId = talentId ?? throw new ArgumentNullException(nameof(talentId));
            MinRank = minRank;
        }

        public override string ToString() => $"{TalentId} rang {MinRank}";
    }
}
