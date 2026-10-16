using EdS.Domain.Combat;

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
