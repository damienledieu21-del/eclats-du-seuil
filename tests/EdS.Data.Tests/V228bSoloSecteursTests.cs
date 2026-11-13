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
/// Sous-lot V2.28b, première partie (PLAN_GRANDE_CARTE.md § 2.3, « en solo, la partie joue le rôle de l'hôte ») : la session d'une zone à
/// région est tenue par l'hôte des secteurs (<see cref="SectorWorld"/>), le personnage entré avec la session de son secteur. Tant que
/// l'aire tient dans le secteur du cœur, la partie est <b>exactement</b> celle d'avant : même journal octet par octet, mêmes événements,
/// même sauvegarde que la session d'une zone sans région. Règle d'arrivée à un point sûr, la même pour toute session.
/// </summary>
[Trait("Category", "Sector")]
public class V228bSoloSecteursTests
{
    /// <summary>Les Vallons livrés, avec leur région (schéma courant) ou sans (schéma 12 : l'ancien fichier), scénario retouché au besoin.</summary>
    private static ZoneDefinition Vallons(bool region, Action<JsonObject>? scenario = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        if (!region)
        {
            root["schemaVersion"] = 12;
            root["data"]!.AsObject().Remove("region");
        }
        string directory = ScenariosDirectory;
        string? temporary = null;
        if (scenario != null)
        {
            temporary = directory = Path.Combine(Path.GetTempPath(), "eds_v228b_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            scenario(s);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            CopyMissingZoneScenarios(directory);
        }
        try
        {
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            return loaded.Zone!;
        }
        finally
        {
            if (temporary != null) Directory.Delete(temporary, true);
        }
    }

    /// <summary>Un loup fragile à deux mètres de l'entrée : le personnage le tue, il réapparaît (populations).</summary>
    private static void WolfNearby(JsonObject scenario)
    {
        JsonObject wolf = scenario["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
        wolf["health"] = 60;
        wolf["position"] = new JsonArray(-16, -12);
    }

    private static GroundVector At(double x, double z) => new(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static SavedCharacter HeroAt(int level)
    {
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);
        return Hero().WithXp(xp);
    }

    private static WorldSave Start(ZoneDefinition zone, long worldTick, int level, long sessions = 0) =>
        new(zone.Id, worldTick, sessions, new[] { PlayerCharacters.PlacesKey }, HeroAt(level));

    /// <summary>Joue <paramref name="ticks"/> ticks ; rend le journal, la trace de tout ce que la session rapporte et la sauvegarde finale.</summary>
    private static (string Log, string Trace, WorldSave Save, WorldSession Session) Play(ZoneDefinition zone, long worldTick, int level, int ticks, bool fight)
    {
        var log = new MemoryCombatLog();
        var session = new WorldSession(Start(zone, worldTick, level), zone, ShippedData, Progression, ItemsCatalog, Book(zone), log: log);
        var trace = new StringBuilder();
        for (int i = 0; i < ticks; i++)
        {
            if (fight && i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", session.CurrentCreature("loup1"))); }
                catch (ArgumentException) { }
            SessionStep step = session.Step();
            if (!step.IsEmpty)
                trace.Append(i).Append(" xp ").Append(string.Join(",", step.Experience.Select(e => $"{e.Source.Key}={e.Xp}")))
                    .Append(" butin ").Append(string.Join(",", step.Loot.Select(l => $"{l.Item}x{l.Count}:{l.Kept}")))
                    .Append(" vies ").Append(string.Join(",", step.Population.Select(p => $"{p.Kind}:{p.Population}:{p.Creature}:{p.Tick}")))
                    .Append(" histoire ").Append(string.Join(",", step.Story.Select(e => $"{e.Kind}:{e.Id}:{e.Step}")))
                    .Append(" chefs ").Append(string.Join(",", step.Boss.Select(b => $"{b.Kind}:{b.Boss}:{b.Form}")))
                    .Append(" monde ").Append(string.Join(",", step.Events.Select(e => $"{e.Kind}:{e.Event}:{e.Group}:{e.Day}:{e.Wave}")))
                    .Append(step.Defeated ? " tombé" : string.Empty).Append('\n');
            if (i % 100 == 99 && !session.Zone.IsOver) trace.Append(i).Append(" empreinte ").Append(session.Zone.Engine.Snapshot().Fingerprint).Append('\n');
        }
        return (log.ToString(), trace.ToString(), session.Save(), session);
    }

    // --- Qui est tenu par l'hôte ------------------------------------------------------------------------------------------

    [Fact]
    public void Zone_a_region_sur_l_hote_les_autres_comme_avant()
    {
        ZoneDefinition vallons = Vallons(true), old = Vallons(false);
        Assert.NotNull(vallons.Region);
        Assert.True(SectorMap.For(vallons).HomeHoldsArea);
        Assert.True(new WorldSession(Start(vallons, 0, 1), vallons, ShippedData, Progression, ItemsCatalog).OnSectors);
        // Sans région (l'ancien fichier, un intérieur) et sans zone du monde : une session de zone, comme avant.
        Assert.Null(old.Region);
        Assert.False(new WorldSession(Start(old, 0, 1), old, ShippedData, Progression, ItemsCatalog).OnSectors);
        ZoneDefinition inn = V16TestData.Interiors.Single(z => z.Id == "zone.auberge");
        Assert.Null(inn.Region);
        Assert.False(new WorldSession(Start(inn, 0, 1), inn, ShippedData, Progression, ItemsCatalog, storyZone: vallons.Id).OnSectors);
        Assert.False(new WorldSession(Start(vallons, 0, 1), vallons.Setup, ShippedData, Progression, ItemsCatalog,
            new CombatantPlacement(At(-18, -12), WorldSession.CharacterRadius)).OnSectors);
    }

    // --- Exactement la partie d'avant ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0L, 1, true)]          // 8 h, niveau 1, un loup tué puis revenu
    [InlineData(117_000L, 12, false)]  // 21 h : l'attaque du hameau (niveau 12)
    [InlineData(135_000L, 12, true)]   // 23 h : la nuit, la faille du Seuil, les créatures à heures
    [InlineData(567_000L, 25, false)]  // jour 2, 23 h, niveau 25
    public void Meme_journal_memes_evenements_meme_sauvegarde_que_la_session_de_zone(long worldTick, int level, bool fight)
    {
        Action<JsonObject>? scenario = fight ? WolfNearby : null;
        ZoneDefinition hosted = Vallons(true, scenario), plain = Vallons(false, scenario);
        int ticks = fight ? 2400 : 800;
        var a = Play(hosted, worldTick, level, ticks, fight);
        var b = Play(plain, worldTick, level, ticks, fight);
        Assert.True(a.Session.OnSectors);
        Assert.False(b.Session.OnSectors);
        Assert.True(a.Log.Length > 10_000);
        Assert.Equal(b.Log, a.Log);        // octet par octet, en-tête compris (le personnage fait partie de la simulation de départ)
        Assert.Equal(b.Trace, a.Trace);
        if (fight)
        {
            Assert.Contains("/death/loup1=", a.Trace, StringComparison.Ordinal);
            Assert.Contains(" vies ", a.Trace, StringComparison.Ordinal);
            Assert.Contains("Respawned", a.Trace, StringComparison.Ordinal);   // le loup est revenu : les populations ont tourné sur l'hôte
        }
        // Même sauvegarde ; seule la zone à région écrit le secteur du point de reprise.
        Assert.Equal(SaveJsonReader.Write(b.Save.WithResume(null)), SaveJsonReader.Write(a.Save.WithResume(null)));
        Assert.Equal((b.Save.Resume!.SafePoint, (string?)null, "s07_06"), (a.Save.Resume!.SafePoint, b.Save.Resume.Sector, a.Save.Resume.Sector));
        Assert.Equal((b.Session.Zone.Session, b.Session.Zone.WorldTick, b.Session.Zone.ConsumedCount), (a.Session.Zone.Session, a.Session.Zone.WorldTick, a.Session.Zone.ConsumedCount));
    }

    [Fact]
    public void Session_terminee_pas_de_renouvellement_la_partie_reprend_comme_avant()
    {
        // Une session courte (5 s) : à sa fin, l'hôte ne la renouvelle pas sous la partie solo ; rien n'avance plus, comme une session de zone.
        static void Short(JsonObject s) => s["data"]!["durationMs"] = 5_000;
        ZoneDefinition hosted = Vallons(true, Short), plain = Vallons(false, Short);
        var a = Play(hosted, 0, 1, 140, false);
        var b = Play(plain, 0, 1, 140, false);
        Assert.True(a.Session.Zone.IsOver);
        Assert.Equal(b.Log, a.Log);
        Assert.Equal(b.Trace, a.Trace);
        Assert.Equal((1L, 100L), (a.Session.Zone.Session, a.Session.Zone.WorldTick));
        Assert.Equal(SaveJsonReader.Write(b.Save.WithResume(null)), SaveJsonReader.Write(a.Save.WithResume(null)));
    }

    [Fact]
    public void Un_tick_apres_la_fin_de_session_puis_sauvegarde_et_session_suivante()
    {
        // Relecture indépendante de la PR #156 : la session terminée reste chez l'hôte, qui n'est plus appelé (il la renouvellerait). Un tick
        // de plus ne fait rien et ne lève pas ; la sauvegarde est celle d'une session de zone ; la session suivante s'ouvre de même.
        static void Short(JsonObject s) => s["data"]!["durationMs"] = 5_000;
        ZoneDefinition hosted = Vallons(true, Short), plain = Vallons(false, Short);
        var logs = new Dictionary<bool, string>();
        var saves = new Dictionary<bool, string>();
        foreach (bool region in new[] { true, false })
        {
            ZoneDefinition zone = region ? hosted : plain;
            var log = new MemoryCombatLog();
            var session = new WorldSession(Start(zone, 0, 1), zone, ShippedData, Progression, ItemsCatalog, Book(zone), log: log);
            Assert.Equal(region, session.OnSectors);
            for (int i = 0; i < 100; i++) session.Step();
            Assert.True(session.Zone.IsOver);
            int lines = log.Lines.Count;
            long tick = session.Zone.WorldTick;
            Assert.True(session.Step().IsEmpty);   // un tick après la fin : rien, sans exception
            Assert.True(session.Step().IsEmpty);
            Assert.Equal((lines, tick, 1L), (log.Lines.Count, session.Zone.WorldTick, session.Zone.Session));
            WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
            Assert.Equal((100L, 1L), (save.WorldTick, save.Sessions));
            // La session suivante : numéro suivant, même temps du monde, personnage entré.
            var next = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, Book(zone), log: log);
            Assert.Equal(region, next.OnSectors);
            for (int i = 0; i < 30; i++) next.Step();
            Assert.Equal((2L, 130L), (next.Zone.Session, next.Zone.WorldTick));
            Assert.True(next.Zone.Engine.View("bastion").IsPresent);
            logs[region] = log.ToString();
            saves[region] = SaveJsonReader.Write(next.Save().WithResume(null));
        }
        Assert.Equal(logs[false], logs[true]);
        Assert.Equal(saves[false], saves[true]);
    }

    [Fact]
    public void Registre_au_plafond_meme_tick_accepte_et_meme_refus_entier_que_la_session_de_zone()
    {
        // Relecture indépendante de la PR #156 : près du plafond du registre, la partie tenue par l'hôte accepte et refuse exactement les
        // mêmes ticks que la session de zone ; un tick refusé ne change rien (ni temps, ni moteur, ni registre).
        foreach (bool region in new[] { true, false })
        {
            ZoneDefinition zone = Vallons(region);
            // Session ouverte avec un registre presque plein, puis rempli jusqu'à laisser <room> places de plus que les morts possibles d'un tick.
            WorldSession At(int room)
            {
                var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.vallons/test/k{i}").Append(PlayerCharacters.PlacesKey);
                var session = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, HeroAt(1)), zone, ShippedData, Progression, ItemsCatalog);
                int fill = SaveValidator.MaxConsumed - session.Zone.ConsumedCount - session.Zone.NewDeathsPossible - room;
                for (int i = 0; i < fill; i++) Assert.True(session.Zone.Consume($"zone.vallons/test/plein{i}"));
                return session;
            }
            // Juste la place des morts possibles d'un tick : accepté, et le registre reste sous le plafond.
            WorldSession exact = At(0);
            Assert.Equal(region, exact.OnSectors);
            Assert.Equal(SaveValidator.MaxConsumed, exact.Zone.ConsumedCount + exact.Zone.NewDeathsPossible);
            Assert.InRange(exact.Zone.NewDeathsPossible, 10, 300);
            exact.Step();
            Assert.Equal(1L, exact.Zone.Engine.CurrentTick);
            Assert.InRange(exact.Zone.ConsumedCount, 0, SaveValidator.MaxConsumed);
            // Une clé de trop : refus entier, deux fois de suite, rien n'a bougé.
            WorldSession full = At(-1);
            Assert.Equal(SaveValidator.MaxConsumed + 1, full.Zone.ConsumedCount + full.Zone.NewDeathsPossible);
            long tick = full.Zone.WorldTick;
            int count = full.Zone.ConsumedCount;
            Assert.Throws<InvalidOperationException>(() => full.Step());
            Assert.Throws<InvalidOperationException>(() => full.Step());
            Assert.Equal((0L, tick, count), (full.Zone.Engine.CurrentTick, full.Zone.WorldTick, full.Zone.ConsumedCount));
            Assert.False(full.Zone.Engine.View("bastion").IsPresent);   // même son arrivée n'a pas eu lieu
        }
    }

