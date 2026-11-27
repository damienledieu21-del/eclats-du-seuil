using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Seasons;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Time;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : contenu saisonnier en données (<c>kind: "seasons"</c>, schéma 1, validateur
/// unique) : herbes rares au printemps, champignons en automne, plus d'Échos dans la brume en automne (taux de présence borné), passage du
/// lac gelé ouvert pendant la fenêtre de glace, verrou de l'île secrète (point d'accroche à venir) ; date fournie par l'hôte à la session ;
/// rendu de la saison (teinte, neige, glace), affichage seulement.
/// </summary>
[Trait("Category", "World")]
public class SaisonsContenuTests
{
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;

    private static ZoneDefinition Zone(double x = -18, double z = -12)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, Items).Zone!;
    }

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, Items).Book!;

    private static SeasonBook Book(ZoneDefinition zone) => SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone).Book!;

    private static CraftingLoadResult CraftingMutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        ZoneDefinition zone = Zone();
        return CraftingJsonReader.Read(root.ToJsonString(), Texts, zone, Items);
    }

    private static JsonObject NodeOf(JsonObject root, string id) =>
        root["data"]!["nodes"]!.AsArray().Select(n => n!.AsObject()).Single(n => (string)n["id"]! == id);

    private static long Utc(int y, int mo, int d, int h = 12) => new DateTimeOffset(y, mo, d, h, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static SeasonLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(SeasonJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return SeasonJsonReader.Read(root.ToJsonString(), Zone());
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();

    private static string First(SeasonLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    private static WorldSession Play(ZoneDefinition zone, bool seasons = true)
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items, null, Crafting(zone));
        if (seasons) session.UseSeasons(Book(zone));
        session.Step();
        return session;
    }

    [Fact]
    public void Contenu_livre_valide()
    {
        SeasonLoadResult r = SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), Zone());
        Assert.True(r.IsValid, r.Report.ToString());
        SeasonBook b = r.Book!;
        Assert.Equal(SeasonValidator.CurrentSchemaVersion, b.SchemaVersion);
        // Nœuds de saison : dans les métiers (version 4).
        CraftingBook c = Crafting(Zone());
        Assert.Equal(CraftingValidator.CurrentSchemaVersion, c.SchemaVersion);
        Assert.Equal(new[] { "cepes_foret", "primevere_ruisseau", "raisin_vignes_est", "raisin_vignes_ouest", "sauge_verger", "trompettes_foret" },   // V2.44.2 : vendanges
            c.Nodes.Where(n => n.IsSeasonal).Select(n => n.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.True(c.Node("primevere_ruisseau").InSeason(Season.Spring));
        Assert.False(c.Node("primevere_ruisseau").InSeason(Season.Summer));
        Assert.False(c.Node("primevere_ruisseau").InSeason(null));
        Assert.True(c.Node("cepes_foret").InSeason(Season.Autumn));
        Assert.False(c.Node("cepes_foret").InSeason(Season.Winter));
        // Les nœuds d'avant (herbes, champignons des quêtes, filons) restent de toute l'année, même sans date.
        Assert.True(c.Node("herbes_village").InSeason(null));
        Assert.True(c.Node("champignons_pre").InSeason(Season.Winter));
        // Relecture de la PR #168 (point 3, après la grande carte #167) : chaque nœud de saison est dans l'aire et praticable : un personnage
        // peut se tenir dessus (point sûr posé au nœud : ni obstacle, ni hors de l'aire).
        foreach (GatheringNode n in c.Nodes.Where(n => n.IsSeasonal))
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray((double)n.Position.X.Raw / 10000, (double)n.Position.Z.Raw / 10000);
            ZoneLoadResult probe = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, Items);
            Assert.True(probe.Zone != null, $"{n.Id} : {probe.Report}");
        }
        foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Winter })
            Assert.True(b.MistEchoPresence(Season.Autumn) > b.MistEchoPresence(s));
        Assert.Equal(Fixed.Parse("0.4"), b.MistEchoPresence(Season.Autumn));
        Assert.Equal(2, b.MaxEchoesPerVolume);
        Assert.True(b.Passage("glace_du_lac").IsOpen(IcePhase.Walkable));
        Assert.False(b.Passage("glace_du_lac").IsOpen(IcePhase.Forming));
        Assert.False(b.Passage("glace_du_lac").IsOpen(null));
        Assert.Equal((Fixed.FromInt(180), Fixed.FromInt(-1230)), (b.Lock("ile_des_saules_dormants").Anchor!.Value.X, b.Lock("ile_des_saules_dormants").Anchor!.Value.Z));   // V2.36 : l'île des saules dormants est sur la carte
        Assert.True(b.LockOpen("ile_des_saules_dormants", IcePhase.Walkable));
        Assert.Throws<ArgumentException>(() => b.Passage("pont"));
        // Le schéma JSON (documentation) accepte le fichier livré.
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "seasons.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(SeasonJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Contenu_invalide_refuse_avec_son_chemin()
    {
        // Nœuds de saison, dans les métiers (version 4).
        static string FirstCrafting(CraftingLoadResult r) { Assert.False(r.IsValid); return r.Report.Errors[0].Path; }
        int i = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!["data"]!["nodes"]!.AsArray()
            .Select((n, k) => ((string)n!["id"]!, k)).Single(x => x.Item1 == "primevere_ruisseau").k;
        Assert.Equal($"$.data.nodes[{i}].seasons[0]", FirstCrafting(CraftingMutated(r => NodeOf(r, "primevere_ruisseau")["seasons"]![0] = "printemps")));
        Assert.Equal($"$.data.nodes[{i}].seasons", FirstCrafting(CraftingMutated(r => NodeOf(r, "primevere_ruisseau")["seasons"] = new JsonArray())));
        Assert.Equal($"$.data.nodes[{i}].seasons", FirstCrafting(CraftingMutated(r => NodeOf(r, "primevere_ruisseau")["seasons"] = new JsonArray("spring", "summer", "autumn", "winter"))));
        Assert.Equal($"$.data.nodes[{i}].seasons[1]", FirstCrafting(CraftingMutated(r => NodeOf(r, "primevere_ruisseau")["seasons"] = new JsonArray("spring", "spring"))));
        Assert.Equal("$.schemaVersion", FirstCrafting(CraftingMutated(r => r["schemaVersion"] = 3)));
        Assert.Equal("$.data.mistEchoes.presence.autumn", First(Mutated(r => Data(r)["mistEchoes"]!["presence"]!["autumn"] = 0.6)));
        Assert.Equal("$.data.mistEchoes.presence.winter", First(Mutated(r => Data(r)["mistEchoes"]!["presence"]!["winter"] = -0.1)));
        Assert.Equal("$.data.mistEchoes.presence.autumn", First(Mutated(r => Data(r)["mistEchoes"]!["presence"]!["summer"] = 0.4)));   // l'automne doit dépasser
        Assert.Equal("$.data.mistEchoes.presence.winter", First(Mutated(r => Data(r)["mistEchoes"]!["presence"]!.AsObject().Remove("winter"))));
        Assert.Equal("$.data.mistEchoes.presence.hiver", First(Mutated(r => Data(r)["mistEchoes"]!["presence"]!["hiver"] = 0.1)));   // champ inconnu, refusé à la lecture
        Assert.Equal("$.data.mistEchoes.maxPerVolume", First(Mutated(r => Data(r)["mistEchoes"]!["maxPerVolume"] = 0)));
        Assert.Equal("$.data.mistEchoes.maxPerVolume", First(Mutated(r => Data(r)["mistEchoes"]!["maxPerVolume"] = 9)));
        Assert.Equal("$.data.passages[0].requires", First(Mutated(r => Data(r)["passages"]![0]!["requires"] = "fire")));
        Assert.Equal("$.data.passages[0].id", First(Mutated(r => Data(r)["passages"]![0]!["id"] = "Glace")));
        Assert.Equal("$.data.locks[0].passage", First(Mutated(r => Data(r)["locks"]![0]!["passage"] = "pont")));
        Assert.Equal("$.data.locks[0].anchor", First(Mutated(r => Data(r)["locks"]![0]!["anchor"] = new JsonArray(99999, 0))));
        Assert.Equal("$.data.locks[1].id", First(Mutated(r => Data(r)["locks"]!.AsArray().Add(JsonNode.Parse("{\"id\":\"ile_des_saules_dormants\",\"passage\":\"glace_du_lac\"}")))));
        Assert.Equal("$.data.locks", First(Mutated(r => Data(r).Remove("locks"))));
        Assert.Equal("$.schemaVersion", First(Mutated(r => r["schemaVersion"] = 3)));   // V2.44.2 : version 2 courante, 1 encore lue
        Assert.Equal("$.kind", First(Mutated(r => r["kind"] = "crafting")));
        // Un point d'accroche dans l'aire est accepté (le jour où l'île sera sur la carte).
        SeasonLoadResult anchored = Mutated(r => Data(r)["locks"]![0]!["anchor"] = new JsonArray(-14, -62));
        Assert.True(anchored.IsValid, anchored.Report.ToString());
        Assert.NotNull(anchored.Book!.Lock("ile_des_saules_dormants").Anchor);
    }

    [Fact]
    public void Sans_date_fournie_rien_de_saisonnier_ne_s_ouvre_et_la_saison_reste_celle_du_jeu()
    {
        WorldSession w = Play(Zone(-4, -20));
        Assert.Null(w.CivilSeason);
        Assert.Equal(WorldCalendar.SeasonOf(w.Zone.Time.Day), w.Season);
        Assert.False(w.NodeInSeason("primevere_ruisseau"));
        Assert.False(w.IsNodeReady("primevere_ruisseau"));
        Assert.Equal(CraftRefusal.OutOfSeason, w.Gather("primevere_ruisseau", out CraftEvent? done));
        Assert.Null(done);
        Assert.Equal(0, w.Inventory.Count("item.primevere_argentee"));
        Assert.False(w.PassageOpen("glace_du_lac"));
        Assert.False(w.LockOpen("ile_des_saules_dormants"));
        Assert.Null(w.MistEchoPresence);
        Assert.Equal(CraftRefusal.Unknown, w.Gather("filon_nord", out _));
        Assert.Throws<ArgumentException>(() => w.PassageOpen("pont"));
        // Sans contenu saisonnier (seasons.json) : les nœuds de saison restent fermés sans date (ils sont dans les métiers), les passages
        // fermés même avec une date (option prudente).
        WorldSession bare = Play(Zone(-4, -20), seasons: false);
        Assert.False(bare.NodeInSeason("primevere_ruisseau"));
        Assert.Equal(CraftRefusal.OutOfSeason, bare.Gather("primevere_ruisseau", out _));
        Assert.False(bare.PassageOpen("glace_du_lac"));
        bare.SetCivilTime(Utc(2027, 1, 10));
        Assert.False(bare.PassageOpen("glace_du_lac"));
        Assert.False(bare.LockOpen("ile_des_saules_dormants"));
        bare.SetCivilTime(Utc(2027, 4, 10));
        Assert.Equal(CraftRefusal.None, bare.Gather("primevere_ruisseau", out _));
    }

    [Fact]
    public void Herbes_rares_au_printemps_champignons_en_automne()
    {
        WorldSession spring = Play(Zone(-4, -20));
        spring.SetCivilTime(Utc(2027, 4, 10));
        Assert.Equal("spring", spring.Season);
        Assert.Equal(Season.Spring, spring.CivilSeason!.Value.Season);
        Assert.True(spring.IsNodeReady("primevere_ruisseau"));
        Assert.Equal(CraftRefusal.None, spring.Gather("primevere_ruisseau", out CraftEvent? got));
        Assert.Equal("item.primevere_argentee", got!.Item);
        Assert.Equal(CraftRefusal.Depleted, spring.Gather("primevere_ruisseau", out _));
        Assert.Equal(CraftRefusal.OutOfSeason, spring.Gather("cepes_foret", out _));   // hors saison avant même la distance
        // Le 31 mai à 23 h 59 de Paris (21 h 59 UTC) : encore le printemps ; à minuit, l'été ferme les herbes rares.
        WorldSession edge = Play(Zone(-4, -20));
        edge.SetCivilTime(new DateTimeOffset(2027, 5, 31, 21, 59, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.True(edge.NodeInSeason("primevere_ruisseau"));
        edge.SetCivilTime(new DateTimeOffset(2027, 5, 31, 22, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.False(edge.NodeInSeason("primevere_ruisseau"));
        Assert.Equal(CraftRefusal.OutOfSeason, edge.Gather("primevere_ruisseau", out _));
        Assert.Equal("summer", edge.Season);

        WorldSession autumn = Play(Zone(-70, 42));
        autumn.SetCivilTime(Utc(2026, 10, 20));
        Assert.Equal(CraftRefusal.None, autumn.Gather("cepes_foret", out CraftEvent? cepe));
        Assert.Equal("item.cepe", cepe!.Item);
        Assert.Equal(CraftRefusal.OutOfSeason, autumn.Gather("primevere_ruisseau", out _));
        Assert.Equal(Fixed.Parse("0.4"), autumn.MistEchoPresence);
        autumn.SetCivilTime(Utc(2026, 12, 2));
        Assert.Equal(Fixed.Parse("0.15"), autumn.MistEchoPresence);
        Assert.Equal("winter", autumn.Season);
    }

    [Fact]
    public void Passage_du_lac_gele_et_verrou_de_l_ile_suivent_la_fenetre_de_glace()
    {
        WorldSession w = Play(Zone());
        (int, int, int, int, bool)[] cases =
        {
            (2026, 11, 30, 23, false),   // 1er décembre à 0 h de Paris : la glace se forme, pas praticable
            (2026, 12, 7, 22, false),    // 7 décembre, 23 h de Paris
            (2026, 12, 7, 23, true),     // 8 décembre, 0 h de Paris
            (2027, 1, 20, 12, true),
            (2027, 2, 28, 22, true),     // 28 février, 23 h de Paris
            (2027, 2, 28, 23, false),    // 1er mars, 0 h de Paris
            (2028, 2, 29, 12, true),     // année bissextile
            (2028, 3, 1, 12, false),
            (2026, 7, 14, 12, false),
        };
        foreach ((int y, int m, int d, int h, bool open) in cases)
        {
            w.SetCivilTime(Utc(y, m, d, h));
            Assert.Equal(open, w.PassageOpen("glace_du_lac"));
            Assert.Equal(open, w.LockOpen("ile_des_saules_dormants"));
        }
    }

    [Fact]
    public void Instant_hors_bornes_refuse_sans_rien_changer()
    {
        WorldSession w = Play(Zone());
        w.SetCivilTime(Utc(2027, 1, 10));
        SeasonState before = w.CivilSeason!.Value;
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetCivilTime(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetCivilTime(long.MaxValue));
        Assert.Equal(before, w.CivilSeason!.Value);
        Assert.Equal(Utc(2027, 1, 10), w.CivilUtcSeconds);
    }

    [Fact]
    public void Le_serveur_recoit_l_instant_de_son_hote()
    {
        ScenarioLoadResult duo = ScenarioJsonReader.ReadFile(Path.Combine(ScenariosDirectory, "coop", "duo_meute.json"), ShippedData);
        var room = new EdS.Net.CombatRoom(ShippedData, duo.Setup!);
        Assert.Null(room.CivilSeason);
        room.SetCivilTime(Utc(2026, 12, 8));
        Assert.Equal((Season.Winter, IcePhase.Walkable), (room.CivilSeason!.Value.Season, room.CivilSeason.Value.Ice));
        Assert.Throws<ArgumentOutOfRangeException>(() => room.SetCivilTime(-1));
        Assert.Equal(Utc(2026, 12, 8), room.CivilUtcSeconds);
        // L'hôte des salles lit l'horloge civile qu'on lui donne (le serveur dédié : l'horloge du système, en UTC), dès le démarrage.
        var hosted = new EdS.Net.CombatRoom(ShippedData, duo.Setup!);
        using var listener = new EdS.Net.TcpNetListener(System.Net.IPAddress.Loopback, 0);
        using (var host = new EdS.Net.RoomHost(hosted, listener, () => Utc(2027, 4, 2)))
        {
            host.Start();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (host.Read(r => r.CivilSeason) is null)
            {
                Assert.True(watch.Elapsed.TotalSeconds < 10, "instant attendu");
                Thread.Sleep(5);
            }
            Assert.Equal(Season.Spring, host.Read(r => r.CivilSeason)!.Value.Season);
        }
    }

    [Fact]
    public void Aucune_regle_de_combat_ne_lit_la_saison()
    {
        // La neige et l'hiver blanc sont un effet visuel : le moteur de combat et la simulation ignorent tout des saisons.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string simulation = Path.Combine(root, "src", "EdS.Simulation");
        if (!Directory.Exists(simulation)) return;   // sources absentes (exécution hors dépôt)
        foreach (string file in Directory.EnumerateFiles(simulation, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("SeasonCalendar", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SeasonState", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IcePhase", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Revue Codex de la PR #239 : en hiver, la surface montre la neige, le col et la glace ; un dessous (même zone, étage « dessous ») n'en montre
    /// aucune et l'ambiance y est celle d'une pièce (<c>Mood().Indoors</c>) : <c>Sheltered</c> compte, pas seulement <c>Indoor</c>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void L_affichage_d_hiver_suit_l_abri_d_un_dessous(bool underground)
    {
        string data = ScenarioTestData.CopyData((file, text) =>
        {
            if (!underground || file != "world/zones/vallons.json") return text;
            JsonObject root = JsonNode.Parse(text)!.AsObject();
            root["schemaVersion"] = ZoneValidator.CurrentSchemaVersion;
            root["data"]!["floor"] = "dessous";
            // Règle du dessous : aucune créature à moins de 8 m d'une porte. Cinq portes du village y manquent : elles sont déplacées d'autant.
            var moved = new Dictionary<string, double[]>
            {
                ["souvenir"] = new[] { -24.0, 17 }, ["porte_grange"] = new[] { -93.8, -50.4 }, ["porte_moulin_interieur"] = new[] { -8.0, 6 },
                ["porte_tour"] = new[] { 23.2, 70.9 }, ["porte_dortoir_abbaye"] = new[] { 1658.0, 97 },
            };
            // V2.51a : un dessous n'a pas d'Envers : la faille de l'Envers est retirée.
            var doorList = root["data"]!["doors"]!.AsArray();
            WithoutRifts(root);   // V2.52a : et celles des douze lieux
            _ = doorList;
            foreach (JsonNode? door in root["data"]!["doors"]!.AsArray())
                if (moved.TryGetValue((string)door!["id"]!, out double[]? at)) door["position"] = new JsonArray(at[0], at[1]);
            return root.ToJsonString();
        });
        string folder = Path.Combine(Path.GetTempPath(), "eds_abri_" + Guid.NewGuid().ToString("N"));
        try
        {
            string savePath = Path.Combine(folder, "partie", "sauvegarde.json");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
                PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression)));
            AdventureSession a = AdventureSession.Load(data, ScenariosDirectory, savePath);
            a.SetCivilTime(Utc(2027, 1, 20));
            a.Advance(0.05);
            Assert.Equal(underground, a.World.Definition!.Underground);
            SeasonLook look = a.SeasonShown;
            if (underground)
            {
                Assert.Equal(0f, look.Snow);
                Assert.Equal(0f, look.PassSnow);
                Assert.Equal(0f, look.Ice);
            }
            else
            {
                Assert.Equal(SeasonLook.WinterSnow, look.Snow);   // la surface, elle, a la neige (et pas de pièce)
                Assert.True(look.Snow > 0f);
            }
            Assert.Equal(underground, a.Mood().Indoors);
        }
        finally
        {
            Directory.Delete(data, true);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void L_aventure_recoit_la_date_de_l_hote_et_la_garde()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_saisons_" + Guid.NewGuid().ToString("N"));
        try
        {
            string savePath = Path.Combine(folder, "partie", "sauvegarde.json");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
                PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression)));
            AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, savePath);
            a.DrainMessages();
            a.Advance(0.05);
            Assert.Null(a.CivilSeason);
            Assert.False(a.Nodes().Single(n => n.Id == "primevere_ruisseau").Present);   // sans date : rien de saisonnier
            Assert.False(a.Nodes().Single(n => n.Id == "primevere_ruisseau").Ready);
            IReadOnlyList<string> order = a.Nodes().Select(n => n.Id).ToList();   // revue Codex de la PR #168 : toujours tous, dans le même ordre
            int Mapped() => a.MapMarkers().Count(m => m.Kind == MapMarkerKind.Node);
            Assert.Equal(a.Nodes().Count(n => n.Present), Mapped());   // revue Codex (2e tour) : la carte ne montre pas les nœuds hors saison
            int winterMap = Mapped();
            Assert.Contains(a.Nodes(), n => n.Id == "herbes_village");
            a.SetCivilTime(Utc(2027, 4, 2));
            Assert.Equal(Season.Spring, a.CivilSeason!.Value.Season);
            Assert.Equal(WardrobeSeason.Spring, a.WardrobeSeason);
            Assert.True(a.Nodes().Single(n => n.Id == "primevere_ruisseau").Present);
            Assert.True(a.Nodes().Single(n => n.Id == "primevere_ruisseau").Ready);
            Assert.False(a.Nodes().Single(n => n.Id == "cepes_foret").Present);
            Assert.Equal(order, a.Nodes().Select(n => n.Id));
            Assert.Equal(winterMap + 2, Mapped());   // au printemps : la primevère et la sauge apparaissent sur la carte
            // L'affichage crée les repères une fois, puis montre ceux qui sont prêts : un nœud qui entre dans sa saison apparaît.
            string markers = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "WorldMarkers.cs"));
            Assert.Contains("m.Body.SetActive(node.Ready);   // saisons", markers, StringComparison.Ordinal);
            Assert.Equal(SeasonLook.Of(a.CivilSeason!.Value).WithSnow(a.World.Seasons!.Snow, a.World.CivilDay), a.SeasonShown);   // V2.44.3 : neige du contenu
            a.SetCivilTime(Utc(2027, 1, 20));
            Assert.Equal(WardrobeSeason.Winter, a.WardrobeSeason);
            Assert.Equal(SeasonLook.WinterSnow, a.SeasonShown.Snow);
            Assert.True(a.World.PassageOpen("glace_du_lac"));
            // Une session nouvelle (halte au point sûr) garde la date fournie et le contenu saisonnier.
            WorldSession before = a.World;
            Assert.True(a.Rest());
            Assert.NotSame(before, a.World);
            Assert.Equal(Season.Winter, a.CivilSeason!.Value.Season);
            Assert.NotNull(a.World.Seasons);
            Assert.Throws<ArgumentOutOfRangeException>(() => a.SetCivilTime(-5));
            Assert.Equal(Utc(2027, 1, 20), a.World.CivilUtcSeconds);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Rendu_de_la_saison_teinte_neige_et_glace_avec_transition()
    {
        SeasonLook winter = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 1, 15)));
        Assert.Equal(SeasonLook.WinterSnow, winter.Snow);
        Assert.Equal(1f, winter.Ice);
        Assert.Equal(1f, winter.Blend);
        SeasonLook firstWinterDay = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2026, 12, 1)));
        Assert.Equal(0f, firstWinterDay.Snow);   // la neige vient avec la transition, pas d'un coup
        Assert.Equal(0f, firstWinterDay.Ice);
        SeasonLook midWinter = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2026, 12, 4), 720));
        Assert.InRange(midWinter.Snow, 0.01f, SeasonLook.WinterSnow - 0.01f);
        Assert.InRange(midWinter.Ice, 0.4f, 0.6f);
        SeasonLook thaw = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 3, 1)));
        Assert.Equal(SeasonLook.WinterSnow, thaw.Snow);   // la neige fond sur les 7 jours du printemps
        Assert.Equal(0f, thaw.Ice);
        Assert.Equal(0f, SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 3, 8))).Snow);
        SeasonLook autumn = SeasonLook.Steady((int)Season.Autumn);
        Assert.True(autumn.R > autumn.B);   // roux
        Assert.Equal(0f, autumn.Snow);
        // Croissance de la teinte bornée sur la transition (de l'été vers l'automne).
        float last = -1f;
        for (int day = 0; day < 8; day++)
        {
            SeasonLook l = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2026, 9, 1).AddDays(day)));
            Assert.InRange(l.Blend, 0f, 1f);
            Assert.True(l.Blend >= last);
            Assert.InRange(l.R, 0.45f, 0.61f);
            last = l.Blend;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => SeasonLook.Of(4, 0, 0f, 0f));
    }
}
