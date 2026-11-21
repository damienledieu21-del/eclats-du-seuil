using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.WorldBosses;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Intérieurs (choix de Damien du 28 octobre 2026 : « on fait la façon 1 », une porte qui mène à un intérieur séparé) : zones en schéma 4
/// (intérieur, portes), portes qui se répondent, passage d'une zone à l'autre dans l'aventure (sauvegarde, session de l'autre zone, arrivée à
/// la porte d'en face), histoire et métiers restés dans la zone de départ.
/// </summary>
[Trait("Category", "World")]
public class InterieursTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_interieurs_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static WorldZonesLoadResult Linked(string? data = null, string? scenarios = null) =>
        ZoneJsonReader.ReadLinked(data ?? DataDirectory, "zone.vallons", scenarios ?? Scenarios, ShippedData, Items);

    private static JsonObject ZoneJson(string zone) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zone)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root) => ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items);

    private static JsonObject Door(JsonObject root, int index = 0) => root["data"]!["doors"]![index]!.AsObject();

    /// <summary>Données et scénarios copiés dans le dossier du test, retouchés ; rend leurs dossiers.</summary>
    private (string Data, string Scenarios) Copy(Action<JsonObject>? vallons = null, Action<JsonObject>? vallonsScenario = null, Action<string>? files = null)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(Scenarios, scenarios);
        if (vallons != null)
        {
            string path = ZoneJsonReader.PathIn(data, "zone.vallons");
            JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            vallons(root);
            File.WriteAllText(path, root.ToJsonString());
        }
        if (vallonsScenario != null)
        {
            string path = Path.Combine(scenarios, "zones", "vallons.json");
            JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            vallonsScenario(root);
            File.WriteAllText(path, root.ToJsonString());
        }
        files?.Invoke(data);
        return (data, scenarios);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    /// <summary>Le point sûr d'entrée des Vallons posé devant la porte de l'auberge (à 1,5 m).</summary>
    private static void EntryAtInn(JsonObject root) => root["data"]!["safePoints"]![0]!["position"] = new JsonArray(-1101, -31.5);

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    private static GroundVector Position(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Position!.Value;

    private static GroundVector At(double x, double z) => new GroundVector(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Zones_reliees_livrees()
    {
        WorldZonesLoadResult linked = Linked();
        Assert.True(linked.IsValid, linked.Report.ToString());
        // Sous-lot V1.7 : la Maison de la Guilde, et ses archives derrière elle (on y entre depuis la guilde).
        // Sous-lot V1.9.6 : la scène du souvenir de la Rupture, reliée au pré par une porte qui ne se montre pas (voir V196OuvertureTests).
        // Sous-lot V2.14 : les boutiques dans de vraies maisons (boulangerie, épicerie, herboristerie, mercerie).
        Assert.Equal(new[] { "zone.vallons", "zone.auberge", "zone.chapelle", "zone.maison", "zone.guilde", "zone.souvenir_rupture", "zone.boulangerie", "zone.epicerie",
                "zone.herboristerie", "zone.mercerie", "zone.forge", "zone.grange", "zone.moulin", "zone.tour", "zone.chaumiere", "zone.atelier_charron", "zone.relais_pont", "zone.foulon", "zone.grotte_cascade", "zone.garde_du_signal", "zone.dortoir_abbaye", "zone.maison_joueur",
                "zone.auberge_cuisine", "zone.auberge_chambres", "zone.archives", "zone.moulin_meules", "zone.auberge_logement", "zone.auberge_combles" },   // lot C (décisions du 16 novembre 2026) : intérieurs de métier
            linked.Zones.Select(z => z.Id));
        // Sous-lot V2.1 : les Vallons passent au schéma 8 (jour et nuit) ; les intérieurs restent au schéma 7, encore lu.
        Assert.All(linked.Zones, z => Assert.Equal(z.Id == "zone.vallons" ? ZoneValidator.TerritorySchemaVersion : z.Underground ? ZoneValidator.CurrentSchemaVersion : ZoneValidator.OldestSchemaVersion, z.SchemaVersion));   // V2.4 : Vallons en 9, intérieurs en 7 ; V2.46 : la grotte (dessous) au schéma courant
        ZoneDefinition vallons = linked.Zones[0];
        Assert.False(vallons.Indoor);
        Assert.Equal(new[] { ("porte_auberge", "auberge", "zone.auberge"), ("porte_chapelle", "chapelle", "zone.chapelle"), ("porte_maison", "maison5", "zone.maison"),
                ("porte_guilde", "maison6", "zone.guilde"), ("porte_boulangerie", "maison2", "zone.boulangerie"), ("porte_epicerie", "maison4", "zone.epicerie"),
                ("porte_herboristerie", "maison7", "zone.herboristerie"), ("porte_mercerie", "maison1", "zone.mercerie"),
                // Lot C (décisions de Damien du 16 novembre 2026) : forge couverte (maison de Maëlle), grange, moulin, tour de guet, maison paysanne
                // (celle d'Achille, aux champs du sud).
                ("porte_forge", "maison3", "zone.forge"), ("porte_grange", "grange", "zone.grange"), ("porte_moulin_interieur", "moulin", "zone.moulin"),
                ("porte_tour", "tour", "zone.tour"), ("porte_chaumiere", "maison9", "zone.chaumiere"),
                // V2.43.4 : l'atelier du charron (maison10 du hameau) et la salle de garde de la tour de signal.
                ("porte_atelier_charron", "maison10", "zone.atelier_charron"), ("porte_tour_de_signal", "tour_de_signal_bergers", "zone.garde_du_signal"),
                // V2.53.2 : la maison du joueur (maison_ville_86 du Bourg).
                ("porte_maison_joueur", "maison_ville_86", "zone.maison_joueur") },
            vallons.Doors.Where(d => d.Building != null).Select(d => (d.Id, d.Building!, d.To)));
        // V2.43.4 : sans bâtiment aussi, le relais du pont, le moulin à foulon (hors de la rivière) et le dortoir (au milieu de ses ruines).
        Assert.Equal(new[] { ("souvenir", "zone.souvenir_rupture"), ("porte_relais_du_pont", "zone.relais_pont"), ("porte_moulin_a_foulon", "zone.foulon"),
            ("porte_grotte_cascade", "zone.grotte_cascade"),   // V2.46 : la grotte derrière la cascade (un dessous)
            ("porte_dortoir_abbaye", "zone.dortoir_abbaye") }, vallons.Doors.Where(d => d.Building is null).Select(d => (d.Id, d.To)));
        foreach (ZoneDefinition inside in linked.Zones.Skip(1).Where(z => z.Id != "zone.souvenir_rupture" && !z.Underground))
        {
            Assert.True(inside.Indoor);
            // Chaque pièce a sa sortie ; les archives sortent dans la guilde, la guilde a aussi la porte des archives. Lot C : la cuisine et les
            // chambres sortent dans l'auberge (qui a leurs deux portes), l'étage des meules dans le moulin (qui a sa porte).
            Door exit = inside.DoorOf("sortie")!;
            string outside = inside.Id switch
            {
                "zone.archives" => "zone.guilde",
                "zone.auberge_cuisine" or "zone.auberge_chambres" => "zone.auberge",
                "zone.moulin_meules" => "zone.moulin",
                // 17 novembre 2026 (fiches A1 à A3) : trois niveaux au-dessus de la salle, de palier en palier.
                "zone.auberge_logement" => "zone.auberge_chambres",
                "zone.auberge_combles" => "zone.auberge_logement",
                _ => "zone.vallons",
            };
            Assert.Equal(("sortie", outside, (string?)null), (exit.Id, exit.To, exit.Building));
            Assert.Equal(exit.Id, linked.Zones.Single(z => z.Id == outside).DoorOf(exit.ToDoor)!.ToDoor);
            Assert.Equal(inside.Id switch { "zone.guilde" or "zone.moulin" or "zone.auberge_chambres" or "zone.auberge_logement" => 2, "zone.auberge" => 3, _ => 1 }, inside.Doors.Count);
            // Un intérieur n'a ni créature ni chef : rien à y combattre.
            Assert.Empty(inside.Setup.Combatants);
            Assert.Empty(inside.Populations);
            Assert.Null(inside.Boss);
            Assert.Equal(inside.Name, Assert.Single(inside.Places).Name);
        }
        Assert.Equal(new[] { "Auberge de l'Aube", "Chapelle du Bourg", "Maison du Bourg", "Maison de la Guilde", "Souvenir de la Rupture", "Boulangerie de Firmin",
                "Épicerie de Bertille", "Herboristerie de Sauge", "Mercerie de Margot", "Forge couverte de Maëlle", "Grange", "Moulin", "Tour de guet",
                "Maison paysanne", "Atelier du charron", "Relais du pont", "Moulin à foulon", "Grotte de la cascade", "Salle de garde de la tour de signal", "Dortoir de l'abbaye", "Chez soi", "Cuisine de l'auberge", "Chambres de l'auberge", "Archives de la Guilde", "Étage des meules",
                "Logement et dortoir de l'auberge", "Combles de l'auberge" },   // V2.14 : les boutiques ; lot C
            linked.Zones.Skip(1).Select(z => z.Name));
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"));
        foreach (string zone in linked.Zones.Select(z => z.Id))
            Assert.True(schema.IsValid(JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zone))).RootElement), zone);
    }

    /// <summary>La porte d'un bâtiment est sur la façade que le décor tourne vers la route : on la voit où l'on entre.</summary>
    [Fact]
    public void Portes_sur_la_facade_tournee_vers_la_route()
    {
        ZoneDefinition vallons = Linked().Zones[0];
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(vallons);
        foreach (Door door in vallons.Doors.Where(d => d.Building != null))   // sous-lot V1.9.6 : la porte de la scène n'est pas dans un bâtiment
        {
            SpaceObstacle building = vallons.Setup.Space!.Obstacles.Single(o => o.Id == door.Building);
            double minX = building.Points.Min(p => p.X.Raw) / 10000.0, maxX = building.Points.Max(p => p.X.Raw) / 10000.0;
            double minZ = building.Points.Min(p => p.Z.Raw) / 10000.0, maxZ = building.Points.Max(p => p.Z.Raw) / 10000.0;
            (int X, int Z) side = TownRules.DoorSide(minX, minZ, maxX, maxZ, door.Position.X.Raw / 10000.0, door.Position.Z.Raw / 10000.0);
            Assert.Equal(TownRules.Facing(minX, minZ, maxX, maxZ, roads), side);
        }
        // Côté le plus proche du point, d'un axe à l'autre.
        Assert.Equal((0, -1), TownRules.DoorSide(0, 0, 10, 6, 5, -1));
        Assert.Equal((0, 1), TownRules.DoorSide(0, 0, 10, 6, 4, 7.5));
        Assert.Equal((-1, 0), TownRules.DoorSide(0, 0, 10, 6, -0.5, 3));
        Assert.Equal((1, 0), TownRules.DoorSide(0, 0, 10, 6, 11, 2));
    }

    [Theory]
    [InlineData("to", "\"zone.vallons\"", "$.data.doors[0].to", ValidationCode.InvalidValue)]
    [InlineData("to", "\"auberge\"", "$.data.doors[0].to", ValidationCode.InvalidValue)]
    [InlineData("toDoor", "\"Sortie\"", "$.data.doors[0].toDoor", ValidationCode.InvalidValue)]
    [InlineData("building", "\"cave\"", "$.data.doors[0].building", ValidationCode.UnknownReference)]
    [InlineData("position", "[-1101, -34]", "$.data.doors[0].position", ValidationCode.OutOfRange)]
    [InlineData("position", "[-1101, -28]", "$.data.doors[0].position", ValidationCode.InvalidValue)]
    [InlineData("position", "[-300, 4]", "$.data.doors[0].position", ValidationCode.OutOfRange)]
    [InlineData("position", "[400000, 4]", "$.data.doors[0].position", ValidationCode.OutOfRange)]   // relecture : plus de dépassement
    [InlineData("position", "[-69, 400000]", "$.data.doors[0].position", ValidationCode.OutOfRange)]
    [InlineData("name", "\" \"", "$.data.doors[0].name", ValidationCode.MissingField)]
    [InlineData("id", "\"porte_auberge\"", "$.data.doors[0].id", ValidationCode.DuplicateId)]
    public void Porte_invalide_refusee_avec_son_chemin(string field, string json, string path, ValidationCode code)
    {
        JsonObject root = ZoneJson("zone.vallons");
        int index = field == "id" ? 1 : 0;
        Door(root, index)[field] = JsonNode.Parse(json);
        ZoneLoadResult result = Read(root);
        Assert.False(result.IsValid);
        string expected = field == "id" ? path.Replace("[0]", "[1]") : path;
        Assert.Contains(result.Report.Errors, e => e.Path == expected && e.Code == code);
    }

    [Fact]
    public void Portes_absentes_trop_nombreuses_ou_ancien_schema()
    {
        JsonObject none = ZoneJson("zone.vallons");
        none["data"]!.AsObject().Remove("doors");
        none["data"]!.AsObject().Remove("indoor");
        ZoneLoadResult plain = Read(none);
        Assert.True(plain.IsValid, plain.Report.ToString());
        Assert.Empty(plain.Zone!.Doors);
        Assert.False(plain.Zone.Indoor);

        JsonObject many = ZoneJson("zone.vallons");
        JsonArray doors = many["data"]!["doors"]!.AsArray();
        for (int i = 0; i < ZoneValidator.MaxDoorsPerCell; i++)   // V2.25.3 : 17 portes au moins dans la case de la première
        {
            JsonObject copy = JsonNode.Parse(doors[0]!.ToJsonString())!.AsObject();
            copy["id"] = "porte_copie_" + i;
            doors.Add(copy);
        }
        // V2.25.3 : avec la région des Vallons (schéma 13), la borne est par case (16 portes) ; sans région, celle d'avant (16 au total).
        Assert.Contains(Read(many).Report.Errors, e => e.Path.StartsWith("$.data.doors[", StringComparison.Ordinal) && e.Code == ValidationCode.LimitExceeded);
        many["schemaVersion"] = 12;
        many["data"]!.AsObject().Remove("region");
        Assert.Contains(Read(many).Report.Errors, e => e.Path == "$.data.doors" && e.Code == ValidationCode.LimitExceeded);

        JsonObject old = ZoneJson("zone.auberge");
        old["schemaVersion"] = 3;
        Assert.Contains(Read(old).Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);

        JsonObject indoor = ZoneJson("zone.auberge");
        indoor["data"]!["indoor"] = "oui";
        Assert.Contains(Read(indoor).Report.Errors, e => e.Path == "$.data.indoor" && e.Code == ValidationCode.WrongType);
    }

    [Fact]
    public void Portes_qui_ne_se_repondent_pas_refusees()
    {
        ZoneDefinition vallons = Read(ZoneJson("zone.vallons")).Zone!;
        ZoneDefinition chapelle = Read(ZoneJson("zone.chapelle")).Zone!, maison = Read(ZoneJson("zone.maison")).Zone!;
        // Sous-lot V1.7 : la guilde et ses archives.
        ZoneDefinition guilde = Read(ZoneJson("zone.guilde")).Zone!, archives = Read(ZoneJson("zone.archives")).Zone!;
        ZoneDefinition souvenir = Read(ZoneJson("zone.souvenir_rupture")).Zone!;   // sous-lot V1.9.6 : la scène du souvenir
        // Zones manquantes.
        ValidationReport alone = ZoneValidator.CheckDoors(new[] { vallons });
        Assert.Equal(21, alone.Errors.Count(e => e.Code == ValidationCode.UnknownReference && e.Path.EndsWith(".to", StringComparison.Ordinal)));   // V2.14 : et les quatre boutiques ; lot C : et cinq intérieurs de métier
        ZoneDefinition[] shops = { Read(ZoneJson("zone.boulangerie")).Zone!, Read(ZoneJson("zone.epicerie")).Zone!, Read(ZoneJson("zone.herboristerie")).Zone!,
            Read(ZoneJson("zone.mercerie")).Zone! };
        // Lot C (16 novembre 2026) : les intérieurs de métier, la cuisine et les chambres de l'auberge, l'étage des meules.
        ZoneDefinition[] trades = new[] { "zone.forge", "zone.grange", "zone.moulin", "zone.moulin_meules", "zone.tour", "zone.chaumiere", "zone.auberge_cuisine",
            "zone.auberge_chambres", "zone.auberge_logement", "zone.auberge_combles",
            // V2.43.4 : les intérieurs des lieux reconstruits.
            "zone.atelier_charron", "zone.relais_pont", "zone.foulon", "zone.garde_du_signal", "zone.dortoir_abbaye", "zone.maison_joueur",
            "zone.grotte_cascade" }.Select(z => Read(ZoneJson(z)).Zone!).ToArray();   // V2.46 : la grotte de la cascade (un dessous)
        ValidationReport all = ZoneValidator.CheckDoors(new[] { vallons, Read(ZoneJson("zone.auberge")).Zone!, chapelle, maison, guilde, archives, souvenir }.Concat(shops).Concat(trades).ToArray());
        Assert.True(all.IsValid, all.ToString());

        // La sortie de l'auberge mène à la porte de la chapelle : ni l'une ni l'autre ne se répondent.
        JsonObject wrong = ZoneJson("zone.auberge");
        Door(wrong)["toDoor"] = "porte_chapelle";
        ValidationReport crossed = ZoneValidator.CheckDoors(new[] { vallons, Read(wrong).Zone!, chapelle, maison, guilde, archives, souvenir }.Concat(shops).ToArray());
        Assert.Contains(crossed.Errors, e => e.Path == "$.data.doors[0].toDoor" && e.Code == ValidationCode.InvalidValue && e.Message.Contains("zone.vallons, porte « porte_auberge »"));
        Assert.Contains(crossed.Errors, e => e.Path == "$.data.doors[0].toDoor" && e.Code == ValidationCode.InvalidValue && e.Message.Contains("zone.auberge, porte « sortie »"));

        // Porte d'arrivée absente.
        JsonObject renamed = ZoneJson("zone.auberge");
        Door(renamed)["id"] = "porte";
        ValidationReport missing = ZoneValidator.CheckDoors(new[] { vallons, Read(renamed).Zone!, chapelle, maison, guilde, archives, souvenir }.Concat(shops).ToArray());
        Assert.Contains(missing.Errors, e => e.Path == "$.data.doors[0].toDoor" && e.Code == ValidationCode.UnknownReference && e.Message.Contains("porte « sortie » absente de zone.auberge"));

        // Zone donnée deux fois.
        Assert.Contains(ZoneValidator.CheckDoors(new[] { vallons, vallons }).Errors, e => e.Code == ValidationCode.DuplicateId);

        // Relecture indépendante : le chargement des zones reliées rend le rapport de la zone dont une porte est en cause.
        (string data, string scenarios) = Copy(files: d =>
        {
            string path = ZoneJsonReader.PathIn(d, "zone.auberge");
            JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Door(root)["toDoor"] = "porte_chapelle";
            File.WriteAllText(path, root.ToJsonString());
        });
        WorldZonesLoadResult linked = Linked(data, scenarios);
        Assert.False(linked.IsValid);
        Assert.Equal("zone.vallons", linked.Zone);   // la porte de l'auberge des Vallons n'a plus de réponse (première zone en cause)
        Assert.All(linked.Report.Errors, e => Assert.StartsWith("zone.vallons,", e.Message, StringComparison.Ordinal));
        Assert.Equal("$.data.doors[0].toDoor", Assert.Single(linked.Report.Errors).Path);
    }

    [Fact]
    public void Forme_du_chef_sur_une_porte_refusee()
    {
        // Relecture indépendante : on arrive par une porte comme par un point sûr ; la première forme du chef n'y est pas posée.
        JsonObject root = ZoneJson("zone.vallons");
        Door(root).Remove("building");
        Door(root)["position"] = new JsonArray(54, 36);
        ZoneLoadResult result = Read(root);
        Assert.Contains(result.Report.Errors, e => e.Path.StartsWith("$.data.boss.forms[0]", StringComparison.Ordinal)
            && e.Message.Contains("chevauche la porte « porte_auberge »", StringComparison.Ordinal));
    }

    [Fact]
    public void Zone_reliee_absente_ou_mal_nommee_refusee()
    {
        (string data, string scenarios) = Copy(v => Door(v, 2)["to"] = "zone.cave");
        WorldZonesLoadResult absent = Linked(data, scenarios);
        Assert.False(absent.IsValid);
        Assert.Empty(absent.Zones);
        // Relecture indépendante : une zone introuvable est signalée sur la porte qui y mène.
        Assert.Equal("zone.vallons", absent.Zone);
        ValidationError cave = Assert.Single(absent.Report.Errors);
        Assert.Equal(("$.data.doors[2].to", ValidationCode.UnknownReference), (cave.Path, cave.Code));
        Assert.Contains("porte « porte_maison » : zone « zone.cave » introuvable", cave.Message, StringComparison.Ordinal);

        Directory.Delete(_folder, true);
        (data, scenarios) = Copy(files: d =>
        {
            string path = ZoneJsonReader.PathIn(d, "zone.maison");
            JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            root["data"]!["id"] = "zone.grenier";
            File.WriteAllText(path, root.ToJsonString());
        });
        WorldZonesLoadResult misnamed = Linked(data, scenarios);
        Assert.False(misnamed.IsValid);
        Assert.Equal("zone.maison", misnamed.Zone);
        Assert.Contains(misnamed.Report.Errors, e => e.Path == "$.data.id" && e.Message.Contains("zone.grenier"));

        // L'aventure refuse de même de se charger.
        Assert.Throws<InvalidDataException>(() => AdventureSession.Load(data, scenarios, SavePath));
    }

    // --- Aventure ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Porte_d_un_lieu_pas_encore_reconstruit_fermee_puis_ouverte()
    {
        // Relecture indépendante de la PR #226 (de bout en bout) : devant la porte de la salle de garde de la tour de signal.
        static void AtTower(JsonObject root) => root["data"]!["safePoints"]![0]!["position"] = new JsonArray(203, 1375.5);
        (string data, string scenarios) = Copy(AtTower);
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.Equal("porte_tour_de_signal", a.DoorInReach!.Id);
        Assert.StartsWith("Porte fermée (", a.DoorHint, StringComparison.Ordinal);   // l'indication dit « fermée »
        Assert.False(a.UseDoor());
        Assert.Equal("zone.vallons", a.World.Definition!.Id);   // la zone ne change pas
        Assert.Contains("Le lieu n'est pas encore reconstruit : la porte reste fermée.", a.DrainMessages());

        // Une fois la tour reconstruite, la porte s'ouvre.
        var done = new SavedRebuilds(new[] { new SavedRebuild("tour_de_signal", 2, done: true) });
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, V11TestData.Progression).WithRebuilds(done)));
        AdventureSession b = AdventureSession.Load(data, scenarios, SavePath);
        b.Advance(0.05);
        b.DrainMessages();
        Assert.StartsWith("F : passer la porte (", b.DoorHint, StringComparison.Ordinal);
        Assert.True(b.UseDoor());
        Assert.Equal("zone.garde_du_signal", b.World.Definition!.Id);
    }

    [Fact]
    public void Entrer_dans_l_auberge_puis_en_sortir()
    {
        (string data, string scenarios) = Copy(EntryAtInn);
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.False(a.Indoors);
        Assert.Equal("porte_auberge", a.DoorInReach!.Id);
        Assert.True(a.CanUseDoor);
        Assert.Equal(20, a.Doors().Count);   // V2.46 : la grotte de la cascade ; V2.53.2 : la porte de la maison du joueur ;   // V2.43.4 : les cinq portes des lieux reconstruits ;   // sous-lot V1.7 : et la Maison de la Guilde ; V2.14 : et les quatre boutiques ; lot C : et cinq intérieurs de métier
        long tick = a.World.Save().WorldTick;

        Assert.True(a.Interact());   // F : la porte (ni PNJ ni nœud à portée)
        Assert.True(a.Indoors);
        Assert.Equal("zone.auberge", a.World.Definition!.Id);
        Assert.Equal(At(0, -4.3), Position(a));
        Assert.Equal(2, a.Opened);
        Assert.Contains("Auberge de l'Aube", a.DrainMessages());
        Assert.Equal("Auberge de l'Aube", a.ZoneName);
        Assert.Equal("Auberge de l'Aube", a.PlaceName);
        Assert.Equal("Coin du feu", a.LastSafePoint);
        // La sauvegarde a suivi : la partie est dans l'auberge, le temps du monde continue.
        WorldSave saved = SaveStore.Read(SavePath).Save!;
        Assert.Equal("zone.auberge", saved.Zone);
        Assert.True(saved.WorldTick >= tick);
        Assert.Equal(1, saved.Sessions);   // la session des Vallons jouée ; celle de l'auberge est la deuxième
        // Dans l'intérieur : ni nœud ni atelier ; la porte ramène au bourg. Sous-lot V1.6 : le seul PNJ est celui de la pièce, l'aubergiste.
        Assert.Equal(new[] { "npc.aubergiste", "npc.serveuse" }, a.Npcs().Select(n => n.Id));   // 17 novembre 2026 : et Perrine, la serveuse
        Assert.Empty(a.Nodes());
        Assert.Empty(a.Workshops());
        // Lot C (16 novembre 2026) : et les portes de la cuisine et des chambres.
        Assert.Equal(new[] { "Bourg d'Aube", "Cuisine", "Chambres" }, a.Doors().Select(d => d.Name));
        Assert.True(a.Map.Indoor);
        Assert.Equal(new[] { "sortie", "cuisine", "chambres" }, a.Map.Doors.Select(d => d.Id));
        Assert.Contains(a.MapMarkers(), m => m.Kind == MapMarkerKind.Door && m.Name == "Bourg d'Aube");
        a.Advance(0.2);
        Assert.True(a.Mood().Indoors);
        Assert.True(a.Mood().InVillage);
        // Sous-lot V2.9 : dans l'auberge, la taverne ; pas de forge dans la zone (+∞, jamais NaN).
        Assert.Equal(AmbiencePlace.TavernInside, a.Mood().Places & AmbiencePlace.TavernInside);
        Assert.True(double.IsPositiveInfinity(a.Mood().ForgeDistance));
        Assert.True(a.UseDoor());
        Assert.False(a.Indoors);
        Assert.Equal("zone.vallons", a.World.Definition!.Id);
        Assert.Equal(At(-1101, -30), Position(a));
        Assert.Equal(3, a.Opened);
        Assert.Equal("Place du village", a.LastSafePoint);   // le point sûr le plus proche de la porte (posé devant elle ici)
        Assert.Equal("zone.vallons", SaveStore.Read(SavePath).Save!.Zone);
        Assert.False(a.Map.Indoor);
        Assert.Equal(20, a.Map.Doors.Count);   // V2.46 : la grotte de la cascade ; V2.53.2 : la porte de la maison du joueur ;   // V2.43.4 : les cinq portes des lieux reconstruits ;   // V2.14 : et les quatre boutiques ; lot C : et cinq intérieurs de métier
        Assert.NotEmpty(a.Npcs());
        Assert.False(a.Mood().Indoors);
    }

    [Fact]
    public void Grand_Solitaire_l_occurrence_continue_a_travers_un_interieur()
    {
        // Relecture indépendante de la PR #178 : passer une porte (auberge) et revenir ne rend pas au boss sa vie pleine, et le temps
        // serveur de la session nouvelle ne recule pas sous celui de l'occurrence reprise.
        (string data, string scenarios) = Copy(EntryAtInn);
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        WorldBossDefinition boss = a.World.WorldBosses!.Boss("grand_solitaire");
        long w = WorldBossSchedule.WindowOf(boss, new DateTimeOffset(2027, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        WorldBossOccurrence occ;
        while (true) { if (WorldBossSchedule.InWindow(boss, w) is WorldBossOccurrence o) { occ = o; break; } w++; }
        a.SetCivilTime((occ.SignsStartMs + boss.SignsMs) / 1000 + 5);
        a.Advance(0.05);
        WorldBossEvent e = a.World.WorldBossNow("grand_solitaire")!;
        Assert.True(e.Damage("personne", 100));
        Assert.True(a.Interact());   // la porte de l'auberge
        Assert.True(a.Indoors);
        Assert.Null(a.World.WorldBosses);
        a.Advance(1);
        Assert.True(a.UseDoor());
        Assert.False(a.Indoors);
        Assert.Same(e, a.World.WorldBossNow("grand_solitaire"));
        Assert.Equal(e.MaxHealth - 100, e.Health);
        long before = e.NowMs;
        a.Advance(0.2);
        Assert.True(a.World.ServerMs >= before);
        Assert.True(e.NowMs > before);   // il avance tout de suite, sans attendre l'hôte
    }

    [Fact]
    public void Pas_de_porte_avec_un_ennemi_proche()
    {
        (string data, string scenarios) = Copy(EntryAtInn, s =>
        {
            JsonObject boar = s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
            boar["position"] = new JsonArray(-1094, -33);
        });
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.NotNull(a.DoorInReach);
        Assert.False(a.CanUseDoor);
        a.Save();
        string before = File.ReadAllText(SavePath);
        Assert.False(a.Interact());
        Assert.False(a.UseDoor());
        Assert.Contains(a.Text("text.ui.door_blocked"), a.DrainMessages());
        Assert.False(a.Indoors);
        Assert.Equal(1, a.Opened);
        Assert.Equal(before, File.ReadAllText(SavePath));
    }

    [Fact]
    public void Partie_reprise_dans_l_interieur_ou_au_depart()
    {
        (string data, string scenarios) = Copy(EntryAtInn);
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        Assert.True(a.UseDoor());
        a.Save();
        // Reprise : dans l'auberge, à son point sûr d'entrée (le coin du feu).
        AdventureSession again = AdventureSession.Load(data, scenarios, SavePath);
        again.Advance(0.05);
        Assert.True(again.Indoors);
        Assert.Equal("zone.auberge", again.World.Definition!.Id);
        Assert.Equal(At(-5.4, 0), Position(again));
        Assert.True(again.CanRest);   // halte au coin du feu
        Assert.True(again.Rest());
        Assert.True(again.Indoors);
        Assert.Equal("Coin du feu", again.LastSafePoint);

        // Une zone que les données ne relient plus : reprise à l'entrée de la zone de départ.
        WorldSave lost = SaveStore.Read(SavePath).Save!;
        SaveStore.Write(SavePath, new WorldSave("zone.grenier", lost.WorldTick, lost.Sessions, lost.Consumed, lost.Character));
        AdventureSession back = AdventureSession.Load(data, scenarios, SavePath);
        back.Advance(0.05);
        Assert.False(back.Indoors);
        Assert.Equal("zone.vallons", back.World.Definition!.Id);
        Assert.Equal(At(-1101, -31.5), Position(back));
    }

    [Fact]
    public void Histoire_et_metiers_restent_dehors()
    {
        // La mission commencée : dans l'auberge, son objectif sur la carte est la porte ; on n'y parle à aucun PNJ, on n'y récolte rien.
        (string data, string scenarios) = Copy(EntryAtInn);
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(data), ShippedData).Progression!;
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("traces") }, null);
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.auberge", 0, 3, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, progression).WithStory(story)));
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        Assert.True(a.Indoors);
        Assert.False(a.World.InStoryZone);
        MapMarker objective = a.Objective!;
        Assert.Equal((0.0, -4.3), (objective.X, objective.Z));
        Assert.Single(a.Journal(), j => !j.Done);   // le journal suit le personnage
        Assert.Null(a.NpcInReach);
        Assert.Null(a.NodeInReach);
        Assert.Null(a.WorkshopHere);
        Assert.Equal(DialogueRefusal.TooFar, a.World.Talk("npc.odile", out _));
        Assert.Equal(CraftRefusal.NotHere, a.World.Gather(a.World.Workshop!.Book.Nodes[0].Id, out _));
    }

    [Fact]
    public void Session_ouverte_a_une_porte()
    {
        WorldZonesLoadResult linked = Linked();
        ZoneDefinition chapelle = linked.Zones.Single(z => z.Id == "zone.chapelle");
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        var save = new WorldSave("zone.chapelle", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", progression));
        var at = new WorldSession(save, chapelle, ShippedData, progression, Items, door: "sortie");
        at.Step();
        Assert.Equal(chapelle.DoorOf("sortie")!.Position, at.Zone.Engine.View("bastion").Position);
        Assert.True(at.InStoryZone);   // sans zone d'histoire donnée : celle-ci
        Assert.Throws<ArgumentException>(() => new WorldSession(save, chapelle, ShippedData, progression, Items, safePoint: "choeur", door: "sortie"));
        Assert.Throws<ArgumentException>(() => new WorldSession(save, chapelle, ShippedData, progression, Items, door: "porte"));
        var elsewhere = new WorldSession(save, chapelle, ShippedData, progression, Items, storyZone: "zone.vallons");
        elsewhere.Step();
        Assert.False(elsewhere.InStoryZone);
        Assert.Equal(chapelle.Entry.Position, elsewhere.Zone.Engine.View("bastion").Position);
    }

    [Fact]
    public void Interieurs_praticables()
    {
        // Chaque intérieur : de la porte, on atteint le point sûr (couloirs d'au moins 1 m entre les meubles) ; chaque meuble laisse le
        // personnage passer autour de lui ou est contre un mur.
        foreach (ZoneDefinition inside in Linked().Zones.Skip(1))
        {
            CombatSpace space = inside.Setup.Space!;
            // Lot C (16 novembre 2026) : chaque porte (cuisine et chambres de l'auberge, étage du moulin), pas seulement la sortie.
            foreach (Door door in inside.Doors) Assert.True(Reachable(space, door.Position, inside.Entry.Position), inside.Id + " : " + door.Id);
            // V2.53.2 : la maison du joueur est vide : il la meuble lui-même.
            Assert.InRange(space.Obstacles.Count, inside.Id == "zone.maison_joueur" ? 0 : inside.Id == "zone.auberge_cuisine" ? 3 : 4, 64);   // la cuisine (fiche A0) : trois meubles
        }
    }

    // --- Aménagement (rendu) ----------------------------------------------------------------------------------------------

    /// <summary>Carte d'un intérieur comme la session la donne : lieux, portes, places dégagées (points sûrs et portes).</summary>
    internal static AdventureMap MapOf(ZoneDefinition zone)
    {
        static double M(Fixed f) => f.Raw / 10000.0;
        return new AdventureMap(zone.Setup.Space!,
            zone.Places.Select(p => new MapPlace(p.Id, p.Name, M(p.Min.X), M(p.Min.Z), M(p.Max.X), M(p.Max.Z))), null,
            zone.SafePoints.Select(s => (M(s.Position.X), M(s.Position.Z))).Concat(zone.Doors.Select(d => (M(d.Position.X), M(d.Position.Z)))),
            zone.Doors.Select(d => new MapDoor(d.Id, d.Name, d.Building, M(d.Position.X), M(d.Position.Z))), zone.Indoor);
    }

    private static string Describe(InteriorPiece p) => FormattableString.Invariant(
        $"{p.Id}:{p.Prop}:{p.X:R}:{p.Z:R}:{p.Yaw:R}:{p.Fit:R}:{p.Height:R}:{p.Y:R}:{p.Parent}:{p.OnWall}");

    [Fact]
    public void Amenagement_des_interieurs()
    {
        var expected = new Dictionary<string, (InteriorStyle Style, InteriorProp[] Props, int Villagers)>
        {
            ["zone.auberge"] = (InteriorStyle.Tavern, new[] { InteriorProp.Table, InteriorProp.Bench, InteriorProp.Barrel, InteriorProp.Bottles,
                InteriorProp.Lantern, InteriorProp.HangingLantern, InteriorProp.Shield, InteriorProp.Crate, InteriorProp.Drawers, InteriorProp.Door }, 3),
            ["zone.chapelle"] = (InteriorStyle.Chapel, new[] { InteriorProp.Candelabra, InteriorProp.Candlestick, InteriorProp.Throne,
                InteriorProp.Chandelier, InteriorProp.Statue, InteriorProp.Painting, InteriorProp.Door }, 1),
            ["zone.maison"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Chest, InteriorProp.Cabinet, InteriorProp.SpinningWheel,
                InteriorProp.Table, InteriorProp.Stool, InteriorProp.CuttingBoard, InteriorProp.Pot, InteriorProp.HangingLantern, InteriorProp.Door }, 1),
            // Sous-lot V1.7 : la Maison de la Guilde (comptoir de la maîtresse, réserve de l'intendant) et ses archives, en genre maison.
            ["zone.guilde"] = (InteriorStyle.House, new[] { InteriorProp.Table, InteriorProp.Crate, InteriorProp.Door }, 1),
            ["zone.archives"] = (InteriorStyle.House, new[] { InteriorProp.Table, InteriorProp.Door }, 1),
            // Sous-lot V1.9.6 : la salle du souvenir de la Rupture (aménagement provisoire en maison : la porte de lumière en autel, les
            // Seuillards en statues ; son vrai décor est pour la PR B, voir MONDE.md), la reine Ysolde.
            ["zone.souvenir_rupture"] = (InteriorStyle.House, new[] { InteriorProp.Statue, InteriorProp.Door }, 1),
            // Sous-lot V2.14 : les boutiques ; à la boulangerie, le four, le pétrin (farine, jarre), les pains. Réponse de Damien du 9 novembre
            // 2026 : plus de commis de décor ; les employés nommés (Bastien, Lucie, Tiphaine, Jeannot) et Margot sont des PNJ, posés par le jeu.
            ["zone.boulangerie"] = (InteriorStyle.Shop, new[] { InteriorProp.Bread, InteriorProp.Broom, InteriorProp.Basket, InteriorProp.ClayPot, InteriorProp.Crate,
                InteriorProp.Barrel, InteriorProp.Door }, 0),
            ["zone.epicerie"] = (InteriorStyle.Shop, new[] { InteriorProp.Shelves, InteriorProp.Barrel, InteriorProp.Crate, InteriorProp.Door }, 0),
            ["zone.herboristerie"] = (InteriorStyle.Shop, new[] { InteriorProp.Table, InteriorProp.Barrel, InteriorProp.Door }, 0),
            ["zone.mercerie"] = (InteriorStyle.Shop, new[] { InteriorProp.Cloth, InteriorProp.SpinningWheel, InteriorProp.Table, InteriorProp.Chest, InteriorProp.Door }, 0),
            // Lot C (décisions de Damien du 16 novembre 2026) : intérieurs de métier, meublés par les modèles du lot C (objets de métier :
            // InteriorFurniture.Fixture) ; personne n'y travaille ni n'y dort (aucun emploi du temps ne les prévoit).
            ["zone.forge"] = (InteriorStyle.House, new[] { InteriorProp.Pot, InteriorProp.HangingLantern, InteriorProp.Door }, 0),
            ["zone.grange"] = (InteriorStyle.House, new[] { InteriorProp.Chest, InteriorProp.HangingLantern, InteriorProp.Door }, 0),
            ["zone.moulin"] = (InteriorStyle.House, new[] { InteriorProp.SpinningWheel, InteriorProp.HangingLantern, InteriorProp.Door }, 0),
            ["zone.moulin_meules"] = (InteriorStyle.House, new[] { InteriorProp.Crate, InteriorProp.HangingLantern, InteriorProp.Door }, 0),
            ["zone.tour"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Table, InteriorProp.Chest, InteriorProp.HangingLantern, InteriorProp.Door }, 0),
            ["zone.chaumiere"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Table, InteriorProp.Chest, InteriorProp.Shelves,
                InteriorProp.Pot, InteriorProp.Door }, 0),
            ["zone.auberge_cuisine"] = (InteriorStyle.Tavern, new[] { InteriorProp.Pot, InteriorProp.Table, InteriorProp.Shelves, InteriorProp.Door }, 0),
            ["zone.auberge_chambres"] = (InteriorStyle.Tavern, new[] { InteriorProp.Bed, InteriorProp.Chest, InteriorProp.Door }, 0),
            ["zone.auberge_logement"] = (InteriorStyle.Tavern, new[] { InteriorProp.Bed, InteriorProp.Chest, InteriorProp.Door }, 0),
            ["zone.auberge_combles"] = (InteriorStyle.Tavern, new[] { InteriorProp.Shelves, InteriorProp.Chest, InteriorProp.Crate, InteriorProp.Door }, 0),
            // V2.43.4 (décision de Damien du 20 novembre 2026) : les intérieurs des lieux reconstruits, meublés par le lot C ; personne n'y vit.
            ["zone.atelier_charron"] = (InteriorStyle.House, new[] { InteriorProp.Chest, InteriorProp.Door }, 0),
            ["zone.relais_pont"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Table, InteriorProp.Door }, 0),
            ["zone.foulon"] = (InteriorStyle.House, new[] { InteriorProp.SpinningWheel, InteriorProp.Table, InteriorProp.Shelves, InteriorProp.Door }, 0),
            ["zone.garde_du_signal"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Table, InteriorProp.Chest, InteriorProp.Door }, 0),
            ["zone.dortoir_abbaye"] = (InteriorStyle.House, new[] { InteriorProp.Bed, InteriorProp.Chest, InteriorProp.Shelves, InteriorProp.Door }, 0),
            // V2.53.2 : la maison du joueur, vide (les meubles posés par le joueur sont dessinés à part, V2.53.3).
            ["zone.maison_joueur"] = (InteriorStyle.House, new[] { InteriorProp.Door }, 0),
        };
        List<ZoneDefinition> insides = Linked().Zones.Where(z => z.Indoor).ToList();
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.Ordinal), insides.Select(z => z.Id).OrderBy(k => k, StringComparer.Ordinal));
        foreach (ZoneDefinition inside in insides)
        {
            AdventureMap map = MapOf(inside);
            InteriorLayout layout = InteriorRules.Layout(map), again = InteriorRules.Layout(MapOf(inside));
            (InteriorStyle style, InteriorProp[] props, int villagers) = expected[inside.Id];
            Assert.Equal(style, layout.Style);
            Assert.Equal(InteriorRules.WallHeight(style), layout.WallHeight);
            // Mêmes données, même aménagement.
            Assert.Equal(layout.Pieces.Select(Describe), again.Pieces.Select(Describe));
            Assert.Equal(layout.Lights.Select(l => (l.Kind, l.X, l.Y, l.Z, l.Range, l.Intensity)), again.Lights.Select(l => (l.Kind, l.X, l.Y, l.Z, l.Range, l.Intensity)));
            Assert.Equal(layout.Windows.Select(w => (w.X, w.Z, w.Wall)), again.Windows.Select(w => (w.X, w.Z, w.Wall)));
            // Chaque meuble des données est reconnu ; les objets attendus du genre de la pièce sont là.
            foreach (SpaceObstacle o in map.Obstacles) Assert.True(InteriorRules.FurnitureFor(o.Id) != InteriorFurniture.None, inside.Id + " : " + o.Id);
            foreach (InteriorProp prop in props) Assert.True(layout.Pieces.Any(p => p.Prop == prop), inside.Id + " : " + prop);
            Assert.Equal(map.Doors.Count, layout.Pieces.Count(p => p.Prop == InteriorProp.Door));   // sous-lot V1.7 : la guilde a deux portes
            // Identifiants uniques ; un objet posé sur un autre vient après lui ; tout est dans la pièce, sous le plafond.
            Assert.Equal(layout.Pieces.Count, layout.Pieces.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count());
            for (int i = 0; i < layout.Pieces.Count; i++)
            {
                InteriorPiece p = layout.Pieces[i];
                Assert.InRange(p.Parent, -1, i - 1);
                Assert.NotEqual(InteriorProp.None, p.Prop);
                Assert.True(p.X >= map.MinX && p.X <= map.MaxX && p.Z >= map.MinZ && p.Z <= map.MaxZ, inside.Id + " : " + p.Id);
                Assert.InRange(p.Y + p.Height, 0, layout.WallHeight);
                Assert.True(p.Fit > 0 || p.Height > 0, inside.Id + " : " + p.Id);
            }
            // Lumières : le feu ou les bougies, bornées, dans la pièce ; chaque lanterne et chaque lustre a la sienne (relecture : les
            // lumières du plafond passent avant les bougies des murs).
            Assert.True(layout.Lights.Count >= 2 && layout.Lights.Count <= InteriorRules.MaxLights, inside.Id + " : " + layout.Lights.Count + " lumières");
            foreach (InteriorPiece lit in layout.Pieces.Where(p => p.Prop == InteriorProp.Lantern || p.Prop == InteriorProp.HangingLantern || p.Prop == InteriorProp.Chandelier))
                Assert.True(layout.Lights.Any(l => Math.Abs(l.X - lit.X) < 0.6 && Math.Abs(l.Z - lit.Z) < 0.6), inside.Id + " : " + lit.Id + " sans lumière");
            // Objets posés au sol à côté l'un de l'autre dans une même emprise : ils ne se chevauchent pas et restent dans l'emprise.
            foreach (SpaceObstacle o in map.Obstacles)
            {
                double ox0 = o.Points.Min(q => q.X.Raw) / 10000.0, ox1 = o.Points.Max(q => q.X.Raw) / 10000.0;
                double oz0 = o.Points.Min(q => q.Z.Raw) / 10000.0, oz1 = o.Points.Max(q => q.Z.Raw) / 10000.0;
                var floor = layout.Pieces.Where(p => p.Id.StartsWith(o.Id + "/", StringComparison.Ordinal) && p.Parent == -1 && p.Y == 0 && p.Fit > 0
                    && p.Prop is InteriorProp.Crate or InteriorProp.Barrel or InteriorProp.Bucket).ToList();
                foreach (InteriorPiece p in floor)
                    Assert.True(p.X - p.Fit / 2 >= ox0 - 0.03 && p.X + p.Fit / 2 <= ox1 + 0.03 && p.Z - p.Fit / 2 >= oz0 - 0.03 && p.Z + p.Fit / 2 <= oz1 + 0.03,
                        inside.Id + " : " + p.Id + " hors de son emprise");
                for (int i = 0; i < floor.Count; i++)
                    for (int j = i + 1; j < floor.Count; j++)
                        Assert.True(Math.Max(Math.Abs(floor[i].X - floor[j].X), Math.Abs(floor[i].Z - floor[j].Z)) >= (floor[i].Fit + floor[j].Fit) / 2 - 0.01,
                            inside.Id + " : " + floor[i].Id + " chevauche " + floor[j].Id);
            }
            // Le siège du prêtre et la hachette de la cheminée sont hors des meubles.
            foreach (InteriorPiece p in layout.Pieces.Where(p => p.Prop == InteriorProp.Throne || p.Prop == InteriorProp.Hatchet))
                Assert.DoesNotContain(map.Obstacles, o => p.X > o.Points.Min(q => q.X.Raw) / 10000.0 - 0.3 && p.X < o.Points.Max(q => q.X.Raw) / 10000.0 + 0.3
                    && p.Z > o.Points.Min(q => q.Z.Raw) / 10000.0 - 0.3 && p.Z < o.Points.Max(q => q.Z.Raw) / 10000.0 + 0.3 && !(p.Prop == InteriorProp.Hatchet && p.Id.StartsWith(o.Id + "/", StringComparison.Ordinal)));   // lot C : sa propre cheminée, quel que soit son nom
            Assert.All(layout.Lights, l => Assert.True(l.X > map.MinX && l.X < map.MaxX && l.Z > map.MinZ && l.Z < map.MaxZ && l.Y > 0 && l.Y < layout.WallHeight));
            // Fenêtres sur les murs, jamais devant la porte ; poutres dans la pièce.
            // Lot C : la forge couverte n'a aucune baie (fiche F0 : auvent ouvert, éclairée par l'accès et la lanterne) ; les deux fenêtres de la
            // maison paysanne (fiche V0) sont au-dessus du coffre et de l'étagère, places que l'aménagement laisse aux meubles (limite connue).
            // Même chose pour la fenêtre arrière de la cuisine de l'auberge (fiche A0), derrière sa table et son étagère.
            Assert.True(layout.Windows.Count > 0 || inside.Id is "zone.forge" or "zone.chaumiere" or "zone.auberge_cuisine", inside.Id + " : aucune fenêtre");
            Assert.All(layout.Windows, w => Assert.True(w.Bottom < w.Top && w.Top < layout.WallHeight && w.Width > 0));
            Assert.All(layout.Beams, b => Assert.InRange(b, layout.BeamsAlongX ? map.MinZ : map.MinX, layout.BeamsAlongX ? map.MaxZ : map.MaxX));
            // Villageois hors des meubles et loin de la porte.
            Assert.Equal(villagers, layout.Villagers.Count);
            foreach (StyleVillager v in layout.Villagers)
            {
                Assert.True(v.X > map.MinX && v.X < map.MaxX && v.Z > map.MinZ && v.Z < map.MaxZ, inside.Id + " : " + v.Id);
                Assert.DoesNotContain(map.Obstacles, o => v.X >= o.Points.Min(q => q.X.Raw) / 10000.0 && v.X <= o.Points.Max(q => q.X.Raw) / 10000.0
                    && v.Z >= o.Points.Min(q => q.Z.Raw) / 10000.0 && v.Z <= o.Points.Max(q => q.Z.Raw) / 10000.0);
            }
        }
    }

    [Theory]
    [InlineData("Chandelier_03_1k.fbx", InteriorProp.Chandelier)]
    [InlineData("GothicBed_01_1k.fbx", InteriorProp.Bed)]
    [InlineData("GothicCabinet_01_1k.fbx", InteriorProp.Cabinet)]
    [InlineData("GothicCommode_01_1k.fbx", InteriorProp.Commode)]
    [InlineData("WoodenChair_01_1k.fbx", InteriorProp.Throne)]
    [InlineData("WoodenTable_01_1k.fbx", InteriorProp.Bench)]
    [InlineData("small_wooden_table_01_1k.fbx", InteriorProp.Table)]
    [InlineData("wooden_table_02_1k.fbx", InteriorProp.Table)]
    [InlineData("painted_wooden_table_1k.fbx", InteriorProp.Table)]
    [InlineData("painted_wooden_bench_1k.fbx", InteriorProp.Bench)]
    [InlineData("painted_wooden_shelves_1k.fbx", InteriorProp.Shelves)]
    [InlineData("painted_wooden_stool_1k.fbx", InteriorProp.Stool)]
    [InlineData("wooden_stool_01_1k.fbx", InteriorProp.Stool)]
    [InlineData("gallinera_chair_1k.fbx", InteriorProp.Chair)]
    [InlineData("vintage_wooden_drawer_01_1k.fbx", InteriorProp.Drawers)]
    [InlineData("treasure_chest_1k.fbx", InteriorProp.Chest)]
    [InlineData("wooden_crate_01_1k.fbx", InteriorProp.Crate)]
    [InlineData("wine_barrel_01_1k.fbx", InteriorProp.Barrel)]
    [InlineData("wooden_bucket_02_1k.fbx", InteriorProp.Bucket)]
    [InlineData("lantern_chandelier_01_1k.fbx", InteriorProp.HangingLantern)]
    [InlineData("wooden_lantern_01_1k.fbx", InteriorProp.Lantern)]
    [InlineData("brass_candleholders_1k.fbx", InteriorProp.Candelabra)]
    [InlineData("wooden_candlestick_1k.fbx", InteriorProp.Candlestick)]
    [InlineData("wooden_bowl_01_1k.fbx", InteriorProp.Bowl)]
    [InlineData("carved_wooden_plate_1k.fbx", InteriorProp.Plate)]
    [InlineData("brass_goblets_1k.fbx", InteriorProp.Goblets)]
    [InlineData("ceramic_pot_1k.fbx", InteriorProp.ClayPot)]
    [InlineData("brass_pot_01_1k.fbx", InteriorProp.Pot)]
    [InlineData("wine_bottles_01_1k.fbx", InteriorProp.Bottles)]
    [InlineData("wicker_basket_01_1k.fbx", InteriorProp.Basket)]
    [InlineData("wooden_cutting_board_1k.fbx", InteriorProp.CuttingBoard)]
    [InlineData("wooden_spoon_1k.fbx", InteriorProp.Spoon)]
    [InlineData("food_apple_01_1k.fbx", InteriorProp.Apple)]
    [InlineData("yellow_onion_1k.fbx", InteriorProp.Onion)]
    [InlineData("kite_shield_1k.fbx", InteriorProp.Shield)]
    [InlineData("antique_estoc_1k.fbx", InteriorProp.Sword)]
    [InlineData("ornate_war_hammer_1k.fbx", InteriorProp.WarHammer)]
    [InlineData("wooden_axe_1k.fbx", InteriorProp.Axe)]
    [InlineData("hatchet_1k.fbx", InteriorProp.Hatchet)]
    [InlineData("wooden_broom_1k.fbx", InteriorProp.Broom)]
    [InlineData("fancy_picture_frame_02_1k.fbx", InteriorProp.Painting)]
    [InlineData("spinning_wheel_01_1k.fbx", InteriorProp.SpinningWheel)]
    [InlineData("large_castle_door_1k.fbx", InteriorProp.Door)]
    [InlineData("gothic_statue_1k.fbx", InteriorProp.Statue)]
    [InlineData("rock_moss_01_1k.fbx", InteriorProp.None)]
    public void Modeles_d_interieur_reconnus(string file, InteriorProp prop)
    {
        Assert.Equal(prop, InteriorRules.PropOf("Assets/Style/PolyHaven/Interieurs/x/" + file));
        Assert.Equal(prop, InteriorRules.PropOf(@"Assets\Style\PolyHaven\Interieurs\x\" + file));
    }

    [Fact]
    public void Objets_d_interieur_hors_du_decor_des_vallons()
    {
        const string inside = "Assets/Style/PolyHaven/Interieurs/wine_barrel_01/wine_barrel_01_1k.fbx";
        const string outside = "Assets/Style/PolyHaven/Models/wine_barrel_01/wine_barrel_01_1k.fbx";
        Assert.True(InteriorRules.IsInterior(inside));
        Assert.True(InteriorRules.IsInterior(@"Assets\Style\PolyHaven\INTERIEURS\a.fbx"));
        Assert.False(InteriorRules.IsInterior(outside));
        Assert.False(InteriorRules.IsInterior("Assets/Style/PolyHaven/InterieursAnciens/a.fbx"));
        Assert.False(InteriorRules.IsInterior(null!));
        Assert.Equal(new[] { outside }, StyleRules.PropCandidates(new[] { inside, outside }, StyleProp.Barrel));
        Assert.False(StyleRules.Matches("Assets/Style/PolyHaven/Interieurs/gothic_statue/rock_statue_1k.fbx", StyleSlot.Rock));
        Assert.Equal(InteriorFurniture.Table, InteriorRules.FurnitureFor("table3"));
        Assert.Equal(InteriorFurniture.Pew, InteriorRules.FurnitureFor("banc8"));
        Assert.Equal(InteriorFurniture.Hearth, InteriorRules.FurnitureFor("cheminee_nord"));
        // Lot C (relecture de la PR #209) : mots stricts des objets de métier (identifiant entier, ou suivi d'un séparateur ou d'un chiffre).
        Assert.Equal(InteriorFurniture.Fixture, InteriorRules.FurnitureFor("bac_de_trempe"));
        Assert.Equal(InteriorFurniture.Fixture, InteriorRules.FurnitureFor("foin"));
        Assert.Equal(InteriorFurniture.Fixture, InteriorRules.FurnitureFor("sacs2"));
        Assert.Equal(InteriorFurniture.Fixture, InteriorRules.FurnitureFor("meules"));
        Assert.Equal(InteriorFurniture.Shelf, InteriorRules.FurnitureFor("rayonnage_linge"));
        Assert.Equal(InteriorFurniture.None, InteriorRules.FurnitureFor("bacon"));
        Assert.Equal(InteriorFurniture.None, InteriorRules.FurnitureFor("sacristie"));
        Assert.Equal(InteriorFurniture.None, InteriorRules.FurnitureFor("foire"));
        Assert.Equal(InteriorFurniture.None, InteriorRules.FurnitureFor("chaises"));
        Assert.Equal(InteriorFurniture.Bed, InteriorRules.FurnitureFor("lit"));
        Assert.Equal(InteriorFurniture.None, InteriorRules.FurnitureFor("fenetre"));
    }

    [Fact]
    public void Carte_peinte_d_une_piece()
    {
        // Relecture indépendante : la carte d'un intérieur est un plan (plancher, meubles, murs, porte), pas un pré.
        ZoneDefinition auberge = Linked().Zones.Single(z => z.Id == "zone.auberge");
        AdventureMap map = MapOf(auberge);
        (int width, int height) = MapPainter.Size(map);
        Assert.Equal(((int)Math.Ceiling((14 + 2 * MapPainter.IndoorMargin) * MapPainter.PixelsPerMeter), (int)Math.Ceiling((10 + 2 * MapPainter.IndoorMargin) * MapPainter.PixelsPerMeter)), (width, height));
        byte[] image = MapPainter.Paint(map, width, height);
        Assert.Equal(image, MapPainter.Paint(map, width, height));
        (double R, double G, double B) At(double x, double z)
        {
            (double u, double v) = MapPainter.ToImage(map, x, z);
            int col = Math.Min(width - 1, (int)(u * width)), row = Math.Min(height - 1, (int)((1 - v) * height));
            int i = (row * width + col) * 4;
            return (image[i] / 255.0, image[i + 1] / 255.0, image[i + 2] / 255.0);
        }
        var floor = At(-1.7, 0.4);   // entre les tables
        var table = At(-3.5, -1.75);   // milieu de table1
        var night = At(-8.5, 0);   // dehors
        Assert.True(floor.R > floor.G && floor.G > floor.B && floor.R > 0.35, $"plancher {floor}");
        Assert.True(table.R + table.G + table.B < floor.R + floor.G + floor.B, $"table {table}");
        Assert.True(night.R + night.G + night.B < 0.3, $"dehors {night}");
        Assert.Equal((0.5, 0.5), MapPainter.ToImage(map, 0, 0));
    }

    [Fact]
    public void Ambiance_des_interieurs()
    {
        // Relecture indépendante : dans une pièce, ni oiseaux ni rivière ; le vent du dehors à peine.
        Assert.Equal(0, StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, true, false, indoors: true));
        Assert.Equal(0, StyleRules.AmbienceVolume(StyleAmbience.River, 2, true, false, indoors: true));
        Assert.InRange(StyleRules.AmbienceVolume(StyleAmbience.Wind, double.NaN, true, false, indoors: true), 0.01, 0.2);
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, true, false) > 0.5);
    }

    [Fact]
    public void Carte_de_metal_des_objets()
    {
        string[] maps =
        {
            "I/wooden_axe/textures/wooden_axe_metal_1k.exr", "I/wooden_axe/textures/wooden_axe_nor_gl_1k.exr",
            "I/GothicBed_01/textures/GothicBed_01_metallic_1k.exr", "I/GothicBed_01/textures/GothicBed_01_roughness_1k.jpg",
            "I/kite_shield/textures/kite_shield_body_metal_1k.png", "I/kite_shield/textures/kite_shield_clamps_trim_metal_1k.png",
        };
        Assert.Equal(maps[0], StyleRules.MetalFor("I/wooden_axe/textures/wooden_axe_diff_1k.jpg", maps));
        Assert.Equal(maps[2], StyleRules.MetalFor("I/GothicBed_01/textures/GothicBed_01_diff_1k.jpg", maps));
        Assert.Equal(maps[4], StyleRules.MetalFor(@"I\kite_shield\textures\kite_shield_body_diff_1k.png", maps));
        Assert.Equal(maps[5], StyleRules.MetalFor("I/kite_shield/textures/kite_shield_clamps_trim_diff_1k.png", maps));
        Assert.Null(StyleRules.MetalFor("I/wooden_bowl_01/textures/wooden_bowl_01_diff_1k.jpg", maps));
        Assert.Null(StyleRules.MetalFor("I/wooden_axe/textures/wooden_axe_rough_1k.jpg", maps));
        Assert.Null(StyleRules.MetalFor(null, maps));
        Assert.Null(StyleRules.MetalFor("I/wooden_axe/textures/wooden_axe_diff_1k.jpg", new string[] { null! }));
        Assert.True(StyleRules.IsMetalMap(maps[0]) && StyleRules.IsMetalMap(maps[2]));
        Assert.False(StyleRules.IsMetalMap(maps[1]) || StyleRules.IsMetalMap("I/a/textures/metalwork_diff_1k.jpg"));
    }

    /// <summary>Recherche en largeur sur une grille de 25 cm : un disque de 0,5 m peut-il aller de <paramref name="from"/> à <paramref name="to"/> ?</summary>
    private static bool Reachable(CombatSpace space, GroundVector from, GroundVector to)
    {
        double x0 = space.Min.X.Raw / 10000.0, z0 = space.Min.Z.Raw / 10000.0, x1 = space.Max.X.Raw / 10000.0, z1 = space.Max.Z.Raw / 10000.0;
        const double step = 0.25, r = 0.5;
        int nx = (int)Math.Round((x1 - x0) / step), nz = (int)Math.Round((z1 - z0) / step);
        var boxes = space.Obstacles.Select(o => (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0,
            o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0)).ToList();
        bool Free(int i, int j)
        {
            double x = x0 + i * step, z = z0 + j * step;
            if (x - r < x0 - 1e-9 || x + r > x1 + 1e-9 || z - r < z0 - 1e-9 || z + r > z1 + 1e-9) return false;
            foreach (var (a, b, c, d) in boxes)
            {
                double dx = Math.Max(0, Math.Max(a - x, x - c)), dz = Math.Max(0, Math.Max(b - z, z - d));
                if (dx * dx + dz * dz < r * r - 1e-9) return false;
            }
            return true;
        }
        (int, int) Cell(GroundVector p) => ((int)Math.Round((p.X.Raw / 10000.0 - x0) / step), (int)Math.Round((p.Z.Raw / 10000.0 - z0) / step));
        var start = Cell(from);
        var goal = Cell(to);
        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int, int)>(new[] { start });
        while (queue.Count > 0)
        {
            var (i, j) = queue.Dequeue();
            if ((i, j) == goal) return true;
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (i + di, j + dj);
                if (next.Item1 < 0 || next.Item2 < 0 || next.Item1 > nx || next.Item2 > nz || !seen.Add(next) || !Free(next.Item1, next.Item2)) continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }

    [Fact]
    public void Montants_du_comptoir_en_nombre_borne()
    {
        // Revue Codex de la PR #67 : un comptoir de 0,2 m donnait un pas nul et une boucle sans fin dans Unity.
        Assert.Equal(new[] { 0.1 }, InteriorRules.CounterPosts(0.2));
        Assert.Equal(new[] { 0.05 }, InteriorRules.CounterPosts(0.1));
        Assert.Empty(InteriorRules.CounterPosts(0));
        Assert.Empty(InteriorRules.CounterPosts(-1));
        Assert.Empty(InteriorRules.CounterPosts(double.NaN));
        Assert.Empty(InteriorRules.CounterPosts(double.PositiveInfinity));
        IReadOnlyList<double> posts = InteriorRules.CounterPosts(3.0);
        Assert.Equal(4, posts.Count);
        Assert.Equal(0.1, posts[0], 9);
        Assert.Equal(2.9, posts[^1], 9);
        Assert.True(posts.Zip(posts.Skip(1), (a, b) => b - a).All(d => d > 0.8 && d < 1.0));
        Assert.Equal(65, InteriorRules.CounterPosts(1e9).Count);
        Assert.Equal(2, InteriorRules.CounterPosts(0.21).Count);
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Interior.cs"));
        Assert.Contains("foreach (double post in InteriorRules.CounterPosts(length))", view, StringComparison.Ordinal);
        Assert.DoesNotContain("s += (length - 0.2f)", view, StringComparison.Ordinal);
    }
}
