using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4 : géométrie exacte du plan au sol, en entiers (valeurs brutes de <see cref="Fixed"/>, 1 = 0,0001 m). Les bornes de
    /// <see cref="SpaceLimits"/> garantissent que tous les carrés tiennent dans un entier 64 bits ; les opérations restent vérifiées.
    /// </summary>
    public static class SpaceMath
    {
        /// <summary>Racine carrée entière par défaut (⌊√n⌋), exacte, sans virgule flottante.</summary>
        public static ulong FloorSqrt(ulong n)
        {
            if (n < 2) return n;
            // Méthode de Newton entière, partant de ⌈n/2⌉ ≥ √n ; décroissance stricte jusqu'à ⌊√n⌋.
            ulong x = n;
            ulong y = (x >> 1) + (x & 1);
            while (y < x)
            {
                x = y;
                y = (x + n / x) >> 1;
            }
            return x;
        }

        /// <summary>Racine carrée entière par excès (⌈√n⌉).</summary>
        public static ulong CeilSqrt(ulong n)
        {
            ulong r = FloorSqrt(n);
            return r * r == n ? r : r + 1;
        }

        /// <summary>Carré de la distance entre deux points, en unités brutes au carré (exact).</summary>
        public static long DistanceSquaredRaw(GroundVector a, GroundVector b)
        {
            long dx = checked(a.X.Raw - b.X.Raw);
            long dz = checked(a.Z.Raw - b.Z.Raw);
            return checked(dx * dx + dz * dz);
        }

        /// <summary>Carré de la longueur d'un vecteur, en unités brutes au carré (exact).</summary>
        public static long LengthSquaredRaw(GroundVector v) => checked(v.X.Raw * v.X.Raw + v.Z.Raw * v.Z.Raw);

        /// <summary>
        /// Portée « entre les bords des emprises » (règles du combat dans l'espace, section 4) : vrai si la distance entre les bords de deux
        /// cercles de rayons <paramref name="radiusA"/> et <paramref name="radiusB"/> est au plus <paramref name="range"/>, c'est-à-dire si
        /// la distance des centres est au plus portée + rayons. Comparaison exacte des carrés : la frontière est incluse au 0,0001 m près.
        /// </summary>
        public static bool WithinReach(GroundVector a, Fixed radiusA, GroundVector b, Fixed radiusB, Fixed range)
        {
            long reach = checked(range.Raw + radiusA.Raw + radiusB.Raw);
            if (reach < 0) return false;
            return DistanceSquaredRaw(a, b) <= checked(reach * reach);
        }

        /// <summary>
        /// Déplacement d'au plus <paramref name="lengthRaw"/> unités brutes dans la direction <paramref name="direction"/> (non nulle) :
        /// chaque composante est <c>composante × longueur ÷ ⌈|direction|⌉</c>, arrondie vers zéro. La longueur obtenue ne dépasse
        /// jamais <paramref name="lengthRaw"/> : la vitesse est bornée, personne ne se téléporte.
        /// </summary>
        public static GroundVector Scale(GroundVector direction, long lengthRaw)
        {
            if (direction.IsZero) throw new ArgumentException("direction nulle.", nameof(direction));
            if (lengthRaw < 0) throw new ArgumentOutOfRangeException(nameof(lengthRaw));
            long norm = checked((long)CeilSqrt((ulong)LengthSquaredRaw(direction)));
            return new GroundVector(
                Fixed.FromRaw(IntMath.MulDiv(direction.X.Raw, lengthRaw, norm, RoundingMode.TowardZero)),
                Fixed.FromRaw(IntMath.MulDiv(direction.Z.Raw, lengthRaw, norm, RoundingMode.TowardZero)));
        }

        /// <summary>Direction ramenée à une longueur d'au plus 1 m (orientation journalisée et affichée).</summary>
        public static GroundVector Normalize(GroundVector direction) => Scale(direction, Fixed.Scale);
    }

    /// <summary>
    /// Sous-lot 11.4 : enveloppes techniques de l'espace (pré-alpha), très au-delà du jeu prévu. Elles bornent les entiers : une
    /// coordonnée de 10 km vaut 10⁸ unités brutes, une différence 2·10⁸, la somme de deux carrés 8·10¹⁶ &lt; 2⁶³.
    /// </summary>
    public static class SpaceLimits
    {
        /// <summary>Coordonnée absolue maximale d'un point ou d'une composante de direction : 10 000 m.</summary>
        public static readonly Fixed MaxCoordinate = Fixed.FromInt(10_000);

        /// <summary>Rayon maximal d'une emprise : 50 m.</summary>
        public static readonly Fixed MaxRadius = Fixed.FromInt(50);

        /// <summary>Portée maximale d'une compétence : 1 000 m.</summary>
        public static readonly Fixed MaxRange = Fixed.FromInt(1_000);

        /// <summary>Vitesse de base maximale : 100 m/s.</summary>
        public static readonly Fixed MaxSpeed = Fixed.FromInt(100);

        /// <summary>Vrai si |valeur| ≤ <see cref="MaxCoordinate"/>.</summary>
        public static bool IsCoordinate(Fixed value) => value >= -MaxCoordinate && value <= MaxCoordinate;
    }
}
