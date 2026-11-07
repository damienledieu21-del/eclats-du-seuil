using System;
using EdS.Runtime;
using EdS.World;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9.1 (échos fantômes et failles, affichage seulement) : la sorte d'écho de chaque combattant connu des Vallons et du
/// souvenir, et les réglages purs de l'affichage (solidité, dissolution, failles).
/// </summary>
[Trait("Category", "World")]
public sealed class V291EchosTests
{
    [Theory]
    [InlineData("loup_echo1", EchoKind.Night)]
    [InlineData("loup_echo6_v3", EchoKind.Night)]
    [InlineData("loup_echo1_ev4", EchoKind.Rift)]
    [InlineData("LOUP_ECHO1_EV12", EchoKind.Rift)]
    [InlineData("loup_blanc", EchoKind.WhiteWolf)]
    [InlineData("loup_blanc_v2", EchoKind.WhiteWolf)]
    [InlineData("echo_de_la_rupture", EchoKind.Rupture)]
    [InlineData("voix_de_la_rupture", EchoKind.Rupture)]
    [InlineData("loup3_v2", EchoKind.Returned)]
    [InlineData("sanglier_v4", EchoKind.Returned)]
    [InlineData("croc_gris_v2", EchoKind.Returned)]
    [InlineData("croc_gris_enrage_v3", EchoKind.Returned)]
    [InlineData("loup_nuit5_v2", EchoKind.Returned)]
    [InlineData("loup3", EchoKind.None)]
    [InlineData("loup_nuit5_ev2", EchoKind.None)]
    [InlineData("croc_gris", EchoKind.None)]
    [InlineData("bandit_epee3_v2", EchoKind.None)]
    [InlineData("rodeur_v2", EchoKind.None)]
    [InlineData("cerf4_v2", EchoKind.None)]
    [InlineData("lievre1_v3", EchoKind.None)]
    [InlineData("loup_v", EchoKind.None)]
    [InlineData("loup3_vx", EchoKind.None)]
    [InlineData("gardien_du_seuil", EchoKind.None)]
    [InlineData("", EchoKind.None)]
    [InlineData(null, EchoKind.None)]
    public void Sorte_d_echo_d_apres_l_identite(string? id, EchoKind expected)
    {
        Assert.Equal(expected, EchoLooks.Of(id));
        Assert.Equal(expected != EchoKind.None, EchoLooks.IsEcho(id));
    }

    [Fact]
    public void Les_echos_de_la_nuit_restent_ceux_de_la_V21()
    {
        foreach (string id in new[] { "loup_echo1", "loup_echo2_v3", "loup_echo1_ev2" })
            Assert.True(NightLamps.IsEcho(id) && EchoLooks.IsEcho(id), id);
    }

    [Fact]
    public void Solidite_bornee_plus_forte_la_nuit_pres_d_une_cicatrice_et_en_pleine_vie()
    {
        foreach (EchoKind kind in Enum.GetValues(typeof(EchoKind)))
            for (int stage = 0; stage <= 6; stage++)
                for (double h = -0.5; h <= 1.5; h += 0.25)
                    for (double n = -1; n <= 2; n += 0.5)
                    {
                        double s = EchoLooks.Solidity(kind, h, stage, n);
                        Assert.InRange(s, EchoLooks.MinSolidity, 1.0);
                    }
        Assert.True(EchoLooks.Solidity(EchoKind.Night, 1, 0, 1) > EchoLooks.Solidity(EchoKind.Night, 1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Rift, 1, 3, 0) > EchoLooks.Solidity(EchoKind.Rift, 1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Returned, 1, 0, 0) > EchoLooks.Solidity(EchoKind.Returned, 0.1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Rupture, 1, 0, 0) > EchoLooks.Solidity(EchoKind.Returned, 1, 0, 0));
        Assert.True(EchoLooks.Glow(2, 1) > EchoLooks.Glow(0, 0));
    }

    [Fact]
    public void Dissolution_a_l_apparition_puis_a_la_mort()
    {
        Assert.Equal(1.0, EchoLooks.Dissolve(0, null), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(-3, null), 6);
        Assert.Equal(0.0, EchoLooks.Dissolve(EchoLooks.AppearSeconds, null), 6);
        Assert.Equal(0.0, EchoLooks.Dissolve(100, null), 6);
        Assert.InRange(EchoLooks.Dissolve(EchoLooks.AppearSeconds / 2, null), 0.4, 0.6);
        Assert.Equal(0.0, EchoLooks.Dissolve(100, 0), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(100, EchoLooks.VanishSeconds), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(100, 60), 6);
        double last = -1;
        for (double t = 0; t <= EchoLooks.VanishSeconds; t += 0.1)
        {
            double d = EchoLooks.Dissolve(5, t);
            Assert.True(d >= last);
            last = d;
        }
    }

    [Fact]
    public void Failles_et_cicatrices_grandissent_avec_le_palier()
    {
        Assert.Equal(0, EchoLooks.ScarStageAt(null, 0, 0));
        Assert.Equal(0, EchoLooks.ScarStageAt(Array.Empty<ScarView>(), 0, 0));
        var previous = EchoLooks.Rift(0, false);
        Assert.Equal(1.0, previous.Scale, 6);
        for (int stage = 1; stage <= 4; stage++)
        {
            var r = EchoLooks.Rift(stage, false);
            Assert.True(r.Scale > previous.Scale && r.PulseHz > previous.PulseHz && r.PullRate > previous.PullRate && r.Light > previous.Light);
            Assert.Equal(r.PullRate / 2, EchoLooks.Rift(stage, true).PullRate, 6);
            Assert.True(EchoLooks.ScarOpening(stage) > EchoLooks.ScarOpening(stage - 1));
            previous = r;
        }
        Assert.Equal(EchoLooks.Rift(8, false), EchoLooks.Rift(50, false));
        Assert.Equal(EchoLooks.Rift(0, false), EchoLooks.Rift(-2, false));
        for (double t = 0; t < 10; t += 0.05) Assert.InRange(EchoLooks.Pulse(t, 1.4), 0.6 - 1e-9, 1.4 + 1e-9);
        Assert.InRange(EchoLooks.ScarOpening(10), 0, 1);
    }

    [Fact]
    public void Trainee_discrete_moitie_moins_sur_telephone()
    {
        Assert.Equal(0, EchoLooks.TrailRate(EchoKind.None, 1, false));
        Assert.Equal(EchoLooks.TrailRate(EchoKind.Night, 0.5, false) / 2, EchoLooks.TrailRate(EchoKind.Night, 0.5, true), 6);
        Assert.True(EchoLooks.TrailRate(EchoKind.Night, 1, false) > EchoLooks.TrailRate(EchoKind.Night, 0, false));
        Assert.InRange(EchoLooks.TrailRate(EchoKind.Rupture, 1, false), 1, 10);
    }
}
