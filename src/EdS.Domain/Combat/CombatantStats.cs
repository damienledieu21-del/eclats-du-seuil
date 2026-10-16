using System;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Statistiques de départ de chaque type de combattant, calculées de la même façon par la validation du combat
    /// et par le moteur (correctif 6.1, M04) : ce que <see cref="CombatSetup.Validate"/> a pu calculer, le moteur le
    /// calcule à l'identique, si bien qu'un combat accepté ne peut pas échouer à sa construction pour ces calculs. Les bornes
    /// ci-dessous sont des enveloppes techniques provisoires de pré-alpha, pas des valeurs d'équilibrage ni des capacités
    /// mesurées (voir docs/MOTEUR.md, « Limites »).
    /// </summary>
    public static class CombatantStats
    {
        /// <summary>Dégâts bruts maximaux d'une attaque d'assaillant (10⁹), très au-delà du jeu prévu.</summary>
        public const long MaxRawDamage = 1_000_000_000;

        /// <summary>Armure et résistance magique maximales d'un mannequin ou d'un assaillant (10⁹).</summary>
        public static readonly Fixed MaxDefense = Fixed.FromInt(1_000_000_000);

        public static DerivedStats ForCharacter(StatRules rules, CharacterBuild build) =>
            DerivedStats.Calculate(rules, (build ?? throw new ArgumentNullException(nameof(build))).Attributes);

        /// <summary>
        /// Mannequin ou assaillant : seules les défenses comptent (armure, résistance magique et, depuis le lot 7.2, résistance
        /// aux altérations) ; puissances et PV de base.
        /// </summary>
        public static DerivedStats ForDefenses(StatRules rules, Fixed armor, Fixed magicResistance, Fixed statusResistance = default)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            return DerivedStats.Calculate(rules, Attributes.None, new[]
            {
                new StatContributions
                {
                    Armor = armor - rules.BaseArmor, MagicResistance = magicResistance - rules.BaseMagicResistance,
                    StatusResistance = statusResistance - rules.BaseStatusResistance,
                },
            });
        }

        /// <summary>Attaque d'assaillant : dégâts bruts = puissance du type correspondant, coefficient 1, sans critique.</summary>
        public static DerivedStats ForAssailantAttack(StatRules rules, long rawDamage)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            Fixed raw = Fixed.FromInt(rawDamage);
            return DerivedStats.Calculate(rules, Attributes.None, new[]
            {
                new StatContributions { PhysicalPower = raw - rules.BasePhysicalPower, MagicPower = raw - rules.BaseMagicPower },
            });
        }

        /// <summary>Type de puissance qui porte une attaque d'assaillant.</summary>
        public static PowerType PowerOf(DamageType type) => type == DamageType.Physique ? PowerType.Physique : PowerType.Magique;
    }
}
