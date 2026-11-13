using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
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
/// Sous-lot V2.28b (2), première partie (PLAN_GRANDE_CARTE.md § 2.3 et 2.4) : la partie solo (<see cref="WorldSession"/>) suit le
/// personnage d'un secteur à l'autre sur une aire à plusieurs secteurs : session et identité qui changent au passage, registre du monde
/// commun aux secteurs ouverts, populations et récompenses de chaque secteur, sauvegarde du monde (sessions des secteurs).
/// Zone d'essai : 512 × 256 m, deux secteurs (s00_00 à l'ouest, s01_00 à l'est), un loup dans chacun, un point sûr dans chacun.
/// </summary>
[Trait("Category", "Sector")]
public class V228b2PassageTests
{
    private static readonly Lazy<ZoneDefinition> TwoSectors = new(() => Essai());

    /// <summary>La zone d'essai, lue et validée comme une zone livrée (zone en schéma 14, scénario en schéma 18) ; retouches possibles.</summary>
    private static ZoneDefinition Essai(Action<JsonObject>? zone = null, Action<JsonObject>? scenario = null)
    {
        JsonObject loup = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!["data"]!["combatants"]!.AsArray()
            .Single(c => (string)c!["id"]! == "loup1")!.AsObject();
        JsonObject Wolf(string id, double x, double z, int health)
        {
            var w = (JsonObject)loup.DeepClone();
            w["id"] = id;
            w["position"] = new JsonArray(x, z);
            w["health"] = health;
            return w;
        }
        var s = new JsonObject
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
                ["combatants"] = new JsonArray(Wolf("loup_ouest", 100, 40, 150), Wolf("loup_est", 400, 128, 60)),
                ["commands"] = new JsonArray(),
            },
        };
        scenario?.Invoke(s);
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
                ["safePoints"] = new JsonArray(Safe("camp_ouest", "Camp de l'ouest", 64, 128), Safe("camp_est", "Camp de l'est", 420, 200)),
                ["places"] = new JsonArray(),
                ["populations"] = new JsonArray(Pop("loup_ouest"), Pop("loup_est")),
                ["passages"] = new JsonArray(),
            },
        };
        zone?.Invoke(root);
        string directory = Path.Combine(Path.GetTempPath(), "eds_v228b2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "zones"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "zones", "essai_secteurs.json"), s.ToJsonString());
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            return loaded.Zone!;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static GroundVector At(double x, double z) => new(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static (double X, double Z) Where(WorldSession w)
    {
        GroundVector p = w.Zone.Engine.View(w.CombatantId).Position!.Value;
        return (p.X.Raw / 1e4, p.Z.Raw / 1e4);
    }

    private static WorldSession Open(ZoneDefinition zone, WorldSave? save = null, MemoryCombatLog? log = null, string? safePoint = null)
    {
        save ??= new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero());
        var w = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, log: log, safePoint: safePoint);
        w.Step();
        return w;
    }

    /// <summary>
    /// Marche vers (x, z) tick par tick (la commande suit le personnage d'une session à l'autre : moteur et identité du moment) ; rend la
    /// trace des ticks où le secteur change. Échoue si la cible n'est pas atteinte.
    /// </summary>
    private static List<string> WalkTo(WorldSession w, double x, double z, List<SessionStep>? steps = null)
    {
        var changes = new List<string>();
        string? sector = w.Sector;
        (double, double)? sent = null;
        for (int i = 0; i < 4000; i++)
        {
            (double px, double pz) = Where(w);
            double dx = x - px, dz = z - pz, d = Math.Sqrt(dx * dx + dz * dz);
            CombatEngine e = w.Zone.Engine;
            if (d <= 1)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId, CombatAction.Stop));
                SessionStep last = w.Step();
                steps?.Add(last);
                return changes;
            }
            var dir = (Math.Round(dx / d, 3), Math.Round(dz / d, 3));
            if (sent != dir || i % 40 == 0)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId,
                    new GroundVector(Fixed.Parse(dir.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse(dir.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
                sent = dir;
            }
            SessionStep step = w.Step();
            steps?.Add(step);
            if (w.Sector != sector)
            {
                changes.Add($"{w.Zone.WorldTick} {sector}->{w.Sector} {w.CombatantId}");
                sector = w.Sector;
                sent = null;   // nouvelle session : la marche est emportée, la commande est redonnée au moteur d'arrivée
            }
        }
        throw new Xunit.Sdk.XunitException($"({x}, {z}) pas atteint ; arrêté en {Where(w)}");
    }

    [Fact]
    public void Aire_a_deux_secteurs_tenue_par_l_hote()
    {
        ZoneDefinition zone = TwoSectors.Value;
        Assert.False(SectorMap.For(zone).HomeHoldsArea);
        WorldSession w = Open(zone);
        Assert.True(w.OnSectors);
        Assert.Equal(("s00_00", "bastion", 0), (w.Sector, w.CombatantId, w.Passages));
        Assert.Equal("s00_00", w.Zone.Sector ?? "s00_00");
        Assert.Equal((64.0, 128.0), Where(w));
    }

    [Fact]
    public void Passage_a_l_est_puis_retour_session_identite_et_etat_suivis()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        Zone west = w.Zone;
        long health = west.Engine.View("bastion").Health!.Value;
        List<string> east = WalkTo(w, 300, 128);
        // Passage au-delà de 8 m du bord (x = 256) : la session et le secteur changent ; l'identité reste la sienne (session neuve).
        Assert.EndsWith("s00_00->s01_00 bastion", Assert.Single(east), StringComparison.Ordinal);
        Assert.Equal(("s01_00", "bastion", 1), (w.Sector, w.CombatantId, w.Passages));
        Assert.NotSame(west, w.Zone);
        Assert.Equal("s01_00", w.Zone.Sector);
        Assert.Equal(health, w.Zone.Engine.View("bastion").Health);
        Assert.True(west.Engine.View("bastion").HasLeft);
        Assert.InRange(Where(w).X, 299, 301);
        // Retour à l'ouest, dont la session est encore ouverte : son identité y a servi, il revient sous une identité nouvelle.
        List<string> back = WalkTo(w, 120, 128);
        Assert.EndsWith("s01_00->s00_00 bastion_p1", Assert.Single(back), StringComparison.Ordinal);
        Assert.Same(west, w.Zone);
        Assert.Equal(("s00_00", "bastion_p1", 2), (w.Sector, w.CombatantId, w.Passages));
        Assert.True(w.Zone.Engine.View("bastion_p1").IsPresent);
        Assert.False(w.InCombat);
        Assert.Equal(health, w.Zone.Engine.View("bastion_p1").Health);
    }

    [Fact]
    public void Creature_vaincue_dans_l_autre_secteur_experience_butin_et_cle_du_secteur()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        WalkTo(w, 396, 128);
        Assert.Equal("s01_00", w.Sector);
        var gains = new List<ExperienceGained>();
        var loot = new List<LootGained>();
        for (int i = 0; i < 600 && gains.Count == 0; i++)
        {
            if (i % 20 == 0)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", w.CurrentCreature("loup_est"))); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
            gains.AddRange(step.Experience);
            loot.AddRange(step.Loot);
        }
        ExperienceGained gain = Assert.Single(gains);
        Assert.Equal("zone.essai/s01_00/1/death/loup_est", gain.Source.Key);
        Assert.True(gain.Xp > 0);
        Assert.Equal(w.Character.Xp, gain.Xp);
        Assert.True(w.Registered("zone.essai/s01_00/1/death/loup_est"));
        Assert.All(loot, l => Assert.Equal(gain.Source.Key, l.Source.Key));
    }

    /// <summary>Frappe la créature de la population jusqu'à sa mort (le loup attaque aussi) ; rend vrai si elle est tombée.</summary>
    private static bool Kill(WorldSession w, string population)
    {
        for (int i = 0; i < 1200; i++)
        {
            if (i % 20 == 0)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", w.CurrentCreature(population))); }
                catch (ArgumentException) { }
            if (w.Step().Experience.Count > 0) return true;
        }
        return false;
    }

    [Fact]
    public void Degats_recus_comptes_pour_l_usure_d_un_secteur_a_l_autre()
    {
        // Le relevé des dégâts reçus (usure) repart du combattant d'arrivée : sans cela, les premiers dégâts reçus à l'est (autant que ceux
        // déjà reçus à l'ouest) seraient perdus.
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        WalkTo(w, 100, 42);
        Assert.True(Kill(w, "loup_ouest"));
        long westTaken = w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken;
        Assert.True(westTaken > 0);
        for (int i = 0; i < 400 && w.InCombat; i++) w.Step();
        w.Step();
        Assert.Equal(0L, w.PendingDamage);   // combat fini : l'usure est passée
        WalkTo(w, 300, 128);
        Assert.Equal("s01_00", w.Sector);
        Assert.Equal(0L, w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken);   // compteur du combattant d'arrivée
        WalkTo(w, 396, 128);
        for (int i = 0; i < 400 && w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken == 0; i++) w.Step();
        long eastTaken = w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken;
        Assert.InRange(eastTaken, 1, westTaken);
        Assert.Equal(eastTaken, w.PendingDamage);
    }

    [Fact]
    public void Point_sur_decouvert_une_seule_fois_quel_que_soit_le_secteur()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        var steps = new List<SessionStep>();
        WalkTo(w, 300, 200, steps);
        WalkTo(w, 419, 200, steps);   // à 1 m du camp de l'est : découvert
        Assert.True(w.IsDiscovered(zone, "camp_est"));
        WalkTo(w, 200, 200, steps);   // retour à l'ouest (session de l'ouest encore ouverte, recopie du registre d'avant la découverte)
        Assert.Equal("s00_00", w.Sector);
        Assert.True(w.IsDiscovered(zone, "camp_est"));
        WalkTo(w, 419, 200, steps);
        Assert.Equal(1, steps.SelectMany(s => s.Story).Count(e => e.Kind == StoryEventKind.SafePointDiscovered && e.Id == "camp_est"));
        Assert.Equal("camp_est", w.LastSafePoint);
        Assert.Equal(("camp_est", "s01_00"), (w.Resume!.SafePoint, w.Resume.Sector));
    }

    [Fact]
    public void Sauvegarde_du_monde_et_partie_reprise_dans_le_secteur_du_point_sur()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        WalkTo(w, 419, 200);
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        // Session du monde, registre du monde (découvertes faites à l'est), dernière session de chaque secteur autre que le cœur.
        Assert.Equal(1L, save.Sessions);
        Assert.Contains("zone.essai/safe/camp_est", save.Consumed);
        Assert.Contains("zone.essai/safe/camp_ouest", save.Consumed);
        Assert.Equal(1L, save.Sectors["s01_00"]);
        Assert.False(save.Sectors.ContainsKey("s00_00"));
        Assert.Equal(("camp_est", "s01_00"), (save.Resume!.SafePoint, save.Resume.Sector));
        Assert.Equal(w.Zone.WorldTick, save.WorldTick);
        // Reprise au camp de l'est : la session de l'est s'ouvre la première, sous sa session suivante ; la partie y est.
        WorldSession again = Open(zone, save, safePoint: WorldSession.ResumeSafePoint(save, zone));
        Assert.Equal(("s01_00", "bastion", 0), (again.Sector, again.CombatantId, again.Passages));
        Assert.Equal(2L, again.Zone.Session);
        Assert.Equal((420.0, 200.0), Where(again));
        Assert.True(again.IsDiscovered(zone, "camp_est"));
        WorldSave next = again.Save();
        Assert.Equal((2L, 2L), (next.Sessions, next.Sectors["s01_00"]));
    }

    [Fact]
    public void Meme_partie_meme_trace_deux_fois()
    {
        // Déterminisme : les mêmes entrées donnent les mêmes journaux de chaque secteur, les mêmes passages et la même sauvegarde.
        string Run()
        {
            var log = new MemoryCombatLog();
            WorldSession w = Open(TwoSectors.Value, log: log);
            var trace = new StringBuilder();
            foreach ((double x, double z) in new[] { (300.0, 128.0), (120.0, 128.0), (300.0, 60.0) })
                trace.AppendJoin("\n", WalkTo(w, x, z)).Append('\n');
            trace.Append(w.Zone.Engine.Snapshot().Fingerprint).Append('\n').Append(SaveJsonReader.Write(w.Save())).Append(log.ToString());
            return trace.ToString();
        }
        string a = Run(), b = Run();
        Assert.Contains("s01_00->s00_00 bastion_p1", a, StringComparison.Ordinal);
        Assert.Contains("s00_00->s01_00 bastion_p2", a, StringComparison.Ordinal);   // l'est aussi a déjà servi « bastion » : identité suivante
        Assert.Equal(a, b);
    }

    [Fact]
    public void Registre_du_monde_au_plafond_tick_refuse_entier()
    {
        // Atomicité près du plafond : la borne est celle du monde entier (registre et sessions ouvertes) ; un tick refusé ne change rien.
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession Filled(int spare)
        {
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, Hero()), zone, ShippedData, Progression, ItemsCatalog);
            Assert.True(w.OnSectors);
            int fill = w.RegistryRoom - w.Zone.NewDeathsPossible - spare;
            for (int i = 0; i < fill; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            Assert.Equal(spare, w.RegistryRoom - w.Zone.NewDeathsPossible);
            return w;
        }
        // Juste la place des morts possibles : accepté.
        WorldSession ok = Filled(0);
        ok.Step();
        Assert.Equal(1L, ok.Zone.Engine.CurrentTick);
        // Une clé de trop : refusé entier, deux fois, rien n'a bougé.
        WorldSession full = Filled(-1);
        long tick = full.Zone.Engine.CurrentTick;
        int count = full.Zone.ConsumedCount;
        Assert.Throws<InvalidOperationException>(() => full.Step());
        Assert.Throws<InvalidOperationException>(() => full.Step());
        Assert.Equal((tick, count, 0), (full.Zone.Engine.CurrentTick, full.Zone.ConsumedCount, full.Passages));
        Assert.True(full.RegistryRoom <= full.Zone.RegistryRoom);
    }

    [Fact]
    public void Aire_a_plusieurs_secteurs_avec_chef_ou_evenement_reste_une_session()
    {
        // Les chefs et les événements du monde ne sont pas encore liés à leur secteur : une telle zone reste une seule session, comme avant.
        ZoneDefinition vallons = V11TestData.Zone();
        Assert.True(vallons.Bosses.Count > 0 || vallons.Events.Count > 0);
        Assert.True(SectorMap.For(vallons).HomeHoldsArea);   // les Vallons d'aujourd'hui : le cœur seul, tenu par l'hôte (V2.28b, première partie)
        Assert.True(new WorldSession(new WorldSave(vallons.Id, 0, 0, Array.Empty<string>(), Hero()), vallons, ShippedData, Progression, ItemsCatalog).OnSectors);
    }
}
