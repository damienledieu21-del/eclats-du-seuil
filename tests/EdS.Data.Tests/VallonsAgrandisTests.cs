using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Vallons agrandis (demande de Damien du 28 octobre 2026 : « un grand agrandissement, on pourrait mettre des bâtiments, des routes ou
/// plein d'autres choses ») : règles d'affichage du bourg (ce que représente chaque obstacle, couches du sol, routes, bois, champs) et
/// bâtiments faits par le code, sans Unity.
/// </summary>
[Trait("Category", "World")]
public class VallonsAgrandisTests
{
    private static ZoneDefinition Zone() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
        ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static List<(string Id, (double X, double Z)[] Points)> Obstacles(ZoneDefinition zone) =>
        zone.Setup.Space!.Obstacles.Select(o => (o.Id, o.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToArray())).ToList();

    [Theory]
    [InlineData("maison7", StyleBuilding.House)]
    [InlineData("auberge", StyleBuilding.Inn)]
    [InlineData("chapelle", StyleBuilding.Chapel)]
    [InlineData("grange", StyleBuilding.Barn)]
    [InlineData("puits", StyleBuilding.Well)]
    [InlineData("etal2", StyleBuilding.Stall)]
    [InlineData("tour", StyleBuilding.Tower)]
    [InlineData("tente3", StyleBuilding.Tent)]
    [InlineData("bois_no4", StyleBuilding.Woods)]
    [InlineData("moulin", StyleBuilding.None)]
    [InlineData("arbre1", StyleBuilding.None)]
    [InlineData("riviere_lac", StyleBuilding.None)]
    [InlineData("pierre_seuil1", StyleBuilding.None)]
    public void Chaque_obstacle_dit_ce_qu_il_represente(string id, StyleBuilding kind) => Assert.Equal(kind, TownRules.BuildingFor(id));

    [Fact]
    public void Chaque_obstacle_des_Vallons_est_habille()
    {
        // Rien ne reste en bloc de pierre simple : chaque obstacle livré a son habit (bâtiment, bois, rivière, clôture, arbre, rocher…).
        foreach ((string id, _) in Obstacles(Zone()))
        {
            bool dressed = TownRules.BuildingFor(id) != StyleBuilding.None || id.StartsWith("riviere", StringComparison.Ordinal)
                || StyleRules.FenceFor(id) != StyleFence.None || StyleRules.ForObstacle(id) != null || id.StartsWith("moulin", StringComparison.Ordinal)
                || id.StartsWith("pilier", StringComparison.Ordinal) || id.StartsWith("front_de_taille", StringComparison.Ordinal);
            Assert.True(dressed, $"{id} sans habit");
        }
    }

    [Fact]
    public void Un_obstacle_allonge_donne_une_rangee_de_taches_et_un_bois_aucune()
    {
        // Revue des Vallons agrandis : la haie de 94 m ne couvre plus le bourg d'un disque de terre de 48 m de rayon.
        List<StyleSpot> hedge = TownRules.GroundSpots(new[] { ("haie_ouest", -100.0, 19.0, -6.0, 20.0) });
        Assert.Equal(94, hedge.Count);
        Assert.All(hedge, s => Assert.InRange(s.Radius, 1.4, 1.6));
        Assert.All(hedge, s => Assert.Equal(19.5, s.Z, 9));
        Assert.Equal(-99.5, hedge[0].X, 9);
        Assert.Single(TownRules.GroundSpots(new[] { ("arbre1", -12.4, 3.6, -11.6, 4.4) }));
        List<StyleSpot> house = TownRules.GroundSpots(new[] { ("maison1", -32.0, 4.0, -25.0, 10.5) });   // V1.10 : petite maison
        Assert.Equal(2, house.Count);   // 7 × 6,5 m : deux taches de terre battue le long du grand côté
        Assert.All(house, s => Assert.Equal(StyleGround.Soil, s.Ground));
        Assert.Empty(TownRules.GroundSpots(new[] { ("bois_no1", -80.0, 43.0, -44.0, 76.0) }));
        Assert.Empty(TownRules.GroundSpots(new[] { ("riviere_nord", 4.0, 1.5, 7.0, 100.0) }));
        // La grille des taches rend le même mélange que la liste entière.
        var zone = Zone();
        List<StyleSpot> all = TownRules.GroundSpots(Obstacles(zone).Select(o => (o.Id, o.Points.Min(p => p.X), o.Points.Min(p => p.Z), o.Points.Max(p => p.X), o.Points.Max(p => p.Z))));
        var grid = new SpotGrid(all);
        for (double x = -100; x <= 140; x += 3.7)
            for (double z = -70; z <= 100; z += 3.1)
            {
                StyleMix a = StyleRules.GroundMix(x, z, all), b = grid.Mix(x, z);
                Assert.Equal((a.Soil, a.Rock, a.Grass), (b.Soil, b.Rock, b.Grass));
            }
    }

    [Fact]
    public void Routes_bord_axe_et_parcours()
    {
        var road = new StyleRoad("r", new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0) }, 4, false);
        RoadHit mid = TownRules.RoadAt(5, 0, new[] { road });
        Assert.Same(road, mid.Road);
        Assert.Equal(-2, mid.Edge, 9);
        Assert.Equal(0, mid.Across, 9);
        Assert.Equal(5, mid.Along, 9);
        RoadHit left = TownRules.RoadAt(5, 1.5, new[] { road });
        Assert.Equal((-0.5, 1.5), (left.Edge, left.Across));
        RoadHit second = TownRules.RoadAt(10, 6, new[] { road });
        Assert.Equal(16, second.Along, 9);   // 10 m du premier tronçon, 6 du second
        Assert.Equal((0.0, 1.0), (second.DirX, second.DirZ));
        Assert.Equal(3, TownRules.RoadAt(-5, 0, new[] { road }).Edge, 9);   // au-delà du bout : distance au premier point
        Assert.Null(TownRules.RoadAt(0, 0, Array.Empty<StyleRoad>()).Road);
        Assert.True(double.IsPositiveInfinity(TownRules.RoadAt(0, 0, Array.Empty<StyleRoad>()).Edge));
        // Terre battue sur un chemin, rien loin de lui ; une rue pavée garde un bord net.
        var grass = new StyleMix(0, 0, 1);
        Assert.Equal(1, TownRules.RoadMix(grass, 5, 0, mid).Soil, 9);
        Assert.Equal(grass.Grass, TownRules.RoadMix(grass, 5, 30, TownRules.RoadAt(5, 30, new[] { road })).Grass);
        var paved = new StyleRoad("p", new[] { (0.0, 0.0), (10.0, 0.0) }, 4, true);
        Assert.Equal(0.5, TownRules.RoadMix(grass, 5, 2, TownRules.RoadAt(5, 2, new[] { paved })).Soil, 9);
        Assert.Throws<ArgumentException>(() => new StyleRoad("x", new[] { (0.0, 0.0) }, 3, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleRoad("x", new[] { (0.0, 0.0), (1.0, 0.0) }, 0, false));
    }

    [Fact]
    public void Les_routes_des_Vallons_relient_les_lieux()
    {
        ZoneDefinition zone = Zone();
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(zone);
        Assert.Equal(10, roads.Count);
        // Chaque point sûr est au bord d'une route (à 6 m au plus) ; la grand-rue et le passage des ruines arrivent aux deux têtes du pont.
        foreach (SafePoint s in zone.SafePoints)
            Assert.True(TownRules.RoadAt(s.Position.X.Raw / 10000.0, s.Position.Z.Raw / 10000.0, roads).Edge < 6, $"{s.Id} loin des routes");
        Assert.Equal("grand_rue", TownRules.RoadAt(3.3, -0.2, roads).Road!.Id);
        Assert.True(TownRules.RoadAt(3.3, -0.2, roads).Edge < 0);
        Assert.Equal("passage_des_ruines", TownRules.RoadAt(7.6, 0.5, roads).Road!.Id);
        Assert.True(TownRules.RoadAt(7.6, 0.5, roads).Edge < 0);
        // Aucune route ne passe sous un bâtiment, un bois ou un rocher (le chemin longe ou contourne) ; seuls le puits et les étals sont posés
        // sur la place pavée.
        foreach ((string id, (double X, double Z)[] points) in Obstacles(zone).Where(o => TownRules.BuildingFor(o.Id) != StyleBuilding.None || StyleRules.ForObstacle(o.Id) != null))
            foreach (StyleRoad road in roads.Where(r => !(r.Paved && TownRules.BuildingFor(id) is StyleBuilding.Well or StyleBuilding.Stall)))
                for (int i = 0; i + 1 < road.Points.Count; i++)
                    for (double t = 0; t <= 1; t += 0.05)
                    {
                        double x = road.Points[i].X + (road.Points[i + 1].X - road.Points[i].X) * t, z = road.Points[i].Z + (road.Points[i + 1].Z - road.Points[i].Z) * t;
                        Assert.True(StyleRules.InsideDistance(x, z, points) < 0, $"{road.Id} traverse {id} en ({x:0.#}, {z:0.#})");
                    }
    }

    [Fact]
    public void Un_bois_est_plein_et_reste_dans_son_emprise()
    {
        var hexagon = new[] { (0.0, 0.0), (20.0, -5.0), (32.0, 6.0), (30.0, 22.0), (12.0, 28.0), (-4.0, 14.0) };
        IReadOnlyList<StyleWoodTree> trees = TownRules.WoodTrees(hexagon, "bois_test", 4);
        Assert.Equal(trees.Select(t => (t.X, t.Z, t.Scale, t.Edge)), TownRules.WoodTrees(hexagon, "bois_test", 4).Select(t => (t.X, t.Z, t.Scale, t.Edge)));
        Assert.All(trees, t => Assert.True(StyleRules.InsideDistance(t.X, t.Z, hexagon) >= 0.8, $"({t.X}, {t.Z}) trop près du bord"));
        Assert.All(trees, t => Assert.InRange(t.Scale, 0.8, 1.2));
        Assert.Contains(trees, t => t.Edge);
        Assert.Contains(trees, t => !t.Edge);
        // Plein : un arbre pour 25 m² au moins ; aucun point du cœur à plus de 4 m d'un arbre.
        Assert.True(trees.Count > 640 / 25.0 * 1.0, $"{trees.Count} arbres");
        for (double x = 0; x < 30; x += 1.3)
            for (double z = 0; z < 26; z += 1.3)
                if (StyleRules.InsideDistance(x, z, hexagon) > 3)
                    Assert.True(trees.Min(t => Math.Sqrt((t.X - x) * (t.X - x) + (t.Z - z) * (t.Z - z))) < 4, $"trou en ({x}, {z})");
        Assert.Empty(TownRules.WoodTrees(new[] { (0.0, 0.0), (1.0, 0.0) }, "x", 4));
        Assert.Empty(TownRules.WoodTrees(hexagon, "x", 0.5));
    }

    [Fact]
    public void Les_champs_evitent_routes_obstacles_et_haltes()
    {
        var road = new StyleRoad("r", new[] { (0.0, 20.0), (60.0, 20.0) }, 3, false);
        var obstacles = new[] { (30.0, 30.0, 40.0, 38.0) };
        var keep = new[] { (10.0, 10.0) };
        IReadOnlyList<StyleField> fields = TownRules.Fields(0, 0, 60, 45, obstacles, new[] { road }, keep, (-100.0, -100.0, 100.0, 100.0));
        Assert.True(fields.Count >= 5, $"{fields.Count} champs");
        foreach (StyleField f in fields)
        {
            Assert.InRange(f.MaxX - f.MinX, 6, 16);
            Assert.InRange(f.MaxZ - f.MinZ, 6, 16);
            Assert.True(f.MinX >= 2 && f.MinZ >= 2 && f.MaxX <= 58 && f.MaxZ <= 43, "dans le lieu, à 2 m du bord");
            Assert.InRange(f.Crop, 0, 2);
            Assert.True(f.MaxZ < 18.5 - 1 + 1e-9 || f.MinZ > 21.5 + 1 - 1e-9, "à 1 m de la route");
            Assert.False(f.MinX < 42 && f.MaxX > 28 && f.MinZ < 40 && f.MaxZ > 28, "à 2 m de l'obstacle");
            double dx = Math.Max(Math.Max(f.MinX - 10, 10 - f.MaxX), 0), dz = Math.Max(Math.Max(f.MinZ - 10, 10 - f.MaxZ), 0);
            Assert.True(dx * dx + dz * dz >= 25, "à 5 m de la halte");
            foreach (StyleField g in fields.Where(g => !g.Equals(f)))
                Assert.False(f.MinX < g.MaxX + 2 && f.MaxX > g.MinX - 2 && f.MinZ < g.MaxZ + 2 && f.MaxZ > g.MinZ - 2, "sentier de 2 m entre deux champs");
        }
        Assert.Contains(fields, f => f.Crop == 0);   // du blé mûr
        Assert.Equal(fields, TownRules.Fields(0, 0, 60, 45, obstacles, new[] { road }, keep, (-100.0, -100.0, 100.0, 100.0)));
        Assert.Empty(TownRules.Fields(0, 0, 60, 45, obstacles, new[] { road }, keep, (100.0, 100.0, 200.0, 200.0)));   // hors de l'aire
    }

    [Fact]
    public void La_route_la_plus_proche_parmi_plusieurs()
    {
        // Relecture des Vallons agrandis : une route hors de sa boîte n'était plus regardée dès qu'une autre avait été vue ; maison6 et
        // maison8, au bout ouest du bourg, regardaient la grand-rue (à 37 m) au lieu de la grand-rue de l'ouest (à 3 m).
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(Zone());
        RoadHit hit = TownRules.RoadAt(-89.5, -6, roads);
        Assert.Equal("grand_rue_ouest", hit.Road!.Id);
        Assert.Equal(2.75, hit.Edge, 6);
        // Même réponse qu'une recherche exhaustive, partout sur l'aire.
        for (double x = -100; x <= 140; x += 2.9)
            for (double z = -70; z <= 100; z += 2.3)
            {
                double best = roads.Min(r => TownRules.RoadAt(x, z, new[] { r }).Edge);
                Assert.Equal(best, TownRules.RoadAt(x, z, roads).Edge, 9);
            }
        Assert.Equal((0, 1), TownRules.Facing(-98, -20, -84, -7, roads));    // maison6 (V1.10) : au nord, vers la grand-rue de l'ouest
        Assert.Equal((0, -1), TownRules.Facing(-99, 4, -93, 10.5, roads));   // maison8 (V1.10) : au sud
    }

    [Fact]
    public void Les_routes_ne_passent_ni_dans_un_obstacle_ni_dans_l_eau()
    {
        // Relecture des Vallons agrandis : toute la largeur de la route (pas seulement son axe), bouts arrondis compris ; seuls le puits et
        // les étals sont posés sur la place pavée, et le chemin du moulin passe la porte (un passage : elle s'ouvre).
        ZoneDefinition zone = Zone();
        var gates = zone.Passages.Select(p => p.Obstacle).ToHashSet();
        var obstacles = Obstacles(zone).Where(o => !gates.Contains(o.Id)).ToList();
        foreach (StyleRoad road in AdventureSession.Roads(zone))
        {
            var samples = new List<(double X, double Z)>();
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), nx = -(b.Z - a.Z) / len, nz = (b.X - a.X) / len;
                for (double t = 0; t <= len; t += 0.4)
                    foreach (double k in new[] { -0.97, -0.5, 0, 0.5, 0.97 })
                        samples.Add((a.X + (b.X - a.X) * t / len + nx * k * road.Width / 2, a.Z + (b.Z - a.Z) * t / len + nz * k * road.Width / 2));
            }
            foreach (var p in new[] { road.Points[0], road.Points[road.Points.Count - 1] })
                for (int k = 0; k < 16; k++)
                    samples.Add((p.X + Math.Cos(k * Math.PI / 8) * road.Width * 0.48, p.Z + Math.Sin(k * Math.PI / 8) * road.Width * 0.48));
            foreach ((string id, (double X, double Z)[] points) in obstacles)
            {
                if (road.Paved && TownRules.BuildingFor(id) is StyleBuilding.Well or StyleBuilding.Stall) continue;
                foreach (var s in samples)
                    Assert.True(StyleRules.InsideDistance(s.X, s.Z, points) <= 0, $"{road.Id} passe dans {id} en ({s.X:0.##}, {s.Z:0.##})");
            }
        }
    }

    [Fact]
    public void Aucune_creature_n_attend_sur_une_halte()
    {
        // Relecture des Vallons agrandis : sanglier4 attaquait dès l'arrivée à la ferme des champs. Chaque créature de départ est à plus de
        // 12 m d'une halte (la règle de la halte) et hors de sa portée d'engagement (bord à bord, avec une marge d'un mètre).
        ZoneDefinition zone = Zone();
        var scenario = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!["data"]!["combatants"]!.AsArray();
        foreach (SafePoint safe in zone.SafePoints)
        {
            double sx = safe.Position.X.Raw / 10000.0, sz = safe.Position.Z.Raw / 10000.0;
            foreach (JsonNode? c in scenario)
            {
                double x = (double)c!["position"]![0]!, z = (double)c["position"]![1]!, radius = (double)c["radius"]!;
                double aggro = c["behavior"]?["aggroRange"] is JsonNode a ? (double)a : 0;
                double d = Math.Sqrt((x - sx) * (x - sx) + (z - sz) * (z - sz));
                Assert.True(d > 12 && d - radius - 0.5 > aggro + 1, $"{c["id"]} à {d:0.#} m de la halte {safe.Id}");
            }
        }
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    public void La_porte_donne_sur_la_route(int x, int z)
    {
        // Maison de 8 × 6 m ; la route passe au sud (0, −1) ou à l'est (1, 0).
        var road = x == 0 ? new StyleRoad("r", new[] { (-20.0, -6.0), (20.0, -6.0) }, 4, false) : new StyleRoad("r", new[] { (9.0, -20.0), (9.0, 20.0) }, 4, false);
        Assert.Equal((x, z), TownRules.Facing(-4, -3, 4, 3, new[] { road }));
        Assert.Equal((0, -1), TownRules.Facing(-4, -3, 4, 3, Array.Empty<StyleRoad>()));
    }

    [Fact]
    public void Les_lieux_habites_et_cultives()
    {
        Assert.True(TownRules.Inhabited("village"));
        Assert.True(TownRules.Inhabited("bourg"));
        Assert.True(TownRules.Inhabited("hameau"));
        Assert.False(TownRules.Inhabited("champs"));
        Assert.True(TownRules.Farmland("champs"));
        Assert.False(TownRules.Farmland("vergers"));
    }

    public static IEnumerable<object[]> Batiments()
    {
        foreach ((string id, (double X, double Z)[] points) in Obstacles(Zone()))
            if (TownRules.BuildingFor(id) is StyleBuilding kind && kind != StyleBuilding.None && kind != StyleBuilding.Woods)
                yield return new object[] { id };
    }

    [Theory]
    [MemberData(nameof(Batiments))]
    public void Chaque_batiment_tient_sur_son_emprise(string id)
    {
        ZoneDefinition zone = Zone();
        (double X, double Z)[] points = Obstacles(zone).First(o => o.Id == id).Points;
        StyleBuilding kind = TownRules.BuildingFor(id);
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(zone);
        double x0 = points.Min(p => p.X), x1 = points.Max(p => p.X), z0 = points.Min(p => p.Z), z1 = points.Max(p => p.Z);
        (int, int) facing = TownRules.Facing(x0, z0, x1, z1, roads);
        StyleBuildingShapes shapes = TownShapes.Building(kind, points, id, facing, 2.5, 2, 2, 2);
        Assert.True(shapes.Top > 1.5, $"{id} : {shapes.Top} m");
        Assert.True(shapes.Walls.Vertices.Count + shapes.Stone.Vertices.Count + shapes.Wood.Vertices.Count > 0);
        foreach (StyleMesh mesh in shapes.All)
        {
            Assert.Equal(mesh.Vertices.Count, mesh.Uvs.Count);
            Assert.Equal(0, mesh.Triangles.Count % 3);
            Assert.All(mesh.Triangles, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
            Assert.All(mesh.Vertices, v => Assert.True(!double.IsNaN(v.X + v.Y + v.Z) && v.Y >= -0.01, $"{id} : sommet {v}"));
            foreach (StyleVector v in mesh.Vertices)
            {
                // Sous 2 m (là où l'on marche), seuls les reliefs (appuis de fenêtre, colombages, volets) sortent de l'emprise de l'obstacle, de
                // 12 cm au plus (moins que le rayon d'un corps, qui ne s'en approche pas plus), et les marches devant les portes (34 cm, sous
                // 50 cm de haut) ; plus haut, les débords restent à 1,25 m au plus.
                double outside = -StyleRules.InsideDistance(v.X, v.Z, points);
                double allowed = v.Y > 2.0 ? 1.25 : v.Y <= 0.5 && ReferenceEquals(mesh, shapes.Stone) ? 0.36 : 0.12;
                Assert.True(outside < allowed, $"{id} : {v} sort de {outside:0.##} m de l'emprise");
            }
        }
        // Mêmes formes à chaque lancement.
        StyleBuildingShapes again = TownShapes.Building(kind, points, id, facing, 2.5, 2, 2, 2);
        Assert.Equal(shapes.All.Select(m => m.Vertices.Count), again.All.Select(m => m.Vertices.Count));
        Assert.Equal(shapes.Walls.Vertices, again.Walls.Vertices);
        // Pour regarder les bâtiments (EDS_BATIMENTS=dossier) : un fichier OBJ par bâtiment, une pièce par matière.
        string? dump = Environment.GetEnvironmentVariable("EDS_BATIMENTS");
        if (!string.IsNullOrEmpty(dump)) WriteObj(Path.Combine(dump, id + ".obj"), shapes);
    }

    [Fact]
    public void Le_toit_d_une_maison_couvre_ses_murs_et_sa_porte_regarde_la_route()
    {
        var footprint = new[] { (-4.0, -3.0), (4.0, -3.0), (4.0, 3.0), (-4.0, 3.0) };
        StyleBuildingShapes house = TownShapes.Building(StyleBuilding.House, footprint, "maison_test", (0, -1), 2.5, 2, 2, 2);
        (StyleVector wallMin, StyleVector wallMax) = house.Walls.Bounds();
        (StyleVector roofMin, StyleVector roofMax) = house.Roof.Bounds();
        Assert.True(roofMin.X < wallMin.X && roofMax.X > wallMax.X && roofMin.Z < wallMin.Z && roofMax.Z > wallMax.Z, "débords du toit");
        Assert.True(roofMax.Y > wallMax.Y - 0.01, "le toit est au-dessus des murs");
        // Faîte : la hauteur des murs plus la demi-largeur (3 m) fois la pente (0,85), sous la faîtière.
        Assert.InRange(roofMax.Y, TownRules.WallHeight(StyleBuilding.House) + 3 * 0.85, TownRules.WallHeight(StyleBuilding.House) + 3 * 0.85 + 0.4);
        // La porte (embrasure sombre de 1,1 m entre 0,45 et 2,6 m, au milieu du mur) est au sud, du côté de la route.
        var door = house.Dark.Vertices.Where(v => Math.Abs(v.X) < 0.56 && v.Y < 0.5 && Math.Abs(v.Z) > 2.5).ToList();
        Assert.NotEmpty(door);
        Assert.All(door, v => Assert.True(v.Z < -2.7, $"porte en {v}"));
        Assert.Contains(house.Wood.Vertices, v => Math.Abs(v.X) < 0.6 && v.Z < -2.8 && v.Z > -2.95 && v.Y > 2.4);   // vantail de planches
        Assert.Equal(StyleSurface.Plaster, house.WallSurface);
        Assert.Equal(StyleSurface.Stone, TownShapes.Building(StyleBuilding.Chapel, footprint, "c", (0, -1), 2.5, 2, 2, 2).WallSurface);
        Assert.Equal(StyleRoofLook.Slate, TownShapes.RoofLookOf(StyleBuilding.Chapel, "chapelle"));
        Assert.Equal(StyleRoofLook.Shingles, TownShapes.RoofLookOf(StyleBuilding.Barn, "grange"));
        Assert.Empty(TownShapes.Building(StyleBuilding.Woods, footprint, "b", (0, -1), 2.5, 2, 2, 2).All.SelectMany(m => m.Vertices));
    }

    [Fact]
    public void La_rue_pavee_couvre_la_route_et_rien_d_autre()
    {
        var road = new StyleRoad("place", new[] { (-67.0, -1.0), (-49.0, -1.0) }, 13, true);
        StyleMesh mesh = TownShapes.Pavement(road, 1.5);
        Assert.NotEmpty(mesh.Triangles);
        Assert.All(mesh.Vertices, v => Assert.Equal(TownShapes.PavementHeight, v.Y, 9));
        Assert.All(mesh.Vertices, v => Assert.True(TownRules.RoadAt(v.X, v.Z, new[] { road }).Edge < 1e-6, $"{v} hors de la rue"));
        // Le milieu, les bouts arrondis et un point du bord sont couverts (un triangle les contient).
        foreach ((double x, double z) in new[] { (-58.0, -1.0), (-72.0, -1.0), (-44.5, -1.0), (-58.0, 5.0) })
            Assert.True(Covered(mesh, x, z), $"({x}, {z}) sans pavés");
        Assert.False(Covered(mesh, -58, 6));
        Assert.Empty(TownShapes.Pavement(null!, 1).Vertices);
    }

    [Fact]
    public void Champs_labours_et_rangs_de_ble()
    {
        var ripe = new StyleField(0, 0, 16, 10, true, 0);
        var plowed = new StyleField(0, 0, 16, 10, false, 2);
        IReadOnlyList<(double X, double Z, double Yaw, double Scale)> wheat = TownShapes.CropSpots(ripe, 0.3);
        Assert.InRange(wheat.Count, 14 * 50, 15 * 54);
        Assert.All(wheat, p => Assert.True(p.X >= -0.1 && p.X <= 16.1 && p.Z >= 0 && p.Z <= 10, $"({p.X}, {p.Z}) hors du champ"));
        Assert.Equal(wheat, TownShapes.CropSpots(ripe, 0.3));
        Assert.Empty(TownShapes.CropSpots(plowed, 0.3));
        StyleMesh furrows = TownShapes.Furrows(plowed, 2);
        (StyleVector min, StyleVector max) = furrows.Bounds();
        Assert.Equal((0.0, 0.0, 10.0), (min.X, min.Z, max.Z));   // rangs le long de z : billons côte à côte en x
        Assert.InRange(max.X, 15.3, 16.0);
        Assert.Equal(TownShapes.FurrowHeight, max.Y, 9);
        Assert.Equal(0.01, min.Y, 9);
    }

    private static bool Covered(StyleMesh mesh, double x, double z)
    {
        for (int i = 0; i < mesh.Triangles.Count; i += 3)
        {
            StyleVector a = mesh.Vertices[mesh.Triangles[i]], b = mesh.Vertices[mesh.Triangles[i + 1]], c = mesh.Vertices[mesh.Triangles[i + 2]];
            double d1 = (x - b.X) * (a.Z - b.Z) - (a.X - b.X) * (z - b.Z), d2 = (x - c.X) * (b.Z - c.Z) - (b.X - c.X) * (z - c.Z), d3 = (x - a.X) * (c.Z - a.Z) - (c.X - a.X) * (z - a.Z);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(neg && pos)) return true;
        }
        return false;
    }

    private static ZoneLoadResult WithZone(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        mutate(root);
        return ZoneJsonReader.Read(root.ToJsonString(), Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData,
            ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!);
    }

    private static JsonArray Roads(JsonObject root) => root["data"]!["roads"]!.AsArray();

    [Fact]
    public void Routes_livrees_au_schema_3()
    {
        ZoneDefinition zone = Zone();
        Assert.Equal(7, zone.SchemaVersion);   // intérieurs (28 octobre 2026) : schéma 4, puis 5 (sous-lot V1.6 : chefs, familles), 6 (créature de la nuit), 7 (V1.8 : mélange) ; les routes restent
        Assert.Equal(ZoneValidator.CurrentSchemaVersion, zone.SchemaVersion);
        Road place = zone.Roads.Single(r => r.Kind == RoadKind.Cobble);
        Assert.Equal(("place_du_bourg", 12.0), (place.Id, place.Width.Raw / 10000.0));
        Assert.All(zone.Roads, r => Assert.InRange(r.Points.Count, 2, ZoneValidator.MaxRoadPoints));
        // Sans routes : une zone valide, sans route.
        ZoneLoadResult none = WithZone(r => r["data"]!.AsObject().Remove("roads"));
        Assert.True(none.Report.IsValid, none.Report.ToString());
        Assert.Empty(none.Zone!.Roads);
        // L'ancien schéma est refusé (pré-alpha : pas de migration).
        ZoneLoadResult old = WithZone(r => r["schemaVersion"] = 2);
        Assert.Contains(old.Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"));
        Assert.True(schema.IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement));
    }

    [Theory]
    [InlineData("kind", "\"gravier\"", "$.data.roads[0].kind", ValidationCode.InvalidValue)]
    [InlineData("width", "0.5", "$.data.roads[0].width", ValidationCode.OutOfRange)]
    [InlineData("width", "20.5", "$.data.roads[0].width", ValidationCode.OutOfRange)]
    [InlineData("points", "[[0, 0]]", "$.data.roads[0].points", ValidationCode.OutOfRange)]
    [InlineData("points", "[[0, 0], [150, 0]]", "$.data.roads[0].points[1]", ValidationCode.OutOfRange)]
    [InlineData("points", "[[0, 0], [0, 0]]", "$.data.roads[0].points[1]", ValidationCode.InvalidValue)]
    [InlineData("id", "\"grand_rue\"", "$.data.roads[2].id", ValidationCode.DuplicateId)]
    public void Route_invalide_refusee_avec_son_chemin(string field, string json, string path, ValidationCode code)
    {
        // Un champ de la première route est remplacé ; pour un identifiant en double, celui de la troisième (la grand-rue est la première).
        ZoneLoadResult r = WithZone(root => Roads(root)[field == "id" ? 2 : 0]![field] = JsonNode.Parse(json));
        Assert.False(r.Report.IsValid);
        Assert.Null(r.Zone);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Routes_trop_nombreuses_ou_incompletes_refusees()
    {
        ZoneLoadResult many = WithZone(root =>
        {
            JsonArray roads = Roads(root);
            for (int i = roads.Count; i <= ZoneValidator.MaxRoads; i++)
            {
                JsonObject copy = roads[0]!.DeepClone().AsObject();
                copy["id"] = "route" + i;
                roads.Add(copy);
            }
        });
        Assert.Contains(many.Report.Errors, e => e.Path == "$.data.roads" && e.Code == ValidationCode.LimitExceeded);
        ZoneLoadResult longRoad = WithZone(root => Roads(root)[0]!["points"] = new JsonArray(Enumerable.Range(0, ZoneValidator.MaxRoadPoints + 1)
            .Select(i => (JsonNode?)new JsonArray(-90 + i, 0)).ToArray()));
        Assert.Contains(longRoad.Report.Errors, e => e.Path == "$.data.roads[0].points" && e.Code == ValidationCode.LimitExceeded);
        foreach (string field in new[] { "id", "kind", "width", "points" })
        {
            ZoneLoadResult missing = WithZone(root => Roads(root)[0]!.AsObject().Remove(field));
            Assert.Contains(missing.Report.Errors, e => e.Path == "$.data.roads[0]." + field && e.Code == ValidationCode.MissingField);
        }
    }

    private static void WriteObj(string path, StyleBuildingShapes shapes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = new StringBuilder();
        int offset = 1;
        string[] names = { "murs", "pierre", "bois", "toit", "toile", "bandes", "sombre" };
        int k = 0;
        foreach (StyleMesh mesh in shapes.All)
        {
            text.Append("o ").Append(names[k++]).Append('\n');
            foreach (StyleVector v in mesh.Vertices) text.Append(string.Format(CultureInfo.InvariantCulture, "v {0:0.####} {1:0.####} {2:0.####}\n", v.X, v.Y, v.Z));
            for (int i = 0; i < mesh.Triangles.Count; i += 3)
                text.Append(string.Format(CultureInfo.InvariantCulture, "f {0} {1} {2}\n", mesh.Triangles[i] + offset, mesh.Triangles[i + 1] + offset, mesh.Triangles[i + 2] + offset));
            offset += mesh.Vertices.Count;
        }
        text.Append(string.Format(CultureInfo.InvariantCulture, "# surface {0} toit {1} toile {2}\n", shapes.WallSurface, shapes.RoofLook, shapes.ClothColor));
        File.WriteAllText(path, text.ToString());
    }
}
