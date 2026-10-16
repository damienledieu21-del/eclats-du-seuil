using System.Diagnostics;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Correctif731;

/// <summary>
/// Correctif 7.3.1 (A73-M03) : plafond global de 4 096 déclencheurs actifs par combat (personnages, assaillants, altérations du
/// catalogue), vérifié par la validation avant l'analyse statique des cycles. À la frontière, validation et avertissements tiennent
/// en temps borné ; au-delà, refus avant tout journal.
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Limits")]
public class M03CombatTriggerCapTests
{
    private static readonly TriggerDefinition Riposte = new(TriggerEvent.HitTaken, new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse("0.1")) });

    /// <summary>
    /// 255 personnages à 16 déclencheurs actifs (8 sur Frappe équipée, 8 sur le passif), un assaillant à 8 et une altération du
    /// catalogue à <paramref name="catalog"/> : 4 088 + <paramref name="catalog"/> déclencheurs.
    /// </summary>
    private static CombatSetup Crowd(int catalog)
    {
        var eight = Enumerable.Repeat(Riposte, 8).ToArray();
        var definition = Class(frappeTriggers: eight, passiveTriggers: eight);
        var fighters = Enumerable.Range(0, 255).Select(i => (CombatantSetup)Character($"p{i}", "joueurs", 0, Attrs(vitalite: 200), null, definition, passives: new[] { Passif }))
            .Append(new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "p0", 1000, 60_000, new[] { Blow(1) }, triggers: eight));
        var statuses = StatusCatalog.Provisional.Definitions
            .Append(new StatusDefinition("marque", StatusCategory.Modifier, 1000, StatusStacking.Refresh, triggers: Enumerable.Repeat(Riposte, Math.Min(8, catalog))));
        if (catalog > 8) statuses = statuses.Append(new StatusDefinition("sceau", StatusCategory.Modifier, 1000, StatusStacking.Refresh, triggers: Enumerable.Repeat(Riposte, catalog - 8)));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, MaxCombatants = CombatRules.AbsoluteMaxCombatants, Statuses = new StatusCatalog(statuses) };
        return Setup(Array.Empty<CombatCommand>(), fighters, durationMs: 1000, rules: rules);
    }

    [Fact]
    public void Plafond_de_declencheurs_d_un_combat_N_moins_1_N_N_plus_1()
    {
        Assert.Equal(4096, TriggerGraph.MaxTriggers);
        foreach (int catalog in new[] { 7, 8 })   // 4 095 et 4 096 : admis, un avertissement de cycle, en temps borné
        {
            CombatSetup setup = Crowd(catalog);
            var clock = Stopwatch.StartNew();
            Assert.Empty(setup.Validate());
            IReadOnlyList<RuleViolation> warnings = setup.TriggerWarnings();
            clock.Stop();
            Assert.Single(warnings);
            Assert.True(clock.ElapsedMilliseconds <= 5000, $"{4088 + catalog} déclencheurs : {clock.ElapsedMilliseconds} ms");
        }

        CombatSetup over = Crowd(9);   // 4 097
        RuleViolation violation = Assert.Single(over.Validate());
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Contains("4097 déclencheurs actifs", violation.Message);
        Assert.Empty(over.TriggerWarnings());   // pas d'analyse au-delà du plafond
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(over, log));
        Assert.Empty(log.Lines);
    }
}
