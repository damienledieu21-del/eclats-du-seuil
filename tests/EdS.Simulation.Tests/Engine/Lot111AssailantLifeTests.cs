using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>
/// Sous-lot 11.1 : assaillants mortels. Avec des PV (<see cref="AssailantSetup.Health"/>), un assaillant meurt à 0 PV comme un
/// personnage : la mort est un état de la simulation (plus aucune attaque, plus une cible valide, une seule ligne <c>died</c>) et la
/// victoire peut revenir aux joueurs. Sans PV, il reste l'assaillant d'entraînement aux PV infinis (A21).
/// </summary>
[Trait("Category", "Engine")]
public class Lot111AssailantLifeTests
{
    /// <summary>Assaillant mortel : <paramref name="health"/> PV, armure nulle, une attaque de 100 toutes les <paramref name="intervalMs"/>.</summary>
    private static AssailantSetup Mortal(long health, string id = "loup", long firstAttackMs = 1000, long intervalMs = 2000, string? target = "bastion",
        string team = "cibles", params AssailantAttack[] attacks) =>
        new(id, team, Fixed.Zero, Fixed.Zero, target, firstAttackMs, intervalMs, attacks.Length == 0 ? new[] { Blow() } : attacks, health: health);

    /// <summary>Personnage qui tue d'un coup (Force 500 : Frappe à 1200 environ sans armure).</summary>
    private static CharacterSetup Killer(string id = "bastion") => Character(id, attributes: Attrs(force: 500, vitalite: 200));

    private static List<JsonElement> Of(MemoryCombatLog log, string kind) => Events(log).OfKind(kind).ToList();

