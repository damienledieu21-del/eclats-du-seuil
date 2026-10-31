using System;
using System.Linq;
using EdS.Domain.Combat;

namespace EdS.Data.Zones
{
    /// <summary>
    /// Sous-lot V1.8 (sujet 4, choix de Damien : zones en mélange) : chaque lieu a un niveau minimum ; si le joueur est plus fort, les
    /// créatures montent à son niveau, moins <see cref="EaseLevels"/> (le réglage qui les garde un peu plus faciles, pour se sentir
    /// puissant). Les chiffres des créatures du scénario sont ceux du niveau <see cref="BaseLevel"/> (formes des chefs :
    /// <see cref="BossBaseLevel"/>) ; chaque niveau de plus ajoute <see cref="HealthPerLevelPercent"/> % de leurs PV et
    /// <see cref="DamagePerLevelPercent"/> % de leurs dégâts bruts (sans jamais les baisser). L'expérience d'une créature vaincue suit ses
    /// PV maximaux : elle monte avec elle. Calcul hors du moteur : la session part d'un scénario déjà réglé (en-tête du journal compris).
    /// </summary>
    public sealed class ZoneScaling
    {
        /// <summary>Bornes de validité : pour cent par niveau, niveaux de facilité.</summary>
        public const int MaxPercentPerLevel = 100, MaxEaseLevels = 10;

        public int BaseLevel { get; }
        public int BossBaseLevel { get; }
        public int HealthPerLevelPercent { get; }
        public int DamagePerLevelPercent { get; }
        public int EaseLevels { get; }

        internal ZoneScaling(int baseLevel, int bossBaseLevel, int healthPerLevelPercent, int damagePerLevelPercent, int easeLevels)
        {
            BaseLevel = baseLevel;
            BossBaseLevel = bossBaseLevel;
            HealthPerLevelPercent = healthPerLevelPercent;
            DamagePerLevelPercent = damagePerLevelPercent;
            EaseLevels = easeLevels;
        }

        /// <summary>Niveau d'une créature d'un lieu de niveau minimum <paramref name="minLevel"/> face à un joueur de niveau <paramref name="playerLevel"/>.</summary>
        public int CreatureLevel(int minLevel, int playerLevel) => Math.Max(minLevel, playerLevel - EaseLevels);

        /// <summary>Pour cent d'une valeur faite pour le niveau <paramref name="from"/> à porter au niveau <paramref name="to"/> (au moins 100).</summary>
        public static long Percent(int perLevel, int from, int to) => 100L + (long)perLevel * Math.Max(0, to - from);

        /// <summary>
        /// La créature <paramref name="creature"/>, faite pour le niveau <paramref name="from"/>, portée au niveau <paramref name="to"/> : PV et
        /// dégâts bruts de chaque attaque multipliés (arrondi vers le bas, au plus <see cref="AssailantSetup.MaxHealth"/> PV). La même
        /// créature si rien ne change.
        /// </summary>
        public AssailantSetup Scale(AssailantSetup creature, int from, int to)
        {
            if (creature is null) throw new ArgumentNullException(nameof(creature));
            if (to <= from || (HealthPerLevelPercent == 0 && DamagePerLevelPercent == 0)) return creature;
            long hp = Percent(HealthPerLevelPercent, from, to), dmg = Percent(DamagePerLevelPercent, from, to);
            long? health = creature.Health is long h ? Math.Min(AssailantSetup.MaxHealth, checked(h * hp) / 100) : (long?)null;
            var attacks = creature.Attacks.Select(a => new AssailantAttack(a.Id, checked(a.RawDamage * dmg) / 100, a.DamageType, a.Targeting, a.Unblockable,
                a.Applies, a.Shape, a.TelegraphMs, a.Range, a.Radius, a.Angle, a.Width, a.Center, a.PersistentMs, a.PulseMs, a.GuardableOverride,
                a.ParryableOverride, a.Charge, a.Push, a.HoldPosition)).ToList();
            return new AssailantSetup(creature.Id, creature.Team, creature.Armor, creature.MagicResistance, creature.Target, creature.FirstAttackMs,
                creature.IntervalMs, attacks, creature.Initiative, creature.StatusResistance, creature.Triggers, health, creature.Placement,
                creature.ArrivalMs, creature.Behavior);
        }
    }
}
