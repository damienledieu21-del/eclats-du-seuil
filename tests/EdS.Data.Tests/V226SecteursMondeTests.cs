using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Validation;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.26 (secteurs de simulation, PLAN_GRANDE_CARTE.md § 2.3 et 2.4) : découpage d'une aire en secteurs (<see cref="SectorMap"/>)
/// et hôte des secteurs (<see cref="SectorWorld"/>) : activation et fermeture, passage d'un secteur à l'autre avec l'état de combat,
/// hystérésis, rencontre retenue jusqu'au bout de la marge, déterminisme sur plusieurs secteurs ouverts, joueurs dans n'importe quel ordre.
/// Région d'essai : 4 × 2 cases à l'origine, soit deux secteurs (s00_00 de 0 à 256 m, s01_00 de 256 à 512 m).
/// </summary>
[Trait("Category", "Sector")]
public class V226SecteursMondeTests
{
    private static readonly CombatSetup Shipped = Load();

    private static CombatSetup Load()
    {
        ScenarioLoadResult r = ScenarioJsonReader.ReadFile(ScenarioPath("espace_chemins"), ShippedData);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Setup!;
    }

    private static CharacterSetup Hero(string id, double x, double z)
    {
        var b = (CharacterSetup)Shipped.Combatants.Single(c => c.Id == "bastion");
        return new CharacterSetup(id, "joueurs", b.Class, b.Build, 0, new CombatantPlacement(P(x, z), b.Placement!.Radius));
    }

    private static GroundVector P(double x, double z) =>
        new(Fixed.Parse(x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse(z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));

    private static AssailantSetup Wolf(string id, double x, double z)
    {
        var w = (AssailantSetup)Shipped.Combatants.Single(c => c.Id == "loup_maison");
        return new AssailantSetup(id, "meute", w.Armor, w.MagicResistance, null, w.FirstAttackMs, w.IntervalMs, w.Attacks, 0, w.StatusResistance, w.Triggers,
            400, new CombatantPlacement(P(x, z), w.Placement!.Radius, null, w.Placement.Speed), null, w.Behavior);
    }

    private static double D(Fixed f) => double.Parse(f.ToString(), System.Globalization.CultureInfo.InvariantCulture);

    private static readonly RegionGrid Grid = new(0, 0, 4, 2);

    private static SectorMap Map(params CombatantSetup[] creatures)
    {
        var rock = new SpaceObstacle("rocher", new[] { P(255, 200), P(258, 200), P(258, 203), P(255, 203) });
        var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, Shipped.Combat, creatures, Array.Empty<CombatCommand>(),
            Shipped.Data, new CombatSpace(P(0, 0), P(512, 256), new[] { rock }), zone: true);
        return new SectorMap(Grid, area, P(64, 128));
    }

    private static SectorWorld World(SectorMap map, Dictionary<string, MemoryCombatLog>? logs = null) =>
        new(map, "zone.essai", 0, 1, logs: logs is null ? null : (sector, session) => logs[$"{sector}/{session}"] = new MemoryCombatLog());

