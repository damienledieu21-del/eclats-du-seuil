using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V1.10 : places des maisons du bourg agrandies pour les grandes maisons de Sketchfab (11 à 16 m ; décision de l'essai de style du
/// 29 octobre 2026). Les huit maisons du bourg passent de 6 à 8 m d'emprise à 9 à 11 m (au moins 7 × 9 m) ; rien ne se chevauche
/// (bâtiments, PNJ, portes, haltes ; les routes sont regardées par <see cref="VallonsAgrandisTests"/>), et la porte de chaque maison où
/// l'on entre donne sur sa route.
/// </summary>
[Trait("Category", "World")]
public class V110BourgTests
{
    private static readonly string[] BourgHouses = { "maison1", "maison2", "maison3", "maison4", "maison5", "maison6", "maison7", "maison8" };

    private static ZoneDefinition Zone() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
        ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static Dictionary<string, (double X0, double Z0, double X1, double Z1)> Boxes(ZoneDefinition zone) =>
        zone.Setup.Space!.Obstacles.ToDictionary(o => o.Id, o => (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0,
            o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0));

    private static double Gap((double X0, double Z0, double X1, double Z1) a, (double X0, double Z0, double X1, double Z1) b) =>
        Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(a.X0 - b.X1, b.X0 - a.X1)), 2) + Math.Pow(Math.Max(0, Math.Max(a.Z0 - b.Z1, b.Z0 - a.Z1)), 2));

    /// <summary>Écart d'un point au bord d'une boîte ; négatif dedans.</summary>
    private static double Gap(double x, double z, (double X0, double Z0, double X1, double Z1) b)
    {
        double dx = Math.Max(b.X0 - x, x - b.X1), dz = Math.Max(b.Z0 - z, z - b.Z1);
        return dx <= 0 && dz <= 0 ? Math.Max(dx, dz) : Math.Sqrt(Math.Pow(Math.Max(0, dx), 2) + Math.Pow(Math.Max(0, dz), 2));
    }

    private static JsonNode StoryData() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "story.json")))!["data"]!;

    [Fact]
    public void Les_maisons_du_bourg_sont_agrandies()
    {
        var boxes = Boxes(Zone());
        foreach (string id in BourgHouses)
        {
            var b = boxes[id];
            double w = b.X1 - b.X0, d = b.Z1 - b.Z0;
            // Au moins 7 × 9 m (avant : 6 × 6 à 8 × 6) : une maison de Sketchfab de 11 m y tient, écrasée d'un quart au plus.
            Assert.True(Math.Min(w, d) >= 7 && Math.Max(w, d) >= 9, $"{id} : {w} × {d} m");
            Assert.True(Math.Max(w, d) * 1.25 >= 11, id);
        }
        // Les maisons hors du bourg (champs, hameau) ne bougent pas.
        Assert.Equal((-74.0, -52.0, -67.0, -46.0), boxes["maison9"]);
        Assert.Equal((76.0, -32.0, 83.0, -26.0), boxes["maison10"]);
    }

    [Fact]
    public void Les_batiments_du_bourg_ne_se_touchent_pas()
    {
        // Deux mètres au moins entre deux bâtiments du bourg (maisons, auberge, chapelle, étals, puits) : on passe entre eux.
        var boxes = Boxes(Zone());
        var ids = BourgHouses.Concat(new[] { "auberge", "chapelle", "etal1", "etal2", "puits" }).ToList();
        for (int i = 0; i < ids.Count; i++)
            for (int k = i + 1; k < ids.Count; k++)
                Assert.True(Gap(boxes[ids[i]], boxes[ids[k]]) >= 2, $"{ids[i]} et {ids[k]} : {Gap(boxes[ids[i]], boxes[ids[k]]):0.##} m");
        // Les maisons restent dans le bourg, sous la haie du nord.
        foreach (string id in BourgHouses)
        {
            var b = boxes[id];
            Assert.True(b.X0 >= -100 && b.X1 <= -24 && b.Z0 >= -24 && b.Z1 <= 17, id);
        }
    }

    [Fact]
    public void Aucun_PNJ_ni_halte_ni_porte_dans_un_batiment()
    {
        ZoneDefinition zone = Zone();
        var boxes = Boxes(zone);
        // PNJ du dehors : à un mètre au moins de tout obstacle (un marchand derrière son étal en est à 1,2 m).
        foreach (JsonNode? npc in StoryData()["npcs"]!.AsArray())
        {
            if (npc!["zone"] != null) continue;
            double x = (double)npc["position"]![0]!, z = (double)npc["position"]![1]!;
            foreach (var (id, b) in boxes)
                Assert.True(Gap(x, z, b) >= 1, $"{npc["id"]} à {Gap(x, z, b):0.##} m de {id}");
        }
        // Haltes : trois mètres au moins de tout bâtiment.
        foreach (SafePoint safe in zone.SafePoints)
        {
            double x = safe.Position.X.Raw / 10000.0, z = safe.Position.Z.Raw / 10000.0;
            foreach (var (id, b) in boxes.Where(kv => TownRules.BuildingFor(kv.Key) is not StyleBuilding.None and not StyleBuilding.Woods))
                Assert.True(Gap(x, z, b) >= 3, $"halte {safe.Id} à {Gap(x, z, b):0.##} m de {id}");
        }
        // Portes du dehors : hors de tout obstacle, à deux mètres au plus de leur bâtiment (le validateur le garde aussi).
        foreach (Door door in zone.Doors)
        {
            double x = door.Position.X.Raw / 10000.0, z = door.Position.Z.Raw / 10000.0;
            foreach (var (id, b) in boxes)
                Assert.True(Gap(x, z, b) > 0.5, $"porte {door.Id} à {Gap(x, z, b):0.##} m de {id}");
            if (door.Building != null) Assert.InRange(Gap(x, z, boxes[door.Building]), 0.5, 2);
        }
    }

    [Theory]
    [InlineData("porte_maison", "maison5")]
    [InlineData("porte_guilde", "maison6")]
    public void La_porte_d_une_maison_ou_l_on_entre_donne_sur_sa_route(string doorId, string house)
    {
        // La façade (porte du modèle) se tourne vers le disque doré de la porte, et celui-ci est du côté de la route la plus proche.
        ZoneDefinition zone = Zone();
        var b = Boxes(zone)[house];
        Door door = zone.Doors.Single(d => d.Id == doorId);
        Assert.Equal(house, door.Building);
        double x = door.Position.X.Raw / 10000.0, z = door.Position.Z.Raw / 10000.0;
        Assert.Equal(TownRules.Facing(b.X0, b.Z0, b.X1, b.Z1, AdventureSession.Roads(zone)), TownRules.DoorSide(b.X0, b.Z0, b.X1, b.Z1, x, z));
        // Au milieu de la façade, à un mètre près.
        Assert.InRange(x - (b.X0 + b.X1) / 2, -1, 1);
    }
}
