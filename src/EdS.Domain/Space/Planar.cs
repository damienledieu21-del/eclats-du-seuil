using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4 : bornes techniques de l'espace d'un combat. Elles gardent toute la géométrie du moteur dans des entiers 64 bits
    /// (les produits exacts de distances passent par <see cref="IntMath.CompareProducts"/>). Sous-lot V2.26 (secteurs de la grande carte,
    /// moteur 0.31.0) : coordonnées dans ±4 096 m (±1 000 m avant), donc écarts ≤ 8,192·10⁷ unités brutes, carrés ≤ 6,72·10¹⁵, sommes de
    /// carrés ≤ 1,35·10¹⁶, loin de 2⁶³ ≈ 9,2·10¹⁸. Enveloppes de pré-alpha, pas des valeurs de jeu.
    /// </summary>
    public static class SpaceLimits
    {
        /// <summary>Coordonnée maximale (en valeur absolue) d'un point de l'espace : 4 096 m depuis V2.26 (1 000 m avant).</summary>
        public const int MaxCoordinateMeters = 4096;

        public static readonly Fixed MaxCoordinate = Fixed.FromInt(MaxCoordinateMeters);

        /// <summary>Rayon d'emprise minimal (0,1 m) et maximal (5 m) d'un combattant.</summary>
        public static readonly Fixed MinRadius = Fixed.FromRatio(1, 10);
        public static readonly Fixed MaxRadius = Fixed.FromInt(5);

        /// <summary>Vitesse minimale (0,1 m/s) et maximale (20 m/s) d'un combattant, vitesse de base des règles comprise.</summary>
        public static readonly Fixed MinSpeed = Fixed.FromRatio(1, 10);
        public static readonly Fixed MaxSpeed = Fixed.FromInt(20);

        /// <summary>
        /// Sous-lot 11.4b : portée ou rayon d'une compétence, d'une application, d'un effet de réaction ou d'une protection : de 0,1 à
        /// 100 m (bornes techniques).
        /// </summary>
        public static readonly Fixed MinRange = Fixed.FromRatio(1, 10);
        public static readonly Fixed MaxRange = Fixed.FromInt(100);

        /// <summary>Une portée est-elle dans [<see cref="MinRange"/>, <see cref="MaxRange"/>] ?</summary>
        public static bool IsRange(Fixed range) => range >= MinRange && range <= MaxRange;

        /// <summary>
        /// Nombre maximal d'obstacles d'un espace (256 depuis V2.26, 64 avant ; rangés par cases, <see cref="ObstacleIndex"/>), et de sommets
        /// d'un obstacle (au moins 3).
        /// </summary>
        public const int MaxObstacles = 256;
        public const int MinVertices = 3, MaxVertices = 16;

        /// <summary>Le point est-il dans le carré de ±<see cref="MaxCoordinate"/> ?</summary>
        public static bool InRange(GroundVector p) => p.X.Abs() <= MaxCoordinate && p.Z.Abs() <= MaxCoordinate;

        /// <summary>Composantes d'une direction donnée en données (déplacement, orientation) : dans [−1, 1], pas toutes deux nulles.</summary>
        public static bool IsDirection(GroundVector v) => !v.IsZero && v.X.Abs() <= Fixed.One && v.Z.Abs() <= Fixed.One;
    }

    /// <summary>
    /// Sous-lot 11.4 : calcul plan en entiers bruts (unité 0,0001 m), sans flottant. Préconditions : composantes d'au plus
    /// <see cref="MaxComponent"/> en valeur absolue (écart de deux points de l'espace, voir <see cref="SpaceLimits"/>) ; l'arithmétique est
    /// vérifiée (<c>checked</c>).
    /// </summary>
    public static class Planar
    {
        /// <summary>Longueur brute d'un vecteur unitaire (1 m).</summary>
        public const long Unit = Fixed.Scale;

        /// <summary>Plus grande composante admise par les opérations de ce type (écart de deux points à ±4 096 m depuis V2.26).</summary>
        public const long MaxComponent = 2L * SpaceLimits.MaxCoordinateMeters * Fixed.Scale;

        public static long Dot(long ax, long az, long bx, long bz) => checked(ax * bx + az * bz);

        /// <summary>Produit vectoriel ax·bz − az·bx : positif si b est à gauche de a (sens trigonométrique du plan x, z).</summary>
        public static long Cross(long ax, long az, long bx, long bz) => checked(ax * bz - az * bx);

        public static long LengthSquared(long x, long z) => checked(x * x + z * z);

        /// <summary>
        /// Vecteur de même direction que (x, z), de longueur <paramref name="length"/> (brute), chaque composante arrondie au plus proche ;
        /// faux pour le vecteur nul. La norme est prise avec 4 ou 2 décimales de plus que l'unité brute (racine entière d'un carré mis à
        /// l'échelle), assez pour une erreur d'au plus une unité brute sur les longueurs du jeu.
        /// </summary>
        public static bool TryResize(long x, long z, long length, out long rx, out long rz)
        {
            rx = rz = 0;
            if (x == 0 && z == 0) return false;
            if (Math.Abs(x) > MaxComponent || Math.Abs(z) > MaxComponent) throw new ArgumentOutOfRangeException(nameof(x), "vecteur hors des bornes de l'espace.");
            long squared = LengthSquared(x, z);
            // squared × precision² < 2⁶³ dans les trois cas. Sous-lot V2.26 : troisième palier (10) pour les écarts au-delà de ±1 000 m ; les
            // vecteurs d'avant (somme des carrés ≤ 8·10¹⁴) gardent exactement leur palier, donc leurs résultats.
            long precision = squared <= 92_000_000_000L ? 10_000 : squared <= 920_000_000_000_000L ? 100 : 10;
            long norm = IntMath.Sqrt(checked(squared * precision * precision));   // |v| × precision, par défaut
            rx = IntMath.MulDiv(x, checked(length * precision), norm, RoundingMode.HalfAwayFromZero);
            rz = IntMath.MulDiv(z, checked(length * precision), norm, RoundingMode.HalfAwayFromZero);
            return true;
        }

        /// <summary>Vecteur unitaire (longueur <see cref="Unit"/>) de même direction ; faux pour le vecteur nul.</summary>
        public static bool TryNormalize(long x, long z, out long ux, out long uz) => TryResize(x, z, Unit, out ux, out uz);

        /// <summary>Deux disques se chevauchent-ils strictement ? (des bords qui se touchent ne se chevauchent pas)</summary>
        public static bool CirclesOverlap(long ax, long az, long ar, long bx, long bz, long br)
        {
            long reach = checked(ar + br);
            return LengthSquared(checked(ax - bx), checked(az - bz)) < checked(reach * reach);
        }

        /// <summary>
        /// Le disque (centre P, rayon r) chevauche-t-il strictement le segment [A, B] ? Test exact : distance au carré comparée à r² sans
        /// arrondi (produits sur 128 bits pour la projection intérieure).
        /// </summary>
        public static bool SegmentOverlapsCircle(long ax, long az, long bx, long bz, long px, long pz, long r)
        {
            long ex = checked(bx - ax), ez = checked(bz - az), wx = checked(px - ax), wz = checked(pz - az);
            long r2 = checked(r * r);
            long t = Dot(wx, wz, ex, ez);
            if (t <= 0) return LengthSquared(wx, wz) < r2;
            long ee = LengthSquared(ex, ez);
            if (t >= ee) return LengthSquared(checked(px - bx), checked(pz - bz)) < r2;
            long c = Math.Abs(Cross(ex, ez, wx, wz));   // distance × |AB|
            return IntMath.CompareProducts(c, c, r2, ee) < 0;
        }

        /// <summary>
        /// Sous-lot 11.4c : le disque fermé (centre P, rayon r) touche-t-il le segment [A, B] (distance au plus r, frontière comprise) ?
        /// Même calcul exact que <see cref="SegmentOverlapsCircle"/>, inégalités larges. Un segment réduit à un point est admis.
        /// </summary>
        public static bool SegmentTouchesCircle(long ax, long az, long bx, long bz, long px, long pz, long r)
        {
            long ex = checked(bx - ax), ez = checked(bz - az), wx = checked(px - ax), wz = checked(pz - az);
            long r2 = checked(r * r);
            long t = Dot(wx, wz, ex, ez);
            if (t <= 0) return LengthSquared(wx, wz) <= r2;
            long ee = LengthSquared(ex, ez);
            if (t >= ee) return LengthSquared(checked(px - bx), checked(pz - bz)) <= r2;
            long c = Math.Abs(Cross(ex, ez, wx, wz));   // distance × |AB|
            return IntMath.CompareProducts(c, c, r2, ee) <= 0;
        }

        /// <summary>Le point P est-il à au plus <paramref name="d"/> du point A (frontière comprise) ?</summary>
        public static bool WithinDistance(long ax, long az, long px, long pz, long d) =>
            LengthSquared(checked(px - ax), checked(pz - az)) <= checked(d * d);

        /// <summary>
        /// Sous-lot 11.4b : les segments fermés [A, B] et [C, D] se touchent-ils (croisement, contact d'une extrémité, chevauchement
        /// colinéaire) ? Test exact par orientations.
        /// </summary>
        public static bool SegmentsIntersect(long ax, long az, long bx, long bz, long cx, long cz, long dx, long dz)
        {
            int d1 = Math.Sign(Cross(checked(bx - ax), checked(bz - az), checked(cx - ax), checked(cz - az)));
            int d2 = Math.Sign(Cross(checked(bx - ax), checked(bz - az), checked(dx - ax), checked(dz - az)));
            int d3 = Math.Sign(Cross(checked(dx - cx), checked(dz - cz), checked(ax - cx), checked(az - cz)));
            int d4 = Math.Sign(Cross(checked(dx - cx), checked(dz - cz), checked(bx - cx), checked(bz - cz)));
            if (d1 * d2 < 0 && d3 * d4 < 0) return true;
            return (d1 == 0 && OnSegment(ax, az, bx, bz, cx, cz)) || (d2 == 0 && OnSegment(ax, az, bx, bz, dx, dz))
                || (d3 == 0 && OnSegment(cx, cz, dx, dz, ax, az)) || (d4 == 0 && OnSegment(cx, cz, dx, dz, bx, bz));
        }

        /// <summary>P, colinéaire à [A, B], est-il dans la boîte du segment (donc sur le segment) ?</summary>
        private static bool OnSegment(long ax, long az, long bx, long bz, long px, long pz) =>
            px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) && pz >= Math.Min(az, bz) && pz <= Math.Max(az, bz);

        /// <summary>Point du segment [A, B] le plus proche de P, arrondi à l'unité brute (sert à la direction d'un dégagement).</summary>
        public static void ClosestOnSegment(long ax, long az, long bx, long bz, long px, long pz, out long qx, out long qz)
        {
            long ex = checked(bx - ax), ez = checked(bz - az);
            long t = Dot(checked(px - ax), checked(pz - az), ex, ez);
            long ee = LengthSquared(ex, ez);
            if (t <= 0 || ee == 0)
            {
                qx = ax;
                qz = az;
            }
            else if (t >= ee)
            {
                qx = bx;
                qz = bz;
            }
            else
            {
                qx = checked(ax + IntMath.MulDiv(ex, t, ee, RoundingMode.HalfAwayFromZero));
                qz = checked(az + IntMath.MulDiv(ez, t, ee, RoundingMode.HalfAwayFromZero));
            }
        }
    }
}
