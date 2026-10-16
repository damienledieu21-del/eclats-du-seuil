using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Combat;

/// <summary>Lot 7.2 : catalogue des altérations, valeurs décidées et validation des définitions (frontières N−1, N, N+1).</summary>
[Trait("Category", "Status")]
[Trait("Category", "Numeric")]
public class Lot72StatusCatalogTests
{
    private static readonly StatusCatalog Shipped = StatusCatalog.Provisional;

    [Fact]
    public void Valeurs_decidees_pour_le_sous_lot_7_2()
    {
        Assert.Empty(Shipped.Validate(50));
        Assert.Equal(new[] { "burn", "bleed", "stun", "stunImmunity", "freeze", "fragility", "guardBreak" }, Shipped.Definitions.Select(d => d.Id));

        // Décisions 1 et 2 : brûlure magique réduite par la résistance magique, saignement physique réduit par l'armure.
        StatusDefinition burn = Shipped["burn"], bleed = Shipped["bleed"];
        Assert.Equal((StatusCategory.Dot, 6000L, 2000L, DamageType.Magique, StatusResistanceMode.Defense, StatusStacking.PerSourceKeepStronger),
            (burn.Category, burn.DurationMs, burn.PeriodMs, burn.DamageType!.Value, burn.Resistance, burn.Stacking));
        Assert.Equal((DamageType.Physique, StatusResistanceMode.Defense), (bleed.DamageType!.Value, bleed.Resistance));

        // Décision 5 : gel, −30 % de déplacement pendant 4 s, ne bloque rien.
        StatusDefinition freeze = Shipped["freeze"];
        Assert.Equal((4000L, Fixed.Parse("-0.3"), StatusBlocks.None, StatusInterrupts.None),
            (freeze.DurationMs, freeze.MovementSpeedModifier, freeze.Blocks, freeze.Interrupts));

        // Décisions 7 et 8 : étourdissement 1,5 s, bloque les actions, interrompt garde et parade ; immunité de 3 s à l'expiration.
        StatusDefinition stun = Shipped["stun"];
        Assert.Equal((1500L, StatusResistanceMode.Duration, "stunImmunity", "stunned"), (stun.DurationMs, stun.Resistance, stun.GrantsOnExpiry, stun.BlockReason));
        Assert.Equal(StatusBlocks.Skills | StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.AssailantAttacks, stun.Blocks);
        Assert.Equal(StatusInterrupts.Guard | StatusInterrupts.Parry, stun.Interrupts);
        // Correctif 7.2 : un étourdissement actif refuse toute nouvelle application (ignore, refus « active ») ; il expire donc
        // toujours et l'immunité suit.
        Assert.Equal(StatusStacking.Ignore, stun.Stacking);
        Assert.Equal(3000L, Shipped["stunImmunity"].DurationMs);
        Assert.Equal(new[] { "stun" }, Shipped["stunImmunity"].ImmuneTo);

        // Décision 10 : Fragilité et rupture de garde inchangées.
        Assert.Equal((6000L, Fixed.Parse("0.1"), StatusStacking.Refresh), (Shipped["fragility"].DurationMs, Shipped["fragility"].DamageTakenIncrease, Shipped["fragility"].Stacking));
        StatusDefinition guardBreak = Shipped["guardBreak"];
        Assert.Equal((3000L, StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.Regeneration, "guardBroken", StatusStacking.Ignore),
            (guardBreak.DurationMs, guardBreak.Blocks, guardBreak.BlockReason, guardBreak.Stacking));

        // Décision 9 : constante de résistance aux altérations 1000, configurable ; la Fragilité seule amplifie les dégâts reçus.
        Assert.Equal(1000, StatRules.Provisional.StatusResistanceConstant);
        Assert.Equal(Fixed.Parse("0.1"), Shipped.TotalDamageTakenIncrease());
    }

    /// <summary>Catalogue livré dont la définition <paramref name="id"/> est remplacée.</summary>
    private static StatusCatalog Replace(string id, StatusDefinition replacement) =>
        new(Shipped.Definitions.Select(d => d.Id == id ? replacement : d));

    private static StatusDefinition Dot(long durationMs = 6000, long periodMs = 2000, StatusStacking stacking = StatusStacking.PerSourceKeepStronger,
        StatusResistanceMode resistance = StatusResistanceMode.Defense, DamageType? type = DamageType.Magique) =>
        new("burn", StatusCategory.Dot, durationMs, stacking, resistance, periodMs, type);

