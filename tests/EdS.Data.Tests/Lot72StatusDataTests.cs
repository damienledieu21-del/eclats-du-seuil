using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.2 : altérations en données. Bloc <c>statuses</c> de <c>rules.json</c> (schéma 4), <c>combat.applies</c> des classes
/// (schéma 3), <c>applies</c> et <c>statusResistance</c> des scénarios (schéma 2), refus des anciennes versions.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Status")]
public class Lot72StatusDataTests
{
    private static JsonArray Statuses(JsonObject rules) => rules["data"]!["statuses"]!.AsArray();

    private static JsonObject Status(JsonObject rules, string id) =>
        Statuses(rules).Select(s => s!.AsObject()).Single(s => s["id"]!.GetValue<string>() == id);

    private static void AssertError(ValidationReport report, string path, ValidationCode code) =>
        Assert.Contains(report.Errors, e => e.Path == path && e.Code == code);

    [Fact]
    public void Regles_livrees_portent_les_sept_alterations()
    {
        LoadedRules rules = RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!;
        // Les sept du lot 7.2 en tête ; sous-lot 8.4 : celles des compétences du Bastion à la suite.
        Assert.Equal(new[] { "burn", "bleed", "stun", "stunImmunity", "freeze", "fragility", "guardBreak",
                "taunt", "steelBarrier", "bastionAbsolute", "bastionAbsoluteGuard", "bastionAbsoluteTaunt" },
            rules.Combat.Statuses.Definitions.Select(d => d.Id));
        Assert.Equal((1000L, "guardBreak"), (rules.Stats.StatusResistanceConstant, rules.Combat.GuardBreakStatus));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public void Anciennes_versions_des_regles_refusees_sans_migration(int version)
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["schemaVersion"] = version);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
    }

    [Fact]
    public void Ancienne_classe_refusee_sans_migration()
    {
        ClassLoadResult result = ClassTestData.ReadMutated(c => c["schemaVersion"] = 2);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(result.Report.Errors).Path, result.Report.Errors[0].Code));
    }

    [Fact]
    public void Ancien_scenario_refuse_sans_migration()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s => s["schemaVersion"] = 1);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(result.Report.Errors).Path, result.Report.Errors[0].Code));
    }

    [Fact]
    public void Anciens_champs_de_Fragilite_et_de_rupture_refuses()
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r =>
        {
            r["data"]!["combat"]!["states"] = new JsonObject { ["fragility"] = new JsonObject { ["durationMs"] = 6000 } };
            r["data"]!["combat"]!["guard"]!["breakDurationMs"] = 3000;
        });
        AssertError(result.Report, "$.data.combat.states", ValidationCode.UnknownField);
        AssertError(result.Report, "$.data.combat.guard.breakDurationMs", ValidationCode.UnknownField);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("category")]
    [InlineData("durationMs")]
    [InlineData("stacking")]
    [InlineData("resistance")]
    public void Champs_obligatoires_d_une_alteration(string field)
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => Status(r, "freeze").Remove(field));
        Assert.False(result.IsValid);
        AssertError(result.Report, $"$.data.statuses[4].{field}", ValidationCode.MissingField);
    }

    [Fact]
    public void Valeurs_inconnues_et_champs_inconnus_localises()
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r =>
        {
            Status(r, "burn")["category"] = "poison";
            Status(r, "stun")["blocks"] = new JsonArray("skills", "voler");
            Status(r, "stun")["interrupts"] = new JsonArray("guard", "guard");
            Status(r, "freeze")["vitesse"] = 1;
            Status(r, "bleed")["damageType"] = "feu";
        });
        AssertError(result.Report, "$.data.statuses[0].category", ValidationCode.UnknownReference);
        AssertError(result.Report, "$.data.statuses[2].blocks[1]", ValidationCode.UnknownReference);
        AssertError(result.Report, "$.data.statuses[2].interrupts[1]", ValidationCode.DuplicateId);
        AssertError(result.Report, "$.data.statuses[4].vitesse", ValidationCode.UnknownField);
        AssertError(result.Report, "$.data.statuses[1].damageType", ValidationCode.UnknownReference);
    }

    [Fact]
    public void Coherence_rattachee_au_chemin_du_fichier()
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r =>
        {
            Status(r, "burn")["durationMs"] = 6050;
            Status(r, "stun")["grantsOnExpiry"] = "inconnu";
            Status(r, "freeze")["id"] = "stun";
        });
        AssertError(result.Report, "$.data.statuses[0].durationMs", ValidationCode.InvalidValue);
        AssertError(result.Report, "$.data.statuses[0].periodMs", ValidationCode.InvalidValue);
        AssertError(result.Report, "$.data.statuses[2].grantsOnExpiry", ValidationCode.UnknownReference);
        AssertError(result.Report, "$.data.statuses[4].id", ValidationCode.DuplicateId);
    }

    [Theory]
    [InlineData("inconnu", ValidationCode.UnknownReference)]
    [InlineData("burn", ValidationCode.InvalidValue)]
    [InlineData("stunImmunity", ValidationCode.InvalidValue)]
    public void Alteration_de_rupture_de_garde_verifiee(string id, ValidationCode code)
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["guard"]!["breakStatus"] = id);
        AssertError(result.Report, "$.data.combat.guard.breakStatus", code);
    }

    [Theory]
    [InlineData(1_000_000_000L, true)]
    [InlineData(1_000_000_001L, false)]
    [InlineData(0L, false)]
    public void Constante_de_resistance_bornee_dans_le_fichier(long constant, bool valid)
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["data"]!["stats"]!["statusResistanceConstant"] = constant);
        Assert.Equal(valid, result.IsValid);
        if (!valid) AssertError(result.Report, "$.data.stats.statusResistanceConstant", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Empreinte_sensible_a_l_ordre_des_alterations_et_pas_aux_cles()
    {
        string shipped = RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Fingerprint;
        RulesLoadResult swapped = RulesTestData.ReadMutated(r =>
        {
            JsonArray list = Statuses(r);
            JsonNode first = list[0]!.DeepClone();
            list[0] = list[1]!.DeepClone();
            list[1] = first;
        });
        Assert.True(swapped.IsValid, swapped.Report.ToString());
        Assert.NotEqual(shipped, swapped.Rules!.Fingerprint);   // l'ordre fixe la priorité des motifs de refus

        RulesLoadResult reordered = RulesTestData.ReadMutated(r =>
        {
            JsonObject freeze = Status(r, "freeze");
            var copy = new JsonObject(freeze.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            Statuses(r)[4] = copy;
        });
        Assert.Equal(shipped, reordered.Rules!.Fingerprint);

        RulesLoadResult explicitDefaults = RulesTestData.ReadMutated(r => Status(r, "burn")["blocks"] = new JsonArray());
        Assert.Equal(shipped, explicitDefaults.Rules!.Fingerprint);   // valeur neutre écrite ou omise : même contenu
    }

    // --- Classes : combat.applies --------------------------------------------------------------

    [Fact]
    public void Classe_sans_applies_refusee()
    {
        ClassLoadResult result = ClassTestData.ReadMutated(c => ClassTestData.Skill(c, 0)["combat"]!.AsObject().Remove("applies"));
        AssertError(result.Report, "$.data.skills[0].combat.applies", ValidationCode.MissingField);
    }

    [Fact]
    public void Classe_applies_lu_et_verifie()
    {
        ClassLoadResult ok = ClassTestData.ReadMutated(c => ClassTestData.Skill(c, 0)["combat"]!["applies"] =
            new JsonArray(new JsonObject { ["status"] = "bleed", ["coefficient"] = 0.25 }, new JsonObject { ["status"] = "stun" }));
        Assert.True(ok.IsValid, ok.Report.ToString());
        var applies = ok.Class!.Definition.Skills[0].Combat!.Applies;
        Assert.Equal(new[] { ("bleed", (Fixed?)Fixed.Parse("0.25")), ("stun", null) }, applies.Select(a => (a.Status, a.Coefficient)));

        ClassLoadResult invalid = ClassTestData.ReadMutated(c => ClassTestData.Skill(c, 0)["combat"]!["applies"] =
            new JsonArray(new JsonObject { ["coefficient"] = 0.25 }, new JsonObject { ["status"] = "bleed", ["coefficient"] = 100.0001 },
                new JsonObject { ["status"] = "stun", ["duree"] = 1 }));
        AssertError(invalid.Report, "$.data.skills[0].combat.applies[0].status", ValidationCode.MissingField);
        AssertError(invalid.Report, "$.data.skills[0].combat.applies[2].duree", ValidationCode.UnknownField);
    }

    [Fact]
    public void Coefficient_d_application_hors_bornes_refuse_par_la_classe()
    {
        ClassLoadResult invalid = ClassTestData.ReadMutated(c => ClassTestData.Skill(c, 0)["combat"]!["applies"] =
            new JsonArray(new JsonObject { ["status"] = "bleed", ["coefficient"] = 100.0001 }));
        AssertError(invalid.Report, "$.data.skills[0].combat.applies[0].coefficient", ValidationCode.InvalidValue);
    }

    // --- Scénarios : applies et statusResistance --------------------------------------------------

    private static JsonObject Attack(JsonObject root) => ScenarioTestData.Combatant(root, 1)["attacks"]![0]!.AsObject();

    [Fact]
    public void Scenario_applies_et_resistance_lus()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s =>
        {
            Attack(s)["applies"] = new JsonArray(new JsonObject { ["status"] = "burn", ["coefficient"] = 0.5 }, new JsonObject { ["status"] = "fragility" });
            ScenarioTestData.Combatant(s, 1)["statusResistance"] = 40;
        }, scenario: "entrainement_garde_parade");
        Assert.True(result.IsValid, result.Report.ToString());
        var assailant = Assert.IsType<AssailantSetup>(result.Setup!.Combatants[1]);
        Assert.Equal(Fixed.FromInt(40), assailant.StatusResistance);
        Assert.Equal(new[] { "burn", "fragility" }, assailant.Attacks[0].Applies.Select(a => a.Status));

        ScenarioLoadResult dummy = ScenarioTestData.ReadMutated(s => ScenarioTestData.Combatant(s, 1)["statusResistance"] = 15);
        Assert.Equal(Fixed.FromInt(15), Assert.IsType<DummySetup>(dummy.Setup!.Combatants[1]).StatusResistance);
        Assert.Equal(Fixed.Zero, Assert.IsType<DummySetup>(ScenarioTestData.ReadMutated(_ => { }).Setup!.Combatants[1]).StatusResistance);
    }

    [Fact]
    public void Scenario_refuse_les_applications_invalides_avant_journal()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s => Attack(s)["applies"] = new JsonArray(
            new JsonObject { ["status"] = "poison" },
            new JsonObject { ["status"] = "burn" },
            new JsonObject { ["status"] = "stunImmunity" },
            new JsonObject { ["status"] = "guardBreak" },
            new JsonObject { ["status"] = "stun", ["coefficient"] = 1 }), scenario: "entrainement_garde_parade");
        const string p = "$.data.combatants[1].attacks[0].applies";
        AssertError(result.Report, p + "[0].status", ValidationCode.UnknownReference);
        AssertError(result.Report, p + "[1].status", ValidationCode.InvalidValue);
        AssertError(result.Report, p + "[2].status", ValidationCode.InvalidValue);
        AssertError(result.Report, p + "[3].status", ValidationCode.InvalidValue);
        AssertError(result.Report, p + "[4].status", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Resistance_aux_alterations_reservee_aux_mannequins_et_assaillants()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s => ScenarioTestData.Combatant(s, 0)["statusResistance"] = 10);
        AssertError(result.Report, "$.data.combatants[0].statusResistance", ValidationCode.InvalidValue);
    }

    [Theory]
    [InlineData("1000000000", true)]
    [InlineData("1000000000.0001", false)]
    [InlineData("-0.0001", false)]
    public void Resistance_aux_alterations_bornee(string value, bool valid)
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s => ScenarioTestData.Combatant(s, 1)["statusResistance"] = JsonNode.Parse(value));
        Assert.Equal(valid, result.IsValid);
        if (!valid) AssertError(result.Report, "$.data.combatants[1].statusResistance", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Attaque_sans_applies_refusee()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s => Attack(s).Remove("applies"), scenario: "entrainement_garde_parade");
        AssertError(result.Report, "$.data.combatants[1].attacks[0].applies", ValidationCode.MissingField);
    }
}
