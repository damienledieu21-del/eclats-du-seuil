using System;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4c : angles en entiers, sans flottant (déterminisme). Sinus et cosinus d'un angle donné en demi-degrés, à l'échelle
    /// 10⁹, calculés par une série de Taylor en entiers 64 bits (division entière tronquée, donc identiques partout) ; exacts aux multiples
    /// de 90°. Servent à poser les côtés d'un cône (souffle d'une attaque) et l'arc de couverture du bouclier.
    /// </summary>
    public static class Angles
    {
        /// <summary>Échelle des sinus et cosinus : 10⁹ vaut 1.</summary>
        public const long Scale = 1_000_000_000;

        /// <summary>π × 10⁹, arrondi.</summary>
        private const long Pi = 3_141_592_654;

        /// <summary>Sinus et cosinus de <paramref name="halfDegrees"/> demi-degrés (tout entier ; ramené à [0, 720[), à l'échelle 10⁹.</summary>
        public static void SinCos(int halfDegrees, out long sin, out long cos)
        {
            int h = ((halfDegrees % 720) + 720) % 720;
            int quadrant = h / 180, rest = h % 180;   // rest dans [0, 180[ : [0°, 90°[
            long s = SinFirstQuadrant(rest), c = SinFirstQuadrant(180 - rest);
            switch (quadrant)
            {
                case 0: sin = s; cos = c; break;
                case 1: sin = c; cos = -s; break;
                case 2: sin = -s; cos = -c; break;
                default: sin = -c; cos = s; break;
            }
        }

        /// <summary>Sinus d'un angle de [0°, 90°] donné en demi-degrés (0 à 180), échelle 10⁹ ; 0 et 10⁹ exacts aux bornes.</summary>
        private static long SinFirstQuadrant(int halfDegrees)
        {
            if (halfDegrees == 0) return 0;
            if (halfDegrees == 180) return Scale;
            long x = Pi * halfDegrees / 360;   // radians × 10⁹, ≤ 1,58·10⁹
            long term = x, sum = x;
            for (int k = 1; k <= 10 && term != 0; k++)
            {
                term = checked(term * x) / Scale;
                term = checked(term * x) / Scale;
                term = -term / ((2L * k) * (2L * k + 1));
                sum += term;
            }
            return Math.Max(0, Math.Min(Scale, sum));
        }

        /// <summary>
        /// Tourne (x, z) de <paramref name="halfDegrees"/> demi-degrés dans le sens trigonométrique du plan (de x vers z), arrondi à
        /// l'unité brute. Préconditions : composantes d'au plus 10⁹ en valeur absolue (un vecteur unitaire en a 10⁴).
        /// </summary>
        public static void Rotate(long x, long z, int halfDegrees, out long rx, out long rz)
        {
            SinCos(halfDegrees, out long s, out long c);
            rx = checked(x * c - z * s) / Scale;
            rz = checked(x * s + z * c) / Scale;
        }

        /// <summary>
        /// Le vecteur v est-il dans l'arc de <paramref name="arcDegrees"/> degrés (1 à 360) centré sur l'axe (non nul) ? L'angle entre l'axe
        /// et v doit valoir au plus la moitié de l'arc, frontière comprise. v nul : dedans. Exact pour 180° (demi-plan, produit scalaire)
        /// et 360° ; sinon, côtés posés par <see cref="Rotate"/> et produits vectoriels exacts.
        /// </summary>
        public static bool WithinArc(long axisX, long axisZ, long vx, long vz, int arcDegrees)
        {
            if (vx == 0 && vz == 0) return true;
            if (arcDegrees >= 360) return true;
            if (arcDegrees == 180) return Planar.Dot(axisX, axisZ, vx, vz) >= 0;
            Planar.TryNormalize(axisX, axisZ, out long ux, out long uz);
            Rotate(ux, uz, arcDegrees, out long lx, out long lz);    // côté gauche : +moitié de l'arc (arcDegrees demi-degrés)
            Rotate(ux, uz, -arcDegrees, out long rx, out long rz);   // côté droit
            if (arcDegrees < 180)
                return Planar.Cross(rx, rz, vx, vz) >= 0 && Planar.Cross(vx, vz, lx, lz) >= 0 && Planar.Dot(ux, uz, vx, vz) > 0;
            // Arc de plus de 180° : dehors seulement strictement entre les deux côtés, du côté opposé à l'axe.
            return !(Planar.Cross(lx, lz, vx, vz) > 0 && Planar.Cross(vx, vz, rx, rz) > 0);
        }
    }
}
