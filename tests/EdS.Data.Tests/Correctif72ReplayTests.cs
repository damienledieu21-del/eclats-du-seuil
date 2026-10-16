using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 7.2 : journaux et rejeux déterministes des nouveaux cas (étourdissement refusé « active », impact paré « parried »,
/// fin effective des DOT avec nextTick), à partir du scénario livré entrainement_alterations accéléré.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Status")]
public class Correctif72ReplayTests
{
    /// <summary>Assaillant principal toutes les 250 ms, dégâts bruts 1 : étourdissements répétés, DOT renouvelés hors cadence.</summary>
    private static CombatSetup Accelerated()
    {
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject assailant = Combatant(root, 1);
            assailant["intervalMs"] = 250;
            foreach (JsonNode? attack in assailant["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
        }, scenario: "entrainement_alterations");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Setup!;
    }

    private static MemoryCombatLog Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    [Fact]
    public void Journal_deterministe_et_rejeu_identique()
    {
        MemoryCombatLog first = Simulate(Accelerated()), second = Simulate(Accelerated());
        Assert.Equal(first.ToString(), second.ToString());
        foreach (string expected in new[] { "\"reason\":\"active\"", "\"reason\":\"immune\"", "\"reason\":\"parried\"", "\"nextTick\":", "\"renewed\":true" })
            Assert.Contains(expected, first.ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(first.Lines, ShippedData).Status);
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    [InlineData("entrainement_alterations")]
    public void Scenarios_livres_acceptes_par_le_controle_de_saturation(string name)
    {
        CombatSetup setup = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!;
        Assert.Empty(setup.Validate());
        CombatResult result = CombatEngine.Run(setup, new MemoryCombatLog());
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.InRange(result.Peaks.PendingEvents, 1, setup.ComputeQueueBound().Total);
    }
}
