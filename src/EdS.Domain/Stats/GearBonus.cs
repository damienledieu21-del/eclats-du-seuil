using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EdS.Domain.Numerics;

namespace EdS.Domain.Stats
{
    /// <summary>
    /// Sous-lot 12.3 : bonus d'équipement d'un personnage, somme des objets portés (puissances, défenses, PV, endurance, résistance aux
    /// altérations). Entiers de 0 à <see cref="MaxValue"/> ; ajoutés aux statistiques dérivées comme contributions plates, avant le coefficient
    /// des compétences (formules du lot 2). Immuable. <see cref="None"/> ne change rien aux statistiques.
    /// </summary>
    public sealed class GearBonus : IEquatable<GearBonus>
    {
        /// <summary>Plus grande valeur d'un bonus (10⁶), enveloppe technique très au-delà du jeu prévu.</summary>
        public const int MaxValue = 1_000_000;

        public static readonly GearBonus None = new GearBonus(0, 0, 0, 0, 0, 0, 0, 0);

        public int PhysicalPower { get; }
        public int AgilePower { get; }
        public int MagicPower { get; }
        public int Armor { get; }
        public int MagicResistance { get; }
        public int MaxHealth { get; }
        public int MaxEndurance { get; }
        public int StatusResistance { get; }

        public GearBonus(int physicalPower = 0, int agilePower = 0, int magicPower = 0, int armor = 0, int magicResistance = 0, int maxHealth = 0,
            int maxEndurance = 0, int statusResistance = 0)
        {
            PhysicalPower = physicalPower;
            AgilePower = agilePower;
            MagicPower = magicPower;
            Armor = armor;
            MagicResistance = magicResistance;
            MaxHealth = maxHealth;
            MaxEndurance = maxEndurance;
            StatusResistance = statusResistance;
        }

        /// <summary>
        /// Identifiants (ceux des données) et valeurs, dans l'ordre fixe des fichiers et des journaux. Revue Codex de la PR #44 : liste non
        /// modifiable (un tableau public pourrait être trié par un appelant, et les noms ne correspondraient plus à l'indexeur).
        /// </summary>
        public static readonly IReadOnlyList<string> Fields = new ReadOnlyCollection<string>(new[]
            { "physicalPower", "agilePower", "magicPower", "armor", "magicResistance", "maxHealth", "maxEndurance", "statusResistance" });

        /// <summary>Rang d'un identifiant dans <see cref="Fields"/>, ou -1 s'il est inconnu.</summary>
        public static int FieldIndex(string name)
        {
            for (int i = 0; i < Fields.Count; i++)
                if (string.Equals(Fields[i], name, StringComparison.Ordinal)) return i;
            return -1;
        }

        public int this[int field] => field switch
        {
            0 => PhysicalPower,
            1 => AgilePower,
            2 => MagicPower,
            3 => Armor,
            4 => MagicResistance,
            5 => MaxHealth,
            6 => MaxEndurance,
            7 => StatusResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        /// <summary>Bonus construit champ par champ (index de <see cref="Fields"/>).</summary>
        public static GearBonus FromValues(int[] values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            if (values.Length != Fields.Count) throw new ArgumentException($"{Fields.Count} valeurs attendues.", nameof(values));
            return new GearBonus(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
        }

        public bool IsNone
        {
            get
            {
                for (int i = 0; i < Fields.Count; i++)
                    if (this[i] != 0) return false;
                return true;
            }
        }

        /// <summary>Somme champ par champ (arithmétique vérifiée).</summary>
        public static GearBonus operator +(GearBonus a, GearBonus b)
        {
            var values = new int[Fields.Count];
            for (int i = 0; i < values.Length; i++) values[i] = checked(a[i] + b[i]);
            return FromValues(values);
        }

        /// <summary>Contributions plates aux statistiques dérivées.</summary>
        public StatContributions ToContributions() => new StatContributions
        {
            PhysicalPower = Fixed.FromInt(PhysicalPower),
            AgilePower = Fixed.FromInt(AgilePower),
            MagicPower = Fixed.FromInt(MagicPower),
            Armor = Fixed.FromInt(Armor),
            MagicResistance = Fixed.FromInt(MagicResistance),
            MaxHealth = Fixed.FromInt(MaxHealth),
            MaxEndurance = Fixed.FromInt(MaxEndurance),
            StatusResistance = Fixed.FromInt(StatusResistance),
        };

        public bool Equals(GearBonus? other)
        {
            if (other is null) return false;
            for (int i = 0; i < Fields.Count; i++)
                if (this[i] != other[i]) return false;
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as GearBonus);

        public override int GetHashCode()
        {
            int h = 17;
            for (int i = 0; i < Fields.Count; i++) h = unchecked(h * 31 + this[i]);
            return h;
        }
    }
}
