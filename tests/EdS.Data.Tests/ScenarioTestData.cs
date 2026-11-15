using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;

namespace EdS.Data.Tests;

/// <summary>Données livrées (data/v0), scénario livré (scenarios/mannequin.json) et mutations pour les tests.</summary>
internal static class ScenarioTestData
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data", "v0");
    public static string MannequinPath => Path.Combine(AppContext.BaseDirectory, "scenarios", "mannequin.json");
    public static string MannequinJson => File.ReadAllText(MannequinPath);
    public static string ScenarioPath(string name) => Path.Combine(AppContext.BaseDirectory, "scenarios", name + ".json");

    /// <summary>
    /// Sous-lot V2.28c (surface étendue) : obstacles ajoutés à l'aire des Vallons quand elle a été portée à toute la région (rivière de la
    /// vallée et des champs avec les gués fermés de la vallée, grand lac, gorge, ravin des Ronces, éboulement, falaises).
    /// </summary>
    public static bool SurfaceObstacle(string id) => id.StartsWith("riviere_vallee", StringComparison.Ordinal) || id.StartsWith("riviere_champs", StringComparison.Ordinal)
        || id is "riviere_grand_lac" or "gorge_ouest" or "ravin_ronces_sud" or "ravin_ronces_est" or "eboulement_nord" or "falaise_collines" or "falaise_cascade"
            or "falaise_crete";

    /// <summary>
    /// Sous-lot V2.28c : remet un scénario des Vallons (racine du fichier) à l'aire d'avant la surface étendue, de [−100, −70] à [140, 100],
    /// sans les obstacles de la surface : pour les essais qui portent sur le cœur seul (parités, hôte au cœur seul, secteurs d'essai).
    /// </summary>
    public static void VallonsBefore(System.Text.Json.Nodes.JsonObject scenario)
    {
        var space = scenario["data"]!["space"]!;
        space["bounds"] = new System.Text.Json.Nodes.JsonObject { ["min"] = new System.Text.Json.Nodes.JsonArray(-100, -70), ["max"] = new System.Text.Json.Nodes.JsonArray(140, 100) };
        var obstacles = space["obstacles"]!.AsArray();
        foreach (var o in obstacles.Where(o => SurfaceObstacle((string)o!["id"]!)).ToList()) obstacles.Remove(o);
    }

    /// <summary>
    /// Relecture indépendante de la PR #167 : la zone des Vallons d'avant la surface étendue (racine du fichier de zone), avec
    /// <see cref="VallonsBefore"/> : sans les passages dont l'obstacle est un obstacle de surface (éboulement, gués de la vallée).
    /// </summary>
    public static void ZoneBefore(JsonObject zone)
    {
        var passages = zone["data"]!["passages"]!.AsArray();
        foreach (var p in passages.Where(p => SurfaceObstacle((string)p!["obstacle"]!)).ToList()) passages.Remove(p);
        // Sous-lot V2.28c (3) : ni les points sûrs, ni les lieux, ni les routes de la région (hors de l'aire d'avant).
        static bool Inside(JsonNode? point) => (double)point![0]! >= -100 && (double)point[0]! <= 140 && (double)point[1]! >= -70 && (double)point[1]! <= 100;
        var data = zone["data"]!;
        foreach (var (key, keep) in new (string, Func<JsonNode, bool>)[]
        {
            ("safePoints", n => Inside(n["position"])),
            ("places", n => Inside(n["min"]) && Inside(n["max"])),
            ("roads", n => n["points"]!.AsArray().All(Inside)),
        })
        {
            var list = data[key]!.AsArray();
            foreach (var n in list.Where(n => !keep(n!)).ToList()) list.Remove(n);
        }
    }

    /// <summary>
    /// Relecture indépendante de la PR #167 : espace réel de la session du secteur du cœur (s07_06 et sa marge de 32 m, de [−148, −110] à
    /// [172, 210]), découpé par l'hôte des secteurs dans l'aire de toute la région : celui où le jeu calcule les trajets des PNJ du bourg.
    /// </summary>
    public static EdS.Domain.Space.CombatSpace HomeSectorSpace()
    {
        var items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        var zone = EdS.Data.Json.ZoneJsonReader.ReadFile(EdS.Data.Json.ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
            Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, items).Zone!;
        var sectors = EdS.World.SectorMap.For(zone);
        return sectors.SetupFor(sectors.HomeSector, 0).Space!;
    }

    /// <summary>
    /// Intérieurs (28 octobre 2026) : copie dans <paramref name="scenarios"/> les scénarios de zone livrés qui y manquent (les intérieurs
    /// reliés aux Vallons par leurs portes, que l'aventure lit avec eux).
    /// </summary>
    public static void CopyMissingZoneScenarios(string scenarios)
    {
        Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
        foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios", "zones"), "*.json"))
        {
            string target = Path.Combine(scenarios, "zones", Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
    }

    public static GameData ShippedData { get; } = GameDataLoader.Load(DataDirectory).Data!;

    /// <summary>
    /// Sous-lot 8.4 : contenu historique du Bastion (classe « 0.2.0-provisoire » des lots 5 à 8.3, au schéma de classe courant :
    /// Frappe du rempart et Heurt de bouclier sans étourdissement, autres compétences sans bloc combat), avec les règles livrées (les
    /// altérations ajoutées au catalogue ne servent qu'aux nouvelles compétences). Les tests de non-régression des journaux historiques
    /// le rejouent : le moteur reproduit leur gameplay tant que le contenu ne change pas. Le contenu livré, lui, change le gameplay
    /// des scénarios (références figées à part).
    /// </summary>
    public static string HistoricalClassPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-0.2.0", "bastion.json");

    public static GameData HistoricalData { get; } =
        new(ShippedData.Rules, new[] { ClassJsonReader.ReadFile(HistoricalClassPath, ShippedData.Rules.Progression).Class! });

    /// <summary>
    /// Sous-lot 8.5 : contenu figé du sous-lot 8.4 (Bastion « 0.3.0-provisoire », compétences complètes, talents et passifs « [TEST] »
    /// sans effet, aucun déclencheur ni modificateur) et scénarios du sous-lot 8.4 (passifs « [TEST] »). Les tests du moteur et de la
    /// chaîne de données écrits avant le 8.5 (mutations de la classe, mesures de capacité sans déclencheur, journaux historiques) s'appuient
    /// sur ce contenu figé : le contenu livré, qui porte désormais des déclencheurs et des modificateurs (talents, passifs) et sera
    /// rééquilibré au sous-lot 8.7, a ses propres tests et ses références figées à part.
    /// </summary>
    public static string FrozenClassPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-0.3.0", "bastion.json");

    /// <summary>Scénario livré au sous-lot 8.4 (contenu figé ; les scénarios des lots 5 à 8.4 sont compatibles avec les deux classes historiques).</summary>
    public static string FrozenScenarioPath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-0.3.0", "scenarios", name + ".json");

    public static string FrozenMannequinPath => FrozenScenarioPath("mannequin");

    public static GameData FrozenData { get; } =
        new(ShippedData.Rules, new[] { ClassJsonReader.ReadFile(FrozenClassPath, ShippedData.Rules.Progression).Class! });

    /// <summary>
    /// Sous-lot 8.7 : Bastion livré au sous-lot 8.5 (« 0.4.0-provisoire », Provocation sans dégâts), figé pour reproduire la référence
    /// <c>reference-0.14.0-contenu-0.4.0</c> et le relevé initial des mesures du 8.6.
    /// </summary>
    public static string Content040ClassPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-0.4.0", "bastion.json");

    /// <summary>Règles livrées (inchangées au 8.7), Bastion figé du 8.5 et builds de référence livrés.</summary>
    public static GameData Content040Data { get; } =
        new(ShippedData.Rules, new[] { ClassJsonReader.ReadFile(Content040ClassPath, ShippedData.Rules.Progression).Class! }, ShippedData.BuildSets);

    /// <summary>
    /// Sous-lot 11.4b : Bastion livré aux sous-lots 8.7 à 11.4a (« 0.5.0-provisoire », sans portée), figé au schéma de classe courant
    /// pour comparer le contenu 0.6.0 (portées) et garder les contrôles du contenu équilibré au 8.7.
    /// </summary>
    public static string Content050ClassPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-0.5.0", "bastion.json");

    public static JsonObject Shipped() => JsonNode.Parse(MannequinJson)!.AsObject();

    /// <summary>Mannequin figé du sous-lot 8.4 (Bastion sans déclencheur ni modificateur avec <see cref="FrozenData"/>).</summary>
    public static JsonObject FrozenMannequin() => JsonNode.Parse(File.ReadAllText(FrozenMannequinPath))!.AsObject();
    public static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();
    public static JsonObject Combatant(JsonObject root, int index) => DataOf(root)["combatants"]![index]!.AsObject();
    public static JsonObject Command(JsonObject root, int index) => DataOf(root)["commands"]![index]!.AsObject();

    public static ScenarioLoadResult ReadMutated(Action<JsonObject> mutate, GameData? data = null, string scenario = "mannequin")
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath(scenario)))!.AsObject();
        mutate(root);
        return ScenarioJsonReader.Read(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), data ?? ShippedData);
    }

    /// <summary>Scénario figé du sous-lot 8.4 muté, lu avec le contenu figé (<see cref="FrozenData"/>) sauf données explicites.</summary>
    public static ScenarioLoadResult ReadFrozenMutated(Action<JsonObject> mutate, GameData? data = null, string scenario = "mannequin")
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(FrozenScenarioPath(scenario)))!.AsObject();
        mutate(root);
        return ScenarioJsonReader.Read(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), data ?? FrozenData);
    }

    /// <summary>Copie de data/v0 dans un dossier temporaire, avec une modification, pour simuler des données changées.</summary>
    public static string CopyData(Func<string, string, string> transform)
    {
        string target = Path.Combine(Path.GetTempPath(), "eds-donnees-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(target, "classes"));
        foreach (string file in Directory.GetFiles(DataDirectory, "*.json", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(DataDirectory, file);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, relative))!);
            File.WriteAllText(Path.Combine(target, relative), transform(relative.Replace('\\', '/'), File.ReadAllText(file)));
        }
        return target;
    }

    /// <summary>
    /// Copie des données au contenu figé du sous-lot 8.4 : règles livrées et classe figée, sans les builds de référence (écrits pour le
    /// contenu livré, ils citent des talents absents de la classe figée).
    /// </summary>
    public static string CopyFrozenData(Func<string, string, string> transform)
    {
        string target = Path.Combine(Path.GetTempPath(), "eds-donnees-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(target, "classes"));
        File.WriteAllText(Path.Combine(target, "rules.json"), transform("rules.json", File.ReadAllText(Path.Combine(DataDirectory, "rules.json"))));
        File.WriteAllText(Path.Combine(target, "classes", "bastion.json"), transform("classes/bastion.json", File.ReadAllText(FrozenClassPath)));
        return target;
    }
}
