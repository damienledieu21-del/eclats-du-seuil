using EdS.Domain.Combat;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Sous-lot 8.1 : combats d'essai construits sans fichier de données, avec des règles de test que les scénarios livrés n'emploient
/// pas (entretien et régénération non standard, coût de blocage élevé, récupération de parade), pour exercer chaque lecture de règle
/// remplacée par la règle effective du combattant : entretien et régénération en virgule fixe, coût de blocage et rupture par impact
/// avec Charges détenues, fenêtre et récupération de parade, recharges, durées d'altération (auteur et cible). Leurs journaux du
/// moteur 0.9.1 sont figés dans Fixtures/logs-0.9.1 (produits depuis le commit 405835d avec ce même fichier) ; sans modificateur,
/// le moteur actuel doit les reproduire au gameplay près des champs ajoutés (Lot81EngineBaselineTests). Ce fichier ne dépend que
/// d'API existant en 0.9.1 : il compile tel quel sur les deux versions.
/// </summary>
internal static class Lot81EngineBaselines
{
    public static readonly IReadOnlyList<(string Name, Func<CombatSetup> Setup)> All = new (string, Func<CombatSetup>)[]
    {
        ("rotation_20s", () => Setup(Rotation(20_000), seed: 2026, durationMs: 20_000)),
        ("garde_entretien_regeneration", GuardUpkeepAndRegeneration),
        ("parade_blocs_rupture", ParryBlocksAndBreak),
        ("alterations_auteur_cible", Statuses),
    };

    /// <summary>Rotation de ReproducibilityTests (journal de référence de l'empreinte ReferenceHash).</summary>
    private static IEnumerable<CombatCommand> Rotation(long durationMs)
    {
        for (long t = 0; t < durationMs; t += 1000)
        {
            yield return Cmd(t, Frappe);
            yield return Cmd(t, Heurt);
            yield return Cmd(t, Onde, target: null);
            yield return Cmd(t, Gratuite);
        }
    }

    /// <summary>Entretien 7/s, blocage gratuit, régénération 13/s après 500 ms : gardes longues, rupture par entretien, reprises, dépenses.</summary>
    private static CombatSetup GuardUpkeepAndRegeneration()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 7, GuardCostPerBlock = 0, EnduranceRegenPerSecond = 13, EnduranceRegenDelayMs = 500 };
        var commands = new[]
        {
            GuardStart(0), GuardStop(3_050), Cmd(3_500, Frappe, target: "assaillant"), GuardStart(5_000), GuardStop(6_000),
            Cmd(7_000, Heurt, target: "assaillant"), GuardStart(9_000),
        };
        return Setup(commands, new CombatantSetup[] { Tank(endurance: 10, actives: new[] { Frappe, Heurt, Onde, Gratuite }), Assailant(500, 700) },
            seed: 11, durationMs: 30_000, rules: rules);
    }

    /// <summary>
    /// Parades parfaites (Charges), parade manquée (récupération 300 ms), puis garde bloquant à 25 d'endurance par coup jusqu'à la
    /// rupture par impact avec des Charges détenues.
    /// </summary>
    private static CombatSetup ParryBlocksAndBreak()
    {
        var rules = new CombatRules { GuardCostPerBlock = 25, ParryFailureRecoveryMs = 300 };
        var commands = new[]
        {
            Parry(950), Parry(1_950), Parry(2_900), Parry(3_500), GuardStart(4_500), Cmd(12_000, Frappe, target: "assaillant"),
        };
        return Setup(commands, new CombatantSetup[] { Tank(), Assailant(1_000, 1_000) }, seed: 5, durationMs: 15_000, rules: rules);
    }

    /// <summary>Altérations appliquées par le personnage (DOT, contrôles) et par un assaillant sur le personnage.</summary>
    private static CombatSetup Statuses()
    {
        var commands = new[]
        {
            Cmd(0, Brasier), Cmd(1_000, Entaille), Cmd(2_000, Assommer), Cmd(4_000, Givre), Cmd(6_000, Brasier), Cmd(9_000, Assommer),
            Cmd(10_000, Entaille, target: "assaillant"),
        };
        var assailant = Assailant(1_500, 2_500, attacks: new[]
        {
            Strike("brulure", 40, Domain.Stats.DamageType.Magique, applies: Apply("burn", "0.5")),
            Strike("assommoir", 10, applies: Apply("stun")),
        });
        CharacterSetup character = Character(attributes: Attrs(vitalite: 200), actives: new[] { Brasier, Entaille, Assommer, Givre });
        return Setup(commands, new CombatantSetup[] { character, Dummy(), assailant }, seed: 3, durationMs: 15_000);
    }
}
