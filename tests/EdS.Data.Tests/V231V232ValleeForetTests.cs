using System.Text.Json.Nodes;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Tools.Zones;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lots V2.31 (vallée de la rivière et cascade) et V2.32 (forêt des Loups et arbre millénaire) de la grande carte : bassin et rochers
/// au pied de la cascade, moulin à foulon et cabane du pêcheur sur la rive est, pont suspendu (vrai troisième passage sur la rivière, décision
/// de Damien du 16 novembre 2026, ouvert avec les gués par la quête « Les passages de la vallée »), sentiers et point sûr du pied de la
/// cascade ; arbre millénaire et cercle de druides, tanière de la meute, cabane du bûcheron, sentier et point sûr de l'arbre (rive ouest,
/// fermée jusqu'à la fin du chapitre 1). Créatures du bestiaire existant (décision de Damien du 16 novembre 2026) : sangliers de la rive est,
/// meute de la tanière, ours de la forêt.
/// </summary>
[Trait("Category", "World")]
public class V231V232ValleeForetTests
{
    [Fact]
    public void Obstacles_dans_leurs_lieux_routes_et_points_surs_degages()
    {
        InPlaces("_chute", "vallee_de_la_riviere", "cascade");
        InPlaces("_foret", "foret_des_loups_profonde");
        RoadsClear("_chute");
        RoadsClear("_foret");
        SafePointClear("pied_de_la_cascade");
        SafePointClear("arbre_millenaire");
        ZoneDefinition zone = Vallons.Value;
        foreach (string id in new[] { "cascade", "moulin_a_foulon", "cabane_du_pecheur", "pont_suspendu" })
            Assert.Equal(10, zone.Places.Single(p => p.Id == id).MinLevel);
        foreach (string id in new[] { "arbre_millenaire", "taniere_de_la_meute", "cabane_du_bucheron", "lisiere_de_la_foret" })
            Assert.Equal(15, zone.Places.Single(p => p.Id == id).MinLevel);
        Assert.Equal(9, Tagged("bloc_cercle_des_druides").Count);
    }

    [Fact]
    public void Etiquettes_strictes_le_scenario_d_un_bloc_redonne_l_aire_d_avant()
    {
        // Relecture de la PR #205 : aucune étiquette de lieu ne prend un obstacle d'avant le V2.31 ; sans les lieux, l'aire redevient celle
        // d'avant (247 obstacles : 233 et les 14 du Bourg du V2.30.5, fossé et boutiques ; 66 créatures), simulable d'un bloc.
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        WithoutLieux(root);
        var ids = root["data"]!["space"]!["obstacles"]!.AsArray().Select(o => (string)o!["id"]!).ToList();
        Assert.Equal(233 + 14 + 5, ids.Count);   // V2.30.5 : sept tronçons du fossé, six boutiques et la maison notable ; V2.40 : porte nord (pan est de la palissade, deux tours, fossé coupé) et ravin des Ronces coupé
        Assert.Contains("falaise_cascade", ids);
        Assert.Contains("riviere_vallee7", ids);
        Assert.Equal(66, root["data"]!["combatants"]!.AsArray().Count);
        Assert.True(ReadVallonsBlock().IsValid);
    }

    [Fact]
    public void Cascade_rive_est_ouverte_depuis_le_gue_de_la_vallee()
    {
        // La rive est n'est pas fermée par le verrou du chapitre 1 : du point sûr du gué au pied de la cascade, à pied, gués fermés.
        Assert.True(Walks((300, 580), (700, 1100), SafePoint("gue_de_la_vallee"), SafePoint("pied_de_la_cascade")));
        // Le moulin et la cabane se contournent : on atteint la berge entre eux et la rivière.
        Assert.True(Walks((300, 580), (700, 1100), SafePoint("pied_de_la_cascade"), (586 + 12, 1067 - 6)));
    }

