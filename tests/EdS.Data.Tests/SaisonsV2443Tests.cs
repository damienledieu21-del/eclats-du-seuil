using EdS.Data.Json;
using EdS.Data.Seasons;
using EdS.Data.Zones;
using EdS.Domain.Time;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.44.3 (l'hiver dans Unity, décisions de Damien du 20 novembre 2026) : la neige affichée suit le contenu saisonnier (plaine
/// et col au-dessus de sa limite, fondu de 7 jours), ni neige ni glace dans une pièce ; l'eau lit la glace, le feuillage fond d'une saison à
/// l'autre, la neige se pose sur tout le décor opaque (toits, rochers, sol) par le shader « EdS/Neige ».
/// </summary>
[Trait("Category", "World")]
public class SaisonsV2443Tests
{
    private static SeasonSnow Snow() =>
        SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), GrandeCarteLieux.Vallons.Value).Book!.Snow!;

    private static string Game(string name) => File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", name));

    [Theory]
    [InlineData(2026, 12, 14, 0f)]
    [InlineData(2026, 12, 15, 1f / 7f)]   // la neige s'installe
    [InlineData(2026, 12, 21, 1f)]
    [InlineData(2027, 1, 20, 1f)]
    [InlineData(2027, 2, 22, 1f)]
    [InlineData(2027, 2, 28, 1f / 7f)]    // puis fond
    [InlineData(2028, 2, 29, 1f / 7f)]    // le 29 février compte comme le 28
    [InlineData(2027, 3, 1, 0f)]
    public void Neige_de_la_plaine_s_installe_et_fond_en_sept_jours(int y, int m, int d, float cover)
    {
        Assert.Equal(cover, SeasonLook.Cover(Snow().Lowland, new CivilDate(y, m, d)), 3);
    }

    [Fact]
    public void Look_de_l_hiver_plaine_col_et_piece()
    {
        SeasonSnow snow = Snow();
        var date = new CivilDate(2026, 11, 20);
        SeasonLook look = SeasonLook.Of(SeasonCalendar.At(date)).WithSnow(snow, date);
        Assert.Equal(0f, look.Snow);   // pas encore en plaine
        Assert.Equal(SeasonLook.WinterSnow, look.PassSnow, 3);   // au col depuis le 1er novembre
        Assert.Equal(220f, look.SnowLine);
        var winter = new CivilDate(2027, 1, 10);
        SeasonLook deep = SeasonLook.Of(SeasonCalendar.At(winter)).WithSnow(snow, winter);
        Assert.Equal(SeasonLook.WinterSnow, deep.Snow, 3);
        Assert.Equal(1f, deep.Ice);
        SeasonLook room = deep.Indoors();
        Assert.Equal((0f, 0f, 0f), (room.Snow, room.PassSnow, room.Ice));
        Assert.Equal((deep.R, deep.Season), (room.R, room.Season));
        // Sans contenu ou sans date : la neige de la saison, comme avant.
        SeasonLook plain = SeasonLook.Of(SeasonCalendar.At(winter));
        Assert.Equal(plain, plain.WithSnow(null, winter));
        Assert.Equal(plain, plain.WithSnow(snow, null));
        Assert.NotEqual(deep, plain);
        var summer = new CivilDate(2027, 7, 10);
        SeasonLook july = SeasonLook.Of(SeasonCalendar.At(summer)).WithSnow(snow, summer);
        Assert.Equal((0f, 0f), (july.Snow, july.PassSnow));
    }

    [Fact]
    public void Le_col_garde_sa_neige_au_dessus_du_relief_des_collines_basses()
    {
        // La limite du col (220 m) passe au-dessus des vignes (60 m) et des collines (110 à 160 m), sous le col (320 m) et les crêtes.
        Assert.Equal(220, (int)(Snow().PassAltitude.Raw / 10000));
        var heights = new ReliefSettings().RegionHeights;
        Assert.Contains(heights, h => h.Height > 220.0);
        Assert.Contains(heights, h => h.Height < 220.0 && h.Height >= 50.0);
    }

    [Fact]
    public void Shaders_de_l_hiver()
    {
        string eau = Game("Eau.shader");
        Assert.Contains("half _EdsIce;", eau);
        Assert.Contains("_EdsIce >= 0.999h ? 1.0h", eau);   // glace praticable : couche entière
        string feuillage = Game("Feuillage.shader"), herbe = Game("Herbe.shader"), neige = Game("Neige.shader");
        Assert.Contains("_PrevMap", feuillage);
        Assert.Contains("lerp(before, now, (half)saturate(_EdsSeason.y))", feuillage);
        Assert.DoesNotContain("SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv)", feuillage);   // toutes les passes fondent (ombre, profondeur)
        foreach (string s in new[] { feuillage, herbe, neige }) Assert.Contains("half EdsSnowAt(float y)", s);
        Assert.Contains("Shader \"EdS/Neige\"", neige);
        Assert.Contains("ZWrite Off", neige);
        string build = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsBuildShaders.cs"));
        Assert.Contains("Shader.Find(\"EdS/Neige\")", build);
        string game = Game("EdsGame.cs"), cover = Game("SnowCover.cs"), view = Game("SeasonView.cs"), trees = Game("SpaceView.Trees.cs");
        Assert.Contains("_snowCover.Sync(Time.deltaTime, a.Indoors, _arena.DecorRoot, CellStreamView.CurrentRoot)", game);
        Assert.Contains("_snowCover?.Clear();", game);
        Assert.Contains("\"EdS/Feuillage\", \"EdS/Herbe\", \"EdS/Eau\"", cover);
        Assert.Contains("ShadowCastingMode.Off", cover);
        Assert.Contains("_EdsSnowPass", view);
        Assert.Contains("FadingFrom(season) != _treeFrom", trees);
    }
}
