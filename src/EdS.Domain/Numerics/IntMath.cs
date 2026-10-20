using System;

namespace EdS.Domain.Numerics
{
    /// <summary>
    /// Arithmétique entière exacte utilisée par <see cref="Fixed"/> :
    /// division arrondie et produit-quotient sur 128 bits sans dépassement intermédiaire.
    /// Uniquement des entiers : résultat identique sur toutes les plateformes.
    /// </summary>
    public static class IntMath
    {
        /// <summary>Calcule <paramref name="numerator"/> / <paramref name="denominator"/> arrondi selon <paramref name="mode"/>.</summary>
        public static long DivRound(long numerator, long denominator, RoundingMode mode)
        {
            if (denominator == 0) throw new DivideByZeroException();
            bool negative = (numerator < 0) != (denominator < 0);
            ulong n = Abs(numerator);
            ulong d = Abs(denominator);
            return ApplySign(RoundMagnitude(n / d, n % d, d, negative, mode), negative);
        }

        /// <summary>
        /// Calcule a × b / d arrondi selon <paramref name="mode"/>, avec un produit intermédiaire sur 128 bits.
        /// Lève <see cref="OverflowException"/> si le résultat ne tient pas dans un <see cref="long"/>.
        /// </summary>
        public static long MulDiv(long a, long b, long d, RoundingMode mode)
        {
            if (d == 0) throw new DivideByZeroException();
            bool negative = ((a < 0) != (b < 0)) != (d < 0);
            ulong ua = Abs(a), ub = Abs(b), ud = Abs(d);

            ulong quotient, remainder;
            if (ua <= uint.MaxValue && ub <= uint.MaxValue)
            {
                // Chemin rapide : le produit tient dans 64 bits.
                ulong product = ua * ub;
                quotient = product / ud;
                remainder = product % ud;
            }
            else
            {
                Multiply128(ua, ub, out ulong hi, out ulong lo);
                if (hi >= ud) throw new OverflowException("Résultat de MulDiv hors de la plage d'un entier 64 bits.");
                Divide128(hi, lo, ud, out quotient, out remainder);
            }

            return ApplySign(RoundMagnitude(quotient, remainder, ud, negative, mode), negative);
        }

        /// <summary>
        /// Sous-lot 11.4 : racine carrée entière par défaut, ⌊√<paramref name="n"/>⌋, d'un entier positif ou nul. Méthode bit à bit,
        /// sans flottant : même résultat sur toutes les plateformes.
        /// </summary>
        public static long Sqrt(long n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), n, "racine d'un nombre négatif.");
            ulong x = (ulong)n, result = 0, bit = 1UL << 62;
            while (bit > x) bit >>= 2;
            while (bit != 0)
            {
                if (x >= result + bit)
                {
                    x -= result + bit;
                    result = (result >> 1) + bit;
                }
                else result >>= 1;
                bit >>= 2;
            }
            return (long)result;
        }

        /// <summary>
        /// Sous-lot 11.4 : compare exactement <paramref name="a"/> × <paramref name="b"/> et <paramref name="c"/> × <paramref name="d"/>
        /// (entiers positifs ou nuls), produits sur 128 bits : −1, 0 ou 1. Sert aux tests exacts de distance du moteur spatial.
        /// </summary>
        public static int CompareProducts(long a, long b, long c, long d)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) throw new ArgumentOutOfRangeException(nameof(a), "facteurs positifs ou nuls attendus.");
            Multiply128((ulong)a, (ulong)b, out ulong hi1, out ulong lo1);
            Multiply128((ulong)c, (ulong)d, out ulong hi2, out ulong lo2);
            if (hi1 != hi2) return hi1 < hi2 ? -1 : 1;
            if (lo1 != lo2) return lo1 < lo2 ? -1 : 1;
            return 0;
        }

        private static ulong RoundMagnitude(ulong q, ulong r, ulong d, bool negative, RoundingMode mode)
        {
            if (r == 0) return q;
            bool up;
            switch (mode)
            {
                case RoundingMode.TowardZero:
                    up = false;
                    break;
                case RoundingMode.Floor:
                    up = negative;
                    break;
                case RoundingMode.Ceiling:
                    up = !negative;
                    break;
                case RoundingMode.HalfAwayFromZero:
                    up = r >= d - r;
                    break;
                case RoundingMode.HalfToEven:
                    ulong rest = d - r;
                    up = r > rest || (r == rest && (q & 1UL) == 1UL);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            return up ? checked(q + 1) : q;
        }

        private static long ApplySign(ulong magnitude, bool negative)
        {
            if (negative)
            {
                if (magnitude > (ulong)long.MaxValue + 1UL) throw new OverflowException();
                return magnitude == (ulong)long.MaxValue + 1UL ? long.MinValue : -(long)magnitude;
            }
            if (magnitude > long.MaxValue) throw new OverflowException();
            return (long)magnitude;
        }

        private static ulong Abs(long value) =>
            value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;

        private static void Multiply128(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong aLo = a & 0xFFFFFFFFUL, aHi = a >> 32;
            ulong bLo = b & 0xFFFFFFFFUL, bHi = b >> 32;

            ulong ll = aLo * bLo;
            ulong lh = aLo * bHi;
            ulong hl = aHi * bLo;
            ulong hh = aHi * bHi;

            ulong mid = (ll >> 32) + (lh & 0xFFFFFFFFUL) + (hl & 0xFFFFFFFFUL);
            lo = (mid << 32) | (ll & 0xFFFFFFFFUL);
            hi = hh + (lh >> 32) + (hl >> 32) + (mid >> 32);
        }

        // Division 128 / 64 bits par décalages successifs ; précondition : hi < d.
        private static void Divide128(ulong hi, ulong lo, ulong d, out ulong quotient, out ulong remainder)
        {
            ulong q = 0;
            ulong r = hi;
            for (int i = 63; i >= 0; i--)
            {
                bool carry = (r >> 63) != 0;
                r = (r << 1) | ((lo >> i) & 1UL);
                q <<= 1;
                if (carry || r >= d)
                {
                    r -= d;
                    q |= 1UL;
                }
            }
            quotient = q;
            remainder = r;
        }
    }
}
