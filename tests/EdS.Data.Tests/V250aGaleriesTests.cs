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
/// Chantier V2.50a (PLAN_GRANDE_CARTE.md, ligne V2.50 ; « a » pour ne pas confondre avec les grottes noyées du V2.50.1) : les galeries sous
/// les pierres du Seuil, deux dessous. Les galeries cristallines (gouffre franchi par deux ponts de pierre, éclats géants, loups-échos de la
/// faille, ours cristallisé, ours des éclats), entrées par une porte au nord du cercle des pierres levées ; la salle des éclats géants en
/// dessous (faille, genre « faille »), son autel et le veilleur des éclats. Chefs dans le chemin standard (<c>chiefs</c>).
/// </summary>
[Trait("Category", "World")]
public sealed class V250aGaleriesTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v250a_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    public static readonly string[] Galleries = { "zone.galeries_seuil", "zone.galeries_eclats" };

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition[]> All = new(() => Galleries.Select(id =>
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, id + " : " + r.Report);
        return r.Zone!;
    }).ToArray());

    private static ZoneDefinition Gallery(int i) => All.Value[i];

    private static IEnumerable<ZoneDefinition> AllZones() => new[] { Vallons.Value }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.vallons"));

    private static (double X, double Z) Min(ZoneDefinition z) => (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);

    private static (double X, double Z) Max(ZoneDefinition z) => (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);

    private static IEnumerable<CombatantSetup> Fighters(ZoneDefinition z) => z.Setup.Combatants.Concat(z.Chiefs.SelectMany(b => b.Forms));

    private static double Dist((double X, double Z) a, (double X, double Z) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    // --- Données ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Deux_niveaux_sous_les_pierres_levees()
    {
        ZoneDefinition[] g = All.Value;
        Assert.All(g, z => Assert.True(z.Underground && z.Sheltered && !z.Indoor && z.SchemaVersion == 15 && z.Region is null, z.Id));
        Assert.Equal(new[] { ("sortie", "zone.vallons", "porte_galeries_seuil", DoorKind.Door), ("faille_des_eclats", "zone.galeries_eclats", "faille", DoorKind.Rift) },
            g[0].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.Equal(new[] { ("faille", "zone.galeries_seuil", "faille_des_eclats", DoorKind.Rift) }, g[1].Doors.Select(d => (d.Id, d.To, d.ToDoor, d.Kind)));
        Assert.True(ZoneValidator.CheckDoors(AllZones().ToArray()).IsValid);
        foreach (string id in Galleries) Assert.Contains(V16TestData.Interiors, z => z.Id == id);
        Assert.Equal(new[] { "Galeries du Seuil : galeries cristallines", "Galeries du Seuil : salle des éclats géants" }, g.Select(z => z.Name));
        // Sous le cercle des pierres levées (niveau 22) : de 22 à 24, au-dessus du fond de la mine et du tombeau de l'abbé.
        Assert.Equal(new[] { 22, 23, 23, 24 }, g.SelectMany(z => z.Places).Select(p => p.MinLevel));
        Assert.Equal(new[] { "sous_les_pierres_levees", "bord_de_la_faille" }, g.Select(z => z.SafePoints.Single().Id));
        Assert.All(g, z => Assert.Equal(z.Entry.Id, z.SafePoints[0].Id));
        Assert.DoesNotContain(g.SelectMany(z => z.Doors), AdventureSession.IsSecretDoor);
    }

    [Fact]
    public void Kit_des_galeries_cristallines_dans_les_donnees()
    {
        // Pièces du kit reconnues au nom des obstacles (rendu Unity au V2.50b) ; le gouffre, les cristaux, les éboulis et l'autel sont bas (on
        // tire par-dessus) ; parois, éclats géants et piliers non.
        var o1 = Gallery(0).Setup.Space!.Obstacles;
        var o2 = Gallery(1).Setup.Space!.Obstacles;
        foreach (string part in new[] { "paroi_cristal", "gouffre_cristal", "eclat_geant", "pilier_cristal", "amas_cristal", "eboulis_cristal" })
            Assert.Contains(o1, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        foreach (string part in new[] { "paroi_cristal", "eclat_geant", "pilier_cristal", "amas_cristal", "autel_eclats" })
            Assert.Contains(o2, o => o.Id.StartsWith(part, StringComparison.Ordinal));
        string[] low = { "gouffre_cristal", "amas_cristal", "eboulis_cristal", "autel_eclats" };
        string[] high = { "paroi_cristal", "eclat_geant", "pilier_cristal" };
        Assert.All(o1.Concat(o2), o => Assert.Equal(low.Any(p => o.Id.StartsWith(p, StringComparison.Ordinal)), o.Low));
        Assert.All(o1.Concat(o2), o => Assert.True(low.Concat(high).Any(p => o.Id.StartsWith(p, StringComparison.Ordinal)), o.Id));
        // Deux ponts de pierre de 4 m sur le gouffre : ses trois tronçons laissent x de −18 à −14 et de 14 à 18.
        var gouffre = o1.Where(o => o.Id.StartsWith("gouffre_cristal", StringComparison.Ordinal)).Select(o => (o.Points.Min(p => P(p).X), o.Points.Max(p => P(p).X))).OrderBy(c => c.Item1).ToList();
        Assert.Equal(new[] { (-28.0, -18.0), (-14.0, 14.0), (18.0, 28.0) }, gouffre);
        Assert.Equal(4, o2.Count(o => o.Id.StartsWith("eclat_geant_salle", StringComparison.Ordinal)));
    }

    [Fact]
    public void Porte_au_nord_du_cercle_des_pierres_levees_a_pied()
    {
        ZoneDefinition vallons = Vallons.Value;
        Door porte = vallons.DoorOf("porte_galeries_seuil")!;
        Assert.Equal(("zone.galeries_seuil", "sortie", DoorKind.Door, "Galeries sous les pierres"), (porte.To, porte.ToDoor, porte.Kind, porte.Name));
        Assert.Null(porte.Building);
        (double x, double z) = P(porte.Position);
        Place cercle = vallons.Places.Single(p => p.Id == "cercle_des_pierres_levees");
        Assert.InRange(x, P(cercle.Min).X, P(cercle.Max).X);
        Assert.InRange(z, P(cercle.Min).Z, P(cercle.Max).Z);
        Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), x, z) < 0.5);
        // De la lisière des terres grises à la porte, à pied.
        Assert.True(Walks((1280, -700), (1620, -540), SafePoint("lisiere_des_terres_grises"), (x, z)));
    }

    [Fact]
    public void Aucun_ennemi_des_Vallons_ni_gardien_de_la_source_a_moins_de_12_m_de_l_entree()
    {
        ZoneDefinition vallons = Vallons.Value;
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        (double X, double Z) door = P(vallons.DoorOf("porte_galeries_seuil")!.Position);
        foreach (CombatantSetup c in Fighters(vallons).Where(c => c is AssailantSetup && c.Placement != null))
            Assert.True(Dist(P(c.Placement!.Position), door) > guard, c.Id);
        // Le gardien du défi de la source (V2.42.3) se lance au cercle : ses trois formes aussi, avec leur aggro de bord à bord.
        JsonObject s = JsonNode.Parse(File.ReadAllText(Path.Combine(Scenarios, "zones", "vallons_gardien_source.json")))!.AsObject();
        double hero = Math.Max(ZoneValidator.CharacterRadius.Raw / 1e4, 0.9);
        int forms = 0;
        foreach (JsonNode? f in s["data"]!["combatants"]!.AsArray())
        {
            double centre = Dist(((double)f!["position"]![0]!, (double)f["position"]![1]!), door);
            Assert.True(centre > guard, (string)f["id"]!);
            Assert.True(centre - (double)f["radius"]! - hero > (double)f["behavior"]!["aggroRange"]! + 0.5, (string)f["id"]!);
            forms++;
        }
        Assert.Equal(3, forms);
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
    public void Le_gouffre_ne_se_passe_que_par_les_ponts()
    {
        // Sans les deux ponts (trous du gouffre bouchés), la rive des éclats et la faille ne s'atteignent plus depuis l'entrée.
        ZoneDefinition z = Gallery(0);
        var closed = z.Setup.Space!.Obstacles.ToList();
        closed.Add(new SpaceObstacle("gouffre_essai_ouest", new[] { GV(-18, 18), GV(-14, 18), GV(-14, 21), GV(-18, 21) }, true));
        closed.Add(new SpaceObstacle("gouffre_essai_est", new[] { GV(14, 18), GV(18, 18), GV(18, 21), GV(14, 21) }, true));
        var flood = V228cSurfaceTests.Flood(closed, Min(z), Max(z), P(z.SafePoints[0].Position), 0.5);
        Assert.False(V228cSurfaceTests.Reaches(flood.Reached, Min(z), P(z.DoorOf("faille_des_eclats")!.Position), 0.5));
    }

    private static GroundVector GV(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 1e4)), Fixed.FromRaw((long)Math.Round(z * 1e4)));

    [Fact]
    public void Chefs_dans_le_chemin_standard_et_recompenses()
    {
        Boss ours = Assert.Single(Gallery(0).Chiefs), veilleur = Assert.Single(Gallery(1).Chiefs);
        Assert.Null(Gallery(0).Boss);
        Assert.Null(Gallery(1).Boss);
        Assert.Equal(("ours_des_eclats", "fact.ours_des_eclats.vaincu", "loot.ours"), (ours.Id, ours.Fact, ours.Loot));
        Assert.Equal(new[] { "ours_cristal_eclats", "ours_cristal_eclats_herisse" }, ours.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 2) }, ours.Rewards);
        Assert.Equal(("veilleur_des_eclats", "fact.veilleur_des_eclats.vaincu", "loot.echo"), (veilleur.Id, veilleur.Fact, veilleur.Loot));
        Assert.Equal(new[] { "loup_echo_veilleur_eclats", "loup_echo_veilleur_eclats_fele", "loup_echo_veilleur_eclats_dechaine" }, veilleur.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.larme_du_seuil", 1), ("item.eclat_de_seuil", 3) }, veilleur.Rewards);

        ItemDefinition larme = Items.Item("item.larme_du_seuil");
        Assert.Equal((ItemType.Equipment, "relic", 20, ItemRarity.Epic, true, "Larme du Seuil"), (larme.Type, larme.Slot, larme.RequiredLevel, larme.Rarity, larme.Named, larme.Name));
        Assert.Equal((60, 110, 20), (larme.Bonus.MagicResistance, larme.Bonus.MaxHealth, larme.Bonus.StatusResistance));
        Assert.Equal(2400, V11TestData.ShippedMerchants.Values["item.larme_du_seuil"]);
        // Le plafond du catalogue reste le niveau 20 (décision de Damien du 22 novembre 2026 pour le cœur de cristal).
        Assert.Equal(20, Items.Items.Max(i => i.RequiredLevel));
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
                    double centre = Dist(P(c.Placement!.Position), P(d.Position));
                    Assert.True(centre >= 8, $"{z.Id} / {d.Id} : {c.Id}");
                    Assert.True(centre > guard, $"{z.Id} / {d.Id} : {c.Id} à {centre:0.##} m");
                    Fixed aggro = Assert.IsType<AssailantSetup>(c).Behavior!.AggroRange!.Value;
                    double edge = centre - c.Placement.Radius.Raw / 1e4 - hero;
                    Assert.True(edge > aggro.Raw / 1e4 + 0.5, $"{z.Id} / {d.Id} : {c.Id} à {edge:0.###} m de bord à bord");
                    pairs++;
                }
        Assert.Equal((3 + 1 + 2) * 2 + (3 + 1 + 3) * 1, pairs);   // galeries : 3 échos, un ours, 2 formes, 2 portes ; salle : 3 échos, un ours, 3 formes, 1 porte
    }

    private ZoneLoadResult WithScenario(string zone, string scenario, Action<JsonObject> edit, Action<JsonObject>? editZone = null)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "galeries_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
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
    [InlineData(5.5, true)]       // un loup-écho à 8 m pile de la faille (0 ; −2,5) : accepté
    [InlineData(5.4999, false)]   // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_a_la_faille(double z, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.galeries_eclats", "galeries_eclats", s => Combatant(s, "loup_echo_galerie4")["position"] = new JsonArray(0.0, z));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.Contains("porte « faille »", StringComparison.Ordinal));
    }

    /// <summary>La troisième forme du veilleur aussi : à 8 m pile de la faille (0 ; −2,5), acceptée ; juste en deçà, refusée.</summary>
    [Theory]
    [InlineData(5.5, true)]
    [InlineData(5.4999, false)]
    public void Frontiere_huit_metres_pour_la_derniere_forme_du_veilleur(double z, bool valid)
    {
        ZoneLoadResult r = WithScenario("zone.galeries_eclats", "galeries_eclats_veilleur", s => Combatant(s, "loup_echo_veilleur_eclats_dechaine")["position"] = new JsonArray(0.0, z));
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[2]" && e.Message.Contains("porte « faille »", StringComparison.Ordinal));
        else
            Assert.True(r.IsValid, r.Report.ToString());
    }

    [Fact]
    public void Seconde_forme_de_l_ours_posee_sur_la_faille_des_eclats_refusee()
    {
        ZoneLoadResult r = WithScenario("zone.galeries_seuil", "galeries_seuil_ours", s => Combatant(s, "ours_cristal_eclats_herisse")["position"] = new JsonArray(20.0, 35.0));
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("porte « faille_des_eclats »", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("escalier")]
    [InlineData("puits")]
    [InlineData("porte")]
    public void Faille_de_genres_differents_refusee(string kind)
    {
        JsonObject salle = ZoneJson("zone.galeries_eclats");
        salle["data"]!["doors"]![0]!["kind"] = kind;
        ZoneLoadResult r = Read(salle);
        Assert.True(r.IsValid, r.Report.ToString());
        ValidationReport report = ZoneValidator.CheckDoors(AllZones().Where(z => z.Id != "zone.galeries_eclats").Append(r.Zone!).ToArray());
        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Path.EndsWith(".kind", StringComparison.Ordinal));
    }

    [Fact]
    public void Galeries_marquees_interieur_refusees()
    {
        JsonObject zone = ZoneJson("zone.galeries_seuil");
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    /// <summary>Chaque chef des galeries ne donne sa récompense qu'à la première victoire ; la seconde, après relecture de la sauvegarde, ne donne rien.</summary>
    [Theory]
    [InlineData("zone.galeries_seuil", "galeries_seuil_ours", "ours_cristal_eclats,ours_cristal_eclats_herisse", "ours_des_eclats", "fact.ours_des_eclats.vaincu", "item.eclat_de_seuil:2", -3.0, 7.0)]
    [InlineData("zone.galeries_eclats", "galeries_eclats_veilleur", "loup_echo_veilleur_eclats,loup_echo_veilleur_eclats_fele,loup_echo_veilleur_eclats_dechaine", "veilleur_des_eclats", "fact.veilleur_des_eclats.vaincu", "item.larme_du_seuil:1,item.eclat_de_seuil:3", -3.0, 7.0)]
    public void Chef_recompense_a_la_premiere_victoire_seulement(string zoneId, string scenario, string formList, string chief, string fact, string rewardList, double x0, double z0)
    {
        string dir = Path.Combine(_folder, "scenarios_" + chief);
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "galeries_*.json")) File.Copy(f, Path.Combine(dir, "zones", Path.GetFileName(f)));
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
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 20));
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
        foreach ((string item, int count) in rewards) Assert.Equal(count, again.Inventory.Count(item));
    }

    private static List<SessionStep> Fight(WorldSession w, string prefix, int max = 8000)
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
    /// Apparences : les loups-échos des galeries et le veilleur des éclats prennent le modèle de loup avec le rendu de faille (violet : ils
    /// sortent de la source) ; l'ours cristallisé et l'ours des éclats, le modèle d'ours, sans rendu d'écho. Chaque nom affiché est écrit.
    /// </summary>
    [Fact]
    public void Apparences_et_noms_des_creatures_des_galeries()
    {
        var all = All.Value.SelectMany(Fighters).Select(c => c.Id).ToList();
        var wolves = all.Where(id => id.StartsWith("loup_echo", StringComparison.Ordinal)).ToList();
        var bears = all.Where(id => id.StartsWith("ours", StringComparison.Ordinal)).ToList();
        Assert.Equal(all.Count, wolves.Count + bears.Count);
        Assert.Equal(6 + 3, wolves.Count);
        Assert.Equal(2 + 2, bears.Count);
        foreach (string id in wolves.Concat(new[] { "loup_echo_galerie2_v3", "loup_echo_veilleur_eclats_fele_v2" }))
        {
            Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant(id, false));
            Assert.Equal(EchoKind.Rift, EchoLooks.Of(id));
            Assert.True(EchoLooks.FromSeuilDepths(id), id);
            Assert.Equal(0, StyleRules.Cubs(id));
        }
        foreach (string id in bears)
        {
            Assert.Equal(StyleSlot.Bear, StyleRules.ForCombatant(id, false));
            Assert.Equal(EchoKind.None, EchoLooks.Of(id));
            Assert.Equal(0, StyleRules.Cubs(id));
        }
        Assert.Equal(new[] { "Loup-écho des galeries", "Loup-écho des galeries", "Loup-écho des galeries", "Ours cristallisé", "Ours des éclats", "Ours des éclats hérissé",
                "Loup-écho des galeries", "Loup-écho des galeries", "Loup-écho des galeries", "Ours cristallisé", "Veilleur des éclats", "Veilleur des éclats fêlé", "Veilleur des éclats déchaîné" },
            all.Select(DisplayNames.Of));
        Assert.Equal("Loup-écho des galeries", DisplayNames.Of("loup_echo_galerie5_v2"));
        Assert.Equal("Lance d'éclat", DisplayNames.Attack("lance_d_eclat"));
        Assert.Equal("Chant du Seuil", DisplayNames.Attack("chant_du_seuil"));
        Assert.Equal("Pluie d'éclats", DisplayNames.Attack("pluie_d_eclats"));
        // Les créatures d'avant ne changent pas : les échos des mines restent des échos de la nuit, même dans leurs « galeries ».
        Assert.Equal(EchoKind.Night, EchoLooks.Of("loup_echo_mine1"));
        Assert.Equal(EchoKind.Night, EchoLooks.Of("loup_echo_gardien_cristaux"));
        Assert.False(EchoLooks.FromSeuilDepths("loup_echo_mine_galerie1"));
        Assert.Equal(EchoKind.Rift, EchoLooks.Of("loup_echo1_ev4"));
        Assert.Equal("Ours des galeries", DisplayNames.Of("ours_mine1"));
    }

    [Fact]
    public void Echos_et_ours_en_treve_sur_chaque_niveau()
    {
        // Revue Codex de la PR #245 : deux équipes ennemies d'une même zone demandent une trêve.
        foreach (ZoneDefinition z in All.Value)
        {
            Assert.Equal(new[] { "echos", "meute" }, Fighters(z).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
            Assert.Equal(new[] { ("echos", "meute") }, z.Setup.Truces.Select(t => (t.A, t.B)));
        }
    }

    [Fact]
    public void Dans_les_galeries_ni_cheval_ni_voyage_rapide_vers_elles()
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

    // --- Garde des portes (12 m) : CanUseDoor refuse tant qu'un ennemi vivant est à 12 m ou moins -------------------------------

    [Theory]
    [InlineData(12.0, true)]      // un ennemi à 12 m pile : la porte reste gardée
    [InlineData(12.0001, false)]  // juste au-delà : la porte se passe
    public void Frontiere_de_la_garde_des_portes_a_douze_metres(double distance, bool guarded)
    {
        Assert.Equal(12.0, AdventureSession.RestSafety.Raw / 1e4);
        ZoneLoadResult loaded = WithScenario("zone.galeries_eclats", "galeries_eclats",
            s => Combatant(s, "loup_echo_galerie4")["position"] = new JsonArray(0.0, -1.0 + distance),
            root => root["data"]!["populations"] = new JsonArray(root["data"]!["populations"]![0]!.DeepClone()));
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var w = new WorldSession(new WorldSave(loaded.Zone!.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero()), loaded.Zone, ShippedData, Progression, Items);
        w.Step();
        Assert.Equal((0.0, -1.0), P(w.Zone.Engine.View(w.CombatantId).Position!.Value));   // au bord de la faille
        Assert.Contains("loup_echo_galerie4", w.Zone.Engine.CombatantIds);
        Assert.Equal(guarded, CombatDisplay.NearbyEnemies(w.Zone.Engine, w.CombatantId, null, AdventureSession.RestSafety).Any());
    }
}
