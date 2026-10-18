using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EdS.Data.Json.Text
{
    // Sous-lot 9.1 : analyseur JSON strict sans dépendance (netstandard2.1, C# 9), pour que la lecture des données soit la même dans
    // les outils .NET, dans Unity (IL2CPP) et sur le futur serveur. Il remplace System.Text.Json et en garde les noms de types et la
    // sémantique utilisée par JsonInput : RFC 8259 sans extension (ni commentaire, ni virgule finale, ni NaN), profondeur bornée,
    // UTF-8 valide, texte brut des nombres conservé (les nombres sont lus exactement en virgule fixe), propriétés dans l'ordre du texte,
    // doublons conservés (JsonInput les signale), ligne de chaque nom de propriété.

    /// <summary>Nature d'une valeur JSON (mêmes noms et valeurs que <c>System.Text.Json.JsonValueKind</c>).</summary>
    internal enum JsonValueKind
    {
        Undefined = 0,
        Object = 1,
        Array = 2,
        String = 3,
        Number = 4,
        True = 5,
        False = 6,
        Null = 7,
    }

    /// <summary>Texte JSON invalide : message en français, ligne et colonne (en octets) comptées à partir de 0.</summary>
    internal sealed class JsonException : Exception
    {
        public long? LineNumber { get; }
        public long? BytePositionInLine { get; }

        public JsonException(string message, long lineNumber, long bytePositionInLine) : base(message)
        {
            LineNumber = lineNumber;
            BytePositionInLine = bytePositionInLine;
        }
    }

    /// <summary>Options d'analyse : seule la profondeur est réglable, la syntaxe est toujours stricte.</summary>
    internal sealed class JsonDocumentOptions
    {
        /// <summary>Nombre maximal de conteneurs (objets, tableaux) imbriqués, racine comprise.</summary>
        public int MaxDepth { get; set; } = 64;
    }

    /// <summary>Nœud de l'arbre analysé (immuable une fois construit).</summary>
    internal sealed class JsonNode
    {
        public JsonValueKind Kind;

        /// <summary>Texte d'une chaîne (déséchappé) ou texte brut d'un nombre.</summary>
        public string? Text;
        public List<JsonProperty>? Properties;
        public List<JsonElement>? Items;
    }

    /// <summary>Valeur JSON en lecture seule (sous-ensemble de l'API de <c>System.Text.Json.JsonElement</c> utilisé par les lecteurs).</summary>
    internal readonly struct JsonElement
    {
        private readonly JsonNode? _node;

        internal JsonElement(JsonNode node) => _node = node;

        public JsonValueKind ValueKind => _node?.Kind ?? JsonValueKind.Undefined;

        private JsonNode Node(JsonValueKind expected)
        {
            if (_node is null || _node.Kind != expected)
                throw new InvalidOperationException($"Valeur JSON {ValueKind} : {expected} attendu.");
            return _node;
        }

        /// <summary>Propriétés dans l'ordre du texte, doublons compris.</summary>
        public IReadOnlyList<JsonProperty> EnumerateObject() => Node(JsonValueKind.Object).Properties!;

        public IReadOnlyList<JsonElement> EnumerateArray() => Node(JsonValueKind.Array).Items!;

        public int GetArrayLength() => Node(JsonValueKind.Array).Items!.Count;

        /// <summary>Texte d'une chaîne ; null pour un null JSON (comme <c>System.Text.Json</c>).</summary>
        public string? GetString()
        {
            if (_node?.Kind == JsonValueKind.Null) return null;
            return Node(JsonValueKind.String).Text;
        }

        public bool GetBoolean()
        {
            if (_node?.Kind == JsonValueKind.True) return true;
            if (_node?.Kind == JsonValueKind.False) return false;
            throw new InvalidOperationException($"Valeur JSON {ValueKind} : booléen attendu.");
        }

        /// <summary>Texte brut d'un nombre, tel qu'écrit (aucune conversion par un flottant).</summary>
        public string GetRawText() => Node(JsonValueKind.Number).Text!;
    }

    /// <summary>Propriété d'un objet : nom déséchappé, valeur et ligne (à partir de 1) où commence son nom.</summary>
    internal readonly struct JsonProperty
    {
        public string Name { get; }
        public JsonElement Value { get; }
        public long Line { get; }

        internal JsonProperty(string name, JsonElement value, long line)
        {
            Name = name;
            Value = value;
            Line = line;
        }
    }

    /// <summary>Document analysé ; <see cref="Parse"/> lève <see cref="JsonException"/> sur un texte invalide.</summary>
    internal sealed class JsonDocument : IDisposable
    {
        public JsonElement RootElement { get; }

        private JsonDocument(JsonElement root) => RootElement = root;

        public void Dispose() { }

        public static JsonDocument Parse(byte[] utf8, JsonDocumentOptions options)
        {
            if (utf8 is null) throw new ArgumentNullException(nameof(utf8));
            if (options is null) throw new ArgumentNullException(nameof(options));
            return new JsonDocument(new Parser(utf8, options.MaxDepth).ParseDocument());
        }

        private sealed class Parser
        {
            private readonly byte[] _s;
            private readonly int _maxDepth;
            private int _pos;
            private long _line;
            private int _lineStart;
            private readonly StringBuilder _buffer = new StringBuilder();

            public Parser(byte[] utf8, int maxDepth)
            {
                _s = utf8;
                _maxDepth = maxDepth;
                // Pas de marque d'ordre UTF-8 : comme System.Text.Json, elle est refusée ici ; JsonInput.ReadFile la retire d'un fichier.
            }

            private JsonException Error(string message) => new JsonException(message, _line, _pos - _lineStart);

            private static string Show(byte b) => b >= 0x20 && b < 0x7F ? $"« {(char)b} »" : $"l'octet 0x{b:X2}";

            private JsonException Unexpected(string expected) =>
                _pos >= _s.Length
                    ? Error($"fin du texte inattendue : {expected} attendu.")
                    : Error($"{Show(_s[_pos])} inattendu : {expected} attendu.");

            public JsonElement ParseDocument()
            {
                SkipWhitespace();
                if (_pos >= _s.Length) throw Error("texte vide : une valeur JSON est attendue.");
                JsonElement root = ParseValue(0);
                SkipWhitespace();
                if (_pos < _s.Length) throw Error($"{Show(_s[_pos])} inattendu après la valeur JSON : un seul document est admis.");
                return root;
            }

            private void SkipWhitespace()
            {
                while (_pos < _s.Length)
                {
                    byte b = _s[_pos];
                    if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r') _pos++;
                    else if (b == (byte)'\n')
                    {
                        _pos++;
                        _line++;
                        _lineStart = _pos;
                    }
                    else return;
                }
            }

            private JsonElement ParseValue(int depth)
            {
                if (_pos >= _s.Length) throw Unexpected("une valeur");
                switch (_s[_pos])
                {
                    case (byte)'{': return ParseObject(depth + 1);
                    case (byte)'[': return ParseArray(depth + 1);
                    case (byte)'"': return new JsonElement(new JsonNode { Kind = JsonValueKind.String, Text = ParseString() });
                    case (byte)'t': return Literal("true", JsonValueKind.True);
                    case (byte)'f': return Literal("false", JsonValueKind.False);
                    case (byte)'n': return Literal("null", JsonValueKind.Null);
                    default:
                        byte c = _s[_pos];
                        if (c == (byte)'-' || (c >= (byte)'0' && c <= (byte)'9')) return ParseNumber();
                        throw Unexpected("une valeur (objet, tableau, texte, nombre, true, false ou null)");
                }
            }

            private void EnterContainer(int depth)
            {
                if (depth > _maxDepth) throw Error($"profondeur maximale de {_maxDepth} objets ou tableaux imbriqués dépassée.");
            }

            private JsonElement ParseObject(int depth)
            {
                EnterContainer(depth);
                _pos++;   // {
                var properties = new List<JsonProperty>();
                SkipWhitespace();
                if (_pos < _s.Length && _s[_pos] == (byte)'}')
                {
                    _pos++;
                    return new JsonElement(new JsonNode { Kind = JsonValueKind.Object, Properties = properties });
                }
                while (true)
                {
                    SkipWhitespace();
                    if (_pos >= _s.Length || _s[_pos] != (byte)'"') throw Unexpected("un nom de propriété entre guillemets");
                    long line = _line + 1;
                    string name = ParseString();
                    SkipWhitespace();
                    if (_pos >= _s.Length || _s[_pos] != (byte)':') throw Unexpected("« : »");
                    _pos++;
                    SkipWhitespace();
                    JsonElement value = ParseValue(depth);
                    properties.Add(new JsonProperty(name, value, line));
                    SkipWhitespace();
                    if (_pos >= _s.Length) throw Unexpected("« , » ou « } »");
                    if (_s[_pos] == (byte)',')
                    {
                        _pos++;
                        continue;
                    }
                    if (_s[_pos] == (byte)'}')
                    {
                        _pos++;
                        return new JsonElement(new JsonNode { Kind = JsonValueKind.Object, Properties = properties });
                    }
                    throw Unexpected("« , » ou « } »");
                }
            }

            private JsonElement ParseArray(int depth)
            {
                EnterContainer(depth);
                _pos++;   // [
                var items = new List<JsonElement>();
                SkipWhitespace();
                if (_pos < _s.Length && _s[_pos] == (byte)']')
                {
                    _pos++;
                    return new JsonElement(new JsonNode { Kind = JsonValueKind.Array, Items = items });
                }
                while (true)
                {
                    SkipWhitespace();
                    items.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (_pos >= _s.Length) throw Unexpected("« , » ou « ] »");
                    if (_s[_pos] == (byte)',')
                    {
                        _pos++;
                        continue;
                    }
                    if (_s[_pos] == (byte)']')
                    {
                        _pos++;
                        return new JsonElement(new JsonNode { Kind = JsonValueKind.Array, Items = items });
                    }
                    throw Unexpected("« , » ou « ] »");
                }
            }

            private JsonElement Literal(string word, JsonValueKind kind)
            {
                for (int i = 0; i < word.Length; i++)
                {
                    if (_pos + i >= _s.Length || _s[_pos + i] != (byte)word[i])
                    {
                        _pos += i;
                        throw Unexpected($"« {word} »");
                    }
                }
                _pos += word.Length;
                return new JsonElement(new JsonNode { Kind = kind });
            }

            /// <summary>Nombre RFC 8259 : -? (0 | [1-9][0-9]*) (. [0-9]+)? ([eE] [+-]? [0-9]+)? ; texte brut conservé.</summary>
            private JsonElement ParseNumber()
            {
                int start = _pos;
                if (_s[_pos] == (byte)'-') _pos++;
                if (_pos >= _s.Length || !IsDigit(_s[_pos])) throw Unexpected("un chiffre");
                if (_s[_pos] == (byte)'0')
                {
                    _pos++;
                    if (_pos < _s.Length && IsDigit(_s[_pos])) throw Error("zéro initial interdit dans un nombre.");
                }
                else Digits();
                if (_pos < _s.Length && _s[_pos] == (byte)'.')
                {
                    _pos++;
                    if (_pos >= _s.Length || !IsDigit(_s[_pos])) throw Unexpected("un chiffre après le point décimal");
                    Digits();
                }
                if (_pos < _s.Length && (_s[_pos] == (byte)'e' || _s[_pos] == (byte)'E'))
                {
                    _pos++;
                    if (_pos < _s.Length && (_s[_pos] == (byte)'+' || _s[_pos] == (byte)'-')) _pos++;
                    if (_pos >= _s.Length || !IsDigit(_s[_pos])) throw Unexpected("un chiffre dans l'exposant");
                    Digits();
                }
                string raw = Encoding.ASCII.GetString(_s, start, _pos - start);
                return new JsonElement(new JsonNode { Kind = JsonValueKind.Number, Text = raw });
            }

            private void Digits()
            {
                while (_pos < _s.Length && IsDigit(_s[_pos])) _pos++;
            }

            private static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';

            /// <summary>Chaîne entre guillemets : UTF-8 valide, aucun caractère de contrôle brut, échappements RFC 8259, paires de substitution complètes.</summary>
            private string ParseString()
            {
                _pos++;   // "
                _buffer.Clear();
                int runStart = _pos;
                while (true)
                {
                    if (_pos >= _s.Length) throw Error("texte non terminé : « \" » attendu.");
                    byte b = _s[_pos];
                    if (b == (byte)'"')
                    {
                        Flush(runStart);
                        _pos++;
                        return _buffer.ToString();
                    }
                    if (b == (byte)'\\')
                    {
                        Flush(runStart);
                        _pos++;
                        Escape();
                        runStart = _pos;
                        continue;
                    }
                    if (b < 0x20) throw Error($"caractère de contrôle 0x{b:X2} non échappé dans un texte.");
                    if (b < 0x80) _pos++;
                    else Utf8Sequence();
                }
            }

            private void Flush(int runStart)
            {
                if (_pos > runStart) _buffer.Append(Encoding.UTF8.GetString(_s, runStart, _pos - runStart));
            }

            private void Escape()
            {
                if (_pos >= _s.Length) throw Error("texte non terminé après « \\ ».");
                byte e = _s[_pos++];
                switch (e)
                {
                    case (byte)'"': _buffer.Append('"'); return;
                    case (byte)'\\': _buffer.Append('\\'); return;
                    case (byte)'/': _buffer.Append('/'); return;
                    case (byte)'b': _buffer.Append('\b'); return;
                    case (byte)'f': _buffer.Append('\f'); return;
                    case (byte)'n': _buffer.Append('\n'); return;
                    case (byte)'r': _buffer.Append('\r'); return;
                    case (byte)'t': _buffer.Append('\t'); return;
                    case (byte)'u':
                        int unit = Hex4();
                        if (unit >= 0xD800 && unit <= 0xDBFF)
                        {
                            if (_pos + 1 >= _s.Length || _s[_pos] != (byte)'\\' || _s[_pos + 1] != (byte)'u')
                                throw Error("demi-paire de substitution UTF-16 sans sa seconde moitié.");
                            _pos += 2;
                            int low = Hex4();
                            if (low < 0xDC00 || low > 0xDFFF) throw Error("demi-paire de substitution UTF-16 sans sa seconde moitié.");
                            _buffer.Append((char)unit).Append((char)low);
                            return;
                        }
                        if (unit >= 0xDC00 && unit <= 0xDFFF) throw Error("seconde moitié de paire de substitution UTF-16 isolée.");
                        _buffer.Append((char)unit);
                        return;
                    default:
                        _pos--;
                        throw Error($"échappement « \\{(e >= 0x20 && e < 0x7F ? ((char)e).ToString() : "?")} » invalide.");
                }
            }

            private int Hex4()
            {
                if (_pos + 4 > _s.Length) throw Error("échappement « \\u » incomplet : quatre chiffres hexadécimaux attendus.");
                string hex = Encoding.ASCII.GetString(_s, _pos, 4);
                for (int i = 0; i < 4; i++)
                    if (!Uri.IsHexDigit(hex[i])) throw Error("échappement « \\u » invalide : quatre chiffres hexadécimaux attendus.");
                _pos += 4;
                return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            /// <summary>Séquence UTF-8 de 2 à 4 octets, sans forme trop longue, sans substitution encodée, au plus U+10FFFF.</summary>
            private void Utf8Sequence()
            {
                byte b = _s[_pos];
                int length;
                int min;
                int code;
                if (b >= 0xC2 && b <= 0xDF) { length = 2; min = 0x80; code = b & 0x1F; }
                else if (b >= 0xE0 && b <= 0xEF) { length = 3; min = 0x800; code = b & 0x0F; }
                else if (b >= 0xF0 && b <= 0xF4) { length = 4; min = 0x10000; code = b & 0x07; }
                else throw Error($"UTF-8 invalide (octet 0x{b:X2}).");
                if (_pos + length > _s.Length) throw Error("UTF-8 invalide (séquence tronquée).");
                for (int i = 1; i < length; i++)
                {
                    byte c = _s[_pos + i];
                    if ((c & 0xC0) != 0x80) throw Error($"UTF-8 invalide (octet 0x{c:X2}).");
                    code = (code << 6) | (c & 0x3F);
                }
                if (code < min || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF)) throw Error("UTF-8 invalide (forme trop longue ou substitution).");
                _pos += length;
            }
        }
    }

    /// <summary>Lignes des noms de propriétés par chemin JSON (<c>$.a.b[2].c</c>), dans l'ordre du texte, doublons compris.</summary>
    internal sealed class JsonLineIndex
    {
        private readonly Dictionary<string, List<long>> _lines;

        private JsonLineIndex(Dictionary<string, List<long>> lines) => _lines = lines;

        public long? Of(string path, int occurrence = 0) =>
            _lines.TryGetValue(path, out List<long>? found) && occurrence < found.Count ? found[occurrence] : (long?)null;

        public static JsonLineIndex Build(JsonElement root)
        {
            var result = new Dictionary<string, List<long>>(StringComparer.Ordinal);
            Walk(root, "$", (path, line) =>
            {
                if (!result.TryGetValue(path, out List<long>? list)) result[path] = list = new List<long>();
                list.Add(line);
                return true;
            });
            return new JsonLineIndex(result);
        }

        /// <summary>Ligne de la première occurrence d'un chemin, en arrêtant le parcours dès qu'il est trouvé.</summary>
        public static long? FindFirst(JsonElement root, string path)
        {
            long? found = null;
            Walk(root, "$", (p, line) =>
            {
                if (p != path) return true;
                found = line;
                return false;
            });
            return found;
        }

        /// <summary>Parcours en profondeur dans l'ordre du texte ; <paramref name="onProperty"/> rend false pour arrêter.</summary>
        private static bool Walk(JsonElement element, string path, Func<string, long, bool> onProperty)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string child = path + "." + property.Name;
                    if (!onProperty(child, property.Line)) return false;
                    if (!Walk(property.Value, child, onProperty)) return false;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                IReadOnlyList<JsonElement> items = element.EnumerateArray();
                for (int i = 0; i < items.Count; i++)
                    if (!Walk(items[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", onProperty)) return false;
            }
            return true;
        }
    }
}
