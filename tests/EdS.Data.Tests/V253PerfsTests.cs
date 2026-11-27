using System;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// V2.53 (audit des performances, docs/PERFS.md) : rendu par classe de machine, petits objets sans ombre, feux sans ombre sur un PC
/// modeste, préfiltre des décors qui cachent le joueur, mesure des images (F9), et leur branchement dans les scripts Unity.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V253PerfsTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Fact]
    public void Le_PC_complet_garde_le_rendu_de_l_essai_realiste()
    {
        RenderProfile full = PerfRules.Render(DeviceTier.Full);
        Assert.Equal((90f, 4, 4096, true, 4, 8, true), (full.ShadowDistance, full.Cascades, full.ShadowResolution, full.SoftShadows, full.Msaa, full.LightsPerObject, full.AmbientOcclusion));
        RenderProfile phone = PerfRules.Render(DeviceTier.Phone);
        Assert.Equal((35f, 2, 1024, false, 2, 4, false), (phone.ShadowDistance, phone.Cascades, phone.ShadowResolution, phone.SoftShadows, phone.Msaa, phone.LightsPerObject, phone.AmbientOcclusion));
    }

    [Fact]
    public void Le_PC_modeste_a_un_rendu_plus_leger_qui_garde_les_lumieres_des_interieurs()
    {
        RenderProfile modest = PerfRules.Render(DeviceTier.Modest), full = PerfRules.Render(DeviceTier.Full);
        Assert.True(modest.ShadowDistance < full.ShadowDistance && modest.Cascades < full.Cascades && modest.ShadowResolution < full.ShadowResolution);
        Assert.False(modest.SoftShadows);
        Assert.False(modest.AmbientOcclusion);
        Assert.True(modest.Msaa < full.Msaa);
        Assert.Equal(full.LightsPerObject, modest.LightsPerObject);   // essai 10 : intérieurs éclairés par huit lumières
        // Même finesse par cascade près du joueur (au moins) : 2048 sur 2 cascades contre 4096 sur 4.
        Assert.True(modest.ShadowResolution / modest.Cascades >= full.ShadowResolution / full.Cascades);
        // Le PC de Damien (GTX 470, 4 Go) prend ce rendu en automatique, comme le réglage Basse.
        Assert.Equal(DeviceTier.Modest, MachineTier.Level(QualityChoice.Auto, 1224, 4096, false).Tier);
        Assert.Equal(DeviceTier.Modest, MachineTier.Level(QualityChoice.Low, 8192, 16384, false).Tier);
    }

    [Theory]
    [InlineData(0.1f, 0.2f, 0.1f, DeviceTier.Full, true)]
    [InlineData(0.1f, 0.2f, 0.1f, DeviceTier.Modest, false)]
    [InlineData(0.1f, 0.2f, 0.1f, DeviceTier.Phone, false)]
    [InlineData(0.59f, 0.3f, 0.59f, DeviceTier.Modest, false)]
    [InlineData(0.6f, 0.1f, 0.1f, DeviceTier.Modest, true)]
    [InlineData(0.1f, 3f, 0.1f, DeviceTier.Modest, true)]
    [InlineData(float.NaN, 0.1f, 0.1f, DeviceTier.Modest, true)]
    public void Seuls_les_petits_objets_perdent_leur_ombre_sur_une_machine_allegee(float x, float y, float z, DeviceTier tier, bool casts)
    {
        Assert.Equal(casts, PerfRules.CastsShadow(x, y, z, tier));
    }

    [Fact]
    public void Les_feux_ne_projettent_d_ombre_que_sur_le_PC_complet()
    {
        Assert.True(PerfRules.FireShadows(DeviceTier.Full));
        Assert.False(PerfRules.FireShadows(DeviceTier.Modest));
        Assert.False(PerfRules.FireShadows(DeviceTier.Phone));
        Assert.False(PerfRules.Reduced(DeviceTier.Full));
        Assert.True(PerfRules.Reduced(DeviceTier.Modest));
    }

    [Fact]
    public void Le_prefiltre_ne_saute_jamais_un_decor_qui_cache_le_joueur()
    {
        // Propriété : sur des milliers de tirages, MasksBody vrai implique que la boîte touche celle des segments (jamais de faux négatif).
        var rng = new Random(253);
        int masked = 0, skipped = 0;
        double R(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        for (int n = 0; n < 20000; n++)
        {
            var camera = (R(-20, 20), R(0, 15), R(-20, 20));
            var player = (R(-20, 20), R(0.5, 3), R(-20, 20));
            var a = (R(-25, 25), R(-1, 12), R(-25, 25));
            var min = (a.Item1, a.Item2, a.Item3);
            var max = (a.Item1 + R(0.1, 8), a.Item2 + R(0.1, 8), a.Item3 + R(0.1, 8));
            var reach = OccluderRules.Reach(camera, player);
            bool near = OccluderRules.Overlap(min, max, reach.Min, reach.Max);
            bool masks = OccluderRules.MasksBody(camera, player, min, max);
            if (masks) masked++;
            if (!near) skipped++;
            Assert.False(masks && !near);
        }
        Assert.True(masked > 100);
        Assert.True(skipped > 10000);   // l'essentiel des décors est sauté
    }

    [Fact]
    public void La_boite_des_segments_va_des_jambes_a_la_tete()
    {
        var reach = OccluderRules.Reach((10, 6, 0), (0, 1, 0));
        Assert.Equal((0.0, 0.3, 0.0), (reach.Min.X, Math.Round(reach.Min.Y, 6), reach.Min.Z));
        Assert.Equal((10.0, 6.0, 0.0), reach.Max);
        Assert.True(OccluderRules.Overlap((0, 0, 0), (1, 1, 1), (1, 1, 1), (2, 2, 2)));   // bords compris
        Assert.False(OccluderRules.Overlap((0, 0, 0), (1, 1, 1), (1.01, 0, 0), (2, 2, 2)));
    }

    [Fact]
    public void La_mesure_des_images_donne_moyenne_pire_et_saccades_sans_deborder()
    {
        var stats = new FrameStats();
        Assert.Equal(0, stats.Fps);
        Assert.True(stats.Due());
        Assert.False(stats.Due());
        for (int i = 0; i < 300; i++) stats.Add(0.02);
        stats.Add(0.1);   // une saccade de 100 ms
        stats.Add(double.NaN);
        stats.Add(-1);
        Assert.Equal(FrameStats.Capacity, stats.Count);
        Assert.Equal(100.0, stats.WorstMs, 6);
        Assert.Equal(1, stats.Hitches);
        Assert.InRange(stats.AverageMs, 20.0, 21.0);
        Assert.InRange(stats.Fps, 47.0, 50.0);
        Assert.True(stats.Due());   // plus de 0,5 s ajoutées
        Assert.Equal("48 i/s · 20.7 ms (pire 100.0) · saccades 1", stats.Text());
    }

    [Fact]
    public void Les_scripts_Unity_branchent_les_allegements()
    {
        string world = Script("Game", "StyleWorld.cs");
        Assert.Contains("DeviceQuality.Tier == DeviceTier.Modest && catalog.ModestPipeline != null", world);
        Assert.Contains("Application.quitting +=", world);   // le rendu du projet revient à la sortie du mode Jeu
        string style = Script("Editor", "EdsStyle.cs");
        Assert.Contains("Tune(light, PerfRules.Render(DeviceTier.Modest), true);", style);
        Assert.Contains("Tune(pipeline, PerfRules.Render(DeviceTier.Full), sceneTextures);", style);
        Assert.Contains("PerfRules.FireShadows(DeviceQuality.Tier)", Script("Game", "SpaceView.Interior.cs"));
        string space = Script("Game", "SpaceView.cs");
        Assert.Contains("SmallCasters();", space);
        Assert.Contains("OccluderRules.Reach(", space);
        string game = Script("Game", "EdsGame.cs");
        Assert.Contains("KeyCode.F9", game);
        Assert.Contains("_frameStats.Add(Time.unscaledDeltaTime);", game);
    }
}
