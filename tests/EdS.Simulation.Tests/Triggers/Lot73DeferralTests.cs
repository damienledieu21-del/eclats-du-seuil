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
/// Lot 7.3.5 : report des réactions entre phases et ticks (§7), réactions perdues en fin de combat (<c>deferredDropped</c>) et
/// annulations à l'exécution (§8, Q5) : <c>bearerDead</c>, <c>originGone</c>, <c>noTarget</c>.
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73DeferralTests
{
    private static TriggerDefinition Riposte(TriggerEvent on, TriggerRole? role = null, TriggerTarget target = TriggerTarget.EventSource) =>
        new(on, new[] { TriggerEffect.Strike(target, Fixed.Parse("0.5")) }, role);

    private static CharacterSetup Bearer(string id = "bastion", IEnumerable<TriggerDefinition>? passive = null, string team = "joueurs",
        Attributes? attributes = null) =>
        Character(id, team, 0, attributes ?? Attrs(vitalite: 200), null, Class(passiveTriggers: passive), passives: passive is null ? null : new[] { Passif });

    private static AssailantSetup Hitter(long firstMs = 1000, long intervalMs = 60_000, string target = "bastion", params AssailantAttack[] attacks) =>
        new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, target, firstMs, intervalMs, attacks.Length == 0 ? new[] { Blow() } : attacks);

    private static (List<JsonElement> Events, CausalGraph Graph) Simulate(CombatSetup setup)
    {
        var (_, log) = Run(setup);
        return (Events(log), CausalGraph.Build(log.Lines));
    }

    private static long Eid(JsonElement e) => e.Int("eid");
    private static int Depth(JsonElement e) => e.TryGetProperty("depth", out JsonElement d) ? d.GetInt32() : 0;
    private static JsonElement Ended(List<JsonElement> events) => events.OfKind("combatEnded").Single();

    private static readonly AssailantAttack Bleed = Strike("entaille", 100, applies: Apply("bleed", "0.4"));

    [Fact]
    public void Report_au_tick_suivant_avec_provenance_conservee()
    {
        // Riposte à un tick de saignement (phase 4) : reportée en résolution du tick suivant, avec deferredFromTick ; racine,
        // profondeur et cause sont celles de la réaction (racine du tour d'assaillant, profondeur 1, cause = le tick de DOT).
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.DotTick, TriggerRole.Target) }), Hitter(attacks: Bleed) }, durationMs: 8000, rules: NoUpkeep));
        var dots = events.OfKind("dotDamage").ToList();
        var strikes = events.OfKind("damage").Where(e => Depth(e) == 1).ToList();
        Assert.Equal(3, strikes.Count);
        for (int i = 0; i < strikes.Count; i++)
        {
            Assert.Equal((dots[i].Tick() + 1, dots[i].Tick(), Eid(dots[i])), (strikes[i].Tick(), strikes[i].Int("deferredFromTick"), strikes[i].Int("cause")));
            Assert.Equal("assailantTurn:assaillant:0", graph[Eid(strikes[i])].Root);
        }
        Assert.DoesNotContain(events.Where(e => Depth(e) == 0), e => e.TryGetProperty("deferredFromTick", out _));
        Assert.Equal(0, Ended(events).Int("deferredDropped"));
    }

    [Fact]
    public void Report_depuis_le_dernier_tick_perdu_et_compte()
    {
        // Dernier tick 140 : le tick de saignement de 140 provoque une riposte pour le tick 141, qui n'existe pas.
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.DotTick, TriggerRole.Target) }), Hitter(attacks: Bleed) }, durationMs: 7050, rules: NoUpkeep));
        Assert.Equal(140L, events.OfKind("dotDamage").Last().Tick());
        Assert.Equal(2, events.OfKind("damage").Count(e => Depth(e) == 1));
        JsonElement ended = Ended(events);
        Assert.Equal((141L, 1L), (ended.Int("ticks"), ended.Int("deferredDropped")));
        Assert.Empty(events.OfKind("reactionCancelled"));
    }

    [Fact]
    public void Report_perdu_quand_le_combat_se_termine_pendant_le_tick()
    {
        // Le tueur riposte à la mort (phase 6) : frappe reportée au tick suivant ; l'équipe adverse est vaincue, rien n'est exécuté.
        var fort = Bearer("fort", new[] { Riposte(TriggerEvent.Death, target: TriggerTarget.AllEnemies) }, "rouge", Attrs(force: 500));
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Frappe, "fort", "faible") }, new CombatantSetup[] { fort, Bearer("faible", team: "bleu", attributes: Attrs(vitalite: 0)) }));
        JsonElement ended = Ended(events);
        Assert.Equal(("teamDefeated", 1L), (ended.Str("reason"), ended.Int("deferredDropped")));
        Assert.Single(events.OfKind("damage"));
    }

    [Fact]
    public void Annulation_porteur_mort_entre_acceptation_et_execution()
    {
        // L'impact (1 point) pose un saignement ; son premier tick tue le Bastion (tick 60, phase 4) ; sa riposte, acceptée de son vivant, est reportée au
        // tick 61, où il est mort : reactionCancelled bearerDead, qui cite le tick de DOT. Son allié garde le combat ouvert.
        var bleed = Strike("entaille", 1, applies: Apply("bleed", "100"));
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Bearer(passive: new[] { Riposte(TriggerEvent.DotTick, TriggerRole.Target) }, attributes: Attrs(vitalite: 0)), Bearer("allie"), Hitter(attacks: bleed),
        }, durationMs: 4000, rules: NoUpkeep));
        JsonElement dot = Assert.Single(events.OfKind("dotDamage"));
        JsonElement cancelled = Assert.Single(events.OfKind("reactionCancelled"));
        Assert.Equal(("bearerDead", 61L, 60L, Eid(dot), 1L, "skill.essai.passif/0@bastion#0"),
            (cancelled.Str("reason")!, cancelled.Tick(), cancelled.Int("deferredFromTick"), cancelled.Int("cause"), cancelled.Int("depth"), cancelled.Str("trigger")!));
        Assert.Equal(("reactionCancelled", Eid(dot)), graph.Cited.Select(c => (c.Kind, c.Cause)).Single());
        Assert.DoesNotContain(events.OfKind("damage"), e => Depth(e) == 1);
        Assert.Equal(0, Ended(events).Int("deferredDropped"));
    }

    [Fact]
    public void Annulation_origine_disparue_l_alteration_porteuse_a_expire()
    {
        // « epine » (2,05 s) porte une riposte aux ticks de DOT subis. Posée au tick 20 avec un saignement : le tick de DOT 60
        // provoque la riposte, reportée au tick 61 ; « epine » expire au tick 61 en phase 1, avant la résolution : originGone.
        var epine = new StatusDefinition("epine", StatusCategory.Modifier, 2050, StatusStacking.Refresh,
            triggers: new[] { Riposte(TriggerEvent.DotTick, TriggerRole.Target) });
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(epine)) };
        var attack = Strike("piqure", 100, applies: new[] { Apply("bleed", "0.4"), Apply("epine") });
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bearer(), Hitter(attacks: attack) }, durationMs: 4000, rules: rules));
        long instance = events.OfKind("stateApplied").Single(e => e.Str("state") == "epine").Int("instance");
        Assert.Equal(61L, events.OfKind("stateExpired").Single(e => e.Str("state") == "epine").Tick());
        JsonElement cancelled = Assert.Single(events.OfKind("reactionCancelled"));
        Assert.Equal(("originGone", 61L, $"epine/0@bastion#{instance}"), (cancelled.Str("reason")!, cancelled.Tick(), cancelled.Str("trigger")!));
        Assert.DoesNotContain(events.OfKind("damage"), e => Depth(e) == 1);
    }

    [Fact]
    public void Annulation_sans_cible_rupture_par_l_entretien()
    {
        // Riposte à la rupture de garde subie : la rupture par l'entretien n'a pas de source (phase 4, report au tick suivant) ;
        // aucune cible au premier effet : noTarget.
        var rules = new CombatRules { GuardUpkeepPerSecond = 1000 };
        var (events, _) = Simulate(Setup(new[] { GuardStart(0) },
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.GuardBroken, TriggerRole.Target) }), Hitter(firstMs: 20_000) }, durationMs: 1000, rules: rules));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        JsonElement cancelled = Assert.Single(events.OfKind("reactionCancelled"));
        Assert.Equal(("noTarget", broken.Tick() + 1, broken.Tick(), Eid(broken)),
            (cancelled.Str("reason")!, cancelled.Tick(), cancelled.Int("deferredFromTick"), cancelled.Int("cause")));
    }

    [Fact]
    public void Etourdissement_du_porteur_ne_bloque_pas_ses_reactions()
    {
        // L'impact étourdit le Bastion (Q12) : sa riposte s'exécute quand même, juste après.
        var stun = Strike("assomme", 100, applies: Apply("stun"));
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.HitTaken) }), Hitter(attacks: stun) }, durationMs: 2000, rules: NoUpkeep));
        JsonElement stunned = events.OfKind("stateApplied").Single(e => e.Str("state") == "stun");
        JsonElement strike = events.OfKind("damage").Single(e => Depth(e) == 1);
        Assert.True(strike.Int("seq") > stunned.Int("seq"));
        Assert.Equal(stunned.Tick(), strike.Tick());
        Assert.Empty(events.OfKind("reactionCancelled"));
    }

    [Fact]
    public void Reaction_autonome_non_annulee_quand_l_alteration_observee_expire()
    {
        // Riposte au dernier tick de saignement (phase 4, l'instance expire dans la même étape) : la réaction est reportée et
        // s'exécute au tick suivant bien que le saignement ait expiré ; seule une réaction portée par une altération en dépend.
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { Riposte(TriggerEvent.DotTick, TriggerRole.Target) }), Hitter(attacks: Bleed) }, durationMs: 8000, rules: NoUpkeep));
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Contains(events.OfKind("damage"), e => Depth(e) == 1 && e.Tick() == expired.Tick() + 1);
        Assert.Empty(events.OfKind("reactionCancelled"));
    }
}
