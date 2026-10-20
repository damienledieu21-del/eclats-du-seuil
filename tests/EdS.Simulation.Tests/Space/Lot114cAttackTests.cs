using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4c : attaques annoncées au sol et garde de face (COMBAT_ESPACE.md, sections 5 et 6). Forme verrouillée au tour de
/// l'assaillant, impact au tick près, frontière exacte ; coup dirigé verrouillé ; zone persistante ; défenses déclarées par attaque ;
/// arc frontal du bouclier ; garde tournée vers la cible sélectionnée.
/// </summary>
[Trait("Category", "Space")]
public class Lot114cAttackTests
{
    /// <summary>Sans entretien de garde ni réaction minimale : les annonces de test n'ont que le temps de sortir de leur forme.</summary>
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 };

    private static Fixed F(string v) => Fixed.Parse(v);

    private static AssailantAttack Form(AttackShape shape, long telegraphMs = 0, string? range = null, string? radius = null, int? angle = null,
        string? width = null, ShapeCenter center = ShapeCenter.Target, long persistentMs = 0, long pulseMs = 0, bool? guardable = null,
        bool? parryable = null, long raw = 100, bool unblockable = false, string id = "souffle") =>
        new(id, raw, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, unblockable, null, shape, telegraphMs,
            range is null ? null : F(range), radius is null ? null : F(radius), angle, width is null ? null : F(width), center, persistentMs, pulseMs,
            guardable, parryable);

    /// <summary>Assaillant qui attaque à <paramref name="firstMs"/> puis toutes les 10 s.</summary>
    private static AssailantSetup Beast(string id, CombatantPlacement at, AssailantAttack attack, long firstMs = 1000, string target = "bastion", long? health = null) =>
        new(id, "cibles", Fixed.Zero, Fixed.Zero, target, firstMs, 10_000, new[] { attack }, health: health, placement: at);

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatRules? rules = null, long durationMs = 4000) =>
        Spatial(Arena(20), combatants, commands, rules ?? Rules, durationMs);

    private static IEnumerable<JsonElement> DamageTo(MemoryCombatLog log, string target) => Lines(log, "damage").Where(d => d.Str("target") == target);

    private static string[] Targets(JsonElement landed) => landed.GetProperty("targets").EnumerateArray().Select(t => t.GetString()!).ToArray();

    // --- Coup dirigé ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("4", true)]          // bords à 2,5 m pile (rayons 0,5 + 1)
    [InlineData("4.0001", false)]
    public void Coup_dirige_seulement_a_portee(string x, bool lands)
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Beast("ours", At(x, "0", "1"), Form(AttackShape.Target, range: "2.5")) },
            Array.Empty<CombatCommand>()));
        if (lands) Assert.Single(DamageTo(log, "bastion"));
        else
        {
            Assert.Empty(DamageTo(log, "bastion"));
            JsonElement skipped = Assert.Single(Lines(log, "attackSkipped"));
            Assert.Equal(("targetOutOfRange", 20L), (skipped.Str("reason"), skipped.Tick()));
        }
    }

    [Fact]
    public void Coup_dirige_sans_ligne_de_vue_saute()
    {
        var (_, log) = Run(Spatial(Arena(20, Rect("mur", "2", "-2", "3", "2")),
            new CombatantSetup[] { Player("bastion", At("0", "0")), Beast("ours", At("5", "0"), Form(AttackShape.Target)) }, Array.Empty<CombatCommand>(), Rules, 4000));
        Assert.Equal("noLineOfSight", Assert.Single(Lines(log, "attackSkipped")).Str("reason"));
        Assert.Empty(DamageTo(log, "bastion"));
    }

    [Fact]
    public void Coup_dirige_annonce_frappe_la_cible_verrouillee_au_tick_d_impact()
    {
        // Annonce de 600 ms (12 ticks) : le Bastion s'éloigne aussitôt, le coup le frappe quand même au tick 32 (cible verrouillée).
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Beast("ours", At("2", "0"), Form(AttackShape.Target, telegraphMs: 600, range: "2.5")) },
            new[] { Move(1000, "bastion", "-1", "0") }));
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        Assert.Equal((20L, "target", 32L, true, true), (started.Tick(), started.Str("shape"), started.Int("impactTick"),
            started.GetProperty("guardable").GetBoolean(), started.GetProperty("parryable").GetBoolean()));
        JsonElement landed = Assert.Single(Lines(log, "attackLanded"));
        Assert.Equal(32L, landed.Tick());
        Assert.Equal(new[] { "bastion" }, Targets(landed));
        JsonElement hit = Assert.Single(DamageTo(log, "bastion"));
        Assert.Equal((32L, landed.Int("eid")), (hit.Tick(), hit.Int("cause")));   // les dégâts sont causés par l'impact
    }

    // --- Zones : forme verrouillée, impact au tick près -----------------------------------------------------------

    [Theory]
    [InlineData(1050, true)]    // part un tick après l'annonce : 10 pas de 0,25 m, bord de l'emprise sur le cercle à l'impact
    [InlineData(1000, false)]   // part au tick de l'annonce : 11 pas, sorti
    public void Sortir_d_un_cercle_annonce_evite_le_coup_au_tick_pres(long leaveMs, bool hit)
    {
        // Cercle de 2 m centré sur le Bastion au tick 20, impact 500 ms plus tard (tick 30, après les déplacements du tick).
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Beast("ours", At("8", "0"), Form(AttackShape.Circle, telegraphMs: 500, radius: "2")) },
            new[] { Move(leaveMs, "bastion", "-1", "0") }));
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        Assert.Equal(("circle", "[0,0]", "2", 30L), (started.Str("shape"), started.GetProperty("origin").GetRawText(), started.GetProperty("radius").GetRawText(), started.Int("impactTick")));
        JsonElement landed = Assert.Single(Lines(log, "attackLanded"));
        Assert.Equal(30L, landed.Tick());
        Assert.Equal(hit ? new[] { "bastion" } : Array.Empty<string>(), Targets(landed));
        Assert.Equal(hit ? 1 : 0, DamageTo(log, "bastion").Count());
    }

    [Fact]
    public void Cercle_autour_de_l_assaillant_et_plusieurs_cibles()
    {
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")), Player("allie", At("2.5", "0")), Player("loin", At("-3", "0")),
            Beast("ours", At("2", "2"), Form(AttackShape.Circle, telegraphMs: 1000, radius: "2.5", center: ShapeCenter.Self)),
        }, Array.Empty<CombatCommand>()));
        // Centre (2, 2) : le Bastion (2,83 m, emprise 0,5) et l'allié (2,06 m) sont touchés, l'autre non.
        Assert.Equal(new[] { "allie", "bastion" }, Targets(Assert.Single(Lines(log, "attackLanded"))));
        Assert.Empty(DamageTo(log, "loin"));
    }

    [Fact]
    public void Cone_vers_la_cible_frontiere_exacte()
    {
        // Ours en (0, 0) visant le Bastion en (3, 0) : axe (1, 0), ouverture 90°, longueur 4.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("3", "0")),
            Player("bord", At("2", "2")),          // sur le côté gauche (45°) : dedans
            Player("loin", At("4.5", "0")),        // centre à 4,5 : emprise qui touche l'arc (4 + 0,5)
            Player("dehors", At("4.5001", "0")),   // 0,1 mm au-delà
            Player("derriere", At("-2", "0")),
            Player("cote", At("0", "3")),          // à 90° de l'axe, hors de l'ouverture
            Beast("ours", At("0", "0"), Form(AttackShape.Cone, telegraphMs: 1000, range: "4", angle: 90)),
        }, Array.Empty<CombatCommand>()));
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        Assert.Equal(("cone", "[1,0]", 90L), (started.Str("shape"), started.GetProperty("axis").GetRawText(), started.Int("angle")));
        Assert.Equal(new[] { "bastion", "bord", "loin" }, Targets(Assert.Single(Lines(log, "attackLanded"))));
    }

    [Fact]
    public void Cone_qui_effleure_une_emprise_par_son_cote()
    {
        // Centre hors de l'ouverture, mais emprise qui touche le côté du cône : touché.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("3", "0")), Player("frole", At("1", "1.5", "0.8")),
            Beast("ours", At("0", "0"), Form(AttackShape.Cone, telegraphMs: 1000, range: "4", angle: 60)),
        }, Array.Empty<CombatCommand>()));
        Assert.Contains("frole", Targets(Assert.Single(Lines(log, "attackLanded"))));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("1.0001", false)]
    public void Bande_de_largeur_donnee(string offset, bool hit)
    {
        // Bande de 6 m sur 1 m depuis l'ours vers le Bastion ; un allié à côté : touché si son emprise atteint la demi-largeur.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("5", "0")), Player("allie", At("3", offset)),
            Beast("ours", At("0", "0"), Form(AttackShape.Line, telegraphMs: 1000, range: "6", width: "1")),
        }, Array.Empty<CombatCommand>()));
        Assert.Equal(hit, Targets(Assert.Single(Lines(log, "attackLanded"))).Contains("allie"));
    }

    [Fact]
    public void Zone_persistante_frappe_a_chaque_impulsion_ce_qui_reste_dedans()
    {
        // Rayon 1,5 sur le Bastion ; 2 s, une impulsion toutes les 500 ms : impacts aux ticks 40, 50, 60, 70. L'allié sort après la
        // deuxième ; le Bastion reste, en garde : la garde n'y fait rien.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")), Player("allie", At("1", "0")),
            Beast("ours", At("8", "0"), Form(AttackShape.Persistent, telegraphMs: 1000, radius: "1.5", persistentMs: 2000, pulseMs: 500)),
        }, new[] { GuardStart(0, "bastion"), Move(2550, "allie", "1", "0") }, durationMs: 5000));
        var landed = Lines(log, "attackLanded");
        Assert.Equal(new[] { 40L, 50L, 60L, 70L }, landed.Select(l => l.Tick()));
        Assert.Equal(new[] { 0L, 1, 2, 3 }, landed.Select(l => l.Int("pulse")));
        Assert.Equal(new[] { 2, 2, 1, 1 }, landed.Select(l => Targets(l).Length));
        Assert.All(DamageTo(log, "bastion"), d => Assert.Equal("unblockable", d.Str("defense")));   // en garde, mais une zone qui reste ne se garde pas
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        Assert.Equal((4L, 10L, false, false), (started.Int("pulses"), started.Int("pulseTicks"), started.GetProperty("guardable").GetBoolean(), started.GetProperty("parryable").GetBoolean()));
    }

    // --- Défenses déclarées et arc du bouclier ----------------------------------------------------------------------

    [Fact]
    public void Zone_ni_paree_ni_gardee_par_defaut_gardee_si_elle_le_dit()
    {
        CombatSetup Setup(bool? guardable, CombatCommand defense) => Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0", facing: V("1", "0"))),
            Beast("ours", At("3", "0"), Form(AttackShape.Circle, telegraphMs: 1000, radius: "1", guardable: guardable)),
        }, new[] { defense });
        var (_, parried) = Run(Setup(null, Parry(2000, "bastion")));
        Assert.Equal("unblockable", Assert.Single(DamageTo(parried, "bastion")).Str("defense"));
        Assert.Empty(Lines(parried, "parrySucceeded"));
        var (_, guarded) = Run(Setup(null, GuardStart(0, "bastion")));
        Assert.Equal("unblockable", Assert.Single(DamageTo(guarded, "bastion")).Str("defense"));
        var (_, guardable) = Run(Setup(true, GuardStart(0, "bastion")));
        Assert.Equal("guard", Assert.Single(DamageTo(guardable, "bastion")).Str("defense"));   // centre du cercle sur le Bastion : de face
    }

    [Theory]
    [InlineData("1", "0", "guard")]       // de face
    [InlineData("0", "1", "guard")]       // à 90° pile : arc de 180° frontière comprise
    [InlineData("-1", "0.0001", "behind")]
    [InlineData("-1", "0", "behind")]     // de dos : dégâts pleins
    public void Garde_seulement_contre_un_coup_venu_de_face(string fx, string fz, string defense)
    {
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0", facing: V(fx, fz))),
            Beast("ours", At("2", "0"), Form(AttackShape.Target, range: "2.5")),
        }, new[] { GuardStart(0, "bastion") }));
        JsonElement hit = Assert.Single(DamageTo(log, "bastion"));
        Assert.Equal(defense, hit.Str("defense"));
        Assert.Equal(defense == "guard" ? 1 : 0, Lines(log, "blocked").Count);
    }

    [Fact]
    public void Parade_de_dos_ratee_et_attaque_historique_de_dos()
    {
        // Le Bastion regarde au nord, l'assaillant (attaque historique, sans forme) est au sud : la parade ne vaut pas.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            new AssailantSetup("loup", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 10_000, new[] { Blow() }, placement: At("0", "-3")),
        }, new[] { Parry(1000, "bastion") }));
        Assert.Equal("behind", Assert.Single(DamageTo(log, "bastion")).Str("defense"));
        Assert.Empty(Lines(log, "parrySucceeded"));
    }

    [Fact]
    public void Arc_du_bouclier_reglable()
    {
        CombatSetup Setup(int arc) => Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0", facing: V("1", "0"))),
            Beast("ours", At("1", "1"), Form(AttackShape.Target)),   // à 45° de l'orientation
        }, new[] { GuardStart(0, "bastion") }, new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, GuardArcDegrees = arc });
        Assert.Equal("guard", Assert.Single(DamageTo(Run(Setup(90)).Log, "bastion")).Str("defense"));     // 45° pile : dedans
        Assert.Equal("behind", Assert.Single(DamageTo(Run(Setup(89)).Log, "bastion")).Str("defense"));
        Assert.Equal("guard", Assert.Single(DamageTo(Run(Setup(360)).Log, "bastion")).Str("defense"));
    }

    // --- Garde orientée vers la cible sélectionnée ------------------------------------------------------------------

    [Fact]
    public void En_garde_le_Bastion_se_tourne_vers_sa_cible_selectionnee()
    {
        // L'ours passe derrière le Bastion : sélectionné et en garde, le Bastion se tourne et garde de face ; sans sélection, il est frappé de dos.
        // Il contourne le Bastion par le nord : (2, 0) → (2, 2) → (−2, 2) → (−2, 0), puis frappe à 2 s.
        CombatantSetup[] Fighters() => new CombatantSetup[]
        {
            Player("bastion", At("0", "0", facing: V("1", "0"))),
            Beast("ours", At("2", "0"), Form(AttackShape.Target, range: "2.5"), firstMs: 2000),
        };
        CombatCommand[] Walk = { Move(0, "ours", "0", "1"), Move(400, "ours", "-1", "0"), Move(1200, "ours", "0", "-1"), Stop(1600, "ours"), GuardStart(0, "bastion") };
        var (track, selected, _) = Trace(Fight(Fighters(), Walk.Append(CombatCommand.SelectTarget(0, "bastion", "ours"))));
        Assert.Equal(V("-2", "0"), track.After("ours", 40));
        Assert.Equal("guard", Assert.Single(DamageTo(selected, "bastion")).Str("defense"));
        JsonElement sel = Assert.Single(Lines(selected, "targetSelected"));
        Assert.Equal(("bastion", "ours", 0L), (sel.Str("actor"), sel.Str("target"), sel.Tick()));
        var (_, unselected) = Run(Fight(Fighters(), Walk));
        Assert.Equal("behind", Assert.Single(DamageTo(unselected, "bastion")).Str("defense"));
    }

    [Fact]
    public void Sans_garde_la_selection_ne_tourne_pas_et_le_deplacement_oriente()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0", facing: V("1", "0"))), PlacedDummy("mannequin", At("0", "5")) },
            new[] { CombatCommand.SelectTarget(0, "bastion", "mannequin") }, durationMs: 1000));
        Assert.Empty(Lines(log, "moved"));   // pas de garde : orientation inchangée
        var (_, guarded) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0", facing: V("1", "0"))), PlacedDummy("mannequin", At("0", "5")) },
            new[] { CombatCommand.SelectTarget(0, "bastion", "mannequin"), GuardStart(0, "bastion"), CombatCommand.SelectTarget(500, "bastion", null) }, durationMs: 1000));
        JsonElement turned = Assert.Single(Lines(guarded, "moved"));
        Assert.Equal((0L, "[0,1]"), (turned.Tick(), turned.GetProperty("combatants")[0].GetProperty("facing").GetRawText()));
        Assert.Null(Lines(guarded, "targetSelected")[1].Str("target"));
    }

    [Fact]
    public void Selection_refusee_d_un_mort_ou_d_un_mannequin()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), PlacedDummy("mannequin", At("0", "5")) },
            new[] { CombatCommand.SelectTarget(0, "mannequin", "bastion") }, durationMs: 500));
        Assert.Equal("actorCannotAct", Assert.Single(Lines(log, "commandRejected")).Str("reason"));
    }

    // --- Annulations et vue du client ------------------------------------------------------------------------------

    [Fact]
    public void Mort_de_l_assaillant_pendant_l_annonce_annule_l_attaque()
    {
        var weak = Beast("ours", At("2", "0"), Form(AttackShape.Circle, telegraphMs: 1500, radius: "1"), health: 1);
        // Un témoin immortel garde le combat ouvert après la mort de l'ours.
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), weak, PlacedDummy("temoin", At("0", "8")) },
            new[] { Cmd(1500, Gratuite, target: "ours") }));
        Assert.Single(Lines(log, "died"));
        JsonElement cancelled = Assert.Single(Lines(log, "attackCancelled"));
        Assert.Equal((50L, "actorDead"), (cancelled.Tick(), cancelled.Str("reason")));
        Assert.Empty(DamageTo(log, "bastion"));
        Assert.Empty(Lines(log, "attackLanded"));
    }

    [Fact]
    public void Attaques_annoncees_visibles_jusqu_a_leur_fin()
    {
        var engine = new CombatEngine(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            Beast("ours", At("8", "0"), Form(AttackShape.Persistent, telegraphMs: 1000, radius: "1.5", persistentMs: 1000, pulseMs: 500)),
        }, Array.Empty<CombatCommand>()), new MemoryCombatLog());
        var seen = new List<(long Tick, int Count)>();
        while (engine.Step()) seen.Add((engine.CurrentTick, engine.Announced().Count));
        Assert.All(seen.Where(s => s.Tick <= 20), s => Assert.Equal(0, s.Count));
        Assert.All(seen.Where(s => s.Tick > 20 && s.Tick <= 50), s => Assert.Equal(1, s.Count));   // annonce, puis zone jusqu'à sa dernière impulsion
        Assert.All(seen.Where(s => s.Tick > 50), s => Assert.Equal(0, s.Count));
    }

    [Fact]
    public void Impact_au_dela_de_la_fin_du_combat_jamais_programme()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Beast("ours", At("2", "0"), Form(AttackShape.Circle, telegraphMs: 3000, radius: "1"), firstMs: 2000) },
            Array.Empty<CombatCommand>(), durationMs: 4000));
        Assert.Single(Lines(log, "attackStarted"));
        Assert.Empty(Lines(log, "attackLanded"));
    }

    [Fact]
    public void Combat_sans_espace_attaques_historiques_inchangees()
    {
        var (_, log) = Run(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { Character(), Assailant() }, durationMs: 3000));
        Assert.Empty(Lines(log, "attackLanded"));
        Assert.All(Lines(log, "attackStarted"), s => Assert.False(s.TryGetProperty("shape", out _)));
        Assert.All(Lines(log, "damage"), d => Assert.Equal("guard", d.Str("defense")));
    }
}