    private static void Walk(SectorWorld w, string player, double x, double z)
    {
        CombatEngine e = w.EngineOf(player);
        e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantOf(player), P(x, z)));
    }

    private static List<SectorEvent> Run(SectorWorld w, int ticks, Func<SectorEvent, bool>? until = null)
    {
        var all = new List<SectorEvent>();
        for (int i = 0; i < ticks; i++)
        {
            IReadOnlyList<SectorEvent> e = w.Step();
            all.AddRange(e);
            if (until != null && e.Any(until)) break;
        }
        return all;
    }

    // --- Découpage ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Secteurs_espaces_avec_marge_creatures_et_graines()
    {
        SectorMap map = Map(Wolf("loup_ouest", 100, 60), Wolf("loup_est", 400, 128));
        Assert.Equal("s00_00", map.HomeSector);
        Assert.Equal((0L, 0L, 2_880_000L, 2_560_000L), map.Space("s00_00")!.Value);   // 256 m + 32 m de marge à l'est, coupé aux bornes
        Assert.Equal((2_240_000L, 0L, 5_120_000L, 2_560_000L), map.Space("s01_00")!.Value);
        Assert.Null(map.Space("s00_01"));   // hors de l'aire : jamais ouvert
        Assert.False(map.HomeHoldsArea);
        CombatSetup west = map.SetupFor("s00_00", 1), east = map.SetupFor("s01_00", 3);
        Assert.Empty(west.Validate());
        Assert.Empty(east.Validate());
        Assert.Equal(new[] { "loup_ouest" }, west.Combatants.Select(c => c.Id));
        Assert.Equal(new[] { "loup_est" }, east.Combatants.Select(c => c.Id));
        Assert.Single(west.Space!.Obstacles);   // le rocher du bord est dans la marge des deux
        Assert.Single(east.Space!.Obstacles);
        Assert.Equal(7L, west.Seed);
        Assert.Equal(SectorMap.SeedOf(7, "s01_00", 3), east.Seed);
        Assert.NotEqual(east.Seed, map.SetupFor("s01_00", 4).Seed);
        Assert.Equal(new[] { "s00_00", "s01_00" }, map.Near(P(200, 128)));   // 56 m du voisin
        Assert.Equal(new[] { "s00_00" }, map.Near(P(100, 128)));
        Assert.Equal(("s00_00", "s01_00", "s01_00"), (map.SectorOf(P(255.9999, 0)), map.SectorOf(P(256, 0)), map.SectorOf(P(9000, -50))));
    }

    [Fact]
    public void Coeur_des_Vallons_exactement_la_zone_d_avant()
    {
        // Les Vallons tiennent tout entiers dans le secteur du cœur (s07_06) : sa simulation est exactement celle de la zone.
        ZoneDefinition vallons = V11TestData.Zone();
        SectorMap map = SectorMap.For(vallons);
        Assert.Equal("s07_06", map.HomeSector);
        Assert.True(map.HomeHoldsArea);
        Assert.Same(vallons.Setup, map.SetupFor("s07_06", 5));
        var log = new MemoryCombatLog();
        var reference = new MemoryCombatLog();
        var world = SectorWorld.For(vallons, 0, 1, logs: (sector, session) => sector == "s07_06" ? log : NullCombatLog.Instance);
        var zone = new Zone(vallons.Id, ZoneSetups.For(vallons, Array.Empty<string>()), 1, 0, log: reference);
        var populations = new ZonePopulations(vallons);
        world.Enter(Hero("bastion", -18, -12));
        zone.Enter(Hero("bastion", -18, -12));
        for (int i = 0; i < 200; i++)
        {
            world.Step();
            zone.Step();
            populations.Step(zone);
        }
        Assert.Equal(new[] { "s07_06" }, world.OpenSectors);
        Assert.Equal(reference.Lines, log.Lines);
        Assert.Equal("zone.vallons/1/death/loup1", world.SectorZone("s07_06")!.EventKey(ZoneEventKind.Death, "loup1"));
    }

    // --- Passage ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Passage_hors_combat_avec_l_etat_au_tick_suivant()
    {
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 240, 128));
        w.Step();
        // Un peu entamée : PV et endurance emportés.
        CombatEngine west = w.EngineOf("alice");
        Assert.Equal(new[] { "s00_00", "s01_00" }, w.OpenSectors);   // 16 m du voisin : ouvert d'emblée
        Walk(w, "alice", 1, 0);
        List<SectorEvent> events = Run(w, 200, e => e.Kind == SectorEventKind.Arrived);
        SectorEvent departed = events.Single(e => e.Kind == SectorEventKind.Departed);
        SectorEvent arrived = events.Single(e => e.Kind == SectorEventKind.Arrived);
        Assert.Equal(("s00_00", "s01_00"), (departed.Sector, departed.Other));
        Assert.Equal(departed.WorldTick + 1, arrived.WorldTick);
        CombatantView before = west.View("alice");
        Assert.True(before.HasLeft);
        Assert.True(before.Position!.Value.X > Fixed.FromInt(264));   // au-delà de 8 m d'hystérésis
        Assert.Equal("s01_00", w.SectorOf("alice"));
        Assert.Equal("alice", w.CombatantOf("alice"));
        w.Step();
        CombatantView after = w.ViewOf("alice");
        Assert.True(after.IsPresent);
        Assert.Equal((before.Health, before.Endurance, before.Resonance), (after.Health, after.Endurance, after.Resonance));
        Assert.Equal(before.Facing, after.Facing);
        Assert.NotNull(after.MoveDirection);   // la marche continue de l'autre côté
        Assert.True(after.Position!.Value.X > before.Position!.Value.X);
        // Les deux sessions sont au même tick du monde.
        Assert.All(w.OpenSectors, s => Assert.Equal(w.WorldTick, w.SectorZone(s)!.WorldTick));
    }

    [Fact]
    public void Hysteresis_pas_d_aller_retour_au_bord()
    {
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Walk(w, "alice", 1, 0);
        Run(w, 200, e => e.Kind == SectorEventKind.Arrived);
        w.Step();
        Walk(w, "alice", -1, 0);
        // Retour vers l'ouest : pas de passage tant qu'elle n'est pas à 8 m au-delà du bord (x < 248).
        var events = new List<SectorEvent>();
        while (!events.Any(e => e.Kind == SectorEventKind.Departed))
        {
            events.AddRange(w.Step());
            CombatantView v = w.ViewOf("alice");
            if (!events.Any(e => e.Kind == SectorEventKind.Departed)) Assert.True(v.Position!.Value.X >= Fixed.FromInt(248) - Fixed.One);
        }
        Assert.True(w.ViewOf("alice").Position!.Value.X < Fixed.FromInt(248));
        Run(w, 2);
        Assert.Equal("s00_00", w.SectorOf("alice"));
        Assert.Equal("alice_p1", w.CombatantOf("alice"));   // son identité d'avant a servi dans cette session : une nouvelle
    }

    [Fact]
    public void Rencontre_retenue_jusqu_au_bout_de_la_marge_puis_les_creatures_lachent()
    {
        // Un loup du secteur ouest s'engage et poursuit ; le joueur file vers l'est : il ne passe pas tant qu'il est poursuivi, jusqu'au bout
        // de la marge (288 m), où le loup lâche ; il passe ensuite.
        SectorWorld w = World(Map(Wolf("loup", 236, 128)));
        w.Enter(Hero("alice", 246, 128));
        w.Step();
        Walk(w, "alice", 1, 0);
        var events = new List<SectorEvent>();
        for (int i = 0; i < 1200 && !events.Any(e => e.Kind == SectorEventKind.Arrived); i++)
        {
            events.AddRange(w.Step());
            if (!events.Any(e => e.Kind == SectorEventKind.Released))
                Assert.DoesNotContain(events, e => e.Kind == SectorEventKind.Departed);
        }
        SectorEvent released = events.First(e => e.Kind == SectorEventKind.Released);
        Assert.Equal(("s00_00", "loup", "alice"), (released.Sector, released.Subject, released.Other));
        Assert.Contains(events, e => e.Kind == SectorEventKind.Arrived && e.WorldTick > released.WorldTick);
        Zone west = w.SectorZone("s00_00")!;
        Assert.True(west.Engine.View("loup").HasLeft && !west.Engine.View("loup").IsDead);   // ni mort ni butin
    }

    // --- Activation et fermeture -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Ouverture_a_96_m_fermeture_apres_60_s_et_reouverture_en_session_nouvelle()
    {
        SectorWorld w = World(Map(Wolf("loup_est", 400, 128)));
        w.Enter(Hero("alice", 100, 128));
        w.Step();
        Assert.Equal(new[] { "s00_00" }, w.OpenSectors);
        Walk(w, "alice", 1, 0);
        List<SectorEvent> opened = Run(w, 400, e => e.Kind == SectorEventKind.Opened);
        SectorEvent open = opened.Single(e => e.Kind == SectorEventKind.Opened);
        Assert.Equal("s01_00", open.Sector);
        Assert.InRange(w.ViewOf("alice").Position!.Value.X.Raw, 1_590_000, 1_610_000);   // à 96 m du bord (160 m)
        Assert.Equal(w.WorldTick, w.SectorZone("s01_00")!.WorldTick);
        Assert.Equal("zone.essai/s01_00/1/death/loup_est", w.SectorZone("s01_00")!.EventKey(ZoneEventKind.Death, "loup_est"));
        Walk(w, "alice", -1, 0);
        List<SectorEvent> closed = Run(w, 2000, e => e.Kind == SectorEventKind.Closed);
        SectorEvent close = closed.Single(e => e.Kind == SectorEventKind.Closed);
        Assert.Equal("s01_00", close.Sector);
        Assert.Equal(new[] { "s00_00" }, w.OpenSectors);
        Assert.Equal(1L, w.SectorSessions["s01_00"]);
        Walk(w, "alice", 1, 0);
        Run(w, 2000, e => e.Kind == SectorEventKind.Opened);
        Assert.Equal(2L, w.SectorSessions["s01_00"]);
        Assert.Equal("zone.essai/s01_00/2/death/loup_est", w.SectorZone("s01_00")!.EventKey(ZoneEventKind.Death, "loup_est"));
        Assert.Equal(SectorMap.SeedOf(7, "s01_00", 2), w.SectorZone("s01_00")!.Setup.Seed);
    }


    // --- Territoire (zones en schéma 14) ----------------------------------------------------------------------------------------------

    private static ZoneLoadResult Vallons14(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["schemaVersion"] = 14;
        mutate?.Invoke(root);
        return ZoneJsonReader.Read(root.ToJsonString(), V11TestData.ScenariosDirectory, ShippedData, V11TestData.ItemsCatalog);
    }

    private static JsonObject Population(JsonObject root, string creature) =>
        root["data"]!["populations"]!.AsArray().Select(p => p!.AsObject()).Single(p => (string)p["creature"]! == creature);

    [Fact]
    public void Territoire_valide_seulement_en_schema_14_et_dans_la_marge()
    {
        Assert.Equal((14, 13), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.RegionSchemaVersion));
        ZoneLoadResult plain = Vallons14();
        Assert.True(plain.IsValid, plain.Report.ToString());   // les Vallons tiennent dans leur secteur, chaque créature à 8 m au moins du bord
        ZoneLoadResult ok = Vallons14(r => Population(r, "loup1")["territory"] = 30);
        Assert.True(ok.IsValid, ok.Report.ToString());
        Assert.Equal(Fixed.FromInt(30), ok.Zone!.PopulationOf("loup1")!.Territory);
        Assert.Null(ok.Zone.PopulationOf("loup2")!.Territory);
        // Schéma 13 : refusé ; hors de ]0, 256] : refusé ; plus que la distance au bord du secteur plus 32 m : refusé.
        JsonObject Mutate13(JsonObject r) { r["schemaVersion"] = 13; Population(r, "loup1")["territory"] = 30; return r; }
        Assert.Contains(Vallons14(r => Mutate13(r)).Report.Errors, e => e.Path.EndsWith(".territory", StringComparison.Ordinal));
        Assert.Contains(Vallons14(r => Population(r, "loup1")["territory"] = 0).Report.Errors, e => e.Path.EndsWith(".territory", StringComparison.Ordinal) && e.Code == ValidationCode.OutOfRange);
        Assert.Contains(Vallons14(r => Population(r, "loup1")["territory"] = 257).Report.Errors, e => e.Path.EndsWith(".territory", StringComparison.Ordinal));
        // Le cœur des Vallons tient toute l'aire : aucun bord intérieur, le territoire va jusqu'au secteur entier (256 m).
        Assert.Null(ZoneValidator.InnerEdgeDistance(ok.Zone.Region!, ok.Zone.Setup.Space!, ok.Zone.PopulationOf("loup1")!.Template.Placement!.Position));
        Assert.True(Vallons14(r => Population(r, "loup1")["territory"] = 256).IsValid);
    }

    [Fact]
    public void Bord_interieur_d_un_secteur_et_marge_du_territoire()
    {
        // Aire de deux secteurs : le bord commun (x = 256) est intérieur ; les bords de l'aire ne le sont pas.
        CombatSpace area = Map().Area.Space!;
        Assert.Equal(Fixed.FromInt(6), ZoneValidator.InnerEdgeDistance(Grid, area, P(250, 128)));
        Assert.Equal(Fixed.FromInt(10), ZoneValidator.InnerEdgeDistance(Grid, area, P(266, 128)));
        Assert.Equal(Fixed.FromInt(156), ZoneValidator.InnerEdgeDistance(Grid, area, P(100, 3)));   // le bord sud est celui de l'aire
        Assert.Null(ZoneValidator.InnerEdgeDistance(Grid, area, P(600, 3)));
        // Départ à 8 m du bord, territoire de 40 m : la créature ne dépasse jamais la marge de 32 m.
        Assert.True(Fixed.FromInt(40) <= ZoneValidator.MinSectorInset + ZoneValidator.SectorMargin);
    }

    [Fact]
    public void Distance_au_bord_du_secteur()
    {
        Assert.Equal(Fixed.FromInt(8), RegionGrid.DistanceToSectorEdge(Grid, P(8, 128)));
        Assert.Equal(Fixed.FromInt(6), RegionGrid.DistanceToSectorEdge(Grid, P(250, 128)));
        Assert.Equal(Fixed.FromInt(10), RegionGrid.DistanceToSectorEdge(Grid, P(266, 128)));
        Assert.Equal(Fixed.Zero, RegionGrid.DistanceToSectorEdge(Grid, P(600, 128)));
    }

    [Fact]
    public void Creature_engagee_hors_de_son_territoire_lache()
    {
        // Un loup qui garde son territoire de 5 m : engagé, il poursuit le joueur qui s'éloigne ; sorti du territoire, il lâche et une vie
        // calme le remplace chez lui.
        ZoneDefinition zone = Vallons14(r => Population(r, "loup1")["territory"] = 5).Zone!;
        Population wolf = zone.PopulationOf("loup1")!;
        GroundVector home = wolf.Template.Placement!.Position;
        SectorWorld w = SectorWorld.For(zone, 0, 1);
        w.Enter(Hero("bastion", D(home.X - Fixed.FromInt(6)), D(home.Z)));
        var populations = new List<(string Sector, PopulationEvent Event)>();
        w.Step(populationEvents: populations);
        Walk(w, "bastion", -1, 0);
        for (int i = 0; i < 1200 && !populations.Any(p => p.Event.Kind == PopulationEventKind.Leashed); i++) w.Step(populationEvents: populations);
        (string sector, PopulationEvent leashed) = populations.First(p => p.Event.Kind == PopulationEventKind.Leashed);
        Assert.Equal(("s07_06", "loup1"), (sector, leashed.Population));
        w.Step();   // départ effectif en fin du tick suivant
        CombatantView gone = w.SectorZone("s07_06")!.Engine.View(leashed.Creature);
        Assert.True(gone.HasLeft && !gone.IsDead);
    }

    // --- Déterminisme -------------------------------------------------------------------------------------------------------------------

    private static (List<string> Events, Dictionary<string, MemoryCombatLog> Logs) Play(bool second, bool reversed)
    {
        var logs = new Dictionary<string, MemoryCombatLog>();
        SectorWorld w = World(Map(Wolf("loup_ouest", 120, 60), Wolf("loup_est", 420, 100)), logs);
        var players = new List<CharacterSetup> { Hero("alice", 200, 128) };
        if (second) players.Add(Hero("bruno", 480, 200));
        if (reversed) players.Reverse();
        foreach (CharacterSetup p in players) w.Enter(p);
        var events = new List<string>();
        for (int t = 0; t < 900; t++)
        {
            if (t == 1) Walk(w, "alice", 1, 0);
            if (t == 300) Walk(w, "alice", -1, 0);
            if (second && t == 1) Walk(w, "bruno", 0, -1);
            events.AddRange(w.Step().Select(e => e.ToString()));
        }
        return (events, logs);
    }

    [Fact]
    public void Deterministe_et_independant_de_l_ordre_des_joueurs_et_des_autres_secteurs()
    {
        var (a, logsA) = Play(second: false, reversed: false);
        var (a2, logsA2) = Play(second: false, reversed: false);
        Assert.Equal(a, a2);
        Assert.Equal(logsA.Keys, logsA2.Keys);
        foreach (string k in logsA.Keys) Assert.Equal(logsA[k].Lines, logsA2[k].Lines);
        // Deux joueurs annoncés dans un ordre ou dans l'autre : mêmes sessions, octet par octet.
        var (b, logsB) = Play(second: true, reversed: false);
        var (c, logsC) = Play(second: true, reversed: true);
        Assert.Equal(b, c);
        foreach (string k in logsB.Keys) Assert.Equal(logsB[k].Lines, logsC[k].Lines);
        // Le secteur ouest ne dépend pas de l'activité du secteur est (ouvert d'emblée par le second joueur dans B) : même journal.
        Assert.Equal(logsA["s00_00/1"].Lines.Skip(1), logsB["s00_00/1"].Lines.Skip(1));
        Assert.Contains(a, e => e.Contains("Arrived s01_00", StringComparison.Ordinal));
    }

    [Fact]
    public void Entrees_refusees_sans_rien_changer()
    {
        SectorWorld w = World(Map());
        Assert.Throws<ArgumentException>(() => w.Enter(new CharacterSetup("sans_place", "joueurs", Hero("x", 1, 1).Class, Hero("x", 1, 1).Build)));
        Assert.Throws<ArgumentException>(() => w.Enter(Hero("loin", 100, 900)));   // hors de l'aire
        w.Enter(Hero("alice", 100, 100));
        Assert.Throws<ArgumentException>(() => w.Enter(Hero("alice", 120, 100)));
        Assert.Throws<ArgumentException>(() => w.Exit("inconnu"));
        Assert.Empty(w.OpenSectors);
        w.Step();
        Assert.Equal(new[] { "alice" }, w.Players);
        w.Exit("alice");
        w.Step();
        Assert.Empty(w.Players);
    }
}
