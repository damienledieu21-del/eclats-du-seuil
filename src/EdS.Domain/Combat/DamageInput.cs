using System;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    /// <summary>Tout ce dont la formule a besoin pour un coup ; le tirage critique est fait avant (moteur).</summary>
    public sealed class DamageInput
    {
        public DerivedStats Attacker { get; }
        public DerivedStats Defender { get; }
        public PowerType PowerType { get; }
        public DamageType DamageType { get; }

        /// <summary>Coefficient de compétence (1.2 = 120 % de la puissance). Positif ou nul.</summary>
        public Fixed SkillCoefficient { get; }

        public DamageBonuses Bonuses { get; }
        public bool IsCritical { get; }

        /// <summary>Réduction appliquée après la défense (garde, parade : 0.6 = −60 %), entre 0 et 1 ; 0 = aucune.</summary>
        public Fixed Mitigation { get; }

        public DamageInput(
            DerivedStats attacker, DerivedStats defender, PowerType powerType, DamageType damageType,
            Fixed skillCoefficient, DamageBonuses bonuses = default, bool isCritical = false, Fixed mitigation = default)
        {
            Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
            Defender = defender ?? throw new ArgumentNullException(nameof(defender));
            if (skillCoefficient < Fixed.Zero)
                throw new ArgumentOutOfRangeException(nameof(skillCoefficient), skillCoefficient, "Le coefficient de compétence ne peut pas être négatif.");
            PowerType = powerType;
            DamageType = damageType;
            SkillCoefficient = skillCoefficient;
            Bonuses = bonuses;
            IsCritical = isCritical;
            if (mitigation < Fixed.Zero || mitigation > Fixed.One)
                throw new ArgumentOutOfRangeException(nameof(mitigation), mitigation, "La réduction doit être comprise entre 0 et 1.");
            Mitigation = mitigation;
        }
    }
}
