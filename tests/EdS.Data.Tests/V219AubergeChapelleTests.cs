using EdS.Runtime;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.19 : auberge et chapelle des Vallons faites à l'atelier (modèles Blender validés par Damien le 10 novembre 2026), versions de jeu
/// LOD1 cuites, exportées recentrées sur le pied des murs ; sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V219AubergeChapelleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v219_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Folder(string id) => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Batiments", id);

    [Theory]
    [InlineData("auberge", "auberge_vallons")]
    [InlineData("chapelle", "chapelle_vallons")]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_deux_textures(string id, string name)
    {
        string[] files = Directory.GetFiles(Folder(id)).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { name + ".fbx", name + "_couleur.jpg", name + "_normale.png" }, files);
        string path = "Assets/EdS/Modeles/Batiments/" + id + "/" + name + ".fbx";
        Assert.Equal(id, TownModels.OwnBuildingOf(path));
        // Le dossier « auberge » de l'atelier n'est pas le genre « Auberge » des modèles de Sketchfab.
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
        Assert.Equal(StyleBuilding.Inn, TownModels.KindOfModel("Assets/Style/Sketchfab/Batiments/Auberge/Olosta/Olosta.fbx"));
    }

    [Theory]
    [InlineData("auberge", "auberge_vallons")]
    [InlineData("chapelle", "chapelle_vallons")]
    public void Textures_couleur_2048_normale_1024_et_fbx_leger(string id, string name)
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder(id), name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));   // réduite : en 2048 elle passait 5 Mo
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder(id), name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder(id), name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 de 1 282 et 1 004 triangles, sans texture embarquée
    }

    /// <summary>
    /// Les portes du jeu : l'auberge (12 × 9 m) a la sienne au milieu du grand côté sud, la chapelle (8 × 14 m) au milieu de son petit
    /// côté nord, comme les façades des modèles.
    /// </summary>
    [Theory]
    [InlineData("auberge", 0, -1, 12.0, 9.0)]
    [InlineData("chapelle", 0, 1, 8.0, 14.0)]
    public void La_facade_regarde_la_porte_du_jeu(string id, int fx, int fz, double along, double deep)
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        AdventureMap map = a.Map;
        var o = map.Obstacles.First(x => x.Id == id);
        double minX = o.Points.Min(p => p.X.Raw / 10000.0), maxX = o.Points.Max(p => p.X.Raw / 10000.0);
        double minZ = o.Points.Min(p => p.Z.Raw / 10000.0), maxZ = o.Points.Max(p => p.Z.Raw / 10000.0);
        Assert.Equal((fx, fz), TownRules.FacingOf(id, minX, minZ, maxX, maxZ, map.Roads, map.Doors));
        // Façade le long de x : sa largeur et la profondeur de l'emprise.
        Assert.Equal(along, maxX - minX, 6);
        Assert.Equal(deep, maxZ - minZ, 6);
        // La porte du jeu est au milieu de la façade, comme celle du modèle (centrée sur la largeur, à X = 5,75 et 3,60 dans Blender).
        var door = map.Doors.First(d => d.Building == id);
        Assert.Equal((minX + maxX) / 2, door.X, 6);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    public void La_lanterne_de_l_auberge_est_au_dessus_de_la_porte_devant_la_facade(int fx, int fz)
    {
        var local = TownModels.OwnLantern("auberge_vallons");
        Assert.NotNull(local);
        Assert.Equal(3.25, local!.Value.Y, 6);
        var w = TownModels.OwnPlace(10, 20, (fx, fz), local.Value.X, local.Value.Y, local.Value.Z);
        // Dans l'axe de la porte (milieu de la façade) et à 4,55 m du centre, du côté de la façade.
        double ahead = (w.X - 10) * fx + (w.Z - 20) * fz, aside = (w.X - 10) * -fz + (w.Z - 20) * fx;
        Assert.Equal(4.55, ahead, 6);
        Assert.Equal(0, aside, 6);
        Assert.Equal(3.25, w.Y, 6);
        Assert.Null(TownModels.OwnLantern("chapelle_vallons"));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    public void La_fumee_sort_de_la_cheminee_de_la_cuisine(int fx, int fz)
    {
        var local = TownModels.OwnChimney("Auberge_Vallons");
        Assert.NotNull(local);
        var w = TownModels.OwnPlace(10, 20, (fx, fz), local!.Value.X, local.Value.Y, local.Value.Z);
        // Cuisine à droite de la porte vue de dehors (1,65 m) et vers l'arrière (2 m derrière le centre), sommet à 13,45 m.
        double ahead = (w.X - 10) * fx + (w.Z - 20) * fz;
        // La droite quand on regarde la façade de dehors (comme TownModels.OwnDoorLantern) : (−fz, fx).
        double right = (w.X - 10) * -fz + (w.Z - 20) * fx;
        Assert.Equal(-2.0, ahead, 6);
        Assert.Equal(1.65, right, 6);
        Assert.Equal(13.45, w.Y, 6);
        Assert.Null(TownModels.OwnChimney("chapelle_vallons"));   // la chapelle n'a pas de cheminée
    }

    /// <summary>
    /// Heure du monde fixée (jour 1) et attente écrite en dur, sans relire l'implémentation : l'auberge ouvre à 06:00 et ferme à 01:00
    /// (merchants.json, aucun jour de repos) ; sa cuisine fume encore à 00:30, plus à 03:00, de nouveau à 06:00.
    /// </summary>
    [Theory]
    [InlineData(0, 30, true)]
    [InlineData(3, 0, false)]
    [InlineData(6, 0, true)]
    public void La_cuisine_de_l_auberge_fume_quand_elle_est_ouverte(int hour, int minute, bool lit)
    {
        Directory.CreateDirectory(_folder);
        string save = Path.Combine(_folder, "partie.json");
        int at = hour * 60 + minute;
        SaveStore.Write(save, new WorldSave("zone.vallons", V214BoutiquesMarcheTests.TickAt(1, at) - 1, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), save);
        a.Advance(0.05);
        Assert.Equal(new WorldTime(1, at), a.World.Zone.Time);
        ShopChimney c = Assert.Single(a.KitchenChimneys());
        Assert.Equal("cuisine/merchant.auberge", c.Id);
        Assert.Equal("auberge", a.ShopBuildingOf(c.Id));
        Assert.Equal(-69, c.X, 6);
        Assert.Equal(9.5, c.Z, 6);
        Assert.Equal(lit, c.Lit);
        Assert.DoesNotContain(a.Chimneys(), x => x.Id == c.Id);   // les cheminées des fournils ne changent pas
    }
}
