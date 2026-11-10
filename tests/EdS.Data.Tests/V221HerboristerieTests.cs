using EdS.Runtime;
using EdS.Data.Saves;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.21 : herboristerie de Sauge (obstacle « maison7 », porte « porte_herboristerie ») faite à l'atelier, version de jeu LOD1 cuite,
/// exportée recentrée sur le pied des murs (4,50 × 5,20) ; épicerie de Bertille avec sa lanterne et le feu d'âtre de sa cheminée pendant
/// les heures d'ouverture (décision de Damien du 10 novembre 2026 au soir) ; lanterne de Sauge toujours allumée. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V221HerboristerieTests : IDisposable
{
    private const string Id = "maison7", Name = "herboristerie_sauge", Epicerie = "epicerie_vallons";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v221_" + Guid.NewGuid().ToString("N"));

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

    /// <summary>Vallons au jour <paramref name="day"/> à <paramref name="hour"/>:<paramref name="minute"/> (heure du monde fixée).</summary>
    private AdventureSession VallonsAt(long day, int hour, int minute)
    {
        Directory.CreateDirectory(_folder);
        string save = Path.Combine(_folder, "partie.json");
        int at = hour * 60 + minute;
        SaveStore.Write(save, new WorldSave("zone.vallons", V214BoutiquesMarcheTests.TickAt(day, at) - 1, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), save);
        a.Advance(0.05);
        Assert.Equal(new WorldTime(day, at), a.World.Zone.Time);
        return a;
    }

    [Fact]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_deux_textures()
    {
        string[] files = Directory.GetFiles(Folder(Id)).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { Name + ".fbx", Name + "_couleur.jpg", Name + "_normale.png" }, files);
        string path = "Assets/EdS/Modeles/Batiments/" + Id + "/" + Name + ".fbx";
        Assert.Equal(Id, TownModels.OwnBuildingOf(path));
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
    }

    [Theory]
    [InlineData("maison7", "herboristerie_sauge")]
    [InlineData("maison4", "epicerie_vallons")]
    public void Textures_couleur_2048_normale_1024_et_fbx_leger(string id, string name)
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder(id), name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder(id), name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder(id), name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 cuits (1 066 et 1 319 triangles), sans texture embarquée
    }

    /// <summary>
    /// Sur les vraies données de la zone : l'emprise de maison7 (6,5 × 6,5 m, X −58 – −51,5, Z −24 – −17,5) a sa porte au milieu du côté
    /// ouest (−x, vers le chemin des champs), et la façade du modèle (Y = 0 dans Blender, +z dans Unity) est tournée vers elle : la porte du
    /// modèle (X = 2,25, au milieu de la façade de 4,50 ; à 2,60 m devant le pied des murs recentré) tombe dans l'axe de la porte du jeu,
    /// entre le centre de l'emprise et elle.
    /// </summary>
    [Fact]
    public void La_facade_et_sa_porte_regardent_la_porte_du_jeu()
    {
        AdventureMap map = Vallons().Map;
        var o = map.Obstacles.First(x => x.Id == Id);
        double minX = o.Points.Min(p => p.X.Raw / 10000.0), maxX = o.Points.Max(p => p.X.Raw / 10000.0);
        double minZ = o.Points.Min(p => p.Z.Raw / 10000.0), maxZ = o.Points.Max(p => p.Z.Raw / 10000.0);
        Assert.Equal((-58.0, -51.5, -24.0, -17.5), (minX, maxX, minZ, maxZ));
        (int X, int Z) facing = TownRules.FacingOf(Id, minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((-1, 0), facing);
        var door = map.Doors.First(d => d.Building == Id);
        Assert.Equal("porte_herboristerie", door.Id);
        Assert.Equal((-59.0, -20.75), (door.X, door.Z));
        double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
        // Porte du modèle : Blender (2,25 ; 0 ; 0) -> repère du FBX recentré (x = 2,25 − X = 0, z = 2,60 − Y = 2,60).
        var modelDoor = TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 2.60);
        Assert.Equal(-57.35, modelDoor.X, 6);
        Assert.Equal(door.Z, modelDoor.Z, 6);
        Assert.InRange(modelDoor.X, door.X, cx);
        // Le mur du fond (Y = 5,20) reste dans l'emprise.
        Assert.Equal(-52.15, TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 2.60 - 5.20).X, 6);
        // L'enseigne se lit de dehors : la droite de la façade vue de la porte du jeu (+X de Blender : x = 2,25 − X négatif) est vers −z
        // dans le monde quand la façade regarde −x (la droite vue de dehors est (−fz, fx), comme TownModels.OwnDoorLantern).
        var right = TownModels.OwnPlace(cx, cz, facing, -2.25, 0.0, 2.60);
        Assert.Equal(cz - 2.25, right.Z, 6);
    }

    /// <summary>
    /// À l'échelle 1, le jardin de Sauge (deux plates-bandes à droite de la maison : X 5,05 – 6,15, Y 0,50 – 4,50 dans Blender) dépasse
    /// de 0,65 m l'emprise vers le sud (−z) : c'est voulu, et la place est libre (aucun autre obstacle, le chemin des champs est loin).
    /// </summary>
    [Fact]
    public void Le_jardin_deborde_au_sud_sur_une_place_libre()
    {
        AdventureMap map = Vallons().Map;
        double cx = -54.75, cz = -20.75;
        var a = TownModels.OwnPlace(cx, cz, (-1, 0), 2.25 - 6.15, 0.0, 2.60 - 0.50);
        var b = TownModels.OwnPlace(cx, cz, (-1, 0), 2.25 - 5.05, 0.0, 2.60 - 4.50);
        double gx0 = Math.Min(a.X, b.X), gx1 = Math.Max(a.X, b.X), gz0 = Math.Min(a.Z, b.Z), gz1 = Math.Max(a.Z, b.Z);
        Assert.Equal((-56.85, -52.85, -24.65, -23.55), (Math.Round(gx0, 6), Math.Round(gx1, 6), Math.Round(gz0, 6), Math.Round(gz1, 6)));
        foreach (var other in map.Obstacles.Where(x => x.Id != Id))
        {
            double ox0 = other.Points.Min(p => p.X.Raw / 10000.0), ox1 = other.Points.Max(p => p.X.Raw / 10000.0);
            double oz0 = other.Points.Min(p => p.Z.Raw / 10000.0), oz1 = other.Points.Max(p => p.Z.Raw / 10000.0);
            Assert.False(ox0 < gx1 && ox1 > gx0 && oz0 < gz1 && oz1 > gz0, other.Id);
        }
        foreach (StyleRoad road in map.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
                foreach (var (px, pz) in new[] { (gx0, gz0), (gx0, gz1), (gx1, gz0), (gx1, gz1) })
                    Assert.True(SegmentDistance(px, pz, road.Points[i], road.Points[i + 1]) > road.Width / 2 + 0.5, road.Id);
    }

    private static double SegmentDistance(double px, double pz, (double X, double Z) a, (double X, double Z) b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, l2 = dx * dx + dz * dz;
        double t = l2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((px - a.X) * dx + (pz - a.Z) * dz) / l2));
        double qx = a.X + t * dx - px, qz = a.Z + t * dz - pz;
        return Math.Sqrt(qx * qx + qz * qz);
    }

    /// <summary>Lanternes connues : Sauge à 1,05 m à droite de sa porte, l'épicerie à 1,35 m à droite de la sienne, sous l'auvent.</summary>
    [Theory]
    [InlineData("herboristerie_sauge", 1.05, 1.925, 2.82)]
    [InlineData("epicerie_vallons", 1.35, 1.99, 5.00)]
    public void Les_lanternes_sont_a_droite_de_la_porte_devant_la_facade(string model, double right, double y, double ahead)
    {
        var local = TownModels.OwnLantern(model);
        Assert.NotNull(local);
        foreach ((int fx, int fz) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
        {
            var w = TownModels.OwnPlace(10, 20, (fx, fz), local!.Value.X, local.Value.Y, local.Value.Z);
            Assert.Equal(ahead, (w.X - 10) * fx + (w.Z - 20) * fz, 6);
            Assert.Equal(right, (w.X - 10) * -fz + (w.Z - 20) * fx, 6);
            Assert.Equal(y, w.Y, 6);
        }
    }

    [Fact]
    public void La_lanterne_de_Sauge_reste_allumee_jour_et_nuit()
    {
        Assert.True(TownModels.OwnLanternAlwaysLit("herboristerie_sauge"));
        Assert.True(TownModels.OwnLanternAlwaysLit("Herboristerie_Sauge"));
        Assert.False(TownModels.OwnLanternAlwaysLit("epicerie_vallons"));
        Assert.False(TownModels.OwnLanternAlwaysLit("auberge_vallons"));
        Assert.False(TownModels.OwnLanternAlwaysLit(null));
        // En plein jour (lanternes du monde à 0), la sienne éclaire pleinement ; les autres suivent le crépuscule.
        Assert.Equal(1.0, TownModels.LanternLevel(true, 0.0), 6);
        Assert.Equal(1.0, TownModels.LanternLevel(true, 0.4), 6);
        Assert.Equal(0.0, TownModels.LanternLevel(false, 0.0), 6);
        Assert.Equal(0.4, TownModels.LanternLevel(false, 0.4), 6);
        Assert.Equal(1.0, TownModels.LanternLevel(false, 1.0), 6);
    }

    /// <summary>
    /// La fumée de l'épicerie sort de son mitron : 3,50 m à droite de la porte vue de dehors, au milieu de la profondeur (sur le faîtage),
    /// à 11,55 m. L'herboristerie n'a pas de feu tenu (ni fournée, ni cuisine, ni âtre décidé) : pas de cheminée connue, donc pas de fumée.
    /// </summary>
    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    public void La_fumee_de_l_epicerie_sort_de_son_mitron(int fx, int fz)
    {
        var local = TownModels.OwnChimney(Epicerie);
        Assert.NotNull(local);
        var w = TownModels.OwnPlace(10, 20, (fx, fz), local!.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal(0.0, (w.X - 10) * fx + (w.Z - 20) * fz, 6);
        Assert.Equal(3.50, (w.X - 10) * -fz + (w.Z - 20) * fx, 6);
        Assert.Equal(11.55, w.Y, 6);
        Assert.Null(TownModels.OwnChimney(Name));
    }

    /// <summary>
    /// Heure du monde fixée et attente écrite en dur, sans relire l'implémentation : l'épicerie ouvre à 05:00 et ferme à 22:00, repos le
    /// jour 6 de la semaine (merchants.json) ; son âtre fume à 05:00, 12:00 et 21:30 le jour 1, pas à 04:30 ni à 22:00, pas le jour 6.
    /// </summary>
    [Theory]
    [InlineData(1, 4, 30, false)]
    [InlineData(1, 5, 0, true)]
    [InlineData(1, 12, 0, true)]
    [InlineData(1, 21, 30, true)]
    [InlineData(1, 22, 0, false)]
    [InlineData(6, 12, 0, false)]
    public void L_atre_de_l_epicerie_fume_pendant_l_ouverture(long day, int hour, int minute, bool lit)
    {
        AdventureSession a = VallonsAt(day, hour, minute);
        ShopChimney c = Assert.Single(a.KitchenChimneys(), x => x.Id.StartsWith("atre/", StringComparison.Ordinal));
        Assert.Equal("atre/merchant.epicier", c.Id);
        Assert.Equal("maison4", a.ShopBuildingOf(c.Id));
        Assert.Equal(-43, c.X, 6);
        Assert.Equal(-14.5, c.Z, 6);
        Assert.Equal(lit, c.Lit);
        // Ni l'herboristerie ni la mercerie n'ont de feu tenu ; les fournils ne changent pas.
        Assert.Equal(new[] { "atre/merchant.epicier", "cuisine/merchant.auberge" }, a.KitchenChimneys().Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal));
        Assert.DoesNotContain(a.Chimneys(), x => a.ShopBuildingOf(x.Id) == "maison4" || a.ShopBuildingOf(x.Id) == Id);
    }
}
