using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Builds;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4a : validation d'un combat positionné, seul validateur (<see cref="CombatSetup.Validate()"/>). Espace, obstacles, places
/// de départ, commandes de déplacement et paramètres de déplacement et de contact des règles, avec des chemins de la forme du fichier.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aSpaceValidationTests
{
    private static readonly CombatDataIdentity Identity = new(new DataFileIdentity("rules", "0.1.0-test", "sha256:r"), Array.Empty<DataFileIdentity>());

    private static GroundVector P(string x, string z) => GroundVector.Parse(x, z);

    private static CombatantPlacement At(string x, string z, string radius = "0.5", GroundVector? facing = null, string? speed = null) =>
        new(P(x, z), Fixed.Parse(radius), facing, speed is null ? null : Fixed.Parse(speed));

    private static CharacterSetup Player(string id, CombatantPlacement? placement) =>
        new(id, "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: placement);

    private static AssailantSetup Creature(string id, CombatantPlacement? placement) =>
        new(id, "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000, new[] { new AssailantAttack("coup", 10) }, placement: placement);

    private static DummySetup Dummy(string id, CombatantPlacement? placement) => new(id, "cibles", Fixed.Zero, Fixed.Zero, placement: placement);

    private static CombatSpace Arena(params SpaceObstacle[] obstacles) => new(P("-10", "-10"), P("10", "10"), obstacles);

    private static SpaceObstacle Rock(string id = "rocher") => new(id, new[] { P("1", "-1"), P("3", "-1"), P("3", "1"), P("1", "1") });

    private static IReadOnlyList<RuleViolation> Errors(CombatSpace? space, IEnumerable<CombatantSetup>? combatants = null,
        IEnumerable<CombatCommand>? commands = null, CombatRules? rules = null) =>
        new CombatSetup(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants ?? new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", "0.6")) },
            commands ?? Array.Empty<CombatCommand>(), Identity, space).Validate();

    /// <summary>Chemin unique de l'erreur unique.</summary>
    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    [Fact]
    public void Combat_positionne_valide()
    {
        Assert.Empty(Errors(Arena(Rock())));
        Assert.Empty(Errors(Arena(), commands: new[] { new CombatCommand(0, "bastion", P("1", "0")), new CombatCommand(500, "bastion", CombatAction.Stop) }));
    }

    [Fact]
    public void Positions_et_espace_vont_ensemble()
    {
        Assert.Equal("combatants[1].position", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", null) })));
        Assert.Equal("combatants[0].position", Only(Errors(null, new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", null) })));
        Assert.Equal("commands[0].action", Only(Errors(null, new CombatantSetup[] { Player("bastion", null), Creature("loup", null) },
            new[] { new CombatCommand(0, "bastion", P("1", "0")) })));
        Assert.Equal("commands[0].action", Only(Errors(null, new CombatantSetup[] { Player("bastion", null), Creature("loup", null) },
            new[] { new CombatCommand(0, "bastion", CombatAction.Stop) })));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1.0001", "0")]
    [InlineData("0", "-1.5")]
    public void Direction_de_marche_invalide(string x, string z) =>
        Assert.Equal("commands[0].direction", Only(Errors(Arena(), commands: new[] { new CombatCommand(0, "bastion", P(x, z)) })));

    [Fact]
    public void Deplacement_sans_direction_impossible_a_construire() =>
        Assert.Throws<ArgumentException>(() => new CombatCommand(0, "bastion", CombatAction.Move));

    [Theory]
    [InlineData("0.0999", "radius")]
    [InlineData("5.0001", "radius")]
    public void Rayon_borne(string radius, string field) =>
        Assert.Equal($"combatants[0].{field}", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0", radius)), Creature("loup", At("5", "0")) })));

    [Fact]
    public void Orientation_nulle_ou_hors_bornes_refusee()
    {
        Assert.Equal("combatants[0].facing", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0", facing: P("0", "0"))), Creature("loup", At("5", "0")) })));
        Assert.Equal("combatants[1].facing", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", facing: P("2", "0"))) })));
    }

    [Fact]
    public void Vitesse_propre_reservee_aux_assaillants_et_bornee()
    {
        Assert.Equal("combatants[0].speed", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0", speed: "6")), Creature("loup", At("5", "0")) })));
        Assert.Equal("combatants[1].speed", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Dummy("loup", At("5", "0", speed: "6")) })));
        Assert.Equal("combatants[1].speed", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", speed: "20.0001")) })));
        Assert.Equal("combatants[1].speed", Only(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", speed: "0.0999")) })));
        Assert.Empty(Errors(Arena(), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", speed: "20")) }));
    }

    [Theory]
    [InlineData("9.5", "0", true)]        // emprise qui touche le bord : admise
    [InlineData("9.5001", "0", false)]
    [InlineData("0", "-9.5001", false)]
    [InlineData("0.7", "-1.4", true)]     // touche le sommet (1, −1) du rocher : distance 0,5 exactement
    [InlineData("0.5", "0", true)]        // touche la face ouest du rocher
    [InlineData("0.5001", "0", false)]    // chevauche le rocher
    [InlineData("2", "0", false)]         // dedans
    public void Emprise_dans_l_aire_sans_chevaucher_le_decor(string x, string z, bool valid)
    {
        IReadOnlyList<RuleViolation> errors = Errors(Arena(Rock()), new CombatantSetup[] { Player("bastion", At(x, z)), Creature("loup", At("-5", "5")) });
        if (valid) Assert.Empty(errors);
        else Assert.Equal("combatants[0].position", Only(errors));
    }

    [Fact]
    public void Position_au_dela_des_bornes_techniques()
    {
        RuleViolation error = Assert.Single(Errors(new CombatSpace(P("-1000", "-1000"), P("1000", "1000")),
            new CombatantSetup[] { Player("bastion", At("1000.0001", "0")), Creature("loup", At("5", "0")) }));
        Assert.Equal((RuleViolationKind.NumericLimit, "combatants[0].position"), (error.Kind, error.Fields.Single()));
    }

    [Fact]
    public void Chevauchement_au_depart_selon_la_regle_de_contact()
    {
        CombatantSetup[] players = { Player("bastion", At("0", "0")), Player("alice", At("0.5", "0")), Creature("loup", At("5", "0")) };
        Assert.Empty(Errors(Arena(), players));   // joueurs : jamais solides entre eux
        CombatantSetup[] contact = { Player("bastion", At("0", "0")), Creature("loup", At("1", "0", "0.6")) };
        Assert.Equal("combatants[1].position", Only(Errors(Arena(), contact)));   // règle A : solides
        var ruleB = new CombatRules { PlayerCreatureContact = ContactRule.SlowedCrossing };
        Assert.Empty(Errors(Arena(), contact, rules: ruleB));                       // règle B : traversables
        CombatantSetup[] large = { Player("bastion", At("0", "0")), Creature("loup", At("1.5", "0", "1.2")) };
        Assert.Equal("combatants[1].position", Only(Errors(Arena(), large, rules: ruleB)));   // grande créature : solide même en B
        CombatantSetup[] creatures = { Player("bastion", At("0", "0")), Creature("loup", At("5", "0", "0.6")), Dummy("souche", At("5.5", "0", "0.6")) };
        Assert.Equal("combatants[2].position", Only(Errors(Arena(), creatures, rules: ruleB)));   // créatures : solides entre elles
        CombatantSetup[] touching = { Player("bastion", At("0", "0")), Creature("loup", At("1.1", "0", "0.6")) };
        Assert.Empty(Errors(Arena(), touching));   // bords qui se touchent : admis
    }

    [Fact]
    public void Aire_et_obstacles_invalides()
    {
        Assert.Equal("space.bounds", Only(Errors(new CombatSpace(P("10", "-10"), P("-10", "10")))));
        Assert.Equal("space.bounds", Only(Errors(new CombatSpace(P("-1000.0001", "-10"), P("10", "10")))));
        Assert.Equal("space.obstacles[1].id", Only(Errors(Arena(Rock(), new SpaceObstacle("rocher", Rock().Points.Select(p => new GroundVector(p.X, p.Z + Fixed.FromInt(5))))))));
        Assert.Equal("space.obstacles[0].id", Only(Errors(Arena(new SpaceObstacle("Rocher", Rock().Points)))));
        Assert.Equal("space.obstacles[0].points", Only(Errors(Arena(new SpaceObstacle("mur", new[] { P("1", "1"), P("2", "1") })))));
        Assert.Equal("space.obstacles[0].points", Only(Errors(Arena(new SpaceObstacle("mur", new[] { P("1", "1"), P("3", "1"), P("2", "1.2"), P("3", "3"), P("1", "3") })))));
        Assert.Equal("space.obstacles[0].points", Only(Errors(Arena(new SpaceObstacle("mur", new[] { P("1", "1"), P("1000.5", "1"), P("1", "3") })))));
        RuleViolation tooMany = Assert.Single(Errors(Arena(Enumerable.Range(0, 65).Select(i => new SpaceObstacle($"o{i}",
            new[] { P("-9", "-9"), P("-8.9", "-9"), P("-9", "-8.9") })).ToArray())));
        Assert.Equal((RuleViolationKind.LimitExceeded, "space.obstacles"), (tooMany.Kind, tooMany.Fields.Single()));
        // Un obstacle en sens horaire est valide.
        Assert.Empty(Errors(Arena(new SpaceObstacle("rocher", Rock().Points.Reverse()))));
    }

    [Fact]
    public void Regles_de_deplacement_et_de_contact_bornees()
    {
        string Field(CombatRules rules) => Assert.Single(Assert.Single(rules.Validate()).Fields);
        Assert.Empty(CombatRules.Provisional.Validate());
        Assert.Equal((Fixed.FromInt(5), Fixed.Parse("0.5"), ContactRule.Solid, Fixed.Parse("0.3"), Fixed.One),
            (CombatRules.Provisional.MovementSpeed, CombatRules.Provisional.GuardSpeedFactor, CombatRules.Provisional.PlayerCreatureContact,
                CombatRules.Provisional.CrossingSpeedFactor, CombatRules.Provisional.LargeCreatureRadius));
        Assert.Equal(nameof(CombatRules.MovementSpeed), Field(new CombatRules { MovementSpeed = Fixed.Parse("0.0999") }));
        Assert.Equal(nameof(CombatRules.MovementSpeed), Field(new CombatRules { MovementSpeed = Fixed.Parse("20.0001") }));
        Assert.Equal(nameof(CombatRules.GuardSpeedFactor), Field(new CombatRules { GuardSpeedFactor = Fixed.Parse("1.0001") }));
        Assert.Equal(nameof(CombatRules.GuardSpeedFactor), Field(new CombatRules { GuardSpeedFactor = Fixed.Parse("-0.0001") }));
        Assert.Empty(new CombatRules { GuardSpeedFactor = Fixed.Zero }.Validate());
        Assert.Equal(nameof(CombatRules.CrossingSpeedFactor), Field(new CombatRules { CrossingSpeedFactor = Fixed.Zero }));
        Assert.Equal(nameof(CombatRules.CrossingSpeedFactor), Field(new CombatRules { CrossingSpeedFactor = Fixed.Parse("1.0001") }));
        Assert.Equal(nameof(CombatRules.LargeCreatureRadius), Field(new CombatRules { LargeCreatureRadius = Fixed.Parse("0.0999") }));
        Assert.Equal(nameof(CombatRules.PlayerCreatureContact), Field(new CombatRules { PlayerCreatureContact = (ContactRule)7 }));
    }

    [Theory]
    [InlineData(true, "0.5", true, "0.5", ContactRule.Solid, false)]
    [InlineData(false, "0.6", false, "0.6", ContactRule.SlowedCrossing, true)]
    [InlineData(true, "0.5", false, "0.6", ContactRule.Solid, true)]
    [InlineData(true, "0.5", false, "0.6", ContactRule.SlowedCrossing, false)]
    [InlineData(false, "0.9999", true, "0.5", ContactRule.SlowedCrossing, false)]
    [InlineData(false, "1", true, "0.5", ContactRule.SlowedCrossing, true)]
    public void Solidite_des_paires(bool firstPlayer, string firstRadius, bool secondPlayer, string secondRadius, ContactRule rule, bool solid)
    {
        var rules = new CombatRules { PlayerCreatureContact = rule };
        Assert.Equal(solid, rules.AreSolid(firstPlayer, Fixed.Parse(firstRadius), secondPlayer, Fixed.Parse(secondRadius)));
        Assert.Equal(solid, rules.AreSolid(secondPlayer, Fixed.Parse(secondRadius), firstPlayer, Fixed.Parse(firstRadius)));
    }

    [Fact]
    public void Identifiants_des_actions_et_des_regles_de_contact()
    {
        Assert.Equal(new[] { "guardStart", "guardStop", "parry", "move", "stop", "select", "leave" }, CombatActionIds.Named);   // 11.4c : select ; 11.4d : leave
        Assert.Equal(new[] { "guardStart", "guardStop", "parry" }, CombatActionIds.Defensive);   // protocole du lot 10 inchangé
        Assert.True(CombatActionIds.TryParseNamed("move", out CombatAction move) && move == CombatAction.Move);
        Assert.False(CombatActionIds.TryParseDefensive("move", out _));
        Assert.Equal(("solid", "slowedCrossing"), (ContactRuleIds.Of(ContactRule.Solid), ContactRuleIds.Of(ContactRule.SlowedCrossing)));
        Assert.Equal(ContactRule.SlowedCrossing, ContactRuleIds.Parse("slowedCrossing"));
    }
}