    private static StatusDefinition Control(string id = "freeze", long durationMs = 4000, StatusStacking stacking = StatusStacking.KeepLonger,
        StatusResistanceMode resistance = StatusResistanceMode.Duration, long periodMs = 0, DamageType? type = null,
        StatusBlocks blocks = StatusBlocks.None, string? reason = null, string? grants = null, Fixed movement = default, Fixed damageTaken = default,
        StatusCategory category = StatusCategory.Control, IEnumerable<string>? immuneTo = null) =>
        new(id, category, durationMs, stacking, resistance, periodMs, type, blocks, blockReason: reason, damageTakenIncrease: damageTaken,
            movementSpeedModifier: movement, grantsOnExpiry: grants, immuneTo: immuneTo);

    private static void AssertInvalid(StatusCatalog catalog, string field, int tickMs = 50) =>
        Assert.Contains(catalog.Validate(tickMs), v => v.Fields.Contains(field));

    [Theory]
    [InlineData(6000, 2000, true)]
    [InlineData(6050, 2000, false)]   // multiple du tick, pas de la période
    [InlineData(5950, 2000, false)]
    [InlineData(2000, 2000, true)]    // un seul tick
    [InlineData(6000, 2010, false)]   // période hors tick
    [InlineData(6000, 0, false)]
    public void Duree_d_un_DOT_multiple_de_la_periode(long durationMs, long periodMs, bool valid)
    {
        IReadOnlyList<RuleViolation> violations = Replace("burn", Dot(durationMs, periodMs)).Validate(50);
        Assert.Equal(valid, violations.Count == 0);
    }

    [Theory]
    [InlineData(CombatRules.AbsoluteMaxDurationMs, true)]
    [InlineData(CombatRules.AbsoluteMaxDurationMs + 50, false)]
    [InlineData(4050, true)]
    [InlineData(4010, false)]
    [InlineData(0, false)]
    [InlineData(-50, false)]
    public void Duree_bornee_et_multiple_du_tick(long durationMs, bool valid) =>
        Assert.Equal(valid, Replace("freeze", Control(durationMs: durationMs, movement: Fixed.Parse("-0.3"))).Validate(50).Count == 0);

    [Fact]
    public void Tick_de_1_ms_accepte_toute_duree_entiere()
    {
        Assert.Empty(Replace("freeze", Control(durationMs: 4001)).Validate(1));
        Assert.Empty(Shipped.Validate(1));
    }

    [Fact]
    public void Coherence_categorie_cumul_resistance()
    {
        AssertInvalid(Replace("burn", Dot(stacking: StatusStacking.Refresh)), "statuses[0].stacking");
        AssertInvalid(Replace("burn", Dot(resistance: StatusResistanceMode.Duration)), "statuses[0].resistance");   // décision 3
        AssertInvalid(Replace("burn", Dot(type: null)), "statuses[0].damageType");
        AssertInvalid(Replace("freeze", Control(stacking: StatusStacking.PerSourceKeepStronger)), "statuses[4].stacking");
        AssertInvalid(Replace("freeze", Control(resistance: StatusResistanceMode.Defense)), "statuses[4].resistance");
        AssertInvalid(Replace("freeze", Control(periodMs: 2000)), "statuses[4].periodMs");
        AssertInvalid(Replace("freeze", Control(type: DamageType.Physique)), "statuses[4].damageType");
        AssertInvalid(Replace("fragility", Control("fragility", 6000, StatusStacking.Refresh, StatusResistanceMode.Duration, category: StatusCategory.Modifier)),
            "statuses[5].resistance");   // la résistance aux altérations ne réduit que les contrôles
        Assert.Empty(Replace("burn", Dot(resistance: StatusResistanceMode.None)).Validate(50));
    }

    [Fact]
    public void Blocages_motifs_et_bornes_des_modificateurs()
    {
        AssertInvalid(Replace("freeze", Control(blocks: StatusBlocks.Skills)), "statuses[4].blockReason");
        AssertInvalid(Replace("freeze", Control(reason: "frozen")), "statuses[4].blockReason");
        AssertInvalid(Replace("freeze", Control(blocks: StatusBlocks.Skills, reason: "Gelé")), "statuses[4].blockReason");
        Assert.Empty(Replace("freeze", Control(movement: -Fixed.One)).Validate(50));
        AssertInvalid(Replace("freeze", Control(movement: -Fixed.One - Fixed.Epsilon)), "statuses[4].movementSpeedModifier");
        AssertInvalid(Replace("freeze", Control(movement: Fixed.One + Fixed.Epsilon)), "statuses[4].movementSpeedModifier");
        Assert.Empty(Replace("freeze", Control(damageTaken: StatusDefinition.MaxDamageTakenIncrease)).Validate(50));
        AssertInvalid(Replace("freeze", Control(damageTaken: StatusDefinition.MaxDamageTakenIncrease + Fixed.Epsilon)), "statuses[4].damageTakenIncrease");
        AssertInvalid(Replace("freeze", Control(damageTaken: -Fixed.Epsilon)), "statuses[4].damageTakenIncrease");
    }

