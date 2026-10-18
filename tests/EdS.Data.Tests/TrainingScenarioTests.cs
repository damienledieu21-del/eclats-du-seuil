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

    [Fact]
    [Trait("Category", "Status")]
    public void Scenario_d_alterations_lu_et_joue()
    {
        CombatSetup setup = Load("entrainement_alterations");
        var assailant = Assert.IsType<AssailantSetup>(setup.Combatants[1]);
        Assert.Equal(Domain.Numerics.Fixed.FromInt(500), assailant.StatusResistance);
        Assert.Equal(new[] { "bleed", "burn", "stun", "freeze" }, assailant.Attacks.SelectMany(a => a.Applies).Select(a => a.Status));
        Assert.Equal(Domain.Numerics.Fixed.Parse("0.5"), assailant.Attacks[0].Applies[0].Coefficient);

        string log = Simulate("entrainement_alterations").ToString();
        foreach (string expected in new[]
        {
            "\"event\":\"dotDamage\"", "\"reason\":\"interrupted\",\"state\":\"stun\"", "\"reason\":\"stunned\"", "\"state\":\"stunImmunity\"",
            "\"reason\":\"immune\"", "\"movementSpeedModifier\":-0.3", "\"event\":\"parrySucceeded\"",
        })
            Assert.Contains(expected, log);
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    [InlineData("entrainement_alterations")]
    [InlineData("entrainement_declencheurs")]
    public void Rejeu_identique_des_scenarios_livres(string name)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate(name).Lines, ShippedData);
        Assert.Equal(ReplayStatus.Identical, outcome.Status);
    }

    [Theory]
    [InlineData("entrainement_garde_parade", "e272d43cfa852219d7aea9b00fc6c341df16578f0ab1828c0288f18f2b3f1e9d")]
    [InlineData("entrainement_rupture", "ec478f0bf414973b5b3ac39059f2ef58a3b95c53a957b37cfeff1fa9a3dd58fd")]
    [InlineData("entrainement_alterations", "b3b0ee7ff192a745b091449464dff6801fba95a8b123cb6ef98ee69f608470d9")]
    [InlineData("entrainement_declencheurs", "302a141ffbfdf0087e73b4628a425978a959e1c8505013e72fa539ce2e53434b")]
    public void Journaux_d_entrainement_stables(string name, string expected)
    {
        // Références avec data/v0 : à mettre à jour sciemment si les données, les scénarios ou le moteur changent.
        // Correctif 6.1 : seuls l'en-tête (moteur 0.6.1, format 2, versions et empreintes des données en schéma 2) et les
        // champs instance/generation des états changent ; tous les autres événements sont identiques (RAPPORT_CORRECTIF_6_1).
        // Lot 7.1 : seul l'en-tête change encore (moteur 0.7.0, règles en schéma 3 et leur empreinte) ; aucun autre octet.
        // Lot 7.2 : format 3 (catégorie et source sur les lignes d'état, applies) ; égalité structurelle avec 0.7.0 vérifiée
        // par Lot72MigrationTests. Nouveau scénario entrainement_alterations (DOT, contrôles, immunité).
        // Correctif 7.2 : moteur 0.8.1, règles 0.4.1 (étourdissement en ignore) ; garde_parade change à la Fragilité parée,
        // alterations à l'étourdissement refusé (active) et aux fins effectives des DOT.
        // Correctif 7.3.1 (D4) : entrainement_declencheurs, figé aussi dans Fixtures/reference-0.9.1 (Correctif731TriggerScenarioTests).
        // Sous-lot 8.1 : moteur 0.10.0, format 5 (statusResistance et rules sur combatant, rampartChargesSpent sur guardBroken) ;
        // journaux 0.9.1 figés dans Fixtures/logs-0.9.1 et reproduits au gameplay près (Lot81ReferenceTests) ; référence du scénario à
        // déclencheurs dans Fixtures/reference-0.10.0.
        // Lot 8 (décision du 18/10/2026) : moteur 0.11.0, règles en schéma 6 (plafonds de réduction), contenu 0.6.0 : seul l'en-tête
        // change ; référence du scénario à déclencheurs dans Fixtures/reference-0.11.0.
        // Sous-lot 8.2 : moteur 0.12.0, format 6 (absorbed et absorbedBy sur damage et dotDamage, damageAbsorbed au bilan,
        // shield.potencyDealt dans les règles effectives) ; données livrées sans bouclier ni renforcement : gameplay inchangé
        // (Lot81ReferenceTests) ; référence du scénario à déclencheurs dans Fixtures/reference-0.12.0. Règles en schéma 7 et classes en
        // schéma 6 (revue Codex de la PR #6) : empreintes de l'en-tête. Sous-lot 8.3 : moteur 0.13.0, format 7 (transferred, damageTransferred,
        // damageRedirected, clés protection.*), règles en schéma 8, classes en 7, scénarios en 3 ; sans protection ni provocation : gameplay
        // inchangé ; référence du scénario à déclencheurs dans Fixtures/reference-0.13.0.
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
        // Correctif 7.2 : la parade parfaite de l'attaque porteuse de Fragilité l'empêche (stateBlocked, raison parried) ;
        // les impacts suivants ne subissent plus +10 % : 427 au lieu de 444.
        Assert.Equal(427, bastion.DamageTaken);
        Assert.Contains(Simulate("entrainement_garde_parade").Lines,
            l => l.Contains("\"event\":\"stateBlocked\"") && l.Contains("\"state\":\"fragility\"") && l.Contains("\"reason\":\"parried\""));
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
