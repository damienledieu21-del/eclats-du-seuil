using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Sous-lot 8.4 : validation des Charges dépensées (X8 : toutes les Charges, coefficient par Charge), du gain de Résonance au lancement,
/// de la clé <c>guard.noUpkeep</c> et de la recharge interne minimale des ripostes automatiques de zone (L8-D2).
/// </summary>
[Trait("Category", "Bastion")]
public class Lot84ValidationTests
{
    private static IReadOnlyList<RuleViolation> ClassErrors(params SkillDefinition[] skills) =>
        new ClassDefinition("class.essai", "Essai",
            new[] { "a", "b", "c" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k, new[] { new TalentDefinition($"talent.essai.{k}.t", "T", 5) })).ToArray(),
            skills).Validate(ProgressionRules.Provisional);

    private static SkillDefinition Active(SkillCombatDefinition combat, SkillKind kind = SkillKind.Active) =>
        new("skill.essai.s", "S", kind, kind == SkillKind.Ultimate ? 10 : 1, combat: combat);

    private static SkillCombatDefinition Strike(int cost = 1, bool all = true, string? perCharge = "0.4", int resonance = 0, bool damage = true) =>
        new(damage ? PowerType.Physique : null, damage ? DamageType.Physique : null, damage ? Fixed.Parse("0.6") : null, 10, 8000,
            rampartChargeCost: cost, applies: null, rampartChargeSpendAll: all, coefficientPerRampartCharge: perCharge is null ? null : Fixed.Parse(perCharge),
            resonanceGain: resonance);

    private static string Single(IReadOnlyList<RuleViolation> errors) => Assert.Single(errors).Fields.Single();

    [Fact]
    public void Contrecoup_toutes_les_Charges_et_coefficient_par_Charge()
    {
        Assert.Empty(ClassErrors(Active(Strike())));
        Assert.Empty(ClassErrors(Active(Strike(cost: 0))));                 // toutes les Charges, aucune exigée
        Assert.Empty(ClassErrors(Active(Strike(cost: 2, all: false))));     // coût fixe : coefficient par Charge dépensée
        Assert.Empty(ClassErrors(Active(Strike(perCharge: "100"))));
        SkillCombatDefinition combat = Strike();
        Assert.Equal((true, Fixed.Parse("0.4"), 1), (combat.RampartChargeSpendAll, combat.CoefficientPerRampartCharge, combat.RampartChargeCost));
        Assert.Equal((4, 1), (combat.RampartChargesSpent(4), Strike(cost: 1, all: false).RampartChargesSpent(4)));
    }

    [Theory]
    [InlineData(0, null, "0.6")]
    [InlineData(5, null, "2.6")]
    [InlineData(1, "-0.5", "0.5")]
    [InlineData(10, null, "4.6")]
    public void Coefficient_effectif_par_Charges_depensees(int charges, string? modifier, string expected)
    {
        SkillDefinition skill = Active(Strike());
        ModifierSet set = modifier is null ? ModifierSet.Empty
            : ModifierSet.From(new[] { new BoundModifier("skill.essai.p", 0, 1, new ModifierDefinition("skill.essai.s.coefficient", Fixed.Parse(modifier))) });
        Assert.Equal(Fixed.Parse(expected), CombatantRules.For(CombatRules.Provisional, set).Coefficient(skill, skill.Combat!, charges));
    }

    [Theory]
    [InlineData(2, null, "100")]        // 0,6 + 2 × 100 : base écrêtée à 100 (revue Codex de la PR #8), et non 200,6
    [InlineData(1, null, "100")]        // 100,6 → 100
    [InlineData(2, "-0.5", "99.5")]     // l'écrêtage de la base précède les modificateurs
    [InlineData(2, "0.1", "100")]
    [InlineData(0, "-1", "0")]          // jamais négatif
    public void Coefficient_par_Charge_ecrete_au_plafond(int charges, string? modifier, string expected)
    {
        SkillDefinition skill = Active(Strike(perCharge: "100"));
        ModifierSet set = modifier is null ? ModifierSet.Empty
            : ModifierSet.From(new[] { new BoundModifier("skill.essai.p", 0, 1, new ModifierDefinition("skill.essai.s.coefficient", Fixed.Parse(modifier))) });
        Assert.Equal(Fixed.Parse(expected), CombatantRules.For(CombatRules.Provisional, set).Coefficient(skill, skill.Combat!, charges));
    }

    [Fact]
    public void Base_non_validee_au_dela_du_plafond_ni_reduite_ni_augmentee()
    {
        // Contrat du sous-lot 8.1 (Lot81CombatantRulesBoundsTests) : une base hors intervalle n'est pas ramenée dans l'intervalle ; les
        // Charges n'y ajoutent rien.
        var combat = new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.FromInt(150), 0, 0, rampartChargeCost: 1,
            rampartChargeSpendAll: true, coefficientPerRampartCharge: Fixed.One);
        SkillDefinition skill = Active(combat);
        Assert.Equal(Fixed.FromInt(150), CombatantRules.For(CombatRules.Provisional, ModifierSet.Empty).Coefficient(skill, combat, 3));
    }

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("100.0001")]
    public void Coefficient_par_Charge_hors_bornes(string perCharge) =>
        Assert.Equal("skills[0].combat.coefficientPerRampartCharge", Single(ClassErrors(Active(Strike(perCharge: perCharge)))));

    [Fact]
    public void Coefficient_par_Charge_sans_effet_refuse()
    {
        // Sans dégâts directs, ou sans aucune Charge dépensée (coût nul, pas « toutes »).
        Assert.Equal("skills[0].combat.coefficientPerRampartCharge", Single(ClassErrors(Active(Strike(damage: false)))));
        Assert.Equal("skills[0].combat.coefficientPerRampartCharge", Single(ClassErrors(Active(Strike(cost: 0, all: false)))));
        Assert.Empty(ClassErrors(Active(Strike(cost: 0, all: false, perCharge: null))));
    }

    [Fact]
    public void Gain_de_Resonance_au_lancement_jamais_sur_une_ultime()
    {
        Assert.Empty(ClassErrors(Active(Strike(resonance: 5))));
        Assert.Empty(ClassErrors(Active(Strike(resonance: SkillCombatDefinition.MaxCost))));
        Assert.Equal("skills[0].combat.resonanceGain", Single(ClassErrors(Active(Strike(resonance: -1)))));
        Assert.Equal("skills[0].combat.resonanceGain", Single(ClassErrors(Active(Strike(resonance: SkillCombatDefinition.MaxCost + 1)))));
        RuleViolation ultimate = Assert.Single(ClassErrors(Active(Strike(resonance: 5), SkillKind.Ultimate)));
        Assert.Equal(("skills[0].combat.resonanceGain", true), (ultimate.Fields.Single(), ultimate.Message.Contains("ultime")));
    }

    [Fact]
    public void Garde_sans_entretien_drapeau_et_renforcement()
    {
        Assert.True(ModifierKeys.TryParse("guard.noUpkeep", out ModifierKey key));
        Assert.Equal((ModifierUnit.Flag, ModifierField.GuardNoUpkeep), (key.Spec.Unit, key.Field));
        Assert.Contains(ModifierField.GuardNoUpkeep, StatusDefinition.BuffKeys);
        // Permanent : entretien nul, l'entretien d'un renforcement n'y change rien.
        CombatantRules permanent = CombatantRules.For(CombatRules.Provisional,
            ModifierSet.From(new[] { new BoundModifier("skill.essai.p", 0, 1, new ModifierDefinition("guard.noUpkeep", Fixed.One)) }));
        Assert.Equal((true, Fixed.Zero, Fixed.Zero), (permanent.GuardNoUpkeep, permanent.GuardUpkeepPerSecond, permanent.GuardUpkeepPerSecondWith(Fixed.FromInt(5))));
        // Porté par un renforcement actif : entretien nul tant qu'il dure ; sans lui, la base (2/s) plus les renforcements.
        CombatantRules none = CombatantRules.For(CombatRules.Provisional, ModifierSet.Empty);
        Assert.Equal((Fixed.Zero, Fixed.FromInt(2), Fixed.FromInt(3)),
            (none.GuardUpkeepPerSecondWith(Fixed.One, temporaryNoUpkeep: true), none.GuardUpkeepPerSecondWith(Fixed.Zero), none.GuardUpkeepPerSecondWith(Fixed.One)));
    }

    // --- L8-D2 : recharge interne minimale des ripostes automatiques de zone --------------------------------------------------------

    private static TriggerDefinition Riposte(TriggerTarget target, long cooldownMs, TriggerEffectKind kind = TriggerEffectKind.Strike) =>
        new(TriggerEvent.PerfectParry, new[]
        {
            kind == TriggerEffectKind.Strike
                ? TriggerEffect.Strike(target, Fixed.Parse("1.2"), PowerType.Physique, DamageType.Physique)
                : TriggerEffect.ApplyStatus(target, "fragility"),
        }, TriggerRole.Target, cooldownMs: cooldownMs);

    private static List<RuleViolation> References(TriggerDefinition trigger, long minimum = 3000)
    {
        var errors = new List<RuleViolation>();
        TriggerDefinition.ValidateReferences(new[] { trigger }, "triggers", StatusCatalog.Provisional, "guardBreak", 50, minimum, errors);
        return errors;
    }

    [Theory]
    [InlineData(2950, false)]   // N − 1 tick
    [InlineData(3000, true)]    // N
    [InlineData(3050, true)]    // N + 1 tick
    [InlineData(0, false)]
    public void Riposte_de_zone_recharge_interne_de_3_s_au_moins(long cooldownMs, bool accepted)
    {
        List<RuleViolation> errors = References(Riposte(TriggerTarget.AllEnemies, cooldownMs));
        if (accepted) Assert.Empty(errors);
        else
        {
            RuleViolation v = Assert.Single(errors);
            Assert.Equal(("triggers[0].cooldownMs", true), (v.Fields.Single(), v.Message.Contains("au moins 3000 ms")));
        }
        Assert.True(Riposte(TriggerTarget.AllEnemies, cooldownMs).IsAreaStrike);
    }

    [Fact]
    public void Seules_les_frappes_de_zone_sont_concernees()
    {
        // Frappe ciblée, ou altération posée sur tous les ennemis : aucune recharge minimale.
        Assert.Empty(References(Riposte(TriggerTarget.EventSource, 0)));
        Assert.Empty(References(Riposte(TriggerTarget.AllEnemies, 0, TriggerEffectKind.ApplyStatus)));
        Assert.False(Riposte(TriggerTarget.EventSource, 0).IsAreaStrike);
        Assert.False(Riposte(TriggerTarget.AllEnemies, 0, TriggerEffectKind.ApplyStatus).IsAreaStrike);
        // Configuration explicite de test : minimum nul.
        Assert.Empty(References(Riposte(TriggerTarget.AllEnemies, 0), minimum: 0));
        Assert.Equal(3000, CombatRules.Provisional.AreaStrikeMinCooldownMs);
    }

    [Fact]
    public void Riposte_de_zone_portee_par_une_alteration_aussi()
    {
        var marked = new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh,
            triggers: new[] { Riposte(TriggerTarget.AllEnemies, 1000) });
        var catalog = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(marked));
        RuleViolation v = Assert.Single(catalog.ValidateTriggers(50, "guardBreak", 3000));
        Assert.Equal($"statuses[{StatusCatalog.Provisional.Definitions.Count}].triggers[0].cooldownMs", v.Fields.Single());
        Assert.Empty(catalog.ValidateTriggers(50, "guardBreak", 1000));
    }

    [Theory]
    [InlineData("taunt", "taunt")]                     // altérations livrées au sous-lot 8.4
    [InlineData("steelBarrier", "shield")]
    [InlineData("bastionAbsolute", "buff")]
    [InlineData("lien", "protection")]
    [InlineData("burn", "dot")]
    [InlineData("stunImmunity", "immunity")]
    public void Rupture_de_garde_controle_ou_modificateur_seulement(string status, string category)
    {
        // Revue Codex de la PR #7 (3e relecture), corrigée ici à la demande de Damien : une protection, une provocation, un bouclier ou un
        // renforcement ne peut pas servir d'état de rupture de garde.
        var lien = new StatusDefinition("lien", StatusCategory.Protection, 6000, StatusStacking.PerSourceKeepStronger, redirectRatio: Fixed.Parse("0.25"));
        var rules = new CombatRules { GuardBreakStatus = status, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(lien)) };
        RuleViolation v = Assert.Single(rules.Validate());
        Assert.Equal((nameof(CombatRules.GuardBreakStatus), true), (v.Fields.Single(), v.Message.Contains($"« {category} »")));
        foreach (string accepted in new[] { "guardBreak", "stun", "freeze", "fragility" })
            Assert.Empty(new CombatRules { GuardBreakStatus = accepted }.Validate());
    }

    [Theory]
    [InlineData(-50, "ne peut pas être négative")]
    [InlineData(3025, "multiple")]
    [InlineData(86_400_050, "24 h")]
    public void Recharge_minimale_des_regles_bornee(long value, string fragment)
    {
        RuleViolation v = Assert.Single(new CombatRules { AreaStrikeMinCooldownMs = value }.Validate(), e => e.Fields.Contains(nameof(CombatRules.AreaStrikeMinCooldownMs)));
        Assert.Contains(fragment, v.Message);
        Assert.DoesNotContain(new CombatRules { AreaStrikeMinCooldownMs = 0 }.Validate(), e => e.Fields.Contains(nameof(CombatRules.AreaStrikeMinCooldownMs)));
    }
}
