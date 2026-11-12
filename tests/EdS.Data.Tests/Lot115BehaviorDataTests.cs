using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.5 : formats des comportements d'ennemi. Scénarios en schéma 8 (<c>behavior</c> d'un assaillant, <c>charge</c> d'une
/// attaque), journal au format 14 rejoué octet par octet ; scénario livré <c>espace_meute</c> : la meute attend, s'engage, approche,
/// encercle le Bastion et annonce ses zones ; le sanglier charge, l'archer garde ses distances.
/// </summary>
[Trait("Category", "Behavior")]
[Trait("Category", "Data")]
public class Lot115BehaviorDataTests
{
    private const string Meute = "espace_meute";

    private static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Meute);

    private static ValidationError Error(ScenarioLoadResult result)
    {
        Assert.False(result.IsValid);
        return Assert.Single(result.Report.Errors);
    }

    private static List<JsonElement> Simulate(ScenarioLoadResult loaded, out MemoryCombatLog log)
    {
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        return log.Lines.Select(l => JsonDocument.Parse(l).RootElement).Where(e => e.TryGetProperty("event", out _)).ToList();
    }

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Tick(JsonElement e) => e.GetProperty("tick").GetInt64();

    [Fact]
    public void Scenario_livre_la_meute_s_engage_encercle_charge_et_annonce()
    {
        List<JsonElement> events = Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Meute), ShippedData), out MemoryCombatLog log);
        IEnumerable<JsonElement> Of(string kind) => events.Where(e => S(e, "event") == kind);
        // Les deux premiers loups voient le Bastion approcher (aggro) ; le reste de la meute suit (pack).
        var engaged = Of("engaged").Select(e => (S(e, "actor")!, S(e, "reason")!)).ToList();
        Assert.Equal(6, engaged.Count);
        Assert.Equal(new[] { ("loup1", "aggro"), ("loup2", "aggro") }, engaged.Take(2));
        Assert.All(engaged.Skip(2), e => Assert.Equal("pack", e.Item2));
        Assert.Contains(Of("attackSkipped"), s => S(s, "reason") == "notEngaged");
        // Les quatre loups finissent autour du Bastion, répartis tout autour.
        JsonElement bastion = Of("combatantSummary").Single(s => S(s, "combatant") == "bastion");
        (double X, double Z) P(JsonElement s) => (s.GetProperty("position")[0].GetDouble(), s.GetProperty("position")[1].GetDouble());
        var center = P(bastion);
        double[] angles = Of("combatantSummary").Where(s => S(s, "combatant")!.StartsWith("loup", StringComparison.Ordinal))
            .Select(s => Math.Atan2(P(s).Z - center.Z, P(s).X - center.X) * 180 / Math.PI).OrderBy(a => a).ToArray();
        Assert.Equal(4, angles.Length);
        for (int k = 0; k < 4; k++) Assert.InRange((angles[(k + 1) % 4] - angles[k] + 360) % 360, 50, 130);
        // Le sanglier charge (bande annoncée, puis il fonce) ; l'archer garde ses distances et annonce sa pluie.
        Assert.Equal(3, Of("charged").Count());   // ticks 60, 140, 220 (toutes les 4 s, annonce de 1 s)
        Assert.Contains(Of("attackLanded"), l => S(l, "attack") == "charge" && l.GetProperty("targets").EnumerateArray().Any(t => t.GetString() == "bastion"));
        JsonElement archer = Of("combatantSummary").Single(s => S(s, "combatant") == "archer");
        double gap = Math.Sqrt(Math.Pow(P(archer).X - center.X, 2) + Math.Pow(P(archer).Z - center.Z, 2)) - 1;
        Assert.InRange(gap, 5.5, 9.5);
        Assert.Contains(Of("attackLanded"), l => S(l, "attack") == "pluie");
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void En_tete_du_journal_porte_les_comportements_et_la_charge()
    {
        Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Meute), ShippedData), out MemoryCombatLog log);
        JsonElement combatants = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario").GetProperty("combatants");
        Assert.Equal("{\"maxRange\":0.5,\"aggroRange\":8,\"encircle\":true}", combatants[1].GetProperty("behavior").GetRawText());
        Assert.Equal("{\"maxRange\":9,\"minRange\":6,\"aggroRange\":14}", combatants[6].GetProperty("behavior").GetRawText());
        Assert.True(combatants[5].GetProperty("attacks")[0].GetProperty("charge").GetBoolean());
        Assert.False(combatants[0].TryGetProperty("behavior", out _));
    }

    [Fact]
    public void Comportement_et_charge_controles_au_chemin_du_fichier()
    {
        JsonObject Behavior(JsonObject root, int i) => DataOf(root)["combatants"]![i]!["behavior"]!.AsObject();
        Assert.Equal("$.data.combatants[1].behavior.maxRange", Error(Mutated(root => Behavior(root, 1).Remove("maxRange"))).Path);
        Assert.Equal("$.data.combatants[1].behavior.maxRange", Error(Mutated(root => Behavior(root, 1)["maxRange"] = 101)).Path);
        Assert.Equal("$.data.combatants[6].behavior.minRange", Error(Mutated(root => Behavior(root, 6)["minRange"] = 10)).Path);
        Assert.Equal("$.data.combatants[1].behavior.aggroRange", Error(Mutated(root => Behavior(root, 1)["aggroRange"] = 0)).Path);
        ValidationError unknown = Error(Mutated(root => Behavior(root, 1)["fuite"] = true));
        Assert.Equal(("$.data.combatants[1].behavior.fuite", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
        Assert.Equal("$.data.combatants[0].behavior", Error(Mutated(root => DataOf(root)["combatants"]![0]!["behavior"] = JsonNode.Parse("{\"maxRange\":1}"))).Path);
        Assert.Equal("$.data.combatants[1].attacks[0].charge", Error(Mutated(root => DataOf(root)["combatants"]![1]!["attacks"]![0]!["charge"] = true)).Path);
        Assert.Equal("$.data.commands[0].action", Error(Mutated(root => DataOf(root)["commands"]![0]!["actor"] = "loup1")).Path);
        // Revue Codex de la PR #33 : avec un comportement, la cible peut être omise (pas seulement nulle).
        ScenarioLoadResult omitted = Mutated(root => DataOf(root)["combatants"]![1]!.AsObject().Remove("target"));
        Assert.True(omitted.IsValid, omitted.Report.ToString());
        Assert.Null(omitted.Setup!.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>().Single(a => a.Id == "loup1").Target);
    }

    [Fact]
    public void Schema_8_et_schema_JSON()
    {
        Assert.Equal(18, ScenarioValidator.CurrentSchemaVersion);   // V2.26 : 18 (état emporté, secteurs) ; V2.4.1 : 17 (bêtes qui se promènent) ; V2.4 : 16 (bêtes qui fuient) ; V1.8d : 15 (bonus modifié) ; V1.3 : 14 (équipement) ;   // sous-lot 11.9 : 9 (obstacle bas, poussée, arrêt pendant l'annonce, meute) ; 12.1 : 10 ; 12.3 : 11 ; 12.6 : 12 ; V1.2 : 13
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Error(Mutated(root => root["schemaVersion"] = 7)).Path, Error(Mutated(root => root["schemaVersion"] = 7)).Code));
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(18, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode behavior = schema["$defs"]!["combatant"]!["properties"]!["behavior"]!;
        Assert.Equal(new[] { "maxRange", "minRange", "aggroRange", "encircle", "pack", "flee", "wander" }, behavior["properties"]!.AsObject().Select(p => p.Key));   // V2.4 : flee ; V2.4.1 : wander
        Assert.Equal("boolean", schema["$defs"]!["attack"]!["properties"]!["charge"]!["type"]!.GetValue<string>());
    }
}
