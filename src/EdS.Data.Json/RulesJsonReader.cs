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
    public static RulesLoadResult ReadFile(string path)
    {
        var report = new ValidationReport();
        string? json = JsonInput.ReadFile(path, report);
        return json is null ? RulesValidator.Validate(new RulesDocument(), report) : Read(json, report);
    }

    public static RulesLoadResult Read(string json) => Read(json, new ValidationReport());

    private static RulesLoadResult Read(string json, ValidationReport report)
    {
        var document = new RulesDocument();
        JsonHeader header = JsonInput.Parse(json, report, (input, data) => ReadObject(input, document, data, DataTree, "$.data"));
        document.SchemaVersion = header.SchemaVersion;
        document.Kind = header.Kind;
        document.ContentVersion = header.ContentVersion;
        return RulesValidator.Validate(document, report);
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

    private static void ReadObject(JsonInput input, RulesDocument document, JsonElement element, Node node, string jsonPath)
    {
        if (!input.RequireKind(element, JsonValueKind.Object, jsonPath, "objet")) return;

        foreach (JsonProperty property in input.UniqueProperties(element, jsonPath))
        {
            string childPath = jsonPath + "." + property.Name;
            if (!node.Children.TryGetValue(property.Name, out Node? child))
            {
                if (node.ReferenceLabel != null)
                    input.Report.Add(childPath, ValidationCode.UnknownReference,
                        $"{node.ReferenceLabel} inconnu « {property.Name} » ; valeurs possibles : {string.Join(", ", node.Children.Keys)}.",
                        input.LineOf(childPath));
                else
                    input.UnknownField(childPath, node.Children.Keys);
                continue;
            }

            if (child.Field != null)
                ReadLeaf(input, document, property.Value, child.Field, childPath);
            else
                ReadObject(input, document, property.Value, child, childPath);
        }
    }

    private static void ReadLeaf(JsonInput input, RulesDocument document, JsonElement value, RuleField field, string path)
    {
        switch (field.Kind)
        {
            case RuleValueKind.Decimal:
                if (input.TryReadDecimal(value, path, out Fixed decimalValue)) document.Set(field.Path, RuleValue.FromDecimal(decimalValue));
                break;
            case RuleValueKind.Int32:
            case RuleValueKind.Int64:
                if (input.TryReadInteger(value, path, out long integer)) document.Set(field.Path, RuleValue.FromInteger(integer));
                break;
            case RuleValueKind.Token:
                if (input.TryReadString(value, path, out string? token)) document.Set(field.Path, RuleValue.FromToken(token!));
                break;
            case RuleValueKind.Int32List:
                if (!input.RequireKind(value, JsonValueKind.Array, path, "liste d'entiers")) break;
                var items = new List<long>();
                bool ok = true;
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (input.TryReadInteger(item, $"{path}[{index}]", out long n)) items.Add(n);
                    else ok = false;
                    index++;
                }
                if (ok) document.Set(field.Path, RuleValue.FromIntegerList(items));
                break;
        }
    }
}
