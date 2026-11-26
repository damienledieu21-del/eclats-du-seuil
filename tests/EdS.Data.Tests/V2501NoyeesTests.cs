using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.50.1 (PLAN_GRANDE_CARTE.md, ligne V2.50.1 ; donjon décidé au sujet 27) : les grottes noyées du lac, deux dessous sous les marais.
/// Les bassins des roseaux (pilleurs d'épaves, loups-échos noyés, capitaine des pilleurs), entrés par les roselières du lac ; la salle des épaves
/// (loups-échos noyés, pilleurs plongeurs, écho de l'épave), reliée par le chenal de marée (à sec à marée basse, plongée de 12 s à marée haute)
/// et par le siphon (toujours noyé, plongée de 25 s). Zones en schéma 16 (eau des portes). Marée et souffle : <see cref="Tides"/>.
/// </summary>
[Trait("Category", "World")]
public sealed class V2501NoyeesTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2501_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    public static readonly string[] Caves = { "zone.grottes_noyees_bassins", "zone.grottes_noyees_epaves" };

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition[]> All = new(() => Caves.Select(id =>
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, id + " : " + r.Report);
        return r.Zone!;
    }).ToArray());

    private static ZoneDefinition Cave(int i) => All.Value[i];

    private static IEnumerable<ZoneDefinition> AllZones() => new[] { Vallons.Value }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.vallons"));

    private static (double X, double Z) Min(ZoneDefinition z) => (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);

    private static (double X, double Z) Max(ZoneDefinition z) => (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);

    private static IEnumerable<CombatantSetup> Fighters(ZoneDefinition z) => z.Setup.Combatants.Concat(z.Chiefs.SelectMany(b => b.Forms));

    /// <summary>Ticks du monde (50 ms) d'une heure du monde : une journée de 3 h de jeu, soit 7 min 30 s par heure.</summary>
    private const long TicksPerHour = 9_000;

    /// <summary>Tick du monde à <paramref name="hour"/> h du jour 0 (le monde commence à 8 h).</summary>
    private static long At(int hour) => (hour - 8) * TicksPerHour;

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Deux_niveaux_sous_les_marais_relies_par_le_chenal_et_le_siphon()
    {
        ZoneDefinition[] g = All.Value;
        Assert.All(g, z => Assert.True(z.Underground && z.Sheltered && !z.Indoor && z.SchemaVersion == 16 && z.Region is null, z.Id));
        Assert.Equal(new[] { ("roselieres", "zone.vallons", "porte_grottes_noyees", DoorKind.Door), ("chenal", "zone.grottes_noyees_epaves", "chenal", DoorKind.Door),
                ("siphon", "zone.grottes_noyees_epaves", "siphon", DoorKind.Door) },
            g[0].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("chenal", "zone.grottes_noyees_bassins", "chenal", DoorKind.Door), ("siphon", "zone.grottes_noyees_bassins", "siphon", DoorKind.Door) },
            g[1].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        // Eau des portes : le chenal noyé à marée haute (plongée de 12 s), le siphon toujours (25 s) ; les roselières à sec.
        Assert.Null(g[0].DoorOf("roselieres")!.Water);
        foreach (ZoneDefinition z in g)
        {
            Assert.Equal((true, (long?)12_000), (z.DoorOf("chenal")!.Water!.Tide, z.DoorOf("chenal")!.Water!.DiveMs));
            Assert.Equal((false, (long?)25_000), (z.DoorOf("siphon")!.Water!.Tide, z.DoorOf("siphon")!.Water!.DiveMs));
        }
        Assert.True(ZoneValidator.CheckDoors(AllZones().ToArray()).IsValid);
        foreach (string id in Caves) Assert.Contains(V16TestData.Interiors, z => z.Id == id);
        Assert.Equal(new[] { "Grottes noyées : bassins des roseaux", "Grottes noyées : salle des épaves" }, g.Select(z => z.Name));
        // Sous le lac (grand lac au niveau 11) : de 11 à 14.
        Assert.Equal(new[] { 11, 12, 13, 14 }, g.SelectMany(z => z.Places).Select(p => p.MinLevel));
        Assert.Equal(new[] { "sous_les_roseaux", "au_bout_du_chenal" }, g.Select(z => z.SafePoints.Single().Id));
        Assert.All(g, z => Assert.Equal(z.Entry.Id, z.SafePoints[0].Id));
        Assert.DoesNotContain(g.SelectMany(z => z.Doors), AdventureSession.IsSecretDoor);
    }

    [Fact]
    public void Kit_de_grotte_noyee_dans_les_donnees()
    {
        // Pièces du kit (rendu Unity au V2.50.2) reconnues au nom des obstacles ; bassins, algues et coffres bas (on tire par-dessus).
        var o1 = Cave(0).Setup.Space!.Obstacles;
        var o2 = Cave(1).Setup.Space!.Obstacles;
        foreach (string part in new[] { "mur_noyee", "bassin_noyee", "rocher_noyee", "algues_noyee", "epave_noyee", "coffre_noyee", "voute_noyee" })
            Assert.Contains(o1, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        foreach (string part in new[] { "mur_noyee", "bassin_noyee", "rocher_noyee", "algues_noyee", "epave_noyee", "coffre_noyee", "voute_noyee" })
            Assert.Contains(o2, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        var all = o1.Concat(o2).ToList();
        foreach (string low in new[] { "bassin_noyee", "algues_noyee", "coffre_noyee" })
            Assert.All(all.Where(o => o.Id.StartsWith(low, StringComparison.Ordinal)), o => Assert.True(o.Low, o.Id));
        foreach (string high in new[] { "mur_noyee", "rocher_noyee", "epave_noyee", "voute_noyee" })
            Assert.All(all.Where(o => o.Id.StartsWith(high, StringComparison.Ordinal)), o => Assert.False(o.Low, o.Id));
        // Les scénarios des chefs reprennent l'espace de leur niveau, obstacle pour obstacle.
        foreach (ZoneDefinition z in All.Value)
            foreach (Boss chief in z.Chiefs)
                Assert.True(chief.Forms.All(f => f.Placement != null), chief.Id);
    }

    [Fact]
    public void Entree_dans_les_roselieres_des_marais_a_pied()
    {
        ZoneDefinition vallons = Vallons.Value;
        Door door = vallons.DoorOf("porte_grottes_noyees")!;
        Assert.Equal(("zone.grottes_noyees_bassins", "roselieres", DoorKind.Door, "Grottes noyées du lac", (DoorWater?)null),
            (door.To, door.ToDoor, door.Kind, door.Name, door.Water));
        (double x, double z) = P(door.Position);
        // Dans les rives du lac, à 6 m au plus d'une haie de roseaux du marais (plan des marais : entrée dans les roselières).
        Place rives = vallons.Places.Single(p => p.Id == "rives_du_lac");
        Assert.InRange(x, P(rives.Min).X, P(rives.Max).X);
        Assert.InRange(z, P(rives.Min).Z, P(rives.Max).Z);
        Assert.Contains(vallons.Setup.Space!.Obstacles, o => o.Id.StartsWith("haie_roseliere_marais", StringComparison.Ordinal)
            && GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) <= 6);
        Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) < 0.5);
        // Des pontons du lac à la porte, à pied (par le pont sur la rivière des champs, qui sépare les pontons du marais de l'est).
        Assert.True(Walks((-100, -880), (900, -200), SafePoint("pontons_du_lac"), (x, z), 2));
    }

    [Fact]
    public void De_l_entree_a_chaque_creature_chef_et_porte_a_pied()
    {
        foreach (ZoneDefinition z in All.Value)
        {
            var flood = V228cSurfaceTests.Flood(z.Setup.Space!.Obstacles, Min(z), Max(z), P(z.SafePoints[0].Position), 0.5);
            var spots = Fighters(z).Select(c => (c.Id, P(c.Placement!.Position))).Concat(z.Doors.Select(d => (d.Id, P(d.Position))));
            foreach ((string id, (double X, double Z) at) in spots)
                Assert.True(V228cSurfaceTests.Reaches(flood.Reached, Min(z), at, 0.5), z.Id + " : " + id);
        }
    }

    [Fact]
    public void Chefs_dans_le_chemin_standard_et_recompenses()
    {
        Boss capitaine = Assert.Single(Cave(0).Chiefs), echo = Assert.Single(Cave(1).Chiefs);
        Assert.Null(Cave(0).Boss);
        Assert.Null(Cave(1).Boss);
        Assert.Equal(("capitaine_des_pilleurs", "fact.capitaine_des_pilleurs.vaincu", "loot.chef_des_bandits"), (capitaine.Id, capitaine.Fact, capitaine.Loot));
        Assert.Equal(new[] { "bandit_chef_noyees", "bandit_chef_noyees_accule" }, capitaine.Forms.Select(f => f.Id));
        Assert.Equal(new[] { (Tides.ShellItem, 1) }, capitaine.Rewards);
        Assert.Equal(("echo_de_l_epave", "fact.echo_de_l_epave.vaincu", "loot.echo"), (echo.Id, echo.Fact, echo.Loot));
        Assert.Equal(new[] { "loup_echo_epave", "loup_echo_epave_dechaine" }, echo.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.amulette_des_noyes", 1) }, echo.Rewards);

        ItemDefinition coquille = Items.Item(Tides.ShellItem);
        Assert.Equal((ItemType.Material, ItemRarity.Rare, "Coquille de souffle"), (coquille.Type, coquille.Rarity, coquille.Name));
        ItemDefinition amulette = Items.Item("item.amulette_des_noyes");
        Assert.Equal((ItemType.Equipment, "neck", 13, ItemRarity.Epic, true, "Amulette des noyés"), (amulette.Type, amulette.Slot, amulette.RequiredLevel, amulette.Rarity, amulette.Named, amulette.Name));
        Assert.Equal((45, 80, 15), (amulette.Bonus.MagicResistance, amulette.Bonus.MaxHealth, amulette.Bonus.StatusResistance));
        Assert.Equal(340, V11TestData.ShippedMerchants.Values["item.amulette_des_noyes"]);
        // La coquille ne s'achète ni ne se vend : le souffle ne se perd pas chez un marchand.
        Assert.False(V11TestData.ShippedMerchants.Values.ContainsKey(Tides.ShellItem));
        var facts = Vallons.Value.Chiefs.Select(c => c.Fact).Concat(V16TestData.Interiors.SelectMany(z => z.Chiefs).Select(c => c.Fact)).ToList();
        Assert.Equal(facts.Count, facts.Distinct().Count());
    }

    [Fact]
    public void Creatures_et_chefs_a_8_m_hors_d_aggro_et_hors_de_la_garde_des_portes()
    {
        // Règles des dessous (V2.45.1, revues Codex des PR #245 et #248, relecture de la PR #250) : 8 m au moins de chaque porte, hors de portée
        // d'aggro de bord à bord (rayon de la créature et du plus gros personnage, 0,5 m de marge) et au-delà de la garde des portes (12 m).
        double hero = Math.Max(ZoneValidator.CharacterRadius.Raw / 1e4, 0.9);
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        int pairs = 0;
        foreach (ZoneDefinition z in All.Value)
            foreach (Door d in z.Doors)
                foreach (CombatantSetup c in Fighters(z))
                {
                    (double x, double zz) = P(c.Placement!.Position);
                    (double dx, double dz) = P(d.Position);
                    double centre = Math.Sqrt((x - dx) * (x - dx) + (zz - dz) * (zz - dz));
                    Assert.True(centre >= 8, $"{z.Id} / {d.Id} : {c.Id}");
                    Assert.True(centre > guard, $"{z.Id} / {d.Id} : {c.Id} à {centre:0.##} m");
                    Fixed aggro = Assert.IsType<AssailantSetup>(c).Behavior!.AggroRange!.Value;
                    double edge = centre - c.Placement.Radius.Raw / 1e4 - hero;
                    Assert.True(edge > aggro.Raw / 1e4 + 0.5, $"{z.Id} / {d.Id} : {c.Id} à {edge:0.###} m de bord à bord");
                    pairs++;
                }
        Assert.Equal((5 + 2) * 3 + (5 + 2) * 2, pairs);   // bassins : 5 créatures, 2 formes, 3 portes ; épaves : 5 créatures, 2 formes, 2 portes
    }

    [Fact]
    public void Aucun_ennemi_des_Vallons_a_moins_de_12_m_de_l_entree()
    {
        ZoneDefinition vallons = Vallons.Value;
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        (double dx, double dz) = P(vallons.DoorOf("porte_grottes_noyees")!.Position);
        foreach (CombatantSetup c in Fighters(vallons).Where(c => c is AssailantSetup && c.Placement != null))
        {
            (double x, double z) = P(c.Placement!.Position);
            Assert.True(Math.Sqrt((x - dx) * (x - dx) + (z - dz) * (z - dz)) > guard, c.Id);
        }
    }

    private ZoneLoadResult WithScenario(string zone, string scenario, Action<JsonObject> edit, Action<JsonObject>? editZone = null)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "grottes_noyees_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(s);
        File.WriteAllText(path, s.ToJsonString());
        JsonObject root = ZoneJson(zone);
        editZone?.Invoke(root);
        return Read(root, dir);
    }

    private static JsonObject Combatant(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    [Theory]
    [InlineData(-18.0, true)]       // un pilleur à 8 m pile du siphon (−26 ; 41) : accepté
    [InlineData(-18.0001, false)]   // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_au_siphon(double x, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.grottes_noyees_bassins", "grottes_noyees_bassins", s => Combatant(s, "bandit_noyees1")["position"] = new JsonArray(x, 41.0));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.Contains("porte « siphon »", StringComparison.Ordinal));
    }

    /// <summary>La seconde forme de l'écho de l'épave aussi : à 8 m pile du siphon (26 ; 20), acceptée ; juste en deçà, refusée.</summary>
    [Theory]
    [InlineData(18.0, true)]
    [InlineData(18.0001, false)]
    public void Frontiere_huit_metres_pour_la_seconde_forme_de_l_echo(double x, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.grottes_noyees_epaves", "grottes_noyees_epaves_echo", s => Combatant(s, "loup_echo_epave_dechaine")["position"] = new JsonArray(x, 20.0));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("porte « siphon »", StringComparison.Ordinal));
        else
            Assert.True(r.IsValid, r.Report.ToString());
    }

    [Fact]
    public void Grottes_marquees_interieur_refusees()
    {
        JsonObject zone = ZoneJson("zone.grottes_noyees_bassins");
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    /// <summary>Chaque chef ne donne sa récompense qu'à la première victoire ; la seconde, après relecture de la sauvegarde, ne donne rien.</summary>
    [Theory]
    [InlineData("zone.grottes_noyees_bassins", "grottes_noyees_bassins_capitaine", "bandit_chef_noyees,bandit_chef_noyees_accule", "capitaine_des_pilleurs", "fact.capitaine_des_pilleurs.vaincu", "item.coquille_de_souffle:1", -3.0, 8.0)]
    [InlineData("zone.grottes_noyees_epaves", "grottes_noyees_epaves_echo", "loup_echo_epave,loup_echo_epave_dechaine", "echo_de_l_epave", "fact.echo_de_l_epave.vaincu", "item.amulette_des_noyes:1", -21.0, 6.5)]
    public void Chef_recompense_a_la_premiere_victoire_seulement(string zoneId, string scenario, string formList, string chief, string fact, string rewardList, double x0, double z0)
    {
        string dir = Path.Combine(_folder, "scenarios_" + chief);
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "grottes_noyees_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject g = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        string[] forms = formList.Split(',');
        for (int i = 0; i < forms.Length; i++)
        {
            JsonObject f = Combatant(g, forms[i]);
            f["health"] = 10;
            f["intervalMs"] = 60_000;
            foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
            f["position"] = new JsonArray(x0 + 3 * i, z0);   // près de l'arrivée, à plus de 8 m des portes
        }
        File.WriteAllText(path, g.ToJsonString());
        JsonObject root = ZoneJson(zoneId);
        root["data"]!["populations"] = new JsonArray();
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        var rewards = rewardList.Split(',').Select(r => (r.Split(':')[0], int.Parse(r.Split(':')[1]))).ToArray();
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 13));
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, Items);
        List<SessionStep> first = Fight(w, forms[0]);
        Assert.Contains(first.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Equal(rewards, first.SelectMany(s => s.Rewards));
        string key = zone.VictoryKeyOf(zone.Chiefs[0]);
        Assert.Equal(zone.Id + "/boss/" + chief, key);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains(key, saved.Consumed);
        Assert.Contains(fact, SaveJsonReader.Write(w.Save()), StringComparison.Ordinal);

        var again = new WorldSession(saved, zone, ShippedData, Progression, Items);
        List<SessionStep> second = Fight(again, forms[0]);
        Assert.Contains(second.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Empty(second.SelectMany(s => s.Rewards));
        Assert.Equal(rewards[0].Item2, again.Inventory.Count(rewards[0].Item1));
    }

    private static List<SessionStep> Fight(WorldSession w, string prefix, int max = 6000)
    {
        var steps = new List<SessionStep>();
        for (int i = 0; i < max && !steps.Any(s => s.Boss.Any(b => b.Kind == BossEventKind.Defeated)); i++)
        {
            string? form = w.BossForms.FirstOrDefault(f => f.StartsWith(prefix, StringComparison.Ordinal));
            if (i % 20 == 0 && form != null && w.Zone.Engine.CombatantIds.Contains(form))
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
        }
        for (int i = 0; i < 5; i++) steps.Add(w.Step());
        return steps;
    }

    /// <summary>
    /// Apparences : les pilleurs d'épaves et leur capitaine prennent le personnage « bandit » (forme humaine, sans rendu d'écho) ; les loups-échos
    /// noyés et l'écho de l'épave, le modèle de loup avec le rendu d'écho de la nuit. Chaque nom affiché est écrit (jamais un repli).
    /// </summary>
    [Fact]
    public void Apparences_et_noms_des_creatures_des_grottes()
    {
        var ids = All.Value.SelectMany(Fighters).Select(c => c.Id).ToList();
        var bandits = ids.Where(id => id.StartsWith("bandit_", StringComparison.Ordinal)).ToList();
        var echoes = ids.Where(id => id.StartsWith("loup_echo", StringComparison.Ordinal)).ToList();
        Assert.Equal(7, bandits.Count);
        Assert.Equal(7, echoes.Count);
        Assert.Equal(ids.Count, bandits.Count + echoes.Count);
        foreach (string id in bandits)
        {
            Assert.Equal(StyleSlot.Character, StyleRules.ForCombatant(id, false));
            Assert.Equal("bandit", StyleRules.CharacterOf(id));
            Assert.Equal(EchoKind.None, EchoLooks.Of(id));
        }
        foreach (string id in echoes)
        {
            Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant(id, false));
            Assert.Equal(EchoKind.Night, EchoLooks.Of(id));
            Assert.Equal(0, StyleRules.Cubs(id));
        }
        var shown = ids.Concat(new[] { "bandit_noyees2_v3", "loup_echo_noyees3_v2" }).Select(DisplayNames.Of).ToList();
        Assert.Equal(new[] { "Pilleur d'épaves", "Pilleur d'épaves", "Archer des pilleurs d'épaves", "Loup-écho noyé", "Loup-écho noyé", "Capitaine des pilleurs", "Capitaine acculé",
                "Loup-écho noyé", "Loup-écho noyé", "Loup-écho noyé", "Pilleur d'épaves", "Archer des pilleurs d'épaves", "Écho de l'épave", "Écho de l'épave déchaîné",
                "Pilleur d'épaves", "Loup-écho noyé" },
            shown);
        Assert.Equal("Coup de harpon", DisplayNames.Attack("coup_de_harpon"));
        Assert.Equal("Lame de fond", DisplayNames.Attack("lame_de_fond"));
        Assert.Equal(new[] { "coup_de_harpon" }, Cave(0).Chiefs[0].Forms[1].Attacks.Select(a => a.Id).Where(a => a == "coup_de_harpon"));
        Assert.Contains(Cave(1).Chiefs[0].Forms[1].Attacks, a => a.Id == "lame_de_fond");
        // Les créatures d'avant ne changent pas.
        Assert.Equal("Passeur aux abois", DisplayNames.Of("bandit_chef_egout_aux_abois"));
        Assert.Equal("Loup-écho des égouts", DisplayNames.Of("loup_echo_egout2"));
    }

    [Fact]
    public void Pilleurs_et_echos_en_treve_a_chaque_niveau()
    {
        // Revue Codex de la PR #245 : deux équipes ennemies d'une même zone demandent une trêve.
        foreach (ZoneDefinition z in All.Value)
        {
            Assert.Equal(new[] { "bandits", "echos" }, Fighters(z).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
            Assert.Equal(new[] { ("echos", "bandits") }, z.Setup.Truces.Select(t => (t.A, t.B)));
        }
    }

    [Fact]
    public void Dans_les_grottes_ni_cheval_ni_voyage_rapide_vers_elles()
    {
        foreach (ZoneDefinition z in All.Value)
        {
            var w = new WorldSession(new WorldSave(z.Id, 0, 0, Array.Empty<string>(),
                PlayerCharacters.Create("bastion", "class.bastion", Progression).WithMount(PlayerCharacters.Horse)), z, ShippedData, Progression, Items);
            w.Step();
            Assert.False(w.CanCallMount);
            Assert.True(w.IsDiscovered(z, z.SafePoints[0].Id));
            Assert.Equal(TravelRefusal.UnknownPlace, w.CanTravel(z, z.SafePoints[0].Id));
        }
    }

    [Theory]
    [InlineData(12.0, true)]      // un ennemi à 12 m pile : la porte reste gardée (CanUseDoor refuse)
    [InlineData(12.001, false)]   // juste au-delà : la porte se passe
    public void Frontiere_de_la_garde_des_portes_a_douze_metres(double distance, bool guarded)
    {
        Assert.Equal(12.0, AdventureSession.RestSafety.Raw / 1e4);
        ZoneLoadResult loaded = WithScenario("zone.grottes_noyees_bassins", "grottes_noyees_bassins",
            s => Combatant(s, "loup_echo_noyees1")["position"] = new JsonArray(Math.Round(0.6 * distance, 4), Math.Round(-1.0 + 0.8 * distance, 4)),   // en biais, à côté du bassin
            root => root["data"]!["populations"] = new JsonArray(root["data"]!["populations"]![3]!.DeepClone()));
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var w = new WorldSession(new WorldSave(loaded.Zone!.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero()), loaded.Zone, ShippedData, Progression, Items);
        w.Step();
        Assert.Equal((0.0, -1.0), P(w.Zone.Engine.View(w.CombatantId).Position!.Value));   // sous les roseaux
        Assert.Contains("loup_echo_noyees1", w.Zone.Engine.CombatantIds);
        Assert.Equal(guarded, CombatDisplay.NearbyEnemies(w.Zone.Engine, w.CombatantId, null, AdventureSession.RestSafety).Any());
    }

    // --- Marée et plongée (calculs purs) ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, TideLevel.Low, 180)]
    [InlineData(179, TideLevel.Low, 1)]
    [InlineData(180, TideLevel.High, 180)]
    [InlineData(359, TideLevel.High, 1)]
    [InlineData(360, TideLevel.Low, 180)]
    [InlineData(480, TideLevel.Low, 60)]      // 8 h, début du monde : marée basse, elle monte à 9 h
    [InlineData(540, TideLevel.High, 180)]
    [InlineData(1260, TideLevel.High, 180)]   // 21 h
    [InlineData(1439, TideLevel.High, 1)]
    public void Maree_de_trois_heures_tiree_de_l_heure_du_monde(int minute, TideLevel level, int turnsIn)
    {
        var time = new WorldTime(3, minute);
        Assert.Equal(level, Tides.At(time, frozen: false));
        Assert.Equal(turnsIn, Tides.MinutesToTurn(time, frozen: false));
        // Lac gelé : la glace retient l'eau, la marée reste basse et ne tourne plus.
        Assert.Equal(TideLevel.Low, Tides.At(time, frozen: true));
        Assert.Null(Tides.MinutesToTurn(time, frozen: true));
    }

    [Fact]
    public void Maree_identique_chaque_jour_et_moitie_basse()
    {
        int low = 0;
        for (int m = 0; m < WorldClock.MinutesPerDay; m++)
        {
            TideLevel t = Tides.At(new WorldTime(0, m), false);
            Assert.Equal(t, Tides.At(new WorldTime(41, m), false));
            if (t == TideLevel.Low) low++;
        }
        Assert.Equal(720, low);
    }

    [Fact]
    public void Souffle_de_base_et_coquille()
    {
        Assert.Equal(15_000, Tides.Breath(0));
        Assert.Equal(30_000, Tides.Breath(1));
        Assert.Equal(30_000, Tides.Breath(3));   // une seule coquille compte
        Assert.Throws<ArgumentOutOfRangeException>(() => Tides.Breath(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tides.Check(null, TideLevel.Low, -1));
    }

    [Fact]
    public void Porte_d_eau_selon_la_maree_et_le_souffle()
    {
        DoorWater chenal = Cave(0).DoorOf("chenal")!.Water!, siphon = Cave(0).DoorOf("siphon")!.Water!;
        DoorWater gue = TideOnly();
        Assert.Equal(WaterPass.Dry, Tides.Check(null, TideLevel.High, 0));
        Assert.Equal(WaterPass.Dry, Tides.Check(chenal, TideLevel.Low, 0));
        Assert.Equal(WaterPass.Dive, Tides.Check(chenal, TideLevel.High, 15_000));
        Assert.Equal(WaterPass.Dive, Tides.Check(chenal, TideLevel.High, 12_000));   // une plongée égale au souffle passe
        Assert.Equal(WaterPass.NoBreath, Tides.Check(chenal, TideLevel.High, 11_999));
        Assert.Equal(WaterPass.NoBreath, Tides.Check(siphon, TideLevel.Low, 15_000));   // toujours noyé, même à marée basse
        Assert.Equal(WaterPass.Dive, Tides.Check(siphon, TideLevel.Low, 30_000));
        Assert.Equal(WaterPass.Dry, Tides.Check(gue, TideLevel.Low, 0));
        Assert.Equal(WaterPass.Drowned, Tides.Check(gue, TideLevel.High, 1_000_000));   // sans plongée : refusé quel que soit le souffle
    }

    /// <summary>Passage de marée sans plongée (gué noyé à marée haute), lu par le validateur.</summary>
    private static DoorWater TideOnly()
    {
        JsonObject root = ZoneJson("zone.grottes_noyees_bassins");
        root["data"]!["doors"]![1]!["water"] = new JsonObject { ["tide"] = true };
        ZoneLoadResult r = Read(root);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!.DoorOf("chenal")!.Water!;
    }

    [Fact]
    public void Jamais_enferme_dans_la_salle_des_epaves()
    {
        // Le chenal se passe toujours avec le souffle de base (à pied à marée basse, plongée de 12 s à marée haute) : on ne reste jamais
        // enfermé sous le lac, même sans coquille. Le siphon, lui, demande la coquille.
        foreach (TideLevel tide in new[] { TideLevel.Low, TideLevel.High })
        {
            Assert.Contains(Cave(1).Doors, d => Tides.Check(d.Water, tide, Tides.BaseBreathMs) != WaterPass.NoBreath && Tides.Check(d.Water, tide, Tides.BaseBreathMs) != WaterPass.Drowned);
            Assert.Equal(WaterPass.NoBreath, Tides.Check(Cave(1).DoorOf("siphon")!.Water, tide, Tides.BaseBreathMs));
            Assert.Equal(WaterPass.Dive, Tides.Check(Cave(1).DoorOf("siphon")!.Water, tide, Tides.Breath(1)));
        }
    }

    // --- Marée et plongée dans la session ------------------------------------------------------------------------------------

    private static WorldSession Session(long worldTick, bool shell)
    {
        SavedCharacter hero = V11TestData.Hero();
        if (shell) hero = hero.WithItems(new[] { new ItemStack(Tides.ShellItem, 1) }, new Dictionary<string, string>());
        return new WorldSession(new WorldSave(Cave(0).Id, worldTick, 0, new[] { PlayerCharacters.PlacesKey }, hero), Cave(0), ShippedData, Progression, Items);
    }

    [Fact]
    public void Maree_de_la_session_tiree_du_tick_du_monde()
    {
        WorldSession low = Session(At(8), false);
        Assert.Equal((TideLevel.Low, (int?)60, false), (low.Tide, low.TideTurnsIn, low.LakeFrozen));
        Assert.Equal(WaterPass.Dry, low.DoorWaterPass("chenal"));
        Assert.Equal(WaterPass.NoBreath, low.DoorWaterPass("siphon"));
        Assert.Equal(WaterPass.Dry, low.DoorWaterPass("roselieres"));
        Assert.Equal(WaterPass.Dry, low.DoorWaterPass("inconnue"));
        WorldSession high = Session(At(9), false);
        Assert.Equal((TideLevel.High, (int?)180), (high.Tide, high.TideTurnsIn));
        Assert.Equal(WaterPass.Dive, high.DoorWaterPass("chenal"));
        Assert.Equal(15_000, high.BreathMs);
        WorldSession shell = Session(At(9), true);
        Assert.Equal(30_000, shell.BreathMs);
        Assert.Equal(WaterPass.Dive, shell.DoorWaterPass("siphon"));
        // Le même tick donne toujours la même marée ; la marée tourne en jouant.
        Assert.Equal(TideLevel.High, Session(At(9), false).Tide);
        WorldSession turning = Session(At(9) - 1, false);
        Assert.Equal(TideLevel.Low, turning.Tide);
        turning.Step();
        Assert.Equal(TideLevel.High, turning.Tide);
    }

    [Fact]
    public void Lac_gele_l_hiver_maree_basse()
    {
        WorldSession w = Session(At(9), false);
        w.UseSeasons(SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), Vallons.Value).Book);
        Assert.Equal(TideLevel.High, w.Tide);   // sans date : le lac n'est pas gelé
        w.SetCivilTime(new DateTimeOffset(2027, 1, 20, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.True(w.LakeFrozen);
        Assert.Equal((TideLevel.Low, (int?)null), (w.Tide, w.TideTurnsIn));
        Assert.Equal(WaterPass.Dry, w.DoorWaterPass("chenal"));
        Assert.Equal(WaterPass.NoBreath, w.DoorWaterPass("siphon"));   // le siphon reste noyé sous la glace
        w.SetCivilTime(new DateTimeOffset(2027, 7, 20, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.False(w.LakeFrozen);
        Assert.Equal(TideLevel.High, w.Tide);
    }

    // --- Dans l'aventure : refus atomique, plongée -----------------------------------------------------------------------------

    private (string Data, string Scenarios) CopyData((double X, double Z) safePoint)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (Scenarios, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        string p = ZoneJsonReader.PathIn(data, "zone.grottes_noyees_bassins");
        JsonObject root = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(safePoint.X, safePoint.Z);
        File.WriteAllText(p, root.ToJsonString());
        return (data, scenarios);
    }

    [Fact]
    public void Siphon_refuse_sans_souffle_sans_rien_changer_puis_plonge_avec_la_coquille()
    {
        (string data, string scenarios) = CopyData((-26, 39.5));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave(Cave(0).Id, At(9), 0, Array.Empty<string>(), V11TestData.Hero()));
        byte[] before = File.ReadAllBytes(save);
        AdventureSession a = V11TestData.Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.Equal("siphon", a.DoorInReach!.Id);
        Assert.True(a.CanUseDoor);
        Assert.Equal("Passage noyé (Siphon de l'épave) : 25 s sous l'eau, votre souffle n'en tient que 15", a.DoorHint);
        Assert.Equal("Marée haute : elle descend dans 3 h 00", a.TideHint);
        Assert.False(a.UseDoor());
        Assert.Equal(Cave(0).Id, a.World.Definition!.Id);
        Assert.Equal(before, File.ReadAllBytes(save));
        Assert.Contains("Trop long pour votre souffle : 25 s sous l'eau, vous n'en tenez que 15. Vous restez sur le bord.", a.DrainMessages());

        SavedCharacter withShell = V11TestData.Hero().WithItems(new[] { new ItemStack(Tides.ShellItem, 1) }, new Dictionary<string, string>());
        SaveStore.Write(save, new WorldSave(Cave(0).Id, At(9), 0, Array.Empty<string>(), withShell));
        AdventureSession b = V11TestData.Adventure(save, data, scenarios);
        b.DrainMessages();
        Assert.Equal("F : plonger (Siphon de l'épave, 25 s sous l'eau ; souffle : 30 s)", b.DoorHint);
        bool dove = b.UseDoor();
        var said = b.DrainMessages();
        Assert.True(dove, string.Join(" | ", said));
        Assert.Equal("zone.grottes_noyees_epaves", b.World.Definition!.Id);
        Assert.Contains("Vous plongez et ressortez de l'autre côté après 25 s sous l'eau.", said);
        Assert.Equal("zone.grottes_noyees_epaves", SaveStore.Read(save).Save!.Zone);
    }

    [Fact]
    public void Chenal_a_pied_a_maree_basse_en_plongee_a_maree_haute()
    {
        (string data, string scenarios) = CopyData((26, 39.5));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        foreach ((long tick, string hint, string? said) in new[]
        {
            (At(8), "F : passer la porte (Salle des épaves)", (string?)null),
            (At(9), "F : plonger (Salle des épaves, 12 s sous l'eau ; souffle : 15 s)", "Vous plongez et ressortez de l'autre côté après 12 s sous l'eau."),
        })
        {
            SaveStore.Write(save, new WorldSave(Cave(0).Id, tick, 0, Array.Empty<string>(), V11TestData.Hero()));
            AdventureSession a = V11TestData.Adventure(save, data, scenarios);
            a.DrainMessages();
            Assert.Equal(hint, a.DoorHint);
            bool passed = a.UseDoor();
            var messages = a.DrainMessages();
            Assert.True(passed, string.Join(" | ", messages));
            Assert.Equal("zone.grottes_noyees_epaves", a.World.Definition!.Id);
            if (said != null) Assert.Contains(said, messages);
            else Assert.DoesNotContain(messages, m => m.StartsWith("Vous plongez", StringComparison.Ordinal));
        }
    }

    // --- Validateur : eau des portes (schéma 16) ----------------------------------------------------------------------------

    [Theory]
    [InlineData(999L)]
    [InlineData(121_000L)]
    [InlineData(12_500L)]
    [InlineData(0L)]
    public void Plongee_hors_bornes_refusee(long dive)
    {
        JsonObject root = ZoneJson("zone.grottes_noyees_bassins");
        root["data"]!["doors"]![1]!["water"]!["diveMs"] = dive;
        ZoneLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.doors[1].water.diveMs" && e.Code == ValidationCode.OutOfRange);
    }

    [Theory]
    [InlineData(1_000L, true)]
    [InlineData(120_000L, true)]
    public void Plongee_aux_bornes_acceptee(long dive, bool valid)
    {
        JsonObject root = ZoneJson("zone.grottes_noyees_bassins");
        root["data"]!["doors"]![1]!["water"]!["diveMs"] = dive;
        Assert.Equal(valid, Read(root).IsValid);
    }

    [Fact]
    public void Passage_toujours_noye_sans_plongee_refuse()
    {
        JsonObject root = ZoneJson("zone.grottes_noyees_bassins");
        root["data"]!["doors"]![2]!["water"] = new JsonObject();
        ZoneLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.doors[2].water.diveMs" && e.Code == ValidationCode.MissingField);
    }

    [Fact]
    public void Eau_d_une_porte_refusee_en_schema_15_et_champ_inconnu_refuse()
    {
        JsonObject root = ZoneJson("zone.grottes_noyees_bassins");
        root["schemaVersion"] = 15;
        ZoneLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.doors[1].water");
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.doors[2].water");
        // Sans eau, le schéma 15 reste lu.
        foreach (int i in new[] { 1, 2 }) root["data"]!["doors"]![i]!.AsObject().Remove("water");
        Assert.True(Read(root).IsValid);
        JsonObject odd = ZoneJson("zone.grottes_noyees_bassins");
        odd["data"]!["doors"]![1]!["water"]!["depth"] = 3;
        Assert.False(Read(odd).IsValid);
    }

    [Fact]
    public void Portes_qui_se_repondent_avec_une_autre_eau_refusees()
    {
        foreach (JsonObject water in new[] { new JsonObject { ["tide"] = true, ["diveMs"] = 13_000 }, new JsonObject { ["diveMs"] = 12_000 } })
        {
            JsonObject root = ZoneJson("zone.grottes_noyees_epaves");
            root["data"]!["doors"]![0]!["water"] = water;
            ZoneLoadResult r = Read(root);
            Assert.True(r.IsValid, r.Report.ToString());
            ValidationReport report = ZoneValidator.CheckDoors(AllZones().Where(z => z.Id != "zone.grottes_noyees_epaves").Append(r.Zone!).ToArray());
            Assert.False(report.IsValid);
            Assert.Contains(report.Errors, e => e.Path.EndsWith(".water", StringComparison.Ordinal));
        }
        // À sec d'un côté, noyée de l'autre : refusé aussi.
        JsonObject dry = ZoneJson("zone.grottes_noyees_epaves");
        dry["data"]!["doors"]![0]!.AsObject().Remove("water");
        ZoneLoadResult d = Read(dry);
        Assert.True(d.IsValid, d.Report.ToString());
        Assert.False(ZoneValidator.CheckDoors(AllZones().Where(z => z.Id != "zone.grottes_noyees_epaves").Append(d.Zone!).ToArray()).IsValid);
    }
}
