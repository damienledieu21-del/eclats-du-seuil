using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Limits;

/// <summary>
/// Correctif 7.2 : contrôle prudent de saturation de la file. Le majorant <see cref="CombatSetup.ComputeQueueBound"/> est
/// comparé à <c>maxPendingEvents</c> avant tout journal : refus à majorant − 1, acceptation au majorant, et le pic mesuré ne
/// le dépasse jamais. Les budgets par défaut (1000 lignes par tick, 100 000 éléments en file) sont inchangés.
/// </summary>
[Trait("Category", "Limits")]
[Trait("Category", "Status")]
public class Correctif72QueueBoundTests
{
    private static AssailantAttack With(string id, bool area = false, params StatusApplication[] applies) =>
        new(id, 1, DamageType.Physique, area ? SkillTargeting.AllEnemies : SkillTargeting.Enemy, false, applies);

    /// <summary>Scénarios d'altérations variés, paramétrés par les règles (la capacité de la file change seule).</summary>
    public static readonly Dictionary<string, Func<CombatRules, CombatSetup>> Scenarios = new()
    {
        ["fragilite_a_chaque_tick"] = r => Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Assailant(0, 50, attacks: With("coup", applies: Apply("fragility"))) }, durationMs: 10_000, rules: r),
        ["etourdissement_a_chaque_tick"] = r => Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Assailant(0, 50, attacks: With("coup", applies: Apply("stun"))) }, durationMs: 10_000, rules: r),
        ["gel_de_zone_trois_sources"] = r => Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Tank("second"), Assailant(0, 50, "a", attacks: With("coup", true, Apply("freeze"))),
                Assailant(50, 100, "b", attacks: With("coup", true, Apply("freeze"))), Assailant(100, 150, "c", attacks: With("coup", true, Apply("freeze"))) },
            durationMs: 10_000, rules: r),
        ["dot_et_controles_deux_sources"] = r => Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Assailant(0, 50, "a", attacks: new[] { With("x", applies: new[] { Apply("burn", "0.5"), Apply("bleed", "0.5") }), With("y", applies: new[] { Apply("stun"), Apply("freeze") }) }),
                Assailant(0, 100, "b", attacks: With("z", applies: new[] { Apply("burn", "0.5"), Apply("fragility") })) },
            durationMs: 10_000, rules: r),
        ["competences_du_personnage"] = r => Setup(
            Enumerable.Range(0, 40).SelectMany(i => new[] { Cmd(i * 50L, Brasier, target: "assaillant"), Cmd(i * 50L + 1000, Assommer, target: "assaillant"), Cmd(i * 50L + 2000, Givre, target: "assaillant") }),
            new CombatantSetup[] { Character(actives: new[] { Brasier, Assommer, Givre, Frappe }), Assailant(60_000, 60_000) }, durationMs: 10_000, rules: r),
        ["garde_brisee"] = r => Setup(new[] { GuardStart(0), GuardStart(4000) },
            new CombatantSetup[] { Tank(), Assailant(1000, 500, attacks: new AssailantAttack("coup", 100, DamageType.Physique, SkillTargeting.Enemy, false, new[] { Apply("fragility") })) }, durationMs: 10_000,
            rules: new CombatRules { GuardCostPerBlock = 1000, GuardUpkeepPerSecond = 0, MaxPendingEvents = r.MaxPendingEvents }),
    };

    public static TheoryData<string> Names() => new(Scenarios.Keys);

    [Theory]
    [MemberData(nameof(Names))]
    public void Refus_a_majorant_moins_un_acceptation_au_majorant(string name)
    {
        var build = Scenarios[name];
        long bound = build(new CombatRules()).ComputeQueueBound().Total;
        Assert.True(bound < 100_000, $"{name} : majorant {bound}");

        // Au majorant : accepté, simulé jusqu'au bout, pic mesuré au plus égal au majorant.
        var (result, _) = Run(build(new CombatRules { MaxPendingEvents = (int)bound }));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.InRange(result.Peaks.PendingEvents, 1, bound);

        // À majorant − 1 : refusé à la validation, avant toute ligne de journal.
        CombatSetup refused = build(new CombatRules { MaxPendingEvents = (int)bound - 1 });
        RuleViolation violation = Assert.Single(refused.Validate());
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Contains($"majorant prudent de la file {bound}", violation.Message);
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(refused, log));
        Assert.Empty(log.Lines);
    }

    [Fact]
    public void Detail_du_majorant()
    {
        // Assaillant à chaque tick avec Fragilité (refresh, 120 ticks) : 1 premier tour + 120 échéances vivantes au plus.
        CombatSetup.QueueBound fragility = Scenarios["fragilite_a_chaque_tick"](new CombatRules()).ComputeQueueBound();
        Assert.Equal((1L, 0L, 120L), (fragility.Initial, fragility.Dots, fragility.Expiries));

        // Étourdissement en ignore : une échéance par cible, plus l'immunité accordée (ignore) : 1 + 1.
        CombatSetup.QueueBound stun = Scenarios["etourdissement_a_chaque_tick"](new CombatRules()).ComputeQueueBound();
        Assert.Equal((1L, 0L, 2L), (stun.Initial, stun.Dots, stun.Expiries));

        // DOT : une instance par (source, cible, altération) ; « a » porte 2 DOT, « b » 1 : 3 ticks programmés au plus.
        Assert.Equal(3L, Scenarios["dot_et_controles_deux_sources"](new CombatRules()).ComputeQueueBound().Dots);

        // Rupture de garde en ignore : elle occupe la place d'une commande guardStart déjà retirée, sans s'ajouter au majorant.
        CombatSetup broken = Scenarios["garde_brisee"](new CombatRules());
        CombatSetup.QueueBound bound = broken.ComputeQueueBound();
        Assert.Equal((3L, 0L, 12L), (bound.Initial, bound.Dots, bound.Expiries));   // 2 commandes + 1 tour ; Fragilité : un tour tous les 10 ticks, 12 en 120 ticks
        var (result, _) = Run(broken);
        Assert.True(result["bastion"].GuardBreaks >= 1);
        Assert.InRange(result.Peaks.PendingEvents, 1, bound.Total);
    }

    [Fact]
    public void Alteration_jamais_appliquee_ne_compte_pas()
    {
        // La classe d'essai porte brûlure, saignement, étourdissement et gel ; sans commande ni attaque qui les applique,
        // le majorant se réduit aux événements initiaux.
        CombatSetup setup = Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Character(), Dummy() }, rules: new CombatRules { MaxPendingEvents = 1 });
        Assert.Equal((1L, 0L, 0L), (setup.ComputeQueueBound().Initial, setup.ComputeQueueBound().Dots, setup.ComputeQueueBound().Expiries));
        Assert.Empty(setup.Validate());
    }

    [Fact]
    public void Budgets_par_defaut_inchanges()
    {
        var rules = new CombatRules();
        Assert.Equal((1000, 100_000), (rules.MaxEventsPerTick, rules.MaxPendingEvents));
        Assert.Equal((1000, 100_000), (CombatRules.Provisional.MaxEventsPerTick, CombatRules.Provisional.MaxPendingEvents));
    }
}
