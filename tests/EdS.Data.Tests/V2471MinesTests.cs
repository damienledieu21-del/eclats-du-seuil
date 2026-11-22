using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.47.1 (PLAN_GRANDE_CARTE.md, ligne V2.47) : les mines de la carrière, donjon du sujet 27 sur trois niveaux d'après la coupe du
/// plan de la mine. Trois dessous (`floor: "dessous"`) reliés en chaîne : la bouche de la mine (porte) mène aux galeries boisées, l'ascenseur
/// de la mine (puits) aux galeries des filons, les échelles du fond (escalier) au cœur de cristal, au-delà de la rivière souterraine. Un chef
/// par niveau ; le gardien des cristaux, au fond, donne le cœur de cristal du Seuil à sa première défaite.
/// </summary>
[Trait("Category", "World")]
public sealed class V2471MinesTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2471_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    public static readonly string[] Levels = { "zone.mine_galeries", "zone.mine_filons", "zone.mine_cristaux" };

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition[]> Mine = new(() => Levels.Select(id =>
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, id + " : " + r.Report);
        return r.Zone!;
    }).ToArray());

    private static ZoneDefinition Level(int i) => Mine.Value[i];

    /// <summary>Les Vallons et toutes les zones reliées par des portes.</summary>
    private static IEnumerable<ZoneDefinition> AllZones() => new[] { Vallons.Value }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.vallons"));

    private static (double X, double Z) Min(ZoneDefinition z) => (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);

    private static (double X, double Z) Max(ZoneDefinition z) => (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);

    private static (HashSet<(int, int)> Reached, bool Edge) FloodFromEntry(ZoneDefinition z, IEnumerable<SpaceObstacle>? extra = null) =>
        // Tous les obstacles arrêtent la marche, les bas compris (rivière souterraine, rails).
        V228cSurfaceTests.Flood(z.Setup.Space!.Obstacles.Concat(extra ?? Array.Empty<SpaceObstacle>()), Min(z), Max(z), P(z.SafePoints[0].Position), 0.5);

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Trois_niveaux_relies_en_chaine_porte_ascenseur_echelles()
    {
        ZoneDefinition[] mine = Mine.Value;
        Assert.All(mine, z => Assert.True(z.Underground && z.Sheltered && !z.Indoor && z.SchemaVersion == 15, z.Id));
        Assert.All(mine, z => Assert.True(z.Region is null, z.Id));   // chaque niveau tient en moins de 256 m : pas de secteurs

        // Surface → galeries : la bouche de la mine (porte) ; galeries → filons : l'ascenseur de la mine (puits) ; filons → cristaux : les
        // échelles du fond (escalier). Chaque porte répond à celle d'en face, du même genre.
        Assert.Equal(new[] { ("sortie", "zone.vallons", "porte_mine_galeries", DoorKind.Door), ("ascenseur", "zone.mine_filons", "ascenseur", DoorKind.Shaft) },
            mine[0].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("ascenseur", "zone.mine_galeries", "ascenseur", DoorKind.Shaft), ("echelles", "zone.mine_cristaux", "echelles", DoorKind.Stairs) },
            mine[1].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("echelles", "zone.mine_filons", "echelles", DoorKind.Stairs) }, mine[2].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal("Ascenseur de la mine", mine[0].DoorOf("ascenseur")!.Name);
        Assert.True(ZoneValidator.CheckDoors(AllZones().ToArray()).IsValid);
        foreach (string id in Levels) Assert.Contains(V16TestData.Interiors, z => z.Id == id);

        // Plus on descend, plus c'est dur : de 18 à 22, sous le Gardien du Seuil (23) comme toute la région.
        Assert.Equal(new[] { 18, 19, 20, 20, 21, 22 }, mine.SelectMany(z => z.Places).Select(p => p.MinLevel));
        Assert.Equal(new[] { "bouche_des_galeries", "camp_des_mineurs", "pied_des_echelles" }, mine.Select(z => z.SafePoints.Single().Id));
        Assert.All(mine, z => Assert.Equal(z.Entry.Id, z.SafePoints[0].Id));
    }

    [Fact]
    public void Bouche_de_la_mine_au_carreau_a_pied_depuis_le_point_sur()
    {
        ZoneDefinition vallons = Vallons.Value;
        Door entree = vallons.DoorOf("porte_mine_galeries")!;
        Assert.Equal(("zone.mine_galeries", "sortie", DoorKind.Door, "Mine de la carrière"), (entree.To, entree.ToDoor, entree.Kind, entree.Name));
        (double x, double z) = P(entree.Position);
        Place carreau = vallons.Places.Single(p => p.Id == "carreau_de_la_mine");
        Assert.InRange(x, P(carreau.Min).X, P(carreau.Max).X);
        Assert.InRange(z, P(carreau.Min).Z, P(carreau.Max).Z);
        Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) < 0.5);
        // Devant les planches de la bouche (V2.37), à moins de 20 m du point sûr de la bouche, et joignable à pied.
        (double sx, double sz) = SafePoint("bouche_de_la_mine");
        Assert.True(Math.Sqrt((x - sx) * (x - sx) + (z - sz) * (z - sz)) < 20);
        Assert.True(Walks((1140, 320), (1220, 375), (sx, sz), (x, z)));
    }

    [Fact]
    public void De_l_entree_a_chaque_creature_chef_et_porte_a_pied()
    {
        foreach (ZoneDefinition z in Mine.Value)
        {
            var flood = FloodFromEntry(z);
            var spots = z.Setup.Combatants.Select(c => (c.Id, P(c.Placement!.Position)))
                .Concat(z.Chiefs.SelectMany(b => b.Forms).Select(f => (f.Id, P(f.Placement!.Position))))
                .Concat(z.Doors.Select(d => (d.Id, P(d.Position))));
            foreach ((string id, (double X, double Z) at) in spots)
                Assert.True(V228cSurfaceTests.Reaches(flood.Reached, Min(z), at, 0.5), z.Id + " : " + id);
        }
    }

    [Fact]
    public void Riviere_souterraine_ne_se_passe_que_par_la_passerelle()
    {
        // Propriété : la passerelle (3 m entre les deux bras de la rivière) est le seul passage vers le cœur de cristal. Bouchée, le gardien
        // et le fond ne s'atteignent plus.
        ZoneDefinition cristaux = Level(2);
        var river = cristaux.Setup.Space!.Obstacles.Where(o => o.Id.StartsWith("riviere_mine", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, river.Count);
        Assert.All(river, o => Assert.True(o.Low));   // de l'eau : elle arrête la marche, pas la vue
        (double X, double Z) gardien = P(cristaux.Chiefs.Single().Forms[0].Placement!.Position);
        Assert.True(V228cSurfaceTests.Reaches(FloodFromEntry(cristaux).Reached, Min(cristaux), gardien));
        var plug = new SpaceObstacle("bouchon", new[] { V(-2, 13.5), V(2, 13.5), V(2, 18.5), V(-2, 18.5) });
        var closed = FloodFromEntry(cristaux, new[] { plug });
        Assert.False(V228cSurfaceTests.Reaches(closed.Reached, Min(cristaux), gardien));
    }

    private static GroundVector V(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 10000)), Fixed.FromRaw((long)Math.Round(z * 10000)));

    [Fact]
    public void Chefs_de_chaque_niveau_et_recompense_au_fond()
    {
        Boss pilleurs = Assert.Single(Level(0).Chiefs), bete = Assert.Single(Level(1).Chiefs), gardien = Assert.Single(Level(2).Chiefs);
        Assert.Equal(("chef_des_pilleurs", "fact.chef_des_pilleurs.vaincu", "loot.chef_des_bandits"), (pilleurs.Id, pilleurs.Fact, pilleurs.Loot));
        Assert.Equal(new[] { "bandit_chef_mine", "bandit_chef_mine_enrage" }, pilleurs.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.minerai_de_fer", 6) }, pilleurs.Rewards);
        Assert.Equal(("bete_des_eboulis", "fact.bete_des_eboulis.vaincue", "loot.ours"), (bete.Id, bete.Fact, bete.Loot));
        Assert.Equal(new[] { "ours_bete_des_eboulis", "ours_bete_des_eboulis_furieuse" }, bete.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 3) }, bete.Rewards);
        Assert.Equal(("gardien_des_cristaux", "fact.gardien_des_cristaux.vaincu", "loot.echo"), (gardien.Id, gardien.Fact, gardien.Loot));
        Assert.Equal(new[] { "loup_echo_gardien_cristaux", "loup_echo_gardien_cristaux_fendu", "loup_echo_gardien_cristaux_brise" }, gardien.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.coeur_de_cristal_du_seuil", 1), ("item.eclat_de_seuil", 2) }, gardien.Rewards);

        ItemDefinition coeur = Items.Item("item.coeur_de_cristal_du_seuil");
        Assert.Equal((ItemType.Equipment, "neck", 20, ItemRarity.Epic, true), (coeur.Type, coeur.Slot, coeur.RequiredLevel, coeur.Rarity, coeur.Named));
        Assert.Equal((70, 120, 12), (coeur.Bonus.MagicResistance, coeur.Bonus.MaxHealth, coeur.Bonus.PhysicalPower));
        Assert.Equal(2400, V11TestData.ShippedMerchants.Values["item.coeur_de_cristal_du_seuil"]);
        // Chaque fait de victoire est neuf (aucun autre chef de la région ne l'apprend).
        var facts = Vallons.Value.Chiefs.Select(c => c.Fact).Concat(V16TestData.Interiors.SelectMany(z => z.Chiefs).Select(c => c.Fact)).ToList();
        Assert.Equal(facts.Count, facts.Distinct().Count());
    }

    [Fact]
    public void Creatures_et_chefs_a_8_m_au_moins_de_chaque_porte()
    {
        foreach (ZoneDefinition z in Mine.Value)
            foreach (Door d in z.Doors)
            {
                (double dx, double dz) = P(d.Position);
                foreach (var c in z.Setup.Combatants.Select(c => c.Placement!.Position).Concat(z.Chiefs.SelectMany(b => b.Forms).Select(f => f.Placement!.Position)))
                {
                    (double x, double zz) = P(c);
                    Assert.True((x - dx) * (x - dx) + (zz - dz) * (zz - dz) >= 64, z.Id + " / " + d.Id);
                }
            }
    }

    /// <summary>
    /// Décision de Damien du 22 novembre 2026 : aucune créature (ni forme de chef) des trois niveaux n'est à portée d'aggro d'une porte, sans
    /// quoi on arrive en combat et on ne peut plus repasser la porte (l'archer de la salle des pilleurs était à 10 m de l'ascenseur pour 13).
    /// </summary>
    [Fact]
    public void Aucune_creature_a_portee_d_aggro_d_une_porte()
    {
        int checkedPairs = 0;
        foreach (ZoneDefinition z in Mine.Value)
            foreach (Door d in z.Doors)
            {
                (double dx, double dz) = P(d.Position);
                foreach (var c in z.Setup.Combatants.Concat(z.Chiefs.SelectMany(b => b.Forms)))
                {
                    if ((c as AssailantSetup)?.Behavior?.AggroRange is not Fixed aggro) continue;
                    (double x, double zz) = P(c.Placement!.Position);
                    double r = aggro.Raw / 1e4;
                    Assert.True((x - dx) * (x - dx) + (zz - dz) * (zz - dz) > r * r, z.Id + " / " + d.Id + " : " + c.Id);
                    checkedPairs++;
                }
            }
        Assert.True(checkedPairs >= 25, checkedPairs.ToString());   // toutes les créatures ont une portée d'aggro
    }

    /// <summary>Niveau <paramref name="zone"/> lu d'un dossier de scénarios d'essai où <paramref name="edit"/> retouche le scénario <paramref name="scenario"/>.</summary>
    private ZoneLoadResult WithScenario(string zone, string scenario, Action<JsonObject> edit, Action<JsonObject>? zoneEdit = null)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "mine_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(s);
        File.WriteAllText(path, s.ToJsonString());
        JsonObject root = ZoneJson(zone);
        zoneEdit?.Invoke(root);
        return Read(root, dir);
    }

    private static JsonObject Combatant(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    [Theory]
    [InlineData(5.5, true)]       // l'écho à 8 m pile de l'ascenseur (20 ; −2,5) : accepté
    [InlineData(5.4999, false)]   // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_a_l_ascenseur(double z, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.mine_filons", "mine_filons", s => Combatant(s, "loup_echo_mine2")["position"] = new JsonArray(20.0, z));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[1].creature" && e.Message.Contains("porte « ascenseur »", StringComparison.Ordinal));
    }

    [Fact]
    public void Gardien_des_cristaux_trop_pres_des_echelles_refuse()
    {
        // Adversarial : la première forme posée au pied des échelles (0 ; −2,5) : refusée au chemin de la forme.
        ZoneLoadResult r = WithScenario("zone.mine_cristaux", "mine_cristaux_gardien", s => Combatant(s, "loup_echo_gardien_cristaux")["position"] = new JsonArray(3.0, 2.0));
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[0]" && e.Message.Contains("porte « echelles »", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("escalier")]   // l'ascenseur d'en haut n'est pas un escalier
    [InlineData("porte")]
    public void Ascenseur_de_genres_differents_refuse(string kind)
    {
        JsonObject galeries = ZoneJson("zone.mine_galeries");
        galeries["data"]!["doors"]![1]!["kind"] = kind;
        ZoneLoadResult r = Read(galeries);
        Assert.True(r.IsValid, r.Report.ToString());   // seule, la zone est valide ; c'est la paire qui ne se répond plus
        ZoneDefinition[] all = AllZones().Where(z => z.Id != "zone.mine_galeries").Append(r.Zone!).ToArray();
        ValidationReport report = ZoneValidator.CheckDoors(all);
        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Path.EndsWith(".kind", StringComparison.Ordinal));
    }

    [Fact]
    public void Niveau_marque_interieur_refuse()
    {
        JsonObject zone = ZoneJson("zone.mine_filons");
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    [Fact]
    public void Gardien_des_cristaux_vaincu_coeur_et_fait_une_fois_puis_rien()
    {
        // Le gardien affaibli (trois formes, 10 PV, 1 dégât par minute) attend près de l'arrivée ; le niveau sans ses autres créatures.
        ZoneLoadResult loaded = WithScenario("zone.mine_cristaux", "mine_cristaux_gardien", g =>
        {
            string[] forms = { "loup_echo_gardien_cristaux", "loup_echo_gardien_cristaux_fendu", "loup_echo_gardien_cristaux_brise" };
            for (int i = 0; i < 3; i++)
            {
                JsonObject f = Combatant(g, forms[i]);
                f["health"] = 10;
                f["intervalMs"] = 60_000;
                foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
                f["position"] = new JsonArray(-3.0 + 3 * i, 7.0);
            }
        }, z => z["data"]!["populations"] = new JsonArray());
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 22));
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, Items);
        List<SessionStep> first = Fight(w, "loup_echo_gardien_cristaux");
        Assert.Contains(first.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Equal(new[] { ("item.coeur_de_cristal_du_seuil", 1), ("item.eclat_de_seuil", 2) }, first.SelectMany(s => s.Rewards));
        Assert.Equal(1, w.Inventory.Count("item.coeur_de_cristal_du_seuil"));
        string key = zone.VictoryKeyOf(zone.Chiefs[0]);
        Assert.Equal("zone.mine_cristaux/boss/gardien_des_cristaux", key);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains(key, saved.Consumed);
        Assert.Contains("fact.gardien_des_cristaux.vaincu", SaveJsonReader.Write(w.Save()), StringComparison.Ordinal);

        var again = new WorldSession(saved, zone, ShippedData, Progression, Items);
        List<SessionStep> second = Fight(again, "loup_echo_gardien_cristaux");
        Assert.Contains(second.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Empty(second.SelectMany(s => s.Rewards));
        Assert.Equal(1, again.Inventory.Count("item.coeur_de_cristal_du_seuil"));
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

    /// <summary>Chaque créature prend l'apparence voulue dans Unity : bandits en personnages, échos en loups-échos de la nuit, ours en ours.</summary>
    [Fact]
    public void Apparence_de_chaque_creature_de_la_mine()
    {
        var all = Mine.Value.SelectMany(z => z.Setup.Combatants.Select(c => c.Id).Concat(z.Chiefs.SelectMany(b => b.Forms).Select(f => f.Id))).ToList();
        Assert.Equal(18, all.Count);
        foreach (string id in all)
        {
            StyleSlot slot = StyleRules.ForCombatant(id, false);
            if (id.StartsWith("bandit", StringComparison.Ordinal)) { Assert.Equal(StyleSlot.Character, slot); Assert.Equal(EchoKind.None, EchoLooks.Of(id)); }
            else if (id.StartsWith("loup_echo", StringComparison.Ordinal)) { Assert.Equal(StyleSlot.Wolf, slot); Assert.Equal(EchoKind.Night, EchoLooks.Of(id)); }
            else
            {
                Assert.StartsWith("ours", id, StringComparison.Ordinal);
                Assert.Equal(StyleSlot.Bear, slot);
                Assert.Equal(0, StyleRules.Cubs(id));
            }
        }
    }

    [Fact]
    public void Treve_entre_echos_et_meute_aux_filons()
    {
        Assert.Contains(Level(1).Setup.Truces, t => (t.A, t.B) is ("echos", "meute") or ("meute", "echos"));
        Assert.Equal(new[] { "bandits" }, Level(0).Setup.Combatants.Select(c => c.Team).Distinct());
        Assert.Equal(new[] { "echos" }, Level(2).Setup.Combatants.Select(c => c.Team).Distinct());
    }

    [Fact]
    public void Dans_la_mine_ni_cheval_ni_voyage_rapide_vers_elle()
    {
        foreach (ZoneDefinition z in Mine.Value)
        {
            var w = new WorldSession(new WorldSave(z.Id, 0, 0, Array.Empty<string>(),
                PlayerCharacters.Create("bastion", "class.bastion", Progression).WithMount(PlayerCharacters.Horse)), z, ShippedData, Progression, Items);
            w.Step();
            Assert.False(w.CanCallMount);
            Assert.True(w.IsDiscovered(z, z.SafePoints[0].Id));
            Assert.Equal(TravelRefusal.UnknownPlace, w.CanTravel(z, z.SafePoints[0].Id));
            Assert.All(z.Populations, p => Assert.True(w.Zone.Engine.View(p.Creature).IsPresent));
        }
    }

    // --- Aventure ----------------------------------------------------------------------------------------------------------

    /// <summary>Copie des données et des scénarios ; <paramref name="galeries"/> retouche le premier niveau.</summary>
    private (string Data, string Scenarios) Copy(Action<JsonObject> galeries)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (Scenarios, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        string path = ZoneJsonReader.PathIn(data, "zone.mine_galeries");
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        galeries(root);
        File.WriteAllText(path, root.ToJsonString());
        // Et le scénario du premier niveau sans ses créatures (aucun ennemi près de l'ascenseur).
        string scenario = Path.Combine(scenarios, "zones", "mine_galeries.json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(scenario))!.AsObject();
        s["data"]!["combatants"] = new JsonArray();
        File.WriteAllText(scenario, s.ToJsonString());
        return (data, scenarios);
    }

    [Fact]
    public void Ascenseur_de_la_mine_descend_et_remonte()
    {
        // Le point sûr du premier niveau posé devant l'ascenseur, ses créatures ôtées (populations et scénario) : on descend aux filons, puis on remonte.
        (string data, string scenarios) = Copy(g =>
        {
            g["data"]!["safePoints"]![0]!["position"] = new JsonArray(21, 36.5);
            g["data"]!["populations"] = new JsonArray();
        });
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.mine_galeries", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = V11TestData.Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.True(a.Underground && a.Sheltered && !a.Indoors);
        Assert.Equal("ascenseur", a.DoorInReach!.Id);
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()) + " / " + a.DoorHint);
        Assert.Equal("zone.mine_filons", a.World.Definition!.Id);
        Assert.True(a.Underground && a.Map.Underground);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.Equal("ascenseur", a.DoorInReach?.Id);   // on arrive à la cage d'en bas
        Assert.Equal(new[] { false, false }, a.Map.Doors.Select(d => d.Outside));   // aucune porte des filons ne mène dehors
        Assert.DoesNotContain(a.TravelDestinations(), d => Levels.Contains(d.Zone));
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()) + " / " + a.DoorHint);   // les échos des filons sont à 8 m au moins
        Assert.Equal("zone.mine_galeries", a.World.Definition!.Id);
        Assert.Equal(new[] { true, false }, a.Map.Doors.Select(d => d.Outside));   // la bouche mène dehors, l'ascenseur non
    }
}
