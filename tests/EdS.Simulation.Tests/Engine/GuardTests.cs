using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Garde (A1) : réduction, coûts, entretien, rupture, régénération suspendue, attaques imparables.</summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
public class GuardTests
{
    private static (CombatResult Result, List<JsonElement> Events) Fight(IEnumerable<CombatCommand> commands, AssailantSetup? assailant = null,
        CombatRules? rules = null, long durationMs = 10_000, CharacterSetup? character = null)
    {
        var (result, log) = Run(Setup(commands, new CombatantSetup[] { character ?? Tank(), assailant ?? Assailant() }, durationMs: durationMs, rules: rules));
        return (result, Events(log));
    }

    private static List<JsonElement> DamageTo(List<JsonElement> events, string target = "bastion") =>
        events.OfKind("damage").Where(e => e.Str("target") == target).ToList();

    [Fact]
    public void Sans_garde_l_impact_de_reference_inflige_100()
    {
        var (_, events) = Fight(Array.Empty<CombatCommand>());
        var hit = DamageTo(events)[0];
        Assert.Equal(20, hit.Tick());
        Assert.Equal(100, hit.Int("amount"));
        Assert.Equal("none", hit.Str("defense"));
        Assert.False(hit.GetProperty("critical").GetBoolean());
    }

    [Fact]
    public void Garde_reduit_de_60_pourcent_et_coute_10_endurance_par_impact()
    {
        var (result, events) = Fight(new[] { GuardStart(0) }, rules: NoUpkeep, durationMs: 2000);
        var hit = Assert.Single(DamageTo(events));
        Assert.Equal(40, hit.Int("amount"));
        Assert.Equal("guard", hit.Str("defense"));
        var blocked = Assert.Single(events.OfKind("blocked"));
        Assert.Equal(10, blocked.Int("enduranceCost"));
        Assert.Equal(90, blocked.Int("endurance"));
        Assert.True(blocked.Int("seq") < hit.Int("seq"));
        Assert.Equal(1, result["bastion"].Blocks);
        Assert.Equal(2, result["bastion"].Resonance);   // A5 : +2 par blocage
    }

    [Fact]
    public void Garde_brisee_avant_la_reduction_si_l_endurance_manque()
    {
        // 100 d'endurance, impact bloqué à 101 : la garde se brise et l'impact passe en entier.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var (result, events) = Fight(new[] { GuardStart(0) }, rules: rules, durationMs: 2000);
        var broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal("insufficientEndurance", broken.Str("reason"));
        var hit = Assert.Single(DamageTo(events));
        Assert.True(broken.Int("seq") < hit.Int("seq"));
        Assert.Equal(100, hit.Int("amount"));
        Assert.Equal("guardBroken", hit.Str("defense"));
        Assert.Empty(events.OfKind("blocked"));
        Assert.Equal(100, result["bastion"].Endurance);
        Assert.Equal(0, result["bastion"].Resonance);
        Assert.Equal(1, result["bastion"].GuardBreaks);

        var state = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal("guardBreak", state.Str("state"));
        Assert.Equal(20 + 60, state.Int("untilTick"));
    }

    [Fact]
    public void Rupture_de_garde_bloque_garde_et_parade_pendant_3_secondes_exactement()
    {
        // Rupture au tick 20 (1000 ms) : refus jusqu'au tick 79, garde et parade acceptées au tick 80 (4000 ms).
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { GuardStart(0), GuardStart(3950), Parry(3950), Parry(4000), GuardStart(4000) };
        var (_, events) = Fight(commands, Assailant(intervalMs: 9000), rules, durationMs: 5000);
        var rejected = events.OfKind("commandRejected").ToList();
        Assert.Equal(2, rejected.Count);
        Assert.All(rejected, r => Assert.Equal(("guardBroken", 79L), (r.Str("reason")!, r.Tick())));
        var expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal((80L, "guardBreak", "expirations"), (expired.Tick(), expired.Str("state")!, expired.Str("phase")!));
        Assert.Equal(80, Assert.Single(events.OfKind("parryStarted")).Tick());
        Assert.Equal(new long[] { 0, 80 }, events.OfKind("guardStarted").Select(e => e.Tick()));
    }

    [Fact]
    public void Entretien_de_2_endurance_par_seconde_sans_derive()
    {
        // Garde du tick 0 au bout de 10 s, sans impact : 1 point tous les 10 ticks (ticks 9, 19, …, 199), 20 au total.
        var (result, events) = Fight(new[] { GuardStart(0) }, Assailant(firstAttackMs: 20_000), durationMs: 10_000);
        var upkeep = events.OfKind("guardUpkeep").ToList();
        Assert.Equal(Enumerable.Range(1, 20).Select(i => i * 10L - 1), upkeep.Select(e => e.Tick()));
        Assert.All(upkeep, e => Assert.Equal(("periodic", 1L), (e.Str("phase")!, e.Int("amount"))));
        Assert.Equal(80, result["bastion"].Endurance);
        Assert.Empty(events.OfKind("enduranceRegenerated"));
    }

    [Fact]
    public void Entretien_impayable_brise_la_garde()
    {
        // 1000/s = 50 par tick : 100 → 50 (tick 0) → 0 (tick 1) → rupture au tick 2.
        var rules = new CombatRules { GuardUpkeepPerSecond = 1000 };
        var (_, events) = Fight(new[] { GuardStart(0) }, Assailant(firstAttackMs: 9000), rules, durationMs: 1000);
        Assert.Equal(new long[] { 50, 0 }, events.OfKind("guardUpkeep").Select(e => e.Int("endurance")));
        var broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((2L, "upkeep", "periodic"), (broken.Tick(), broken.Str("reason")!, broken.Str("phase")!));
    }

