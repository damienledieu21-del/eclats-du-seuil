using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Shields;

/// <summary>
/// Sous-lot 8.2 : données d'essai des boucliers (X4) et des renforcements (X3). Le Bastion d'essai (Vitalité 200, 2 100 PV max) porte
/// sur son passif des déclencheurs qui lui posent boucliers et renforcements quand il lance une compétence ; l'assaillant d'entraînement
/// frappe 100 dégâts bruts physiques (armure 0) à 1 s puis toutes les 2 s.
/// </summary>
internal static class Lot82Kit
{
    /// <summary>Bouclier de 4 s (80 ticks), cumul par source.</summary>
    public static readonly StatusDefinition Egide = new("egide", StatusCategory.Shield, 4000, StatusStacking.PerSourceKeepStronger);

    /// <summary>Second bouclier, 6 s (120 ticks).</summary>
    public static readonly StatusDefinition Barriere = new("barriere", StatusCategory.Shield, 6000, StatusStacking.PerSourceKeepStronger);

    /// <summary>Renforcement de 8 s : dégâts subis −30 %.</summary>
    public static readonly StatusDefinition Fortifie = new("fortifie", StatusCategory.Buff, 8000, StatusStacking.Refresh,
        modifiers: new[] { Mod("damageTaken", "-0.3") });

    /// <summary>Renforcement de 1 s : entretien de la garde −2 par seconde (garde sans entretien avec les règles provisoires).</summary>
    public static readonly StatusDefinition SansEntretien = new("sansEntretien", StatusCategory.Buff, 1000, StatusStacking.Refresh,
        modifiers: new[] { Mod("guard.upkeepPerSecond", "-2") });

    /// <summary>Renforcement de 8 s : dégâts infligés +50 % (catégorie altération).</summary>
    public static readonly StatusDefinition Furie = new("furie", StatusCategory.Buff, 8000, StatusStacking.Refresh,
        modifiers: new[] { Mod("damageDealt", "0.5") });

    /// <summary>Règles provisoires dont le catalogue reçoit les altérations d'essai à la suite des altérations livrées.</summary>
    public static CombatRules RulesWith(params StatusDefinition[] extra) =>
        new() { Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Concat(extra)) };

    public static readonly CombatRules Rules = RulesWith(Egide, Barriere, Fortifie, SansEntretien, Furie);

    /// <summary>Déclencheur : à chaque compétence lancée par le porteur, il se pose l'altération (coefficient pour un bouclier).</summary>
    public static TriggerDefinition OnCast(string status, string? coefficient = null) =>
        new(TriggerEvent.SkillCast, new[] { TriggerEffect.ApplyStatus(TriggerTarget.Bearer, status, coefficient is null ? null : Fixed.Parse(coefficient)) });

    /// <summary>Déclencheur : à chaque impact subi, le porteur se pose l'altération.</summary>
    public static TriggerDefinition OnHitTaken(string status, string? coefficient = null) =>
        new(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.Bearer, status, coefficient is null ? null : Fixed.Parse(coefficient)) },
            TriggerRole.Target);

    /// <summary>Bastion d'essai : 2 100 PV max par défaut, déclencheurs (et modificateurs) sur son passif équipé.</summary>
    public static CharacterSetup Protected(IEnumerable<TriggerDefinition> triggers, int vitalite = 200, ModifierDefinition[]? passiveModifiers = null) =>
        Character(attributes: Attrs(vitalite: vitalite),
            definition: Class(passiveTriggers: triggers, modifiers: passiveModifiers is null ? null : new Dictionary<string, ModifierDefinition[]> { [Passif] = passiveModifiers }),
            passives: new[] { Passif });

    /// <summary>Compétence gratuite du Bastion sur l'assaillant (déclenche les poses), au temps donné.</summary>
    public static CombatCommand Cast(long atMs = 0) => Cmd(atMs, Gratuite, target: "assaillant");

    public static CombatSetup Fight(IEnumerable<CombatCommand> commands, CharacterSetup bastion, AssailantSetup? assailant = null, long durationMs = 3100,
        CombatRules? rules = null) =>
        Setup(commands, new CombatantSetup[] { bastion, assailant ?? Assailant() }, durationMs: durationMs, rules: rules ?? Rules);

    public static long Long(this JsonElement e, string name) => e.GetProperty(name).GetInt64();

    /// <summary>Parts absorbées d'une ligne de dégâts : (instance, altération, source, part, restant).</summary>
    public static (long Instance, string State, string? Source, long Amount, long Remaining)[] AbsorbedBy(JsonElement line) =>
        line.GetProperty("absorbedBy").EnumerateArray()
            .Select(a => (a.Long("instance"), a.Str("state")!, a.Str("source"), a.Long("amount"), a.Long("remaining"))).ToArray();
}
