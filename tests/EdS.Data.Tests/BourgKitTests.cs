using System.IO.Compression;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Kit modulaire du Bourg d'Aube (décisions de Damien du 25 novembre 2026) : les maisons ordinaires (<c>maison_ville_NN</c>) prennent une des
/// douze maisons du kit, tirée de la position de l'emplacement, à la taille de la parcelle, façade vers la rue, ardoise pour les notables
/// (environ une sur six) ; chaque maison a sa version lointaine (LOD1) ; les bâtiments nommés gardent leur modèle ; l'ancienne maison type
/// reste en repli. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class BourgKitTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private static IReadOnlyList<SpaceObstacle> Obstacles => Vallons.Value.Setup.Space!.Obstacles;

    private static readonly Lazy<List<StyleRoad>> Roads = new(() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!["roads"]!.AsArray()
            .Select(r => new StyleRoad(r!["id"]!.GetValue<string>(), r["points"]!.AsArray().Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToList(),
                r["width"]!.GetValue<double>(), false)).ToList());

    private static readonly Lazy<BourgDress> Dress = new(() => BourgModels.Dress(Obstacles, Roads.Value));

    private static List<BourgPiece> Houses => Dress.Value.Pieces.Where(p => BourgModels.IsTownHouse(p.Covers)).ToList();

    private static (double X0, double X1, double Z0, double Z1) Box(string id)
    {
        SpaceObstacle o = Obstacles.Single(q => q.Id == id);
        return (o.Points.Min(p => p.X.Raw / 1e4), o.Points.Max(p => p.X.Raw / 1e4), o.Points.Min(p => p.Z.Raw / 1e4), o.Points.Max(p => p.Z.Raw / 1e4));
    }

    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Decor", "bourg_kit");

    [Fact]
    public void Every_ordinary_house_gets_a_kit_house_facing_the_street_inside_its_plot()
    {
        var houses = Houses;
        Assert.Equal(119, houses.Count);
        foreach (BourgPiece h in houses)
        {
            KitHouse? kit = BourgKit.Find(h.Piece);
            Assert.True(kit != null, h.Covers + " : " + h.Piece);
            Assert.Equal(new[] { h.Piece }, Dress.Value.Needs[h.Covers!]);
            var b = Box(h.Covers!);
            var f = BourgModels.Footprint(h.Piece, h.X, h.Z, h.Yaw);
            // Façades à l'est ou à l'ouest : profondeur le long de x, débords de toit (BourgKit.SideTolerance) le long de z.
            Assert.True(f.X0 >= b.X0 - BourgModels.HouseTolerance - 1e-6 && f.X1 <= b.X1 + BourgModels.HouseTolerance + 1e-6
                && f.Z0 >= b.Z0 - BourgKit.SideTolerance - 1e-6 && f.Z1 <= b.Z1 + BourgKit.SideTolerance + 1e-6, h.Covers + " déborde " + h.Piece + " " + f + " " + b + " " + h.X + "," + h.Z + " " + h.Yaw);
            // Même façade que l'ancienne maison (orientation vers la rue gardée) ; l'avant de la maison au bord de l'emprise côté rue.
            Assert.Equal(BourgModels.HouseFacing(b, Roads.Value), ((int)Math.Round(Math.Sin(h.Yaw * Math.PI / 180)), (int)Math.Round(Math.Cos(h.Yaw * Math.PI / 180))));
            double front = h.Yaw > 0 ? f.X1 - b.X1 : b.X0 - f.X0;
            Assert.True(Math.Abs(front) < 1e-6, h.Covers + " : avant à " + front + " m du bord");
        }
        // Tailles adaptées : plusieurs maisons différentes, aucune sur plus d'un tiers des emplacements.
        var used = houses.GroupBy(h => h.Piece).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(used.Count >= 8, string.Join(", ", used.Select(u => u.Key + " " + u.Value)));
        Assert.True(used.Values.Max() <= 40, string.Join(", ", used.Select(u => u.Key + " " + u.Value)));
    }

    [Fact]
    public void The_choice_is_deterministic_and_drawn_from_the_position()
    {
        var again = BourgModels.Dress(Obstacles, Roads.Value).Pieces.Where(p => BourgModels.IsTownHouse(p.Covers)).ToList();
        Assert.Equal(Houses.Select(h => (h.Piece, h.X, h.Z, h.Yaw)), again.Select(h => (h.Piece, h.X, h.Z, h.Yaw)));
        // Même emprise sous un autre identifiant : même maison (tirée de la position, pas du nom).
        foreach (BourgPiece h in Houses.Take(20))
        {
            var b = Box(h.Covers!);
            var facing = BourgModels.HouseFacing(b, Roads.Value);
            Assert.Equal(h.Piece, BourgKit.Choose(b, facing)!.Piece);
            Assert.Equal(h.Piece, BourgKit.Choose((b.X0, b.X1, b.Z0, b.Z1), facing)!.Piece);
        }
        // Une emprise déplacée change de tirage au moins une fois sur dix positions.
        var first = Box(Houses[0].Covers!);
        var picks = Enumerable.Range(0, 10).Select(i => BourgKit.Choose((first.X0 + 37 * i, first.X1 + 37 * i, first.Z0, first.Z1), (1, 0))!.Piece).Distinct().Count();
        Assert.True(picks >= 2);
    }

    [Fact]
    public void About_one_house_in_six_is_a_slate_roofed_notable()
    {
        var houses = Houses;
        int slate = houses.Count(h => BourgKit.Find(h.Piece)!.Slate);
        Assert.InRange(slate, houses.Count / 9, houses.Count / 4);
        Assert.Equal(new[] { "kit_m05_notable", "kit_m12_lucarnes" }, BourgKit.Houses.Where(h => h.Slate).Select(h => h.Piece));
        // Une emprise trop étroite pour une maison d'ardoise prend une maison de tuile.
        var narrow = BourgKit.Choose((0, 9, 0, 7), (1, 0))!;
        Assert.True(narrow.Width <= 7 + 2 * BourgKit.SideTolerance);
    }

    [Fact]
    public void Named_buildings_keep_their_models_and_every_kit_house_has_its_old_house_as_fallback()
    {
        var pieces = Dress.Value.Pieces;
        foreach (string id in new[] { "maison_ville_armurier", "maison_ville_forgeron", "maison_ville_tanneur", "maison_ville_apothicaire",
            "maison_ville_tailleur", "maison_ville_taverne", "maison_ville_notable" })
        {
            Assert.Equal(new[] { BourgModels.ShopModel(id)! }, pieces.Where(p => p.Covers == id).Select(p => p.Piece));
            Assert.Null(pieces.Single(p => p.Covers == id).Fallback);
        }
        Assert.Equal(new[] { BourgModels.Provost, BourgModels.Guardhouse, BourgModels.Granary, BourgModels.Oven }.OrderBy(p => p, StringComparer.Ordinal),
            pieces.Where(p => p.Covers == "maison_ville_grande_maison").Select(p => p.Piece).OrderBy(p => p, StringComparer.Ordinal));
        Assert.DoesNotContain(pieces, p => BourgKit.Find(p.Piece) != null && !BourgModels.IsTownHouse(p.Covers));
        foreach (BourgPiece h in Houses)
        {
            Assert.Contains(h.Fallback!.Piece, BourgModels.Houses);
            Assert.Equal(h.Fallback.Piece, Dress.Value.Fallbacks[h.Covers!]);
            BourgPiece old = h.Fallback.For(h);
            Assert.Equal(h.Covers, old.Covers);
            Assert.Equal(h.Yaw, old.Yaw);
        }
    }

    [Fact]
    public void Kit_models_their_far_versions_and_the_palette_are_shipped()
    {
        Assert.Equal(12, BourgKit.Houses.Count);
        Assert.Equal(12 * 2 + 1, Directory.GetFiles(Folder).Length);
        Assert.True(Directory.GetFiles(Folder).Sum(f => new FileInfo(f).Length) < 4_000_000);
        byte[] png = File.ReadAllBytes(Path.Combine(Folder, BourgKit.Palette + ".png"));
        // 34 familles, 76 teintes : 8 colonnes de cases de 4 px, 10 rangées (32 × 40 px).
        Assert.Equal((32, 40), V218BoulangerieTests.PngSize(png));
        foreach (KitHouse h in BourgKit.Houses)
        {
            string near = Path.Combine(Folder, "bourg_" + h.Piece + ".fbx"), far = Path.Combine(Folder, "bourg_" + BourgKit.Far(h.Piece) + ".fbx");
            Assert.Equal(h.Triangles, FbxTriangles(near));
            Assert.Equal(h.FarTriangles, FbxTriangles(far));
            Assert.True(h.FarTriangles <= 800 && h.FarTriangles * 15 <= h.Triangles, h.Piece);
            Assert.Equal(h.Box, BourgModels.Box(BourgKit.Far(h.Piece)));
            Assert.True(AtelierDecor.IsPiece("bourg_" + h.Piece, h.Piece) && !AtelierDecor.IsPiece("bourg_" + BourgKit.Far(h.Piece), h.Piece));
            Assert.Equal("bourg_kit", AtelierDecor.OwnDecorOf("Assets/EdS/Modeles/Decor/bourg_kit/bourg_" + h.Piece + ".fbx"));
            Assert.NotNull(BourgModels.FrontDoor(h.Piece));
            Assert.NotEmpty(BourgModels.FacadeWindows(h.Piece));
        }
        // La vue pose la version lointaine dans un LODGroup (seuils décroissants pour toutes les machines).
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Bourg.cs"));
        Assert.Contains("KitLod(model, p.Piece, catalog, panes);", view, StringComparison.Ordinal);
        Assert.Contains("AddComponent<LODGroup>()", view, StringComparison.Ordinal);
        foreach (DeviceTier tier in Enum.GetValues<DeviceTier>())
        {
            var t = BourgKit.LodHeights(12, tier, 60);
            Assert.True(t.Far > t.Cull && t.Cull > 0 && t.Far < 1, tier.ToString());
        }
    }

    [Fact]
    public void Door_lanterns_clear_the_overhang_and_follow_the_fallback_house()
    {
        // Relecture de la PR #255 : lanterne avancée de l'encorbellement + 0,1 m (M01, M04) ; repli sur la porte de l'ancienne maison.
        Assert.Equal(new[] { "kit_m01_etroite_pignon", "kit_m04_drapier" }, BourgKit.Houses.Where(h => h.Overhang > 0).Select(h => h.Piece));
        var lanterns = Dress.Value.Pieces.Where(p => p.Piece == BourgModels.WallLantern).ToList();
        foreach (BourgPiece h in Houses)
        {
            KitHouse kit = BourgKit.Find(h.Piece)!;
            double extra = kit.Overhang > 0 ? kit.Overhang + 0.1 : 0;
            var at = BourgModels.Place(h.X, h.Z, h.Yaw, -(kit.Door.X + 0.9), -kit.Door.Y + 0.25 + extra);
            BourgPiece l = lanterns.Single(q => Math.Abs(q.X - at.X) < 1e-6 && Math.Abs(q.Z - at.Z) < 1e-6);
            Assert.Null(l.Covers);
            Assert.Equal(h.Piece, l.Fallback!.When);
            (double dx, double dy) = BourgModels.FrontDoor(h.Fallback!.Piece)!.Value;
            var old = BourgModels.Place(h.Fallback.X, h.Fallback.Z, h.Fallback.Yaw, -(dx + 0.9), -dy + 0.25);
            Assert.Equal((old.X, old.Z, h.Fallback.Yaw), (l.Fallback.X, l.Fallback.Z, l.Fallback.Yaw));
            Assert.Equal(BourgModels.WallLantern, l.Fallback.For(l).Piece);
            Assert.Equal(l.Height, l.Fallback.For(l).Height);
        }
        // La palette est réglée dès son import (postprocesseur), les vitres de nuit suivent le LOD de la maison.
        string editor = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsBourgKitTextures.cs"));
        Assert.Contains("OnPreprocessTexture", editor, StringComparison.Ordinal);
        Assert.Contains("FilterMode.Point", editor, StringComparison.Ordinal);
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Bourg.cs"));
        Assert.Contains("nearList.AddRange(windows);", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Bourg_triangles_stay_reasonable_from_afar()
    {
        int near = BourgKit.Triangles(Dress.Value.Pieces, false), far = BourgKit.Triangles(Dress.Value.Pieces, true);
        // Chiffres de la PR : 119 maisons, 753 302 triangles de près (toutes en détail), 22 568 de loin.
        Assert.Equal((753_302, 22_568), (near, far));
        Assert.True(far <= 30_000 && far * 25 <= near);
        // Ardoise : 21 notables sur 119 (≈ une sur six) ; les douze maisons sont posées.
        Assert.Equal(21, Houses.Count(h => BourgKit.Find(h.Piece)!.Slate));
        Assert.Equal(12, Houses.Select(h => h.Piece).Distinct().Count());
    }

    /// <summary>Triangles d'un FBX binaire (Blender) : polygones de « PolygonVertexIndex » (indice négatif = dernier sommet d'une face).</summary>
    internal static int FbxTriangles(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(b, 0, 18), StringComparison.Ordinal);
        uint version = BitConverter.ToUInt32(b, 23);
        bool wide = version >= 7500;
        int total = 0;
        void Node(ref int at, int end)
        {
            while (at < end)
            {
                long nodeEnd = wide ? (long)BitConverter.ToUInt64(b, at) : BitConverter.ToUInt32(b, at);
                long props = wide ? (long)BitConverter.ToUInt64(b, at + 8) : BitConverter.ToUInt32(b, at + 4);
                int head = wide ? 25 : 13;
                if (nodeEnd == 0) { at += head; return; }
                int nameLength = b[at + head - 1];
                string name = System.Text.Encoding.ASCII.GetString(b, at + head, nameLength);
                int p = at + head + nameLength;
                for (long k = 0; k < props; k++)
                {
                    char type = (char)b[p++];
                    switch (type)
                    {
                        case 'C': p += 1; break;
                        case 'Y': p += 2; break;
                        case 'I': case 'F': p += 4; break;
                        case 'D': case 'L': p += 8; break;
                        case 'S': case 'R': p += 4 + BitConverter.ToInt32(b, p); break;
                        default:
                            int count = BitConverter.ToInt32(b, p), encoding = BitConverter.ToInt32(b, p + 4), length = BitConverter.ToInt32(b, p + 8);
                            byte[] data = b.AsSpan(p + 12, length).ToArray();
                            if (encoding == 1)
                            {
                                using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
                                using var o = new MemoryStream();
                                z.CopyTo(o);
                                data = o.ToArray();
                            }
                            if (name == "PolygonVertexIndex" && type == 'i')
                            {
                                int corners = 0;
                                for (int i = 0; i < count; i++)
                                {
                                    corners++;
                                    if (BitConverter.ToInt32(data, i * 4) < 0)
                                    {
                                        total += corners - 2;
                                        corners = 0;
                                    }
                                }
                            }
                            p += 12 + length;
                            break;
                    }
                }
                int child = p;
                if (child < nodeEnd) Node(ref child, (int)nodeEnd);
                at = (int)nodeEnd;
            }
        }
        int start = 27;
        Node(ref start, b.Length - 200);
        return total;
    }
}
