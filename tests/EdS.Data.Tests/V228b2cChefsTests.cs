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

    [Fact]
    public void Session_renouvelee_le_chef_recommence_a_sa_premiere_forme()
    {
        // Session de 40 ticks : le chef, à sa seconde forme, revient à sa première avec la session suivante (comme à une halte) ; aucun
        // tick ne lève, la récompense n'est donnée qu'une fois.
        ZoneDefinition zone = Essai(scenario: s => s["data"]!["durationMs"] = 40_000, chief: c =>
        {
            foreach (JsonNode? form in c["data"]!["combatants"]!.AsArray())
                foreach (JsonNode? attack in form!["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
        });
        WorldSession w = Open(zone, safePoint: "guet_est", level: 20);
        Zone first = w.Zone;
        var steps = new List<SessionStep>();
        for (int i = 0; i < 2000 && !steps.Any(s => s.Boss.Any(b => b.Form == Chef + "_furieux")); i++)
        {
            if (i % 20 == 0 && ChiefForm(w) is string form)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
        }
        Assert.StartsWith(Chef + "_furieux", ChiefForm(w));
        Assert.Same(first, w.Zone);
        for (int i = 0; i < 2000 && ReferenceEquals(first, w.Zone); i++) steps.Add(w.Step());
        Assert.False(w.IsDefeated);
        Assert.Equal(1, w.Renewals);
        Assert.Equal(Chef, ChiefForm(w));
        Assert.Contains(Chef, w.Zone.Engine.CombatantIds);
        Assert.True(w.Zone.Engine.View(Chef).IsPresent);
        Assert.Contains(steps.Last().Boss, b => b.Kind == BossEventKind.FormArrived && b.Form == Chef);
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
    public void Plafond_du_registre_deux_chefs_qui_reviennent_comptent_dans_la_borne()
    {
        // Les chefs qui arrivent avec une session nouvelle comptent dans la borne du tick : une aire sans autre créature, deux chefs à l'est,
        // vaincus dans la session qui finit (aucune mort possible de plus pour eux) ; la session renouvelée les ramène tous les deux. Juste à
        // la borne, le tick est accepté entier ; sans eux dans la borne, le tick était accepté puis interrompu après le renouvellement.
        JsonArray crocForms = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons_croc_gris")))!["data"]!["combatants"]!.AsArray();
        JsonObject Weak(string id, double x)
        {
            var f = (JsonObject)crocForms[0]!.DeepClone();
            f["id"] = id;
            f["position"] = new JsonArray(x, 60);
            f["health"] = 5;
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
                s["data"]!["durationMs"] = 30_000;
            },
            chief: c => c["data"]!["combatants"] = new JsonArray(Weak(Chef, 326)),
            extra: (scenario, files) => files["essai_chef2"] = scenario(new JsonArray(Weak("chef_nord", 336))));
        foreach (int spare in new[] { 0, -1 })
        {
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
            long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 20);
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, Hero().WithXp(xp)), zone, ShippedData, Progression, ItemsCatalog, safePoint: "guet_est");
            int defeated = 0;
            for (int i = 0; i < 600 && defeated < 2 && !w.Zone.IsOver; i++)
            {
                string? target = w.BossForms.FirstOrDefault(f => w.Zone.Engine.CombatantIds.Contains(f) && !w.Zone.Engine.View(f).IsDead);
                if (i % 20 == 0 && target != null)
                    try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", target)); }
                    catch (ArgumentException) { }
                defeated += w.Step().Boss.Count(b => b.Kind == BossEventKind.Defeated);
            }
            Assert.Equal(2, defeated);
            while (!w.Zone.IsOver) w.Step();
            SectorWorld host = w.Host!;
            long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
            for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            if (spare == 0)
            {
                w.Step();
                Assert.Equal(1, w.Renewals);
                Assert.Contains(Chef, w.Zone.Engine.CombatantIds);
                Assert.Contains("chef_nord", w.Zone.Engine.CombatantIds);
                continue;
            }
            Assert.False(w.RenewsSessions);
        }
    }

    // --- Parité : Vallons d'une seule session contre Vallons à plusieurs secteurs ---------------------------------------------

    /// <summary>
    /// Vallons sans événement du monde (ils restent d'un seul secteur jusqu'à la partie suivante), un point sûr d'essai près de Croc-Gris ;
    /// <paramref name="wide"/> : aire agrandie vers l'est jusqu'à x = 300 (deux secteurs), sinon l'aire livrée (le cœur seul). L'archer
    /// bandit à 6 m du bord du cœur est reculé dans les deux.
    /// </summary>
    private static ZoneDefinition Vallons(bool wide)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!.AsObject().Remove("events");
        root["data"]!["safePoints"]!.AsArray().Add(new JsonObject { ["id"] = "essai_croc", ["name"] = "Essai", ["position"] = new JsonArray(1, 34) });
        string directory = Path.Combine(Path.GetTempPath(), "eds_v228b2c_v_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "zones"));
        try
        {
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "bandit_arc1")!["position"] = new JsonArray(126, 68);
            if (wide) s["data"]!["space"]!["bounds"]!["max"]![0] = 300;
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            // Croc-Gris affaibli (PV et coups) : le combat va jusqu'à la victoire en quelques centaines de ticks.
            JsonObject croc = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons_croc_gris")))!.AsObject();
            foreach (JsonNode? form in croc["data"]!["combatants"]!.AsArray())
            {
                form!["health"] = 10;
                foreach (JsonNode? attack in form["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
            }
            File.WriteAllText(Path.Combine(directory, "zones", "vallons_croc_gris.json"), croc.ToJsonString());
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

    private static string Trace(ZoneDefinition zone, int ticks)
    {
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 25);
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithXp(xp)), zone, ShippedData, Progression,
            ItemsCatalog, safePoint: "essai_croc");
        var trace = new StringBuilder();
        for (int i = 0; i < ticks; i++)
        {
            string? croc = w.BossForms.FirstOrDefault(f => f.StartsWith("croc_gris", StringComparison.Ordinal));
            if (i % 20 == 1 && croc != null)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", croc)); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
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
        trace.Append(SaveJsonReader.Write(w.Save().WithResume(null)).Replace("\"sectors\"", "\"sectors\""));
        return trace.ToString();
    }

    [Fact]
    public void Parite_Vallons_une_session_contre_hote_a_plusieurs_secteurs_sans_changer_de_secteur()
    {
        // Tant que le personnage reste dans le secteur du cœur, les Vallons à deux secteurs (hôte, chefs liés à leur secteur) jouent comme
        // les Vallons d'une seule session : mêmes créatures, mêmes positions, mêmes PV, même combat contre Croc-Gris (formes, victoire,
        // récompense), même sauvegarde.
        ZoneDefinition single = Vallons(false), wide = Vallons(true);
        Assert.True(SectorMap.For(single).HomeHoldsArea);
        Assert.False(SectorMap.For(wide).HomeHoldsArea);
        string a = Trace(single, 2400), b = Trace(wide, 2400);
        Assert.Contains("Defeated:croc_gris", a, StringComparison.Ordinal);
        Assert.Contains("item.collier_de_croc_gris", a, StringComparison.Ordinal);
        Assert.Equal(a, b);
    }
}
