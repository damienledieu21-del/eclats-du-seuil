using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Behavior;

/// <summary>
/// Sous-lot 11.5 : comportements des ennemis dans l'espace (approcher, poursuivre, garder une distance, encercler, s'engager, charger) et
/// choix de cible déterministe.
/// </summary>
[Trait("Category", "Behavior")]
public class Lot115BehaviorTests
{
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 };

    private static Fixed F(string v) => Fixed.Parse(v);

    private static AssailantAttack Bite(string range = "1", long raw = 10) =>
        new("morsure", raw, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Target, range: F(range));

    /// <summary>Ennemi à comportement (sans cible de scénario : il prend le plus proche).</summary>
    internal static AssailantSetup Hunter(string id, CombatantPlacement at, string maxRange, string? minRange = null, string? aggro = null, bool encircle = false,
        string? target = null, long firstMs = 600_000, long intervalMs = 1000, long? health = null, params AssailantAttack[] attacks) =>
        new(id, "meute", Fixed.Zero, Fixed.Zero, target, firstMs, intervalMs, attacks.Length == 0 ? new[] { Bite() } : attacks, health: health, placement: at,
            behavior: new AssailantBehavior(F(maxRange), minRange is null ? null : F(minRange), aggro is null ? null : F(aggro), encircle));

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand>? commands = null, long durationMs = 6000,
        CombatSpace? space = null, CombatRules? rules = null) =>
        Spatial(space ?? Arena(20), combatants, commands ?? Array.Empty<CombatCommand>(), rules ?? Rules, durationMs);

    private static double Edge(GroundVector a, GroundVector b, double ra, double rb) =>
        Math.Sqrt(Math.Pow((a.X.Raw - b.X.Raw) / 1e4, 2) + Math.Pow((a.Z.Raw - b.Z.Raw) / 1e4, 2)) - ra - rb;

    [Fact]
    public void Un_loup_approche_jusqu_a_sa_distance_puis_s_arrete_face_a_sa_cible()
    {
        var (track, log, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("8", "0.5"), "0.5") }, durationMs: 3000));
        // 5 m/s : 0,25 m par tick ; bords à 7 m au départ, il s'arrête à 0,5 m (revue Codex de la PR #33 : le dernier pas s'arrête à la
        // frontière de sa bande, à 2 mm près).
        double final = Edge(track.After("loup", 59), track.After("bastion", 59), 0.5, 0.5);
        Assert.InRange(final, 0.498, 0.502);
        Assert.Equal(track.After("loup", 40), track.After("loup", 59));   // arrêté
        // Arrêté, il fait face à sa cible (orientation unitaire vers le Bastion, à 10⁻⁴ près).
        GroundVector facing = engine.View("loup").Facing!.Value, at = track.After("loup", 59), toward = track.After("bastion", 59);
        double fx = facing.X.Raw / 1e4, fz = facing.Z.Raw / 1e4, dx = (toward.X.Raw - at.X.Raw) / 1e4, dz = (toward.Z.Raw - at.Z.Raw) / 1e4;
        Assert.InRange((fx * dx + fz * dz) / Math.Sqrt(dx * dx + dz * dz), 0.9999, 1.0001);
        Assert.Empty(Lines(log, "engaged"));   // sans distance d'engagement : engagé d'emblée, sans ligne
    }

    [Fact]
    public void Bande_plus_etroite_qu_un_pas_l_ennemi_s_arrete_a_sa_frontiere_sans_osciller()
    {
        // Revue Codex de la PR #33 : à 20 m/s (1 m par tick), une distance à garder d'exactement 0,5 m (ou 3 m) : le pas s'arrête à la
        // frontière de la bande, au lieu de la dépasser et d'osciller de part et d'autre.
        var (track, _, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            Hunter("loup", At("10.3", "0", speed: "20"), "0.5", minRange: "0.5"),
            Hunter("archer", At("-1.8", "0", speed: "20"), "3", minRange: "3"),
        }, durationMs: 2000));
        for (int t = 12; t < 40; t++)
        {
            Assert.Equal(track.After("loup", 11), track.After("loup", t));
            Assert.Equal(track.After("archer", 11), track.After("archer", t));
        }
        Assert.InRange(Edge(track.After("loup", 11), track.After("bastion", 11), 0.5, 0.5), 0.4997, 0.5003);
        Assert.InRange(Edge(track.After("archer", 11), track.After("bastion", 11), 0.5, 0.5), 2.9997, 3.0003);
    }

    [Fact]
    public void Un_tireur_garde_ses_distances_il_recule_quand_on_approche()
    {
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("archer", At("7", "0"), "9", minRange: "6") },
            new[] { Move(0, "bastion", "1", "0") }, durationMs: 3000));
        // Il ne bouge pas tant que la distance (6 m entre les bords) est dans [6, 9], puis recule à la vitesse de base.
        Assert.Equal(track.After("archer", 0), track.After("archer", 1));
        // (jusqu'au tick 40 : au-delà, il bute contre le bord de l'aire, à x = 19,5, et le Bastion le rattrape)
        for (int t = 2; t <= 40; t++)
            Assert.True(Edge(track.After("archer", t), track.After("bastion", t), 0.5, 0.5) >= 5.75, $"tick {t}");
        Assert.True(track.After("archer", 40).X > track.After("archer", 0).X);
    }

    [Fact]
    public void Engagement_par_distance_puis_toute_la_meute()
    {
        // Le Bastion avance vers trois loups ; le premier s'engage à 4 m (aggro), les deux autres, à 3 m de lui, suivent (pack).
        var (track, log, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("-6", "0")),
            Hunter("loup1", At("4", "0"), "0.5", aggro: "4", firstMs: 0), Hunter("loup2", At("6", "2"), "0.5", aggro: "3", firstMs: 0),
            Hunter("loup3", At("6", "-2"), "0.5", aggro: "3", firstMs: 0),
        }, new[] { Move(0, "bastion", "1", "0") }, durationMs: 4000));
        var engaged = Lines(log, "engaged").Select(e => (e.Tick(), e.Str("actor"), e.Str("reason"))).ToList();
        Assert.Equal(3, engaged.Count);
        Assert.Equal(("loup1", "aggro"), (engaged[0].Item2, engaged[0].Item3));
        Assert.All(engaged.Skip(1), e => Assert.Equal("pack", e.Item3));
        long first = engaged[0].Item1;
        Assert.InRange(engaged[1].Item1 - first, 0, 1);
        // Avant l'engagement : immobile, attaques sautées (notEngaged).
        Assert.Equal(track.After("loup1", 0), track.After("loup1", (int)first));
        Assert.Contains(Lines(log, "attackSkipped"), s => s.Str("reason") == "notEngaged" && s.Tick() < first);
        Assert.DoesNotContain(Lines(log, "attackSkipped"), s => s.Str("reason") == "notEngaged" && s.Tick() > first + 1);
    }

    [Theory]
    [InlineData("hit")]
    [InlineData("taunt")]
    public void Engagement_quand_on_le_frappe_ou_le_provoque(string how)
    {
        string skill = how == "hit" ? EdS.Simulation.Tests.Space.Lot114bRangeTests.Appel : EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri;
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Appel, EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri }),
            Hunter("loup", At("10", "0"), "0.5", aggro: "2"),
        }, new[] { Cmd(0, skill, target: how == "hit" ? "loup" : null) }, durationMs: 1000, rules: NoUpkeep));
        JsonElement engaged = Assert.Single(Lines(log, "engaged"));
        Assert.Equal((how, "bastion"), (engaged.Str("reason"), engaged.Str("target")));
        Assert.Equal(1L, engaged.Tick());   // relevé au début du tick suivant le coup
    }

    [Fact]
    public void Cible_de_scenario_puis_le_plus_proche_et_provocation_prioritaire()
    {
        // L'ours vise sa cible de scénario (« loin ») ; elle part : il prend le plus proche ; à distance égale, l'identifiant décide.
        var engine = new CombatEngine(Fight(new CombatantSetup[]
        {
            Player("loin", At("-9", "0")), Player("pres_b", At("3", "0")), Player("pres_a", At("0", "3")),
            Hunter("ours", At("0", "0"), "0.5", target: "loin"),
        }, new[] { new CombatCommand(0, "loin", CombatAction.Leave) }, durationMs: 2000), NullCombatLog.Instance);
        Assert.Equal("loin", engine.View("ours").CurrentTarget);
        engine.Step();
        Assert.Equal("pres_a", engine.View("ours").CurrentTarget);   // 3 m chacun : « pres_a » avant « pres_b »
    }

    [Fact]
    public void Une_meute_encercle_sa_cible_a_parts_egales()
    {
        var (track, _, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            Hunter("loup1", At("6", "0.5"), "0.5", encircle: true), Hunter("loup2", At("6", "-0.5"), "0.5", encircle: true),
            Hunter("loup3", At("7", "1"), "0.5", encircle: true), Hunter("loup4", At("7", "-1"), "0.5", encircle: true),
        }, durationMs: 6000));
        var bastion = track.After("bastion", 119);
        double[] angles = new[] { "loup1", "loup2", "loup3", "loup4" }.Select(id =>
        {
            GroundVector p = track.After(id, 119);
            return Math.Atan2((p.Z.Raw - bastion.Z.Raw) / 1e4, (p.X.Raw - bastion.X.Raw) / 1e4) * 180 / Math.PI;
        }).OrderBy(a => a).ToArray();
        for (int k = 0; k < 4; k++)
        {
            double gap = (angles[(k + 1) % 4] - angles[k] + 360) % 360;
            Assert.InRange(gap, 70, 110);   // quatre places à 90° les unes des autres
        }
        foreach (string id in new[] { "loup1", "loup2", "loup3", "loup4" })
            Assert.InRange(Edge(track.After(id, 119), bastion, 0.5, 0.5), 0, 0.75);
    }

    [Fact]
    public void Il_s_arrete_pendant_l_annonce_de_son_attaque()
    {
        AssailantAttack rain = new("pluie", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Circle, 1000, radius: F("1"));
        var (track, log, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")), Hunter("archer", At("12", "0"), "6", firstMs: 500, intervalMs: 3000, attacks: rain),
        }, durationMs: 3000));
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        long from = started.Tick(), until = started.Int("impactTick");
        Assert.Equal(10L, from);
        for (long t = from + 1; t <= until; t++) Assert.Equal(track.After("archer", (int)from + 1), track.After("archer", (int)t));
        Assert.NotEqual(track.After("archer", (int)until), track.After("archer", (int)until + 2));   // il repart après l'impact
    }

    [Fact]
    public void Charge_jusqu_au_bout_de_la_bande_ou_jusqu_au_premier_obstacle()
    {
        AssailantAttack Charge(string range) => new("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F(range), width: F("1"), charge: true);
        // Sans obstacle : la bande de 6 m depuis (0, 0) vers le Bastion en (10, 0) ; le sanglier fonce jusqu'à (6, 0).
        var (_, open) = Run(Fight(new CombatantSetup[] { Player("bastion", At("10", "0")), Hunter("sanglier", At("0", "0"), "20", firstMs: 0, attacks: Charge("6")) }, durationMs: 2000));
        JsonElement charged = Assert.Single(Lines(open, "charged"));
        Assert.Equal((20L, "[6,0]"), (charged.Tick(), charged.GetProperty("position").GetRawText()));
        // Un muret à x = 3 : il s'arrête au contact (bord à 3, rayon 0,5 : centre au plus à 2,5).
        var (_, wall) = Run(Spatial(Arena(20, Rect("muret", "3", "-2", "4", "2")), new CombatantSetup[]
            { Player("bastion", At("10", "0")), Hunter("sanglier", At("0", "0"), "20", firstMs: 0, attacks: Charge("6")) }, Array.Empty<CombatCommand>(), Rules, 2000));
        double x = JsonDocument.Parse(Assert.Single(Lines(wall, "charged")).GetProperty("position").GetRawText()).RootElement[0].GetDouble();
        Assert.InRange(x, 2.25, 2.5);
        // Le Bastion dans la bande : touché, et le sanglier s'arrête contre lui.
        var (_, hit) = Run(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Hunter("sanglier", At("0", "0"), "20", firstMs: 0, attacks: Charge("8")) }, durationMs: 2000));
        Assert.Equal(new[] { "bastion" }, Assert.Single(Lines(hit, "attackLanded")).GetProperty("targets").EnumerateArray().Select(t => t.GetString()));
        double stop = JsonDocument.Parse(Assert.Single(Lines(hit, "charged")).GetProperty("position").GetRawText()).RootElement[0].GetDouble();
        Assert.InRange(stop, 2.75, 3.0);
        // Revue Codex de la PR #33 : la charge part après les coups de la bande (ligne charged après la ligne damage du Bastion).
        List<JsonElement> lines = Events(hit).ToList();
        int damage = lines.FindIndex(e => e.Str("event") == "damage" && e.Str("target") == "bastion");
        Assert.True(damage >= 0 && damage < lines.FindIndex(e => e.Str("event") == "charged"));
    }

    [Fact]
    public void Deux_charges_annoncees_qui_se_chevauchent_vont_au_bout_de_leur_bande()
    {
        // Revue Codex de la PR #33 : annoncée à 0 et à 600 ms depuis (0, 0), chaque bande va jusqu'à (6, 0). La première charge y porte le
        // sanglier ; la seconde, déjà au bout de sa bande, ne le pousse pas plus loin (avant : jusqu'au Bastion, à x = 9).
        var charge = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"), charge: true);
        var boar = new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 0, 600, new[] { charge }, placement: At("0", "0"));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("10", "0")), boar }, durationMs: 1700));
        Assert.Equal(new[] { (20L, "[6,0]"), (32L, "[6,0]") }, Lines(log, "charged").Select(c => (c.Tick(), c.GetProperty("position").GetRawText())));
    }

    [Fact]
    public void Un_coup_sans_degats_engage_aussi()
    {
        // Revue Codex de la PR #33 : être frappé engage, même si rien n'atteint les PV (coup à coefficient nul ici ; bouclier, transfert).
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Effleure }),
            Hunter("loup", At("10", "0"), "0.5", aggro: "2"),
        }, new[] { Cmd(0, EdS.Simulation.Tests.Space.Lot114bRangeTests.Effleure, target: "loup") }, durationMs: 1000, rules: NoUpkeep));
        Assert.Equal(0L, Lines(log, "damage").Single(d => d.Str("target") == "loup").GetProperty("amount").GetInt64());
        JsonElement engaged = Assert.Single(Lines(log, "engaged"));
        Assert.Equal(("hit", 1L), (engaged.Str("reason"), engaged.Tick()));
    }

    [Fact]
    public void Instantane_d_ennemis_a_comportement_engagement_relu_et_controle()
    {
        // Le loup (sans distance d'engagement) est engagé d'emblée, le guetteur attend : l'instantané porte l'engagement, la copie relue
        // continue à l'identique ; un loup désengagé est refusé, un guetteur engagé est un autre état possible (accepté). Revue Codex de la
        // PR #33 : un renfort à comportement pas encore arrivé ne peut être ni engagé, ni déjà frappé.
        var renfort = new AssailantSetup("renfort", "meute", Fixed.Zero, Fixed.Zero, null, 600_000, 1000, new[] { Bite() }, placement: At("8", "6"),
            arrivalMs: 1500, behavior: new AssailantBehavior(F("0.5"), null, F("2"), false));
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("6", "0"), "0.5"), Hunter("guetteur", At("-8", "0"), "0.5", aggro: "2"),
            renfort }, durationMs: 2000);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 5) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup completed = engine.CompletedSetup();
        string guetteur = EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.Tampered(text, "{\"fighter\":\"guetteur\"", "\"engaged\":false", "\"engaged\":true");
        Assert.Equal(guetteur, CombatEngine.Restore(completed, CombatSnapshot.Parse(guetteur), NullCombatLog.Instance).Snapshot().Text);
        EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.AssertRefused(completed, text, new[]
        {
            ("{\"fighter\":\"loup\"", "\"engaged\":true", "\"engaged\":false", "Engagement de « loup »"),
            ("{\"fighter\":\"bastion\"", "\"engaged\":false", "\"engaged\":true", "Engagement de « bastion »"),
            ("{\"fighter\":\"renfort\"", "\"struck\":false", "\"struck\":true", "« renfort », pas encore entré"),
            ("{\"fighter\":\"renfort\"", "\"engaged\":false", "\"engaged\":true", "« renfort », pas encore entré"),
        });
        EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.AssertResumesIdentically(engine, log);
    }

    [Fact]
    public void Deplacement_pilote_d_un_ennemi_a_comportement_refuse()
    {
        // Revue Codex de la PR #33 : le pilote ne peut pas faire marcher (ni arrêter) un ennemi qui se déplace seul ; le refus ne change
        // rien et le combat complété reste valide. Un ennemi sans comportement se pilote toujours.
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("6", "0"), "0.5"),
            Creature("ours", At("-6", "0"), firstAttackMs: 600_000) }, durationMs: 1000), NullCombatLog.Instance);
        engine.Step();
        string before = engine.Snapshot().Text;
        foreach (CombatCommand command in new[] { Move(50, "loup", "1", "0"), new CombatCommand(50, "loup", CombatAction.Stop) })
        {
            Assert.Contains("se déplace seul", Assert.Throws<ArgumentException>(() => engine.Submit(command)).Message);
            Assert.Throws<ArgumentException>(() => engine.SubmitAll(new[] { Move(50, "bastion", "1", "0"), command }));
        }
        Assert.Equal(before, engine.Snapshot().Text);
        engine.Submit(Move(50, "ours", "0", "1"));
        while (!engine.IsFinished) engine.Step();
        CombatSetup completed = engine.CompletedSetup();
        Assert.Empty(completed.Validate());
        Assert.Equal(CombatAction.Move, Assert.Single(completed.Commands).Action);
    }

    [Fact]
    public void Sans_comportement_rien_ne_change()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("ours", At("5", "0"), firstAttackMs: 1000) }, durationMs: 3000));
        Assert.Empty(Lines(log, "engaged"));
        Assert.Empty(Lines(log, "charged"));
        Assert.Empty(Lines(log, "moved"));
    }
}
