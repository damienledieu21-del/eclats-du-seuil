using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.3.1 : déclencheurs dans les données. Classes au format 4 (talents et compétences), règles au schéma 5
/// (<c>combat.limits.maxTriggerDepth</c>, <c>statuses[].triggers</c>), assaillants de scénario ; forme, validation unique,
/// empreintes, avertissements de cycles statiques, en-tête de journal et rejeu.
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Data")]
public class Lot73TriggerDataTests
{
    private static JsonNode Riposte(string on = "onHitTaken", double coefficient = 0.5) => JsonNode.Parse($$"""
        { "on": "{{on}}", "effects": [ { "type": "strike", "target": "eventSource", "coefficient": {{coefficient.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "powerType": "physique", "damageType": "physique" } ] }
        """)!;

    private static JsonArray List(params JsonNode[] triggers) => new(triggers);

    // --- Classes (format 4) --------------------------------------------------------------

    [Fact]
    public void Classe_figee_du_8_4_sans_declencheur()
    {
        // Q14 : aucune donnée livrée ne portait de déclencheur jusqu'au contenu du lot 8. Sous-lot 8.5 : les talents et passifs livrés en
        // portent (Lot85ContentDataTests) ; ces tests de forme partent de la classe figée du 8.4, qui n'en a aucun.
        Assert.All(ClassTestData.FrozenBastion.Trees.SelectMany(t => t.Talents), t => Assert.Empty(t.Triggers));
        Assert.All(ClassTestData.FrozenBastion.Skills, s => Assert.Empty(s.Triggers));
    }

    [Fact]
    public void Talent_et_competence_avec_declencheurs_lus()
    {
        ClassLoadResult result = ClassTestData.ReadFrozenMutated(root =>
        {
            ClassTestData.Talent(root, 0, 0)["triggers"] = List(JsonNode.Parse("""
                { "on": "onBlock", "role": "target", "cooldownMs": 1000,
                  "filter": { "damageType": "physique" },
                  "effects": [ { "type": "gainResource", "resource": "rampartCharges", "amount": { "base": 1, "perRank": 1 } } ] }
                """)!);
            ClassTestData.Skill(root, 7)["triggers"] = List(JsonNode.Parse("""
                { "on": "onStateApplied", "role": "source", "filter": { "category": "dot", "renewed": false },
                  "effects": [ { "type": "applyStatus", "target": "eventTarget", "status": "fragility" } ] }
                """)!);
        });
        Assert.True(result.IsValid, result.Report.ToString());
        TriggerDefinition talent = Assert.Single(result.Class!.Definition.Trees[0].Talents[0].Triggers);
        Assert.Equal((TriggerEvent.Block, TriggerRole.Target, 1000L), (talent.On, talent.Role!.Value, talent.CooldownMs));
        Assert.Equal(Domain.Stats.DamageType.Physique, talent.Filter.DamageType);
        TriggerEffect gain = Assert.Single(talent.Effects);
        Assert.Equal((TriggerEffectKind.GainResource, TriggerResource.RampartCharges), (gain.Kind, gain.Resource!.Value));
        Assert.Equal(Fixed.FromInt(3), gain.Amount!.Value.At(3));
        TriggerDefinition passive = Assert.Single(result.Class.Definition.Skills[7].Triggers);
        Assert.Equal((StatusCategory.Dot, false), (passive.Filter.Category!.Value, passive.Filter.Renewed!.Value));
        // Appliquer une altération sur « altération appliquée » : cycle statique (filtres ignorés), simple avertissement.
        Assert.Equal("$.data.skills[7].triggers[0]", Assert.Single(result.Report.Warnings).Path);
    }

