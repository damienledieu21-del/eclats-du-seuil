using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
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
    private static bool Kill(WorldSession w, string population, List<SessionStep>? steps = null)
    {
        for (int i = 0; i < 1200; i++)
        {
            if (i % 20 == 0)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", w.CurrentCreature(population))); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
            steps?.Add(step);
            if (step.Experience.Count > 0) return true;
        }
        return false;
    }

    // --- Revue Codex de la PR #157 (1er tour) : relever, propager, puis remplacer ----------------------------------------------

    [Fact]
    public void Degats_du_dernier_tick_au_depart_comptes_pour_l_usure()
    {
        // Un loup du bord saigne le personnage (saignement : 6 s, un coup toutes les 2 s) ; il est vaincu, puis le personnage attend k ticks
        // et passe à l'est en saignant encore. Pour chaque k (un saignement tombe au tick même du départ pour l'un d'eux), tous les dégâts
        // reçus par ses deux combattants sont comptés pour l'usure : rapports d'usure et dégâts en attente.
        ZoneDefinition zone = Essai(
            z => z["data"]!["populations"]!.AsArray().Add(new JsonObject { ["creature"] = "loup_bord", ["respawnMs"] = 600_000, ["leashRange"] = 20, ["loot"] = "loot.loup" }),
            sc =>
            {
                var wolf = (JsonObject)sc["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "loup_ouest")!.DeepClone();
                wolf["id"] = "loup_bord";
                wolf["position"] = new JsonArray(244, 128);
                wolf["health"] = 120;
                wolf["attacks"]![0]!["applies"] = new JsonArray(new JsonObject { ["status"] = "bleed", ["coefficient"] = 0.5 });
                sc["data"]!["combatants"]!.AsArray().Add(wolf);
            });
        int bled = 0;
        for (int k = 0; k < 44; k++)
        {
            WorldSession w = Open(zone);
            var steps = new List<SessionStep>();
            WalkTo(w, 238, 128, steps);
            Assert.True(Kill(w, "loup_bord", steps));
            for (int i = 0; i < k; i++) steps.Add(w.Step());
            Zone west = w.Zone;
            string westId = w.CombatantId;
            WalkTo(w, 300, 128, steps);
            Assert.Equal(("s01_00", 1), (w.Sector, w.Passages));
            for (int i = 0; i < 200; i++) steps.Add(w.Step());   // saignement fini, combat fini : l'usure est passée
            long taken = west.Engine.View(westId).Counters.DamageTaken + w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken;
            long counted = steps.Sum(x => x.Wear?.Damage ?? 0) + w.PendingDamage;
            Assert.True(taken > 0);
            Assert.True(taken == counted, $"k = {k} : {taken} reçus, {counted} comptés");
            if (w.Zone.Engine.View(w.CombatantId).Counters.DamageTaken > 0) bled++;
        }
        Assert.Equal(0, bled);   // les altérations ne passent pas : le saignement s'arrête au passage
    }

    [Fact]
    public void Releves_repris_avec_les_ecarts_du_dernier_tick_du_sortant()
    {
        // Le sortant a reçu 50 dégâts et fait 7 coups, 2 blocages, 1 parade ; le dernier relevé en avait compté 40, 5, 2 et 0 (son dernier
        // tick n'est pas relevé). L'arrivant part de zéro. Le prochain relevé doit compter 10, 2, 0 et 1 de plus que l'arrivant.
        var rebased = WorldSession.Rebased((50, 7, 2, 1), (0, 0, 0, 0), (40, 5, 2, 0));
        Assert.Equal((-10L, -2L, 0L, -1L), rebased);
        // Propriété : arrivant quelconque, l'écart compté au relevé suivant est celui du sortant plus celui de l'arrivant.
        foreach (long arrive in new long[] { 0, 3, 120 })
        {
            var r = WorldSession.Rebased((50, 7, 2, 1), (arrive, arrive, arrive, arrive), (40, 5, 2, 0));
            Assert.Equal((10L, 2L, 0L, 1L), (arrive - r.Taken, arrive - r.Hits, arrive - r.Blocks, arrive - r.Parries));
        }
        // Rien de neuf chez le sortant : l'arrivant repart de ses propres compteurs.
        Assert.Equal((4L, 4L, 4L, 4L), WorldSession.Rebased((40, 5, 2, 0), (4, 4, 4, 4), (40, 5, 2, 0)));
    }

    [Fact]
    public void Nuit_d_une_creature_rare_tombee_dans_un_autre_secteur_au_tick_du_monde()
    {
        // La session de l'est s'ouvre ~20 s après celle de l'ouest ; la créature rare de l'est tombe à 22 h (début de la nuit) pendant que le
        // personnage est à l'ouest. Le tick de sa mort, relatif à la session de l'est, donne la nuit 0 ; relatif à celle de l'ouest, il
        // tomberait avant 22 h (de jour) et la créature reviendrait la même nuit.
        ZoneDefinition zone = Essai(
            z => z["data"]!["populations"]!.AsArray().Add(JsonNode.Parse("""
                { "creature": "loup_rare", "respawnMs": 600000, "leashRange": 25, "loot": "loot.loup",
                  "night": { "chancePercent": 35, "signMs": 15000, "sign": "text.zone.loup_blanc.signe" } }
                """)),
            sc =>
            {
                var wolf = (JsonObject)sc["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "loup_est")!.DeepClone();
                wolf["id"] = "loup_rare";
                wolf["position"] = new JsonArray(420, 60);
                sc["data"]!["combatants"]!.AsArray().Add(wolf);
            });
        var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
        const long start = 124_800;   // 21 h 52
        WorldSession w = Open(zone, new WorldSave(zone.Id, start, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
        WalkTo(w, 170, 128);
        Assert.Equal("s00_00", w.Sector);
        Zone east = w.Host!.SectorZone("s01_00")!;
        Assert.True(east.StartWorldTick > w.Zone.StartWorldTick + 100);
        while (w.Host.WorldTick < 126_100) w.Step();   // 22 h passées
        long death = checked(w.Host.WorldTick - 1 - east.StartWorldTick);   // tick de la session de l'est
        Assert.Null(NightCreatures.NightOf(clock.At(w.Zone.StartWorldTick + death)));   // l'ancienne conversion : de jour
        long night = NightCreatures.NightOf(clock.At(east.StartWorldTick + death))!.Value;
        string key = NightCreatures.Key(zone.Id, night, "loup_rare");
        Assert.False(w.Registered(key));
        w.AfterSector("s01_00", new[] { new ZoneEvent(ZoneEventKind.Death, "zone.essai/s01_00/1/death/loup_rare", "loup_rare", death, true) });
        Assert.True(w.Registered(key));
    }

    [Fact]
    public void Bonus_expire_au_tick_du_passage_retire_au_combattant_d_arrivee()
    {
        // Un élixir actif expire au tick même du départ (pour l'une des durées essayées) : le bonus retiré n'est pas perdu, il est appliqué
        // au combattant d'arrivée.
        ZoneDefinition zone = TwoSectors.Value;
        // Tick de la partie (compté depuis l'ouverture) où le passage a lieu, en marchant tout droit vers l'est.
        WorldSession timing = Open(zone);
        int passage = 1;
        CombatEngine first = timing.Zone.Engine;
        first.Submit(new CombatCommand(first.CurrentTick * first.TickMilliseconds, timing.CombatantId, new GroundVector(Fixed.One, Fixed.Zero)));
        while (timing.Passages == 0)
        {
            timing.Step();
            passage++;
        }
        Assert.True(passage > 100);
        foreach (int ticks in Enumerable.Range(passage - 3, 7))
        {
            SavedCharacter hero = Hero().WithBoons(new[] { new SavedBoon(BoonKind.Elixir, "item.elixir_de_vigueur", ticks) });
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog);
            GearBonus boosted = w.CombatGear;
            w.Step();
            CombatEngine e = w.Zone.Engine;
            e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId, new GroundVector(Fixed.One, Fixed.Zero)));
            while (w.Passages == 0) w.Step();
            for (int i = 0; i < 20; i++) w.Step();
            Assert.Empty(w.ActiveBoons);
            Assert.NotEqual(boosted, w.CombatGear);
            Assert.True(w.Zone.Engine.View(w.CombatantId).Gear!.Equals(w.CombatGear), $"durée {ticks}, passage au tick {passage}");
        }
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
    public void Aire_a_plusieurs_secteurs_avec_un_evenement_du_monde_reste_une_seule_session()
    {
        // Relecture indépendante de la PR #157 : les événements du monde ne sont pas encore liés à leur secteur : une aire à plusieurs secteurs
        // qui en a reste une seule session (comme avant), sans passage.
        ZoneDefinition zone = Essai(z => z["data"]!["events"] = JsonNode.Parse("""
            [{ "id": "chasse", "kind": "hunt", "everyDays": 1, "chancePercent": 50, "firstDay": 1, "start": "05:15", "spreadMinutes": 45,
               "announceMinutes": 0, "durationMinutes": 60,
               "groups": [{ "id": "ouest", "weight": 1, "creatures": [{ "like": "loup_ouest", "position": [60, 60] }, { "like": "loup_ouest", "position": [70, 60] }] }] }]
            """));
        Assert.Single(zone.Events);
        Assert.False(SectorMap.For(zone).HomeHoldsArea);
        WorldSession w = Open(zone);
        Assert.False(w.OnSectors);
        Assert.Null(w.Sector);
        Assert.Null(w.Zone.Sector);
        WalkTo(w, 300, 128);   // toute l'aire est une seule session : aucun passage
        Assert.Equal((0, "bastion", (string?)null), (w.Passages, w.CombatantId, w.Sector));
        Assert.InRange(Where(w).X, 299, 301);
    }

    [Fact]
    public void Personnage_sans_combattant_la_partie_ne_leve_pas_et_le_retrouve()
    {
        // Relecture indépendante de la PR #157 : en reprise d'un passage (refusé des deux côtés) ou refusé pour de bon, le personnage n'a
        // pas de combattant : chaque tick de la partie avance sans lever (aucun tick partiel), la session et l'identité restent les dernières.
        ZoneDefinition zone = TwoSectors.Value;
        foreach (bool refused in new[] { false, true })
        {
            WorldSession w = Open(zone);
            WalkTo(w, 200, 128);
            SectorWorld host = w.Host!;
            Zone before = w.Zone;
            if (refused) host.RefuseForTest("bastion", "essai");
            else host.ResumeForTest("bastion");
            Assert.False(host.IsFollowed("bastion"));
            long tick = host.WorldTick;
            w.Step();   // le tick où le personnage n'a pas de combattant : ne lève pas
            for (int i = 0; i < 19; i++) w.Step();
            Assert.Equal(tick + 20, host.WorldTick);
            Assert.Same(before, w.Zone);
            w.Save();
            if (refused)
            {
                Assert.Equal("bastion", w.CombatantId);   // la dernière identité, son combattant parti
                Assert.False(host.IsFollowed("bastion"));
                continue;
            }
            // La reprise replace le personnage dès qu'une place est libre (ici tout de suite) : la partie le retrouve, sous l'identité suivante.
            Assert.True(host.IsFollowed("bastion"));
            Assert.Equal(host.CombatantOf("bastion"), w.CombatantId);
            Assert.Equal("bastion_p1", w.CombatantId);
            Assert.True(w.Zone.Engine.View(w.CombatantId).IsPresent);
        }
    }

    [Fact]
    public void Passage_pendant_un_combat_au_bout_de_la_marge_la_creature_lache()
    {
        // Engagé par un loup près du bord, le personnage reste dans son secteur jusqu'au bout de la marge (32 m au-delà du bord) ; là, le
        // loup lâche (ni mort, ni butin) et le personnage passe au tick suivant.
        ZoneDefinition zone = Essai(
            z => z["data"]!["populations"]!.AsArray().Add(new JsonObject { ["creature"] = "loup_bord", ["respawnMs"] = 90_000, ["leashRange"] = 20, ["loot"] = "loot.loup" }),
            s =>
            {
                var w = (JsonObject)s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "loup_ouest")!.DeepClone();
                w["id"] = "loup_bord";
                w["position"] = new JsonArray(236, 128);
                w["health"] = 500;
                s["data"]!["combatants"]!.AsArray().Add(w);
            });
        WorldSession w = Open(zone);
        var steps = new List<SessionStep>();
        List<string> changes = WalkTo(w, 320, 128, steps);
        string change = Assert.Single(changes);
        Assert.EndsWith("s00_00->s01_00 bastion", change, StringComparison.Ordinal);
        Assert.Contains(steps.SelectMany(x => x.Population), e => e.Kind == PopulationEventKind.Leashed && e.Population == "loup_bord");
        Assert.Contains(steps, x => x.Population.Any(e => e.Kind == PopulationEventKind.Leashed));
        Assert.Empty(steps.SelectMany(x => x.Experience));   // lâché, pas vaincu
        Assert.False(w.IsDefeated);
        Assert.Equal(1, w.Passages);
    }

    [Fact]
    public void Bord_exact_hysteresis_et_aller_retour_rapide()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSession w = Open(zone);
        WalkTo(w, 256, 128);   // sur le bord : pas de passage
        Assert.Equal("s00_00", w.Sector);
        WalkTo(w, 263, 128);   // moins de 8 m au-delà : toujours pas
        Assert.Equal(("s00_00", 0), (w.Sector, w.Passages));
        WalkTo(w, 266, 128);   // plus de 8 m : passage
        Assert.Equal(("s01_00", 1), (w.Sector, w.Passages));
        WalkTo(w, 251, 128);   // revenu sous le bord, mais à moins de 8 m : on reste à l'est
        Assert.Equal(("s01_00", 1), (w.Sector, w.Passages));
        WalkTo(w, 245, 128);   // plus de 8 m à l'ouest : retour
        Assert.Equal(("s00_00", 2, "bastion_p1"), (w.Sector, w.Passages, w.CombatantId));
        WalkTo(w, 266, 128);   // et de nouveau à l'est, aussitôt
        Assert.Equal(("s01_00", 3), (w.Sector, w.Passages));
        Assert.True(w.Zone.Engine.View(w.CombatantId).IsPresent);
    }

    [Fact]
    public void Sauvegarde_abimee_secteurs_et_point_de_reprise()
    {
        ZoneDefinition zone = TwoSectors.Value;
        WorldSave fine = Open(zone).Save();
        // Point de reprise avec un secteur faux : le point sûr fait foi, le secteur est réécrit.
        WorldSave wrong = fine.WithResume(new SavedResume("camp_est", "s05_05"));
        WorldSession a = Open(zone, wrong, safePoint: WorldSession.ResumeSafePoint(wrong, zone));
        Assert.Equal("s01_00", a.Sector);
        Assert.Equal("s01_00", a.Save().Resume!.Sector);
        // Une session de secteur pour un secteur hors de la région : gardée telle quelle, sans effet.
        var odd = new WorldSave(fine.Zone, fine.WorldTick, fine.Sessions, fine.Consumed, fine.Character,
            sectors: new[] { new KeyValuePair<string, long>("s09_09", 4) }, resume: fine.Resume);
        WorldSession b = Open(zone, odd);
        WalkTo(b, 300, 128);
        Assert.Equal("s01_00", b.Sector);
        WorldSave kept = SaveJsonReader.Read(SaveJsonReader.Write(b.Save())).Save!;
        Assert.Equal((4L, 1L), (kept.Sectors["s09_09"], kept.Sectors["s01_00"]));
        // Session d'un secteur déjà au plafond du temps du monde : le secteur ne peut plus s'ouvrir ; le personnage reste à l'ouest, au
        // bout de la marge, et la partie avance (aucun tick partiel).
        var worn = new WorldSave(fine.Zone, fine.WorldTick, fine.Sessions, fine.Consumed, fine.Character,
            sectors: new[] { new KeyValuePair<string, long>("s01_00", SaveValidator.MaxWorldTick) }, resume: fine.Resume);
        Assert.True(SaveJsonReader.Read(SaveJsonReader.Write(worn)).IsValid);
        WorldSession c = Open(zone, worn);
        for (int i = 0; i < 2000 && Where(c).X < 280; i++)
        {
            CombatEngine e = c.Zone.Engine;
            if (i % 40 == 0) e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, c.CombatantId, new GroundVector(Fixed.One, Fixed.Zero)));
            c.Step();
        }
        for (int i = 0; i < 100; i++) c.Step();
        Assert.Equal(("s00_00", 0), (c.Sector, c.Passages));
        Assert.InRange(Where(c).X, 270, 288);
        Assert.DoesNotContain("s01_00", c.Host!.OpenSectors);
    }

    [Fact]
    public void Reserve_du_registre_de_tous_les_secteurs_ouverts_la_decouverte_attend()
    {
        // Relecture indépendante de la PR #157 : une inscription de la partie (ici la découverte d'un point sûr) garde la place des morts
        // possibles de tous les secteurs ouverts, comme la borne du tick du monde ; sinon elle remplit le registre et le monde ne peut plus
        // avancer. Deux secteurs ouverts (le personnage à 56 m du bord), registre presque plein.
        ZoneDefinition zone = Essai(z => z["data"]!["safePoints"]!.AsArray().Add(new JsonObject { ["id"] = "borne", ["name"] = "Borne", ["position"] = new JsonArray(212, 128) }));
        var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
        WorldSession w = Open(zone, new WorldSave(zone.Id, 0, 0, keys, Hero()));
        WalkTo(w, 200, 128);
        Assert.Equal(new[] { "s00_00", "s01_00" }, w.Host!.OpenSectors);
        Assert.False(w.IsDiscovered(zone, "borne"));
        int own = w.Zone.Engine.CombatantIds.Count, all = w.Host.OpenCombatants;
        Assert.True(all > own);
        // Place restante : de quoi inscrire une clé avec la seule réserve du secteur du personnage, pas avec celle de tous les secteurs.
        int fill = w.RegistryRoom - own - 1;
        for (int i = 0; i < fill; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
        Assert.Equal(own + 1, w.RegistryRoom);
        Assert.True(w.Host.NextTickRegistryNeed <= SaveValidator.MaxConsumed);
        // Il s'approche de la borne : la découverte attend ; le monde continue d'avancer, tick après tick.
        long tick = w.Host.WorldTick;
        WalkTo(w, 211, 128);
        for (int i = 0; i < 20; i++) w.Step();
        Assert.False(w.IsDiscovered(zone, "borne"));
        Assert.True(w.Host.WorldTick > tick + 20);
        Assert.True(w.Host.NextTickRegistryNeed <= SaveValidator.MaxConsumed);
    }
}
