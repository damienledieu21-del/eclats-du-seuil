using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using FsCheck.Xunit;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Correctif731;

/// <summary>
/// Correctif 7.3.1 (A73-M04) : la réservation d'un événement programmé crédite son propre retrait, certain dès qu'elle passe. Le
/// contrôle de capacité porte sur l'occupation nette (file − 1 + insertions), pas sur le nombre d'insertions. Avant le correctif, le
/// scénario minimal (un DOT, un tour d'assaillant, capacité 2, majorant garanti 2) s'arrêtait sur <c>queueLimitExceeded</c> au
/// premier tick de DOT alors que la file n'a jamais contenu plus de 2 éléments.
/// </summary>
[Trait("Category", "Limits")]
[Trait("Category", "Regression")]
public class M04QueueCapacityTests
{
    private static CombatRules Capacity(int capacity) => new() { GuardUpkeepPerSecond = 0, MaxPendingEvents = capacity };

    /// <summary>Un assaillant pose <paramref name="dots"/> DOT (saignement, brûlure) sur <paramref name="targets"/> cible(s) à chaque tour.</summary>
    private static CombatSetup Dots(int capacity, int dots = 1, int targets = 1)
    {
        StatusApplication[] applies = new[] { Apply("bleed", "0.2"), Apply("burn", "0.2") }.Take(dots).ToArray();
        var attack = new AssailantAttack("entaille", 10, DamageType.Physique, targets > 1 ? SkillTargeting.AllEnemies : SkillTargeting.Enemy, false, applies);
        var fighters = Enumerable.Range(0, targets).Select(i => (CombatantSetup)Tank(i == 0 ? "bastion" : $"allie{i}"))
            .Append(Assailant(0, 1000, attacks: attack));
        return Setup(Array.Empty<CombatCommand>(), fighters, durationMs: 5000, rules: Capacity(capacity));
    }

    [Theory]
    [InlineData(1, 1, 2)]   // scénario minimal de l'audit
    [InlineData(2, 1, 3)]
    [InlineData(1, 2, 3)]
    [InlineData(2, 2, 5)]
    public void Majorant_garanti_atteint_exactement_sans_arret(int dots, int targets, long expected)
    {
        CombatSetup.QueueBound bound = Dots(100_000, dots, targets).ComputeQueueBound();
        Assert.True(bound.Guaranteed);
        Assert.Equal(expected, bound.Total);
        int n = (int)bound.Total;

        // N − 1 : refusé avant tout journal (saturation réelle possible).
        CombatSetup refused = Dots(n - 1, dots, targets);
        RuleViolation violation = Assert.Single(refused.Validate());
        Assert.Equal(RuleViolationKind.LimitExceeded, violation.Kind);
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(refused, log));
        Assert.Empty(log.Lines);

        // N et N + 1 : simulé jusqu'au bout ; le pic mesuré atteint le majorant (il est exact ici) sans le dépasser.
        foreach (int capacity in new[] { n, n + 1 })
        {
            var (result, run) = Run(Dots(capacity, dots, targets));
            Assert.Equal((CombatEndReason.DurationReached, (string?)null), (result.EndReason, result.AbortReason));
            Assert.Equal(bound.Total, result.Peaks.PendingEvents);
            Assert.NotEmpty(Events(run).OfKind("dotDamage"));
        }
    }

    [Fact]
    public void Saturation_reelle_toujours_arretee()
    {
        // Sans le contrôle d'admission, capacité 1 : le tour d'assaillant programme le tour suivant et un tick de DOT, soit
        // 2 éléments après son propre retrait. L'arrêt queueLimitExceeded reste le filet, au tick 0, avant toute mutation du tour.
        var (result, log) = RunWithoutQueueBound(Dots(1));
        Assert.Equal((CombatEndReason.Aborted, "queueLimitExceeded", 0L), (result.EndReason, result.AbortReason, result.EndTick));
        Assert.Empty(Events(log).OfKind("hit"));
    }

    private static readonly string[] Statuses = { "bleed", "burn", "fragility", "stun", "freeze" };

    /// <summary>Combat sans déclencheur généré : 1 ou 2 personnages, 1 à 3 assaillants porteurs d'altérations, commandes d'altérations.</summary>
    private static CombatSetup Generate(int seed, int capacity)
    {
        var rng = new Random(seed);
        var fighters = new List<CombatantSetup>();
        int characters = 1 + rng.Next(2);
        for (int i = 0; i < characters; i++)
            fighters.Add(Character(i == 0 ? "bastion" : "second", attributes: Attrs(vitalite: 200), actives: new[] { Brasier, Entaille, Assommer, Givre }));
        for (int i = 0, n = 1 + rng.Next(3); i < n; i++)
        {
            var attacks = Enumerable.Range(0, 1 + rng.Next(2)).Select(k =>
            {
                string status = Statuses[rng.Next(Statuses.Length)];
                return new AssailantAttack($"x{k}", 1 + rng.Next(20), DamageType.Physique, rng.Next(3) == 0 ? SkillTargeting.AllEnemies : SkillTargeting.Enemy,
                    false, new[] { Apply(status, status is "bleed" or "burn" ? "0.2" : null) });
            }).ToArray();
            fighters.Add(Assailant(50L * rng.Next(20), 50L * (1 + rng.Next(40)), $"a{i}", target: rng.Next(characters) == 0 ? "bastion" : (characters > 1 ? "second" : "bastion"), attacks: attacks));
        }
        var commands = Enumerable.Range(0, rng.Next(12)).Select(_ => Cmd(50L * rng.Next(80), new[] { Brasier, Entaille, Assommer, Givre }[rng.Next(4)],
            characters > 1 && rng.Next(2) == 0 ? "second" : "bastion", $"a{rng.Next(fighters.Count - characters)}")).OrderBy(c => c.AtMs).ToArray();
        return Setup(commands, fighters, seed: seed, durationMs: 4000, rules: Capacity(capacity));
    }

    [Property(MaxTest = 200)]
    public bool Scenario_garanti_jamais_arrete_sur_la_file(int seed)
    {
        CombatSetup probe = Generate(seed, 100_000);
        if (probe.Validate().Count > 0) return true;
        CombatSetup.QueueBound bound = probe.ComputeQueueBound();
        Assert.True(bound.Guaranteed);
        CombatSetup tight = Generate(seed, (int)bound.Total);
        Assert.Empty(tight.Validate());
        var (result, _) = Run(tight);
        Assert.NotEqual("queueLimitExceeded", result.AbortReason);
        return result.Peaks.PendingEvents <= bound.Total;
    }
}
