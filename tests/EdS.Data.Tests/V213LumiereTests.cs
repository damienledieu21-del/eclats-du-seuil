using System;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.13 (essai de Damien du 9 novembre 2026 : « le jeu est trop sombre ») : règle du curseur de luminosité
/// (<see cref="BrightnessRules"/>), son branchement dans le jeu, et la nuit qui reste lisible.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V213LumiereTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Theory]
    [InlineData(-5, 50)]
    [InlineData(0, 50)]
    [InlineData(49, 50)]
    [InlineData(50, 50)]
    [InlineData(137, 137)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(int.MaxValue, 200)]
    [InlineData(int.MinValue, 50)]
    public void Borne_entre_50_et_200(int given, int expected) => Assert.Equal(expected, BrightnessRules.Clamp(given));

    [Fact]
    public void Valeur_decimale_absurde_donne_100()
    {
        Assert.Equal(100, BrightnessRules.Clamp(double.NaN));
        Assert.Equal(100, BrightnessRules.Clamp(double.PositiveInfinity));
        Assert.Equal(100, BrightnessRules.Clamp(double.NegativeInfinity));
        Assert.Equal(125, BrightnessRules.Clamp(124.6));
        Assert.Equal(200, BrightnessRules.Clamp(1e12));
    }

    [Fact]
    public void Pas_de_10_points_bornes()
    {
        Assert.Equal(110, BrightnessRules.Step(100, 1));
        Assert.Equal(90, BrightnessRules.Step(100, -1));
        Assert.Equal(100, BrightnessRules.Step(100, 0));
        Assert.Equal(200, BrightnessRules.Step(200, 1));
        Assert.Equal(50, BrightnessRules.Step(50, -1));
        Assert.Equal(200, BrightnessRules.Step(195, 1));
        Assert.Equal(200, BrightnessRules.Step(900, 0));   // une valeur hors bornes est d'abord ramenée
    }

    [Fact]
    public void Cent_pour_cent_ne_change_rien_et_les_bornes_font_une_vue()
    {
        Assert.Equal(0.0, BrightnessRules.ExposureOffset(100), 12);
        Assert.Equal(-1.0, BrightnessRules.ExposureOffset(50), 12);
        Assert.Equal(1.0, BrightnessRules.ExposureOffset(200), 12);
        Assert.Equal(1.0, BrightnessRules.Factor(100));
    }

    [Fact]
    public void Exposition_croissante_et_texte()
    {
        double previous = double.NegativeInfinity;
        for (int p = BrightnessRules.MinPercent; p <= BrightnessRules.MaxPercent; p++)
        {
            double e = BrightnessRules.ExposureOffset(p);
            Assert.True(e > previous);
            previous = e;
        }
        Assert.Equal("Luminosité : 120 %", BrightnessRules.Label(120));
        Assert.Equal("Luminosité : 200 %", BrightnessRules.Label(999));
    }

    [Fact]
    public void Le_curseur_est_branche_dans_le_jeu()
    {
        string world = Script("Game", "StyleWorld.cs");
        Assert.Equal(3, world.Split(new[] { "BrightnessSetting.ExposureOffset" }, StringSplitOptions.None).Length - 1);
        string hud = Script("Game", "HudView.cs");
        Assert.Contains("BrightnessSetting.Step(-1)", hud);
        Assert.Contains("BrightnessSetting.Step(1)", hud);
        Assert.Contains("BrightnessSetting.Label", hud);
        Assert.Contains("PlayerPrefs.SetInt(Key", Script("Game", "BrightnessSetting.cs"));
    }

    [Fact]
    public void Les_reglages_par_defaut_sont_remis_a_la_prochaine_preparation()
    {
        string catalog = Script("Game", "StyleCatalog.cs");
        Assert.Contains("AmbienceDefaults = 3;", catalog);
        Assert.Contains("IndoorDefaults = 2;", catalog);
    }

    [Fact]
    public void La_nuit_reste_lisible_et_plus_claire_que_la_nuit_noire()
    {
        Assert.True(DayCycle.NightAmbient >= 0.65);
        DayLight midnight = DayCycle.At(0);
        Assert.True(midnight.AmbientLevel >= 0.65);
        Assert.True(midnight.SkyLevel >= 0.3);
        Assert.True(midnight.SunLevel > 0);   // la lune éclaire
    }
}
