using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.30.3 : monuments, étals et maisons du Bourg d'Aube fortifié habillés des modèles de l'atelier (église P01, parvis P02, cimetière
/// P03, halle P04, maison du prévôt P05, corps de garde P06, écuries P07, grenier P08, lavoir P09, four banal P10, fontaine P11, puits
/// P12, étals R11 à R18, maisons types H01 à H12). Placement et orientation vérifiés sur les vraies données de zone ; écarts connus
/// écrits en dur. Données de zone et moteur inchangés. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2303BourgMonumentsMaisonsTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private static IReadOnlyList<SpaceObstacle> Obstacles => Vallons.Value.Setup.Space!.Obstacles;

    /// <summary>Routes de la zone comme la carte les donne au décor.</summary>
    private static readonly Lazy<List<StyleRoad>> Roads = new(() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!["roads"]!.AsArray()
            .Select(r => new StyleRoad(r!["id"]!.GetValue<string>(), r["points"]!.AsArray().Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToList(),
                r["width"]!.GetValue<double>(), false)).ToList());

    private static readonly Lazy<BourgDress> Dress = new(() => BourgModels.Dress(Obstacles, Roads.Value));

    private static IEnumerable<BourgPiece> Inside => Dress.Value.Pieces.Where(p => !p.Piece.StartsWith("f", StringComparison.Ordinal));

    private static (double X0, double X1, double Z0, double Z1) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 1e4), o.Points.Max(p => p.X.Raw / 1e4), o.Points.Min(p => p.Z.Raw / 1e4), o.Points.Max(p => p.Z.Raw / 1e4));

    private static (double X0, double X1, double Z0, double Z1) Box(string id) => Box(Obstacles.Single(o => o.Id == id));

    private static bool Overlaps((double X0, double X1, double Z0, double Z1) a, (double X0, double X1, double Z0, double Z1) b) =>
        a.X0 < b.X1 && a.X1 > b.X0 && a.Z0 < b.Z1 && a.Z1 > b.Z0;

    /// <summary>Débord (m) d'un pied hors d'une boîte, du côté où il dépasse le plus.</summary>
    private static double Overflow((double X0, double X1, double Z0, double Z1) f, (double X0, double X1, double Z0, double Z1) b) =>
        new[] { b.X0 - f.X0, f.X1 - b.X1, b.Z0 - f.Z0, f.Z1 - b.Z1, 0 }.Max();

    private static string Folder(string group) => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Decor", group);

    [Fact]
    public void Every_building_of_the_town_is_dressed()
    {
        var town = Obstacles.Where(o => o.Id.Contains("_ville", StringComparison.Ordinal) && !BourgModels.IsWall(o.Id) && !BourgModels.IsTower(o.Id)
            && !o.Id.StartsWith("panneau", StringComparison.Ordinal)).Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();
        Assert.Equal(town, Dress.Value.Needs.Keys.Where(k => !BourgModels.IsWall(k) && !BourgModels.IsTower(k)).OrderBy(i => i, StringComparer.Ordinal));
        Assert.Equal(119, town.Count(i => i.StartsWith("maison_ville_", StringComparison.Ordinal) && i != "maison_ville_grande_maison"));
        Assert.Equal(new[] { BourgModels.Church }, Dress.Value.Needs["tour_ville_clocher"]);
        Assert.Equal(new[] { BourgModels.Provost, BourgModels.Guardhouse, BourgModels.Granary, BourgModels.Oven }.OrderBy(p => p, StringComparer.Ordinal),
            Dress.Value.Needs["maison_ville_grande_maison"]);
    }

    [Fact]
    public void Houses_fit_their_footprint_gable_to_the_nearest_road_on_a_short_side()
    {
        var used = new Dictionary<string, int>();
        foreach (BourgPiece h in Inside.Where(p => p.Piece.StartsWith("h", StringComparison.Ordinal)))
        {
            var b = Box(h.Covers!);
            var f = BourgModels.Footprint(h.Piece, h.X, h.Z, h.Yaw);
            Assert.True(Overflow(f, b) <= BourgModels.HouseTolerance + 1e-9, h.Covers + " déborde " + h.Piece);
            Assert.Equal(((b.X0 + b.X1) / 2, (b.Z0 + b.Z1) / 2), (h.X, h.Z));
            // Pignon sur le petit côté (toutes les emprises sont plus longues en x).
            Assert.True(h.Yaw is 90 or -90, h.Covers + " " + h.Yaw);
            Assert.True(BourgModels.Box(h.Piece)!.Value.Y1 - BourgModels.Box(h.Piece)!.Value.Y0 >= BourgModels.Box(h.Piece)!.Value.X1 - BourgModels.Box(h.Piece)!.Value.X0, h.Piece);
            used[h.Piece] = used.TryGetValue(h.Piece, out int n) ? n + 1 : 1;
        }
        Assert.Equal(119, used.Values.Sum());
        // H09 (11 m de façade) et H12 (5,60 m, trop petite) ne tiennent sur aucune emprise : livrées, pas posées.
        Assert.DoesNotContain("h09_notable_large", used.Keys);
        Assert.True(used.Count >= 9, string.Join(", ", used.Select(u => u.Key + " " + u.Value)));
        // Pas deux fois de suite la même maison sur plus de la moitié des emprises d'une même taille.
        Assert.True(used.Values.Max() <= 40, string.Join(", ", used.Select(u => u.Key + " " + u.Value)));
    }

    [Fact]
    public void Monuments_stand_on_their_obstacles_with_the_known_overflows()
    {
        var expected = new (string Piece, string Obstacle, double Yaw, double Overflow)[]
        {
            (BourgModels.Church, "chapelle_ville_eglise", 180, 2.1),        // 18 × 34 dans 16 × 30 : 1 m de chaque côté, 2,1 m au sud
            (BourgModels.Hall, "grange_ville_halle", 0, 0),
            (BourgModels.Stables, "grange_ville_ecurie", 180, 8),             // U de 30 × 24 sur 30 × 16 : 8 m au nord
            (BourgModels.Well, "puits_ville_marche", 180, 0.5),
            (BourgModels.Provost, "maison_ville_grande_maison", 180, 0.57),  // auvent de la façade
            (BourgModels.Guardhouse, "maison_ville_grande_maison", 180, 0.57),
            (BourgModels.Oven, "maison_ville_grande_maison", 180, 1.5),   // auvent devant la bouche du four
            (BourgModels.Granary, "maison_ville_grande_maison", 180, 0),
        };
        foreach (var e in expected)
        {
            BourgPiece p = Inside.Single(q => q.Piece == e.Piece);
            Assert.Equal(e.Obstacle, p.Covers);
            Assert.Equal(e.Yaw, p.Yaw, 6);
            Assert.Equal(e.Overflow, Overflow(BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw), Box(e.Obstacle)), 2);
        }
        // Les bâtiments de la cour du prévôt ne se chevauchent pas.
        var court = Inside.Where(p => p.Covers == "maison_ville_grande_maison").Select(p => BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw)).ToList();
        for (int i = 0; i < court.Count; i++)
            for (int j = i + 1; j < court.Count; j++)
                Assert.False(Overlaps(court[i], court[j]));
        // Les six étals de la place, chacun tourné vers la route ; leurs modèles (3 × 2,4) tiennent dans les obstacles (4 × 3).
        for (int i = 1; i <= 6; i++)
        {
            BourgPiece s = Inside.Single(q => q.Covers == "etal_ville_" + i);
            Assert.Equal(BourgModels.Stalls[i - 1], s.Piece);
            Assert.Equal(0, Overflow(BourgModels.Footprint(s.Piece, s.X, s.Z, s.Yaw), Box(s.Covers!)), 6);
        }
    }

    [Fact]
    public void Nothing_lands_on_another_obstacle_a_road_or_a_door()
    {
        var doors = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!["doors"]!.AsArray()
            .Select(d => (X: d!["position"]![0]!.GetValue<double>(), Z: d["position"]![1]!.GetValue<double>())).ToList();
        Assert.Equal(9, doors.Count);
        foreach (BourgPiece p in Inside)
        {
            var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
            var own = new HashSet<string> { p.Covers ?? string.Empty };
            if (p.Piece == BourgModels.Church) own.Add("tour_ville_clocher");
            var hits = Obstacles.Where(o => !own.Contains(o.Id) && Overlaps(f, Box(o))).Select(o => o.Id).ToList();
            Assert.True(hits.Count == 0, p.Piece + " sur " + string.Join(", ", hits));
            // Le décor (parvis, cimetière, fontaine, lavoir, étals de plus) reste hors des routes ; un bâtiment habillé peut dépasser son
            // obstacle de son débord, jamais sur une route.
            var corners = new[] { (f.X0, f.Z0), (f.X0, f.Z1), (f.X1, f.Z0), (f.X1, f.Z1), ((f.X0 + f.X1) / 2, (f.Z0 + f.Z1) / 2) };
            Assert.True(corners.All(c => TownRules.RoadAt(c.Item1, c.Item2, Roads.Value).Edge > 0), p.Piece + " sur une route");
            Assert.DoesNotContain(doors, d => d.X > f.X0 - 1 && d.X < f.X1 + 1 && d.Z > f.Z0 - 1 && d.Z < f.Z1 + 1);
        }
        Assert.Equal(6, Inside.Count(p => p.Covers == null));   // parvis, cimetière, fontaine, lavoir, étals des étoffes et des poteries
    }

    [Fact]
    public void Shipped_models_are_light_game_exports()
    {
        foreach ((string group, int count, IEnumerable<string> pieces) in new[]
        {
            ("bourg_monuments", 20, new[] { BourgModels.Church, BourgModels.Parvis, BourgModels.Cemetery, BourgModels.Hall, BourgModels.Provost,
                BourgModels.Guardhouse, BourgModels.Stables, BourgModels.Granary, BourgModels.Washhouse, BourgModels.Oven, BourgModels.Fountain,
                BourgModels.Well }.Concat(BourgModels.Stalls)),
            ("bourg_maisons", 13, (IEnumerable<string>)BourgModels.Houses),
        })
        {
            Assert.Equal(count * 3, Directory.GetFiles(Folder(group)).Length);
            foreach (string piece in pieces)
            {
                string fbx = Path.Combine(Folder(group), "bourg_" + piece + ".fbx");
                Assert.True(File.Exists(fbx), fbx);
                Assert.NotNull(BourgModels.Box(piece));
                Assert.True(new FileInfo(Path.Combine(Folder(group), "bourg_" + piece + "_couleur.jpg")).Length < 600_000);
                Assert.True(new FileInfo(Path.Combine(Folder(group), "bourg_" + piece + "_normale.png")).Length < 700_000);
            }
            Assert.True(Directory.GetFiles(Folder(group)).Sum(f => new FileInfo(f).Length) < 15_000_000);
        }
    }
}
