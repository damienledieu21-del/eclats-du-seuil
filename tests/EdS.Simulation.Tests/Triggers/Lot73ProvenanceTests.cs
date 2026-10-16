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
/// Lot 7.3.2 : identité des effets (<c>eid</c>), parenté (<c>cause</c>), racines et provenance mémorisée sur les instances
/// d'altération. Aucune réaction encore : tout effet est de profondeur 0. Chaque simulation de <see cref="Run"/> vérifie déjà le
/// graphe causal complet (<see cref="CausalGraph"/>) ; ces tests vérifient les liens attendus, effet par effet.
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73ProvenanceTests
{
    private static long Eid(JsonElement e) => e.Int("eid");

    private static long? Cause(JsonElement e) => e.GetProperty("cause").ValueKind == JsonValueKind.Null ? null : e.GetProperty("cause").GetInt64();

    private static string? Root(JsonElement e) => e.TryGetProperty("root", out JsonElement root) ? root.GetString() : null;

    private static (List<JsonElement> Events, CausalGraph Graph) Simulate(CombatSetup setup)
    {
        var (_, log) = Run(setup);
        return (Events(log), CausalGraph.Build(log.Lines));
    }

    // --- Chaîne et provenance (structures) ----------------------------------------------------------

    [Fact]
    public void Chaine_immuable_et_branches_soeurs_independantes()
    {
        TriggerChain a = TriggerChain.Extend(null, "A");
        TriggerChain b = TriggerChain.Extend(a, "B"), c = TriggerChain.Extend(a, "C");
        Assert.Equal((1, 2, 2), (a.Length, b.Length, c.Length));
        Assert.True(TriggerChain.Contains(b, "A") && TriggerChain.Contains(b, "B"));
        Assert.False(TriggerChain.Contains(b, "C"));   // la sœur C n'apparaît jamais dans la chaîne de B
        Assert.False(TriggerChain.Contains(c, "B"));
        Assert.False(TriggerChain.Contains(a, "B"));   // étendre ne modifie pas le parent
        Assert.False(TriggerChain.Contains(null, "A"));
    }

    [Fact]
    public void Chaine_bornee_au_plafond_absolu()
    {
        TriggerChain? chain = null;
        for (int i = 0; i < TriggerChain.MaxLength; i++) chain = TriggerChain.Extend(chain, $"T{i}");
        Assert.Equal(CombatRules.AbsoluteMaxTriggerDepth, chain!.Length);
        Assert.Throws<InvalidOperationException>(() => TriggerChain.Extend(chain, "T8"));
    }

    [Fact]
    public void Provenance_consequence_directe_et_reaction()
    {
        Provenance root = Provenance.NewRoot("command:3", "bastion");
        Assert.Equal(("command:3", 0, (long?)null, (string?)null), (root.Root, root.Depth, root.Cause, root.Trigger));
        Assert.Null(root.Chain);

        Provenance direct = root.CausedBy(7);
        Assert.Equal(("command:3", 0, (long?)7, "bastion"), (direct.Root, direct.Depth, direct.Cause, direct.Source));
        Assert.Null(direct.Chain);

        Provenance reaction = direct.ReactionTo(9, "talent.x/0@mannequin#0", "mannequin");
        Assert.Equal(("command:3", 1, (long?)9, "talent.x/0@mannequin#0", "mannequin"),
            (reaction.Root, reaction.Depth, reaction.Cause, reaction.Trigger, reaction.Source));
        Assert.True(TriggerChain.Contains(reaction.Chain, "talent.x/0@mannequin#0"));
        Assert.Equal(0, direct.Depth);   // immuable

        Provenance second = reaction.CausedBy(10).ReactionTo(11, "skill.y/1@bastion#0", "bastion", 4, 2);
        Assert.Equal((2, 2, (long?)4, (int?)2), (second.Depth, second.Chain!.Length, second.SourceInstance, second.SourceGeneration));
        Provenance inherited = second.CausedBy(12);
        Assert.Equal((2, "skill.y/1@bastion#0", (long?)4), (inherited.Depth, inherited.Trigger, inherited.SourceInstance));
    }

    // --- Racines et conséquences directes ------------------------------------------------------------

    [Fact]
    public void Commande_racine_puis_consequences_directes()
    {
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe) }));
        JsonElement cast = Assert.Single(events.OfKind("skillCast"));
        Assert.Equal(("command:0", (long?)null), (Root(cast), Cause(cast)));
        Assert.Equal(1, Eid(cast));
        JsonElement damage = Assert.Single(events.OfKind("damage"));
        JsonElement gain = Assert.Single(events.OfKind("resourceGained"));
        Assert.Equal((Eid(cast), Eid(cast)), (Cause(damage)!.Value, Cause(gain)!.Value));
        Assert.Null(Root(damage));

        // Fin de recharge et régénération : racines propres (P1), sans parent.
        JsonElement ready = Assert.Single(events.OfKind("cooldownReady"));
        Assert.Equal(($"cooldown:bastion:{Frappe}:120", (long?)null), (Root(ready), Cause(ready)));
        JsonElement regen = events.OfKind("enduranceRegenerated").First();
        Assert.Equal($"regeneration:bastion:{regen.Tick()}", Root(regen));

        // Chaque effet remonte à sa racine ; le graphe compte un effet racine par racine.
        Assert.Equal("command:0", graph[Eid(damage)].Root);
        Assert.Equal(events.Count(e => e.TryGetProperty("eid", out _) && Cause(e) is null), graph.Roots.Count);
    }

    [Fact]
    public void Diagnostics_sans_identite_ni_provenance()
    {
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe), Cmd(0, Heurt), Cmd(100, Ruineuse) }));
        string[] diagnostics = { "combatant", "commandRejected", "combatantSummary", "combatEnded" };
        Assert.Equal(2, events.OfKind("commandRejected").Count());
        foreach (JsonElement e in events)
        {
            bool diagnostic = diagnostics.Contains(e.Str("event"));
            Assert.Equal(!diagnostic, e.TryGetProperty("eid", out _));
            if (diagnostic) Assert.DoesNotContain(CausalGraph.ProvenanceFields, f => e.TryGetProperty(f, out _));
        }
        Assert.Equal(events.Count(e => diagnostics.Contains(e.Str("event"))), graph.Diagnostics);
    }

    [Fact]
    public void Tours_d_assaillant_numerotes_sauts_compris()
    {
        // L'assaillant, étourdi au tick 0, saute son tour 0 (tick 20) : son premier impact est le tour 1, racine assailantTurn:…:1.
        var bastion = Character(actives: new[] { Assommer, Frappe, Heurt, Onde });
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Assommer, target: "assaillant") }, new CombatantSetup[] { bastion, Assailant(1000, 1000) },
            durationMs: 5000, rules: NoUpkeep));
        int skipped = events.TakeWhile(e => e.Str("event") != "attackStarted").Count(e => e.Str("event") == "attackSkipped");
        Assert.True(skipped >= 1);
        var roots = events.OfKind("attackStarted").Select(Root).ToList();
        Assert.Equal(Enumerable.Range(skipped, roots.Count).Select(n => $"assailantTurn:assaillant:{n}"), roots);
        JsonElement stunned = events.OfKind("stateApplied").First(e => e.Str("state") == "stun");
        Assert.Equal("damage", events.Single(e => e.TryGetProperty("eid", out _) && Eid(e) == Cause(stunned)).Str("event"));
    }

    [Fact]
    public void Parade_fin_de_fenetre_heritee_et_recompenses_causees_par_la_parade()
    {
        var (events, graph) = Simulate(Setup(new[] { Parry(1000) }, new CombatantSetup[] { Character(), Assailant(intervalMs: 9000) }));
        JsonElement started = Assert.Single(events.OfKind("parryStarted"));
        JsonElement ended = Assert.Single(events.OfKind("parryEnded"));
        Assert.Equal(Eid(started), Cause(ended));                       // fenêtre fermée en phase 1 : héritée de la commande
        Assert.Equal("command:0", graph[Eid(ended)].Root);
        JsonElement attack = Assert.Single(events.OfKind("attackStarted"));
        JsonElement parried = Assert.Single(events.OfKind("parrySucceeded"));
        Assert.Equal((Eid(attack), Eid(attack)), (Cause(parried)!.Value, Cause(events.OfKind("damage").Single())!.Value));
        Assert.All(events.OfKind("resourceGained"), g => Assert.Equal(Eid(parried), Cause(g)));
    }

    [Fact]
    public void Garde_entretien_racine_periodique_et_rupture()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 1000 };
        var (events, graph) = Simulate(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { Character(), Assailant(firstAttackMs: 20_000) },
            durationMs: 10_000, rules: rules));
        Assert.Equal(new[] { "periodic:bastion:bastion:0", "periodic:bastion:bastion:1" }, events.OfKind("guardUpkeep").Select(Root));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal(("periodic:bastion:bastion:2", (long?)null), (Root(broken), Cause(broken)));
        JsonElement breakState = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal(Eid(broken), Cause(breakState));
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal(Eid(breakState), Cause(expired));                // A4 : l'expiration hérite de l'application
        Assert.Equal("periodic:bastion:bastion:2", graph[Eid(expired)].Root);
    }

    [Fact]
    public void Blocage_gain_cause_par_le_blocage()
    {
        var (events, _) = Simulate(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { Character(), Assailant(intervalMs: 9000) }, rules: NoUpkeep));
        JsonElement blocked = Assert.Single(events.OfKind("blocked"));
        Assert.Equal(Eid(Assert.Single(events.OfKind("attackStarted"))), Cause(blocked));
        Assert.Equal(Eid(blocked), Cause(Assert.Single(events.OfKind("resourceGained"))));
    }

    // --- Altérations : provenance mémorisée sur l'instance --------------------------------------------

    [Fact]
    public void Renouvellement_remplace_la_provenance_sans_nouvelle_racine()
    {
        // Gels de « a » et « b » au tick 20 (« b » refusé, notLonger), renouvelé par « c » au tick 40 : l'expiration (tick 118)
        // a pour cause l'application de « c » (A2) ; le refus ne change rien et n'est pas un effet.
        var freeze = Strike("gele", 100, applies: Apply("freeze"));
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Tank(), Assailant(intervalMs: 60_000, id: "a", attacks: freeze), Assailant(intervalMs: 60_000, id: "b", attacks: freeze),
            Assailant(2000, 60_000, id: "c", attacks: freeze),
        }, durationMs: 8000, rules: NoUpkeep));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(2, applied.Count);
        JsonElement renewal = applied[1];
        Assert.True(renewal.GetProperty("renewed").GetBoolean());
        Assert.NotNull(Cause(renewal));                                   // pas une racine
        Assert.Equal("assailantTurn:c:0", graph[Eid(renewal)].Root);
        Assert.Equal("assailantTurn:a:0", graph[Eid(applied[0])].Root);
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal(Eid(renewal), Cause(expired));
        Assert.False(Assert.Single(events.OfKind("stateBlocked")).TryGetProperty("eid", out _));
    }

    [Fact]
    public void Renouvellement_de_dot_ticks_suivants_herites_de_la_nouvelle_application()
    {
        // Brûlure de « a » au tick 20 renouvelée au tick 60 (avant le tick périodique) par la même source : le tick 60 et les
        // suivants ont pour cause l'application du tick 60.
        var burn = Strike("brule", 100, DamageType.Magique, applies: Apply("burn", "0.4"));
        var again = Strike("encore", 100, DamageType.Magique, applies: Apply("burn", "0.4"));
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
            { new DummySetup("mannequin", "joueurs", Fixed.Zero, Fixed.Zero),
                Assailant(1000, 2000, "a", "cibles", "mannequin", new[] { burn, again }.Concat(Enumerable.Range(0, 10).Select(i => Blow(id: $"pause_{i}"))).ToArray()) },
            durationMs: 20_000));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(2, applied.Count);
        var dots = events.OfKind("dotDamage").ToList();
        Assert.All(dots, d => Assert.Equal(d.Tick() < 60 ? Eid(applied[0]) : Eid(applied[1]), Cause(d)));
        Assert.Contains(dots, d => d.Tick() == 60);
        Assert.Equal(Eid(applied[1]), Cause(Assert.Single(events.OfKind("stateExpired"))));
    }

    [Fact]
    public void Mort_par_dot_heritee_du_tick_mortel()
    {
        var caster = Assailant(1000, 60_000, "assaillant", "cibles", "cible", Strike("entaille", 100, applies: Apply("bleed", "100")));
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character("cible"), Tank(), caster }, durationMs: 10_000));
        JsonElement dot = Assert.Single(events.OfKind("dotDamage"));
        JsonElement died = Assert.Single(events.OfKind("died"));
        JsonElement removed = Assert.Single(events.OfKind("stateRemoved"));
        Assert.Equal((Eid(dot), Eid(died)), (Cause(died)!.Value, Cause(removed)!.Value));
        Assert.Equal(Eid(events.OfKind("stateApplied").Single()), Cause(dot));
        Assert.Equal("assailantTurn:assaillant:0", graph[Eid(removed)].Root);
    }

    [Fact]
    public void Mort_par_impact_heritee_du_premier_impact_mortel()
    {
        // Deux coups au même tick sur « faible » : le premier l'amène à 0, la mort en hérite ; le second ne change rien.
        CombatantSetup[] combatants =
        {
            Character("fort", "rouge", initiative: 5, attributes: Attrs(force: 500)), Character("second", "rouge", attributes: Attrs(force: 500)),
            Character("faible", "bleu", attributes: Attrs(vitalite: 0)),
        };
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe, "fort", "faible"), Cmd(0, Frappe, "second", "faible") }, combatants));
        var hits = events.OfKind("damage").ToList();
        Assert.Equal(2, hits.Count);
        JsonElement died = Assert.Single(events.OfKind("died"));
        Assert.Equal(Eid(hits[0]), Cause(died));
        Assert.Equal("command:0", graph[Eid(died)].Root);
    }

    [Fact]
    public void Mort_sans_effet_mortel_racine_propre()
    {
        // Points de vie maximaux nuls (règles sans PV de base, vitalité 0) : mort constatée au tick 0 sans effet mortel.
        var stats = new StatRules { BaseMaxHealth = Fixed.Zero };
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { Character(attributes: Attrs(vitalite: 0)), Dummy() }, Array.Empty<CombatCommand>(), Identity);
        var (events, _) = Simulate(setup);
        JsonElement died = Assert.Single(events.OfKind("died"));
        Assert.Equal(("endOfTick:bastion:0", (long?)null), (Root(died), Cause(died)));
    }
}
