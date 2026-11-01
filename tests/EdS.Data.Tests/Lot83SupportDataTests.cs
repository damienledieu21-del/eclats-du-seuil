using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.3 : soutien en données. Classes (schéma 7) : ciblages <c>self</c>, <c>ally</c>, <c>allAllies</c>, compétences sans dégâts,
/// <c>applies[].target</c> ; règles (schéma 8) : catégories <c>taunt</c> et <c>protection</c>, <c>redirectRatio</c> ; scénarios (schéma
/// 3) : <c>applies[].target</c> des attaques. Refus des versions précédentes, conformité des schémas JSON au programme.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Support")]
public class Lot83SupportDataTests
{
    private static readonly int N = StatusCatalog.Provisional.Definitions.Count;   // altération ajoutée en fin de catalogue (sous-lot 8.4 : 12 livrées)

    private static void AssertError(ValidationReport report, string path, ValidationCode code) =>
        Assert.Contains(report.Errors, e => e.Path == path && e.Code == code);

    /// <summary>Bloc combat de soutien posé sur skills[2] (provocation, sans bloc dans les données livrées).</summary>
    private static JsonObject Support(string targeting, params JsonObject[] applies) => new()
    {
        ["enduranceCost"] = 10, ["cooldownMs"] = 12000, ["targeting"] = targeting, ["unblockable"] = false,
        ["rampartChargeCost"] = 0, ["rampartChargeGain"] = 0, ["provisional"] = true,
        ["applies"] = new JsonArray(applies.Select(a => (JsonNode?)a).ToArray()),
    };

