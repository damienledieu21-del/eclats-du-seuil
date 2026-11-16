using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.30.4 : décor des rues du Bourg d'Aube fortifié (voirie R01 à R03, charrois et mobilier R19 à R24, accessoires de rue R25 à R28,
/// accessoires de garde F16 à F19, cours H21 à H25), posé par une table de décor : aucun obstacle ne lui correspond. Vérifié sur les
/// vraies données de zone : rien sur un obstacle (les sols passent sous les étals et le puits), rien sur une route hors des sols, aucun
/// accessoire sur un autre, ni devant une porte, ni dans le passage d'une porte de la ville. Données de zone et moteur inchangés. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2304BourgRuesTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private static IReadOnlyList<SpaceObstacle> Obstacles => Vallons.Value.Setup.Space!.Obstacles;

    private static JsonNode Zone => JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!;

    private static readonly Lazy<List<StyleRoad>> Roads = new(() => Zone["roads"]!.AsArray()
        .Select(r => new StyleRoad(r!["id"]!.GetValue<string>(), r["points"]!.AsArray().Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToList(),
            r["width"]!.GetValue<double>(), false)).ToList());

    private static readonly Lazy<BourgDress> Dress = new(() => BourgModels.Dress(Obstacles, Roads.Value));

    private static List<BourgPiece> Streets => Dress.Value.Pieces.Where(p => BourgModels.IsStreetDecor(p.Piece)).ToList();

    private static (double X0, double X1, double Z0, double Z1) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 1e4), o.Points.Max(p => p.X.Raw / 1e4), o.Points.Min(p => p.Z.Raw / 1e4), o.Points.Max(p => p.Z.Raw / 1e4));

    private static bool Overlaps((double X0, double X1, double Z0, double Z1) a, (double X0, double X1, double Z0, double Z1) b) =>
        a.X0 < b.X1 && a.X1 > b.X0 && a.Z0 < b.Z1 && a.Z1 > b.Z0;

    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Decor", "bourg_rues");

    [Fact]
    public void Street_decor_is_placed_on_the_real_town()
    {
        var count = Streets.GroupBy(p => p.Piece.Substring(0, 3)).ToDictionary(g => g.Key, g => g.Count());
        string all = string.Join(", ", count.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Key + " " + c.Value));
        Assert.True(count["r01"] >= 200, all);       // pavés de la place et bordures
        Assert.True(count["r02"] >= 60, all);        // route d'Ambre pavée entre les portes
        Assert.True(count["r03"] >= 20, all);        // chemin des vignes
        Assert.Equal(3, count["r25"]);               // un panneau par porte
        // Braseros dedans, bannières dehors, râtelier : là où la place est libre (tours, ronde et escaliers en prennent).
        Assert.True(count["f17"] >= 3 && count["f19"] >= 3 && count["f18"] >= 2, all);
        Assert.Equal(3, count["r19"]);               // charrettes à côté des étals
        Assert.Equal(1, count["f16"]);               // barrière de péage, ouverte, devant la porte de l'Est
        Assert.Equal(4, count["r20"]);               // chariots de caravane à la porte de l'Ouest
        Assert.True(count["r26"] + count.GetValueOrDefault("r27") + count.GetValueOrDefault("r28") >= 12, all);
        Assert.True(count.Keys.Count(k => k.StartsWith("h2", StringComparison.Ordinal)) >= 4, all);
        Assert.InRange(Streets.Count(p => p.Piece.StartsWith("h2", StringComparison.Ordinal)), 25, 40);   // une maison sur trois, moins les places prises
        Assert.All(Streets, p => Assert.Null(p.Covers));
    }

    [Fact]
    public void Nothing_lands_on_an_obstacle_a_road_a_door_another_prop_or_a_gate_passage()
    {
        var doors = Zone["doors"]!.AsArray().Select(d => (X: d!["position"]![0]!.GetValue<double>(), Z: d["position"]![1]!.GetValue<double>())).ToList();
        var gates = Dress.Value.Pieces.Where(p => p.Piece is BourgModels.EastGate or BourgModels.WestGate or BourgModels.NorthGate).ToList();
        var others = Dress.Value.Pieces.Where(p => !BourgModels.IsGround(p.Piece)).ToList();
        foreach (BourgPiece p in Streets)
        {
            var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
            bool ground = BourgModels.IsGround(p.Piece);
            var hits = Obstacles.Where(o => Overlaps(f, Box(o)) && !(ground && (o.Id.StartsWith("etal_ville_", StringComparison.Ordinal)
                || o.Id == "puits_ville_marche" || o.Id.StartsWith("panneau", StringComparison.Ordinal)))).Select(o => o.Id).ToList();
            Assert.True(hits.Count == 0, p.Piece + " sur " + string.Join(", ", hits));
            if (ground) continue;
            Assert.True(new[] { (f.X0, f.Z0), (f.X0, f.Z1), (f.X1, f.Z0), (f.X1, f.Z1) }.All(c => TownRules.RoadAt(c.Item1, c.Item2, Roads.Value).Edge >= 0.3), p.Piece + " sur une route");
            Assert.DoesNotContain(doors, d => d.X > f.X0 - 1.5 && d.X < f.X1 + 1.5 && d.Z > f.Z0 - 1.5 && d.Z < f.Z1 + 1.5);
            Assert.DoesNotContain(others, q => !(q.Piece == p.Piece && q.X == p.X && q.Z == p.Z) && Overlaps(f, BourgModels.Footprint(q.Piece, q.X, q.Z, q.Yaw)));
            // Le passage d'une porte (sa largeur, 15 m de part et d'autre du mur) reste libre.
            foreach (BourgPiece g in gates)
            {
                double a = g.Yaw * Math.PI / 180, half = BourgModels.Passage(g.Piece) / 2;
                var corners = new[] { (f.X0, f.Z0), (f.X0, f.Z1), (f.X1, f.Z0), (f.X1, f.Z1) };
                Assert.DoesNotContain(corners, c =>
                {
                    double dx = c.Item1 - g.X, dz = c.Item2 - g.Z, across = dx * Math.Sin(a) + dz * Math.Cos(a), along = dx * Math.Cos(a) - dz * Math.Sin(a);
                    return Math.Abs(across) < 15 && Math.Abs(along) < half;
                });
            }
        }
    }

    [Fact]
    public void Shipped_models_are_light_game_exports()
    {
        var used = Streets.Select(p => p.Piece).Distinct().ToList();
        var files = Directory.GetFiles(Folder, "*.fbx").Select(f => Path.GetFileNameWithoutExtension(f).Substring("bourg_".Length)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.Equal(41, files.Count);
        Assert.All(files, f => Assert.True(BourgModels.IsStreetDecor(f) && BourgModels.Box(f) != null, f));
        Assert.Equal(files, used.OrderBy(f => f, StringComparer.Ordinal));   // toutes les pièces livrées sont posées
        Assert.Equal(41 * 3, Directory.GetFiles(Folder).Length);
        Assert.True(Directory.GetFiles(Folder).Sum(f => new FileInfo(f).Length) < 20_000_000);
    }
}
