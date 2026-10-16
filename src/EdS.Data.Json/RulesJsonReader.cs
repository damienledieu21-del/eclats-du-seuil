using System.Globalization;
using System.Text;
using System.Text.Json;
using EdS.Data.Rules;
using EdS.Data.Validation;
using EdS.Domain.Numerics;

namespace EdS.Data.Json;

/// <summary>
/// Adaptateur System.Text.Json pour <c>rules.json</c>, réservé aux outils .NET (CLI, tests, futur serveur).
/// Il ne fait que traduire le JSON en <see cref="RulesDocument"/> et signaler les erreurs de forme
/// (syntaxe, types, champs inconnus ou en double, nombres mal écrits) ; tout le reste est délégué au
/// validateur commun <see cref="RulesValidator"/>. Aucune réflexion : le format est lu à partir de <see cref="RulesCatalog"/>.
/// </summary>
public static class RulesJsonReader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32,
    };

    public static RulesLoadResult ReadFile(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var report = new ValidationReport();
            report.Add("$", ValidationCode.FileUnreadable, $"impossible de lire « {path} » : {e.Message}");
            return RulesValidator.Validate(new RulesDocument(), report);
        }
        return Read(json);
    }

    public static RulesLoadResult Read(string json)
    {
        var report = new ValidationReport();
        var document = new RulesDocument();
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(bytes, DocumentOptions);
        }
        catch (JsonException e)
        {
            report.Add("$", ValidationCode.InvalidJson, "JSON invalide : " + FirstSentence(e.Message), e.LineNumber + 1);
            return RulesValidator.Validate(document, report);
        }

        using (parsed)
        {
            var reader = new Walker(report, document, LineIndex.Build(bytes));
            reader.ReadRoot(parsed.RootElement);
        }
        return RulesValidator.Validate(document, report);
    }

    private static string FirstSentence(string message)
    {
        int end = message.IndexOf(" Path:", StringComparison.Ordinal);
        return end > 0 ? message[..end] : message;
    }

    /// <summary>Arbre attendu sous <c>data</c>, construit depuis le catalogue.</summary>
    private sealed class Node
    {
        public Dictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);
        public RuleField? Field { get; set; }
        public string? ReferenceLabel { get; set; }

        public static Node BuildDataTree()
        {
            var root = new Node();
            foreach (RuleField field in RulesCatalog.Fields)
            {
                Node node = root;
                string prefix = string.Empty;
                foreach (string segment in field.Path.Split('.'))
                {
                    prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                    if (!node.Children.TryGetValue(segment, out Node? child))
                    {
                        child = new Node();
                        if (RulesCatalog.ReferenceMaps.TryGetValue(prefix, out string? label)) child.ReferenceLabel = label;
                        node.Children[segment] = child;
                    }
                    node = child;
                }
                node.Field = field;
            }
            return root;
        }
    }

    private static readonly Node DataTree = Node.BuildDataTree();

    private sealed class Walker(ValidationReport report, RulesDocument document, LineIndex lines)
    {
        public void ReadRoot(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                report.Add("$", ValidationCode.WrongType, $"objet JSON attendu à la racine, {Describe(root.ValueKind)} trouvé.", 1);
                return;
            }

            foreach (JsonProperty property in UniqueProperties(root, "$"))
            {
                string path = "$." + property.Name;
                JsonElement value = property.Value;
                switch (property.Name)
                {
                    case "schemaVersion":
                        if (TryReadInteger(value, path, out long version))
                        {
                            if (version is < int.MinValue or > int.MaxValue)
                                report.Add(path, ValidationCode.OutOfRange, "version hors plage.", lines.Of(path));
                            else
                                document.SchemaVersion = (int)version;
                        }
                        break;
                    case "kind":
                        if (TryReadString(value, path, out string? kind)) document.Kind = kind;
                        break;
                    case "contentVersion":
                        if (TryReadString(value, path, out string? contentVersion)) document.ContentVersion = contentVersion;
                        break;
                    case "data":
                        ReadObject(value, DataTree, "$.data", string.Empty);
                        break;
                    case "$schema":
                        // Indication pour les éditeurs (auto-complétion) ; ignorée par le programme.
                        TryReadString(value, path, out _);
                        break;
                    default:
                        report.Add(path, ValidationCode.UnknownField,
                            "champ inconnu ; champs possibles : $schema, schemaVersion, kind, contentVersion, data.", lines.Of(path));
                        break;
                }
            }
        }

        private void ReadObject(JsonElement element, Node node, string jsonPath, string dataPath)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                report.Add(jsonPath, ValidationCode.WrongType, $"objet attendu, {Describe(element.ValueKind)} trouvé.", lines.Of(jsonPath));
                return;
            }

            foreach (JsonProperty property in UniqueProperties(element, jsonPath))
            {
                string childJsonPath = jsonPath + "." + property.Name;
                string childDataPath = dataPath.Length == 0 ? property.Name : dataPath + "." + property.Name;
                if (!node.Children.TryGetValue(property.Name, out Node? child))
                {
                    string expected = string.Join(", ", node.Children.Keys);
                    if (node.ReferenceLabel != null)
                        report.Add(childJsonPath, ValidationCode.UnknownReference,
                            $"{node.ReferenceLabel} inconnu « {property.Name} » ; valeurs possibles : {expected}.", lines.Of(childJsonPath));
                    else
                        report.Add(childJsonPath, ValidationCode.UnknownField,
                            $"champ inconnu ; champs possibles : {expected}.", lines.Of(childJsonPath));
                    continue;
                }

                if (child.Field != null)
                    ReadLeaf(property.Value, child.Field, childJsonPath);
                else
                    ReadObject(property.Value, child, childJsonPath, childDataPath);
            }
        }

        private void ReadLeaf(JsonElement value, RuleField field, string path)
        {
            switch (field.Kind)
            {
                case RuleValueKind.Decimal:
                    if (TryReadDecimal(value, path, out Fixed decimalValue)) document.Set(field.Path, RuleValue.FromDecimal(decimalValue));
                    break;
                case RuleValueKind.Int32:
                case RuleValueKind.Int64:
                    if (TryReadInteger(value, path, out long integer)) document.Set(field.Path, RuleValue.FromInteger(integer));
                    break;
                case RuleValueKind.Token:
                    if (TryReadString(value, path, out string? token)) document.Set(field.Path, RuleValue.FromToken(token!));
                    break;
            }
        }

        private IEnumerable<JsonProperty> UniqueProperties(JsonElement element, string jsonPath)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (seen.Add(property.Name))
                {
                    yield return property;
                    continue;
                }
                string path = jsonPath + "." + property.Name;
                report.Add(path, ValidationCode.DuplicateField, "champ présent plusieurs fois dans le même objet ; seule la première occurrence est lue.",
                    lines.Of(path, occurrence: 1));
            }
        }

        private bool TryReadDecimal(JsonElement value, string path, out Fixed result)
        {
            result = Fixed.Zero;
            if (value.ValueKind != JsonValueKind.Number)
            {
                report.Add(path, ValidationCode.WrongType, $"nombre attendu, {Describe(value.ValueKind)} trouvé.", lines.Of(path));
                return false;
            }
            string raw = value.GetRawText();
            if (Fixed.TryParse(raw, out result)) return true;

            bool wellFormed = raw.IndexOfAny(['e', 'E']) < 0 && (raw.IndexOf('.') < 0 || raw.Length - raw.IndexOf('.') - 1 <= Fixed.Decimals);
            if (wellFormed)
                report.Add(path, ValidationCode.OutOfRange, $"{raw} est hors de la plage représentable.", lines.Of(path));
            else
                report.Add(path, ValidationCode.InvalidNumber, $"{raw} : 4 décimales au plus et pas d'exposant (ex. 1.5, 0.0001, 1000).", lines.Of(path));
            return false;
        }

        private bool TryReadInteger(JsonElement value, string path, out long result)
        {
            result = 0;
            if (value.ValueKind != JsonValueKind.Number)
            {
                report.Add(path, ValidationCode.WrongType, $"nombre entier attendu, {Describe(value.ValueKind)} trouvé.", lines.Of(path));
                return false;
            }
            string raw = value.GetRawText();
            if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
            {
                report.Add(path, ValidationCode.InvalidNumber, $"{raw} : nombre entier attendu (sans décimale ni exposant).", lines.Of(path));
                return false;
            }
            if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)) return true;
            report.Add(path, ValidationCode.OutOfRange, $"{raw} dépasse la plage d'un entier 64 bits.", lines.Of(path));
            return false;
        }

        private bool TryReadString(JsonElement value, string path, out string? result)
        {
            result = null;
            if (value.ValueKind != JsonValueKind.String)
            {
                report.Add(path, ValidationCode.WrongType, $"texte attendu, {Describe(value.ValueKind)} trouvé.", lines.Of(path));
                return false;
            }
            result = value.GetString();
            return true;
        }

        private static string Describe(JsonValueKind kind) => kind switch
        {
            JsonValueKind.Object => "un objet",
            JsonValueKind.Array => "un tableau",
            JsonValueKind.String => "un texte",
            JsonValueKind.Number => "un nombre",
            JsonValueKind.True or JsonValueKind.False => "un booléen",
            JsonValueKind.Null => "null",
            _ => "une valeur inattendue",
        };
    }

    /// <summary>Associe chaque chemin JSON à la ligne où son nom apparaît, pour des erreurs localisables.</summary>
    private sealed class LineIndex
    {
        private readonly Dictionary<string, List<long>> _lines;

        private LineIndex(Dictionary<string, List<long>> lines) => _lines = lines;

        public long? Of(string path, int occurrence = 0) =>
            _lines.TryGetValue(path, out List<long>? found) && occurrence < found.Count ? found[occurrence] : null;

        public static LineIndex Build(byte[] utf8)
        {
            var newlines = new List<long>();
            for (int i = 0; i < utf8.Length; i++)
                if (utf8[i] == (byte)'\n') newlines.Add(i);

            long LineOf(long offset)
            {
                int index = newlines.BinarySearch(offset);
                return (index < 0 ? ~index : index) + 1;
            }

            var result = new Dictionary<string, List<long>>(StringComparer.Ordinal);
            var stack = new List<string> { "$" };
            string? pendingName = null;
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        pendingName = reader.GetString();
                        string path = string.Join(".", stack) + "." + pendingName;
                        if (!result.TryGetValue(path, out List<long>? list)) result[path] = list = new List<long>();
                        list.Add(LineOf(reader.TokenStartIndex));
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (reader.CurrentDepth > 0) stack.Add(pendingName ?? "[]");
                        pendingName = null;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        if (reader.CurrentDepth > 0) stack.RemoveAt(stack.Count - 1);
                        pendingName = null;
                        break;
                    default:
                        pendingName = null;
                        break;
                }
            }
            return new LineIndex(result);
        }
    }
}
