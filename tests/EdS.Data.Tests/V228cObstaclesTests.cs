using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28c, première partie (surface étendue : plafond des obstacles) : l'aire d'une zone (scénario de zone, schéma 19) peut avoir
/// jusqu'à 4 096 obstacles ; elle est simulée par secteurs, et chaque espace simulé (secteur et marge de 32 m) en garde au plus 256 : le
/// validateur des zones le vérifie secteur par secteur, le moteur refuse un espace simulé qui en a plus.
/// </summary>
[Trait("Category", "Sector")]
public class V228cObstaclesTests
{
    private static JsonObject Square(string id, double x, double z) => new()
    {
        ["id"] = id,
        ["points"] = new JsonArray(new JsonArray(x, z), new JsonArray(x + 1, z), new JsonArray(x + 1, z + 1), new JsonArray(x, z + 1)),
    };

    /// <summary>Petits carrés de 1 m : <paramref name="columns"/> × <paramref name="rows"/> à partir de (x, z), tous les 10 m et 5 m.</summary>
    private static IEnumerable<JsonObject> Grid(string prefix, double x, double z, int columns, int rows)
    {
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
                yield return Square($"{prefix}_{c}_{r}", x + 10 * c, z + 5 * r);
    }

    /// <summary>La zone d'essai (deux secteurs : ouest x &lt; 256, est), scénario en schéma 19, avec des obstacles en plus.</summary>
    private static ZoneLoadResult Read(int westColumns, int westRows, int eastColumns, int eastRows, int schema = 19, bool region = true) =>
        V228b2cChefsTests.Read(zone: z =>
        {
            if (!region)
            {
                z["schemaVersion"] = 12;
                z["data"]!.AsObject().Remove("region");
            }
        }, scenario: s =>
        {
            s["schemaVersion"] = schema;
            JsonArray obstacles = s["data"]!["space"]!["obstacles"]!.AsArray();
            foreach (JsonObject o in Grid("ouest", 10, 200, westColumns, westRows)) obstacles.Add(o);
            foreach (JsonObject o in Grid("est", 300, 220, eastColumns, eastRows)) obstacles.Add(o);
        });

    [Fact]
    public void Aire_de_zone_plus_de_256_obstacles_chaque_secteur_au_plus_256()
    {
        // 231 obstacles à l'ouest (x ≤ 211 : hors de l'espace de l'est, qui commence à 224), 147 à l'est (x ≥ 300 : hors de celui de l'ouest,
        // qui finit à 288), plus le rocher : 379 dans l'aire.
        ZoneLoadResult loaded = Read(21, 11, 21, 7);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        Assert.Equal(379, zone.Setup.Space!.Obstacles.Count);
        var map = SectorMap.For(zone);
        Assert.Equal(232, map.SetupFor("s00_00", 1).Space!.Obstacles.Count);
        Assert.Equal(147, map.SetupFor("s01_00", 1).Space!.Obstacles.Count);
        // L'aire entière ne se simule pas d'un bloc : le moteur refuse un espace simulé de plus de 256 obstacles.
        var refused = Assert.Throws<CombatSetupException>(() => new CombatEngine(zone.Setup, new MemoryCombatLog()));
        Assert.Contains("379 obstacles dans un espace simulé, au plus 256", refused.Message, StringComparison.Ordinal);
        // La partie, elle, joue secteur par secteur, et passe de l'un à l'autre.
        WorldSession w = V228b2PassageTests.Open(zone);
        Assert.True(w.OnSectors);
        Assert.Equal(232, w.Zone.Setup.Space!.Obstacles.Count);
        List<string> changes = V228b2PassageTests.WalkTo(w, 320, 128);
        Assert.EndsWith("s00_00->s01_00 bastion", Assert.Single(changes), StringComparison.Ordinal);
        Assert.Equal(147, w.Zone.Setup.Space!.Obstacles.Count);
    }

    [Fact]
    public void Un_secteur_a_plus_de_256_obstacles_zone_refusee_avec_son_nom()
    {
        // 25 × 11 = 275 à l'ouest (plus le rocher) : le secteur de l'ouest dépasse ; l'est (147) tient.
        ZoneLoadResult loaded = Read(25, 11, 21, 7);
        Assert.False(loaded.IsValid);
        string report = loaded.Report.ToString();
        Assert.Contains("secteur s00_00 : 276 obstacles dans son espace simulé (marge de 32 m comprise), au plus 256", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secteur s01_00", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Obstacle_dans_la_marge_compte_pour_les_deux_secteurs()
    {
        // 256 à l'est (16 × 16), plus un obstacle dans la marge de l'est (x = 230, dans l'espace de l'est qui commence à 224) : 257 pour l'est.
        ZoneLoadResult loaded = V228b2cChefsTests.Read(scenario: s =>
        {
            s["schemaVersion"] = 19;
            JsonArray obstacles = s["data"]!["space"]!["obstacles"]!.AsArray();
            foreach (JsonObject o in Grid("est", 300, 170, 16, 16)) obstacles.Add(o);   // x de 300 à 451, z de 170 à 246
            obstacles.Add(Square("marge", 230, 240));
        });
        Assert.Contains("secteur s01_00 : 257 obstacles", loaded.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Zone_sans_region_au_plus_256_obstacles()
    {
        ZoneLoadResult loaded = Read(21, 11, 21, 7, region: false);
        Assert.Contains("379 obstacles dans l'aire d'une zone sans région (simulée d'un bloc), au plus 256", loaded.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_18_au_plus_256_obstacles_meme_pour_une_zone()
    {
        // Pré-alpha : une version antérieure est refusée entière (aucune migration) ; les scénarios livrés passent en schéma 19.
        ZoneLoadResult loaded = Read(21, 11, 21, 7, schema: 18);
        Assert.False(loaded.IsValid);
        Assert.Contains("version de format 18 antérieure à la version 19", loaded.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_qui_n_est_pas_une_zone_au_plus_256_et_zone_au_plus_4096()
    {
        JsonObject Fight(int count, bool zone)
        {
            var root = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            root["schemaVersion"] = 19;
            if (!zone) root["data"]!["zone"] = false;
            root["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(-4000, -4000), ["max"] = new JsonArray(4000, 4000) };
            var obstacles = new JsonArray();
            for (int i = 0; i < count; i++) obstacles.Add(Square($"o{i}", -3900 + 10 * (i % 700), 3000 + 10 * (i / 700)));
            root["data"]!["space"]!["obstacles"] = obstacles;
            return root;
        }
        Assert.Contains("257 obstacles, au plus 256", ScenarioJsonReader.Read(Fight(257, zone: false).ToJsonString(), ShippedData).Report.ToString(), StringComparison.Ordinal);
        ScenarioLoadResult area = ScenarioJsonReader.Read(Fight(4096, zone: true).ToJsonString(), ShippedData);
        Assert.True(area.Setup != null, area.Report.ToString());
        Assert.Equal(SpaceLimits.MaxAreaObstacles, area.Setup!.Space!.Obstacles.Count);
        Assert.Contains("4097 obstacles, au plus 4096", ScenarioJsonReader.Read(Fight(4097, zone: true).ToJsonString(), ShippedData).Report.ToString(), StringComparison.Ordinal);
    }
}