    [Fact]
    public void Assaillant_mortel_meurt_a_zero_PV_une_seule_fois()
    {
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, target: "loup") }, new CombatantSetup[] { Killer(), Mortal(300) }));
        var damage = Assert.Single(Of(log, "damage"));
        Assert.Equal((0L, true), (damage.Int("health"), damage.Int("overkill") > 0));
        var died = Assert.Single(Of(log, "died"));
        Assert.Equal(("loup", 0L), (died.Str("combatant"), died.Tick()));
        Assert.Equal(damage.Int("eid"), died.GetProperty("cause").GetInt64());   // provenance (A6) : causée par le coup mortel
        Assert.True(result["loup"].IsDead);
        Assert.Equal((0L, 300L, 0L), (result["loup"].Health, result["loup"].MaxHealth, result["loup"].DeathTick));
    }

    [Fact]
    public void Ligne_combatant_et_en_tete_portent_les_PV_d_un_assaillant_mortel_seulement()
    {
        var (_, log) = Run(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Mortal(250), Assailant(id: "mannequin_vivant") }));
        var combatants = Of(log, "combatant").ToDictionary(c => c.Str("combatant")!);
        Assert.Equal((250L, 250L), (combatants["loup"].Int("health"), combatants["loup"].Int("maxHealth")));
        Assert.Equal(JsonValueKind.Null, combatants["mannequin_vivant"].GetProperty("health").ValueKind);
        JsonElement header = JsonDocument.Parse(log.Lines[0]).RootElement;
        JsonElement[] described = header.GetProperty("scenario").GetProperty("combatants").EnumerateArray().ToArray();
        Assert.Equal(250, described.Single(c => c.GetProperty("id").GetString() == "loup").GetProperty("health").GetInt64());
        Assert.False(described.Single(c => c.GetProperty("id").GetString() == "mannequin_vivant").TryGetProperty("health", out _));
        // Format 9 au sous-lot 11.1 ; le format courant (10 depuis le 11.4a) garde ces champs.
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version), (header.GetProperty("formatVersion").GetInt32(), header.GetProperty("engineVersion").GetString()));
        Assert.True(CombatEngine.LogFormatVersion >= 9);
    }

    [Fact]
    public void Assaillant_mort_n_attaque_plus_et_son_tour_programme_est_abandonne_sans_ligne()
    {
        // Le loup attaque à 1 s puis toutes les secondes ; il meurt à 2,5 s (tick 50).
        var (result, log) = Run(Setup(new[] { Cmd(2500, Frappe, target: "loup") },
            new CombatantSetup[] { Killer(), Mortal(300, firstAttackMs: 1000, intervalMs: 1000) }, durationMs: 10_000));
        var died = Assert.Single(Of(log, "died"));
        Assert.Equal(50, died.Tick());
        List<JsonElement> attacks = Of(log, "attackStarted");
        Assert.Equal(new long[] { 20, 40 }, attacks.Select(a => a.Tick()));   // 1 s et 2 s ; plus rien après la mort
        Assert.DoesNotContain(Events(log), e => e.TryGetProperty("actor", out JsonElement a) && a.GetString() == "loup" && e.Tick() > 50);
        Assert.Empty(Of(log, "attackSkipped"));   // tour périmé : aucune ligne
        Assert.Equal(CombatEndReason.TeamDefeated, result.EndReason);
        Assert.Equal(("joueurs", 50L), (result.Winner, result.EndTick));
    }

    [Fact]
    public void Attaque_et_mort_au_meme_tick_l_attaque_partie_est_resolue()
    {
        // Même tick : le tour du loup (1 s) et le coup du Bastion. L'attaque lancée en phase de commandes est résolue dans le tick ; la
        // mort n'est relevée qu'en fin de tick (A10).
        var (result, log) = Run(Setup(new[] { Cmd(1000, Frappe, target: "loup") },
            new CombatantSetup[] { Killer(), Mortal(300, firstAttackMs: 1000, intervalMs: 1000) }));
        Assert.Equal(20, Assert.Single(Of(log, "attackStarted")).Tick());
        Assert.Contains(Of(log, "damage"), d => d.Str("source") == "loup" && d.Tick() == 20);
        Assert.Equal(20, Assert.Single(Of(log, "died")).Tick());
        Assert.Equal("joueurs", result.Winner);
    }

    [Fact]
    public void Vue_du_pilote_sans_prochain_coup_apres_la_mort()
    {
        var engine = new CombatEngine(Setup(new[] { Cmd(500, Frappe, target: "loup") }, new CombatantSetup[] { Killer(), Mortal(300, firstAttackMs: 1000) }),
            new MemoryCombatLog());
        Assert.Equal(20, engine.View("loup").NextAttackTick);
        while (!engine.View("loup").IsDead && engine.Step()) { }
        CombatantView dead = engine.View("loup");
        Assert.True(dead.IsDead);
        Assert.Null(dead.NextAttackTick);
        Assert.Equal(0, dead.Health);
    }

    [Fact]
    public void Assaillant_mort_n_est_plus_une_cible()
    {
        // Deux loups ; le premier meurt à 0 s. Une frappe sur lui est refusée (targetDead), l'onde ne touche plus que le second.
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, target: "loup"), Cmd(1000, Gratuite, target: "loup"), Cmd(2000, Onde, target: null) },
            new CombatantSetup[] { Killer(), Mortal(300), Mortal(1_000_000, id: "loup2", firstAttackMs: 1500) }));
        var rejected = Assert.Single(Of(log, "commandRejected"));
        Assert.Equal(("loup", "targetDead"), (rejected.Str("target"), rejected.Str("reason")));
        List<JsonElement> onde = Of(log, "damage").Where(d => d.Tick() == 40).ToList();
        Assert.Equal(new[] { "loup2" }, onde.Select(d => d.Str("target")));
        Assert.False(result["loup2"].IsDead);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
    }

    [Fact]
    public void Victoire_des_joueurs_quand_tous_les_ennemis_meurent()
    {
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, target: "loup"), Cmd(1000, Heurt, target: "loup2") },
            new CombatantSetup[] { Killer(), Mortal(300), Mortal(300, id: "loup2") }));
        Assert.Equal(new[] { "loup", "loup2" }, Of(log, "died").Select(d => d.Str("combatant")));
        Assert.Equal(CombatEndReason.TeamDefeated, result.EndReason);
        Assert.Equal(("joueurs", 20L), (result.Winner, result.EndTick));
        var ended = Assert.Single(Of(log, "combatEnded"));
        Assert.Equal(("teamDefeated", "joueurs"), (ended.Str("reason"), ended.Str("winner")));
    }

    [Fact]
    public void Un_ennemi_aux_PV_infinis_garde_son_equipe_en_vie()
    {
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, target: "loup") }, new CombatantSetup[] { Killer(), Mortal(300), Assailant(id: "totem") }));
        Assert.Single(Of(log, "died"));
        Assert.False(result["totem"].IsDead);
        Assert.Null(result["totem"].Health);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.Null(result.Winner);
    }

    [Fact]
    public void Assaillant_sans_PV_reste_immortel_comme_avant()
    {
        var commands = Enumerable.Range(0, 5).Select(i => Cmd(i * 2000L, Frappe, target: "assaillant"));
        var (result, log) = Run(Setup(commands, new CombatantSetup[] { Killer(), Assailant() }));
        Assert.Empty(Of(log, "died"));
        Assert.False(result["assaillant"].IsDead);
        Assert.Null(result["assaillant"].MaxHealth);
        Assert.True(result["assaillant"].DamageTaken > 1000);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
    }

    [Fact]
    public void Deux_coups_mortels_au_meme_tick_une_seule_mort()
    {
        var (result, log) = Run(Setup(new[] { Cmd(0, Frappe, "bastion", "loup"), Cmd(0, Frappe, "bastion2", "loup") },
            new CombatantSetup[] { Killer(), Killer("bastion2"), Mortal(300) }));
        Assert.Equal(2, Of(log, "damage").Count(d => d.Str("target") == "loup"));
        Assert.Single(Of(log, "died"));
        Assert.Equal(0, result["loup"].Health);
        Assert.Equal("joueurs", result.Winner);
    }

    [Fact]
    public void Mort_par_saignement_cause_par_le_tick_de_DOT()
    {
        // Entaille : 0,5 et un saignement ; le loup n'a que 1 PV de plus que le coup direct : un tick de saignement l'achève.
        CharacterSetup bleeder = Tank(actives: new[] { Entaille, Frappe, Heurt, Onde });
        var probe = Run(Setup(new[] { Cmd(0, Entaille, target: "loup") }, new CombatantSetup[] { bleeder, Mortal(1_000_000) })).Log;
        long direct = Of(probe, "damage").Single(d => d.Str("target") == "loup").Int("amount");
        var (result, log) = Run(Setup(new[] { Cmd(0, Entaille, target: "loup") }, new CombatantSetup[] { bleeder, Mortal(direct + 1) }));
        var died = Assert.Single(Of(log, "died"));
        JsonElement dot = Of(log, "dotDamage").First();
        Assert.Equal(dot.Int("eid"), died.GetProperty("cause").GetInt64());
        Assert.Equal(dot.Tick(), died.Tick());
        Assert.True(result["loup"].IsDead);
    }

    [Fact]
    public void Mort_retire_les_etats_portes_par_l_assaillant()
    {
        // Assommer étourdit le loup (1,5 s) ; la Frappe suivante, à 1 s, le tue : l'étourdissement est retiré avec la raison death.
        var (_, log) = Run(Setup(new[] { Cmd(0, Assommer, target: "loup"), Cmd(1000, Frappe, target: "loup") },
            new CombatantSetup[] { Character(attributes: Attrs(force: 14), actives: new[] { Assommer, Frappe, Heurt, Onde }), Mortal(5) }));
        var died = Assert.Single(Of(log, "died"));
        Assert.Equal(20, died.Tick());
        Assert.Contains(Of(log, "stateRemoved"), s => s.Str("combatant") == "loup" && s.Str("state") == "stun" && s.Str("reason") == "death" && s.Tick() == 20);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(AssailantSetup.MaxHealth + 1)]
    public void PV_hors_bornes_refuses_avec_le_chemin(long health)
    {
        RuleViolation violation = Assert.Single(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Mortal(health) }).Validate());
        Assert.Equal("combatants[1].health", Assert.Single(violation.Fields));
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(AssailantSetup.MaxHealth)]
    public void PV_aux_bornes_admis(long health)
    {
        CombatSetup setup = Setup(new[] { Cmd(0, Frappe, target: "loup") }, new CombatantSetup[] { Killer(), Mortal(health) });
        Assert.Empty(setup.Validate());
        var (result, _) = Run(setup);
        Assert.Equal(health == 1, result["loup"].IsDead);
    }

    [Fact]
    public void Proprietes_sur_combats_aleatoires()
    {
        // Pour 200 combats tirés au hasard (graine fixe) : une mort au plus par combattant ; aucune ligne d'un assaillant après sa mort ;
        // plus aucun ennemi vivant ⇔ victoire des joueurs ; même combat, même journal.
        var random = new Random(1101);
        int withDeaths = 0, victories = 0, defeats = 0;
        for (int n = 0; n < 200; n++)
        {
            int count = random.Next(1, 4);
            var combatants = new List<CombatantSetup>
            {
                Character(attributes: Attrs(force: random.Next(20, 300), vitalite: random.Next(80, 400)), actives: new[] { Frappe, Onde, Entaille, Assommer }),
            };
            for (int k = 0; k < count; k++)
                combatants.Add(Mortal(random.Next(1, 2000), id: "loup" + k, firstAttackMs: 50L * random.Next(0, 40), intervalMs: 50L * random.Next(4, 60)));
            string[] skills = { Frappe, Onde, Entaille, Assommer };
            var commands = Enumerable.Range(0, random.Next(1, 30))
                .Select(i => 50L * random.Next(0, 200))
                .Distinct().OrderBy(t => t)
                .Select(t => { string skill = skills[random.Next(skills.Length)]; return Cmd(t, skill, target: skill == Onde ? null : "loup" + random.Next(count)); })
                .ToArray();
            CombatSetup setup = Setup(commands, combatants, seed: n, durationMs: 10_000);
            Assert.Empty(setup.Validate());
            var (result, log) = Run(setup);
            List<JsonElement> events = Events(log);
            var deaths = events.OfKind("died").GroupBy(d => d.Str("combatant")!).ToDictionary(g => g.Key, g => Assert.Single(g).Tick());
            foreach (var (id, tick) in deaths)
                Assert.DoesNotContain(events, e => e.Tick() > tick && e.TryGetProperty("actor", out JsonElement a) && a.GetString() == id);
            bool allEnemiesDead = Enumerable.Range(0, count).All(k => result["loup" + k].IsDead);
            Assert.Equal(allEnemiesDead && !result["bastion"].IsDead, result.Winner == "joueurs");
            Assert.Equal(log.ToString(), Run(setup).Log.ToString());
            if (deaths.Keys.Any(id => id != "bastion")) withDeaths++;
            if (result.Winner == "joueurs") victories++;
            if (result.Winner == "cibles") defeats++;
        }
        // L'échantillon couvre bien les cas : des morts d'ennemis, des victoires et des défaites.
        Assert.True(withDeaths >= 80 && victories >= 30 && defeats >= 20, $"{withDeaths} combats avec une mort d'ennemi, {victories} victoires, {defeats} défaites");
    }
}
