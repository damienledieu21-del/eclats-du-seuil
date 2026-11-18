using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.30.5 : décisions de Damien du 16 novembre 2026 (vers 21 h 30) pour le Bourg d'Aube fortifié : ouvertures des données resserrées sur
/// les passages des portes ; fossé infranchissable autour de la palissade (obstacles bas, creusé dans le relief) avec un pont devant chaque
/// porte ; données alignées sur les modèles (clocher sur celui de l'église, écuries de 24 m, maison notable H09 et boutiques P13 à P18 le
/// long de la route d'Ambre) ; façades vivantes (lanternes sur potence, auvents, linge, bannières, râtelier mural). Sur les vraies données.
/// </summary>
[Trait("Category", "World")]
public class V2305BourgDecisionsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2305_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private static IReadOnlyList<SpaceObstacle> Obstacles => Vallons.Value.Setup.Space!.Obstacles;

    private static (double X0, double X1, double Z0, double Z1) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 1e4), o.Points.Max(p => p.X.Raw / 1e4), o.Points.Min(p => p.Z.Raw / 1e4), o.Points.Max(p => p.Z.Raw / 1e4));

    private static (double X0, double X1, double Z0, double Z1) Box(string id) => Box(Obstacles.Single(o => o.Id == id));

    private static bool Blocked(double x, double z) => Obstacles.Any(o =>
    {
        var b = Box(o);
        return x > b.X0 && x < b.X1 && z > b.Z0 && z < b.Z1;
    });

    private AdventureMap Map()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.RegionMap;
    }

    [Fact]
    public void Openings_match_the_gate_passages_and_the_data_follow_the_models()
    {
        // Ouvertures : le long de chaque ligne, ce qui n'est ni palissade ni tour est exactement le passage de la porte.
        (double X, double Z, bool AlongX, double Width)[] gates = { (-710, -50, false, 4.2), (-1310, -90, false, 6.0), (-1160, -320, true, 3.0) };
        foreach ((double gx, double gz, bool alongX, double width) in gates)
        {
            int open = 0;
            for (double t = -10; t <= 10; t += 0.05)
            {
                double x = alongX ? gx + t : gx, z = alongX ? gz : gz + t;
                if (!Obstacles.Where(o => BourgModels.IsWall(o.Id) || BourgModels.IsTower(o.Id)).Any(o =>
                    {
                        var b = Box(o);
                        return x >= b.X0 && x <= b.X1 && z >= b.Z0 && z <= b.Z1;
                    })) open++;
            }
            Assert.Equal(width, open * 0.05, 1);
        }
        // Clocher sur celui du modèle de l'église (avant gauche, façade au sud), écuries de 30 × 24.
        Assert.Equal((-905.8, -899.8, 8.0, 14.0), Box("tour_ville_clocher"));
        Assert.Equal((-790.0, -760.0, 0.0, 24.0), Box("grange_ville_ecurie"));
        BourgPiece church = BourgModels.Dress(Obstacles).Pieces.Single(p => p.Piece == BourgModels.Church);
        // Clocher du modèle : X 0,20 – 6,20, Y 0 – 6 de la réservation de 18 × 34 (recentrée de −9, −17), façade au sud.
        Assert.Equal((-897.0 - 8.8, -897.0 - 2.8, 25.0 - 17.0, 25.0 - 11.0), (church.X - 8.8, church.X - 2.8, church.Z - 17.0, church.Z - 11.0));
        // Le point sûr de la porte de l'Est est juste dedans, sur la route, derrière la porte (dans la ville : son écran de chargement est
        // celui du Bourg, V2.53).
        SafePoint gate = Vallons.Value.SafePoints.Single(s => s.Id == "porte_est_du_bourg");
        Assert.Equal((-722.0, -50.0), (gate.Position.X.Raw / 1e4, gate.Position.Z.Raw / 1e4));
        Assert.False(Blocked(-722, -50));
    }

    [Fact]
    public void The_moat_rings_the_town_and_is_crossed_only_by_the_bridges()
    {
        var ditches = Obstacles.Where(o => BourgModels.IsDitch(o.Id)).ToList();
        Assert.Equal(8, ditches.Count);   // V2.40 : le fossé nord coupé devant la porte nord
        Assert.All(ditches, d => Assert.True(d.Low));
        // Tour au milieu du fossé (7,7 m hors de la ligne), tous les 0,5 m : bloqué partout sauf devant les trois portes.
        var open = new List<(double X, double Z)>();
        void Walk(double x0, double z0, double x1, double z1)
        {
            double len = Math.Max(Math.Abs(x1 - x0), Math.Abs(z1 - z0));
            for (double t = 0; t <= len; t += 0.5)
            {
                double x = x0 + (x1 - x0) * t / len, z = z0 + (z1 - z0) * t / len;
                if (!ditches.Any(o => { var b = Box(o); return x > b.X0 && x < b.X1 && z > b.Z0 && z < b.Z1; })) open.Add((x, z));
            }
        }
        const double M = BourgModels.MoatCenter;
        Walk(-1310 - M, 180 + M, -710 + M, 180 + M);
        Walk(-710 + M, 180 + M, -710 + M, -320 - M);
        Walk(-710 + M, -320 - M, -1310 - M, -320 - M);
        Walk(-1310 - M, -320 - M, -1310 - M, 180 + M);
        Assert.All(open, p => Assert.True(
            (p.X == -710 + M && Math.Abs(p.Z - -50) < 2.6) || (p.X == -1310 - M && Math.Abs(p.Z - -90) < 3.6) || (p.Z == -320 - M && Math.Abs(p.X - -1160) < 2.0)
                || (p.Z == 180 + M && Math.Abs(p.X - -940) < 2.0),   // V2.40 : porte nord
            $"fossé ouvert en ({p.X}, {p.Z})"));
        Assert.Equal(4, open.Select(p => Math.Round(p.X / 50) * 1000 + Math.Round(p.Z / 50)).Distinct().Count());
        // Un pont devant chaque porte, au milieu du fossé, dans l'axe du passage, aussi large que le trou du fossé.
        var dress = BourgModels.Dress(Obstacles);
        var bridges = dress.Pieces.Where(p => p.Piece is BourgModels.EastBridge or BourgModels.WestBridge or BourgModels.NorthBridge)
            .Select(p => (p.Piece, p.X, p.Z, p.Yaw)).OrderBy(p => p.Piece, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { (BourgModels.EastBridge, -710 + M, -50.0, 90.0), (BourgModels.WestBridge, -1310 - M, -90.0, -90.0), (BourgModels.NorthBridge, -940.0, 180 + M, 0.0), (BourgModels.NorthBridge, -1160.0, -320 - M, 180.0) },   // V2.40 : pont de la porte nord
            bridges);
        // Modules du fossé serrés sur chaque tronçon (aucun jour), talus au pied de chaque module droit de palissade.
        foreach (SpaceObstacle d in ditches)
        {
            var b = Box(d);
            bool alongX = b.X1 - b.X0 > b.Z1 - b.Z0;
            var spans = dress.Pieces.Where(p => p.Covers == d.Id).Select(p => BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw))
                .Select(f => alongX ? (f.X0, f.X1) : (f.Z0, f.Z1)).OrderBy(s => s.Item1).ToList();
            double at = alongX ? b.X0 : b.Z0;
            foreach (var s in spans)
            {
                Assert.True(s.Item1 <= at + 1e-6, d.Id);
                at = Math.Max(at, s.Item2);
            }
            Assert.True(at >= (alongX ? b.X1 : b.Z1) - 1e-6, d.Id);
        }
        Assert.Equal(dress.Pieces.Count(p => p.Piece is BourgModels.Wall or BourgModels.WallBroken or BourgModels.WallMended),
            dress.Pieces.Count(p => p.Piece == BourgModels.Bank));
    }

    [Fact]
    public void The_moat_is_dug_in_the_relief_but_not_under_the_bridges()
    {
        AdventureMap map = Map();
        GroundRelief relief = GroundRelief.For(map);
        const double M = BourgModels.MoatCenter;
        Assert.Equal(BourgModels.DitchDepth, relief.Moat(-710 + M, 50), 6);       // côté est, loin des portes
        Assert.Equal(BourgModels.DitchDepth, relief.Moat(-1000, 180 + M), 6);     // côté nord
        Assert.Equal(0, relief.Moat(-710 + M, -50), 6);                            // pont de l'Est
        Assert.Equal(0, relief.Moat(-1310 - M, -90), 6);                           // pont de l'Ouest
        Assert.Equal(0, relief.Moat(-1160, -320 - M), 6);                          // pont de la poterne
        Assert.Equal(0, relief.Moat(-710 - 2, 50), 6);                             // pied intérieur de la palissade
        Assert.Equal(0, relief.Moat(-1010, -77), 6);                               // place du marché
        // Le relief affiché descend bien de la profondeur du fossé (même sol sans les obstacles du fossé).
        var without = new AdventureMap(new CombatSpace(map.Space.Min, map.Space.Max, map.Space.Obstacles.Where(o => !BourgModels.IsDitch(o.Id))),
            map.Places, map.Roads, map.Clearings, map.Doors, map.Indoor, map.Speakers, map.Gates, map.Region);
        GroundRelief plain = GroundRelief.For(without);
        Assert.Equal(plain.Height(-710 + M, 50) - BourgModels.DitchDepth, relief.Height(-710 + M, 50), 6);
        Assert.Equal(plain.Height(-710 + M, -50), relief.Height(-710 + M, -50), 6);
    }

    [Fact]
    public void Shops_and_the_large_notable_house_stand_along_the_road_with_living_facades()
    {
        var dress = BourgModels.Dress(Obstacles);
        var roads = AdventureSession.Roads(Vallons.Value);
        var withRoads = BourgModels.Dress(Obstacles, roads);
        foreach (string id in new[] { "maison_ville_apothicaire", "maison_ville_armurier", "maison_ville_forgeron", "maison_ville_tanneur",
            "maison_ville_tailleur", "maison_ville_taverne", "maison_ville_notable" })
        {
            BourgPiece p = withRoads.Pieces.Single(q => q.Covers == id);
            Assert.Equal(BourgModels.ShopModel(id), p.Piece);
            Assert.Equal(180, p.Yaw, 6);   // façade au sud, vers la route d'Ambre
            var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
            var b = Box(id);
            Assert.True(new[] { b.X0 - f.X0, f.X1 - b.X1, b.Z0 - f.Z0, f.Z1 - b.Z1 }.Max() <= 0.35, id);
            Assert.True(TownRules.RoadAt((b.X0 + b.X1) / 2, b.Z0, roads).Edge > 3.5, id + " trop près de la route");
            // Lanterne sur potence à droite de la porte ; auvent au-dessus de l'entrée (sauf la maison notable).
            // (Façade du modèle au sud, à 0,25 m près de l'avant de l'emprise ; voir Lanterns_hang_on_the_model_facade.)
            Assert.Contains(withRoads.Pieces, q => q.Piece == BourgModels.WallLantern && q.Height > 2 && q.Z > b.Z0 - 1 && q.Z < b.Z0 + 1 && q.X > b.X0 && q.X < b.X1);
            Assert.Equal(id != "maison_ville_notable", withRoads.Pieces.Any(q => BourgModels.Awnings.Contains(q.Piece) && q.X == (b.X0 + b.X1) / 2 && q.Height > 1.5));
        }
        // Une lanterne par maison et par boutique ; linge dans les ruelles entre boutiques ; bannières sur la maison du prévôt ; râtelier mural.
        Assert.Equal(119 + 7, withRoads.Pieces.Count(p => p.Piece == BourgModels.WallLantern));
        Assert.InRange(withRoads.Pieces.Count(p => p.Piece == BourgModels.Line), 2, 6);
        Assert.Equal(2, withRoads.Pieces.Count(p => p.Piece == BourgModels.WallBanner));
        Assert.Equal(1, withRoads.Pieces.Count(p => p.Piece == BourgModels.WallRack));
        // Rien de mural sur une route ni dans un autre obstacle que celui contre lequel il est fixé.
        // (La corde à linge, 8 m, entre dans les deux murs qui la portent : elle n'est vérifiée que sur la route.)
        foreach (BourgPiece p in withRoads.Pieces.Where(p => p.Height > 0))
        {
            var f = BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw);
            Assert.True(TownRules.RoadAt(p.X, p.Z, roads).Edge > 0, p.Piece + " sur une route");
            if (p.Piece == BourgModels.Line) continue;
            // Le bâtiment qui la porte (le plus proche) : son emprise de collision peut déborder du mur du modèle.
            string carrier = withRoads.Pieces.Where(q => q.Covers != null).OrderBy(q => (q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z)).First().Covers!;
            Assert.DoesNotContain(Obstacles.Where(o => o.Id != carrier), o =>
            {
                var b = Box(o);
                return f.X0 + 0.3 < b.X1 && f.X1 - 0.3 > b.X0 && f.Z0 + 0.3 < b.Z1 && f.Z1 - 0.3 > b.Z0;
            });
        }
        Assert.Equal(dress.Needs.Count, withRoads.Needs.Count);
    }

    /// <summary>
    /// Revue Codex de la PR #204 : la lanterne de porte d'un monument habillé est sur la façade de son modèle, devant l'entrée, hors de tout
    /// obstacle (l'église avance de 2 m devant le sien ; la cour du prévôt a une lanterne par bâtiment).
    /// </summary>
    [Fact]
    public void Monument_door_lanterns_hang_on_the_model_facade()
    {
        var dress = BourgModels.Dress(Obstacles, AdventureSession.Roads(Vallons.Value));
        var lit = dress.Pieces.Where(p => BourgModels.HasDoorLantern(p.Piece)).ToList();
        Assert.Equal(7, lit.Count);   // église, halle, écuries, prévôt, corps de garde, grenier, four banal
        foreach (BourgPiece p in lit)
        {
            (double x, double z) = BourgModels.DoorLanternOf(p);
            // Hors de tout autre obstacle ; la halle (24 × 14, obstacle 30 × 18) et le grenier (au fond de la cour du prévôt, qui reste un seul
            // obstacle) ont leur lanterne dans leur propre obstacle, sur la façade de leur modèle.
            Assert.DoesNotContain(Obstacles.Where(o => o.Id != p.Covers), o => { var b = Box(o); return x > b.X0 && x < b.X1 && z > b.Z0 && z < b.Z1; });
            if (p.Piece != BourgModels.Hall && p.Piece != BourgModels.Granary) Assert.False(Blocked(x, z), p.Piece + " : lanterne dans un obstacle");
            double a = p.Yaw * Math.PI / 180, ahead = (x - p.X) * Math.Sin(a) + (z - p.Z) * Math.Cos(a);
            Assert.Equal(-BourgModels.Box(p.Piece)!.Value.Y0 + 0.25, ahead, 6);
        }
        BourgPiece church = lit.Single(p => p.Piece == BourgModels.Church);
        Assert.Equal(7.65, BourgModels.DoorLanternOf(church).Z, 6);   // façade de l'église à z 7,90 (obstacle à partir de z 10)
    }

    /// <summary>
    /// Décisions de Damien du 16 novembre 2026 (vers 23 h 20) et du 17 novembre 2026 (vers 2 h 15) : chaque boutique de la route d'Ambre a
    /// son marchand, avec un prénom, devant sa boutique, et des répliques (salut, achat, rumeur du Bourg, au revoir) ; la maison notable
    /// (H09) est habitée par une famille, sans boutique ; aucune quête nouvelle.
    /// </summary>
    [Fact]
    public void Each_new_shop_has_its_named_merchant_and_the_notable_house_a_family()
    {
        var story = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "story.json")))!["data"]!;
        var merchants = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "merchants.json")))!["data"]!["merchants"]!.AsArray();
        var texts = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "texts", "fr.json")))!["data"]!["entries"]!;
        foreach (string k in new[] { "armurier", "forgeron", "tanneur", "apothicaire", "tailleur", "taverne" })
        {
            var m = merchants.Single(x => (string?)x!["id"] == "merchant." + k + "_ville")!;
            Assert.Equal("maison_ville_" + k, (string?)m["shop"]!["building"]);
            Assert.Equal("npc." + k + "_ville", (string?)m["npc"]);
            var npc = story["npcs"]!.AsArray().Single(n => (string?)n!["id"] == "npc." + k + "_ville")!;
            Assert.Matches("^[A-ZÉ][a-zéè]+, ", (string)texts[(string)npc["name"]!]!);   // « Hugon, l'armurier »
            var nodes = story["dialogues"]!.AsArray().Single(d => (string?)d!["id"] == (string?)npc["dialogue"])!["nodes"]!.AsArray().Select(n => (string)n!["id"]!).ToList();
            Assert.Equal(new[] { "accueil", "achat", "rumeur" }, nodes);
        }
        var family = story["npcs"]!.AsArray().Where(n => ((string)n!["id"]!).StartsWith("npc.notable", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, family.Count);
        Assert.All(family, n => Assert.EndsWith("Vaudry, notable du Bourg", (string)texts[(string)n!["name"]!]!));
        Assert.DoesNotContain(merchants, m => ((string)m!["npc"]!).StartsWith("npc.notable", StringComparison.Ordinal));
        Assert.DoesNotContain(story["quests"]!.AsArray(), q => q!.ToJsonString().Contains("_ville\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Revue Codex de la PR #204 : les modèles sans volets séparés (boutiques P13 à P18, prévôt, corps de garde, écuries) ont des fenêtres
    /// de façade à éclairer la nuit : devant le mur de façade, dans la largeur du modèle, hors de la porte (au milieu).
    /// </summary>
    [Fact]
    public void Models_without_separate_shutters_have_facade_windows()
    {
        foreach (string piece in new[] { "p13_armurier", "p14_forgeron", "p15_tanneur", "p16_apothicaire", "p17_tailleur", "p18_taverne",
            BourgModels.Provost, BourgModels.Guardhouse, BourgModels.Stables })
        {
            var windows = BourgModels.FacadeWindows(piece);
            var b = BourgModels.Box(piece)!.Value;
            Assert.InRange(windows.Count, 4, 4);
            Assert.All(windows, w =>
            {
                Assert.InRange(w.X, -b.X1, -b.X0);              // dans la largeur (x = −X)
                Assert.InRange(w.Z, 0.5, -b.Y0 + 0.05);        // devant le centre, pas au-delà du débord avant
                Assert.True(Math.Abs(w.X) >= 1.0, piece + " : fenêtre sur la porte");
                Assert.InRange(w.Y, 1.5, 5.0);
            });
        }
        Assert.Empty(BourgModels.FacadeWindows("h05_marchand_moyenne"));   // les maisons types ont leurs volets
        Assert.Empty(BourgModels.FacadeWindows(BourgModels.Church));
    }

    /// <summary>
    /// Revue Codex de la PR #204 : la lanterne sur potence d'une maison ou d'une boutique est accrochée au mur de façade du modèle posé,
    /// 0,25 m devant lui, 0,9 m à droite de sa porte, et non au bord de l'emprise de collision (maison_ville_23 : emprise de 14 × 10 m,
    /// maison d'angle de 8,55 m : la lanterne flottait à 2,70 m du mur).
    /// </summary>
    [Fact]
    public void Lanterns_hang_on_the_model_facade()
    {
        var dress = BourgModels.Dress(Obstacles, AdventureSession.Roads(Vallons.Value));
        var lanterns = dress.Pieces.Where(p => p.Piece == BourgModels.WallLantern).ToList();
        var fronts = dress.Pieces.Where(p => p.Covers != null && BourgModels.FrontDoor(p.Piece) != null).ToList();
        Assert.Equal(fronts.Count, lanterns.Count);
        foreach (BourgPiece h in fronts)
        {
            (double dx, double dy) = BourgModels.FrontDoor(h.Piece)!.Value;
            var expected = BourgModels.Place(h.X, h.Z, h.Yaw, -(dx + 0.9), -dy + 0.25);
            Assert.Contains(lanterns, l => l.X == expected.X && l.Z == expected.Z && l.Yaw == h.Yaw);
            // Devant le mur de façade, dans le pied du modèle (élargi de la saillie de la potence) : rien ne flotte.
            var f = BourgModels.Footprint(h.Piece, h.X, h.Z, h.Yaw);
            Assert.True(expected.X >= f.X0 - 0.6 && expected.X <= f.X1 + 0.6 && expected.Z >= f.Z0 - 0.6 && expected.Z <= f.Z1 + 0.6, h.Covers);
        }
        BourgPiece h23 = fronts.Single(p => p.Covers == "maison_ville_23");
        var l23 = lanterns.Single(l => Math.Abs(l.X - h23.X) < 8 && Math.Abs(l.Z - h23.Z) < 8);
        double a = h23.Yaw * Math.PI / 180, ahead = (l23.X - h23.X) * Math.Sin(a) + (l23.Z - h23.Z) * Math.Cos(a);
        Assert.Equal(-BourgModels.FrontDoor(h23.Piece)!.Value.Y + 0.25, ahead, 6);
        Assert.True(ahead <= -BourgModels.Box(h23.Piece)!.Value.Y0 + 0.25);
    }
}
