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
/// Sous-lot 8.4 (X8) : Contrecoup dépense toutes les Charges détenues, au moins une, et frappe à 0,6 + 0,4 par Charge dépensée. Une
/// compétence à coût fixe ajoute aussi son coefficient par Charge dépensée. Refus atomique sans Charge ; précontrôle numérique au
/// maximum de Charges du personnage.
/// </summary>
[Trait("Category", "Bastion")]
public class Lot84ChargeTests
{
    /// <summary><paramref name="charges"/> lancers de Charger (un par tick), puis <paramref name="skill"/> sur le mannequin.</summary>
    private static (List<JsonElement> Events, CharacterSetup Hero) Spend(int charges, string skill = Contrecoup, ClassDefinition? definition = null)
    {
        CharacterSetup hero = Hero(definition: definition);
        var commands = Enumerable.Range(0, charges).Select(i => Cmd(50L * i, Charger, target: null)).Append(Cmd(50L * charges, skill)).ToArray();
        var events = Simulate(Fight(commands, new CombatantSetup[] { hero, Dummy(armor: 0) }, durationMs: 1000), out _);
        return (events, hero);
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(2, "1.4")]
    [InlineData(3, "1.8")]
    [InlineData(5, "2.6")]   // maximum de Charges des règles provisoires
    public void Contrecoup_depense_toutes_les_Charges(int charges, string coefficient)
    {
        var (events, hero) = Spend(charges);
        JsonElement cast = events.OfKind("skillCast").Single(c => c.Str("skill") == Contrecoup);
        Assert.Equal((charges, 0L), (cast.Long("rampartChargeCost"), cast.Long("rampartCharges")));
        JsonElement hit = events.OfKind("damage").Single();
        Assert.Equal(Expected(hero, coefficient, hit.GetProperty("critical").GetBoolean()), hit.Long("amount"));
    }

    [Fact]
    public void Contrecoup_sans_Charge_refuse_sans_rien_modifier()
    {
        // Charges insuffisantes : refus, ni endurance ni recharge ; une Charge plus tard, le même lancer passe.
        var events = Simulate(Fight(new[] { Cmd(0, Contrecoup), Cmd(50, Charger, target: null), Cmd(100, Contrecoup) },
            new CombatantSetup[] { Hero(), Dummy(armor: 0) }, durationMs: 300), out CombatResult result);
        Assert.Equal("insufficientRampartCharges", events.OfKind("commandRejected").Single().Str("reason"));
        JsonElement cast = events.OfKind("skillCast").Single(c => c.Str("skill") == Contrecoup);
        Assert.Equal((2L, 1L, 90L), (cast.Tick(), cast.Long("rampartChargeCost"), cast.Long("endurance")));
        Assert.Equal(1, result["bastion"].CommandsRejected);
    }

    [Fact]
    public void Cout_fixe_coefficient_par_Charge_depensee()
    {
        // Trois Charges, coût fixe de 2 : deux dépensées (0,6 + 0,8), une reste.
        var (events, hero) = Spend(3, Fixe);
        JsonElement cast = events.OfKind("skillCast").Single(c => c.Str("skill") == Fixe);
        Assert.Equal((2L, 1L), (cast.Long("rampartChargeCost"), cast.Long("rampartCharges")));
        JsonElement hit = events.OfKind("damage").Single();
        Assert.Equal(Expected(hero, "1.4", hit.GetProperty("critical").GetBoolean()), hit.Long("amount"));
        // Une seule Charge : refusé (coût fixe exigé).
        Assert.Equal("insufficientRampartCharges", Spend(1, Fixe).Events.OfKind("commandRejected").Single().Str("reason"));
    }

    [Fact]
    public void Modificateur_de_coefficient_et_Charges_maximales()
    {
        // skill.<id>.coefficient +0,1 et rampartCharges.max +5 : dix Charges, 0,6 + 4 + 0,1.
        ClassDefinition definition = KitClass(new[] { Mod($"{Contrecoup}.coefficient", "0.1"), Mod("rampartCharges.max", "5") });
        var (events, hero) = Spend(10, definition: definition);
        JsonElement hit = events.OfKind("damage").Single();
        Assert.Equal(10L, events.OfKind("skillCast").Single(c => c.Str("skill") == Contrecoup).Long("rampartChargeCost"));
        Assert.Equal(Expected(hero, "4.7", hit.GetProperty("critical").GetBoolean()), hit.Long("amount"));
    }

    [Fact]
    public void Charges_conservees_par_un_refus_et_rejeu_identique()
    {
        // Contrecoup en recharge (8 s) : refusé, les Charges restent.
        ClassDefinition definition = KitClass(counter: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.Parse("0.6"), 10, 8000,
            SkillTargeting.Enemy, rampartChargeCost: 1, rampartChargeSpendAll: true, coefficientPerRampartCharge: Fixed.Parse("0.4")));
        CombatSetup setup = Fight(new[] { Cmd(0, Charger, target: null), Cmd(50, Contrecoup), Cmd(100, Charger, target: null), Cmd(150, Contrecoup) },
            new CombatantSetup[] { Hero(definition: definition), Dummy(armor: 0) }, durationMs: 300);
        var events = Simulate(setup, out CombatResult result);
        Assert.Equal("cooldown", events.OfKind("commandRejected").Single().Str("reason"));
        Assert.Equal(1, result["bastion"].RampartCharges);
        var (_, a) = Run(setup);
        var (_, b) = Run(setup);
        Assert.True(CombatReplay.Compare(a.Lines, b.Lines).Identical);
    }

    [Theory]
    [InlineData(true, false)]    // toutes les Charges (jusqu'à 5) : 0,6 + 10 × 5 = 50,6, cumul au-delà de 2⁶³
    [InlineData(false, true)]    // coût fixe d'une Charge : 0,6 + 10 = 10,6, admis
    public void Precontrole_au_maximum_de_Charges(bool all, bool accepted)
    {
        // Puissance de 10¹² (comme Lot73TriggerSetupTests) : 200 ticks × 1 000 événements × pire impact (coefficient × 10¹² × 1,5 × 1,1).
        var stats = new StatRules { PhysicalPowerPerForce = Fixed.FromInt(1_000_000) };
        ClassDefinition definition = KitClass(counter: Counter(perCharge: "10", all: all));
        CharacterSetup hero = new("bastion", "joueurs", definition, new CharacterBuild("class.essai84", 10, Attrs(force: BuildValidator.MaxAttributeValue),
            null, new[] { Charger, Contrecoup }, null, new[] { Passif84 }));
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { hero, Dummy(armor: 0) }, new[] { Cmd(0, Contrecoup) }, Identity);
        IReadOnlyList<RuleViolation> errors = setup.Validate();
        if (accepted) Assert.Empty(errors);
        else Assert.Equal(RuleViolationKind.NumericLimit, Assert.Single(errors).Kind);
    }
}
