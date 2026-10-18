using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Shields.Lot82Kit;

namespace EdS.Simulation.Tests.Shields;

/// <summary>
/// Sous-lot 8.2 (revue Codex de la PR #6) : réservation exacte du retrait des boucliers épuisés, en fin de tick. La réservation du
/// retrait est placée exactement en N − 1, N et N + 1 pour chacun des trois budgets (travail, lignes, file) : à N − 1 le combat
/// s'arrête sur elle, au même tick, sans <c>stateRemoved</c> ni notification et avec un journal identique jusque-là ; à N et N + 1
/// le bouclier est retiré. Un déclencheur du porteur sur <c>onStateRemoved</c> rend le retrait notifiable (une mise en file).
/// </summary>
[Trait("Category", "Shield")]
[Trait("Category", "Limits")]
public class Lot82ReservationTests
{
    /// <summary>Retrait d'un état du porteur (rôle cible : celui qui portait l'état) : +1 endurance (gain de réaction, jamais notifié).</summary>
    private static readonly TriggerDefinition OnRemoved = new(TriggerEvent.StateRemoved,
        new[] { TriggerEffect.GainResource(TriggerResource.Endurance, Fixed.One) }, TriggerRole.Target);

    private static CombatRules Budgets(int work = 10_000, int events = 1000, int pending = 100_000) => new()
    {
        MaxWorkPerTick = work, MaxEventsPerTick = events, MaxPendingEvents = pending, Statuses = Rules.Statuses,
    };

    /// <summary>
    /// Égide de 42 (2 % de 2 100 PV) posée au lancer, au tick 5 ; le coup de 100 de l'assaillant au tick 20 l'épuise : retrait à la
    /// fin du tick 20. Le coup pose aussi saignement, gel et Fragilité : le tick 20 est le plus chargé du combat (lignes, travail et
    /// file), si bien qu'un budget placé à N − 1 de la réservation du retrait n'arrête rien avant elle.
    /// </summary>
    private static CombatSetup Scenario(CombatRules rules) =>
        Fight(new[] { Cast(250) }, Protected(new[] { OnCast("egide", "0.02"), OnRemoved }),
            Assailant(intervalMs: 60_000, attacks: Strike("entaille", 100, DamageType.Physique, false, Apply("bleed", "0.5"), Apply("freeze"), Apply("fragility"))),
            durationMs: 1500, rules: rules);

    private sealed record Traced(CombatResult Result, MemoryCombatLog Log, List<CombatEngine.ReservationAttempt> Attempts);

    /// <summary>Sans le majorant d'admission : les capacités de file testées ici sont volontairement trop petites pour lui.</summary>
    private static Traced RunTraced(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log, queueBound: false) { ReservationTrace = new List<CombatEngine.ReservationAttempt>() };
        while (engine.Step()) { }
        CausalGraph.Build(log.Lines);
        return new Traced(engine.Result!, log, engine.ReservationTrace);
    }

    private static bool Depleted(string line) => line.Contains("\"event\":\"stateRemoved\"") && line.Contains("\"reason\":\"depleted\"");

    /// <summary>Lignes de simulation (après l'en-tête et les deux combattants), sans l'enveloppe de fin.</summary>
    private static List<string> SimulationLines(MemoryCombatLog log)
    {
        var lines = log.Lines.Skip(3).ToList();
        int end = lines.FindIndex(l => l.Contains("\"event\":\"simulationAborted\"") || l.Contains("\"event\":\"combatantSummary\""));
        return lines.Take(end).ToList();
    }

    [Fact]
    public void Retrait_reserve_avec_sa_ligne_et_sa_notification()
    {
        Traced full = RunTraced(Scenario(Budgets()));
        CombatEngine.ReservationAttempt at = Assert.Single(full.Attempts, a => a.Kind == "depleted");
        Assert.Equal(20L, at.Tick);
        // Examen du combattant + retrait d'un bouclier : une ligne et la notification du déclencheur (une mise en file).
        Assert.Equal((1L, 1L), (at.NeedEvents, at.NeedQueue));
        Assert.Contains(full.Log.Lines, Depleted);
        // La réaction au retrait s'exécute dans la même phase (point fixe de la phase 6), après la réservation du retrait.
        Assert.Contains(full.Attempts.Skip(full.Attempts.IndexOf(at) + 1), a => a.Kind == "reaction" && a.Tick == 20);
    }

    [Theory]
    [InlineData("work")]
    [InlineData("events")]
    [InlineData("queue")]
    public void Reservation_du_retrait_aux_frontieres_N_moins_1_N_N_plus_1(string budget)
    {
        Traced full = RunTraced(Scenario(Budgets()));
        int index = full.Attempts.FindIndex(a => a.Kind == "depleted");
        Assert.True(index >= 0);
        CombatEngine.ReservationAttempt at = full.Attempts[index];
        (long used, long need) = budget switch
        {
            "work" => (at.Work, at.NeedWork),
            "events" => (at.Events, at.NeedEvents),
            _ => (at.Queue, at.NeedQueue),
        };
        Assert.True(need > 0, $"le retrait ne consomme pas de {budget}");
        long n = checked(used + need);
        CombatRules At(long limit) => budget switch
        {
            "work" => Budgets(work: (int)limit, events: (int)Math.Min(1000, limit)),
            "events" => Budgets(events: (int)limit),
            _ => Budgets(pending: (int)limit),
        };

        // N − 1 : arrêt exactement sur cette réservation, au même tick ; ni retrait ni notification, journal identique jusque-là.
        Traced refused = RunTraced(Scenario(At(n - 1)));
        string reason = budget switch { "work" => "workBudgetExceeded", "events" => "eventBudgetExceeded", _ => "queueLimitExceeded" };
        Assert.Equal((CombatEndReason.Aborted, reason, at.Tick), (refused.Result.EndReason, refused.Result.AbortReason, refused.Result.EndTick));
        Assert.Equal(index + 1, refused.Attempts.Count);
        Assert.Equal((at.Kind, at.Tick, at.Work, at.Events, at.Queue), (refused.Attempts[index].Kind, refused.Attempts[index].Tick,
            refused.Attempts[index].Work, refused.Attempts[index].Events, refused.Attempts[index].Queue));
        Assert.DoesNotContain(refused.Log.Lines, Depleted);
        List<string> before = SimulationLines(refused.Log);
        Assert.Equal(before, SimulationLines(full.Log).Take(before.Count));

        // N et N + 1 : le bouclier épuisé est retiré.
        Assert.Contains(RunTraced(Scenario(At(n))).Log.Lines, Depleted);
        Assert.Contains(RunTraced(Scenario(At(n + 1))).Log.Lines, Depleted);
    }
}
