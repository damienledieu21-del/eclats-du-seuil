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
/// Sous-lot V2.28b (2c), suite (décision de Damien du 14 novembre 2026, « AAA », 3) : les événements du monde et les échos rôdeurs de leurs
/// cicatrices sont liés au secteur de leur lieu. Zone d'essai : celle des chefs (deux secteurs), avec une chasse à l'est.
/// </summary>
[Trait("Category", "Sector")]
public class V228b2cEvenementsTests
{
    private const string Hunt = "chasse_est";

    /// <summary>Une chasse de deux loups à l'est (chaque jour, sûre), à <paramref name="x"/> ; retouches du scénario.</summary>
    private static ZoneLoadResult Read(double x = 380, Action<JsonObject>? scenario = null, Action<JsonObject>? hunt = null)
    {
        var e = (JsonObject)JsonNode.Parse($$"""
            { "id": "{{Hunt}}", "kind": "hunt", "everyDays": 1, "chancePercent": 100, "firstDay": 0, "start": "06:00", "spreadMinutes": 0,
              "announceMinutes": 0, "durationMinutes": 120,
              "groups": [{ "id": "est", "weight": 1, "creatures": [{ "like": "loup_est", "position": [{{x}}, 90] }, { "like": "loup_est", "position": [{{x + 6}}, 90] }] }] }
            """)!;
        hunt?.Invoke(e);
        return V228b2cChefsTests.Read(zone: z => z["data"]!["events"] = new JsonArray(e), scenario: scenario);
    }

