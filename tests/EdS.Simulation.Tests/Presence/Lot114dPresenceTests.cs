using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Space;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Presence.PresenceKit;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Presence;

/// <summary>
/// Sous-lot 11.4d : combattants qui arrivent et partent en cours de combat. Arrivée au début de son tick (phase setup), refusée si une
/// emprise solide occupe la place ; départ en fin de tick, après les morts ; un absent n'agit pas, n'est pas ciblé et ne coûte rien.
/// </summary>
[Trait("Category", "Presence")]
public class Lot114dPresenceTests
{
    private static List<JsonElement> Lines(MemoryCombatLog log, string kind) => Events(log).OfKind(kind).ToList();

    private static JsonElement Summary(MemoryCombatLog log, string id) => Lines(log, "combatantSummary").Single(s => s.Str("combatant") == id);

    private static IEnumerable<JsonElement> DamageTo(MemoryCombatLog log, string target) => Lines(log, "damage").Where(d => d.Str("target") == target);

    // --- Arrivées --------------------------------------------------------------------------------------------

    [Fact]
    public void Un_renfort_arrive_au_debut_de_son_tick_et_n_existe_pas_avant()
    {
        var (result, log) = Run(Setup(new[]
        {
            Cmd(1000, Gratuite, "renfort"),   // avant son arrivée : refusée
            Cmd(2000, Gratuite, "renfort"),   // au tick de son arrivée : acceptée
            Cmd(1000, Gratuite, "bastion", "second"),    // un ennemi pas encore arrivé n'est pas une cible
        }, new CombatantSetup[]
        {
            Tank(), Tank("renfort").Arriving(2000), Dummy(), Dummy("second").Arriving(2000), Assailant(firstAttackMs: 1000, intervalMs: 2000, target: "renfort"),
        }, durationMs: 4000));
        Assert.Equal(new[] { "bastion", "mannequin", "assaillant" }, Lines(log, "combatant").Select(c => c.Str("combatant")));
        Assert.Equal(new[] { "renfort", "second" }, Lines(log, "combatantJoined").Select(j => j.Str("combatant")));   // ordre du scénario
        JsonElement joined = Lines(log, "combatantJoined")[0];
        Assert.Equal((40L, "setup", "renfort", "character", "arrival:renfort"),
            (joined.Tick(), joined.Str("phase"), joined.Str("combatant"), joined.Str("type"), joined.Str("root")));
        Assert.Equal(JsonValueKind.Null, joined.GetProperty("cause").ValueKind);
        var rejected = Lines(log, "commandRejected");
        Assert.Contains(rejected, r => (r.Str("actor"), r.Str("reason"), r.Tick()) == ("renfort", "actorAbsent", 20));
        Assert.Contains(rejected, r => (r.Str("actor"), r.Str("target"), r.Str("reason"), r.Tick()) == ("bastion", "second", "targetAbsent", 20));
        Assert.Single(Lines(log, "skillCast"), c => c.Str("actor") == "renfort" && c.Tick() == 40);
        // L'assaillant vise le renfort : absent au tick 20 (attaque sautée), frappé au tick 60.
        JsonElement skipped = Assert.Single(Lines(log, "attackSkipped"));
        Assert.Equal((20L, "renfort", "targetAbsent"), (skipped.Tick(), skipped.Str("target"), skipped.Str("reason")));
        Assert.Equal(new[] { 60L }, DamageTo(log, "renfort").Select(d => d.Tick()));
        JsonElement summary = Summary(log, "renfort");
        Assert.Equal(("joined", 40L, JsonValueKind.Null), (summary.Str("presence"), summary.Int("arrivedTick"), summary.GetProperty("leftTick").ValueKind));
        Assert.False(Summary(log, "bastion").TryGetProperty("presence", out _));   // présent du début à la fin : aucun champ
        Assert.Equal(("joined", 40L), (result["renfort"].Presence, result["renfort"].ArrivedTick));
        Assert.Null(result["bastion"].Presence);
    }

