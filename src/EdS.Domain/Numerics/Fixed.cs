using System;
using System.Globalization;

namespace EdS.Domain.Numerics
{
    /// <summary>
    /// Nombre à virgule fixe signé, 4 décimales exactes (échelle 10 000), stocké sur 64 bits.
    /// Remplace float/double dans tout le chemin de combat : mêmes résultats sur PC, mobile et serveur.
    /// Toutes les opérations sont vérifiées (<see cref="OverflowException"/> en cas de dépassement)
    /// et toute perte de précision passe par un <see cref="RoundingMode"/> explicite.
    /// </summary>
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        /// <summary>Nombre d'unités brutes par unité entière.</summary>
        public const long Scale = 10_000;

        /// <summary>Nombre de décimales représentées.</summary>
        public const int Decimals = 4;

        /// <summary>Arrondi appliqué par les opérateurs * et /.</summary>
        public const RoundingMode DefaultRounding = RoundingMode.HalfAwayFromZero;

        public static readonly Fixed Zero = new Fixed(0);
        public static readonly Fixed One = new Fixed(Scale);
        public static readonly Fixed Epsilon = new Fixed(1);
        public static readonly Fixed MaxValue = new Fixed(long.MaxValue);
        public static readonly Fixed MinValue = new Fixed(long.MinValue);

        /// <summary>Valeur brute : la valeur réelle vaut <c>Raw / 10 000</c>.</summary>
        public long Raw { get; }

        private Fixed(long raw) => Raw = raw;

        public static Fixed FromRaw(long raw) => new Fixed(raw);

        public static Fixed FromInt(long value) => new Fixed(checked(value * Scale));

        /// <summary>Construit <paramref name="numerator"/> / <paramref name="denominator"/>, arrondi à 4 décimales.</summary>
        public static Fixed FromRatio(long numerator, long denominator, RoundingMode mode = DefaultRounding) =>
            new Fixed(IntMath.MulDiv(numerator, Scale, denominator, mode));

        /// <summary>Construit un pourcentage exprimé en points de base (500 = 5 % = 0,05).</summary>
        public static Fixed FromBasisPoints(long basisPoints) => new Fixed(basisPoints);

        public static Fixed Multiply(Fixed a, Fixed b, RoundingMode mode) =>
            new Fixed(IntMath.MulDiv(a.Raw, b.Raw, Scale, mode));

        public static Fixed Divide(Fixed a, Fixed b, RoundingMode mode) =>
            new Fixed(IntMath.MulDiv(a.Raw, Scale, b.Raw, mode));

        /// <summary>Arrondit à l'entier selon <paramref name="mode"/>.</summary>
        public long RoundToInt(RoundingMode mode = DefaultRounding) => IntMath.DivRound(Raw, Scale, mode);

        public Fixed Abs() => Raw < 0 ? new Fixed(checked(-Raw)) : this;

        public static Fixed Min(Fixed a, Fixed b) => a.Raw <= b.Raw ? a : b;

        public static Fixed Max(Fixed a, Fixed b) => a.Raw >= b.Raw ? a : b;

        public static Fixed Clamp(Fixed value, Fixed min, Fixed max)
        {
            if (min.Raw > max.Raw) throw new ArgumentException("min doit être inférieur ou égal à max.");
            return Max(min, Min(value, max));
        }

        public static Fixed operator +(Fixed a, Fixed b) => new Fixed(checked(a.Raw + b.Raw));
        public static Fixed operator -(Fixed a, Fixed b) => new Fixed(checked(a.Raw - b.Raw));
        public static Fixed operator -(Fixed a) => new Fixed(checked(-a.Raw));
        public static Fixed operator *(Fixed a, Fixed b) => Multiply(a, b, DefaultRounding);
        public static Fixed operator /(Fixed a, Fixed b) => Divide(a, b, DefaultRounding);
        public static Fixed operator *(Fixed a, long b) => new Fixed(checked(a.Raw * b));
        public static Fixed operator *(long a, Fixed b) => new Fixed(checked(a * b.Raw));

        public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;
        public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
        public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

        public bool Equals(Fixed other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Fixed other && Equals(other);
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

        /// <summary>
        /// Écriture canonique, indépendante de la culture : point décimal, zéros de fin supprimés
        /// (« 1.25 », « -0.5 », « 3 »).
        /// </summary>
        public override string ToString()
        {
            bool negative = Raw < 0;
            ulong magnitude = negative ? (ulong)(-(Raw + 1)) + 1UL : (ulong)Raw;
            ulong integer = magnitude / Scale;
            ulong fraction = magnitude % Scale;
            string sign = negative ? "-" : string.Empty;
            if (fraction == 0) return sign + integer.ToString(CultureInfo.InvariantCulture);
            string digits = fraction.ToString("D4", CultureInfo.InvariantCulture).TrimEnd('0');
            return sign + integer.ToString(CultureInfo.InvariantCulture) + "." + digits;
        }

        /// <summary>
        /// Lecture stricte d'un décimal invariant (« 12 », « -0.05 », « 1.2345 »).
        /// Refuse plus de 4 décimales, exposants, séparateurs de milliers et espaces :
        /// une valeur de configuration n'est jamais arrondie en silence.
        /// </summary>
        public static Fixed Parse(string text)
        {
            if (TryParse(text, out Fixed value)) return value;
            throw new FormatException($"Valeur à virgule fixe invalide : « {text} » (format attendu : -12.3456, 4 décimales au plus).");
        }

        public static bool TryParse(string? text, out Fixed value)
        {
            value = Zero;
            if (string.IsNullOrEmpty(text)) return false;

            int i = 0;
            bool negative = false;
            if (text![0] == '-' || text[0] == '+')
            {
                negative = text[0] == '-';
                i = 1;
            }

            ulong integer = 0;
            int integerDigits = 0;
            for (; i < text.Length && text[i] != '.'; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9') return false;
                if (integer > (ulong.MaxValue - 9) / 10) return false;
                integer = integer * 10 + (ulong)(c - '0');
                integerDigits++;
            }
            if (integerDigits == 0) return false;

            ulong fraction = 0;
            int fractionDigits = 0;
            if (i < text.Length)
            {
                i++; // point décimal
                if (i == text.Length) return false;
                for (; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c < '0' || c > '9') return false;
                    if (++fractionDigits > Decimals) return false;
                    fraction = fraction * 10 + (ulong)(c - '0');
                }
                for (int k = fractionDigits; k < Decimals; k++) fraction *= 10;
            }

            const ulong limit = (ulong)long.MaxValue + 1UL;
            if (integer > limit / (ulong)Scale) return false;
            ulong magnitude = integer * (ulong)Scale + fraction;
            if (magnitude > limit || (!negative && magnitude == limit)) return false;

            value = new Fixed(negative
                ? (magnitude == limit ? long.MinValue : -(long)magnitude)
                : (long)magnitude);
            return true;
        }
    }
}
