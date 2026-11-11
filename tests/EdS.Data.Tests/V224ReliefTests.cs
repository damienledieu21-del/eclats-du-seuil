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
        Assert.Contains("StyleWorld.UseRelief(map);", view, StringComparison.Ordinal);
        // Revue Codex de la PR #144 (2e tour) : le relief de la zone est posé avant ses repères (construits avant le décor de SpaceView),
        // à l'ouverture de l'aventure comme au passage d'une porte ; plat sans terrain peint.
        string game = Game("EdsGame.cs");
        int start = game.IndexOf("private void StartAdventure()", StringComparison.Ordinal), show = game.IndexOf("private void ShowAdventureSession()", StringComparison.Ordinal);
        Assert.True(start >= 0 && show > start);
        foreach (int from in new[] { start, show })
        {
            int relief = game.IndexOf("StyleWorld.UseRelief(_adventure.Map);", from, StringComparison.Ordinal);
            int markers = game.IndexOf("_markers = new WorldMarkers(_adventure);", from, StringComparison.Ordinal);
            Assert.True(relief > from && relief < markers, "relief après les repères");
        }
        Assert.Contains("if (map == null || !CanPaintGround)", world, StringComparison.Ordinal);
        // Aucun modèle recalé au sol plat (y = 0) après mise à l'échelle.
        foreach (string file in Directory.GetFiles(Path.Combine(root, "unity", "Assets", "EdS"), "*.cs", SearchOption.AllDirectories))
        {
            Assert.DoesNotMatch(@"StyleLook\.Ground\([^,]+,\s*0f\)", File.ReadAllText(file));
            // Revue Codex (3e tour) : le décor lit le sol réellement affiché (StyleWorld), jamais le relief théorique de la carte.
            if (Path.GetFileName(file) != "StyleWorld.cs")
                Assert.DoesNotContain("GroundRelief.For(", File.ReadAllText(file), StringComparison.Ordinal);
        }
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

    /// <summary>
    /// Fusion de main (V2.25) : les modèles de l'atelier qui ne sont pas des bâtiments du bourg (moulin, palissades du camp) et les nouveaux
    /// bâtiments (grange, tour, puits, étals, tentes) sont à plat **avec leurs débords** (auvents, toit du puits, roue et bief du moulin) ; la
    /// roue et l'eau du moulin sont des enfants du repère du modèle, montés avec lui (même hauteur par rapport au bâtiment).
    /// </summary>
    [Fact]
    public void Les_modeles_de_V225_sont_a_plat_avec_leurs_debords()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        const double overhang = 1.42;   // jusqu'à 1,42 m hors de l'emprise (le replat est horizontal jusqu'à 1,5 m)
        foreach (string id in new[] { "grange", "moulin", "tour", "puits", "etal1", "etal2", "tente1", "tente2", "tente3", "palissade_camp1", "palissade_camp2" })
        {
            SpaceObstacle o = map.Obstacles.Single(x => x.Id == id);
            Assert.True(GroundRelief.Builds(o), id);
            var pts = Points(o);
            double level = r.Pads.Single(p => p.Id == id).Level;
            double x0 = pts.Min(p => p.X) - overhang, x1 = pts.Max(p => p.X) + overhang, z0 = pts.Min(p => p.Z) - overhang, z1 = pts.Max(p => p.Z) + overhang;
            for (double x = x0; x <= x1 + 1e-9; x += 0.25)
                for (double z = z0; z <= z1 + 1e-9; z += 0.25)
                    if (GroundRelief.PolygonDistance(pts, x, z) <= overhang)   // débord rectangulaire d'1 m, coins compris (1,41 m)
                        Assert.True(Math.Abs(r.Height(x, z) - level) < 1e-9, id + " : sol pas à plat en " + x + ", " + z);
        }
        // Roue (rayon 1,75) et eau du bief (4,8 m de long) du moulin : dans son emprise de 6 × 6 élargie du replat, quelle que soit sa façade.
        SpaceObstacle mill = map.Obstacles.Single(x => x.Id == "moulin");
        double half = Math.Min(Points(mill).Max(p => p.X) - Points(mill).Min(p => p.X), Points(mill).Max(p => p.Z) - Points(mill).Min(p => p.Z)) / 2;
        (double fx, _) = TownModels.OwnFootCenter("moulin_vallons");
        var wheel = TownModels.OwnPartCenter("moulin_vallons", "roue")!.Value;
        var water = TownModels.OwnPartCenter("moulin_vallons", "eau")!.Value;
        Assert.True(Math.Abs(wheel.X - fx) + 0.62 <= half + overhang && TownModels.MillWheelRadius <= half + overhang);
        Assert.True(Math.Abs(water.X - fx) + 0.45 <= half + overhang && 2.4 <= half + overhang);
        Assert.InRange(water.Y, 0, wheel.Y - TownModels.MillWheelRadius + 0.3);   // les aubes basses trempent dans le bief
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        int shape = view.IndexOf("private void ObstacleShape(SpaceObstacle obstacle)", StringComparison.Ordinal);
        Assert.True(view.IndexOf("OwnPiece(id, points)", shape, StringComparison.Ordinal) > shape);   // monté par Obstacle (Lift)
        Assert.Contains("Transform frame = model.transform.parent;", view, StringComparison.Ordinal);
        Assert.Contains("Object.Instantiate(own.Wheel, frame, false)", view, StringComparison.Ordinal);
        Assert.Contains("Object.Instantiate(own.Water, frame, false)", view, StringComparison.Ordinal);
        Assert.Contains("go.transform.SetParent(_root.transform, false);", view, StringComparison.Ordinal);
    }

    /// <summary>
    /// Revue Codex de la PR #144 (accord de Damien : « Ok pour corriger la caméra ») : près du bord des Vallons, une vue éloignée et basse
    /// mettait la caméra sous les collines. <see cref="CameraRules.TerrainClearDistance"/> la rapproche contre le sol affiché.
    /// </summary>
    [Fact]
    public void La_camera_ne_passe_jamais_sous_le_relief()
    {
        AdventureMap map = Vallons();
        GroundRelief r = GroundRelief.For(map);
        Func<double, double, double> ground = (x, z) => r.Height(x, z);
        // Le cas de la revue : depuis (-99, 70), 24 m vers l'ouest, basse.
        (double X, double Y, double Z) target = (-99, r.Height(-99, 70) + CameraRules.TargetHeight(CameraRules.MaxDistance), 70);
        var dir = CameraRules.Offset(90, 5, 1);
        Assert.True(dir.X < -0.9);   // la caméra est à l'ouest du personnage
        double far = target.Y + dir.Y * CameraRules.MaxDistance;
        Assert.True(far < r.Height(target.X + dir.X * CameraRules.MaxDistance, target.Z + dir.Z * CameraRules.MaxDistance), "le cas de la revue n'est plus reproduit");
        double d = CameraRules.TerrainClearDistance(target, dir, ground);
        Assert.InRange(d, CameraRules.CollisionMinDistance, CameraRules.MaxDistance - 1);
        AssertClear(target, dir, d, ground);
        // Balayage : bords de l'aire, toutes les orientations, inclinaisons basses : la caméra et la vue vers le personnage restent au-dessus du sol.
        foreach ((double px, double pz) in new[] { (map.MinX + 1, map.MinZ + 30), (map.MinX + 6, 70.0), (map.MaxX - 2, map.MaxZ - 20), (0.0, map.MaxZ - 3), (20.0, map.MinZ + 2) })
            for (double yaw = 0; yaw < 360; yaw += 30)
                foreach (double pitch in new[] { -10.0, 0, 8, 22 })
                {
                    (double X, double Y, double Z) t = (px, r.Height(px, pz) + 1.2, pz);
                    var v = CameraRules.Offset(yaw, pitch, 1);
                    double c = CameraRules.TerrainClearDistance(t, v, ground);
                    AssertClear(t, v, c, ground);
                    Assert.True(CameraRules.TerrainClearDistance(t, v, ground, 6) <= 6);
                }
        // Sol plat : vue par-dessus, rien ne change ; par en dessous, comme GroundSafeDistance.
        Func<double, double, double> flat = (x, z) => 0;
        Assert.Equal(CameraRules.MaxDistance, CameraRules.TerrainClearDistance((0, 1.6, 0), CameraRules.Offset(0, 22, 1), flat));
        Assert.Equal(8, CameraRules.TerrainClearDistance((0, 1.6, 0), CameraRules.Offset(0, 22, 1), flat, 8));
        double low = CameraRules.TerrainClearDistance((0, 1.6, 0), CameraRules.Offset(0, -10, 1), flat);
        Assert.Equal(CameraRules.GroundSafeDistance(CameraRules.MaxDistance, -10, 1.6), low, 1);
        // Entrées sans sol, non finies, point visé déjà trop bas : rien à protéger.
        Assert.Equal(CameraRules.MaxDistance, CameraRules.TerrainClearDistance((0, 1.6, 0), dir, null));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.TerrainClearDistance((double.NaN, 1.6, 0), dir, flat));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.TerrainClearDistance((0, 0.1, 0), dir, flat));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.TerrainClearDistance((0, 1.6, 0), dir, (x, z) => double.NaN));
    }

    private static void AssertClear((double X, double Y, double Z) t, (double X, double Y, double Z) v, double d, Func<double, double, double> ground)
    {
        for (double s = 0; s <= d; s += 0.05)
            Assert.True(t.Y + v.Y * s >= ground(t.X + v.X * s, t.Z + v.Z * s) + CameraRules.GroundClearance - 0.02,
                "sous le sol à " + s + " m (" + t + ", " + v + ")");
    }

    /// <summary>Sous-lot V2.24 : la distance contre le relief glisse sans saut (vite en se rapprochant, en douceur en revenant).</summary>
    [Fact]
    public void La_camera_se_rapproche_du_relief_sans_saut()
    {
        double dt = 1 / 60.0;
        double near = CameraRules.SettleTerrain(24, 5, dt);
        Assert.InRange(near, 5.5, 23.9);   // pas de saut d'une image à l'autre
        double back = CameraRules.SettleTerrain(5, 24, dt);
        Assert.True(24 - near > back - 5);   // le rapprochement est plus rapide que le retour
        double shown = 24;
        for (int i = 0; i < 30; i++) shown = CameraRules.SettleTerrain(shown, 5, dt);
        Assert.InRange(shown, 5, 5.2);   // une demi-seconde suffit
        Assert.Equal(7, CameraRules.SettleTerrain(double.NaN, 7, dt));
        Assert.Equal(7, CameraRules.SettleTerrain(7, double.NaN, dt));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.SettleTerrain(double.NaN, double.NaN, dt));
        Assert.Equal(24, CameraRules.SettleTerrain(24, 5, double.NaN));
        Assert.Equal(3.35, CameraRules.AboveGround(1, 3), 9);
        Assert.Equal(5, CameraRules.AboveGround(5, 3));
        Assert.Equal(5, CameraRules.AboveGround(5, double.NaN));
        // Unity : la caméra passe par le relief affiché, avec un délégué gardé (pas d'allocation par image).
        string rig = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "CameraRig.cs"));
        Assert.Contains("private static readonly System.Func<double, double, double> StandHeight = (x, z) => StyleWorld.StandHeight((float)x, (float)z);", rig, StringComparison.Ordinal);
        Assert.Contains("CameraRules.TerrainClearDistance((_target.x, _target.y, _target.z), (dx, dy, dz), StandHeight, _shownDistance)", rig, StringComparison.Ordinal);
        Assert.Contains("CameraRules.SettleTerrain(_terrainClear, ground, deltaTime)", rig, StringComparison.Ordinal);
        Assert.Contains("CameraRules.AboveGround(position.y, StyleWorld.StandHeight(position.x, position.z))", rig, StringComparison.Ordinal);
        int follow = rig.IndexOf("public void Follow(", StringComparison.Ordinal), end = rig.IndexOf("private static bool OverInterface()", StringComparison.Ordinal);
        Assert.DoesNotContain("=>", rig.Substring(follow, end - follow), StringComparison.Ordinal);
    }
}
