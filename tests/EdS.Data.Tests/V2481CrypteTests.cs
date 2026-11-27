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
/// Chantier V2.48.1 (PLAN_GRANDE_CARTE.md, ligne V2.48) : la crypte-donjon sous l'abbaye (deux niveaux : galerie des gisants et ossuaire, puis
/// tombeau de l'abbé) et la petite crypte secrète sous la chapelle du Bourg (dalle descellée, sans repère tant qu'on ne l'a pas passée).
/// Créatures : les échos des moines de l'abbaye (bestiaire existant des échos, corps de moine en robe, rendu d'écho revenu).
/// </summary>
[Trait("Category", "World")]
public sealed class V2481CrypteTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2481_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    public static readonly string[] Crypts = { "zone.crypte_abbaye", "zone.crypte_tombeau", "zone.crypte_chapelle" };

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition[]> All = new(() => Crypts.Select(id =>
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, id + " : " + r.Report);
        return r.Zone!;
    }).ToArray());

    private static ZoneDefinition Crypt(int i) => All.Value[i];

    private static IEnumerable<ZoneDefinition> AllZones() => new[] { Vallons.Value }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.vallons"));

    private static (double X, double Z) Min(ZoneDefinition z) => (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);

    private static (double X, double Z) Max(ZoneDefinition z) => (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);

    private static IEnumerable<CombatantSetup> Fighters(ZoneDefinition z) => z.Setup.Combatants.Concat(z.Chiefs.SelectMany(b => b.Forms));

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Deux_niveaux_sous_l_abbaye_et_la_crypte_de_la_chapelle()
    {
        ZoneDefinition[] c = All.Value;
        Assert.All(c, z => Assert.True(z.Underground && z.Sheltered && !z.Indoor && z.SchemaVersion == 15 && z.Region is null, z.Id));
        Assert.Equal(new[] { ("sortie", "zone.vallons", "porte_crypte_abbaye", DoorKind.Door), ("escalier_du_tombeau", "zone.crypte_tombeau", "escalier_du_tombeau", DoorKind.Stairs) },
            c[0].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("escalier_du_tombeau", "zone.crypte_abbaye", "escalier_du_tombeau", DoorKind.Stairs) }, c[1].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("dalle", "zone.chapelle", "secret_dalle", DoorKind.Hatch) }, c[2].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.True(ZoneValidator.CheckDoors(AllZones().ToArray()).IsValid);
        foreach (string id in Crypts) Assert.Contains(V16TestData.Interiors, z => z.Id == id);

        // Sous l'abbaye (niveau 20 en surface), de 20 à 22 ; la crypte de la chapelle, au Bourg, au niveau 6.
        Assert.Equal(new[] { 20, 21, 21, 22, 6 }, c.SelectMany(z => z.Places).Select(p => p.MinLevel));
        Assert.Equal(new[] { "pied_de_l_escalier", "bas_de_l_escalier", "sous_la_dalle" }, c.Select(z => z.SafePoints.Single().Id));
        Assert.All(c, z => Assert.Equal(z.Entry.Id, z.SafePoints[0].Id));

        // La chapelle passe au schéma 15 pour sa trappe ; sa sortie ne change pas et reste sa première porte.
        ZoneDefinition chapelle = V16TestData.Interiors.Single(z => z.Id == "zone.chapelle");
        Assert.Equal(15, chapelle.SchemaVersion);
        Assert.Equal(new[] { ("sortie", DoorKind.Door), ("secret_dalle", DoorKind.Hatch) }, chapelle.Doors.Select(d => (d.Id, d.Kind)));
    }

    [Fact]
    public void Escalier_dans_le_choeur_de_la_nef_a_pied_depuis_le_parvis()
    {
        ZoneDefinition vallons = Vallons.Value;
        Door entree = vallons.DoorOf("porte_crypte_abbaye")!;
        Assert.Equal(("zone.crypte_abbaye", "sortie", DoorKind.Door, "Escalier de la crypte"), (entree.To, entree.ToDoor, entree.Kind, entree.Name));
        (double x, double z) = P(entree.Position);
        Place nef = vallons.Places.Single(p => p.Id == "nef_de_l_abbaye");
        Assert.InRange(x, P(nef.Min).X + 45, P(nef.Max).X);   // le chœur, à l'est de la nef
        Assert.InRange(z, P(nef.Min).Z, P(nef.Max).Z);
        Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) < 0.5);
        Assert.True(Walks((1620, 120), (1790, 200), SafePoint("parvis_de_l_abbaye"), (x, z)));
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
    public void Chefs_et_recompenses_a_la_premiere_victoire()
    {
        Boss ossuaire = Assert.Single(Crypt(0).Chiefs), abbe = Assert.Single(Crypt(1).Chiefs), sacristain = Assert.Single(Crypt(2).Chiefs);
        Assert.Equal(("gardien_de_l_ossuaire", "fact.gardien_de_l_ossuaire.vaincu", "loot.echo"), (ossuaire.Id, ossuaire.Fact, ossuaire.Loot));
        Assert.Equal(new[] { "moine_echo_gardien_ossuaire", "moine_echo_gardien_ossuaire_dechaine" }, ossuaire.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 2) }, ossuaire.Rewards);
        Assert.Equal(("abbe_sans_repos", "fact.abbe_sans_repos.apaise", "loot.echo"), (abbe.Id, abbe.Fact, abbe.Loot));
        Assert.Equal(new[] { "moine_echo_abbe", "moine_echo_abbe_courrouce", "moine_echo_abbe_dechire" }, abbe.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.sceau_de_l_abbe", 1), ("item.eclat_de_seuil", 2) }, abbe.Rewards);
        Assert.Equal(("sacristain", "fact.sacristain.apaise"), (sacristain.Id, sacristain.Fact));
        Assert.Equal(new[] { ("item.anneau_du_sacristain", 1) }, sacristain.Rewards);   // récompense modeste
        Assert.Empty(Crypt(2).Populations);

        ItemDefinition sceau = Items.Item("item.sceau_de_l_abbe"), anneau = Items.Item("item.anneau_du_sacristain");
        Assert.Equal((ItemType.Equipment, "ring", 20, ItemRarity.Epic, true), (sceau.Type, sceau.Slot, sceau.RequiredLevel, sceau.Rarity, sceau.Named));
        Assert.Equal((50, 100, 20), (sceau.Bonus.MagicResistance, sceau.Bonus.MaxHealth, sceau.Bonus.Armor));
        Assert.Equal((ItemType.Equipment, "ring", 6, ItemRarity.Rare, true), (anneau.Type, anneau.Slot, anneau.RequiredLevel, anneau.Rarity, anneau.Named));
        Assert.Equal((35, 10), (anneau.Bonus.MaxHealth, anneau.Bonus.StatusResistance));
        Assert.Equal(2400, V11TestData.ShippedMerchants.Values["item.sceau_de_l_abbe"]);
        Assert.Equal(180, V11TestData.ShippedMerchants.Values["item.anneau_du_sacristain"]);
        var facts = Vallons.Value.Chiefs.Select(c => c.Fact).Concat(V16TestData.Interiors.SelectMany(z => z.Chiefs).Select(c => c.Fact)).ToList();
        Assert.Equal(facts.Count, facts.Distinct().Count());
    }

    [Fact]
    public void Creatures_et_chefs_a_8_m_hors_d_aggro_et_hors_de_la_garde_des_portes()
    {
        // Règles des dessous (V2.45.1 et décision de Damien du 22 novembre 2026, revue Codex de la PR #248) : 8 m au moins de chaque porte,
        // hors de portée d'aggro de bord à bord (rayon de la créature et du plus gros personnage, 0,5 m de marge) ; et ici, en plus, aucun
        // ennemi à moins de la garde des portes (RestSafety, 12 m) : on peut toujours repartir par où l'on est arrivé.
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
        Assert.Equal(12 + 7 + 1, pairs);   // (4 moines + 2 formes) × 2 portes ; 4 moines + 3 formes ; le sacristain
    }

    [Fact]
    public void Aucun_ennemi_des_Vallons_a_moins_de_12_m_d_une_porte_de_dessous()
    {
        // Relecture indépendante de la PR #250 : l'écho de l'abbaye à 11 m de l'escalier de la crypte bloquait la porte (CanUseDoor refuse à
        // moins de RestSafety d'un ennemi vivant). Aucun ennemi des Vallons (créatures et formes des chefs) à 12 m ou moins d'une porte qui
        // descend (grotte, mines, crypte).
        ZoneDefinition vallons = Vallons.Value;
        var under = V16TestData.Interiors.Where(z => z.Underground).Select(z => z.Id).ToHashSet();
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        var doors = vallons.Doors.Where(d => under.Contains(d.To)).ToList();
        Assert.Contains(doors, d => d.Id == "porte_crypte_abbaye");
        Assert.True(doors.Count >= 3, string.Join(", ", doors.Select(d => d.Id)));
        foreach (Door d in doors)
            foreach (CombatantSetup c in Fighters(vallons).Where(c => c is AssailantSetup && c.Placement != null))
            {
                (double x, double z) = P(c.Placement!.Position);
                (double dx, double dz) = P(d.Position);
                double centre = Math.Sqrt((x - dx) * (x - dx) + (z - dz) * (z - dz));
                Assert.True(centre > guard, $"{d.Id} : {c.Id} à {centre:0.##} m");
            }
    }

    private ZoneLoadResult WithScenario(string zone, string scenario, Action<JsonObject> edit)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "crypte_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(s);
        File.WriteAllText(path, s.ToJsonString());
        return Read(ZoneJson(zone), dir);
    }

    private static JsonObject Combatant(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    [Theory]
    [InlineData(5.5, true)]       // un moine à 8 m pile de l'escalier (0 ; −2,5) : accepté
    [InlineData(5.4999, false)]   // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_a_l_escalier_du_tombeau(double z, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.crypte_tombeau", "crypte_tombeau", s => Combatant(s, "moine_echo_tombeau1")["position"] = new JsonArray(0.0, z));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.Contains("porte « escalier_du_tombeau »", StringComparison.Ordinal));
    }

    [Fact]
    public void Sacristain_pose_sur_la_dalle_refuse()
    {
        ZoneLoadResult r = WithScenario("zone.crypte_chapelle", "crypte_chapelle_sacristain", s => Combatant(s, "moine_echo_sacristain")["position"] = new JsonArray(0.0, 3.0));
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[0]" && e.Message.Contains("porte « dalle »", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("porte")]
    [InlineData("escalier")]
    public void Dalle_de_genres_differents_refusee(string kind)
    {
        JsonObject crypte = ZoneJson("zone.crypte_chapelle");
        crypte["data"]!["doors"]![0]!["kind"] = kind;
        ZoneLoadResult r = Read(crypte);
        Assert.True(r.IsValid, r.Report.ToString());
        ValidationReport report = ZoneValidator.CheckDoors(AllZones().Where(z => z.Id != "zone.crypte_chapelle").Append(r.Zone!).ToArray());
        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Path.EndsWith(".kind", StringComparison.Ordinal));
    }

    [Fact]
    public void Crypte_marquee_interieur_refusee()
    {
        JsonObject zone = ZoneJson("zone.crypte_tombeau");
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    /// <summary>
    /// Chaque chef des cryptes ne donne sa récompense qu'à la première victoire (relecture indépendante de la PR #250 : le gardien de
    /// l'ossuaire et le sacristain, en plus de l'abbé) ; la seconde victoire, après relecture de la sauvegarde, ne donne rien.
    /// </summary>
    [Theory]
    [InlineData("zone.crypte_tombeau", "crypte_tombeau_abbe", "moine_echo_abbe,moine_echo_abbe_courrouce,moine_echo_abbe_dechire", "abbe_sans_repos", "fact.abbe_sans_repos.apaise", "item.sceau_de_l_abbe:1,item.eclat_de_seuil:2")]
    [InlineData("zone.crypte_abbaye", "crypte_abbaye_gardien", "moine_echo_gardien_ossuaire,moine_echo_gardien_ossuaire_dechaine", "gardien_de_l_ossuaire", "fact.gardien_de_l_ossuaire.vaincu", "item.eclat_de_seuil:2")]
    [InlineData("zone.crypte_chapelle", "crypte_chapelle_sacristain", "moine_echo_sacristain", "sacristain", "fact.sacristain.apaise", "item.anneau_du_sacristain:1")]
    public void Chef_recompense_a_la_premiere_victoire_seulement(string zoneId, string scenario, string formList, string chief, string fact, string rewardList)
    {
        string dir = Path.Combine(_folder, "scenarios_" + chief);
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "crypte_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject g = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        string[] forms = formList.Split(',');
        for (int i = 0; i < forms.Length; i++)
        {
            JsonObject f = Combatant(g, forms[i]);
            f["health"] = 10;
            f["intervalMs"] = 60_000;
            foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
            f["position"] = new JsonArray(-3.0 + 3 * i, 7.0);   // près de l'arrivée
        }
        File.WriteAllText(path, g.ToJsonString());
        JsonObject root = ZoneJson(zoneId);
        root["data"]!["populations"] = new JsonArray();
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        var rewards = rewardList.Split(',').Select(r => (r.Split(':')[0], int.Parse(r.Split(':')[1]))).ToArray();
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 22));
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

    /// <summary>Relecture indépendante de la PR #250 : le validateur garde à 8 m des portes toutes les formes d'un chef, pas seulement la première.</summary>
    [Theory]
    [InlineData(10.0, true)]      // la forme déchaînée à 8 m pile de l'escalier du tombeau (18 ; 33) : acceptée
    [InlineData(10.0001, false)]  // juste en deçà : refusée, au chemin de la seconde forme
    public void Frontiere_huit_metres_pour_la_seconde_forme_du_gardien(double x, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.crypte_abbaye", "crypte_abbaye_gardien", s => Combatant(s, "moine_echo_gardien_ossuaire_dechaine")["position"] = new JsonArray(x, 33.0));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("porte « escalier_du_tombeau »", StringComparison.Ordinal));
        else
            Assert.True(r.IsValid, r.Report.ToString());
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

    /// <summary>Chaque créature des cryptes est un moine revenu : corps humain en robe du clergé, rendu d'écho revenu (gris-bleu pâle).</summary>
    [Fact]
    public void Apparence_des_moines_de_la_crypte()
    {
        var all = All.Value.SelectMany(z => Fighters(z).Select(c => c.Id)).ToList();
        Assert.Equal(14, all.Count);
        foreach (string id in all)
        {
            Assert.StartsWith("moine_echo", id, StringComparison.Ordinal);
            Assert.Equal(StyleSlot.Character, StyleRules.ForCombatant(id, false));
            Assert.Equal("moine", StyleRules.CharacterOf(id));
            Assert.Equal(EchoKind.Returned, EchoLooks.Of(id));
            HumanLook look = HumanLooks.For(id, id);
            Assert.Equal(HumanOutfit.Clergy, look.Outfit);
            Assert.False(look.Female);
        }
        // Les autres échos ne changent pas : ceux de l'abbaye en surface, les loups-échos.
        Assert.Equal(EchoKind.None, EchoLooks.Of("echo_abbaye1"));
        Assert.Equal(EchoKind.Night, EchoLooks.Of("loup_echo_mine1"));
        Assert.Equal(HumanOutfit.Peasant, HumanLooks.OutfitOf("paysan", "paysan"));
    }

    [Fact]
    public void Une_seule_equipe_d_echos_sans_treve_a_declarer()
    {
        Assert.All(All.Value, z => Assert.Equal(new[] { "echos" }, Fighters(z).Select(c => c.Team).Distinct()));
        Assert.All(All.Value, z => Assert.Empty(z.Setup.Truces));
    }

    [Fact]
    public void Dans_les_cryptes_ni_cheval_ni_voyage_rapide_vers_elles()
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

    // --- La dalle secrète de la chapelle -----------------------------------------------------------------------------------

    [Fact]
    public void Seule_la_dalle_de_la_chapelle_est_secrete()
    {
        var secret = AllZones().SelectMany(z => z.Doors.Where(AdventureSession.IsSecretDoor).Select(d => (z.Id, d.Id))).ToList();
        Assert.Equal(new[] { ("zone.chapelle", "secret_dalle") }, secret);
        Assert.Throws<ArgumentNullException>(() => AdventureSession.IsSecretDoor(null!));
    }

    /// <summary>
    /// Relecture indépendante de la PR #250 : frontières de la porte secrète. Seul le préfixe exact « secret_ » compte (« secret » sans tiret
    /// bas, « Secret_… » ou « SECRET_… » ne sont pas secrets ; la casse compte) ; une porte secrète vers une zone absente reste cachée.
    /// </summary>
    [Theory]
    [InlineData("secret_dalle", true)]
    [InlineData("secret_", true)]
    [InlineData("secret", false)]
    [InlineData("secretdalle", false)]
    [InlineData("dalle_secret_", false)]
    public void Prefixe_de_la_porte_secrete(string id, bool secret)
    {
        Assert.Equal(secret, AdventureSession.IsSecretDoor(ChapelDoor(id, "zone.crypte_chapelle")));
    }

    /// <summary>La casse compte : une majuscule est refusée par le validateur (identifiant local en minuscules), elle n'est donc jamais secrète.</summary>
    [Theory]
    [InlineData("Secret_dalle")]
    [InlineData("SECRET_DALLE")]
    public void Porte_secrete_en_majuscules_refusee(string id)
    {
        JsonObject chapelle = ZoneJson("zone.chapelle");
        chapelle["data"]!["doors"]![1]!["id"] = id;
        ZoneLoadResult r = Read(chapelle);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.doors[1].id");
    }

    /// <summary>Porte de la chapelle relue avec un autre identifiant et une autre zone d'arrivée (une seule zone est validée : l'arrivée n'est pas vérifiée).</summary>
    private static Door ChapelDoor(string id, string to)
    {
        JsonObject chapelle = ZoneJson("zone.chapelle");
        Assert.Equal("secret_dalle", (string)chapelle["data"]!["doors"]![1]!["id"]!);
        chapelle["data"]!["doors"]![1]!["id"] = id;
        chapelle["data"]!["doors"]![1]!["to"] = to;
        ZoneLoadResult r = Read(chapelle);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!.DoorOf(id)!;
    }

    [Fact]
    public void Dalle_sans_repere_tant_qu_on_ne_l_a_pas_passee_puis_montree()
    {
        // Données copiées : le point sûr de la chapelle posé près de la dalle (à portée, loin de la sortie), le sacristain ôté.
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (Scenarios, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        void Edit(string zone, Action<JsonObject> edit)
        {
            string p = ZoneJsonReader.PathIn(data, zone);
            JsonObject root = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
            edit(root);
            File.WriteAllText(p, root.ToJsonString());
        }
        Edit("zone.chapelle", c => c["data"]!["safePoints"]![0]!["position"] = new JsonArray(3.9, -5.6));
        Edit("zone.crypte_chapelle", c => c["data"]!["chiefs"] = new JsonArray());
        // V2.57 : sans sacristain, la quête du sacristain (son fait) ne tient plus : l'histoire sans les quêtes des Vallons.
        string storyPath = StoryJsonReader.StoryPathIn(data);
        JsonNode story = JsonNode.Parse(File.ReadAllText(storyPath))!;
        V257TestData.WithoutQuetes(story);
        File.WriteAllText(storyPath, story.ToJsonString());
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.chapelle", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = V11TestData.Adventure(save, data, scenarios);
        a.DrainMessages();

        Door dalle = a.World.Definition!.DoorOf("secret_dalle")!;
        Assert.True(a.SecretHidden(dalle));
        Assert.False(a.SecretHidden(a.World.Definition!.DoorOf("sortie")!));
        // Zone d'arrivée absente du monde : jamais découverte, la porte secrète reste cachée ; une porte ordinaire vers elle ne l'est pas.
        Assert.True(a.SecretHidden(ChapelDoor("secret_dalle", "zone.inexistante")));
        Assert.False(a.SecretHidden(ChapelDoor("dalle", "zone.inexistante")));
        Assert.Equal(new[] { "sortie" }, a.Doors().Select(d => d.Id));   // ni étiquette…
        Assert.DoesNotContain(a.MapMarkers(), m => m.Kind == MapMarkerKind.Door && m.Name == "Dalle descellée");   // … ni repère
        Assert.Contains(a.Map.Doors, d => d.Id == "secret_dalle" && d.Kind == DoorKind.Hatch);   // le décor la garde (Unity : une dalle au sol)
        Assert.Equal("secret_dalle", a.DoorInReach!.Id);   // en fouillant, on la trouve
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()) + " / " + a.DoorHint);
        Assert.Equal("zone.crypte_chapelle", a.World.Definition!.Id);
        Assert.True(a.Underground);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.Equal("dalle", a.DoorInReach?.Id);
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()) + " / " + a.DoorHint);
        Assert.Equal("zone.chapelle", a.World.Definition!.Id);
        // Revenu de la crypte : la dalle est connue, elle a son étiquette et son repère.
        Assert.False(a.SecretHidden(a.World.Definition!.DoorOf("secret_dalle")!));
        Assert.Equal(new[] { "sortie", "secret_dalle" }, a.Doors().Select(d => d.Id));
        Assert.Contains(a.MapMarkers(), m => m.Kind == MapMarkerKind.Door && m.Name == "Dalle descellée");
    }
}
