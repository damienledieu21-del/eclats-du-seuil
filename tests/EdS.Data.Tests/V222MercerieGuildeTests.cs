using EdS.Runtime;
using EdS.Data.Saves;
using EdS.World;
using EdS.Domain.Space;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.22 : mercerie de Margot (obstacle « maison1 », porte « porte_mercerie ») et maison de la Guilde (obstacle « maison6 », porte
/// « porte_guilde ») faites à l'atelier, versions de jeu LOD1 cuites, exportées recentrées sur le pied des murs ; lanterne de la
/// mercerie ; feu de tisanes permanent et léger de Sauge (décision de Damien du 10 novembre 2026 au soir) ; ordre des huit lumières
/// (relecture de la PR #140). Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V222MercerieGuildeTests : IDisposable
{
    private const string Mercerie = "mercerie_vallons", Guilde = "guilde_vallons", Sauge = "herboristerie_sauge";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v222_" + Guid.NewGuid().ToString("N"));

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

    private static (double MinX, double MaxX, double MinZ, double MaxZ) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 10000.0), o.Points.Max(p => p.X.Raw / 10000.0), o.Points.Min(p => p.Z.Raw / 10000.0), o.Points.Max(p => p.Z.Raw / 10000.0));

    [Theory]
    [InlineData("maison1", "mercerie_vallons")]
    [InlineData("maison6", "guilde_vallons")]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_deux_textures(string id, string name)
    {
        string[] files = Directory.GetFiles(Folder(id)).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { name + ".fbx", name + "_couleur.jpg", name + "_normale.png" }, files);
        string path = "Assets/EdS/Modeles/Batiments/" + id + "/" + name + ".fbx";
        Assert.Equal(id, TownModels.OwnBuildingOf(path));
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
    }

    [Theory]
    [InlineData("maison1", "mercerie_vallons")]
    [InlineData("maison6", "guilde_vallons")]
    public void Textures_couleur_2048_normale_1024_et_fbx_leger(string id, string name)
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder(id), name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder(id), name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder(id), name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 cuits (807 et 972 triangles), sans texture embarquée
    }

    /// <summary>
    /// Sur les vraies données de la zone : l'emprise de maison1 (7 × 6,5 m, X −32 – −25, Z 4 – 10,5) a sa porte au milieu du côté nord
    /// (−z, vers la grand-rue), et la façade du modèle (Y = 0 dans Blender, +z dans Unity) est tournée vers elle : la porte P1 du modèle
    /// (X = 3,20, au milieu de la façade de 6,40 ; à 2,90 m devant le pied des murs recentré) tombe dans l'axe de la porte du jeu, entre
    /// le centre de l'emprise et elle ; le mur du fond (Y = 5,80) et la porte arrière P2 restent dans l'emprise.
    /// </summary>
    [Fact]
    public void La_facade_de_la_mercerie_regarde_la_porte_du_jeu()
    {
        AdventureMap map = Vallons().Map;
        var (minX, maxX, minZ, maxZ) = Box(map.Obstacles.First(x => x.Id == "maison1"));
        Assert.Equal((-32.0, -25.0, 4.0, 10.5), (minX, maxX, minZ, maxZ));
        (int X, int Z) facing = TownRules.FacingOf("maison1", minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((0, -1), facing);
        var door = map.Doors.First(d => d.Building == "maison1");
        Assert.Equal("porte_mercerie", door.Id);
        Assert.Equal((-28.5, 3.0), (door.X, door.Z));
        double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
        // Porte du modèle : Blender (3,20 ; 0 ; 0) -> repère du FBX recentré (x = 3,20 − X = 0, z = 2,90 − Y = 2,90).
        var modelDoor = TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 2.90);
        Assert.Equal(door.X, modelDoor.X, 6);
        Assert.Equal(4.35, modelDoor.Z, 6);
        Assert.InRange(modelDoor.Z, door.Z, cz);
        // Mur du fond (Y = 5,80) : dans l'emprise.
        Assert.Equal(10.15, TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 2.90 - 5.80).Z, 6);
        // L'enseigne en ciseaux (Y −0,39, le plus en avant) s'arrête avant la porte du jeu.
        Assert.InRange(TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 2.90 + 0.39).Z, door.Z, minZ);
    }

    /// <summary>
    /// Sur les vraies données de la zone : l'emprise de maison6 (14 × 13 m, X −98 – −84, Z −20 – −7) a sa porte au milieu du côté sud
    /// (+z, vers la grand-rue ouest), et la façade du modèle est tournée vers elle : la porte principale (X = 6,60, au milieu de la
    /// façade de 13,20 ; à 5,00 m devant le pied des murs recentré) tombe dans l'axe de la porte du jeu, entre le centre de l'emprise et
    /// elle. Le perron (Y −2,60 – 0, X 5,00 – 8,20) sort de l'emprise vers la porte : c'est voulu. Il s'arrête à 0,10 m au-delà du point
    /// de la porte du jeu, sur une place libre (aucun obstacle, la grand-rue ouest à plus de 2 m).
    /// </summary>
    [Fact]
    public void La_facade_de_la_Guilde_et_son_perron_regardent_la_porte_du_jeu()
    {
        AdventureMap map = Vallons().Map;
        var (minX, maxX, minZ, maxZ) = Box(map.Obstacles.First(x => x.Id == "maison6"));
        Assert.Equal((-98.0, -84.0, -20.0, -7.0), (minX, maxX, minZ, maxZ));
        (int X, int Z) facing = TownRules.FacingOf("maison6", minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((0, 1), facing);
        var door = map.Doors.First(d => d.Building == "maison6");
        Assert.Equal("porte_guilde", door.Id);
        Assert.Equal((-91.0, -6.0), (door.X, door.Z));
        double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
        // Porte du modèle : Blender (6,60 ; 0 ; 0) -> repère du FBX recentré (x = 6,60 − X = 0, z = 5,00 − Y = 5,00).
        var modelDoor = TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 5.00);
        Assert.Equal(door.X, modelDoor.X, 6);
        Assert.Equal(-8.5, modelDoor.Z, 6);
        Assert.InRange(modelDoor.Z, cz, door.Z);
        // Débord arrière du toit (Y 10,40) et rives (X −0,40 et 13,60) : dans l'emprise.
        Assert.Equal(-18.9, TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, 5.00 - 10.40).Z, 6);
        Assert.Equal(-84.0, Math.Max(TownModels.OwnPlace(cx, cz, facing, 6.60 + 0.40, 0.0, 0.0).X, TownModels.OwnPlace(cx, cz, facing, 6.60 - 13.60, 0.0, 0.0).X), 6);
        // Perron : de Y −2,60 à 0, joues X 5,00 – 8,20.
        var a = TownModels.OwnPlace(cx, cz, facing, 6.60 - 5.00, 0.0, 5.00 + 2.60);
        var b = TownModels.OwnPlace(cx, cz, facing, 6.60 - 8.20, 0.0, 5.00);
        double px0 = Math.Min(a.X, b.X), px1 = Math.Max(a.X, b.X), pz0 = Math.Min(a.Z, b.Z), pz1 = Math.Max(a.Z, b.Z);
        Assert.Equal((-92.6, -89.4, -8.5, -5.9), (Math.Round(px0, 6), Math.Round(px1, 6), Math.Round(pz0, 6), Math.Round(pz1, 6)));
        Assert.True(pz1 > maxZ);                       // il sort de l'emprise, vers la porte : voulu
        Assert.InRange(door.X, px0, px1);              // la porte du jeu est dans l'axe du perron
        foreach (var other in map.Obstacles.Where(x => x.Id != "maison6"))
        {
            var (ox0, ox1, oz0, oz1) = Box(other);
            Assert.False(ox0 < px1 && ox1 > px0 && oz0 < pz1 && oz1 > pz0, other.Id);
        }
        foreach (StyleRoad road in map.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
                foreach (var (x, z) in new[] { (px0, pz1), (px1, pz1) })
                    Assert.True(SegmentDistance(x, z, road.Points[i], road.Points[i + 1]) > road.Width / 2 + 2.0, road.Id);
    }

    private static double SegmentDistance(double px, double pz, (double X, double Z) a, (double X, double Z) b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, l2 = dx * dx + dz * dz;
        double t = l2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((px - a.X) * dx + (pz - a.Z) * dz) / l2));
        double qx = a.X + t * dx - px, qz = a.Z + t * dz - pz;
        return Math.Sqrt(qx * qx + qz * qz);
    }

    /// <summary>
    /// La lanterne murale de la mercerie (Lumiere_Lanterne : X 4,00, Y −0,22, Z 1,925) est à 0,80 m à droite de la porte P1 vue de
    /// dehors, 0,22 m devant la façade ; elle s'allume au crépuscule comme les autres. La Guilde n'a pas de lanterne dans son modèle
    /// (fiche : blason et bannières seulement) : sa lumière de porte reste celle d'OwnDoorLantern.
    /// </summary>
    [Fact]
    public void La_lanterne_de_la_mercerie_est_a_droite_de_sa_porte()
    {
        var local = TownModels.OwnLantern(Mercerie);
        Assert.NotNull(local);
        foreach ((int fx, int fz) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
        {
            var w = TownModels.OwnPlace(10, 20, (fx, fz), local!.Value.X, local.Value.Y, local.Value.Z);
            Assert.Equal(2.90 + 0.22, (w.X - 10) * fx + (w.Z - 20) * fz, 6);
            Assert.Equal(0.80, (w.X - 10) * -fz + (w.Z - 20) * fx, 6);
            Assert.Equal(1.925, w.Y, 6);
        }
        // Sur la vraie emprise (façade vers −z) : x −27,70, z 4,13.
        var real = TownModels.OwnPlace(-28.5, 7.25, (0, -1), local!.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal((-27.7, 4.13), (Math.Round(real.X, 6), Math.Round(real.Z, 6)));
        Assert.False(TownModels.OwnLanternAlwaysLit(Mercerie));
        Assert.Null(TownModels.OwnLantern(Guilde));
        Assert.False(TownModels.OwnLanternAlwaysLit(Guilde));
    }

    /// <summary>
    /// Ni la mercerie (aucun feu dans son métier) ni la Guilde (aucune boutique, aucune heure, aucun âtre dans ses données) n'ont de feu
    /// tenu : pas de cheminée connue, rien dans Chimneys ni KitchenChimneys pour leurs maisons, donc pas de fumée.
    /// </summary>
    [Fact]
    public void Ni_la_mercerie_ni_la_Guilde_ne_fument()
    {
        Assert.Null(TownModels.OwnChimney(Mercerie));
        Assert.Null(TownModels.OwnChimney(Guilde));
        AdventureSession a = Vallons();
        foreach (ShopChimney c in a.Chimneys().Concat(a.KitchenChimneys()))
            Assert.DoesNotContain(a.ShopBuildingOf(c.Id), new[] { "maison1", "maison6" });
        Assert.Equal("maison1", a.ShopBuildingOf("enseigne/merchant.mercerie"));
    }

    /// <summary>
    /// Feu de tisanes de Sauge : cheminée de la fiche à X 2,25, Y 4,20, sommet Z 6,05 (origine au coin avant gauche) ; dans le repère
    /// du modèle recentré (X − 2,25, Y − 2,60) : x 0, y 6,05, z −1,60, soit 1,60 m derrière le centre, dans l'axe de la porte.
    /// </summary>
    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    public void La_fumee_de_Sauge_sort_de_sa_cheminee(int fx, int fz)
    {
        var local = TownModels.OwnChimney(Sauge);
        Assert.NotNull(local);
        Assert.Equal((0.0, 6.05, -1.60), (Math.Round(local!.Value.X, 6), Math.Round(local.Value.Y, 6), Math.Round(local.Value.Z, 6)));
        var w = TownModels.OwnPlace(10, 20, (fx, fz), local.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal(-1.60, (w.X - 10) * fx + (w.Z - 20) * fz, 6);
        Assert.Equal(0.0, (w.X - 10) * -fz + (w.Z - 20) * fx, 6);
        Assert.Equal(6.05, w.Y, 6);
    }

    /// <summary>
    /// Heure du monde fixée et attente écrite en dur : le feu de tisanes de Sauge brûle à toute heure, tous les jours (fumée
    /// permanente), et sa fumée est légère (0,35) ; les autres feux gardent leur fumée pleine.
    /// </summary>
    [Theory]
    [InlineData(7, 3, 0)]
    [InlineData(1, 4, 30)]
    [InlineData(1, 12, 0)]
    [InlineData(1, 23, 30)]
    [InlineData(6, 12, 0)]
    public void Le_feu_de_tisanes_de_Sauge_brule_a_toute_heure(long day, int hour, int minute)
    {
        AdventureSession a = VallonsAt(day, hour, minute);
        ShopChimney c = Assert.Single(a.KitchenChimneys(), x => x.Id.StartsWith("tisane/", StringComparison.Ordinal));
        Assert.Equal("tisane/merchant.herboriste", c.Id);
        Assert.Equal("maison7", a.ShopBuildingOf(c.Id));
        Assert.Equal(-54.75, c.X, 6);
        Assert.Equal(-20.75, c.Z, 6);
        Assert.True(c.Lit);
        Assert.Equal(0.35, TownModels.SmokeStrength(c.Id), 6);
        Assert.Equal(1.0, TownModels.SmokeStrength("atre/merchant.epicier"), 6);
        Assert.Equal(1.0, TownModels.SmokeStrength("cuisine/merchant.auberge"), 6);
        Assert.Equal(1.0, TownModels.SmokeStrength("cheminee/merchant.boulangerie"), 6);
        Assert.Equal(1.0, TownModels.SmokeStrength(null), 6);
    }

    /// <summary>
    /// Relecture de la PR #140 : le jour (et au crépuscule), la lanterne toujours allumée prend une des huit lumières même si huit
    /// autres lanternes, éteintes, sont plus proches ; la nuit, seule la distance compte ; à égalité, l'ordre des indices.
    /// </summary>
    [Fact]
    public void Les_lanternes_toujours_allumees_passent_d_abord_le_jour()
    {
        double[] d = { 1, 4, 9, 16, 25, 36, 49, 64, 81, 400 };
        bool[] always = { false, false, false, false, false, false, false, false, false, true };
        Assert.Equal(new[] { 9, 0, 1, 2, 3, 4, 5, 6 }, TownModels.LampOrder(d, always, 0.0, 8));
        Assert.Equal(new[] { 9, 0, 1, 2, 3, 4, 5, 6 }, TownModels.LampOrder(d, always, 0.5, 8));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, TownModels.LampOrder(d, always, 1.0, 8));
        Assert.Equal(new[] { 1, 0, 2 }, TownModels.LampOrder(new double[] { 4, 1, 4 }, new[] { false, false, false }, 1.0, 8));
        Assert.Equal(new[] { 2, 1 }, TownModels.LampOrder(new double[] { 4, 1, 9 }, new[] { false, false, true }, 0.0, 2));
        Assert.Empty(TownModels.LampOrder(Array.Empty<double>(), Array.Empty<bool>(), 0.0, 8));
        Assert.Throws<ArgumentException>(() => TownModels.LampOrder(new double[] { 1 }, Array.Empty<bool>(), 0.0, 8));
    }
}
