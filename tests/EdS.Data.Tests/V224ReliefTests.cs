using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.24 « relief des Vallons » (décision de Damien du 11 novembre 2026) : hauteur du sol **affiché** (<see cref="GroundRelief"/>),
/// sur les vraies données de la zone. Affichage seulement : le moteur, les règles, les journaux et le réseau restent sur un sol plat. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V224ReliefTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v224_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private AdventureMap Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map;
    }

    private static IEnumerable<(double X, double Z)> Grid(AdventureMap map, double step)
    {
        for (double x = map.MinX; x <= map.MaxX; x += step)
            for (double z = map.MinZ; z <= map.MaxZ; z += step)
                yield return (x, z);
    }

    private static (double X, double Z)[] Points(SpaceObstacle o) => o.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToArray();

    [Fact]
    public void Le_relief_est_deterministe_et_plat_dans_une_piece()
    {
        AdventureMap map = Vallons();
        GroundRelief a = GroundRelief.For(map), b = GroundRelief.For(map);
        Assert.False(a.IsFlat);
        foreach ((double x, double z) in Grid(map, 7.3)) Assert.Equal(a.Height(x, z), b.Height(x, z));
        Assert.Equal(a.Pads.Select(p => (p.Id, p.Level, p.Group)), b.Pads.Select(p => (p.Id, p.Level, p.Group)));
        // Pièce, zone sans route, autre étage : plat.
        AdventureSession session = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "piece.json"));
        CombatSpace space = session.World.Zone.Setup.Space!;
        var room = new AdventureMap(space, map.Places, map.Roads, indoor: true);
        Assert.True(GroundRelief.For(new AdventureMap(space, map.Places)).IsFlat);   // sans route : plat
        Assert.True(GroundRelief.For(room).IsFlat);
        Assert.Equal(0, GroundRelief.For(room).Height(1, 2));
        Assert.Equal(0, a.Height(30, 30, floor: -1));
        Assert.True(GroundRelief.Flat.IsFlat);
    }

    /// <summary>Vallons doux : quelques mètres dans l'aire, pentes praticables ; collines et falaises plus hautes hors de l'aire.</summary>
    [Fact]
    public void Amplitude_et_pentes_restent_douces_dans_l_aire()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        double high = 0, steep = 0;
        foreach ((double x, double z) in Grid(map, 2))
        {
            double h = r.Height(x, z);
            Assert.InRange(h, 0, 5);
            high = Math.Max(high, h);
            steep = Math.Max(steep, r.Slope(x, z));
        }
        Assert.InRange(high, 1.5, 5);   // il y a vraiment des vallons
        Assert.InRange(steep, 0.05, 0.45);   // 24° au plus (talus au nord du front de taille)
        // Hors de l'aire : collines (et falaises) qui montent, sans dépasser la hauteur du terrain de Unity (40 m, base 3 m).
        double outside = Enumerable.Range(0, 40).Max(i => r.Height(map.MinX - 40, map.MinZ + i * map.Depth / 40));
        Assert.InRange(outside, 8, 30);
        // Raccord continu au bord de l'aire (les collines partent d'une pente nulle).
        for (double z = map.MinZ; z <= map.MaxZ; z += 5)
        {
            Assert.InRange(Math.Abs(r.Height(map.MinX - 0.05, z) - r.Height(map.MinX, z)), 0, 0.05);
            Assert.InRange(Math.Abs(r.Height(map.MaxX + 0.05, z) - r.Height(map.MaxX, z)), 0, 0.05);
        }
    }

    /// <summary>Chaque obstacle bâti est posé sur un replat horizontal : aucun bâtiment de travers, rien d'enterré ni de flottant.</summary>
    [Fact]
    public void Chaque_obstacle_bati_est_sur_un_replat()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        var built = map.Obstacles.Where(GroundRelief.Builds).ToList();
        foreach (string id in new[] { "maison1", "auberge", "chapelle", "grange", "puits", "etal1", "tente1", "palissade_camp1", "mur_nord", "porte_moulin",
                     "bloc1", "front_de_taille", "pierre_seuil1", "tour", "moulin", "pilier1" })
            Assert.Contains(built, o => o.Id == id);
        foreach (string id in new[] { "riviere_sud", "riviere_lac", "arbre1", "bois_no1", "haie_ouest" })
            Assert.DoesNotContain(built, o => o.Id == id);
        foreach (SpaceObstacle o in built)
        {
            var pts = Points(o);
            double level = r.Pads.Single(p => p.Id == o.Id).Level;
            double cx = pts.Average(p => p.X), cz = pts.Average(p => p.Z);
            // Les sommets, le centre et le tour à 50 cm de l'emprise sont exactement à la hauteur du replat.
            var probes = pts.Concat(new[] { (cx, cz) }).Concat(pts.Select(p => (p.X + Math.Sign(p.X - cx) * 0.35, p.Z + Math.Sign(p.Z - cz) * 0.35)));
            foreach ((double x, double z) in probes) Assert.True(Math.Abs(r.Height(x, z) - level) < 1e-9, o.Id + " n'est pas sur un replat en " + x + ", " + z);
        }
        // Le groupe du bourg est un seul replat ; la butte du Seuil porte ses pierres plus haut que la rivière.
        Assert.Single(r.Pads.Where(p => p.Id == "maison2" || p.Id == "auberge").Select(p => p.Group).Distinct());
        Assert.InRange(r.Pads.Single(p => p.Id == "pierre_seuil1").Level, 1.5, 5);
        Assert.InRange(r.Pads.Single(p => p.Id == "tour").Level, 1.2, 5);   // butte de la tour (la rivière, à 20 m, la tasse un peu)
    }

    /// <summary>Routes et chemins pas trop pentus ; points sûrs (haltes) et places dégagées à plat.</summary>
    [Fact]
    public void Routes_peu_pentues_et_points_surs_a_plat()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        double worst = 0;
        foreach (StyleRoad road in map.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                (double ax, double az) = road.Points[i];
                (double bx, double bz) = road.Points[i + 1];
                int n = (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / 0.5);
                for (int k = 0; k <= n; k++) worst = Math.Max(worst, r.Slope(ax + (bx - ax) * k / n, az + (bz - az) * k / n));
            }
        Assert.InRange(worst, 0, 0.25);   // 14° au plus sous les routes
        Assert.NotEmpty(map.Clearings);
        // Places dégagées à plat ; seules celles dont le replat touche la berge (où le sol redescend à 0 vers l'eau) gardent une pente,
        // faible. Relecture de la PR #144 : borne resserrée et compte des exceptions.
        int nearWater = 0;
        foreach ((double x, double z) in map.Clearings)
        {
            bool shore = r.Water(x, z) < r.Settings.ShoreFlat + r.Settings.ShoreBlend + r.Settings.ClearingRadius;
            if (shore) nearWater++;
            foreach ((double dx, double dz) in new[] { (0.0, 0.0), (1.5, 0.0), (0.0, -1.5), (-1.2, 1.2) })
                Assert.True(shore ? r.Slope(x + dx, z + dz) <= 0.15 : r.Slope(x + dx, z + dz) < 1e-9, "place dégagée en pente en " + x + ", " + z);
        }
        Assert.InRange(nearWater, 0, 3);
    }

    /// <summary>
    /// Relecture de la PR #144 : une rue pavée ou un labour drapé (chaque sommet monté de la hauteur du sol sous lui, comme
    /// <c>SpaceView.Drape</c>) suit le sol entre ses sommets à 5 cm près une fois découpé en morceaux de 1 m ; sans découpe, il s'enterre ou flotte.
    /// </summary>
    [Fact]
    public void Rues_pavees_et_labours_decoupes_suivent_le_relief()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        double Gap(StyleMesh mesh)
        {
            double worst = 0;
            for (int t = 0; t < mesh.Triangles.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    StyleVector a = mesh.Vertices[mesh.Triangles[t + e]], b = mesh.Vertices[mesh.Triangles[t + (e + 1) % 3]];
                    double mx = (a.X + b.X) / 2, mz = (a.Z + b.Z) / 2;
                    worst = Math.Max(worst, Math.Abs((r.Height(a.X, a.Z) + r.Height(b.X, b.Z)) / 2 - r.Height(mx, mz)));
                }
            return worst;
        }
        // La place du bourg (seule rue pavée) est sur un replat : on la déplace sur une pente pour que le test morde.
        StyleRoad paved = map.Roads.Single(x => x.Paved);
        var slope = new StyleRoad("essai", new[] { (30.0, -40.0), (60.0, -30.0), (90.0, 20.0) }, paved.Width, true);
        Assert.True(Gap(TownShapes.Pavement(slope, 1.5)) > 0.1);   // sans découpe : enterré ou flottant
        Assert.InRange(Gap(TownShapes.Pavement(slope, 1.5, 24, 1.0)), 0, 0.05);
        Assert.InRange(Gap(TownShapes.Pavement(paved, 1.5, 24, 1.0)), 0, 0.05);
        var boxes = map.Obstacles.Select(o => (o.Points.Min(p => p.X.Raw) / 1e4, o.Points.Min(p => p.Z.Raw) / 1e4, o.Points.Max(p => p.X.Raw) / 1e4,
            o.Points.Max(p => p.Z.Raw) / 1e4)).ToList();
        var fields = map.Places.Where(p => TownRules.Farmland(p.Id))
            .SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ, boxes, map.Roads, map.Clearings, (map.MinX, map.MinZ, map.MaxX, map.MaxZ))).ToList();
        Assert.NotEmpty(fields);
        double whole = fields.Max(f => Gap(TownShapes.Furrows(f, 2))), cut = fields.Max(f => Gap(TownShapes.Furrows(f, 2, 1.0)));
        Assert.True(whole > cut);
        Assert.InRange(cut, 0, 0.05);
        // Le découpage ne change pas la surface couverte (mêmes bornes).
        foreach (StyleField f in fields.Take(3)) Assert.Equal(TownShapes.Furrows(f, 2).Bounds(), TownShapes.Furrows(f, 2, 1.0).Bounds());
    }

    /// <summary>L'eau reste dans le creux (0 sur la rivière et le lac, et sur leurs berges) ; le tablier du pont se raccorde au sol.</summary>
    [Fact]
    public void Eau_dans_le_creux_et_raccord_avec_le_pont()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        foreach (SpaceObstacle water in map.Obstacles.Where(o => o.Low))
        {
            var pts = Points(water);
            foreach ((double x, double z) in pts) Assert.Equal(0, r.Height(x, z));
            Assert.Equal(0, r.Height(pts.Average(p => p.X), pts.Average(p => p.Z)));
        }
        // Pont cintré (SpaceView.Bridge : x 3,4 – 7,6 sur la brèche −2 – 1,5) : sol à 0 sous le tablier et à ses deux bouts, donc
        // StandHeight = relief + tablier est continu (4 cm aux bouts comme avant).
        for (double x = 2.9; x <= 8.1; x += 0.1)
            for (double z = -2; z <= 1.5; z += 0.5)
            {
                Assert.Equal(0, r.Height(x, z));
                double stand = r.Height(x, z) + StyleShapes.StandHeight(x, z, 3.4, -2, 7.6, 1.5, 0.35);
                Assert.Equal(StyleShapes.StandHeight(x, z, 3.4, -2, 7.6, 1.5, 0.35), stand);
            }
        // Le chemin du lac descend vers l'eau.
        Assert.True(r.Height(24, -24) > r.Height(0, -66) + 0.3);
        Assert.True(r.Height(0, -66) < 0.1);
        Assert.Equal(0, r.Height(0, -63));   // au bord du lac
    }

    /// <summary>
    /// Affichage seulement : aucun projet de la simulation, des données ou du réseau ne connaît le relief ; Unity passe par StandHeight
    /// (terrain, personnages, décor, marqueurs, herbe, annonces).
    /// </summary>
    [Fact]
    public void La_simulation_ne_connait_pas_le_relief_et_unity_le_suit()
    {
        string root = Lot93UnityPackageTests.Root;
        foreach (string project in new[] { "EdS.Domain", "EdS.Simulation", "EdS.Data", "EdS.Data.Json", "EdS.Net", "EdS.World", "EdS.Server" })
        {
            string dir = Path.Combine(root, "src", project);
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                Assert.DoesNotContain("GroundRelief", File.ReadAllText(file), StringComparison.Ordinal);
        }
        string Game(string name) => File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", name));
        string world = Game("StyleWorld.cs"), view = Game("SpaceView.cs");
        Assert.Contains("(_relief.IsFlat ? 0f : (float)_relief.Height(x, z))", world, StringComparison.Ordinal);
        Assert.Contains("TerrainBase - dig + (_relief.IsFlat ? 0 : _relief.Height(x, z))", world, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.SetRelief(map != null ? GroundRelief.For(map) : null);", view, StringComparison.Ordinal);
        // Revue Codex de la PR #144 : sans terrain (sol de remplacement plat), le relief est retiré avant de poser le décor ; les noms
        // des repères suivent le sol affiché.
        int noTerrain = view.IndexOf("if (!terrain) StyleWorld.SetRelief(null);", StringComparison.Ordinal);
        Assert.True(noTerrain > view.IndexOf("_terrain = StyleWorld.Ground(", StringComparison.Ordinal));
        Assert.True(noTerrain < view.IndexOf("foreach (SpaceObstacle obstacle in space.Obstacles) Obstacle(obstacle);", StringComparison.Ordinal));
        Assert.True(noTerrain < view.IndexOf("Border(\"Bord ouest\"", StringComparison.Ordinal));
        Assert.Contains("new Vector3(at.x, m.Height + StyleWorld.StandHeight(at.x, at.z), at.z)", Game("WorldMarkers.cs"), StringComparison.Ordinal);
        Assert.Contains("Lift(children, occluders, boxes, Ground(cx, cz, 0.4f));", view, StringComparison.Ordinal);
        Assert.Contains("Drape(Shape(\"Rue pavée \"", view, StringComparison.Ordinal);
        Assert.Contains("(StyleWorld.HasRelief || StyleWorld.OnDeck(", view, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.StandHeight(x, z), z), Quaternion", Game("StyleGrass.cs"), StringComparison.Ordinal);
        Assert.Contains("m.Base + StyleWorld.StandHeight(step.x, step.z)", Game("WorldMarkers.cs"), StringComparison.Ordinal);
        Assert.Contains("StyleWorld.OnGround(", Game("SpaceView.Night.cs"), StringComparison.Ordinal);
        Assert.Contains("StyleWorld.OnGround(", Game("SpaceView.Weather.cs"), StringComparison.Ordinal);
        Assert.Contains("StyleWorld.OnGround(", Game("ShopsView.cs"), StringComparison.Ordinal);
        Assert.Contains("center.y - bottom * k", Game("StyleLook.cs"), StringComparison.Ordinal);
        // Plus aucun décor posé à y = 0 dans le décor de plein air (hors intérieurs, arène et pont, sur l'eau à 0).
        Assert.DoesNotContain("StyleSlot.Plant, \"plante\" + placed, new Vector3(x, 0f, z)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("new Vector3((float)tree.X, 0f, (float)tree.Z)", view, StringComparison.Ordinal);
    }
}
