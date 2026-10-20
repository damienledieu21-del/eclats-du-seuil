using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4a (L11-D5) : collisions entre combattants. Joueurs entre eux : ni blocage ni poussée. Créatures entre elles : évitement,
/// sans poussée. Joueur et créature : règle A (solides, on glisse) ou B (traversée freinée), une grande créature restant solide ; règle A,
/// garde-fou : une créature ne finit pas un pas qui laisserait un joueur sans issue. Un mort n'est solide pour personne.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aContactTests
{
    private static readonly CombatRules RuleB = new() { PlayerCreatureContact = ContactRule.SlowedCrossing };

    private static GroundVector Raw(long x, long z) => GroundVector.FromRaw(x, z);

    /// <summary>Le Bastion marche vers l'est depuis (−3, 0) vers une créature immobile de rayon <paramref name="radius"/> en (0, 0).</summary>
    private static Dictionary<string, List<GroundVector>> IntoCreature(string radius, CombatRules? rules = null, bool dummy = false)
    {
        CombatantSetup creature = dummy ? PlacedDummy("loup", At("0", "0", radius)) : Creature("loup", At("0", "0", radius));
        CombatSetup setup = Spatial(Arena(), new[] { Player("bastion", At("-3", "0")), creature }, new[] { Move(0, "bastion", "1", "0") }, rules);
        return Trace(setup).Track;
    }

    [Fact]
    public void Joueurs_se_traversent_sans_se_bloquer()
    {
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[]
            {
                Player("alice", At("-2", "0")), Player("bruno", At("2", "0")), PlacedDummy("mannequin", At("0", "8")),
            }, new[] { Move(0, "alice", "1", "0"), Move(0, "bruno", "-1", "0") });
        var (track, _, _) = Trace(setup);
        Assert.Equal(Raw(20_000, 0), track.After("alice", 16));
        Assert.Equal(Raw(-20_000, 0), track.After("bruno", 16));
        Assert.Equal(Raw(0, 0), track.After("alice", 8));   // même place au même tick : permis entre joueurs
        Assert.Equal(Raw(0, 0), track.After("bruno", 8));
    }

    [Fact]
    public void Creatures_s_evitent_sans_se_pousser()
    {
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[]
            {
                Player("bastion", At("0", "8")), Creature("loup_a", At("-3", "0", "0.6")), Creature("loup_b", At("0", "0", "0.6")),
            }, new[] { Move(0, "loup_a", "1", "0") });
        var (track, log, _) = Trace(setup);
        Assert.Equal(Raw(-12_500, 0), track.After("loup_a", 7));
        Assert.All(track["loup_a"].Skip(8), p => Assert.Equal(Raw(-12_003, 0), p));   // au contact (1,2 m) plus le dégagement
        Assert.All(track["loup_b"], p => Assert.Equal(Raw(0, 0), p));                   // jamais poussée
        Assert.DoesNotContain(Lines(log, "moved"), m => m.GetRawText().Contains("trapPrevented"));
    }

    [Fact]
    public void Regle_A_joueur_arrete_par_une_creature_sans_la_pousser()
    {
        var track = IntoCreature("0.6");
        Assert.Equal(Raw(-12_500, 0), track.After("bastion", 7));
        Assert.All(track["bastion"].Skip(8), p => Assert.Equal(Raw(-11_003, 0), p));
        Assert.All(track["loup"], p => Assert.Equal(Raw(0, 0), p));
    }

    [Fact]
    public void Regle_A_vaut_aussi_pour_un_mannequin()
    {
        var track = IntoCreature("0.6", dummy: true);
        Assert.All(track["bastion"].Skip(8), p => Assert.Equal(Raw(-11_003, 0), p));
    }

    [Fact]
    public void Regle_B_traversee_freinee_jamais_bloquee()
    {
        var track = IntoCreature("0.6", RuleB);
        // Hors contact jusqu'à −1,1 exclu ; −1,0 au tick 8 (chevauchement), puis 0,3 × 0,25 = 0,075 m par tick tant que le Bastion
        // chevauche la créature, jusqu'à 1,1 (bords qui se touchent), puis de nouveau 0,25 m par tick.
        Assert.Equal(Raw(-10_000, 0), track.After("bastion", 8));
        Assert.Equal(Raw(-9_250, 0), track.After("bastion", 9));
        Assert.Equal(Raw(11_000, 0), track.After("bastion", 36));
        Assert.Equal(Raw(13_500, 0), track.After("bastion", 37));
        Assert.All(track["loup"], p => Assert.Equal(Raw(0, 0), p));
    }

    [Fact]
    public void Grande_creature_solide_meme_en_regle_B()
    {
        var track = IntoCreature("1.2", RuleB);   // 1,2 m ≥ combat.contact.largeCreatureRadius (1 m)
        Assert.All(track["bastion"].Skip(6), p => Assert.Equal(Raw(-17_003, 0), p));
    }

    [Fact]
    public void Creature_qui_avance_s_arrete_au_contact_d_un_joueur_en_terrain_ouvert()
    {
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("3", "0", "0.6")) },
            new[] { Move(0, "loup", "-1", "0") });
        var (track, log, _) = Trace(setup);
        Assert.Equal(Raw(11_003, 0), track["loup"][^1]);
        Assert.All(track["bastion"], p => Assert.Equal(Raw(0, 0), p));
        Assert.DoesNotContain(Lines(log, "moved"), m => m.GetRawText().Contains("trapPrevented"));   // le joueur garde ses issues
    }

    [Fact]
    public void Regle_A_garde_fou_une_creature_ne_coince_pas_un_joueur_dans_un_angle()
    {
        // Bastion dans l'angle sud-ouest de l'aire, loup qui arrive en diagonale : sans garde-fou il fermerait l'angle au contact.
        CombatSetup setup = Spatial(Box("0", "0", "10", "10"), new CombatantSetup[] { Player("bastion", At("0.5", "0.5")), Creature("loup", At("3", "3", "0.6")) },
            new[] { Move(0, "loup", "-1", "-1"), Move(3000, "bastion", "1", "0") });
        var (track, log, _) = Trace(setup);
        var prevented = Lines(log, "moved").SelectMany(m => m.GetProperty("combatants").EnumerateArray())
            .Where(c => c.TryGetProperty("trapPrevented", out _)).ToList();
        Assert.NotEmpty(prevented);
        Assert.All(prevented, c => Assert.Equal(("loup", "bastion"), (c.Str("combatant"), c.Str("trapPrevented"))));
        // Tant que le Bastion attend dans l'angle, le loup est retenu là où son pas suivant l'aurait enfermé, sans contact.
        long held = Lines(log, "moved").First(m => m.GetRawText().Contains("trapPrevented")).Tick();
        long contact = 11_000L * 11_000L;
        Assert.All(Enumerable.Range((int)held, 60 - (int)held), t =>
        {
            Assert.Equal(track.After("loup", (int)held), track.After("loup", t));
            Assert.True(DistanceSquaredRaw(track.After("loup", t), track.After("bastion", t)) > contact);
        });
        // Il sort de l'angle quand il le veut (commande au tick 60) ; le loup, qui pousse toujours, le gêne sans l'enfermer.
        Assert.True(track.After("bastion", 80).X > track.After("bastion", 61).X);
    }

    [Fact]
    public void Regle_B_pas_de_garde_fou_le_joueur_n_est_jamais_bloque()
    {
        CombatSetup setup = Spatial(Box("0", "0", "10", "10"), new CombatantSetup[] { Player("bastion", At("0.5", "0.5")), Creature("loup", At("3", "3", "0.6")) },
            new[] { Move(0, "loup", "-1", "-1") }, RuleB);
        var (track, log, _) = Trace(setup);
        Assert.DoesNotContain(Lines(log, "moved"), m => m.GetRawText().Contains("trapPrevented"));
        Assert.Equal(Raw(6000, 6000), track["loup"][^1]);   // il traverse le joueur et s'arrête au bord de l'aire
    }

    [Fact]
    public void Un_mort_ne_marche_plus_et_n_est_plus_solide()
    {
        CharacterSetup killer = Player("bastion", At("0", "0"), Attrs(force: 500, vitalite: 200));
        // Un témoin immortel garde le combat ouvert après la mort du loup.
        CombatSetup setup = Spatial(Arena(), new CombatantSetup[] { killer, Creature("loup", At("2", "0", "0.6"), health: 100), Creature("temoin", At("-8", "8")) },
            new[] { Cmd(0, Frappe, target: "loup"), Move(0, "loup", "0", "1"), Move(100, "bastion", "1", "0") });
        var (track, log, engine) = Trace(setup);
        Assert.Contains(Lines(log, "died"), d => d.Str("combatant") == "loup" && d.Tick() == 0);
        Assert.Equal(Raw(20_000, 2500), track.After("loup", 1));   // a marché au tick 0, vivant jusqu'à la fin du tick
        Assert.All(track["loup"].Skip(1), p => Assert.Equal(Raw(20_000, 2500), p));
        Assert.Null(engine.View("loup").MoveDirection);
        Assert.True(track["bastion"][^1].X > Fixed.FromInt(5));   // traverse la place du mort
    }
}
