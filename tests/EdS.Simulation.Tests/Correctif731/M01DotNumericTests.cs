using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Correctif731;

/// <summary>
/// Correctif 7.3.1 (A73-M01) : le précontrôle numérique calcule le pire tick d'un DOT avec la formule périodique réelle (puissance
/// figée, mode de résistance de l'altération), séparément des impacts directs, et l'inclut dans le cumul. Avant le correctif, il
/// utilisait la formule directe : avec un multiplicateur de catégorie maximal nul (ou inférieur à 1), elle sous-estimait le DOT, et le
/// contre-exemple de l'audit était admis puis s'arrêtait sur <c>overflow</c>, avec des compteurs déjà modifiés sans ligne de dégâts.
/// En défense supplémentaire, le moteur prépare PV et compteurs dans des variables vérifiées avant toute affectation.
/// </summary>
[Trait("Category", "Numeric")]
[Trait("Category", "Regression")]
public class M01DotNumericTests
{
    public enum Via { Direct, Reaction }

    private static readonly DamageRules AuditDamage = new() { MaxCategoryMultiplier = Fixed.Zero, MinimumDamage = long.MaxValue };
    private static readonly StatRules AuditStats = new() { BaseMagicPower = Fixed.One };

    /// <summary>
    /// Contre-exemple de l'audit : mannequin(s) sans défense, assaillant qui pose une brûlure de coefficient
    /// <paramref name="coefficient"/>, soit par son attaque (<see cref="Via.Direct"/>), soit par un déclencheur à son lancer
    /// (<see cref="Via.Reaction"/>, puissance de base du porteur) ; deux cibles : attaque ou déclencheur de zone.
    /// </summary>
    private static CombatSetup Scenario(Via via, int targets, DamageRules damage, StatRules stats, string coefficient = "1",
        long raw = 1, long durationMs = 5000, CombatRules? rules = null)
    {
        Fixed c = Fixed.Parse(coefficient);
        bool zone = targets > 1;
        var applies = via == Via.Direct ? new[] { new StatusApplication("burn", c) } : Array.Empty<StatusApplication>();
        var attack = new AssailantAttack("tap", raw, DamageType.Physique, zone ? SkillTargeting.AllEnemies : SkillTargeting.Enemy, false, applies);
        TriggerDefinition[]? triggers = via == Via.Reaction
            ? new[] { new TriggerDefinition(TriggerEvent.SkillCast, new[] { TriggerEffect.ApplyStatus(zone ? TriggerTarget.AllEnemies : TriggerTarget.EventTarget, "burn", c) }) }
            : null;
        var fighters = new List<CombatantSetup> { new DummySetup("target", "blue", Fixed.Zero, Fixed.Zero) };
        if (zone) fighters.Add(new DummySetup("other", "blue", Fixed.Zero, Fixed.Zero));
        fighters.Add(new AssailantSetup("attacker", "red", Fixed.Zero, Fixed.Zero, "target", 0, 10_000, new[] { attack }, triggers: triggers));
        return new CombatSetup(42, durationMs, stats, damage, ProgressionRules.Provisional, rules ?? CombatRules.Provisional, fighters,
            Array.Empty<CombatCommand>(), Identity);
    }

    public static readonly TheoryData<Via, int> Variants = new() { { Via.Direct, 1 }, { Via.Reaction, 1 }, { Via.Direct, 2 }, { Via.Reaction, 2 } };

    [Theory]
    [MemberData(nameof(Variants))]
    public void Contre_exemple_de_l_audit_refuse_avant_journal(Via via, int targets)
    {
        CombatSetup setup = Scenario(via, targets, AuditDamage, AuditStats);
        RuleViolation violation = Assert.Single(setup.Validate());
        Assert.Equal(RuleViolationKind.NumericLimit, violation.Kind);
        Assert.Equal($"combatants[{targets}]", Assert.Single(violation.Fields));   // l'assaillant, localisé
        Assert.Contains("altération « burn »", violation.Message);
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Empty(log.Lines);
    }

    /// <summary>Somme des montants journalisés (dégâts directs et ticks de DOT) par cible et par source.</summary>
    private static void AssertCountersMatchLog(MemoryCombatLog log, CombatResult result)
    {
        List<JsonElement> events = Events(log);
        var lines = events.Where(e => e.Str("event") is "damage" or "dotDamage").ToList();
        foreach (CombatantResult c in result.Combatants)
        {
            long taken = 0, dealt = 0;
            foreach (JsonElement e in lines)
            {
                long amount = e.Int("amount");
                if (e.Str("target") == c.Id) taken = checked(taken + amount);
                if (e.Str("source") == c.Id) dealt = checked(dealt + amount);
            }
            Assert.Equal((c.Id, taken, dealt), (c.Id, c.DamageTaken, c.DamageDealt));
            JsonElement summary = events.OfKind("combatantSummary").Single(e => e.Str("combatant") == c.Id);
            Assert.Equal((taken, dealt), (summary.Int("damageTaken"), summary.Int("damageDealt")));
        }
    }

