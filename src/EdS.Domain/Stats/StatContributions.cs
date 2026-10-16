using EdS.Domain.Numerics;

namespace EdS.Domain.Stats
{
    /// <summary>
    /// Contributions plates aux statistiques venant d'une source (équipement, effet, talent).
    /// Les valeurs peuvent être négatives (malus, réduction d'armure) ; les bornes sont appliquées
    /// au calcul des statistiques dérivées, jamais ici. Les sources s'additionnent avec <c>+</c>.
    /// </summary>
    public sealed class StatContributions
    {
        public static readonly StatContributions None = new StatContributions();

        public Fixed PhysicalPower { get; init; }
        public Fixed AgilePower { get; init; }
        public Fixed MagicPower { get; init; }
        public Fixed Armor { get; init; }
        public Fixed MagicResistance { get; init; }
        public Fixed MaxHealth { get; init; }
        public Fixed MaxEndurance { get; init; }
        public Fixed StatusResistance { get; init; }

        /// <summary>Bonus de chance de critique en points de base (100 = +1 %).</summary>
        public int CriticalChanceBasisPoints { get; init; }

        /// <summary>Bonus au multiplicateur critique (0.25 = +0,25).</summary>
        public Fixed CriticalMultiplier { get; init; }

        public Fixed GetPower(PowerType type) =>
            type == PowerType.Physique ? PhysicalPower : type == PowerType.Agile ? AgilePower : MagicPower;

        public static StatContributions operator +(StatContributions a, StatContributions b) => new StatContributions
        {
            PhysicalPower = a.PhysicalPower + b.PhysicalPower,
            AgilePower = a.AgilePower + b.AgilePower,
            MagicPower = a.MagicPower + b.MagicPower,
            Armor = a.Armor + b.Armor,
            MagicResistance = a.MagicResistance + b.MagicResistance,
            MaxHealth = a.MaxHealth + b.MaxHealth,
            MaxEndurance = a.MaxEndurance + b.MaxEndurance,
            StatusResistance = a.StatusResistance + b.StatusResistance,
            CriticalChanceBasisPoints = checked(a.CriticalChanceBasisPoints + b.CriticalChanceBasisPoints),
            CriticalMultiplier = a.CriticalMultiplier + b.CriticalMultiplier,
        };
    }
}
