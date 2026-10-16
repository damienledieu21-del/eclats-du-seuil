using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Somme des bonus de dégâts par catégorie, en fraction (0.2 = +20 %, -0.1 = -10 %).
    /// Immuable : <see cref="Add"/> renvoie une nouvelle valeur.
    /// </summary>
    public readonly struct DamageBonuses : IEquatable<DamageBonuses>
    {
        public static DamageBonuses None => default;

        public Fixed Competence { get; }
        public Fixed Talent { get; }
        public Fixed Alteration { get; }

        private DamageBonuses(Fixed competence, Fixed talent, Fixed alteration)
        {
            Competence = competence;
            Talent = talent;
            Alteration = alteration;
        }

        public Fixed this[BonusCategory category]
        {
            get
            {
                switch (category)
                {
                    case BonusCategory.Competence: return Competence;
                    case BonusCategory.Talent: return Talent;
                    case BonusCategory.Alteration: return Alteration;
                    default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
                }
            }
        }

        /// <summary>Ajoute un bonus (ou malus si négatif) à une catégorie.</summary>
        public DamageBonuses Add(BonusCategory category, Fixed bonus)
        {
            switch (category)
            {
                case BonusCategory.Competence: return new DamageBonuses(Competence + bonus, Talent, Alteration);
                case BonusCategory.Talent: return new DamageBonuses(Competence, Talent + bonus, Alteration);
                case BonusCategory.Alteration: return new DamageBonuses(Competence, Talent, Alteration + bonus);
                default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        public bool Equals(DamageBonuses other) =>
            Competence == other.Competence && Talent == other.Talent && Alteration == other.Alteration;

        public override bool Equals(object? obj) => obj is DamageBonuses other && Equals(other);

        public override int GetHashCode() => unchecked((Competence.GetHashCode() * 31 + Talent.GetHashCode()) * 31 + Alteration.GetHashCode());
    }
}
