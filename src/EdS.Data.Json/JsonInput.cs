using System.Globalization;
using System.Text;
using System.Text.Json;
using EdS.Data.Validation;
using EdS.Domain.Numerics;

namespace EdS.Data.Json;

/// <summary>En-tête commun à tous les fichiers de données.</summary>
internal sealed class JsonHeader
{
    public int? SchemaVersion { get; set; }
    public string? Kind { get; set; }
    public string? ContentVersion { get; set; }
}

/// <summary>
/// Lecture JSON stricte partagée par tous les adaptateurs : syntaxe, en-tête, types, champs inconnus ou en double,
/// nombres. Toute erreur est ajoutée au rapport avec son chemin et, si possible, sa ligne.
/// </summary>
internal sealed class JsonInput
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 64,
    };

    public ValidationReport Report { get; }
    private readonly JsonLineIndex _lines;

    private JsonInput(ValidationReport report, JsonLineIndex lines)
    {
        Report = report;
        _lines = lines;
    }

    public long? LineOf(string path, int occurrence = 0) => _lines.Of(path, occurrence);

    /// <summary>Lit un fichier ; en cas d'échec, le rapport contient <see cref="ValidationCode.FileUnreadable"/>.</summary>
    public static string? ReadFile(string path, ValidationReport report)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            report.Add("$", ValidationCode.FileUnreadable, $"impossible de lire « {path} » : {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Analyse le texte puis lit l'en-tête commun ; <paramref name="readData"/> reçoit l'élément <c>data</c>.
    /// </summary>
    public static JsonHeader Parse(string json, ValidationReport report, Action<JsonInput, JsonElement> readData)
    {
        var header = new JsonHeader();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(bytes, DocumentOptions);
        }
        catch (JsonException e)
        {
            report.Add("$", ValidationCode.InvalidJson, "JSON invalide : " + FirstSentence(e.Message), e.LineNumber + 1);
            return header;
        }

        using (parsed)
        {
            var input = new JsonInput(report, JsonLineIndex.Build(bytes));
            input.ReadRoot(parsed.RootElement, header, readData);
        }
        return header;
    }

    private void ReadRoot(JsonElement root, JsonHeader header, Action<JsonInput, JsonElement> readData)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Report.Add("$", ValidationCode.WrongType, $"objet JSON attendu à la racine, {Describe(root.ValueKind)} trouvé.", 1);
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
                            Report.Add(path, ValidationCode.OutOfRange, "version hors plage.", LineOf(path));
                        else
                            header.SchemaVersion = (int)version;
                    }
                    break;
                case "kind":
                    if (TryReadString(value, path, out string? kind)) header.Kind = kind;
                    break;
                case "contentVersion":
                    if (TryReadString(value, path, out string? contentVersion)) header.ContentVersion = contentVersion;
                    break;
                case "data":
                    readData(this, value);
                    break;
                case "$schema":
                    // Indication pour les éditeurs (auto-complétion) ; ignorée par le programme.
                    TryReadString(value, path, out _);
                    break;
                default:
                    Report.Add(path, ValidationCode.UnknownField,
                        "champ inconnu ; champs possibles : $schema, schemaVersion, kind, contentVersion, data.", LineOf(path));
                    break;
            }
        }
    }

    private static string FirstSentence(string message)
    {
        int end = message.IndexOf(" Path:", StringComparison.Ordinal);
        return end > 0 ? message[..end] : message;
    }

    public bool RequireKind(JsonElement element, JsonValueKind expected, string path, string label)
    {
        if (element.ValueKind == expected) return true;
        Report.Add(path, ValidationCode.WrongType, $"{label} attendu, {Describe(element.ValueKind)} trouvé.", LineOf(path));
        return false;
    }

    /// <summary>Propriétés d'un objet, chaque nom une seule fois ; les doublons sont signalés.</summary>
    public IEnumerable<JsonProperty> UniqueProperties(JsonElement element, string jsonPath)
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
            Report.Add(path, ValidationCode.DuplicateField, "champ présent plusieurs fois dans le même objet ; seule la première occurrence est lue.",
                LineOf(path, occurrence: 1));
        }
    }

    public void UnknownField(string path, IEnumerable<string> expected) =>
        Report.Add(path, ValidationCode.UnknownField, $"champ inconnu ; champs possibles : {string.Join(", ", expected)}.", LineOf(path));

    public bool TryReadDecimal(JsonElement value, string path, out Fixed result)
    {
        result = Fixed.Zero;
        if (value.ValueKind != JsonValueKind.Number)
        {
            Report.Add(path, ValidationCode.WrongType, $"nombre attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
            return false;
        }
        string raw = value.GetRawText();
        if (Fixed.TryParse(raw, out result)) return true;

        bool wellFormed = raw.IndexOfAny(['e', 'E']) < 0 && (raw.IndexOf('.') < 0 || raw.Length - raw.IndexOf('.') - 1 <= Fixed.Decimals);
        if (wellFormed)
            Report.Add(path, ValidationCode.OutOfRange, $"{raw} est hors de la plage représentable.", LineOf(path));
        else
            Report.Add(path, ValidationCode.InvalidNumber, $"{raw} : 4 décimales au plus et pas d'exposant (ex. 1.5, 0.0001, 1000).", LineOf(path));
        return false;
    }

    public bool TryReadInteger(JsonElement value, string path, out long result)
    {
        result = 0;
        if (value.ValueKind != JsonValueKind.Number)
        {
            Report.Add(path, ValidationCode.WrongType, $"nombre entier attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
            return false;
        }
        string raw = value.GetRawText();
        if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
        {
            Report.Add(path, ValidationCode.InvalidNumber, $"{raw} : nombre entier attendu (sans décimale ni exposant).", LineOf(path));
            return false;
        }
        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)) return true;
        Report.Add(path, ValidationCode.OutOfRange, $"{raw} dépasse la plage d'un entier 64 bits.", LineOf(path));
        return false;
    }

    /// <summary>Entier 32 bits ; hors plage → <see cref="ValidationCode.OutOfRange"/>.</summary>
    public bool TryReadInt32(JsonElement value, string path, out int result)
    {
        result = 0;
        if (!TryReadInteger(value, path, out long n)) return false;
        if (n is < int.MinValue or > int.MaxValue)
        {
            Report.Add(path, ValidationCode.OutOfRange, $"{n} dépasse la plage d'un entier 32 bits.", LineOf(path));
            return false;
        }
        result = (int)n;
        return true;
    }

    public bool TryReadString(JsonElement value, string path, out string? result)
    {
        result = null;
        if (value.ValueKind != JsonValueKind.String)
        {
            Report.Add(path, ValidationCode.WrongType, $"texte attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
            return false;
        }
        result = value.GetString();
        return true;
    }

    public bool TryReadBoolean(JsonElement value, string path, out bool result)
    {
        result = false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result = value.GetBoolean();
            return true;
        }
        Report.Add(path, ValidationCode.WrongType, $"booléen (true ou false) attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
        return false;
    }

    public static string Describe(JsonValueKind kind) => kind switch
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
internal sealed class JsonLineIndex
{
    private readonly Dictionary<string, List<long>> _lines;

    private JsonLineIndex(Dictionary<string, List<long>> lines) => _lines = lines;

    public long? Of(string path, int occurrence = 0) =>
        _lines.TryGetValue(path, out List<long>? found) && occurrence < found.Count ? found[occurrence] : null;

    public static JsonLineIndex Build(byte[] utf8)
    {
        var newlines = new List<long>();
        for (int i = 0; i < utf8.Length; i++)
            if (utf8[i] == (byte)'\n') newlines.Add(i);

        long LineOf(long offset)
        {
            int index = newlines.BinarySearch(offset);
            return (index < 0 ? ~index : index) + 1;
        }

        void Record(Dictionary<string, List<long>> map, string path, long line)
        {
            if (!map.TryGetValue(path, out List<long>? list)) map[path] = list = new List<long>();
            list.Add(line);
        }

        // Pile des segments de chemin ; pour un tableau, on garde aussi l'indice de l'élément courant.
        var result = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        var stack = new List<string> { "$" };
        var arrayIndex = new Stack<int>();
        var inArray = new Stack<bool>();
        string? pendingName = null;
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });

        string ElementSegment()
        {
            int i = arrayIndex.Pop();
            arrayIndex.Push(i + 1);
            return $"[{i}]";
        }

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    pendingName = reader.GetString();
                    Record(result, string.Join(".", stack.Where(x => x != "\u0000")) + "." + pendingName, LineOf(reader.TokenStartIndex));
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    if (reader.CurrentDepth > 0)
                    {
                        if (pendingName != null) stack.Add(pendingName);
                        else if (inArray.Count > 0 && inArray.Peek())
                        {
                            string segment = ElementSegment();
                            stack[stack.Count - 1] += segment;
                            stack.Add("\u0000"); // marqueur : l'élément a modifié le segment parent
                        }
                        else stack.Add("[]");
                    }
                    inArray.Push(reader.TokenType == JsonTokenType.StartArray);
                    if (reader.TokenType == JsonTokenType.StartArray) arrayIndex.Push(0);
                    pendingName = null;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (reader.TokenType == JsonTokenType.EndArray) arrayIndex.Pop();
                    inArray.Pop();
                    if (reader.CurrentDepth > 0)
                    {
                        string top = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        if (top == "\u0000")
                        {
                            string parent = stack[stack.Count - 1];
                            stack[stack.Count - 1] = parent.Substring(0, parent.LastIndexOf('['));
                        }
                    }
                    pendingName = null;
                    break;
                default:
                    if (pendingName == null && inArray.Count > 0 && inArray.Peek()) ElementSegment();
                    pendingName = null;
                    break;
            }
        }
        return new JsonLineIndex(result);
    }
}
