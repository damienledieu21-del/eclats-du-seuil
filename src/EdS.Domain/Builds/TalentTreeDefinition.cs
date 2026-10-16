using System;
using System.Collections.Generic;

namespace EdS.Domain.Builds
{
    /// <summary>Branche de talents d'une classe (ex. Rempart, Contrecoup, Gardien).</summary>
    public sealed class TalentTreeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<TalentDefinition> Talents { get; }

        public TalentTreeDefinition(string id, string name, IReadOnlyList<TalentDefinition> talents)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Talents = talents ?? throw new ArgumentNullException(nameof(talents));
        }
    }
}
