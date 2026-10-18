using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Sous-lot 8.3 : validation du soutien (ciblages soi, allié, tous les alliés ; compétences sans dégâts ; cibles propres d'application),
/// des provocations et protections du catalogue, du rôle <c>ally</c> des déclencheurs et des clés <c>protection.*</c>.
/// </summary>
[Trait("Category", "Support")]
public class Lot83ValidationTests
{
    private static readonly StatusCatalog Shipped = StatusCatalog.Provisional;
    private static readonly int N = StatusCatalog.Provisional.Definitions.Count;   // sous-lot 8.4 : douze altérations livrées
    private static readonly string P = $"statuses[{N}]";

    private static SkillDefinition Skill(SkillCombatDefinition combat, string id = "skill.essai.s") => new(id, "S", SkillKind.Active, combat: combat);

    private static IReadOnlyList<RuleViolation> ClassErrors(params SkillDefinition[] skills) =>
        new ClassDefinition("class.essai", "Essai",
            new[] { "a", "b", "c" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k, new[] { new TalentDefinition($"talent.essai.{k}.t", "T", 5) })).ToArray(),
            skills).Validate(ProgressionRules.Provisional);

    private static SkillCombatDefinition Support(SkillTargeting targeting, bool unblockable = false, params StatusApplication[] applies) =>
        new(null, null, null, 0, 0, targeting, unblockable: unblockable, applies: applies);

    private static SkillCombatDefinition Damage(SkillTargeting targeting) =>
        new(PowerType.Physique, DamageType.Physique, Fixed.One, 0, 0, targeting);

    [Fact]
    public void Soutien_sans_degats_pour_les_ciblages_allies()
    {
        foreach (SkillTargeting t in new[] { SkillTargeting.Self, SkillTargeting.Ally, SkillTargeting.AllAllies, SkillTargeting.Enemy, SkillTargeting.AllEnemies })
            Assert.Empty(ClassErrors(Skill(Support(t))));
        foreach (SkillTargeting t in new[] { SkillTargeting.Self, SkillTargeting.Ally, SkillTargeting.AllAllies })
            Assert.Equal("skills[0].combat.targeting", Assert.Single(ClassErrors(Skill(Damage(t)))).Fields.Single());
        Assert.Empty(ClassErrors(Skill(Damage(SkillTargeting.Enemy)), Skill(Damage(SkillTargeting.AllEnemies), "skill.essai.z")));
        Assert.True(SkillTargeting.Ally.IsFriendly() && SkillTargeting.Ally.NeedsTarget() && !SkillTargeting.AllAllies.NeedsTarget());
    }

    [Fact]
    public void Soutien_sans_imparable_et_degats_tout_ou_rien()
    {
        Assert.Equal("skills[0].combat.unblockable", Assert.Single(ClassErrors(Skill(Support(SkillTargeting.Enemy, unblockable: true)))).Fields.Single());
        Assert.Throws<ArgumentException>(() => new SkillCombatDefinition(PowerType.Physique, null, Fixed.One, 0, 0));
        Assert.Throws<ArgumentException>(() => new SkillCombatDefinition(null, null, Fixed.One, 0, 0));
        var support = Support(SkillTargeting.Self);
        Assert.Equal((false, Fixed.Zero), (support.HasDamage, support.Coefficient));
        Assert.True(Damage(SkillTargeting.Enemy).HasDamage);
    }

    [Fact]
    public void Coefficient_d_une_competence_de_soutien_non_modifiable()
    {
        var passive = new SkillDefinition("skill.essai.p", "P", SkillKind.Passive,
            modifiers: new[] { new ModifierDefinition("skill.essai.s.coefficient", Fixed.Parse("0.1")) });
        RuleViolation v = Assert.Single(ClassErrors(Skill(Support(SkillTargeting.Self)), passive));
        Assert.Equal("skills[1].modifiers[0].key", v.Fields.Single());
        Assert.Contains("compétence de soutien", v.Message);
        var cooldown = new SkillDefinition("skill.essai.p", "P", SkillKind.Passive,
            modifiers: new[] { new ModifierDefinition("skill.essai.s.cooldownMs", Fixed.FromInt(-500)) });
        Assert.Empty(ClassErrors(Skill(Support(SkillTargeting.Self)), cooldown));
    }

    [Fact]
    public void Cible_propre_d_application()
    {
        Assert.Equal(new[] { "self", "allAllies", "allEnemies" }, ApplicationTargets.Ids.Keys);
        Assert.Null(ApplicationTargets.Of(ApplicationTarget.Targets));
        Assert.Equal("allEnemies", ApplicationTargets.Of(ApplicationTarget.AllEnemies));
        var errors = new List<RuleViolation>();
        StatusApplication.ValidateList(new[] { new StatusApplication("stun", null, (ApplicationTarget)9) }, "applies", errors);
        Assert.Equal("applies[0].target", Assert.Single(errors).Fields.Single());
    }

    private static StatusDefinition Taunt(StatusStacking stacking = StatusStacking.PerSourceKeepStronger, StatusResistanceMode resistance = StatusResistanceMode.None) =>
        new("provoc", StatusCategory.Taunt, 3000, stacking, resistance);

    private static StatusDefinition Protection(string ratio = "0.25", StatusStacking stacking = StatusStacking.PerSourceKeepStronger) =>
        new("lien", StatusCategory.Protection, 6000, stacking, redirectRatio: Fixed.Parse(ratio));

    private static IReadOnlyList<RuleViolation> Errors(params StatusDefinition[] extra) => new StatusCatalog(Shipped.Definitions.Concat(extra)).Validate(50);

    [Fact]
    public void Provocation_et_protection_cumulees_par_source()
    {
        Assert.Empty(Errors(Taunt(), Protection()));
        Assert.Empty(Errors(Taunt(resistance: StatusResistanceMode.Duration)));   // durée réduite comme un contrôle
        Assert.Equal(P + ".stacking", Assert.Single(Errors(Taunt(StatusStacking.Refresh))).Fields.Single());
        Assert.Equal(P + ".stacking", Assert.Single(Errors(Protection(stacking: StatusStacking.Ignore))).Fields.Single());
        Assert.Equal(P + ".resistance", Assert.Single(Errors(Taunt(resistance: StatusResistanceMode.Defense))).Fields.Single());
        Assert.Equal(new[] { "taunt", "protection" }, new[] { StatusCategory.Taunt, StatusCategory.Protection }.Select(StatusIds.Of));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0.0001", true)]
    [InlineData("0", false)]
    [InlineData("1.0001", false)]
    [InlineData("-0.1", false)]
    public void Part_transferee_d_une_protection_dans_l_intervalle(string ratio, bool valid)
    {
        IReadOnlyList<RuleViolation> errors = Errors(Protection(ratio));
        if (valid) Assert.Empty(errors);
        else Assert.Equal(P + ".redirectRatio", Assert.Single(errors).Fields.Single());
        // Hors protection, aucune part.
        Assert.Equal(P + ".redirectRatio", Assert.Single(Errors(new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh,
            redirectRatio: Fixed.Parse("0.5")))).Fields.Single());
    }

    [Fact]
    public void Provocation_et_protection_jamais_accordees()
    {
        var grants = new StatusDefinition("voile", StatusCategory.Modifier, 2000, StatusStacking.Refresh, grantsOnExpiry: "provoc");
        RuleViolation v = Assert.Single(Errors(Taunt(), grants));
        Assert.Equal(($"statuses[{N + 1}].grantsOnExpiry", true), (v.Fields.Single(), v.Message.Contains("provocation ne peut pas être accordée")));
        var grantsLink = new StatusDefinition("voile", StatusCategory.Modifier, 2000, StatusStacking.Refresh, grantsOnExpiry: "lien");
        Assert.Contains("protection ne peut pas être accordée", Assert.Single(Errors(Protection(), grantsLink)).Message);
    }

    private static IReadOnlyList<RuleViolation> TriggerErrors(TriggerEvent on, TriggerRole? role)
    {
        var errors = new List<RuleViolation>();
        TriggerDefinition.ValidateList(new[] { new TriggerDefinition(on, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One) }, role) },
            "triggers", 1, false, errors);
        return errors;
    }

    [Fact]
    public void Role_allie_la_ou_le_role_cible_est_admis()
    {
        foreach (TriggerEvent on in new[] { TriggerEvent.HitTaken, TriggerEvent.Block, TriggerEvent.PerfectParry, TriggerEvent.GuardBroken,
                     TriggerEvent.StateApplied, TriggerEvent.StateExpired, TriggerEvent.StateRemoved, TriggerEvent.DotTick })
        {
            Assert.True(TriggerDefinition.AllyAllowed(on), on.ToString());
            Assert.Empty(TriggerErrors(on, TriggerRole.Ally));
        }
        foreach (TriggerEvent on in new[] { TriggerEvent.SkillCast, TriggerEvent.HitDealt, TriggerEvent.ResourceGained, TriggerEvent.Death })
        {
            Assert.False(TriggerDefinition.AllyAllowed(on), on.ToString());
            Assert.Equal("triggers[0].role", Assert.Single(TriggerErrors(on, TriggerRole.Ally)).Fields.Single());
        }
        // Jamais implicite : sans rôle, onHitTaken reste « cible ».
        Assert.Empty(TriggerErrors(TriggerEvent.HitTaken, null));
        Assert.Equal(TriggerRole.Target, new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One) }).EffectiveRole);
        Assert.Equal(TriggerRole.Ally, TriggerIds.Roles["ally"]);
    }

    [Fact]
    public void Cles_de_protection_et_regles_effectives()
    {
        Assert.True(ModifierKeys.TryParse("protection.redirectRatio", out ModifierKey ratio));
        Assert.True(ModifierKeys.TryParse("protection.redirectThroughGuard", out ModifierKey guard));
        Assert.Equal((ModifierUnit.Decimal, Fixed.One, ModifierUnit.Flag), (ratio.Spec.Unit, ratio.Spec.MaxMagnitude, guard.Spec.Unit));
        var modifiers = ModifierSet.From(new[]
        {
            new BoundModifier("skill.essai.p", 0, 1, new ModifierDefinition("protection.redirectRatio", Fixed.Parse("0.8"))),
            new BoundModifier("skill.essai.q", 0, 1, new ModifierDefinition("protection.redirectRatio", Fixed.Parse("0.5"))),
            new BoundModifier("skill.essai.p", 1, 1, new ModifierDefinition("protection.redirectThroughGuard", Fixed.One)),
        });
        CombatantRules rules = CombatantRules.For(CombatRules.Provisional, modifiers);
        Assert.Equal((Fixed.One, true), (rules.ProtectionRedirectRatio, rules.ProtectionRedirectThroughGuard));   // somme écrêtée à 1
        Assert.Equal(Fixed.One, rules.RedirectRatio(Protection("0.25")));                                          // part écrêtée à 1
        CombatantRules negative = CombatantRules.For(CombatRules.Provisional,
            ModifierSet.From(new[] { new BoundModifier("skill.essai.p", 0, 1, new ModifierDefinition("protection.redirectRatio", Fixed.Parse("-0.5"))) }));
        Assert.Equal(Fixed.Zero, negative.RedirectRatio(Protection("0.25")));                                       // jamais négative
    }
}
