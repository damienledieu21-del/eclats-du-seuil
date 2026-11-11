using EdS.Runtime;
using EdS.World;
using EdS.Domain.Space;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.25 : grange, moulin (roue qui tourne, eau du bief qui défile), tour de guet, puits, deux étals, tente du camp (une pour les trois) et
/// palissades du camp faits à l'atelier, versions de jeu LOD1 cuites, recentrées, sol à 0, posées à l'échelle 1 (<see
/// cref="TownModels.OwnFit"/>). Emprises et façades vérifiées sur les vraies données de zone ; données de zone inchangées. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V225FermeMoulinCampTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v225_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Folder(string id) => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Batiments", id);

    private AdventureSession Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a;
    }

    private static (double MinX, double MaxX, double MinZ, double MaxZ) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 10000.0), o.Points.Max(p => p.X.Raw / 10000.0), o.Points.Min(p => p.Z.Raw / 10000.0), o.Points.Max(p => p.Z.Raw / 10000.0));

    [Theory]
    [InlineData("grange", "grange_vallons")]
    [InlineData("tour", "tour_vallons")]
    [InlineData("puits", "puits_vallons")]
    [InlineData("etal1", "etal_legumes_vallons")]
    [InlineData("etal2", "etal_etoffes_vallons")]
    [InlineData("tente", "tente_camp")]
    [InlineData("palissade_camp1", "palissade_camp1")]
    [InlineData("palissade_camp2", "palissade_camp2")]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_textures(string id, string name)
    {
        string[] files = Directory.GetFiles(Folder(id)).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { name + ".fbx", name + "_couleur.jpg", name + "_normale.png" }, files);
        string path = "Assets/EdS/Modeles/Batiments/" + id + "/" + name + ".fbx";
        Assert.Equal(id, TownModels.OwnBuildingOf(path));
        Assert.Null(TownModels.OwnPartOf(path));
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
        CheckTextures(id, name);
    }

    [Fact]
    public void Le_moulin_a_son_batiment_sa_roue_et_son_eau()
    {
        string[] files = Directory.GetFiles(Folder("moulin")).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { "moulin_eau.fbx", "moulin_roue.fbx", "moulin_vallons.fbx", "moulin_vallons_couleur.jpg", "moulin_vallons_normale.png" }, files);
        Assert.Null(TownModels.OwnPartOf("Assets/EdS/Modeles/Batiments/moulin/moulin_vallons.fbx"));
        Assert.Equal("roue", TownModels.OwnPartOf("Assets/EdS/Modeles/Batiments/moulin/moulin_roue.fbx"));
        Assert.Equal("eau", TownModels.OwnPartOf("Assets/EdS/Modeles/Batiments/moulin/Moulin_Eau.FBX"));
        Assert.Null(TownModels.OwnPartOf("Assets/Autre/moulin_roue.fbx"));
        CheckTextures("moulin", "moulin_vallons");
        foreach (string part in new[] { "moulin_roue", "moulin_eau" })
            Assert.InRange(new FileInfo(Path.Combine(Folder("moulin"), part + ".fbx")).Length, 5_000, 100_000);
    }

    private static void CheckTextures(string id, string name)
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder(id), name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder(id), name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder(id), name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);
    }

    [Fact]
    public void Les_trois_tentes_partagent_le_modele_du_dossier_tente()
    {
        foreach (string id in new[] { "tente1", "tente2", "tente3", "Tente2" }) Assert.Equal("tente", TownModels.OwnModelFolder(id));
        Assert.Equal("tente", TownModels.OwnModelFolder("tente"));
        Assert.Equal("grange", TownModels.OwnModelFolder("grange"));
        Assert.Equal("tentation", TownModels.OwnModelFolder("tentation"));
        Assert.Equal("maison2", TownModels.OwnModelFolder("maison2"));
    }

    // Une ligne par obstacle : emprise (minX, maxX, minZ, maxZ), façade (fx, fz), boîte de l'export de jeu (repère de Blender recentré,
    // X0, X1, Y0, Y1 : export_jeu.json), débord attendu hors de l'emprise (m). Le modèle est posé à l'échelle 1, le centre de son emprise
    // (TownModels.OwnFootCenter) au centre de l'obstacle. Gardé hors des InlineData (noms de test courts).
    private static readonly Dictionary<string, (string Model, double[] V)> Pieces = new Dictionary<string, (string, double[])>
    {
        ["grange"] = ("grange_vallons", new[] { -92.0, -80.0, -50.0, -42.0, 0, 1, -6.0, 6.0, -3.982, 3.982, 0.0 }),
        ["moulin"] = ("moulin_vallons", new[] { -6.0, 0.0, 6.0, 12.0, -1, 0, -2.482, 3.5, -3.0, 3.0, 0.0 }),
        ["tour"] = ("tour_vallons", new[] { 24.3, 31.7, 64.3, 71.7, 1, 0, -3.731, 3.731, -3.731, 3.731, 0.031 }),
        ["puits"] = ("puits_vallons", new[] { -59.2, -56.8, -2.2, 0.2, -1, 0, -1.39, 1.21, -1.2, 1.2, 0.19 }),
        ["etal1"] = ("etal_legumes_vallons", new[] { -55.0, -52.0, 5.0, 7.0, 0, -1, -1.75, 1.75, -1.3, 1.3, 0.3 }),
        ["etal2"] = ("etal_etoffes_vallons", new[] { -64.0, -61.0, -9.0, -7.0, 0, 1, -1.75, 1.75, -1.3, 1.3, 0.3 }),
        ["tente1"] = ("tente_camp", new[] { 109.6, 114.4, 78.6, 83.4, -1, 0, -1.929, 1.929, -2.33, 2.324, 0.0 }),
        ["tente2"] = ("tente_camp", new[] { 119.6, 124.4, 71.6, 76.4, -1, 0, -1.929, 1.929, -2.33, 2.324, 0.0 }),
        ["tente3"] = ("tente_camp", new[] { 121.6, 126.4, 86.6, 91.4, -1, 0, -1.929, 1.929, -2.33, 2.324, 0.0 }),
        ["palissade_camp1"] = ("palissade_camp1", new[] { 92.0, 93.0, 82.0, 98.0, -1, 0, -7.987, 7.987, -0.48, 0.427, 0.0 }),
        ["palissade_camp2"] = ("palissade_camp2", new[] { 93.0, 116.0, 66.0, 67.0, 0, -1, -11.487, 11.487, -0.48, 0.427, 0.0 }),
    };

    /// <summary>Façade d'un obstacle comme la vue la décide (porte, puis route, puis la face extérieure des palissades).</summary>
    private static (int X, int Z) Facing(AdventureMap map, string id, (double MinX, double MaxX, double MinZ, double MaxZ) b) =>
        TownModels.OwnFacing(id, TownRules.FacingOf(id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));

    /// <summary>Point du repère de Blender recentré (X, Y) posé dans le monde : Unity change le signe de x (x = −X, z = −Y).</summary>
    private static (double X, double Z) World(string model, double cx, double cz, (int X, int Z) facing, double x, double y)
    {
        (double fx, double fz) = TownModels.OwnFootCenter(model);
        var p = TownModels.OwnPlace(cx, cz, facing, -x - fx, 0.0, -y - fz);
        return (p.X, p.Z);
    }

    /// <summary>
    /// Sur les vraies données de zone : chaque emprise est celle de la fiche (scenarios/zones/vallons.json), la façade attendue, et la
    /// boîte du modèle posé ne sort de l'emprise que du débord attendu (toit du puits, auvents des étals, tuiles de la tour).
    /// </summary>
    [Theory]
    [InlineData("grange")]
    [InlineData("moulin")]
    [InlineData("tour")]
    [InlineData("puits")]
    [InlineData("etal1")]
    [InlineData("etal2")]
    [InlineData("tente1")]
    [InlineData("tente2")]
    [InlineData("tente3")]
    [InlineData("palissade_camp1")]
    [InlineData("palissade_camp2")]
    public void Chaque_piece_tient_dans_son_emprise(string id)
    {
        (string model, double[] v) = Pieces[id];
        AdventureMap map = Vallons().Map;
        var b = Box(map.Obstacles.First(o => o.Id == id));
        Assert.Equal((v[0], v[1], v[2], v[3]), (Math.Round(b.MinX, 2), Math.Round(b.MaxX, 2), Math.Round(b.MinZ, 2), Math.Round(b.MaxZ, 2)));
        (int X, int Z) facing = Facing(map, id, b);
        Assert.Equal(((int)v[4], (int)v[5]), facing);
        double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2;
        var corners = new[] { (v[6], v[8]), (v[6], v[9]), (v[7], v[8]), (v[7], v[9]) }.Select(c => World(model, cx, cz, facing, c.Item1, c.Item2)).ToArray();
        double out0 = Math.Max(Math.Max(b.MinX - corners.Min(c => c.X), corners.Max(c => c.X) - b.MaxX),
            Math.Max(b.MinZ - corners.Min(c => c.Z), corners.Max(c => c.Z) - b.MaxZ));
        Assert.Equal(v[10], Math.Round(Math.Max(0.0, out0), 3), 3);
    }

    /// <summary>
    /// Le moulin : son emprise de 6 × 6 (bief compris) a son centre 0,50 m à droite du pivot (centre des murs). Posé façade à l'ouest, vers
    /// le chemin du moulin, la roue et le bief (à droite de la façade, Blender X 4,65 – 5,70) sont au sud, dans l'emprise, et l'eau du bief
    /// (Blender +Y, vers l'arrière) coule vers l'est, vers la rivière.
    /// </summary>
    [Fact]
    public void Le_moulin_est_recentre_sur_son_emprise_et_son_bief_coule_vers_la_riviere()
    {
        Assert.Equal((-0.5, 0.0), TownModels.OwnFootCenter("moulin_vallons"));
        Assert.Equal((0.0, 0.0), TownModels.OwnFootCenter("grange_vallons"));
        AdventureMap map = Vallons().Map;
        var b = Box(map.Obstacles.First(o => o.Id == "moulin"));
        (int X, int Z) facing = Facing(map, "moulin", b);
        Assert.Equal((-1, 0), facing);
        // Les murs (X −2,20 – 2,20 recentrés), centrés 0,50 m au nord du centre de l'obstacle (z 9).
        var walls = World("moulin_vallons", -3.0, 9.0, facing, 0.0, 0.0);
        Assert.Equal((-3.0, 9.5), (Math.Round(walls.X, 6), Math.Round(walls.Z, 6)));
        // Axe de la roue (repère du jeu X 3,00) : au sud des murs, dans l'emprise.
        var axle = World("moulin_vallons", -3.0, 9.0, facing, 3.0, 0.0);
        Assert.Equal((-3.0, 6.5), (Math.Round(axle.X, 6), Math.Round(axle.Z, 6)));
        Assert.InRange(axle.Z, b.MinZ, walls.Z);
        // Le courant du bief (de Y −2,4 à Y 2,4) va vers l'est, vers la rivière (x 4 – 7).
        var upstream = World("moulin_vallons", -3.0, 9.0, facing, 3.0, -2.4);
        var downstream = World("moulin_vallons", -3.0, 9.0, facing, 3.0, 2.4);
        var river = Box(map.Obstacles.First(o => o.Id == "riviere_nord"));
        Assert.True(downstream.X > upstream.X && downstream.X <= b.MaxX && downstream.X < river.MinX);
        // Centres des pièces animées, repère d'Unity (x = −X).
        Assert.Equal((-2.835, 1.8, 0.0), TownModels.OwnPartCenter("moulin_vallons", "roue"));
        Assert.Equal((-3.0, 0.15, 0.0), TownModels.OwnPartCenter("moulin_vallons", "eau"));
        Assert.Null(TownModels.OwnPartCenter("grange_vallons", "roue"));
        Assert.Null(TownModels.OwnPartCenter("moulin_vallons", "toit"));
    }

    /// <summary>
    /// La roue tourne doucement (aubes basses à la vitesse de l'eau, moins de 4 tours par minute), dans le sens du courant du bief
    /// (Blender +Y, vers l'arrière : −z du modèle dans Unity) ; l'eau défile dans le même sens (v croît le long du courant).
    /// </summary>
    [Fact]
    public void La_roue_et_l_eau_vont_dans_le_sens_du_courant()
    {
        double speed = TownModels.MillWheelDegreesPerSecond;
        Assert.Equal(0.7 / 1.75 * 180 / Math.PI, speed, 9);
        Assert.InRange(speed * 60 / 360, 3.0, 4.0);
        // L'aube du bas (y −1,75) avance vers −z pour un petit angle positif ; celle du haut vers +z.
        var low = TownModels.MillWheelTurn(1.0, -1.75, 0.0);
        var high = TownModels.MillWheelTurn(1.0, 1.75, 0.0);
        Assert.True(low.Z < 0 && high.Z > 0);
        // Vitesse au bord de la roue = vitesse de l'eau.
        Assert.Equal(TownModels.MillWaterSpeed, TownModels.MillWheelRadius * speed * Math.PI / 180, 9);
        var quarter = TownModels.MillWheelTurn(90.0, 1.0, 0.0);
        Assert.Equal((0.0, 1.0), (Math.Round(quarter.Y, 9), Math.Round(quarter.Z, 9)));
        // Le décalage de v décroît avec le temps (la texture descend le courant), reste dans ]−1 ; 0].
        Assert.Equal(0.0, TownModels.MillWaterOffset(0));
        Assert.Equal(-0.7, TownModels.MillWaterOffset(1.0), 9);
        Assert.Equal(-0.4, TownModels.MillWaterOffset(2.0), 9);
        Assert.Equal(0.0, TownModels.MillWaterOffset(-5));
        for (double t = 0; t < 30; t += 0.37) Assert.InRange(TownModels.MillWaterOffset(t), -1.0, 0.0);
    }

    /// <summary>
    /// Palissade 2 : face extérieure au sud, son portail (passage de 2,20 m centré à 7,00 m de l'extrémité ouest du modèle) tombe à
    /// x ≈ 100 ; l'obstacle reste un rectangle plein (question à Damien dans la PR). Palissade 1 : tournée d'un quart de tour, face
    /// extérieure à l'ouest, sa longueur le long de z.
    /// </summary>
    [Fact]
    public void Les_palissades_du_camp_regardent_dehors_et_le_portail_reste_ferme()
    {
        AdventureMap map = Vallons().Map;
        SpaceObstacle p2 = map.Obstacles.First(o => o.Id == "palissade_camp2");
        Assert.Equal(4, p2.Points.Count);
        Assert.Equal((0, -1), TownModels.OwnFacing("palissade_camp2", (0, 1)));
        Assert.Equal((-1, 0), TownModels.OwnFacing("palissade_camp1", (1, 0)));
        Assert.Equal((0, 1), TownModels.OwnFacing("grange", (0, 1)));
        var gate = World("palissade_camp2", 104.5, 66.5, (0, -1), 7.0 - 11.5, 0.0);
        Assert.Equal((100.0, 66.5), (Math.Round(gate.X, 6), Math.Round(gate.Z, 6)));
        var west = World("palissade_camp2", 104.5, 66.5, (0, -1), -11.5, 0.0);
        Assert.Equal(93.0, west.X, 6);
        var north = World("palissade_camp1", 92.5, 90.0, (-1, 0), 8.0, 0.0);
        var south = World("palissade_camp1", 92.5, 90.0, (-1, 0), -8.0, 0.0);
        Assert.Equal(new[] { 82.0, 98.0 }, new[] { north.Z, south.Z }.Select(z => Math.Round(z, 6)).OrderBy(z => z).ToArray());
        // Face extérieure (+z du modèle, Blender −Y) vers l'ouest.
        var outside = World("palissade_camp1", 92.5, 90.0, (-1, 0), 0.0, -1.0);
        Assert.True(outside.X < 92.5);
    }

    /// <summary>
    /// Aucune de ces pièces n'a de feu tenu ni de lanterne dans les données : pas de cheminée connue, pas de lanterne de modèle. La vue
    /// pose le moulin et les palissades par le modèle de l'atelier avant le moulin de Tim0 et la palissade de rondins, et ne pose plus la
    /// roue de l'Asset Store quand le moulin de l'atelier est là.
    /// </summary>
    [Fact]
    public void Ni_fumee_ni_lanterne_et_la_vue_prend_les_modeles_de_l_atelier()
    {
        foreach (string m in new[] { "grange_vallons", "moulin_vallons", "tour_vallons", "puits_vallons", "etal_legumes_vallons", "etal_etoffes_vallons",
                     "tente_camp", "palissade_camp1", "palissade_camp2" })
        {
            Assert.Null(TownModels.OwnChimney(m));
            Assert.Null(TownModels.OwnLantern(m));
        }
        string game = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");
        string view = File.ReadAllText(Path.Combine(game, "SpaceView.cs"));
        int own = view.IndexOf("OwnPiece(id, points)) return;", StringComparison.Ordinal);
        Assert.True(own > 0);
        Assert.True(own < view.IndexOf("StyleFence fence = StyleRules.FenceFor(id);", StringComparison.Ordinal));
        Assert.True(own < view.IndexOf("StyleProp.Mill", StringComparison.Ordinal));
        Assert.Contains("catalog.OwnBuilding(\"moulin\") != null", File.ReadAllText(Path.Combine(game, "WorldMarkers.cs")), StringComparison.Ordinal);
        string turn = File.ReadAllText(Path.Combine(game, "MillTurn.cs"));
        Assert.Contains("Quaternion.Euler(_angle, 0f, 0f)", turn, StringComparison.Ordinal);
        Assert.Contains("TownModels.MillWaterOffset", turn, StringComparison.Ordinal);
    }
}
