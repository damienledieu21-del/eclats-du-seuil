using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Correctif 6.1, M04 : les limites numériques sont vérifiées par <see cref="CombatSetup.Validate"/>, avant toute
/// construction de l'état du combat. Avant, des dégâts bruts supérieurs à la capacité de <c>Fixed.FromInt</c> passaient
/// la validation puis faisaient échouer la construction du moteur (exception non prévue) ; un combat accepté peut
/// désormais toujours être construit, et un combat refusé l'est avant le premier tick, sans rien journaliser. La simulation
/// elle-même n'est protégée que pour les calculs recensés par le précontrôle (complété au correctif 6.1.1, voir CaM01NumericEnvelopeTests).
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Regression")]
public class M04InitializationLimitsTests
{
    private static void AssertRefused(CombatSetup setup, string field)
    {
        Assert.Contains(setup.Validate(), v => v.Fields.Contains(field));
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Contains(error.Violations, v => v.Fields.Contains(field));
        Assert.Empty(log.Lines);
    }

    private static CombatSetup WithAssailant(AssailantSetup assailant, long durationMs = 10_000) =>
        Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), assailant }, durationMs: durationMs);

    [Theory]
    [InlineData(CombatantStats.MaxRawDamage + 1)]
    [InlineData(long.MaxValue / Fixed.Scale + 1)]   // au-delà de Fixed.FromInt : levait OverflowException à la construction du moteur
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    public void Degats_bruts_hors_limites_refuses_avant_le_premier_tick(long raw) =>
        AssertRefused(WithAssailant(Assailant(attacks: Blow(raw))), "combatants[1].attacks[0].rawDamage");

    [Fact]
    public void Degats_bruts_a_la_limite_acceptes_et_simules()
    {
        var setup = WithAssailant(Assailant(attacks: new[] { Blow(CombatantStats.MaxRawDamage, fragility: true, id: "a"), Blow(CombatantStats.MaxRawDamage, id: "b") }));
        Assert.Empty(setup.Validate());
        var (result, _) = Run(setup);
        Assert.Equal(CombatEndReason.TeamDefeated, result.EndReason);   // aucune interruption « overflow »
    }

    [Theory]
    [InlineData("dummy")]
    [InlineData("assailant")]
    public void Defenses_hors_limites_refusees_pour_mannequins_et_assaillants(string type)
    {
        foreach (Fixed armor in new[] { Fixed.MaxValue, CombatantStats.MaxDefense + Fixed.Epsilon, -Fixed.Epsilon, Fixed.MinValue })
        {
            CombatantSetup target = type == "dummy"
                ? new DummySetup("cible", "cibles", armor, armor)
                : new AssailantSetup("cible", "cibles", armor, armor, "bastion", 1000, 2000, new[] { Blow() });
            var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), target });
            AssertRefused(setup, "combatants[1].armor");
            Assert.Contains(setup.Validate(), v => v.Fields.Contains("combatants[1].magicResistance"));
        }
        CombatantSetup limit = type == "dummy"
            ? new DummySetup("cible", "cibles", CombatantStats.MaxDefense, CombatantStats.MaxDefense)
            : new AssailantSetup("cible", "cibles", CombatantStats.MaxDefense, CombatantStats.MaxDefense, "bastion", 1000, 2000, new[] { Blow() });
        var accepted = Setup(new[] { Cmd(0, Frappe, target: "cible") }, new CombatantSetup[] { Tank(), limit });
        Assert.Empty(accepted.Validate());
        Assert.NotEqual(CombatEndReason.Aborted, Run(accepted).Result.EndReason);
    }

    [Fact]
    public void Personnage_attributs_ou_statistiques_hors_limites_refuse()
    {
        // Attributs au-delà du plafond du build : refus par le validateur de builds.
        AssertRefused(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character(attributes: new Attributes(int.MaxValue, 0, 0, int.MaxValue, 0, int.MaxValue)), Dummy() }),
            "combatants[0].build");
        // Attributs légaux mais règles qui font déborder les statistiques dérivées : refus avant le moteur.
        var stats = new StatRules { MaxHealthPerVitalite = Fixed.FromInt(1_000_000_000) };
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { Character(attributes: Attrs(vitalite: BuildValidator.MaxAttributeValue)), Dummy() }, Array.Empty<CombatCommand>(), Identity);
        AssertRefused(setup, "combatants[0].build.attributes");
    }

    [Fact]
    public void Pire_impact_d_un_personnage_refuse_si_il_deborde()
    {
        // Puissance énorme et coefficient maximal : le pire impact (critique, Fragilité, aucune défense) déborde.
        var stats = new StatRules { PhysicalPowerPerForce = Fixed.FromInt(1_000_000) };   // 10¹² de puissance : calculable
        var definition = Class(frappeCoefficient: "100");
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { Character(definition: definition, attributes: Attrs(force: BuildValidator.MaxAttributeValue)), Dummy() },
            new[] { Cmd(0, Frappe) }, Identity);
        AssertRefused(setup, "combatants[0]");
    }

    [Fact]
    public void Cumul_des_degats_sur_la_duree_verifie()
    {
        // Pire impact × ticks × événements par tick doit tenir sur 64 bits : avec 10⁹ événements par tick, refus.
        var rules = new CombatRules { MaxEventsPerTick = int.MaxValue };
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Assailant(attacks: Blow(CombatantStats.MaxRawDamage)) },
            durationMs: 600_000, rules: rules);
        AssertRefused(setup, "combatants[1]");
    }

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("100.0001")]
    [InlineData("922337203685477")]
    public void Coefficient_de_competence_hors_limites(string coefficient) =>
        Assert.Contains(Class(frappeCoefficient: coefficient).Validate(ProgressionRules.Provisional), v => v.Fields.Contains("skills[0].combat.coefficient"));

    [Fact]
    public void Validation_et_moteur_calculent_les_memes_statistiques()
    {
        // La validation calcule exactement ce que le moteur calculera : mêmes valeurs dans l'en-tête du journal.
        var assailant = Assailant(attacks: Blow(777));
        var events = Events(Run(WithAssailant(assailant)).Log);
        var logged = events.OfKind("attackStarted").First();
        Assert.Equal(777, logged.Int("rawDamage"));
        Assert.Equal(777, events.OfKind("damage").First().Int("amount"));
        Assert.Equal(Fixed.FromInt(777), CombatantStats.ForAssailantAttack(StatRules.Provisional, 777).PhysicalPower);
    }
}