    [Fact]
    public void Un_allie_absent_n_est_ni_cible_ni_compte_dans_un_groupe()
    {
        // L'onde (tous les ennemis) ne touche pas un ennemi qui n'est pas encore arrivé ; une fois là, si.
        var (_, log) = Run(Setup(new[] { Cmd(0, Onde, target: null), Cmd(2500, Onde, target: null) },
            new CombatantSetup[] { Tank(), Dummy(), Dummy("second").Arriving(1000) }, durationMs: 4000));
        Assert.Equal(new[] { "mannequin" }, Lines(log, "damage").Where(d => d.Tick() == 0).Select(d => d.Str("target")));
        Assert.Equal(new[] { "mannequin", "second" }, Lines(log, "damage").Where(d => d.Tick() == 50).Select(d => d.Str("target")));
    }

    [Fact]
    public void Un_assaillant_arrive_attaque_apres_son_delai_compte_depuis_son_arrivee()
    {
        // Seuls le Bastion et un assaillant qui arrivera : l'équipe absente compte jusqu'à son arrivée (le combat ne s'arrête pas au tick 0).
        var (result, log) = Run(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 500, intervalMs: 2000).Arriving(1000) }, durationMs: 4000));
        Assert.Equal(20L, Assert.Single(Lines(log, "combatantJoined")).Tick());
        Assert.Equal(new[] { 30L, 70L }, Lines(log, "attackStarted").Select(a => a.Tick()));
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
    }

    [Fact]
    public void Une_arrivee_attendue_garde_son_equipe_en_vie()
    {
        // Le Bastion tombe au tick 20 ; son renfort arrive au tick 60 : le combat continue jusqu'à lui, puis jusqu'à la fin.
        var (result, log) = Run(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Character(attributes: Attrs(vitalite: 1)), Tank("renfort").Arriving(3000),
            Assailant(firstAttackMs: 1000, intervalMs: 10_000, attacks: Blow(raw: 100_000_000)),
        }, durationMs: 5000));
        Assert.Equal(20L, Assert.Single(Lines(log, "died")).Tick());
        Assert.Equal(60L, Assert.Single(Lines(log, "combatantJoined")).Tick());
        Assert.Equal((CombatEndReason.DurationReached, 99L), (result.EndReason, result.EndTick));
    }

    [Fact]
    public void Arrivee_au_dernier_tick_et_arrivee_apres_la_fin_refusee()
    {
        var (_, log) = Run(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Dummy(), Tank("tardif").Arriving(9950) }, durationMs: 10_000));
        Assert.Equal(199L, Assert.Single(Lines(log, "combatantJoined")).Tick());   // dernier tick du combat
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Dummy(), Tank("tardif").Arriving(2000) }, durationMs: 2000), NullCombatLog.Instance));
        Assert.Contains(error.Violations, v => v.Fields.Contains("combatants[2].arrivalMs"));
    }

    [Theory]
    [InlineData("0.5", "0", true, "ogre")]    // chevauche l'ogre (créatures solides entre elles)
    [InlineData("2", "0", false, null)]       // emprises qui se touchent : place libre
    public void Arrivee_refusee_si_une_emprise_solide_occupe_la_place(string x, string z, bool refused, string? blocker)
    {
        var (result, log) = Run(Spatial(Arena(20), new CombatantSetup[]
        {
            Player("bastion", At("-5", "0")), Creature("ogre", At("0", "0", "1")),
            Creature("loup", At(x, z, "1"), firstAttackMs: 0).Arriving(1000),
        }, Array.Empty<CombatCommand>(), NoUpkeep, 3000));
        if (refused)
        {
            JsonElement no = Assert.Single(Lines(log, "arrivalRefused"));
            Assert.Equal((20L, "loup", "occupied", blocker), (no.Tick(), no.Str("combatant"), no.Str("reason"), no.Str("blocker")));
            Assert.Empty(Lines(log, "combatantJoined"));
            Assert.Empty(Lines(log, "attackStarted"));
            Assert.Equal("refused", result["loup"].Presence);
        }
        else
        {
            JsonElement joined = Assert.Single(Lines(log, "combatantJoined"));
            Assert.Equal("[2,0]", joined.GetProperty("position").GetRawText());
            Assert.Single(Lines(log, "attackStarted"));   // première attaque au tick de son arrivée (délai nul)
        }
    }

    [Fact]
    public void Un_joueur_peut_arriver_sur_un_autre_joueur()
    {
        // Les joueurs se traversent : aucune emprise ne les gêne.
        var (_, log) = Run(Spatial(Arena(20), new CombatantSetup[] { Player("bastion", At("0", "0")), Player("renfort", At("0.2", "0")).Arriving(500), Creature("ogre", At("5", "0")) },
            Array.Empty<CombatCommand>(), NoUpkeep, 2000));
        Assert.Single(Lines(log, "combatantJoined"));
        Assert.Empty(Lines(log, "arrivalRefused"));
    }

    [Fact]
    public void Un_absent_n_est_pas_solide_et_ne_marche_pas()
    {
        // Le loup attendu est sur le chemin du Bastion : avant son arrivée, il ne gêne pas (le Bastion le traverse sans dégagement) ; la ligne
        // moved ne parle jamais de lui avant son arrivée.
        var (_, log) = Run(Spatial(Arena(20), new CombatantSetup[]
        {
            Player("bastion", At("-3", "0")), Creature("loup", At("0", "0"), firstAttackMs: 600_000).Arriving(1500), Creature("ogre", At("8", "0")),
        }, new[] { Move(0, "bastion", "1", "0"), Stop(1000, "bastion") }, NoUpkeep, 3000));
        JsonElement last = Lines(log, "moved").Where(m => m.Tick() < 20).Last();
        string position = last.GetProperty("combatants").EnumerateArray().Single(c => c.Str("combatant") == "bastion").GetProperty("position").GetRawText();
        Assert.Equal("[2,0]", position);   // 20 pas de 0,25 m en ligne droite, à travers la place du loup
        Assert.DoesNotContain(Lines(log, "moved").SelectMany(m => m.GetProperty("combatants").EnumerateArray()), c => c.Str("combatant") == "loup");
    }

    // --- Départs ---------------------------------------------------------------------------------------------

    [Fact]
    public void Depart_en_fin_de_tick_etats_et_provocation_retires()
    {
        string cri = Lot114bRangeTests.Cri;
        var (result, log) = Run(Setup(new[]
        {
            Cmd(0, cri, target: null),
            Leave(1000, "bastion"), Leave(1000, "bastion"),   // la seconde : alreadyLeaving
            Cmd(1000, Lot114bRangeTests.Libre, "bastion", "assaillant"),   // même tick, avant le départ effectif : acceptée
            Cmd(1500, Lot114bRangeTests.Libre, "bastion", "assaillant"),   // après : actorAbsent
        }, new CombatantSetup[]
        {
            Lot114bRangeTests.Hero("bastion", null, new[] { cri, Lot114bRangeTests.Libre }), Lot114bRangeTests.Hero("allie", null),
            Assailant(firstAttackMs: 2000, intervalMs: 1000, target: "allie"),
        }, durationMs: 3500, rules: NoUpkeep));
        JsonElement left = Assert.Single(Lines(log, "combatantLeft"));
        Assert.Equal((20L, "endOfTick", "bastion", 1L, "command:1"), (left.Tick(), left.Str("phase"), left.Str("combatant"), left.Int("command"), left.Str("root")));
        var removed = Lines(log, "stateRemoved").Where(r => r.Tick() == 20).ToList();
        Assert.Contains(removed, r => (r.Str("combatant"), r.Str("state"), r.Str("reason")) == ("bastion", "bastionAbsolute", "left"));
        Assert.Contains(removed, r => (r.Str("combatant"), r.Str("state"), r.Str("reason")) == ("assaillant", "bastionAbsoluteTaunt", "sourceLeft"));
        Assert.All(removed, r => Assert.Equal(left.Int("eid"), r.Int("cause")));
        Assert.DoesNotContain(removed, r => r.Str("combatant") == "allie");   // un renforcement n'est pas lié à sa source
        var rejected = Lines(log, "commandRejected");
        Assert.Contains(rejected, r => (r.Tick(), r.Str("reason")) == (20, "alreadyLeaving"));
        Assert.Contains(rejected, r => (r.Tick(), r.Str("reason"), r.Str("actor")) == (30, "actorAbsent", "bastion"));
        Assert.Single(Lines(log, "skillCast"), c => c.Tick() == 20);
        // La provocation partie, l'assaillant revient à sa cible : l'allié.
        Assert.Equal(new[] { "allie", "allie" }, Lines(log, "attackStarted").Select(a => a.Str("target")));
        JsonElement summary = Summary(log, "bastion");
        Assert.Equal(("left", 20L), (summary.Str("presence"), summary.Int("leftTick")));
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("arrivedTick").ValueKind);
        Assert.Equal(("left", 20L), (result["bastion"].Presence, result["bastion"].LeftTick));
    }

    [Fact]
    public void Un_assaillant_vise_qui_part_n_est_plus_frappe()
    {
        var (_, log) = Run(Setup(new[] { Leave(1500, "bastion") },
            new CombatantSetup[] { Tank(), Tank("allie"), Assailant(firstAttackMs: 1000, intervalMs: 1000, target: "bastion") }, durationMs: 4000));
        Assert.Equal(new[] { 20L }, DamageTo(log, "bastion").Select(d => d.Tick()));
        Assert.Equal(new[] { 40L, 60L }, Lines(log, "attackSkipped").Select(a => a.Tick()));
        Assert.All(Lines(log, "attackSkipped"), a => Assert.Equal("targetAbsent", a.Str("reason")));
    }

    [Fact]
    public void Mort_pendant_le_tick_du_depart_il_reste_mort()
    {
        var (result, log) = Run(Setup(new[] { Leave(1000, "bastion") }, new CombatantSetup[]
        {
            Character(attributes: Attrs(vitalite: 1)), Tank("allie"), Assailant(firstAttackMs: 1000, intervalMs: 10_000, attacks: Blow(raw: 100_000_000)),
        }, durationMs: 3000));
        JsonElement cancelled = Assert.Single(Lines(log, "leaveCancelled"));
        Assert.Equal((20L, "bastion", 0L, "actorDead"), (cancelled.Tick(), cancelled.Str("combatant"), cancelled.Int("command"), cancelled.Str("reason")));
        Assert.Empty(Lines(log, "combatantLeft"));
        Assert.True(result["bastion"].IsDead);
        Assert.Null(result["bastion"].Presence);
    }

    [Fact]
    public void Le_depart_attend_les_reactions_aux_morts_du_tick()
    {
        // Revue Codex de la PR #32 : l'assaillant tue l'allié au tick 20 et réagit à cette mort en fragilisant tous ses ennemis ; le Bastion,
        // qui part à ce tick, est encore là pour cette réaction (point fixe des morts d'abord), puis part : la fragilité est retirée (left).
        var assailant = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "allie", 1000, 10_000, new[] { Blow(raw: 100_000_000) },
            triggers: new[] { new TriggerDefinition(TriggerEvent.Death, new[] { TriggerEffect.ApplyStatus(TriggerTarget.AllEnemies, "fragility") }) });
        var (_, log) = Run(Setup(new[] { Leave(1000, "bastion") }, new CombatantSetup[] { Tank(), Character("allie", attributes: Attrs(vitalite: 1)), Tank("renfort"), assailant },
            durationMs: 3000, rules: AreaStrikes));
        var tick20 = Events(log).Where(e => e.Tick() == 20 && e.Str("phase") == "endOfTick").Select(e => (e.Str("event"), e.TryGetProperty("combatant", out var c) ? c.GetString() : null)).ToList();
        int died = tick20.IndexOf(("died", "allie"));
        int applied = tick20.IndexOf(("stateApplied", "bastion"));
        int left = tick20.IndexOf(("combatantLeft", "bastion"));
        int removed = tick20.IndexOf(("stateRemoved", "bastion"));
        Assert.True(died >= 0 && died < applied && applied < left && left < removed, string.Join(" ; ", tick20));
        Assert.Equal("left", Lines(log, "stateRemoved").Single(r => r.Str("combatant") == "bastion").Str("reason"));
        Assert.Empty(Lines(log, "leaveCancelled"));
    }

    [Fact]
    public void Depart_refuse_a_un_mort_et_a_un_absent()
    {
        var (_, log) = Run(Setup(new[] { Leave(2000, "bastion"), Leave(0, "renfort"), Leave(1000, "allie"), Leave(2000, "allie") }, new CombatantSetup[]
        {
            Character(attributes: Attrs(vitalite: 1)), Tank("allie"), Tank("renfort").Arriving(500),
            Assailant(firstAttackMs: 1000, intervalMs: 10_000, attacks: Blow(raw: 100_000_000)),
        }, durationMs: 3000));
        var rejected = Lines(log, "commandRejected").Select(r => (r.Tick(), r.Str("actor")!, r.Str("reason")!)).ToList();
        Assert.Equal(new[] { (0L, "renfort", "actorAbsent"), (40L, "allie", "actorAbsent"), (40L, "bastion", "actorDead") }, rejected.OrderBy(r => r.Item1).ThenBy(r => r.Item2, StringComparer.Ordinal));
    }

    [Fact]
    public void Une_equipe_toute_partie_laisse_la_victoire_a_l_autre()
    {
        // L'arrivée attendue de l'autre équipe reste attendue (presence pending).
        var (result, log) = Run(Setup(new[] { Leave(1000, "bastion") }, new CombatantSetup[] { Tank(), Dummy(), Dummy("tardif").Arriving(3000) }, durationMs: 5000));
        Assert.Equal((CombatEndReason.TeamDefeated, 20L, "cibles"), (result.EndReason, result.EndTick, result.Winner));
        Assert.Single(Lines(log, "combatantLeft"));
        Assert.Equal(("pending", "left"), (Summary(log, "tardif").Str("presence"), Summary(log, "bastion").Str("presence")));
    }

    [Fact]
    public void Une_attaque_annoncee_d_un_assaillant_parti_est_annulee()
    {
        AssailantAttack circle = new("ecrasement", 100, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Circle, 1000,
            radius: Fixed.FromInt(2));
        var (_, log) = Run(Spatial(Arena(20), new CombatantSetup[]
            {
                Player("bastion", At("0", "0")), Creature("ogre", At("4", "0"), firstAttackMs: 1000, attacks: new[] { circle }), Creature("temoin", At("-10", "0")),
            },
            new[] { Leave(1500, "ogre") }, new CombatRules { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 }, 4000));
        JsonElement started = Assert.Single(Lines(log, "attackStarted"));
        Assert.Equal(40L, started.Int("impactTick"));
        JsonElement cancelled = Assert.Single(Lines(log, "attackCancelled"));
        Assert.Equal((40L, "actorAbsent"), (cancelled.Tick(), cancelled.Str("reason")));
        Assert.Empty(DamageTo(log, "bastion"));
    }

    [Fact]
    public void Sans_arrivee_ni_depart_aucune_ligne_ni_champ_nouveau()
    {
        var (_, log) = Run(Setup(new[] { Cmd(0, Frappe) }));
        foreach (string kind in new[] { "combatantJoined", "arrivalRefused", "combatantLeft", "leaveCancelled" }) Assert.Empty(Lines(log, kind));
        Assert.All(Lines(log, "combatantSummary"), s => Assert.False(s.TryGetProperty("presence", out _)));
    }

    // --- Vue du pilote ---------------------------------------------------------------------------------------

    [Fact]
    public void Vue_d_un_combattant_absent()
    {
        var engine = new CombatEngine(Setup(new[] { Leave(500, "bastion") }, new CombatantSetup[] { Tank(), Tank("renfort").Arriving(1000), Dummy() }, durationMs: 3000),
            NullCombatLog.Instance);
        Assert.Equal((false, 20L, false), (engine.View("renfort").IsPresent, engine.View("renfort").ArrivalTick, engine.View("renfort").HasLeft));
        Assert.True(engine.View("bastion").IsPresent);
        for (int i = 0; i <= 10; i++) engine.Step();
        Assert.Equal((false, true), (engine.View("bastion").IsPresent, engine.View("bastion").HasLeft));
        for (int i = 11; i <= 20; i++) engine.Step();
        Assert.True(engine.View("renfort").IsPresent);
    }
}
