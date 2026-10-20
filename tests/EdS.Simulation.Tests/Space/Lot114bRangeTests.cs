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
/// Sous-lot 11.4b : portées et ligne de vue d'un combat positionné (COMBAT_ESPACE.md, section 4). Frontières exactes : à la portée pile,
/// on est à portée ; un dix-millième de mètre au-delà, non. Compétence à cible : bords des emprises et ligne de vue au lancement ; groupe :
/// rayon autour du lanceur au moment de l'impact ; réactions et protections : portée au moment où elles agissent.
/// </summary>
[Trait("Category", "Space")]
public class Lot114bRangeTests
{
    public const string Coup = "skill.portee.coup";        // ennemi, 2,5 m (mêlée)
    public const string Appel = "skill.portee.appel";      // ennemi, 15 m
    public const string Vague = "skill.portee.onde";        // tous les ennemis, rayon 5 m
    public const string Lien = "skill.portee.lien";        // allié, 15 m, pose le lien protecteur (soutien)
    public const string Cri = "skill.portee.cri";          // soi ; alliés et ennemis à 15 m
    public const string Libre = "skill.portee.libre";      // ennemi, sans portée
    public const string Riposte = "skill.portee.riposte";  // passif : parade parfaite → frappe la source à 2,5 m
    public const string Colosse = "skill.portee.colosse";  // passif : parade parfaite → frappe les ennemis à 5 m, puis +10 Résonance
    public const string Serment = "skill.portee.serment";  // passif : un allié touché → provoque la source à 15 m

    private static Fixed F(string value) => Fixed.Parse(value);

    private static SkillCombatDefinition Hit(string coefficient, SkillTargeting targeting, string? range, params StatusApplication[] applies) =>
        new(EdS.Domain.Stats.PowerType.Physique, EdS.Domain.Stats.DamageType.Physique, F(coefficient), 0, 0, targeting, applies: applies,
            range: range is null ? null : F(range));

