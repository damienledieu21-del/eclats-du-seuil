using System;
using System.Collections.Generic;
using System.Linq;
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
        public IReadOnlyList<long>? List { get; }

        private RuleValue(RuleValueKind kind, Fixed decimalValue, long integer, string? token, IReadOnlyList<long>? list = null)
        {
            Kind = kind;
            Decimal = decimalValue;
            Integer = integer;
            Token = token;
            List = list;
        }

        public static RuleValue FromDecimal(Fixed value) => new RuleValue(RuleValueKind.Decimal, value, 0, null);
        public static RuleValue FromInteger(long value) => new RuleValue(RuleValueKind.Int64, Fixed.Zero, value, null);
        public static RuleValue FromToken(string value) =>
            new RuleValue(RuleValueKind.Token, Fixed.Zero, 0, value ?? throw new ArgumentNullException(nameof(value)));
        public static RuleValue FromIntegerList(IEnumerable<long> values) =>
            new RuleValue(RuleValueKind.Int32List, Fixed.Zero, 0, null, EdS.Domain.Collections.Frozen.List(values, nameof(values)));

        /// <summary>Écriture canonique, utilisée par l'empreinte : indépendante de la mise en forme du fichier.</summary>
        public string ToCanonicalString()
        {
            switch (Kind)
            {
                case RuleValueKind.Decimal: return Decimal.ToString();
                case RuleValueKind.Int32:
                case RuleValueKind.Int64: return Integer.ToString(CultureInfo.InvariantCulture);
                case RuleValueKind.Int32List: return "[" + string.Join(",", List!.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
                default: return Token!;
            }
        }
    }
}
