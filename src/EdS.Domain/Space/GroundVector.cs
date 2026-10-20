using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4 : point ou vecteur du plan au sol, en mètres, en virgule fixe (<see cref="Fixed"/>, 0,0001 m près). Les axes sont
    /// ceux du sol de Unity : <see cref="X"/> vers l'est, <see cref="Z"/> vers le nord ; aucune hauteur (un seul niveau au lot 11).
    /// </summary>
    public readonly struct GroundVector : IEquatable<GroundVector>
    {
        public Fixed X { get; }
        public Fixed Z { get; }

        public GroundVector(Fixed x, Fixed z)
        {
            X = x;
            Z = z;
        }

        public static GroundVector FromRaw(long x, long z) => new GroundVector(Fixed.FromRaw(x), Fixed.FromRaw(z));

        public static GroundVector Parse(string x, string z) => new GroundVector(Fixed.Parse(x), Fixed.Parse(z));

        public bool IsZero => X.Raw == 0 && Z.Raw == 0;

        public bool Equals(GroundVector other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is GroundVector other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 31 + Z.GetHashCode());
        public static bool operator ==(GroundVector a, GroundVector b) => a.Equals(b);
        public static bool operator !=(GroundVector a, GroundVector b) => !a.Equals(b);

        public override string ToString() => $"[{X}, {Z}]";
    }
}
