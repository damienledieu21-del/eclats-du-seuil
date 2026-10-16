using System.Reflection;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using FsCheck.Xunit;
using static EdS.Data.Tests.RulesTestData;

namespace EdS.Data.Tests;

[Trait("Category", "Data")]
public class RulesJsonReaderTests
{
    private static void AssertSingleError(RulesLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Rules);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    // --- Fichier livré --------------------------------------------------------------

    [Fact]
    public void Fichier_livre_valide()
    {
        RulesLoadResult result = RulesJsonReader.ReadFile(ShippedPath);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal(3, result.Rules!.SchemaVersion);
        Assert.Equal("0.3.0-provisoire", result.Rules.ContentVersion);
        Assert.Matches("^sha256:[0-9a-f]{64}$", result.Rules.Fingerprint);
    }

    [Fact]
    public void Fichier_livre_identique_aux_valeurs_provisoires_du_domaine()
    {
        LoadedRules rules = RulesJsonReader.ReadFile(ShippedPath).Rules!;
        AssertSameProperties(StatRules.Provisional, rules.Stats);
        AssertSameProperties(DamageRules.Provisional, rules.Damage);
        AssertSameProperties(ProgressionRules.Provisional, rules.Progression);
        AssertSameProperties(CombatRules.Provisional, rules.Combat);
        Assert.Equal(Fixed.FromInt(100), rules.Stats.BaseMaxHealth);   // E1
    }

    private static void AssertSameProperties<T>(T expected, T actual)
    {
        foreach (PropertyInfo p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? e = p.GetValue(expected), a = p.GetValue(actual);
            bool same = e is IEnumerable<int> el && a is IEnumerable<int> al ? el.SequenceEqual(al) : Equals(e, a);
            Assert.True(same, $"{typeof(T).Name}.{p.Name} : {e} attendu, {a} lu.");
        }
    }

    // --- Empreinte -------------------------------------------------------------------

    [Fact]
    public void Empreinte_independante_de_la_mise_en_forme()
    {
        string original = RulesJsonReader.Read(ShippedJson).Rules!.Fingerprint;

        // JSON compacté, clés réordonnées, « 2 » écrit « 2.0 » : mêmes valeurs, même empreinte.
        JsonObject root = Shipped();
        var data = root["data"]!.AsObject();
        root.Remove("data");
        root.Remove("kind");
        var reordered = new JsonObject { ["data"] = data, ["kind"] = "rules" };
        foreach (var (key, value) in root.ToList()) { root.Remove(key); reordered[key] = value; }
        Set(reordered, "stats.perAttribute.force.physicalPower", JsonNode.Parse("2.0"));
        string compact = reordered.ToJsonString();

        Assert.Equal(original, RulesJsonReader.Read(compact).Rules!.Fingerprint);
    }

