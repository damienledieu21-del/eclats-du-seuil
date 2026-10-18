using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.2 : renforcements (<c>category: "buff"</c>, liste <c>modifiers</c>) et boucliers (<c>category: "shield"</c>) dans le bloc
/// <c>statuses</c> de <c>rules.json</c> (schéma 7) : lecture, erreurs localisées par leur chemin JSON, empreinte, conformité du schéma.
/// Les règles livrées n'en déclarent aucun : leur empreinte n'en dépend pas.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Shield")]
public class Lot82StatusDataTests
{
    private const string P = "$.data.statuses[7]";   // première altération ajoutée après les sept livrées

    private static JsonArray Statuses(JsonObject rules) => rules["data"]!["statuses"]!.AsArray();

    private static JsonObject Modifier(string key, JsonNode? add) => new() { ["key"] = key, ["add"] = add };

    private static JsonObject Buff(string id = "fortifie", params JsonObject[] modifiers) => new()
    {
        ["id"] = id, ["category"] = "buff", ["durationMs"] = 8000, ["stacking"] = "refresh", ["resistance"] = "none",
        ["modifiers"] = new JsonArray(modifiers.Select(m => (JsonNode?)m).ToArray()),
    };

    private static JsonObject Shield(string id = "egide") => new()
    {
        ["id"] = id, ["category"] = "shield", ["durationMs"] = 4000, ["stacking"] = "perSourceKeepStronger", ["resistance"] = "none",
    };

    private static RulesLoadResult With(params JsonObject[] statuses) =>
        RulesTestData.ReadMutated(r => { foreach (JsonObject s in statuses) Statuses(r).Add(s); });

