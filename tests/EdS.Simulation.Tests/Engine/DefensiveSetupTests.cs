using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Validation d'un combat avec assaillant et commandes défensives, avant toute simulation.</summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
public class DefensiveSetupTests
{
    private static IReadOnlyList<RuleViolation> Validate(CombatantSetup assailant, params CombatCommand[] commands) =>
        Setup(commands, new CombatantSetup[] { Tank(), assailant }).Validate();

    private static AssailantSetup Make(string? target = "bastion", long first = 1000, long interval = 2000, params AssailantAttack[] attacks) =>
        new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, target, first, interval, attacks.Length == 0 ? new[] { Blow() } : attacks);

    [Fact]
    public void Assaillant_de_reference_valide() => Assert.Empty(Validate(Make()));

    [Theory]
    [InlineData("fantome", "combatants[1].target")]
    [InlineData("assaillant", "combatants[1].target")]
    [InlineData(null, "combatants[1].target")]
    public void Cible_inconnue_alliee_ou_absente(string? target, string path) =>
        Assert.Contains(Validate(Make(target)), v => v.Fields.Contains(path));

    [Fact]
    public void Attaques_de_zone_sans_cible_autorisees() =>
        Assert.Empty(Validate(Make(null, attacks: Blow(targeting: SkillTargeting.AllEnemies))));

    [Theory]
    [InlineData(-50, 2000, "combatants[1].firstAttackMs")]
    [InlineData(1010, 2000, "combatants[1].firstAttackMs")]
    [InlineData(1000, 0, "combatants[1].intervalMs")]
    [InlineData(1000, 2020, "combatants[1].intervalMs")]
    public void Rythme_hors_tick_ou_hors_bornes(long first, long interval, string path) =>
        Assert.Contains(Validate(Make(first: first, interval: interval)), v => v.Fields.Contains(path));

    [Fact]
    public void Attaques_invalides()
    {
        var violations = Validate(Make(attacks: new[] { Blow(-1), Blow(id: "coup"), new AssailantAttack("Mauvais id", 10) }));
        Assert.Contains(violations, v => v.Fields.Contains("combatants[1].attacks[0].rawDamage"));
        Assert.Contains(violations, v => v.Fields.Contains("combatants[1].attacks[1].id") && v.Kind == RuleViolationKind.DuplicateId);
        Assert.Contains(violations, v => v.Fields.Contains("combatants[1].attacks[2].id"));
        Assert.Contains(Validate(new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 0, 2000, Array.Empty<AssailantAttack>())),
            v => v.Fields.Contains("combatants[1].attacks"));
    }

    [Fact]
    public void Commande_defensive_sans_competence_ni_cible()
    {
        Assert.Empty(Validate(Make(), GuardStart(0), GuardStop(50), Parry(100)));
        Assert.Throws<ArgumentException>(() => new CombatCommand(0, "bastion", CombatAction.UseSkill));
    }

    [Fact]
    public void Identifiants_d_actions_stables()
    {
        Assert.Equal(new[] { "skill", "guardStart", "guardStop", "parry" },
            new[] { CombatAction.UseSkill, CombatAction.GuardStart, CombatAction.GuardStop, CombatAction.Parry }.Select(CombatActionIds.Of));
        Assert.True(CombatActionIds.TryParseDefensive("parry", out CombatAction parry) && parry == CombatAction.Parry);
        Assert.False(CombatActionIds.TryParseDefensive("skill", out _));
    }

    [Fact]
    public void En_tete_du_journal_decrit_l_assaillant_et_les_actions()
    {
        string header = CombatLogHeader.Write(Setup(new[] { Parry(100) }, new CombatantSetup[] { Tank(), Make(attacks: Blow(fragility: true)) }));
        Assert.Contains("\"type\":\"assailant\",\"team\":\"cibles\",\"initiative\":0,\"armor\":0,\"magicResistance\":0,\"statusResistance\":0,\"target\":\"bastion\",\"firstAttackMs\":1000,\"intervalMs\":2000,"
            + "\"attacks\":[{\"id\":\"coup\",\"rawDamage\":100,\"damageType\":\"physique\",\"targeting\":\"enemy\",\"unblockable\":false,\"applies\":[{\"status\":\"fragility\"}]}]", header);
        Assert.Contains("\"commands\":[{\"atMs\":100,\"actor\":\"bastion\",\"action\":\"parry\"}]", header);
    }
}
