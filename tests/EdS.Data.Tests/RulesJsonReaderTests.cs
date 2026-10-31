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
        Assert.Equal(14, result.Rules!.SchemaVersion);   // V1.3 : effets d'équipement ; sous-lot 11.4a : déplacement et contact ; 11.4b : portée des protections ; 11.4c : arc, annonces ; V1.2 : boucliers
        Assert.Equal("0.14.0-provisoire", result.Rules.ContentVersion);   // 0.14.0 : effets d'équipement (V1.3) ; 0.13.0 : garde des boucliers (V1.2) ; 0.12.0 : 128 combattants ; 0.9.0 : déplacement et contact (11.4a) ; 0.10.0 : portée du lien protecteur (11.4b) ; 0.11.0 : arc et annonces (11.4c)
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
            if (e is StatusCatalog ec && a is StatusCatalog ac)
            {
                // Lot 7.2 : catalogue des altérations comparé définition par définition, dans l'ordre.
                Assert.Equal(ec.Definitions.Count, ac.Definitions.Count);
                for (int i = 0; i < ec.Definitions.Count; i++) AssertSameProperties(ec.Definitions[i], ac.Definitions[i]);
                continue;
            }
            if (e is IReadOnlyList<ModifierDefinition> em && a is IReadOnlyList<ModifierDefinition> am)
            {
                // Sous-lot 8.4 : renforcements livrés, modificateurs comparés un par un (clé, valeur, conditions).
                Assert.Equal(em.Select(m => (m.Key, m.Add.Base, m.Add.PerRank, string.Join(",", m.Conditions))),
                    am.Select(m => (m.Key, m.Add.Base, m.Add.PerRank, string.Join(",", m.Conditions))));
                continue;
            }
            bool same = e is IEnumerable<int> el && a is IEnumerable<int> al ? el.SequenceEqual(al)
                : e is IEnumerable<string> es && a is IEnumerable<string> @as ? es.SequenceEqual(@as)
                : Equals(e, a);
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
        // Lot 7.2 : schéma 4, bloc statuses et constante de résistance aux altérations dans le texte canonique.
        // Correctif 7.2 : empilement de l'étourdissement passé à ignore.
        // Lot 7.3 : schéma 5 et combat.limits.maxTriggerDepth ; une altération sans déclencheur n'ajoute rien au texte canonique.
        // Correctif 7.3.1 (A73-M02) : contrat canonique 2 (version en tête, champs absents marqués, listes indicées).
        // Lot 8 (décision du 18/10/2026) : schéma 6, contenu 0.6.0, plafonds combat.guard.maxDamageReduction et combat.damageTaken.maxReduction.
        // Sous-lot 8.2 : schéma 7 (renforcements et boucliers) ; seule la version change dans le texte canonique.
        // Sous-lot 8.3 : schéma 8 ; redirectRatio (0) écrit pour chaque altération.
        // Sous-lot 8.4 : schéma 9, contenu 0.7.0 : combat.triggers.areaStrikeMinCooldownMs et les cinq altérations du Bastion.
        // Sous-lot 8.5 : contenu 0.8.0, schéma inchangé : les trois altérations des talents du Gardien.
        // Sous-lot 11.4a : schéma 10, contenu 0.9.0 : combat.movement, combat.contact, et l'étourdissement bloque le déplacement.
        // Sous-lot 11.4b : schéma 11, contenu 0.10.0 : range écrit pour chaque altération (absent, sauf le lien protecteur : 15 m).
        // Sous-lot 11.4c : schéma 12, contenu 0.11.0 : combat.guard.arcDegrees et combat.telegraph.minReactionMs.
        // Décision de Damien du 29 octobre 2026 : contenu 0.12.0, schéma inchangé : combat.limits.maxCombatants de 64 à 128.
        // Sous-lot V1.2 : schéma 13, contenu 0.13.0 : combat.shield (bornes de la garde des boucliers ; ancienne empreinte 81d40127…).
        // Sous-lot V1.3 : schéma 14, contenu 0.14.0 : combat.equipment (bornes des effets d'équipement ; ancienne empreinte d6dd1953…).
        Assert.Equal(
            "sha256:358fc35af7834e17f587c23a83d6bd6703b00c7c0ad6eca538d8d3b07c8b74f7",
            RulesJsonReader.Read(ShippedJson).Rules!.Fingerprint);
    }

    // --- En-tête -----------------------------------------------------------------------

    [Fact]
    public void Version_de_schema_future_non_prise_en_charge() =>
        AssertSingleError(ReadMutated(r => r["schemaVersion"] = 15), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);

    [Fact]
    public void Format_5_sans_plafonds_refuse_explicitement()
    {
        // Décision du 18/10/2026 : schéma 6, plafonds de réduction dans les règles. Un fichier au format 5 (sans ces deux champs) est
        // refusé par sa version, avec un seul message, sans migration (politique pré-alpha).
        RulesLoadResult result = ReadMutated(r =>
        {
            r["schemaVersion"] = 5;
            r["data"]!["combat"]!["guard"]!.AsObject().Remove("maxDamageReduction");
            r["data"]!["combat"]!.AsObject().Remove("damageTaken");
        });
        AssertSingleError(result, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
    }

    [Fact]
    public void Plafonds_de_reduction_lus_et_verifies()
    {
        // Les deux plafonds sont lus dans le fichier ; une réduction de garde au-dessus de son plafond est refusée sur les deux champs.
        LoadedRules rules = ReadMutated(r =>
        {
            r["data"]!["combat"]!["guard"]!["maxDamageReduction"] = 0.8;
            r["data"]!["combat"]!["guard"]!["damageReduction"] = 0.8;
            r["data"]!["combat"]!["damageTaken"]!["maxReduction"] = 0.9;
        }).Rules!;
        Assert.Equal(Fixed.Parse("0.8"), rules.Combat.MaxGuardDamageReduction);
        Assert.Equal(Fixed.Parse("0.9"), rules.Combat.MaxTotalDamageReduction);
        RulesLoadResult above = ReadMutated(r => r["data"]!["combat"]!["guard"]!["maxDamageReduction"] = 0.5);
        Assert.False(above.IsValid);
        Assert.Contains(above.Report.Errors, e => e.Path == "$.data.combat.guard.damageReduction");
        RulesLoadResult outside = ReadMutated(r => r["data"]!["combat"]!["damageTaken"]!["maxReduction"] = 1.5);
        Assert.Contains(outside.Report.Errors, e => e.Path == "$.data.combat.damageTaken.maxReduction");
    }

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
        // Chaque paramètre du catalogue, plus le bloc « statuses » (lot 7.2).
        Assert.Equal(RulesCatalog.Fields.Count + 1, result.Report.Errors.Count(e => e.Code == ValidationCode.MissingField));
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.statuses" && e.Code == ValidationCode.MissingField);
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
