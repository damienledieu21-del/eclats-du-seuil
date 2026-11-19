using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.30.2 : l'enceinte du Bourg d'Aube fortifié habillée des modèles de l'atelier (palissade F01, F03, F04, ronde couverte F05, tours F06
/// et F07, portes F08, F09, F10). Placement vérifié sur les vraies données de zone (scenarios/zones/vallons.json, routes de
/// data/v0/world/zones/vallons.json) : chaque pan et chaque tour habillés, palissade continue d'un coin à l'autre, faces extérieures
/// dehors, passages des portes dans les ouvertures des données et sur la route, vantaux rabattus hors du passage, rien sur une maison ni
/// sur une route hors des portes. Écarts connus écrits en dur (palissade et tours de porte traversables là où les données laissent passer).
/// Données de zone et moteur inchangés. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2302BourgEnceinteTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private static IReadOnlyList<SpaceObstacle> Obstacles => Vallons.Value.Setup.Space!.Obstacles;

    private static readonly Lazy<BourgDress> All = new(() => BourgModels.Dress(Obstacles));

    /// <summary>V2.30.3 : l'habillage ne garde ici que l'enceinte (pièces F, pans et tours).</summary>
    private static readonly Lazy<BourgDress> Dress = new(() => new BourgDress(All.Value.Pieces.Where(p => p.Piece.StartsWith("f", StringComparison.Ordinal) && !BourgModels.IsStreetDecor(p.Piece) && !BourgModels.IsFacadeOrMoat(p.Piece)).ToList(),
        All.Value.Needs.Where(n => BourgModels.IsWall(n.Key) || BourgModels.IsTower(n.Key)).ToDictionary(n => n.Key, n => n.Value)));

    private static (double X0, double X1, double Z0, double Z1) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 1e4), o.Points.Max(p => p.X.Raw / 1e4), o.Points.Min(p => p.Z.Raw / 1e4), o.Points.Max(p => p.Z.Raw / 1e4));

    private static (double X0, double X1, double Z0, double Z1) Box(string id) => Box(Obstacles.Single(o => o.Id == id));

    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Decor", "bourg_enceinte");

    /// <summary>Routes de la zone : (identifiant, largeur, points).</summary>
    private static List<(string Id, double Width, List<(double X, double Z)> Points)> Roads()
    {
        var zone = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!["roads"]!.AsArray();
        return zone.Select(r => (r!["id"]!.GetValue<string>(), r["width"]!.GetValue<double>(),
            r["points"]!.AsArray().Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToList())).ToList();
    }

    /// <summary>Distance d'un point à l'axe d'une route.</summary>
    private static double Distance(List<(double X, double Z)> line, double x, double z)
    {
        double best = double.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            (double ax, double az) = line[i];
            (double bx, double bz) = line[i + 1];
            double dx = bx - ax, dz = bz - az, t = Math.Clamp(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz), 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(ax + t * dx - x, 2) + Math.Pow(az + t * dz - z, 2)));
        }
        return best;
    }

    private static bool Overlaps((double X0, double X1, double Z0, double Z1) a, (double X0, double X1, double Z0, double Z1) b, double margin = 0) =>
        a.X0 < b.X1 - margin && a.X1 > b.X0 + margin && a.Z0 < b.Z1 - margin && a.Z1 > b.Z0 + margin;

    [Fact]
    public void Every_wall_and_tower_of_the_town_is_dressed_and_nothing_else()
    {
        var expected = Obstacles.Where(o => o.Id.StartsWith("palissade_ville_", StringComparison.Ordinal)
            || o.Id.StartsWith("tour_ville_", StringComparison.Ordinal) && o.Id != "tour_ville_clocher").Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal);
        Assert.Equal(expected, Dress.Value.Needs.Keys.OrderBy(i => i, StringComparer.Ordinal));
        Assert.Equal(19, Dress.Value.Needs.Count);   // 8 pans, 11 tours (le clocher est celui de l'église) ; V2.40 : porte nord (un pan, deux tours)
        // Les deux tours de la porte de l'Ouest sont sous la porte à tours F09 ; les autres sont des tours de flanquement.
        Assert.Equal(new[] { BourgModels.WestGate }, Dress.Value.Needs["tour_ville_porte_ouest_sud"]);
        Assert.Equal(new[] { BourgModels.WestGate }, Dress.Value.Needs["tour_ville_porte_ouest_nord"]);
        foreach (string t in new[] { "nord_ouest", "nord_est", "sud_ouest", "sud_est", "porte_est_sud", "porte_est_nord", "poterne_sud" })
            Assert.Equal(new[] { BourgModels.FlankTower }, Dress.Value.Needs["tour_ville_" + t]);
        // Hors du Bourg (le cœur, le hameau) : rien.
        Assert.Empty(BourgModels.Dress(Obstacles.Where(o => !o.Id.Contains("_ville", StringComparison.Ordinal)).ToList()).Pieces);
        // V2.30.3 : l'église (son clocher) et les maisons ne prennent aucune pièce de l'enceinte.
        Assert.All(All.Value.Needs.Where(n => !BourgModels.IsWall(n.Key) && !BourgModels.IsTower(n.Key) && !BourgModels.IsDitch(n.Key)), n => Assert.DoesNotContain(n.Value, p => p.StartsWith("f", StringComparison.Ordinal)));
    }

    [Fact]
    public void Each_line_is_closed_from_corner_to_corner_with_the_outer_face_outside()
    {
        var pieces = Dress.Value.Pieces;
        var lines = new (bool AlongX, double Line, double From, double To, double Yaw)[]
        {
            (true, 180, -1310, -710, 0), (true, -320, -1310, -710, 180), (false, -710, -320, 180, 90), (false, -1310, -320, 180, -90),
        };
        foreach (var l in lines)
        {
            var spans = new List<(double A, double B)>();
            foreach (BourgPiece p in pieces.Where(p => p.Piece != BourgModels.Walkway && p.Piece != BourgModels.WatchTower))
            {
                var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
                bool on = l.AlongX ? f.Z0 <= l.Line && f.Z1 >= l.Line : f.X0 <= l.Line && f.X1 >= l.Line;
                if (!on) continue;
                spans.Add(l.AlongX ? (f.X0, f.X1) : (f.Z0, f.Z1));
                if (p.Piece.StartsWith("f0", StringComparison.Ordinal) && p.Piece != BourgModels.FlankTower && p.Piece != BourgModels.WatchTower)
                {
                    // Module ou porte : centré sur la ligne de la palissade, face extérieure (+z du modèle) dehors.
                    Assert.Equal(l.Line, l.AlongX ? p.Z : p.X, 6);
                    Assert.Equal(l.Yaw, p.Yaw, 6);
                }
            }
            double at = l.From;
            foreach (var s in spans.OrderBy(s => s.A))
            {
                Assert.True(s.A <= at + 1e-6, $"jour dans la ligne {l.Line} à {at}");
                at = Math.Max(at, s.B);
            }
            Assert.True(at >= l.To - 1e-6);
        }
        int modules = pieces.Count(p => p.Piece.Contains("palissade", StringComparison.Ordinal));
        Assert.InRange(modules, 340, 380);   // ≈ 2 200 m de tour en modules de 6 m
        Assert.Equal(14, pieces.Count(p => p.Piece == BourgModels.WallStairs));
        Assert.InRange(pieces.Count(p => p.Piece == BourgModels.WallBroken || p.Piece == BourgModels.WallMended), 20, 50);
    }

    [Fact]
    public void Gates_stand_in_the_openings_their_passage_on_the_road_and_their_leaves_folded_out_of_it()
    {
        var roads = Roads();
        var gates = Dress.Value.Pieces.Where(p => p.Piece is BourgModels.EastGate or BourgModels.WestGate or BourgModels.NorthGate).ToList();
        Assert.Equal(4, gates.Count);   // V2.40 : la porte nord
        var expected = new (string Piece, double X, double Z, double Yaw, double From, double To, string Road, double Walk)[]
        {
            // V2.30.5 (décision de Damien du 16 novembre 2026) : ouvertures des données resserrées sur les passages des modèles (est 4,2 m,
            // ouest 6 m, poterne 3 m) : plus rien de traversable à côté du passage.
            (BourgModels.EastGate, -710, -50, 90, -52.1, -47.9, "route_d_ambre_ouest", 0),
            (BourgModels.WestGate, -1310, -90, -90, -93, -87, "route_d_ambre_ouest", 0),
            (BourgModels.NorthGate, -1160, -320, 180, -1161.5, -1158.5, "chemin_des_vignes", 0),
            // V2.40 (décision de Damien du 16 novembre 2026) : porte nord, comme la poterne : ouverture de 3 m (x −941,5 à −938,5), F10 au milieu.
            (BourgModels.NorthGate, -940, 180, 0, -941.5, -938.5, "rue_de_la_porte_nord", 0),
        };
        foreach (var e in expected)
        {
            BourgPiece g = gates.Single(p => p.Piece == e.Piece && Math.Abs(p.X - e.X) < 1 && Math.Abs(p.Z - e.Z) < 1);
            Assert.Equal((e.X, e.Z, e.Yaw), (g.X, g.Z, g.Yaw));
            Assert.Null(g.Covers);
            double along = e.Piece == BourgModels.NorthGate ? g.X : g.Z, half = BourgModels.Passage(e.Piece) / 2;
            // Le passage du modèle est tout entier dans l'ouverture des données, et la route le traverse en son milieu.
            Assert.True(along - half >= e.From && along + half <= e.To);
            var road = roads.Single(r => r.Id == e.Road);
            Assert.True(Distance(road.Points, g.X, g.Z) < 0.5, e.Piece + " hors de la route");
            Assert.True(road.Width <= BourgModels.Passage(e.Piece) + 1.5);
            // Largeur de l'ouverture des données couverte par le modèle hors du passage, de chaque côté (écart connu).
            var f = BourgModels.Footprint(e.Piece, g.X, g.Z, g.Yaw);
            double modelFrom = e.Piece == BourgModels.NorthGate ? f.X0 : f.Z0, modelTo = e.Piece == BourgModels.NorthGate ? f.X1 : f.Z1;
            double walled = Math.Min(along - half - Math.Max(e.From, modelFrom), Math.Min(e.To, modelTo) - along - half);
            Assert.Equal(e.Walk, walled, 1);
        }
        // Vantaux rabattus vers la ville (−z du modèle), hors du gabarit du passage ; herse levée de 5,70 m.
        foreach (string gate in new[] { BourgModels.EastGate, BourgModels.WestGate, BourgModels.NorthGate })
        {
            double half = BourgModels.Passage(gate) / 2, leaf = half;
            foreach (BourgModels.OpenPart part in BourgModels.OpenParts(gate).Where(p => p.Turn != 0))
            {
                Assert.Equal(half, Math.Abs(part.X), 6);   // charnière au bord du passage
                double a = part.Turn * Math.PI / 180, dir = part.X > 0 ? -1 : 1;
                double endX = part.X + dir * leaf * Math.Cos(a), endZ = part.Z - dir * leaf * Math.Sin(a);
                Assert.True(endZ < -leaf * 0.9, gate + " " + part.Name + " pas vers la ville");
                Assert.True(Math.Abs(endX) >= half - 0.01, gate + " " + part.Name + " dans le passage");
            }
        }
        Assert.Equal(BourgModels.PortcullisLift, BourgModels.OpenParts(BourgModels.WestGate).Single(p => p.Name == "Herse").Lift);
    }

    [Fact]
    public void Towers_fit_their_obstacles_and_face_outside()
    {
        foreach (BourgPiece t in Dress.Value.Pieces.Where(p => p.Piece == BourgModels.FlankTower))
        {
            var box = Box(t.Covers!);
            var f = BourgModels.Footprint(t.Piece, t.X, t.Z, t.Yaw);
            Assert.True(f.X0 >= box.X0 && f.X1 <= box.X1 && f.Z0 >= box.Z0 && f.Z1 <= box.Z1, t.Covers + " déborde");
            (double cx, double cz) = (-1010, -70);
            Assert.True(Math.Sin(t.Yaw * Math.PI / 180) * (t.X - cx) + Math.Cos(t.Yaw * Math.PI / 180) * (t.Z - cz) > 0, t.Covers + " tournée vers la ville");
        }
        Assert.Equal(9, Dress.Value.Pieces.Count(p => p.Piece == BourgModels.FlankTower));   // V2.40 : les deux tours de la porte nord
        // Quatre tours de guet sur la ronde (plan illustré), au milieu des plus longs pans, 1,5 m en retrait vers la ville.
        var watch = Dress.Value.Pieces.Where(p => p.Piece == BourgModels.WatchTower).OrderBy(p => p.X).ThenBy(p => p.Z).Select(p => (p.X, p.Z, p.Yaw)).ToList();
        Assert.Equal(new[] { (-1308.5, 46.5, -90.0), (-1125.75, 178.5, 0.0), (-934.25, -318.5, 180.0), (-711.5, -186.05, 90.0) }, watch);   // V2.30.5 : pans allongés ; V2.40 : le pan nord le plus long s'arrête à la porte nord
    }

    [Fact]
    public void Nothing_of_the_enclosure_lands_on_a_building_or_on_a_road_away_from_the_gates()
    {
        var others = Obstacles.Where(o => !BourgModels.IsWall(o.Id) && !BourgModels.IsTower(o.Id)).ToList();
        var roads = Roads();
        foreach (BourgPiece p in Dress.Value.Pieces)
        {
            var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
            Assert.DoesNotContain(others, o => Overlaps(f, Box(o)));
            bool gate = p.Piece is BourgModels.EastGate or BourgModels.WestGate or BourgModels.NorthGate;
            if (gate) continue;
            // Hors des portes, aucune pièce ne pose un coin ni son centre sur une route (bord compris).
            var points = new[] { (p.X, p.Z), (f.X0, f.Z0), (f.X0, f.Z1), (f.X1, f.Z0), (f.X1, f.Z1) };
            foreach (var r in roads)
                Assert.DoesNotContain(points, q => Distance(r.Points, q.Item1, q.Item2) < r.Width / 2);
        }
        // La ronde couverte (garde-corps et auvent) ne coiffe que les modules qui bordent une porte, 1,04 m vers la ville.
        var walkways = Dress.Value.Pieces.Where(p => p.Piece == BourgModels.Walkway).ToList();
        Assert.InRange(walkways.Count, 8, 24);
        Assert.All(walkways, w => Assert.Null(w.Covers));
    }

    [Fact]
    public void Shipped_models_are_light_game_exports_with_their_own_atlas()
    {
        var used = Dress.Value.Pieces.Select(p => p.Piece).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(10, used.Count);
        foreach (string piece in used)
        {
            string fbx = Path.Combine(Folder, "bourg_" + piece + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.Equal("bourg_enceinte", AtelierDecor.OwnDecorOf(fbx));
            Assert.True(AtelierDecor.IsPiece("bourg_" + piece, piece));
            Assert.True(ImageSize(Path.Combine(Folder, "bourg_" + piece + "_couleur.jpg")) <= 1024);
            Assert.True(ImageSize(Path.Combine(Folder, "bourg_" + piece + "_normale.png")) <= 512);
            Assert.NotNull(BourgModels.Box(piece));
        }
        // LOD1 et atlas seulement : ni LOD0, ni .blend, ni rendu ; la courbe F02 (cercle de 600 m) n'est pas livrée (pans droits).
        Assert.Equal(45, Directory.GetFiles(Folder).Length);   // V2.30.5 : fossé, talus et ponts en plus
        Assert.True(Directory.GetFiles(Folder).Sum(f => new FileInfo(f).Length) < 10_000_000);
    }

    /// <summary>
    /// Relecture indépendante des PR #195 à #199 : la vue d'un secteur n'a qu'une fenêtre de l'aire (≈ 384 m) ; l'habillage est calculé une
    /// fois sur l'aire entière (<see cref="AdventureMap.Bourg"/>) et la fenêtre en pose ce qui la touche. Au milieu du côté ouest (aucune
    /// porte dans la fenêtre) et au milieu du côté nord : mêmes pièces que l'aire entière, faces extérieures dehors, aucune porte fantôme.
    /// </summary>
    [Fact]
    public void A_sector_window_poses_the_whole_town_dress_restricted_to_it()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v2302_" + Guid.NewGuid().ToString("N"));
        try
        {
            AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(folder, "partie.json"));
            a.Advance(0.05);
            AdventureMap whole = a.RegionMap;
            foreach ((double cx, double cz, double yaw) in new[] { (-1310.0, 60.0, -90.0), (-1100.0, 180.0, 0.0) })   // V2.40 : loin de la porte nord (x = −940)
            {
                long r(double v) => (long)Math.Round(v * 10000);
                CombatSpace space = AdventureMap.WindowOf(whole.Space, (r(cx - 64), r(cz - 64), r(cx + 64), r(cz + 64)));
                var window = new AdventureMap(space, whole.Places, whole.Roads, whole.Clearings, whole.Doors, whole.Indoor, whole.Speakers, whole.Gates, whole.Region)
                    { Whole = whole };
                Assert.Same(whole.Bourg, window.Bourg);
                double x0 = space.Min.X.Raw / 1e4, z0 = space.Min.Z.Raw / 1e4, x1 = space.Max.X.Raw / 1e4, z1 = space.Max.Z.Raw / 1e4;
                var near = window.Bourg.Near(x0, z0, x1, z1, 0).ToList();
                Assert.Equal(whole.Bourg.Pieces.Where(p => near.Contains(p)), near);
                var wall = near.Where(p => p.Piece.Contains("palissade", StringComparison.Ordinal) && (yaw == 0 ? Math.Abs(p.Z - cz) < 0.01 : Math.Abs(p.X - cx) < 0.01)).ToList();
                Assert.True(wall.Count >= 20);
                Assert.All(wall, p => Assert.Equal(yaw, p.Yaw, 6));
                Assert.DoesNotContain(near, p => p.Piece is BourgModels.EastGate or BourgModels.WestGate or BourgModels.NorthGate);
                // Calculé sur la fenêtre seule (l'erreur relevée), le centre de l'enceinte serait faux.
                Assert.NotEqual(BourgModels.Dress(space.Obstacles).Pieces.Count(p => p.Piece.Contains("palissade", StringComparison.Ordinal)), whole.Bourg.Pieces.Count(p => p.Piece.Contains("palissade", StringComparison.Ordinal)));
            }
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    /// <summary>Seules les grandes pièces (6 m et plus) cachent le joueur ; modules de palissade et petits décors non (téléphone).</summary>
    [Fact]
    public void Only_tall_pieces_occlude()
    {
        Assert.False(BourgModels.Occludes(BourgModels.Wall));
        Assert.False(BourgModels.Occludes(BourgModels.Walkway));
        Assert.True(BourgModels.Occludes(BourgModels.FlankTower));
        Assert.True(BourgModels.Occludes(BourgModels.EastGate));
        Assert.True(BourgModels.Occludes(BourgModels.WatchTower));
    }

    /// <summary>Plus grand côté (px) d'un PNG ou d'un JPEG, lu dans son en-tête.</summary>
    internal static int ImageSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (b[0] == 0x89 && b[1] == 'P') return Math.Max(b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19], b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
        for (int i = 2; i + 9 < b.Length;)
        {
            if (b[i] != 0xFF) { i++; continue; }
            byte m = b[i + 1];
            if (m >= 0xC0 && m <= 0xC3) return Math.Max(b[i + 5] << 8 | b[i + 6], b[i + 7] << 8 | b[i + 8]);
            i += 2 + (b[i + 2] << 8 | b[i + 3]);
        }
        throw new InvalidDataException(path);
    }
}
