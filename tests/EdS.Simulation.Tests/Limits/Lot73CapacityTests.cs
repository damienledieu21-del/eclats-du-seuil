using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using Xunit.Abstractions;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Limits;

/// <summary>
/// Lot 7.3.6 : capacités mesurées avec déclencheurs, règles provisoires (budgets 10 000 / 1 000 / 100 000, profondeur 4). Arbre
/// maximal (ripostes de zone entre deux équipes), ping-pong entre deux porteurs, 64 porteurs frappés à chaque tick. Les valeurs sont
/// reportées dans docs/LIMITES_LOT_7.md ; un changement qui les modifie doit être fait sciemment. Les mesures sans déclencheur
/// (<c>Lot71CapacityTests</c>) sont inchangées.
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Limits")]
public class Lot73CapacityTests(ITestOutputHelper output)
{
    private sealed record Measured(CombatResult Result, List<JsonElement> Events, int Reactions, int Suppressed, int MaxDepth, long Bound);

    private Measured Measure(string label, CombatSetup setup)
    {
        var (result, log) = Run(setup);
        List<JsonElement> events = Events(log);
        // Une réaction = un couple (effet observé, déclencheur) dont les lignes sont d'un niveau plus profond que l'effet observé.
        var depthOf = events.Where(e => e.TryGetProperty("eid", out _)).ToDictionary(e => e.Int("eid"), Depth);
        int reactions = events.Where(e => e.TryGetProperty("trigger", out _) && e.TryGetProperty("eid", out _)
                && Depth(e) == depthOf[e.Int("cause")] + 1)
            .Select(e => (e.Int("cause"), e.Str("trigger"))).Distinct().Count();
        int suppressed = events.OfKind("triggerSuppressed").Count();
        int depth = events.Select(Depth).DefaultIfEmpty(0).Max();
        CombatSetup.QueueBound bound = setup.ComputeQueueBound();
        Assert.False(bound.Guaranteed);
        output.WriteLine($"{label} : fin {result.EndReason}{(result.AbortReason is null ? "" : " (" + result.AbortReason + ")")} au tick {result.EndTick}, " +
            $"travail {result.Peaks.WorkPerTick}, lignes {result.Peaks.EventsPerTick}, file {result.Peaks.PendingEvents} (majorant non garanti {bound.Total}), " +
            $"effets de réaction {reactions}, suppressions {suppressed}, profondeur max {depth}, journal {events.Count} lignes");
        return new Measured(result, events, reactions, suppressed, depth, bound.Total);
    }

    private static int Depth(JsonElement e) => e.TryGetProperty("depth", out JsonElement d) && e.TryGetProperty("eid", out _) ? d.GetInt32() : 0;

    private static TriggerDefinition Riposte(TriggerTarget target, string coefficient = "0.1") =>
        new(TriggerEvent.HitTaken, new[] { TriggerEffect.Strike(target, Fixed.Parse(coefficient)) });

    private static CharacterSetup Bearer(string id, string team, TriggerDefinition trigger) =>
        Character(id, team, 0, Attrs(vitalite: 200), null, Class(passiveTriggers: new[] { trigger }), passives: new[] { Passif });

    /// <summary>
    /// <paramref name="k"/> contre <paramref name="k"/> : chacun riposte à l'impact subi par une frappe de zone. Une commande de
    /// a0 sur b0 ouvre l'arbre : chaque niveau multiplie les branches par k, jusqu'à la profondeur 4 ou au retour d'un porteur
    /// déjà dans la chaîne (cycle).
    /// </summary>
    private static CombatSetup Tree(int k) => Setup(new[] { Cmd(0, Frappe, "a0", "b0") },
        Enumerable.Range(0, k).Select(i => (CombatantSetup)Bearer($"a{i}", "rouge", Riposte(TriggerTarget.AllEnemies)))
            .Concat(Enumerable.Range(0, k).Select(i => Bearer($"b{i}", "bleu", Riposte(TriggerTarget.AllEnemies)))),
        durationMs: 1000, rules: NoUpkeep);

