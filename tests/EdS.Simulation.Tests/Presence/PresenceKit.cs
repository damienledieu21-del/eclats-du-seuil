using EdS.Domain.Combat;

namespace EdS.Simulation.Tests.Presence;

/// <summary>Sous-lot 11.4d : combattants de test qui arrivent en cours de combat, et commande de départ.</summary>
internal static class PresenceKit
{
    /// <summary>Le même combattant, qui arrive à <paramref name="arrivalMs"/> (null : présent dès le départ).</summary>
    public static CombatantSetup Arriving(this CombatantSetup c, long? arrivalMs) => c switch
    {
        CharacterSetup ch => new CharacterSetup(ch.Id, ch.Team, ch.Class, ch.Build, ch.Initiative, ch.Placement, arrivalMs),
        AssailantSetup a => new AssailantSetup(a.Id, a.Team, a.Armor, a.MagicResistance, a.Target, a.FirstAttackMs, a.IntervalMs, a.Attacks,
            a.Initiative, a.StatusResistance, a.Triggers, a.Health, a.Placement, arrivalMs),
        DummySetup d => new DummySetup(d.Id, d.Team, d.Armor, d.MagicResistance, d.Initiative, d.StatusResistance, d.Placement, arrivalMs),
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    public static CombatCommand Leave(long atMs, string actor) => new(atMs, actor, CombatAction.Leave);
}
