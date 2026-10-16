using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Correctif 6.1, M02 : chaque état temporaire a une identité d'instance et une génération. Une échéance périmée ne
/// déclenche jamais de transition ; un état non cumulable expire exactement une fois. Avant, deux applications au même
/// tick programmaient deux échéances identiques et l'état « expirait » deux fois.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
[Trait("Category", "Regression")]
public class M02StateInstanceTests
{
    private static List<JsonElement> Fight(IEnumerable<CombatCommand> commands, long durationMs, CombatRules? rules = null, params CombatantSetup[] others) =>
        Events(Run(Setup(commands, new CombatantSetup[] { Tank() }.Concat(others), durationMs: durationMs, rules: rules ?? NoUpkeep)).Log);

    private static AssailantSetup Fele(string id, long firstAttackMs = 1000, long intervalMs = 9000) =>
        Assailant(firstAttackMs: firstAttackMs, intervalMs: intervalMs, id: id, attacks: Blow(fragility: true, id: "fele"));

    private static (long Tick, string State, long Instance, long Generation) Key(JsonElement e) =>
        (e.Tick(), e.Str("state")!, e.Int("instance"), e.Int("generation"));

    [Fact]
    public void Deux_applications_simultanees_une_seule_expiration()
    {
        var events = Fight(Array.Empty<CombatCommand>(), 8000, null, Fele("a_fele"), Fele("b_fele"));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { (20L, "fragility", 1L, 1L), (20L, "fragility", 1L, 2L) }, applied.Select(Key));
        Assert.Equal(new[] { false, true }, applied.Select(a => a.GetProperty("renewed").GetBoolean()));
        var expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal((140L, "fragility", 1L, 2L), Key(expired));
    }

    [Fact]
    public void Renouvellements_successifs_une_seule_expiration_de_la_derniere_generation()
    {
        // Applications aux ticks 20, 60 et 100 : générations 1, 2, 3 de la même instance ; échéance finale 220.
        // Impacts simples aux ticks 140 et 180, puis nouvelle application au tick 220, après l'expiration : instance 2.
        var assailant = new AssailantSetup("fele", "cibles", default, default, "bastion", 1000, 2000,
            new[] { Blow(fragility: true, id: "f1"), Blow(fragility: true, id: "f2"), Blow(fragility: true, id: "f3"), Blow(id: "s1"), Blow(id: "s2") });
        var events = Fight(Array.Empty<CombatCommand>(), 12_000, null, assailant);
        Assert.Equal(new[] { (20L, "fragility", 1L, 1L), (60L, "fragility", 1L, 2L), (100L, "fragility", 1L, 3L), (220L, "fragility", 2L, 1L) },
            events.OfKind("stateApplied").Select(Key));
        Assert.Equal(new[] { (220L, "fragility", 1L, 3L) }, events.OfKind("stateExpired").Select(Key));
    }

    [Fact]
    public void Expiration_et_reapplication_au_meme_tick()
    {
        // Application au tick 20 (échéance 140) ; nouvelle application au tick 140 : l'expiration (phase 1) passe d'abord,
        // puis la réapplication (phase 3) crée une nouvelle instance qui n'est pas « renouvelée » et expire à 260.
        var events = Fight(Array.Empty<CombatCommand>(), 14_000, null, Fele("fele", intervalMs: 6000));
        Assert.Equal(new[] { (20L, "fragility", 1L, 1L), (140L, "fragility", 2L, 1L), (260L, "fragility", 3L, 1L) },
            events.OfKind("stateApplied").Select(Key));
        Assert.All(events.OfKind("stateApplied"), a => Assert.False(a.GetProperty("renewed").GetBoolean()));
        var expired = events.OfKind("stateExpired").ToList();
        Assert.Equal(new[] { (140L, "fragility", 1L, 1L), (260L, "fragility", 2L, 1L) }, expired.Select(Key));
        Assert.True(expired[0].Int("seq") < events.OfKind("stateApplied").ElementAt(1).Int("seq"));
        // L'impact du tick 140 n'est pas fragilisé : l'état a expiré avant la résolution.
        Assert.False(events.OfKind("damage").Single(d => d.Tick() == 140).GetProperty("fragile").GetBoolean());
    }

    [Fact]
    public void Double_application_puis_reapplication_a_l_echeance()
    {
        // Le cas le plus piégeux : deux applications au tick 20 (deux échéances à 140, générations 1 et 2), puis une
        // réapplication au tick 140. Une seule expiration à 140 (génération 2), la génération 1 périmée reste sans effet.
        var events = Fight(Array.Empty<CombatCommand>(), 9000, null, Fele("a_fele", intervalMs: 6000), Fele("b_fele"));
        Assert.Equal(new[] { (140L, "fragility", 1L, 2L) }, events.OfKind("stateExpired").Select(Key));
        Assert.Equal((140L, "fragility", 2L, 1L), Key(events.OfKind("stateApplied").Last()));
    }

    [Fact]
    public void Mort_retire_les_etats_et_perime_leurs_echeances()
    {
        // « bastion » (100 PV) garde avec un blocage impayable : rupture, puis l'impact fragilisant le tue au tick 20.
        // Son allié survit : le combat continue et les échéances 80 (rupture) et 140 (Fragilité) arrivent, périmées.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 1000 };
        var fragile = Character(attributes: Attrs(vitalite: 0));
        var ally = Character(id: "allie", attributes: Attrs(vitalite: 200));
        var log = Run(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { fragile, ally, Fele("fele") }, durationMs: 10_000, rules: rules)).Log;
        var events = Events(log);
        Assert.Equal(20L, Assert.Single(events.OfKind("died")).Tick());
        var removed = events.OfKind("stateRemoved").ToList();
        Assert.Equal(new[] { (20L, "fragility", 2L, 1L), (20L, "guardBreak", 1L, 1L) }, removed.Select(Key));
        Assert.All(removed, r => Assert.Equal("death", r.Str("reason")));
        Assert.Empty(events.OfKind("stateExpired"));
        Assert.Equal(CombatEndReason.DurationReached, CombatEngine.Run(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { fragile, ally, Fele("fele") },
            durationMs: 10_000, rules: rules), new MemoryCombatLog()).EndReason);
    }

    [Fact]
    public void Rupture_de_garde_expire_une_fois_et_rend_la_garde_possible()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 1000 };
        var events = Fight(new[] { GuardStart(0), GuardStart(3950), GuardStart(4000) }, 5000, rules, Assailant(intervalMs: 9000));
        Assert.Equal(new[] { (80L, "guardBreak", 1L, 1L) }, events.OfKind("stateExpired").Select(Key));
        Assert.Equal("guardBroken", events.OfKind("commandRejected").Single().Str("reason"));   // tick 79 : encore brisée
        Assert.Equal(new long[] { 0, 80 }, events.OfKind("guardStarted").Select(e => e.Tick()));
    }

    [Fact]
    public void Propriete_chaque_instance_expire_au_plus_une_fois_et_jamais_perimee()
    {
        var random = new Random(602);
        for (int run = 0; run < 100; run++)
        {
            var assailants = Enumerable.Range(0, random.Next(1, 4)).Select(i => new AssailantSetup($"a{i}", "cibles", default, default, "bastion",
                random.Next(0, 60) * 50L, random.Next(1, 160) * 50L,
                Enumerable.Range(0, random.Next(1, 4)).Select(k => Blow(fragility: random.Next(3) > 0, id: $"c{k}")).ToArray())).ToArray();
            var events = Fight(Array.Empty<CombatCommand>(), 20_000, null, assailants);
            var applied = events.OfKind("stateApplied").ToList();
            var expired = events.OfKind("stateExpired").ToList();
            Assert.Equal(expired.Count, expired.Select(e => e.Int("instance")).Distinct().Count());   // au plus une fois
            foreach (JsonElement e in expired)
            {
                // L'expiration porte la dernière génération appliquée de l'instance, à son échéance exacte.
                JsonElement last = applied.Where(a => a.Int("instance") == e.Int("instance") && a.Int("seq") < e.Int("seq")).Last();
                Assert.Equal((last.Int("generation"), last.Int("untilTick")), (e.Int("generation"), e.Tick()));
            }
            // Fragilité active ⇔ « fragile » dans chaque impact : recalculée indépendamment depuis les applications.
            foreach (JsonElement hit in events.OfKind("damage"))
            {
                bool expected = applied.Any(a => a.Int("seq") < hit.Int("seq") && a.Tick() <= hit.Tick() && hit.Tick() < a.Int("untilTick"));
                Assert.Equal(expected, hit.GetProperty("fragile").GetBoolean());
            }
        }
    }
}
