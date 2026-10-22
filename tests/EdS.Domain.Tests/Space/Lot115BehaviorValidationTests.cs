using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Builds;

namespace EdS.Domain.Tests.Space;

/// <summary>Sous-lot 11.5 : validation des comportements d'ennemi et de la charge (seul validateur, chemins de la forme du fichier).</summary>
[Trait("Category", "Behavior")]
public class Lot115BehaviorValidationTests
{
    private static readonly CombatDataIdentity Identity = new(new DataFileIdentity("rules", "0.1.0-test", "sha256:r"), Array.Empty<DataFileIdentity>());

    private static Fixed F(string v) => Fixed.Parse(v);

    private static CombatantPlacement At(string x, string z) => new(GroundVector.Parse(x, z), F("0.5"));

    private static readonly AssailantAttack Bite = new("morsure", 10, DamageType.Physique, SkillTargeting.Enemy, false, null);

    private static IReadOnlyList<RuleViolation> Errors(AssailantBehavior? behavior, string? target = null, bool spatial = true, IEnumerable<CombatCommand>? commands = null,
        AssailantAttack? attack = null) =>
        new CombatSetup(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[]
            {
                new CharacterSetup("bastion", "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: spatial ? At("0", "0") : null),
                new AssailantSetup("loup", "meute", Fixed.Zero, Fixed.Zero, target, 1000, 2000, new[] { attack ?? Bite }, placement: spatial ? At("5", "0") : null,
                    behavior: behavior),
            }, commands ?? Array.Empty<CombatCommand>(), Identity,
            spatial ? new CombatSpace(GroundVector.Parse("-10", "-10"), GroundVector.Parse("10", "10"), Array.Empty<SpaceObstacle>()) : null).Validate();

    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    [Fact]
    public void Comportements_valides_et_cible_facultative()
    {
        Assert.Empty(Errors(new AssailantBehavior(F("0.5"))));
        Assert.Empty(Errors(new AssailantBehavior(F("9"), F("6"), F("12"), encircle: true), target: "bastion"));
        Assert.Empty(Errors(new AssailantBehavior(F("0"), F("0"))));   // au contact
        Assert.Equal("combatants[1].target", Only(Errors(null)));        // sans comportement, une attaque à cible unique exige une cible
    }

    [Fact]
    public void Distances_et_combat_positionne()
    {
        Assert.Equal("combatants[1].behavior", Only(Errors(new AssailantBehavior(F("1")), target: "bastion", spatial: false)));
        Assert.Equal("combatants[1].behavior.maxRange", Only(Errors(new AssailantBehavior(F("100.0001")))));
        Assert.Equal("combatants[1].behavior.maxRange", Only(Errors(new AssailantBehavior(F("-1")))));
        Assert.Equal("combatants[1].behavior.minRange", Only(Errors(new AssailantBehavior(F("2"), F("3")))));
        Assert.Equal("combatants[1].behavior.aggroRange", Only(Errors(new AssailantBehavior(F("2"), aggroRange: F("0")))));
        Assert.Empty(Errors(new AssailantBehavior(F("2"), aggroRange: F("100"))));
    }

    [Fact]
    public void Un_ennemi_qui_a_un_comportement_se_deplace_seul()
    {
        var move = new CombatCommand(0, "loup", GroundVector.Parse("1", "0"));
        Assert.Equal("commands[0].action", Only(Errors(new AssailantBehavior(F("1")), commands: new[] { move })));
        Assert.Equal("commands[0].action", Only(Errors(new AssailantBehavior(F("1")), commands: new[] { new CombatCommand(0, "loup", CombatAction.Stop) })));
        Assert.Empty(Errors(null, target: "bastion", commands: new[] { move }));   // sans comportement : commandé par le scénario
        Assert.Empty(Errors(new AssailantBehavior(F("1")), commands: new[] { new CombatCommand(0, "loup", CombatAction.Leave) }));
    }

    [Fact]
    public void Charge_seulement_sur_une_bande()
    {
        AssailantAttack Shaped(AttackShape shape, bool charge) => new("charge", 10, DamageType.Physique, SkillTargeting.Enemy, false, null, shape, 3000,
            range: shape is AttackShape.Line or AttackShape.Target ? F("6") : null, radius: shape == AttackShape.Circle ? F("2") : null,
            width: shape == AttackShape.Line ? F("1") : null, charge: charge);
        Assert.Empty(Errors(null, target: "bastion", attack: Shaped(AttackShape.Line, true)));
        Assert.Equal("combatants[1].attacks[0].charge", Only(Errors(null, target: "bastion", attack: Shaped(AttackShape.Circle, true))));
        Assert.Equal("combatants[1].attacks[0].charge", Only(Errors(null, target: "bastion", attack: Shaped(AttackShape.Target, true))));
        Assert.Equal("combatants[1].attacks[0].charge", Only(Errors(null, target: "bastion",
            attack: new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null, charge: true))));
    }
}
