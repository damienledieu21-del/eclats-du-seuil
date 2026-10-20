using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.1 : PV des assaillants dans les scénarios (schéma 4) et dans l'en-tête du journal (format 9). Un assaillant avec
/// <c>health</c> est mortel ; sans, il garde les PV infinis de l'entraînement (A21).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class Lot111ScenarioDataTests
{
    private const string Scenario = "entrainement_garde_parade";

    private static JsonObject AssailantOf(JsonObject root) =>
        DataOf(root)["combatants"]!.AsArray().Select(c => c!.AsObject()).First(c => c["type"]!.GetValue<string>() == "assailant");

    private static ScenarioLoadResult WithHealth(JsonNode? health) => ReadMutated(s => AssailantOf(s)["health"] = health, scenario: Scenario);

    [Fact]
    public void PV_lus_et_assaillant_mortel()
    {
        ScenarioLoadResult result = WithHealth(400);
        Assert.True(result.IsValid, result.Report.ToString());
        AssailantSetup assailant = result.Setup!.Combatants.OfType<AssailantSetup>().Single();
        Assert.Equal(400, assailant.Health);
        ScenarioLoadResult shipped = ReadMutated(_ => { }, scenario: Scenario);
        Assert.Null(shipped.Setup!.Combatants.OfType<AssailantSetup>().Single().Health);
    }

    [Fact]
    public void Journal_rejoue_a_l_identique_et_en_tete_relu_avec_les_PV()
    {
        ScenarioLoadResult result = WithHealth(400);
        var log = new MemoryCombatLog();
        CombatResult combat = CombatEngine.Run(result.Setup!, log);
        Assert.Equal(400, combat["assaillant"].MaxHealth);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
        CombatSetup? read = CombatLogReplayer.ReadSetup(log.Lines[0], ShippedData, out IReadOnlyList<string> problems);
        Assert.Empty(problems);
        Assert.Equal(400, read!.Combatants.OfType<AssailantSetup>().Single().Health);
    }

    [Fact]
    public void Assaillant_tue_dans_le_scenario_d_entrainement()
    {
        // Peu de PV : les coups du Bastion du scénario suffisent ; la mort et la victoire passent par le journal et le rejeu.
        ScenarioLoadResult result = WithHealth(60);
        var log = new MemoryCombatLog();
        CombatResult combat = CombatEngine.Run(result.Setup!, log);
        Assert.True(combat["assaillant"].IsDead);
        Assert.Equal((CombatEndReason.TeamDefeated, "joueurs"), (combat.EndReason, combat.Winner));
        Assert.Single(log.Lines, l => l.Contains("\"event\":\"died\"", StringComparison.Ordinal));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1000000001")]
    public void PV_hors_bornes_refuses(string health)
    {
        ScenarioLoadResult result = WithHealth(JsonNode.Parse(health));
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.combatants[1].health");
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("\"400\"")]
    [InlineData("null")]
    public void PV_non_entiers_refuses(string health)
    {
        ScenarioLoadResult result = WithHealth(JsonNode.Parse(health));
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.combatants[1].health");
    }

    [Fact]
    public void PV_refuses_sur_un_personnage()
    {
        ScenarioLoadResult result = ReadMutated(s => DataOf(s)["combatants"]![0]!.AsObject()["health"] = 400, scenario: Scenario);
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.combatants[0].health");
    }

    [Fact]
    public void PV_refuses_sur_un_mannequin()
    {
        ScenarioLoadResult result = ReadMutated(s =>
            DataOf(s)["combatants"]!.AsArray().Select(c => c!.AsObject()).First(c => c["type"]!.GetValue<string>() == "dummy")["health"] = 400);
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path.EndsWith(".health", StringComparison.Ordinal));
    }

    [Fact]
    public void Schema_3_refuse_sans_migration()
    {
        Assert.Equal(5, ScenarioValidator.CurrentSchemaVersion);   // sous-lot 11.4 : 5 (combat positionné)
        ValidationError error = Assert.Single(ReadMutated(s => s["schemaVersion"] = 3, scenario: Scenario).Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
        Assert.Contains("antérieure à la version 5", error.Message);
    }

    [Fact]
    public void Schema_JSON_des_scenarios_en_version_4_avec_les_PV()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")));
        JsonElement root = schema.RootElement;
        Assert.Equal(5, root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());   // 11.4 : 5
        JsonElement health = root.GetProperty("$defs").GetProperty("combatant").GetProperty("properties").GetProperty("health");
        Assert.Equal(("integer", 1, AssailantSetup.MaxHealth),
            (health.GetProperty("type").GetString(), health.GetProperty("minimum").GetInt64(), health.GetProperty("maximum").GetInt64()));
    }

    /// <summary>
    /// Joue la meute à vaincre en solo avec un build de référence : <paramref name="defend"/> faux, le joueur ne se défend pas ; vrai, il
    /// alterne parade juste avant un coup et garde levée (script simple, déterministe).
    /// </summary>
    private static (CombatResult Result, double Seconds) PlaySolo(string build, bool defend)
    {
        TrainingSession session = TrainingSession.Create(ShippedData, ScenarioJsonReader.ReadFile(ScenarioPath("meute_a_vaincre"), ShippedData).Setup!, build);
        RealtimeCombat combat = session.Start();
        for (int step = 0; step < 90 * 20 && !combat.IsFinished; step++)
        {
            if (defend)
            {
                double? next = combat.Others().Where(o => o.IsEnemy && !o.View.IsDead && o.NextAttackSeconds is double).Select(o => o.NextAttackSeconds).Min();
                bool parryTurn = step / 7 % 2 == 0;
                if (parryTurn && next is double n && n < 0.12) combat.Parry();
                combat.SetGuard(!parryTurn && next is double g && g < 0.4);
            }
            if (step % 4 == 0) combat.UseSlot(step / 4 % 5);
            combat.Advance(0.05);
        }
        Assert.True(combat.IsFinished);
        return (combat.Result!, combat.Tick * combat.TickSeconds);
    }

    [Theory]
    [InlineData("build.bastion.rempart")]
    [InlineData("build.bastion.contrecoup")]
    [InlineData("build.bastion.gardien")]
    [InlineData("build.bastion.hybride")]
    [InlineData("build.bastion.niveau_10")]
    public void Meute_a_vaincre_gagnee_en_se_defendant_perdue_sans_defense(string build)
    {
        // Valeurs provisoires (scénario de démonstration du 11.1) : chaque build de référence gagne en se défendant, avant la fin du temps
        // imparti, et tombe sans se défendre.
        var (won, seconds) = PlaySolo(build, defend: true);
        Assert.Equal((CombatEndReason.TeamDefeated, "joueurs"), (won.EndReason, won.Winner));
        Assert.InRange(seconds, 5, 60);
        var (lost, _) = PlaySolo(build, defend: false);
        Assert.Equal("cibles", lost.Winner);
    }

    [Fact]
    public void Scenarios_livres_au_schema_4()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "scenarios");
        string[] files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(file, ShippedData);
            Assert.True(loaded.IsValid, file + " : " + loaded.Report);
        }
    }
}
