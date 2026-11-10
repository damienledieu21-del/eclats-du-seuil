using EdS.Runtime;
using EdS.World;
using EdS.Domain.Space;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.23 : maison de Rosalie (obstacle « maison5 », porte « porte_maison », chat roux dans le modèle) et maisons 3, 8, 9 et 10 faites à
/// l'atelier, versions de jeu LOD1 cuites, exportées recentrées sur le pied des murs, sol à 0, posées à l'échelle 1 (<see
/// cref="TownModels.OwnFit"/>). Orientation et emprises vérifiées sur les vraies données de zone ; aucune fumée (aucun feu tenu dans les
/// données de ces maisons), aucune lanterne dans les modèles. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V223MaisonsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v223_" + Guid.NewGuid().ToString("N"));

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
    [InlineData("maison5", "maison_rosalie_vallons")]
    [InlineData("maison3", "maison3_vallons")]
    [InlineData("maison8", "maison8_vallons")]
    [InlineData("maison9", "maison9_vallons")]
    [InlineData("maison10", "maison10_vallons")]
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
    [InlineData("maison5", "maison_rosalie_vallons")]
    [InlineData("maison3", "maison3_vallons")]
    [InlineData("maison8", "maison8_vallons")]
    [InlineData("maison9", "maison9_vallons")]
    [InlineData("maison10", "maison10_vallons")]
    public void Textures_couleur_2048_normale_1024_et_fbx_leger(string id, string name)
    {
        byte[] normal = File.ReadAllBytes(Path.Combine(Folder(id), name + "_normale.png"));
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(normal));
        Assert.InRange(normal.Length, 1, 4_000_000);
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(Folder(id), name + "_couleur.jpg"))));
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder(id), name + ".fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 cuits (650 à 836 triangles), sans texture embarquée
    }

    /// <summary>
    /// Sur les vraies données de la zone, pour chaque maison : emprise, façade (vers la porte du jeu pour Rosalie, vers la route la plus
    /// proche pour les autres), porte du modèle (au milieu du pied des murs de la façade : à <paramref name="front"/> m devant le centre
    /// recentré) dans l'axe du centre de l'emprise. Les coins du toit (enveloppe de la fiche, repère de Blender : X <paramref name="x0"/> –
    /// <paramref name="x1"/>, Y <paramref name="y0"/> – <paramref name="y1"/>, pied des murs centré en <paramref name="wx"/>,
    /// <paramref name="wy"/>) restent dans l'emprise, sauf du côté et de la longueur attendus (<paramref name="overflow"/>, m).
    /// </summary>
    [Theory]
    // Rosalie : parcelle de 10 × 9 (X −0,30 – 9,70), mais le modèle est recentré sur la maison (X 3,20) : le potager et sa clôture (jusqu'à
    // X 9,637) sortent de 1,437 m à l'est (+x, à droite de la façade vue de dehors). Devant : les pots s'arrêtent à Y −0,525.
    [InlineData("maison5", -89.5, -79.5, 4.0, 13.0, 0, -1, 3.20, 3.90, -0.30, 9.637, -0.525, 8.10, -84.5, 4.6, 1.437, 7.00, 9.637, 0.40, 7.50)]
    [InlineData("maison3", -35.0, -25.0, -15.0, -6.0, 0, 1, 4.70, 4.20, -0.30, 9.70, -0.30, 8.70, -30.0, -6.3, 0.0, 0.0, 0.0, 0.0, 0.0)]
    [InlineData("maison8", -99.0, -93.0, 4.0, 10.5, 0, -1, 2.70, 2.95, -0.30, 5.70, -0.30, 6.20, -96.0, 4.3, 0.0, 0.0, 0.0, 0.0, 0.0)]
    // Maison 9 : façade vers l'est (+x, bout du chemin du lac) ; l'emprise (7 × 6) est posée en long sur x, la maison (7 × 6 en façade)
    // tournée met ses 7 m sur z : l'appentis (X 4,40 – 6,70, à droite de la façade, donc vers +z) sort de 1,50 m au sud ; son coin
    // sud-ouest est à 1,70 m du bord du chemin des champs.
    [InlineData("maison9", -74.0, -67.0, -52.0, -46.0, 1, 0, 2.20, 2.70, -0.30, 6.70, -0.30, 5.70, -67.8, -49.0, 1.5, 4.40, 6.70, 0.80, 4.90)]
    [InlineData("maison10", 76.0, 83.0, -32.0, -26.0, 0, -1, 3.20, 2.70, -0.30, 6.70, -0.30, 5.70, 79.5, -31.7, 0.0, 0.0, 0.0, 0.0, 0.0)]
    public void Chaque_maison_regarde_sa_rue_et_tient_dans_son_emprise(string id, double minX, double maxX, double minZ, double maxZ, int fx, int fz,
        double wx, double wy, double x0, double x1, double y0, double y1, double doorX, double doorZ, double overflow,
        double ex0, double ex1, double ey0, double ey1)
    {
        AdventureMap map = Vallons().Map;
        Assert.Equal((minX, maxX, minZ, maxZ), Box(map.Obstacles.First(o => o.Id == id)));
        (int X, int Z) facing = TownRules.FacingOf(id, minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((fx, fz), facing);
        double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
        // Repère du FBX recentré, après l'import d'Unity (signe de x changé) : x = wx − X, z = wy − Y.
        var modelDoor = TownModels.OwnPlace(cx, cz, facing, 0.0, 0.0, wy);
        Assert.Equal((doorX, doorZ), (Math.Round(modelDoor.X, 6), Math.Round(modelDoor.Z, 6)));
        var corners = new[] { (x0, y0), (x0, y1), (x1, y0), (x1, y1) }
            .Select(c => TownModels.OwnPlace(cx, cz, facing, wx - c.Item1, 0.0, wy - c.Item2)).ToArray();
        double px0 = corners.Min(c => c.X), px1 = corners.Max(c => c.X), pz0 = corners.Min(c => c.Z), pz1 = corners.Max(c => c.Z);
        double out0 = Math.Max(Math.Max(minX - px0, px1 - maxX), Math.Max(minZ - pz0, pz1 - maxZ));
        Assert.Equal(overflow, Math.Round(Math.Max(0.0, out0), 6));
        if (overflow == 0) return;
        // Ce qui sort de l'emprise (l'élément débordant, repère de Blender X ex0 – ex1, Y ey0 – ey1 : le potager de Rosalie, l'appentis de
        // la maison 9), coupé au bord de l'emprise, ne touche aucun autre obstacle et reste à plus de 1,5 m du bord des routes.
        var e = new[] { (ex0, ey0), (ex0, ey1), (ex1, ey0), (ex1, ey1) }
            .Select(c => TownModels.OwnPlace(cx, cz, facing, wx - c.Item1, 0.0, wy - c.Item2)).ToArray();
        double ox0 = e.Min(c => c.X), ox1 = e.Max(c => c.X), oz0 = e.Min(c => c.Z), oz1 = e.Max(c => c.Z);
        if (ox1 > maxX) ox0 = Math.Max(ox0, maxX);
        if (ox0 < minX) ox1 = Math.Min(ox1, minX);
        if (oz1 > maxZ) oz0 = Math.Max(oz0, maxZ);
        if (oz0 < minZ) oz1 = Math.Min(oz1, minZ);
        Assert.Equal(overflow, Math.Round(Math.Max(Math.Max(ox1 - maxX, minX - ox0), Math.Max(oz1 - maxZ, minZ - oz0)), 6));
        foreach (var other in map.Obstacles.Where(o => o.Id != id))
        {
            var (bx0, bx1, bz0, bz1) = Box(other);
            Assert.False(bx0 < ox1 && bx1 > ox0 && bz0 < oz1 && bz1 > oz0, other.Id);
        }
        foreach (StyleRoad road in map.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
                foreach (var (x, z) in new[] { (ox0, oz0), (ox0, oz1), (ox1, oz0), (ox1, oz1) })
                    Assert.True(SegmentDistance(x, z, road.Points[i], road.Points[i + 1]) > road.Width / 2 + 1.5, road.Id);
    }

    /// <summary>
    /// Maison de Rosalie : la porte P1 (X 3,20) tombe dans l'axe de la porte du jeu (x −84,5, z 3), entre elle et le centre de l'emprise ;
    /// le potager (à droite de la façade vue de dehors, X 7,00 – 9,50) est à l'est (+x), comme le côté droit de la fiche, et le chat roux
    /// (centre X 6,352, Y 2,02, à 1 m du sol) au rebord de la fenêtre avant du mur droit reste dans l'emprise.
    /// </summary>
    [Fact]
    public void Rosalie_sa_porte_son_potager_a_droite_et_son_chat()
    {
        AdventureMap map = Vallons().Map;
        var door = map.Doors.First(d => d.Building == "maison5");
        Assert.Equal("porte_maison", door.Id);
        Assert.Equal((-84.5, 3.0), (door.X, door.Z));
        (int X, int Z) facing = TownRules.FacingOf("maison5", -89.5, 4.0, -79.5, 13.0, map.Roads, map.Doors);
        var p1 = TownModels.OwnPlace(-84.5, 8.5, facing, 3.20 - 3.20, 0.0, 3.90);
        Assert.Equal(door.X, p1.X, 6);
        Assert.InRange(p1.Z, door.Z, 8.5);
        var garden = TownModels.OwnPlace(-84.5, 8.5, facing, 3.20 - 8.25, 0.0, 3.90 - 4.0);
        Assert.True(garden.X > -84.5);
        // Vu de dehors (devant la porte, regard vers +z), la droite est +x dans Unity.
        Assert.Equal((-79.45, 4.6), (Math.Round(garden.X, 6), Math.Round(TownModels.OwnPlace(-84.5, 8.5, facing, 0.0, 0.0, 3.90).Z, 6)));
        var cat = TownModels.OwnPlace(-84.5, 8.5, facing, 3.20 - 6.352, 1.0, 3.90 - 2.02);
        Assert.Equal((-81.348, 1.0, 6.62), (Math.Round(cat.X, 6), Math.Round(cat.Y, 6), Math.Round(cat.Z, 6)));
        Assert.InRange(cat.X, -89.5, -79.5);
        Assert.InRange(cat.Z, 4.0, 13.0);
    }

    /// <summary>
    /// Aucune de ces maisons n'a de feu tenu dans les données (aucune boutique : ni fournée, ni cuisine, ni âtre, ni tisanes) : pas de
    /// cheminée connue, rien dans Chimneys ni KitchenChimneys pour elles, donc pas de fumée. Pas de lanterne dans leurs modèles : leur
    /// lumière de porte reste celle d'OwnDoorLantern.
    /// </summary>
    [Fact]
    public void Ni_fumee_ni_lanterne_pour_ces_maisons()
    {
        string[] models = { "maison_rosalie_vallons", "maison3_vallons", "maison8_vallons", "maison9_vallons", "maison10_vallons" };
        foreach (string m in models)
        {
            Assert.Null(TownModels.OwnChimney(m));
            Assert.Null(TownModels.OwnLantern(m));
            Assert.False(TownModels.OwnLanternAlwaysLit(m));
        }
        AdventureSession a = Vallons();
        foreach (ShopChimney c in a.Chimneys().Concat(a.KitchenChimneys()))
            Assert.DoesNotContain(a.ShopBuildingOf(c.Id), new[] { "maison3", "maison5", "maison8", "maison9", "maison10" });
    }

    private static double SegmentDistance(double px, double pz, (double X, double Z) a, (double X, double Z) b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, l2 = dx * dx + dz * dz;
        double t = l2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((px - a.X) * dx + (pz - a.Z) * dz) / l2));
        double qx = a.X + t * dx - px, qz = a.Z + t * dz - pz;
        return Math.Sqrt(qx * qx + qz * qz);
    }
}
