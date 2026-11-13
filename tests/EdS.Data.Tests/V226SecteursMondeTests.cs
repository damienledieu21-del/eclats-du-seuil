using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Saves;
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

    private static SectorMap Map(params CombatantSetup[] creatures) => Map(Shipped.Combat, creatures);

    private static SectorMap Map(CombatRules rules, params CombatantSetup[] creatures)
    {
        var rock = new SpaceObstacle("rocher", new[] { P(255, 200), P(258, 200), P(258, 203), P(255, 203) });
        var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, rules, creatures, Array.Empty<CombatCommand>(),
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
        // Relecture indépendante de la PR #150 : un joueur blessé et fatigué, qui vient de frapper (recharges en cours), passe à l'est : il
        // arrive avec les mêmes PV, la même endurance, la même Résonance et les mêmes recharges restantes.
        CharacterSetup hero = Hero("alice", 242, 128);
        hero = new CharacterSetup(hero.Id, hero.Team, hero.Class, hero.Build, 0, hero.Placement, 0, carried: new CarriedState(health: 200, endurance: 30, enduranceRegenMs: 1000));
        var souche = new DummySetup("souche", "cibles", Fixed.FromInt(50), Fixed.Zero, placement: new CombatantPlacement(P(242, 129.5), Fixed.FromRatio(1, 2)));
        SectorWorld w = World(Map(souche));
        w.Enter(hero);
        w.Step();
        CombatEngine west = w.EngineOf("alice");
        Assert.Equal((200L, 30L), (west.View("alice").Health!.Value, west.View("alice").Endurance));
        west.Submit(new CombatCommand(west.CurrentTick * 50, "alice", "skill.bastion.frappe_du_rempart", "souche"));
        w.Step();
        CombatantView struck = west.View("alice");
        Assert.True(struck.CooldownReadyTick("skill.bastion.frappe_du_rempart") > west.CurrentTick);
        Assert.Equal(new[] { "s00_00", "s01_00" }, w.OpenSectors);   // 14 m du voisin : ouvert d'emblée
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
        CombatEngine east = w.EngineOf("alice");
        long cooldownLeft = before.CooldownReadyTick("skill.bastion.frappe_du_rempart") - west.CurrentTick;
        Assert.True(cooldownLeft > 0, "la recharge de la frappe doit courir encore au passage");
        w.Step();
        CombatantView after = w.ViewOf("alice");
        Assert.True(after.IsPresent);
        Assert.Equal(200L, after.Health);
        Assert.True(after.Health < after.MaxHealth);
        Assert.InRange(after.Endurance, before.Endurance, before.Endurance + 1);
        Assert.True(after.Endurance < after.MaxEndurance);
        Assert.Equal((before.Resonance, before.RampartCharges), (after.Resonance, after.RampartCharges));
        // Même recharge restante, en ticks du monde (les deux sessions ne sont pas ouvertes au même tick : leurs numéros de tick diffèrent).
        Assert.Equal(cooldownLeft - 1, after.CooldownReadyTick("skill.bastion.frappe_du_rempart") - east.CurrentTick);
        Assert.Equal(before.Facing, after.Facing);
        Assert.NotNull(after.MoveDirection);   // la marche continue de l'autre côté
        Assert.True(after.Position!.Value.X > before.Position!.Value.X);
        Assert.All(w.OpenSectors, s => Assert.Equal(w.WorldTick, w.SectorZone(s)!.WorldTick));
    }

    [Fact]
    public void Pas_de_depart_sans_place_dans_le_secteur_d_arrivee()
    {
        // Relecture indépendante de la PR #150 : secteur est plein (deux places, deux loups au calme) : le joueur ne part pas, il reste dans
        // son secteur (bloqué au bout de la marge) et n'est jamais perdu.
        CombatRules two = Rules(Shipped.Combat, 2);
        SectorMap map = Map(two, Wolf("loup_a", 450, 60), Wolf("loup_b", 450, 200));
        SectorWorld w = World(map);
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Assert.Equal(0, w.SectorZone("s01_00")!.Engine.FreePlaces);
        Walk(w, "alice", 1, 0);
        List<SectorEvent> events = Run(w, 400);
        Assert.DoesNotContain(events, e => e.Kind == SectorEventKind.Departed || e.Kind == SectorEventKind.Arrived);
        Assert.Equal("s00_00", w.SectorOf("alice"));
        CombatantView v = w.ViewOf("alice");
        Assert.True(v.IsPresent && !v.IsDead);
        Assert.True(v.Position!.Value.X > Fixed.FromInt(286));   // au bout de la marge
    }

    /// <summary>Les règles livrées, places limitées à <paramref name="places"/> (copie des propriétés).</summary>
    private static CombatRules Rules(CombatRules shipped, int places)
    {
        var copy = new CombatRules();
        foreach (var property in typeof(CombatRules).GetProperties().Where(p => p.CanWrite))
            property.SetValue(copy, property.GetValue(shipped));
        typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCombatants))!.SetValue(copy, places);
        return copy;
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

    [Fact]
    public void Fermeture_purge_les_cles_de_mort_et_sauvegarde_les_sessions_des_secteurs()
    {
        SectorWorld w = World(Map(Wolf("loup_est", 400, 128)));
        w.Enter(Hero("alice", 200, 128));
        w.Step();
        Zone east = w.SectorZone("s01_00")!;
        Assert.True(east.Consume("zone.essai/s01_00/1/death/loup_est"));
        Assert.True(east.Consume("zone.essai/quest/source"));
        Assert.Contains("zone.essai/s01_00/1/death/loup_est", w.Consumed);   // registre vivant : la session est ouverte
        Walk(w, "alice", -1, 0);
        Run(w, 3000, e => e.Kind == SectorEventKind.Closed);
        Assert.Equal(new[] { "s00_00" }, w.OpenSectors);
        Assert.DoesNotContain("zone.essai/s01_00/1/death/loup_est", w.Consumed);
        Assert.Contains("zone.essai/quest/source", w.Consumed);
        EdS.Data.Saves.WorldSave save = w.Save();
        Assert.Equal(1L, save.Sectors["s01_00"]);
        Assert.DoesNotContain(save.Consumed, EdS.Data.Saves.SaveValidator.IsDeathKey);
        Assert.Equal(save.Sectors, SaveJsonReader.Read(SaveJsonReader.Write(save)).Save!.Sectors);
        // Reprise : la réouverture prend la session suivante.
        var resumed = new SectorWorld(w.Map, "zone.essai", save.WorldTick, 2, save.Consumed, sectorSessions: save.Sectors);
        resumed.Enter(Hero("alice", 250, 128));
        resumed.Step();
        Assert.Equal(2L, resumed.SectorSessions["s01_00"]);
    }

    // --- Revue Codex de la PR #150 ------------------------------------------------------------------------------------------------------

    private static readonly RegionGrid Wide = new(0, 0, 6, 2);   // trois secteurs : s00_00, s01_00, s02_00

    private static SectorMap WideMap(params CombatantSetup[] creatures)
    {
        var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, Shipped.Combat, creatures, Array.Empty<CombatCommand>(),
            Shipped.Data, new CombatSpace(P(0, 0), P(768, 256)), zone: true);
        return new SectorMap(Wide, area, P(64, 128));
    }

    [Fact]
    public void Deux_joueurs_de_deux_secteurs_arrivent_au_meme_tick_dans_le_meme_secteur()
    {
        // Alice vient de l'ouest, Bruno de l'est ; tous deux entrent dans le secteur du milieu au même tick du monde : aucun n'est perdu.
        SectorWorld w = new(WideMap(), "zone.essai", 0, 1);
        w.Enter(Hero("alice", 255.5, 128));
        w.Enter(Hero("bruno", 512.5, 128));
        w.Step();
        Assert.Equal(("s00_00", "s02_00"), (w.SectorOf("alice"), w.SectorOf("bruno")));
        Walk(w, "alice", 1, 0);
        Walk(w, "bruno", -1, 0);
        var events = new List<SectorEvent>();
        for (int i = 0; i < 200 && events.Count(e => e.Kind == SectorEventKind.Arrived) < 2; i++) events.AddRange(w.Step());
        SectorEvent[] arrived = events.Where(e => e.Kind == SectorEventKind.Arrived).ToArray();
        Assert.Equal(2, arrived.Length);
        Assert.Equal(arrived[0].WorldTick, arrived[1].WorldTick);   // positions symétriques : même tick
        Assert.All(arrived, a => Assert.Equal("s01_00", a.Sector));
        w.Step();
        Assert.True(w.ViewOf("alice").IsPresent && w.ViewOf("bruno").IsPresent);
        Assert.Equal(("s01_00", "s01_00"), (w.SectorOf("alice"), w.SectorOf("bruno")));
    }

    [Fact]
    public void Arrivee_programmee_d_une_creature_reservee_pour_le_passage()
    {
        // Une créature dont l'arrivée est programmée à ce tick (réapparition) tient déjà sa place : un joueur qui passe prend la place libre
        // la plus proche au lieu d'être refusé au début du tick.
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Zone east = w.SectorZone("s01_00")!;
        GroundVector at = P(270, 128);
        Fixed radius = Fixed.FromRatio(1, 2);
        Assert.Equal(at, w.FreeSpotIn("s01_00", at, radius));
        AssailantSetup wolf = Wolf("loup_surprise", 270, 128);
        east.Engine.Join(new AssailantSetup(wolf.Id, wolf.Team, wolf.Armor, wolf.MagicResistance, wolf.Target, 600_000, wolf.IntervalMs, wolf.Attacks, 0,
            wolf.StatusResistance, wolf.Triggers, wolf.Health, wolf.Placement, checked(east.Engine.CurrentTick * 50), wolf.Behavior));
        Assert.False(east.Engine.View("loup_surprise").IsPresent);   // pas encore arrivée
        GroundVector spot = w.FreeSpotIn("s01_00", at, radius)!.Value;
        Assert.NotEqual(at, spot);
        Assert.False(Planar.CirclesOverlap(spot.X.Raw, spot.Z.Raw, radius.Raw, at.X.Raw, at.Z.Raw, wolf.Placement!.Radius.Raw));
    }

    [Fact]
    public void Pas_de_depart_quand_le_secteur_d_arrivee_n_a_plus_d_identite()
    {
        // La session du secteur est n'a plus d'identité libre (1 024 combattants passés) : le joueur ne part pas et n'est pas perdu.
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        CombatEngine east = w.SectorZone("s01_00")!.Engine;
        int n = 0;
        while (east.IdentitiesLeft > 0)
        {
            var batch = new List<string>();
            while (east.IdentitiesLeft > 0 && east.FreePlaces > 0)
            {
                string id = $"souche{n++}";
                east.Join(new DummySetup(id, "cibles", Fixed.Zero, Fixed.Zero, placement: new CombatantPlacement(P(400 + n % 60, 20 + (n / 60) % 100 * 2), Fixed.FromRatio(1, 10)),
                    arrivalMs: checked(east.CurrentTick * 50)));
                batch.Add(id);
            }
            foreach (string id in batch) w.SectorZone("s01_00")!.Leave(id);
            w.Step();
        }
        Assert.Equal(0, east.IdentitiesLeft);
        Walk(w, "alice", 1, 0);
        List<SectorEvent> events = Run(w, 300);
        Assert.DoesNotContain(events, e => e.Kind == SectorEventKind.Departed || e.Kind == SectorEventKind.Arrived);
        Assert.Equal("s00_00", w.SectorOf("alice"));
        Assert.True(w.ViewOf("alice").IsPresent);
    }

    [Fact]
    public void Coeur_rouvert_en_session_nouvelle_cles_au_format_du_coeur()
    {
        var logs = new Dictionary<string, MemoryCombatLog>();
        SectorWorld w = World(Map(), logs);
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Assert.Equal("zone.essai/1/death/x", w.SectorZone("s00_00")!.EventKey(ZoneEventKind.Death, "x"));
        Walk(w, "alice", 1, 0);
        Run(w, 3000, e => e.Kind == SectorEventKind.Closed && e.Sector == "s00_00");
        Assert.Null(w.SectorZone("s00_00"));
        Walk(w, "alice", -1, 0);
        Run(w, 3000, e => e.Kind == SectorEventKind.Opened && e.Sector == "s00_00");
        Zone home = w.SectorZone("s00_00")!;
        Assert.Equal(2L, home.Session);
        Assert.Equal("zone.essai/2/death/x", home.EventKey(ZoneEventKind.Death, "x"));   // format du cœur, session nouvelle
        Assert.Equal(SectorMap.SeedOf(7, "s00_00", 2), home.Setup.Seed);
        Assert.Equal(2L, w.SectorSessions["s00_00"]);
        Assert.True(logs.ContainsKey("s00_00/1") && logs.ContainsKey("s00_00/2"));   // journaux distincts
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

    // --- Revue Codex de la PR #150 (2e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Sortie_juste_apres_l_arrivee_d_un_passage_aucun_combattant_sans_suivi()
    {
        // P1 : Exit au tick où l'arrivée du passage est programmée : le combattant arrive puis s'en va dans le même tick ; il n'est jamais
        // présent, actif et ciblable sans joueur.
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Walk(w, "alice", 1, 0);
        List<SectorEvent> events = Run(w, 200, e => e.Kind == SectorEventKind.Arrived);
        Assert.Contains(events, e => e.Kind == SectorEventKind.Arrived && e.Sector == "s01_00");
        CombatEngine east = w.EngineOf("alice");
        string id = w.CombatantOf("alice");
        Assert.False(east.View(id).IsPresent);   // arrivée programmée au prochain tick
        w.Exit("alice");
        w.Step();
        Assert.Empty(w.Players);
        for (int i = 0; i < 5; i++)
        {
            CombatantView v = east.View(id);
            Assert.False(v.IsPresent);
            Assert.True(v.HasLeft);
            w.Step();
        }
    }

    [Fact]
    public void Deux_joueurs_entres_au_meme_endroit_tous_deux_places()
    {
        // P2 : deux joueurs entrent au même endroit : tous deux arrivent (les personnages ne se bloquent pas entre eux, règle de contact) ;
        // aucun n'est laissé sans combattant.
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 100, 100));
        w.Enter(Hero("bruno", 100, 100));
        w.Step();
        CombatantView a = w.ViewOf("alice"), b = w.ViewOf("bruno");
        Assert.True(a.IsPresent && b.IsPresent);
        Assert.False(w.IsWaiting("alice") || w.IsWaiting("bruno"));
        Assert.Throws<ArgumentException>(() => w.Enter(Hero("bruno", 120, 100)));   // déjà là, et bien là
    }

    [Fact]
    public void Joueur_entre_sur_une_creature_solide_place_a_cote()
    {
        SectorWorld w = World(Map(Wolf("loup", 100, 100)));
        w.Enter(Hero("alice", 100, 100));
        w.Step();
        CombatantView a = w.ViewOf("alice");
        Assert.True(a.IsPresent);
        Assert.NotEqual(P(100, 100), a.Position!.Value);
        Assert.True(w.EngineOf("alice").View("loup").IsPresent);
    }

    [Fact]
    public void Joueur_entre_dans_un_secteur_plein_attend_puis_prend_la_place_liberee()
    {
        // P2 : plus aucune place (deux places : un loup au calme et Alice) : le joueur attend (sans combattant) et réessaie à chaque tick ; il entre dès qu'une place se libère.
        SectorWorld w = World(Map(Rules(Shipped.Combat, 2), Wolf("loup", 230, 230)));
        w.Enter(Hero("alice", 100, 100));
        w.Step();
        w.Enter(Hero("bruno", 140, 100));
        Run(w, 3);
        Assert.True(w.IsWaiting("bruno"));
        Assert.False(w.IsWaiting("alice"));
        Assert.Throws<InvalidOperationException>(() => w.CombatantOf("bruno"));
        Assert.Throws<ArgumentException>(() => w.Enter(Hero("bruno", 140, 100)));
        Assert.Equal(new[] { "alice", "bruno" }, w.Players);
        w.Exit("alice");
        w.Step();   // départ d'Alice en fin de tick
        w.Step();   // place libre : Bruno arrive
        Assert.False(w.IsWaiting("bruno"));
        Assert.True(w.ViewOf("bruno").IsPresent);
        Assert.Equal(new[] { "bruno" }, w.Players);
    }

    // --- Revue Codex de la PR #150 (3e tour) ---------------------------------------------------------------------------------------------

    /// <summary>Carte d'essai dont chaque session de secteur dure <paramref name="durationMs"/>.</summary>
    private static SectorMap ShortMap(long durationMs, CombatRules rules, params CombatantSetup[] creatures)
    {
        SectorMap m = Map(rules, creatures);
        CombatSetup a = m.Area;
        var area = new CombatSetup(a.Seed, durationMs, a.Stats, a.Damage, a.Progression, a.Combat, a.Combatants, a.Commands, a.Data, a.Space, a.Zone, a.Truces);
        return new SectorMap(Grid, area, P(64, 128));
    }

    private static CharacterSetup Hurt(string id, double x, double z, long health)
    {
        CharacterSetup h = Hero(id, x, z);
        return new CharacterSetup(h.Id, h.Team, h.Class, h.Build, 0, h.Placement, 0, carried: new CarriedState(health: health));
    }

    [Fact]
    public void Joueur_present_jusqu_a_la_duree_limite_le_secteur_repart_et_le_monde_continue()
    {
        // P1 : la session du secteur atteint sa durée (5 s) : le secteur repart dans une session nouvelle au même tick du monde, le joueur y
        // arrive avec son état ; le monde continue, plusieurs fois de suite.
        SectorWorld w = World(ShortMap(5_000, Shipped.Combat));
        w.Enter(Hurt("alice", 100, 128, 200));
        w.Step();
        Assert.Equal(200L, w.ViewOf("alice").Health);
        long session = w.SectorZone("s00_00")!.Session;
        var renewed = new List<SectorEvent>();
        for (int i = 0; i < 400; i++)
        {
            renewed.AddRange(w.Step().Where(e => e.Kind == SectorEventKind.Renewed && e.Sector == "s00_00"));
            Assert.All(w.OpenSectors, s => Assert.Equal(w.WorldTick, w.SectorZone(s)!.WorldTick));
        }
        Assert.True(renewed.Count >= 3, $"{renewed.Count} renouvellements");
        Assert.True(w.SectorZone("s00_00")!.Session > session);
        Assert.Equal(w.SectorZone("s00_00")!.Session.ToString(System.Globalization.CultureInfo.InvariantCulture), renewed[^1].Other);
        CombatantView v = w.ViewOf("alice");
        Assert.True(v.IsPresent && !v.IsDead);
        Assert.True(v.Health <= 200 && v.Health < v.MaxHealth, "l'état (PV) passe d'une session à la suivante");
        Assert.False(w.SectorZone("s00_00")!.IsOver);
        // Il marche encore d'une session à l'autre.
        Walk(w, "alice", 1, 0);
        double x0 = D(w.ViewOf("alice").Position!.Value.X);
        Run(w, 120);
        Assert.True(D(w.ViewOf("alice").Position!.Value.X) > x0);
    }

    private static void Exhaust(SectorWorld w, string sector, int keep, double x0)
    {
        Zone zone = w.SectorZone(sector)!;
        int n = 0;
        while (zone.Engine.IdentitiesLeft > keep)
        {
            var batch = new List<string>();
            while (zone.Engine.IdentitiesLeft > keep && zone.Engine.FreePlaces > 0)
            {
                string id = $"souche_{sector}_{n++}";
                zone.Engine.Join(new DummySetup(id, "cibles", Fixed.Zero, Fixed.Zero,
                    placement: new CombatantPlacement(P(x0 + n % 60, 20 + (n / 60) % 100 * 2), Fixed.FromRatio(1, 10)), arrivalMs: checked(zone.Engine.CurrentTick * 50)));
                batch.Add(id);
            }
            foreach (string id in batch) zone.Leave(id);
            w.Step();
        }
    }

    [Fact]
    public void Reprise_sans_place_des_deux_cotes_gardee_jusqu_au_placement()
    {
        // P2 : au moment du passage, plus aucune identité ni à l'arrivée ni au départ : la reprise est gardée (sans combattant), jamais
        // effacée ; elle est faite dès qu'une place existe (ici, session nouvelle du secteur de départ à sa durée), avec le même état.
        SectorWorld w = World(ShortMap(60_000, Shipped.Combat));
        w.Enter(Hurt("alice", 250, 128, 200));
        w.Step();
        Exhaust(w, "s00_00", 0, 20);
        Exhaust(w, "s01_00", 1, 400);
        Walk(w, "alice", 1, 0);
        Run(w, 300, e => e.Kind == SectorEventKind.Departed);
        Zone east = w.SectorZone("s01_00")!;
        east.Engine.Join(new DummySetup("dernier", "cibles", Fixed.Zero, Fixed.Zero, placement: new CombatantPlacement(P(500, 250), Fixed.FromRatio(1, 10)),
            arrivalMs: checked(east.Engine.CurrentTick * 50)));
        Assert.Equal(0, east.Engine.IdentitiesLeft);
        List<SectorEvent> events = Run(w, 5);
        Assert.DoesNotContain(events, e => e.Kind == SectorEventKind.Arrived);
        Assert.Contains("alice", w.Players);
        Assert.True(w.IsResuming("alice"));
        Assert.Throws<InvalidOperationException>(() => w.CombatantOf("alice"));
        Run(w, 1300, e => e.Kind == SectorEventKind.Renewed);
        w.Step();
        Assert.False(w.IsResuming("alice"));
        CombatantView v = w.ViewOf("alice");
        Assert.True(v.IsPresent && !v.IsDead);
        Assert.Equal(200L, v.Health);
    }

    [Fact]
    public void Cles_de_mort_des_sessions_fermees_purgees_faits_persistants_gardes()
    {
        // P2 : de nombreuses sessions successives du même secteur (durée de 1 s) : les clés de mort de chaque session fermée sont purgées,
        // les faits persistants (autres clés) gardés ; le registre ne grossit pas.
        SectorWorld w = World(ShortMap(1_000, Shipped.Combat), null);
        w.Enter(Hero("alice", 100, 128));
        w.Step();
        int sessions = 0;
        for (int i = 0; i < 4000 && sessions < 60; i++)
        {
            Zone home = w.SectorZone("s00_00")!;
            if (home.Session != sessions)
            {
                for (int k = 0; k < 50; k++) Assert.True(home.Consume(home.EventKey(ZoneEventKind.Death, $"loup{k}")));
                Assert.True(home.Consume($"zone.essai/coffre_{home.Session}"));
                sessions = (int)home.Session;
            }
            w.Step();
        }
        Assert.True(sessions >= 60);
        IReadOnlyList<string> consumed = w.Consumed;
        Assert.True(consumed.Count(k => k.Contains("/death/")) <= 50, $"{consumed.Count(k => k.Contains("/death/"))} clés de mort gardées");
        Assert.All(consumed.Where(k => k.Contains("/death/")), k => Assert.StartsWith($"zone.essai/{w.SectorZone("s00_00")!.Session}/death/", k));
        Assert.True(consumed.Count(k => k.StartsWith("zone.essai/coffre_", StringComparison.Ordinal)) >= 59);
    }

    [Fact]
    public void Fermeture_d_un_secteur_purge_ses_cles_de_mort()
    {
        SectorWorld w = World(Map(Wolf("loup_est", 400, 128)));
        w.Enter(Hero("alice", 100, 128));
        w.Step();
        Walk(w, "alice", 1, 0);
        Run(w, 400, e => e.Kind == SectorEventKind.Opened);
        Zone east = w.SectorZone("s01_00")!;
        Assert.True(east.Consume(east.EventKey(ZoneEventKind.Death, "loup_est")));
        Assert.True(east.Consume("zone.essai/s01_00/coffre"));
        Walk(w, "alice", -1, 0);
        Run(w, 2000, e => e.Kind == SectorEventKind.Closed);
        Assert.DoesNotContain("zone.essai/s01_00/1/death/loup_est", w.Consumed);
        Assert.Contains("zone.essai/s01_00/coffre", w.Consumed);
    }

    // --- Revue Codex de la PR #150 (4e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Populations_a_heures_voulues_comme_dans_l_hote_d_une_zone()
    {
        // P2 : le prédicat de présence (heures, temps, nuits) est passé aux populations de chaque secteur, au tick du monde : même journal
        // que l'hôte d'une zone avec le même prédicat, et les créatures à heures arrivent quand elles sont voulues.
        ZoneDefinition vallons = V11TestData.Zone();
        Population timed = vallons.Populations.First(x => x.Timed);
        bool Wanted(Population pop, long tick) => tick >= 20;
        var log = new MemoryCombatLog();
        var reference = new MemoryCombatLog();
        var seen = new List<long>();
        var world = SectorWorld.For(vallons, 0, 1, logs: (sector, session) => sector == "s07_06" ? log : NullCombatLog.Instance,
            wanted: (pop, tick) => { seen.Add(tick); return Wanted(pop, tick); });
        var zone = new Zone(vallons.Id, ZoneSetups.For(vallons, Array.Empty<string>()), 1, 0, log: reference);
        var populations = new ZonePopulations(vallons);
        world.Enter(Hero("bastion", -18, -12));
        zone.Enter(Hero("bastion", -18, -12));
        var events = new List<(string Sector, PopulationEvent Event)>();
        var expected = new List<PopulationEvent>();
        for (int i = 0; i < 200; i++)
        {
            world.Step(populationEvents: events);
            zone.Step();
            expected.AddRange(populations.Step(zone, 0, Wanted));
        }
        Assert.Equal(reference.Lines, log.Lines);
        static string Text(PopulationEvent e) => $"{e.Kind}/{e.Population}/{e.Creature}/{e.Tick}";
        Assert.Equal(expected.Select(Text), events.Select(e => Text(e.Event)));
        Assert.Contains(events, e => e.Event.Population == timed.Creature && e.Event.Tick >= 20);
        Assert.NotEmpty(seen);

        // Sans prédicat : jamais voulues (comme l'hôte d'une zone sans prédicat).
        var quiet = SectorWorld.For(vallons, 0, 1);
        quiet.Enter(Hero("bastion", -18, -12));
        var none = new List<(string Sector, PopulationEvent Event)>();
        for (int i = 0; i < 200; i++) quiet.Step(populationEvents: none);
        Assert.DoesNotContain(none, e => e.Event.Population == timed.Creature && e.Event.Tick >= 20);
        Assert.NotEqual(events.Select(e => Text(e.Event)), none.Select(e => Text(e.Event)));
    }

    [Fact]
    public void Predicat_de_presence_au_tick_du_monde()
    {
        // Un monde qui commence au tick 1 000 : le prédicat reçoit le tick du monde, pas celui de la session du secteur.
        ZoneDefinition vallons = V11TestData.Zone();
        var seen = new List<long>();
        var world = SectorWorld.For(vallons, 1_000, 1, wanted: (pop, tick) => { seen.Add(tick); return false; });
        world.Enter(Hero("bastion", -18, -12));
        for (int i = 0; i < 10; i++) world.Step();
        Assert.NotEmpty(seen);
        Assert.All(seen, t => Assert.InRange(t, 1_000, 1_011));
    }

    [Fact]
    public void Arrivee_refusee_sans_rien_changer_si_la_commande_de_marche_ne_tient_pas()
    {
        // P1 : la marche qui continue de l'autre côté a son budget (maxCommands) réservé avant l'arrivée. Le budget de commandes du secteur
        // est s'épuise entre le départ et l'arrivée : l'arrivée n'y est pas admise (refus atomique : aucun combattant d'Alice à l'est) ; Alice
        // revient à l'ouest avec son état, son suivi reste cohérent, le monde n'est jamais interrompu par une commande refusée.
        CombatRules rules = Rules(Shipped.Combat, Shipped.Combat.MaxCombatants);
        typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCommands))!.SetValue(rules, 8);
        SectorWorld w = World(Map(rules));
        w.Enter(Hurt("alice", 250, 128, 200));
        w.Step();
        CombatEngine east = w.SectorZone("s01_00")!.Engine;
        foreach (string id in new[] { "souche_a", "souche_b" })
            east.Join(new DummySetup(id, "cibles", Fixed.FromInt(50), Fixed.Zero, placement: new CombatantPlacement(P(id == "souche_a" ? 500 : 480, 250), Fixed.FromRatio(1, 2)),
                arrivalMs: checked(east.CurrentTick * 50)));
        w.Step();
        Walk(w, "alice", 1, 0);
        List<SectorEvent> run = Run(w, 300, e => e.Kind == SectorEventKind.Departed);
        Assert.Contains(run, e => e.Kind == SectorEventKind.Departed);
        foreach (string id in new[] { "souche_a", "souche_b" })
            for (int k = 0; k < 4; k++)
                east.Submit(new CombatCommand(east.CurrentTick * 50, id, k % 2 == 0 ? CombatAction.GuardStart : CombatAction.GuardStop));
        Assert.True(east.HasRoomToJoinWith(0));
        Assert.False(east.HasRoomToJoinWith(1));
        List<SectorEvent> back = w.Step().ToList();
        Assert.Contains(back, e => e.Kind == SectorEventKind.Arrived && e.Sector == "s00_00" && e.Other == "s00_00");   // retour au départ
        Assert.DoesNotContain(east.CombatantIds, id => id.StartsWith("alice", StringComparison.Ordinal));
        w.Step();
        Assert.Equal("s00_00", w.SectorOf("alice"));
        CombatantView v = w.ViewOf("alice");
        Assert.True(v.IsPresent);
        Assert.Equal(200L, v.Health);
        // Ensuite, plus de départ vers un secteur sans budget pour l'arrivée : Alice reste à l'ouest, sans erreur.
        for (int i = 0; i < 200; i++)
        {
            Assert.DoesNotContain(w.Step(), e => e.Kind == SectorEventKind.Departed);
            Assert.Equal("s00_00", w.SectorOf("alice"));
        }
        Assert.True(w.ViewOf("alice").IsPresent);
    }

    // --- Revue Codex de la PR #150 (5e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Bonus_d_equipement_courant_garde_au_passage()
    {
        // P2 : un bonus d'équipement changé en jeu (commande gear) passe avec le joueur : PV au-dessus du maximum de départ, placement réussi
        // et bonus conservé.
        SectorWorld w = World(Map());
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        CombatEngine west = w.EngineOf("alice");
        long baseMax = west.View("alice").MaxHealth!.Value;
        var gear = new EdS.Domain.Stats.GearBonus(maxHealth: 500);
        west.Submit(CombatCommand.SetGear(west.CurrentTick * 50, "alice", gear));
        Run(w, 400, _ => w.ViewOf("alice").Health > baseMax);
        CombatantView before = w.ViewOf("alice");
        Assert.True(before.Health > baseMax, $"PV {before.Health}, maximum de départ {baseMax}");
        Assert.Equal(gear, before.Gear);
        Walk(w, "alice", 1, 0);
        Run(w, 300, e => e.Kind == SectorEventKind.Arrived);
        w.Step();
        Assert.Equal("s01_00", w.SectorOf("alice"));
        CombatantView after = w.ViewOf("alice");
        Assert.True(after.IsPresent);
        Assert.Equal(gear, after.Gear);
        Assert.True(after.Health > baseMax);
        Assert.Equal(before.MaxHealth, after.MaxHealth);
    }

    [Fact]
    public void Lacher_refuse_par_le_budget_la_creature_reste_et_le_monde_continue()
    {
        // P2 : au bout de la marge, le lâcher d'une créature est refusé (budget de commandes épuisé, InvalidOperationException) : elle reste
        // (nouvel essai au tick suivant), le monde n'est jamais interrompu.
        CombatRules rules = Rules(Shipped.Combat, Shipped.Combat.MaxCombatants);
        typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCommands))!.SetValue(rules, 1);
        SectorWorld w = World(Map(rules, Wolf("loup", 236, 128)));
        w.Enter(Hero("alice", 246, 128));
        w.Step();
        Walk(w, "alice", 1, 0);   // seule commande admise
        var events = new List<SectorEvent>();
        for (int i = 0; i < 1200; i++) events.AddRange(w.Step());
        Assert.DoesNotContain(events, e => e.Kind == SectorEventKind.Released || e.Kind == SectorEventKind.Departed);
        Zone west = w.SectorZone("s00_00")!;
        Assert.False(west.Engine.View("loup").HasLeft);
        Assert.Equal(1201L, w.WorldTick);
    }

    // --- Revue Codex de la PR #150 (6e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Secteur_abandonne_ferme_a_60_s_malgre_des_sessions_de_5_s()
    {
        // P2 : sessions de 5 s ; le secteur est, ouvert puis laissé sans joueur proche, se renouvelle plusieurs fois et se ferme quand même
        // 60 s après le départ du dernier joueur proche (le compteur d'inactivité passe d'une session à la suivante).
        SectorWorld w = World(ShortMap(5_000, Shipped.Combat));
        w.Enter(Hero("alice", 100, 128));
        w.Step();
        Walk(w, "alice", 1, 0);
        Run(w, 400, e => e.Kind == SectorEventKind.Opened && e.Sector == "s01_00");
        Walk(w, "alice", -1, 0);
        long left = w.WorldTick;
        List<SectorEvent> events = Run(w, 2_400, e => e.Kind == SectorEventKind.Closed && e.Sector == "s01_00");
        SectorEvent closed = Assert.Single(events, e => e.Kind == SectorEventKind.Closed && e.Sector == "s01_00");
        Assert.True(events.Count(e => e.Kind == SectorEventKind.Renewed && e.Sector == "s01_00") >= 5);
        Assert.InRange(closed.WorldTick - left, 1_200, 1_210);   // 60 s après le départ du dernier joueur proche
        Assert.Equal(new[] { "s00_00" }, w.OpenSectors);
    }

    // --- Revue Codex de la PR #150 (7e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Aire_dans_la_marge_du_coeur_mais_pas_dans_son_rectangle_pas_de_creature_simulee_deux_fois()
    {
        // L'exemple de la revue : aire de 280 × 256 m, entrée dans s00_00, créature en (270, 100) : elle appartient au seul secteur s01_00.
        var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, Shipped.Combat, new CombatantSetup[] { Wolf("loup", 270, 100) },
            Array.Empty<CombatCommand>(), Shipped.Data, new CombatSpace(P(0, 0), P(280, 256)), zone: true);
        var map = new SectorMap(Wide, area, P(64, 128));
        Assert.False(map.HomeHoldsArea);
        Assert.DoesNotContain(map.SetupFor("s00_00", 1).Combatants, c => c.Id == "loup");
        Assert.DoesNotContain(map.SetupFor("s00_00", 2, reopened: true).Combatants, c => c.Id == "loup");
        Assert.Contains(map.SetupFor("s01_00", 1).Combatants, c => c.Id == "loup");
    }

    [Fact]
    public void Propriete_chaque_creature_a_exactement_un_proprietaire()
    {
        // Aires variées (dans un secteur, sur une limite de secteur, débordant de quelques mètres, sur plusieurs secteurs) et places variées
        // (bords de l'aire, limites de secteur, coins) : chaque créature est dans la simulation d'exactement un secteur simulable, à la
        // première ouverture comme à la réouverture du cœur.
        double[] maxXs = { 128, 200, 256, 257, 280, 288, 300, 512, 520, 600, 768 }, maxZs = { 100, 128, 256 };
        double[] minXs = { 0, 64 };
        int cases = 0;
        foreach (double minX in minXs)
            foreach (double maxX in maxXs)
                foreach (double maxZ in maxZs)
                {
                    if (maxX <= minX + 16) continue;
                    var xs = new SortedSet<double> { minX + 1, (minX + maxX) / 2, maxX - 1, maxX - 0.0001 };
                    foreach (double edge in new double[] { 256, 512 }) if (edge > minX && edge < maxX) { xs.Add(edge); xs.Add(edge - 0.0001); xs.Add(edge + 0.0001); }
                    var zs = new SortedSet<double> { 1, maxZ / 2, maxZ - 1 };
                    var creatures = new List<CombatantSetup>();
                    int n = 0;
                    foreach (double x in xs)
                        foreach (double z in zs)
                            creatures.Add(new DummySetup($"c{n++}", "cibles", Fixed.FromInt(50), Fixed.Zero,
                                placement: new CombatantPlacement(new GroundVector(Fixed.FromRaw((long)Math.Round(x * 10_000)), Fixed.FromRaw((long)Math.Round(z * 10_000))), Fixed.FromRatio(1, 10))));
                    var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, Shipped.Combat, creatures,
                        Array.Empty<CombatCommand>(), Shipped.Data, new CombatSpace(P(minX, 0), P(maxX, maxZ)), zone: true);
                    foreach (double entryX in new[] { minX + 8, maxX - 8 })
                    {
                        var map = new SectorMap(Wide, area, P(entryX, Math.Min(64, maxZ / 2)));
                        string[] sectors = { "s00_00", "s01_00", "s02_00" };
                        foreach (bool reopen in new[] { false, true })
                        {
                            var owners = creatures.ToDictionary(c => c.Id, _ => new List<string>());
                            foreach (string sector in sectors.Where(x => map.Space(x) != null))
                                foreach (CombatantSetup c in map.SetupFor(sector, reopen ? 2 : 1, reopened: reopen && sector == map.HomeSector).Combatants)
                                    owners[c.Id].Add(sector);
                            foreach (var (id, list) in owners)
                                Assert.True(list.Count == 1, $"aire [{minX}, {maxX}] × [0, {maxZ}], cœur {map.HomeSector}, {id} en " +
                                    $"{creatures.Single(c => c.Id == id).Placement!.Position} : propriétaires [{string.Join(", ", list)}]");
                            cases++;
                        }
                    }
                }
        Assert.True(cases > 100);
    }

    // --- Revue Codex de la PR #150 (8e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Passage_ouvert_pendant_le_monde_son_obstacle_disparait_a_la_session_suivante()
    {
        // La clé du passage du moulin est consommée pendant le monde : la session en cours garde la porte ; après fermeture et réouverture
        // du cœur, la session nouvelle n'a plus la porte (docs/MONDE.md : « l'obstacle disparaît à la session suivante »).
        ZoneDefinition vallons = V11TestData.Zone();
        var moulin = vallons.Passages.Single(x => x.Id == "moulin");
        var world = SectorWorld.For(vallons, 0, 1);
        world.Enter(Hero("bastion", -18, -12));
        world.Step();
        Zone first = world.SectorZone("s07_06")!;
        Assert.Contains(first.Setup.Space!.Obstacles, o => o.Id == moulin.Obstacle);
        Assert.True(first.Consume(vallons.PassageKey("moulin")));
        world.Step();
        Assert.Contains(world.SectorZone("s07_06")!.Setup.Space!.Obstacles, o => o.Id == moulin.Obstacle);   // pas pendant la session
        world.Exit("bastion");
        Run(world, 1_400, e => e.Kind == SectorEventKind.Closed && e.Sector == "s07_06");
        Assert.Empty(world.OpenSectors);
        Assert.Contains(vallons.PassageKey("moulin"), world.Consumed);
        world.Enter(Hero("bastion", -18, -12));
        world.Step();
        Zone next = world.SectorZone("s07_06")!;
        Assert.True(next.Session > first.Session);
        Assert.DoesNotContain(next.Setup.Space!.Obstacles, o => o.Id == moulin.Obstacle);
        Assert.Equal(first.Setup.Space.Obstacles.Count - 1, next.Setup.Space.Obstacles.Count);
    }

    // --- Revue Codex de la PR #150 (9e tour) ---------------------------------------------------------------------------------------------

    [Fact]
    public void Registre_presque_plein_tick_refuse_entier_puis_repris_apres_liberation()
    {
        // P1 : deux secteurs ouverts (Alice à la limite), registre presque plein : le tick est refusé entier (aucun secteur n'avance, ni le tick
        // du monde) ; dès que la place exigée est libérée (ici : Bruno renonce à entrer), le monde reprend.
        SectorWorld Make(int keys)
        {
            var consumed = Enumerable.Range(0, keys).Select(k => $"zone.essai/fait_{k:D6}");
            return new SectorWorld(Map(Wolf("loup_ouest", 100, 60), Wolf("loup_est", 400, 128)), "zone.essai", 0, 1, consumed);
        }
        SectorWorld twin = Make(0);
        twin.Enter(Hero("alice", 250, 128));
        twin.Step();
        twin.Enter(Hero("bruno", 100, 100));
        long rest = twin.NextTickRegistryNeed;
        Assert.InRange(rest, 5, 50);
        SectorWorld w = Make(checked((int)(SaveValidator.MaxConsumed - rest + 1)));
        w.Enter(Hero("alice", 250, 128));
        w.Step();
        Assert.Equal(new[] { "s00_00", "s01_00" }, w.OpenSectors);
        w.Enter(Hero("bruno", 100, 100));
        Assert.Equal(SaveValidator.MaxConsumed + 1L, w.NextTickRegistryNeed);
        long tick = w.WorldTick;
        var before = w.OpenSectors.ToDictionary(x => x, x => w.SectorZone(x)!.Engine.CurrentTick);
        Assert.Throws<InvalidOperationException>(() => w.Step());
        Assert.Throws<InvalidOperationException>(() => w.Step());
        Assert.Equal(tick, w.WorldTick);
        Assert.All(w.OpenSectors, x => Assert.Equal((before[x], tick), (w.SectorZone(x)!.Engine.CurrentTick, w.SectorZone(x)!.WorldTick)));
        Assert.Equal(new[] { "alice" }, w.Players);
        Assert.True(w.CancelEnter("bruno"));
        Assert.Equal(SaveValidator.MaxConsumed + 0L, w.NextTickRegistryNeed);
        w.Step();
        Assert.Equal(tick + 1, w.WorldTick);
        Assert.All(w.OpenSectors, x => Assert.Equal(w.WorldTick, w.SectorZone(x)!.WorldTick));
        Assert.True(w.ViewOf("alice").IsPresent);
    }

    [Fact]
    public void Entree_invalide_refusee_tout_de_suite_avec_son_diagnostic()
    {
        // P2 : PV emportés au-delà du maximum : refus définitif à l'annonce (diagnostic), pas une attente de place.
        SectorWorld w = World(Map());
        ArgumentException e = Assert.Throws<ArgumentException>(() => w.Enter(Hurt("alice", 100, 128, 10_000_000)));
        Assert.Contains("alice", e.Message);
        Assert.Contains("carried", e.Message, StringComparison.OrdinalIgnoreCase);
        w.Step();
        Assert.Empty(w.Players);
        // Une entrée valide au même endroit (sur un loup) reste admise : la place exacte est choisie à l'arrivée.
        SectorWorld v = World(Map(Wolf("loup", 100, 100)));
        v.Enter(Hero("bruno", 100, 100));
        v.Step();
        Assert.True(v.ViewOf("bruno").IsPresent);
        Assert.Null(v.RefusalOf("bruno"));
    }

    [Fact]
    public void Registre_a_99_999_cles_aucun_secteur_n_avance_seul()
    {
        // Le scénario de la revue : registre à 99 999 clés ; le secteur ouest (aucun mortel) avancerait, l'est (Alice et un loup) refuserait
        // sa réservation : le tick est refusé entier, aucun secteur n'avance seul (avant : l'ouest avançait, le monde restait bloqué).
        SectorWorld w = World(Map(Wolf("loup_est", 400, 128)));
        w.Enter(Hero("alice", 300, 128));
        w.Step();
        Assert.Equal(new[] { "s00_00", "s01_00" }, w.OpenSectors);
        Zone east = w.SectorZone("s01_00")!;
        for (int k = 0; east.RegistryRoom > 1; k++) east.Consume($"zone.essai/fait_{k:D6}");
        long tick = w.WorldTick;
        Assert.Throws<InvalidOperationException>(() => w.Step());
        Assert.Equal(tick, w.WorldTick);
        Assert.All(w.OpenSectors, x => Assert.Equal(tick, w.SectorZone(x)!.WorldTick));
        Assert.Throws<InvalidOperationException>(() => w.Step());   // toujours refusé entier, jamais un secteur en avance
        Assert.All(w.OpenSectors, x => Assert.Equal(tick, w.SectorZone(x)!.WorldTick));
    }

    // --- Revue Codex de la PR #150 (10e tour) --------------------------------------------------------------------------------------------

    [Fact]
    public void Commandes_de_l_aire_decoupees_avec_leurs_acteurs()
    {
        // Une aire valide avec une commande de départ (garde) pour une souche de l'est, et une commande de l'ouest qui vise l'est : le secteur
        // ouest s'ouvre (la commande de l'est n'y est pas, celle qui vise l'autre secteur est retirée) ; l'est garde la sienne.
        var west = new DummySetup("souche_ouest", "cibles", Fixed.FromInt(50), Fixed.Zero, placement: new CombatantPlacement(P(100, 100), Fixed.FromRatio(1, 2)));
        var east = new DummySetup("souche_est", "cibles", Fixed.FromInt(50), Fixed.Zero, placement: new CombatantPlacement(P(400, 100), Fixed.FromRatio(1, 2)));
        var commands = new[]
        {
            new CombatCommand(0, "souche_est", CombatAction.GuardStart),
            new CombatCommand(0, "souche_ouest", CombatAction.GuardStart),
            new CombatCommand(50, "souche_ouest", "skill.inconnue", "souche_est"),
        };
        var area = new CombatSetup(7, 3_600_000, Shipped.Stats, Shipped.Damage, Shipped.Progression, Shipped.Combat, new CombatantSetup[] { west, east }, commands,
            Shipped.Data, new CombatSpace(P(0, 0), P(512, 256)), zone: true);
        var map = new SectorMap(Grid, area, P(64, 128));
        CombatSetup w = map.SetupFor("s00_00", 1), e = map.SetupFor("s01_00", 1);
        Assert.Equal(new[] { "souche_ouest" }, w.Commands.Select(c => c.Actor));
        Assert.Null(w.Commands.Single().Target);
        Assert.Equal(new[] { "souche_est" }, e.Commands.Select(c => c.Actor));
        Assert.Empty(w.Validate());
        Assert.Empty(e.Validate());
        SectorWorld world = World(map);
        world.Enter(Hero("alice", 100, 128));
        world.Step();
        Assert.Contains("s00_00", world.OpenSectors);
    }

    [Fact]
    public void Identifiant_dont_la_cle_de_mort_serait_trop_longue_refuse_a_l_entree()
    {
        SectorWorld w = World(Map());
        Assert.Throws<ArgumentException>(() => w.Enter(Hero(new string('a', 190), 100, 128)));
        Assert.Throws<ArgumentException>(() => w.Enter(Hero(new string('a', 150), 100, 128)));
        w.Step();
        Assert.Empty(w.Players);
        // Un identifiant long mais admis passe d'un secteur à l'autre (identité <id>_p1) sans refus.
        string id = new string('a', 120);
        w.Enter(Hero(id, 250, 128));
        w.Step();
        Walk(w, id, 1, 0);
        Run(w, 300, x => x.Kind == SectorEventKind.Arrived);
        w.Step();
        Assert.Equal("s01_00", w.SectorOf(id));
        Assert.Null(w.RefusalOf(id));
    }

    [Fact]
    public void Joueur_refuse_pour_de_bon_pas_renouvele_le_monde_continue()
    {
        // Un joueur refusé pour de bon (sans combattant) reste suivi avec son diagnostic ; le renouvellement de la session l'ignore (pas de vue
        // d'un combattant vide) ; les autres joueurs passent dans la session nouvelle.
        SectorWorld w = World(ShortMap(5_000, Shipped.Combat));
        w.Enter(Hero("alice", 100, 128));
        w.Enter(Hero("bruno", 120, 128));
        w.Step();
        w.RefuseForTest("alice", "diagnostic d'essai");
        List<SectorEvent> events = Run(w, 300);
        Assert.Contains(events, x => x.Kind == SectorEventKind.Renewed && x.Sector == "s00_00");
        Assert.Equal("diagnostic d'essai", w.RefusalOf("alice"));
        Assert.True(w.ViewOf("bruno").IsPresent);
        Assert.Throws<InvalidOperationException>(() => w.CombatantOf("alice"));
        w.Exit("alice");
        w.Step();
        Assert.Equal(new[] { "bruno" }, w.Players);
    }
}