    // --- Parité à travers les sessions : couvre-feu, défaite, halte, voyage --------------------------------------------------

    /// <summary>Un loup mortel à deux mètres de l'entrée : le personnage tombe en quelques secondes.</summary>
    private static void DeadlyWolf(JsonObject scenario)
    {
        JsonObject wolf = scenario["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
        wolf["position"] = new JsonArray(-16, -12);
        foreach (JsonNode? attack in wolf["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
    }

    /// <summary>
    /// Enchaîne les sessions comme le fait l'aventure (sauvegarde, puis session nouvelle au point sûr par la règle d'arrivée) : couvre-feu au
    /// Bourg la nuit, défaite au village, reprise, halte, voyage rapide. Rend tous les journaux, toutes les sauvegardes et la trace.
    /// </summary>
    private static (string Logs, string Saves, string Trace) Chain(ZoneDefinition zone)
    {
        var logs = new StringBuilder();
        var saves = new StringBuilder();
        var trace = new StringBuilder();
        var all = new List<MemoryCombatLog>();
        StoryBook story = Book(zone);
        SavedCharacter hero = Hero().WithStory(new SavedStory(reputation: new Dictionary<string, int> { ["guard"] = -700 }));
        var save = new WorldSave(zone.Id, 130_000, 0, new[] { PlayerCharacters.PlacesKey }, hero);   // 22 h 27 : la nuit, la garde hait le personnage
        WorldSession Open(string? safePoint)
        {
            (string? at, GroundVector? place) = WorldSession.SafeArrival(save, zone, Progression, safePoint, zone.Id);
            var log = new MemoryCombatLog();
            var session = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, story, log: log, safePoint: at, place: place);
            trace.Append("session ").Append(session.Zone.Session).Append(" à ").Append(session.LastSafePoint).Append(' ').Append(place?.ToString() ?? "sa place").Append('\n');
            all.Add(log);
            return session;
        }
        void Close(WorldSession session, string why)
        {
            save = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
            saves.Append(why).Append('\n').Append(SaveJsonReader.Write(save.WithResume(null)));
            trace.Append(why).Append(" tick ").Append(session.Zone.Engine.CurrentTick).Append(" reprise ").Append(save.Resume!.SafePoint).Append('\n');
        }
        // 1. Au Bourg, la nuit : le couvre-feu tombe ; le personnage est reconduit au point sûr du couvre-feu.
        WorldSession bourg = Open("place_du_bourg");
        string? curfew = null;
        for (int i = 0; i < 40 && curfew is null; i++)
        {
            bourg.Step();
            curfew = bourg.CurfewSafePoint;
        }
        Assert.Equal("place_du_village", curfew);
        Close(bourg, "couvre-feu");
        // 2. Au village, le loup mortel : défaite.
        WorldSession village = Open(curfew);
        for (int i = 0; i < 1200 && !village.IsDefeated; i++) village.Step();
        Assert.True(village.IsDefeated);
        Close(village, "défaite");
        // 3. Reprise au dernier point sûr, puis halte (sauvegarde et session nouvelle au même point sûr).
        WorldSession again = Open(save.Resume!.SafePoint);
        for (int i = 0; i < 5; i++) again.Step();
        Close(again, "halte");
        WorldSession rested = Open(save.Resume!.SafePoint);
        for (int i = 0; i < 5; i++) rested.Step();
        Close(rested, "voyage");
        // 4. Voyage rapide vers la place du Bourg.
        WorldSession travelled = Open("place_du_bourg");
        for (int i = 0; i < 100; i++) travelled.Step();
        Close(travelled, "fin");
        foreach (MemoryCombatLog log in all) logs.Append(log.ToString());
        return (logs.ToString(), saves.ToString(), trace.ToString());
    }

    [Fact]
    public void Couvre_feu_defaite_halte_et_voyage_meme_journal_et_memes_sauvegardes_que_la_session_de_zone()
    {
        // Relecture indépendante de la PR #156 : la parité tient à travers les changements de session de la partie.
        var a = Chain(Vallons(true, DeadlyWolf));
        var b = Chain(Vallons(false, DeadlyWolf));
        Assert.True(a.Logs.Length > 50_000);
        Assert.Equal(b.Logs, a.Logs);
        Assert.Equal(b.Saves, a.Saves);
        Assert.Equal(b.Trace, a.Trace);
        Assert.Contains("couvre-feu", a.Trace, StringComparison.Ordinal);
        Assert.Contains("défaite", a.Trace, StringComparison.Ordinal);
        Assert.Contains("session 5 à place_du_bourg", a.Trace, StringComparison.Ordinal);
    }

    // --- L'hôte : entrée avec la session, début de session, crochets du tick --------------------------------------------------

    private static CharacterSetup Player(ZoneDefinition zone, double x, double z)
    {
        var session = new WorldSession(Start(zone, 0, 1), zone, ShippedData, Progression, ItemsCatalog);
        var hero = (CharacterSetup)session.Zone.Setup.Combatants.Single(c => c.Id == "bastion");
        return new CharacterSetup(hero.Id, hero.Team, hero.Class, hero.Build, hero.Initiative, new CombatantPlacement(At(x, z), WorldSession.CharacterRadius));
    }

    [Fact]
    public void Entree_avec_la_session_meme_journal_que_la_zone_et_refus()
    {
        ZoneDefinition zone = Vallons(true);
        CharacterSetup hero = Player(zone, -18, -12);
        var log = new MemoryCombatLog();
        var reference = new MemoryCombatLog();
        SectorWorld world = SectorWorld.For(zone, 1200, 4, logs: (sector, session) => log);
        Assert.Empty(world.OpenSectors);
        world.EnterAtStart(hero);
        Assert.Equal(new[] { "s07_06" }, world.OpenSectors);
        Assert.Equal(("s07_06", "bastion", false, false), (world.SectorOf("bastion"), world.CombatantOf("bastion"), world.IsWaiting("bastion"), world.IsResuming("bastion")));
        var same = new Zone(zone.Id, ZoneSetups.For(zone, Array.Empty<string>()), 4, 1200, null, reference, entering: new[] { hero });
        var lives = new ZonePopulations(zone);
        Assert.Equal(reference.ToString(), log.ToString());   // même en-tête : le personnage est dans la simulation de départ
        IReadOnlyList<SectorEvent> first = world.Step();
        same.Step();
        lives.Step(same);
        Assert.Equal(SectorEventKind.Opened, Assert.Single(first).Kind);   // l'ouverture faite hors d'un tick est rendue au tick suivant
        Assert.True(world.ViewOf("bastion").IsPresent);
        Assert.Equal(At(-18, -12), world.ViewOf("bastion").Position);
        for (int i = 0; i < 100; i++)
        {
            Assert.Empty(world.Step());
            same.Step();
            lives.Step(same);
        }
        Assert.Equal(reference.ToString(), log.ToString());
        Assert.Equal(same.Engine.Snapshot().Fingerprint, world.SectorZone("s07_06")!.Engine.Snapshot().Fingerprint);
        // Refus, sans rien changer : déjà là, sans place, hors de l'aire, secteur déjà ouvert.
        Assert.Throws<ArgumentException>(() => world.EnterAtStart(hero));
        Assert.Throws<ArgumentNullException>(() => world.EnterAtStart(null!));
        var other = new CharacterSetup("alice", hero.Team, hero.Class, hero.Build, 0, new CombatantPlacement(At(-20, -12), WorldSession.CharacterRadius));
        Assert.Throws<InvalidOperationException>(() => world.EnterAtStart(other));
        SectorWorld empty = SectorWorld.For(zone, 0, 1);
        Assert.Throws<ArgumentException>(() => empty.EnterAtStart(new CharacterSetup("alice", hero.Team, hero.Class, hero.Build, 0)));
        Assert.Throws<ArgumentException>(() => empty.EnterAtStart(new CharacterSetup("alice", hero.Team, hero.Class, hero.Build, 0,
            new CombatantPlacement(At(500, 500), WorldSession.CharacterRadius))));
        Assert.Empty(empty.OpenSectors);
        Assert.Empty(empty.Players);
    }

    [Fact]
    public void Debut_de_session_selon_l_heure_et_le_temps_du_monde()
    {
        // Les créatures à heures ne sont là d'emblée qu'à leurs heures : l'hôte ouvre chaque session au moment du monde, comme une session de zone.
        ZoneDefinition zone = Vallons(true);
        Population timed = zone.Populations.First(p => p.Hours != null);
        var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
        Func<long, (DayPhase?, WeatherKind?)> start = tick => (clock.At(tick).Phase, WorldWeather.At(zone.Id, clock.At(tick)).Kind);
        var present = new List<bool>();
        foreach (long tick in new[] { 0L, 27_000L, 108_000L, 135_000L, 189_000L })   // 8 h, 11 h, 20 h, 23 h, 5 h
        {
            SectorWorld world = SectorWorld.For(zone, tick, 1, start: start);
            world.EnterAtStart(Player(zone, -18, -12));
            Zone sector = world.SectorZone("s07_06")!;
            CombatSetup expected = ZoneSetups.For(zone, Array.Empty<string>(), clock.At(tick).Phase, WorldWeather.At(zone.Id, clock.At(tick)).Kind);
            Assert.Equal(expected.Combatants.Select(c => c.Id).Append("bastion"), sector.Setup.Combatants.Select(c => c.Id));
            present.Add(sector.Setup.Combatants.Any(c => c.Id == timed.Creature));
            Assert.Equal(ZonePopulations.PresentAtStart(timed, clock.At(tick).Phase, WorldWeather.At(zone.Id, clock.At(tick)).Kind), present.Last());
        }
        Assert.Contains(true, present);
        Assert.Contains(false, present);
        // Sans début de session donné : toutes là, comme avant.
        SectorWorld all = SectorWorld.For(zone, 135_000, 1);
        all.EnterAtStart(Player(zone, -18, -12));
        Assert.Equal(ZoneSetups.For(zone, Array.Empty<string>()).Combatants.Count + 1, all.SectorZone("s07_06")!.Setup.Combatants.Count);
    }

    [Fact]
    public void Crochets_du_tick_entre_la_session_et_ses_populations()
    {
        ZoneDefinition zone = Vallons(true, WolfNearby);
        SectorWorld world = SectorWorld.For(zone, 0, 1);
        world.EnterAtStart(Player(zone, -18, -12));
        Zone sector = world.SectorZone("s07_06")!;
        var calls = new List<string>();
        int asked = 0;
        for (int i = 0; i < 400 && !calls.Any(c => c.Contains("loup1", StringComparison.Ordinal)); i++)
        {
            if (i % 20 == 1)
                try { sector.Engine.Submit(new CombatCommand(sector.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            var zoneEvents = new List<(string Sector, ZoneEvent Event)>();
            long before = sector.Engine.CurrentTick;
            world.Step(zoneEvents, null, (name, events) =>
            {
                // Appelé après le tick de la session (elle a avancé), avec ses événements, avant ses populations.
                Assert.Equal(before + 1, sector.Engine.CurrentTick);
                calls.Add($"{name}:{string.Join(",", events.Select(e => e.Combatant))}");
            }, name => { asked++; return 0; });
            Assert.Equal(zoneEvents.Select(e => e.Event.Combatant), calls.Last().Split(':')[1].Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
        Assert.Contains("s07_06:loup1", calls);
        Assert.All(calls, c => Assert.StartsWith("s07_06:", c, StringComparison.Ordinal));   // un appel par secteur ouvert et par tick
        Assert.Equal(calls.Count, asked);
        // Places gardées : avec toutes les places gardées, la population ne fait pas revenir son loup ; sans, il revient.
        int Respawns(int reserved)
        {
            SectorWorld w = SectorWorld.For(zone, 0, 1);
            w.EnterAtStart(Player(zone, -18, -12));
            Zone s = w.SectorZone("s07_06")!;
            int n = 0;
            for (int i = 0; i < 2400; i++)
            {
                if (i % 20 == 1)
                    try { s.Engine.Submit(new CombatCommand(s.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                    catch (ArgumentException) { }
                var lives = new List<(string Sector, PopulationEvent Event)>();
                w.Step(null, lives, null, _ => reserved);
                n += lives.Count(l => l.Event.Kind == PopulationEventKind.Respawned);
            }
            return n;
        }
        Assert.True(Respawns(0) > 0);
        Assert.Equal(0, Respawns(10_000));
    }

    [Fact]
    public void Registre_borne_exacte_sans_session_a_naitre()
    {
        // La borne du prochain tick est celle d'une session de zone tant qu'aucune session ne peut naître avant le tick des secteurs.
        ZoneDefinition zone = Vallons(true);
        SectorWorld world = SectorWorld.For(zone, 0, 1);
        world.EnterAtStart(Player(zone, -18, -12));
        Zone sector = world.SectorZone("s07_06")!;
        Assert.Equal(sector.ConsumedCount + sector.NewDeathsPossible, world.NextTickRegistryNeed);
        world.Step();
        Assert.Equal(sector.ConsumedCount + sector.NewDeathsPossible, world.NextTickRegistryNeed);
        // Une entrée annoncée peut faire naître une session avant le tick : toutes les créatures de l'aire et le joueur sont comptés.
        CharacterSetup hero = Player(zone, -20, -12);
        world.Enter(new CharacterSetup("alice", hero.Team, hero.Class, hero.Build, 0, hero.Placement));
        Assert.Equal(sector.ConsumedCount + sector.NewDeathsPossible + world.Map.Area.Combatants.Count + 1, world.NextTickRegistryNeed);
        Assert.True(world.CancelEnter("alice"));
        Assert.Equal(sector.ConsumedCount + sector.NewDeathsPossible, world.NextTickRegistryNeed);
    }

    // --- Règle d'arrivée à un point sûr, la même pour toute session ---------------------------------------------------------

    /// <summary>Le sanglier arrive au premier tick sur (x, z) : il passe avant le personnage.</summary>
    private static Action<JsonObject> BoarOn(double x, double z) => scenario =>
    {
        JsonObject boar = scenario["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
        boar["position"] = new JsonArray(x, z);
        boar["arrivalMs"] = 0;
    };

    [Fact]
    public void Regle_d_arrivee_a_un_point_sur()
    {
        ZoneDefinition calm = Vallons(true), entry = Vallons(true, BoarOn(-18, -12)), bourg = Vallons(true, BoarOn(-58, -6));
        (string?, GroundVector?) none = (null, null);
        // Place libre : le point sûr même. Entrée (null) ou point sûr nommé.
        Assert.Equal(none, WorldSession.SafeArrival(Start(calm, 0, 1), calm, Progression, null));
        Assert.Equal(("place_du_bourg", (GroundVector?)null), WorldSession.SafeArrival(Start(calm, 0, 1), calm, Progression, "place_du_bourg"));
        // Entrée occupée : la place libre la plus proche de l'entrée (anneaux de 0,5 m, l'est d'abord).
        (string? point, GroundVector? place) = WorldSession.SafeArrival(Start(entry, 0, 1), entry, Progression, null);
        Assert.Null(point);
        Assert.NotNull(place);
        Assert.True(place!.Value.X > At(-18, -12).X && place.Value.Z == At(-18, -12).Z);
        Assert.Equal(("place_du_village", place), WorldSession.SafeArrival(Start(entry, 0, 1), entry, Progression, "place_du_village"));   // l'entrée nommée : même place
        Assert.Equal((point, place), WorldSession.SafeArrival(Start(entry, 0, 1), entry, Progression, null));                 // déterministe
        // Point sûr occupé, aucune place autour de lui (anneaux réduits) : l'entrée de la zone, libre.
        Assert.Equal(none, WorldSession.SafeArrival(Start(bourg, 0, 1), bourg, Progression, "place_du_bourg", null, rings: 1));
        // Entrée occupée, aucune place autour d'elle : l'entrée même (dernier recours : l'arrivée y attendra).
        Assert.Equal(none, WorldSession.SafeArrival(Start(entry, 0, 1), entry, Progression, null, null, rings: 1));
        // Point sûr occupé sans place, entrée occupée avec une place : la place près de l'entrée.
        ZoneDefinition both = Vallons(true, s =>
        {
            BoarOn(-58, -6)(s);
            JsonObject wolf = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["position"] = new JsonArray(-18, -12);
            wolf["radius"] = 0.4;
            wolf["arrivalMs"] = 0;
        });
        // Deux anneaux (1 m) : aucune place hors de l'emprise du sanglier (0,9 m) au Bourg, une place hors de celle du loup (0,4 m) à l'entrée.
        (string? fallback, GroundVector? near) = WorldSession.SafeArrival(Start(both, 0, 1), both, Progression, "place_du_bourg", null, rings: 2);
        Assert.Null(fallback);
        Assert.NotNull(near);
        Assert.Equal(At(-17, -12), near);
        Assert.Throws<ArgumentException>(() => WorldSession.SafeArrival(Start(calm, 0, 1), calm, Progression, "nulle_part"));
        Assert.Throws<ArgumentNullException>(() => WorldSession.SafeArrival(null!, calm, Progression, null));
        Assert.Throws<ArgumentNullException>(() => WorldSession.SafeArrival(Start(calm, 0, 1), null!, Progression, null));
        Assert.Throws<ArgumentNullException>(() => WorldSession.SafeArrival(Start(calm, 0, 1), calm, null!, null));
        // La session ouverte à cette place : le personnage y arrive, sur l'hôte comme dans une zone.
        foreach (bool region in new[] { true, false })
        {
            ZoneDefinition zone = Vallons(region, BoarOn(-18, -12));
            (string? at, GroundVector? spot) = WorldSession.SafeArrival(Start(zone, 0, 1), zone, Progression, null);
            Assert.Equal(place, spot);
            var session = new WorldSession(Start(zone, 0, 1), zone, ShippedData, Progression, ItemsCatalog, safePoint: at, place: spot);
            session.Step();
            session.Step();
            Assert.Equal(region, session.OnSectors);
            Assert.True(session.Zone.Engine.View("bastion").IsPresent);
            Assert.Equal(spot, session.Zone.Engine.View("bastion").Position);
        }
    }
}