    [Fact]
    public void References_immunites_et_etat_accorde()
    {
        AssertInvalid(Replace("stun", Control("stun", 1500, grants: "inconnu")), "statuses[2].grantsOnExpiry");
        AssertInvalid(Replace("stun", Control("stun", 1500, grants: "stun")), "statuses[2].grantsOnExpiry");
        AssertInvalid(Replace("stun", Control("stun", 1500, grants: "burn")), "statuses[2].grantsOnExpiry");   // un DOT n'a pas de source
        // Profondeur 1 : l'état accordé n'accorde rien lui-même.
        AssertInvalid(Replace("freeze", Control(grants: "stun")), "statuses[4].grantsOnExpiry");
        StatusCatalog immunity(params string[] ids) =>
            Replace("stunImmunity", Control("stunImmunity", 3000, StatusStacking.Ignore, StatusResistanceMode.None, category: StatusCategory.Immunity, immuneTo: ids));
        AssertInvalid(immunity(), "statuses[3].immuneTo");
        AssertInvalid(immunity("inconnu"), "statuses[3].immuneTo[0]");
        AssertInvalid(immunity("stunImmunity"), "statuses[3].immuneTo[0]");
        AssertInvalid(immunity("stun", "stun"), "statuses[3].immuneTo[1]");
        Assert.Empty(immunity("stun", "freeze").Validate(50));
        AssertInvalid(Replace("freeze", Control(immuneTo: new[] { "stun" })), "statuses[4].immuneTo");
    }

    [Fact]
    public void Identifiants_uniques_et_valides()
    {
        AssertInvalid(new StatusCatalog(Shipped.Definitions.Append(Control())), "statuses[7].id");
        AssertInvalid(Replace("freeze", Control("Gel")), "statuses[4].id");
        Assert.Contains(new StatusCatalog(Enumerable.Range(0, StatusCatalog.MaxDefinitions + 1).Select(i => Control($"etat{i}"))).Validate(50),
            v => v.Kind == RuleViolationKind.LimitExceeded);
        Assert.Empty(new StatusCatalog(Enumerable.Range(0, StatusCatalog.MaxDefinitions).Select(i => Control($"etat{i}"))).Validate(50));
    }

    [Theory]
    [InlineData(1_000_000_000L, true)]
    [InlineData(1_000_000_001L, false)]
    [InlineData(1L, true)]
    [InlineData(0L, false)]
    public void Constante_de_resistance_aux_alterations_bornee(long constant, bool valid) =>
        Assert.Equal(valid, !new StatRules { StatusResistanceConstant = constant }.Validate().Any(v => v.Fields.Contains(nameof(StatRules.StatusResistanceConstant))));

    [Theory]
    [InlineData("burn", "0.5", null)]
    [InlineData("burn", null, "coefficient obligatoire")]
    [InlineData("stun", null, null)]
    [InlineData("stun", "1", "pas de coefficient")]
    [InlineData("stunImmunity", null, "immunité")]
    [InlineData("guardBreak", null, "mécanique de garde")]
    [InlineData("inconnu", null, "altération inconnue")]
    public void Applications_autorisees(string status, string? coefficient, string? problem)
    {
        string? actual = Shipped.CheckApplication(new StatusApplication(status, coefficient is null ? null : Fixed.Parse(coefficient)), "guardBreak");
        if (problem is null) Assert.Null(actual);
        else Assert.Contains(problem, actual);
    }

    [Theory]
    [InlineData("100", true)]
    [InlineData("100.0001", false)]
    [InlineData("0", true)]
    [InlineData("-0.0001", false)]
    public void Coefficient_d_application_borne(string coefficient, bool valid)
    {
        var errors = new List<RuleViolation>();
        StatusApplication.ValidateList(new[] { new StatusApplication("burn", Fixed.Parse(coefficient)) }, "applies", errors);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void Application_en_double_refusee()
    {
        var errors = new List<RuleViolation>();
        StatusApplication.ValidateList(new[] { new StatusApplication("stun"), new StatusApplication("stun") }, "applies", errors);
        Assert.Equal("applies[1].status", Assert.Single(errors).Fields.Single());
    }

    [Fact]
    public void Rupture_de_garde_designe_une_alteration_utilisable()
    {
        Assert.Empty(CombatRules.Provisional.Validate());
        Assert.Empty(new CombatRules { GuardBreakStatus = "freeze" }.Validate());   // un contrôle quelconque convient
    }
}
