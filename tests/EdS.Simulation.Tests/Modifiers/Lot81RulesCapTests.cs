using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Modifiers;

/// <summary>
/// Décision du 18/10/2026 (question 2 du rapport 8.1) : les plafonds de réduction (garde 75 %, total 85 %) sont des paramètres des
/// règles (<c>combat.guard.maxDamageReduction</c>, <c>combat.damageTaken.maxReduction</c>). Une valeur déclarée de réduction de garde
/// est bornée par le plafond des règles au combat (la classe seule ne connaît que la borne d'une fraction) ; la valeur effective est
/// écrêtée au plafond des règles ; la réduction totale d'un impact suit le plafond des règles.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Limits")]
public class Lot81RulesCapTests
{
    private static CombatSetup Solo(ClassDefinition definition, CombatRules? rules = null, IReadOnlyDictionary<string, int>? talents = null) =>
        Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Character(definition: definition, talents: talents), Dummy() }, rules: rules);

    private static ClassDefinition OnTalent(params ModifierDefinition[] modifiers) =>
        Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [TalentAlpha] = modifiers });

    [Theory]
    [InlineData("guard.damageReduction")]
    [InlineData("guard.reductionAtZeroEndurance")]
    public void Valeur_declaree_bornee_par_le_plafond_des_regles(string key)
    {
        // Plafond livré (0,75) : 0,75 admis, 0,7501 refusé au combat, avec le chemin interne et la clé des règles dans le message.
        Assert.Empty(Solo(OnTalent(Mod(key, "0.75"))).Validate());
        Assert.Empty(Solo(OnTalent(Mod(key, "-0.75"))).Validate());
        foreach (string value in new[] { "0.7501", "-0.7501" })
        {
            RuleViolation error = Assert.Single(Solo(OnTalent(Mod(key, value))).Validate());
            Assert.Equal((RuleViolationKind.InvalidValue, "combatants[0].class"), (error.Kind, Assert.Single(error.Fields)));
            Assert.Contains("trees[0].talents[0].modifiers[0].add", error.Message);
            Assert.Contains("combat.guard.maxDamageReduction", error.Message);
        }
        // Rang maximal (5) : base 0,7 + 4 × 0,0125 = 0,75 admis ; + 4 × 0,013 = 0,752 refusé, même si le build n'a que le rang 1.
        Assert.Empty(Solo(OnTalent(Mod(key, "0.7", "0.0125"))).Validate());
        Assert.Single(Solo(OnTalent(Mod(key, "0.7", "0.013"))).Validate());
        // Plafond relevé à 0,8 par les règles : 0,78 admis ; abaissé à 0,5 (base 0,5) : 0,5001 refusé.
        Assert.Empty(Solo(OnTalent(Mod(key, "0.78")), new CombatRules { MaxGuardDamageReduction = Fixed.Parse("0.8") }).Validate());
        var low = new CombatRules { MaxGuardDamageReduction = Fixed.Parse("0.5"), GuardDamageReduction = Fixed.Parse("0.5") };
        Assert.Empty(Solo(OnTalent(Mod(key, "0.5")), low).Validate());
        Assert.Single(Solo(OnTalent(Mod(key, "0.5001")), low).Validate());
    }

    [Fact]
    public void Plafond_des_regles_ecrete_la_reduction_de_garde_effective()
    {
        // Deux sources légales (0,1 au talent alpha, 0,1 au talent bêta) : 0,6 + 0,2 = 0,8, écrêtée à 0,75 avec les règles livrées,
        // à 0,8 avec un plafond de 0,8. Valeur effective journalisée sur la ligne combatant.
        ClassDefinition definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]>
        {
            [TalentAlpha] = new[] { Mod("guard.damageReduction", "0.1") },
            [TalentBeta] = new[] { Mod("guard.damageReduction", "0.1") },
        });
        var talents = new Dictionary<string, int> { [TalentAlpha] = 1, [TalentBeta] = 1 };
        Assert.Contains("\"guard.damageReduction\":0.75", Run(Solo(definition, talents: talents)).Log.Lines[1]);
        Assert.Contains("\"guard.damageReduction\":0.8",
            Run(Solo(definition, new CombatRules { MaxGuardDamageReduction = Fixed.Parse("0.8") }, talents)).Log.Lines[1]);
    }

    [Fact]
    public void Plafond_total_des_regles_applique_aux_degats_subis()
    {
        // damageTaken −0,9 sur un passif : sans garde, la réduction totale vaut min(plafond, 0,9). Un seul coup de l'assaillant à 1 s
        // (100 bruts, armure 0) : 15 dégâts avec le plafond livré (0,85), 10 avec un plafond de 0,9, 50 avec un plafond de 0,5.
        ClassDefinition definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("damageTaken", "-0.9") } });
        foreach (var (cap, expected) in new[] { ("0.85", 15L), ("0.9", 10L), ("0.5", 50L) })
        {
            CombatSetup setup = Setup(Array.Empty<CombatCommand>(),
                new CombatantSetup[] { Character(definition: definition, passives: new[] { Passif }), Assailant() },
                durationMs: 1050, rules: new CombatRules { MaxTotalDamageReduction = Fixed.Parse(cap) });
            CombatResult result = Run(setup).Result;
            Assert.Equal(expected, result.Combatants[0].DamageTaken);
        }
    }
}
