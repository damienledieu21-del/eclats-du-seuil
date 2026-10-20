using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4d : instantané d'état sur tous les scénarios livrés (déclencheurs et réactions reportées, DOT, boucliers, protections,
/// provocations, garde et parade, combats positionnés, attaques annoncées, arrivées et départs). À chaque tick, l'instantané se relit
/// (contrôles de cohérence de l'état relu, puis réécriture à l'identique, dans <see cref="CombatEngine.Restore"/>) ; à cinq ticks
/// répartis et à chaque tick où une réaction reportée attend dans la file, la copie relue avance avec l'original jusqu'à la fin : mêmes
/// instantanés à chaque tick, mêmes lignes de journal, même résultat.
/// </summary>
[Trait("Category", "Presence")]
[Trait("Category", "Data")]
public class Lot114dSnapshotDataTests
{
    public static IEnumerable<object[]> Scenarios() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory, "scenarios"), p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Instantanes_d_un_scenario_livre_relus_et_continues_a_l_identique(string file)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "scenarios", file), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        long ticks = loaded.Setup!.DurationMs / loaded.Setup.Combat.TickMilliseconds;
        var resumeAt = new HashSet<long>(Enumerable.Range(0, 5).Select(k => ticks * k / 5 + 3));
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(loaded.Setup, log);
        var copies = new List<(CombatEngine Copy, MemoryCombatLog Log, int From)>();
        int checkedSnapshots = 0;
        while (!engine.IsFinished)
        {
            long tick = engine.CurrentTick;
            string text = engine.Snapshot().Text;
            // Une réaction reportée (entrainement_declencheurs) est dans la file entre deux ticks : la copie continue aussi depuis là.
            bool resume = resumeAt.Contains(tick) || text.Contains("\"item\":\"reaction\"", StringComparison.Ordinal);
            var restoredLog = new MemoryCombatLog();
            CombatEngine restored = CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), restoredLog);
            checkedSnapshots++;
            if (resume) copies.Add((restored, restoredLog, log.Lines.Count));
            engine.Step();
            foreach (var (copy, _, _) in copies)
            {
                copy.Step();
                if (!engine.IsFinished) Assert.Equal(engine.Snapshot().Text, copy.Snapshot().Text);
                Assert.Equal(engine.IsFinished, copy.IsFinished);
            }
        }
        Assert.True(checkedSnapshots > 3);
        foreach (var (copy, resumed, from) in copies)
        {
            Assert.Equal(log.Lines.Skip(from), resumed.Lines);
            Assert.Equal((engine.Result!.EndReason, engine.Result.EndTick), (copy.Result!.EndReason, copy.Result.EndTick));
        }
    }
}
