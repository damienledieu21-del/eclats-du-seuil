using EdS.Domain.Time;

namespace EdS.Domain.Tests.Time;

[Trait("Category", "Time")]
public class TickRateTests
{
    [Fact]
    public void Tick_initial_de_50_ms()
    {
        Assert.Equal(50, TickRate.Default.MillisecondsPerTick);
        Assert.Equal(20, TickRate.Default.TicksPerSecond);
    }

    [Theory]
    // Durées du cahier des charges et de la validation : DOT 6 s / 2 s, parade parfaite 150 ms.
    [InlineData(50, 6000, 120)]
    [InlineData(50, 2000, 40)]
    [InlineData(50, 150, 3)]
    [InlineData(20, 6000, 300)]
    [InlineData(20, 2000, 100)]
    [InlineData(20, 0, 0)]
    public void Conversion_exacte(int msPerTick, long ms, long ticks)
    {
        var rate = new TickRate(msPerTick);
        Assert.Equal(ticks, rate.ToTicks(ms));
        Assert.Equal(ms, rate.ToMilliseconds(ticks));
    }

    [Fact]
    public void Fenetre_de_parade_non_representable_a_20_ms()
    {
        // 150 ms tombe juste à 50 ms (3 ticks) mais pas à 20 ms : refus explicite, pas d'arrondi.
        var rate = new TickRate(20);
        Assert.False(rate.TryToTicks(150, out _));
        Assert.Throws<ArgumentException>(() => rate.ToTicks(150));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(30)]   // ne divise pas 1000 ms
    [InlineData(1001)]
    public void Duree_de_tick_invalide(int msPerTick) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickRate(msPerTick));

    [Fact]
    public void Durees_negatives_refusees()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickRate.Default.ToTicks(-50));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickRate.Default.ToMilliseconds(-1));
    }

    [Fact]
    public void Egalite_par_valeur() => Assert.Equal(new TickRate(50), TickRate.Default);
}
