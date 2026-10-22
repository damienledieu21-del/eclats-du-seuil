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
    public void Centres_confondus_le_tireur_recule_a_l_oppose_de_son_orientation()
    {
        // Revue Codex de la PR #33 (tour 5) : règle B (traversée freinée), le joueur et l'archer partent de la même place ; l'archer recule
        // à l'opposé de son orientation (vers l'ouest, il regarde l'est) jusqu'à sa distance minimale, au lieu de rester immobile.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, PlayerCreatureContact = ContactRule.SlowedCrossing };
        var (track, _, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            Hunter("archer", At("0", "0", facing: GroundVector.Parse("1", "0")), "4", minRange: "2"),
        }, durationMs: 2000, rules: rules));
        GroundVector after = track.After("archer", 39);
        Assert.True(after.X < Fixed.Zero, $"archer en {after}");
        Assert.InRange(Edge(after, track.After("bastion", 39), 0.5, 0.5), 1.998, 2.002);
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
    public void Provocation_retiree_avant_la_phase_d_engagement_engage_quand_meme()
    {
        // Revue Codex de la PR #33 (tour 6) : le Bastion provoque le loup (Cri) et part dans le même tick ; la provocation est retirée en fin
        // de tick (sourceLeft), avant la phase d'engagement du tick suivant. Le loup, provoqué, s'engage quand même (taunt) et vise l'allié.
        var (_, log) = Run(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri }),
            Player("allie", At("-10", "0")),
            Hunter("loup", At("10", "0"), "0.5", aggro: "2"),
        }, new[] { Cmd(0, EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri, target: null), new CombatCommand(0, "bastion", CombatAction.Leave) },
            durationMs: 1000, rules: NoUpkeep));
        Assert.Contains(Lines(log, "stateRemoved"), r => r.Str("combatant") == "loup" && r.Str("reason") == "sourceLeft" && r.Tick() == 0);
        JsonElement engaged = Assert.Single(Lines(log, "engaged"));
        Assert.Equal(("taunt", "allie", 1L), (engaged.Str("reason"), engaged.Str("target"), engaged.Tick()));
        // L'instantané porte la provocation reçue (taunted) ; une provocation active non mémorisée, ou reçue par un personnage, est refusée.
        var engine = new CombatEngine(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri }),
            Hunter("loup", At("10", "0"), "0.5", aggro: "2"),
        }, new[] { Cmd(0, EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri, target: null) }, durationMs: 1000, rules: NoUpkeep), NullCombatLog.Instance);
        engine.Step();
        string text = engine.Snapshot().Text;
        Assert.Contains("\"taunted\":true", text, StringComparison.Ordinal);
        Assert.Equal(text, CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.AssertRefused(engine.CompletedSetup(), text, new[]
        {
            ("{\"fighter\":\"loup\"", "\"taunted\":true", "\"taunted\":false", "Provocation active de « loup » non mémorisée"),
            ("{\"fighter\":\"bastion\"", "\"taunted\":false", "\"taunted\":true", "Provocation reçue par « bastion » impossible"),
        });
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
    public void Un_ennemi_au_centre_de_la_cible_prend_la_premiere_place_de_l_anneau()
    {
        // Revue Codex de la PR #33 (tour 6) : règle B (traversée freinée), loup2 part du centre du Bastion (vecteur nul, sans angle). Le tri
        // de l'anneau le range d'abord (ordre total, transitif) : il prend la place 0, prise sur l'axe x faute de direction, et part vers
        // l'est. Avant (comparé par identifiant aux autres, qui se comparaient par angle : ordre non transitif), il partait vers le sud-ouest.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, PlayerCreatureContact = ContactRule.SlowedCrossing };
        var (track, _, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")),
            Hunter("loup1", At("0.5", "3"), "0.5", encircle: true), Hunter("loup2", At("0", "0"), "0.5", encircle: true),
            Hunter("loup3", At("3", "0.5"), "0.5", encircle: true),
        }, durationMs: 500, rules: rules));
        Assert.Equal(GroundVector.Parse("0", "0"), track.After("loup2", 0));   // place de départ
        GroundVector first = track.After("loup2", 1);
        Assert.True(first.X > Fixed.Zero && first.Z == Fixed.Zero, $"loup2 en {first}");
    }

    [Fact]
    public void Deux_equipes_qui_encerclent_la_meme_cible_ont_chacune_leur_anneau()
    {
        // Revue Codex de la PR #33 (tour 7) : trois équipes ; le loup de la meute (est) et celui des rivaux (nord) encerclent le Bastion.
        // Chacun est seul dans l'anneau de son équipe : il prend la place 0, dans sa propre direction, et descend tout droit (x = 0) ; dans
        // un anneau partagé, le rival aurait pris la place opposée au loup (vers l'ouest).
        var rival = new AssailantSetup("rival", "rivaux", Fixed.Zero, Fixed.Zero, null, 600_000, 1000, new[] { Bite() }, placement: At("0", "6"),
            behavior: new AssailantBehavior(F("0.5"), null, null, true));
        var (track, _, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("6", "0"), "0.5", encircle: true), rival },
            durationMs: 1000));
        Assert.Equal(("bastion", "bastion"), (engine.View("loup").CurrentTarget, engine.View("rival").CurrentTarget));
        for (int t = 1; t <= 10; t++)
        {
            Assert.Equal(Fixed.Zero, track.After("rival", t).X);
            Assert.Equal(Fixed.Zero, track.After("loup", t).Z);
        }
        Assert.True(track.After("rival", 10).Z < Fixed.Parse("6"));
    }

    [Fact]
    public void Accelere_il_s_arrete_a_sa_place_d_anneau_sans_osciller()
    {
        // Revue Codex de la PR #33 (tour 11) : un loup encerclant se pose deux accélérations (+100 % chacune, vitesse triplée) par un cri
        // annoncé 200 ms autour de lui (il attend pendant l'annonce) ; ensuite, son pas, trois fois la tolérance de sa place d'anneau,
        // s'arrête sur elle (x = 2,5) au lieu de la dépasser et de repartir en sens inverse (avant : 2,9 et 2,15 en alternance).
        var hate1 = new StatusDefinition("hate1", StatusCategory.Modifier, 60_000, StatusStacking.Refresh, movementSpeedModifier: Fixed.One);
        var hate2 = new StatusDefinition("hate2", StatusCategory.Modifier, 60_000, StatusStacking.Refresh, movementSpeedModifier: Fixed.One);
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0,
            Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Concat(new[] { hate1, hate2 })) };
        var cri = new AssailantAttack("cri", 1, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("hate1", target: ApplicationTarget.Self), new StatusApplication("hate2", target: ApplicationTarget.Self) },
            AttackShape.Circle, 200, radius: F("0.5"), center: ShapeCenter.Self);
        var (track, log, _) = Trace(Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")), Hunter("loup", At("6.65", "0"), "3", encircle: true, firstMs: 0, intervalMs: 60_000, attacks: cri),
        }, durationMs: 3000, rules: rules));
        Assert.Equal(2, Lines(log, "stateApplied").Count(a => a.Str("combatant") == "loup"));
        Assert.NotEqual(track.After("loup", 6), track.After("loup", 7));   // il repart, accéléré
        int still = Enumerable.Range(6, 52).First(t => track.After("loup", t) == track.After("loup", t + 1));
        Assert.True(still < 30, $"arrêté au tick {still}");
        for (int t = still; t < 59; t++) Assert.Equal(track.After("loup", still), track.After("loup", t));
        Assert.Equal(Fixed.Parse("2.5"), track.After("loup", still).X);
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
        // Revue Codex de la PR #33 (tour 4) : le coin d'un obstacle mince frôlé entre deux échantillons (centres à x = 2 et 2,25, tous deux
        // libres) arrête la charge : le disque balayé le toucherait. Elle s'arrête à x = 2.
        var sliver = new SpaceObstacle("piquet", new[] { GroundVector.Parse("2.1", "0.49"), GroundVector.Parse("2.12", "0.49"),
            GroundVector.Parse("2.12", "3"), GroundVector.Parse("2.1", "3") });
        var (_, corner) = Run(Spatial(Arena(20, sliver), new CombatantSetup[]
            { Player("bastion", At("10", "0")), Hunter("sanglier", At("0", "0"), "20", firstMs: 0, attacks: Charge("6")) }, Array.Empty<CombatCommand>(), Rules, 2000));
        Assert.Equal("[2,0]", Assert.Single(Lines(corner, "charged")).GetProperty("position").GetRawText());
    }

    [Fact]
    public void La_recherche_du_bout_de_la_charge_compte_son_travail()
    {
        // Revue Codex de la PR #33 (tour 10) : chaque sous-pas de la recherche (au plus la moitié du rayon, 0,25 m ici) et son contrôle de
        // piège coûtent une unité de travail, réservée avec l'impact : une bande de 40 m coûte 2 × (160 − 24) unités de plus qu'une bande
        // de 6 m au tick de l'impact (Bastion hors des deux bandes, à 50 m).
        long WorkAtImpact(string range)
        {
            var charge = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
                range: F(range), width: F("1"), charge: true);
            var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("50", "0")), Hunter("sanglier", At("0", "0"), "60", firstMs: 0, attacks: charge) },
                durationMs: 2000, space: Arena(60)), NullCombatLog.Instance);
            while (engine.CurrentTick <= 20) engine.Step();
            return engine.LastTickWork;
        }
        Assert.Equal(2 * (160 - 24), WorkAtImpact("40") - WorkAtImpact("6"));
    }

    [Fact]
    public void Instantane_un_chargeur_qui_n_a_pas_encore_charge_garde_sa_place()
    {
        // Revue Codex de la PR #33 (tour 12) : un sanglier sans comportement, dont la première charge est annoncée à 3 s, n'a pas encore pu
        // bouger au tick 10 : un instantané qui le déplace est refusé par la reprise de confiance (Restore) comme par la reprise vérifiée
        // par rejeu (RestoreVerified, décision de Damien du 22 octobre 2026).
        var charge = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"), charge: true);
        var boar = new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 3000, 60_000, new[] { charge }, placement: At("0", "0"));
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("10", "0")), boar }, durationMs: 6000), NullCombatLog.Instance);
        while (engine.CurrentTick < 10) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"sanglier\"", StringComparison.Ordinal));
        string moved = text.Replace(line, line.Replace("\"x\":0,", "\"x\":40000,", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.NotEqual(text, moved);
        Assert.Contains("Place de « sanglier » hors de portée", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(setup, CombatSnapshot.Parse(moved), NullCombatLog.Instance)).Message);
        Assert.Contains("différent de l'état rejoué", Assert.Throws<FormatException>(() =>
            CombatEngine.RestoreVerified(setup, CombatSnapshot.Parse(moved), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Instantane_pas_d_un_ennemi_a_comportement_borne()
    {
        // Revue Codex de la PR #33 (13e tour) : un ennemi à comportement n'est plus libre de toute place dans un instantané. Pas encore
        // engagé (guetteur loin du Bastion), il n'a jamais bougé ; engagé, il a fait au plus un pas par tick depuis le début.
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("guetteur", At("15", "0"), "0.5", aggro: "2"),
            Hunter("loup", At("-8", "0"), "0.5") }, durationMs: 6000), NullCombatLog.Instance);
        while (engine.CurrentTick < 10) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string Moved(string id, string from, string to)
        {
            string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"" + id + "\"", StringComparison.Ordinal));
            string changed = line.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(line, changed);
            return text.Replace(line, changed, StringComparison.Ordinal);
        }
        Assert.Contains("Place de « guetteur » hors de portée", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(setup, CombatSnapshot.Parse(Moved("guetteur", "\"x\":150000,", "\"x\":140000,")), NullCombatLog.Instance)).Message);
        // Le loup engagé d'emblée a marché 10 ticks (au plus 2,5 m, bornés à deux pas par tick) : il ne peut pas être à 9 m de son départ.
        string loup = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"loup\"", StringComparison.Ordinal));
        string x = System.Text.RegularExpressions.Regex.Match(loup, "\"x\":(-?[0-9]+),").Groups[1].Value;
        Assert.Contains("Place de « loup » hors de portée", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(setup, CombatSnapshot.Parse(Moved("loup", "\"x\":" + x + ",", "\"x\":10000,")), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Instantane_engagement_sans_cause_possible_refuse()
    {
        // Revue Codex de la PR #33 (18e tour) : au tick 10, le guetteur (engagement à 2 m) est à 15 m du Bastion immobile et n'a reçu ni
        // coup ni provocation : un instantané qui le dit engagé depuis le tick 0 (ou 5) est refusé, aucune cause n'a pu l'engager. Le loup,
        // engagé d'emblée (sans distance d'engagement), et un vrai engagement tardif restent admis (autres tests).
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("guetteur", At("15", "0"), "0.5", aggro: "2") },
            durationMs: 6000), NullCombatLog.Instance);
        while (engine.CurrentTick < 10) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"guetteur\"", StringComparison.Ordinal));
        Assert.Contains("\"engaged\":false,\"engagedTick\":null", line, StringComparison.Ordinal);
        foreach (int at in new[] { 0, 5, 9 })
        {
            string forged = text.Replace(line, line.Replace("\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":" + at,
                StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Contains("Engagement de « guetteur » au tick " + at + " sans cause possible", Assert.Throws<FormatException>(() =>
                CombatEngine.Restore(setup, CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
        }
    }

    [Fact]
    public void Instantane_pas_d_un_ennemi_engage_tard_comptes_depuis_son_engagement()
    {
        // Revue Codex de la PR #33 (16e tour) : un guetteur (engagement à 2 m) attend que le Bastion approche ; juste après son engagement,
        // il n'a pu faire qu'un ou deux pas : un instantané qui l'a déjà déplacé de 3 m est refusé (ses pas partent de son engagement, pas de
        // son arrivée).
        var (_, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("guetteur", At("6", "0"), "0.5", aggro: "2") },
            new[] { Move(0, "bastion", "1", "0") }, durationMs: 3000));
        long engagedAt = Lines(log, "engaged").Single(e => e.Str("actor") == "guetteur").Tick();
        Assert.InRange(engagedAt, 5, 40);
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("guetteur", At("6", "0"), "0.5", aggro: "2") },
            new[] { Move(0, "bastion", "1", "0") }, durationMs: 3000), NullCombatLog.Instance);
        while (engine.CurrentTick <= engagedAt) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Contains("\"engagedTick\":" + engagedAt, text, StringComparison.Ordinal);
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"guetteur\"", StringComparison.Ordinal));
        string x = System.Text.RegularExpressions.Regex.Match(line, "\"x\":(-?[0-9]+),").Groups[1].Value;
        string moved = text.Replace(line, line.Replace("\"x\":" + x + ",", "\"x\":" + (long.Parse(x) + 30_000) + ",", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.NotEqual(text, moved);
        // 18e tour : éloigné de 3 m, il n'a plus non plus de cause d'engagement possible à son tick ; l'un ou l'autre refus.
        Assert.Matches("Place de « guetteur » hors de portée|Engagement de « guetteur » au tick [0-9]+ sans cause possible", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(setup, CombatSnapshot.Parse(moved), NullCombatLog.Instance)).Message);
        // Un tick d'engagement retouché (plus tôt que le vrai, pour élargir la borne) ne passe pas non plus inaperçu par la reprise vérifiée.
        string earlier = text.Replace("\"engagedTick\":" + engagedAt, "\"engagedTick\":1", StringComparison.Ordinal);
        Assert.Contains("différent de l'état rejoué", Assert.Throws<FormatException>(() =>
            CombatEngine.RestoreVerified(setup, CombatSnapshot.Parse(earlier), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Annonce_qui_tombe_apres_la_fin_arrete_l_ennemi_jusqu_a_la_fin()
    {
        // Revue Codex de la PR #33 (13e tour) : combat de 2 s ; le loup, encore en approche, annonce au tick 30 une bande d'une seconde dont
        // l'impact (tick 50) tombe après la fin : il s'arrête quand même jusqu'à la fin, comme pendant toute annonce.
        var line = new AssailantAttack("coup", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"));
        var (track, log, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("15", "0"), "0.5", firstMs: 1500, attacks: line) },
            durationMs: 2000));
        Assert.Equal(30, Lines(log, "attackStarted").Single(a => a.Str("actor") == "loup").Tick());
        Assert.NotEqual(track.After("loup", 28), track.After("loup", 29));   // encore en marche avant l'annonce
        for (int t = 31; t <= 39; t++) Assert.Equal(track.After("loup", 30), track.After("loup", t));
        // L'instantané garde cette annonce (announcedToEnd) ; elle n'est admise qu'avec un tour déjà joué dont l'impact tombe après la fin
        // (14e tour : calculé par attaque, sans parcourir l'historique des tours).
        var fight = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Hunter("loup", At("15", "0"), "0.5", firstMs: 1500, attacks: line) },
            durationMs: 2000);
        var engine2 = new CombatEngine(fight, NullCombatLog.Instance);
        while (engine2.CurrentTick < 35) engine2.Step();
        string late = engine2.Snapshot().Text;
        Assert.Contains("\"announcedToEnd\":true", late, StringComparison.Ordinal);
        Assert.Equal(late, CombatEngine.Restore(engine2.CompletedSetup(), CombatSnapshot.Parse(late), NullCombatLog.Instance).Snapshot().Text);
        var engine3 = new CombatEngine(fight, NullCombatLog.Instance);
        while (engine3.CurrentTick < 20) engine3.Step();
        string early = engine3.Snapshot().Text;
        string forged = early.Replace("\"announcedToEnd\":false", "\"announcedToEnd\":true", StringComparison.Ordinal);
        Assert.NotEqual(early, forged);
        Assert.Contains("sans tour qui la donne", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(engine3.CompletedSetup(), CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Provocation_posee_par_un_ennemi_sur_un_personnage_ne_casse_pas_l_instantane()
    {
        // Revue Codex de la PR #33 (17e tour) : une attaque d'ennemi peut viser un personnage d'une provocation (altération de catégorie
        // taunt, admise par le scénario). Elle est refusée (notApplicable, sous-lot 8.3 : une provocation ne vise qu'un assaillant) et seuls
        // les assaillants mémorisent la provocation reçue (taunted) : l'instantané du Bastion reste relisible.
        var taunting = new AssailantAttack("defi", 1, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("bastionAbsoluteTaunt") }, AttackShape.Target, range: F("5"));
        var engine = new CombatEngine(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri }),
            Hunter("loup", At("2", "0"), "5", firstMs: 0, attacks: taunting),
        }, durationMs: 3000, rules: NoUpkeep), NullCombatLog.Instance);
        var (_, tlog, _) = Trace(Fight(new CombatantSetup[]
        {
            EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Cri }),
            Hunter("loup", At("2", "0"), "5", firstMs: 0, attacks: taunting),
        }, durationMs: 3000, rules: NoUpkeep));
        JsonElement blocked = Lines(tlog, "stateBlocked").First(b => b.Str("combatant") == "bastion");
        Assert.Equal(("bastionAbsoluteTaunt", "notApplicable", 0L), (blocked.Str("state"), blocked.Str("reason"), blocked.Tick()));
        while (engine.CurrentTick < 5) engine.Step();
        string text = engine.Snapshot().Text;
        string bastion = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal));
        Assert.Contains("\"taunted\":false", bastion, StringComparison.Ordinal);
        Assert.Equal(text, CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
    }

    [Fact]
    public void Instantane_une_charge_bloquee_ne_libere_pas_la_place()
    {
        // Revue Codex de la PR #33 (17e tour) : un sanglier sans comportement s'étourdit lui-même avec sa charge (chargeBlocked au tick 20) :
        // il n'a jamais foncé, sa place reste bornée par ses pas (aucun). Seule une charge réellement résolue (charged) la libère.
        var stunning = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("stun", target: ApplicationTarget.Self) }, AttackShape.Line, 1000, range: F("6"), width: F("1"), charge: true);
        var boar = new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 0, 60_000, new[] { stunning }, placement: At("0", "0"));
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), boar }, durationMs: 3000), NullCombatLog.Instance);
        while (engine.CurrentTick < 30) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Contains("\"charged\":false", text, StringComparison.Ordinal);
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"sanglier\"", StringComparison.Ordinal));
        string moved = text.Replace(line, line.Replace("\"x\":0,", "\"x\":-40000,", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.NotEqual(text, moved);
        Assert.Contains("Place de « sanglier » hors de portée", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(setup, CombatSnapshot.Parse(moved), NullCombatLog.Instance)).Message);
        // Après une vraie charge, le champ est posé et relu ; une charge annoncée à 3 s ne peut pas être déjà résolue au tick 10.
        var charge = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"), charge: true);
        var real = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("10", "0")),
            new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 0, 60_000, new[] { charge }, placement: At("0", "0")) }, durationMs: 3000),
            NullCombatLog.Instance);
        while (real.CurrentTick < 25) real.Step();
        string charged = real.Snapshot().Text;
        Assert.Contains("\"charged\":true", charged, StringComparison.Ordinal);
        Assert.Equal(charged, CombatEngine.Restore(real.CompletedSetup(), CombatSnapshot.Parse(charged), NullCombatLog.Instance).Snapshot().Text);
        var late = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("10", "0")),
            new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 3000, 60_000, new[] { charge }, placement: At("0", "0")) }, durationMs: 6000),
            NullCombatLog.Instance);
        while (late.CurrentTick < 10) late.Step();
        string early = late.Snapshot().Text;
        string boarLine = early.Split('\n').First(l => l.StartsWith("{\"fighter\":\"sanglier\"", StringComparison.Ordinal));
        string forged = early.Replace(boarLine, boarLine.Replace("\"charged\":false", "\"charged\":true", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.NotEqual(early, forged);
        Assert.Contains("Charge de « sanglier » sans tour de charge", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(late.CompletedSetup(), CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Instantane_annonce_terminale_sautee_avant_l_engagement_refusee()
    {
        // Revue Codex de la PR #33 (17e tour) : combat de 2 s, tour unique au tick 20 dont l'impact (tick 40) tombe après la fin. Le
        // guetteur, pas encore engagé, saute ce tour (attackSkipped) puis s'engage quand le Bastion approche : il n'a rien annoncé, un
        // instantané qui lui prête une annonce jusqu'à la fin (et l'immobiliserait) est refusé.
        var line = new AssailantAttack("coup", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"));
        CombatSetup Setup() => Fight(new CombatantSetup[] { Player("bastion", At("0", "0")),
            Hunter("guetteur", At(Distance, "0"), "0.5", aggro: "2", firstMs: 1000, intervalMs: 60_000, attacks: line) },
            new[] { Move(0, "bastion", "1", "0") }, durationMs: 2000);
        var (_, log, _) = Trace(Setup());
        Assert.Contains(Lines(log, "attackSkipped"), a => a.Str("actor") == "guetteur" && a.Tick() == 20);
        long engagedAt = Lines(log, "engaged").Single(e => e.Str("actor") == "guetteur").Tick();
        Assert.InRange(engagedAt, 21, 37);
        var engine = new CombatEngine(Setup(), NullCombatLog.Instance);
        while (engine.CurrentTick < 38) engine.Step();
        string text = engine.Snapshot().Text;
        Assert.Contains("\"engagedTick\":" + engagedAt, text, StringComparison.Ordinal);
        Assert.Equal(text, CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string guetteur = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"guetteur\"", StringComparison.Ordinal));
        string forged = text.Replace(guetteur, guetteur.Replace("\"announcedToEnd\":false", "\"announcedToEnd\":true", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.NotEqual(text, forged);
        Assert.Contains("sans tour qui la donne", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
    }

    private const string Distance = "11";

    [Fact]
    public void Une_charge_qui_l_etourdit_lui_meme_ne_fonce_pas()
    {
        // Revue Codex de la PR #33 (tour 8) : la bande pose un étourdissement sur le sanglier lui-même, avec ses coups. L'immobilisation est
        // relue après eux : il ne fonce pas, et la ligne réservée est chargeBlocked (raison de l'étourdissement) au lieu de charged.
        var stunning = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("stun", target: ApplicationTarget.Self) }, AttackShape.Line, 1000, range: F("6"), width: F("1"), charge: true);
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Hunter("sanglier", At("0", "0"), "20", firstMs: 0, attacks: stunning) },
            durationMs: 2000));
        Assert.Contains(Lines(log, "stateApplied"), s => s.Str("combatant") == "sanglier" && s.Str("state") == "stun" && s.Tick() == 20);
        Assert.Empty(Lines(log, "charged"));
        JsonElement blocked = Assert.Single(Lines(log, "chargeBlocked"));
        Assert.Equal(("sanglier", "charge", 20L), (blocked.Str("actor"), blocked.Str("attack"), blocked.Tick()));
        Assert.False(string.IsNullOrEmpty(blocked.Str("reason")));
        Assert.Equal(track.After("sanglier", 19), track.After("sanglier", 21));
        // Tour 9 : étourdi pendant l'annonce (par son attaque suivante, deux ticks après), donc déjà immobilisé à l'impact : la charge écrit
        // aussi chargeBlocked.
        var charge = new AssailantAttack("charge", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000,
            range: F("6"), width: F("1"), charge: true);
        var cri = new AssailantAttack("cri", 1, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("stun", target: ApplicationTarget.Self) });
        var (before, early, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")),
            Hunter("sanglier", At("0", "0"), "20", firstMs: 0, intervalMs: 100, attacks: new[] { charge, cri }) }, durationMs: 2000));
        JsonElement stunned = Lines(early, "stateApplied").First(s => s.Str("combatant") == "sanglier" && s.Str("state") == "stun");
        Assert.True(stunned.Tick() < 20, $"étourdi au tick {stunned.Tick()}");
        Assert.Empty(Lines(early, "charged"));
        JsonElement already = Assert.Single(Lines(early, "chargeBlocked"));
        Assert.Equal(("sanglier", "charge", 20L), (already.Str("actor"), already.Str("attack"), already.Tick()));
        Assert.Equal(before.After("sanglier", 19), before.After("sanglier", 21));
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
        string guetteur = EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.Tampered(text, "{\"fighter\":\"guetteur\"", "\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":4");
        Assert.Equal(guetteur, CombatEngine.Restore(completed, CombatSnapshot.Parse(guetteur), NullCombatLog.Instance).Snapshot().Text);
        EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.AssertRefused(completed, text, new[]
        {
            ("{\"fighter\":\"loup\"", "\"engaged\":true", "\"engaged\":false", "Engagement de « loup »"),
            ("{\"fighter\":\"bastion\"", "\"engaged\":false", "\"engaged\":true", "Engagement de « bastion »"),
            ("{\"fighter\":\"renfort\"", "\"struck\":false", "\"struck\":true", "« renfort », pas encore entré"),
            ("{\"fighter\":\"renfort\"", "\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":0", "« renfort », pas encore entré"),
            // 16e tour : un engagé sans tick d'engagement, ou à un tick pas encore joué.
            ("{\"fighter\":\"guetteur\"", "\"engaged\":false", "\"engaged\":true", "Tick d'engagement de « guetteur »"),
            ("{\"fighter\":\"guetteur\"", "\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":5", "Tick d'engagement de « guetteur »"),
            ("{\"fighter\":\"renfort\"", "\"taunted\":false", "\"taunted\":true", "« renfort », pas encore entré"),
        });
        // Revue Codex de la PR #33 (tour 4) : avant le premier tick, le guetteur (distance d'engagement) n'est pas encore engagé.
        var fresh = new CombatEngine(setup, NullCombatLog.Instance);
        string zero = fresh.Snapshot().Text;
        EdS.Simulation.Tests.Presence.Lot114dSnapshotTests.AssertRefused(fresh.CompletedSetup(), zero, new[]
        {
            ("{\"fighter\":\"guetteur\"", "\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":0", "Tick d'engagement de « guetteur »"),
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
        // Revue Codex de la PR #33 (16e tour) : la vue le dit (CanMove faux pour un ennemi à comportement, vrai pour l'ours).
        Assert.False(engine.View("loup").CanMove);
        Assert.True(engine.View("ours").CanMove);
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