    private static ZoneDefinition Essai(double x = 380, Action<JsonObject>? scenario = null)
    {
        ZoneLoadResult loaded = Read(x, scenario);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    /// <summary>Tick du monde où commence la première occurrence (06 h 00) après le début du temps du monde.</summary>
    private static long StartTick(ZoneDefinition zone)
    {
        var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
        // La première occurrence après le début du temps du monde (et au moins 100 ticks après lui).
        EventOccurrence o = WorldEventSchedule.Next(zone.Id, zone.Id, zone.Events.Single(), checked(WorldEventSchedule.Absolute(clock.At(100)) + 1))!;
        return WorldEventSchedule.TickAt(clock, o.StartAt);
    }

    private static WorldSession Open(ZoneDefinition zone, long worldTick, string safePoint, int level = 1, IEnumerable<string>? consumed = null)
    {
        SavedCharacter hero = Hero();
        if (level > 1) hero = hero.WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level));
        var keys = (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey);
        return new WorldSession(new WorldSave(zone.Id, worldTick, 0, keys, hero), zone, ShippedData, Progression, ItemsCatalog, safePoint: safePoint);
    }

    private static List<string> HuntersIn(Zone zone) =>
        zone.Engine.CombatantIds.Where(id => id.Contains("_ev", StringComparison.Ordinal) && zone.Engine.View(id) is { IsPresent: true, IsDead: false }).ToList();

    private static void Walk(WorldSession w, double x, double z)
    {
        (double, double)? sent = null;
        for (int i = 0; i < 6000; i++)
        {
            CombatEngine e = w.Zone.Engine;
            CombatantView me = e.View(w.CombatantId);
            if (!me.IsPresent || me.Position is not GroundVector p) { w.Step(); sent = null; continue; }
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
            if (d <= 1)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId, CombatAction.Stop));
                w.Step();
                return;
            }
            var dir = (Math.Round(dx / d, 3), Math.Round(dz / d, 3));
            if (sent != dir || i % 40 == 0)
            {
                e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId,
                    new GroundVector(Fixed.Parse(dir.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse(dir.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
                sent = dir;
            }
            w.Step();
        }
        throw new Xunit.Sdk.XunitException($"({x}, {z}) pas atteint");
    }

    // --- Validateur ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Validateur_un_evenement_tient_dans_le_secteur_de_son_lieu()
    {
        Assert.True(Read().IsValid);
        // Une créature dans l'autre secteur : refusée, avec son chemin.
        ZoneLoadResult other = Read(hunt: e => e["groups"]![0]!["creatures"]![1]!["position"] = new JsonArray(200, 90));
        Assert.False(other.IsValid);
        Assert.Contains(other.Report.Errors, e => e.Path == "$.data.events[0].groups[0].creatures[1].position" && e.Message.Contains("hors du secteur", StringComparison.Ordinal));
        // À 4 m du bord intérieur (x = 256) : refusée ; à 8 m : acceptée.
        ZoneLoadResult edge = Read(260 - 6 + 2);   // 256 et 262 : la première à 0 m du bord
        Assert.False(edge.IsValid);
        Assert.Contains(edge.Report.Errors, e => e.Path.StartsWith("$.data.events[0].groups[0].creatures[0]", StringComparison.Ordinal) && e.Message.Contains("moins de 8 m", StringComparison.Ordinal));
        Assert.True(Read(264).IsValid);
        // Les Vallons livrés : tous leurs événements sont dans le cœur.
        ZoneDefinition vallons = Zone();
        Assert.NotEmpty(vallons.Events);
    }

    // --- Événements sur l'hôte -------------------------------------------------------------------------------------------

    [Fact]
    public void Evenement_dans_un_secteur_ferme_il_a_lieu_et_ses_creatures_arrivent_a_l_ouverture()
    {
        ZoneDefinition zone = Essai();
        long start = StartTick(zone);
        WorldSession w = Open(zone, start - 40, "camp_ouest");
        Assert.True(w.OnSectors);
        Assert.Equal("s01_00", w.EventsRuntime!.SectorOf(Hunt));
        var notices = new List<WorldEventNotice>();
        for (int i = 0; i < 80; i++) notices.AddRange(w.Step().Events);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Started && n.Event == Hunt);   // l'occurrence a lieu, secteur fermé
        Assert.Null(w.Host!.SectorZone("s01_00"));
        Assert.Equal(0, w.EventsRuntime.PendingIn("s01_00"));   // sans place réservée tant qu'il est fermé
        Walk(w, 200, 128);   // à moins de 96 m de l'est : il s'ouvre ; les chasseurs y arrivent après une place réservée
        Zone east = w.Host.SectorZone("s01_00")!;
        for (int i = 0; i < 40 && HuntersIn(east).Count < 2; i++) w.Step();
        Assert.Equal(2, HuntersIn(east).Count);
        Assert.Empty(HuntersIn(w.Zone));   // pas dans la session de l'ouest
    }

    [Fact]
    public void Place_reservee_en_attente_comptee_dans_les_places_gardees_du_secteur()
    {
        // Leçon des tours précédents (bornes exactes comptant les états en attente) : une créature d'événement dont la place est réservée
        // mais pas encore prise (sa place est occupée par le personnage) est comptée dans les places gardées de son secteur, que reçoivent
        // les populations de ce secteur sur l'hôte : aucune arrivée de population ne la lui prend.
        ZoneLoadResult loaded = Read(hunt: e => e["groups"]![0]!["creatures"]![0]!["position"] = new JsonArray(420, 200));
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        long start = StartTick(zone);
        WorldSession w = Open(zone, start - 20, "camp_est");   // le personnage se tient sur la place du premier chasseur
        Assert.Equal("s01_00", w.Sector);
        int waiting = 0, before = 0;
        var forms = new HashSet<int>();   // places gardées hors événements (formes du chef de l'est) : inchangées
        for (int i = 0; i < 120; i++)
        {
            w.Step();
            int pending = w.EventsRuntime!.PendingIn("s01_00");
            forms.Add(w.ReservedIn("s01_00") - pending);
            Assert.Equal(0, w.EventsRuntime.PendingIn("s00_00"));
            if (pending > 0) waiting++;
            else if (waiting == 0) before++;
        }
        Assert.True(before > 0 && waiting > 0);
        Assert.Single(forms);
        Assert.Single(HuntersIn(w.Zone));
    }

    [Fact]
    public void Session_renouvelee_les_creatures_d_evenement_continuent_telles_quelles()
    {
        // Décision de Damien du 14 novembre 2026 (« AAA », 2), appliquée par analogie aux créatures d'événement : au renouvellement, elles
        // arrivent dans la session nouvelle à leur place (libre la plus proche), avec leurs PV emportés ; pas de retour à leur place de départ.
        ZoneDefinition zone = Essai(scenario: s =>
        {
            s["data"]!["durationMs"] = 60_000;
            foreach (JsonNode? c in s["data"]!["combatants"]!.AsArray())   // loups (et chasseurs, sur leur modèle) qui frappent à peine
                foreach (JsonNode? attack in c!["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
        });
        long start = StartTick(zone);
        WorldSession w = Open(zone, start - 5, "camp_est", level: 12);
        for (int i = 0; i < 400 && HuntersIn(w.Zone).Count < 2; i++) w.Step();
        Zone first = w.Zone;
        List<string> hunters = HuntersIn(first);
        Assert.Equal(2, hunters.Count);
        // Le personnage blesse le premier chasseur (sa place et ses PV changent) ; à la fin de la session, leur place et leurs PV.
        GroundVector target = first.Engine.View(hunters[0]).Position!.Value;
        Walk(w, target.X.Raw / 1e4 - 1.5, target.Z.Raw / 1e4);
        for (int i = 0; i < 200 && first.Engine.View(hunters[0]) is { } h && h.Health == h.MaxHealth && !w.Zone.IsOver; i++)
        {
            if (i % 20 == 0)
                try { first.Engine.Submit(new CombatCommand(first.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", hunters[0])); }
                catch (ArgumentException) { }
            w.Step();
        }
        Assert.Same(first, w.Zone);
        CombatantView hurt = first.Engine.View(hunters[0]);
        Assert.True(hurt.Health < hurt.MaxHealth && !hurt.IsDead, "le chasseur n'a pas été blessé");
        while (!w.Zone.IsOver) w.Step();
        var before = hunters.Select(id => first.Engine.View(id)).Select(v => (v.Position!.Value, v.Health)).ToList();
        w.Step();
        Assert.True(w.Renewals == 1, $"vaincu : {w.IsDefeated}, renouvelle : {w.RenewsSessions}");
        for (int i = 0; i < 3; i++) w.Step();
        List<string> carried = HuntersIn(w.Zone);
        Assert.Equal(2, carried.Count);
        Assert.DoesNotContain(carried, hunters.Contains);   // identités nouvelles
        foreach ((GroundVector at, long? health) in before)
            Assert.Contains(carried, id => w.Zone.Engine.View(id) is { } v && v.Health == health
                && Math.Abs(v.Position!.Value.X.Raw - at.X.Raw) + Math.Abs(v.Position.Value.Z.Raw - at.Z.Raw) < 100_000);
    }

    /// <summary>Loups (et chasseurs, sur leur modèle) à 1 PV qui frappent à peine ; session de <paramref name="durationMs"/>.</summary>
    private static ZoneDefinition Fragile(long durationMs, Action<JsonObject>? hunt = null)
    {
        ZoneLoadResult loaded = Read(hunt: hunt, scenario: s =>
        {
            s["data"]!["durationMs"] = durationMs;
            foreach (JsonNode? c in s["data"]!["combatants"]!.AsArray())
            {
                if (((string)c!["id"]!).StartsWith("loup", StringComparison.Ordinal)) c["health"] = 1;
                foreach (JsonNode? attack in c["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
            }
        });
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    /// <summary>
    /// Partie à l'est, chasseurs arrivés à côté du camp ; le personnage frappe le premier chasseur au tick de session
    /// <paramref name="strikeAt"/> (null : jamais) ; rend la partie à la fin de sa session (pas encore renouvelée), le chasseur et le tick
    /// (de session) où il est mort, s'il l'est, et s'il est mort au tick même où la session finit.
    /// </summary>
    private static (WorldSession W, string Hunter, long? DiedAt, bool OnLast) StrikeNearEnd(ZoneDefinition zone, long? strikeAt)
    {
        WorldSession w = Open(zone, StartTick(zone) - 5, "camp_est", level: 12);
        for (int i = 0; i < 100 && HuntersIn(w.Zone).Count < 2; i++) w.Step();
        CombatEngine e = w.Zone.Engine;
        string hunter = HuntersIn(w.Zone)[0];
        long? died = null;
        bool onLast = false;
        while (!w.Zone.IsOver)
        {
            if (strikeAt == e.CurrentTick)
                try { e.Submit(new CombatCommand(e.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", hunter)); }
                catch (ArgumentException) { }
            bool alive = !e.View(hunter).IsDead;
            w.Step();
            if (alive && e.View(hunter).IsDead)
            {
                died = e.CurrentTick;
                onLast = w.Zone.IsOver;   // le moteur simule son dernier tick et finit dans le même pas
            }
        }
        return (w, hunter, died, onLast);
    }

    [Fact]
    public void Creature_morte_au_dernier_tick_de_la_session_relevee_avant_le_renouvellement()
    {
        // Relecture indépendante de la PR #160 (relever, propager, remplacer) : un chasseur tué au tout dernier tick de la session n'est
        // pas relu par le pas des événements (session finie) ; le renouvellement le relève mort avant tout remplacement : il ne revient pas
        // vivant dans la session nouvelle, et sa mort compte pour l'issue.
        ZoneDefinition zone = Fragile(20_000, hunt: e =>
        {
            e["groups"]![0]!["creatures"]![0]!["position"] = new JsonArray(421.5, 200);   // au contact du personnage, au camp de l'est
            e["groups"]![0]!["creatures"]![1]!["position"] = new JsonArray(426, 204);
        });
        // Un premier essai donne le délai entre la frappe et la mort, et le dernier tick ; puis la frappe est posée pour finir au dernier tick.
        var probe = StrikeNearEnd(zone, 200);
        Assert.NotNull(probe.DiedAt);
        long delay = probe.DiedAt!.Value - 200, last = probe.W.Zone.Engine.CurrentTick;
        (WorldSession W, string Hunter, long? DiedAt, bool OnLast)? hit = null;
        for (long k = delay - 2; k <= delay + 6 && hit is null; k++)
        {
            var run = StrikeNearEnd(zone, last - k);
            if (run.OnLast) hit = run;
        }
        Assert.True(hit != null, "aucune frappe ne tue au dernier tick");
        WorldSession w = hit!.Value.W;
        Assert.True(w.Zone.Engine.View(hit.Value.Hunter).IsDead);
        Assert.True(w.IsEventCreature(hit.Value.Hunter));
        w.Step();
        Assert.Equal(1, w.Renewals);
        Assert.False(w.IsEventCreature(hit.Value.Hunter));   // identités de la session finie purgées
        Walk(w, 405, 200);                       // sa place libérée (le personnage s'en éloigne)
        for (int i = 0; i < 60; i++) w.Step();   // plus d'une seconde : une créature remise en attente serait revenue
        Assert.Single(HuntersIn(w.Zone));        // seul le second chasseur, emporté
        Assert.Equal(1, w.EventsRuntime!.PendingPlaces + HuntersIn(w.Zone).Count);
    }

    [Fact]
    public void Evenement_issu_d_un_secteur_ferme_ouvert_pendant_qu_il_dure_une_seule_issue()
    {
        // Relecture indépendante de la PR #160 : une attaque (issue à la dernière créature vaincue) commence secteur fermé ; le personnage
        // ouvre le secteur et tue les deux assaillants : une seule issue (une victoire), aucune seconde.
        ZoneDefinition zone = Fragile(3_600_000, hunt: e =>
        {
            e["kind"] = "raid";
            e["reward"] = new JsonObject { ["xp"] = 10, ["money"] = 5 };
        });
        long start = StartTick(zone);
        WorldSession w = Open(zone, start - 40, "camp_ouest", level: 12);
        for (int i = 0; i < 80; i++) w.Step();
        Assert.Null(w.Host!.SectorZone("s01_00"));
        Walk(w, 370, 96);
        var notices = new List<WorldEventNotice>();
        for (int i = 0; i < 2400 && !w.Character.Events.Any(x => x.Won > 0); i++)
        {
            CombatEngine e = w.Zone.Engine;
            if (i % 10 == 0 && HuntersIn(w.Zone).FirstOrDefault() is string h)
            {
                GroundVector at = e.View(h).Position!.Value;
                CombatantView me = e.View(w.CombatantId);
                double dx = at.X.Raw / 1e4 - me.Position!.Value.X.Raw / 1e4, dz = at.Z.Raw / 1e4 - me.Position.Value.Z.Raw / 1e4;
                if (dx * dx + dz * dz > 9) { Walk(w, at.X.Raw / 1e4 - 1.5, at.Z.Raw / 1e4); continue; }
                try { e.Submit(new CombatCommand(e.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", h)); }
                catch (ArgumentException) { }
            }
            notices.AddRange(w.Step().Events);
        }
        for (int i = 0; i < 200; i++) notices.AddRange(w.Step().Events);
        SavedEvent saved = Assert.Single(w.Character.Events, x => x.Id.EndsWith(Hunt, StringComparison.Ordinal));
        Assert.Equal((1L, 0L), (saved.Won, saved.Lost));
        Assert.DoesNotContain(notices, n => n.Kind == WorldEventNoticeKind.Lost && n.Event == Hunt);
    }

    [Fact]
    public void Secteur_ferme_puis_rouvert_les_creatures_d_evenement_reviennent_a_leur_place()
    {
        ZoneDefinition zone = Essai();
        long start = StartTick(zone);
        WorldSession w = Open(zone, start - 5, "camp_est");
        for (int i = 0; i < 400 && HuntersIn(w.Zone).Count < 2; i++) w.Step();
        Zone east = w.Zone;
        Walk(w, 64, 128);   // à l'ouest, loin : l'est se ferme après 60 s
        for (int i = 0; i < 1300 && w.Host!.SectorZone("s01_00") != null; i++) w.Step();
        Assert.Null(w.Host!.SectorZone("s01_00"));
        for (int i = 0; i < 20; i++) w.Step();   // sans lever, secteur fermé
        Walk(w, 200, 128);
        Zone again = w.Host.SectorZone("s01_00")!;
        Assert.NotSame(east, again);
        for (int i = 0; i < 60 && HuntersIn(again).Count < 2; i++) w.Step();
        List<string> back = HuntersIn(again);
        Assert.Equal(2, back.Count);
        Assert.All(back, id => Assert.Equal(again.Engine.View(id).MaxHealth, again.Engine.View(id).Health));
    }

    [Fact]
    public void Plafond_du_registre_creatures_d_evenement_emportees_comptees_dans_la_borne()
    {
        // Les créatures d'événement emportées dans la session renouvelée comptent dans la borne du tick (tout ou rien) : juste à la borne,
        // accepté entier ; une clé de plus, pas de renouvellement, rien ne lève. Sans elles, le tick était interrompu après le remplacement.
        foreach (int spare in new[] { 0, -1 })
        {
            ZoneDefinition zone = Essai(scenario: s => s["data"]!["durationMs"] = 20_000);
            long start = StartTick(zone);
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}");
            WorldSession w = Open(zone, start - 5, "camp_est", consumed: keys);
            for (int i = 0; i < 400 && HuntersIn(w.Zone).Count < 2; i++) w.Step();
            Assert.Equal(2, HuntersIn(w.Zone).Count);
            while (!w.Zone.IsOver) w.Step();
            SectorWorld host = w.Host!;
            long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
            for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            if (spare == 0)
            {
                w.Step();
                Assert.Equal(1, w.Renewals);
                Assert.Equal(2, w.Zone.Engine.CombatantIds.Count(id => id.Contains("_ev", StringComparison.Ordinal)));
                continue;
            }
            Assert.False(w.RenewsSessions);
            w.Step();
            Assert.Equal(0, w.Renewals);
        }
    }

    [Fact]
    public void Cicatrice_dans_un_secteur_ferme_ses_echos_roent_dans_la_session_de_son_secteur()
    {
        // Décision 3 de Damien, aux cicatrices : les échos rôdeurs d'une cicatrice jouent dans la session du secteur de son lieu ; secteur
        // fermé, ils attendent sans place réservée ; ouvert, ils s'y lèvent (jamais dans la session de l'ouest).
        ZoneLoadResult loaded = V228b2cChefsTests.Read(zone: z =>
        {
            var rift = (JsonObject)JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!["data"]!["events"]!.AsArray()
                .Single(x => (string)x!["id"]! == "faille_du_seuil")!.DeepClone();
            var g = (JsonObject)rift["groups"]![0]!.DeepClone();
            g.Remove("spreadTo");
            g["position"] = new JsonArray(380, 90);
            g["creatures"] = new JsonArray(Creature(380, 94), Creature(384, 90), Creature(376, 90));
            g["haunts"] = new JsonArray(Creature(390, 100), Creature(370, 100), Creature(380, 104));
            rift["groups"] = new JsonArray(g);
            z["data"]!["events"] = new JsonArray(rift);
        });
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        SavedCharacter hero = Hero().WithScars(new[] { new SavedScar("zone.essai/faille_du_seuil/ruines", 2, 0, 0) });
        var w = new WorldSession(new WorldSave(zone.Id, 2000, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog,
            safePoint: "camp_ouest");
        Assert.True(w.OnSectors);
        Assert.Equal("s01_00", w.EventsRuntime!.SectorOf("faille_du_seuil"));
        for (int i = 0; i < 2400; i++) w.Step();   // deux minutes du monde : le secteur de la cicatrice est fermé
        Assert.Null(w.Host!.SectorZone("s01_00"));
        Assert.Empty(HuntersIn(w.Zone));
        Assert.Equal(0, w.EventsRuntime.PendingIn("s01_00"));
        Walk(w, 200, 128);
        Zone east = w.Host.SectorZone("s01_00")!;
        for (int i = 0; i < 2400 && HuntersIn(east).Count == 0; i++) w.Step();
        Assert.NotEmpty(HuntersIn(east));   // les échos voulus au palier 2 (un seul)
        Assert.Empty(HuntersIn(w.Zone));
        // Relecture indépendante de la PR #160 : sauvegardée puis rechargée, la cicatrice reste liée à son secteur : ses échos se lèvent dans
        // la session de l'est (secteur du personnage repris au camp de l'est), jamais à l'ouest.
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        SavedScar scar = Assert.Single(save.Character!.Scars);
        Assert.Equal(("zone.essai/faille_du_seuil/ruines", 2), (scar.Id, scar.Stage));
        var again = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, safePoint: "camp_est");
        Assert.Equal("s01_00", again.Sector);
        for (int i = 0; i < 2400 && HuntersIn(again.Zone).Count == 0; i++) again.Step();
        Assert.NotEmpty(HuntersIn(again.Zone));
        Assert.Equal("s01_00", again.Sector);

        static JsonObject Creature(double x, double z) => new() { ["like"] = "loup_est", ["position"] = new JsonArray(x, z) };
    }

    // --- Vallons livrés ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("attaque_hameau", "hameau_de_la_carriere")]
    [InlineData("faille_du_seuil", "essai_croc")]
    public void Parite_Vallons_avec_evenements_une_session_contre_hote_a_plusieurs_secteurs(string id, string safePoint)
    {
        ZoneDefinition shipped = Zone();
        var clock = new WorldClock(shipped.Setup.Combat.TickMilliseconds);
        EventOccurrence o = WorldEventSchedule.Next(shipped.Id, shipped.Id, shipped.Events.Single(e => e.Id == id), checked(WorldEventSchedule.Absolute(clock.At(100)) + 1))!;
        long worldTick = checked(WorldEventSchedule.TickAt(clock, o.StartAt) - 40);   // juste avant l'occurrence
        // Les Vallons livrés, événements du monde compris (chacun lié au secteur de son lieu, tous dans le cœur) : le chemin d'avant (session
        // de zone, sans région) et l'hôte au cœur seul jouent octet pour octet ; l'hôte à deux secteurs joue de même tant que le personnage
        // reste dans le cœur (trace, personnage sauvegardé ; 1 200 ticks : plus loin, un sanglier du bord est du cœur passe x = 140, la
        // limite de l'aire d'une seule session, ce qui n'est pas une différence des événements).
        (string Trace, string Log, int Open) plain = V228b2cChefsTests.TraceOf(V228b2cChefsTests.VallonsOf(0, events: true), 1200, safePoint, worldTick),
            home = V228b2cChefsTests.TraceOf(V228b2cChefsTests.VallonsOf(1, events: true), 1200, safePoint, worldTick),
            wide = V228b2cChefsTests.TraceOf(V228b2cChefsTests.VallonsOf(2, events: true), 1200, safePoint, worldTick);
        string[] world = plain.Trace.Split('\n').Where(l => l.Contains(" monde ", StringComparison.Ordinal) && !l.EndsWith(" monde ", StringComparison.Ordinal)).ToArray();
        Assert.True(world.Length > 0, string.Join("|", plain.Trace.Split('\n').Select(l => l[(l.IndexOf(" monde", StringComparison.Ordinal) + 1)..]).Distinct().Take(20)));
        Assert.Equal(plain.Log, home.Log);
        Assert.Equal(plain.Trace, home.Trace);
        static string Played(string t) => t[..t.IndexOf("fin ", StringComparison.Ordinal)];
        static string Hero(string t) => JsonNode.Parse(t[t.IndexOf('{')..])!["data"]!["character"]!.ToJsonString();
        Assert.Equal(Played(home.Trace), Played(wide.Trace));
        Assert.Equal(Hero(home.Trace), Hero(wide.Trace));
    }

    [Fact]
    public void Parite_Vallons_avec_evenements_au_dela_des_renouvellements()
    {
        // Relecture indépendante de la PR #160 : sessions de 20 s (au moins un renouvellement en 1 200 ticks), juste avant la faille du Seuil :
        // l'hôte au cœur seul et l'hôte à deux secteurs jouent de même à travers les renouvellements (trace, personnage sauvegardé).
        ZoneDefinition shipped = Zone();
        var clock = new WorldClock(shipped.Setup.Combat.TickMilliseconds);
        EventOccurrence o = WorldEventSchedule.Next(shipped.Id, shipped.Id, shipped.Events.Single(e => e.Id == "faille_du_seuil"), checked(WorldEventSchedule.Absolute(clock.At(100)) + 1))!;
        long worldTick = checked(WorldEventSchedule.TickAt(clock, o.StartAt) - 40);
        (string Trace, string Log, int Open) home = V228b2cChefsTests.TraceOf(V228b2cChefsTests.VallonsOf(1, events: true, durationMs: 20_000), 1200, "essai_croc", worldTick),
            wide = V228b2cChefsTests.TraceOf(V228b2cChefsTests.VallonsOf(2, events: true, durationMs: 20_000), 1200, "essai_croc", worldTick);
        string renewals = home.Trace[(home.Trace.IndexOf("renouvellements ", StringComparison.Ordinal) + 16)..].Split('\n')[0];
        Assert.True(int.Parse(renewals, System.Globalization.CultureInfo.InvariantCulture) >= 1, renewals);
        Assert.Contains("Started:faille_du_seuil", home.Trace, StringComparison.Ordinal);
        static string Played(string t) => t[..t.IndexOf("fin ", StringComparison.Ordinal)];
        static string Hero(string t) => JsonNode.Parse(t[t.IndexOf('{')..])!["data"]!["character"]!.ToJsonString();
        Assert.Equal(Played(home.Trace), Played(wide.Trace));
        Assert.Equal(Hero(home.Trace), Hero(wide.Trace));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Plafond_du_registre_Vallons_livres_chefs_evenements_et_populations_tout_ou_rien(int spare)
    {
        // Relecture indépendante de la PR #160 : les Vallons livrés (chefs, événements du monde, populations), session courte finie pendant
        // la faille du Seuil, registre à la borne exacte du tick : accepté entier (renouvelé) ; une clé de plus : pas de renouvellement,
        // rien ne lève, aucun tick partiel.
        ZoneDefinition zone = Zone(scenario: s => s["data"]!["durationMs"] = 10_000);
        var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
        EventOccurrence o = WorldEventSchedule.Next(zone.Id, zone.Id, zone.Events.Single(e => e.Id == "faille_du_seuil"), checked(WorldEventSchedule.Absolute(clock.At(100)) + 1))!;
        long at = checked(WorldEventSchedule.TickAt(clock, o.StartAt) - 60);
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 25);
        var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 2000).Select(i => $"zone.vallons/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
        var w = new WorldSession(new WorldSave(zone.Id, at, 0, keys, Hero().WithXp(xp)), zone, ShippedData, Progression, ItemsCatalog);
        Assert.True(w.OnSectors);
        while (!w.Zone.IsOver) w.Step();
        Assert.Contains(w.Zone.Engine.CombatantIds, id => w.IsEventCreature(id));
        SectorWorld host = w.Host!;
        long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
        Assert.True(room > 0);
        for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.vallons/test/plein{i:D5}"));
        int consumed = w.Zone.ConsumedCount;
        long tick = w.Zone.WorldTick;
        w.Step();
        if (spare == 0)
        {
            Assert.Equal(1, w.Renewals);
            Assert.Equal(tick + 1, w.Zone.WorldTick);
            Assert.True(w.Zone.ConsumedCount <= SaveValidator.MaxConsumed);
            for (int i = 0; i < 40; i++) w.Step();
            Assert.True(w.Zone.ConsumedCount <= SaveValidator.MaxConsumed);
            return;
        }
        Assert.False(w.RenewsSessions);
        Assert.Equal(0, w.Renewals);
        Assert.Equal((consumed, tick), (w.Zone.ConsumedCount, w.Zone.WorldTick));   // rien n'a bougé
    }

    /// <summary>Ticks du monde des vagues de la faille (et de son départ), sur <paramref name="ticks"/> ticks à partir de 22 h 50 du premier jour de faille.</summary>
    private static List<string> RiftTimeline(long sessionMs, int ticks)
    {
        ZoneDefinition zone = Zone(scenario: s => s["data"]!["durationMs"] = sessionMs);
        WorldEventDefinition rift = zone.Events.Single(e => e.Id == "faille_du_seuil");
        var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
        EventOccurrence o = WorldEventSchedule.Next(zone.Id, zone.Id, rift, checked(WorldEventSchedule.Absolute(clock.At(100)) + 1))!;
        long at = checked(WorldEventSchedule.TickAt(clock, o.StartAt) - 60);
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 25);
        var w = new WorldSession(new WorldSave(zone.Id, at, 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithXp(xp)), zone, ShippedData, Progression, ItemsCatalog);
        var timeline = new List<string>();
        for (int i = 0; i < ticks; i++)
            foreach (WorldEventNotice n in w.Step().Events)
                if (n.Event == "faille_du_seuil") timeline.Add($"{w.Zone.WorldTick} {n.Kind} {n.Wave}");
        return timeline;
    }

    [Fact]
    public void Vallons_vagues_de_la_faille_au_temps_du_monde_a_travers_les_renouvellements()
    {
        // Leçon des tours précédents (ticks du monde) : les échéances des vagues sont en ticks du monde ; des sessions renouvelées toutes
        // les 30 s n'en changent pas les instants (même déroulé qu'avec une seule session d'une heure).
        List<string> one = RiftTimeline(3_600_000, 2400), renewed = RiftTimeline(30_000, 2400);
        Assert.Contains(one, l => l.Contains(" Wave ", StringComparison.Ordinal));
        Assert.Equal(one, renewed);
    }
}
