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
            or "falaise_crete" or "ravin_ronces_sud_est"   // V2.40 : le ravin des Ronces coupé par la passerelle
        || id.Contains("_ville", StringComparison.Ordinal)   // sous-lot V2.30 : le Bourg fortifié (palissade, tours, maisons de la ville)
        || id.StartsWith("panneau_", StringComparison.Ordinal)   // sous-lot V2.30.1 : panneaux des croisements du village au Bourg
        || LieuTags.Any(t => id.Contains(t, StringComparison.Ordinal));   // sous-lots V2.31 et suivants : les lieux de la grande carte

    /// <summary>
    /// Sous-lots V2.31 et suivants (les lieux de la grande carte) : chaque obstacle d'un lieu porte l'étiquette de son lieu dans son
    /// identifiant (« bloc_rocher_chute_1 », « maison_cabane_du_bucheron_foret », « loup_taniere_foret1 ») ; une étiquette n'apparaît dans
    /// aucun identifiant d'avant le V2.31 (« _cascade » aurait pris falaise_cascade).
    /// </summary>
    public static readonly string[] LieuTags = { "_chute", "_foret", "_vignes", "_bergers", "_abbaye", "_marais", "_mine", "_fort", "_corrompu", "_passage" };

    /// <summary>
    /// Sous-lots V2.31 et suivants : l'aire des Vallons dépasse 256 obstacles avec les lieux de la grande carte ; elle ne se simule plus d'un
    /// bloc (seulement par secteurs, <see cref="EdS.Data.Json.ScenarioJsonReader.ReadFile(string, EdS.Data.GameData, bool)"/>). Les essais qui
    /// simulent le scénario de la zone d'un bloc le lisent sans les obstacles des lieux (<see cref="LieuTags"/>) : l'aire d'avant le V2.31.
    /// </summary>
    public static void WithoutLieux(JsonObject scenario)
    {
        var obstacles = scenario["data"]!["space"]!["obstacles"]!.AsArray();
        foreach (var o in obstacles.Where(o => IsLieu((string?)o!["id"])).ToList()) obstacles.Remove(o);
        // Décision de Damien du 16 novembre 2026 (vers 23 h) : les créatures des lieux aussi.
        var combatants = scenario["data"]!["combatants"]!.AsArray();
        foreach (var c in combatants.Where(c => IsLieu((string?)c!["id"])).ToList()) combatants.Remove(c);
    }

    /// <summary>Sous-lots V2.31 et suivants : identifiant (obstacle ou créature) d'un lieu de la grande carte.</summary>
    public static bool IsLieu(string? id) => id != null && LieuTags.Any(t => id.Contains(t, StringComparison.Ordinal));

    /// <summary>Retire d'une zone (racine du fichier) les populations et les passages des lieux de la grande carte.</summary>
    public static void ZoneWithoutLieux(JsonObject zone)
    {
        if (zone["data"]?["populations"] is JsonArray populations)
            foreach (var p in populations.Where(p => IsLieu((string?)p?["creature"])).ToList()) populations.Remove(p);
        if (zone["data"]?["passages"] is JsonArray passages)
            foreach (var p in passages.Where(p => IsLieu((string?)p?["obstacle"])).ToList()) passages.Remove(p);
    }

    /// <summary>
    /// Sous-lots V2.31 et suivants : copie des scénarios livrés où l'aire des Vallons est sans les obstacles des lieux (<see cref="WithoutLieux"/>),
    /// pour une zone lue dans un schéma d'avant la région (schéma 12 et moins : son scénario se lit d'un bloc, 256 obstacles au plus).
    /// </summary>
    public static string BlockScenariosDirectory => BlockScenarios.Value;

    private static readonly Lazy<string> BlockScenarios = new(() =>
    {
        string source = Path.Combine(AppContext.BaseDirectory, "scenarios");
        string target = Path.Combine(Path.GetTempPath(), "eds-scenarios-bloc-" + Environment.ProcessId);
        foreach (string file in Directory.GetFiles(source, "*.json", SearchOption.AllDirectories))
        {
            string to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
        }
        string vallons = Path.Combine(target, "zones", "vallons.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(vallons))!.AsObject();
        WithoutLieux(root);
        File.WriteAllText(vallons, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return target;
    });

    /// <summary>
    /// Sous-lots V2.31 et suivants : dossier des scénarios d'une zone (racine du fichier) selon qu'elle a une région ou non. Une zone sans région
    /// (schéma d'avant le 13) perd les populations et passages des lieux (leurs créatures et obstacles ne sont pas dans le scénario d'un bloc)
    /// et leurs lieux nommés (<see cref="LieuPlaces"/>) : elle en garde au plus 64.
    /// </summary>
    public static string ScenariosFor(JsonObject zone)
    {
        if (zone["data"]?["region"] is not null) return Path.Combine(AppContext.BaseDirectory, "scenarios");
        ZoneWithoutLieux(zone);
        if (zone["data"]?["places"] is JsonArray places)
            foreach (var p in places.Where(p => LieuPlaces.Contains((string?)p?["id"])).ToList()) places.Remove(p);
        return BlockScenariosDirectory;
    }

    /// <summary>Sous-lots V2.31 et suivants : texte d'une zone préparée par <see cref="ScenariosFor"/> (à appeler avant elle, même résultat).</summary>
    public static string PreparedZone(JsonObject zone)
    {
        ScenariosFor(zone);
        return zone.ToJsonString();
    }

    /// <summary>Sous-lots V2.31 et suivants : lieux nommés ajoutés avec les lieux de la grande carte.</summary>
    public static readonly HashSet<string?> LieuPlaces = new(StringComparer.Ordinal)
    {
        "cascade", "moulin_a_foulon", "cabane_du_pecheur", "pont_suspendu", "arbre_millenaire", "taniere_de_la_meute", "cabane_du_bucheron",
        "lisiere_de_la_foret", "domaine_du_chateau", "pressoir_du_chateau", "chai_du_chateau", "vignes_du_chateau", "bergerie_des_collines",
        "cairns_de_la_crete", "tour_de_signal", "nef_de_l_abbaye", "cloitre_de_l_abbaye", "dortoir_de_l_abbaye", "jardin_de_l_abbaye",
        "cabanes_sur_pilotis", "ile_des_saules_dormants", "village_des_mineurs", "carreau_de_la_mine", "gradins_de_la_carriere",
        "enceinte_du_fort", "tour_des_bandits", "cercle_des_pierres_levees", "abords_de_la_porte_nord", "passerelle_des_ronces",
        "pont_des_murmures", "col_des_cretes_de_fer", "poste_de_peage", "route_de_l_est", "pont_effondre",
    };

    /// <summary>Sous-lots V2.31 et suivants : le scénario de la zone des Vallons, d'un bloc (<see cref="WithoutLieux"/>).</summary>
    public static ScenarioLoadResult ReadVallonsBlock(GameData? data = null) => ReadMutated(WithoutLieux, data, "zones/vallons");

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
        var combatants = scenario["data"]!["combatants"]!.AsArray();   // sous-lots V2.31 et suivants : créatures des lieux
        foreach (var c in combatants.Where(c => IsLieu((string?)c!["id"])).ToList()) combatants.Remove(c);
        foreach (var o in obstacles.Where(o => MovedBuildings.Contains((string)o!["id"]!)))
            foreach (var p in o!["points"]!.AsArray()) BackToCore(p!);
    }

    /// <summary>
    /// Sous-lot V2.30.1 (déménagement du bourg) : décalage du bloc des boutiques, du cœur à la ville fortifiée (mètres). Les essais du
    /// cœur seul (<see cref="VallonsBefore"/>, <see cref="ZoneBefore"/>, <see cref="StoryBefore"/>) remettent le bourg à sa place d'avant.
    /// </summary>
    public const double BourgDX = -1032, BourgDZ = -34;

    /// <summary>
    /// Sous-lot V2.30.1 : du bout de la grand-rue du village à la place du bourg déménagé, en lignes droites libres (route d'Ambre, porte
    /// de l'Est de la ville, point sûr de la place du marché, rue du marché) ; le dernier point est à 1 m du point sûr <c>place_du_bourg</c> (−1 090, −40).
    /// </summary>
    public static readonly (double X, double Z)[] RoadToBourg =
        { (-50, -1), (-99, -1), (-300, -20), (-500, -40), (-700, -50), (-1010, -76), (-1040, -60), (-1060, -35), (-1090, -39) };

    /// <summary>Sous-lot V2.30.1 : les bâtiments du bourg déménagés (le hameau garde <c>maison3</c> et <c>maison8</c>).</summary>
    public static readonly string[] MovedBuildings = { "maison1", "maison2", "maison4", "maison5", "maison6", "maison7", "auberge", "chapelle", "puits", "etal1", "etal2" };

    /// <summary>Sous-lot V2.30.1 : le point est-il dans le bloc du bourg déménagé (ville fortifiée) ?</summary>
    public static bool InMovedBourg(double x, double z) => x >= -100 + BourgDX && x <= -24 + BourgDX && z >= -30 + BourgDZ && z <= 20 + BourgDZ;

    /// <summary>Sous-lot V2.30.1 : position du bourg avant le déménagement, d'une position d'après.</summary>
    public static (double X, double Z) Old(double x, double z) => (x - BourgDX, z - BourgDZ);

    /// <summary>Sous-lot V2.30.1 : position d'après le déménagement, d'une position du bourg d'avant.</summary>
    public static (double X, double Z) New(double x, double z) => (x + BourgDX, z + BourgDZ);

    /// <summary>
    /// Sous-lot V2.30.1 : une partie des Vallons qui reprend au point sûr de la place du bourg (dans la ville fortifiée), à l'instant
    /// <paramref name="tick"/> : la carte et le décor de la session sont ceux du secteur du bourg (les essais des bâtiments du bourg).
    /// </summary>
    public static EdS.Runtime.AdventureSession LoadAtBourg(string savePath, long tick = 0, EdS.Data.Saves.SavedCharacter? hero = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        EdS.World.SaveStore.Write(savePath, new EdS.Data.Saves.WorldSave("zone.vallons", tick, 0, Array.Empty<string>(), hero,
            resume: new EdS.Data.Saves.SavedResume("place_du_bourg")));
        var a = EdS.Runtime.AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), savePath);
        a.Advance(0.05);
        return a;
    }

    private static void BackToCore(JsonNode point)
    {
        double x = (double)point[0]!, z = (double)point[1]!;
        if (!InMovedBourg(x, z)) return;
        point[0] = Math.Round(x - BourgDX, 4);
        point[1] = Math.Round(z - BourgDZ, 4);
    }

    private static void BackToCore(JsonNode? owner, string key)
    {
        if (owner?[key] is JsonArray p) BackToCore(p);
    }

    /// <summary>
    /// Relecture indépendante de la PR #167 : la zone des Vallons d'avant la surface étendue (racine du fichier de zone), avec
    /// <see cref="VallonsBefore"/> : sans les passages dont l'obstacle est un obstacle de surface (éboulement, gués de la vallée).
    /// </summary>
    /// <summary>V2.43.4 : les portes des intérieurs des lieux reconstruits (hors de l'aire d'avant, et au-delà de 16 portes sans région).</summary>
    public static readonly string[] RebuildDoors = { "porte_atelier_charron", "porte_relais_du_pont", "porte_moulin_a_foulon", "porte_tour_de_signal", "porte_dortoir_abbaye",
        "porte_maison_joueur" };   // V2.53.2 : et la porte de la maison du joueur (hors de l'aire d'avant)

    /// <summary>V2.46 : les portes des dessous (hors de l'aire d'avant) : la grotte derrière la cascade, puis la mine (V2.47.1).</summary>
    public static readonly string[] UnderDoors = { "porte_grotte_cascade", "porte_mine_galeries", "porte_crypte_abbaye" };   // V2.47.1 : et la bouche de la mine ; V2.48.1 : et l'escalier de la crypte de l'abbaye

    /// <summary>V2.43.4 : la zone des Vallons (racine du fichier) sans les portes des lieux reconstruits (V2.46 : ni celles des dessous).</summary>
    public static void WithoutRebuildDoors(JsonObject zone)
    {
        if (zone["data"]?["doors"] is not JsonArray doors) return;
        foreach (var d in doors.Where(d => RebuildDoors.Contains((string)d!["id"]!) || UnderDoors.Contains((string)d!["id"]!)).ToList()) doors.Remove(d);
    }

    public static void ZoneBefore(JsonObject zone)
    {
        WithoutRebuildDoors(zone);   // V2.43.4
        var passages = zone["data"]!["passages"]!.AsArray();
        foreach (var p in passages.Where(p => SurfaceObstacle((string)p!["obstacle"]!)).ToList()) passages.Remove(p);
        ZoneWithoutLieux(zone);   // sous-lots V2.31 et suivants : populations des lieux
        // Sous-lot V2.28c (3) : ni les points sûrs, ni les lieux, ni les routes de la région (hors de l'aire d'avant).
        static bool Inside(JsonNode? point) => (double)point![0]! >= -100 && (double)point[0]! <= 140 && (double)point[1]! >= -70 && (double)point[1]! <= 100;
        var data = zone["data"]!;
        // Sous-lot V2.30.1 : le bourg revient au cœur (portes, point sûr, lieux, places de nuit, place pavée).
        foreach (var n in data["doors"]!.AsArray()) BackToCore(n, "position");
        foreach (var n in data["safePoints"]!.AsArray()) BackToCore(n, "position");
        foreach (var n in data["nightSpots"]!.AsArray()) BackToCore(n, "position");
        foreach (var n in data["places"]!.AsArray())
            if ((string?)n!["id"] is "bourg" or "four_de_firmin") { BackToCore(n, "min"); BackToCore(n, "max"); }
        foreach (var n in data["roads"]!.AsArray())
            if ((string?)n!["id"] == "place_du_bourg") foreach (var p in n!["points"]!.AsArray()) BackToCore(p!);
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
    /// Sous-lot V2.30.1 (revue Codex de la PR #190) : le boss du monde d'avant le déménagement (racine du fichier des boss du monde), pour
    /// une zone passée par <see cref="ZoneBefore"/> : la cible « bourg » reprend sa route et ses objectifs au bord du cœur.
    /// </summary>
    public static void WorldBossesBefore(JsonNode bosses)
    {
        foreach (var boss in bosses["data"]!["bosses"]!.AsArray())
            foreach (var target in boss!["targets"]!.AsArray())
            {
                if ((string?)target!["id"] != "bourg") continue;
                target["route"] = new JsonArray(new JsonArray(-40, -30), new JsonArray(-32, -24));
                target["objectives"] = new JsonArray(
                    new JsonObject { ["id"] = "palissade", ["position"] = new JsonArray(-30, -22) },
                    new JsonObject { ["id"] = "porte", ["position"] = new JsonArray(-28, -20) },
                    new JsonObject { ["id"] = "auvent_du_grenier", ["position"] = new JsonArray(-26, -18) });
            }
    }

    /// <summary>
    /// Sous-lot V2.36 : les saisons d'avant l'île des saules dormants (dossier de données copié) : le verrou d'hiver sans point d'accroche,
    /// pour une aire réduite au cœur (l'île, au milieu du grand lac, en est hors).
    /// </summary>
    public static void SeasonsBefore(string dataDirectory)
    {
        string path = Path.Combine(dataDirectory, "world", "seasons.json");
        JsonNode seasons = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var l in seasons["data"]!["locks"]!.AsArray()) l!.AsObject().Remove("anchor");
        // Sous-lot V2.44.2 : l'aire d'avant n'a ni vigne ni col ni créatures de saison : le contenu saisonnier y reste en version 1.
        seasons["schemaVersion"] = 1;
        foreach (string key in new[] { "vine", "snow", "creatures" }) seasons["data"]!.AsObject().Remove(key);
        File.WriteAllText(path, seasons.ToJsonString());
    }

    /// <summary>
    /// Sous-lot V2.28c (4) : l'histoire des Vallons d'avant la surface étendue (racine du fichier d'histoire), pour une zone passée par
    /// <see cref="ZoneBefore"/> : sans la quête des passages de la vallée (ses passages n'y existent pas), ni ses répliques (<c>v228_</c>) ni
    /// les réponses qui y mènent.
    /// </summary>
    public static void StoryBefore(JsonNode story)
    {
        var data = story["data"]!;
        // Sous-lot V2.30.1 : PNJ, haltes et invités des fêtes du bourg reviennent au cœur.
        int town = 0;
        foreach (var npc in data["npcs"]!.AsArray())
        {
            // Lot C (relecture de la PR #212) : les voyageurs attendent aux portes de la ville, hors de l'aire d'avant ; là, au bourg.
            if ((string?)npc!["id"] is "npc.voyageur_convoyeur" or "npc.voyageuse_pelerine")
                foreach (var slot in npc["schedule"]!.AsArray().Where(x => (string?)x!["activity"] == "route"))
                {
                    var spot = (string?)npc["id"] == "npc.voyageur_convoyeur" ? (-1130.0, -40.0) : (-1060.0, -35.0);
                    slot!["position"] = new JsonArray(spot.Item1, spot.Item2);
                    slot["rain"]!["position"] = new JsonArray(spot.Item1, spot.Item2);
                }
            // V2.30.5 : les marchands des boutiques de la route d'Ambre (et le notable), hors du cœur, attendent sur l'ancienne place du bourg.
            // V2.43.2b : les PNJ des lieux reconstruits hors du cœur (pont, moulin à foulon, tour de signal, abbaye) attendent là aussi.
            if (((string?)npc!["id"])!.EndsWith("_ville", StringComparison.Ordinal)
                || (string?)npc["id"] is "npc.passeuse_du_pont" or "npc.foulonniere" or "npc.guetteuse" or "npc.frere_hospitalier"
                || (string?)npc["id"] is "npc.maitre_oeuvre")   // V2.53.2 : le maître d'œuvre de la maison du joueur
            {
                var at = new JsonArray(-62.0 + town++, -6.0);
                npc["position"] = at.DeepClone();
                foreach (var slot in npc["schedule"]?.AsArray() ?? new JsonArray()) slot!["position"] = at.DeepClone();
                continue;
            }
            BackToCore(npc, "position");
            foreach (var slot in npc!["schedule"]?.AsArray() ?? new JsonArray()) { BackToCore(slot, "position"); BackToCore(slot!["rain"], "position"); }
        }
        foreach (var festival in data["festivals"]?.AsArray() ?? new JsonArray())
            foreach (var guest in festival!["guests"]?.AsArray() ?? new JsonArray()) BackToCore(guest, "position");
        var quests = data["quests"]!.AsArray();
        // Sous-lot V2.28d (3) : ni la quête du cheval (les rives du lac sont hors du cœur), ni ses répliques (v2283_).
        foreach (var q in quests.Where(q => (string?)q!["id"] is "quest.passages_de_la_vallee" or "quest.le_cheval_de_la_ferme").ToList()) quests.Remove(q);
        // Sous-lot V2.40 (décision de Damien du 18 novembre 2026) : la fin du chapitre 1 ouvre la passerelle des Ronces, hors du cœur.
        foreach (var q in quests)
            foreach (var owner in new[] { q }.Concat(q!["steps"]?.AsArray() ?? new JsonArray()))
                if (owner!["effects"] is JsonArray effects)
                    foreach (var e in effects.Where(e => (string?)e!["do"] == "openPassage" && (string?)e["id"] == "passerelle_des_ronces").ToList()) effects.Remove(e);
        foreach (var d in data["dialogues"]!.AsArray())
        {
            var entries = d!["entries"]!.AsArray();
            var nodes = d["nodes"]!.AsArray();
            foreach (var e in entries.Where(e => ((string?)e!["node"])!.StartsWith("v228", StringComparison.Ordinal)).ToList()) entries.Remove(e);
            foreach (var n in nodes.Where(n => ((string?)n!["id"])!.StartsWith("v228", StringComparison.Ordinal)).ToList()) nodes.Remove(n);
            foreach (var n in nodes)
                if (n!["choices"] is JsonArray choices)
                    foreach (var c in choices.Where(c => ((string?)c!["next"])?.StartsWith("v228", StringComparison.Ordinal) == true).ToList()) choices.Remove(c);
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
    /// Sous-lot V2.30.1 : espace réel de la session du secteur du bourg déménagé (celui du point sûr <c>place_du_bourg</c>, dans la ville
    /// fortifiée, avec sa marge), où le jeu calcule les trajets des PNJ du bourg.
    /// </summary>
    public static EdS.Domain.Space.CombatSpace BourgSectorSpace()
    {
        var items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        var zone = EdS.Data.Json.ZoneJsonReader.ReadFile(EdS.Data.Json.ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
            Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, items).Zone!;
        var sectors = EdS.World.SectorMap.For(zone);
        return sectors.SetupFor(sectors.OwnerOf(zone.SafePoints.Single(s => s.Id == "place_du_bourg").Position), 0).Space!;
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
        if (scenario == "zones/vallons") WithoutLieux(root);   // sous-lots V2.31 et suivants : simulé d'un bloc
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