    [Fact]
    public void Empreinte_ignore_la_version_de_contenu_mais_pas_les_valeurs()
    {
        string original = RulesJsonReader.Read(ShippedJson).Rules!.Fingerprint;
        string relabeled = ReadMutated(r => r["contentVersion"] = "0.2.0").Rules!.Fingerprint;
        string changed = ReadMutated(r => Set(r, "stats.base.maxHealth", 101)).Rules!.Fingerprint;
        Assert.Equal(original, relabeled);
        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void Empreinte_stable_entre_executions()
    {
        // Valeur de référence : changer le format canonique change toutes les empreintes déjà journalisées.
        // Correctif 6.1 : nouvelle valeur car la version de schéma (2) fait partie du texte canonique ; valeurs inchangées.
        Assert.Equal(
            "sha256:9473e0ca90af517d9bf03807d7e08e4d06cafd781ba2ba3b094d7aadc29db5a1",
            RulesJsonReader.Read(ShippedJson).Rules!.Fingerprint);
    }

    // --- En-tête -----------------------------------------------------------------------

    [Fact]
    public void Version_de_schema_future_non_prise_en_charge() =>
        AssertSingleError(ReadMutated(r => r["schemaVersion"] = 4), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);

    [Fact]
    public void Ancien_format_v1_refuse_explicitement_sans_migration()
    {
        // Correctif 6.1 : un fichier de règles d'avant le lot 6 (sans blocs garde, parade…) est refusé par sa version,
        // avec un seul message explicite, au lieu d'une cascade de champs manquants.
        RulesLoadResult result = ReadMutated(r =>
        {
            r["schemaVersion"] = 1;
            r["data"]!["combat"]!.AsObject().Remove("guard");
            r["data"]!["combat"]!.AsObject().Remove("parry");
        });
        AssertSingleError(result, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
        Assert.Contains("aucune migration", result.Report.Errors[0].Message);
    }

    [Fact]
    public void Ancien_format_v2_refuse_explicitement_sans_migration()
    {
        // Lot 7.1 : un fichier de règles du lot 6 (sans les cinq limites d'admission et de budget) est refusé par sa
        // version, avec un seul message, sans cascade de champs manquants.
        RulesLoadResult result = ReadMutated(r =>
        {
            r["schemaVersion"] = 2;
            var limits = r["data"]!["combat"]!["limits"]!.AsObject();
            foreach (string name in new[] { "maxCombatants", "maxCommands", "maxCommandsPerActorPerTick", "maxPendingEvents", "maxWorkPerTick" })
                limits.Remove(name);
        });
        AssertSingleError(result, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
        Assert.Contains("aucune migration", result.Report.Errors[0].Message);
    }

    [Fact]
    public void Mauvais_type_de_fichier() =>
        AssertSingleError(ReadMutated(r => r["kind"] = "classe"), "$.kind", ValidationCode.WrongKind);

    [Theory]
    [InlineData("v1")]
    [InlineData("0.1")]
    [InlineData("01.0.0")]
    public void Version_de_contenu_invalide(string version) =>
        AssertSingleError(ReadMutated(r => r["contentVersion"] = version), "$.contentVersion", ValidationCode.InvalidContentVersion);

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("kind")]
    [InlineData("contentVersion")]
    public void En_tete_obligatoire(string field) =>
        AssertSingleError(ReadMutated(r => r.Remove(field)), "$." + field, ValidationCode.MissingField);

    [Fact]
    public void Data_absent_signale_chaque_parametre_manquant()
    {
        var result = ReadMutated(r => r.Remove("data"));
        Assert.Equal(RulesCatalog.Fields.Count, result.Report.Errors.Count(e => e.Code == ValidationCode.MissingField));
    }

    // --- Champs ------------------------------------------------------------------------

    public static TheoryData<string> AllFields()
    {
        var data = new TheoryData<string>();
        foreach (RuleField f in RulesCatalog.Fields) data.Add(f.Path);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Chaque_parametre_est_obligatoire(string path)
    {
        var result = ReadMutated(r => Remove(r, path));
        Assert.Single(result.Report.Errors);
        AssertSingleError(result, "$.data." + path, ValidationCode.MissingField);
    }

    [Fact]
    public void Champ_inconnu_avec_ligne()
    {
        var result = ReadMutated(r => Set(r, "stats.base.maxHealt", 100));
        AssertSingleError(result, "$.data.stats.base.maxHealt", ValidationCode.UnknownField);
        Assert.NotNull(result.Report.Errors.Single(e => e.Code == ValidationCode.UnknownField).Line);
    }

    [Fact]
    public void Champ_en_double()
    {
        string json = ShippedJson.Replace("\"maxHealth\": 100,", "\"maxHealth\": 100,\n        \"maxHealth\": 200,");
        var result = RulesJsonReader.Read(json);
        AssertSingleError(result, "$.data.stats.base.maxHealth", ValidationCode.DuplicateField);
        Assert.Equal(15, result.Report.Errors.Single().Line);
    }

    [Fact]
    public void Type_de_degats_inconnu_est_une_reference_invalide() =>
        AssertSingleError(ReadMutated(r => Set(r, "damage.defenseConstants.feu", 1000)),
            "$.data.damage.defenseConstants.feu", ValidationCode.UnknownReference);

    [Fact]
    public void Attribut_inconnu_est_une_reference_invalide() =>
        AssertSingleError(ReadMutated(r => Set(r, "stats.perAttribute.charisme", new JsonObject { ["maxHealth"] = 1 })),
            "$.data.stats.perAttribute.charisme", ValidationCode.UnknownReference);

    [Fact]
    public void Mode_d_arrondi_inconnu() =>
        AssertSingleError(ReadMutated(r => Set(r, "damage.finalRounding", "auHasard")),
            "$.data.damage.finalRounding", ValidationCode.UnknownReference);

    [Theory]
    [InlineData("stats.base.maxHealth", "\"100\"")]
    [InlineData("stats.base.maxHealth", "true")]
    [InlineData("stats.base.maxHealth", "null")]
    [InlineData("stats.critical.maxChanceBasisPoints", "[6000]")]
    [InlineData("damage.finalRounding", "1")]
    [InlineData("stats.base", "0")]
    public void Mauvais_type(string path, string json) =>
        AssertSingleError(ReadMutated(r => Set(r, path, JsonNode.Parse(json))), "$.data." + path, ValidationCode.WrongType);

    [Theory]
    [InlineData("stats.base.maxHealth", "1e2")]
    [InlineData("stats.base.maxHealth", "100.00001")]
    [InlineData("stats.critical.maxChanceBasisPoints", "6000.5")]
    [InlineData("damage.minimumDamage", "1.0")]
    public void Nombre_mal_ecrit(string path, string raw) =>
        AssertSingleError(ReadMutated(r => Set(r, path, JsonNode.Parse(raw))), "$.data." + path, ValidationCode.InvalidNumber);

    [Theory]
    [InlineData("stats.critical.maxChanceBasisPoints", "3000000000")]
    [InlineData("damage.minimumDamage", "99999999999999999999")]
    [InlineData("stats.base.maxHealth", "99999999999999999999")]
    public void Nombre_hors_plage(string path, string raw) =>
        AssertSingleError(ReadMutated(r => Set(r, path, JsonNode.Parse(raw))), "$.data." + path, ValidationCode.OutOfRange);

    // --- Syntaxe -----------------------------------------------------------------------

    [Theory]
    [InlineData("{ \"schemaVersion\": 1, }")]           // virgule finale
    [InlineData("{ // commentaire\n }")]
    [InlineData("{ \"schemaVersion\": 1")]
    [InlineData("")]
    public void JSON_invalide(string json)
    {
        var result = RulesJsonReader.Read(json);
        AssertSingleError(result, "$", ValidationCode.InvalidJson);
        Assert.NotNull(result.Report.Errors[0].Line);
    }

    [Fact]
    public void Racine_qui_n_est_pas_un_objet() =>
        AssertSingleError(RulesJsonReader.Read("[1, 2]"), "$", ValidationCode.WrongType);

    [Fact]
    public void Fichier_introuvable() =>
        AssertSingleError(RulesJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "absent.json")), "$", ValidationCode.FileUnreadable);

    // --- Cohérence de gameplay (validation du domaine, rattachée au bon chemin) -----------

    [Fact]
    public void Parametre_incoherent_rattache_a_son_chemin()
    {
        var result = ReadMutated(r => Set(r, "stats.critical.maxChanceBasisPoints", 12_000));
        Assert.Single(result.Report.Errors);
        AssertSingleError(result, "$.data.stats.critical.maxChanceBasisPoints", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Bornes_inversees_signalees_sur_les_deux_champs()
    {
        var result = ReadMutated(r =>
        {
            Set(r, "damage.categoryMultiplier.min", 5);
            Set(r, "damage.categoryMultiplier.max", 2);
        });
        AssertSingleError(result, "$.data.damage.categoryMultiplier.min", ValidationCode.InvalidValue);
        AssertSingleError(result, "$.data.damage.categoryMultiplier.max", ValidationCode.InvalidValue);
    }

    [Theory]
    [InlineData("damage.defenseConstants.physique", "0")]
    [InlineData("damage.defenseConstants.magique", "-1000")]
    [InlineData("stats.base.maxHealth", "-1")]
    [InlineData("stats.critical.minMultiplier", "0.9")]
    [InlineData("damage.categoryMultiplier.min", "-0.5")]
    [InlineData("stats.critical.baseChanceBasisPoints", "7000")]
    public void Valeurs_refusees_par_le_gameplay(string path, string raw) =>
        AssertSingleError(ReadMutated(r => Set(r, path, JsonNode.Parse(raw))), "$.data." + path, ValidationCode.InvalidValue);

    [Fact]
    public void Toutes_les_erreurs_sont_rapportees_en_une_passe()
    {
        var result = ReadMutated(r =>
        {
            r["kind"] = "autre";
            Set(r, "stats.base.armor", "zéro");
            Set(r, "damage.finalRounding", "auHasard");
            Remove(r, "damage.minimumDamage");
            Set(r, "stats.base.inconnu", 1);
        });
        Assert.Equal(5, result.Report.Errors.Count);
    }

    // --- Propriétés ----------------------------------------------------------------------

    [Property(MaxTest = 300)]
    public bool Toute_valeur_decimale_valide_est_relue_a_l_identique(uint raw)
    {
        Fixed health = Fixed.FromRaw(raw);
        var result = ReadMutated(r => Set(r, "stats.base.maxHealth", JsonNode.Parse(health.ToString())));
        return result.IsValid && result.Rules!.Stats.BaseMaxHealth == health;
    }

    [Property(MaxTest = 300)]
    public bool Toute_constante_negative_ou_nulle_est_refusee(uint magnitude)
    {
        long value = -(long)magnitude;
        var result = ReadMutated(r => Set(r, "damage.defenseConstants.physique", value));
        return !result.IsValid && result.Report.Errors.All(e => e.Path == "$.data.damage.defenseConstants.physique");
    }
}
