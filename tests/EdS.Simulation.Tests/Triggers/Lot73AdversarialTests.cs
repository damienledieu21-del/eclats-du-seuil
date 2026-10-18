using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Triggers;

/// <summary>
/// Lot 7.3.7 : tests adversariaux du plan (§10.2) non couverts par les étapes précédentes : cycles différés (par DOT, par expiration,
/// par report), cycle traversant une commande, arbre maximal exact S + S² + S³ + S⁴, explosion arrêtée par le budget sans réaction
/// partielle, parade parfaite qui bloque une altération sans réaction <c>onStateApplied</c>.
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73AdversarialTests
{
    private static CharacterSetup Bearer(string id = "bastion", IEnumerable<TriggerDefinition>? passive = null, string team = "joueurs",
        Attributes? attributes = null, string[]? actives = null) =>
        Character(id, team, 0, attributes ?? Attrs(vitalite: 200), actives, Class(passiveTriggers: passive), passives: passive is null ? null : new[] { Passif });

    private static TriggerDefinition Applies(TriggerEvent on, string status, TriggerTarget target, TriggerRole? role = null, string? coefficient = null,
        TriggerFilter? filter = null) =>
        new(on, new[] { TriggerEffect.ApplyStatus(target, status, coefficient is null ? null : Fixed.Parse(coefficient)) }, role, filter);

    private static TriggerDefinition Strikes(TriggerEvent on, TriggerTarget target, string coefficient, TriggerRole? role = null) =>
        new(on, new[] { TriggerEffect.Strike(target, Fixed.Parse(coefficient)) }, role);

    private static (List<JsonElement> Events, CausalGraph Graph, CombatResult Result, MemoryCombatLog Log) Simulate(CombatSetup setup)
    {
        var (result, log) = Run(setup);
        return (Events(log), CausalGraph.Build(log.Lines), result, log);
    }

    private static long Eid(JsonElement e) => e.Int("eid");
    private static int Depth(JsonElement e) => e.TryGetProperty("depth", out JsonElement d) && e.TryGetProperty("eid", out _) ? d.GetInt32() : 0;

    /// <summary>Réactions exécutées : couples (effet observé, déclencheur) dont les lignes sont d'un niveau plus profond que l'effet observé.</summary>
    private static List<(long Cause, string Trigger, int Depth, int Lines)> Reactions(List<JsonElement> events)
    {
        var depthOf = events.Where(e => e.TryGetProperty("eid", out _)).ToDictionary(Eid, Depth);
        return events.Where(e => e.TryGetProperty("eid", out _) && e.TryGetProperty("trigger", out _) && Depth(e) == depthOf[e.Int("cause")] + 1)
            .GroupBy(e => (e.Int("cause"), e.Str("trigger")!))
            .Select(g => (g.Key.Item1, g.Key.Item2, Depth(g.First()), g.Count())).ToList();
    }

    // --- Cycles différés ----------------------------------------------------------------------------

    [Fact]
    public void Cycle_differe_par_DOT_une_reapplication_puis_cycle_a_chaque_tick()
    {
        // Le Bastion brûle l'assaillant, puis réapplique la brûlure à chaque tick de DOT dont il est la source. Le premier tick (0)
        // provoque la réapplication (1) ; l'instance hérite de sa provenance (A8) : les ticks suivants sont de profondeur 1, chaîne
        // [A] : cycle à chaque tick. La cadence ne change pas ; la fin effective est celle de la réapplication.
        var reapply = Applies(TriggerEvent.DotTick, "burn", TriggerTarget.EventTarget, TriggerRole.Source, "0.5");
        CombatSetup Build(TriggerDefinition[]? passive) => Setup(new[] { Cmd(0, Brasier, target: "assaillant") },
            new CombatantSetup[] { Bearer(passive: passive, actives: new[] { Brasier, Frappe, Heurt, Onde }), Assailant(firstAttackMs: 60_000) },
            durationMs: 15_000, rules: NoUpkeep);
        var (events, _, _, _) = Simulate(Build(new[] { reapply }));
        var (control, _, _, _) = Simulate(Build(null));

        var dots = events.OfKind("dotDamage").ToList();
        var applied = events.OfKind("stateApplied").Where(e => e.Str("state") == "burn").ToList();
        Assert.Equal(new[] { 0, 1 }, applied.Select(Depth));
        Assert.Equal(dots[0].Tick(), applied[1].Tick());
        Assert.Equal(new[] { 0 }.Concat(Enumerable.Repeat(1, dots.Count - 1)), dots.Select(Depth));
        var cycles = events.OfKind("triggerSuppressed").ToList();
        Assert.Equal(dots.Skip(1).Select(Eid), cycles.Select(c => c.Int("cause")));
        Assert.All(cycles, c => Assert.Equal(("cycle", 2L), (c.Str("reason")!, c.Int("depth"))));

        // Cadence : mêmes ticks que sans déclencheur, prolongés ; fin effective à l'échéance de la réapplication.
        long period = StatusCatalog.Provisional["burn"].PeriodMs / 50;
        var controlTicks = control.OfKind("dotDamage").Select(e => e.Tick()).ToList();
        Assert.Equal(controlTicks, dots.Select(e => e.Tick()).Take(controlTicks.Count));
        Assert.All(dots.Zip(dots.Skip(1)), p => Assert.Equal(period, p.Second.Tick() - p.First.Tick()));
        Assert.Equal(applied[1].Int("untilTick"), events.OfKind("stateExpired").Single(e => e.Str("state") == "burn").Tick());
    }

    [Fact]
    public void Cycle_differe_par_expiration_une_boucle_puis_cycle()
    {
        // L'assaillant pose la Fragilité ; à son expiration, le Bastion se la repose. L'instance reposée porte la provenance de la
        // réaction : sa propre expiration relancerait le même déclencheur, déjà dans la chaîne : cycle.
        var again = Applies(TriggerEvent.StateExpired, "fragility", TriggerTarget.Bearer, TriggerRole.Target, filter: new TriggerFilter(status: "fragility"));
        var (events, _, _, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { again }), Assailant(intervalMs: 60_000, attacks: Blow(1, fragility: true)) },
            durationMs: 20_000, rules: NoUpkeep));
        var expired = events.OfKind("stateExpired").Where(e => e.Str("state") == "fragility").ToList();
        Assert.Equal(new[] { 0, 1 }, expired.Select(Depth));
        Assert.Equal(new[] { 0, 1 }, events.OfKind("stateApplied").Where(e => e.Str("state") == "fragility").Select(Depth));
        JsonElement cycle = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", 2L, Eid(expired[1])), (cycle.Str("reason")!, cycle.Int("depth"), cycle.Int("cause")));
    }

    [Fact]
    public void Cycle_differe_par_report_detecte_au_tick_suivant()
    {
        // « fort » frappe à la mort de chacun de ses ennemis (phase 6) : sa frappe de zone est reportée au tick suivant et tue e2 ;
        // cette mort, causée par la réaction, relancerait le même déclencheur : cycle au tick 1.
        var avenge = Strikes(TriggerEvent.Death, TriggerTarget.AllEnemies, "1");
        var (events, graph, _, _) = Simulate(Setup(new[] { Cmd(0, Frappe, "fort", "e1") }, new CombatantSetup[]
        {
            Bearer("fort", new[] { avenge }, "rouge", Attrs(force: 500)),
            Bearer("e1", team: "bleu", attributes: Attrs(vitalite: 0)), Bearer("e2", team: "bleu", attributes: Attrs(vitalite: 0)),
            Dummy("mannequin", "bleu"),
        }, durationMs: 1000, rules: NoUpkeepAreaStrikes));
        var died = events.OfKind("died").ToList();
        Assert.Equal(new[] { ("e1", 0L, 0), ("e2", 1L, 1) }, died.Select(d => (d.Str("combatant")!, d.Tick(), Depth(d))));
        Assert.All(events.OfKind("damage").Where(e => Depth(e) == 1), e => Assert.Equal((1L, 0L), (e.Tick(), e.Int("deferredFromTick"))));
        JsonElement cycle = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", 1L, 2L, Eid(died[1])), (cycle.Str("reason")!, cycle.Tick(), cycle.Int("depth"), cycle.Int("cause")));
        Assert.Equal("command:0", graph[Eid(died[1])].Root);
    }

    [Fact]
    public void Cycle_traversant_une_commande_nouvelle_racine_sans_suppression()
    {
        // Le Bastion frappe de nouveau la cible de chacun de ses impacts. Dans une même racine, sa frappe de réaction le relance :
        // cycle. Une nouvelle commande ouvre une nouvelle racine : la réaction y est acceptée de nouveau.
        var follow = Strikes(TriggerEvent.HitDealt, TriggerTarget.EventTarget, "0.1");
        var (events, graph, _, _) = Simulate(Setup(new[] { Cmd(0, Gratuite), Cmd(1000, Gratuite) },
            new CombatantSetup[] { Bearer(passive: new[] { follow }), Dummy() }, durationMs: 2000, rules: NoUpkeep));
        var reactions = events.OfKind("damage").Where(e => Depth(e) == 1).ToList();
        Assert.Equal(new[] { "command:0", "command:1" }, reactions.Select(e => graph[Eid(e)].Root));
        var cycles = events.OfKind("triggerSuppressed").ToList();
        Assert.Equal(reactions.Select(Eid), cycles.Select(c => c.Int("cause")));
        Assert.All(cycles, c => Assert.Equal(("cycle", 2L), (c.Str("reason")!, c.Int("depth"))));
    }

    // --- Arbre maximal et explosion --------------------------------------------------------------------

    private static readonly StatusDefinition[] Marks = Enumerable.Range(1, 4)
        .Select(i => new StatusDefinition($"m{i}", StatusCategory.Modifier, 1000, StatusStacking.Refresh)).ToArray();

    private static TriggerDefinition OnMark(int observed, int applied) =>
        Applies(TriggerEvent.StateApplied, $"m{applied}", TriggerTarget.AllEnemies, TriggerRole.Target, filter: new TriggerFilter(status: $"m{observed}"));

    /// <summary>
    /// x et S porteurs a contre S porteurs b. L'onde de x touche les S porteurs b (niveau 1 : chacun pose m1 sur l'équipe de x) ;
    /// chaque a reçoit m1 et pose m2 sur les b (niveau 2) ; chaque b reçoit m2 et pose m3 (niveau 3) ; chaque a reçoit m3 et pose
    /// m4 (niveau 4) ; chaque b reçoit m4 : profondeur 5, suppression. Chaque niveau fait réagir un déclencheur absent de la chaîne :
    /// S + S² + S³ + S⁴ réactions, S⁵ suppressions « depth », aucun cycle.
    /// </summary>
    private static CombatSetup Tree(int s)
    {
        TriggerDefinition[] b = { Applies(TriggerEvent.HitTaken, "m1", TriggerTarget.AllEnemies), OnMark(2, 3), OnMark(4, 1) };
        TriggerDefinition[] a = { OnMark(1, 2), OnMark(3, 4) };
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Concat(Marks)) };
        return Setup(new[] { Cmd(0, Onde, "x", null) },
            new CombatantSetup[] { Bearer("x", team: "rouge") }
                .Concat(Enumerable.Range(0, s).Select(i => Bearer($"a{i}", a, "rouge")))
                .Concat(Enumerable.Range(0, s).Select(i => Bearer($"b{i}", b, "bleu"))),
            durationMs: 500, rules: rules);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Arbre_maximal_exact_S_plus_S2_plus_S3_plus_S4(int s)
    {
        var (events, graph, result, _) = Simulate(Tree(s));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        var reactions = Reactions(events);
        for (int level = 1; level <= 4; level++)
            Assert.Equal((int)Math.Pow(s, level), reactions.Count(r => r.Depth == level));
        Assert.Equal(s + s * s + s * s * s + s * s * s * s, reactions.Count);
        var suppressed = events.OfKind("triggerSuppressed").ToList();
        Assert.Equal((int)Math.Pow(s, 5), suppressed.Count);
        Assert.All(suppressed, e => Assert.Equal(("depth", 5L), (e.Str("reason")!, e.Int("depth"))));
        Assert.Equal(4, graph.Nodes.Values.Max(n => n.Depth));
    }

    [Fact]
    public void Explosion_arretee_par_le_budget_sans_reaction_partielle_et_deterministe()
    {
        // S = 4 : 340 réactions et plus de 1 400 lignes d'état dans le tick 0, au-delà des 1 000 lignes permises.
        var (events, _, result, log) = Simulate(Tree(4));
        Assert.Equal((CombatEndReason.Aborted, "eventBudgetExceeded", 0L), (result.EndReason, result.AbortReason, result.EndTick));
        // Chaque réaction exécutée a écrit toutes ses lignes : une application par ennemi vivant de son porteur (5 pour un b, 4 pour un a).
        var reactions = Reactions(events);
        Assert.NotEmpty(reactions);
        Assert.All(reactions, r => Assert.Equal(r.Trigger.Contains("@b") ? 5 : 4, r.Lines));
        Assert.Equal(log.Lines, Run(Tree(4)).Log.Lines);
    }

    // --- Parade et altérations ------------------------------------------------------------------------

    [Fact]
    public void Parade_parfaite_qui_empeche_une_alteration_ne_provoque_aucune_reaction()
    {
        // La Fragilité de l'impact paré est refusée (stateBlocked parried, diagnostic) : ni la cible (onStateApplied, rôle cible)
        // ni l'assaillant (onStateApplied, rôle source) ne réagissent.
        var onApplied = new TriggerDefinition(TriggerEvent.StateApplied, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One) }, TriggerRole.Target);
        var onApplying = new TriggerDefinition(TriggerEvent.StateApplied, new[] { TriggerEffect.Strike(TriggerTarget.EventTarget, Fixed.One) }, TriggerRole.Source);
        var (events, _, _, _) = Simulate(Setup(new[] { Parry(1000) }, new CombatantSetup[]
        {
            Bearer(passive: new[] { onApplied }),
            new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 60_000, new[] { Blow(1, fragility: true) }, triggers: new[] { onApplying }),
        }, durationMs: 4000, rules: NoUpkeep));
        Assert.Equal("parried", Assert.Single(events.OfKind("stateBlocked")).Str("reason"));
        Assert.Empty(events.OfKind("stateApplied"));
        Assert.DoesNotContain(events, e => e.TryGetProperty("depth", out _));
    }
}
