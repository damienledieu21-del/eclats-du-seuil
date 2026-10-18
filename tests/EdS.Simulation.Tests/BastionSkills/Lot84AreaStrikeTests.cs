using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.BastionSkills.Lot84Kit;

namespace EdS.Simulation.Tests.BastionSkills;

/// <summary>
/// Sous-lot 8.4 (L8-D2) : toute riposte automatique de zone (effet <c>strike</c> sur <c>allEnemies</c>) a au moins 3 s de recharge
/// interne (<c>combat.triggers.areaStrikeMinCooldownMs</c>), sauf configuration explicite de test. Refus au combat, avec le chemin du
/// déclencheur, pour un personnage, un assaillant ou une altération du catalogue ; au combat, la recharge espace les frappes.
/// </summary>
[Trait("Category", "Bastion")]
public class Lot84AreaStrikeTests
{
    private static TriggerDefinition Judgement(long cooldownMs) =>
        new(TriggerEvent.PerfectParry, new[] { TriggerEffect.Strike(TriggerTarget.AllEnemies, Fixed.Parse("1.2"), PowerType.Physique, DamageType.Physique) },
            TriggerRole.Target, cooldownMs: cooldownMs);

    private static readonly int PassiveIndex = KitClass().Skills.Select((s, i) => (s, i)).Single(x => x.s.Id == Passif84).i;

    /// <summary>Assaillant qui frappe le Bastion chaque seconde dès 1 s ; le Bastion pare chaque attaque (fenêtre ouverte 50 ms avant).</summary>
    private static CombatSetup Parries(long cooldownMs, CombatRules? rules = null, int count = 4) =>
        Fight(Enumerable.Range(0, count).Select(i => Parry(950 + 1000L * i)),
            new CombatantSetup[] { Hero(definition: KitClass(passiveTriggers: new[] { Judgement(cooldownMs) })), Assailant(1000, 1000), Dummy(armor: 0) },
            durationMs: 1000L * count + 500, rules: rules);

    [Theory]
    [InlineData(2950, false)]
    [InlineData(3000, true)]
    public void Recharge_interne_de_3_s_au_moins(long cooldownMs, bool accepted)
    {
        IReadOnlyList<RuleViolation> errors = Parries(cooldownMs).Validate();
        if (accepted)
        {
            Assert.Empty(errors);
            return;
        }
        RuleViolation v = Assert.Single(errors);
        Assert.Equal("combatants[0].class", v.Fields.Single());
        Assert.Contains($"skills[{PassiveIndex}].triggers[0].cooldownMs", v.Message);
        Assert.Contains("au moins 3000 ms", v.Message);
        Assert.Throws<CombatSetupException>(() => Run(Parries(cooldownMs)));
    }

    [Fact]
    public void La_recharge_espace_les_frappes_de_zone()
    {
        // Parades parfaites aux ticks 20, 40, 60 et 80 : frappes de zone aux ticks 20 et 80 (recharge de 60 ticks), sur l'assaillant et
        // le mannequin ; les parades des ticks 40 et 60 ne déclenchent rien.
        var events = Simulate(Parries(3000), out CombatResult result);
        Assert.Equal(4, result["bastion"].PerfectParries);
        long[] strikes = events.OfKind("damage").Where(d => d.Str("source") == "bastion").Select(d => d.Tick()).Distinct().ToArray();
        Assert.Equal(new[] { 20L, 80L }, strikes);
        Assert.Equal(4, events.OfKind("damage").Count(d => d.Str("source") == "bastion"));
    }

    [Fact]
    public void Configuration_explicite_de_test()
    {
        // combat.triggers.areaStrikeMinCooldownMs = 0 : recharge interne nulle admise, une frappe à chaque parade parfaite.
        var rules = new CombatRules { GlobalCooldownMs = 0, AreaStrikeMinCooldownMs = 0 };
        Assert.Empty(Parries(0, rules).Validate());
        var events = Simulate(Parries(0, rules), out _);
        Assert.Equal(new[] { 20L, 40L, 60L, 80L }, events.OfKind("damage").Where(d => d.Str("source") == "bastion").Select(d => d.Tick()).Distinct());
    }

    [Fact]
    public void Riposte_de_zone_d_assaillant_ou_d_alteration_refusee()
    {
        AssailantSetup assailant = new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 1000, new[] { Blow() },
            triggers: new[] { new TriggerDefinition(TriggerEvent.HitDealt, new[] { TriggerEffect.Strike(TriggerTarget.AllEnemies, Fixed.One, PowerType.Physique, DamageType.Physique) },
                cooldownMs: 1000) });
        RuleViolation v = Assert.Single(Fight(Array.Empty<CombatCommand>(), new CombatantSetup[] { Hero(), assailant }).Validate());
        Assert.Equal("combatants[1].triggers[0].cooldownMs", v.Fields.Single());

        var marked = new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh, triggers: new[] { Judgement(2000) });
        var rules = new CombatRules { GlobalCooldownMs = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(marked)) };
        RuleViolation status = Assert.Single(Fight(Array.Empty<CombatCommand>(), new CombatantSetup[] { Hero(), Assailant() }, rules: rules).Validate());
        Assert.Equal($"rules.statuses[{StatusCatalog.Provisional.Definitions.Count}].triggers[0].cooldownMs", status.Fields.Single());
    }
}
