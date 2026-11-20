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
        string game = Game("EdsGame.cs"), view = Game("SeasonView.cs"), space = Game("SpaceView.cs");
        Assert.Contains("_snowCover.Sync(Time.deltaTime, a.Indoors, _arena.DecorRoot, CellStreamView.CurrentRoot)", game);
        Assert.Contains("_snowCover?.Clear();", game);
        Assert.Contains("_EdsSnowPass", view);
        Assert.Contains("r.gameObject.name != WinterLook.OverlayName", space);   // le fondu des obstacles ignore la neige posée
        // Revue Codex de la PR #232 (2e tour) : neige du sol au relief réel du Terrain, lit de la bande ouverte creusé, maillages détruits.
        string cover = Game("SnowCover.cs");
        Assert.Contains("terrain.SampleHeight(", cover);
        Assert.DoesNotContain("StyleWorld.StandHeight", cover);
        Assert.Contains("if (_terrainMeshes.TryGetValue(t, out Mesh mesh) && mesh != null) Object.Destroy(mesh);", cover);
        Assert.Contains("if (_ice != null && _ice != _water) Object.Destroy(_ice);", space);   // 3e tour : la copie de l'eau qui gèle est libérée
        Assert.Contains("if (_ice != null && _ice != _water && _ice != StyleWorld.Water()) Object.Destroy(_ice);", Game("CellStreamView.cs"));
        Assert.Contains("foreach (SpaceObstacle o in OpenIceStrips(space, map))\n                rivers.Add(", space.Replace("\r\n", "\n"));
        Assert.Contains("_EdsIce > 0.001h && _IceAllowed > 0.5h", eau);   // seule la bande du lac gèle
    }

    // --- Comportement (règles portables, relecture indépendante de la PR #232) ---------------------------------------------

    private static readonly (string, int)[] Opaque = { ("Universal Render Pipeline/Lit", 2000) };

    [Fact]
    public void Neige_posee_seulement_sur_le_decor_opaque_immobile()
    {
        Assert.True(WinterLook.Accepts("toit", true, false, Opaque));
        Assert.True(WinterLook.Accepts("maison", true, false, new[] { ("Universal Render Pipeline/Lit", 2000), ("Universal Render Pipeline/Lit", 2449) }));
        Assert.False(WinterLook.Accepts("toit", false, false, Opaque));   // coupé
        Assert.False(WinterLook.Accepts("loup", true, true, Opaque));     // ancêtre animé (bête, objet tenu)
        Assert.False(WinterLook.Accepts(WinterLook.OverlayName, true, false, Opaque));   // jamais un double de double
        Assert.False(WinterLook.Accepts("vide", true, false, Array.Empty<(string, int)>()));
        Assert.False(WinterLook.Accepts("vitre", true, false, new[] { ("Universal Render Pipeline/Lit", 2000), ("Universal Render Pipeline/Lit", 3000) }));
        Assert.False(WinterLook.Accepts("haie", true, false, new[] { ("Universal Render Pipeline/Lit", 2450) }));   // découpé
        Assert.False(WinterLook.Accepts("cassé", true, false, new[] { ((string)null!, 2000) }));
        foreach (string shader in WinterLook.SkippedShaders)
            Assert.False(WinterLook.Accepts("x", true, false, new[] { (shader, 2000) }), shader);
        Assert.Contains("EdS/Feuillage", WinterLook.SkippedShaders);
        Assert.Contains("EdS/Neige", WinterLook.SkippedShaders);
        Assert.Throws<ArgumentNullException>(() => WinterLook.Accepts("x", true, false, null!));
        Assert.Equal((6000, 2f), (WinterLook.MaxOverlays, WinterLook.RescanSeconds));
    }

    [Fact]
    public void Refus_passager_reexamine_refus_definitif_retenu()
    {
        // Revue Codex de la PR #232 : un modèle coupé ou en fondu au moment de la recherche est réexaminé plus tard.
        Assert.Equal(WinterLook.Verdict.Accepted, WinterLook.Judge("toit", true, false, Opaque));
        Assert.Equal(WinterLook.Verdict.Later, WinterLook.Judge("toit", false, false, Opaque));
        Assert.Equal(WinterLook.Verdict.Later, WinterLook.Judge("toit en fondu", true, false, new[] { ("Universal Render Pipeline/Lit", 3000) }));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge("loup", true, true, Opaque));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge("loup coupé", false, true, Opaque));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge(WinterLook.OverlayName, true, false, Opaque));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge("buisson", false, false, new[] { ("EdS/Feuillage", 2450) }));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge("vide", true, false, Array.Empty<(string, int)>()));
        Assert.Equal(WinterLook.Verdict.Never, WinterLook.Judge("cassé", true, false, new[] { ((string)null!, 2000) }));
        // Accepts reste vrai seulement pour Accepted.
        Assert.Equal(WinterLook.Accepts("toit", false, false, Opaque), WinterLook.Judge("toit", false, false, Opaque) == WinterLook.Verdict.Accepted);
    }

    [Theory]
    [InlineData(100f, 2f)]
    [InlineData(400f, 2f)]
    [InlineData(800f, 4f)]
    [InlineData(1200f, 6f)]
    public void Couche_de_neige_du_terrain_bornee(float side, float step)
    {
        // Revue Codex de la PR #232 : le terrain du catalogue reçoit une couche de neige ; au plus 201 × 201 sommets (maillage 16 bits).
        Assert.Equal(step, WinterLook.TerrainSnowStep(side), 3);
        int cells = (int)Math.Ceiling(side / WinterLook.TerrainSnowStep(side));
        Assert.True((cells + 1) * (cells + 1) < 65536);
    }

    [Theory]
    [InlineData(true, true, 2000, true)]
    [InlineData(false, true, 2000, false)]   // plus de neige : caché
    [InlineData(true, false, 2000, false)]   // modèle coupé (obstacle qui cache le joueur) : sa neige aussi
    [InlineData(true, true, 3000, false)]    // modèle en fondu transparent : sa neige ne flotte pas
    [InlineData(true, true, 2449, true)]
    public void Le_double_suit_son_modele(bool snowing, bool enabled, int queue, bool shown)
    {
        Assert.Equal(shown, WinterLook.OverlayShown(snowing, enabled, queue));
    }

    [Fact]
    public void Seule_la_bande_praticable_du_lac_gele()
    {
        // Réponse de Damien du 20 novembre 2026 : seul le lac gèle, là où la glace est praticable ; les rivières restent libres.
        ZoneDefinition zone = GrandeCarteLieux.Vallons.Value;
        var waters = zone.Setup.Space!.Obstacles.Where(o => o.Id.StartsWith("riviere", StringComparison.Ordinal)).ToList();
        var frozen = waters.Where(o => WinterLook.IceWater(o.Id)).Select(o => o.Id).ToList();
        Assert.Equal(new[] { "riviere_glace_du_lac_marais" }, frozen);
        Assert.Equal(zone.Passages.Single(p => p.Id == "glace_du_lac").Obstacle, frozen[0]);
        Assert.False(WinterLook.IceWater("riviere_grand_lac"));
        Assert.False(WinterLook.IceWater(null!));
    }
}
