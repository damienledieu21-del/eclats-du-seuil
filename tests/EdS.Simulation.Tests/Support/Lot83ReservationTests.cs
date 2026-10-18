using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Support.Lot83Kit;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 : réservation exacte d'une action dont l'impact est transféré à un protecteur (impact, ligne de transfert) et d'une
/// compétence de soutien à cibles propres, budgets de travail et de lignes en N − 1, N et N + 1 : à N − 1, arrêt au même tick sur cette
/// réservation, sans aucune ligne de l'action ; à N et N + 1, l'action passe.
/// </summary>
[Trait("Category", "Support")]
[Trait("Category", "Limits")]
public class Lot83ReservationTests
{
    private static CombatRules Budgets(int work = 10_000, int events = 1000) => new()
    {
        GuardUpkeepPerSecond = 0, MaxWorkPerTick = work, MaxEventsPerTick = events, Statuses = Rules.Statuses,
    };

    private sealed record Traced(CombatResult Result, MemoryCombatLog Log, List<CombatEngine.ReservationAttempt> Attempts);

    private static Traced RunTraced(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log, queueBound: false) { ReservationTrace = new List<CombatEngine.ReservationAttempt>() };
        while (engine.Step()) { }
        CausalGraph.Build(log.Lines);
        return new Traced(engine.Result!, log, engine.ReservationTrace);
    }

    /// <summary>
    /// Lien posé au tick 0 ; au tick 20, l'attaque de l'assaillant sur l'allié (qui porte gel, Fragilité et saignement) est transférée
    /// pour un quart ; ultime de groupe au tick 40. Pour que le tick examiné soit le plus chargé du combat (un budget à N − 1 n'arrête rien
    /// avant), l'attaque porte quatre altérations quand on examine le tick 20 (<paramref name="heavy"/>), aucune pour le tick 40. Le budget
    /// de travail n'est examiné qu'au tick 20 : au tick 40, le travail fixe du tick 0 (une unité par combattant et par phase) dépasse celui
    /// de l'ultime.
    /// </summary>
    private static CombatSetup Scenario(CombatRules rules, bool heavy) =>
        Fight(new[] { Use(0, Lien, target: "allie"), Use(2000, Absolu) },
            new CombatantSetup[] { Bastion(Lien, Absolu, Barriere, Garde), Ally(), Brute(intervalMs: 60_000, applies: heavy ? new[]
            {
                new StatusApplication("freeze"), new StatusApplication("fragility"), new StatusApplication("stun"),
                new StatusApplication("bleed", Domain.Numerics.Fixed.Parse("0.1")),
            } : Array.Empty<StatusApplication>()) },
            durationMs: 2100, rules: rules);

    public static readonly TheoryData<long, string, string> Boundaries = new()
    {
        { 20, "work", "damageTransferred" }, { 20, "events", "damageTransferred" }, { 40, "events", "stateApplied" },
    };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Reservation_de_l_action_aux_frontieres(long tick, string budget, string marker)
    {
        Traced full = RunTraced(Scenario(Budgets(), tick == 20));
        int index = full.Attempts.FindLastIndex(a => a.Kind == "action" && a.Tick == tick);
        Assert.True(index >= 0);
        CombatEngine.ReservationAttempt at = full.Attempts[index];
        long n = budget == "work" ? at.Work + at.NeedWork : at.Events + at.NeedEvents;
        CombatRules At(long limit) => budget == "work" ? Budgets(work: (int)limit, events: (int)Math.Min(1000, limit)) : Budgets(events: (int)limit);
        // Précondition : aucun tick antérieur ne consomme plus que N − 1 (sinon l'arrêt aurait lieu avant cette réservation).
        long earlier = 0;
        var probe = new CombatEngine(Scenario(Budgets(), tick == 20), new MemoryCombatLog(), queueBound: false);
        while (probe.CurrentTick < tick && probe.Step()) earlier = Math.Max(earlier, budget == "work" ? probe.LastTickWork : probe.LastTickEvents);
        Assert.True(earlier <= n - 1, $"tick antérieur à {earlier} pour N − 1 = {n - 1} ({budget})");
        bool Passed(Traced run) => run.Log.Lines.Any(l => l.Contains($"\"tick\":{tick},") && l.Contains($"\"event\":\"{marker}\""));
        Assert.True(Passed(full));

        Traced refused = RunTraced(Scenario(At(n - 1), tick == 20));
        Assert.Equal((CombatEndReason.Aborted, budget == "work" ? "workBudgetExceeded" : "eventBudgetExceeded", tick),
            (refused.Result.EndReason, refused.Result.AbortReason, refused.Result.EndTick));
        Assert.Equal(index + 1, refused.Attempts.Count);
        Assert.False(Passed(refused));
        Assert.DoesNotContain(refused.Log.Lines, l => l.Contains($"\"tick\":{tick},") && (l.Contains("\"event\":\"damage\"") || l.Contains("\"event\":\"stateApplied\"")));
        Assert.True(Passed(RunTraced(Scenario(At(n), tick == 20))));
        Assert.True(Passed(RunTraced(Scenario(At(n + 1), tick == 20))));
    }
}
