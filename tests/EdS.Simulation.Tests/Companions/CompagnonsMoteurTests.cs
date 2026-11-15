using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Companions;

/// <summary>
/// Compagnons (sujet 11 de la feuille de route, décisions de Damien du 14 novembre 2026) : compagnon allié guidé par l'ordinateur dans le
/// combat positionné, ordres simples (suivre, attendre, cibler, protéger, revenir), jauge de lien et attaque à deux, jamais automatique.
/// Tests unitaires, adversariaux et de propriétés ; atomicité des refus ; instantané.
/// </summary>
[Trait("Category", "Companion")]
public class CompagnonsMoteurTests
{
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 };

    private static readonly CompanionDuo ContreJour = new("contre_jour", 300, EdS.Domain.Stats.DamageType.Physique, 800, 1500, Fixed.FromInt(3));

    private static AssailantAttack Seax(long telegraphMs = 0, long raw = 40, string range = "1.2") =>
        new("seax", raw, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Target, telegraphMs, Fixed.Parse(range));

    /// <summary>Compagnon de « bastion » : 800 PV, attaque toutes les secondes à partir de <paramref name="firstMs"/>.</summary>
    private static AssailantSetup Companion(CombatantPlacement at, string id = "nyx", string owner = "bastion", int bond = 0, CompanionDuo? duo = null,
        long firstMs = 500, string team = "joueurs", AssailantAttack? attack = null, long health = 800, string follow = "1.5", string leash = "10",
        int coordinated = 10, int protect = 5, AssailantBehavior? behavior = null, string? target = null) =>
        new(id, team, Fixed.Zero, Fixed.Zero, target, firstMs, 1000, new[] { attack ?? Seax() }, health: health,
            placement: at, behavior: behavior ?? new AssailantBehavior(Fixed.Parse("0.5")),
            companion: new CompanionSetup(owner, bond, Fixed.Parse(follow), Fixed.Parse(leash), 3000, coordinated, protect, duo));

    /// <summary>Loup engagé d'emblée (sans distance d'engagement), qui mord <paramref name="victim"/> ou le plus proche.</summary>
    private static AssailantSetup Wolf(string id, CombatantPlacement at, long health = 600, long firstMs = 600_000, AssailantBehavior? behavior = null) =>
        new(id, "meute", Fixed.Zero, Fixed.Zero, null, firstMs, 1500, new[] { Seax(raw: 30, range: "1") }, health: health, placement: at,
            behavior: behavior ?? new AssailantBehavior(Fixed.Parse("0.5")));

    private static CombatCommand Order(long atMs, CompanionOrder order, string? target = null, string companion = "nyx", string actor = "bastion") =>
        CombatCommand.GiveOrder(atMs, actor, companion, order, target);

    private static CombatCommand Duo(long atMs, string target, string companion = "nyx", string actor = "bastion") =>
        CombatCommand.DuoAttack(atMs, actor, companion, target);

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand>? commands = null, long durationMs = 6000,
        CombatRules? rules = null, CombatSpace? space = null)
    {
        List<CombatantSetup> all = combatants.ToList();
        if (all.All(c => c.Team == "joueurs")) all.Add(PlacedDummy("mannequin", At("-19", "-19")));
        return Spatial(space ?? Arena(20), all, commands ?? Array.Empty<CombatCommand>(), rules ?? Rules, durationMs);
    }

    private static IEnumerable<JsonElement> Hits(MemoryCombatLog log, string source) => Lines(log, "damage").Where(d => d.Str("source") == source);

    private static List<string> Problems(CombatSetup setup) => setup.Validate().SelectMany(v => v.Fields).ToList();

    // --- Suivre ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Sans_ennemi_il_rejoint_son_Eveille_et_s_arrete_a_la_distance_de_suite()
    {
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("8", "0", "0.45")) }, durationMs: 4000));
        GroundVector end = track.After("nyx", 79);
        // Bords à 1,5 m : centres à 0,5 + 0,45 + 1,5 = 2,45 m (tolérance de la bande).
        long d = IntMath.Sqrt(DistanceSquaredRaw(end, V("0", "0")));
        Assert.InRange(d, Fixed.Parse("2.4").Raw, Fixed.Parse("2.5").Raw);
        Assert.Empty(Hits(log, "nyx"));
        // Sans ennemi, aucune attaque : ses tours sont sautés faute de cible (le mannequin lointain est hors de la laisse).
        Assert.All(Lines(log, "attackSkipped").Where(e => e.Str("actor") == "nyx"), e => Assert.Equal("noTarget", e.Str("reason")));
    }

    [Fact]
    public void Suivre_vise_le_plus_proche_a_la_laisse_de_l_Eveille_et_ignore_la_bete_paisible()
    {
        // Loup proche (engagé d'emblée) ; loup lointain hors de la laisse de 10 m ; cerf paisible tout près, jamais engagé.
        var deer = new AssailantSetup("cerf", "faune", Fixed.Zero, Fixed.Zero, null, 600_000, 2000, new[] { Seax() }, health: 300,
            placement: At("-1.5", "0"), behavior: new AssailantBehavior(Fixed.FromInt(5), Fixed.FromInt(3), Fixed.FromInt(1), flee: true));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.5", "0.45")),
            Wolf("loup", At("3", "0", "0.6")), Wolf("loin", At("15", "15", "0.6")), deer }));
        Assert.NotEmpty(Hits(log, "nyx"));
        Assert.All(Hits(log, "nyx"), h => Assert.Equal("loup", h.Str("target")));
    }

    // --- Cibler ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Cibler_vise_la_cible_puis_revient_a_suivre_quand_elle_tombe()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.5", "0.45"), attack: Seax(raw: 200)),
            Wolf("loup", At("3", "0", "0.6")), Wolf("cible", At("-3", "2", "0.6"), health: 300) },
            new[] { Order(0, CompanionOrder.Target, "cible") }));
        JsonElement given = Assert.Single(Lines(log, "orderGiven"));
        Assert.Equal(("target", "cible"), (given.Str("order"), given.Str("target")));
        List<JsonElement> hits = Hits(log, "nyx").ToList();
        Assert.Equal(new[] { "cible", "cible" }, hits.Take(2).Select(h => h.Str("target")));
        JsonElement died = Assert.Single(Lines(log, "died"), e => e.Str("combatant") == "cible");
        JsonElement ended = Assert.Single(Lines(log, "orderEnded"));
        Assert.Equal((died.Tick() + 1, "target", "targetDead"), (ended.Tick(), ended.Str("order"), ended.Str("reason")));
        Assert.All(hits.Skip(2), h => Assert.Equal("loup", h.Str("target")));   // puis suivre : le loup
    }

    [Fact]
    public void Ordres_refuses_sans_rien_changer()
    {
        // Cibler un allié, ordre d'un Éveillé mort ou au compagnon tombé : refus (diagnostic), aucun ordre donné.
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.5", "0.45"), health: 1),
            Wolf("loup", At("1.6", "1.5", "0.6"), firstMs: 0) },
            new[] { Order(0, CompanionOrder.Target, "bastion"), Order(500, CompanionOrder.Wait) }));
        Assert.Empty(Lines(log, "orderGiven"));
        Assert.Equal(new[] { "targetNotEnemy", "companionDead" }, Lines(log, "commandRejected").Select(e => e.Str("reason")));
    }

    [Fact]
    public void La_vue_montre_la_cible_de_l_ordre_et_aucune_en_revenant()
    {
        // Revue Codex de la PR #169 : la cible exposée par View est celle que le compagnon attaque vraiment.
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "6", "0.45")),
            Wolf("proche", At("1.5", "6", "0.6"), health: 100_000), Wolf("cible", At("-4", "0", "0.6"), health: 100_000) },
            new[] { Order(0, CompanionOrder.Target, "cible"), Order(100, CompanionOrder.Return) }), new MemoryCombatLog());
        Assert.Equal("proche", engine.View("nyx").CurrentTarget);   // suivre : le plus proche
        engine.Step();
        Assert.Equal("cible", engine.View("nyx").CurrentTarget);
        engine.Step();
        engine.Step();
        Assert.Null(engine.View("nyx").CurrentTarget);   // revenir : aucune cible
    }

    // --- Attendre ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Attendre_ne_bouge_pas_et_ne_frappe_que_ce_qui_vient_a_lui()
    {
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("-6", "0", "0.45")),
            Wolf("loup", At("6", "0", "0.6")) }, new[] { Order(0, CompanionOrder.Wait) }, durationMs: 3000));
        Assert.All(track["nyx"], p => Assert.Equal(V("-6", "0"), p));
        Assert.Empty(Hits(log, "nyx"));
        Assert.Contains(Lines(log, "attackSkipped"), e => e.Str("actor") == "nyx" && e.Str("reason") == "targetOutOfRange");
    }

    // --- Protéger et lien --------------------------------------------------------------------------------------------

    [Fact]
    public void Proteger_vise_l_ennemi_le_plus_proche_de_l_Eveille_et_gagne_du_lien()
    {
        // Le loup « menace » mord l'Éveillé ; le loup « proche » est plus près du compagnon. Protéger : la menace.
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("4", "0", "0.45")),
            Wolf("menace", At("-1.1", "0", "0.6"), firstMs: 0), Wolf("proche", At("5.2", "0", "0.6")) },
            new[] { Order(0, CompanionOrder.Protect) }, durationMs: 4000));
        Assert.All(Hits(log, "nyx"), h => Assert.Equal("menace", h.Str("target")));
        JsonElement gain = Lines(log, "bondGained").First();
        Assert.Equal(("protected", 5L), (gain.Str("reason"), gain.Int("amount")));
    }

    [Fact]
    public void Coups_coordonnes_un_gain_par_tick_jauge_bornee_a_100()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 95),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            Enumerable.Range(0, 20).Select(i => Cmd(i * 250, Gratuite, target: "loup")), durationMs: 5000));
        List<JsonElement> gains = Lines(log, "bondGained");
        Assert.NotEmpty(gains);
        Assert.Equal(gains.Count, gains.Select(g => g.Tick()).Distinct().Count());
        Assert.Equal((5L, 100L), (gains[0].Int("amount"), gains[0].Int("bond")));   // 95 + 10, borné à 100
        Assert.Single(gains);   // pleine : plus aucune ligne
        Assert.Equal("coordinated", gains[0].Str("reason"));
    }

    [Fact]
    public void Coup_de_zone_de_l_Eveille_puis_coup_du_compagnon_sur_l_une_des_cibles_coordonne()
    {
        // Revue Codex de la PR #169 : l'onde de l'Éveillé touche a_loup puis b_loup ; le compagnon frappe ensuite a_loup : la trace de a_loup
        // n'est pas écrasée par b_loup, le coup est coordonné.
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45")),
            Wolf("a_loup", At("1.2", "0", "0.6"), health: 100_000), Wolf("b_loup", At("0", "-1.2", "0.6"), health: 100_000) },
            new[] { Cmd(0, Onde, target: null) }, durationMs: 1500));
        Assert.Equal(new[] { "a_loup", "b_loup" }, Lines(log, "damage").Where(d => d.Str("source") == "bastion").Select(d => d.Str("target")));
        JsonElement hit = Hits(log, "nyx").First();
        Assert.Equal("a_loup", hit.Str("target"));
        JsonElement gain = Assert.Single(Lines(log, "bondGained"), g => g.Tick() == hit.Tick());
        Assert.Equal("coordinated", gain.Str("reason"));
    }

    // --- Revenir -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Revenir_annule_l_annonce_saute_les_attaques_puis_revient_a_suivre()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")),
            Companion(At("6", "0", "0.45"), attack: Seax(telegraphMs: 400), firstMs: 0), Wolf("loup", At("7.2", "0", "0.6"), health: 100_000) },
            new[] { Order(0, CompanionOrder.Target, "loup"), Order(100, CompanionOrder.Return) }, durationMs: 4000));
        JsonElement cancelled = Assert.Single(Lines(log, "attackCancelled"));
        Assert.Equal(("nyx", "recalled"), (cancelled.Str("actor"), cancelled.Str("reason")));
        long arrived = Lines(log, "orderEnded").Single().Tick();
        Assert.DoesNotContain(Hits(log, "nyx"), h => h.Tick() < arrived);
        JsonElement ended = Assert.Single(Lines(log, "orderEnded"));
        Assert.Equal(("return", "arrived"), (ended.Str("order"), ended.Str("reason")));
        Assert.Contains(Lines(log, "attackSkipped"), e => e.Str("actor") == "nyx" && e.Str("reason") == "recalled");
    }

    [Fact]
    public void Revenir_finit_dans_la_bande_d_arret_et_quand_l_Eveille_n_est_plus_la()
    {
        // Relecture indépendante de la PR #169 : bords à 1,5 m + 0,001 m (dans la tolérance de la bande où il s'arrête) : l'ordre finit au tick
        // suivant au lieu de durer jusqu'à la fin du combat.
        var (_, band) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("2.451", "0", "0.45")),
            Wolf("loup", At("15", "15", "0.6"), health: 100_000) }, new[] { Order(0, CompanionOrder.Return) }, durationMs: 1000));
        JsonElement ended = Assert.Single(Lines(band, "orderEnded"));
        Assert.Equal((1L, "arrived"), (ended.Tick(), ended.Str("reason")));
        // L'Éveillé tombé : « revenir » finit (ownerDead).
        var brute = new AssailantSetup("brute", "meute", Fixed.Zero, Fixed.Zero, "bastion", 0, 500, new[] { Seax(raw: 100_000, range: "2") }, health: 100_000,
            placement: At("1.2", "0", "0.6"));
        var (_, owner) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Player("autre", At("-6", "-6")), Companion(At("-8", "0", "0.45")), brute },
            new[] { Order(0, CompanionOrder.Return) }, durationMs: 1000));
        Assert.Contains(Lines(owner, "orderEnded"), e => e.Str("reason") == "ownerDead");
    }

    [Fact]
    public void Eveille_hors_du_combat_le_compagnon_garde_sa_place_meme_avec_une_cible()
    {
        // Revue Codex de la PR #169 (3e tour) : l'Éveillé part (leave) ; un autre joueur garde l'équipe en vie ; le loup reste à portée de laisse.
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Player("autre", At("-8", "-8")), Companion(At("0", "2", "0.45")),
            Wolf("loup", At("6", "2", "0.6"), health: 100_000, behavior: new AssailantBehavior(Fixed.FromInt(20))) },
            new[] { new CombatCommand(0, "bastion", CombatAction.Leave) }, durationMs: 2000));
        GroundVector start = track.After("nyx", 1);
        Assert.All(track["nyx"].Skip(1), p => Assert.Equal(start, p));
    }

    [Fact]
    public void Pilote_admet_les_ordres_d_un_personnage_et_d_un_compagnon_arrives_par_Join()
    {
        // Relecture indépendante de la PR #169 : les contrôles du pilote connaissent les arrivées (pas seulement les combattants du scénario).
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Wolf("loup", At("6", "0")) }), new MemoryCombatLog());
        engine.Step();
        CharacterSetup baseHero = Player("arrivant", At("-4", "0"));
        engine.Join(new CharacterSetup(baseHero.Id, baseHero.Team, baseHero.Class, baseHero.Build, 0, baseHero.Placement, 50));
        AssailantSetup c = Companion(At("-4", "2", "0.45"), id: "chien", owner: "arrivant");
        engine.Join(new AssailantSetup(c.Id, c.Team, c.Armor, c.MagicResistance, null, c.FirstAttackMs, c.IntervalMs, c.Attacks, health: c.Health,
            placement: c.Placement, arrivalMs: 50, behavior: c.Behavior, companion: c.Companion));
        engine.Submit(Order(50, CompanionOrder.Target, "loup", companion: "chien", actor: "arrivant"));
        while (engine.Step()) { }
        Assert.Single(engine.SubmittedCommands);
    }

    // --- Attaque à deux ----------------------------------------------------------------------------------------------

    [Fact]
    public void Attaque_a_deux_jauge_pleine_seulement_jamais_automatique()
    {
        // Jauge à 100 sans commande : aucune attaque à deux.
        var (_, idle) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) }, durationMs: 4000));
        Assert.Empty(Lines(idle, "duoStarted"));
        // Jauge à 90 : refus bondNotFull.
        var (_, low) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 90, duo: ContreJour),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) }, new[] { Duo(0, "loup") }, durationMs: 2000));
        Assert.Equal("bondNotFull", Assert.Single(Lines(low, "commandRejected")).Str("reason"));
        Assert.Empty(Lines(low, "duoStarted"));
    }

    [Fact]
    public void Attaque_a_deux_depense_la_jauge_frappe_apres_l_annonce_et_bloque_les_competences()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            new[] { Duo(1000, "loup"), Cmd(1500, Gratuite, target: "loup"), Cmd(2550, Gratuite, target: "loup") }, durationMs: 4000));
        JsonElement started = Assert.Single(Lines(log, "duoStarted"));
        Assert.Equal((20L, 0L, 36L, 50L), (started.Tick(), started.Int("bond"), started.Int("impactTick"), started.Int("readyTick")));
        JsonElement landed = Assert.Single(Lines(log, "duoLanded"));
        Assert.Equal(36, landed.Tick());
        JsonElement hit = Assert.Single(Hits(log, "nyx"), h => h.Str("attack") == "contre_jour");
        Assert.Equal(36, hit.Tick());
        // L'Éveillé ne lance rien avant la fin de la récupération (tick 50), puis de nouveau.
        Assert.Equal("globalCooldown", Assert.Single(Lines(log, "commandRejected")).Str("reason"));
        Assert.Contains(Lines(log, "skillCast"), e => e.Tick() == 51);
        // Le compagnon saute ses attaques pendant l'attaque à deux.
        Assert.Contains(Lines(log, "attackSkipped"), e => e.Str("actor") == "nyx" && e.Str("reason") == "duo");
    }

    [Theory]
    [InlineData("-6", "0", "0", "1.2")]   // l'Éveillé loin, le compagnon à portée
    [InlineData("0", "0", "-6", "0")]     // le compagnon loin, l'Éveillé à portée
    public void Attaque_a_deux_refusee_si_un_seul_partenaire_est_a_portee(string ox, string oz, string cx, string cz)
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At(ox, oz)), Companion(At(cx, cz, "0.45"), bond: 100, duo: ContreJour, firstMs: 600_000),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000, behavior: new AssailantBehavior(Fixed.FromInt(20))) }, new[] { Order(0, CompanionOrder.Wait), Duo(50, "loup") },
            durationMs: 500));
        Assert.Equal("targetOutOfRange", Assert.Single(Lines(log, "commandRejected")).Str("reason"));
    }

    [Fact]
    public void Coups_de_l_attaque_a_deux_sans_gain_de_lien_et_rappel_qui_l_annule()
    {
        // L'Éveillé frappe le loup, puis l'attaque à deux le frappe dans la fenêtre de coordination : aucun gain à l'impact.
        AssailantSetup nyx = Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour, firstMs: 600_000);
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), nyx, Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            new[] { Cmd(0, Gratuite, target: "loup"), Duo(1500, "loup") }, durationMs: 3000));
        long landed = Assert.Single(Lines(log, "duoLanded")).Tick();
        Assert.DoesNotContain(Lines(log, "bondGained"), g => g.Tick() == landed);
        // « Revenir » pendant l'annonce : l'attaque à deux ne part pas (duoCancelled, recalled), jauge non rendue.
        var (_, recalled) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), nyx, Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            new[] { Duo(0, "loup"), Order(200, CompanionOrder.Return) }, durationMs: 2000));
        Assert.Equal("recalled", Assert.Single(Lines(recalled, "duoCancelled")).Str("reason"));
        Assert.Empty(Lines(recalled, "duoLanded"));
    }

    [Fact]
    public void Attaque_a_deux_hors_de_portee_refusee_et_annulee_si_la_cible_tombe()
    {
        var (_, far) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour),
            Wolf("loup", At("8", "0", "0.6"), health: 100_000, behavior: new AssailantBehavior(Fixed.FromInt(20))) }, new[] { Duo(0, "loup") }, durationMs: 1000));
        Assert.Equal("targetOutOfRange", Assert.Single(Lines(far, "commandRejected")).Str("reason"));
        // La cible tombe pendant l'annonce (achevée par un autre joueur) : annulée, jauge non rendue.
        var (_, gone) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Player("autre", At("2.4", "0")),
            Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour), Wolf("loup", At("1.2", "0", "0.6"), health: 1),
            Wolf("loin", At("18", "18", "0.6"), health: 100_000) }, new[] { Duo(0, "loup"), Cmd(250, Gratuite, actor: "autre", target: "loup") }, durationMs: 2000));
        JsonElement cancelled = Assert.Single(Lines(gone, "duoCancelled"));
        Assert.Equal("targetDead", cancelled.Str("reason"));
        Assert.Equal(0, Assert.Single(Lines(gone, "duoStarted")).Int("bond"));
    }

    // --- Hors combat, équipes, bêtes ------------------------------------------------------------------------------------

    [Fact]
    public void Un_compagnon_ne_garde_pas_seul_son_equipe_en_vie()
    {
        // L'Éveillé (1 PV de marge) tombe ; le compagnon vivant ne prolonge pas le combat.
        var brute = new AssailantSetup("brute", "meute", Fixed.Zero, Fixed.Zero, "bastion", 0, 500, new[] { Seax(raw: 100_000, range: "2") }, health: 100_000,
            placement: At("1.2", "0", "0.6"));
        var (result, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("-3", "0", "0.45")), brute }, durationMs: 4000));
        Assert.Contains(Lines(log, "died"), e => e.Str("combatant") == "bastion");
        JsonElement end = Assert.Single(Lines(log, "combatEnded"));
        Assert.Equal(("teamDefeated", "meute"), (end.Str("reason"), end.Str("winner")));
        Assert.True(result.Combatants.Single(c => c.Id == "nyx").DeathTick is null);
    }

    [Fact]
    public void Une_bete_qui_erre_s_engage_contre_un_compagnon_a_1_PV()
    {
        // Revue Codex de la PR #169 (5e tour) : un compagnon à 1 PV, seul ennemi à distance d'engagement, n'est pas une « bête à bout ».
        var wander = new WanderBehavior(WanderStyle.Graze, Fixed.FromInt(3), Fixed.Parse("0.5"), 2000, 500, 1000);
        var wolf = new AssailantSetup("loup", "meute", Fixed.Zero, Fixed.Zero, null, 600_000, 1500, new[] { Seax(raw: 30, range: "1") }, health: 500,
            placement: At("4", "0", "0.6"), behavior: new AssailantBehavior(Fixed.Parse("0.5"), aggroRange: Fixed.FromInt(4), wander: wander));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("-9", "0")), Companion(At("2", "0", "0.45"), health: 1, firstMs: 600_000), wolf },
            durationMs: 500));
        JsonElement engaged = Assert.Single(Lines(log, "engaged"));
        Assert.Equal((0L, "loup", "aggro"), (engaged.Tick(), engaged.Str("actor"), engaged.Str("reason")));
    }

    [Fact]
    public void Ses_coups_sur_une_creature_ne_sont_ni_reduits_ni_plafonnes_entre_betes()
    {
        CombatRules rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, InfightingDamageReduction = Fixed.Parse("0.5") };
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), attack: Seax(raw: 100)),
            Wolf("loup", At("1.2", "1.2", "0.6"), health: 150) }, rules: rules, durationMs: 3000));
        List<JsonElement> hits = Hits(log, "nyx").ToList();
        Assert.Equal(100, hits[0].Int("amount"));
        Assert.Contains(Lines(log, "died"), e => e.Str("combatant") == "loup");
    }

    // --- Validation (adversarial) ---------------------------------------------------------------------------------------

    [Fact]
    public void Validation_d_un_compagnon()
    {
        CombatantSetup player = Player("bastion", At("0", "0"));
        CombatantSetup wolf = Wolf("loup", At("5", "0"));
        Assert.Empty(Problems(Fight(new[] { player, Companion(At("2", "0")), wolf })));
        Assert.Contains("combatants[1].companion.owner", Problems(Fight(new[] { player, Companion(At("2", "0"), owner: "personne"), wolf })));
        Assert.Contains("combatants[1].companion.owner", Problems(Fight(new[] { player, Companion(At("2", "0"), owner: "loup"), wolf })));
        Assert.Contains("combatants[1].team", Problems(Fight(new[] { player, Companion(At("2", "0"), team: "autres"), wolf })));
        Assert.Contains("combatants[1].companion.bond", Problems(Fight(new[] { player, Companion(At("2", "0"), bond: 101), wolf })));
        Assert.Contains("combatants[1].companion.bond", Problems(Fight(new[] { player, Companion(At("2", "0"), bond: -1), wolf })));
        Assert.Contains("combatants[1].companion.leash", Problems(Fight(new[] { player, Companion(At("2", "0"), leash: "1"), wolf })));
        Assert.Contains("combatants[1].companion.followDistance", Problems(Fight(new[] { player, Companion(At("2", "0"), follow: "0"), wolf })));
        Assert.Contains("combatants[1].companion.coordinatedGain", Problems(Fight(new[] { player, Companion(At("2", "0"), coordinated: 101), wolf })));
        Assert.Contains("combatants[1].behavior.aggroRange",
            Problems(Fight(new[] { player, Companion(At("2", "0"), behavior: new AssailantBehavior(Fixed.One, aggroRange: Fixed.One)), wolf })));
        Assert.Contains("combatants[1].target", Problems(Fight(new[] { player, Companion(At("2", "0"), target: "loup"), wolf })));
        Assert.Contains("combatants[1].companion.duo.id",
            Problems(Fight(new[] { player, Companion(At("2", "0"), duo: new CompanionDuo("seax", 10, EdS.Domain.Stats.DamageType.Physique, 800, 1500, Fixed.One)), wolf })));
        Assert.Contains("combatants[1].companion.duo.recoveryMs",
            Problems(Fight(new[] { player, Companion(At("2", "0"), duo: new CompanionDuo("duo", 10, EdS.Domain.Stats.DamageType.Physique, 800, 800, Fixed.One)), wolf })));
        // Trois compagnons pour un Éveillé : refusé au troisième.
        Assert.Contains("combatants[3].companion.owner", Problems(Fight(new[] { player, Companion(At("2", "0"), id: "a"), Companion(At("2", "2"), id: "b"),
            Companion(At("2", "-2"), id: "c"), wolf })));
        // Sans espace : refusé.
        var flat = new CombatSetup(1, 1000, Domain.Stats.StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, Rules,
            new CombatantSetup[] { Character(), new AssailantSetup("nyx", "joueurs", Fixed.Zero, Fixed.Zero, null, 0, 1000, new[] { Blow() }, health: 10,
                companion: new CompanionSetup("bastion", 0, Fixed.One, Fixed.FromInt(5), 3000, 1, 1)), Dummy() }, Array.Empty<CombatCommand>(), Identity);
        Assert.Contains("combatants[1].companion", flat.Validate().SelectMany(v => v.Fields));
    }

    [Fact]
    public void Validation_des_ordres_et_attaques_a_deux()
    {
        CombatantSetup[] fighters = { Player("bastion", At("0", "0")), Player("autre", At("0", "3")), Companion(At("2", "0")), Wolf("loup", At("5", "0")) };
        Assert.Empty(Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Target, "loup"), Order(50, CompanionOrder.Follow) })));
        Assert.Contains("commands[0].target", Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Target) })));
        Assert.Contains("commands[0].target", Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Wait, "loup") })));
        Assert.Contains("commands[0].companion", Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Wait, actor: "autre") })));
        Assert.Contains("commands[0].companion", Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Wait, companion: "loup") })));
        Assert.Contains("commands[0].actor", Problems(Fight(fighters, new[] { Order(0, CompanionOrder.Wait, actor: "loup", companion: "nyx") })));
        Assert.Contains("commands[0].companion", Problems(Fight(fighters, new[] { Duo(0, "loup") })));   // pas encore d'attaque à deux
        Assert.Contains("commands[0].order", Problems(Fight(fighters, new[] { Order(0, (CompanionOrder)9) })));
    }

    [Fact]
    public void Pilote_admet_un_ordre_et_refuse_une_commande_mal_formee_sans_rien_changer()
    {
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Player("autre", At("0", "3")), Companion(At("2", "0")),
            Wolf("loup", At("5", "0")) });
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        engine.Step();
        string before = engine.Snapshot().Text;
        Assert.Throws<ArgumentException>(() => engine.Submit(Order(50, CompanionOrder.Wait, actor: "autre")));
        Assert.Equal(before, engine.Snapshot().Text);
        engine.Submit(Order(50, CompanionOrder.Wait));
        Assert.Single(engine.SubmittedCommands);
    }

    // --- Propriétés, déterminisme, instantané ------------------------------------------------------------------------

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(42L)]
    [InlineData(2026L)]
    public void Proprietes_jauge_bornee_et_journal_reproductible(long seed)
    {
        var random = new Random((int)seed);
        string P() => (random.Next(-80, 81) / 10m).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var commands = new List<CombatCommand>();
        var orders = new[] { CompanionOrder.Follow, CompanionOrder.Wait, CompanionOrder.Protect, CompanionOrder.Return };
        for (int i = 0; i < 12; i++)
            commands.Add(i % 3 == 0 ? Duo(i * 400, "loup") : i % 3 == 1 ? Order(i * 400, CompanionOrder.Target, "loup") : Order(i * 400, orders[random.Next(4)]));
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At(P(), P())), Companion(At(P(), P(), "0.45"), bond: random.Next(0, 101), duo: ContreJour),
            Wolf("loup", At("12", "12", "0.6"), health: 5000, firstMs: 0), Wolf("loup2", At("-12", "12", "0.6"), health: 5000, firstMs: 0) }, commands, durationMs: 6000);
        var (_, a) = Run(setup);
        var (_, b) = Run(setup);
        Assert.Equal(a.Lines, b.Lines);
        Assert.All(Lines(a, "bondGained"), g => Assert.InRange(g.Int("bond"), 0, 100));
        // Jamais d'attaque à deux sans jauge pleine ni commande : la jauge suivie ligne à ligne vaut 100 avant chaque départ, et une commande duo
        // de ce tick l'a demandée.
        int bond = ((AssailantSetup)setup.Combatants[1]).Companion!.Bond;
        foreach (JsonElement e in Events(a))
        {
            string kind = e.GetProperty("event").GetString()!;
            if (kind == "bondGained") { Assert.Equal(bond + e.Int("amount"), e.Int("bond")); bond = (int)e.Int("bond"); }
            if (kind != "duoStarted") continue;
            Assert.Equal(100, bond);
            Assert.Contains(commands, c => c.Action == CombatAction.Duo && c.AtMs / 50 == e.Tick());
            bond = 0;
            Assert.Equal(0, e.Int("bond"));
        }
    }

    [Fact]
    public void Eveille_qui_change_de_secteur_pendant_la_recuperation_d_une_attaque_a_deux_l_emporte()
    {
        // Revue Codex de la PR #169 (6e tour) : la récupération de l'attaque à deux (1 500 ms) dépasse la recharge globale ordinaire ;
        // l'état emporté qui la porte doit être admis à l'arrivée dans un autre secteur, et l'Éveillé y reste empêché jusqu'au bout.
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) }, new[] { Duo(1000, "loup") }, durationMs: 4000);
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        while (engine.CurrentTick < 21) engine.Step();   // attaque à deux partie au tick 20, récupération jusqu'au tick 50
        CarriedState carried = engine.Carried("bastion");
        Assert.True(carried.GlobalCooldownMs > Rules.GlobalCooldownMs, carried.GlobalCooldownMs.ToString());
        Assert.Equal((50 - engine.CurrentTick) * 50, carried.GlobalCooldownMs);

        CharacterSetup bastion = Player("bastion", At("0", "0"));
        var arriving = new CharacterSetup(bastion.Id, bastion.Team, bastion.Class, bastion.Build, bastion.Initiative, bastion.Placement, 500,
            bastion.Gear, bastion.Consumables, bastion.Shield, bastion.Equipment, carried);
        CombatSetup other = Fight(new CombatantSetup[] { arriving, Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            new[] { Cmd(500, Gratuite, target: "loup"), Cmd(500 + carried.GlobalCooldownMs, Gratuite, target: "loup") }, durationMs: 4000);
        Assert.Empty(Problems(other));
        var (_, log) = Run(other);
        Assert.Equal("globalCooldown", Assert.Single(Lines(log, "commandRejected")).Str("reason"));
        Assert.Contains(Lines(log, "skillCast"), e => e.Tick() == (500 + carried.GlobalCooldownMs) / 50);
        // Au-delà de la plus longue récupération possible d'une attaque à deux : refusé au chemin précis.
        var tooLong = new CharacterSetup(bastion.Id, bastion.Team, bastion.Class, bastion.Build, bastion.Initiative, bastion.Placement, 500,
            bastion.Gear, bastion.Consumables, bastion.Shield, bastion.Equipment, new CarriedState(globalCooldownMs: CompanionDuo.MaxPhaseMs + 50));
        Assert.Contains(Problems(Fight(new CombatantSetup[] { tooLong, Wolf("loup", At("1.2", "0", "0.6")) })), f => f.EndsWith("carried.globalCooldownMs", StringComparison.Ordinal));
    }

    [Fact]
    public void Instantane_pendant_une_attaque_a_deux_reprend_a_l_identique_et_refuse_une_jauge_trafiquee()
    {
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Companion(At("0", "1.2", "0.45"), bond: 100, duo: ContreJour),
            Wolf("loup", At("1.2", "0", "0.6"), health: 100_000, firstMs: 0) },
            new[] { Duo(1000, "loup"), Order(1200, CompanionOrder.Protect), Cmd(2600, Gratuite, target: "loup") }, durationMs: 4000);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 30) engine.Step();   // annonce en cours (impact au tick 36)
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.Contains("\"duoImpactTick\":36", snapshot.Text);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        while (resumed.Step()) { }
        while (engine.Step()) { }
        Assert.Equal(log.Lines.Skip(log.Lines.Count - resumedLog.Lines.Count + 1), resumedLog.Lines.Skip(1));
        Assert.Single(Lines(log, "duoLanded"));
        // Remplacements ciblés sur l'objet companion de Nyx ; l'exception précise nomme le défaut.
        string tampered = snapshot.Text.Replace("\"bond\":0,\"recalledTick\"", "\"bond\":150,\"recalledTick\"");
        Assert.NotEqual(snapshot.Text, tampered);
        Assert.Contains("jauge de lien", Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(tampered), new MemoryCombatLog())).Message);
        string early = snapshot.Text.Replace("\"duoImpactTick\":36", "\"duoImpactTick\":99");
        Assert.NotEqual(snapshot.Text, early);
        Assert.Contains("impact hors de l'annonce", Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(early), new MemoryCombatLog())).Message);
    }

    [Fact]
    public void Coup_de_l_Eveille_avant_l_arrivee_du_compagnon_instantane_restaurable()
    {
        // Revue Codex de la PR #169 (4e tour) : le compagnon arrive à 1 s ; l'Éveillé frappe avant ; l'instantané pris avant l'arrivée se restaure.
        AssailantSetup c = Companion(At("0", "1.2", "0.45"));
        var late = new AssailantSetup(c.Id, c.Team, c.Armor, c.MagicResistance, null, c.FirstAttackMs, c.IntervalMs, c.Attacks, health: c.Health,
            placement: c.Placement, arrivalMs: 1000, behavior: c.Behavior, companion: c.Companion);
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), late, Wolf("loup", At("1.2", "0", "0.6"), health: 100_000) },
            new[] { Cmd(0, Gratuite, target: "loup") }, durationMs: 3000);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 5) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        while (resumed.Step()) { }
        while (engine.Step()) { }
        Assert.Equal(log.Lines.Skip(log.Lines.Count - resumedLog.Lines.Count + 1), resumedLog.Lines.Skip(1));
    }

    [Fact]
    public void Sans_compagnon_rien_ne_change_dans_l_instantane()
    {
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Wolf("loup", At("5", "0")) }), new MemoryCombatLog());
        engine.Step();
        Assert.DoesNotContain("\"companion\"", engine.Snapshot().Text);
    }
}
