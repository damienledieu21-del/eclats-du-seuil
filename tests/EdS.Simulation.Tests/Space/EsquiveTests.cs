using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Esquive (décision de Damien du 14 novembre 2026, qui revient sur L11-D4) : bond de 3 m en 150 ms (3 ticks) dans la direction donnée ou vers
/// l'arrière, invulnérabilité de 300 ms (6 ticks) au début du bond, 20 d'endurance, 1 s (20 ticks) de recharge. Tests unitaires, adversariaux
/// et de propriétés : coup imparable esquivé au tick près, mur, bord de l'aire, endurance, recharge, garde, contrôle, créatures, atomicité,
/// instantané.
/// </summary>
[Trait("Category", "Space")]
public class EsquiveTests
{
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 };

    private static AssailantAttack Imparable(long telegraphMs = 0, AttackShape shape = AttackShape.Target, string? radius = null, long raw = 100,
        IEnumerable<StatusApplication>? applies = null, Fixed? push = null, string? range = "2.5", string? width = null) =>
        new("imparable", raw, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, unblockable: true, applies, shape, telegraphMs,
            range is null ? null : Fixed.Parse(range), radius is null ? null : Fixed.Parse(radius), width: width is null ? null : Fixed.Parse(width),
            push: push);

    /// <summary>Ours qui attaque à <paramref name="firstMs"/> (tick 20 par défaut) puis toutes les 10 s.</summary>
    private static AssailantSetup Bear(CombatantPlacement at, AssailantAttack attack, long firstMs = 1000) =>
        new("ours", "cibles", Fixed.Zero, Fixed.Zero, "bastion", firstMs, 10_000, new[] { attack }, placement: at);

    private static CombatCommand Dodge(long atMs, string? x = null, string? z = null, string actor = "bastion") =>
        CombatCommand.DodgeToward(atMs, actor, x is null ? null : V(x, z!));

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatRules? rules = null,
        CombatSpace? space = null, long durationMs = 3000)
    {
        // Un combat oppose deux équipes : un mannequin lointain quand l'essai n'a pas d'ennemi.
        List<CombatantSetup> all = combatants.ToList();
        if (all.All(c => c.Team == "joueurs")) all.Add(PlacedDummy("mannequin", At("-9", "-9")));
        return Spatial(space ?? Arena(20), all, commands, rules ?? Rules, durationMs);
    }

    private static IEnumerable<JsonElement> DamageTo(MemoryCombatLog log, string target) => Lines(log, "damage").Where(d => d.Str("target") == target);

    private static string Reason(MemoryCombatLog log) => Assert.Single(Lines(log, "commandRejected")).Str("reason")!;

    // --- Invulnérabilité au tick près ------------------------------------------------------------------------

    /// <summary>
    /// Coup dirigé imparable, impact au tick 20 (sans annonce). Une esquive au tick d (d ≤ 20) couvre les ticks d à d + 5 : l'impact est
    /// esquivé si 20 − d ≤ 5, reçu sinon. Le bond (vers le haut) garde l'ours à portée pendant les 2 premiers ticks seulement : les cas sont
    /// choisis pour que le coup soit à portée (le bond de côté reste à portée : bords à 2,5 m au plus).
    /// </summary>
    [Theory]
    [InlineData(20, true)]   // esquive au tick même de l'impact (phase des commandes avant la résolution)
    [InlineData(15, true)]   // dernier tick d'invulnérabilité : 15 + 5
    [InlineData(14, false)]  // invulnérabilité finie au tick 20
    public void Coup_imparable_esquive_au_tick_pres(long dodgeTick, bool evaded)
    {
        // Ours collé au personnage, bond vers le bas puis vers le haut du même côté : bord à bord ≤ 2,5 m pendant tout l'essai.
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Bear(At("2", "0", "1"), Imparable()) },
            new[] { Dodge(dodgeTick * 50, "0", "1") }));
        Assert.Single(Lines(log, "dodgeStarted"));
        if (evaded)
        {
            Assert.Empty(DamageTo(log, "bastion"));
            JsonElement e = Assert.Single(Lines(log, "evaded"));
            Assert.Equal((20L, "ours", "bastion", "imparable"), (e.Tick(), e.Str("source"), e.Str("target"), e.Str("attack")));
        }
        else
        {
            Assert.Empty(Lines(log, "evaded"));
            Assert.Single(DamageTo(log, "bastion"));
        }
    }

    [Fact]
    public void Ligne_dodgeStarted_avec_echeances()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Dodge(500, "1", "0") }));
        JsonElement d = Assert.Single(Lines(log, "dodgeStarted"));
        Assert.Equal((10L, 13L, 16L, 30L), (d.Tick(), d.GetProperty("endTick").GetInt64(), d.GetProperty("invulnerableUntilTick").GetInt64(),
            d.GetProperty("readyTick").GetInt64()));
    }

    [Fact]
    public void Zone_et_impulsions_esquivees_pendant_l_invulnerabilite_seulement()
    {
        // Zone persistante verrouillée sous le personnage (annonce de 500 ms : impulsions aux ticks 30, 32, 34…) ; un bond court (0,5 m) au
        // tick 28 le laisse dedans : ticks 28 à 33 invulnérables : impulsions 30 et 32 esquivées, 34 reçue.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, DodgeDistance = Fixed.Parse("0.5"), DodgeDurationMs = 50 };
        var zone = new AssailantAttack("brasier", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, true, null, AttackShape.Persistent, 500,
            radius: Fixed.FromInt(2), center: ShapeCenter.Target, persistentMs: 600, pulseMs: 100);
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Bear(At("10", "0"), zone) }, new[] { Dodge(1400, "0", "-1") }, rules));
        Assert.Equal(new[] { 30L, 32L }, Lines(log, "evaded").Select(e => e.Tick()));
        Assert.Contains(DamageTo(log, "bastion"), d => d.Tick() == 34);
        Assert.DoesNotContain(DamageTo(log, "bastion"), d => d.Tick() < 34);
    }

    [Fact]
    public void Alteration_et_poussee_esquivees()
    {
        // Bande imparable qui étourdit et repousse (annonce de 500 ms, impact au tick 30) ; bond de côté au tick 30 : 1 m, encore dans la bande.
        var stunning = Imparable(telegraphMs: 500, shape: AttackShape.Line, range: "6", width: "2", applies: new[] { new StatusApplication("stun") }, push: Fixed.FromInt(2));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Bear(At("3", "0", "1"), stunning) }, new[] { Dodge(1500, "0", "1") }));
        JsonElement landed = Assert.Single(Lines(log, "attackLanded"));
        Assert.Contains("bastion", landed.GetProperty("targets").GetRawText());
        Assert.Single(Lines(log, "evaded"));
        Assert.DoesNotContain(Lines(log, "stateApplied"), s => s.Str("combatant") == "bastion");
        JsonElement blocked = Assert.Single(Lines(log, "pushBlocked"));
        Assert.Equal("evaded", blocked.Str("reason"));
        Assert.Empty(Lines(log, "pushed"));
    }

    private static CharacterSetup Placed(CharacterSetup c, CombatantPlacement at, string? team = null) =>
        new(c.Id, team ?? c.Team, c.Class, c.Build, c.Initiative, at, c.ArrivalMs);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pas_de_transfert_vers_un_protecteur_invulnerable(bool dodges)
    {
        // Relecture indépendante de la PR #164 : le Bastion protège l'allié (Lien, 25 %) puis esquive ; le coup du tick 20 sur l'allié ne lui
        // transfère rien (ni ligne damageTransferred) : l'allié prend tout. Sans esquive, le transfert a lieu.
        var brute = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "allie", 1000, 2000,
            new[] { new AssailantAttack("coup", 100, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false) }, placement: At("0", "4"));
        var commands = new List<CombatCommand> { Support.Lot83Kit.Use(0, Support.Lot83Kit.Lien, target: "allie") };
        if (dodges) commands.Add(Dodge(950, "0", "-1"));
        var (_, log) = Run(Spatial(Arena(20), new CombatantSetup[]
        {
            Placed(Support.Lot83Kit.Bastion(), At("0", "0")), Placed(Support.Lot83Kit.Ally(), At("2", "0")), brute,
        }, commands, Support.Lot83Kit.Rules, 1100));
        JsonElement hit = Assert.Single(DamageTo(log, "allie"));
        Assert.Equal(dodges ? 0L : 25L, hit.GetProperty("transferred").GetInt64());
        Assert.Equal(dodges ? 0 : 1, Lines(log, "damageTransferred").Count);
        Assert.Equal(dodges ? 1 : 0, Lines(log, "dodgeStarted").Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Coup_esquive_sans_Resonance_pour_l_attaquant(bool dodges)
    {
        // Décision de Damien du 14 novembre 2026 (vers 23 h 58) : un coup esquivé ne donne aucune Résonance à l'attaquant (duel entre deux
        // personnages d'équipes ennemies, Coup au tick 20).
        var commands = new List<CombatCommand> { Support.Lot83Kit.Use(1000, Support.Lot83Kit.Coup, actor: "duelliste", target: "bastion") };
        if (dodges) commands.Add(Dodge(950, "0", "1"));
        var (_, log) = Run(Spatial(Arena(20), new CombatantSetup[]
        {
            Placed(Support.Lot83Kit.Bastion(), At("0", "0")),
            Placed(Support.Lot83Kit.Hero("duelliste", new[] { Support.Lot83Kit.Coup, Support.Lot83Kit.Lien, Support.Lot83Kit.Garde, Support.Lot83Kit.Barriere }), At("1.5", "0"), "rivaux"),
        }, commands, Support.Lot83Kit.Rules, 1100));
        bool Gained(JsonElement g) => g.Str("actor") == "duelliste" && g.Str("source") == "directHit";
        Assert.Equal(dodges ? 1 : 0, Lines(log, "evaded").Count);
        Assert.Equal(dodges ? 0 : 1, Lines(log, "resourceGained").Count(Gained));
    }

    // --- Bond : direction, décor, bords ---------------------------------------------------------------------

    [Fact]
    public void Bond_de_3_m_en_3_ticks_orientation_inchangee()
    {
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0", facing: V(1, 0))) }, new[] { Dodge(0, "0", "1") }));
        Assert.Equal(V("0", "1"), track.After("bastion", 1));
        Assert.Equal(V("0", "2"), track.After("bastion", 2));
        Assert.Equal(V("0", "3"), track.After("bastion", 3));
        Assert.Equal(V("0", "3"), track.After("bastion", 10));
        Assert.All(Lines(log, "moved"), m => Assert.Equal("[1,0]", m.GetProperty("combatants")[0].GetProperty("facing").GetRawText()));
    }

    [Fact]
    public void Sans_direction_vers_l_arriere_de_l_orientation()
    {
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0", facing: V(0, 1))) }, new[] { Dodge(0) }));
        Assert.Equal(V("0", "-3"), track.After("bastion", 3));
    }

    [Fact]
    public void Le_bond_remplace_la_marche_puis_la_marche_reprend()
    {
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Move(0, "bastion", "1", "0"), Dodge(50, "0", "1") }));
        Assert.Equal(V("0.25", "0"), track.After("bastion", 1));                // marche de 5 m/s : 0,25 m
        Assert.Equal(V("0.25", "3"), track.After("bastion", 4));               // bond pur sur 3 ticks
        Assert.Equal(V("0.5", "3"), track.After("bastion", 5));                // la marche reprend
    }

    [Fact]
    public void Un_bond_s_arrete_avant_un_mur()
    {
        var space = Arena(20, Rect("mur", "2", "-5", "2.2", "5"));
        var (track, _, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Dodge(0, "1", "0") }, space: space));
        GroundVector end = track.After("bastion", 5);
        Assert.True(end.X.Raw <= Fixed.Parse("1.5").Raw && end.X.Raw > Fixed.Parse("1.49").Raw, end.ToString());
        Assert.Equal(Fixed.Zero, end.Z);
        Assert.False(engine.View("bastion").IsDead);
    }

    [Fact]
    public void Un_mur_mince_n_est_jamais_traverse()
    {
        // Mur de 1 cm, bond de 3 m d'un seul tick (vitesse maximale : 3 m en 150 ms, mais un bond de 1 tick de 0,5 m sur un rayon de 0,1 m).
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, DodgeDistance = Fixed.FromInt(1), DodgeDurationMs = 50 };
        var space = Arena(20, Rect("mur", "0.5", "-5", "0.51", "5"));
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0", "0.1")) }, new[] { Dodge(0, "1", "0") }, rules, space));
        Assert.True(track.After("bastion", 2).X.Raw < Fixed.Parse("0.5").Raw);
    }

    [Fact]
    public void Collé_au_mur_refus_dodgeBlocked_atomique()
    {
        var space = Arena(20, Rect("mur", "0.5", "-5", "1", "5"));
        var (track, log, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Dodge(100, "1", "0") }, space: space));
        Assert.Equal("dodgeBlocked", Reason(log));
        Assert.Empty(Lines(log, "dodgeStarted"));
        Assert.Equal(V("0", "0"), track.After("bastion", 10));
        CombatantView me = engine.View("bastion");
        Assert.Equal((me.MaxEndurance, 0L), (me.Endurance, me.DodgeReadyTick));   // rien dépensé, pas de recharge
    }

    [Fact]
    public void Au_bord_de_l_aire_le_bond_s_arrete_au_bord()
    {
        // Aire de ±10 m : le personnage à x = 8,5 (rayon 0,5) ne dépasse pas 9,5.
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("8.5", "0")) }, new[] { Dodge(0, "1", "0") }, space: Arena(10)));
        Assert.Single(Lines(log, "dodgeStarted"));
        Assert.Equal(V("9.5", "0"), track.After("bastion", 3));
        // Déjà au bord : refus.
        var (_, again) = Run(Fight(new CombatantSetup[] { Player("bastion", At("9.5", "0")) }, new[] { Dodge(0, "1", "0") }, space: Arena(10)));
        Assert.Equal("dodgeBlocked", Reason(again));
    }

    [Fact]
    public void Une_creature_solide_arrete_le_bond()
    {
        // Règle de contact livrée (A) : une créature de grande taille est solide pour le joueur.
        var (track, _, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("ours", At("3", "0", "1")) },
            new[] { Dodge(0, "1", "0") }));
        Assert.True(track.After("bastion", 3).X.Raw <= Fixed.Parse("1.5").Raw);
    }

    // --- Refus ---------------------------------------------------------------------------------------------

    [Fact]
    public void Recharge_refus_puis_accepte_au_tick_pres()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) },
            new[] { Dodge(0, "1", "0"), Dodge(950, "-1", "0"), Dodge(1000, "-1", "0") }));
        Assert.Equal("dodgeCooldown", Reason(log));
        Assert.Equal(new[] { 0L, 20L }, Lines(log, "dodgeStarted").Select(d => d.Tick()));
    }

    [Fact]
    public void Endurance_insuffisante_refus_atomique()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, DodgeEnduranceCost = 1000, DodgeCooldownMs = 150, DodgeInvulnerableMs = 100 };
        var (track, log, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Dodge(0, "1", "0") }, rules));
        Assert.Equal("insufficientEndurance", Reason(log));
        Assert.Equal(V("0", "0"), track.After("bastion", 5));
        Assert.Equal(0L, engine.View("bastion").DodgeReadyTick);
    }

    [Fact]
    public void Endurance_depensee_et_regeneration_retardee()
    {
        var (_, log, engine) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { Dodge(0, "1", "0") }, durationMs: 500));
        JsonElement d = Assert.Single(Lines(log, "dodgeStarted"));
        CombatantView me = engine.View("bastion");
        Assert.Equal(me.MaxEndurance - 20, d.GetProperty("endurance").GetInt64());
        Assert.Equal(me.MaxEndurance - 20, me.Endurance);   // délai de régénération d'une seconde
    }

    [Fact]
    public void En_garde_refus_guardActive_puis_accepte_apres_l_avoir_quittee()
    {
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[]
        {
            new CombatCommand(0, "bastion", CombatAction.GuardStart), Dodge(50, "1", "0"),
            new CombatCommand(100, "bastion", CombatAction.GuardStop), Dodge(100, "1", "0"),
        }));
        Assert.Equal("guardActive", Reason(log));
        Assert.Equal(2L, Assert.Single(Lines(log, "dodgeStarted")).Tick());
    }

    [Fact]
    public void Etourdi_refus_stunned()
    {
        var stun = new AssailantAttack("assommer", 1, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, new[] { new StatusApplication("stun") },
            AttackShape.Target, 0, range: Fixed.FromInt(3));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Bear(At("2", "0", "1"), stun, firstMs: 500) },
            new[] { Dodge(550, "0", "1") }));
        Assert.Equal("stunned", Reason(log));
    }

    [Fact]
    public void Les_creatures_n_esquivent_pas()
    {
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("ours", At("5", "0")) }, new[] { Dodge(0, "1", "0", "ours") });
        Assert.Contains(setup.Validate(), v => v.Message.Contains("seul un personnage esquive"));
        var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("ours", At("5", "0")) }, Array.Empty<CombatCommand>()),
            new MemoryCombatLog());
        Assert.Throws<ArgumentException>(() => engine.Submit(Dodge(0, "1", "0", "ours")));
        Assert.False(engine.View("ours").CanDodge);
        Assert.True(engine.View("bastion").CanDodge);
    }

    [Fact]
    public void Sans_espace_ni_direction_invalide_refus_de_validation()
    {
        CombatSetup flat = new(1, 1000, Domain.Stats.StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, Rules,
            new CombatantSetup[] { Character("bastion", "joueurs") }, new[] { Dodge(0) }, Identity, null);
        Assert.Contains(flat.Validate(), v => v.Message.Contains("esquive dans un combat sans espace"));
        CombatSetup zero = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")) }, new[] { CombatCommand.DodgeToward(0, "bastion", V("0", "0")) });
        Assert.Contains(zero.Validate(), v => v.Fields.Any(f => f.EndsWith(".direction")));
    }

    // --- Règles -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0", 150, 300, 20, 1000, nameof(CombatRules.DodgeDistance))]
    [InlineData("10.0001", 150, 300, 20, 1000, nameof(CombatRules.DodgeDistance))]
    [InlineData("3", 0, 0, 20, 1000, nameof(CombatRules.DodgeDurationMs))]
    [InlineData("3", 100, 300, 20, 1000, nameof(CombatRules.DodgeDistance))]   // 30 m/s > 20 m/s
    [InlineData("3", 150, 1050, 20, 1000, nameof(CombatRules.DodgeInvulnerableMs))]   // invulnérabilité permanente
    [InlineData("3", 150, 100, 20, 100, nameof(CombatRules.DodgeCooldownMs))]   // recharge plus courte que le bond
    [InlineData("3", 150, 300, -1, 1000, nameof(CombatRules.DodgeEnduranceCost))]
    public void Regles_d_esquive_bornees(string distance, long duration, long invulnerable, long cost, long cooldown, string field)
    {
        var rules = new CombatRules { DodgeDistance = Fixed.Parse(distance), DodgeDurationMs = duration, DodgeInvulnerableMs = invulnerable,
            DodgeEnduranceCost = cost, DodgeCooldownMs = cooldown };
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(field));
        Assert.DoesNotContain(CombatRules.Provisional.Validate(), v => v.Fields.Any(f => f.StartsWith("Dodge")));
    }

    // --- Propriétés et reprise -------------------------------------------------------------------------------

    [Fact]
    public void Proprietes_sur_places_et_directions_aleatoires()
    {
        // Jamais plus loin que la distance, jamais dans un obstacle ni hors de l'aire ; deux exécutions donnent le même journal.
        var random = new System.Random(1414);
        for (int run = 0; run < 60; run++)
        {
            var obstacles = Enumerable.Range(0, 4).Select(k =>
            {
                int ox = random.Next(-8, 7), oz = random.Next(-8, 7);
                return Rect("o" + k, ox.ToString(), oz.ToString(), (ox + 1).ToString(), (oz + random.Next(1, 4)).ToString());
            }).ToArray();
            var space = Arena(10, obstacles);
            GroundVector start;
            do start = V(random.Next(-9, 10).ToString(), random.Next(-9, 10).ToString());
            while (space.Obstacles.Any(o => new ConvexPolygon(o.Points).OverlapsCircle(start.X.Raw, start.Z.Raw, Fixed.Parse("0.5").Raw))
                || DistanceSquaredRaw(start, V(-9, -9)) < Fixed.FromInt(2).Raw * Fixed.FromInt(2).Raw);
            string dx = (random.Next(-10, 11) / 10.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string dz = dx == "0" ? "1" : (random.Next(-10, 11) / 10.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", new CombatantPlacement(start, Fixed.Parse("0.5"), null, null)) },
                new[] { Dodge(0, dx, dz) }, space: space, durationMs: 500);
            var (track, log, _) = Trace(setup);
            GroundVector end = track.After("bastion", 5);
            Assert.True(DistanceSquaredRaw(start, end) <= Fixed.Parse("3.002").Raw * Fixed.Parse("3.002").Raw, /* glissement : dégagements de 0,3 mm */ $"essai {run} : {start} → {end}");
            Assert.DoesNotContain(space.Obstacles, o => new ConvexPolygon(o.Points).OverlapsCircle(end.X.Raw, end.Z.Raw, Fixed.Parse("0.5").Raw));
            Assert.InRange(end.X.Raw, Fixed.Parse("-9.5").Raw, Fixed.Parse("9.5").Raw);
            Assert.InRange(end.Z.Raw, Fixed.Parse("-9.5").Raw, Fixed.Parse("9.5").Raw);
            Assert.Equal(log.Lines, Run(setup).Log.Lines);
        }
    }

    [Fact]
    public void Instantane_au_milieu_du_bond_reprend_a_l_identique()
    {
        CombatSetup setup = Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Bear(At("2", "0", "1"), Imparable()) },
            new[] { Dodge(950, "0", "1") });
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 20) engine.Step();   // tick 20 à venir : bond en cours, invulnérable
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.Contains("\"dodgeStartTick\":19", snapshot.Text);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        Assert.Equal(engine.View("bastion").InvulnerableUntilTick, resumed.View("bastion").InvulnerableUntilTick);
        while (resumed.Step()) { }
        while (engine.Step()) { }
        Assert.Equal(log.Lines.Skip(log.Lines.Count - resumedLog.Lines.Count + 1), resumedLog.Lines.Skip(1));
        Assert.Single(Lines(log, "evaded"));
        // Un instantané trafiqué (esquive sans commande) est refusé.
        string tampered = snapshot.Text.Replace("\"dodgeStartTick\":19", "\"dodgeStartTick\":18");
        Assert.ThrowsAny<Exception>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(tampered), new MemoryCombatLog()));
    }
}