    [Theory]
    [InlineData(Via.Direct, 1, 80L, 1)]
    [InlineData(Via.Reaction, 1, 80L, 1)]
    [InlineData(Via.Direct, 2, 40L, 1)]    // audit : le second DOT du tick 40 avait déjà modifié deux compteurs sans ligne
    [InlineData(Via.Reaction, 2, 40L, 1)]
    public void Sans_precontrole_le_debordement_n_applique_aucun_impact_a_moitie(Via via, int targets, long tick, int dotLines)
    {
        // Défense en profondeur : précontrôle omis (réservé aux tests), le débordement est atteint ; l'impact fautif n'a rien modifié.
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(Scenario(via, targets, AuditDamage, AuditStats), log, queueBound: true, numericLimits: false);
        while (engine.Step()) { }
        CausalGraph.Build(log.Lines);
        CombatResult result = engine.Result!;
        Assert.Equal((CombatEndReason.Aborted, "overflow", tick), (result.EndReason, result.AbortReason, result.EndTick));
        Assert.Equal(dotLines, Events(log).OfKind("dotDamage").Count());
        AssertCountersMatchLog(log, result);
    }

    // Frontière exacte : 24 h, ticks de 50 ms, 1000 lignes par tick, soit M = 1 728 000 000 impacts au plus. Un tick de DOT sans défense
    // vaut la puissance figée P × c (P = 10⁹) : c = 5,3375 donne 5 337 500 000 × M ≤ 2⁶³ − 1 ; c = 5,3376 dépasse.
    private static readonly CombatRules Day = new() { MaxDurationMs = CombatRules.AbsoluteMaxDurationMs };
    private static readonly StatRules Strong = new() { BaseMagicPower = Fixed.FromInt(1_000_000_000) };

    public static TheoryData<Via, int, string> Matrix()
    {
        var data = new TheoryData<Via, int, string>();
        foreach (Via via in new[] { Via.Direct, Via.Reaction })
            foreach (int targets in new[] { 1, 2 })
                foreach (string max in new[] { "0", "0.5", "10" })
                    data.Add(via, targets, max);
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Frontiere_du_cumul_des_dot_quel_que_soit_le_multiplicateur_de_categorie(Via via, int targets, string max)
    {
        var damage = new DamageRules { MaxCategoryMultiplier = Fixed.Parse(max) };
        long raw = via == Via.Direct ? 1_000_000_000 : 1;   // puissance d'un DOT d'assaillant : ses dégâts bruts ; d'une réaction : celle du porteur
        CombatSetup At(string c) => Scenario(via, targets, damage, Strong, c, raw, CombatRules.AbsoluteMaxDurationMs, Day);

        Assert.Empty(At("5.3375").Validate());
        RuleViolation refused = Assert.Single(At("5.3376").Validate());
        Assert.Equal((RuleViolationKind.NumericLimit, $"combatants[{targets}]"), (refused.Kind, Assert.Single(refused.Fields)));
        Assert.Contains("tick de altération « burn »", refused.Message);
        Assert.Contains("5337600000", refused.Message);
    }

    [Fact]
    public void Defense_reelle_reduit_le_dot_et_garde_le_majorant_sans_defense()
    {
        // La brûlure est réduite par la résistance magique : contre une cible résistante, le tick réel est plus petit, mais le
        // cumul reste majoré sans défense (une défense ne fait que réduire). Le calcul périodique couvre les deux.
        var damage = new DamageRules { MaxCategoryMultiplier = Fixed.Zero };
        CombatSetup setup = Scenario(Via.Direct, 1, damage, Strong, "5.3376", 1_000_000_000, CombatRules.AbsoluteMaxDurationMs, Day);
        var resistant = setup.Combatants.Select(c => c is DummySetup d ? new DummySetup(d.Id, d.Team, Fixed.Zero, Fixed.FromInt(1000)) : c).ToArray();
        var withDefense = new CombatSetup(42, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, setup.Combat, resistant, setup.Commands, Identity);
        Assert.Single(withDefense.Validate(), v => v.Kind == RuleViolationKind.NumericLimit);
        // Pire tick réel contre la cible résistante : 5 337 600 000 × 1000 / (1000 + 1000), moitié moindre que sans défense.
        Assert.Equal(2_668_800_000, DamageCalculator.ComputePeriodic(Fixed.FromInt(5_337_600_000),
            CombatantStats.ForDefenses(setup.Stats, Fixed.Zero, Fixed.FromInt(1000)), DamageType.Magique, true, damage));
    }

    [Fact]
    public void Scenarios_ordinaires_inchanges()
    {
        // Règles livrées : la brûlure de l'audit et sa réaction sont admises et simulées sans arrêt, compteurs conformes au journal.
        foreach (Via via in new[] { Via.Direct, Via.Reaction })
            foreach (int targets in new[] { 1, 2 })
            {
                CombatSetup setup = Scenario(via, targets, DamageRules.Provisional, StatRules.Provisional);
                Assert.Empty(setup.Validate());
                var (result, log) = Run(setup);
                Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
                Assert.NotEmpty(Events(log).OfKind("dotDamage"));
                AssertCountersMatchLog(log, result);
            }
    }
}
