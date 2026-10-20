using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4 : point ou direction sur le plan au sol (x, z), en mètres, en virgule fixe (0,0001 m près). Aucun nombre à virgule
    /// flottante : les distances se comparent au carré, en entiers exacts (<see cref="SpaceMath"/>).
    /// </summary>
    public readonly struct GroundVector : IEquatable<GroundVector>
    {
        public static readonly GroundVector Zero = new GroundVector(Fixed.Zero, Fixed.Zero);

        public Fixed X { get; }
        public Fixed Z { get; }

        public GroundVector(Fixed x, Fixed z)
        {
            X = x;
            Z = z;
        }

        public bool IsZero => X.Raw == 0 && Z.Raw == 0;

        public bool Equals(GroundVector other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is GroundVector other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 31 + Z.GetHashCode());
        public static bool operator ==(GroundVector a, GroundVector b) => a.Equals(b);
        public static bool operator !=(GroundVector a, GroundVector b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Z})";
    }
}
