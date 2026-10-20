using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Builds;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4c : validation des formes d'attaque, des annonces évitables, de la sélection et des nouvelles règles (seul validateur,
/// chemins de la forme du fichier) ; angles entiers comparés aux fonctions trigonométriques flottantes.
/// </summary>
[Trait("Category", "Space")]
public class Lot114cFormValidationTests
{
    private static readonly CombatDataIdentity Identity = new(new DataFileIdentity("rules", "0.1.0-test", "sha256:r"), Array.Empty<DataFileIdentity>());

    private static Fixed F(string v) => Fixed.Parse(v);

    private static CombatantPlacement At(string x, string z, string radius = "0.5") => new(GroundVector.Parse(x, z), F(radius));

    private static AssailantAttack Form(AttackShape? shape, long telegraphMs = 2000, string? range = null, string? radius = null, int? angle = null,
        string? width = null, ShapeCenter center = ShapeCenter.Target, long persistentMs = 0, long pulseMs = 0, bool? guardable = null, bool? parryable = null,
        bool unblockable = false, SkillTargeting targeting = SkillTargeting.Enemy) =>
        new("coup", 10, DamageType.Physique, targeting, unblockable, null, shape, telegraphMs, range is null ? null : F(range), radius is null ? null : F(radius),
            angle, width is null ? null : F(width), center, persistentMs, pulseMs, guardable, parryable);

