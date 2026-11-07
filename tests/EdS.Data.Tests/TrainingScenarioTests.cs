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
    [InlineData("entrainement_garde_parade", "274202b6fb46837410de73e9f08757d52b628ada6d0858e4212bb5f92334c198")]
    [InlineData("entrainement_rupture", "b1a1a72f5bdccb4869e813367fcab2c9e346989e08b1e1ec908e94ff8a5d6d82")]
    [InlineData("entrainement_alterations", "26ebb98781926ac74755a63f2c0ed5b19f198a956cfea77752f07a107e16c013")]
    [InlineData("entrainement_declencheurs", "f50c64f09249a5be5f27d51f434cf40b43a7c194556c6b4d66ff6efbef0ce843")]
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
        // inchangé ; référence du scénario à déclencheurs dans Fixtures/reference-0.13.0. Sous-lot 8.4 : moteur 0.14.0, format 8
        // (guard.noUpkeep), règles en schéma 9 (contenu 0.7.0), classes en 8 (contenu 0.3.0, fiches complètes) : le gameplay change
        // (Heurt de bouclier étourdit, Provocation se lance) ; le moteur reproduit l'ancien sur le contenu historique (Lot81ReferenceTests) ;
        // référence du scénario à déclencheurs dans Fixtures/reference-0.14.0. Sous-lot 8.5 : moteur inchangé (0.14.0, format 8),
        // règles au contenu 0.8.0 (altérations du Gardien), Bastion au contenu 0.4.0 (talents, intermédiaires et passifs réels) : les
        // scénarios équipent des passifs réels et leurs talents agissent (Écho défensif, Égide réactive, Riposte précise, Garde stable) ;
        // le rupture ne garde que les talents de Gardien et de Contrecoup pour montrer encore une rupture ; le moteur reproduit l'ancien
        // gameplay avec le contenu figé du 8.4 (Correctif731TriggerScenarioTests) ; référence du scénario à déclencheurs dans
        // Fixtures/reference-0.14.0-contenu-0.4.0. Sous-lot 8.7 : Bastion au contenu 0.5.0 (Provocation inflige 0,8) : seuls l'empreinte
        // et la version de la classe changent dans ces quatre journaux (aucune Provocation lancée) ; référence du scénario à
        // déclencheurs dans Fixtures/reference-0.14.0-contenu-0.5.0, celle du 0.4.0 reproduite avec le contenu figé du 8.5.
        // Sous-lot 11.1 : moteur 0.15.0, format 9 (PV facultatifs des assaillants), scénarios en schéma 4 ; aucun assaillant livré n'a de
        // PV : seul l'en-tête change (format et version du moteur), corps identique à celui du moteur 0.14.0 ; référence du scénario à
        // déclencheurs dans Fixtures/reference-0.15.0-contenu-0.5.0. Sous-lot 11.4a : moteur 0.16.0, format 10, règles 0.9.0
        // (déplacement et contact), scénarios en schéma 5 ; aucun n'a d'espace : seul l'en-tête change (format, version, identité des
        // règles, dont l'étourdissement qui bloque désormais le déplacement), corps identique à celui du moteur 0.15.0 ; référence dans
        // Fixtures/reference-0.16.0-contenu-0.5.0. Sous-lot 11.4b : moteur 0.17.0, format 11, règles 0.10.0 (portée du lien protecteur),
        // Bastion 0.6.0 (portées) ; sans espace, une portée est ignorée : seul l'en-tête change (format, version, identité des règles et
        // de la classe), corps identique à celui du moteur 0.16.0 ; référence dans Fixtures/reference-0.17.0-contenu-0.6.0. Sous-lot 11.4c :
        // moteur 0.18.0, format 12, règles 0.11.0 (arc du bouclier, réaction des annonces), scénarios en schéma 6 ; sans espace, seul
        // l'en-tête change ; référence dans Fixtures/reference-0.18.0-contenu-0.6.0. Sous-lot 11.4d : moteur 0.19.0, format 13, scénarios en
        // schéma 7 ; sans arrivée ni départ, seul l'en-tête change ; référence dans Fixtures/reference-0.19.0-contenu-0.6.0. Sous-lot 11.5 :
        // moteur 0.20.0, format 14, schéma 8 ; sans comportement, seul l'en-tête change ; référence dans Fixtures/reference-0.20.0-contenu-0.6.0.
        // Sous-lot 11.9 : moteur 0.21.0, format 15, schéma 9 ; seul l'en-tête change ; référence dans Fixtures/reference-0.21.0-contenu-0.6.0.
        // Sous-lot 12.1 : moteur 0.22.0, format 16, schéma 10 ; seul l'en-tête change ; référence dans Fixtures/reference-0.22.0-contenu-0.6.0.
        // Sous-lot 12.3 : moteur 0.23.0, format 17, schéma 11 ; seul l'en-tête change ; référence dans Fixtures/reference-0.23.0-contenu-0.6.0.
        // Sous-lot 12.6 : moteur 0.24.0, format 18, schéma 12 ; seul l'en-tête change ; référence dans Fixtures/reference-0.24.0-contenu-0.6.0.
        // 29 octobre 2026 (décision de Damien) : règles 0.12.0, 128 combattants au plus ; seule l'identité des règles change dans l'en-tête ;
        // référence dans Fixtures/reference-0.24.0-contenu-0.6.0-regles-0.12.0.
        // Sous-lot V1.2 : moteur 0.25.0, format 19, règles 0.13.0 (schéma 13, bornes de la garde des boucliers), scénarios en schéma 13 ;
        // aucun scénario d'entraînement n'a de bouclier : seul l'en-tête change ; référence dans Fixtures/reference-0.25.0-contenu-0.6.0-regles-0.13.0.
        // Sous-lot V1.3 : moteur 0.26.0, format 20, règles 0.14.0 (schéma 14, bornes des effets d'équipement), scénarios en schéma 14 ; aucun de
        // ces scénarios n'a d'effet d'équipement : seul l'en-tête change ; référence dans Fixtures/reference-0.26.0-contenu-0.6.0-regles-0.14.0.
        // Relecture de la PR #78 : combat.equipment.allowedStatuses (vide) change l'empreinte des règles 0.14.0 (non fusionnées) : seule
        // l'empreinte de l'en-tête change ; la référence 0.26.0, elle aussi non fusionnée, est regénérée.
        // Sous-lot V1.8 : règles 0.15.0 (barre « 4 + ultime dès le début »), même moteur et même format : seule l'identité des règles change
        // dans l'en-tête ; référence dans Fixtures/reference-0.26.0-contenu-0.6.0-regles-0.15.0.
        // Sous-lot V1.8d : moteur 0.27.0, format 21 (bonus modifié), scénarios en schéma 15 ; aucun de ces scénarios n'a de bonus modifié :
        // seul l'en-tête change ; référence dans Fixtures/reference-0.27.0-contenu-0.6.0-regles-0.15.0 (avec le nouveau entrainement_bonus).
        // Sous-lot V2.4 : moteur 0.28.0, format 22, règles 0.16.0 (schéma 15), scénarios en schéma 16 ; aucun de ces scénarios n'a deux
        // équipes de créatures : seul l'en-tête change ; référence dans Fixtures/reference-0.28.0-contenu-0.6.0-regles-0.16.0.
        // Sous-lot V2.4.1 : moteur 0.29.0, format 23, règles 0.17.0 (schéma 16), scénarios en schéma 17 ; aucun de ces scénarios n'a de bête
        // qui erre : seul l'en-tête change ; référence dans Fixtures/reference-0.29.0-contenu-0.6.0-regles-0.17.0.
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Simulate(name).ToString()))).ToLowerInvariant();
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void Entrainement_garde_et_parade_resultats_attendus()
    {
        CombatResult result = CombatEngine.Run(Load("entrainement_garde_parade"), new MemoryCombatLog());
        CombatantResult bastion = result["bastion"];
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        // Sous-lot 8.4 : le Heurt de bouclier (tick 210) étourdit l'assaillant 1,5 s : son attaque du tick 220 est sautée (stunned), un
        // blocage de moins (4 → 3) et 40 dégâts de moins (100 × 0,4).
        Assert.Equal((3L, 8L, 1L, 0L), (bastion.Blocks, bastion.PerfectParries, bastion.FailedParries, bastion.GuardBreaks));
        Assert.Equal(5, bastion.RampartCharges);   // 8 parades, plafond 5
        // Correctif 7.2 : la parade parfaite de l'attaque porteuse de Fragilité l'empêche (stateBlocked, raison parried) ;
        // les impacts suivants ne subissent plus +10 % : 427 au lieu de 444 (387 depuis l'étourdissement du sous-lot 8.4). Sous-lot 8.5 :
        // 89, la garde plus forte (Garde stable, Posture de fer) et les boucliers d'Égide réactive absorbant le reste.
        Assert.Equal(89, bastion.DamageTaken);
        Assert.Contains(Simulate("entrainement_garde_parade").Lines,
            l => l.Contains("\"event\":\"attackSkipped\"") && l.Contains("\"reason\":\"stunned\""));
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
