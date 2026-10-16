using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Domain.Tests.Combat;

[Trait("Category", "Engine")]
public class CombatRulesTests
{
    [Fact]
    public void Valeurs_provisoires_coherentes()
    {
        var rules = CombatRules.Provisional;
        Assert.Empty(rules.Validate());
        Assert.Equal(50, rules.TickMilliseconds);
        Assert.Equal(1000, rules.GlobalCooldownMs);
        Assert.Equal(10, rules.EnduranceRegenPerSecond);
        Assert.Equal(1000, rules.EnduranceRegenDelayMs);
        Assert.Equal(20, rules.Tick.TicksPerSecond);
    }

    [Fact]
    public void Valeurs_provisoires_du_lot_6()
    {
        var r = CombatRules.Provisional;
        Assert.Equal((Fixed.Parse("0.6"), 2, 10, 3000L), (r.GuardDamageReduction, r.GuardUpkeepPerSecond, r.GuardCostPerBlock, r.GuardBreakDurationMs));
        Assert.Equal((150L, Fixed.Parse("0.9"), 500L, 1), (r.ParryWindowMs, r.ParryDamageReduction, r.ParryFailureRecoveryMs, r.RampartChargesPerParry));
        Assert.Equal(5, r.RampartChargesMax);
        Assert.Equal((100, 3, 8, 2, 100), (r.ResonanceMax, r.ResonancePerDirectHit, r.ResonancePerPerfectParry, r.ResonancePerBlock, r.UltimateResonanceCost));
        Assert.Equal((6000L, Fixed.Parse("0.1")), (r.FragilityDurationMs, r.FragilityDamageTakenIncrease));
        Assert.Equal(3, r.ParryWindowMs / r.TickMilliseconds);   // A2 : trois ticks à 50 ms
    }

    public static TheoryData<string, CombatRules> DefensiveRulesInvalid => new()
    {
        { nameof(CombatRules.GuardDamageReduction), new CombatRules { GuardDamageReduction = Fixed.Parse("1.1") } },
        { nameof(CombatRules.ParryDamageReduction), new CombatRules { ParryDamageReduction = Fixed.Parse("-0.1") } },
        { nameof(CombatRules.FragilityDamageTakenIncrease), new CombatRules { FragilityDamageTakenIncrease = Fixed.Parse("-0.1") } },
        { nameof(CombatRules.GuardUpkeepPerSecond), new CombatRules { GuardUpkeepPerSecond = -1 } },
        { nameof(CombatRules.GuardCostPerBlock), new CombatRules { GuardCostPerBlock = -1 } },
        { nameof(CombatRules.GuardBreakDurationMs), new CombatRules { GuardBreakDurationMs = 0 } },
        { nameof(CombatRules.GuardBreakDurationMs), new CombatRules { GuardBreakDurationMs = 3010 } },
        { nameof(CombatRules.ParryWindowMs), new CombatRules { ParryWindowMs = 0 } },
        { nameof(CombatRules.ParryWindowMs), new CombatRules { ParryWindowMs = 140 } },
        { nameof(CombatRules.ParryFailureRecoveryMs), new CombatRules { ParryFailureRecoveryMs = -50 } },
        { nameof(CombatRules.FragilityDurationMs), new CombatRules { FragilityDurationMs = CombatRules.AbsoluteMaxDurationMs + 50 } },
        { nameof(CombatRules.RampartChargesMax), new CombatRules { RampartChargesMax = -1 } },
        { nameof(CombatRules.ResonancePerBlock), new CombatRules { ResonancePerBlock = -2 } },
        { nameof(CombatRules.UltimateResonanceCost), new CombatRules { UltimateResonanceCost = 101 } },
    };

    [Theory]
    [MemberData(nameof(DefensiveRulesInvalid))]
    public void Regles_defensives_incoherentes_signalees(string field, CombatRules rules) =>
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(field));

    [Fact]
    public void Recuperation_de_parade_nulle_autorisee() => Assert.Empty(new CombatRules { ParryFailureRecoveryMs = 0, GuardUpkeepPerSecond = 0 }.Validate());

    [Theory]
    [InlineData(nameof(CombatRules.TickMilliseconds), 30)]
    [InlineData(nameof(CombatRules.TickMilliseconds), 0)]
    [InlineData(nameof(CombatRules.GlobalCooldownMs), 1025)]
    [InlineData(nameof(CombatRules.GlobalCooldownMs), -50)]
    [InlineData(nameof(CombatRules.EnduranceRegenDelayMs), 10)]
    [InlineData(nameof(CombatRules.EnduranceRegenPerSecond), -1)]
    [InlineData(nameof(CombatRules.MaxDurationMs), 0)]
    [InlineData(nameof(CombatRules.MaxDurationMs), CombatRules.AbsoluteMaxDurationMs + 50)]
    [InlineData(nameof(CombatRules.MaxEventsPerTick), 0)]
    public void Valeur_incoherente_signalee(string field, long value)
    {
        var rules = field switch
        {
            nameof(CombatRules.TickMilliseconds) => new CombatRules { TickMilliseconds = (int)value },
            nameof(CombatRules.GlobalCooldownMs) => new CombatRules { GlobalCooldownMs = value },
            nameof(CombatRules.EnduranceRegenDelayMs) => new CombatRules { EnduranceRegenDelayMs = value },
            nameof(CombatRules.EnduranceRegenPerSecond) => new CombatRules { EnduranceRegenPerSecond = (int)value },
            nameof(CombatRules.MaxDurationMs) => new CombatRules { MaxDurationMs = value },
            _ => new CombatRules { MaxEventsPerTick = (int)value },
        };
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(field));
    }

    [Fact]
    public void Temps_global_de_recharge_nul_autorise() => Assert.Empty(new CombatRules { GlobalCooldownMs = 0 }.Validate());
}
