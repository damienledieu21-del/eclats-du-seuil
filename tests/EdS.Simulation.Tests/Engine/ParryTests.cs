using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Parade parfaite (A2) : fenêtre semi-ouverte de 3 ticks, récompense unique, récupération, priorité sur la garde.</summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
public class ParryTests
{
    private static (CombatResult Result, List<JsonElement> Events) Fight(IEnumerable<CombatCommand> commands, params CombatantSetup[] others)
    {
        var fighters = new List<CombatantSetup> { Tank() };
        fighters.AddRange(others.Length == 0 ? new CombatantSetup[] { Assailant() } : others);
        var (result, log) = Run(Setup(commands, fighters, durationMs: 4000, rules: NoUpkeep));
        return (result, Events(log));
    }

    [Theory]
    [InlineData(-1, false)]   // impact un tick avant la commande
    [InlineData(0, true)]     // même tick : la commande (phase 2) précède la résolution (phase 3)
    [InlineData(1, true)]
    [InlineData(2, true)]     // dernier tick de [P, P + 3[
    [InlineData(3, false)]    // P + 3 : la fenêtre est fermée (phase 1)
    public void Fenetre_exacte_de_trois_ticks(int impactOffsetTicks, bool parried)
    {
        // Impact au tick 20 (1000 ms) ; parade au tick 20 − décalage.
        long parryMs = (20 - impactOffsetTicks) * 50L;
        var (result, events) = Fight(new[] { Parry(parryMs) }, Assailant(intervalMs: 9000));
        var hit = Assert.Single(events.OfKind("damage"));
        Assert.Equal(parried ? "parry" : "none", hit.Str("defense"));
        Assert.Equal(parried ? 10 : 100, hit.Int("amount"));
        Assert.Equal(parried ? 1 : 0, result["bastion"].RampartCharges);
        Assert.Equal(parried ? 8 : 0, result["bastion"].Resonance);

        var started = Assert.Single(events.OfKind("parryStarted"));
        Assert.Equal(started.Tick() + 3, started.Int("windowEndTick"));
        var ended = Assert.Single(events.OfKind("parryEnded"));
        Assert.Equal(started.Tick() + 3, ended.Tick());
        Assert.Equal(parried ? "success" : "failed", ended.Str("outcome"));
    }

    [Fact]
    public void Parade_reussie_journalisee_et_recompensee_une_fois()
    {
        var (result, events) = Fight(new[] { Parry(1000) }, Assailant(intervalMs: 9000));
        var kinds = events.Where(e => e.Tick() == 20 && e.Str("phase") == "resolution").Select(e => e.Str("event") + ":" + (e.TryGetProperty("resource", out var r) ? r.GetString() : ""));
        Assert.Equal(new[] { "parrySucceeded:", "damage:", "resourceGained:rampartCharges", "resourceGained:resonance" }, kinds);
        Assert.Equal(1, result["bastion"].PerfectParries);
        Assert.Equal(0, result["bastion"].FailedParries);
        Assert.Equal(JsonValueKind.Null, Assert.Single(events.OfKind("parryEnded")).GetProperty("recoveryUntilTick").ValueKind);
    }

    [Fact]
    public void Parade_ratee_sans_charge_puis_recuperation_de_500_ms()
    {
        // Parade au tick 0, fenêtre [0, 3[ vide ; récupération [3, 13[ : refus au tick 12, acceptation au tick 13.
        var (result, events) = Fight(new[] { Parry(0), Parry(600), Parry(650) }, Assailant(firstAttackMs: 9000));
        var failed = events.OfKind("parryEnded").First();
        Assert.Equal(("failed", 3L, 13L), (failed.Str("outcome")!, failed.Tick(), failed.Int("recoveryUntilTick")));
        var rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal(("parryRecovery", 12L, "parry"), (rejected.Str("reason")!, rejected.Tick(), rejected.Str("action")!));
        Assert.Equal(new long[] { 0, 13 }, events.OfKind("parryStarted").Select(e => e.Tick()));
        Assert.Equal(0, result["bastion"].RampartCharges);
        Assert.Equal(2, result["bastion"].FailedParries);
        Assert.Empty(events.OfKind("resourceGained"));
    }