    [Fact]
    public void Empreinte_de_classe_inchangee_sans_declencheur_changee_avec()
    {
        string shipped = ClassJsonReader.Read(ClassTestData.FrozenJson, ClassTestData.Progression).Class!.Fingerprint;
        ClassLoadResult empty = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 0, 0)["triggers"] = new JsonArray());
        Assert.Equal(shipped, empty.Class!.Fingerprint);
        ClassLoadResult with = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 0, 0)["triggers"] = List(Riposte()));
        ClassLoadResult other = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 0, 0)["triggers"] = List(Riposte(coefficient: 0.6)));
        Assert.NotEqual(shipped, with.Class!.Fingerprint);
        Assert.NotEqual(with.Class.Fingerprint, other.Class!.Fingerprint);
    }

    [Fact]
    public void Erreurs_de_forme_localisees()
    {
        ClassLoadResult missing = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 0, 0)["triggers"] = List(JsonNode.Parse("""{ "effects": [] }""")!));
        Assert.Equal(("$.data.trees[0].talents[0].triggers[0].on", ValidationCode.MissingField), (Assert.Single(missing.Report.Errors).Path, missing.Report.Errors[0].Code));

        ClassLoadResult unknown = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 0, 0)["triggers"] = List(Riposte("onStateBlocked")));
        Assert.Equal(("$.data.trees[0].talents[0].triggers[0].on", ValidationCode.UnknownReference), (Assert.Single(unknown.Report.Errors).Path, unknown.Report.Errors[0].Code));

        ClassLoadResult field = ClassTestData.ReadFrozenMutated(root =>
        {
            JsonNode trigger = Riposte();
            trigger["chance"] = 0.5;   // Q6 : pas de chance de déclenchement en 7.3
            ClassTestData.Talent(root, 0, 0)["triggers"] = List(trigger);
        });
        Assert.Equal(("$.data.trees[0].talents[0].triggers[0].chance", ValidationCode.UnknownField), (Assert.Single(field.Report.Errors).Path, field.Report.Errors[0].Code));

        ClassLoadResult ranked = ClassTestData.ReadFrozenMutated(root =>
        {
            JsonNode trigger = Riposte();
            trigger["effects"]![0]!["coefficient"] = JsonNode.Parse("""{ "perRank": 0.1 }""");
            ClassTestData.Talent(root, 0, 0)["triggers"] = List(trigger);
        });
        Assert.Equal(("$.data.trees[0].talents[0].triggers[0].effects[0].coefficient.base", ValidationCode.MissingField),
            (Assert.Single(ranked.Report.Errors).Path, ranked.Report.Errors[0].Code));
    }

    [Fact]
    public void Validation_de_gameplay_rattachee_au_fichier()
    {
        // Valeur par rang hors bornes au rang maximal du talent (garde_stable : 3 rangs ; 98 + 2 × 2 = 102 > 100).
        ClassLoadResult result = ClassTestData.ReadFrozenMutated(root =>
        {
            JsonNode trigger = Riposte();
            trigger["effects"]![0]!["coefficient"] = JsonNode.Parse("""{ "base": 98, "perRank": 2 }""");
            ClassTestData.Talent(root, 0, 0)["triggers"] = List(trigger);
        });
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.data.trees[0].talents[0].triggers[0].effects[0].coefficient", ValidationCode.InvalidValue), (error.Path, error.Code));

        // Valeur par rang sur une compétence : refusée.
        ClassLoadResult skill = ClassTestData.ReadFrozenMutated(root =>
        {
            JsonNode trigger = Riposte();
            trigger["effects"]![0]!["coefficient"] = JsonNode.Parse("""{ "base": 1, "perRank": 1 }""");
            ClassTestData.Skill(root, 0)["triggers"] = List(trigger);
        });
        Assert.Equal("$.data.skills[0].triggers[0].effects[0].coefficient", Assert.Single(skill.Report.Errors).Path);
    }

    [Fact]
    public void Cycle_statique_de_classe_avertissement_sans_refus()
    {
        ClassLoadResult result = ClassTestData.ReadFrozenMutated(root => ClassTestData.Talent(root, 1, 0)["triggers"] = List(Riposte()));
        Assert.True(result.IsValid);
        ValidationError warning = Assert.Single(result.Report.Warnings);
        Assert.Equal(("$.data.trees[1].talents[0].triggers[0]", ValidationCode.TriggerCycle), (warning.Path, warning.Code));
        Assert.Equal(0, result.Report.TotalCount);
    }

    [Fact]
    public void Format_de_classe_3_refuse()
    {
        ClassLoadResult result = ClassTestData.ReadFrozenMutated(c => c["schemaVersion"] = 3);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(result.Report.Errors).Path, result.Report.Errors[0].Code));
    }

    // --- Règles (schéma 5) ---------------------------------------------------------------

    [Fact]
    public void Profondeur_maximale_lue_obligatoire_et_bornee()
    {
        Assert.Equal(4, RulesJsonReader.Read(RulesTestData.ShippedJson).Rules!.Combat.MaxTriggerDepth);
        RulesLoadResult missing = RulesTestData.ReadMutated(r => RulesTestData.Remove(r, "combat.limits.maxTriggerDepth"));
        Assert.Equal(("$.data.combat.limits.maxTriggerDepth", ValidationCode.MissingField), (Assert.Single(missing.Report.Errors).Path, missing.Report.Errors[0].Code));
        RulesLoadResult high = RulesTestData.ReadMutated(r => RulesTestData.Set(r, "combat.limits.maxTriggerDepth", 9));
        Assert.Equal(("$.data.combat.limits.maxTriggerDepth", ValidationCode.InvalidValue), (Assert.Single(high.Report.Errors).Path, high.Report.Errors[0].Code));
        Assert.Equal(0, RulesTestData.ReadMutated(r => RulesTestData.Set(r, "combat.limits.maxTriggerDepth", 0)).Rules!.Combat.MaxTriggerDepth);
        Assert.Equal(8, RulesTestData.ReadMutated(r => RulesTestData.Set(r, "combat.limits.maxTriggerDepth", 8)).Rules!.Combat.MaxTriggerDepth);
    }

    [Fact]
    public void Regles_de_schema_4_refusees()
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["schemaVersion"] = 4);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(result.Report.Errors).Path, result.Report.Errors[0].Code));
    }

    private static JsonObject Status(JsonObject root, string id) =>
        root["data"]!["statuses"]!.AsArray().Select(s => s!.AsObject()).Single(s => (string)s["id"]! == id);

    [Fact]
    public void Declencheurs_d_alteration_lus_valides_et_dans_l_empreinte()
    {
        string shipped = RulesJsonReader.Read(RulesTestData.ShippedJson).Rules!.Fingerprint;
        RulesLoadResult result = RulesTestData.ReadMutated(r => Status(r, "fragility")["triggers"] = List(JsonNode.Parse("""
            { "on": "onHitTaken", "effects": [ { "type": "applyStatus", "target": "eventSource", "status": "freeze" } ] }
            """)!));
        Assert.True(result.IsValid, result.Report.ToString());
        StatusDefinition fragility = result.Rules!.Combat.Statuses["fragility"];
        Assert.Equal(TriggerEvent.HitTaken, Assert.Single(fragility.Triggers).On);
        Assert.NotEqual(shipped, result.Rules.Fingerprint);
        Assert.Equal(shipped, RulesTestData.ReadMutated(r => Status(r, "fragility")["triggers"] = new JsonArray()).Rules!.Fingerprint);
        Assert.Empty(result.Report.Warnings);   // geler n'est pas un impact subi
    }

    [Fact]
    public void Declencheur_d_alteration_reference_inconnue_et_cycle()
    {
        RulesLoadResult unknown = RulesTestData.ReadMutated(r => Status(r, "fragility")["triggers"] = List(JsonNode.Parse("""
            { "on": "onHitTaken", "effects": [ { "type": "applyStatus", "target": "eventSource", "status": "poison" } ] }
            """)!));
        ValidationError error = Assert.Single(unknown.Report.Errors);
        Assert.Equal(("$.data.statuses[5].triggers[0].effects[0].status", ValidationCode.UnknownReference), (error.Path, error.Code));

        RulesLoadResult cycle = RulesTestData.ReadMutated(r => Status(r, "burn")["triggers"] = List(JsonNode.Parse("""
            { "on": "onDotTick", "role": "target", "effects": [ { "type": "applyStatus", "target": "eventSource", "status": "burn", "coefficient": 0.1 } ] }
            """)!));
        Assert.True(cycle.IsValid, cycle.Report.ToString());
        Assert.Equal("$.data.statuses[0].triggers[0]", Assert.Single(cycle.Report.Warnings).Path);
    }

    // --- Scénarios (Q4) ------------------------------------------------------------------

    private static JsonNode AssailantRiposte() => JsonNode.Parse("""
        { "on": "onBlock", "role": "source", "cooldownMs": 500, "filter": { "damageType": "physique" },
          "effects": [ { "type": "strike", "target": "eventTarget", "coefficient": 1, "powerType": "physique", "damageType": "magique", "unblockable": true },
                       { "type": "applyStatus", "target": "eventTarget", "status": "bleed", "coefficient": { "base": 0.25 } } ] }
        """)!;

    [Fact]
    public void Declencheurs_d_assaillant_lus_et_valides()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadFrozenMutated(s => ScenarioTestData.Combatant(s, 1)["triggers"] = List(AssailantRiposte()), scenario: "entrainement_garde_parade");
        Assert.True(result.IsValid, result.Report.ToString());
        var assailant = (AssailantSetup)result.Setup!.Combatants[1];
        TriggerDefinition trigger = Assert.Single(assailant.Triggers);
        Assert.Equal(("assailant.assaillant", 2), (assailant.TriggerDefinitionId, trigger.Effects.Count));
        Assert.True(result.Setup.HasTriggers);
        Assert.False(ScenarioTestData.ReadFrozenMutated(_ => { }, scenario: "entrainement_garde_parade").Setup!.HasTriggers);
    }

    [Fact]
    public void Declencheurs_reserves_aux_assaillants_et_valides()
    {
        ScenarioLoadResult dummy = ScenarioTestData.ReadFrozenMutated(s => ScenarioTestData.Combatant(s, 1)["triggers"] = List(AssailantRiposte()));
        Assert.Equal("$.data.combatants[1].triggers", Assert.Single(dummy.Report.Errors).Path);

        ScenarioLoadResult cooldown = ScenarioTestData.ReadFrozenMutated(s =>
        {
            JsonNode trigger = AssailantRiposte();
            trigger["cooldownMs"] = 525;
            ScenarioTestData.Combatant(s, 1)["triggers"] = List(trigger);
        }, scenario: "entrainement_garde_parade");
        Assert.Equal(("$.data.combatants[1].triggers[0].cooldownMs", ValidationCode.InvalidValue), (Assert.Single(cooldown.Report.Errors).Path, cooldown.Report.Errors[0].Code));
    }

    [Fact]
    public void Cycle_statique_de_scenario_avertissement()
    {
        // Riposte de l'assaillant sur impact subi : peut s'observer elle-même.
        ScenarioLoadResult result = ScenarioTestData.ReadFrozenMutated(s => ScenarioTestData.Combatant(s, 1)["triggers"] = List(Riposte()), scenario: "entrainement_garde_parade");
        Assert.True(result.IsValid, result.Report.ToString());
        ValidationError warning = Assert.Single(result.Report.Warnings);
        Assert.Equal("$.data.combatants[1].triggers[0]", warning.Path);
        Assert.Contains("assailant.assaillant/0@assaillant", warning.Message);
    }

    [Fact]
    public void En_tete_de_journal_et_rejeu_avec_declencheurs_d_assaillant()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadFrozenMutated(s => ScenarioTestData.Combatant(s, 1)["triggers"] = List(AssailantRiposte()), scenario: "entrainement_garde_parade");
        var log = new MemoryCombatLog();
        CombatEngine.Run(result.Setup!, log);
        Assert.Contains("\"triggers\":[{\"on\":\"onBlock\",\"role\":\"source\",\"filter\":{\"damageType\":\"physique\"},\"cooldownMs\":500,", log.Lines[0]);
        Assert.Contains("\"coefficient\":0.25", log.Lines[0]);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ScenarioTestData.FrozenData).Status);

        // Sans déclencheur, l'en-tête n'a pas de champ triggers (journaux existants inchangés).
        var plain = new MemoryCombatLog();
        CombatEngine.Run(ScenarioTestData.ReadFrozenMutated(_ => { }, scenario: "entrainement_garde_parade").Setup!, plain);
        Assert.DoesNotContain("\"triggers\"", plain.Lines[0]);
    }
}
