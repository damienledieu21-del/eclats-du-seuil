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
/// Chantier V2.49.1 (PLAN_GRANDE_CARTE.md, ligne V2.49) : les égouts du Bourg, deux dessous. Le grand collecteur (canal, passerelles, grille,
/// contrebandiers et leur passeur), entré par la bouche d'égout de la place du marché ou par le déversoir, 3,8 m au sud du fossé sud ; la vieille
/// citerne en dessous (échelle, genre puits), ses loups-échos et la bête de la citerne. Chefs dans le chemin standard (<c>chiefs</c>).
/// </summary>
[Trait("Category", "World")]
public sealed class V2491EgoutsTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2491_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    public static readonly string[] Sewers = { "zone.egouts_collecteur", "zone.egouts_citerne" };

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition[]> All = new(() => Sewers.Select(id =>
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, id + " : " + r.Report);
        return r.Zone!;
    }).ToArray());

    private static ZoneDefinition Sewer(int i) => All.Value[i];

    private static IEnumerable<ZoneDefinition> AllZones() => new[] { Vallons.Value }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.vallons"));

    private static (double X, double Z) Min(ZoneDefinition z) => (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);

    private static (double X, double Z) Max(ZoneDefinition z) => (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);

    private static IEnumerable<CombatantSetup> Fighters(ZoneDefinition z) => z.Setup.Combatants.Concat(z.Chiefs.SelectMany(b => b.Forms));

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Deux_niveaux_sous_le_Bourg()
    {
        ZoneDefinition[] e = All.Value;
        Assert.All(e, z => Assert.True(z.Underground && z.Sheltered && !z.Indoor && z.SchemaVersion == 15 && z.Region is null, z.Id));
        Assert.Equal(new[] { ("bouche_du_marche", "zone.vallons", "porte_egouts_marche", DoorKind.Door), ("deversoir", "zone.vallons", "porte_egouts_deversoir", DoorKind.Door),
                ("echelle_de_la_citerne", "zone.egouts_citerne", "echelle", DoorKind.Shaft) },
            e[0].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("echelle", "zone.egouts_collecteur", "echelle_de_la_citerne", DoorKind.Shaft) }, e[1].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.True(ZoneValidator.CheckDoors(AllZones().ToArray()).IsValid);
        foreach (string id in Sewers) Assert.Contains(V16TestData.Interiors, z => z.Id == id);
        Assert.Equal(new[] { "Égouts du Bourg : grand collecteur", "Égouts du Bourg : vieille citerne" }, e.Select(z => z.Name));

        // Sous le Bourg (niveau 1, la crypte de la chapelle au niveau 6) : de 7 à 10, au niveau de la route d'Havre d'Ambre.
        Assert.Equal(new[] { 7, 8, 9, 10 }, e.SelectMany(z => z.Places).Select(p => p.MinLevel));
        Assert.Equal(new[] { "sous_la_bouche_du_marche", "pied_de_l_echelle" }, e.Select(z => z.SafePoints.Single().Id));
        Assert.All(e, z => Assert.Equal(z.Entry.Id, z.SafePoints[0].Id));
        // Aucune porte secrète ici : les deux entrées se voient.
        Assert.DoesNotContain(e.SelectMany(z => z.Doors), AdventureSession.IsSecretDoor);
    }

    [Fact]
    public void Kit_du_collecteur_et_de_la_citerne_dans_les_donnees()
    {
        // Les pièces du kit d'égout se reconnaissent au nom des obstacles (rendu Unity au V2.49.2) : canal, piliers, grilles, tuyaux, caisses,
        // repaires ; le canal et le bassin sont bas (on tire par-dessus), les murs et les piliers non.
        var o1 = Sewer(0).Setup.Space!.Obstacles;
        var o2 = Sewer(1).Setup.Space!.Obstacles;
        foreach (string part in new[] { "mur_egout", "canal_egout", "pilier_egout", "grille_egout", "tuyau_egout", "caisses_egout", "repaire_egout" })
            Assert.Contains(o1, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        foreach (string part in new[] { "mur_egout", "bassin_egout", "pilier_egout", "grille_egout", "deversoir_egout", "repaire_egout" })
            Assert.Contains(o2, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        Assert.All(o1.Concat(o2).Where(o => o.Id.StartsWith("canal_egout", StringComparison.Ordinal) || o.Id.StartsWith("bassin_egout", StringComparison.Ordinal)), o => Assert.True(o.Low, o.Id));
        Assert.All(o1.Concat(o2).Where(o => o.Id.StartsWith("mur_egout", StringComparison.Ordinal) || o.Id.StartsWith("pilier_egout", StringComparison.Ordinal)), o => Assert.False(o.Low, o.Id));
        // Deux passerelles de 4 m sur le canal : ses trois tronçons laissent x de −20 à −16 et de 16 à 20.
        var canal = o1.Where(o => o.Id.StartsWith("canal_egout", StringComparison.Ordinal)).Select(o => (o.Points.Min(p => P(p).X), o.Points.Max(p => P(p).X))).OrderBy(c => c.Item1).ToList();
        Assert.Equal(new[] { (-30.0, -20.0), (-16.0, 16.0), (20.0, 30.0) }, canal);
    }

    [Fact]
    public void Bouche_sur_la_place_du_marche_et_deversoir_dans_le_fosse_a_pied()
    {
        ZoneDefinition vallons = Vallons.Value;
        Door bouche = vallons.DoorOf("porte_egouts_marche")!, deversoir = vallons.DoorOf("porte_egouts_deversoir")!;
        Assert.Equal(("zone.egouts_collecteur", "bouche_du_marche", DoorKind.Door, "Bouche d'égout du marché"), (bouche.To, bouche.ToDoor, bouche.Kind, bouche.Name));
        Assert.Equal(("zone.egouts_collecteur", "deversoir", DoorKind.Door, "Déversoir des égouts"), (deversoir.To, deversoir.ToDoor, deversoir.Kind, deversoir.Name));
        Assert.Null(bouche.Building);
        Assert.Null(deversoir.Building);
        (double bx, double bz) = P(bouche.Position);
        Place place = vallons.Places.Single(p => p.Id == "place_du_marche_de_la_ville");
        Assert.InRange(bx, P(place.Min).X, P(place.Max).X);
        Assert.InRange(bz, P(place.Min).Z, P(place.Max).Z);
        // Le déversoir, hors de la palissade (z = −320), au sud du fossé.
        (double dx, double dz) = P(deversoir.Position);
        var fosse = Tagged("fosse_ville_sud_est").Single();
        Assert.True(dz < fosse.Points.Min(p => P(p).Z), dz.ToString());
        Assert.InRange(dx, fosse.Points.Min(p => P(p).X), fosse.Points.Max(p => P(p).X));
        foreach ((double x, double z) in new[] { (bx, bz), (dx, dz) })
            Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) < 0.5);
        // De la place du marché à la bouche ; du marché au déversoir par la poterne sud.
        Assert.True(Walks((-1060, -130), (-960, -20), SafePoint("place_du_marche_de_la_ville"), (bx, bz)));
        Assert.True(Walks((-1200, -360), (-990, -60), SafePoint("place_du_marche_de_la_ville"), (dx, dz)));
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
        Boss passeur = Assert.Single(Sewer(0).Chiefs), bete = Assert.Single(Sewer(1).Chiefs);
        Assert.Null(Sewer(0).Boss);
        Assert.Null(Sewer(1).Boss);
        Assert.Equal(("passeur_des_contrebandiers", "fact.passeur_des_contrebandiers.vaincu", "loot.chef_des_bandits"), (passeur.Id, passeur.Fact, passeur.Loot));
        Assert.Equal(new[] { "bandit_chef_egout", "bandit_chef_egout_aux_abois" }, passeur.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.manteau_du_passeur", 1) }, passeur.Rewards);
        Assert.Equal(("bete_de_la_citerne", "fact.bete_de_la_citerne.vaincue", "loot.echo"), (bete.Id, bete.Fact, bete.Loot));
        Assert.Equal(new[] { "loup_echo_bete_citerne", "loup_echo_bete_citerne_enragee" }, bete.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 2) }, bete.Rewards);

        ItemDefinition manteau = Items.Item("item.manteau_du_passeur");
        Assert.Equal((ItemType.Equipment, "cloak", 8, ItemRarity.Rare, true, "Manteau du passeur"), (manteau.Type, manteau.Slot, manteau.RequiredLevel, manteau.Rarity, manteau.Named, manteau.Name));
        Assert.Equal((30, 40, 10), (manteau.Bonus.MagicResistance, manteau.Bonus.MaxHealth, manteau.Bonus.MaxEndurance));
        Assert.Equal(260, V11TestData.ShippedMerchants.Values["item.manteau_du_passeur"]);
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
        Assert.Equal((4 + 4 + 2) * 3 + (3 + 4 + 2) * 1, pairs);   // collecteur : 4 contrebandiers, 3 rats et un rat géant, 2 formes, 3 portes ; citerne : 3 échos, 4 rats, 2 formes, 1 porte
    }

    [Fact]
    public void Aucun_ennemi_des_Vallons_a_moins_de_12_m_des_entrees_des_egouts()
    {
        ZoneDefinition vallons = Vallons.Value;
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        foreach (string id in new[] { "porte_egouts_marche", "porte_egouts_deversoir" })
        {
            (double dx, double dz) = P(vallons.DoorOf(id)!.Position);
            foreach (CombatantSetup c in Fighters(vallons).Where(c => c is AssailantSetup && c.Placement != null))
            {
                (double x, double z) = P(c.Placement!.Position);
                Assert.True(Math.Sqrt((x - dx) * (x - dx) + (z - dz) * (z - dz)) > guard, $"{id} : {c.Id}");
            }
        }
    }

    private ZoneLoadResult WithScenario(string zone, string scenario, Action<JsonObject> edit, Action<JsonObject>? editZone = null)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "egouts_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
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
    [InlineData(5.5, true)]       // un loup-écho à 8 m pile de l'échelle (0 ; −2,5) : accepté
    [InlineData(5.4999, false)]   // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_a_l_echelle_de_la_citerne(double z, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.egouts_citerne", "egouts_citerne", s => Combatant(s, "loup_echo_egout1")["position"] = new JsonArray(0.0, z));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.Contains("porte « echelle »", StringComparison.Ordinal));
    }

    /// <summary>La seconde forme du passeur aussi : à 8 m pile de l'échelle de la citerne (26 ; 38), acceptée ; juste en deçà, refusée.</summary>
    [Theory]
    [InlineData(18.0, true)]
    [InlineData(18.0001, false)]
    public void Frontiere_huit_metres_pour_la_seconde_forme_du_passeur(double x, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.egouts_collecteur", "egouts_collecteur_passeur", s => Combatant(s, "bandit_chef_egout_aux_abois")["position"] = new JsonArray(x, 38.0));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("porte « echelle_de_la_citerne »", StringComparison.Ordinal));
        else
            Assert.True(r.IsValid, r.Report.ToString());
    }

    [Fact]
    public void Contrebandier_pose_sur_la_bouche_du_marche_refuse()
    {
        ZoneLoadResult r = WithScenario("zone.egouts_collecteur", "egouts_collecteur", s => Combatant(s, "bandit_egout1")["position"] = new JsonArray(-24.0, 3.0));
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.Contains("porte « bouche_du_marche »", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("escalier")]
    [InlineData("trappe")]
    [InlineData("porte")]
    public void Echelle_de_genres_differents_refusee(string kind)
    {
        JsonObject citerne = ZoneJson("zone.egouts_citerne");
        citerne["data"]!["doors"]![0]!["kind"] = kind;
        ZoneLoadResult r = Read(citerne);
        Assert.True(r.IsValid, r.Report.ToString());
        ValidationReport report = ZoneValidator.CheckDoors(AllZones().Where(z => z.Id != "zone.egouts_citerne").Append(r.Zone!).ToArray());
        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Path.EndsWith(".kind", StringComparison.Ordinal));
    }

    [Fact]
    public void Egouts_marques_interieur_refuses()
    {
        JsonObject zone = ZoneJson("zone.egouts_collecteur");
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    /// <summary>Chaque chef des égouts ne donne sa récompense qu'à la première victoire ; la seconde, après relecture de la sauvegarde, ne donne rien.</summary>
    [Theory]
    [InlineData("zone.egouts_collecteur", "egouts_collecteur_passeur", "bandit_chef_egout,bandit_chef_egout_aux_abois", "passeur_des_contrebandiers", "fact.passeur_des_contrebandiers.vaincu", "item.manteau_du_passeur:1", -27.0, 7.0)]
    [InlineData("zone.egouts_citerne", "egouts_citerne_bete", "loup_echo_bete_citerne,loup_echo_bete_citerne_enragee", "bete_de_la_citerne", "fact.bete_de_la_citerne.vaincue", "item.eclat_de_seuil:2", -3.0, 7.0)]
    public void Chef_recompense_a_la_premiere_victoire_seulement(string zoneId, string scenario, string formList, string chief, string fact, string rewardList, double x0, double z0)
    {
        string dir = Path.Combine(_folder, "scenarios_" + chief);
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "egouts_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject g = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        string[] forms = formList.Split(',');
        for (int i = 0; i < forms.Length; i++)
        {
            JsonObject f = Combatant(g, forms[i]);
            f["health"] = 10;
            f["intervalMs"] = 60_000;
            foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
            f["position"] = new JsonArray(x0 + 3 * i, z0);   // près de l'arrivée, à plus de 8 m de la porte
        }
        File.WriteAllText(path, g.ToJsonString());
        JsonObject root = ZoneJson(zoneId);
        root["data"]!["populations"] = new JsonArray();
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        var rewards = rewardList.Split(',').Select(r => (r.Split(':')[0], int.Parse(r.Split(':')[1]))).ToArray();
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 10));
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
    /// Apparences : les contrebandiers et leur passeur prennent le personnage « bandit » (forme humaine, sans rendu d'écho) ; les loups-échos et
    /// la bête de la citerne, le modèle de loup avec le rendu d'écho de la nuit. Chaque nom affiché est écrit (jamais le repli « Bandit egout »).
    /// </summary>
    [Fact]
    public void Apparences_et_noms_des_creatures_des_egouts()
    {
        // Décision de Damien du 23 novembre 2026 : des rats et des rats géants sur les deux niveaux, à part (équipe « rats »).
        var bandits = Fighters(Sewer(0)).Select(c => c.Id).Where(id => !StyleRules.IsRat(id)).ToList();
        var echoes = Fighters(Sewer(1)).Select(c => c.Id).Where(id => !StyleRules.IsRat(id)).ToList();
        Assert.Equal(6, bandits.Count);
        Assert.Equal(5, echoes.Count);
        foreach (string id in bandits)
        {
            Assert.StartsWith("bandit_", id, StringComparison.Ordinal);
            Assert.Equal(StyleSlot.Character, StyleRules.ForCombatant(id, false));
            Assert.Equal("bandit", StyleRules.CharacterOf(id));
            Assert.Equal(EchoKind.None, EchoLooks.Of(id));
        }
        foreach (string id in echoes)
        {
            Assert.StartsWith("loup_echo", id, StringComparison.Ordinal);
            Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant(id, false));
            Assert.Equal(EchoKind.Night, EchoLooks.Of(id));
            Assert.Equal(0, StyleRules.Cubs(id));
        }
        var shown = bandits.Concat(echoes).Concat(new[] { "bandit_egout2_v3", "loup_echo_egout3_v2" }).Select(DisplayNames.Of).ToList();
        Assert.Equal(new[] { "Contrebandier", "Contrebandier", "Archer des contrebandiers", "Archer des contrebandiers", "Passeur des contrebandiers", "Passeur aux abois",
                "Loup-écho des égouts", "Loup-écho des égouts", "Loup-écho des égouts", "Bête de la citerne", "Bête de la citerne enragée", "Contrebandier", "Loup-écho des égouts" },
            shown);
        Assert.Equal("Coup de gaffe", DisplayNames.Attack("coup_de_gaffe"));
        Assert.Equal("Vague de fange", DisplayNames.Attack("vague_de_fange"));
        // Les créatures d'avant ne changent pas.
        Assert.Equal("Archer des pilleurs", DisplayNames.Of("bandit_arc_mine_galerie2"));
        Assert.Equal(EchoKind.Night, EchoLooks.Of("loup_echo_mine1"));
    }

    [Fact]
    public void Rats_et_equipe_du_niveau_en_treve()
    {
        // Revue Codex de la PR #245 : deux équipes ennemies d'une même zone demandent une trêve. Décision de Damien du 23 novembre 2026 : les
        // rats rejoignent les contrebandiers et les échos ; ils ne se battent pas entre eux (une trêve par niveau).
        Assert.Equal(new[] { "bandits", "rats" }, Fighters(Sewer(0)).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(new[] { "echos", "rats" }, Fighters(Sewer(1)).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(new[] { ("rats", "bandits") }, Sewer(0).Setup.Truces.Select(t => (t.A, t.B)));
        Assert.Equal(new[] { ("rats", "echos") }, Sewer(1).Setup.Truces.Select(t => (t.A, t.B)));
    }

    [Fact]
    public void Rats_et_rats_geants_sur_les_deux_niveaux()
    {
        // Décision de Damien du 23 novembre 2026 : rats (petits, en groupe, faibles) et rats géants (plus forts), sur les deux niveaux.
        foreach (ZoneDefinition z in All.Value)
        {
            var rats = z.Setup.Combatants.OfType<AssailantSetup>().Where(c => c.Id.StartsWith("rat_egout", StringComparison.Ordinal)).ToList();
            var giants = z.Setup.Combatants.OfType<AssailantSetup>().Where(c => c.Id.StartsWith("rat_geant_egout", StringComparison.Ordinal)).ToList();
            Assert.Equal(3, rats.Count);
            Assert.Single(giants);
            Assert.All(rats, r => Assert.True(r.Behavior!.Pack && r.Health < giants[0].Health && r.Placement!.Radius < giants[0].Placement!.Radius, r.Id));
            // En groupe : chaque rat à 2,5 m au plus d'un autre.
            Assert.All(rats, r => Assert.Contains(rats, o => o != r && Math.Sqrt(Math.Pow(P(o.Placement!.Position).X - P(r.Placement!.Position).X, 2)
                + Math.Pow(P(o.Placement!.Position).Z - P(r.Placement!.Position).Z, 2)) <= 2.5));
            Assert.All(z.Populations.Where(p => StyleRules.IsRat(p.Creature)), p => Assert.Equal(p.Creature.StartsWith("rat_geant", StringComparison.Ordinal) ? "loot.rat_geant" : null, p.Loot));
            foreach (string id in rats.Concat(giants).Select(c => c.Id).Concat(new[] { "rat_egout1_v2", "rat_geant_egout2_v3" }))
            {
                Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant(id, false));   // repli : le loup, à petite échelle
                Assert.Equal(EchoKind.None, EchoLooks.Of(id));
                Assert.Equal(EchoKind.None, EchoLooks.Of(id, afterDeath: true));   // un rat revenu n'est pas un écho
                Assert.Equal(0, StyleRules.Cubs(id));
                Assert.Equal(id.StartsWith("rat_geant", StringComparison.Ordinal) ? 0.6 : 0.3, StyleRules.BeastHeight(id));
                Assert.Equal(id.StartsWith("rat_geant", StringComparison.Ordinal) ? "Rat géant" : "Rat des égouts", DisplayNames.Of(id));
            }
        }
        // « ratelier_bois » (obstacle) n'est pas un rat.
        Assert.False(StyleRules.IsRat("ratelier_bois"));
        Assert.True(StyleRules.IsRat("rat3"));
    }

    [Fact]
    public void Dans_les_egouts_ni_cheval_ni_voyage_rapide_vers_eux()
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

    // --- Décisions de Damien du 23 novembre 2026 : objet nommé à faible taux de drop (rats géants) ---------------------------

    private static LootTable RatLoot(ItemCatalog? catalog = null)
    {
        Assert.True((catalog ?? Items).TryGetLoot("loot.rat_geant", out LootTable table));
        return table;
    }

    [Fact]
    public void Anneau_perdu_des_egouts_objet_nomme_du_butin_des_rats_geants()
    {
        ItemDefinition anneau = Items.Item("item.anneau_perdu_des_egouts");
        Assert.Equal((ItemType.Equipment, "ring", 8, ItemRarity.Rare, true, "Anneau perdu des égouts"), (anneau.Type, anneau.Slot, anneau.RequiredLevel, anneau.Rarity, anneau.Named, anneau.Name));
        Assert.Equal(300, V11TestData.ShippedMerchants.Values["item.anneau_perdu_des_egouts"]);
        Assert.Equal(new[] { ("item.potion_de_soin", 1, 20), ("item.anneau_perdu_des_egouts", 1, 5) }, RatLoot().Drops.Select(d => (d.Item, d.Count, d.ChancePercent)));
        // Seuls les rats géants le portent, sur les deux niveaux ; les chefs gardent leurs récompenses (manteau, deux éclats).
        Assert.Equal(new[] { "rat_geant_egout1", "rat_geant_egout2" }, All.Value.SelectMany(z => z.Populations).Where(p => p.Loot == "loot.rat_geant").Select(p => p.Creature));
    }

    [Fact]
    public void Anneau_perdu_tire_a_5_pour_cent_sur_un_grand_nombre_de_morts()
    {
        LootTable table = RatLoot();
        int rings = 0, potions = 0;
        const int n = 100_000;
        for (int i = 0; i < n; i++)
            foreach (LootRoll r in Loot.Roll(table, $"zone.egouts_citerne/{i}/death/rat_geant_egout2"))
            {
                if (r.Item == "item.anneau_perdu_des_egouts") rings++;
                if (r.Item == "item.potion_de_soin") potions++;
            }
        // Écart type d'environ 69 sur 100 000 tirages à 5 % : la marge de 300 en fait plus de quatre.
        Assert.InRange(rings, 4_700, 5_300);
        Assert.InRange(potions, 19_400, 20_600);
    }

    [Fact]
    public void Tirage_de_l_anneau_deterministe_par_la_cle_de_la_mort()
    {
        // Générateur Pcg32 initialisé par la clé de l'événement (jamais d'horloge) : même clé, même butin ; une autre clé, un autre flux.
        LootTable table = RatLoot();
        string Show(string key) => string.Join(",", Loot.Roll(table, key).Select(r => $"{r.Item}x{r.Count}#{r.Rank}"));
        var keys = Enumerable.Range(0, 2_000).Select(i => $"zone.egouts_collecteur/{i}/death/rat_geant_egout1").ToList();
        var first = keys.Select(Show).ToList();
        Assert.Equal(first, keys.Select(Show).ToList());
        Assert.Contains(first, r => r.Contains("item.anneau_perdu_des_egouts", StringComparison.Ordinal));
        Assert.Contains(first, r => r.Length == 0);
        Assert.True(first.Distinct().Count() > 1);
    }

    [Fact]
    public void Anneau_perdu_quand_le_sac_est_plein()
    {
        // Table d'essai : l'anneau tombe toujours ; le sac est plein : l'objet est annoncé, mais perdu, et rien d'autre ne bouge.
        JsonObject itemsRoot = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        JsonObject ratTable = itemsRoot["data"]!["loot"]!.AsArray().Single(t => (string)t!["id"]! == "loot.rat_geant")!.AsObject();
        ratTable["drops"]![1]!["chancePercent"] = 100;
        ItemCatalog always = ItemJsonReader.Read(itemsRoot.ToJsonString(), ShippedData).Catalog!;
        ZoneLoadResult loaded = WithScenario("zone.egouts_citerne", "egouts_citerne", s =>
        {
            JsonObject r = Combatant(s, "rat_geant_egout2");
            r["health"] = 10;
            r["intervalMs"] = 60_000;
            r["position"] = new JsonArray(-3.0, 7.0);
        }, root => root["data"]!["populations"] = new JsonArray(root["data"]!["populations"]!.AsArray()
            .Where(p => (string)p!["creature"]! == "rat_geant_egout2").Select(p => p!.DeepClone()).ToArray()));
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        foreach (bool full in new[] { false, true })
        {
            SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression)
                .WithItems(full ? Enumerable.Repeat(new ItemStack("item.gambison", 1), always.InventorySize).ToArray() : Array.Empty<ItemStack>(), new Dictionary<string, string>());
            var w = new WorldSession(new WorldSave(loaded.Zone!.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), loaded.Zone, ShippedData, Progression, always);
            var loot = new List<LootGained>();
            for (int i = 0; i < 3_000 && !loot.Any(l => l.Item == "item.anneau_perdu_des_egouts"); i++)
            {
                if (i % 20 == 0 && w.Zone.Engine.CombatantIds.Contains("rat_geant_egout2"))
                    try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", "rat_geant_egout2")); }
                    catch (ArgumentException) { }
                loot.AddRange(w.Step().Loot);
            }
            LootGained ring = Assert.Single(loot, l => l.Item == "item.anneau_perdu_des_egouts");
            Assert.Equal(!full, ring.Kept);
            Assert.Equal(full ? 0 : 1, w.Inventory.Count("item.anneau_perdu_des_egouts"));
            if (full) Assert.Equal(always.InventorySize, w.Inventory.Stacks.Count);
        }
    }

    // --- Relecture indépendante : frontière de la garde des portes (12 m) ------------------------------------------------------

    [Theory]
    [InlineData(12.0, true)]      // un ennemi à 12 m pile : la porte reste gardée (CanUseDoor refuse)
    [InlineData(12.0001, false)]  // juste au-delà : la porte se passe
    public void Frontiere_de_la_garde_des_portes_a_douze_metres(double distance, bool guarded)
    {
        // CanUseDoor (AdventureSession) refuse tant qu'un ennemi vivant est à RestSafety (12 m) ou moins : même appel qu'ici.
        Assert.Equal(12.0, AdventureSession.RestSafety.Raw / 1e4);
        ZoneLoadResult loaded = WithScenario("zone.egouts_citerne", "egouts_citerne",
            s => Combatant(s, "loup_echo_egout1")["position"] = new JsonArray(0.0, -1.0 + distance),
            root => root["data"]!["populations"] = new JsonArray(root["data"]!["populations"]![0]!.DeepClone()));
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var w = new WorldSession(new WorldSave(loaded.Zone!.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero()), loaded.Zone, ShippedData, Progression, Items);
        w.Step();
        Assert.Equal((0.0, -1.0), P(w.Zone.Engine.View(w.CombatantId).Position!.Value));   // au pied de l'échelle
        Assert.Contains("loup_echo_egout1", w.Zone.Engine.CombatantIds);
        Assert.Equal(guarded, CombatDisplay.NearbyEnemies(w.Zone.Engine, w.CombatantId, null, AdventureSession.RestSafety).Any());
    }
}