    [Fact]
    public void Pont_suspendu_vrai_passage_ferme_jusqu_a_la_quete()
    {
        ZoneDefinition zone = Vallons.Value;
        Passage pont = zone.Passages.Single(p => p.Id == "pont_suspendu");
        var shape = Shape(zone.Setup.Space!.Obstacles.Single(o => o.Id == pont.Obstacle));
        (double cx, double cz) = (shape.Average(p => p.X), shape.Average(p => p.Z));
        // Entre le gué nord et la cascade ; le sentier du pont le traverse.
        Assert.InRange(cz, 960, 1060);
        Assert.True(EdS.Runtime.TownRules.RoadAt(cx, cz, EdS.Runtime.AdventureSession.Roads(zone)).Edge < 0);
        (double X, double Z) min = (cx - 40, cz - 40), max = (cx + 40, cz + 40);
        (double X, double Z) east = (cx + 20, cz - 12), west = (cx - 20, cz + 12);
        Assert.False(Walks(min, max, east, west, 0.5));
        Assert.True(Walks(min, max, east, west, 0.5, pont.Obstacle));
    }

    [Fact]
    public void Arbre_millenaire_dans_la_rive_ouest_fermee_et_cercle_ouvert()
    {
        var zone = Vallons.Value;
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = (zone.Region!.Min.X.Raw / 1e4, zone.Region.Min.Z.Raw / 1e4), max = (zone.Region.Max.X.Raw / 1e4, zone.Region.Max.Z.Raw / 1e4);
        var locked = V228cSurfaceTests.Flood(obstacles, min, max, (-20, 300), 2);
        Assert.False(locked.Edge);
        Assert.True(V228cSurfaceTests.Reaches(locked.Reached, min, SafePoint("arbre_millenaire"), 2));
        Assert.False(V228cSurfaceTests.Reaches(locked.Reached, min, SafePoint("pied_de_la_cascade"), 2));
        // Dans le cercle, au pied de l'arbre, par l'ouverture du sud-est.
        Assert.True(Walks((-900, 730), (-800, 830), SafePoint("arbre_millenaire"), (-850 + 7, 780 - 3), 0.5));
    }

    [Fact]
    public void Creatures_des_lieux_dans_leurs_lieux_toujours_la()
    {
        ZoneDefinition zone = Vallons.Value;
        var lieux = zone.Populations.Where(p => (p.Creature.Contains("_chute", StringComparison.Ordinal) || p.Creature.Contains("_foret", StringComparison.Ordinal)) && p.Hours is null).ToList();   // créatures de jour (celles de nuit : V233A239)
        Assert.Equal(new[] { "loup_taniere_foret1", "loup_taniere_foret2", "loup_taniere_foret3", "loup_taniere_foret4", "ours_foret1",
            "sanglier_chute1", "sanglier_chute2" }, lieux.Select(p => p.Creature).OrderBy(c => c, StringComparer.Ordinal));
        foreach (Population p in lieux)
        {
            var c = zone.Setup.Combatants.Single(x => x.Id == p.Creature);
            GroundVector at = c.Placement!.Position;
            Assert.Equal(p.Creature.StartsWith("sanglier", StringComparison.Ordinal) ? 10 : 15, zone.MinLevelAt(at));
            Assert.Null(p.Hours);
            Assert.Null(p.Weather);
            Assert.Null(p.Night);
        }
    }

