using EdS.Simulation.Randomness;
using FsCheck.Xunit;

namespace EdS.Simulation.Tests.Randomness;

[Trait("Category", "Numeric")]
public class Pcg32Tests
{
    [Fact]
    public void Vecteurs_de_reference_pcg32_demo()
    {
        // Sortie publiée du programme pcg32-demo de pcg-c-basic : pcg32_srandom_r(42, 54).
        uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
        var rng = new Pcg32(42, 54);
        foreach (uint value in expected)
            Assert.Equal(value, rng.NextUInt());
    }

    [Property(MaxTest = 500)]
    public bool Meme_graine_meme_suite(ulong seed, ulong stream)
    {
        var a = new Pcg32(seed, stream);
        var b = new Pcg32(seed, stream);
        for (int i = 0; i < 64; i++)
            if (a.NextUInt() != b.NextUInt()) return false;
        return true;
    }

    [Fact]
    public void Flux_differents_suites_differentes()
    {
        var a = new Pcg32(42, 1);
        var b = new Pcg32(42, 2);
        int same = 0;
        for (int i = 0; i < 1000; i++)
            if (a.NextUInt() == b.NextUInt()) same++;
        Assert.True(same < 5, $"{same} tirages identiques sur 1000");
    }

    [Property(MaxTest = 500)]
    public bool Capture_et_restauration_reprennent_la_suite(ulong seed, byte skip)
    {
        var rng = new Pcg32(seed);
        for (int i = 0; i < skip; i++) rng.NextUInt();
        var restored = Pcg32.Restore(rng.Capture());
        for (int i = 0; i < 32; i++)
            if (rng.NextUInt() != restored.NextUInt()) return false;
        return true;
    }

    [Fact]
    public void Restauration_refuse_un_increment_pair() =>
        Assert.Throws<ArgumentException>(() => Pcg32.Restore(new Pcg32State(1, 2)));

    [Property(MaxTest = 1000)]
    public bool Tirage_borne_dans_l_intervalle(ulong seed, uint bound)
    {
        if (bound == 0) return true;
        var rng = new Pcg32(seed);
        for (int i = 0; i < 32; i++)
            if (rng.NextBounded(bound) >= bound) return false;
        return true;
    }

    [Fact]
    public void Borne_nulle_refusee() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32(1).NextBounded(0));

    [Fact]
    public void Tirage_borne_sans_biais_visible()
    {
        // 6 classes, 600 000 tirages : chaque classe doit rester à ±1 % de 100 000.
        var rng = new Pcg32(2026);
        var counts = new int[6];
        for (int i = 0; i < 600_000; i++) counts[rng.NextBounded(6)]++;
        Assert.All(counts, c => Assert.InRange(c, 99_000, 101_000));
    }

    [Theory]
    [InlineData(500)]    // critique de base provisoire : 5 %
    [InlineData(6000)]   // plafond provisoire : 60 %
    public void Frequence_des_reussites_proche_de_la_chance(int chance)
    {
        var rng = new Pcg32(7);
        const int draws = 200_000;
        int hits = 0;
        for (int i = 0; i < draws; i++)
            if (rng.RollBasisPoints(chance)) hits++;
        double observed = hits * 10_000.0 / draws;
        Assert.InRange(observed, chance - 30, chance + 30);
    }

    [Fact]
    public void Chances_extremes_et_consommation_constante()
    {
        var a = new Pcg32(99);
        var b = new Pcg32(99);
        for (int i = 0; i < 1000; i++)
        {
            Assert.False(a.RollBasisPoints(0));
            Assert.True(b.RollBasisPoints(10_000));
        }
        // 0 % et 100 % consomment autant de tirages : les deux générateurs restent synchrones.
        Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Chance_hors_bornes_refusee(int chance) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32(1).RollBasisPoints(chance));
}
