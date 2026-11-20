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
    private static IEnumerable<JsonObject> Grid(string prefix, double x, double z, int columns, int rows, double dx = 10, double dz = 5)
    {
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
                yield return Square($"{prefix}_{c}_{r}", x + dx * c, z + dz * r);
    }

    /// <summary>La zone d'essai (deux secteurs : ouest x &lt; 256, est), scénario en schéma 19, avec des obstacles en plus.</summary>
    private static ZoneLoadResult Read(int westColumns, int westRows, int eastColumns, int eastRows, int schema = ScenarioValidator.CurrentSchemaVersion, bool region = true) =>
        V228b2cChefsTests.Read(zone: z =>
        {
            if (!region)
            {
                z["schemaVersion"] = 12;
                z["data"]!.AsObject().Remove("region");
                ScenarioTestData.WithoutRebuildDoors(z);   // V2.43.4 : sans les portes des lieux reconstruits (au plus 16 portes hors région)
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
        Assert.Contains("379 obstacles", refused.Message, StringComparison.Ordinal);   // l'aire d'un bloc : refusée par la validation du moteur
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

    /// <summary>La zone d'essai, scénario en schéma 19, avec les obstacles <paramref name="extra"/> en plus du rocher.</summary>
    private static ZoneLoadResult With(IEnumerable<JsonObject> extra, Action<JsonObject>? scenario = null) =>
        V228b2cChefsTests.Read(scenario: s =>
        {
            s["schemaVersion"] = 24;
            JsonArray obstacles = s["data"]!["space"]!["obstacles"]!.AsArray();
            foreach (JsonObject o in extra) obstacles.Add(o);
            scenario?.Invoke(s);
        });

    [Fact]
    public void Obstacle_dans_la_marge_compte_pour_les_deux_secteurs()
    {
        // L'espace de l'ouest finit à x = 288, celui de l'est commence à x = 224. 255 à l'est (x ≥ 300) plus un obstacle dans la marge
        // commune (x = 230) : 256 pour l'est, accepté (borne exacte) ; un de plus dans la marge : 257 pour l'est, refusé.
        IEnumerable<JsonObject> East(int n) => Grid("est", 300, 210, 32, 8, dx: 5).Take(n);   // x de 300 à 456, z de 210 à 246 (loin du camp de l'est)
        ZoneLoadResult exact = With(East(255).Append(Square("marge", 230, 240)));
        Assert.True(exact.IsValid, exact.Report.ToString());
        var map = SectorMap.For(exact.Zone!);
        Assert.Equal(256, map.SetupFor("s01_00", 1).Space!.Obstacles.Count);
        Assert.Equal(2, map.SetupFor("s00_00", 1).Space!.Obstacles.Count);   // le rocher et l'obstacle de la marge
        ZoneLoadResult over = With(East(255).Append(Square("marge", 230, 240)).Append(Square("marge2", 232, 240)));
        Assert.Contains("secteur s01_00 : 257 obstacles", over.Report.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secteur s00_00", over.Report.ToString(), StringComparison.Ordinal);
        // De même à l'ouest : 254 à l'ouest (x ≤ 200), plus le rocher, plus deux obstacles dans la marge commune (x = 270) : 257 pour l'ouest.
        IEnumerable<JsonObject> West(int n) => Grid("ouest", 10, 130, 20, 13).Take(n);
        ZoneLoadResult west = With(West(254).Append(Square("marge", 270, 240)).Append(Square("marge2", 272, 240)));
        Assert.Contains("secteur s00_00 : 257 obstacles", west.Report.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secteur s01_00", west.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Obstacle_a_cheval_sur_deux_secteurs_et_limites_exactes_de_l_espace()
    {
        // Un obstacle à cheval sur la limite des secteurs (x = 256) compte pour les deux ; un obstacle qui touche exactement la limite de
        // l'espace de l'est (x = 224, bord de sa marge) y compte (boîtes fermées : la règle de l'hôte) ; un obstacle juste avant n'y compte pas.
        ZoneLoadResult loaded = With(new[] { Square("cheval", 255.5, 240), Square("bord", 223, 230), Square("avant", 222, 220) });
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var map = SectorMap.For(loaded.Zone!);
        string[] east = map.SetupFor("s01_00", 1).Space!.Obstacles.Select(o => o.Id).ToArray();
        string[] west = map.SetupFor("s00_00", 1).Space!.Obstacles.Select(o => o.Id).ToArray();
        Assert.Contains("cheval", east);
        Assert.Contains("cheval", west);
        Assert.Contains("bord", east);       // x de 223 à 224 : touche la limite x = 224
        Assert.DoesNotContain("avant", east);
        // Le validateur compte comme l'hôte : 254 à l'est, plus « cheval » et « bord » : 256 (accepté) ; un de plus : 257.
        IEnumerable<JsonObject> East(int n) => Grid("est", 300, 210, 32, 8, dx: 5).Take(n);
        Assert.True(With(East(254).Append(Square("cheval", 255.5, 240)).Append(Square("bord", 223, 230))).IsValid);
        Assert.Contains("secteur s01_00 : 257 obstacles",
            With(East(255).Append(Square("cheval", 255.5, 240)).Append(Square("bord", 223, 230))).Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Coupe_aux_bornes_de_l_aire_et_secteur_du_coeur_qui_tient_toute_l_aire()
    {
        // Aire réduite au carré du secteur de l'ouest (0 à 256) : un seul secteur, le cœur, qui contient toute l'aire ; son espace est l'aire
        // (coupée à ses bornes) : 257 obstacles y sont refusés, 256 acceptés, et l'est (hors de l'aire) n'est jamais compté.
        void Small(JsonObject s) => s["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(0, 0), ["max"] = new JsonArray(256, 256) };
        ZoneLoadResult ok = V228b2cChefsTests.Read(
            zone: z =>
            {
                z["data"]!["safePoints"] = new JsonArray(new JsonObject { ["id"] = "camp_ouest", ["name"] = "Camp de l'ouest", ["position"] = new JsonArray(64, 128) });
                z["data"]!["populations"] = new JsonArray(new JsonObject { ["creature"] = "loup_ouest", ["respawnMs"] = 90_000, ["leashRange"] = 20, ["loot"] = "loot.loup" });
                z["data"]!.AsObject().Remove("chiefs");
            },
            scenario: s =>
            {
                s["schemaVersion"] = 24;
                Small(s);
                JsonArray combatants = s["data"]!["combatants"]!.AsArray();
                foreach (JsonNode? c in combatants.Where(c => (string)c!["id"]! == "loup_est").ToList()) combatants.Remove(c);
                JsonArray obstacles = s["data"]!["space"]!["obstacles"]!.AsArray();
                foreach (JsonObject o in Grid("o", 10, 130, 20, 13).Take(255)) obstacles.Add(o);
            });
        Assert.True(ok.IsValid, ok.Report.ToString());
        var map = SectorMap.For(ok.Zone!);
        Assert.True(map.HomeHoldsArea);
        Assert.Null(map.Space("s01_00"));
        Assert.Equal(256, map.SetupFor("s00_00", 1).Space!.Obstacles.Count);
        var space = map.SetupFor("s00_00", 1).Space!;
        Assert.Equal((0L, 0L, 256L, 256L), (space.Min.X.Raw / 10_000, space.Min.Z.Raw / 10_000, space.Max.X.Raw / 10_000, space.Max.Z.Raw / 10_000));   // coupé à l'aire
    }

    [Fact]
    public void Zone_sans_region_au_plus_256_obstacles()
    {
        // Sans région, l'aire est simulée d'un bloc : refusée dès la lecture de son scénario.
        ZoneLoadResult loaded = Read(21, 11, 21, 7, region: false);
        Assert.False(loaded.IsValid);
        Assert.Contains("379 obstacles, au plus 256", loaded.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_de_zone_lu_seul_plus_de_256_obstacles_refuse_des_la_validation_validate_et_simulate()
    {
        // Relecture indépendante de la PR #165 : un scénario de zone lu seul (« eds validate », « eds simulate », l'en-tête d'un journal) est
        // simulé d'un bloc : plus de 256 obstacles y sont refusés dès la validation, avec un chemin précis ; seule sa zone à région le lit
        // comme une aire (jusqu'à 4 096).
        var root = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        root["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(-4000, -4000), ["max"] = new JsonArray(4000, 4000) };
        WithoutLieux(root);   // sous-lots V2.31 et suivants : l'aire d'avant les lieux (233 obstacles), complétée jusqu'à 300
        var obstacles = root["data"]!["space"]!["obstacles"]!.AsArray();
        for (int i = obstacles.Count; i < 300; i++) obstacles.Add(Square($"o{i}", -3900 + 10 * (i % 700), 3000 + 10 * (i / 700)));
        ScenarioLoadResult alone = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.Null(alone.Setup);
        Assert.Contains(alone.Report.Errors, e => e.Path == "$.data.space.obstacles" && e.Message.Contains("300 obstacles, au plus 256", StringComparison.Ordinal));
        Assert.NotNull(ScenarioJsonReader.Read(root.ToJsonString(), ShippedData, area: true).Setup);
        // La ligne de commande (relecture de la PR #205, sous-lot V2.31 : l'aire des Vallons dépasse 256 obstacles) : validate lit un scénario
        // de zone comme une aire, vérifiée par sa zone (secteurs), et refuse sans exception celle qu'aucune zone ne prend pour aire ; simulate simule le secteur d'entrée de sa zone, et refuse sans exception
        // un scénario de zone qu'aucune zone ne prend pour aire (code 1).
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Path.Combine(directory, "zone.json");
            File.WriteAllText(path, root.ToJsonString());
            string repository = Path.GetFullPath(Path.Combine(DataDirectory, "..", ".."));   // données livrées
            var validate = Lot71Files.Cli(repository, "validate", path);
            Assert.Equal(1, validate.ExitCode);   // revue Codex de la PR #205 : sans sa zone, les secteurs ne se vérifient pas
            Assert.Contains("aire de 300 obstacles, simulée par secteurs", validate.Error, StringComparison.Ordinal);
            var simulate = Lot71Files.Cli(repository, "simulate", path, "--data", DataDirectory, "--out", Path.Combine(directory, "j.jsonl"));
            Assert.Equal(1, simulate.ExitCode);
            Assert.Contains("aire de 300 obstacles, simulée par secteurs", simulate.Output + simulate.Error, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", simulate.Output + simulate.Error, StringComparison.Ordinal);
            return 0;
        });
    }

    [Fact]
    public void Schema_18_au_plus_256_obstacles_meme_pour_une_zone()
    {
        // Pré-alpha : une version antérieure est refusée entière (aucune migration) ; les scénarios livrés passent en schéma 19 (20 depuis l'esquive).
        ZoneLoadResult loaded = Read(21, 11, 21, 7, schema: 18);
        Assert.False(loaded.IsValid);
        Assert.Contains("version de format 18 antérieure à la version 23", loaded.Report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_qui_n_est_pas_une_zone_au_plus_256_et_zone_au_plus_4096()
    {
        JsonObject Fight(int count, bool zone)
        {
            var root = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            root["schemaVersion"] = 24;
            if (!zone) root["data"]!["zone"] = false;
            root["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(-4000, -4000), ["max"] = new JsonArray(4000, 4000) };
            var obstacles = new JsonArray();
            for (int i = 0; i < count; i++) obstacles.Add(Square($"o{i}", -3900 + 10 * (i % 700), 3000 + 10 * (i / 700)));
            root["data"]!["space"]!["obstacles"] = obstacles;
            return root;
        }
        Assert.Contains("257 obstacles, au plus 256", ScenarioJsonReader.Read(Fight(257, zone: false).ToJsonString(), ShippedData, area: true).Report.ToString(), StringComparison.Ordinal);
        ScenarioLoadResult exact = ScenarioJsonReader.Read(Fight(256, zone: true).ToJsonString(), ShippedData);   // 256, lu seul : accepté
        Assert.True(exact.Setup != null, exact.Report.ToString());
        ScenarioLoadResult area = ScenarioJsonReader.Read(Fight(4096, zone: true).ToJsonString(), ShippedData, area: true);
        Assert.True(area.Setup != null, area.Report.ToString());
        Assert.Equal(SpaceLimits.MaxAreaObstacles, area.Setup!.Space!.Obstacles.Count);
        Assert.Contains("4097 obstacles, au plus 4096", ScenarioJsonReader.Read(Fight(4097, zone: true).ToJsonString(), ShippedData, area: true).Report.ToString(), StringComparison.Ordinal);
    }
}
