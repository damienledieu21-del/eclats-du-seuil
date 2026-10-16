using EdS.Domain.Stats;
using EdS.Simulation.Combat;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Tests.Combat;

[Trait("Category", "Formula")]
public class CriticalRollTests
{
    private static DerivedStats WithCritBonus(int bonus) =>
        DerivedStats.Calculate(StatRules.Provisional, Attributes.None,
            new[] { new StatContributions { CriticalChanceBasisPoints = bonus } });

    [Theory]
    [InlineData(0, 500)]          // 5 % de base
    [InlineData(10_000, 6000)]    // plafond 60 %
    public void Frequence_conforme_a_la_chance_bornee(int bonus, int expectedBp)
    {
        var rng = new Pcg32(11);
        var attacker = WithCritBonus(bonus);
        int hits = 0;
        const int draws = 200_000;
        for (int i = 0; i < draws; i++)
            if (CriticalRoll.Roll(rng, attacker)) hits++;
        Assert.InRange(hits * 10_000.0 / draws, expectedBp - 30, expectedBp + 30);
    }

    [Fact]
    public void Chance_nulle_jamais_critique_et_un_tirage_consomme()
    {
        var a = new Pcg32(5);
        var b = new Pcg32(5);
        var attacker = WithCritBonus(-10_000);
        for (int i = 0; i < 100; i++)
        {
            Assert.False(CriticalRoll.Roll(a, attacker));
            b.NextUInt();
        }
        Assert.Equal(b.NextUInt(), a.NextUInt());
    }

    [Fact]
    public void Meme_graine_memes_critiques()
    {
        var attacker = WithCritBonus(2000);
        var a = new Pcg32(2026);
        var b = new Pcg32(2026);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(CriticalRoll.Roll(a, attacker), CriticalRoll.Roll(b, attacker));
    }
}
