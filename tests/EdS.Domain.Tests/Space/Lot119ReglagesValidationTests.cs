using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Builds;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.9 : validation des réglages issus de l'essai de Damien du 22 octobre 2026 (poussée d'une bande, arrêt pendant l'annonce,
/// meute, obstacle bas), au seul validateur, avec les chemins de la forme du fichier.
/// </summary>
[Trait("Category", "Behavior")]
public class Lot119ReglagesValidationTests
{
    private static readonly CombatDataIdentity Identity = new(new DataFileIdentity("rules", "0.1.0-test", "sha256:r"), Array.Empty<DataFileIdentity>());

    private static Fixed F(string v) => Fixed.Parse(v);

    private static CombatantPlacement At(string x, string z) => new(GroundVector.Parse(x, z), F("0.5"));

    private static IReadOnlyList<RuleViolation> Errors(AssailantAttack attack, AssailantBehavior? behavior = null) =>
        new CombatSetup(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[]
            {
                new CharacterSetup("bastion", "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: At("0", "0")),
                new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000, new[] { attack }, placement: At("5", "0"),
                    behavior: behavior),
            }, Array.Empty<CombatCommand>(), Identity,
            new CombatSpace(GroundVector.Parse("-10", "-10"), GroundVector.Parse("10", "10"), Array.Empty<SpaceObstacle>())).Validate();

    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    private static AssailantAttack Shaped(AttackShape shape, Fixed? push = null, bool hold = true) =>
        new("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null, shape, 1000,
            range: shape is AttackShape.Line or AttackShape.Target ? F("6") : null, radius: shape == AttackShape.Circle ? F("2") : null,
            width: shape == AttackShape.Line ? F("1") : null, push: push, holdPosition: hold);

    [Fact]
    public void Poussee_seulement_sur_une_bande_et_dans_les_bornes()
    {
        Assert.Empty(Errors(Shaped(AttackShape.Line, F("2"))));
        Assert.Empty(Errors(Shaped(AttackShape.Line, F("100"))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(Shaped(AttackShape.Circle, F("2")))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(Shaped(AttackShape.Target, F("2")))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(Shaped(AttackShape.Line, F("0")))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(Shaped(AttackShape.Line, F("-1")))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(Shaped(AttackShape.Line, F("100.0001")))));
        Assert.Equal("combatants[1].attacks[0].push", Only(Errors(new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null,
            push: F("2")))));
    }

    [Fact]
    public void Arret_pendant_l_annonce_seulement_pour_une_attaque_a_forme()
    {
        Assert.Empty(Errors(Shaped(AttackShape.Circle, hold: false)));
        Assert.Empty(Errors(Shaped(AttackShape.Line, hold: false)));
        Assert.Equal("combatants[1].attacks[0].holdPosition", Only(Errors(new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false,
            null, holdPosition: false))));
        // Par défaut, l'ennemi s'arrête pendant l'annonce et suit la meute (comportement du sous-lot 11.5).
        Assert.True(new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null).HoldPosition);
        Assert.True(new AssailantBehavior(F("1")).Pack);
        Assert.Empty(Errors(Shaped(AttackShape.Line), new AssailantBehavior(F("1"), pack: false)));
    }

    [Fact]
    public void Obstacle_bas_porte_par_la_definition()
    {
        var points = new[] { GroundVector.Parse("1", "1"), GroundVector.Parse("2", "1"), GroundVector.Parse("2", "2") };
        Assert.False(new SpaceObstacle("rocher", points).Low);
        Assert.True(new SpaceObstacle("riviere", points, low: true).Low);
    }
}
