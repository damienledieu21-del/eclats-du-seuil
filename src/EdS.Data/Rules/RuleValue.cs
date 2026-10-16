using System;
using System.Globalization;
using EdS.Domain.Numerics;

namespace EdS.Data.Rules
{
    /// <summary>Valeur lue pour un paramètre, indépendante du format de fichier d'origine.</summary>
    public readonly struct RuleValue
    {
        public RuleValueKind Kind { get; }
        public Fixed Decimal { get; }
        public long Integer { get; }
        public string? Token { get; }

        private RuleValue(RuleValueKind kind, Fixed decimalValue, long integer, string? token)
        {
            Kind = kind;
            Decimal = decimalValue;
            Integer = integer;
            Token = token;
        }

        public static RuleValue FromDecimal(Fixed value) => new RuleValue(RuleValueKind.Decimal, value, 0, null);
        public static RuleValue FromInteger(long value) => new RuleValue(RuleValueKind.Int64, Fixed.Zero, value, null);
        public static RuleValue FromToken(string value) =>
            new RuleValue(RuleValueKind.Token, Fixed.Zero, 0, value ?? throw new ArgumentNullException(nameof(value)));

        /// <summary>Écriture canonique, utilisée par l'empreinte : indépendante de la mise en forme du fichier.</summary>
        public string ToCanonicalString()
        {
            switch (Kind)
            {
                case RuleValueKind.Decimal: return Decimal.ToString();
                case RuleValueKind.Int32:
                case RuleValueKind.Int64: return Integer.ToString(CultureInfo.InvariantCulture);
                default: return Token!;
            }
        }
    }
}
