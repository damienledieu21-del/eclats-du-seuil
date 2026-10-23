using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EdS.Data.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using static EdS.Data.Tests.Lot81ModifierJson;

namespace EdS.Data.Tests;

/// <summary>
/// Évaluateur JSON Schema (draft 2020-12) limité aux mots-clés des schémas du dépôt, sans dépendance de paquet. Un mot-clé inconnu
/// lève une exception au lieu d'être ignoré : un schéma enrichi d'un mot-clé non évalué ne peut pas passer pour conforme.
/// </summary>
internal sealed class MiniJsonSchema
{
    private static readonly HashSet<string> Annotations = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$comment", "$defs", "title", "description", "default", "examples", "then", "else",
    };

    private readonly JsonElement _root;
    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    private MiniJsonSchema(JsonElement root) => _root = root;

    public static MiniJsonSchema Load(string path) => new(JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone());

    public bool IsValid(JsonElement instance) => Valid(_root, instance);

    private bool Valid(JsonElement schema, JsonElement x) => schema.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Object => schema.EnumerateObject().All(k => Keyword(schema, k.Name, k.Value, x)),
        _ => throw new NotSupportedException($"schéma inattendu : {schema.GetRawText()}"),
    };

    private bool Keyword(JsonElement schema, string name, JsonElement v, JsonElement x)
    {
        switch (name)
        {
            case "$ref": return Valid(Resolve(v.GetString()!), x);
            case "type": return HasType(x, v.GetString()!);
            case "const": return JsonEquals(v, x);
            case "enum": return v.EnumerateArray().Any(e => JsonEquals(e, x));
            case "properties":
                return x.ValueKind != JsonValueKind.Object
                    || v.EnumerateObject().All(p => !x.TryGetProperty(p.Name, out JsonElement value) || Valid(p.Value, value));
            case "required":
                return x.ValueKind != JsonValueKind.Object || v.EnumerateArray().All(r => x.TryGetProperty(r.GetString()!, out _));
            case "additionalProperties":
            {
                if (x.ValueKind != JsonValueKind.Object) return true;
                var declared = schema.TryGetProperty("properties", out JsonElement properties)
                    ? properties.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                return x.EnumerateObject().All(p => declared.Contains(p.Name) || Valid(v, p.Value));
            }
            case "items": return x.ValueKind != JsonValueKind.Array || x.EnumerateArray().All(e => Valid(v, e));
            case "propertyNames":   // revue Codex de la PR #43 (schéma des sauvegardes) : chaque nom de propriété valide le sous-schéma
                return x.ValueKind != JsonValueKind.Object
                    || x.EnumerateObject().All(p => Valid(v, JsonDocument.Parse(JsonSerializer.Serialize(p.Name)).RootElement));
            case "minItems": return x.ValueKind != JsonValueKind.Array || x.GetArrayLength() >= v.GetInt32();
            case "maxItems": return x.ValueKind != JsonValueKind.Array || x.GetArrayLength() <= v.GetInt32();
            case "uniqueItems":
            {
                if (x.ValueKind != JsonValueKind.Array || !v.GetBoolean()) return true;
                List<JsonElement> items = x.EnumerateArray().ToList();
                for (int i = 0; i < items.Count; i++)
                    for (int j = i + 1; j < items.Count; j++)
                        if (JsonEquals(items[i], items[j])) return false;
                return true;
            }
            case "contains": return x.ValueKind != JsonValueKind.Array || x.EnumerateArray().Any(e => Valid(v, e));
            case "minimum": return x.ValueKind != JsonValueKind.Number || Number(x) >= Number(v);
            case "maximum": return x.ValueKind != JsonValueKind.Number || Number(x) <= Number(v);
            case "exclusiveMinimum": return x.ValueKind != JsonValueKind.Number || Number(x) > Number(v);
            case "exclusiveMaximum": return x.ValueKind != JsonValueKind.Number || Number(x) < Number(v);
            case "minLength": return x.ValueKind != JsonValueKind.String || x.GetString()!.EnumerateRunes().Count() >= v.GetInt32();
            case "maxLength": return x.ValueKind != JsonValueKind.String || x.GetString()!.EnumerateRunes().Count() <= v.GetInt32();
            case "pattern": return x.ValueKind != JsonValueKind.String || Pattern(v.GetString()!).IsMatch(x.GetString()!);
            case "allOf": return v.EnumerateArray().All(s => Valid(s, x));
            case "anyOf": return v.EnumerateArray().Any(s => Valid(s, x));
            case "oneOf": return v.EnumerateArray().Count(s => Valid(s, x)) == 1;
            case "not": return !Valid(v, x);
            case "if":
                return Valid(v, x)
                    ? !schema.TryGetProperty("then", out JsonElement then) || Valid(then, x)
                    : !schema.TryGetProperty("else", out JsonElement otherwise) || Valid(otherwise, x);
            default:
                if (Annotations.Contains(name)) return true;
                throw new NotSupportedException($"mot-clé JSON Schema non pris en charge par MiniJsonSchema : « {name} ».");
        }
    }

    private JsonElement Resolve(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal)) throw new NotSupportedException($"référence non locale : {reference}");
        JsonElement node = _root;
        foreach (string token in reference[2..].Split('/'))
            node = node.GetProperty(token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
        return node;
    }

    private Regex Pattern(string pattern)
    {
        if (!_patterns.TryGetValue(pattern, out Regex? regex))
            _patterns[pattern] = regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return regex;
    }

    private static bool HasType(JsonElement x, string type) => type switch
    {
        "object" => x.ValueKind == JsonValueKind.Object,
        "array" => x.ValueKind == JsonValueKind.Array,
        "string" => x.ValueKind == JsonValueKind.String,
        "boolean" => x.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => x.ValueKind == JsonValueKind.Null,
        "number" => x.ValueKind == JsonValueKind.Number,
        "integer" => x.ValueKind == JsonValueKind.Number && decimal.Truncate(Number(x)) == Number(x),
        _ => throw new NotSupportedException($"type JSON Schema inconnu : {type}"),
    };

    /// <summary>Valeur mathématique d'un nombre JSON (1, 1.0 et 1e0 sont égaux, comme en JSON Schema).</summary>
    private static decimal Number(JsonElement x) =>
        decimal.Parse(x.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number) return Number(a) == Number(b);
        if (a.ValueKind != b.ValueKind) return false;
        switch (a.ValueKind)
        {
            case JsonValueKind.String: return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Array:
                return a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p => JsonEquals(p.First, p.Second));
            case JsonValueKind.Object:
            {
                var left = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var right = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                return left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out JsonElement other) && JsonEquals(p.Value, other));
            }
            default: return true;   // true, false, null : même genre
        }
    }
}

