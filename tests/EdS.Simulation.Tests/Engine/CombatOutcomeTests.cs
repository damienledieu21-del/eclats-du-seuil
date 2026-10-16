using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

[Trait("Category", "Engine")]
public class CombatOutcomeTests
{
    [Fact]
    public void Degats_conformes_a_la_formule()
    {
        // Force 14 → puissance 28 ; × 1,2 = 33,6 ; armure 100 → × 1000/1100 = 30,5455 → 31.
        var events = Events(Run(Setup(new[] { Cmd(0, Frappe) }, seed: 3)).Log);
        var damage = Assert.Single(events.OfKind("damage"));
        var attacker = DerivedStats.Calculate(StatRules.Provisional, Attrs());
        var defender = DerivedStats.Calculate(StatRules.Provisional, Attributes.None, new[] { new StatContributions { Armor = Fixed.FromInt(100) } });
        bool critical = damage.GetProperty("critical").GetBoolean();
        long expected = DamageCalculator.Compute(new DamageInput(attacker, defender, PowerType.Physique, DamageType.Physique, Fixed.Parse("1.2"),
            isCritical: critical), DamageRules.Provisional).FinalDamage;
        Assert.Equal(expected, damage.Int("amount"));
        if (!critical) Assert.Equal(31, damage.Int("amount"));
    }

    [Fact]
    public void Mannequin_aux_pv_infinis_enregistre_les_degats()
    {
        var commands = Enumerable.Range(0, 10).Select(i => Cmd(i * 1000L, Gratuite));
        var (result, log) = Run(Setup(commands));
        var dummy = result["mannequin"];
        Assert.Null(dummy.Health);
        Assert.False(dummy.IsDead);
        long sum = Events(log).OfKind("damage").Sum(e => e.Int("amount"));
        Assert.Equal(sum, dummy.DamageTaken);
        Assert.Equal(sum, result["bastion"].DamageDealt);
        Assert.Equal(10, result["bastion"].Hits);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.Equal(10_000, result.DurationMs);
    }

    [Fact]
    public void Mort_en_fin_de_tick_et_victoire()
    {
        CombatantSetup[] combatants = { Character("fort", "rouge", attributes: Attrs(force: 500)), Character("faible", "bleu", attributes: Attrs(vitalite: 0)) };
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, "fort", "faible") }, combatants));
        var events = Events(log);
        var damage = Assert.Single(events.OfKind("damage"));
        Assert.Equal(0, damage.Int("health"));
        Assert.True(damage.Int("overkill") > 0);
        var died = Assert.Single(events.OfKind("died"));
        Assert.Equal("endOfTick", died.Str("phase"));
        Assert.Equal(CombatEndReason.TeamDefeated, result.EndReason);
        Assert.Equal("rouge", result.Winner);
        Assert.Equal(0, result.EndTick);
        Assert.True(result["faible"].IsDead);
    }

    [Fact]
    public void Morts_simultanees_sans_vainqueur()
    {
        var killer = Attrs(force: 500, vitalite: 0);
        CombatantSetup[] combatants = { Character("a", "rouge", attributes: killer), Character("b", "bleu", attributes: killer) };
        var result = Run(Setup(new[] { Cmd(0, Frappe, "a", "b"), Cmd(0, Frappe, "b", "a") }, combatants)).Result;
        Assert.Equal(CombatEndReason.TeamDefeated, result.EndReason);
        Assert.Null(result.Winner);
        Assert.True(result["a"].IsDead && result["b"].IsDead);
    }

    [Fact]
    public void Ultime_lancee_depuis_son_emplacement()
    {
        var events = Events(Run(Setup(new[] { Cmd(0, Ultime) })).Log);
        Assert.Equal(Ultime, Assert.Single(events.OfKind("skillCast")).Str("skill"));
    }

    [Fact]
    public void Fin_du_journal_resume_puis_fin_de_combat()
    {
        var events = Events(Run(Setup(new[] { Cmd(0, Frappe) })).Log);
        Assert.Equal("combatEnded", events[^1].Str("event"));
        Assert.Equal("durationReached", events[^1].Str("reason"));
        Assert.Equal(200, events[^1].Int("ticks"));
        Assert.Equal(new[] { "bastion", "mannequin" }, events.OfKind("combatantSummary").Select(e => e.Str("combatant")));
    }
}
