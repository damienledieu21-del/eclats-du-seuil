using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28b (2c), première partie (PLAN_GRANDE_CARTE.md § 2.5) : les chefs vivent dans le secteur de leur première forme. Sur
/// l'hôte des secteurs, chacun arrive avec chaque session nouvelle de son secteur (ouverture, réouverture, renouvellement) et se joue
/// dans sa session ; une zone à plusieurs secteurs avec des chefs (sans événement du monde) est tenue par l'hôte.
/// Zone d'essai : 512 × 256 m, deux secteurs (s00_00 à l'ouest, s01_00 à l'est), un loup et un point sûr dans chacun, un chef à l'est.
/// </summary>
[Trait("Category", "Sector")]
public class V228b2cChefsTests
{
    private const string Chef = "chef_est";

    /// <summary>Zone d'essai lue et validée comme une zone livrée ; retouches de la zone, du scénario et du scénario du chef.</summary>
    internal static ZoneLoadResult Read(Action<JsonObject>? zone = null, Action<JsonObject>? scenario = null, Action<JsonObject>? chief = null,
        Action<Func<JsonArray, JsonObject>, Dictionary<string, JsonObject>>? extra = null)
    {
        JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!["data"]!.AsObject();
        JsonObject loup = vallons["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "loup1")!.AsObject();
        JsonObject Wolf(string id, double x, double z, int health)
        {
            var w = (JsonObject)loup.DeepClone();
            w["id"] = id;
            w["position"] = new JsonArray(x, z);
            w["health"] = health;
            return w;
        }
        JsonArray crocForms = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons_croc_gris")))!["data"]!["combatants"]!.AsArray();
        JsonObject Form(int index, string id, double x, double z)
        {
            var f = (JsonObject)crocForms[index]!.DeepClone();
            f["id"] = id;
            f["position"] = new JsonArray(x, z);
            f["health"] = 40;
            return f;
        }
        JsonObject Scenario(JsonArray combatants) => new()
        {
            ["schemaVersion"] = 18,
            ["kind"] = "scenario",
            ["contentVersion"] = "0.1.0-provisoire",
            ["data"] = new JsonObject
            {
                ["seed"] = 7,
                ["durationMs"] = 3_600_000,
                ["zone"] = true,
                ["space"] = new JsonObject
                {
                    ["bounds"] = new JsonObject { ["min"] = new JsonArray(0, 0), ["max"] = new JsonArray(512, 256) },
                    ["obstacles"] = new JsonArray(new JsonObject { ["id"] = "rocher", ["points"] = new JsonArray(new JsonArray(150, 20), new JsonArray(153, 20), new JsonArray(153, 23), new JsonArray(150, 23)) }),
                },
                ["combatants"] = combatants,
                ["commands"] = new JsonArray(),
            },
        };
        JsonObject s = Scenario(new JsonArray(Wolf("loup_ouest", 100, 40, 150), Wolf("loup_est", 400, 128, 60)));
        scenario?.Invoke(s);
        JsonObject c = Scenario(new JsonArray(Form(0, Chef, 330, 60), Form(1, Chef + "_furieux", 334, 60)));
        chief?.Invoke(c);
        JsonObject Safe(string id, string name, double x, double z) => new() { ["id"] = id, ["name"] = name, ["position"] = new JsonArray(x, z) };
        JsonObject Pop(string creature) => new() { ["creature"] = creature, ["respawnMs"] = 90_000, ["leashRange"] = 20, ["loot"] = "loot.loup" };
        var root = new JsonObject
        {
            ["schemaVersion"] = 14,
            ["kind"] = "zone",
            ["contentVersion"] = "0.1.0-provisoire",
            ["data"] = new JsonObject
            {
                ["id"] = "zone.essai",
                ["name"] = "Essai",
                ["scenario"] = "zones/essai_secteurs",
                ["entry"] = "camp_ouest",
                ["region"] = new JsonObject { ["origin"] = new JsonArray(0, 0), ["columns"] = 4, ["rows"] = 2 },
                ["safePoints"] = new JsonArray(Safe("camp_ouest", "Camp de l'ouest", 64, 128), Safe("camp_est", "Camp de l'est", 420, 200),
                    Safe("guet_est", "Guet de l'est", 330, 63)),
                ["places"] = new JsonArray(),
                ["populations"] = new JsonArray(Pop("loup_ouest"), Pop("loup_est")),
                ["passages"] = new JsonArray(),
                ["chiefs"] = new JsonArray(new JsonObject
                {
                    ["id"] = Chef, ["scenario"] = "zones/essai_chef", ["forms"] = new JsonArray(Chef, Chef + "_furieux"), ["fact"] = "fact.chef_est.vaincu",
                    ["rewards"] = new JsonArray(new JsonObject { ["item"] = "item.collier_de_croc_gris", ["count"] = 1 }), ["loot"] = "loot.loup",
                }),
            },
        };
        zone?.Invoke(root);
        var files = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        extra?.Invoke(Scenario, files);
        string directory = Path.Combine(Path.GetTempPath(), "eds_v228b2c_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "zones"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "zones", "essai_secteurs.json"), s.ToJsonString());
            File.WriteAllText(Path.Combine(directory, "zones", "essai_chef.json"), c.ToJsonString());
            foreach ((string name, JsonObject file) in files) File.WriteAllText(Path.Combine(directory, "zones", name + ".json"), file.ToJsonString());
            return ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static ZoneDefinition Essai(Action<JsonObject>? zone = null, Action<JsonObject>? scenario = null, Action<JsonObject>? chief = null,
        Action<Func<JsonArray, JsonObject>, Dictionary<string, JsonObject>>? extra = null)
    {
        ZoneLoadResult loaded = Read(zone, scenario, chief, extra);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    private static readonly Lazy<ZoneDefinition> WithChief = new(() => Essai());

    private static WorldSession Open(ZoneDefinition zone, WorldSave? save = null, string? safePoint = null, int level = 1)
    {
        SavedCharacter hero = Hero();
        if (level > 1) hero = hero.WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level));
        save ??= new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero);
        var w = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, safePoint: safePoint);
        w.Step();
        return w;
    }

