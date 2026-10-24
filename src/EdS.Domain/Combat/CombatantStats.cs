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

        public static DerivedStats ForCharacter(StatRules rules, CharacterBuild build) => ForCharacter(rules, build, ModifierSet.Empty);

        /// <summary>
        /// Sous-lot 8.1 : statistiques d'un personnage avec ses modificateurs permanents. <c>endurance.max</c> et
        /// <c>statusResistance</c> sont des contributions plates ; <c>maxHealth</c> est un pourcentage du PV max calculé, écrêté dans
        /// [−90 %, +1 000 %] (<see cref="CombatantRules.MinMaxHealthPercent"/>), appliqué par une multiplication en virgule fixe arrondie
        /// au plus proche ; le moteur et la validation en prennent ensuite la partie entière (plancher), comme avant. Sans modificateur,
        /// identique au calcul sans contribution. Fonction partagée par la validation et le moteur (M04).
        /// </summary>
        public static DerivedStats ForCharacter(StatRules rules, CharacterBuild build, ModifierSet modifiers) =>
            ForCharacter(rules, build, modifiers, GearBonus.None);

        /// <summary>
        /// Sous-lot 12.3 : avec le bonus de l'équipement porté, contributions plates ajoutées comme celles des modificateurs ; le pourcentage
        /// <c>maxHealth</c> des modificateurs s'applique aux PV max équipement compris. Sans équipement, identique à
        /// <see cref="ForCharacter(StatRules, CharacterBuild, ModifierSet)"/>.
        /// </summary>
        public static DerivedStats ForCharacter(StatRules rules, CharacterBuild build, ModifierSet modifiers, GearBonus gear)
        {
            if (build is null) throw new ArgumentNullException(nameof(build));
            if (modifiers is null) throw new ArgumentNullException(nameof(modifiers));
            if (gear is null) throw new ArgumentNullException(nameof(gear));
            Fixed endurance = modifiers.Sum(ModifierField.EnduranceMax), resistance = modifiers.Sum(ModifierField.StatusResistance);
            StatContributions? equipment = gear.IsNone ? null : gear.ToContributions();
            StatContributions[]? flat = endurance == Fixed.Zero && resistance == Fixed.Zero
                ? (equipment is null ? null : new[] { equipment })
                : equipment is null ? new[] { new StatContributions { MaxEndurance = endurance, StatusResistance = resistance } }
                : new[] { new StatContributions { MaxEndurance = endurance, StatusResistance = resistance }, equipment };
            DerivedStats stats = DerivedStats.Calculate(rules, build.Attributes, flat);
            Fixed percent = modifiers.Sum(ModifierField.MaxHealth);
            if (percent == Fixed.Zero) return stats;
            percent = Fixed.Clamp(percent, CombatantRules.MinMaxHealthPercent, CombatantRules.MaxMaxHealthPercent);
            Fixed extra = stats.MaxHealth * percent;
            var withExtra = new StatContributions { MaxEndurance = endurance, StatusResistance = resistance, MaxHealth = extra };
            return DerivedStats.Calculate(rules, build.Attributes, equipment is null ? new[] { withExtra } : new[] { withExtra, equipment });
        }

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
