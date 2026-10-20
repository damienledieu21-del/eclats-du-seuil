using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4a : déplacement dans un combat positionné. Vitesse de base au tick près (5 m/s, 0,25 m par tick de 50 ms), garde à la moitié,
/// ralentissement des altérations, bords de l'aire et décor solides, glissement le long d'un obstacle, rien ne traverse un mur mince.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aMovementTests
{
    private static GroundVector Raw(long x, long z) => GroundVector.FromRaw(x, z);

    private static CombatSetup Walk(IEnumerable<CombatCommand> commands, CombatRules? rules = null, CombatSpace? space = null, params CombatantSetup[] others) =>
        Spatial(space ?? Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), PlacedDummy("mannequin", At("0", "8")) }.Concat(others), commands, rules);

    [Fact]
    public void Combat_sans_espace_inchange_aucune_ligne_de_deplacement()
    {
        var (_, log) = Run(Setup(new[] { Cmd(0, Frappe), GuardStart(1000) }));
        Assert.Empty(Lines(log, "moved"));
        Assert.DoesNotContain("\"position\"", string.Join("\n", log.Lines));
        Assert.DoesNotContain("\"space\"", log.Lines[0]);
        Assert.DoesNotContain("movement", string.Join("\n", log.Lines));
    }

    [Fact]
    public void Marche_a_la_vitesse_de_base_au_tick_pres_puis_s_arrete()
    {
        var (track, log, _) = Trace(Walk(new[] { Move(0, "bastion", "1", "0"), Stop(1000, "bastion") }));
        // 5 m/s × 50 ms = 0,25 m par tick, dès le tick de la commande (phase movement après les commandes) ; arrêt au tick 20.
        for (int t = 0; t <= 30; t++) Assert.Equal(Raw(2500L * Math.Min(t, 20), 0), track.After("bastion", t));
        var started = Assert.Single(Lines(log, "moveStarted"));
        Assert.Equal((0L, "bastion", 0L), (started.Tick(), started.Str("actor"), started.Int("command")));
        Assert.Equal("[1,0]", started.GetProperty("direction").GetRawText());
        var stopped = Assert.Single(Lines(log, "moveStopped"));
        Assert.Equal((20L, "bastion", 1L), (stopped.Tick(), stopped.Str("actor"), stopped.Int("command")));
        var moved = Lines(log, "moved");
        Assert.Equal(Enumerable.Range(0, 20).Select(t => (long)t), moved.Select(m => m.Tick()));
        Assert.All(moved, m => Assert.Equal("movement", m.Str("phase")));
        Assert.All(moved, m => Assert.False(m.TryGetProperty("eid", out _)));   // ligne d'état, sans provenance
        JsonElement last = Assert.Single(moved[^1].GetProperty("combatants").EnumerateArray());
        Assert.Equal(("bastion", "[5,0]", "[1,0]"), (last.Str("combatant"), last.GetProperty("position").GetRawText(), last.GetProperty("facing").GetRawText()));
    }

    [Fact]
    public void Ligne_combatant_et_en_tete_portent_la_place_de_depart()
    {
        CombatSetup setup = Spatial(Arena(5, Rect("rocher", "1", "1", "2", "2")),
            new CombatantSetup[] { Player("bastion", At("-1", "0.5", facing: V("0.6", "0.8"))), Creature("loup", At("3", "-2", "0.6", speed: "6.5")) },
            new[] { Move(0, "loup", "-1", "0"), Stop(500, "loup") });
        var (_, log) = Run(setup);
        var combatants = Lines(log, "combatant").ToDictionary(c => c.Str("combatant")!);
        Assert.Equal(("[-1,0.5]", "[0.6,0.8]", "0.5", "5"), (combatants["bastion"].GetProperty("position").GetRawText(),
            combatants["bastion"].GetProperty("facing").GetRawText(), combatants["bastion"].GetProperty("radius").GetRawText(),
            combatants["bastion"].GetProperty("speed").GetRawText()));
        Assert.Equal(("[3,-2]", "[0,1]", "0.6", "6.5"), (combatants["loup"].GetProperty("position").GetRawText(),
            combatants["loup"].GetProperty("facing").GetRawText(), combatants["loup"].GetProperty("radius").GetRawText(),
            combatants["loup"].GetProperty("speed").GetRawText()));
        JsonElement scenario = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario");
        Assert.Equal("{\"bounds\":{\"min\":[-5,-5],\"max\":[5,5]},\"obstacles\":[{\"id\":\"rocher\",\"points\":[[1,1],[2,1],[2,2],[1,2]]}]}",
            scenario.GetProperty("space").GetRawText());
        JsonElement[] described = scenario.GetProperty("combatants").EnumerateArray().ToArray();
        Assert.Equal("[0.6,0.8]", described[0].GetProperty("facing").GetRawText());
        Assert.False(described[0].TryGetProperty("speed", out _));   // vitesse propre : assaillant seulement, écrite si donnée
        Assert.Equal("6.5", described[1].GetProperty("speed").GetRawText());
        Assert.Equal("[{\"atMs\":0,\"actor\":\"loup\",\"action\":\"move\",\"direction\":[-1,0]},{\"atMs\":500,\"actor\":\"loup\",\"action\":\"stop\"}]",
            scenario.GetProperty("commands").GetRawText());
        Assert.Equal(CombatEngine.LogFormatVersion, JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    public void Vitesse_propre_d_un_assaillant()
    {
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("-5", "5", "0.6", speed: "8")) },
            new[] { Move(0, "loup", "1", "0") });
        var (track, _, _) = Trace(setup);
        Assert.Equal(Raw(-50_000 + 4000 * 5, 50_000), track.After("loup", 5));   // 8 m/s : 0,4 m par tick
    }

    [Fact]
    public void Direction_normalisee_et_orientation()
    {
        var (track, log, engine) = Trace(Walk(new[] { Move(0, "bastion", "1", "1") }, durationMs: 1000));
        var started = Assert.Single(Lines(log, "moveStarted"));
        Assert.Equal("[0.7071,0.7071]", started.GetProperty("direction").GetRawText());
        GroundVector p = track.After("bastion", 4);
        long distance = IntMath.Sqrt(DistanceSquaredRaw(p, Raw(0, 0)));
        Assert.InRange(distance, 10_000 - 5, 10_000 + 5);   // 4 ticks × 0,25 m = 1 m, à 0,5 mm près
        Assert.Equal(p.X, p.Z);
        Assert.Equal(Raw(7071, 7071), engine.View("bastion").Facing);
        Assert.Equal(Raw(7071, 7071), engine.View("bastion").MoveDirection);
    }

    private static CombatSetup Walk(IEnumerable<CombatCommand> commands, long durationMs) =>
        Spatial(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), PlacedDummy("mannequin", At("0", "8")) }, commands, durationMs: durationMs);

    [Fact]
    public void Orientation_par_defaut_vers_le_nord_et_gardee_a_l_arret()
    {
        var (_, _, engine) = Trace(Walk(new[] { Move(0, "bastion", "-1", "0"), Stop(200, "bastion") }));
        CombatantView view = engine.View("bastion");
        Assert.Equal(Raw(-10_000, 0), view.Facing);   // la dernière direction de marche
        Assert.Null(view.MoveDirection);
        Assert.Equal(Raw(0, 10_000), engine.View("mannequin").Facing);   // jamais bougé : nord par défaut
        Assert.Equal(Fixed.Parse("0.5"), view.Radius);
    }

    [Fact]
    public void Garde_a_la_moitie_de_la_vitesse()
    {
        var (track, _, _) = Trace(Walk(new[] { Move(0, "bastion", "1", "0"), GuardStart(0), GuardStop(500) }));
        Assert.Equal(Raw(1250 * 10, 0), track.After("bastion", 10));   // 0,125 m par tick en garde
        Assert.Equal(Raw(1250 * 10 + 2500 * 4, 0), track.After("bastion", 14));
    }

    [Fact]
    public void Garde_a_vitesse_nulle_immobile_et_orientation_inchangee()
    {
        var rules = new CombatRules { GuardSpeedFactor = Fixed.Zero };
        var (track, log, engine) = Trace(Walk(new[] { GuardStart(0), Move(0, "bastion", "1", "0") }, rules));
        Assert.All(track["bastion"], p => Assert.Equal(Raw(0, 0), p));
        Assert.Empty(Lines(log, "moved"));
        Assert.Equal(Raw(0, 10_000), engine.View("bastion").Facing);
    }

    [Fact]
    public void Gel_ralentit_de_30_pour_cent_sans_toucher_au_reste()
    {
        // L'assaillant gèle le Bastion au tick 20 (phase de résolution, après le déplacement du tick) : 0,175 m par tick ensuite.
        AssailantSetup gel = Creature("givre", At("-8", "-8"), firstAttackMs: 1000, attacks: new[] { Strike("gel", 1, DamageType.Physique, false, Apply("freeze")) });
        var (track, log, _) = Trace(Walk(new[] { Move(0, "bastion", "1", "0") }, null, null, gel));
        Assert.Equal(Raw(2500 * 21, 0), track.After("bastion", 21));
        Assert.Equal(Raw(2500 * 21 + 1750, 0), track.After("bastion", 22));
        Assert.Equal(Raw(2500 * 21 + 1750 * 5, 0), track.After("bastion", 26));
        Assert.Contains(Lines(log, "stateApplied"), s => s.Str("state") == "freeze" && s.Tick() == 20);
    }

    [Fact]
    public void Bord_de_l_aire_arrete_exactement_a_l_emprise()
    {
        var (track, _, _) = Trace(Walk(new[] { Move(0, "bastion", "1", "0") }));
        Assert.Equal(Raw(95_000, 0), track.After("bastion", 38));   // 10 m − 0,5 m de rayon
        Assert.All(track["bastion"].Skip(38), p => Assert.Equal(Raw(95_000, 0), p));
    }

    [Fact]
    public void Obstacle_arrete_puis_fait_glisser_sans_jamais_chevaucher()
    {
        SpaceObstacle rock = Rect("rocher", "1", "-1", "3", "1");
        CombatSetup setup = Spatial(Arena(10, rock), new CombatantSetup[] { Player("bastion", At("-2", "0")), PlacedDummy("mannequin", At("0", "8")) },
            new[] { Move(0, "bastion", "1", "0"), Move(2000, "bastion", "1", "1") });
        var (track, _, _) = Trace(setup);
        // Face ouest du rocher en x = 1 : le bord de l'emprise la touche (x = 0,5) ; poussé contre la face, le Bastion ne bouge plus (le
        // dégagement de 0,3 mm ne vaut pas un pas : pas de tremblement).
        Assert.Equal(Raw(5000, 0), track.After("bastion", 10));
        Assert.All(track["bastion"].Skip(11).Take(30), p => Assert.Equal(Raw(5000, 0), p));
        // En diagonale : glisse vers le nord le long de la face, x inchangé, puis contourne le coin et repart au nord-est.
        Assert.All(Enumerable.Range(41, 5), t =>
        {
            Assert.Equal(4997, track.After("bastion", t).X.Raw);
            Assert.True(track.After("bastion", t).Z > track.After("bastion", t - 1).Z);
        });
        GroundVector end = track["bastion"][^1];
        Assert.True(end.Z > Fixed.FromInt(2) && end.X > Fixed.FromInt(2), end.ToString());
        var polygon = new ConvexPolygon(rock.Points);
        Assert.All(track["bastion"], p => Assert.False(polygon.OverlapsCircle(p.X.Raw, p.Z.Raw, 5000), p.ToString()));
    }

    [Fact]
    public void Mur_mince_jamais_traverse_meme_a_vitesse_maximale()
    {
        // Mur de 1 cm, créature de 0,1 m à 20 m/s (1 m par tick) : sous-pas d'au plus la moitié du rayon, le mur arrête toujours.
        SpaceObstacle wall = Rect("mur", "0", "-5", "0.01", "5");
        CombatSetup setup = Spatial(Arena(10, wall),
            new CombatantSetup[] { Player("bastion", At("5", "0")), Creature("fleche", At("-3", "0", "0.1", speed: "20")) },
            new[] { Move(0, "fleche", "1", "0") });
        var (track, _, _) = Trace(setup);
        Assert.All(track["fleche"], p => Assert.True(p.X <= Fixed.Parse("-0.1"), p.ToString()));
        Assert.Equal(Raw(-1003, 0), track["fleche"][^1]);
    }

    [Fact]
    public void Arret_refuse_a_l_arret_deplacement_refuse_au_mannequin_et_au_mort()
    {
        CharacterSetup killer = Player("bastion", At("0", "0"), Attrs(force: 500, vitalite: 200));
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[] { killer, PlacedDummy("mannequin", At("0", "8")), Creature("loup", At("4", "0"), health: 100) },
            new[] { Stop(0, "bastion"), Move(0, "mannequin", "1", "0"), Cmd(0, Frappe, target: "loup"), Move(100, "loup", "1", "0") });
        var (result, log) = Run(setup);
        var rejected = Lines(log, "commandRejected").Select(r => (r.Str("actor"), r.Str("action"), r.Str("reason"))).ToList();
        Assert.Equal(new (string?, string?, string?)[] { ("bastion", "stop", "notMoving"), ("mannequin", "move", "actorCannotAct"), ("loup", "move", "actorDead") }, rejected);
        Assert.True(result["loup"].IsDead);
    }

    [Fact]
    public void Plusieurs_marcheurs_une_ligne_par_tick_dans_l_ordre_des_identifiants()
    {
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[] { Player("zoe", At("-5", "0")), Player("alice", At("-5", "3")), PlacedDummy("mannequin", At("5", "8")) },
            new[] { Move(0, "zoe", "1", "0"), Move(0, "alice", "1", "0") });
        var (_, log) = Run(setup);
        JsonElement first = Lines(log, "moved")[0];
        Assert.Equal(new[] { "alice", "zoe" }, first.GetProperty("combatants").EnumerateArray().Select(c => c.Str("combatant")));
    }

    [Fact]
    public void Meme_combat_positionne_meme_journal_octet_par_octet()
    {
        CombatSetup setup = Spatial(Arena(10, Rect("rocher", "1", "-1", "3", "1")),
            new CombatantSetup[] { Player("bastion", At("-2", "0")), Creature("loup", At("6", "0.3", "0.6")) },
            new[] { Move(0, "bastion", "1", "0.2"), Move(0, "loup", "-1", "0"), GuardStart(600), Move(1500, "bastion", "0", "1") });
        string first = Run(setup).Log.ToString();
        Assert.Equal(first, Run(setup).Log.ToString());
        Assert.Contains("\"event\":\"moved\"", first);
    }
}
