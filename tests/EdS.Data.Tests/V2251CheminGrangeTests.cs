using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.25.1 (décision de Damien du 11 novembre 2026 : « faut que la grange ait un chemin pour aller jusqu'à sa porte ») : le chemin de la
/// grange part du chemin des champs et arrive dans l'axe de la porte charretière (façade sud, +z), sur une aire de terre battue. Deux routes
/// de terre de la zone (<c>data.roads</c>, schéma de zone v12 inchangé) : données d'affichage seulement (carte peinte, sol du décor, flaques,
/// façades), sans effet sur la simulation ni sur le scénario de référence. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2251CheminGrangeTests
{
    // Emprise de l'obstacle « grange » (scenarios/zones/vallons.json) et sa porte charretière : 3 m de large, au milieu de la façade sud.
    private const double MinX = -92, MaxX = -80, MinZ = -50, MaxZ = -42, DoorX = (MinX + MaxX) / 2, DoorWidth = 3;

    private static ZoneDefinition Zone() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
        ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static AdventureMap Map() => AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"),
        Path.Combine(Path.GetTempPath(), "eds_v2251_" + Guid.NewGuid().ToString("N"), "sauvegarde.json")).Map;

    private static StyleRoad Road(IReadOnlyList<StyleRoad> roads, string id) => Assert.Single(roads, r => r.Id == id);

    [Fact]
    public void Le_chemin_part_du_chemin_des_champs_et_arrive_dans_l_axe_de_la_porte()
    {
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(Zone());
        StyleRoad path = Road(roads, "chemin_de_la_grange");
        Assert.False(path.Paved);
        Assert.Equal(3, path.Width, 9);   // une charrette
        // Départ sur le chemin des champs (dans sa largeur).
        var start = path.Points[0];
        Assert.True(TownRules.RoadAt(start.X, start.Z, new[] { Road(roads, "chemin_des_champs") }).Edge < -1, $"départ {start}");
        // Dernier tronçon : dans l'axe de la porte, vers elle (+z vers −z), arrêté à moins de 2 m de la façade, sans la toucher.
        var a = path.Points[path.Points.Count - 2];
        var b = path.Points[path.Points.Count - 1];
        Assert.Equal((DoorX, DoorX), (a.X, b.X));
        Assert.True(a.Z > b.Z && b.Z > MaxZ && b.Z - MaxZ < 2, $"arrivée {b}");
        Assert.True(b.Z - path.Width / 2 > MaxZ, "le chemin entre dans la grange");
        // Toute la largeur de la porte donne sur la terre battue (chemin ou aire), à 0,5 m de la façade.
        for (double x = DoorX - DoorWidth / 2; x <= DoorX + DoorWidth / 2 + 1e-9; x += 0.25)
            Assert.True(TownRules.RoadAt(x, MaxZ + 0.5, roads).Edge < 0, $"devant la porte en ({x}, {MaxZ + 0.5})");
    }

    [Fact]
    public void L_aire_de_terre_battue_devant_la_porte()
    {
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(Zone());
        StyleRoad yard = Road(roads, "aire_de_la_grange");
        Assert.False(yard.Paved);
        // Plus large que la porte et que le chemin, centrée sur l'axe de la porte, le long de la façade sans y entrer.
        Assert.True(yard.Width > DoorWidth);
        Assert.Equal(DoorX, (yard.Points[0].X + yard.Points[yard.Points.Count - 1].X) / 2, 9);
        Assert.All(yard.Points, p => Assert.True(p.Z - yard.Width / 2 >= MaxZ + 0.15, $"aire {p}"));
        Assert.All(yard.Points, p => Assert.InRange(p.X, MinX, MaxX));
        // Un carré de 6 × 3 m devant la porte est tout de terre battue.
        for (double x = DoorX - 3; x <= DoorX + 3; x += 0.5)
            for (double z = MaxZ + 0.6; z <= MaxZ + 3.6; z += 0.5)
                Assert.True(TownRules.RoadAt(x, z, new[] { yard }).Edge < 0, $"aire en ({x}, {z})");
    }

    [Fact]
    public void La_porte_de_la_grange_fait_face_au_chemin()
    {
        AdventureMap map = Map();
        // La route la plus proche décide maintenant seule la façade sud (+z) ; l'orientation propre à l'atelier la garde.
        (int X, int Z) facing = TownRules.FacingOf("grange", MinX, MinZ, MaxX, MaxZ, map.Roads, map.Doors);
        Assert.Equal((0, 1), facing);
        Assert.Equal((0, 1), TownModels.OwnFacing("grange", facing));
        RoadHit front = TownRules.RoadAt(DoorX, MaxZ + 1, map.Roads);
        Assert.True(front.Edge < 0);
        Assert.Contains(front.Road!.Id, new[] { "chemin_de_la_grange", "aire_de_la_grange" });
    }

    [Fact]
    public void Le_chemin_et_l_aire_ne_touchent_aucun_obstacle()
    {
        // Toute la largeur (bords à 97 %) et les bouts arrondis, contre tous les obstacles de la zone.
        ZoneDefinition zone = Zone();
        var obstacles = zone.Setup.Space!.Obstacles.Select(o => (o.Id, o.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToArray())).ToList();
        foreach (StyleRoad road in AdventureSession.Roads(zone).Where(r => r.Id is "chemin_de_la_grange" or "aire_de_la_grange"))
        {
            var samples = new List<(double X, double Z)>();
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), nx = -(b.Z - a.Z) / len, nz = (b.X - a.X) / len;
                for (double t = 0; t <= len; t += 0.2)
                    foreach (double k in new[] { -0.97, -0.5, 0, 0.5, 0.97 })
                        samples.Add((a.X + (b.X - a.X) * t / len + nx * k * road.Width / 2, a.Z + (b.Z - a.Z) * t / len + nz * k * road.Width / 2));
            }
            foreach (var p in new[] { road.Points[0], road.Points[road.Points.Count - 1] })
                for (int k = 0; k < 32; k++)
                    samples.Add((p.X + Math.Cos(k * Math.PI / 16) * road.Width * 0.49, p.Z + Math.Sin(k * Math.PI / 16) * road.Width * 0.49));
            foreach ((string id, (double X, double Z)[] points) in obstacles)
                foreach (var s in samples)
                    Assert.True(StyleRules.InsideDistance(s.X, s.Z, points) <= 0, $"{road.Id} touche {id} en ({s.X:0.##}, {s.Z:0.##})");
        }
    }

    [Fact]
    public void La_carte_peinte_montre_le_chemin_et_l_aire()
    {
        AdventureMap map = Map();
        (int width, int height) = MapPainter.Size(map);
        byte[] image = MapPainter.Paint(map, width, height);
        (double R, double G, double B) At(double x, double z)
        {
            (double u, double v) = MapPainter.ToImage(map, x, z);
            int col = Math.Min(width - 1, (int)(u * width)), row = Math.Min(height - 1, (int)((1 - v) * height));
            int i = (row * width + col) * 4;
            return (image[i] / 255.0, image[i + 1] / 255.0, image[i + 2] / 255.0);
        }
        // Terre battue (comme la grand-rue) : rouge > vert > bleu, au milieu du chemin et sur l'aire devant la porte.
        foreach (var (x, z) in new[] { (-82.0, -37.6), (-76.0, -38.6), (DoorX, -39.3), (DoorX - 2, -40.8) })
        {
            var dirt = At(x, z);
            Assert.True(dirt.R > dirt.G && dirt.G > dirt.B && dirt.R > 0.3, $"chemin en ({x}, {z}) : {dirt}");
        }
    }
}