    [Fact]
    public void Ligne_de_commande_valide_et_simule_le_secteur_d_entree()
    {
        // Relecture de la PR #205 : eds validate lit l'aire de la zone (plus de 256 obstacles) ; eds simulate simule le secteur du point
        // d'entrée (ZoneAreas.EntrySector), comme le jeu.
        string root = AppContext.BaseDirectory;
        var validate = Lot71Files.Cli(root, "validate", Path.Combine(root, "scenarios", "zones", "vallons.json"));
        Assert.Equal(0, validate.ExitCode);
        Assert.Contains("simulée par secteurs (zone zone.vallons", validate.Output, StringComparison.Ordinal);
        // Revue Codex de la PR #205 : une aire dont un secteur dépasse 256 obstacles est refusée par sa zone (validateur des secteurs).
        Lot71Files.InTempDirectory(directory =>
        {
            foreach (string file in Directory.GetFiles(Path.Combine(root, "data"), "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "scenarios"), "*.json", SearchOption.AllDirectories)))
            {
                string to = Path.Combine(directory, Path.GetRelativePath(root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(file, to);
            }
            JsonObject crowded = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            var obstacles = crowded["data"]!["space"]!["obstacles"]!.AsArray();
            for (int i = 0; i < 300; i++)
            {
                double x = 600 + (i % 20) * 2, z = -300 + (i / 20) * 2;
                obstacles.Add(new JsonObject { ["id"] = $"caillou{i}", ["points"] = new JsonArray(new JsonArray(x, z), new JsonArray(x + 0.5, z), new JsonArray(x + 0.5, z + 0.5), new JsonArray(x, z + 0.5)) });
            }
            string path = Path.Combine(directory, "scenarios", "zones", "vallons.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, crowded.ToJsonString());
            var refused = Lot71Files.Cli(directory, "validate", path);
            Assert.Equal(1, refused.ExitCode);
            Assert.Contains("obstacles dans son espace simulé", refused.Error, StringComparison.Ordinal);
            return 0;
        });
        var setup = ZoneAreas.EntrySector(ScenarioPath("zones/vallons"), DataDirectory, ShippedData, out string? sector, out string? why);
        Assert.True(setup != null, why);
        Assert.Equal(SectorMapHome(), sector);
        Assert.InRange(setup!.Space!.Obstacles.Count, 1, SpaceLimits.MaxObstacles);
        Assert.False(ZoneAreas.IsZoneScenario(ScenarioPath("mannequin")));
    }

    [Fact]
    public void Ancienne_sauvegarde_quete_finie_le_pont_s_ouvre_des_la_reprise_dans_son_secteur()
    {
        // Revue Codex de la PR #205 : une sauvegarde d'avant le pont suspendu, quête des passages finie (éboulement et gués ouverts), qui
        // reprend près du pont : le passage est rattrapé dans l'hôte des secteurs dès la première session (obstacle absent du secteur,
        // clé au registre, gardée dans la sauvegarde).
        ZoneDefinition zone = Vallons.Value;
        var quests = new Dictionary<string, EdS.Data.Saves.QuestProgress>(StringComparer.Ordinal)
        {
            ["quest.traces_sous_le_moulin"] = new(EdS.Data.Saves.QuestProgress.Done),
            ["quest.passages_de_la_vallee"] = new(EdS.Data.Saves.QuestProgress.Done),
        };
        EdS.Data.Saves.SavedCharacter hero = V11TestData.Hero().WithStory(new EdS.Data.Saves.SavedStory(quests: quests));
        var consumed = new[] { EdS.World.PlayerCharacters.PlacesKey }.Concat(new[] { "eboulement", "gue_vallee_sud", "gue_vallee_nord" }.Select(zone.PassageKey));
        var save = new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, consumed, hero);
        string pont = zone.Passages.Single(p => p.Id == "pont_suspendu").Obstacle;
        var w = new EdS.World.WorldSession(save, zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, V11TestData.Book(zone),
            safePoint: "pied_de_la_cascade");
        w.Step();
        Assert.True(w.IsPassageOpen("pont_suspendu"));
        Assert.DoesNotContain(w.Zone.Setup.Space!.Obstacles, o => o.Id == pont);
        Assert.Contains(zone.PassageKey("pont_suspendu"), w.Save().Consumed);
        // Sans la quête finie, le pont reste fermé.
        var fresh = new EdS.World.WorldSession(new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, V11TestData.Hero()),
            zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, V11TestData.Book(zone), safePoint: "pied_de_la_cascade");
        fresh.Step();
        Assert.False(fresh.IsPassageOpen("pont_suspendu"));
        Assert.Contains(fresh.Zone.Setup.Space!.Obstacles, o => o.Id == pont);
    }

    private static string SectorMapHome() => EdS.World.SectorMap.For(Vallons.Value).HomeSector;
}