    /// <summary>Mesures figées : fin, tick de fin, pics (travail, lignes, file), réactions exécutées, suppressions, profondeur maximale.</summary>
    private static void AssertMeasured(Measured m, CombatEndReason end, long tick, long work, long events, long pending, int reactions, int suppressed, int depth)
    {
        Assert.Equal((end, tick), (m.Result.EndReason, m.Result.EndTick));
        Assert.Equal((work, events, pending), (m.Result.Peaks.WorkPerTick, m.Result.Peaks.EventsPerTick, m.Result.Peaks.PendingEvents));
        Assert.Equal((reactions, suppressed, depth), (m.Reactions, m.Suppressed, m.MaxDepth));
        Assert.InRange(m.Result.Peaks.PendingEvents, 0, m.Bound);
    }

    [Theory]
    // 2 contre 2 : 1 + 2 + 2 + 2 réactions (le retour d'un porteur déjà dans la chaîne est un cycle), 8 suppressions.
    [InlineData(2, CombatEndReason.DurationReached, 19, 113, 25, 5, 7, 8)]
    [InlineData(3, CombatEndReason.DurationReached, 19, 446, 114, 37, 22, 45)]
    [InlineData(4, CombatEndReason.DurationReached, 19, 1359, 375, 145, 53, 160)]
    // 6 contre 6 : l'explosion est arrêtée par le budget de lignes au tick 0, proprement.
    [InlineData(6, CombatEndReason.Aborted, 0, 3621, 969, 741, 155, 36)]
    public void Arbre_maximal_de_ripostes_de_zone(int k, CombatEndReason end, long tick, long work, long events, long pending, int reactions, int suppressed)
    {
        Measured m = Measure($"arbre {k} contre {k}", Tree(k));
        AssertMeasured(m, end, tick, work, events, pending, reactions, suppressed, 4);
        if (end == CombatEndReason.Aborted) Assert.Equal("eventBudgetExceeded", m.Result.AbortReason);
    }

    [Fact]
    public void Ping_pong_entre_deux_porteurs()
    {
        // a frappe b ; b riposte (1), a riposte (2) ; la riposte suivante de b referait b : cycle à la profondeur 3.
        Measured m = Measure("ping-pong", Setup(new[] { Cmd(0, Frappe, "a", "b") },
            new CombatantSetup[] { Bearer("a", "rouge", Riposte(TriggerTarget.EventSource)), Bearer("b", "bleu", Riposte(TriggerTarget.EventSource)) },
            durationMs: 1000, rules: NoUpkeep));
        AssertMeasured(m, CombatEndReason.DurationReached, 19, 30, 6, 2, 2, 1, 2);
        JsonElement line = Assert.Single(m.Events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", 3L), (line.Str("reason"), line.Int("depth")));
    }

    /// <summary>32 personnages et 32 assaillants, tous porteurs d'une riposte ; chaque assaillant frappe « son » personnage à chaque tick.</summary>
    private static CombatSetup SixtyFour(TriggerTarget target) => Setup(Array.Empty<CombatCommand>(),
        Enumerable.Range(0, 32).Select(i => (CombatantSetup)Bearer($"bastion_{i:00}", "joueurs", Riposte(target)))
            .Concat(Enumerable.Range(0, 32).Select(i => new AssailantSetup($"assaillant_{i:00}", "cibles", Fixed.Zero, Fixed.Zero, $"bastion_{i:00}", 0, 50,
                new[] { Blow(1) }, triggers: new[] { Riposte(target) }))),
        durationMs: 30_000, rules: NoUpkeep);

    [Fact]
    public void Soixante_quatre_porteurs_ripostes_ciblees()
    {
        // Par tick et par paire : impact, riposte du personnage (1), riposte de l'assaillant (2), puis cycle.
        Measured m = Measure("64 porteurs, ripostes ciblées", SixtyFour(TriggerTarget.EventSource));
        AssertMeasured(m, CombatEndReason.DurationReached, 599, 928, 160, 64, 38_400, 19_200, 2);
    }

    [Fact]
    public void Soixante_quatre_porteurs_ripostes_de_zone()
    {
        // 32 ripostes de zone sur 32 cibles dès le premier tick : arrêt par le budget de lignes.
        Measured m = Measure("64 porteurs, ripostes de zone", SixtyFour(TriggerTarget.AllEnemies));
        AssertMeasured(m, CombatEndReason.Aborted, 0, 2616, 832, 808, 24, 0, 1);
        Assert.Equal("eventBudgetExceeded", m.Result.AbortReason);
    }
}
