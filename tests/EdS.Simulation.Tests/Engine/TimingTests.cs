using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Frontières temporelles : recharges, temps global de recharge, régénération (ticks de 50 ms).</summary>
[Trait("Category", "Engine")]
public class TimingTests
{
    private static List<System.Text.Json.JsonElement> Events(params CombatCommand[] commands) =>
        TestCombat.Events(Run(Setup(commands)).Log);

    [Fact]
    public void Recharge_refusee_un_tick_avant_son_terme()
    {
        var events = Events(Cmd(0, Frappe), Cmd(5950, Frappe));
        var rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal(119, rejected.Tick());
        Assert.Equal("cooldown", rejected.Str("reason"));
    }

    [Fact]
    public void Recharge_qui_expire_au_tick_courant_permet_la_commande()
    {
        var events = Events(Cmd(0, Frappe), Cmd(6000, Frappe));
        Assert.Empty(events.OfKind("commandRejected"));
        var casts = events.OfKind("skillCast").ToList();
        Assert.Equal(new long[] { 0, 120 }, casts.Select(c => c.Tick()));
        Assert.Equal(120, casts[0].Int("cooldownReadyTick"));

        // La levée de la recharge (phase « expirations ») précède la commande du même tick.
        var ready = Assert.Single(events.OfKind("cooldownReady"));
        Assert.Equal(120, ready.Tick());
        Assert.True(ready.Int("seq") < casts[1].Int("seq"));
        Assert.Equal("expirations", ready.Str("phase"));
        Assert.Equal("commands", casts[1].Str("phase"));
    }

    [Fact]
    public void Temps_global_de_recharge_a_la_frontiere()
    {
        var early = Events(Cmd(0, Frappe), Cmd(950, Heurt));
        Assert.Equal("globalCooldown", Assert.Single(early.OfKind("commandRejected")).Str("reason"));
        var exact = Events(Cmd(0, Frappe), Cmd(1000, Heurt));
        Assert.Empty(exact.OfKind("commandRejected"));
    }

    [Fact]
    public void Temps_global_de_recharge_configurable()
    {
        var rules = new CombatRules { GlobalCooldownMs = 0 };
        var events = TestCombat.Events(Run(Setup(new[] { Cmd(0, Frappe), Cmd(0, Heurt) }, rules: rules)).Log);
        Assert.Equal(2, events.OfKind("skillCast").Count());
        Assert.Empty(events.OfKind("commandRejected"));
    }

    [Fact]
    public void Regeneration_apres_une_seconde_complete_puis_dix_par_seconde()
    {
        // Dépense au tick 0 : rien jusqu'au tick 20 inclus, puis +1 tous les deux ticks (+10 par seconde).
        var regen = Events(Cmd(0, Frappe)).OfKind("enduranceRegenerated").ToList();
        Assert.Equal(21, regen[0].Tick());
        Assert.Equal(10, regen.Count);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => 21L + 2 * i), regen.Select(e => e.Tick()));
        Assert.All(regen, e => Assert.Equal(1, e.Int("amount")));
        Assert.Equal(100, regen[^1].Int("endurance"));   // 90 après la dépense, plafond 100
    }

    [Fact]
    public void Regeneration_plafonnee_au_maximum()
    {
        var result = Run(Setup(new[] { Cmd(0, Frappe) }, durationMs: 10_000)).Result;
        Assert.Equal(100, result["bastion"].Endurance);   // endurance max = 100 + 2 × 0
    }

    [Fact]
    public void Chaque_depense_relance_le_delai()
    {
        // Dépenses aux ticks 0 et 20 : la régénération ne démarre qu'au tick 41.
        var regen = Events(Cmd(0, Frappe), Cmd(1000, Heurt)).OfKind("enduranceRegenerated").ToList();
        Assert.Equal(41, regen[0].Tick());
        Assert.Equal(75 + 1, regen[0].Int("endurance"));
    }

    [Fact]
    public void Competence_sans_cout_ne_relance_pas_le_delai()
    {
        var regen = Events(Cmd(0, Frappe), Cmd(1000, Gratuite)).OfKind("enduranceRegenerated").ToList();
        Assert.Equal(21, regen[0].Tick());
    }
}
