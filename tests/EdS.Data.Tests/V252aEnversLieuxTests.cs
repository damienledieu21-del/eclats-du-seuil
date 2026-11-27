using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.52a (PLAN_GRANDE_CARTE.md, ligne V2.52) : le contenu de l'Envers des douze lieux. Chaque grand lieu des Vallons a sa version
/// inversée dans <c>zone.envers_vallons</c> : un lieu au niveau de l'Envers, ses pièces (dont celles du palier 2 du kit de la V2.51), ses
/// reflets, au moins un secret (coffre ou stèle, schéma de zone 18) et des failles de plus, au même point des deux côtés ; trois chefs de plus
/// (forêt, fort, abbaye) dans le chemin standard <c>chiefs</c>, récompensés à la première victoire.
/// </summary>
[Trait("Category", "World")]
public sealed class V252aEnversLieuxTests : IDisposable
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v252a_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemCatalog Items => V11TestData.ItemsCatalog;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static ZoneDefinition Vallons => GrandeCarteLieux.Vallons.Value;

    private static ZoneDefinition Envers => V16TestData.Interiors.Single(z => z.Id == "zone.envers_vallons");

    private static JsonObject ZoneJson(string id) => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root, string? scenarios = null) => ZoneJsonReader.Read(root.ToJsonString(), scenarios ?? Scenarios, ShippedData, Items);

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static double Dist((double X, double Z) a, (double X, double Z) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    private static IEnumerable<AssailantSetup> Fighters(ZoneDefinition z) =>
        z.Setup.Combatants.Concat(z.Bosses.SelectMany(b => b.Forms)).OfType<AssailantSetup>().Where(c => c.Placement != null);

    /// <summary>Les douze lieux de l'Envers (les trois passages comptés à part), avec leur niveau.</summary>
    public static readonly (string Id, int Level)[] Lieux =
    {
        ("envers_du_village", 24), ("envers_du_bourg", 24), ("envers_de_la_vallee", 25), ("envers_des_coteaux", 26), ("envers_de_la_foret", 26),
        ("envers_des_collines", 27), ("envers_du_fort", 28), ("envers_de_la_carriere", 27), ("envers_de_l_abbaye", 27), ("envers_du_lac", 26),
        ("envers_des_terres_corrompues", 24), ("envers_du_pont_des_murmures", 27), ("envers_du_col", 28), ("envers_de_la_route_d_ambre", 25),
    };

    private static Place PlaceOf(string id) => Envers.Places.Single(p => p.Id == id);

    // --- Données valides, lieux, niveaux -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("zone.envers_vallons")]
    [InlineData("zone.vallons")]
    public void Zones_valides_l_Envers_en_schema_18(string id)
    {
        ZoneLoadResult r = Read(ZoneJson(id));
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(id == "zone.envers_vallons" ? 18 : 17, r.Zone!.SchemaVersion);   // les Vallons restent au schéma 17 (sans secrets)
        Assert.True(ZoneValidator.CheckDoors(new[] { Vallons }.Concat(V16TestData.Interiors).ToList()).IsValid);
    }

    [Fact]
    public void Les_douze_lieux_ont_leur_version_inversee_au_niveau_de_l_Envers()
    {
        foreach ((string id, int level) in Lieux)
        {
            Place place = PlaceOf(id);
            Assert.Equal(level, place.MinLevel);
            Assert.InRange(place.MinLevel, 24, Progression.MaxLevel);   // au-delà du Gardien du Seuil (23), comme la première faille
            // Ses reflets, au moins un, tous au niveau de l'Envers.
            var inside = Envers.Populations.Where(p => p.Template.Placement is CombatantPlacement c && place.Contains(c.Position)).ToList();
            Assert.True(inside.Count >= 1, id);
            Assert.All(inside, p => Assert.True(Envers.MinLevelAt(p.Template.Placement!.Position) >= 24, p.Creature));
            // Au moins un secret dans son emprise.
            Assert.Contains(Envers.Secrets, s => place.Contains(s.Position));
        }
        // L'ordre des niveaux de la surface est gardé (retenu par Damien le 27 novembre 2026) : l'Envers d'un lieu plus dur à la surface
        // n'est jamais plus facile, sauf l'entrée de la V2.51 (les terres corrompues, à 24).
        (string Envers, string Surface)[] pairs =
        {
            ("envers_du_bourg", "ville_du_bourg"), ("envers_de_la_vallee", "vallee_de_la_riviere"), ("envers_des_coteaux", "coteaux_du_chateau"),
            ("envers_de_la_foret", "foret_des_loups_profonde"), ("envers_des_collines", "hautes_collines"), ("envers_du_fort", "fort_des_bandits"),
            ("envers_de_la_carriere", "grande_carriere"), ("envers_de_l_abbaye", "abbaye_en_ruine"), ("envers_du_lac", "grand_lac"),
            ("envers_de_la_route_d_ambre", "route_d_havre_d_ambre"), ("envers_du_col", "route_du_col"),
        };
        foreach (var a in pairs)
            foreach (var b in pairs)
                if (Vallons.Places.Single(p => p.Id == a.Surface).MinLevel < Vallons.Places.Single(p => p.Id == b.Surface).MinLevel)
                    Assert.True(PlaceOf(a.Envers).MinLevel <= PlaceOf(b.Envers).MinLevel, $"{a.Envers} / {b.Envers}");
    }

    [Fact]
    public void Pieces_des_lieux_et_du_palier_2_posees()
    {
        var own = Envers.Setup.Space!.Obstacles.Where(o => o.Id.StartsWith("envers_", StringComparison.Ordinal)).Select(o => o.Id).ToList();
        foreach (string prefix in new[] { "envers_statue_inversee_", "envers_maison_effondree_", "envers_pierre_levee_fendue_", "envers_brasier_violet_",
                     "envers_moulin_renverse_", "envers_clocher_renverse_", "envers_cascade_figee_", "envers_tonneaux_renverses_", "envers_cairn_suspendu_",
                     "envers_palissade_calcinee_", "envers_wagonnet_suspendu_", "envers_rosace_brisee_", "envers_barque_noire_", "envers_pont_brise_" })
            Assert.Contains(own, id => id.StartsWith(prefix, StringComparison.Ordinal));
        // L'arbre millénaire mort remplace celui de la surface, au même endroit.
        SpaceObstacle tree = Envers.Setup.Space.Obstacles.Single(o => o.Id == "envers_arbre_millenaire_mort");
        SpaceObstacle alive = Vallons.Setup.Space!.Obstacles.Single(o => o.Id == "arbre_millenaire_foret");
        Assert.True(Dist(Centre(tree), Centre(alive)) < 5);
        Assert.DoesNotContain(Envers.Setup.Space.Obstacles, o => o.Id == "arbre_millenaire_foret");
    }

    private static (double X, double Z) Centre(SpaceObstacle o) => (o.Points.Average(p => p.X.Raw / 1e4), o.Points.Average(p => p.Z.Raw / 1e4));

    // --- Failles -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Failles_des_lieux_au_meme_point_des_deux_cotes()
    {
        var up = Vallons.Doors.Where(d => d.Kind == DoorKind.Rift).ToList();
        var down = Envers.Doors.ToList();
        Assert.Equal(14, up.Count);   // la faille de la V2.51 et treize de plus
        Assert.Equal(up.Count, down.Count);
        foreach (Door d in up)
        {
            Door back = Envers.DoorOf(d.ToDoor)!;
            Assert.Equal(("zone.envers_vallons", DoorKind.Rift, "zone.vallons", d.Id, DoorKind.Rift), (d.To, d.Kind, back.To, back.ToDoor, back.Kind));
            Assert.Equal(d.Position, back.Position);
        }
        // Chaque lieu (sauf le pont et le col, barrés comme leurs passages de la surface) a une faille à moins de 120 m de son emprise.
        foreach ((string id, _) in Lieux.Where(l => l.Id != "envers_du_pont_des_murmures" && l.Id != "envers_du_col"))
        {
            Place place = PlaceOf(id);
            Assert.Contains(down, d => Gap(place, P(d.Position)) < 120);
        }
        // À la surface, une faille reste à 8 m au moins des autres portes (on y arrive).
        foreach (Door d in up)
            foreach (Door other in Vallons.Doors.Where(o => o.Id != d.Id))
                Assert.True(Dist(P(d.Position), P(other.Position)) >= 8, $"{d.Id} / {other.Id}");
    }

    private static double Gap(Place p, (double X, double Z) at)
    {
        double dx = Math.Max(Math.Max(p.Min.X.Raw / 1e4 - at.X, 0), at.X - p.Max.X.Raw / 1e4);
        double dz = Math.Max(Math.Max(p.Min.Z.Raw / 1e4 - at.Z, 0), at.Z - p.Max.Z.Raw / 1e4);
        return Math.Sqrt(dx * dx + dz * dz);
    }

    [Fact]
    public void Toutes_les_formes_loin_des_failles_des_deux_cotes()
    {
        // Revues Codex : créatures, chefs et toutes leurs formes à plus de 12 m de toute faille et hors d'aggro de bord à bord ; 8 m des autres
        // portes. Dans l'Envers, toutes les portes sont des failles.
        double hero = Math.Max(ZoneValidator.CharacterRadius.Raw / 1e4, 0.9);
        foreach (Door d in Envers.Doors)
            foreach (AssailantSetup c in Fighters(Envers))
            {
                double centre = Dist(P(c.Placement!.Position), P(d.Position));
                Assert.True(centre > 12, $"{c.Id} à {centre:0.##} m de {d.Id}");
                Assert.True(centre - c.Placement.Radius.Raw / 1e4 - hero > c.Behavior!.AggroRange!.Value.Raw / 1e4, $"{c.Id} / {d.Id} (aggro)");
            }
        foreach (Door d in Vallons.Doors.Where(x => x.Kind == DoorKind.Rift))
            foreach (AssailantSetup c in Fighters(Vallons))
            {
                double centre = Dist(P(c.Placement!.Position), P(d.Position));
                Assert.True(centre > 12, $"{c.Id} à {centre:0.##} m de {d.Id}");
                Assert.True(centre - c.Placement.Radius.Raw / 1e4 - hero > (c.Behavior?.AggroRange?.Raw ?? 0) / 1e4, $"{c.Id} / {d.Id} (aggro)");
            }
    }

    [Theory]
    [InlineData(12.0001, true)]
    [InlineData(12.0, false)]
    public void Frontiere_d_une_forme_de_chef_a_une_nouvelle_faille(double margin, bool valid)
    {
        // La seconde forme de l'Ombre du vieux Brunot posée près de la faille de la forêt : refusée à 12 m + rayon + 0,9 + aggro pile.
        Door rift = Envers.DoorOf("faille_vallons_de_la_foret")!;
        AssailantSetup form = Envers.Chiefs.Single(c => c.Id == "ombre_du_vieux_brunot").Forms[1];
        double reach = Math.Max(12, form.Placement!.Radius.Raw / 1e4 + 0.9 + form.Behavior!.AggroRange!.Value.Raw / 1e4) - 12 + margin;
        (double x, double z) = P(rift.Position);
        string dir = CopyScenarios("envers_vallons_brunot", s =>
        {
            JsonObject f = Combatant(s, form.Id);
            f["position"] = new JsonArray(Math.Round(x + reach, 4), z);
            s["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(-1000, 500), ["max"] = new JsonArray(-500, 900) };   // les deux formes et la faille
        });
        ZoneLoadResult r = Read(ZoneJson("zone.envers_vallons"), dir);
        int index = Envers.Chiefs.ToList().FindIndex(c => c.Id == "ombre_du_vieux_brunot");
        bool refused = r.Report.Errors.Any(e => e.Path == $"$.data.chiefs[{index}].forms[1]" && e.Message.Contains("faille_vallons_de_la_foret", StringComparison.Ordinal));
        Assert.True(refused == !valid, r.Report.ToString());
        Assert.DoesNotContain(r.Report.Errors, e => e.Path.EndsWith(".scenario", StringComparison.Ordinal));   // le scénario du chef est lu
    }

    private string CopyScenarios(string scenario, Action<JsonObject> edit)
    {
        string dir = Path.Combine(_folder, "scenarios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        foreach (string f in Directory.GetFiles(Path.Combine(Scenarios, "zones"), "*.json").Select(x => Path.GetFileName(x))
            .Where(x => x == "vallons.json" || x.StartsWith("envers_vallons", StringComparison.Ordinal)))
            File.Copy(Path.Combine(Scenarios, "zones", f), Path.Combine(dir, "zones", f));
        string path = Path.Combine(dir, "zones", scenario + ".json");
        JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(s);
        File.WriteAllText(path, s.ToJsonString());
        return dir;
    }

    private static JsonObject Combatant(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    // --- Créatures, trêves, chefs, apparences ----------------------------------------------------------------------------

    [Fact]
    public void Reflets_et_ombres_en_treve_partout()
    {
        Assert.Equal(new[] { "ombres", "reflets" }, Fighters(Envers).Select(c => c.Team).Distinct().OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(new[] { ("reflets", "ombres") }, Envers.Setup.Truces.Select(t => (t.A, t.B)));
        // Le plafond des combattants d'une session tient (créatures, personnage, toutes les formes).
        Assert.True(Envers.Setup.Combatants.Count + 1 + Envers.Bosses.Sum(b => b.Forms.Count) <= Envers.Setup.Combat.MaxCombatants);
    }

    [Fact]
    public void Chefs_des_lieux_majeurs_dans_le_chemin_standard()
    {
        Assert.Null(Envers.Boss);
        Assert.Equal(new[] { "reflet_du_gardien", "ombre_du_vieux_brunot", "reflet_de_machefer", "abbe_renverse" }, Envers.Chiefs.Select(c => c.Id));
        (string Chief, string Place, string Item, int Forms)[] expected =
        {
            ("ombre_du_vieux_brunot", "envers_de_la_foret", "item.ecorce_du_millenaire", 2),
            ("reflet_de_machefer", "envers_du_fort", "item.chevaliere_de_machefer", 2),
            ("abbe_renverse", "envers_de_l_abbaye", "item.chapelet_renverse", 3),
        };
        foreach (var e in expected)
        {
            Boss chief = Envers.Chiefs.Single(c => c.Id == e.Chief);
            Assert.Equal(e.Forms, chief.Forms.Count);
            Assert.Equal((e.Item, 1), chief.Rewards[0]);
            Assert.All(chief.Forms, f => Assert.True(PlaceOf(e.Place).Contains(f.Placement!.Position), f.Id));
            Assert.Equal($"zone.envers_vallons/boss/{e.Chief}", Envers.VictoryKeyOf(chief));
            ItemDefinition item = Items.Item(e.Item);
            Assert.True(item.Named);
            Assert.Equal(24, item.RequiredLevel);
            Assert.True(V11TestData.ShippedMerchants.Values.ContainsKey(e.Item));
        }
        var facts = Vallons.Chiefs.Select(c => c.Fact).Concat(V16TestData.Interiors.SelectMany(z => z.Chiefs).Select(c => c.Fact)).ToList();
        Assert.Equal(facts.Count, facts.Distinct().Count());
    }

    [Fact]
    public void Chef_de_l_abbaye_recompense_a_la_premiere_victoire_seulement()
    {
        string[] forms = { "moine_reflet_abbe", "moine_reflet_abbe_courrouce", "moine_reflet_abbe_dechire" };
        string dir = CopyScenarios("envers_vallons_abbe", g =>
        {
            for (int i = 0; i < forms.Length; i++)
            {
                JsonObject f = Combatant(g, forms[i]);
                f["health"] = 10;
                f["intervalMs"] = 60_000;
                foreach (JsonNode? a in f["attacks"]!.AsArray()) a!["rawDamage"] = 1;
                f["position"] = new JsonArray(1654.0 + 2 * i, -463.0);   // près de l'autel (à portée d'aggro), loin des failles
            }
            g["data"]!["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(1630, -480), ["max"] = new JsonArray(1680, -440) };
        });
        JsonObject root = ZoneJson("zone.envers_vallons");
        root["data"]!["populations"] = new JsonArray();
        // Les formes sont déplacées à l'autel : le lieu de l'abbaye ne compte plus, et la forme posée au plus près des failles reste à plus de 12 m.
        ZoneLoadResult loaded = Read(root, dir);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        SavedCharacter hero = V11TestData.Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 20));
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, Items);
        List<SessionStep> first = Fight(w, "moine_reflet_abbe");
        string Debug() => string.Join(" | ", w.Zone.Engine.CombatantIds.Select(id => $"{id} {w.Zone.Engine.View(id).Health} {w.Zone.Engine.View(id).Position} {w.Zone.Engine.View(id).IsDead}")) + " forms=" + string.Join(",", w.BossForms);
        Assert.True(first.SelectMany(s => s.Boss).Any(b => b.Kind == BossEventKind.Defeated), Debug());
        Assert.Equal(new[] { ("item.chapelet_renverse", 1), ("item.eclat_de_seuil", 4) }, first.SelectMany(s => s.Rewards));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains("zone.envers_vallons/boss/abbe_renverse", saved.Consumed);
        var again = new WorldSession(saved, zone, ShippedData, Progression, Items);
        List<SessionStep> second = Fight(again, "moine_reflet_abbe");
        Assert.Contains(second.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Empty(second.SelectMany(s => s.Rewards));
    }

    private static List<SessionStep> Fight(WorldSession w, string prefix, int max = 12000)
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
    public void Apparences_noms_et_prefixes_de_tous_les_reflets()
    {
        // Revues Codex : ForCombatant, CharacterOf, EchoLooks.Of et DisplayNames pour chaque reflet et chaque forme, vies suivantes comprises ;
        // aucun identifiant lu comme une ourse (« ours_envers » avait été pris pour une ourse et ses oursons).
        var expected = new (string Prefix, StyleSlot Slot, string Character, string Name)[]
        {
            ("ours_reflet_brunot_dechaine", StyleSlot.Bear, "ours", "Ombre du vieux Brunot déchaînée"),
            ("ours_reflet_brunot", StyleSlot.Bear, "ours", "Ombre du vieux Brunot"),
            ("bandit_reflet_machefer_furieux", StyleSlot.Character, "bandit", "Reflet de Mâchefer furieux"),
            ("bandit_reflet_machefer", StyleSlot.Character, "bandit", "Reflet de Mâchefer"),
            ("moine_reflet_abbe_courrouce", StyleSlot.Character, "moine", "L'abbé renversé courroucé"),
            ("moine_reflet_abbe_dechire", StyleSlot.Character, "moine", "L'abbé renversé déchiré"),
            ("moine_reflet_abbe", StyleSlot.Character, "moine", "L'abbé renversé"),
            ("loup_echo_reflet_gardien", StyleSlot.Wolf, "loup", "Reflet du Gardien"),
            ("loup_echo_envers", StyleSlot.Wolf, "loup", "Reflet de loup"),
            ("ours_reflet", StyleSlot.Bear, "ours", "Ombre d'ours"),
            ("sanglier_reflet", StyleSlot.Boar, "sanglier", "Reflet de sanglier"),
            ("chien_reflet", StyleSlot.Wolf, "chien", "Chien de cendre"),
            ("garde_reflet", StyleSlot.Character, "garde", "Garde renversé"),
            ("bandit_reflet", StyleSlot.Character, "bandit", "Bandit renversé"),
            ("mineur_reflet", StyleSlot.Character, "mineur", "Mineur renversé"),
            ("vendangeur_reflet", StyleSlot.Character, "vendangeur", "Vendangeur renversé"),
            ("archer_reflet", StyleSlot.Character, "archer", "Archer renversé"),
            ("moine_reflet", StyleSlot.Character, "moine", "Moine renversé"),
        };
        var ids = Fighters(Envers).Select(c => c.Id).ToList();
        Assert.True(ids.Count > 60, $"{ids.Count}");
        foreach (string id in ids.Concat(ids.Select(i => i + "_v2")))
        {
            var e = expected.First(x => id.StartsWith(x.Prefix, StringComparison.Ordinal));
            if (e.Prefix == "loup_echo_reflet_gardien" && id.Contains("brise", StringComparison.Ordinal)) e.Name = "Reflet du Gardien brisé";
            Assert.Equal(e.Slot, StyleRules.ForCombatant(id, false));
            Assert.Equal(e.Character, StyleRules.CharacterOf(id));
            Assert.Equal(EchoKind.Rift, EchoLooks.Of(id));
            Assert.True(EchoLooks.FromEnvers(id), id);
            Assert.Equal(0, StyleRules.Cubs(id));
            Assert.Equal(e.Name, DisplayNames.Of(id));
            Assert.NotEqual(1.45, StyleRules.BeastHeight(id));   // jamais la hauteur de l'ourse
        }
        Assert.Equal(0.8, StyleRules.BeastHeight("chien_reflet2"));
        Assert.Equal(2.2, StyleRules.BeastHeight("ours_reflet_brunot_dechaine"));
        Assert.Equal("Morsure de cendre", DisplayNames.Attack("morsure_de_cendre"));
        Assert.Equal(HumanOutfit.Clergy, HumanLooks.OutfitOf(StyleRules.CharacterOf("moine_reflet3"), string.Empty));
        Assert.Equal(HumanOutfit.Guard, HumanLooks.OutfitOf(StyleRules.CharacterOf("garde_reflet1"), string.Empty));
        // Les créatures d'avant ne changent pas.
        foreach (string id in new[] { "sanglier1", "bandit_epee3", "archer1", "moine_echo_crypte1", "loup_echo_galerie1" }) Assert.False(EchoLooks.FromEnvers(id), id);
        Assert.False(StyleRules.IsAshHound("chien1"));
        Assert.Equal(EchoKind.Returned, EchoLooks.Of("moine_echo_crypte2"));
    }

    // --- Secrets (schéma 18) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Secrets_des_lieux_bien_formes()
    {
        Assert.True(Envers.Secrets.Count >= 14, $"{Envers.Secrets.Count}");
        JsonObject texts = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "texts", "fr.json")))!["data"]!["entries"]!.AsObject();
        foreach (ZoneSecret s in Envers.Secrets)
        {
            Assert.True(texts.ContainsKey(s.Text), s.Text);
            Assert.Equal($"zone.envers_vallons/secret/{s.Id}", Envers.SecretKey(s.Id));
            if (s.Kind == SecretKind.Chest) Assert.NotEmpty(s.Rewards);
        }
        Assert.Contains(Envers.Secrets, s => s.Kind == SecretKind.Chest);
        Assert.Contains(Envers.Secrets, s => s.Kind == SecretKind.Lore);
        Assert.Empty(Vallons.Secrets);
        foreach (string key in new[] { "text.ui.secret.open_hint", "text.ui.secret.read_hint", "text.ui.secret.empty", "text.ui.secret.in_combat", "text.ui.secret.bag_full" })
            Assert.True(texts.ContainsKey(key), key);
    }

    [Fact]
    public void Secret_mal_forme_refuse_avec_son_chemin()
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
        JsonObject S(JsonObject r, int i) => r["data"]!["secrets"]![i]!.AsObject();
        int chest = Envers.Secrets.ToList().FindIndex(s => s.Kind == SecretKind.Chest);
        int lore = Envers.Secrets.ToList().FindIndex(s => s.Kind == SecretKind.Lore);
        Refused(With(r => S(r, chest)["kind"] = "tresor"), $"$.data.secrets[{chest}].kind");
        Refused(With(r => S(r, chest).Remove("rewards")), $"$.data.secrets[{chest}].rewards");
        Refused(With(r => S(r, chest)["rewards"]![0]!["item"] = "item.inconnu"), $"$.data.secrets[{chest}].rewards[0].item");
        Refused(With(r => S(r, chest)["rewards"]![0]!["count"] = 0), $"$.data.secrets[{chest}].rewards[0].count");
        Refused(With(r => S(r, lore)["text"] = "pas une clé"), $"$.data.secrets[{lore}].text");
        Refused(With(r => S(r, lore)["id"] = Envers.Secrets[chest].Id), $"$.data.secrets[{lore}].id");
        // Dans un obstacle reflété (le moulin du village).
        Refused(With(r => S(r, chest)["position"] = new JsonArray(-3.0, 9.0)), $"$.data.secrets[{chest}].position");
        // En schéma 17, les secrets sont refusés ; sans eux, le document de schéma 17 reste lu.
        Refused(With(r => r["schemaVersion"] = 17), "$.data.secrets");
        ZoneLoadResult old = With(r => { r["schemaVersion"] = 17; r["data"]!.AsObject().Remove("secrets"); });
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Empty(old.Zone!.Secrets);
        // Une stèle sans récompense est permise.
        Assert.Empty(Envers.Secrets[lore].Rewards);
    }

    /// <summary>Données copiées : le coffre de la fêlure posé à 1,5 m de l'autel de l'Aube (à portée du personnage qui y entre).</summary>
    private (string Data, string Scenarios) CopyAll(string secret, (double X, double Z) at)
    {
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
        root["data"]!["secrets"]!.AsArray().Single(s => (string)s!["id"]! == secret)!["position"] = new JsonArray(at.X, at.Z);
        File.WriteAllText(p, root.ToJsonString());
        return (data, scenarios);
    }

    [Fact]
    public void Coffre_fouille_une_seule_fois_refus_atomiques()
    {
        (string data, string scenarios) = CopyAll("coffre_de_la_felure", (1656, -468.5));
        ZoneDefinition zone = Read(JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(data, "zone.envers_vallons")))!.AsObject(), scenarios).Zone!;
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero()), zone, ShippedData, Progression, Items);
        w.Step();
        Assert.Equal(SecretRefusal.Unknown, w.CanOpenSecret("secret_inconnu"));
        Assert.Equal(SecretRefusal.OutOfReach, w.CanOpenSecret("coffre_du_sommet"));
        Assert.Equal("coffre_de_la_felure", w.SecretsInReach().Single().Id);
        int before = w.Inventory.Count("item.eclat_de_seuil");
        var given = new List<(string, int)>();
        Assert.Equal(SecretRefusal.None, w.OpenSecret("coffre_de_la_felure", given));
        Assert.Equal(new[] { ("item.eclat_de_seuil", 3) }, given);
        Assert.Equal(before + 3, w.Inventory.Count("item.eclat_de_seuil"));
        Assert.True(w.IsSecretOpened("coffre_de_la_felure"));
        // Une deuxième fouille ne change rien.
        Assert.Equal(SecretRefusal.AlreadyOpened, w.OpenSecret("coffre_de_la_felure", given));
        Assert.Equal(before + 3, w.Inventory.Count("item.eclat_de_seuil"));
        Assert.Single(given);
        // La sauvegarde garde la fouille.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains("zone.envers_vallons/secret/coffre_de_la_felure", saved.Consumed);
        var again = new WorldSession(saved, zone, ShippedData, Progression, Items);
        again.Step();
        Assert.True(again.IsSecretOpened("coffre_de_la_felure"));
        Assert.Equal(SecretRefusal.AlreadyOpened, again.CanOpenSecret("coffre_de_la_felure"));
    }

    [Fact]
    public void Coffre_refuse_sac_plein_sans_rien_changer()
    {
        (string data, string scenarios) = CopyAll("coffre_de_la_felure", (1656, -468.5));
        ZoneDefinition zone = Read(JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(data, "zone.envers_vallons")))!.AsObject(), scenarios).Zone!;
        // Un sac plein d'objets qui ne s'empilent pas avec les éclats.
        ItemDefinition filler = Items.Items.First(i => i.Stack == 1 && i.Id != "item.eclat_de_seuil" && i.Slot is null);
        WorldSession? full = null;
        for (int n = 24; n > 0 && full is null; n--)
            try
            {
                var stacks = Enumerable.Range(0, n).Select(_ => new ItemStack(filler.Id, 1)).ToArray();
                full = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, V11TestData.Hero(0, stacks)), zone, ShippedData, Progression, Items);
            }
            catch (ArgumentException) { }
        WorldSession w = full!;
        w.Step();
        string before = SaveJsonReader.Write(w.Save());
        Assert.Equal(SecretRefusal.BagFull, w.OpenSecret("coffre_de_la_felure"));
        Assert.False(w.IsSecretOpened("coffre_de_la_felure"));
        Assert.Equal(before, SaveJsonReader.Write(w.Save()));
    }

    private WorldSession SessionAtAltar(Action<string, string>? edit = null, IEnumerable<string>? consumed = null)
    {
        (string data, string scenarios) = CopyAll("coffre_de_la_felure", (1656, -468.5));
        edit?.Invoke(data, scenarios);
        ZoneDefinition zone = Read(JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(data, "zone.envers_vallons")))!.AsObject(), scenarios).Zone!;
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey).ToArray(), V11TestData.Hero()),
            zone, ShippedData, Progression, Items);
        w.Step();
        return w;
    }

    [Theory]
    [InlineData(1, true)]    // une place de plus que la réserve des morts possibles : la fouille passe
    [InlineData(0, false)]   // juste la réserve : refus atomique (RegistryFull), rien ne change
    public void Coffre_refuse_registre_plein_sans_rien_changer(int spare, bool opens)
    {
        WorldSession w = SessionAtAltar();
        for (int i = 0; i < SaveValidator.MaxConsumed && w.RegistryRoom - w.DeathReserve > spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D6}"));
        Assert.Equal(spare, w.RegistryRoom - w.DeathReserve);
        int before = w.Inventory.Count("item.eclat_de_seuil"), room = w.RegistryRoom;
        SecretRefusal refusal = w.OpenSecret("coffre_de_la_felure");
        if (opens)
        {
            Assert.Equal(SecretRefusal.None, refusal);
            Assert.Equal(before + 3, w.Inventory.Count("item.eclat_de_seuil"));
            Assert.Equal(room - 1, w.RegistryRoom);
            return;
        }
        Assert.Equal(SecretRefusal.RegistryFull, refusal);
        Assert.Equal((before, room, false), (w.Inventory.Count("item.eclat_de_seuil"), w.RegistryRoom, w.IsSecretOpened("coffre_de_la_felure")));
    }

    [Fact]
    public void Coffre_refuse_en_combat_sans_rien_changer()
    {
        // Un reflet posé à 8 m de l'autel (à plus de 12 m de la faille), qui engage le personnage.
        WorldSession w = SessionAtAltar((data, scenarios) =>
        {
            string path = Path.Combine(scenarios, "zones", "envers_vallons.json");
            JsonObject s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            JsonObject wolf = Combatant(s, "loup_echo_envers1");
            wolf["position"] = new JsonArray(1664.0, -470.0);
            File.WriteAllText(path, s.ToJsonString());
        });
        for (int i = 0; i < 200 && !w.InCombat; i++) w.Step();
        Assert.True(w.InCombat);
        int before = w.Inventory.Count("item.eclat_de_seuil");
        Assert.Equal(SecretRefusal.InCombat, w.OpenSecret("coffre_de_la_felure"));
        Assert.Equal(before, w.Inventory.Count("item.eclat_de_seuil"));
        Assert.False(w.IsSecretOpened("coffre_de_la_felure"));
    }

    [Fact]
    public void Partie_reprise_avec_un_coffre_deja_fouille()
    {
        // Une sauvegarde qui porte déjà la clé du coffre : il ne redonne rien, la stèle voisine se lit toujours.
        WorldSession w = SessionAtAltar(consumed: new[] { "zone.envers_vallons/secret/coffre_de_la_felure" });
        Assert.True(w.IsSecretOpened("coffre_de_la_felure"));
        int before = w.Inventory.Count("item.eclat_de_seuil");
        Assert.Equal(SecretRefusal.AlreadyOpened, w.OpenSecret("coffre_de_la_felure"));
        Assert.Equal(before, w.Inventory.Count("item.eclat_de_seuil"));
        Assert.False(w.IsSecretOpened("stele_du_cercle"));
    }

    [Fact]
    public void Stele_lue_dans_l_aventure_et_relue()
    {
        (string data, string scenarios) = CopyAll("stele_du_cercle", (1656, -468.5));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.envers_vallons", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
        AdventureSession a = V11TestData.Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.Equal("stele_du_cercle", a.SecretInReach!.Id);
        Assert.Equal("F : lire (Stèle du cercle fendu)", a.SecretHint);
        Assert.True(a.Interact());   // la touche d'action (F) lit la stèle
        Assert.Contains(a.DrainMessages(), m => m.StartsWith("Les pierres levées de l'Envers", StringComparison.Ordinal));
        Assert.True(a.IsSecretOpened("stele_du_cercle"));
        Assert.True(a.OpenSecret());   // une stèle se relit
        Assert.Contains(a.DrainMessages(), m => m.StartsWith("Les pierres levées de l'Envers", StringComparison.Ordinal));
    }
}
