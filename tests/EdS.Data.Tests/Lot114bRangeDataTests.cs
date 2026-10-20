using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4b : portées en données. Classes en schéma 9 (<c>combat.range</c>, <c>combat.applies[].range</c>,
/// <c>triggers[].effects[].range</c>), règles en schéma 11 (<c>statuses[].range</c> d'une protection), Bastion au contenu 0.6.0 avec les
/// portées retenues par Damien le 20/10/2026 (COMBAT_ESPACE.md, section 7), scénario livré <c>espace_portees</c>. Les portées ne sont
/// lues ni dans les déclencheurs des règles ni dans les scénarios (attaques d'assaillant : formes au 11.4c).
/// </summary>
[Trait("Category", "Space")]
[Trait("Category", "Data")]
public class Lot114bRangeDataTests
{
    private const string Portees = "espace_portees";

    private static Fixed F(string value) => Fixed.Parse(value);

    private static ValidationError SingleError(ValidationReport report)
    {
        Assert.False(report.IsValid);
        return Assert.Single(report.Errors);
    }

    // --- Bastion 0.6.0 ------------------------------------------------------------------------------------

    [Fact]
    public void Portees_du_Bastion_retenues_par_Damien()
    {
        ClassDefinition bastion = ClassTestData.Bastion;
        SkillCombatDefinition Combat(string id) => bastion.Skills.Single(s => s.Id == $"skill.bastion.{id}").Combat!;
        // Mêlée : cible à 2,5 m ; Provocation et Barrière d'acier : 15 m ; Onde de rupture : cercle de 5 m autour du Bastion.
        Assert.Equal(F("2.5"), Combat("frappe_du_rempart").Range);
        Assert.Equal(F("2.5"), Combat("heurt_de_bouclier").Range);
        Assert.Equal(F("2.5"), Combat("contrecoup").Range);
        Assert.Equal(F("15"), Combat("provocation").Range);
        Assert.Equal(F("15"), Combat("barriere_d_acier").Range);
        Assert.Equal((SkillTargeting.AllEnemies, F("5")), (Combat("onde_de_rupture").Targeting, Combat("onde_de_rupture").Range));
        // Bastion absolu : provoque les assaillants à 15 m, −30 % pour les alliés à 15 m ; la garde, sur soi, sans rayon.
        SkillCombatDefinition absolu = Combat("bastion_absolu");
        Assert.Null(absolu.Range);
        Assert.Equal(new[] { ("bastionAbsolute", ApplicationTarget.AllAllies, (Fixed?)F("15")), ("bastionAbsoluteGuard", ApplicationTarget.Targets, null),
            ("bastionAbsoluteTaunt", ApplicationTarget.AllEnemies, F("15")) }, absolu.Applies.Select(a => (a.Status, a.Target, a.Range)).ToArray());
        // Réactions : Riposte précise sur l'attaquant à 2,5 m ; Jugement du colosse dans un cercle de 5 m ; Serment du gardien sur
        // l'assaillant à 15 m du Bastion.
        TriggerEffect Effect(string talent, int effect = 0) =>
            bastion.Trees.SelectMany(t => t.Talents).Single(t => t.Id == $"talent.bastion.{talent}").Triggers[0].Effects[effect];
        Assert.Equal((TriggerTarget.EventSource, (Fixed?)F("2.5")), (Effect("contrecoup.riposte_precise").Target!.Value, Effect("contrecoup.riposte_precise").Range));
        Assert.Equal((TriggerTarget.AllEnemies, (Fixed?)F("5")), (Effect("contrecoup.jugement_du_colosse").Target!.Value, Effect("contrecoup.jugement_du_colosse").Range));
        Assert.Null(Effect("contrecoup.jugement_du_colosse", 1).Range);
        Assert.Equal((TriggerTarget.EventSource, (Fixed?)F("15")), (Effect("gardien.serment_du_gardien").Target!.Value, Effect("gardien.serment_du_gardien").Range));
        // Lien protecteur : actif tant que l'allié est à 15 m (portée de l'altération, dans les règles).
        Assert.True(ShippedData.Rules.Combat.Statuses.TryGet("protectiveLink", out StatusDefinition link));
        Assert.Equal(F("15"), link.Range);
        Assert.All(ShippedData.Rules.Combat.Statuses.Definitions.Where(d => d.Id != "protectiveLink"), d => Assert.Null(d.Range));
    }

    [Fact]
    public void Seules_les_portees_changent_depuis_le_contenu_0_5_0()
    {
        JsonObject shipped = JsonNode.Parse(File.ReadAllText(ClassTestData.BastionPath))!.AsObject();
        JsonObject frozen = JsonNode.Parse(File.ReadAllText(Content050ClassPath))!.AsObject();
        Assert.Equal(("0.6.0-provisoire", "0.5.0-provisoire"), (shipped["contentVersion"]!.GetValue<string>(), frozen["contentVersion"]!.GetValue<string>()));
        int removed = 0;
        void Strip(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o.Remove("range")) removed++;
                    foreach (var (_, child) in o.ToList()) Strip(child);
                    break;
                case JsonArray a:
                    foreach (JsonNode? child in a) Strip(child);
                    break;
            }
        }
        Strip(shipped["data"]);
        shipped["contentVersion"] = "0.5.0-provisoire";
        Assert.Equal(11, removed);   // six compétences, deux applications de Bastion absolu, trois réactions, et aucune autre
        Assert.Equal(frozen.ToJsonString(), shipped.ToJsonString());
    }

    [Fact]
    public void Bastion_livre_au_contenu_0_6_0()
    {
        ClassLoadResult result = ClassJsonReader.ReadFile(ClassTestData.BastionPath, ClassTestData.Progression);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal((9, "0.6.0-provisoire"), (result.Class!.SchemaVersion, result.Class.ContentVersion));
        Assert.Equal("sha256:5224f8119fd2f729d41bfc3bfc7ef9d5859f90f871b6ccf0f85ad32110690876", result.Class.Fingerprint);
    }

    [Fact]
    public void Une_portee_change_l_empreinte_de_la_classe()
    {
        string Fingerprint(Action<JsonObject> mutate) => ClassTestData.ReadMutated(mutate).Class!.Fingerprint;
        string[] fingerprints =
        {
            ClassJsonReader.ReadFile(ClassTestData.BastionPath, ClassTestData.Progression).Class!.Fingerprint,
            Fingerprint(r => ClassTestData.Skill(r, 0)["combat"]!["range"] = 3),
            Fingerprint(r => ClassTestData.Skill(r, 0)["combat"]!.AsObject().Remove("range")),
            Fingerprint(r => ClassTestData.Skill(r, 6)["combat"]!["applies"]![0]!["range"] = 14),
            Fingerprint(r => ClassTestData.Talent(r, 1, 0)["triggers"]![0]!["effects"]![0]!["range"] = 3),
        };
        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
    }

    // --- Lecture et refus -----------------------------------------------------------------------------------

    [Fact]
    public void Portee_hors_bornes_refusee_au_chemin_du_fichier()
    {
        ValidationError low = SingleError(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 0)["combat"]!["range"] = 0.05).Report);
        Assert.Equal("$.data.skills[0].combat.range", low.Path);
        Assert.Contains("[0.1, 100]", low.Message);
        Assert.Equal("$.data.skills[6].combat.applies[0].range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 6)["combat"]!["applies"]![0]!["range"] = 101).Report).Path);
        Assert.Equal("$.data.trees[1].talents[0].triggers[0].effects[0].range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Talent(r, 1, 0)["triggers"]![0]!["effects"]![0]!["range"] = 0).Report).Path);
        Assert.Equal("$.data.skills[0].combat.range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 0)["combat"]!["range"] = "loin").Report).Path);
    }

    [Fact]
    public void Portee_sans_objet_refusee()
    {
        // Bastion absolu (soi) : pas de portée ; sa garde (application sur soi) : pas de rayon ; Égide réactive (porteur) : pas de portée.
        Assert.Equal("$.data.skills[6].combat.range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 6)["combat"]!["range"] = 15).Report).Path);
        Assert.Equal("$.data.skills[6].combat.applies[1].range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 6)["combat"]!["applies"]![1]!["range"] = 15).Report).Path);
        int aegis = ClassTestData.Shipped()["data"]!["trees"]![2]!["talents"]!.AsArray().Select((t, i) => (t, i))
            .Single(x => x.t!["id"]!.GetValue<string>() == "talent.bastion.gardien.egide_reactive").i;
        Assert.Equal($"$.data.trees[2].talents[{aegis}].triggers[0].effects[0].range",
            SingleError(ClassTestData.ReadMutated(r => ClassTestData.Talent(r, 2, aegis)["triggers"]![0]!["effects"]![0]!["range"] = 5).Report).Path);
    }

    [Fact]
    public void Portee_d_une_alteration_seulement_pour_une_protection()
    {
        RulesLoadResult protection = RulesTestData.ReadMutated(r => Status(r, "protectiveLink")["range"] = 20);
        Assert.True(protection.IsValid, protection.Report.ToString());
        Assert.Equal(F("20"), protection.Rules!.Combat.Statuses.Definitions.Single(d => d.Id == "protectiveLink").Range);
        ValidationError taunt = SingleError(RulesTestData.ReadMutated(r => Status(r, "taunt")["range"] = 15).Report);
        Assert.Matches(@"^\$\.data\.statuses\[\d+\]\.range$", taunt.Path);
        Assert.Contains("protection", taunt.Message);
        Assert.Matches(@"^\$\.data\.statuses\[\d+\]\.range$", SingleError(RulesTestData.ReadMutated(r => Status(r, "protectiveLink")["range"] = 0.01).Report).Path);
        // Sans portée : lien actif à toute distance (règles antérieures).
        RulesLoadResult unbounded = RulesTestData.ReadMutated(r => Status(r, "protectiveLink").Remove("range"));
        Assert.True(unbounded.IsValid);
        Assert.Null(unbounded.Rules!.Combat.Statuses.Definitions.Single(d => d.Id == "protectiveLink").Range);
        Assert.NotEqual(unbounded.Rules.Fingerprint, ShippedData.Rules.Fingerprint);
    }

    private static JsonObject Status(JsonObject rules, string id) =>
        rules["data"]!["statuses"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == id)!.AsObject();

    [Fact]
    public void Portee_inconnue_dans_les_declencheurs_des_regles_et_des_scenarios()
    {
        // Règles : un déclencheur d'altération garde la forme du schéma 10 ; scénarios : attaques et déclencheurs d'assaillant aussi.
        ValidationError rules = SingleError(RulesTestData.ReadMutated(r => Status(r, "taunt")["triggers"] = JsonNode.Parse(
            """[ { "on": "onHitTaken", "role": "target", "effects": [ { "type": "strike", "target": "eventSource", "coefficient": 1, "powerType": "physique", "damageType": "physique", "range": 2 } ] } ]""")).Report);
        Assert.Equal((ValidationCode.UnknownField, true), (rules.Code, rules.Path.EndsWith(".triggers[0].effects[0].range", StringComparison.Ordinal)));
        ScenarioLoadResult trigger = ReadMutated(s => s["data"]!["combatants"]![1]!["triggers"] = JsonNode.Parse(
            """[ { "on": "onHitTaken", "role": "target", "effects": [ { "type": "strike", "target": "eventSource", "coefficient": 1, "powerType": "physique", "damageType": "physique", "range": 2 } ] } ]"""),
            scenario: Portees);
        Assert.Equal(("$.data.combatants[1].triggers[0].effects[0].range", ValidationCode.UnknownField), (SingleError(trigger.Report).Path, SingleError(trigger.Report).Code));
        ScenarioLoadResult applies = ReadMutated(s => s["data"]!["combatants"]![1]!["attacks"]![0]!["applies"] = JsonNode.Parse("""[ { "status": "fragility", "range": 3 } ]"""),
            scenario: Portees);
        Assert.Equal(("$.data.combatants[1].attacks[0].applies[0].range", ValidationCode.UnknownField), (SingleError(applies.Report).Path, SingleError(applies.Report).Code));
    }

    [Fact]
    public void Schemas_decrivent_les_portees()
    {
        JsonNode Schema(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", name)))!;
        JsonNode klass = Schema("class.schema.json");
        Assert.Equal(9, klass["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode combat = klass["$defs"]!["combat"]!;
        Assert.Equal((0.1, 100.0), (combat["properties"]!["range"]!["minimum"]!.GetValue<double>(), combat["properties"]!["range"]!["maximum"]!.GetValue<double>()));
        Assert.NotNull(combat["properties"]!["applies"]!["items"]!["properties"]!["range"]);
        Assert.NotNull(klass["$defs"]!["trigger"]!["properties"]!["effects"]!["items"]!["properties"]!["range"]);
        JsonNode rules = Schema("rules.schema.json");
        Assert.Equal(DataFormat.RulesSchemaVersion, rules["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Null(rules["$defs"]!["trigger"]!["properties"]!["effects"]!["items"]!["properties"]!["range"]);
        Assert.Null(Schema("scenario.schema.json")["$defs"]!["trigger"]!["properties"]!["effects"]!["items"]!["properties"]!["range"]);
    }

    // --- Scénario livré -------------------------------------------------------------------------------------

    private static List<JsonElement> Simulate(out CombatResult result)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath(Portees), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        result = CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
        return log.Lines.Select(l => JsonDocument.Parse(l).RootElement).Where(e => e.TryGetProperty("event", out _)).ToList();
    }

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Tick(JsonElement e) => e.GetProperty("tick").GetInt64();

    [Fact]
    public void Scenario_livre_le_Bastion_au_contact()
    {
        List<JsonElement> events = Simulate(out CombatResult result);
        IEnumerable<JsonElement> Of(string kind) => events.Where(e => S(e, "event") == kind);
        // Tick 0 : le loup est trop loin pour la Frappe, l'archer est caché par le muret pour la Provocation.
        Assert.Equal(new[] { (0L, "skill.bastion.frappe_du_rempart", "loup", "targetOutOfRange"), (0L, "skill.bastion.provocation", "archer", "noLineOfSight") },
            Of("commandRejected").Select(e => (Tick(e), S(e, "skill")!, S(e, "target")!, S(e, "reason")!)).ToArray());
        // Le Bastion et le loup marchent l'un vers l'autre ; au tick 13, la Frappe part (bords à 2,5 m au plus).
        Assert.Equal(new[] { (13L, "skill.bastion.frappe_du_rempart"), (34L, "skill.bastion.onde_de_rupture"), (70L, "skill.bastion.provocation") },
            Of("skillCast").Select(e => (Tick(e), S(e, "skill")!)).ToArray());
        // L'onde ne touche que le loup (l'ours et l'archer sont hors du cercle de 5 m).
        Assert.Equal("loup", Assert.Single(Of("damage"), e => S(e, "skill") == "skill.bastion.onde_de_rupture").GetProperty("target").GetString());
        // Parade parfaite contre le loup (tick 40) : Riposte précise ; contre la flèche de l'archer (tick 60) : riposte annulée, hors de portée.
        JsonElement riposte = Assert.Single(Of("damage"), e => S(e, "trigger") == "talent.bastion.contrecoup.riposte_precise/0@bastion#0");
        Assert.Equal((40L, "loup"), (Tick(riposte), S(riposte, "target")));
        JsonElement cancelled = Assert.Single(Of("reactionCancelled"));
        Assert.Equal((60L, "outOfRange", "talent.bastion.contrecoup.riposte_precise/0@bastion#0"), (Tick(cancelled), S(cancelled, "reason"), S(cancelled, "trigger")));
        // Tick 70 : l'ours, à moins de 15 m et en vue, est provoqué.
        Assert.Contains(Of("stateApplied"), e => Tick(e) == 70 && S(e, "state") == "taunt" && S(e, "combatant") == "ours");
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
    }
}
