using System.Text.Json.Nodes;
using EdS.Data.Builds;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Tools.Measures;
using EdS.Data.Scenarios;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.7 : passe d'équilibrage mesurée. Décision de Damien du 18/10/2026 : la Provocation inflige une frappe physique de 0,8 (seul
/// changement de données, Bastion au contenu 0.5.0) ; le pilote lance une provocation qui inflige des dégâts sur l'ennemi désigné quand
/// aucun assaillant ne vise un allié. Les objectifs provisoires de la feuille de route sont tous atteints avec les données livrées ; le
/// relevé du 8.6 (contenu figé 0.4.0) reste reproductible.
/// </summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Bastion")]
public class Lot87BalanceTests
{
    private static string MeasuresDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios", "mesures");

    private static IReadOnlyList<MeasureScenario> Scenarios(GameData data)
    {
        var failures = new List<(string Path, ScenarioLoadResult Result)>();
        IReadOnlyList<MeasureScenario> scenarios = Measures.LoadScenarios(MeasuresDirectory, data, failures);
        Assert.Empty(failures);
        return scenarios;
    }

    private static IReadOnlyList<ReferenceBuild> References(GameData data) =>
        new[] { Measures.Rempart, Measures.Contrecoup, Measures.Gardien }.Select(id => { Assert.True(data.TryGetBuild(id, out ReferenceBuild b)); return b; }).ToList();

    private static IReadOnlyList<ObjectiveResult> Objectives(GameData data) =>
        Measures.Objectives(Measures.RunAll(Scenarios(data), References(data), Measures.Profiles.Values.ToList()));

    /// <summary>Empreinte du Bastion 0.5.0 figé au schéma de classe 9 (sous-lot 11.4).</summary>
    internal const string Content050Fingerprint = "sha256:b54ca7238ab70c8d5e125cfff3d851d9877c1b3138cff034f123f5781f45172f";

    [Fact]
    public void Bastion_livre_au_contenu_0_5_0()
    {
        // Sous-lot 11.4 : le Bastion du 8.7 est figé (Fixtures/content-0.5.0, au schéma de classe 9) ; le contenu livré 0.6.0 n'y ajoute que
        // les portées (Lot114SpaceDataTests). Ancienne empreinte, au schéma 8 : 51f673a6….
        ClassLoadResult result = ClassJsonReader.ReadFile(ScenarioTestData.Content050ClassPath, ClassTestData.Progression);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal((9, "0.5.0-provisoire"), (result.Class!.SchemaVersion, result.Class.ContentVersion));
        Assert.Equal(Content050Fingerprint, result.Class.Fingerprint);
        ValidationErrorAssert.SingleCycleWarning(result);
    }

    [Fact]
    public void Seule_la_Provocation_change_depuis_le_contenu_0_4_0()
    {
        // Comparaison champ par champ des deux fichiers de classe : seuls la version de contenu et le bloc combat de la Provocation
        // (puissance, type de dégâts, coefficient) diffèrent.
        JsonObject shipped = JsonNode.Parse(File.ReadAllText(ScenarioTestData.Content050ClassPath))!.AsObject();   // sous-lot 11.4 : figé
        JsonObject frozen = JsonNode.Parse(File.ReadAllText(Content040ClassPath))!.AsObject();
        Assert.Equal(("0.5.0-provisoire", "0.4.0-provisoire"), (shipped["contentVersion"]!.GetValue<string>(), frozen["contentVersion"]!.GetValue<string>()));
        JsonObject Provocation(JsonObject root) => root["data"]!["skills"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == "skill.bastion.provocation")!["combat"]!.AsObject();
        JsonObject after = Provocation(shipped);
        Assert.Equal(("physique", "physique", 0.8m), (after["powerType"]!.GetValue<string>(), after["damageType"]!.GetValue<string>(), after["coefficient"]!.GetValue<decimal>()));
        Assert.True(after.Remove("powerType") && after.Remove("damageType") && after.Remove("coefficient"));
        shipped["contentVersion"] = frozen["contentVersion"]!.GetValue<string>();
        Assert.Equal(frozen.ToJsonString(), shipped.ToJsonString());

        SkillCombatDefinition combat = ClassTestData.Bastion.Skills.Single(s => s.Id == "skill.bastion.provocation").Combat!;
        Assert.True(combat.HasDamage);
        Assert.Equal((10, 12_000L, 5, "taunt"), (combat.EnduranceCost, combat.CooldownMs, combat.ResonanceGain, combat.Applies.Single().Status));
    }

