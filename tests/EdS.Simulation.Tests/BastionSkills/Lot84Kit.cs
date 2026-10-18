using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.BastionSkills;

/// <summary>
/// Sous-lot 8.4 : données d'essai des mécanismes du Bastion. Classe « Essai 84 » aux valeurs des fiches du Bastion, avec les altérations
/// livrées du catalogue provisoire (taunt, steelBarrier, bastionAbsolute, bastionAbsoluteGuard, bastionAbsoluteTaunt), plus deux
/// compétences d'essai : Charger (+1 Charge au lancement) et Fixe (coût fixe de 2 Charges, coefficient par Charge). Règles provisoires
/// sans temps de recharge global, pour enchaîner les lancers d'un tick à l'autre ; Provocation et Charger sans recharge propre.
/// </summary>
internal static class Lot84Kit
{
    public const string Charger = "skill.essai84.recharge";        // soi : +1 Charge au lancement
    public const string Contrecoup = "skill.essai84.contrecoup";    // 0,6 + 0,4 par Charge, toutes les Charges, au moins une
    public const string Fixe = "skill.essai84.fixe";                // 0,6 + 0,4 par Charge, exactement 2 Charges
    public const string Provocation = "skill.essai84.provocation";  // ennemi : provocation 3 s, +5 Résonance, 10 d'endurance
    public const string Barriere = "skill.essai84.barriere";        // allié : bouclier de 15 % des PV max du lanceur, 6 s
    public const string OndeDeRupture = "skill.essai84.onde";                // tous les ennemis : 0,5 et Fragilité
    public const string Absolu = "skill.essai84.absolu";            // ultime : −30 % de dégâts subis aux alliés, garde sans entretien, provocation
    public const string Passif84 = "skill.essai84.passif";
    public const string Passif84b = "skill.essai84.passif2";

    public static readonly CombatRules Rules = new() { GlobalCooldownMs = 0 };

    public static SkillCombatDefinition Counter(string perCharge = "0.4", bool all = true, int cost = 1) =>
        new(PowerType.Physique, DamageType.Physique, Fixed.Parse("0.6"), 10, 0, SkillTargeting.Enemy, rampartChargeCost: cost,
            rampartChargeSpendAll: all, coefficientPerRampartCharge: Fixed.Parse(perCharge));

    public static ClassDefinition KitClass(ModifierDefinition[]? passiveModifiers = null, IEnumerable<TriggerDefinition>? passiveTriggers = null,
        SkillCombatDefinition? counter = null) =>
        new("class.essai84", "Essai 84",
            new[] { "a", "b", "c" }.Select(k => new TalentTreeDefinition($"tree.essai84.{k}", k,
                new[] { new TalentDefinition($"talent.essai84.{k}.t", "T", 5) })).ToArray(),
            new[]
            {
                new SkillDefinition(Charger, "Charger", SkillKind.Active, combat: new SkillCombatDefinition(null, null, null, 0, 0, SkillTargeting.Self, rampartChargeGain: 1)),
                new SkillDefinition(Contrecoup, "Contrecoup", SkillKind.Active, combat: counter ?? Counter()),
                new SkillDefinition(Fixe, "Fixe", SkillKind.Active, combat: Counter(all: false, cost: 2)),
                new SkillDefinition(Provocation, "Provocation", SkillKind.Active, combat: new SkillCombatDefinition(null, null, null, 10, 0, SkillTargeting.Enemy,
                    applies: new[] { new StatusApplication("taunt") }, resonanceGain: 5)),
                new SkillDefinition(Barriere, "Barrière", SkillKind.Active, combat: new SkillCombatDefinition(null, null, null, 20, 20_000, SkillTargeting.Ally,
                    applies: new[] { new StatusApplication("steelBarrier", Fixed.Parse("0.15")) })),
                new SkillDefinition(OndeDeRupture, "OndeDeRupture", SkillKind.Active, combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.Parse("0.5"),
                    20, 12_000, SkillTargeting.AllEnemies, applies: new[] { new StatusApplication("fragility") })),
                new SkillDefinition(Absolu, "Absolu", SkillKind.Ultimate, 10, combat: new SkillCombatDefinition(null, null, null, 0, 0, SkillTargeting.Self,
                    applies: new[]
                    {
                        new StatusApplication("bastionAbsolute", null, ApplicationTarget.AllAllies), new StatusApplication("bastionAbsoluteGuard"),
                        new StatusApplication("bastionAbsoluteTaunt", null, ApplicationTarget.AllEnemies),
                    })),
                new SkillDefinition(Passif84, "Passif84", SkillKind.Passive, triggers: passiveTriggers, modifiers: passiveModifiers),
                new SkillDefinition(Passif84b, "Passif84 2", SkillKind.Passive),
            });

    /// <summary>Personnage d'essai de niveau 10 : quatre actives, l'ultime, le passif (porteur des modificateurs et déclencheurs).</summary>
    public static CharacterSetup Hero(string id = "bastion", string[]? actives = null, ClassDefinition? definition = null, int vitalite = 200,
        int force = 14, string team = "joueurs") =>
        new(id, team, definition ?? KitClass(),
            new CharacterBuild("class.essai84", 10, Attrs(force: force, vitalite: vitalite), null, actives ?? new[] { Charger, Contrecoup, Fixe, Provocation },
                Absolu, new[] { Passif84 }));

    public static CombatSetup Fight(IEnumerable<CombatCommand> commands, IEnumerable<CombatantSetup> combatants, long durationMs = 3100, CombatRules? rules = null) =>
        Setup(commands, combatants, durationMs: durationMs, rules: rules ?? Rules);

    public static List<JsonElement> Simulate(CombatSetup setup, out CombatResult result)
    {
        var (r, log) = Run(setup);
        result = r;
        return Events(log);
    }

    /// <summary>Montant attendu d'un impact du personnage sur une cible sans défense, au coefficient donné, critique compris.</summary>
    public static long Expected(CharacterSetup character, string coefficient, bool critical, DamageBonuses? bonuses = null)
    {
        DerivedStats attacker = CombatantStats.ForCharacter(StatRules.Provisional, character.Build, ModifierSet.Empty);
        DerivedStats target = CombatantStats.ForDefenses(StatRules.Provisional, Fixed.Zero, Fixed.Zero);
        return DamageCalculator.Compute(new DamageInput(attacker, target, PowerType.Physique, DamageType.Physique, Fixed.Parse(coefficient),
            bonuses ?? DamageBonuses.None, critical, Fixed.Zero), DamageRules.Provisional).FinalDamage;
    }

    public static long Long(this JsonElement e, string name) => e.GetProperty(name).GetInt64();
}