    [Fact]
    public void Regeneration_suspendue_pendant_la_garde_puis_reprise()
    {
        // Frappe au tick 0 (−10), garde aussitôt (sans entretien), arrêt au tick 100 : aucun point avant ;
        // la dernière dépense date de plus d'une seconde, la régénération compte à partir du tick 100 (1er point au tick 101).
        var commands = new[] { Cmd(0, Frappe, target: "assaillant"), GuardStart(0), GuardStop(5000) };
        var (_, events) = Fight(commands, Assailant(firstAttackMs: 20_000), NoUpkeep, durationMs: 7000);
        var regen = events.OfKind("enduranceRegenerated").ToList();
        Assert.Equal(101, regen[0].Tick());
        Assert.Equal(100, regen.Last().Int("endurance"));
        Assert.Equal(10, regen.Sum(e => e.Int("amount")));
    }

    [Fact]
    public void Regeneration_suspendue_pendant_la_rupture()
    {
        // Frappe au tick 0, rupture au tick 20 (impact), fin au tick 80 : premier point au tick 81.
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Frappe, target: "assaillant"), GuardStart(0) };
        var (_, events) = Fight(commands, Assailant(intervalMs: 9000), rules, durationMs: 6000);
        Assert.Equal(81, events.OfKind("enduranceRegenerated").First().Tick());
    }

    [Fact]
    public void Arret_de_garde_attend_une_seconde_depuis_la_derniere_depense()
    {
        // Impact bloqué au tick 20 (−10), arrêt au tick 25 : la régénération attend le tick 40 (1er point au tick 41).
        var (_, events) = Fight(new[] { GuardStart(0), GuardStop(1250) }, Assailant(intervalMs: 9000), NoUpkeep, durationMs: 4000);
        Assert.Equal(41, events.OfKind("enduranceRegenerated").First().Tick());
    }

    [Fact]
    public void Compétence_refusee_pendant_la_garde()
    {
        var (_, events) = Fight(new[] { GuardStart(0), Cmd(500, Frappe, target: "assaillant"), GuardStop(600), Cmd(600, Frappe, target: "assaillant") },
            Assailant(firstAttackMs: 20_000), durationMs: 2000);
        var rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal(("guardActive", "skill"), (rejected.Str("reason")!, rejected.Str("action")!));
        Assert.Equal(12, Assert.Single(events.OfKind("skillCast")).Tick());
    }

    [Fact]
    public void Commandes_de_garde_incoherentes_refusees_et_journalisees()
    {
        var (result, events) = Fight(new[] { GuardStop(0), GuardStart(100), GuardStart(200), GuardStart(200, "assaillant") },
            Assailant(firstAttackMs: 20_000), durationMs: 1000);
        // Au tick 4, « assaillant » passe avant « bastion » (identifiant ordinal, initiatives égales).
        Assert.Equal(new[] { "guardNotActive", "actorCannotAct", "guardAlreadyActive" }, events.OfKind("commandRejected").Select(e => e.Str("reason")));
        Assert.All(events.OfKind("commandRejected"), e => Assert.Equal(JsonValueKind.Null, e.GetProperty("skill").ValueKind));
        Assert.Equal(2, result["bastion"].CommandsRejected);
    }

    [Fact]
    public void Attaque_imparable_ignore_la_garde()
    {
        var (result, events) = Fight(new[] { GuardStart(0) }, Assailant(attacks: Blow(unblockable: true)), NoUpkeep, durationMs: 2000);
        var hit = Assert.Single(DamageTo(events));
        Assert.Equal((100L, "unblockable"), (hit.Int("amount"), hit.Str("defense")!));
        Assert.Empty(events.OfKind("blocked"));
        Assert.Equal(100, result["bastion"].Endurance);
        Assert.Equal(0, result["bastion"].Resonance);
    }

    [Fact]
    public void Competence_imparable_ignore_la_garde_d_un_personnage()
    {
        CombatantSetup[] fighters = { Tank("alpha", actives: new[] { Percee }), Tank("beta", "rivaux") };
        var commands = new[] { GuardStart(0, "beta"), Cmd(0, Percee, "alpha", "beta"), Cmd(0, Gratuite, "beta", "alpha") };
        var events = Events(Run(Setup(commands, fighters, durationMs: 1000, rules: NoUpkeep)).Log);
        Assert.Equal("unblockable", events.OfKind("damage").Single(e => e.Str("target") == "beta").Str("defense"));
        Assert.Equal("guardActive", Assert.Single(events.OfKind("commandRejected")).Str("reason"));
    }

    [Fact]
    public void Garde_contre_zone_un_blocage_par_defenseur()
    {
        // Une attaque de zone touche deux personnages en garde : chacun bloque son impact et gagne +2, une seule fois.
        CombatantSetup[] fighters = { Tank("alpha"), Tank("beta"), Assailant(target: null, attacks: Blow(targeting: Domain.Builds.SkillTargeting.AllEnemies)) };
        var (result, log) = Run(Setup(new[] { GuardStart(0, "alpha"), GuardStart(0, "beta") }, fighters, durationMs: 2000, rules: NoUpkeep));
        var events = Events(log);
        Assert.Equal(new[] { "alpha", "beta" }, events.OfKind("blocked").Select(e => e.Str("actor")));
        Assert.Equal(2, result["alpha"].Resonance);
        Assert.Equal(2, result["beta"].Resonance);
    }
}