    private static void AssertError(RulesLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Renforcement_et_bouclier_lus()
    {
        RulesLoadResult result = With(Buff("fortifie", Modifier("damageTaken", -0.3), Modifier("guard.upkeepPerSecond", -1)), Shield());
        Assert.True(result.IsValid, result.Report.ToString());
        StatusCatalog catalog = result.Rules!.Combat.Statuses;
        StatusDefinition buff = catalog["fortifie"], shield = catalog["egide"];
        Assert.Equal((StatusCategory.Buff, 8000L, StatusStacking.Refresh), (buff.Category, buff.DurationMs, buff.Stacking));
        Assert.Equal(new[] { ("damageTaken", Fixed.Parse("-0.3")), ("guard.upkeepPerSecond", Fixed.Parse("-1")) },
            buff.Modifiers.Select(m => (m.Key, m.Add.Base)));
        Assert.All(buff.Modifiers, m => Assert.Equal((Fixed.Zero, 0), (m.Add.PerRank, m.Conditions.Count)));
        Assert.Equal((StatusCategory.Shield, StatusStacking.PerSourceKeepStronger, 0), (shield.Category, shield.Stacking, shield.Modifiers.Count));
        Assert.True(catalog.HasShields);
        // Valeur simple ou objet { base } : même modificateur.
        RulesLoadResult explicitBase = With(Buff("fortifie", Modifier("damageTaken", new JsonObject { ["base"] = -0.3 }), Modifier("guard.upkeepPerSecond", -1)), Shield());
        Assert.Equal(result.Rules.Fingerprint, explicitBase.Rules!.Fingerprint);
    }

    [Fact]
    public void Regles_livrees_sans_renforcement_ni_bouclier()
    {
        StatusCatalog shipped = RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Combat.Statuses;
        Assert.DoesNotContain(shipped.Definitions, d => d.IsBuff || d.IsShield || d.Modifiers.Count > 0);
    }

    [Fact]
    public void Erreurs_de_domaine_rattachees_au_chemin_json()
    {
        AssertError(With(Buff("fortifie")), P + ".modifiers", ValidationCode.InvalidValue);
        AssertError(With(Buff("fortifie", Modifier("parry.windowMs", 100))), P + ".modifiers[0].key", ValidationCode.InvalidValue);
        AssertError(With(Buff("fortifie", Modifier("inconnue", 1))), P + ".modifiers[0].key", ValidationCode.UnknownReference);
        AssertError(With(Buff("fortifie", Modifier("damageTaken", 0.1))), P + ".modifiers[0].add", ValidationCode.InvalidValue);
        AssertError(With(Buff("fortifie", Modifier("damageTaken", new JsonObject { ["base"] = -0.1, ["perRank"] = -0.1 }))), P + ".modifiers[0].add",
            ValidationCode.InvalidValue);
        JsonObject conditional = Modifier("damageDealt", 0.5);
        conditional["conditions"] = new JsonArray("whileGuarding");
        AssertError(With(Buff("fortifie", conditional)), P + ".modifiers[0].conditions", ValidationCode.InvalidValue);
        AssertError(With(Buff("fortifie", Modifier("damageTaken", -0.1), Modifier("damageTaken", -0.2))), P + ".modifiers[1].key", ValidationCode.DuplicateId);

        JsonObject refreshShield = Shield();
        refreshShield["stacking"] = "refresh";
        AssertError(With(refreshShield), P + ".stacking", ValidationCode.InvalidValue);
        JsonObject control = new()
        {
            ["id"] = "marque", ["category"] = "modifier", ["durationMs"] = 4000, ["stacking"] = "refresh", ["resistance"] = "none",
            ["modifiers"] = new JsonArray(Modifier("damageTaken", -0.1)),
        };
        AssertError(With(control), P + ".modifiers", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Erreurs_de_forme_localisees()
    {
        JsonObject notArray = Buff("fortifie");
        notArray["modifiers"] = "damageTaken";
        AssertError(With(notArray), P + ".modifiers", ValidationCode.WrongType);
        JsonObject extra = Modifier("damageTaken", -0.1);
        extra["valeur"] = 1;
        AssertError(With(Buff("fortifie", extra)), P + ".modifiers[0].valeur", ValidationCode.UnknownField);
        JsonObject missing = new() { ["key"] = "damageTaken" };
        AssertError(With(Buff("fortifie", missing)), P + ".modifiers[0].add", ValidationCode.MissingField);
        JsonObject category = Shield();
        category["category"] = "bouclier";
        AssertError(With(category), P + ".category", ValidationCode.UnknownReference);
    }

    [Fact]
    public void Liste_vide_hors_renforcement_equivaut_a_liste_absente()
    {
        string absent = With(Shield()).Rules!.Fingerprint;
        JsonObject empty = Shield();
        empty["modifiers"] = new JsonArray();
        RulesLoadResult result = With(empty);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal(absent, result.Rules!.Fingerprint);
    }

    [Fact]
    public void Empreinte_sensible_aux_renforcements_et_aux_boucliers()
    {
        string[] fingerprints =
        {
            RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Fingerprint,
            With(Shield()).Rules!.Fingerprint,
            With(Shield("barriere")).Rules!.Fingerprint,
            With(Buff("fortifie", Modifier("damageTaken", -0.3))).Rules!.Fingerprint,
            With(Buff("fortifie", Modifier("damageTaken", -0.2))).Rules!.Fingerprint,                                           // valeur
            With(Buff("fortifie", Modifier("damageDealt", 0.3))).Rules!.Fingerprint,                                            // clé
            With(Buff("fortifie", Modifier("damageTaken", -0.3), Modifier("damageDealt", 0.3))).Rules!.Fingerprint,             // nombre
            With(Buff("fortifie", Modifier("damageDealt", 0.3), Modifier("damageTaken", -0.3))).Rules!.Fingerprint,             // ordre
        };
        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
    }

    [Fact]
    public void Forme_canonique_des_modificateurs_d_un_renforcement()
    {
        StatusCatalog catalog = With(Buff("fortifie", Modifier("damageTaken", -0.3))).Rules!.Combat.Statuses;
        var text = new StringBuilder();
        StatusShape.AppendCanonical(text, catalog);
        string canonical = text.ToString();
        Assert.EndsWith("statuses[7].id=fortifie\nstatuses[7].category=buff\nstatuses[7].durationMs=8000\nstatuses[7].periodMs=0\n" +
            "statuses[7].stacking=refresh\nstatuses[7].resistance=none\nstatuses[7].damageType?\nstatuses[7].blocks.count=0\n" +
            "statuses[7].interrupts.count=0\nstatuses[7].blockReason?\nstatuses[7].damageTakenIncrease=0\nstatuses[7].movementSpeedModifier=0\n" +
            "statuses[7].grantsOnExpiry?\nstatuses[7].immuneTo.count=0\n" +
            "statuses[7].modifiers.count=1\nstatuses[7].modifiers[0].key=damageTaken\nstatuses[7].modifiers[0].add=-0.3\n" +
            "statuses[7].modifiers[0].conditions.count=0\nstatuses[7].redirectRatio=0\n", canonical);   // 8.3 : redirectRatio
        Assert.DoesNotContain("statuses[6].modifiers", canonical);   // altérations sans modificateur : rien d'écrit
    }

    [Fact]
    public void Regles_au_schema_6_refusees_meme_sans_renforcement()
    {
        // Revue Codex de la PR #6 : renforcements et boucliers = schéma de règles 7. Un fichier 6 est refusé par sa version, un seul
        // message, sans migration (politique pré-alpha), même s'il n'utilise aucune nouveauté.
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["schemaVersion"] = 6);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
        Assert.Contains($"antérieure à la version {DataFormat.RulesSchemaVersion}", error.Message);
        Assert.True(RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.SchemaVersion >= 7);
    }

    [Fact]
    public void Classe_au_schema_5_refusee_meme_sans_puissance_de_boucliers()
    {
        // Clé shield.potencyDealt = schéma de classe 6 ; un fichier 5 est refusé par sa version.
        ClassLoadResult result = ClassTestData.ReadMutated(c => c["schemaVersion"] = 5);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
        Assert.Contains($"antérieure à la version {DataFormat.ClassSchemaVersion}", error.Message);
    }

    [Fact]
    public void Schema_JSON_des_alterations()
    {
        // Le bloc « statuses » du schéma décrit exactement les champs lus (StatusShape.FieldNames), les catégories du domaine et, pour
        // les modificateurs d'un renforcement, les clés admises et leur nombre maximal.
        string path = Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement item = schema.RootElement.GetProperty("properties").GetProperty("data").GetProperty("properties").GetProperty("statuses").GetProperty("items");
        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        JsonElement properties = item.GetProperty("properties");
        Assert.Equal(StatusShape.FieldNames.OrderBy(n => n, StringComparer.Ordinal),
            properties.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(StatusIds.Categories.Keys.OrderBy(n => n, StringComparer.Ordinal),
            properties.GetProperty("category").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).OrderBy(n => n, StringComparer.Ordinal));

        JsonElement modifiers = properties.GetProperty("modifiers");
        Assert.Equal(ModifierDefinition.MaxPerOwner, modifiers.GetProperty("maxItems").GetInt32());
        JsonElement modifier = modifiers.GetProperty("items");
        Assert.False(modifier.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "add", "key" }, modifier.GetProperty("properties").EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(StatusDefinition.BuffKeys.Select(f => ModifierKeys.Specs[(int)f].Name),
            modifier.GetProperty("properties").GetProperty("key").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!));
    }
}
