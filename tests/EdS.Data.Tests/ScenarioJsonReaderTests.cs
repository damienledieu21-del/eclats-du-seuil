using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

[Trait("Category", "Engine")]
public class ScenarioJsonReaderTests
{
    private static void AssertError(ScenarioLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.True(result.Report.Errors.Any(e => e.Path == path && e.Code == code), $"{path} [{code}] attendu ; obtenu :\n{result.Report}");
    }

    [Fact]
    public void Donnees_livrees_chargees()
    {
        GameDataLoadResult loaded = GameDataLoader.Load(DataDirectory);
        Assert.True(loaded.IsValid, string.Join("\n", loaded.Failures.Select(f => $"{f.Path} : {f.Report}")));
        Assert.Equal(new[] { "class.bastion" }, loaded.Data!.Classes.Select(c => c.Definition.Id));
    }

    [Fact]
    public void Scenario_livre_valide()
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(MannequinPath, ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        CombatSetup setup = result.Setup!;
        Assert.Equal(42, setup.Seed);
        Assert.Equal(60_000, setup.DurationMs);
        Assert.IsType<CharacterSetup>(setup.Combatants[0]);
        var dummy = Assert.IsType<DummySetup>(setup.Combatants[1]);
        Assert.Equal(100, dummy.Armor.RoundToInt());
        Assert.Equal(new[] { "class.bastion" }, setup.Data.Classes.Select(c => c.Id));
        Assert.Equal(ShippedData.Rules.Fingerprint, setup.Data.Rules.Fingerprint);
    }

    [Fact]
    public void Competences_provisoires_du_bastion()
    {
        ShippedData.TryGetClass("class.bastion", out var bastion);
        bastion.Definition.TryGetSkill("skill.bastion.frappe_du_rempart", out var frappe);
        bastion.Definition.TryGetSkill("skill.bastion.heurt_de_bouclier", out var heurt);
        Assert.Equal(("1.2", 10, 6000L, true), (frappe.Combat!.Coefficient.ToString(), frappe.Combat.EnduranceCost, frappe.Combat.CooldownMs, frappe.Combat.IsProvisional));
        Assert.Equal(("0.8", 15, 10000L, true), (heurt.Combat!.Coefficient.ToString(), heurt.Combat.EnduranceCost, heurt.Combat.CooldownMs, heurt.Combat.IsProvisional));
        Assert.Equal(2, bastion.Definition.Skills.Count(s => s.Combat != null));
    }

    [Fact]
    public void Champ_obligatoire_absent() =>
        AssertError(ReadMutated(r => DataOf(r).Remove("seed")), "$.data.seed", ValidationCode.MissingField);

    [Fact]
    public void Attribut_manquant_inconnu_ou_negatif()
    {
        var result = ReadMutated(r =>
        {
            var attributes = Combatant(r, 0)["build"]!["attributes"]!.AsObject();
            attributes.Remove("force");
            attributes["chance"] = 3;
            attributes["volonte"] = -1;
        });
        AssertError(result, "$.data.combatants[0].build.attributes.force", ValidationCode.MissingField);
        AssertError(result, "$.data.combatants[0].build.attributes.chance", ValidationCode.UnknownReference);
        AssertError(result, "$.data.combatants[0].build.attributes.volonte", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Type_de_combattant_inconnu() =>
        AssertError(ReadMutated(r => Combatant(r, 1)["type"] = "boss"), "$.data.combatants[1].type", ValidationCode.UnknownReference);

    [Fact]
    public void Champ_reserve_a_un_autre_type() =>
        AssertError(ReadMutated(r => Combatant(r, 0)["armor"] = 5), "$.data.combatants[0].armor", ValidationCode.InvalidValue);

    [Fact]
    public void Classe_inconnue() =>
        AssertError(ReadMutated(r => Combatant(r, 0)["class"] = "class.mage"), "$.data.combatants[0].class", ValidationCode.UnknownReference);

    [Fact]
    public void Combattant_inconnu_dans_une_commande() =>
        AssertError(ReadMutated(r => Command(r, 0)["actor"] = "fantome"), "$.data.commands[0].actor", ValidationCode.UnknownReference);

    [Fact]
    public void Instant_non_multiple_du_tick() =>
        AssertError(ReadMutated(r => Command(r, 0)["atMs"] = 1010), "$.data.commands[0].atMs", ValidationCode.InvalidValue);

    [Fact]
    public void Duree_au_dela_de_la_limite_des_regles() =>
        AssertError(ReadMutated(r => DataOf(r)["durationMs"] = 600_050), "$.data.durationMs", ValidationCode.InvalidValue);

    [Fact]
    public void Build_invalide_rattache_au_combattant() =>
        AssertError(ReadMutated(r => Combatant(r, 0)["build"]!["level"] = 4), "$.data.combatants[0].build", ValidationCode.InvalidValue);

    [Fact]
    public void Identifiant_de_combattant_en_double() =>
        AssertError(ReadMutated(r => Combatant(r, 1)["id"] = "bastion"), "$.data.combatants[1].id", ValidationCode.DuplicateId);

    [Fact]
    public void Cible_et_ultime_facultatives()
    {
        var result = ReadMutated(r =>
        {
            Command(r, 0).Remove("target");
            Combatant(r, 0)["build"]!.AsObject().Remove("ultimate");
        });
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Null(result.Setup!.Commands[0].Target);
        Assert.Null(((CharacterSetup)result.Setup.Combatants[0]).Build.Ultimate);
    }

    [Fact]
    public void Mauvais_type_de_fichier() =>
        AssertError(ReadMutated(r => r["kind"] = "class"), "$.kind", ValidationCode.WrongKind);
}
