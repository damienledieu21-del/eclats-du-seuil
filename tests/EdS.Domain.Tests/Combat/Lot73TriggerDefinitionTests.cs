using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Lot 7.3.1 : modèle et validation des déclencheurs (PLAN_LOT_7_3.md §4.1). Une erreur localisée par cas hors bornes ou
/// combinaison interdite ; références au catalogue ; profondeur maximale ; graphe statique des cycles (avertissements).
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Data")]
public class Lot73TriggerDefinitionTests
{
    private static readonly TriggerEffect Riposte = TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse("0.5"));

    private static TriggerDefinition On(TriggerEvent on, TriggerRole? role = null, TriggerFilter? filter = null, long cooldownMs = 0, params TriggerEffect[] effects) =>
        new(on, effects.Length == 0 ? new[] { Riposte } : effects, role, filter, cooldownMs);

    private static List<RuleViolation> Shape(TriggerDefinition trigger, int maxRank = 1, bool ranked = false)
    {
        var errors = new List<RuleViolation>();
        TriggerDefinition.ValidateList(new[] { trigger }, "triggers", maxRank, ranked, errors);
        return errors;
    }

    private static void AssertSingle(List<RuleViolation> errors, string field, string? contains = null)
    {
        RuleViolation error = Assert.Single(errors);
        Assert.Equal(field, Assert.Single(error.Fields));
        if (contains != null) Assert.Contains(contains, error.Message);
    }

    [Fact]
    public void Declencheur_minimal_valide_et_roles_par_defaut()
    {
        Assert.Empty(Shape(On(TriggerEvent.HitTaken)));
        Assert.Equal(TriggerRole.Target, On(TriggerEvent.HitTaken).EffectiveRole);
        Assert.Equal(TriggerRole.Self, On(TriggerEvent.SkillCast).EffectiveRole);
        Assert.Equal(TriggerRole.Source, On(TriggerEvent.Death).EffectiveRole);
        Assert.Null(On(TriggerEvent.Block).EffectiveRole);   // deux rôles possibles : il faut le déclarer
    }

    [Theory]
    [InlineData(TriggerEvent.SkillCast, TriggerRole.Self)]
    [InlineData(TriggerEvent.HitDealt, TriggerRole.Source)]
    [InlineData(TriggerEvent.HitTaken, TriggerRole.Target)]
    [InlineData(TriggerEvent.Block, TriggerRole.Source)]
    [InlineData(TriggerEvent.Block, TriggerRole.Target)]
    [InlineData(TriggerEvent.PerfectParry, TriggerRole.Target)]
    [InlineData(TriggerEvent.GuardBroken, TriggerRole.Target)]
    [InlineData(TriggerEvent.ResourceGained, TriggerRole.Self)]
    [InlineData(TriggerEvent.StateApplied, TriggerRole.Source)]
    [InlineData(TriggerEvent.StateExpired, TriggerRole.Target)]
    [InlineData(TriggerEvent.StateRemoved, TriggerRole.Source)]
    [InlineData(TriggerEvent.DotTick, TriggerRole.Target)]
    [InlineData(TriggerEvent.Death, TriggerRole.Source)]
    public void Roles_admis(TriggerEvent on, TriggerRole role) => Assert.Empty(Shape(On(on, role)));

    [Theory]
    [InlineData(TriggerEvent.SkillCast, TriggerRole.Target)]
    [InlineData(TriggerEvent.HitDealt, TriggerRole.Target)]
    [InlineData(TriggerEvent.HitTaken, TriggerRole.Source)]
    [InlineData(TriggerEvent.ResourceGained, TriggerRole.Source)]
    [InlineData(TriggerEvent.Block, TriggerRole.Self)]
    [InlineData(TriggerEvent.Death, TriggerRole.Target)]
    public void Roles_refuses(TriggerEvent on, TriggerRole role) => AssertSingle(Shape(On(on, role)), "triggers[0].role");

    [Fact]
    public void Le_mort_ne_reagit_pas() =>
        AssertSingle(Shape(On(TriggerEvent.Death, TriggerRole.Target)), "triggers[0].role", "le mort ne réagit pas");

    [Theory]
    [InlineData(TriggerEvent.Block)]
    [InlineData(TriggerEvent.StateApplied)]
    [InlineData(TriggerEvent.DotTick)]
    public void Role_obligatoire_si_plusieurs_possibles(TriggerEvent on) =>
        AssertSingle(Shape(On(on)), "triggers[0].role", "rôle obligatoire");

    public static TheoryData<TriggerEvent, TriggerFilter, string> ForbiddenFilters() => new()
    {
        { TriggerEvent.StateApplied, new TriggerFilter(defenses: new[] { HitDefense.Guard }), "defense" },
        { TriggerEvent.Block, new TriggerFilter(defenses: new[] { HitDefense.Guard }), "defense" },
        { TriggerEvent.HitTaken, new TriggerFilter(status: "burn"), "status" },
        { TriggerEvent.HitTaken, new TriggerFilter(category: StatusCategory.Dot), "category" },
        { TriggerEvent.HitTaken, new TriggerFilter(resource: TriggerResource.Resonance), "resource" },
        { TriggerEvent.StateExpired, new TriggerFilter(renewed: true), "renewed" },
        { TriggerEvent.DotTick, new TriggerFilter(skills: new[] { "skill.x.y" }), "skills" },
        { TriggerEvent.ResourceGained, new TriggerFilter(damageType: DamageType.Magique), "damageType" },
        { TriggerEvent.GuardBroken, new TriggerFilter(skills: new[] { "coup" }), "skills" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenFilters))]
    public void Critere_de_filtre_sans_objet(TriggerEvent on, TriggerFilter filter, string criterion) =>
        AssertSingle(Shape(On(on, TriggerDefinition.AllowedRoles(on)[0], filter)), $"triggers[0].filter.{criterion}", "sans objet");

    [Fact]
    public void Filtres_admis_et_identifiants()
    {
        Assert.Empty(Shape(On(TriggerEvent.HitTaken, filter: new TriggerFilter(new[] { "skill.bastion.frappe", "coup_lourd" }, DamageType.Physique,
            new[] { HitDefense.Guard, HitDefense.Parry }))));
        Assert.Empty(Shape(On(TriggerEvent.StateApplied, TriggerRole.Target, new TriggerFilter(category: StatusCategory.Control, renewed: false))));
        Assert.Empty(Shape(On(TriggerEvent.ResourceGained, filter: new TriggerFilter(resource: TriggerResource.RampartCharges))));
        AssertSingle(Shape(On(TriggerEvent.HitTaken, filter: new TriggerFilter(new[] { "Pas Valide" }))), "triggers[0].filter.skills[0]");
        AssertSingle(Shape(On(TriggerEvent.HitTaken, filter: new TriggerFilter(new[] { "coup", "coup" }))), "triggers[0].filter.skills[1]");
        AssertSingle(Shape(On(TriggerEvent.HitTaken, filter: new TriggerFilter(defenses: new[] { HitDefense.Guard, HitDefense.Guard }))), "triggers[0].filter.defense[1]");
        Assert.Equal(2, Shape(On(TriggerEvent.DotTick, TriggerRole.Target, new TriggerFilter(status: "burn", category: StatusCategory.Dot))).Single().Fields.Count);
        AssertSingle(Shape(On(TriggerEvent.ResourceGained, filter: new TriggerFilter(resource: TriggerResource.Endurance))), "triggers[0].filter.resource");
    }

    [Theory]
    [InlineData(-50)]
    [InlineData(86_400_050)]
    public void Recharge_interne_bornee(long cooldownMs) =>
        AssertSingle(Shape(On(TriggerEvent.HitTaken, cooldownMs: cooldownMs)), "triggers[0].cooldownMs");

    [Fact]
    public void De_un_a_quatre_effets_et_huit_declencheurs()
    {
        AssertSingle(Shape(new TriggerDefinition(TriggerEvent.HitTaken, Array.Empty<TriggerEffect>())), "triggers[0].effects");
        AssertSingle(Shape(On(TriggerEvent.HitTaken, effects: Enumerable.Repeat(Riposte, 5).ToArray())), "triggers[0].effects");
        Assert.Empty(Shape(On(TriggerEvent.HitTaken, effects: Enumerable.Repeat(Riposte, 4).ToArray())));
        var errors = new List<RuleViolation>();
        TriggerDefinition.ValidateList(Enumerable.Repeat(On(TriggerEvent.HitTaken), 9).ToArray(), "triggers", 1, false, errors);
        Assert.Equal((RuleViolationKind.LimitExceeded, "triggers"), (Assert.Single(errors).Kind, errors[0].Fields[0]));
        errors.Clear();
        TriggerDefinition.ValidateList(Enumerable.Repeat(On(TriggerEvent.HitTaken), 8).ToArray(), "triggers", 1, false, errors);
        Assert.Empty(errors);
    }

    public static TheoryData<TriggerEffect, string> InvalidEffects() => new()
    {
        { new TriggerEffect(TriggerEffectKind.Strike, null, Fixed.One, PowerType.Physique, DamageType.Physique), "target" },
        { TriggerEffect.Strike(TriggerTarget.Bearer, Fixed.One), "target" },
        { new TriggerEffect(TriggerEffectKind.Strike, TriggerTarget.EventSource, null, PowerType.Physique, DamageType.Physique), "coefficient" },
        { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.FromInt(101)), "coefficient" },
        { TriggerEffect.Strike(TriggerTarget.EventSource, -Fixed.One), "coefficient" },
        { new TriggerEffect(TriggerEffectKind.Strike, TriggerTarget.EventSource, Fixed.One, null, DamageType.Physique), "powerType" },
        { new TriggerEffect(TriggerEffectKind.Strike, TriggerTarget.EventSource, Fixed.One, PowerType.Physique, null), "damageType" },
        { new TriggerEffect(TriggerEffectKind.Strike, TriggerTarget.EventSource, Fixed.One, PowerType.Physique, DamageType.Physique, status: "burn"), "status" },
        { new TriggerEffect(TriggerEffectKind.Strike, TriggerTarget.EventSource, Fixed.One, PowerType.Physique, DamageType.Physique, amount: Fixed.One), "amount" },
        { new TriggerEffect(TriggerEffectKind.ApplyStatus, null, status: "fragility"), "target" },
        { new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventTarget), "status" },
        { new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventTarget, status: "fragility", unblockable: true), "unblockable" },
        { new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventTarget, status: "fragility", powerType: PowerType.Agile), "powerType" },
        { TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "burn", Fixed.FromInt(200)), "coefficient" },
        { new TriggerEffect(TriggerEffectKind.GainResource, amount: Fixed.One), "resource" },
        { new TriggerEffect(TriggerEffectKind.GainResource, resource: TriggerResource.Resonance), "amount" },
        { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.Parse("1.5")), "amount" },
        { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.FromInt(1_000_001)), "amount" },
        { new TriggerEffect(TriggerEffectKind.GainResource, TriggerTarget.EventSource, resource: TriggerResource.Resonance, amount: Fixed.One), "target" },
        { new TriggerEffect(TriggerEffectKind.GainResource, resource: TriggerResource.Resonance, amount: Fixed.One, status: "burn"), "status" },
        { new TriggerEffect(TriggerEffectKind.GainResource, resource: TriggerResource.Resonance, amount: Fixed.One, coefficient: Fixed.One), "coefficient" },
    };

    [Theory]
    [MemberData(nameof(InvalidEffects))]
    public void Effet_invalide_une_erreur_localisee(TriggerEffect effect, string field) =>
        AssertSingle(Shape(On(TriggerEvent.HitTaken, effects: effect)), $"triggers[0].effects[0].{field}");

    [Fact]
    public void Effets_valides_et_gain_toujours_au_porteur()
    {
        Assert.Empty(Shape(On(TriggerEvent.HitTaken, effects: new[]
        {
            TriggerEffect.Strike(TriggerTarget.AllEnemies, Fixed.FromInt(100), PowerType.Magique, DamageType.Magique, unblockable: true),
            TriggerEffect.ApplyStatus(TriggerTarget.Bearer, "freeze"),
            TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "burn", Fixed.Parse("0.3")),
            new TriggerEffect(TriggerEffectKind.GainResource, TriggerTarget.Bearer, resource: TriggerResource.Endurance, amount: Fixed.FromInt(1_000_000)),
        })));
    }

    [Fact]
    public void OnDeath_cible_de_l_evenement_refusee() =>
        AssertSingle(Shape(On(TriggerEvent.Death, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "fragility"))), "triggers[0].effects[0].target", "morte");

    [Fact]
    public void Valeurs_par_rang_reservees_aux_talents_et_bornees_a_chaque_rang()
    {
        var ranked = new RankedValue(Fixed.FromInt(10), Fixed.FromInt(30));   // 10, 40, 70, 100 aux rangs 1 à 4
        TriggerDefinition strike = On(TriggerEvent.HitTaken, effects: TriggerEffect.Strike(TriggerTarget.EventSource, ranked));
        Assert.Empty(Shape(strike, maxRank: 4, ranked: true));
        AssertSingle(Shape(strike, maxRank: 5, ranked: true), "triggers[0].effects[0].coefficient", "rang possible");
        AssertSingle(Shape(strike), "triggers[0].effects[0].coefficient", "seul un talent");
        Assert.Equal(Fixed.FromInt(70), ranked.At(3));

        // Décroissant jusqu'à une valeur négative au rang maximal : refusé.
        var falling = new RankedValue(Fixed.FromInt(5), -Fixed.FromInt(2));
        TriggerDefinition gain = On(TriggerEvent.HitTaken, effects: TriggerEffect.GainResource(TriggerResource.Resonance, falling));
        Assert.Empty(Shape(gain, maxRank: 3, ranked: true));
        AssertSingle(Shape(gain, maxRank: 4, ranked: true), "triggers[0].effects[0].amount");
        // Gain entier : base et perRank entiers.
        AssertSingle(Shape(On(TriggerEvent.HitTaken, effects: TriggerEffect.GainResource(TriggerResource.Resonance, new RankedValue(Fixed.One, Fixed.Parse("0.5")))), 3, true),
            "triggers[0].effects[0].amount", "entière");
    }

    // --- Références (catalogue, tick) -----------------------------------------------------

    private static List<RuleViolation> References(TriggerDefinition trigger, int tickMs = 50)
    {
        var errors = new List<RuleViolation>();
        TriggerDefinition.ValidateReferences(new[] { trigger }, "triggers", StatusCatalog.Provisional, "guardBreak", tickMs, errors);
        return errors;
    }

    [Theory]
    [InlineData("inconnue", null, RuleViolationKind.UnknownReference)]
    [InlineData("stunImmunity", null, RuleViolationKind.InvalidValue)]
    [InlineData("guardBreak", null, RuleViolationKind.InvalidValue)]
    [InlineData("burn", null, RuleViolationKind.InvalidValue)]
    [InlineData("fragility", "0.5", RuleViolationKind.InvalidValue)]
    public void Alteration_appliquee_par_reaction_meme_regle_que_les_competences(string status, string? coefficient, RuleViolationKind kind)
    {
        var effect = TriggerEffect.ApplyStatus(TriggerTarget.EventSource, status, coefficient is null ? null : Fixed.Parse(coefficient));
        RuleViolation error = Assert.Single(References(On(TriggerEvent.HitTaken, effects: effect)));
        Assert.Equal((kind, "triggers[0].effects[0].status"), (error.Kind, error.Fields[0]));
    }

    [Fact]
    public void Filtre_d_alteration_inconnue_et_recharge_hors_tick()
    {
        RuleViolation unknown = Assert.Single(References(On(TriggerEvent.StateApplied, TriggerRole.Target, new TriggerFilter(status: "poison"))));
        Assert.Equal((RuleViolationKind.UnknownReference, "triggers[0].filter.status"), (unknown.Kind, unknown.Fields[0]));
        AssertSingle(References(On(TriggerEvent.HitTaken, cooldownMs: 1025)), "triggers[0].cooldownMs", "multiple");
        Assert.Empty(References(On(TriggerEvent.HitTaken, cooldownMs: 1050)));
        Assert.Empty(References(On(TriggerEvent.HitTaken, cooldownMs: 1025), tickMs: 0));   // tick invalide : signalé ailleurs
        Assert.Empty(References(On(TriggerEvent.HitTaken, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "bleed", Fixed.Parse("0.4")))));
    }

    [Fact]
    public void Declencheurs_d_alteration_valides_par_le_catalogue()
    {
        var withTrigger = new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh,
            triggers: new[] { On(TriggerEvent.HitTaken, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "absente")), On(TriggerEvent.HitTaken, TriggerRole.Source) });
        var catalog = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(withTrigger));
        RuleViolation shape = Assert.Single(catalog.Validate(50));
        Assert.Equal("statuses[7].triggers[1].role", shape.Fields[0]);
        RuleViolation reference = Assert.Single(catalog.ValidateTriggers(50, "guardBreak"));
        Assert.Equal("statuses[7].triggers[0].effects[0].status", reference.Fields[0]);
        Assert.Empty(StatusCatalog.Provisional.ValidateTriggers(50, "guardBreak"));
        Assert.All(StatusCatalog.Provisional.Definitions, d => Assert.Empty(d.Triggers));
    }

    // --- Profondeur maximale ------------------------------------------------------------------

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(-1, false)]
    [InlineData(9, false)]
    public void Profondeur_maximale_bornee(int depth, bool valid)
    {
        IReadOnlyList<RuleViolation> errors = new CombatRules { MaxTriggerDepth = depth }.Validate();
        if (valid) Assert.Empty(errors);
        else Assert.Equal(nameof(CombatRules.MaxTriggerDepth), Assert.Single(Assert.Single(errors).Fields));
    }

    [Fact]
    public void Profondeur_livree_4_plafond_8()
    {
        Assert.Equal(4, CombatRules.Provisional.MaxTriggerDepth);
        Assert.Equal(8, CombatRules.AbsoluteMaxTriggerDepth);
    }

    // --- Graphe statique (A15, Q1) -------------------------------------------------------------

    private static NamedTrigger Named(string label, TriggerDefinition definition) => new(label, "t." + label, definition);

    [Fact]
    public void Cycle_direct_avertissement()
    {
        // Une riposte qui réagit aux impacts subis et frappe : peut s'observer elle-même (rôles et filtres ignorés).
        RuleViolation warning = Assert.Single(TriggerGraph.Cycles(new[] { Named("riposte", On(TriggerEvent.HitTaken)) }, StatusCatalog.Provisional));
        Assert.Equal("t.riposte", warning.Fields[0]);
        Assert.Contains("riposte → riposte", warning.Message);
        Assert.Contains("avertissement", warning.Message);
    }

    [Fact]
    public void Cycle_indirect_une_alerte_par_composante()
    {
        // A : impact subi → applique Fragilité ; B : altération appliquée → frappe. A → B → A ; C isolé (gain, Q8).
        var a = On(TriggerEvent.HitTaken, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "fragility"));
        var b = On(TriggerEvent.StateApplied, TriggerRole.Source);
        var c = On(TriggerEvent.ResourceGained, effects: TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One));
        RuleViolation warning = Assert.Single(TriggerGraph.Cycles(new[] { Named("c", c), Named("a", a), Named("b", b) }, StatusCatalog.Provisional));
        Assert.Contains("(a, b)", warning.Message);
        Assert.Equal("t.a", warning.Fields[0]);
    }

    [Fact]
    public void Gain_de_ressource_jamais_dans_un_cycle()
    {
        // Q8 : un gain par réaction n'est pas observable ; « gain → gain » n'est donc pas un cycle.
        var gain = On(TriggerEvent.ResourceGained, effects: TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One));
        Assert.Empty(TriggerGraph.Cycles(new[] { Named("g", gain) }, StatusCatalog.Provisional));
        // Une frappe produit des gains naturels (Résonance de l'impact) : observable.
        var strikeOnGain = On(TriggerEvent.ResourceGained);
        Assert.Single(TriggerGraph.Cycles(new[] { Named("s", strikeOnGain) }, StatusCatalog.Provisional));
    }

    [Fact]
    public void Cycle_differe_par_dot_selon_le_catalogue()
    {
        // Réappliquer une brûlure à chaque tick de DOT : cycle statique (DOT → dotDamage). Avec Fragilité (non périodique) : aucun.
        var burn = On(TriggerEvent.DotTick, TriggerRole.Source, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "burn", Fixed.Parse("0.2")));
        Assert.Single(TriggerGraph.Cycles(new[] { Named("brulure", burn) }, StatusCatalog.Provisional));
        var fragility = On(TriggerEvent.DotTick, TriggerRole.Source, effects: TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "fragility"));
        Assert.Empty(TriggerGraph.Cycles(new[] { Named("fragilite", fragility) }, StatusCatalog.Provisional));
        // Imparable : ni blocage ni parade produits.
        var unblockable = On(TriggerEvent.Block, TriggerRole.Target, effects: TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One, unblockable: true));
        Assert.Empty(TriggerGraph.Cycles(new[] { Named("u", unblockable) }, StatusCatalog.Provisional));
    }
}
