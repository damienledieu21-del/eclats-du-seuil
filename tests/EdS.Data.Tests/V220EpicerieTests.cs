using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.20 : épicerie de Bertille (obstacle « maison4 », porte « porte_epicerie ») faite à l'atelier (modèle Blender d'après la fiche et la
/// décision de Damien du 10 novembre 2026), version de jeu LOD1 cuite, exportée recentrée sur le pied des murs (10,80 × 9,40) ; sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V220EpicerieTests : IDisposable
{
    private const string Id = "maison4", Name = "epicerie_vallons";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v220_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Batiments", Id);

    private AdventureSession Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a;
    }

    [Fact]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_deux_textures()
    {
        string[] files = Directory.GetFiles(Folder).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { Name + ".fbx", Name + "_couleur.jpg", Name + "_normale.png" }, files);
        string path = "Assets/EdS/Modeles/Batiments/" + Id + "/" + Name + ".fbx";
        Assert.Equal(Id, TownModels.OwnBuildingOf(path));
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
    }

    [Fact]
    public void Textures_couleur_2048_normale_1024_et_fbx_leger()
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder, Name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder, Name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder, Name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 cuit de 1 319 triangles (1 291 avant la lanterne de la V2.21), sans texture embarquée
    }

    /// <summary>
    /// Sur les vraies données de la zone : l'emprise de maison4 (12 × 13 m) a sa porte au milieu du côté nord (+z), et la façade du modèle
    /// (Y = 0 dans Blender, +z dans Unity) est tournée vers elle : la porte du modèle (X = 5,40, au milieu de la façade de 10,80 ; à 4,70 m
    /// devant le pied des murs recentré) tombe dans l'axe de la porte du jeu, entre le centre de l'emprise et elle, et l'auvent (1,80 m de
    /// plus) s'arrête avant elle.
    /// </summary>
    [Fact]
    public void La_facade_et_sa_porte_regardent_la_porte_du_jeu()
    {
        AdventureMap map = Vallons().Map;
        var o = map.Obstacles.First(x => x.Id == Id);
        double minX = o.Points.Min(p => p.X.Raw / 10000.0), maxX = o.Points.Max(p => p.X.Raw / 10000.0);
        double minZ = o.Points.Min(p => p.Z.Raw / 10000.0), maxZ = o.Points.Max(p => p.Z.Raw / 10000.0);
        Assert.Equal((12.0, 13.0), (maxX - minX, maxZ - minZ));
        (int X, int Z) facing = TownRules.FacingOf(Id, minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((0, 1), facing);
        var door = map.Doors.First(d => d.Building == Id);
        Assert.Equal("porte_epicerie", door.Id);
        double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
        // Porte du modèle : Blender (5,40 ; 0 ; 0) -> repère du FBX recentré (x = 5,40 − X = 0, z = 4,70 − Y = 4,70).
        var modelDoor = TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 4.70);
        Assert.Equal(door.X, modelDoor.X, 6);
        Assert.Equal(-9.8, modelDoor.Z, 6);
        Assert.InRange(modelDoor.Z, cz, door.Z);
        // Bord avant de l'auvent (Y = −1,80) : encore devant la façade, avant la porte du jeu (z = −7).
        var eave = TownModels.OwnPlace(cx, cz, facing, 0.0, 2.84, 4.70 + 1.80);
        Assert.InRange(eave.Z, modelDoor.Z, door.Z);
        // L'enseigne peinte se lit de dehors : la droite de la façade vue de la porte du jeu (+X de Blender, x = 5,40 − X négatif)
        // est vers −x dans le monde quand la façade regarde +z, comme pour les autres bâtiments de l'atelier.
        var right = TownModels.OwnPlace(cx, cz, facing, -5.40, 0.0, 4.70);
        Assert.Equal(cx - 5.40, right.X, 6);
    }

    [Fact]
    public void La_cheminee_et_la_lanterne_du_modele_sont_connues_depuis_la_V221()
    {
        // V2.21 (décision de Damien du 10 novembre 2026 au soir) : feu d'âtre pendant les heures d'ouverture et lanterne sous l'auvent ;
        // le détail est vérifié dans V221HerboristerieTests. L'épicerie n'a toujours pas de fournée.
        Assert.NotNull(TownModels.OwnChimney(Name));
        Assert.NotNull(TownModels.OwnLantern(Name));
        AdventureSession a = Vallons();
        Assert.DoesNotContain(a.Chimneys(), c => a.ShopBuildingOf(c.Id) == Id);
        Assert.Contains(a.KitchenChimneys(), c => a.ShopBuildingOf(c.Id) == Id);
        Assert.Equal(Id, a.ShopBuildingOf("enseigne/merchant.epicier"));
    }
}