    private static (double X, double Z) Where(WorldSession w)
    {
        GroundVector p = w.Zone.Engine.View(w.CombatantId).Position!.Value;
        return (p.X.Raw / 1e4, p.Z.Raw / 1e4);
    }

    /// <summary>Marche vers (x, z) ; rend les pas joués (la commande suit le personnage d'une session à l'autre).</summary>
    private static List<SessionStep> WalkTo(WorldSession w, double x, double z)
    {
        var steps = new List<SessionStep>();
        (double, double)? sent = null;
        for (int i = 0; i < 6000; i++)
        {
            CombatEngine e = w.Zone.Engine;
            CombatantView me = e.View(w.CombatantId);
            if (!me.IsPresent)
            {
                steps.Add(w.Step());
                sent = null;
                continue;
            }
            (double px, double pz) = Where(w);
            double dx = x - px, dz = z - pz, d = Math.Sqrt(dx * dx + dz * dz);
            if (d <= 1)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId, CombatAction.Stop));
                steps.Add(w.Step());
                return steps;
            }
            var dir = (Math.Round(dx / d, 3), Math.Round(dz / d, 3));
            if (sent != dir || i % 40 == 0)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId,
                    new GroundVector(Fixed.Parse(dir.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse(dir.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
                sent = dir;
            }
            steps.Add(w.Step());
        }
        throw new Xunit.Sdk.XunitException($"({x}, {z}) pas atteint ; arrêté en {Where(w)}");
    }

    private static string? ChiefForm(WorldSession w) => w.BossForms.FirstOrDefault(f => f.StartsWith(Chef, StringComparison.Ordinal));

    /// <summary>Frappe la forme du chef du moment jusqu'à sa défaite ; rend les pas joués.</summary>
    private static List<SessionStep> FightChief(WorldSession w, int max = 2400)
    {
        var steps = new List<SessionStep>();
        for (int i = 0; i < max && !steps.Any(s => s.Boss.Any(b => b.Kind == BossEventKind.Defeated)); i++)
        {
            if (i % 20 == 0 && ChiefForm(w) is string form)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
        }
        return steps;
    }

    // --- Validateur ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Validateur_formes_d_un_chef_dans_un_seul_secteur_loin_de_ses_bords()
    {
        Assert.True(Read().IsValid);
        // Seconde forme dans l'autre secteur : refusée, avec le chemin de la forme.
        ZoneLoadResult other = Read(chief: c => c["data"]!["combatants"]![1]!["position"] = new JsonArray(200, 60));
        Assert.False(other.IsValid);
        Assert.Contains(other.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("hors du secteur", StringComparison.Ordinal));
        // Première forme à 5 m du bord intérieur (x = 256) : refusée.
        ZoneLoadResult edge = Read(chief: c =>
        {
            c["data"]!["combatants"]![0]!["position"] = new JsonArray(261, 60);
            c["data"]!["combatants"]![1]!["position"] = new JsonArray(270, 60);
        });
        Assert.False(edge.IsValid);
        Assert.Contains(edge.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[0]" && e.Message.Contains("moins de 8 m", StringComparison.Ordinal));
        // Relecture indépendante de la PR #159 : la règle vaut aussi pour une zone à région en schéma 13 (l'hôte la tient aussi).
        ZoneLoadResult v13 = Read(zone: z => z["schemaVersion"] = 13, chief: c => c["data"]!["combatants"]![1]!["position"] = new JsonArray(200, 60));
        Assert.False(v13.IsValid);
        Assert.Contains(v13.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Message.Contains("hors du secteur", StringComparison.Ordinal));
        Assert.True(Read(zone: z => z["schemaVersion"] = 13).IsValid);
        // À 8 m : acceptée.
        Assert.True(Read(chief: c =>
        {
            c["data"]!["combatants"]![0]!["position"] = new JsonArray(264, 60);
            c["data"]!["combatants"]![1]!["position"] = new JsonArray(270, 60);
        }).IsValid);
    }

    // --- Chefs sur l'hôte ------------------------------------------------------------------------------------------------

    [Fact]
    public void Zone_a_plusieurs_secteurs_avec_un_chef_tenue_par_l_hote_le_chef_arrive_avec_son_secteur()
    {
        WorldSession w = Open(WithChief.Value);
        Assert.True(w.OnSectors);
        Assert.True(w.RenewsSessions);
        Assert.Equal("s00_00", w.Sector);
        Assert.Null(ChiefForm(w));   // l'est n'est pas ouvert : pas de chef
        Assert.DoesNotContain(Chef, w.Zone.Engine.CombatantIds);
        List<SessionStep> steps = WalkTo(w, 200, 128);   // à moins de 96 m de l'est : il s'ouvre
        Assert.Contains("s01_00", w.Host!.OpenSectors);
        Zone east = w.Host.SectorZone("s01_00")!;
        Assert.Equal(Chef, ChiefForm(w));
        Assert.Contains(Chef, east.Engine.CombatantIds);
        Assert.DoesNotContain(Chef, w.Zone.Engine.CombatantIds);
        Assert.Single(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.FormArrived && b.Form == Chef);
        // Il est présent dès le premier tick de sa session (arrivé avant lui).
        Assert.True(east.Engine.View(Chef).IsPresent);
    }

    [Fact]
    public void Chef_vaincu_dans_son_secteur_recompense_une_fois_victoire_au_registre()
    {
        WorldSession w = Open(WithChief.Value, safePoint: "guet_est", level: 20);
        Assert.Equal("s01_00", w.Sector);
        Assert.Equal(Chef, ChiefForm(w));
        List<SessionStep> steps = FightChief(w);
        Assert.Contains(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.FormArrived && b.Form == Chef + "_furieux");
        Assert.Contains(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated);
        Assert.Single(steps.SelectMany(s => s.Rewards), r => r.Item == "item.collier_de_croc_gris");
        Assert.True(w.Registered(w.Definition!.VictoryKeyOf(w.Definition.Bosses[0])));
        Assert.True(w.BossDefeated || ChiefForm(w) != null);
    }

    /// <summary>Zone d'essai à sessions de 40 s, chef aux coups faibles (le personnage tient), son secteur à l'est.</summary>
    /// <remarks>La forme furieuse a beaucoup de PV : un coup ne la tue pas.</remarks>
    private static ZoneDefinition ShortWeak(Action<JsonObject>? zone = null, int furious = 4000) => Essai(zone: zone, scenario: s => s["data"]!["durationMs"] = 40_000, chief: c =>
    {
        foreach (JsonNode? form in c["data"]!["combatants"]!.AsArray())
        {
            if (form!["id"]!.GetValue<string>() != Chef) form["health"] = furious;
            foreach (JsonNode? attack in form["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
        }
    });

    private static void Strike(WorldSession w, int i)
    {
        if (i % 20 == 0 && ChiefForm(w) is string form && w.Zone.Engine.CombatantIds.Contains(form))
            try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", form)); }
            catch (ArgumentException) { }
    }

    [Fact]
    public void Session_renouvelee_le_combat_du_chef_continue_tel_quel()
    {
        // Décision de Damien du 14 novembre 2026 (« AAA », 2) : la session de 40 s se renouvelle pendant le combat ; le chef y continue tel
        // quel : même forme (la furieuse), même place, mêmes PV ; une seule victoire, une seule récompense.
        WorldSession w = Open(ShortWeak(), safePoint: "guet_est", level: 20);
        Zone first = w.Zone;
        var steps = new List<SessionStep>();
        for (int i = 0; i < 2000 && !steps.Any(s => s.Boss.Any(b => b.Form == Chef + "_furieux")); i++)
        {
            Strike(w, i);
            steps.Add(w.Step());
        }
        // Un coup sur la forme furieuse : elle n'a plus tous ses PV.
        for (int i = 0; i < 400 && w.Zone.Engine.View(ChiefForm(w)!).Health == w.Zone.Engine.View(ChiefForm(w)!).MaxHealth; i++)
        {
            Strike(w, i);
            steps.Add(w.Step());
        }
        Assert.Same(first, w.Zone);
        CombatantView before = w.Zone.Engine.View(ChiefForm(w)!);
        Assert.True(before.Health < before.MaxHealth);
        while (!w.Zone.IsOver) steps.Add(w.Step());
        before = first.Engine.View(ChiefForm(w)!);
        SessionStep renewal = w.Step();
        Assert.Equal(1, w.Renewals);
        Assert.StartsWith(Chef + "_furieux", ChiefForm(w));
        CombatantView after = w.Zone.Engine.View(ChiefForm(w)!);
        Assert.True(after.IsPresent);
        Assert.Equal((before.Health, before.MaxHealth, before.Position), (after.Health, after.MaxHealth, after.Position));
        Assert.Contains(renewal.Boss, b => b.Kind == BossEventKind.Carried && b.Phase == 1);
        Assert.DoesNotContain(renewal.Boss, b => b.Kind == BossEventKind.FormArrived);
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => id == Chef);   // pas de retour à la première forme
        // Le renouvellement suivant l'emporte encore : toujours la même forme, aucune victoire ni récompense en route.
        steps.Clear();
        Zone second = w.Zone;
        for (int i = 0; i < 2000 && ReferenceEquals(second, w.Zone); i++) steps.Add(w.Step());
        Assert.Equal(2, w.Renewals);
        Assert.StartsWith(Chef + "_furieux", ChiefForm(w));
        Assert.Contains(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Carried);
        Assert.DoesNotContain(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated || b.Kind == BossEventKind.FormArrived);
        Assert.Empty(steps.SelectMany(s => s.Rewards));
    }

    [Fact]
    public void Chef_vaincu_le_reste_au_renouvellement_recompense_en_attente_gardee_puis_donnee_une_fois()
    {
        // Sac plein à la victoire : la récompense attend. La session se renouvelle : le chef vaincu le reste (aucune forme), la récompense
        // attend toujours ; une place libérée : elle arrive, une fois ; la sauvegarde garde la victoire, une partie reprise ne la redonne pas.
        ZoneDefinition zone = ShortWeak(furious: 40);
        SavedCharacter hero = Hero(0, Enumerable.Repeat(new ItemStack("item.gambison", 1), 24).ToArray())
            .WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 20));
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog, safePoint: "guet_est");
        var steps = new List<SessionStep>();
        for (int i = 0; i < 2400 && !steps.Any(s => s.Boss.Any(b => b.Kind == BossEventKind.Defeated)); i++)
        {
            Strike(w, i);
            steps.Add(w.Step());
        }
        Assert.Contains(steps.SelectMany(s => s.Story), e => e.Kind == StoryEventKind.RewardBlocked);
        Assert.Empty(steps.SelectMany(s => s.Rewards));
        string key = w.Definition!.VictoryKeyOf(w.Definition.Bosses[0]);
        Assert.False(w.Registered(key));
        Zone first = w.Zone;
        steps.Clear();
        for (int i = 0; i < 2000 && ReferenceEquals(first, w.Zone); i++) steps.Add(w.Step());
        Assert.Equal(1, w.Renewals);
        Assert.True(w.BossDefeated || w.Definition.Boss is null);
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => id.StartsWith(Chef, StringComparison.Ordinal));   // vaincu, il le reste
        Assert.Empty(steps.SelectMany(s => s.Rewards));
        Assert.Equal(InventoryRefusal.None, w.Inventory.Remove("item.gambison", 1));
        SessionStep given = w.Step();
        Assert.Equal(new[] { ("item.collier_de_croc_gris", 1) }, given.Rewards);
        Assert.Empty(w.Step().Rewards);
        Assert.True(w.Registered(key));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains(key, saved.Consumed);
        WorldSession again = Open(zone, saved, safePoint: "guet_est");
        Assert.Equal(Chef, ChiefForm(again));   // une vraie session nouvelle : il recommence
        Assert.True(again.Registered(key));
    }

    [Fact]
    public void Chef_qui_lache_au_bout_de_la_marge_revient_a_sa_place_avec_tous_ses_PV()
    {
        // Décision de Damien du 14 novembre 2026 (« AAA », 1) : le chef poursuit le personnage vers l'ouest ; au bout de la marge de son
        // secteur, il lâche, revient à sa place et reprend tous ses PV (même forme) ; le personnage passe ensuite à l'ouest.
        ZoneDefinition zone = Essai(
            zone: z => z["data"]!["safePoints"]!.AsArray().Add(new JsonObject { ["id"] = "guet_bord", ["name"] = "Bord", ["position"] = new JsonArray(274, 61) }),
            chief: c =>
            {
                foreach (JsonNode? form in c["data"]!["combatants"]!.AsArray())
                {
                    form!["position"] = new JsonArray(form["id"]!.GetValue<string>() == Chef ? 272 : 276, 56);
                    form["health"] = 4000;
                    foreach (JsonNode? attack in form["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
                }
            });
        WorldSession w = Open(zone, safePoint: "guet_bord", level: 20);
        Assert.Equal("s01_00", w.Sector);
        Zone east = w.Zone;
        // Un coup : il n'a plus tous ses PV, et il est engagé.
        for (int i = 0; i < 400 && east.Engine.View(Chef).Health == east.Engine.View(Chef).MaxHealth; i++)
        {
            Strike(w, i);
            w.Step();
        }
        Assert.True(east.Engine.View(Chef).Health < east.Engine.View(Chef).MaxHealth);
        List<SessionStep> steps = WalkTo(w, 150, 64);
        Assert.Equal("s00_00", w.Sector);
        BossEvent back = Assert.Single(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Returned);
        Assert.Equal((Chef, 0), (back.Boss, back.Phase));
        Assert.True(east.Engine.View(Chef).HasLeft);
        CombatantView home = east.Engine.View(back.Form);
        Assert.NotEqual(Chef, back.Form);   // identité nouvelle, même forme
        Assert.True(home.IsPresent);
        Assert.Equal(home.MaxHealth, home.Health);
        Assert.Equal(new GroundVector(Fixed.FromInt(272), Fixed.FromInt(56)), home.Position);
        Assert.DoesNotContain(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.FormArrived);
    }


    [Fact]
    public void Secteur_ferme_le_chef_en_sommeil_puis_nouveau_combat_a_la_reouverture()
    {
        WorldSession w = Open(WithChief.Value);
        WalkTo(w, 200, 128);
        Zone east = w.Host!.SectorZone("s01_00")!;
        Assert.Equal(Chef, ChiefForm(w));
        WalkTo(w, 64, 128);   // à plus de 96 m de l'est : il se ferme après 60 s
        for (int i = 0; i < 1300 && w.Host.SectorZone("s01_00") != null; i++) w.Step();
        Assert.Null(w.Host.SectorZone("s01_00"));
        for (int i = 0; i < 20; i++) w.Step();   // le combat en sommeil ne lève pas
        WalkTo(w, 200, 128);
        Zone again = w.Host.SectorZone("s01_00")!;
        Assert.NotSame(east, again);
        Assert.Contains(Chef, again.Engine.CombatantIds);
        Assert.True(again.Engine.View(Chef).IsPresent);
    }

    [Fact]
    public void Plafond_du_registre_le_chef_qui_arrive_compte_dans_la_borne()
    {
        // Atomicité : le renouvellement d'une session où arrive un chef réserve aussi sa mort possible. Juste à la borne : accepté entier ;
        // une clé de plus : pas de renouvellement (reprise au dernier point sûr), et aucun tick ne lève.
        foreach (int spare in new[] { 0, -1 })
        {
            ZoneDefinition zone = Essai(scenario: s => s["data"]!["durationMs"] = 2_000);
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, Hero()), zone, ShippedData, Progression, ItemsCatalog, safePoint: "guet_est");
            while (!w.Zone.IsOver) w.Step();
            SectorWorld host = w.Host!;
            long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
            for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            Assert.Equal(spare, SaveValidator.MaxConsumed - host.NextTickRegistryNeed);
            if (spare == 0)
            {
                Assert.True(w.RenewsSessions);
                w.Step();   // sans la place du chef dans la borne, le tick lèverait après le renouvellement (tick partiel)
                Assert.Equal(1, w.Renewals);
                Assert.Contains(Chef, w.Zone.Engine.CombatantIds);
                continue;
            }
            Assert.False(w.RenewsSessions);
            Assert.Equal(0, w.Renewals);
        }
    }

    [Fact]
    public void Plafond_du_registre_deux_chefs_emportes_borne_exacte()
    {
        // Relecture indépendante de la PR #159 : la borne d'un tick qui renouvelle une session est exacte. Une aire sans autre créature,
        // deux chefs à l'est, vivants à la fin de la session : la session nouvelle les reprend (combat emporté) ; la borne d'avant le tick
        // vaut exactement le registre et les morts possibles d'après (aucune mort, aucune clé). Juste à la borne : accepté entier ; une
        // clé de plus : pas de renouvellement, reprise au dernier point sûr, rien ne lève. Avant, la session terminée et toutes les
        // créatures de l'aire étaient comptées en plus : le tick était refusé à tort.
        JsonArray crocForms = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons_croc_gris")))!["data"]!["combatants"]!.AsArray();
        JsonObject Weak(string id, double x)
        {
            var f = (JsonObject)crocForms[0]!.DeepClone();
            f["id"] = id;
            f["position"] = new JsonArray(x, 30);
            foreach (JsonNode? attack in f["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
            return f;
        }
        ZoneDefinition zone = Essai(
            zone: z =>
            {
                z["data"]!["populations"] = new JsonArray();
                z["data"]!["chiefs"]![0]!["forms"] = new JsonArray(Chef);
                z["data"]!["chiefs"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = "chef_nord", ["scenario"] = "zones/essai_chef2", ["forms"] = new JsonArray("chef_nord"), ["fact"] = "fact.chef_nord.vaincu",
                    ["rewards"] = new JsonArray(), ["loot"] = "loot.loup",
                });
            },
            scenario: s =>
            {
                s["data"]!["combatants"] = new JsonArray();
                s["data"]!["durationMs"] = 2_000;
            },
            chief: c => c["data"]!["combatants"] = new JsonArray(Weak(Chef, 300)),
            extra: (scenario, files) => files["essai_chef2"] = scenario(new JsonArray(Weak("chef_nord", 360))));
        foreach (int spare in new[] { 0, -1 })
        {
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, Hero()), zone, ShippedData, Progression, ItemsCatalog, safePoint: "camp_est");
            Assert.Equal("s01_00", w.Sector);
            while (!w.Zone.IsOver) w.Step();
            SectorWorld host = w.Host!;
            long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
            for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            Assert.Equal(spare, SaveValidator.MaxConsumed - host.NextTickRegistryNeed);
            if (spare == 0)
            {
                long need = host.NextTickRegistryNeed;
                w.Step();
                Assert.Equal(1, w.Renewals);
                Assert.Contains(Chef, w.Zone.Engine.CombatantIds);
                Assert.Contains("chef_nord", w.Zone.Engine.CombatantIds);
                Assert.Equal(need, host.NextTickRegistryNeed);   // exacte : la borne d'avant est le besoin d'après
                continue;
            }
            Assert.False(w.RenewsSessions);
            Assert.Equal(0, w.Renewals);
        }
    }

    [Fact]
    public void Rappel_de_naissance_qui_leve_au_renouvellement_rien_n_a_change()
    {
        // Relecture indépendante de la PR #159 : le rappel de naissance est appelé avant toute mutation de l'hôte. S'il lève (une erreur
        // qui n'est pas un refus du moteur), le tick est refusé entier : même session, même tick du monde, aucun secteur fermé ni ouvert.
        WorldSession w = Open(ShortWeak(), safePoint: "guet_est");
        SectorWorld host = w.Host!;
        host.OnOpened((sector, zone, renewed) => throw new FormatException("essai"), sector => 0);
        while (!w.Zone.IsOver) w.Step();
        Zone before = w.Zone;
        long tick = host.WorldTick;
        string[] open = host.OpenSectors.ToArray();
        Assert.Throws<FormatException>(() => w.Step());
        Assert.Same(before, host.SectorZone("s01_00"));
        Assert.Equal(tick, host.WorldTick);
        Assert.Equal(open, host.OpenSectors);
        Assert.Equal(0, w.Renewals);
    }

    // --- Parité : Vallons d'une seule session contre Vallons à plusieurs secteurs ---------------------------------------------

    /// <summary>Forme des Vallons pour la parité : session de zone (sans région, le chemin d'avant), hôte au cœur seul, hôte à deux secteurs.</summary>
    private enum Shape { ZoneSession, Home, Wide }

    /// <summary>
    /// Vallons sans événement du monde (ils restent d'un seul secteur jusqu'à la partie suivante), un point sûr d'essai près de Croc-Gris,
    /// Croc-Gris affaibli ; l'archer bandit à 6 m du bord du cœur reculé. <see cref="Shape.ZoneSession"/> : sans région (zone en schéma 12,
    /// comme avant les secteurs) ; <see cref="Shape.Wide"/> : aire agrandie vers l'est jusqu'à x = 300 (deux secteurs).
    /// </summary>
    private static ZoneDefinition Vallons(Shape shape)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!.AsObject().Remove("events");
        root["data"]!["safePoints"]!.AsArray().Add(new JsonObject { ["id"] = "essai_croc", ["name"] = "Essai", ["position"] = new JsonArray(1, 34) });
        if (shape == Shape.ZoneSession)
        {
            root["schemaVersion"] = 12;
            root["data"]!.AsObject().Remove("region");
        }
        string directory = Path.Combine(Path.GetTempPath(), "eds_v228b2c_v_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "zones"));
        try
        {
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "bandit_arc1")!["position"] = new JsonArray(126, 68);
            if (shape == Shape.Wide) s["data"]!["space"]!["bounds"]!["max"]![0] = 300;
            // Toutes les créatures frappent à peine : le personnage traverse le Vallon sans tomber (la parité porte sur le monde, pas sur sa mort).
            foreach (JsonNode? c in s["data"]!["combatants"]!.AsArray())
                foreach (JsonNode? attack in c!["attacks"]?.AsArray() ?? new JsonArray()) attack!["rawDamage"] = 1;
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            // Croc-Gris affaibli (PV et coups) : le combat va jusqu'à la victoire en quelques centaines de ticks ; les autres chefs frappent à
            // peine (le personnage traverse le Vallon sans tomber).
            foreach (string chief in new[] { "vallons_croc_gris", "vallons_chef", "vallons_bandits" })
            {
                JsonObject forms = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/" + chief)))!.AsObject();
                foreach (JsonNode? form in forms["data"]!["combatants"]!.AsArray())
                {
                    if (chief == "vallons_croc_gris") form!["health"] = 10;
                    foreach (JsonNode? attack in form!["attacks"]?.AsArray() ?? new JsonArray()) attack!["rawDamage"] = 1;
                }
                File.WriteAllText(Path.Combine(directory, "zones", chief + ".json"), forms.ToJsonString());
            }
            CopyMissingZoneScenarios(directory);
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            return loaded.Zone!;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Joue <paramref name="ticks"/> ticks depuis <paramref name="safePoint"/> (Croc-Gris frappé s'il est là) ; rend la trace (ce
    /// que la partie rapporte, combattants du cœur tous les 100 ticks, sauvegarde), le journal et le nombre de secteurs ouverts au plus.
    /// </summary>
    private static (string Trace, string Log, int Open) Trace(ZoneDefinition zone, int ticks, string safePoint = "essai_croc")
    {
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 25);
        var log = new MemoryCombatLog();
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithXp(xp)), zone, ShippedData, Progression,
            ItemsCatalog, log: log, safePoint: safePoint);
        var trace = new StringBuilder();
        int open = 1;
        for (int i = 0; i < ticks; i++)
        {
            string? croc = w.BossForms.FirstOrDefault(f => f.StartsWith("croc_gris", StringComparison.Ordinal));
            if (i % 20 == 1 && croc != null)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", croc)); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
            open = Math.Max(open, w.Host?.OpenSectors.Count ?? 1);
            if (!step.IsEmpty)
                trace.Append(i).Append(" xp ").Append(string.Join(",", step.Experience.Select(e => $"{e.Source.Key}={e.Xp}")))
                    .Append(" butin ").Append(string.Join(",", step.Loot.Select(l => $"{l.Item}x{l.Count}:{l.Kept}")))
                    .Append(" vies ").Append(string.Join(",", step.Population.Select(p => $"{p.Kind}:{p.Population}:{p.Creature}:{p.Tick}")))
                    .Append(" chefs ").Append(string.Join(",", step.Boss.Select(b => $"{b.Kind}:{b.Boss}:{b.Form}:{b.Phase}:{b.Tick}")))
                    .Append(" récompenses ").Append(string.Join(",", step.Rewards.Select(r => $"{r.Item}x{r.Count}")))
                    .Append(step.Defeated ? " tombé" : string.Empty).Append('\n');
            if (i % 100 == 99)
                foreach (string id in w.Zone.Engine.CombatantIds)
                {
                    CombatantView v = w.Zone.Engine.View(id);
                    trace.Append(i).Append(' ').Append(id).Append(' ').Append(v.Position).Append(' ').Append(v.Health).Append(v.IsDead ? " mort" : "").Append('\n');
                }
        }
        Assert.Equal(0, w.Passages);
        CombatantView end = w.Zone.Engine.View(w.CombatantId);
        trace.Append("fin ").Append(end.Position).Append(' ').Append(end.MoveDirection).Append(' ').Append(end.Health).Append(' ').Append(end.IsDead).Append(' ').Append(end.Counters.CommandsRejected).Append('\n');
        trace.Append(SaveJsonReader.Write(w.Save().WithResume(null)));
        return (trace.ToString(), log.ToString(), open);
    }

    [Fact]
    public void Parite_Vallons_session_de_zone_contre_hote_au_coeur_seul()
    {
        // Relecture indépendante de la PR #159 : le chemin d'avant (session de zone, sans région) et l'hôte (cœur seul, chefs liés à leur
        // secteur) : même journal octet par octet, même trace, même sauvegarde.
        (string Trace, string Log, int Open) plain = Trace(Vallons(Shape.ZoneSession), 2400), hosted = Trace(Vallons(Shape.Home), 2400);
        Assert.Contains("Defeated:croc_gris", plain.Trace, StringComparison.Ordinal);
        Assert.Contains("item.collier_de_croc_gris", plain.Trace, StringComparison.Ordinal);
        Assert.Equal(plain.Log, hosted.Log);
        Assert.Equal(plain.Trace, hosted.Trace);
    }

    [Fact]
    public void Parite_Vallons_une_session_contre_hote_a_plusieurs_secteurs_sans_changer_de_secteur()
    {
        // Tant que le personnage reste dans le secteur du cœur, les Vallons à deux secteurs (hôte, chefs liés à leur secteur, voisin ouvert
        // près de lui : OnOpened appelé) jouent comme les Vallons d'une seule session : mêmes créatures, positions, PV, combat contre
        // Croc-Gris jusqu'à la victoire et la récompense, même sauvegarde.
        ZoneDefinition single = Vallons(Shape.Home), wide = Vallons(Shape.Wide);
        Assert.True(SectorMap.For(single).HomeHoldsArea);
        Assert.False(SectorMap.For(wide).HomeHoldsArea);
        (string Trace, string Log, int Open) a = Trace(single, 2400), b = Trace(wide, 2400);
        Assert.Contains("Defeated:croc_gris", a.Trace, StringComparison.Ordinal);
        Assert.Contains("item.collier_de_croc_gris", a.Trace, StringComparison.Ordinal);
        Assert.Equal(a.Trace, b.Trace);
        // Au hameau de la carrière (x = 80), le secteur voisin (x ≥ 140) est à moins de 96 m : il s'ouvre dès le début (OnOpened est appelé
        // pour lui) ; le cœur joue toujours de même.
        (string Trace, string Log, int Open) c = Trace(single, 1200, "hameau_de_la_carriere"), d = Trace(wide, 1200, "hameau_de_la_carriere");
        Assert.Equal((1, 2), (c.Open, d.Open));
        // La sauvegarde du monde à deux secteurs écrit en plus la session du voisin (sectors) : on compare la trace jusqu'à elle, et le
        // personnage sauvegardé.
        static string Played(string t) => t[..t.IndexOf("fin ", StringComparison.Ordinal)];
        static string Hero(string t) => JsonNode.Parse(t[t.IndexOf('{')..])!["data"]!["character"]!.ToJsonString();
        Assert.Equal(Played(c.Trace), Played(d.Trace));
        Assert.Equal(Hero(c.Trace), Hero(d.Trace));
    }
}
