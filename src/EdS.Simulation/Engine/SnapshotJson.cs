using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4d : lecture des lignes d'un instantané d'état (<see cref="CombatSnapshot"/>), écrites par <see cref="JsonLineWriter"/>.
    /// Sous-ensemble strict de JSON, sans dépendance (Unity/IL2CPP) : objets, tableaux, chaînes (échappements du rédacteur et
    /// <c>\uXXXX</c>), entiers signés sur 64 bits, <c>true</c>, <c>false</c>, <c>null</c> ; ni flottant, ni espace, ni doublon de nom.
    /// Le moteur relit un instantané puis le réécrit : le texte doit revenir identique, ce qui refuse toute écriture non canonique.
    /// </summary>
    internal sealed class SnapshotNode
    {
        private readonly Dictionary<string, SnapshotNode>? _fields;
        private readonly List<SnapshotNode>? _items;
        private readonly string? _text;
        private readonly long _number;
        private readonly bool _bool;

        public SnapshotNodeKind Kind { get; }

        private SnapshotNode(SnapshotNodeKind kind, Dictionary<string, SnapshotNode>? fields = null, List<SnapshotNode>? items = null, string? text = null,
            long number = 0, bool flag = false)
        {
            Kind = kind;
            _fields = fields;
            _items = items;
            _text = text;
            _number = number;
            _bool = flag;
        }

        public static SnapshotNode Parse(string line)
        {
            int position = 0;
            SnapshotNode node = ReadValue(line, ref position, 0);
            if (position != line.Length) throw Error(line, position, "texte en trop après la valeur");
            return node;
        }

        // --- Lecture typée -------------------------------------------------------------------------------------------------

        public bool Has(string name) => Kind == SnapshotNodeKind.Object && _fields!.ContainsKey(name);

        public SnapshotNode this[string name]
        {
            get
            {
                if (Kind != SnapshotNodeKind.Object) throw new FormatException($"Objet attendu pour lire « {name} ».");
                return _fields!.TryGetValue(name, out SnapshotNode? value) ? value : throw new FormatException($"Champ « {name} » absent.");
            }
        }

        public IReadOnlyList<SnapshotNode> Items => Kind == SnapshotNodeKind.Array ? _items! : throw new FormatException("Tableau attendu.");

        public bool IsNull => Kind == SnapshotNodeKind.Null;

        public long Long => Kind == SnapshotNodeKind.Number ? _number : throw new FormatException("Entier attendu.");

        public int Int => checked((int)Long);

        public long? NullableLong => IsNull ? (long?)null : Long;

        public int? NullableInt => IsNull ? (int?)null : Int;

        public bool Bool => Kind == SnapshotNodeKind.True || (Kind == SnapshotNodeKind.False ? false : throw new FormatException("Booléen attendu."));

        public string String => Kind == SnapshotNodeKind.String ? _text! : throw new FormatException("Chaîne attendue.");

        public string? NullableString => IsNull ? null : String;

        // --- Analyse --------------------------------------------------------------------------------------------------------

        private const int MaxDepth = 32;

        private static FormatException Error(string text, int position, string message) =>
            new FormatException($"Instantané illisible (colonne {position}) : {message}.");

        private static SnapshotNode ReadValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw Error(s, i, "imbrication trop profonde");
            if (i >= s.Length) throw Error(s, i, "valeur attendue");
            char c = s[i];
            switch (c)
            {
                case '{':
                {
                    i++;
                    var fields = new Dictionary<string, SnapshotNode>(StringComparer.Ordinal);
                    if (i < s.Length && s[i] == '}')
                    {
                        i++;
                        return new SnapshotNode(SnapshotNodeKind.Object, fields: fields);
                    }
                    while (true)
                    {
                        if (i >= s.Length || s[i] != '"') throw Error(s, i, "nom de champ attendu");
                        string name = ReadString(s, ref i);
                        if (i >= s.Length || s[i] != ':') throw Error(s, i, "« : » attendu");
                        i++;
                        if (fields.ContainsKey(name)) throw Error(s, i, $"champ « {name} » en double");
                        fields[name] = ReadValue(s, ref i, depth + 1);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == '}') { i++; return new SnapshotNode(SnapshotNodeKind.Object, fields: fields); }
                        throw Error(s, i, "« , » ou « } » attendu");
                    }
                }
                case '[':
                {
                    i++;
                    var items = new List<SnapshotNode>();
                    if (i < s.Length && s[i] == ']')
                    {
                        i++;
                        return new SnapshotNode(SnapshotNodeKind.Array, items: items);
                    }
                    while (true)
                    {
                        items.Add(ReadValue(s, ref i, depth + 1));
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == ']') { i++; return new SnapshotNode(SnapshotNodeKind.Array, items: items); }
                        throw Error(s, i, "« , » ou « ] » attendu");
                    }
                }
                case '"':
                    return new SnapshotNode(SnapshotNodeKind.String, text: ReadString(s, ref i));
                case 't':
                    Expect(s, ref i, "true");
                    return new SnapshotNode(SnapshotNodeKind.True, flag: true);
                case 'f':
                    Expect(s, ref i, "false");
                    return new SnapshotNode(SnapshotNodeKind.False);
                case 'n':
                    Expect(s, ref i, "null");
                    return new SnapshotNode(SnapshotNodeKind.Null);
                default:
                {
                    int start = i;
                    if (s[i] == '-') i++;
                    int digits = i;
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                    if (i == digits || (s[digits] == '0' && i - digits > 1)) throw Error(s, start, "entier attendu");
                    if (!long.TryParse(s.Substring(start, i - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                        throw Error(s, start, "entier hors de 64 bits");
                    return new SnapshotNode(SnapshotNodeKind.Number, number: value);
                }
            }
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error(s, i, $"« {word} » attendu");
            i += word.Length;
        }

        private static string ReadString(string s, ref int i)
        {
            i++;   // guillemet ouvrant
            var text = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(s, i, "chaîne inachevée");
                char c = s[i++];
                if (c == '"') return text.ToString();
                if (c < 0x20) throw Error(s, i, "caractère de contrôle dans une chaîne");
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }
                if (i >= s.Length) throw Error(s, i, "échappement inachevé");
                char e = s[i++];
                switch (e)
                {
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                            throw Error(s, i, "\\u suivi de quatre chiffres hexadécimaux attendu");
                        text.Append((char)code);
                        i += 4;
                        break;
                    default:
                        throw Error(s, i, $"échappement « \\{e} » inconnu");
                }
            }
        }
    }

    internal enum SnapshotNodeKind
    {
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null,
    }
}
