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
/// Lot 7.3.4 : réactions et effets génériques (frappe, application d'altération, gain de ressource), provenance des effets de
/// réaction, cycles directs et indirects, branches sœurs, profondeur maximale, gains non récursifs (Q8), réactions à la mort.
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73ReactionTests
{
    private static readonly TriggerFilter NoFilter = TriggerFilter.None;

    private static TriggerDefinition Gain(TriggerEvent on, int amount = 1, TriggerRole? role = null, TriggerFilter? filter = null,
        TriggerResource resource = TriggerResource.Resonance) =>
        new(on, new[] { TriggerEffect.GainResource(resource, Fixed.FromInt(amount)) }, role, filter);

    private static TriggerDefinition Riposte(TriggerEvent on = TriggerEvent.HitTaken, TriggerTarget target = TriggerTarget.EventSource,
        string coefficient = "0.5", TriggerRole? role = null) =>
        new(on, new[] { TriggerEffect.Strike(target, Fixed.Parse(coefficient)) }, role);

    private static TriggerDefinition Applies(TriggerEvent on, string status, TriggerTarget target, TriggerRole? role = null, string? coefficient = null) =>
        new(on, new[] { TriggerEffect.ApplyStatus(target, status, coefficient is null ? null : Fixed.Parse(coefficient)) }, role);

    private static CharacterSetup Bearer(string id = "bastion", IEnumerable<TriggerDefinition>? passive = null, string team = "joueurs",
        Attributes? attributes = null, string[]? actives = null, int initiative = 0) =>
        Character(id, team, initiative, attributes ?? Attrs(vitalite: 200), actives, Class(passiveTriggers: passive), passives: passive is null ? null : new[] { Passif });

    private static AssailantSetup Hitter(long firstMs = 1000, long intervalMs = 60_000, string id = "assaillant", string target = "bastion",
        IEnumerable<TriggerDefinition>? triggers = null, params AssailantAttack[] attacks) =>
        new(id, "cibles", Fixed.Zero, Fixed.Zero, target, firstMs, intervalMs, attacks.Length == 0 ? new[] { Blow() } : attacks, triggers: triggers);

    private static (List<JsonElement> Events, CausalGraph Graph) Simulate(CombatSetup setup)
    {
        var (_, log) = Run(setup);
        return (Events(log), CausalGraph.Build(log.Lines));
    }

    private static long Eid(JsonElement e) => e.Int("eid");
    private static long Cause(JsonElement e) => e.GetProperty("cause").GetInt64();
    private static int Depth(JsonElement e) => e.TryGetProperty("depth", out JsonElement d) ? d.GetInt32() : 0;
    private static string? Trigger(JsonElement e) => e.TryGetProperty("trigger", out JsonElement t) ? t.GetString() : null;

    // --- Effets ------------------------------------------------------------------------------------

    [Fact]
    public void Gain_de_reaction_journalise_avec_sa_provenance()
    {
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Bearer(passive: new[] { Gain(TriggerEvent.HitDealt, 2) }), Dummy() }));
        JsonElement damage = Assert.Single(events.OfKind("damage"));
        JsonElement gain = events.OfKind("resourceGained").Single(e => e.Str("source") == "trigger");
        Assert.Equal((Eid(damage), 1, "skill.essai.passif/0@bastion#0", "resonance", 2L, "resolution"),
            (Cause(gain), Depth(gain), Trigger(gain), gain.Str("resource"), gain.Int("amount"), gain.Str("phase")));
        Assert.Equal("command:0", graph[Eid(gain)].Root);
        Assert.Equal(new[] { "skill.essai.passif/0@bastion#0" }, graph[Eid(gain)].Chain);
    }

    [Fact]
    public void Gain_d_endurance_par_reaction_plafonne()
    {
        // Frappe : 100 → 90 d'endurance ; la réaction rend 25, dont 15 perdus au plafond.
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Frappe) },
            new CombatantSetup[] { Bearer(passive: new[] { Gain(TriggerEvent.SkillCast, 25, resource: TriggerResource.Endurance) }), Dummy() }));
        JsonElement gain = events.OfKind("resourceGained").Single(e => e.Str("resource") == "endurance");
        Assert.Equal((10L, 15L, 100L, "commands"), (gain.Int("amount"), gain.Int("wasted"), gain.Int("value"), gain.Str("phase")));
    }

    [Fact]
    public void Application_par_reaction_provenance_memorisee_par_l_instance()
    {
        // Le Bastion pose la Fragilité sur son agresseur à chaque impact subi : application de profondeur 1, source = porteur ;
        // l'expiration hérite de cette provenance (A4), sans augmenter la profondeur.
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Applies(TriggerEvent.HitTaken, "fragility", TriggerTarget.EventSource) }), Hitter() }, durationMs: 8000, rules: NoUpkeep));
        JsonElement applied = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal(("assaillant", "bastion", 1, "skill.essai.passif/0@bastion#0"), (applied.Str("combatant"), applied.Str("source"), Depth(applied), Trigger(applied)));
        Assert.Equal(Eid(events.OfKind("damage").Single()), Cause(applied));
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal((Eid(applied), 1), (Cause(expired), Depth(expired)));
        Assert.Equal("assailantTurn:assaillant:0", graph[Eid(expired)].Root);
    }

    [Fact]
    public void Frappe_de_reaction_en_resolution_sans_critique_ni_tirage()
    {
        // Riposte du Bastion à chaque impact subi : même tick, phase de résolution, après l'impact. Le Bastion frappe aussi par
        // commandes : ses impacts (et leurs critiques, tirés du même flux) sont identiques avec ou sans riposte.
        var commands = Enumerable.Range(0, 5).Select(i => Cmd(i * 1000L, Gratuite, target: "assaillant")).ToArray();
        CombatSetup Fight(IEnumerable<TriggerDefinition>? passive) =>
            Setup(commands, new CombatantSetup[] { Bearer(passive: passive), Hitter(500, 1000) }, durationMs: 5000, rules: NoUpkeep);
        var (plain, _) = Simulate(Fight(null));
        var (riposte, graph) = Simulate(Fight(new[] { Riposte() }));

        var strikes = riposte.OfKind("damage").Where(e => Depth(e) == 1).ToList();
        Assert.Equal(5, strikes.Count);
        foreach (JsonElement strike in strikes)
        {
            JsonElement cause = riposte.Single(e => e.TryGetProperty("eid", out _) && Eid(e) == Cause(strike));
            Assert.Equal(("damage", "assaillant", "bastion"), (cause.Str("event"), cause.Str("source"), cause.Str("target")));
            Assert.Equal((cause.Tick(), "resolution", "bastion", "assaillant"), (strike.Tick(), strike.Str("phase"), strike.Str("source"), strike.Str("target")));
            Assert.False(strike.GetProperty("critical").GetBoolean());
            Assert.Equal(JsonValueKind.Null, strike.GetProperty("skill").ValueKind);
            Assert.Equal(JsonValueKind.Null, strike.GetProperty("attack").ValueKind);
        }
        string Own(JsonElement e) => $"{e.Tick()}:{e.GetProperty("critical").GetBoolean()}:{e.Int("amount")}";
        Assert.Equal(plain.OfKind("damage").Where(e => e.Str("source") == "bastion").Select(Own),
            riposte.OfKind("damage").Where(e => e.Str("source") == "bastion" && Depth(e) == 0).Select(Own));
        Assert.DoesNotContain(riposte.OfKind("resourceGained"), e => e.Str("source") == "directHit" && Depth(e) == 1);   // A5 : pas de Résonance
    }

    [Fact]
    public void Frappe_de_reaction_a_un_tick_de_dot_reportee_au_tick_suivant()
    {
        // Le Bastion riposte à chaque tick de saignement qu'il subit (phase 4) : la frappe part en résolution du tick suivant.
        var bleed = Strike("entaille", 100, applies: Apply("bleed", "0.4"));
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.DotTick, role: TriggerRole.Target) }), Hitter(attacks: bleed) }, durationMs: 8000, rules: NoUpkeep));
        var dots = events.OfKind("dotDamage").ToList();
        var strikes = events.OfKind("damage").Where(e => Depth(e) == 1).ToList();
        Assert.Equal(dots.Count, strikes.Count);
        Assert.Equal(dots.Select(d => (d.Tick() + 1, Eid(d))), strikes.Select(s => (s.Tick(), Cause(s))));
        Assert.All(strikes, s => Assert.Equal("resolution", s.Str("phase")));
    }

    // --- Cycles et profondeur -----------------------------------------------------------------------

    [Fact]
    public void Cycle_direct_supprime_des_la_premiere_recursion()
    {
        // Le Bastion frappe la cible de chacun de ses impacts : sa propre frappe de réaction le notifie, il est dans la chaîne.
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Frappe) },
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.HitDealt, TriggerTarget.EventTarget) }), Dummy() }));
        var hits = events.OfKind("damage").ToList();
        Assert.Equal(new[] { 0, 1 }, hits.Select(Depth));
        JsonElement suppressed = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", 2L, Eid(hits[1]), "skill.essai.passif/0@bastion#0"),
            (suppressed.Str("reason"), suppressed.Int("depth"), suppressed.Int("cause"), suppressed.Str("trigger")));
    }

    [Fact]
    public void Cycle_indirect_entre_deux_porteurs_A_B_A()
    {
        // Ping-pong entre deux porteurs : l'assaillant frappe (0), le Bastion riposte (1), l'assaillant riposte (2) ; le Bastion est
        // déjà dans la chaîne : cycle à la profondeur 3. Deux porteurs ne suffisent donc pas à atteindre la profondeur 4.
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte() }), Hitter(triggers: new[] { Riposte() }) }, durationMs: 2000, rules: NoUpkeep));
        Assert.Equal(new[] { 0, 1, 2 }, events.OfKind("damage").Select(Depth));
        JsonElement suppressed = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", 3L, "skill.essai.passif/0@bastion#0"), (suppressed.Str("reason"), suppressed.Int("depth"), suppressed.Str("trigger")));
        JsonElement last = events.OfKind("damage").Last();
        Assert.Equal(new[] { "skill.essai.passif/0@bastion#0", "assailant.assaillant/0@assaillant#0" }, graph[Eid(last)].Chain);
    }

    [Fact]
    public void Cycle_indirect_a_trois_A_B_C_A()
    {
        // A (Bastion) pose la Fragilité sur la cible de ses impacts ; B (assaillant) répond à l'état reçu par un gel sur sa source ;
        // C (Bastion) répond à l'état reçu en frappant sa source ; cet impact relance A, déjà dans la chaîne : cycle à la profondeur 4.
        var a = Applies(TriggerEvent.HitDealt, "fragility", TriggerTarget.EventTarget);
        var c = Riposte(TriggerEvent.StateApplied, TriggerTarget.EventSource, role: TriggerRole.Target);
        var b = Applies(TriggerEvent.StateApplied, "freeze", TriggerTarget.EventSource, TriggerRole.Target);
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Gratuite, target: "assaillant") },
            new CombatantSetup[] { Bearer(passive: new[] { a, c }), Hitter(60_000, triggers: new[] { b }) }, durationMs: 2000, rules: NoUpkeep));
        JsonElement cycle = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(("cycle", "skill.essai.passif/0@bastion#0", 4L), (cycle.Str("reason"), cycle.Str("trigger"), cycle.Int("depth")));
        Assert.Equal(new[] { "skill.essai.passif/0@bastion#0", "assailant.assaillant/0@assaillant#0", "skill.essai.passif/1@bastion#0" },
            graph[cycle.Int("cause")].Chain);
        Assert.Equal(new[] { 1, 2 }, events.OfKind("stateApplied").Select(Depth));
    }

    [Fact]
    public void Profondeur_maximale_puis_suppression()
    {
        // Trois contre trois, riposte sur tous les ennemis : chaque niveau fait réagir des porteurs encore absents de la chaîne,
        // jusqu'à la profondeur 4 ; au-delà, suppression « depth ». Aucune ligne au-delà de 4.
        CombatantSetup[] fighters =
        {
            Bearer("a1", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "x"),
            Bearer("a2", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "x"),
            Bearer("a3", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "x"),
            Bearer("b1", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "y"),
            Bearer("b2", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "y"),
            Bearer("b3", new[] { Riposte(target: TriggerTarget.AllEnemies, coefficient: "0.01") }, "y"),
        };
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Gratuite, "a1", "b1") }, fighters, durationMs: 1000, rules: NoUpkeepAreaStrikes));
        Assert.Equal(4, graph.Nodes.Values.Max(n => n.Depth));
        Assert.Contains(events.OfKind("triggerSuppressed"), e => e.Str("reason") == "depth" && e.Int("depth") == 5);
        Assert.All(events.OfKind("triggerSuppressed").Where(e => e.Str("reason") == "depth"), e => Assert.Equal(5L, e.Int("depth")));
        Assert.All(graph.Nodes.Values.Where(n => n.Depth == 4), n => Assert.Equal(4, n.Chain.Select(t => t.Split('@')[1]).Distinct().Count()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Profondeur_maximale_reglable(int maxDepth)
    {
        var rules = new CombatRules { MaxTriggerDepth = maxDepth, GuardUpkeepPerSecond = 0 };
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte() }), Hitter(triggers: new[] { Riposte() }) }, durationMs: 2000, rules: rules));
        Assert.Equal(Math.Min(maxDepth, 2), graph.Nodes.Values.Max(n => n.Depth));
        JsonElement suppressed = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal(maxDepth >= 2 ? "cycle" : "depth", suppressed.Str("reason"));
        Assert.Equal(Math.Min(maxDepth, 2) + 1, suppressed.Int("depth"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Branches_soeurs_independantes_quel_que_soit_leur_ordre(bool swapped)
    {
        // B (frappe la cible de ses impacts) et C (gain sur impact infligé) observent la frappe de réaction de B : B est supprimé
        // (cycle), C réagit avec sa propre chaîne {B, C}, profondeur 2, dans les deux ordres.
        TriggerDefinition b = Riposte(TriggerEvent.HitDealt, TriggerTarget.EventTarget), c = Gain(TriggerEvent.HitDealt);
        string bId = swapped ? "skill.essai.passif/1@bastion#0" : "skill.essai.passif/0@bastion#0";
        string cId = swapped ? "skill.essai.passif/0@bastion#0" : "skill.essai.passif/1@bastion#0";
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe) },
            new CombatantSetup[] { Bearer(passive: swapped ? new[] { c, b } : new[] { b, c }), Dummy() }));
        JsonElement strike = events.OfKind("damage").Single(e => Depth(e) == 1);
        JsonElement suppressed = Assert.Single(events.OfKind("triggerSuppressed"));
        Assert.Equal((bId, "cycle", Eid(strike)), (suppressed.Str("trigger")!, suppressed.Str("reason")!, suppressed.Int("cause")));
        JsonElement second = events.OfKind("resourceGained").Single(e => Depth(e) == 2);
        Assert.Equal((Eid(strike), cId), (Cause(second), Trigger(second)!));
        Assert.Equal(new[] { bId, cId }, graph[Eid(second)].Chain);
        Assert.Equal(1, events.OfKind("resourceGained").Count(e => Depth(e) == 1));   // C réagit aussi à l'impact d'origine
    }

    // --- Gains non récursifs (Q8) -------------------------------------------------------------------

    [Fact]
    public void Gain_naturel_notifie_gain_de_reaction_jamais_quelle_que_soit_la_profondeur()
    {
        // Le Bastion, en garde, réagit à chacun de ses gains de Résonance par un gain de Résonance. Gains naturels : Résonance de
        // blocage sous la frappe de riposte de l'assaillant à un allié (profondeur 1, tick 10), puis sous l'attaque de l'assaillant
        // (profondeur 0, tick 20). Chaque gain naturel provoque exactement un gain de réaction, qui n'en provoque aucun autre.
        var bastion = Bearer(passive: new[] { Gain(TriggerEvent.ResourceGained, filter: new TriggerFilter(resource: TriggerResource.Resonance)) });
        var commands = new[] { GuardStart(0), Cmd(500, Gratuite, "allie", "assaillant") };
        var (events, graph) = Simulate(Setup(commands,
            new CombatantSetup[] { bastion, Bearer("allie"), Hitter(1000, 60_000, triggers: new[] { Riposte(target: TriggerTarget.AllEnemies) }) },
            durationMs: 3000, rules: NoUpkeepAreaStrikes));
        var gains = events.OfKind("resourceGained").Where(e => e.Str("actor") == "bastion").ToList();
        var natural = gains.Where(e => e.Str("source") != "trigger").ToList();
        var reactions = gains.Where(e => e.Str("source") == "trigger").ToList();
        Assert.Equal(new[] { (10L, 1, "block"), (20L, 0, "block") }, natural.Select(e => (e.Tick(), Depth(e), e.Str("source")!)));
        Assert.Equal(natural.Select(Eid), reactions.Select(Cause));
        Assert.Equal(new[] { 2, 1 }, reactions.Select(Depth));
        Assert.Empty(events.OfKind("triggerSuppressed"));
        Assert.All(reactions, r => Assert.DoesNotContain(graph.Nodes.Values, n => n.Cause == Eid(r)));
    }

    // --- Mort ---------------------------------------------------------------------------------------

    [Fact]
    public void Reaction_du_tueur_en_fin_de_tick_une_seule_fois()
    {
        var fort = Bearer("fort", new[] { Gain(TriggerEvent.Death, 5), Applies(TriggerEvent.Death, "fragility", TriggerTarget.AllEnemies) }, "rouge",
            Attrs(force: 500));
        var faible = Bearer("faible", team: "bleu", attributes: Attrs(vitalite: 0));
        var autre = Bearer("autre", team: "bleu");
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Frappe, "fort", "faible") }, new CombatantSetup[] { fort, faible, autre }));
        JsonElement died = Assert.Single(events.OfKind("died"));
        JsonElement gain = events.OfKind("resourceGained").Single(e => e.Str("source") == "trigger");
        Assert.Equal((Eid(died), "endOfTick", 5L), (Cause(gain), gain.Str("phase"), gain.Int("amount")));
        JsonElement applied = Assert.Single(events.OfKind("stateApplied"));     // seul « autre » est encore un ennemi vivant
        Assert.Equal(("autre", Eid(died), "endOfTick"), (applied.Str("combatant"), Cause(applied), applied.Str("phase")));
    }
}