/// <summary>
/// Sous-lot 8.1, contre-vérification : le schéma JSON de classe (data/schemas/class.schema.json) n'est jamais plus strict que le
/// programme (DONNEES.md). Des fichiers de classe dérivés de bastion.json, un modificateur ou une liste à la fois, sont jugés par le
/// chargeur C# (forme et domaine) et par le schéma : tout fichier que le chargeur accepte, le schéma l'accepte. Le schéma peut être
/// plus large (règles du jeu, références, doublons) ; les règles qu'il reprend sont vérifiées à part, pour qu'un évaluateur
/// trop permissif ne fasse pas passer le test pour rien.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Data")]
public class Lot81SchemaConformanceTests
{
    private static readonly MiniJsonSchema Schema =
        MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "class.schema.json"));

    private sealed record Owner(string Name, Func<JsonObject, JsonObject> Of);

    /// <summary>Porteurs de bastion.json : talents de 1, 2, 3 et 5 rangs, talent majeur, passive, actives avec et sans bloc combat, ultime.</summary>
    private static readonly Owner[] Owners =
    {
        new("talent à 3 rangs", r => ClassTestData.Talent(r, 0, 0)),
        new("talent à 1 rang", r => ClassTestData.Talent(r, 0, 1)),
        new("talent à 2 rangs", r => ClassTestData.Talent(r, 1, 1)),
        new("talent à 5 rangs", r => ClassTestData.Talent(r, 0, 3)),
        new("talent majeur", r => ClassTestData.Talent(r, 0, 2)),
        new("passive", r => ClassTestData.Skill(r, 7)),
        new("active avec combat", r => ClassTestData.Skill(r, 0)),
        new("active sans combat", r => ClassTestData.Skill(r, 2)),
        new("ultime", r => ClassTestData.Skill(r, 6)),
    };

    private static Owner OwnerNamed(string name) => Owners.Single(o => o.Name == name);

    /// <summary>Verdicts du chargeur et du schéma pour bastion.json dont le porteur reçoit la liste <paramref name="modifiers"/> (texte JSON).</summary>
    private static (bool Loader, bool Schema) Judge(Owner owner, string modifiers)
    {
        JsonObject root = ClassTestData.Shipped();
        owner.Of(root)["modifiers"] = JsonNode.Parse(modifiers);
        string text = ClassTestData.ToJson(root);
        bool loader = ClassJsonReader.Read(text, ClassTestData.Progression).IsValid;
        using JsonDocument document = JsonDocument.Parse(text);
        return (loader, Schema.IsValid(document.RootElement));
    }

    private static string KeyOf(ModifierSpec spec) => spec.Name
        .Replace("status.<id>", "status.fragility", StringComparison.Ordinal)
        .Replace("skill.<id>", "skill.bastion.frappe_du_rempart", StringComparison.Ordinal);

    private static decimal ToDecimal(Fixed value) => value.Raw / (decimal)Fixed.Scale;

    /// <summary>Pas d'une clé : plus petite valeur déclarable (0,0001, 1, ou 50 ms, la durée du tick livrée).</summary>
    private static decimal Step(ModifierSpec spec) => spec.Unit switch
    {
        ModifierUnit.Decimal => 0.0001m,
        ModifierUnit.Milliseconds => 50m,
        _ => 1m,
    };

    /// <summary>
    /// Valeurs <c>add</c> d'une clé (textes JSON) : bornes déclarées et voisines, zéro sous plusieurs écritures, petites valeurs, objets
    /// { base, perRank } croisés, et des formes que le chargeur refuse (5 décimales, exposant, champ inconnu, type faux).
    /// </summary>
    private static IEnumerable<string> Values(ModifierSpec spec)
    {
        decimal max = ToDecimal(spec.MaxMagnitude), step = Step(spec);
        decimal[] numbers = { max, -max, max + step, -max - step, step, -step, 0m, 1m, -1m, 0.5m, decimal.Round(max / 2 / step) * step };
        foreach (decimal n in numbers) yield return Num(n);
        foreach (string raw in new[] { "1.0", "-0", "0.0", "0.00005", "1e0" }) yield return raw;
        foreach (decimal n in numbers) yield return $"{{\"base\":{Num(n)}}}";
        foreach (decimal b in new[] { step, -step, 0m, 1m, max })
            foreach (decimal w in new[] { 0m, step, -step, 1m, max })
                yield return $"{{\"base\":{Num(b)},\"perRank\":{Num(w)}}}";
        yield return $"{{\"perRank\":{Num(step)}}}";
        yield return "{\"base\":1,\"perRank\":0,\"x\":1}";
        yield return "\"1\"";
        yield return "null";
    }

    /// <summary>Tally d'un balayage : fichiers acceptés, refusés par les deux, refusés par le programme seul, et contre-exemples.</summary>
    private sealed class Tally
    {
        public int Accepted, BothRefused, SchemaLooser;
        public readonly List<string> Stricter = new();

        public void Add(Owner owner, string modifiers, (bool Loader, bool Schema) verdict)
        {
            if (verdict.Loader && verdict.Schema) Accepted++;
            else if (verdict.Loader) Stricter.Add($"{owner.Name} : {modifiers}");
            else if (verdict.Schema) SchemaLooser++;
            else BothRefused++;
        }

        public void AssertNeverStricter(int minAccepted, int minRefused)
        {
            Assert.True(Stricter.Count == 0,
                $"{Stricter.Count} fichier(s) acceptés par le programme et refusés par le schéma :\n{string.Join("\n", Stricter.Take(20))}");
            // Le balayage doit trancher dans les deux sens, sinon il ne prouverait rien.
            Assert.True(Accepted >= minAccepted, $"{Accepted} fichiers acceptés, au moins {minAccepted} attendus");
            Assert.True(BothRefused >= minRefused, $"{BothRefused} fichiers refusés par les deux, au moins {minRefused} attendus");
        }
    }

    [Fact]
    public void Evaluateur_accepte_les_classes_livrees()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "data", "v0", "classes");
        string[] files = Directory.GetFiles(directory, "*.json");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            Assert.True(Schema.IsValid(document.RootElement), Path.GetFileName(file));
        }
    }

    [Fact]
    public void Programme_accepte_donc_schema_accepte_chaque_cle_valeur_et_porteur()
    {
        // 20 clés × 9 porteurs × 56 valeurs.
        var tally = new Tally();
        foreach (ModifierSpec spec in ModifierKeys.Specs)
            foreach (Owner owner in Owners)
                foreach (string add in Values(spec))
                {
                    string modifiers = $"[{{\"key\":\"{KeyOf(spec)}\",\"add\":{add}}}]";
                    tally.Add(owner, modifiers, Judge(owner, modifiers));
                }
        tally.AssertNeverStricter(minAccepted: 1000, minRefused: 3000);
    }

    [Fact]
    public void Programme_accepte_donc_schema_accepte_chaque_ensemble_de_conditions()
    {
        string[] conditionLists =
        {
            "[]", "[\"whileGuarding\"]", "[\"perRampartCharge\"]", "[\"perResonanceStep\"]", "[\"whileGuarding\",\"perRampartCharge\"]",
            "[\"perRampartCharge\",\"whileGuarding\"]", "[\"whileGuarding\",\"perResonanceStep\"]", "[\"perResonanceStep\",\"whileGuarding\"]",
            "[\"perRampartCharge\",\"perResonanceStep\"]", "[\"whileGuarding\",\"whileGuarding\"]",
            "[\"whileGuarding\",\"perRampartCharge\",\"perResonanceStep\"]", "[\"inconnue\"]", "null", "\"whileGuarding\"",
        };
        var tally = new Tally();
        foreach (ModifierSpec spec in ModifierKeys.Specs)
            foreach (Owner owner in new[] { OwnerNamed("talent à 3 rangs"), OwnerNamed("talent à 1 rang"), OwnerNamed("talent majeur"), OwnerNamed("passive") })
                foreach (string add in new[] { Num(Step(spec)), Num(-Step(spec)), "1" })
                    foreach (string conditions in conditionLists)
                    {
                        string modifiers = $"[{{\"key\":\"{KeyOf(spec)}\",\"add\":{add},\"conditions\":{conditions}}}]";
                        tally.Add(owner, modifiers, Judge(owner, modifiers));
                    }
        tally.AssertNeverStricter(minAccepted: 100, minRefused: 1000);
    }

    [Fact]
    public void Programme_accepte_donc_schema_accepte_les_listes()
    {
        static string M(string key, string add, string? conditions = null) =>
            $"{{\"key\":\"{key}\",\"add\":{add}{(conditions is null ? "" : $",\"conditions\":{conditions}")}}}";
        string[] distinct =
        {
            M("guard.damageReduction", "0.01"), M("guard.costPerBlock", "-1"), M("parry.windowMs", "50"), M("rampartCharges.max", "1"),
            M("maxHealth", "0.1"), M("statusResistance", "5"), M("damageDealt", "0.1"), M("damageTaken", "-0.1"), M("endurance.max", "10"),
        };
        string[] lists =
        {
            "[]", "null", "{}",
            $"[{string.Join(",", distinct.Take(8))}]",                                           // 8 : plafond par porteur
            $"[{string.Join(",", distinct)}]",                                                   // 9
            $"[{distinct[0]},{distinct[0]}]",                                                    // doublon
            $"[{M("damageDealt", "0.1")},{M("damageDealt", "0.1", "[\"whileGuarding\"]")}]",     // même clé, autres conditions
            $"[{M("damageDealt", "0.1", "[\"whileGuarding\",\"perRampartCharge\"]")},{M("damageDealt", "0.1", "[\"perRampartCharge\",\"whileGuarding\"]")}]",
            $"[{M("guard.noUpkeepBreak", "1")},{M("guard.breakShortenPerCharge", "500")}]",
            "[{\"key\":\"damageDealt\"}]", "[{\"add\":0.1}]", "[null]", "[1]",
        };
        var tally = new Tally();
        foreach (Owner owner in Owners)
            foreach (string modifiers in lists)
                tally.Add(owner, modifiers, Judge(owner, modifiers));
        tally.AssertNeverStricter(minAccepted: 20, minRefused: 40);
    }

    [Theory]
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":0}")]                                     // nul
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":{\"base\":0}}")]
    [InlineData("talent à 3 rangs", "{\"key\":\"damageDealt\",\"add\":{\"base\":0,\"perRank\":0}}")]
    [InlineData("passive", "{\"key\":\"guard.noUpkeepBreak\",\"add\":2}")]                             // drapeau ≠ 1
    [InlineData("talent à 3 rangs", "{\"key\":\"guard.noUpkeepBreak\",\"add\":{\"base\":1,\"perRank\":1}}")]
    [InlineData("passive", "{\"key\":\"damageTaken\",\"add\":0.1}")]                                   // damageTaken positif
    [InlineData("talent à 3 rangs", "{\"key\":\"damageTaken\",\"add\":{\"base\":0.1,\"perRank\":-0.01}}")]
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":{\"base\":0.1,\"perRank\":0.1}}")]        // perRank sur une passive
    [InlineData("active avec combat", "{\"key\":\"damageDealt\",\"add\":0.1}")]                        // porteur non admis
    [InlineData("ultime", "{\"key\":\"damageDealt\",\"add\":0.1}")]
    [InlineData("passive", "{\"key\":\"guard.costPerBlock\",\"add\":-1,\"conditions\":[\"whileGuarding\"]}")]   // condition hors dégâts
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":0.1,\"conditions\":[\"perRampartCharge\",\"perResonanceStep\"]}")]
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":0.1,\"conditions\":[\"whileGuarding\",\"whileGuarding\"]}")]
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":0.1,\"conditions\":[\"inconnue\"]}")]
    [InlineData("passive", "{\"key\":\"damageDealt\",\"add\":0.1,\"x\":1}")]                           // champ inconnu
    [InlineData("passive", "{\"key\":\"damagedealt\",\"add\":0.1}")]                                   // clé hors motif
    [InlineData("passive", "{\"key\":\"damageDealt\"}")]                                               // add absent
    public void Regles_reprises_par_le_schema_refusees_par_les_deux(string owner, string modifier)
    {
        var (loader, schema) = Judge(OwnerNamed(owner), $"[{modifier}]");
        Assert.False(loader, "programme");
        Assert.False(schema, "schéma");
    }
}
