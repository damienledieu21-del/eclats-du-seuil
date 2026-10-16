using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Domain.Stats
{
    /// <summary>
    /// Statistiques calculées d'un combattant à un instant donné. Jamais stockées en dur :
    /// toujours recalculées par <see cref="Calculate"/> depuis les attributs et les contributions.
    /// Puissances, PV, endurance et résistance aux altérations sont bornés à 0 ;
    /// les défenses gardent leur valeur brute (éventuellement négative) et exposent une valeur effective ≥ 0.
    /// </summary>
    public sealed class DerivedStats
    {
        public Fixed PhysicalPower { get; }
        public Fixed AgilePower { get; }
        public Fixed MagicPower { get; }

        /// <summary>Armure avant plancher ; peut être négative sous l'effet de malus.</summary>
        public Fixed Armor { get; }

        /// <summary>Résistance magique avant plancher ; peut être négative sous l'effet de malus.</summary>
        public Fixed MagicResistance { get; }

        public Fixed MaxHealth { get; }
        public Fixed MaxEndurance { get; }
        public Fixed StatusResistance { get; }

        /// <summary>Chance de critique effective, déjà bornée (points de base).</summary>
        public int CriticalChanceBasisPoints { get; }

        /// <summary>Multiplicateur critique effectif, déjà borné.</summary>
        public Fixed CriticalMultiplier { get; }

        private DerivedStats(
            Fixed physicalPower, Fixed agilePower, Fixed magicPower, Fixed armor, Fixed magicResistance,
            Fixed maxHealth, Fixed maxEndurance, Fixed statusResistance, int criticalChance, Fixed criticalMultiplier)
        {
            PhysicalPower = physicalPower;
            AgilePower = agilePower;
            MagicPower = magicPower;
            Armor = armor;
            MagicResistance = magicResistance;
            MaxHealth = maxHealth;
            MaxEndurance = maxEndurance;
            StatusResistance = statusResistance;
            CriticalChanceBasisPoints = criticalChance;
            CriticalMultiplier = criticalMultiplier;
        }

        public Fixed GetPower(PowerType type)
        {
            switch (type)
            {
                case PowerType.Physique: return PhysicalPower;
                case PowerType.Agile: return AgilePower;
                case PowerType.Magique: return MagicPower;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Défense effective opposée à un type de dégâts : jamais négative (arbitrage D3).</summary>
        public Fixed GetEffectiveDefense(DamageType type)
        {
            switch (type)
            {
                case DamageType.Physique: return Fixed.Max(Fixed.Zero, Armor);
                case DamageType.Magique: return Fixed.Max(Fixed.Zero, MagicResistance);
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// Calcule les statistiques : valeur = base + attribut × coefficient + somme des contributions (équipement, effets…),
        /// puis bornes. Les contributions sont additionnées dans l'ordre reçu ; l'addition étant exacte, l'ordre est sans effet.
        /// </summary>
        public static DerivedStats Calculate(StatRules rules, Attributes attributes, IEnumerable<StatContributions>? contributions = null)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            StatContributions sum = StatContributions.None;
            if (contributions != null)
                foreach (StatContributions c in contributions)
                    sum += c ?? throw new ArgumentException("Contribution nulle.", nameof(contributions));

            Fixed physical = rules.BasePhysicalPower + rules.PhysicalPowerPerForce * attributes.Force + sum.PhysicalPower;
            Fixed agile = rules.BaseAgilePower + rules.AgilePowerPerDexterite * attributes.Dexterite + sum.AgilePower;
            Fixed magic = rules.BaseMagicPower + rules.MagicPowerPerIntelligence * attributes.Intelligence + sum.MagicPower;
            Fixed armor = rules.BaseArmor + sum.Armor;
            Fixed magicResistance = rules.BaseMagicResistance + sum.MagicResistance;
            Fixed maxHealth = rules.BaseMaxHealth + rules.MaxHealthPerVitalite * attributes.Vitalite + sum.MaxHealth;
            Fixed maxEndurance = rules.BaseMaxEndurance + rules.MaxEndurancePerEndurance * attributes.Endurance + sum.MaxEndurance;
            Fixed statusResistance = rules.BaseStatusResistance + rules.StatusResistancePerVolonte * attributes.Volonte + sum.StatusResistance;

            long chance = (long)rules.BaseCriticalChanceBasisPoints + sum.CriticalChanceBasisPoints;
            int criticalChance = (int)Math.Max(rules.MinCriticalChanceBasisPoints, Math.Min(rules.MaxCriticalChanceBasisPoints, chance));
            Fixed criticalMultiplier = Fixed.Clamp(
                rules.BaseCriticalMultiplier + sum.CriticalMultiplier, rules.MinCriticalMultiplier, rules.MaxCriticalMultiplier);

            return new DerivedStats(
                Fixed.Max(Fixed.Zero, physical),
                Fixed.Max(Fixed.Zero, agile),
                Fixed.Max(Fixed.Zero, magic),
                armor,
                magicResistance,
                Fixed.Max(Fixed.Zero, maxHealth),
                Fixed.Max(Fixed.Zero, maxEndurance),
                Fixed.Max(Fixed.Zero, statusResistance),
                criticalChance,
                criticalMultiplier);
        }
    }
}
