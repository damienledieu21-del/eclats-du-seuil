using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Limits;

/// <summary>
/// Lot 7.3.6 : budgets des déclencheurs et majorant de file (§6, C12, Q11). Chaque réservation d'une notification et d'une réaction
/// est placée exactement en N − 1, N et N + 1 pour chacun des trois budgets (travail, lignes, file) : à N − 1 le combat s'arrête sur
/// cette réservation, sans aucune ligne de la réaction et avec un journal identique jusque-là ; à N et N + 1 elle passe. Avec
/// déclencheurs, le majorant de file n'est plus garanti et l'arrêt <c>queueLimitExceeded</c> est atteignable après une admission
/// valide, sans réaction partielle.
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Limits")]
public class Lot73BudgetTests
{
    private static readonly StatusDefinition Marque = new("marque", StatusCategory.Modifier, 60_000, StatusStacking.Refresh);
    private static readonly StatusDefinition Sceau = new("sceau", StatusCategory.Modifier, 60_000, StatusStacking.Refresh);
    private static readonly StatusCatalog Catalog = new(StatusCatalog.Provisional.Definitions.Append(Marque).Append(Sceau));

    private static CombatRules Rules(int work = 10_000, int events = 1000, int pending = 100_000, int depth = 4) =>
        new() { GuardUpkeepPerSecond = 0, MaxWorkPerTick = work, MaxEventsPerTick = events, MaxPendingEvents = pending, MaxTriggerDepth = depth, Statuses = Catalog };

