using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Triggers;

/// <summary>
/// Lot 7.3.1 : déclencheurs dans la description d'un combat. Porteurs actifs (talent de rang ≥ 1, compétences équipées, Q2),
/// références au catalogue et au tick des règles, précontrôle numérique des frappes de réaction, avertissements de cycles.
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73TriggerSetupTests
{
    private static TriggerDefinition Riposte(string coefficient = "0.5") =>
        new(TriggerEvent.HitTaken, new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse(coefficient)) });

    private static TriggerDefinition Gain(int amount = 1) =>
        new(TriggerEvent.SkillCast, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, new RankedValue(Fixed.FromInt(amount), Fixed.One)) });

    [Fact]
    public void Porteurs_actifs_selon_le_build()
    {
        ClassDefinition definition = Class(talentTriggers: new[] { Gain() }, frappeTriggers: new[] { Riposte() }, passiveTriggers: new[] { Riposte(), Gain() });

        // Sans rang ni passif équipé : seule l'active équipée Frappe porte ses déclencheurs.
        BoundTrigger only = Assert.Single(Character(definition: definition).ActiveTriggers());
        Assert.Equal((TriggerOrigin.Skill, Frappe, 0, 1, "skill.essai.frappe/0"), (only.Origin, only.DefinitionId, only.Index, only.Rank, only.Label));

        // Talent au rang 3, passif équipé, Frappe non équipée.
        CharacterSetup full = Character(definition: definition, actives: new[] { Heurt }, talents: new Dictionary<string, int> { [TalentAlpha] = 3 }, passives: new[] { Passif });
        var bound = full.ActiveTriggers().ToList();
        Assert.Equal(new[] { (TriggerOrigin.Talent, TalentAlpha, 0, 3), (TriggerOrigin.Passive, Passif, 0, 1), (TriggerOrigin.Passive, Passif, 1, 1) },
            bound.Select(b => (b.Origin, b.DefinitionId, b.Index, b.Rank)));
        Assert.Equal(Fixed.FromInt(3), bound[0].Definition.Effects[0].Amount!.Value.At(bound[0].Rank));   // 1 + 1 × (3 − 1)

        // Rang 0 explicite : inactif.
        Assert.Empty(Character(definition: definition, actives: new[] { Heurt }, talents: new Dictionary<string, int> { [TalentAlpha] = 0 }).ActiveTriggers());
    }

    [Fact]
    public void Ultime_equipee_porte_ses_declencheurs()
    {
        var withUltimate = new ClassDefinition("class.essai", "Essai", Essai.Trees,
            Essai.Skills.Select(s => s.Id == Ultime ? new SkillDefinition(s.Id, s.Name, s.Kind, s.MinLevel, s.TalentRequirements, s.IsPlaceholder, s.Combat, new[] { Riposte() }) : s).ToArray());
        Assert.Equal(TriggerOrigin.Skill, Assert.Single(Character(definition: withUltimate).ActiveTriggers()).Origin);
        Assert.Empty(Character(definition: withUltimate, ultimate: null).ActiveTriggers());
    }

    [Fact]
    public void Combat_valide_avec_declencheurs_et_presence_signalee()
    {
        CombatSetup setup = Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Character(definition: Class(frappeTriggers: new[] { Riposte() })), Dummy() });
        Assert.Empty(setup.Validate());
        Assert.True(setup.HasTriggers);
        Assert.False(Setup(new[] { Cmd(0, Frappe) }).HasTriggers);
    }

    [Fact]
    public void References_des_declencheurs_de_classe_verifiees_avec_les_regles()
    {
        var unknown = new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "poison") });
        var offTick = new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "fragility") }, cooldownMs: 1025);
        CombatSetup setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character(definition: Class(talentTriggers: new[] { unknown, offTick })), Dummy() });
        IReadOnlyList<RuleViolation> errors = setup.Validate();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.Equal("combatants[0].class", Assert.Single(e.Fields)));
        Assert.Equal(RuleViolationKind.UnknownReference, errors[0].Kind);
        Assert.Contains("trees[0].talents[0].triggers[0].effects[0].status", errors[0].Message);
        Assert.Contains("trees[0].talents[0].triggers[1].cooldownMs", errors[1].Message);
    }

    [Fact]
    public void Declencheurs_d_assaillant_valides_dans_le_combat()
    {
        var bad = new TriggerDefinition(TriggerEvent.Block, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "stunImmunity") }, TriggerRole.Source);
        var assailant = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000, new[] { Blow() }, triggers: new[] { bad, Riposte("200") });
        IReadOnlyList<RuleViolation> errors = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), assailant }).Validate();
        Assert.Equal(new[] { "combatants[1].triggers[1].effects[0].coefficient", "combatants[1].triggers[0].effects[0].status" }, errors.Select(e => e.Fields[0]));
    }

    [Fact]
    public void Declencheurs_d_alteration_du_catalogue_verifies_par_le_combat()
    {
        var marked = new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh,
            triggers: new[] { new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "guardBreak") }) });
        var rules = new CombatRules { Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(marked)) };
        RuleViolation error = Assert.Single(Setup(new[] { Cmd(0, Frappe) }, rules: rules).Validate());
        Assert.Equal("rules.statuses[7].triggers[0].effects[0].status", error.Fields[0]);
    }

    [Theory]
    [InlineData("1.2", true)]
    [InlineData("100", false)]
    public void Precontrole_numerique_des_frappes_de_reaction(string coefficient, bool valid)
    {
        // Puissance de 10¹² : la Frappe (1.2, critique) tient dans le cumul sur la durée ; une frappe de réaction à 100 × la
        // puissance (sans critique, Q7) dépasse 2⁶³ une fois cumulée sur 200 ticks × 1000 événements comptés.
        var stats = new StatRules { PhysicalPowerPerForce = Fixed.FromInt(1_000_000) };
        CharacterSetup character = Character(attributes: Attrs(force: BuildValidator.MaxAttributeValue),
            definition: Class(passiveTriggers: new[] { Riposte(coefficient) }), passives: new[] { Passif });
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { character, Dummy(armor: 0) }, new[] { Cmd(0, Frappe) }, Identity);
        IReadOnlyList<RuleViolation> errors = setup.Validate();
        if (valid) Assert.Empty(errors);
        else
        {
            RuleViolation error = Assert.Single(errors);
            Assert.Equal((RuleViolationKind.NumericLimit, "combatants[0]"), (error.Kind, error.Fields[0]));
            Assert.Contains("cumul", error.Message);
        }
    }

    [Fact]
    public void Avertissements_de_cycles_statiques_du_combat()
    {
        var assailant = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000, new[] { Blow() }, triggers: new[] { Riposte() });
        CombatSetup setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character(definition: Class(frappeTriggers: new[] { Riposte() })), assailant });
        Assert.Empty(setup.Validate());
        RuleViolation warning = Assert.Single(setup.TriggerWarnings());   // les deux ripostes forment une seule composante
        Assert.Contains("skill.essai.frappe/0@bastion, assailant.assaillant/0@assaillant", warning.Message);
        Assert.Equal("combatants[0].class", warning.Fields[0]);
        Assert.Empty(Setup(new[] { Cmd(0, Frappe) }).TriggerWarnings());
    }
}
