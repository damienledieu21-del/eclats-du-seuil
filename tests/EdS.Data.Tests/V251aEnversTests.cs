using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Corruption;
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
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.51a (PLAN_GRANDE_CARTE.md, ligne V2.51) : la couche de l'Envers des Vallons. Une deuxième couche de la région, même grille et
/// mêmes coordonnées : son aire part des obstacles des Vallons (reflet, schéma de zone 17), moins ceux qui n'existent pas dans l'Envers (la
/// porte du moulin), plus les siens (arbres morts, ruines inversées, éclats noirs, autel de l'Aube). Une faille au même point des deux côtés,
/// au nord des terres corrompues ; les reflets (loups, ours) et le reflet du Gardien, chef dans le chemin standard.
/// </summary>
[Trait("Category", "World")]
public sealed class V251aEnversTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v251a_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static ZoneDefinition Vallons => GrandeCarteLieux.Vallons.Value;

    private static ZoneDefinition Envers => V16TestData.Interiors.Single(z => z.Id == "zone.envers_vallons");

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static double Dist((double X, double Z) a, (double X, double Z) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    private static IEnumerable<CombatantSetup> Fighters(ZoneDefinition z) =>
        z.Setup.Combatants.Concat(z.Bosses.SelectMany(b => b.Forms)).Where(c => c is AssailantSetup && c.Placement != null);

    private static readonly (double X, double Z) Rift = (1640, -455);

    // --- Couche en reflet -----------------------------------------------------------------------------------------------

    [Fact]
    public void L_Envers_reflete_la_region_case_par_case()
    {
        ZoneDefinition e = Envers;
        Assert.Equal((ZoneFloor.Envers, true, false, false), (e.Floor, e.Envers, e.Indoor, e.Sheltered));
        Assert.Equal(("zone.vallons", "zones/vallons"), (e.Mirror!.Of, e.Mirror.Scenario));
        Assert.Equal(new[] { "porte_moulin" }, e.Mirror.Removed);
        Assert.Null(Vallons.Mirror);
        // Même grille, mêmes bornes.
        Assert.Equal((Vallons.Region!.OriginX, Vallons.Region.OriginZ, Vallons.Region.Columns, Vallons.Region.Rows),
            (e.Region!.OriginX, e.Region.OriginZ, e.Region.Columns, e.Region.Rows));
        Assert.Equal((Vallons.Setup.Space!.Min, Vallons.Setup.Space.Max), (e.Setup.Space!.Min, e.Setup.Space.Max));
        // Chaque obstacle de la surface est dans l'Envers, au même endroit, sauf la porte du moulin ; puis les siens.
        var surface = Vallons.Setup.Space.Obstacles;
        var envers = e.Setup.Space.Obstacles;
        var kept = surface.Where(o => o.Id != "porte_moulin").ToList();
        Assert.Equal(kept.Select(o => (o.Id, o.Low, string.Join(";", o.Points))), envers.Take(kept.Count).Select(o => (o.Id, o.Low, string.Join(";", o.Points))));
        var own = envers.Skip(kept.Count).Select(o => o.Id).ToList();
        Assert.Equal(10, own.Count);
        Assert.All(own, id => Assert.StartsWith("envers_", id, StringComparison.Ordinal));
        Assert.DoesNotContain(envers, o => o.Id == "porte_moulin");
        // Sa propre population, pas celle de la surface.
        Assert.Equal(new[] { "loup_echo_envers1", "loup_echo_envers2", "loup_echo_envers3", "ours_reflet1" }, e.Populations.Select(p => p.Creature));
        Assert.DoesNotContain(e.Setup.Combatants, c => Vallons.Setup.Combatants.Any(v => v.Id == c.Id));
        Assert.Equal("autel_de_l_aube", e.Entry.Id);
        Assert.Equal(24, e.MinLevelAt(e.Entry.Position));
    }

    [Fact]
    public void Failles_au_meme_point_des_deux_cotes()
    {
        Door up = Vallons.DoorOf("faille_de_l_envers")!, down = Envers.DoorOf("faille_des_vallons")!;
        Assert.Equal(("zone.envers_vallons", "faille_des_vallons", DoorKind.Rift), (up.To, up.ToDoor, up.Kind));
        Assert.Equal(("zone.vallons", "faille_de_l_envers", DoorKind.Rift), (down.To, down.ToDoor, down.Kind));
        Assert.Equal(up.Position, down.Position);
        Assert.Equal(Rift, P(up.Position));
        Assert.Contains(Vallons.Places, p => p.Id == "terres_corrompues" && p.Contains(up.Position));
        // Hors de tout obstacle, des deux côtés.
        foreach (ZoneDefinition z in new[] { Vallons, Envers })
            Assert.DoesNotContain(z.Setup.Space!.Obstacles, o => GroundRelief.PolygonDistance(o.Points.Select(P).ToList(), Rift.X, Rift.Z) < 3);
        Assert.True(ZoneValidator.CheckDoors(new[] { Vallons }.Concat(V16TestData.Interiors).ToList()).IsValid);
    }

    [Fact]
    public void Faille_decalee_ou_porte_ordinaire_entre_les_couches_refusees()
    {
        JsonObject root = ZoneJson("zone.envers_vallons");
        root["data"]!["doors"]![0]!["position"] = new JsonArray(1640.5, -455.0);
        ZoneLoadResult moved = Read(root);
        Assert.True(moved.IsValid, moved.Report.ToString());
        var zones = new[] { Vallons }.Concat(V16TestData.Interiors.Where(z => z.Id != "zone.envers_vallons")).Append(moved.Zone!).ToList();
        ValidationReport r = ZoneValidator.CheckDoors(zones);
        Assert.Contains(r.Errors, x => x.Path == "$.data.doors[0].position" && x.Message.Contains("même point", StringComparison.Ordinal));
    }

    [Fact]
    public void Reflet_mal_forme_refuse_avec_son_chemin()
    {
        static ZoneLoadResult With(Action<JsonObject> edit)
        {
            JsonObject root = ZoneJson("zone.envers_vallons");
            edit(root);
            return Read(root);
        }
        void Refused(ZoneLoadResult r, string path)
        {
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path == path);
        }
        Refused(With(r => r["data"]!["mirror"]!["remove"] = new JsonArray("obstacle_inconnu")), "$.data.mirror.remove[0]");
        Refused(With(r => r["data"]!["mirror"]!["remove"] = new JsonArray("porte_moulin", "porte_moulin")), "$.data.mirror.remove[1]");
        Refused(With(r => r["data"]!["mirror"]!["of"] = "zone.envers_vallons"), "$.data.mirror.of");
        Refused(With(r => r["data"]!.AsObject().Remove("mirror")), "$.data.mirror");
        Refused(With(r => r["data"]!.AsObject().Remove("region")), "$.data.region");
        Refused(With(r => r["data"]!["indoor"] = true), "$.data.indoor");
        Refused(With(r => r["schemaVersion"] = 16), "$.data.floor");
        // Un reflet hors de l'Envers.
        Refused(With(r => r["data"]!["floor"] = "surface"), "$.data.mirror");
        // Un obstacle propre qui reprend l'identifiant d'un obstacle gardé de la surface.
        string dir = CopyScenarios(s => s["data"]!["space"]!["obstacles"]![0]!["id"] = "moulin");
        Refused(Read(ZoneJson("zone.envers_vallons"), dir), "$.data.scenario");
        // Des bornes différentes de celles de la surface.
        dir = CopyScenarios(s => s["data"]!["space"]!["bounds"]!["max"] = new JsonArray(2100, 1458));
        Refused(Read(ZoneJson("zone.envers_vallons"), dir), "$.data.mirror.scenario");
        // Une créature posée dans un obstacle reflété (le moulin) : l'aire composée est refusée.
        dir = CopyScenarios(s => Combatant(s, "loup_echo_envers1")["position"] = new JsonArray(-3.0, 9.0));
        Refused(Read(ZoneJson("zone.envers_vallons"), dir), "$.data.mirror");
        // La zone reflétée doit être celle du scénario reflété.
        JsonObject bad = ZoneJson("zone.envers_vallons");
        bad["data"]!["mirror"]!["scenario"] = "zones/galeries_seuil";
        bad["data"]!["mirror"]!["remove"] = new JsonArray();
        ZoneLoadResult other = Read(bad);
        Assert.False(other.IsValid);   // bornes différentes
    }

    private string CopyScenarios(Action<JsonObject> edit, string scenario = "envers_vallons")
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in new[] { "vallons.json", "envers_vallons.json", "envers_vallons_reflet.json" })
            File.Copy(Path.Combine(Scenarios, "zones", f), Path.Combine(dir, "zones", f));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(s);
        File.WriteAllText(path, s.ToJsonString());
        return dir;
    }

    private static JsonObject Combatant(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    // --- Règles des portes des deux côtés (revues Codex des PR #245, #248, #250 et #256) ---------------------------------

    [Fact]
    public void Creatures_chefs_et_formes_a_8_m_hors_d_aggro_et_hors_de_la_garde_des_deux_cotes()
    {
        double hero = Math.Max(ZoneValidator.CharacterRadius.Raw / 1e4, 0.9);
        double guard = AdventureSession.RestSafety.Raw / 1e4;
        int pairs = 0;
        void Check(string zone, (double X, double Z) door, string id, (double X, double Z) at, double radius, double aggro)
        {
            double centre = Dist(at, door);
            Assert.True(centre >= 8, $"{zone} : {id}");
            Assert.True(centre > guard, $"{zone} : {id} à {centre:0.##} m");
            Assert.True(centre - radius - hero > aggro + 0.5, $"{zone} : {id} de bord à bord");
            pairs++;
        }
        // Côté Envers : chaque créature, chaque forme du chef, à chaque porte.
        foreach (Door d in Envers.Doors)
            foreach (CombatantSetup c in Fighters(Envers))
                Check(Envers.Id, P(d.Position), c.Id, P(c.Placement!.Position), c.Placement.Radius.Raw / 1e4, ((AssailantSetup)c).Behavior!.AggroRange!.Value.Raw / 1e4);
        Assert.Equal(4 + 2, pairs);
        // Côté Vallons : toutes les créatures de la surface, tous les chefs et toutes leurs formes, le gardien de la source (défi V2.42.3).
        pairs = 0;
        foreach (CombatantSetup c in Fighters(Vallons))
            Check(Vallons.Id, Rift, c.Id, P(c.Placement!.Position), c.Placement.Radius.Raw / 1e4, ((AssailantSetup)c).Behavior?.AggroRange?.Raw / 1e4 ?? 0);
        foreach (string file in new[] { "vallons_gardien_source", "vallons_chef", "vallons_bandits", "vallons_croc_gris" })
            foreach (JsonNode? f in JsonNode.Parse(File.ReadAllText(Path.Combine(Scenarios, "zones", file + ".json")))!["data"]!["combatants"]!.AsArray())
                Check(file, Rift, (string)f!["id"]!, ((double)f["position"]![0]!, (double)f["position"]![1]!), (double)f["radius"]!, (double)f["behavior"]!["aggroRange"]!);
        Assert.True(pairs > 100);
        // Les créatures des événements du monde aussi (attaques, failles du Seuil, chasses).
        foreach (WorldEventDefinition ev in Vallons.Events)
            foreach (WorldEventGroup g in ev.Groups)
                foreach (WorldEventCreature c in g.Creatures)
                    Assert.True(Dist(P(c.Position), Rift) > guard + 10, $"{ev.Id} / {c.Like}");
    }

    [Theory]
    [InlineData(1648.0, true)]      // un reflet à 8 m pile de la faille : accepté par le validateur (8 m)
    [InlineData(1647.9999, false)]  // juste en deçà : refusé, au chemin de sa population
    public void Frontiere_huit_metres_a_la_faille_dans_l_Envers(double x, bool valid)
    {
        string dir = CopyScenarios(s => Combatant(s, "loup_echo_envers1")["position"] = new JsonArray(x, -455.0));
        ZoneLoadResult r = Read(ZoneJson("zone.envers_vallons"), dir);
        Assert.Equal(valid, r.IsValid);
        if (!valid)
            Assert.Contains(r.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Message.StartsWith("dans l'Envers", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-447.0, true)]
    [InlineData(-447.0001, false)]
    public void Frontiere_huit_metres_pour_la_seconde_forme_du_reflet(double z, bool valid)
    {
        string dir = CopyScenarios(s => Combatant(s, "loup_echo_reflet_gardien_brise")["position"] = new JsonArray(1640.0, z), "envers_vallons_reflet");
        // La forme posée près de la faille sort du petit espace du scénario du chef : on l'élargit.
        string path = Path.Combine(dir, "zones", "envers_vallons_reflet.json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        s["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(1470, -570), ["max"] = new JsonArray(1660, -430) };
        File.WriteAllText(path, s.ToJsonString());
        ZoneLoadResult r = Read(ZoneJson("zone.envers_vallons"), dir);
        if (valid) Assert.DoesNotContain(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]");
        else Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("porte « faille_des_vallons »", StringComparison.Ordinal));
    }

    [Fact]
    public void A_la_surface_une_creature_a_moins_de_8_m_d_une_faille_refusee()
    {
        // Côté Vallons, la règle des 8 m vaut près d'une faille (pas près des autres portes de la surface).
        // La faille posée à 7,9 m d'une créature peuplée (le premier côté libre d'obstacle) : refusée.
        foreach (Population pop in Vallons.Populations.Where(p => p.Template.Placement != null).Take(12))
            foreach ((double dx, double dz) in new[] { (7.9, 0.0), (-7.9, 0.0), (0.0, 7.9), (0.0, -7.9) })
            {
                JsonObject root = ZoneJson("zone.vallons");
                JsonObject door = root["data"]!["doors"]!.AsArray().Single(d => (string)d!["id"]! == "faille_de_l_envers")!.AsObject();
                (double x, double z) = P(pop.Template.Placement!.Position);
                door["position"] = new JsonArray(Math.Round(x + dx, 4), Math.Round(z + dz, 4));
                ZoneLoadResult r = Read(root);
                if (r.Report.Errors.Any(e => e.Path.StartsWith("$.data.doors", StringComparison.Ordinal))) continue;   // dans un obstacle : côté suivant
                Assert.Contains(r.Report.Errors, e => e.Message.StartsWith("près d'une faille", StringComparison.Ordinal) && e.Message.Contains(pop.Creature, StringComparison.Ordinal));
                return;
            }
        Assert.Fail("aucune place libre pour l'essai");
    }

    [Theory]
    [InlineData(12.0, true)]
    [InlineData(12.0001, false)]
    public void Frontiere_de_la_garde_de_la_faille_a_douze_metres(double distance, bool guarded)
    {
        string dir = CopyScenarios(s => Combatant(s, "loup_echo_envers1")["position"] = new JsonArray(1656.0, -470.0 - distance));
        JsonObject root = ZoneJson("zone.envers_vallons");
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var w = new WorldSession(new WorldSave(loaded.Zone!.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero()), loaded.Zone, ShippedData, Progression, Items);
        w.Step();
        Assert.Equal((1656.0, -470.0), P(w.Zone.Engine.View(w.CombatantId).Position!.Value));   // à l'autel de l'Aube
        Assert.Equal(guarded, CombatDisplay.NearbyEnemies(w.Zone.Engine, w.CombatantId, null, AdventureSession.RestSafety).Any());
    }

    // --- Créatures, trêves, chefs, apparences ---------------------------------------------------------------------------

    [Fact]
    public void Reflets_et_ombres_en_treve()
    {
        Assert.Equal(new[] { "ombres", "reflets" }, Fighters(Envers).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(new[] { ("reflets", "ombres") }, Envers.Setup.Truces.Select(t => (t.A, t.B)));
    }

    [Fact]
    public void Chef_dans_le_chemin_standard()
    {
        Assert.Null(Envers.Boss);
        Boss reflet = Assert.Single(Envers.Chiefs);
        Assert.Equal(("reflet_du_gardien", "fact.reflet_du_gardien.vaincu", "loot.echo"), (reflet.Id, reflet.Fact, reflet.Loot));
        Assert.Equal(new[] { "loup_echo_reflet_gardien", "loup_echo_reflet_gardien_brise" }, reflet.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 4) }, reflet.Rewards);
        Assert.Equal("zone.envers_vallons/boss/reflet_du_gardien", Envers.VictoryKeyOf(reflet));
        var facts = Vallons.Chiefs.Select(c => c.Fact).Concat(V16TestData.Interiors.SelectMany(z => z.Chiefs).Select(c => c.Fact)).ToList();
        Assert.Equal(facts.Count, facts.Distinct().Count());
    }

    [Fact]
    public void Chef_recompense_a_la_premiere_victoire_seulement()
    {
        string dir = CopyScenarios(g =>
        {
            string[] forms = { "loup_echo_reflet_gardien", "loup_echo_reflet_gardien_brise" };
            for (int i = 0; i < forms.Length; i++)
            {
                JsonObject f = Combatant(g, forms[i]);
                f["health"] = 10;
                f["intervalMs"] = 60_000;
                foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
                f["position"] = new JsonArray(1656.0 + 3 * i, -460.0);   // près de l'autel, à plus de 8 m de la faille
            }
            g["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(1630, -480), ["max"] = new JsonArray(1680, -440) };
        }, "envers_vallons_reflet");
        JsonObject root = ZoneJson("zone.envers_vallons");
        root["data"]!["populations"] = new JsonArray();
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 20));
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, Items);
        List<SessionStep> first = Fight(w, "loup_echo_reflet_gardien");
        Assert.Contains(first.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Equal(new[] { ("item.eclat_de_seuil", 4) }, first.SelectMany(s => s.Rewards));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains("zone.envers_vallons/boss/reflet_du_gardien", saved.Consumed);
        Assert.Contains("fact.reflet_du_gardien.vaincu", SaveJsonReader.Write(w.Save()), StringComparison.Ordinal);
        var again = new WorldSession(saved, zone, ShippedData, Progression, Items);
        List<SessionStep> second = Fight(again, "loup_echo_reflet_gardien");
        Assert.Contains(second.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Empty(second.SelectMany(s => s.Rewards));
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

    [Fact]
    public void Apparences_et_noms_des_reflets()
    {
        var ids = Fighters(Envers).Select(c => c.Id).ToList();
        Assert.Equal(6, ids.Count);
        foreach (string id in ids.Concat(new[] { "loup_echo_envers2_v3", "ours_reflet1_v2" }))
        {
            bool bear = id.StartsWith("ours", StringComparison.Ordinal);
            Assert.Equal(bear ? StyleSlot.Bear : StyleSlot.Wolf, StyleRules.ForCombatant(id, false));
            Assert.Equal(EchoKind.Rift, EchoLooks.Of(id));
            Assert.True(EchoLooks.FromEnvers(id), id);
            Assert.Equal(0, StyleRules.Cubs(id));
            Assert.Equal(bear ? "ours" : "loup", StyleRules.CharacterOf(id));
        }
        Assert.Equal(new[] { "Reflet de loup", "Reflet de loup", "Reflet de loup", "Ombre d'ours", "Reflet du Gardien", "Reflet du Gardien brisé" },
            ids.Select(DisplayNames.Of));
        Assert.Equal("Reflet de loup", DisplayNames.Of("loup_echo_envers3_v2"));
        // Les créatures d'avant ne changent pas.
        Assert.False(EchoLooks.FromEnvers("loup_echo_galerie1"));
        Assert.Equal(EchoKind.Night, EchoLooks.Of("loup_echo_mine1"));
        Assert.Equal(EchoKind.None, EchoLooks.Of("ours_galerie1"));
    }

    // --- Voyage, monture, corruption ------------------------------------------------------------------------------------

    [Fact]
    public void Le_voyage_rapide_ne_mene_jamais_dans_l_Envers()
    {
        var w = new WorldSession(new WorldSave(Envers.Id, 0, 0, Array.Empty<string>(), V11TestData.Hero()), Envers, ShippedData, Progression, Items);
        w.Step();
        Assert.True(w.IsDiscovered(Envers, "autel_de_l_aube"));
        Assert.Equal(TravelRefusal.UnknownPlace, w.CanTravel(Envers, "autel_de_l_aube"));
    }

    [Fact]
    public void L_Envers_reflete_la_corruption_de_la_surface()
    {
        CorruptionBook book = CorruptionJsonReader.Read(File.ReadAllText(CorruptionJsonReader.PathIn(DataDirectory)), Vallons, V11TestData.ItemsCatalog,
            V11TestData.ScenariosDirectory, ShippedData).Book!;
        long[] since = CorruptionRules.CorruptedSince(book, SavedCorruption.Empty);
        ZoneDefinition strong = CorruptionRules.Strengthen(Envers, book, SavedCorruption.Empty, 9);
        Assert.NotSame(Envers, strong);
        int changed = 0;
        foreach (AssailantSetup a in strong.Setup.Combatants.OfType<AssailantSetup>())
        {
            AssailantSetup old = Envers.Setup.Combatants.OfType<AssailantSetup>().Single(x => x.Id == a.Id);
            long percent = CorruptionRules.StrengthAt(book, since, 9, old.Placement!.Position);
            Assert.Equal(percent == 100 ? old.Health : ZoneScaling.Strengthen(old, percent).Health, a.Health);
            if (percent > 100) changed++;
        }
        Assert.True(changed >= 1, $"{changed} reflets renforcés");   // le reflet le plus proche de la faille du nord, au jour 9
        // Une autre zone (un dessous) ne change pas.
        ZoneDefinition cave = V16TestData.Interiors.First(z => z.Underground);
        Assert.Same(cave, CorruptionRules.Strengthen(cave, book, SavedCorruption.Empty, 9));
    }

    // --- De bout en bout : par la faille, dans l'Envers, et retour -------------------------------------------------------

    [Fact]
    public void Par_la_faille_dans_l_Envers_et_retour_au_meme_point()
    {
        // Données copiées : l'autel de l'Aube posé à 1,5 m de la faille (à portée).
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (Scenarios, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        string p = ZoneJsonReader.PathIn(data, "zone.envers_vallons");
        JsonObject root = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(1640.0, -453.5);
        File.WriteAllText(p, root.ToJsonString());
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.envers_vallons", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = V11TestData.Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.True(a.Map.Envers);
        Assert.Equal("faille_des_vallons", a.DoorInReach!.Id);
        Assert.True(a.CanUseDoor, a.DoorHint);
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()));
        Assert.Equal("zone.vallons", a.World.Definition!.Id);
        Assert.False(a.Map.Envers);
        a.Advance(0.05);
        Assert.Equal(Rift, P(a.World.Zone.Engine.View(a.World.CombatantId).Position!.Value));   // au même point
        Assert.Equal("faille_de_l_envers", a.DoorInReach!.Id);
        Assert.True(a.CanUseDoor, a.DoorHint);
        Assert.True(a.UseDoor(), string.Join(" | ", a.DrainMessages()));
        Assert.Equal("zone.envers_vallons", a.World.Definition!.Id);
        a.Advance(0.05);
        Assert.Equal(Rift, P(a.World.Zone.Engine.View(a.World.CombatantId).Position!.Value));
        Assert.True(a.Map.Envers);
        // La sauvegarde est dans l'Envers, la reprise à l'autel de l'Aube (point sûr de l'Envers le plus proche).
        WorldSave written = SaveStore.Read(save).Save!;
        Assert.Equal("zone.envers_vallons", written.Zone);
        Assert.Equal("autel_de_l_aube", written.Resume?.SafePoint);
    }
}
