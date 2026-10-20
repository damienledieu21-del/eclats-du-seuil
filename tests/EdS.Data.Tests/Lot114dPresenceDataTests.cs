using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4d : formats des arrivées et des départs. Scénarios en schéma 7 (<c>arrivalMs</c> sur un combattant, action <c>leave</c>),
/// journal au format 13 rejoué octet par octet depuis son en-tête ; scénario livré <c>espace_renfort</c> : un second Bastion rejoint
/// l'affrontement, provoque l'ogre, puis part (la provocation tombe avec lui) ; un loup arrive, un autre est refusé (place occupée).
/// </summary>
[Trait("Category", "Presence")]
[Trait("Category", "Data")]
public class Lot114dPresenceDataTests
{
    private const string Renfort = "espace_renfort";

    private static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Renfort);

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
    public void Scenario_livre_renfort_qui_arrive_provoque_et_part()
    {
        List<JsonElement> events = Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData), out MemoryCombatLog log);
        IEnumerable<JsonElement> Of(string kind) => events.Where(e => S(e, "event") == kind);
        // Avant son arrivée, le renfort n'existe pas : sa garde à 2 s est refusée.
        Assert.Equal((40L, "renfort", "actorAbsent"), Of("commandRejected").Select(r => (Tick(r), S(r, "actor"), S(r, "reason"))).First());
        // Tick 60 : il arrive, sélectionne l'ogre et le provoque au tick suivant ; le jet de l'ogre (tick 80) le vise.
        Assert.Equal(new[] { (60L, "renfort"), (80L, "loup") }, Of("combatantJoined").Select(j => (Tick(j), S(j, "combatant")!)).ToArray());
        Assert.Equal((61L, "taunt", "renfort"), Of("stateApplied").Where(a => S(a, "combatant") == "ogre").Select(a => (Tick(a), S(a, "state"), S(a, "source"))).Single());
        Assert.Equal("renfort", S(Of("attackStarted").Single(a => S(a, "attack") == "jet"), "target"));
        // Le second loup arriverait sur l'ogre : refusé.
        JsonElement refused = Assert.Single(Of("arrivalRefused"));
        Assert.Equal((80L, "loup_cache", "occupied", "ogre"), (Tick(refused), S(refused, "combatant"), S(refused, "reason"), S(refused, "blocker")));
        // Tick 100 : il part en fin de tick, sa provocation tombe avec lui ; l'ogre revient au Bastion.
        JsonElement left = Assert.Single(Of("combatantLeft"));
        Assert.Equal((100L, "renfort"), (Tick(left), S(left, "combatant")));
        JsonElement removed = Assert.Single(Of("stateRemoved"));
        Assert.Equal(("ogre", "taunt", "sourceLeft", left.GetProperty("eid").GetInt64()), (S(removed, "combatant"), S(removed, "state"), S(removed, "reason"), removed.GetProperty("cause").GetInt64()));
        Assert.Equal("bastion", S(Of("attackStarted").Last(a => S(a, "actor") == "ogre"), "target"));
        Assert.Equal((110L, "actorAbsent"), Of("commandRejected").Where(r => S(r, "actor") == "renfort").Select(r => (Tick(r), S(r, "reason"))).Last());
        Assert.Equal(new[] { ("renfort", "left"), ("loup", "joined"), ("loup_cache", "refused") },
            Of("combatantSummary").Where(s => s.TryGetProperty("presence", out _)).Select(s => (S(s, "combatant")!, S(s, "presence")!)).ToArray());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void En_tete_du_journal_porte_les_arrivees_et_les_departs()
    {
        Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData), out MemoryCombatLog log);
        JsonElement scenario = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario");
        Assert.Equal(3000L, scenario.GetProperty("combatants")[1].GetProperty("arrivalMs").GetInt64());
        Assert.False(scenario.GetProperty("combatants")[0].TryGetProperty("arrivalMs", out _));
        Assert.Contains(scenario.GetProperty("commands").EnumerateArray(), c => c.GetRawText() == "{\"atMs\":5000,\"actor\":\"renfort\",\"action\":\"leave\"}");
        Assert.Equal(13, JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    public void Arrivee_et_depart_controles_au_chemin_du_fichier()
    {
        Assert.Equal("$.data.combatants[1].arrivalMs", Error(Mutated(root => DataOf(root)["combatants"]![1]!["arrivalMs"] = 3010)).Path);
        Assert.Equal("$.data.combatants[1].arrivalMs", Error(Mutated(root => DataOf(root)["combatants"]![1]!["arrivalMs"] = 9000)).Path);
        Assert.Equal("$.data.combatants[1].arrivalMs", Error(Mutated(root => DataOf(root)["combatants"]![1]!["arrivalMs"] = "tôt")).Path);
        JsonObject Leave(JsonObject root) => DataOf(root)["commands"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["action"] == "leave");
        Assert.Equal("$.data.commands[6].target", Error(Mutated(root => Leave(root)["target"] = "ogre")).Path);
        Assert.Equal("$.data.commands[6].skill", Error(Mutated(root => Leave(root)["skill"] = "skill.bastion.provocation")).Path);
        // Sans espace aussi : arrivée et départ ne dépendent pas des positions.
        ScenarioLoadResult flat = ReadMutated(root =>
        {
            DataOf(root)["combatants"]![1]!["arrivalMs"] = 1000;
            DataOf(root)["commands"]!.AsArray().Add(JsonNode.Parse("""{ "atMs": 2000, "actor": "assaillant", "action": "leave" }"""));
        }, scenario: "entrainement_garde_parade");
        Assert.True(flat.IsValid, flat.Report.ToString());
        Assert.Equal(1000L, flat.Setup!.Combatants[1].ArrivalMs);
        Assert.Equal(CombatAction.Leave, flat.Setup.Commands[^1].Action);
    }

    [Fact]
    public void Schema_7_et_schema_JSON()
    {
        Assert.Equal(7, ScenarioValidator.CurrentSchemaVersion);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Error(Mutated(root => root["schemaVersion"] = 6)).Path, Error(Mutated(root => root["schemaVersion"] = 6)).Code));
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(7, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Equal("integer", schema["$defs"]!["combatant"]!["properties"]!["arrivalMs"]!["type"]!.GetValue<string>());
        Assert.Contains("leave", schema["$defs"]!["command"]!["properties"]!["action"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Protocole_du_lot_10_ne_transmet_pas_le_depart()
    {
        var leave = new TickMessage(new TickCommands(3, new[] { new CombatCommand(150, "bastion", CombatAction.Leave) }), "fnv1a64:0000000000000000");
        Assert.Contains("11.7", Assert.Throws<ArgumentException>(() => MessageCodec.Encode(leave)).Message);
    }
}
