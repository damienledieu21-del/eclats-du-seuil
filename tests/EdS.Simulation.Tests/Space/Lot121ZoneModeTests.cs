using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 12.1 : mode zone du moteur (ARCHITECTURE_MONDE.md, section 4 : « un mode zone sans fin globale »). Une zone ne s'arrête pas
/// quand une équipe tombe, seulement au bout de sa durée ; un combat fermé reste inchangé.
/// </summary>
[Trait("Category", "Space")]
public class Lot121ZoneModeTests
{
    private static CombatSetup AsZone(CombatSetup s, bool zone, long? durationMs = null) =>
        new(s.Seed, durationMs ?? s.DurationMs, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants, s.Commands, s.Data, s.Space, zone);

    /// <summary>Le Bastion d'essai frappe un loup de 1 PV au contact : le loup meurt au premier coup.</summary>
    private static CombatSetup WolfHunt() => Spatial(Arena(10), new CombatantSetup[]
    {
        EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0"), new[] { EdS.Simulation.Tests.Space.Lot114bRangeTests.Appel }),
        Creature("loup", At("2", "0"), health: 1),
    }, new[] { Cmd(0, EdS.Simulation.Tests.Space.Lot114bRangeTests.Appel, target: "loup") }, NoUpkeep, 3000);

    private static string? EndReason(MemoryCombatLog log) =>
        Events(log).OfKind("combatEnded").Single().GetProperty("reason").GetString();

    [Fact]
    public void Combat_ferme_fini_a_la_mort_du_dernier_ennemi_zone_continue()
    {
        var (closed, closedLog) = Run(WolfHunt());
        Assert.Equal("teamDefeated", EndReason(closedLog));
        Assert.True(closed["loup"].IsDead);
        long death = Events(closedLog).OfKind("died").Single().Tick();
        Assert.Equal(death, Events(closedLog).OfKind("combatEnded").Single().Tick());

        var (zone, zoneLog) = Run(AsZone(WolfHunt(), true));
        Assert.True(zone["loup"].IsDead);
        Assert.Equal("durationReached", EndReason(zoneLog));
        Assert.Equal(59, Events(zoneLog).OfKind("combatEnded").Single().Tick());
        // Jusqu'à la mort, les deux journaux sont les mêmes (en-tête mis à part : zone: true).
        string[] a = closedLog.Lines.Skip(1).TakeWhile(l => !l.Contains("\"combatantSummary\"", StringComparison.Ordinal) && !l.Contains("\"combatEnded\"", StringComparison.Ordinal)).ToArray();
        Assert.Equal(a, zoneLog.Lines.Skip(1).Take(a.Length));
        Assert.Contains("\"zone\":true", zoneLog.Lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"zone\"", closedLog.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Zone_longue_et_instantane()
    {
        // Une zone peut durer au-delà de la limite des combats fermés ; elle se relit d'un instantané comme un combat.
        CombatSetup zone = AsZone(WolfHunt(), true, 3_600_000);
        Assert.Empty(zone.Validate());
        Assert.Contains(AsZone(WolfHunt(), false, 3_600_000).Validate(), v => v.Fields.Contains("durationMs"));
        var engine = new CombatEngine(zone, NullCombatLog.Instance);
        while (engine.CurrentTick < 100) engine.Step();
        Assert.False(engine.IsFinished);
        string text = engine.Snapshot().Text;
        CombatEngine restored = CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), NullCombatLog.Instance);
        Assert.Equal(text, restored.Snapshot().Text);
        Assert.Equal(text, CombatEngine.RestoreVerified(engine.CompletedSetup(), CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
    }
}
