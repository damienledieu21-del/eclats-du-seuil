using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Support.Lot83Kit;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 (X9) : rôle <c>ally</c> des déclencheurs : la cible de l'événement est un allié vivant du porteur, autre que lui.
/// </summary>
[Trait("Category", "Support")]
[Trait("Category", "Trigger")]
public class Lot83AllyRoleTests
{
    private static CharacterSetup Watcher(params TriggerEffect[] effects) =>
        Hero("bastion", new[] { Lien, Barriere, Garde, Provocation }, 200, SupportClass(passiveTriggers: new[]
        {
            new TriggerDefinition(TriggerEvent.HitTaken, effects, TriggerRole.Ally, cooldownMs: 0),
        }));

    [Fact]
    public void Reagit_aux_impacts_sur_un_allie_seulement()
    {
        // Deux assaillants : l'un frappe l'allié, l'autre le Bastion. Seul l'impact sur l'allié rapporte une Charge au Bastion.
        var other = new AssailantSetup("brute", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000,
            new[] { new AssailantAttack("coup", 100, Domain.Stats.DamageType.Physique, Domain.Builds.SkillTargeting.Enemy, false, null) });
        var events = Simulate(Fight(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Watcher(TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.One)), Ally(), Brute(), other }, durationMs: 1100), out _);
        JsonElement gain = events.OfKind("resourceGained").Single(g => g.Str("actor") == "bastion");
        JsonElement hit = events.OfKind("damage").Single(d => d.Str("target") == "allie");
        Assert.Equal((hit.Long("eid"), 1L), (gain.Long("cause"), gain.Long("depth")));
    }

    [Fact]
    public void Provoque_l_assaillant_qui_frappe_l_allie()
    {
        // Serment (forme d'essai) : quand un allié est frappé, le Bastion provoque la source ; l'attaque suivante le vise.
        var events = Simulate(Fight(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Watcher(TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "provoc")), Ally(), Brute() }, durationMs: 3100), out _);
        Assert.Equal(new[] { "allie", "bastion" }, events.OfKind("attackStarted").Select(a => a.Str("target")));
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("assaillant", "bastion", 1L), (applied.Str("combatant"), applied.Str("source"), applied.Long("depth")));
    }
}