    /// <summary>Classe d'essai à portées (même identifiant que la classe d'essai, pour l'identité des données des combats de test).</summary>
    public static readonly ClassDefinition Portee = new("class.essai", "Portées",
        new[] { "alpha", "beta", "gamma" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k,
            new[] { new TalentDefinition($"talent.essai.{k}.a", "A", 5) })).ToArray(),
        new[]
        {
            new SkillDefinition(Coup, "Coup", SkillKind.Active, combat: Hit("1", SkillTargeting.Enemy, "2.5")),
            new SkillDefinition(Appel, "Appel", SkillKind.Active, combat: Hit("0.1", SkillTargeting.Enemy, "15")),
            new SkillDefinition(Vague, "Onde", SkillKind.Active, combat: Hit("0.5", SkillTargeting.AllEnemies, "5")),
            new SkillDefinition(Libre, "Libre", SkillKind.Active, combat: Hit("0.1", SkillTargeting.Enemy, null)),
            new SkillDefinition(Lien, "Lien", SkillKind.Active, combat: new SkillCombatDefinition(null, null, null, 0, 0, SkillTargeting.Ally,
                applies: new[] { new StatusApplication("protectiveLink") }, range: F("15"))),
            new SkillDefinition(Cri, "Cri", SkillKind.Active, combat: new SkillCombatDefinition(null, null, null, 0, 0, SkillTargeting.Self,
                applies: new[]
                {
                    new StatusApplication("bastionAbsolute", target: ApplicationTarget.AllAllies, range: F("15")),
                    new StatusApplication("bastionAbsoluteTaunt", target: ApplicationTarget.AllEnemies, range: F("15")),
                })),
            new SkillDefinition(Riposte, "Riposte", SkillKind.Passive, triggers: new[]
            {
                new TriggerDefinition(TriggerEvent.PerfectParry, new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One, range: F("2.5")) }, TriggerRole.Target),
            }),
            new SkillDefinition(Colosse, "Colosse", SkillKind.Passive, triggers: new[]
            {
                new TriggerDefinition(TriggerEvent.PerfectParry, new[]
                {
                    TriggerEffect.Strike(TriggerTarget.AllEnemies, Fixed.One, range: F("5")),
                    TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.FromInt(10)),
                }, TriggerRole.Target, cooldownMs: 3000),
            }),
            new SkillDefinition(Serment, "Serment", SkillKind.Passive, triggers: new[]
            {
                new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "oathTaunt", range: F("15")) }, TriggerRole.Ally),
            }),
        });

    public static CharacterSetup Hero(string id, CombatantPlacement? placement, string[]? actives = null, string[]? passives = null) =>
        new(id, "joueurs", Portee, new CharacterBuild("class.essai", 10, Attrs(vitalite: 200), null,
            actives ?? new[] { Coup, Appel, Vague, Lien }, null, passives), 0, placement);

    private static CombatSetup Positioned(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatSpace? space = null,
        long durationMs = 4000) =>
        Spatial(space ?? Arena(30), combatants, commands, NoUpkeep, durationMs);

    private static List<JsonElement> Rejected(MemoryCombatLog log) => Lines(log, "commandRejected");

    private static IEnumerable<JsonElement> DamageTo(MemoryCombatLog log, string target) => Lines(log, "damage").Where(d => d.Str("target") == target);

    // --- Compétence à cible : portée au lancement -------------------------------------------------

    [Theory]
    [InlineData("3.5", "0", true)]        // bords à 2,5 m pile (deux rayons de 0,5)
    [InlineData("3.5001", "0", false)]    // 0,1 mm au-delà
    [InlineData("2.1", "2.8", true)]      // diagonale exacte (triangle 3-4-5 × 0,7) : centres à 3,5 m
    [InlineData("2.1", "2.8001", false)]
    [InlineData("-3.5", "0", true)]
    [InlineData("0", "-3.5001", false)]
    public void Portee_d_une_competence_a_cible_a_la_frontiere_exacte(string x, string z, bool accepted)
    {
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At(x, z)) },
            new[] { Cmd(0, Coup) }));
        if (accepted)
        {
            Assert.Empty(Rejected(log));
            Assert.Single(Lines(log, "skillCast"));
            Assert.Single(DamageTo(log, "mannequin"));
        }
        else
        {
            JsonElement refusal = Assert.Single(Rejected(log));
            Assert.Equal(("targetOutOfRange", Coup, "mannequin"), (refusal.Str("reason"), refusal.Str("skill"), refusal.Str("target")));
            Assert.Empty(Lines(log, "skillCast"));
            Assert.Empty(DamageTo(log, "mannequin"));
        }
    }

    [Fact]
    public void Portee_mesuree_entre_les_bords_des_emprises()
    {
        // Rayons 1,2 et 0,8 : centres à 4,5 m, bords à 2,5 m.
        var (_, near) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0", "1.2")), PlacedDummy("mannequin", At("4.5", "0", "0.8")) },
            new[] { Cmd(0, Coup) }));
        Assert.Empty(Rejected(near));
        var (_, far) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0", "1.2")), PlacedDummy("mannequin", At("4.5001", "0", "0.8")) },
            new[] { Cmd(0, Coup) }));
        Assert.Equal("targetOutOfRange", Assert.Single(Rejected(far)).Str("reason"));
    }

    [Fact]
    public void Refus_hors_de_portee_atomique_ni_cout_ni_recharge()
    {
        // Le refus ne consomme rien : la même compétence est acceptée au tick suivant une fois à portée.
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("3.75", "0")) },
            new[] { Cmd(0, Coup), Move(0, "bastion", "1", "0"), Stop(50, "bastion"), Cmd(50, Coup) }));
        Assert.Equal(("targetOutOfRange", 0L), (Assert.Single(Rejected(log)).Str("reason"), Assert.Single(Rejected(log)).Tick()));
        JsonElement cast = Assert.Single(Lines(log, "skillCast"));
        Assert.Equal(1L, cast.Tick());   // après un pas de 0,25 m : bords à 2,5 m pile
    }

    [Fact]
    public void Cible_verrouillee_au_lancement_meme_si_elle_s_eloigne_pendant_le_tick()
    {
        // L'assaillant marche dans la phase movement du tick du lancement ; l'impact (phase resolution) le touche quand même.
        var (track, log, _) = Trace(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), Creature("loup", At("3.5", "0"), target: "bastion") },
            new[] { Cmd(0, Coup, target: "loup"), Move(0, "loup", "1", "0") }));
        Assert.Empty(Rejected(log));
        Assert.Equal(0L, Assert.Single(DamageTo(log, "loup")).Tick());
        Assert.Equal(V("3.75", "0"), track.After("loup", 1));
    }

    [Fact]
    public void Sans_portee_une_competence_a_cible_atteint_toute_l_aire()
    {
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("-29", "-29"), new[] { Libre }), PlacedDummy("mannequin", At("29", "29")) },
            new[] { Cmd(0, Libre) }));
        Assert.Empty(Rejected(log));
        Assert.Single(DamageTo(log, "mannequin"));
    }

    // --- Ligne de vue -------------------------------------------------------------------------------

    [Fact]
    public void Mur_entre_le_lanceur_et_sa_cible_refus_sans_ligne_de_vue()
    {
        CombatSpace walled = Arena(30, Rect("mur", "5", "-2", "6", "2"));
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("10", "0")) },
            new[] { Cmd(0, Appel) }, walled));
        JsonElement refusal = Assert.Single(Rejected(log));
        Assert.Equal(("noLineOfSight", Appel), (refusal.Str("reason"), refusal.Str("skill")));
        // Contourné par le haut : ligne de vue rétablie.
        var (_, clear) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("10", "5")) },
            new[] { Cmd(0, Appel) }, walled));
        Assert.Empty(Rejected(clear));
    }

    [Fact]
    public void Ligne_de_vue_qui_effleure_un_bord_est_coupee()
    {
        // Le segment des centres (z = 0) longe le bord inférieur du rocher : il le touche, donc la vue est coupée ; 0,1 mm plus haut, non.
        var (_, touching) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "-1")), PlacedDummy("mannequin", At("10", "-1")) },
            new[] { Cmd(0, Appel) }, Arena(30, Rect("rocher", "4", "-1", "6", "3"))));
        Assert.Equal("noLineOfSight", Assert.Single(Rejected(touching)).Str("reason"));
        var (_, clear) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "-1")), PlacedDummy("mannequin", At("10", "-1")) },
            new[] { Cmd(0, Appel) }, Arena(30, Rect("rocher", "4", "-0.9999", "6", "3"))));
        Assert.Empty(Rejected(clear));
    }

    [Fact]
    public void Ligne_de_vue_qui_passe_par_un_sommet_est_coupee()
    {
        // Losange dont le sommet bas est sur le segment des centres.
        var diamond = new SpaceObstacle("pierre", new[] { V("5", "0"), V("6", "1"), V("5", "2"), V("4", "1") });
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("10", "0")) },
            new[] { Cmd(0, Appel) }, Arena(30, diamond)));
        Assert.Equal("noLineOfSight", Assert.Single(Rejected(log)).Str("reason"));
    }

    [Fact]
    public void Portee_controlee_avant_la_ligne_de_vue()
    {
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("20", "0")) },
            new[] { Cmd(0, Appel) }, Arena(30, Rect("mur", "5", "-2", "6", "2"))));
        Assert.Equal("targetOutOfRange", Assert.Single(Rejected(log)).Str("reason"));
    }

    [Fact]
    public void Ligne_de_vue_exigee_aussi_d_une_competence_sans_portee_et_d_un_soutien_sur_un_allie()
    {
        CombatSpace walled = Arena(30, Rect("mur", "3", "-2", "4", "2"));
        var (_, enemy) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0"), new[] { Libre }), PlacedDummy("mannequin", At("8", "0")) },
            new[] { Cmd(0, Libre) }, walled));
        Assert.Equal("noLineOfSight", Assert.Single(Rejected(enemy)).Str("reason"));
        var (_, ally) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), Hero("allie", At("8", "0")), PlacedDummy("mannequin", At("0", "10")) },
            new[] { Cmd(0, Lien, target: "allie") }, walled));
        Assert.Equal("noLineOfSight", Assert.Single(Rejected(ally)).Str("reason"));
    }

    // --- Soutien : allié à portée, le lanceur toujours atteignable -----------------------------------

    [Fact]
    public void Soutien_sur_un_allie_a_portee_ou_sur_soi()
    {
        // Allié aux bords à 15 m pile : accepté ; 0,1 mm au-delà : refusé ; sur soi : toujours accepté.
        var (_, near) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), Hero("allie", At("16", "0")), PlacedDummy("mannequin", At("0", "10")) },
            new[] { Cmd(0, Lien, target: "allie") }));
        Assert.Empty(Rejected(near));
        Assert.Equal("allie", Assert.Single(Lines(near, "stateApplied")).Str("combatant"));
        var (_, far) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), Hero("allie", At("16.0001", "0")), PlacedDummy("mannequin", At("0", "10")) },
            new[] { Cmd(0, Lien, target: "allie"), Cmd(0, Lien, actor: "allie", target: "allie") }));
        JsonElement refusal = Assert.Single(Rejected(far));
        Assert.Equal(("targetOutOfRange", "bastion"), (refusal.Str("reason"), refusal.Str("actor")));
    }

    // --- Compétences de groupe : rayon au moment de l'impact -------------------------------------------

    [Fact]
    public void Onde_touche_les_ennemis_dont_l_emprise_touche_le_cercle()
    {
        // Rayon 5 autour du centre du lanceur ; emprises de 0,5 : un ennemi compte si son centre est à 5,5 m au plus.
        var (_, log) = Run(Positioned(new CombatantSetup[]
        {
            Hero("bastion", At("0", "0")),
            PlacedDummy("a", At("5.5", "0")), PlacedDummy("b", At("0", "-5.5001")), PlacedDummy("c", At("-3.3", "4.4")), PlacedDummy("d", At("3.3", "4.4001")),
        }, new[] { Cmd(0, Vague, target: null) }));
        Assert.Empty(Rejected(log));
        Assert.Equal(new[] { "a", "c" }, Lines(log, "damage").Select(d => d.Str("target")).ToArray());
    }

    [Fact]
    public void Onde_jugee_a_l_impact_apres_les_deplacements_du_tick()
    {
        // L'assaillant à la frontière s'éloigne dans la phase movement du tick du lancement : l'onde (même tick, phase resolution) le
        // manque ; s'il reste, elle le touche.
        CombatantSetup[] Fighters() => new CombatantSetup[] { Hero("bastion", At("0", "0")), Creature("loup", At("5.5", "0")) };
        var (_, stays) = Run(Positioned(Fighters(), new[] { Cmd(0, Vague, target: null) }));
        Assert.Single(DamageTo(stays, "loup"));
        var (_, leaves) = Run(Positioned(Fighters(), new[] { Cmd(0, Vague, target: null), Move(0, "loup", "1", "0") }));
        Assert.Single(Lines(leaves, "skillCast"));
        Assert.Empty(DamageTo(leaves, "loup"));
        // L'inverse : il entre dans le rayon pendant le tick.
        var (_, enters) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), Creature("loup", At("5.75", "0")) },
            new[] { Cmd(0, Vague, target: null), Move(0, "loup", "-1", "0") }));
        Assert.Single(DamageTo(enters, "loup"));
    }

    [Fact]
    public void Onde_sans_ennemi_dans_le_rayon_lancee_sans_cible_ni_resonance()
    {
        var (result, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("20", "0")) },
            new[] { Cmd(0, Vague, target: null) }));
        Assert.Single(Lines(log, "skillCast"));
        Assert.Empty(Lines(log, "damage"));
        Assert.Equal(0, result["bastion"].Resonance);
    }

    [Fact]
    public void Onde_traverse_le_decor()
    {
        // La ligne de vue ne vaut que pour une compétence à cible (COMBAT_ESPACE.md, section 1) : l'onde atteint un ennemi derrière un mur.
        var (_, log) = Run(Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("4.5", "0")) },
            new[] { Cmd(0, Vague, target: null) }, Arena(30, Rect("mur", "1.5", "-2", "2", "2"))));
        Assert.Single(DamageTo(log, "mannequin"));
    }

    [Fact]
    public void Applications_de_groupe_limitees_a_leur_rayon()
    {
        // Cri : renforcement des alliés et provocation des ennemis dans un rayon de 15 m ; le lanceur est toujours son propre allié.
        var (_, log) = Run(Positioned(new CombatantSetup[]
        {
            Hero("bastion", At("0", "0"), new[] { Cri }),
            Hero("proche", At("15.5", "0")), Hero("loin", At("-15.5001", "0")),
            Creature("loup", At("0", "15.5")), Creature("ours", At("0", "-15.5001")),
        }, new[] { Cmd(0, Cri, target: null) }));
        Assert.Empty(Rejected(log));
        var applied = Lines(log, "stateApplied").Select(s => (s.Str("state")!, s.Str("combatant")!)).ToArray();
        Assert.Equal(new[] { ("bastionAbsolute", "bastion"), ("bastionAbsolute", "proche"), ("bastionAbsoluteTaunt", "loup") }, applied);
    }

    // --- Réactions : portée au moment où elles agissent ------------------------------------------------

    private static CombatSetup Parried(string passive, params CombatantSetup[] creatures) =>
        Positioned(new CombatantSetup[] { Hero("bastion", At("0", "0"), new[] { Coup }, new[] { passive }) }.Concat(creatures),
            new[] { Parry(1000) }, durationMs: 1500);

    [Theory]
    [InlineData("3.5", true)]
    [InlineData("3.5001", false)]
    public void Riposte_seulement_contre_un_attaquant_a_portee(string x, bool strikes)
    {
        var (_, log) = Run(Parried(Riposte, Creature("loup", At(x, "0"), firstAttackMs: 1000)));
        Assert.Single(Lines(log, "parrySucceeded"));
        if (strikes)
        {
            Assert.Single(DamageTo(log, "loup"));
            Assert.Empty(Lines(log, "reactionCancelled"));
        }
        else
        {
            Assert.Empty(DamageTo(log, "loup"));
            JsonElement cancelled = Assert.Single(Lines(log, "reactionCancelled"));
            Assert.Equal("outOfRange", cancelled.Str("reason"));
        }
    }

    [Fact]
    public void Riposte_de_zone_seulement_sur_les_ennemis_du_rayon()
    {
        var (result, log) = Run(Parried(Colosse,
            Creature("loup", At("5.5", "0"), firstAttackMs: 1000), Creature("ours", At("-5.5001", "0")), Creature("lynx", At("0", "5.5"))));
        Assert.Equal(new[] { "loup", "lynx" }, Lines(log, "damage").Where(d => d.Str("source") == "bastion").Select(d => d.Str("target")).ToArray());
        Assert.Equal(NoUpkeep.ResonancePerPerfectParry + 10, result["bastion"].Resonance);   // parade parfaite, puis gain de la réaction
    }

    [Fact]
    public void Riposte_de_zone_sans_ennemi_du_rayon_annulee_gain_compris()
    {
        // Règle existante (§8 des déclencheurs) : un premier effet sans cible annule la réaction entière ; ici parce que la portée a
        // écarté tous les ennemis (outOfRange), donc pas de Résonance non plus.
        var (result, log) = Run(Parried(Colosse, Creature("loup", At("5.5001", "0"), firstAttackMs: 1000)));
        Assert.Equal("outOfRange", Assert.Single(Lines(log, "reactionCancelled")).Str("reason"));
        Assert.Equal(NoUpkeep.ResonancePerPerfectParry, result["bastion"].Resonance);   // la parade parfaite seule
    }

    [Theory]
    [InlineData("16", true)]
    [InlineData("16.0001", false)]
    public void Serment_provoque_la_source_a_portee_du_porteur(string x, bool taunts)
    {
        // L'assaillant frappe l'allié ; le porteur (bastion) provoque l'assaillant s'il est à 15 m de lui (bords des emprises).
        var (_, log) = Run(Positioned(new CombatantSetup[]
        {
            Hero("bastion", At("0", "0"), new[] { Coup }, new[] { Serment }), Hero("allie", At("0", "5")),
            Creature("loup", At(x, "0"), target: "allie", firstAttackMs: 1000),
        }, Array.Empty<CombatCommand>(), durationMs: 1500));
        Assert.Single(DamageTo(log, "allie"));
        if (taunts)
        {
            JsonElement taunt = Assert.Single(Lines(log, "stateApplied"));
            Assert.Equal(("oathTaunt", "loup", "bastion"), (taunt.Str("state"), taunt.Str("combatant"), taunt.Str("source")));
        }
        else
        {
            Assert.Empty(Lines(log, "stateApplied"));
            Assert.Equal("outOfRange", Assert.Single(Lines(log, "reactionCancelled")).Str("reason"));
        }
    }

    // --- Protection : transfert seulement à portée ------------------------------------------------------

    [Fact]
    public void Lien_protecteur_transfere_seulement_a_portee_et_reste_pose()
    {
        // Lien posé à 15 m pile ; l'allié s'éloigne pendant 1 s (5 m), est frappé hors de portée (aucun transfert, lien toujours posé),
        // revient, puis est frappé à nouveau à portée (transfert).
        var (_, log) = Run(Positioned(new CombatantSetup[]
        {
            Hero("bastion", At("0", "0")), Hero("allie", At("16", "0")),
            Creature("loup", At("16", "10"), target: "allie", firstAttackMs: 1000),
        }, new[] { Cmd(0, Lien, target: "allie"), Move(0, "allie", "1", "0"), Move(1000, "allie", "-1", "0"), Stop(2000, "allie") }, durationMs: 3500));
        Assert.Empty(Rejected(log));
        Assert.Equal("protectiveLink", Assert.Single(Lines(log, "stateApplied")).Str("state"));
        Assert.Equal(new[] { 20L, 60L }, DamageTo(log, "allie").Select(d => d.Tick()).ToArray());
        JsonElement transfer = Assert.Single(Lines(log, "damageTransferred"));
        Assert.Equal((60L, "allie", "bastion"), (transfer.Tick(), transfer.Str("protected"), transfer.Str("target")));
        Assert.True(transfer.Int("amount") > 0);
        Assert.Equal(new[] { 0L, transfer.Int("amount") }, DamageTo(log, "allie").Select(d => d.Int("transferred")).ToArray());
        Assert.Empty(Lines(log, "stateRemoved"));
        Assert.Empty(Lines(log, "stateExpired"));
    }

    [Fact]
    public void Protection_hors_de_portee_au_tick_de_dot_ne_transfere_rien()
    {
        // Un tick de DOT sur le protégé suit la même règle que l'impact (coût exact compris). Saignement posé à chaque coup (toutes les
        // 2 s), ticks toutes les 2 s ; lien posé à 15 m pile ; l'allié s'écarte de 1,25 m entre 2,5 s et 2,75 s : avant, impacts et ticks
        // transfèrent ; après, plus rien.
        AssailantAttack bleeding = Strike("morsure", 100, applies: Apply("bleed", "0.4"));
        var (_, log) = Run(Positioned(new CombatantSetup[]
        {
            Hero("bastion", At("0", "0")), Hero("allie", At("16", "0")),
            Creature("loup", At("16", "10"), target: "allie", firstAttackMs: 0, attacks: new[] { bleeding }),
        }, new[] { Cmd(0, Lien, target: "allie"), Move(2500, "allie", "1", "0"), Stop(2750, "allie") }, durationMs: 4500));
        var dots = Lines(log, "dotDamage").Where(d => d.Str("target") == "allie").ToList();
        var hits = DamageTo(log, "allie").ToList();
        Assert.Contains(dots, d => d.Tick() < 50);
        Assert.Contains(dots, d => d.Tick() > 55);
        Assert.Contains(hits, d => d.Tick() > 55);
        Assert.All(dots.Concat(hits), d => Assert.Equal(d.Tick() < 50, d.Int("transferred") > 0));
        Assert.All(Lines(log, "damageTransferred"), t => Assert.True(t.Tick() < 50));
    }

    // --- Sans espace : portées ignorées --------------------------------------------------------------------

    [Fact]
    public void Combat_sans_espace_ignore_les_portees()
    {
        var setup = Setup(new[] { Cmd(0, Coup), Cmd(1000, Appel), Cmd(2000, Vague, target: null) },
            new CombatantSetup[] { Hero("bastion", null), Dummy() }, durationMs: 4000);
        var (_, log) = Run(setup);
        Assert.Empty(Rejected(log));
        Assert.Equal(3, Lines(log, "damage").Count);
    }
}