    [Fact]
    public void Tous_les_objectifs_provisoires_atteints_avec_les_donnees_livrees()
    {
        IReadOnlyList<ObjectiveResult> objectives = Objectives(ShippedData);
        Assert.Equal(8, objectives.Count);
        Assert.All(objectives, o => Assert.True(o.Passed == true, $"{o.Id} : {o.Detail}"));
    }

    [Fact]
    public void Releve_du_8_6_reproduit_avec_le_contenu_fige()
    {
        // Avant la passe : seul S1 échoue (écart de 27,21 % : Gardien 16,8 contre Contrecoup 23,08).
        IReadOnlyList<ObjectiveResult> before = Objectives(Content040Data);
        ObjectiveResult s1 = before.Single(o => o.Id == "S1.dps");
        Assert.False(s1.Passed);
        Assert.StartsWith("écart 27.21 %", s1.Detail, StringComparison.Ordinal);
        Assert.All(before.Where(o => o.Id != "S1.dps"), o => Assert.True(o.Passed == true, o.Id));
    }

    [Fact]
    public void Ecart_S1_du_Gardien_ramene_sous_15_pourcent()
    {
        ObjectiveResult s1 = Objectives(ShippedData).Single(o => o.Id == "S1.dps");
        Assert.Equal("écart 10.53 % (Rempart 20.65, Contrecoup 23.08, Gardien 21.2)", s1.Detail);
    }

    private static PilotedCombat Gardien(string scenario, PilotProfile profile = PilotProfile.Standard)
    {
        MeasureScenario s = Scenarios(ShippedData).Single(x => x.Id == scenario);
        Assert.True(ShippedData.TryGetBuild(Measures.Gardien, out ReferenceBuild gardien));
        return PilotedCombat.Run(Measures.WithBuild(s.Setup, gardien), Measures.MeasuredId, profile, NullCombatLog.Instance);
    }

    [Fact]
    public void Provocation_lancee_sur_le_mannequin_pour_ses_degats()
    {
        // S1 : aucun assaillant ; la Provocation, qui inflige désormais des dégâts, frappe l'ennemi désigné (provocation refusée
        // notApplicable sur un mannequin, +5 Résonance), toutes les 12 s à partir du tick 0.
        PilotedCombat combat = Gardien("S1");
        var casts = combat.Setup.Commands.Where(c => c.Skill == "skill.bastion.provocation").ToList();
        Assert.Equal(5, casts.Count);
        Assert.All(casts, c => Assert.Equal("mannequin", c.Target));
        Assert.Equal(0, combat.Result["bastion"].CommandsRejected);
    }

    [Fact]
    public void Provocation_detourne_d_abord_un_assaillant_qui_vise_l_allie()
    {
        // S4 : les deux pillards visent l'allié ; la première Provocation va au premier qui frappe (pillard_a, tick 20).
        PilotedCombat combat = Gardien("S4");
        CombatCommand first = combat.Setup.Commands.First(c => c.Skill == "skill.bastion.provocation");
        Assert.Equal("pillard_a", first.Target);
    }
}

/// <summary>Avertissement de cycle statique de la classe livrée (Riposte précise et Jugement du colosse, sous-lot 8.5).</summary>
internal static class ValidationErrorAssert
{
    public static void SingleCycleWarning(ClassLoadResult result)
    {
        var warning = Assert.Single(result.Report.Warnings);
        Assert.Equal("$.data.trees[1].talents[0].triggers[0]", warning.Path);
        Assert.Contains("talent.bastion.contrecoup.riposte_precise/0, talent.bastion.contrecoup.jugement_du_colosse/0", warning.Message, StringComparison.Ordinal);
    }
}
