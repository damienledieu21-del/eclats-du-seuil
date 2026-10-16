using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>Scénarios d'entraînement (lot 6) : assaillant, garde, parade ; lecture, erreurs, rejeu et journaux de référence.</summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
public class TrainingScenarioTests
{
    private static CombatSetup Load(string name)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(name), log);
        return log;
    }

    private static void AssertError(ScenarioLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    private static ScenarioLoadResult Mutate(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: "entrainement_garde_parade");

    [Fact]
    public void Assaillant_et_commandes_defensives_lus()
    {
        CombatSetup setup = Load("entrainement_garde_parade");
        var assailant = Assert.IsType<AssailantSetup>(setup.Combatants[1]);
        Assert.Equal(("bastion", 1000L, 2000L), (assailant.Target!, assailant.FirstAttackMs, assailant.IntervalMs));
        Assert.Equal(new[] { 100L, 100, 100, 100, 60 }, assailant.Attacks.Select(a => a.RawDamage));
        Assert.True(assailant.Attacks[4].Unblockable);
        Assert.Equal("fragility", Assert.Single(assailant.Attacks[2].Applies).Status);
        Assert.Equal(SkillTargeting.Enemy, assailant.Attacks[0].Targeting);
        Assert.Equal(CombatAction.GuardStart, setup.Commands[0].Action);
        Assert.Null(setup.Commands[0].Skill);
        Assert.Contains(setup.Commands, c => c.Action == CombatAction.Parry);
        Assert.Contains(setup.Commands, c => c.Action == CombatAction.GuardStop);
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    public void Rejeu_identique_des_scenarios_livres(string name)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate(name).Lines, ShippedData);
        Assert.Equal(ReplayStatus.Identical, outcome.Status);
    }

    [Theory]
    [InlineData("entrainement_garde_parade", "923c00a2d954bf887d50d986e8e20c316cd1fe31c02f497f07942e5286b4fbc9")]
    [InlineData("entrainement_rupture", "3cf03fc5dbdf715c2e7db2d65cba30ae39cb1d270d12555da3224a8066686aba")]
    public void Journaux_d_entrainement_stables(string name, string expected)
    {
        // Références avec data/v0 : à mettre à jour sciemment si les données, les scénarios ou le moteur changent.
        // Correctif 6.1 : seuls l'en-tête (moteur 0.6.1, format 2, versions et empreintes des données en schéma 2) et les
        // champs instance/generation des états changent ; tous les autres événements sont identiques (RAPPORT_CORRECTIF_6_1).
        // Lot 7.1 : seul l'en-tête change encore (moteur 0.7.0, règles en schéma 3 et leur empreinte) ; aucun autre octet.
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Simulate(name).ToString()))).ToLowerInvariant();
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void Entrainement_garde_et_parade_resultats_attendus()
    {
        CombatResult result = CombatEngine.Run(Load("entrainement_garde_parade"), new MemoryCombatLog());
        CombatantResult bastion = result["bastion"];
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.Equal((4L, 8L, 1L, 0L), (bastion.Blocks, bastion.PerfectParries, bastion.FailedParries, bastion.GuardBreaks));
        Assert.Equal(5, bastion.RampartCharges);   // 8 parades, plafond 5
        Assert.Equal(444, bastion.DamageTaken);
    }

    [Fact]
    public void Entrainement_rupture_resultats_attendus()
    {
        var log = Simulate("entrainement_rupture");
        CombatResult result = CombatEngine.Run(Load("entrainement_rupture"), new MemoryCombatLog());
        Assert.Equal(1, result["bastion"].GuardBreaks);
        Assert.Equal(2, result["bastion"].CommandsRejected);
        Assert.Contains(log.Lines, l => l.Contains("\"event\":\"guardBroken\"") && l.Contains("\"reason\":\"insufficientEndurance\""));
        Assert.Equal(2, log.Lines.Count(l => l.Contains("\"reason\":\"guardBroken\"")));
    }

    [Fact]
    public void Action_defensive_avec_competence_refusee() =>
        AssertError(Mutate(r => Command(r, 0)["skill"] = "skill.bastion.frappe_du_rempart"), "$.data.commands[0].skill", ValidationCode.InvalidValue);

    [Fact]
    public void Action_inconnue() =>
        AssertError(Mutate(r => Command(r, 0)["action"] = "esquive"), "$.data.commands[0].action", ValidationCode.UnknownReference);

    [Fact]
    public void Commande_sans_competence_ni_action() =>
        AssertError(Mutate(r => Command(r, 0).Remove("action")), "$.data.commands[0].skill", ValidationCode.MissingField);

    [Fact]
    public void Assaillant_sans_attaques() =>
        AssertError(Mutate(r => Combatant(r, 1).Remove("attacks")), "$.data.combatants[1].attacks", ValidationCode.MissingField);

    [Fact]
    public void Liste_d_attaques_vide() =>
        AssertError(Mutate(r => Combatant(r, 1)["attacks"] = new JsonArray()), "$.data.combatants[1].attacks", ValidationCode.InvalidValue);

    [Fact]
    public void Cible_de_l_assaillant_obligatoire_meme_nulle() =>
        AssertError(Mutate(r => Combatant(r, 1).Remove("target")), "$.data.combatants[1].target", ValidationCode.MissingField);

    [Fact]
    public void Cible_de_l_assaillant_inconnue_ou_alliee()
    {
        AssertError(Mutate(r => Combatant(r, 1)["target"] = "fantome"), "$.data.combatants[1].target", ValidationCode.UnknownReference);
        AssertError(Mutate(r => Combatant(r, 1)["target"] = "assaillant"), "$.data.combatants[1].target", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Attaque_a_cible_unique_sans_cible() =>
        AssertError(Mutate(r => Combatant(r, 1)["target"] = null), "$.data.combatants[1].target", ValidationCode.InvalidValue);

    [Fact]
    public void Attaque_mal_decrite()
    {
        AssertError(Mutate(r => Combatant(r, 1)["attacks"]![0]!["damageType"] = "feu"), "$.data.combatants[1].attacks[0].damageType", ValidationCode.UnknownReference);
        AssertError(Mutate(r => Combatant(r, 1)["attacks"]![0]!.AsObject().Remove("unblockable")), "$.data.combatants[1].attacks[0].unblockable", ValidationCode.MissingField);
        AssertError(Mutate(r => Combatant(r, 1)["attacks"]![0]!["rawDamage"] = -1), "$.data.combatants[1].attacks[0].rawDamage", ValidationCode.InvalidValue);
        AssertError(Mutate(r => Combatant(r, 1)["attacks"]![1]!["id"] = "coup"), "$.data.combatants[1].attacks[1].id", ValidationCode.DuplicateId);
    }

    [Fact]
    public void Rythme_de_l_assaillant_aligne_sur_le_tick()
    {
        AssertError(Mutate(r => Combatant(r, 1)["intervalMs"] = 0), "$.data.combatants[1].intervalMs", ValidationCode.InvalidValue);
        AssertError(Mutate(r => Combatant(r, 1)["intervalMs"] = 1999), "$.data.combatants[1].intervalMs", ValidationCode.InvalidValue);
        AssertError(Mutate(r => Combatant(r, 1)["firstAttackMs"] = 10), "$.data.combatants[1].firstAttackMs", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Champs_d_assaillant_refuses_ailleurs()
    {
        AssertError(ReadMutated(r => Combatant(r, 1)["intervalMs"] = 2000), "$.data.combatants[1].intervalMs", ValidationCode.InvalidValue);
        AssertError(Mutate(r => Combatant(r, 0)["attacks"] = new JsonArray()), "$.data.combatants[0].attacks", ValidationCode.InvalidValue);
        AssertError(Mutate(r => Combatant(r, 1)["class"] = "class.bastion"), "$.data.combatants[1].class", ValidationCode.InvalidValue);
    }
}