    private static IReadOnlyList<RuleViolation> Errors(AssailantAttack attack, bool spatial = true, CombatRules? rules = null, bool withPlayer = true,
        IEnumerable<CombatCommand>? commands = null)
    {
        var combatants = new List<CombatantSetup>();
        if (withPlayer) combatants.Add(new CharacterSetup("bastion", "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: spatial ? At("0", "0") : null));
        else combatants.Add(new DummySetup("bastion", "joueurs", Fixed.Zero, Fixed.Zero, placement: spatial ? At("0", "0") : null));
        combatants.Add(new AssailantSetup("ours", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 3000, new[] { attack }, placement: spatial ? At("5", "0") : null));
        return new CombatSetup(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants, commands ?? Array.Empty<CombatCommand>(), Identity, spatial ? new CombatSpace(GroundVector.Parse("-10", "-10"), GroundVector.Parse("10", "10"), Array.Empty<SpaceObstacle>()) : null).Validate();
    }

    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    private const string A = "combatants[1].attacks[0]";

    [Fact]
    public void Formes_valides()
    {
        Assert.Empty(Errors(Form(null, telegraphMs: 0)));
        Assert.Empty(Errors(Form(AttackShape.Target, telegraphMs: 0)));
        Assert.Empty(Errors(Form(AttackShape.Target, telegraphMs: 600, range: "2.5", parryable: false)));
        Assert.Empty(Errors(Form(AttackShape.Cone, range: "4", angle: 90)));
        Assert.Empty(Errors(Form(AttackShape.Line, range: "6", width: "1", guardable: true)));
        Assert.Empty(Errors(Form(AttackShape.Circle, radius: "2", center: ShapeCenter.Self)));
        Assert.Empty(Errors(Form(AttackShape.Persistent, radius: "2", persistentMs: 3000, pulseMs: 500)));
        Assert.Empty(Errors(Form(null, telegraphMs: 0, parryable: false)));   // une attaque historique peut ne pas se parer
    }

    [Fact]
    public void Forme_seulement_dans_un_combat_positionne_et_sur_la_cible()
    {
        Assert.Equal(A + ".shape", Only(Errors(Form(AttackShape.Circle, radius: "2"), spatial: false)));
        Assert.Equal(A + ".targeting", Only(Errors(Form(AttackShape.Circle, radius: "2", targeting: SkillTargeting.AllEnemies))));
        Assert.Equal(A + ".shape", Only(Errors(Form((AttackShape)9))));
    }

    [Theory]
    [InlineData(AttackShape.Cone, "angle")]
    [InlineData(AttackShape.Line, "width")]
    [InlineData(AttackShape.Circle, "radius")]
    public void Dimension_obligatoire_de_chaque_forme(AttackShape shape, string field)
    {
        AssailantAttack attack = shape switch
        {
            AttackShape.Cone => Form(shape, range: "4"),
            AttackShape.Line => Form(shape, range: "4"),
            _ => Form(shape),
        };
        Assert.Equal($"{A}.{field}", Only(Errors(attack)));
        Assert.Equal(A + ".range", Only(Errors(Form(AttackShape.Cone, angle: 60))));
    }

    [Fact]
    public void Dimension_sans_objet_refusee()
    {
        Assert.Equal(A + ".radius", Only(Errors(Form(AttackShape.Cone, range: "4", angle: 60, radius: "2"))));
        Assert.Equal(A + ".angle", Only(Errors(Form(AttackShape.Circle, radius: "2", angle: 60))));
        Assert.Equal(A + ".width", Only(Errors(Form(AttackShape.Circle, radius: "2", width: "1"))));
        Assert.Equal(A + ".range", Only(Errors(Form(AttackShape.Circle, radius: "2", range: "3"))));
        Assert.Equal(A + ".center", Only(Errors(Form(AttackShape.Cone, range: "4", angle: 60, center: ShapeCenter.Self))));
        Assert.Equal(A + ".persistentMs", Only(Errors(Form(AttackShape.Circle, radius: "2", persistentMs: 1000))));
        Assert.Equal(A + ".tickMs", Only(Errors(Form(AttackShape.Line, range: "4", width: "1", pulseMs: 500))));
        Assert.Equal(A + ".telegraphMs", Only(Errors(Form(null, telegraphMs: 500))));
        Assert.Equal(A + ".radius", Only(Errors(Form(null, telegraphMs: 0, radius: "2"))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(181)]
    public void Ouverture_du_cone_bornee(int angle) => Assert.Equal(A + ".angle", Only(Errors(Form(AttackShape.Cone, range: "4", angle: angle))));

    [Fact]
    public void Durees_et_dimensions_bornees()
    {
        Assert.Equal(A + ".telegraphMs", Only(Errors(Form(AttackShape.Target, telegraphMs: 510))));
        Assert.Equal(A + ".telegraphMs", Only(Errors(Form(AttackShape.Target, telegraphMs: -50))));
        Assert.Equal(A + ".range", Only(Errors(Form(AttackShape.Target, telegraphMs: 0, range: "100.0001"))));
        Assert.Equal(A + ".tickMs", Only(Errors(Form(AttackShape.Persistent, radius: "2", persistentMs: 1000, pulseMs: 1500))));
        Assert.Equal(A + ".persistentMs", Only(Errors(Form(AttackShape.Persistent, radius: "2", persistentMs: 0, pulseMs: 500))));
        Assert.Equal(A + ".tickMs", Only(Errors(Form(AttackShape.Persistent, radius: "2", persistentMs: 1000, pulseMs: 30))));
    }

    [Fact]
    public void Defenses_declarees_coherentes()
    {
        Assert.Equal(A + ".parryable", Only(Errors(Form(AttackShape.Circle, radius: "2", parryable: true))));
        Assert.Equal(A + ".guardable", Only(Errors(Form(AttackShape.Persistent, radius: "2", persistentMs: 1000, pulseMs: 500, guardable: true))));
        Assert.Equal(A + ".guardable", Only(Errors(Form(AttackShape.Target, telegraphMs: 0, unblockable: true, guardable: true))));
        // Valeurs par défaut : coup dirigé gardé et paré ; zone ni l'un ni l'autre ; imparable ni l'un ni l'autre.
        Assert.Equal((true, true), (Form(AttackShape.Target).Guardable, Form(AttackShape.Target).Parryable));
        Assert.Equal((false, false), (Form(AttackShape.Cone, range: "4", angle: 60).Guardable, Form(AttackShape.Cone, range: "4", angle: 60).Parryable));
        Assert.Equal((true, false), (Form(AttackShape.Line, range: "4", width: "1", guardable: true).Guardable, Form(AttackShape.Line, range: "4", width: "1", guardable: true).Parryable));
        Assert.Equal((false, false), (Form(null, unblockable: true).Guardable, Form(null, unblockable: true).Parryable));
    }

    [Fact]
    public void Annonce_evitable_au_pas_de_base()
    {
        // Cercle de 2 m, joueur de 0,5 m, 5 m/s : 500 ms pour sortir, + 300 ms de réaction : 800 ms au moins.
        Assert.Empty(Errors(Form(AttackShape.Circle, telegraphMs: 800, radius: "2")));
        RuleViolation tooShort = Assert.Single(Errors(Form(AttackShape.Circle, telegraphMs: 750, radius: "2")));
        Assert.Equal(A + ".telegraphMs", tooShort.Fields.Single());
        Assert.Contains("au moins 800 ms", tooShort.Message);
        // Bande de 1 m : 0,5 + 0,5 m, 200 ms ; cône demi-disque de 4 m : cercle inscrit de 2 m, + 0,5 : 500 ms.
        Assert.Empty(Errors(Form(AttackShape.Line, telegraphMs: 500, range: "6", width: "1")));
        Assert.Single(Errors(Form(AttackShape.Line, telegraphMs: 450, range: "6", width: "1")));
        Assert.Empty(Errors(Form(AttackShape.Cone, telegraphMs: 800, range: "4", angle: 180)));
        Assert.Single(Errors(Form(AttackShape.Cone, telegraphMs: 750, range: "4", angle: 180)));
        // Réaction réglable, vitesse de base réglable ; sans joueur dans le combat, rien à juger.
        Assert.Empty(Errors(Form(AttackShape.Circle, telegraphMs: 500, radius: "2"), rules: new CombatRules { TelegraphMinReactionMs = 0 }));
        Assert.Single(Errors(Form(AttackShape.Circle, telegraphMs: 500, radius: "2"), rules: new CombatRules { TelegraphMinReactionMs = 0, MovementSpeed = F("4") }));
        Assert.Empty(Errors(Form(AttackShape.Circle, telegraphMs: 0, radius: "2"), withPlayer: false));
        // Un coup dirigé n'a pas à être évitable : on le garde ou on le pare.
        Assert.Empty(Errors(Form(AttackShape.Target, telegraphMs: 0)));
    }

    [Fact]
    public void Selection_seulement_dans_un_combat_positionne()
    {
        CombatCommand select = CombatCommand.SelectTarget(0, "bastion", "ours");
        Assert.Equal((CombatAction.Select, "ours", (string?)null), (select.Action, select.Target, select.Skill));
        Assert.Empty(Errors(Form(null, telegraphMs: 0), commands: new[] { select, CombatCommand.SelectTarget(500, "bastion", null) }));
        Assert.Equal("commands[0].action", Only(Errors(Form(null, telegraphMs: 0), spatial: false, commands: new[] { select })));
        Assert.Equal("commands[0].target", Only(Errors(Form(null, telegraphMs: 0), commands: new[] { CombatCommand.SelectTarget(0, "bastion", "inconnu") })));
        Assert.True(CombatActionIds.TryParseNamed("select", out CombatAction parsed) && parsed == CombatAction.Select);
        Assert.False(CombatActionIds.TryParseDefensive("select", out _));   // le protocole du lot 10 ne la transmet pas
    }

    [Fact]
    public void Regles_de_l_arc_et_des_annonces()
    {
        Assert.Equal((180, 300L), (CombatRules.Provisional.GuardArcDegrees, CombatRules.Provisional.TelegraphMinReactionMs));
        Assert.Empty(new CombatRules { GuardArcDegrees = 1, TelegraphMinReactionMs = 0 }.Validate());
        Assert.Empty(new CombatRules { GuardArcDegrees = 360, TelegraphMinReactionMs = 10_000 }.Validate());
        Assert.Equal(nameof(CombatRules.GuardArcDegrees), Only(new CombatRules { GuardArcDegrees = 0 }.Validate()));
        Assert.Equal(nameof(CombatRules.GuardArcDegrees), Only(new CombatRules { GuardArcDegrees = 361 }.Validate()));
        Assert.Equal(nameof(CombatRules.TelegraphMinReactionMs), Only(new CombatRules { TelegraphMinReactionMs = -1 }.Validate()));
        Assert.Equal(nameof(CombatRules.TelegraphMinReactionMs), Only(new CombatRules { TelegraphMinReactionMs = 10_001 }.Validate()));
    }

    [Fact]
    public void Majorant_de_file_compte_les_impacts_annonces()
    {
        CombatSetup Setup(AssailantAttack attack) => new(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[]
            {
                new CharacterSetup("bastion", "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: At("0", "0")),
                new AssailantSetup("ours", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 500, new[] { attack }, placement: At("5", "0")),
            }, Array.Empty<CombatCommand>(), Identity, new CombatSpace(GroundVector.Parse("-10", "-10"), GroundVector.Parse("10", "10"), Array.Empty<SpaceObstacle>()));
        Assert.Equal(0, Setup(Form(AttackShape.Target, telegraphMs: 0)).ComputeQueueBound().Attacks);
        // Annonce de 2 s, tour toutes les 500 ms : au plus 5 impacts en attente ; zone de 3 s en plus : 11.
        Assert.Equal(5, Setup(Form(AttackShape.Circle, telegraphMs: 2000, radius: "2")).ComputeQueueBound().Attacks);
        Assert.Equal(11, Setup(Form(AttackShape.Persistent, telegraphMs: 2000, radius: "2", persistentMs: 3000, pulseMs: 500)).ComputeQueueBound().Attacks);
    }

    // --- Angles entiers --------------------------------------------------------------------------------------

    [Fact]
    public void Sinus_et_cosinus_entiers_proches_des_flottants_et_exacts_aux_angles_droits()
    {
        for (int h = -720; h <= 1440; h++)
        {
            Angles.SinCos(h, out long sin, out long cos);
            double x = h * Math.PI / 360;
            Assert.True(Math.Abs(sin - Math.Sin(x) * Angles.Scale) <= 4, $"sin {h / 2.0}° : {sin}");
            Assert.True(Math.Abs(cos - Math.Cos(x) * Angles.Scale) <= 4, $"cos {h / 2.0}° : {cos}");
        }
        foreach (var (h, s, c) in new[] { (0, 0L, Angles.Scale), (180, Angles.Scale, 0L), (360, 0L, -Angles.Scale), (540, -Angles.Scale, 0L) })
        {
            Angles.SinCos(h, out long sin, out long cos);
            Assert.Equal((s, c), (sin, cos));
        }
        Angles.Rotate(10_000, 0, 180, out long rx, out long rz);   // 90° : (1, 0) → (0, 1)
        Assert.Equal((0L, 10_000L), (rx, rz));
    }

    [Fact]
    public void Arc_entier_conforme_aux_angles_flottants()
    {
        var random = new Random(1144);
        int inside = 0, outside = 0;
        for (int n = 0; n < 50_000; n++)
        {
            long ax = random.Next(-10_000, 10_001), az = random.Next(-10_000, 10_001), vx = random.Next(-200_000, 200_001), vz = random.Next(-200_000, 200_001);
            if (ax == 0 && az == 0) continue;
            int arc = random.Next(1, 361);
            double angle = Math.Abs(Math.IEEERemainder(Math.Atan2(vz, vx) - Math.Atan2(az, ax), 2 * Math.PI)) * 180 / Math.PI;
            bool result = Angles.WithinArc(ax, az, vx, vz, arc);
            if (vx == 0 && vz == 0) { Assert.True(result); continue; }
            if (angle < arc / 2.0 - 0.01) { Assert.True(result, $"{arc}° : ({vx}, {vz}) à {angle}° de ({ax}, {az})"); inside++; }
            else if (angle > arc / 2.0 + 0.01) { Assert.False(result, $"{arc}° : ({vx}, {vz}) à {angle}° de ({ax}, {az})"); outside++; }
        }
        Assert.True(inside > 10_000 && outside > 10_000, $"{inside} / {outside}");
        Assert.True(Angles.WithinArc(10_000, 0, 0, 5, 180));    // 90° pile : dedans
        Assert.False(Angles.WithinArc(10_000, 0, -1, 5, 180));
        Assert.True(Angles.WithinArc(10_000, 0, -5, 0, 360));
        Assert.True(Angles.WithinArc(10_000, 0, 0, 0, 1));
    }

    [Fact]
    public void Zones_frontieres_exactes()
    {
        // Cercle de 2 autour de (0, 0) : une emprise de 0,5 à 2,5 m le touche, à 2,5001 m non.
        var circle = new AttackZone(AttackShape.Circle, 0, 0, 0, 0, 20_000);
        Assert.True(circle.Touches(25_000, 0, 5_000));
        Assert.False(circle.Touches(25_001, 0, 5_000));
        // Bande de 4 m de long, demi-largeur 0,5, vers l'est : bout arrondi.
        var line = new AttackZone(AttackShape.Line, 0, 0, 10_000, 0, 40_000, halfWidth: 5_000);
        Assert.True(line.Touches(20_000, 10_000, 5_000));
        Assert.False(line.Touches(20_000, 10_001, 5_000));
        Assert.True(line.Touches(50_000, 0, 5_000));
        Assert.False(line.Touches(50_001, 0, 5_000));
        Assert.False(line.Touches(-10_001, 0, 5_000));
        // Cône de 90°, 4 m, vers l'est : sur le côté à 45°, dedans ; derrière, dehors ; bout à 4 m + emprise.
        var cone = new AttackZone(AttackShape.Cone, 0, 0, 10_000, 0, 40_000, angle: 90);
        Assert.True(cone.Touches(20_000, 20_000, 1));
        Assert.True(cone.Touches(45_000, 0, 5_000));
        Assert.False(cone.Touches(45_001, 0, 5_000));
        Assert.False(cone.Touches(-10_000, 0, 5_000));
        Assert.True(cone.Touches(0, 0, 1));
        // Distances de sortie : cercle (rayon + emprise), bande (demi-largeur + emprise), cône (cercle inscrit + emprise).
        Assert.Equal(25_000, AttackZone.EscapeDistance(AttackShape.Circle, 20_000, 0, 0, 5_000));
        Assert.Equal(10_000, AttackZone.EscapeDistance(AttackShape.Line, 40_000, 0, 5_000, 5_000));
        Assert.Equal(25_000, AttackZone.EscapeDistance(AttackShape.Cone, 40_000, 180, 0, 5_000));
    }
}
