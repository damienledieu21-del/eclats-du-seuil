using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>Sous-lot V2.29 (horizon, plan de la grande carte § 6.3) : la silhouette des terres lointaines autour des Vallons.</summary>
[Trait("Category", "Sector")]
public class V229HorizonTests
{
    private static readonly HorizonProfile P = HorizonProfile.Vallons;

    private static int At(double azimuth) => (int)Math.Round(azimuth * HorizonProfile.Samples / 360.0);

    [Fact]
    public void Cretes_au_nord_est_foret_a_l_ouest_collines_ailleurs()
    {
        int peak = Enumerable.Range(0, HorizonProfile.Samples).OrderByDescending(P.Height).First();
        Assert.InRange(HorizonProfile.Azimuth(peak), HorizonProfile.MountainsAt - HorizonProfile.MountainsHalfWidth, HorizonProfile.MountainsAt + HorizonProfile.MountainsHalfWidth);
        Assert.InRange(P.Height(peak), HorizonProfile.SnowLine, HorizonProfile.MountainPeak);
        Assert.True(P.Snow(peak));
        Assert.Equal(HorizonLayer.Mountains, P.Layer(At(HorizonProfile.MountainsAt)));
        Assert.Equal(HorizonLayer.Forest, P.Layer(At(HorizonProfile.ForestAt)));
        Assert.Equal(HorizonLayer.Hills, P.Layer(At(180)));
        Assert.InRange(P.Height(At(HorizonProfile.ForestAt)), 100, HorizonProfile.ForestTop);
        Assert.InRange(P.Height(At(180)), HorizonProfile.HillsBase, HorizonProfile.HillsBase + 45);
        Assert.False(P.Snow(At(HorizonProfile.ForestAt)));
    }

    [Fact]
    public void Bornee_continue_hors_des_pics_et_periodique()
    {
        for (int i = 0; i < HorizonProfile.Samples; i++)
        {
            Assert.InRange(P.Height(i), HorizonProfile.SeaLevel, HorizonProfile.MountainPeak);   // V2.29 (2) : la mer, à l'ouest, au plus bas
            double step = Math.Abs(P.Height(i + 1) - P.Height(i));
            // Hors de la chaîne des Crêtes (et de ses piémonts), la silhouette est douce.
            double fromRange = Math.Abs(((HorizonProfile.Azimuth(i) - HorizonProfile.MountainsAt) % 360 + 540) % 360 - 180);
            Assert.True(fromRange < HorizonProfile.MountainsHalfWidth + 1 || step < 15, $"saut de {step:0.0} m en {i}");
            Assert.True(step < 160, $"saut de {step:0.0} m en {i}");
        }
        Assert.Equal(P.Height(0), P.Height(HorizonProfile.Samples));
        // Relecture de la PR #182 : pas de marche au nord, entre le dernier point et le premier.
        Assert.True(Math.Abs(P.Height(HorizonProfile.Samples - 1) - P.Height(0)) < 5, $"{P.Height(HorizonProfile.Samples - 1) - P.Height(0):0.0} m au nord");
        Assert.Equal(P.Height(-1), P.Height(HorizonProfile.Samples - 1));
        (double x, double z) north = HorizonProfile.Direction(0), east = HorizonProfile.Direction(At(90));
        Assert.Equal((0.0, 1.0), (Math.Round(north.x, 9), Math.Round(north.z, 9)));
        Assert.Equal((1.0, 0.0), (Math.Round(east.x, 9), Math.Round(east.z, 9)));
    }

    [Fact]
    public void Toujours_la_meme()
    {
        Assert.Same(HorizonProfile.Vallons, P);
        double sum = Enumerable.Range(0, HorizonProfile.Samples).Sum(P.Height);
        Assert.InRange(sum / HorizonProfile.Samples, HorizonProfile.HillsBase, 400);
    }

    /// <summary>Sous-lot V2.29 (2) : la mer de la Côte des Brumes à l'ouest, à la place des collines ; la forêt reste au nord-ouest.</summary>
    [Fact]
    public void La_mer_a_l_ouest_la_foret_au_nord_ouest()
    {
        Assert.Equal(HorizonLayer.Sea, P.Layer(At(HorizonProfile.SeaAt)));
        Assert.Equal(HorizonLayer.Sea, P.Layer(At(250)));
        Assert.InRange(P.Height(At(HorizonProfile.SeaAt)), HorizonProfile.SeaLevel - 0.5, HorizonProfile.SeaLevel + 0.5);
        Assert.Equal(HorizonLayer.Forest, P.Layer(At(315)));
        Assert.Equal(HorizonLayer.Hills, P.Layer(At(180)));
        Assert.False(P.Snow(At(HorizonProfile.SeaAt)));
    }
}
