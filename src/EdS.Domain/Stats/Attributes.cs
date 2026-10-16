using System;

namespace EdS.Domain.Stats
{
    /// <summary>Valeurs des six attributs d'un personnage (entiers positifs ou nuls).</summary>
    public readonly struct Attributes : IEquatable<Attributes>
    {
        public int Force { get; }
        public int Dexterite { get; }
        public int Intelligence { get; }
        public int Vitalite { get; }
        public int Volonte { get; }
        public int Endurance { get; }

        public Attributes(int force, int dexterite, int intelligence, int vitalite, int volonte, int endurance)
        {
            Force = RequirePositive(force, nameof(force));
            Dexterite = RequirePositive(dexterite, nameof(dexterite));
            Intelligence = RequirePositive(intelligence, nameof(intelligence));
            Vitalite = RequirePositive(vitalite, nameof(vitalite));
            Volonte = RequirePositive(volonte, nameof(volonte));
            Endurance = RequirePositive(endurance, nameof(endurance));
        }

        public static Attributes None => default;

        public int this[AttributeKind kind]
        {
            get
            {
                switch (kind)
                {
                    case AttributeKind.Force: return Force;
                    case AttributeKind.Dexterite: return Dexterite;
                    case AttributeKind.Intelligence: return Intelligence;
                    case AttributeKind.Vitalite: return Vitalite;
                    case AttributeKind.Volonte: return Volonte;
                    case AttributeKind.Endurance: return Endurance;
                    default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }
            }
        }

        private static int RequirePositive(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name, value, "Un attribut ne peut pas être négatif.");
            return value;
        }

        public bool Equals(Attributes other) =>
            Force == other.Force && Dexterite == other.Dexterite && Intelligence == other.Intelligence
            && Vitalite == other.Vitalite && Volonte == other.Volonte && Endurance == other.Endurance;

        public override bool Equals(object? obj) => obj is Attributes other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Force;
                h = h * 31 + Dexterite;
                h = h * 31 + Intelligence;
                h = h * 31 + Vitalite;
                h = h * 31 + Volonte;
                return h * 31 + Endurance;
            }
        }
    }
}