    private static ClassLoadResult WithCombat(JsonObject combat) => ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 2)["combat"] = combat);

    [Fact]
    public void Competence_de_soutien_lue_sans_degats()
    {
        ClassLoadResult result = WithCombat(Support("ally", new JsonObject { ["status"] = "stun" }, new JsonObject { ["status"] = "freeze", ["target"] = "self" }));
        Assert.True(result.IsValid, result.Report.ToString());
        SkillCombatDefinition combat = result.Class!.Definition.Skills[2].Combat!;
        Assert.Equal((false, SkillTargeting.Ally), (combat.HasDamage, combat.Targeting));
        Assert.Equal(new[] { ApplicationTarget.Targets, ApplicationTarget.Self }, combat.Applies.Select(a => a.Target));
        foreach (var (id, targeting) in new[] { ("self", SkillTargeting.Self), ("allAllies", SkillTargeting.AllAllies) })
            Assert.Equal(targeting, WithCombat(Support(id)).Class!.Definition.Skills[2].Combat!.Targeting);
        // Empreinte : la cible propre et l'absence de dégâts entrent dans la forme canonique.
        string plain = WithCombat(Support("ally", new JsonObject { ["status"] = "stun" })).Class!.Fingerprint;
        string own = WithCombat(Support("ally", new JsonObject { ["status"] = "stun", ["target"] = "allEnemies" })).Class!.Fingerprint;
        Assert.NotEqual(plain, own);
    }

    [Fact]
    public void Degats_tous_ensemble_ou_aucun()
    {
        JsonObject partial = Support("enemy");
        partial["coefficient"] = 0.5;
        ClassLoadResult result = WithCombat(partial);
        AssertError(result.Report, "$.data.skills[2].combat.powerType", ValidationCode.MissingField);
        AssertError(result.Report, "$.data.skills[2].combat.damageType", ValidationCode.MissingField);
        JsonObject friendly = Support("ally");
        friendly["powerType"] = "physique";
        friendly["damageType"] = "physique";
        friendly["coefficient"] = 0.5;
        AssertError(WithCombat(friendly).Report, "$.data.skills[2].combat.targeting", ValidationCode.InvalidValue);
        JsonObject unblockable = Support("enemy");
        unblockable["unblockable"] = true;
        AssertError(WithCombat(unblockable).Report, "$.data.skills[2].combat.unblockable", ValidationCode.InvalidValue);
        AssertError(WithCombat(Support("allies")).Report, "$.data.skills[2].combat.targeting", ValidationCode.UnknownReference);
        AssertError(WithCombat(Support("self", new JsonObject { ["status"] = "stun", ["target"] = "ally" })).Report,
            "$.data.skills[2].combat.applies[0].target", ValidationCode.UnknownReference);
    }

    private static JsonObject Status(string id, string category, string stacking = "perSourceKeepStronger", double? ratio = null)
    {
        var s = new JsonObject { ["id"] = id, ["category"] = category, ["durationMs"] = 3000, ["stacking"] = stacking, ["resistance"] = "none" };
        if (ratio is { } r) s["redirectRatio"] = r;
        return s;
    }

    private static RulesLoadResult WithStatuses(params JsonObject[] statuses) =>
        RulesTestData.ReadMutated(r => { foreach (JsonObject s in statuses) r["data"]!["statuses"]!.AsArray().Add(s); });

    [Fact]
    public void Provocation_et_protection_lues()
    {
        RulesLoadResult result = WithStatuses(Status("provoc", "taunt"), Status("lien", "protection", ratio: 0.25));
        Assert.True(result.IsValid, result.Report.ToString());
        StatusCatalog catalog = result.Rules!.Combat.Statuses;
        Assert.Equal((StatusCategory.Taunt, StatusCategory.Protection, Fixed.Parse("0.25")), (catalog["provoc"].Category, catalog["lien"].Category, catalog["lien"].RedirectRatio));
        AssertError(WithStatuses(Status("lien", "protection")).Report, $"$.data.statuses[{N}].redirectRatio", ValidationCode.InvalidValue);
        AssertError(WithStatuses(Status("provoc", "taunt", ratio: 0.5)).Report, $"$.data.statuses[{N}].redirectRatio", ValidationCode.InvalidValue);
        AssertError(WithStatuses(Status("provoc", "taunt", "refresh")).Report, $"$.data.statuses[{N}].stacking", ValidationCode.InvalidValue);
        // L'empreinte des règles dépend de la part transférée.
        Assert.NotEqual(WithStatuses(Status("lien", "protection", ratio: 0.25)).Rules!.Fingerprint, WithStatuses(Status("lien", "protection", ratio: 0.5)).Rules!.Fingerprint);
    }

    [Fact]
    public void Versions_precedentes_refusees_sans_migration()
    {
        // Sous-lot 8.4 : versions courantes 9 (règles) et 8 (classes) ; celles du 8.3 sont refusées à leur tour. Sous-lot 11.4a : règles 10 ;
        // 11.4b : règles 11 et classes 9 ; 11.4c : règles 12, scénarios 6.
        ValidationError rules = Assert.Single(RulesTestData.ReadMutated(r => r["schemaVersion"] = 8).Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (rules.Path, rules.Code));
        Assert.Contains("antérieure à la version 14", rules.Message);   // V1.2 : règles 13 (boucliers) ; V1.3 : 14 (effets d'équipement)
        ValidationError klass = Assert.Single(ClassTestData.ReadMutated(c => c["schemaVersion"] = 7).Report.Errors);
        Assert.Contains("antérieure à la version 9", klass.Message);
        ValidationError scenario = Assert.Single(ScenarioTestData.ReadMutated(s => s["schemaVersion"] = 2).Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (scenario.Path, scenario.Code));
        Assert.Equal(15, ScenarioValidator.CurrentSchemaVersion);   // V1.8d : 15 (bonus modifié) ; V1.3 : 14 (équipement) ;   // V1.2 : 13 ; sous-lot 11.1 : 4 (health des assaillants mortels) ; 11.4a : 5 (positions) ; 11.4c : 6 (formes) ; 11.4d : 7 (arrivées) ; 11.5 : 8 ; 11.9 : 9 ; 12.1 : 10 ; 12.3 : 11 ; 12.6 : 12
    }

    [Fact]
    public void Attaque_d_assaillant_cible_propre_et_ciblage_offensif()
    {
        ScenarioLoadResult result = ScenarioTestData.ReadMutated(s =>
        {
            JsonObject assailant = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).First(c => c["type"]!.GetValue<string>() == "assailant");
            assailant["attacks"]![0]!["applies"] = new JsonArray(new JsonObject { ["status"] = "freeze", ["target"] = "self" });
        }, scenario: "entrainement_garde_parade");
        Assert.True(result.IsValid, result.Report.ToString());
        AssailantSetup a = result.Setup!.Combatants.OfType<AssailantSetup>().First();
        Assert.Equal(ApplicationTarget.Self, a.Attacks[0].Applies.Single().Target);
        ScenarioLoadResult ally = ScenarioTestData.ReadMutated(s =>
            s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).First(c => c["type"]!.GetValue<string>() == "assailant")["attacks"]![0]!["targeting"] = "ally",
            scenario: "entrainement_garde_parade");
        Assert.Contains(ally.Report.Errors, e => e.Path.EndsWith(".attacks[0].targeting") && e.Code == ValidationCode.UnknownReference);
    }

    private static JsonElement Schema(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", name + ".schema.json"))).RootElement.Clone();

    private static string[] Enum(JsonElement node) => node.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static string[] Sorted(IEnumerable<string> keys) => keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Theory]
    [InlineData("class")]
    [InlineData("rules")]
    [InlineData("scenario")]
    public void Schemas_conformes_aux_identifiants_du_programme(string name)
    {
        // Les énumérations des schémas reprennent exactement celles du programme (un schéma plus strict refuserait des fichiers valides :
        // revue du 8.3, catégories buff et shield des filtres de déclencheurs, oubliées au 8.2).
        JsonElement trigger = Schema(name).GetProperty("$defs").GetProperty("trigger").GetProperty("properties");
        Assert.Equal(Sorted(TriggerIds.Roles.Keys), Enum(trigger.GetProperty("role")));
        Assert.Equal(Sorted(StatusIds.Categories.Keys), Enum(trigger.GetProperty("filter").GetProperty("properties").GetProperty("category")));
        JsonElement root = Schema(name);
        switch (name)
        {
            case "class":
                JsonElement combat = root.GetProperty("$defs").GetProperty("combat").GetProperty("properties");
                Assert.Equal(Sorted(ClassValidator.Targetings.Keys), Enum(combat.GetProperty("targeting")));
                Assert.Equal(Sorted(ApplicationTargets.Ids.Keys), Enum(combat.GetProperty("applies").GetProperty("items").GetProperty("properties").GetProperty("target")));
                break;
            case "scenario":
                JsonElement attack = root.GetProperty("$defs").GetProperty("attack").GetProperty("properties");
                Assert.Equal(Sorted(ClassValidator.AttackTargetings), Enum(attack.GetProperty("targeting")));
                Assert.Equal(Sorted(ApplicationTargets.Ids.Keys), Enum(attack.GetProperty("applies").GetProperty("items").GetProperty("properties").GetProperty("target")));
                break;
            default:
                JsonElement status = root.GetProperty("properties").GetProperty("data").GetProperty("properties").GetProperty("statuses").GetProperty("items").GetProperty("properties");
                Assert.Equal(Sorted(StatusIds.Categories.Keys), Enum(status.GetProperty("category")));
                break;
        }
    }

    [Fact]
    public void Schema_de_classe_accepte_une_competence_de_soutien()
    {
        // L'évaluateur des tests du sous-lot 8.1 : le schéma accepte la compétence de soutien que le programme accepte, et refuse des
        // dégâts partiels comme lui.
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "class.schema.json"));
        JsonObject root = ClassTestData.Shipped();
        ClassTestData.Skill(root, 2)["combat"] = Support("ally", new JsonObject { ["status"] = "stun", ["target"] = "self" });
        Assert.True(schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement));
        JsonObject partial = Support("enemy");
        partial["coefficient"] = 0.5;
        ClassTestData.Skill(root, 2)["combat"] = partial;
        Assert.False(schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement));
    }
}