    /// <summary>Riposte à l'impact subi : frappe l'assaillant puis lui pose un saignement (une mise en file : son tick de DOT).</summary>
    private static readonly TriggerDefinition Riposte = new(TriggerEvent.HitTaken,
        new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse("0.5")), TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "bleed", Fixed.Parse("0.1")) });

    private static CharacterSetup Bastion(TriggerDefinition trigger) =>
        Character("bastion", "joueurs", 0, Attrs(vitalite: 200), null, Class(passiveTriggers: new[] { trigger }), passives: new[] { Passif });

    private static AssailantSetup Hitter(long intervalMs = 60_000) =>
        new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, intervalMs, new[] { Blow(1) });

    private static CombatSetup Scenario(CombatRules rules, TriggerDefinition? trigger = null, long intervalMs = 60_000, long durationMs = 3000) =>
        Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bastion(trigger ?? Riposte), Hitter(intervalMs) }, durationMs: durationMs, rules: rules);

    private sealed record Traced(CombatResult Result, MemoryCombatLog Log, List<CombatEngine.ReservationAttempt> Attempts, int Accepted);

    /// <summary>Sans le majorant d'admission : les capacités de file testées ici sont volontairement trop petites pour lui.</summary>
    private static Traced RunTraced(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log, queueBound: false)
        {
            ReservationTrace = new List<CombatEngine.ReservationAttempt>(),
            ReactionTrace = new List<(long, string, long)>(),
        };
        while (engine.Step()) { }
        CausalGraph.Build(log.Lines);
        return new Traced(engine.Result!, log, engine.ReservationTrace, engine.ReactionTrace.Count);
    }

    /// <summary>Lignes de simulation (après l'en-tête et les deux combattants), sans l'enveloppe de fin.</summary>
    private static List<string> SimulationLines(MemoryCombatLog log)
    {
        var lines = log.Lines.Skip(3).ToList();
        int end = lines.FindIndex(l => l.Contains("\"event\":\"simulationAborted\"") || l.Contains("\"event\":\"combatantSummary\""));
        return lines.Take(end).ToList();
    }

    private static bool IsReaction(string line) => line.Contains("\"depth\":");

    public static readonly TheoryData<string, string> Boundaries = new()
    {
        { "notify", "work" }, { "notify", "events" }, { "notify", "queue" },
        { "reaction", "work" }, { "reaction", "events" }, { "reaction", "queue" },
    };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Reservation_aux_frontieres_N_moins_1_N_N_plus_1(string kind, string budget)
    {
        // La notification ne coûte une ligne que si elle supprime : pour le budget de lignes, profondeur 0 (suppression).
        int depth = kind == "notify" && budget == "events" ? 0 : 4;
        // Correctif 7.3.1 (A73-M04) : la réservation crédite le retrait de l'élément traité. Une notification à une seule réaction a
        // alors une occupation nette 0 + 1 (N − 1 = 0, hors règles) ; pour la file, le porteur a deux ripostes : la notification en
        // met deux en file, soit une de plus que le tour d'assaillant qui l'a produite.
        bool twin = kind == "notify" && budget == "queue";
        CombatSetup Scenario(CombatRules rules) => twin
            ? Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
                {
                    Character("bastion", "joueurs", 0, Attrs(vitalite: 200), null, Class(passiveTriggers: new[] { Riposte, Riposte }), passives: new[] { Passif }),
                    Hitter(),
                }, durationMs: 3000, rules: rules)
            : Lot73BudgetTests.Scenario(rules);
        Traced full = RunTraced(Scenario(Rules(depth: depth)));
        int index = full.Attempts.FindIndex(a => a.Kind == kind);
        Assert.True(index >= 0);
        CombatEngine.ReservationAttempt at = full.Attempts[index];
        (long used, long need) = budget switch
        {
            "work" => (at.Work, at.NeedWork),
            "events" => (at.Events, at.NeedEvents),
            _ => (at.Queue, at.NeedQueue),
        };
        Assert.True(need > 0, $"{kind} ne consomme pas de {budget}");
        long n = checked(used + need);
        CombatRules At(long limit) => budget switch
        {
            "work" => Rules(work: (int)limit, events: (int)Math.Min(1000, limit), depth: depth),
            "events" => Rules(events: (int)limit, depth: depth),
            _ => Rules(pending: (int)limit, depth: depth),
        };
        bool Passed(Traced run) => kind == "notify"
            ? (depth == 0 ? run.Log.Lines.Any(l => l.Contains("\"event\":\"triggerSuppressed\"")) : run.Accepted > 0)
            : run.Log.Lines.Any(IsReaction);

        // N − 1 : arrêt exactement sur cette réservation, au même tick ; rien de la réaction, journal identique jusque-là.
        Traced refused = RunTraced(Scenario(At(n - 1)));
        string reason = budget switch { "work" => "workBudgetExceeded", "events" => "eventBudgetExceeded", _ => "queueLimitExceeded" };
        Assert.Equal((CombatEndReason.Aborted, reason, at.Tick), (refused.Result.EndReason, refused.Result.AbortReason, refused.Result.EndTick));
        Assert.Equal(index + 1, refused.Attempts.Count);
        Assert.Equal((at.Kind, at.Tick, at.Work, at.Events, at.Queue), (refused.Attempts[index].Kind, refused.Attempts[index].Tick,
            refused.Attempts[index].Work, refused.Attempts[index].Events, refused.Attempts[index].Queue));
        Assert.False(Passed(refused));
        Assert.DoesNotContain(refused.Log.Lines, IsReaction);
        List<string> before = SimulationLines(refused.Log);
        Assert.Equal(before, SimulationLines(full.Log).Take(before.Count));

        // N et N + 1 : la réservation passe.
        Assert.True(Passed(RunTraced(Scenario(At(n)))));
        Assert.True(Passed(RunTraced(Scenario(At(n + 1)))));
    }

    [Fact]
    public void Majorant_non_garanti_des_qu_un_declencheur_existe()
    {
        CombatSetup.QueueBound plain = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character("bastion"), Hitter() }, rules: Rules(work: 500))
            .ComputeQueueBound();
        Assert.Equal((true, 0L), (plain.Guaranteed, plain.Transient));

        CombatSetup.QueueBound bound = Scenario(Rules(work: 500)).ComputeQueueBound();
        Assert.Equal((false, 1000L), (bound.Guaranteed, bound.Transient));
        Assert.Equal(bound.Initial + bound.Dots + bound.Expiries + 1000, bound.Total);   // le saignement posé par la riposte n'est pas majoré
        Assert.Equal((plain.Initial, plain.Dots, plain.Expiries), (bound.Initial, bound.Dots, bound.Expiries));
        Assert.Contains("NON garanti", bound.Describe(100_000));
        Assert.Contains("garanti", plain.Describe(100_000));
        Assert.DoesNotContain("NON", plain.Describe(100_000));

        // Une altération du catalogue qui porte un déclencheur suffit, même sans porteur au départ.
        var porteuse = new StatusDefinition("porteuse", StatusCategory.Modifier, 1000, StatusStacking.Refresh, triggers: new[] { Riposte });
        var rules = new CombatRules { Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(porteuse)) };
        Assert.False(Setup(Array.Empty<CombatCommand>(), rules: rules).ComputeQueueBound().Guaranteed);
    }

    [Fact]
    public void Terme_transitoire_refuse_a_l_admission_quand_la_capacite_ne_suffit_pas()
    {
        CombatSetup setup = Scenario(Rules(work: 500));
        long total = setup.ComputeQueueBound().Total;
        Assert.Empty(Scenario(Rules(work: 500, events: 500, pending: (int)total)).Validate());
        RuleViolation violation = Assert.Single(Scenario(Rules(work: 500, events: 500, pending: (int)total - 1)).Validate());
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Contains("terme transitoire des déclencheurs 1000", violation.Message);
    }

    [Fact]
    public void Saturation_par_reactions_persistantes_arret_queueLimitExceeded_sans_reaction_partielle()
    {
        // Chaque impact subi (un par tick) provoque une réaction qui pose « marque » puis « sceau » (60 s, refresh) sur l'assaillant :
        // deux échéances de plus par tick, non majorées. L'admission valide le combat ; la file déborde en cours de combat.
        var trigger = new TriggerDefinition(TriggerEvent.HitTaken,
            new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "marque"), TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "sceau") });
        CombatSetup Build(int pending) => Scenario(Rules(work: 50, events: 50, pending: pending), trigger, intervalMs: 50, durationMs: 60_000);
        long total = Build(100_000).ComputeQueueBound().Total;
        CombatSetup setup = Build((int)total);
        Assert.False(setup.ComputeQueueBound().Guaranteed);
        Assert.Empty(setup.Validate());

        var (result, log) = Run(setup);
        Assert.Equal((CombatEndReason.Aborted, "queueLimitExceeded"), (result.EndReason, result.AbortReason));
        List<JsonElement> events = Events(log);
        Assert.Equal(total, Assert.Single(events.OfKind("simulationAborted")).Int("limit"));
        Assert.True(result.Peaks.PendingEvents <= total);
        // Réservation de la réaction entière avant toute mutation : autant de « sceau » que de « marque ».
        int marques = events.OfKind("stateApplied").Count(e => e.Str("state") == "marque");
        Assert.True(marques > 0);
        Assert.Equal(marques, events.OfKind("stateApplied").Count(e => e.Str("state") == "sceau"));

        // Arrêt déterministe : deux exécutions identiques octet par octet.
        Assert.Equal(log.Lines, Run(Build((int)total)).Log.Lines);
    }
}