    [Fact]
    public void Nouvelle_parade_refusee_pendant_la_fenetre()
    {
        var (_, events) = Fight(new[] { Parry(0), Parry(100) }, Assailant(firstAttackMs: 9000));
        Assert.Equal(("parryActive", 2L), (Assert.Single(events.OfKind("commandRejected")).Str("reason")!, events.OfKind("commandRejected").Single().Tick()));
    }

    [Fact]
    public void Deux_impacts_dans_la_fenetre_reduits_mais_une_seule_recompense()
    {
        var (result, events) = Fight(new[] { Parry(1000) }, Assailant(intervalMs: 9000), Assailant(firstAttackMs: 1050, intervalMs: 9000, id: "second"));
        Assert.All(events.OfKind("damage"), e => Assert.Equal((10L, "parry"), (e.Int("amount"), e.Str("defense")!)));
        Assert.Equal(2, events.OfKind("damage").Count());
        Assert.Single(events.OfKind("parrySucceeded"));
        Assert.Equal(1, result["bastion"].RampartCharges);
        Assert.Equal(8, result["bastion"].Resonance);
    }

    [Fact]
    public void Parade_pendant_la_garde_remplace_la_garde_sans_double_protection()
    {
        // Garde active ; impact au tick 20 dans la fenêtre : parade seule (−90 %), ni coût de blocage ni +2.
        // Impact au tick 60, hors fenêtre : garde (−60 %), coût de 10. La garde reste active après la parade.
        var (result, events) = Fight(new[] { GuardStart(0), Parry(1000) }, Assailant(intervalMs: 2000));
        var hits = events.OfKind("damage").ToList();
        Assert.Equal(new[] { (20L, "parry", 10L), (60L, "guard", 40L) }, hits.Select(h => (h.Tick(), h.Str("defense")!, h.Int("amount"))));
        Assert.True(Assert.Single(events.OfKind("parryStarted")).GetProperty("guarding").GetBoolean());
        Assert.Single(events.OfKind("blocked"));
        Assert.Equal(90, result["bastion"].Endurance);
        Assert.Equal(8 + 2, result["bastion"].Resonance);
        Assert.Empty(events.OfKind("guardStopped"));
    }

    [Fact]
    public void Attaque_imparable_ignore_la_parade()
    {
        var (result, events) = Fight(new[] { Parry(1000) }, Assailant(intervalMs: 9000, attacks: Blow(unblockable: true)));
        var hit = Assert.Single(events.OfKind("damage"));
        Assert.Equal((100L, "unblockable"), (hit.Int("amount"), hit.Str("defense")!));
        Assert.Equal("failed", Assert.Single(events.OfKind("parryEnded")).Str("outcome"));
        Assert.Equal(0, result["bastion"].RampartCharges);
    }

    [Fact]
    public void Rupture_par_entretien_interrompt_la_fenetre_de_parade()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 1000 };
        var commands = new[] { GuardStart(0), Parry(50) };
        var events = Events(Run(Setup(commands, new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 9000) }, durationMs: 1000, rules: rules)).Log);
        var ended = Assert.Single(events.OfKind("parryEnded"));
        Assert.Equal(("interrupted", 2L), (ended.Str("outcome")!, ended.Tick()));
        Assert.True(events.OfKind("guardBroken").Single().Int("seq") < ended.Int("seq"));
    }

    [Fact]
    public void Parade_contre_zone_une_seule_recompense()
    {
        var aoe = Assailant(target: null, intervalMs: 9000, attacks: Blow(targeting: SkillTargeting.AllEnemies));
        var fighters = new CombatantSetup[] { Tank("alpha"), Tank("beta"), aoe };
        var (result, log) = Run(Setup(new[] { Parry(1000, "alpha") }, fighters, durationMs: 2000, rules: NoUpkeep));
        var events = Events(log);
        Assert.Equal(new[] { ("alpha", "parry"), ("beta", "none") }, events.OfKind("damage").Select(e => (e.Str("target")!, e.Str("defense")!)));
        Assert.Equal(1, result["alpha"].RampartCharges);
        Assert.Equal(0, result["beta"].RampartCharges);
    }
}
