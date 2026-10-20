using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4a : formats d'un combat positionné. Scénarios en schéma 5 (bloc <c>space</c>, places de départ, <c>move</c> et <c>stop</c>),
/// règles en schéma 10 (<c>combat.movement</c>, <c>combat.contact</c>), journal au format 10 rejoué octet par octet depuis son en-tête ;
/// scénario livré <c>espace_passage</c> (le Bastion tient un passage étroit) ; le protocole du lot 10 ne transmet pas les déplacements.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aSpaceDataTests
{
    private const string Passage = "espace_passage";

    private static GroundVector Raw(long x, long z) => GroundVector.FromRaw(x, z);

    private static ValidationError Error(ScenarioLoadResult result)
    {
        Assert.False(result.IsValid);
        return Assert.Single(result.Report.Errors);
    }

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Passage);

    [Fact]
    public void Scenario_livre_le_Bastion_tient_le_passage()
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath(Passage), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        CombatSetup setup = loaded.Setup!;
        Assert.Equal((3, ContactRule.Solid), (setup.Space!.Obstacles.Count, setup.Combat.PlayerCreatureContact));
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        var bastion = new List<GroundVector>();
        var loup = new List<GroundVector>();
        do
        {
            bastion.Add(engine.View("bastion").Position!.Value);
            loup.Add(engine.View("loup").Position!.Value);
        }
        while (engine.Step());
        // Le Bastion bute contre la face ouest du rocher (x = −4,5 − 0,5) et y reste tant qu'il pousse vers l'est.
        Assert.Equal(Raw(-52_500, 5000), bastion[3]);
        Assert.All(bastion.Skip(4).Take(7), p => Assert.Equal(Raw(-50_000, 5000), p));
        // Arrêté à l'entrée du passage (tick 44), en garde : le loup, arrivé de l'est, est retenu contre lui et le mur nord.
        Assert.All(Enumerable.Range(44, 77), t => Assert.Equal(Raw(18_330, -1607), bastion[t]));
        Assert.All(Enumerable.Range(48, 73), t => Assert.Equal(Raw(27_659, 4241), loup[t]));
        Assert.All(loup.Take(121), p => Assert.True(p.X > Fixed.Parse("2.4"), p.ToString()));
        // Il recule en garde, à la moitié de la vitesse : le loup passe.
        Assert.Equal(Raw(17_080, -1607), bastion[121]);
        Assert.Equal(Raw(15_830, -1607), bastion[122]);
        Assert.True(loup[^1].X < Fixed.FromInt(-5));
        Assert.DoesNotContain("trapPrevented", log.ToString());
        Assert.Equal(CombatEndReason.DurationReached, engine.Result!.EndReason);
        Assert.Equal(Raw(-6670, -1607), engine.Result["bastion"].Position);
        // Rejeu depuis l'en-tête : identique octet par octet.
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Resume_de_fin_porte_la_position_finale()
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(Passage), ShippedData).Setup!, log);
        JsonElement summary = log.Lines.Select(l => JsonDocument.Parse(l).RootElement).First(e => e.TryGetProperty("event", out var k) && k.GetString() == "combatantSummary");
        Assert.Equal("[-0.667,-0.1607]", summary.GetProperty("position").GetRawText());
    }

    [Fact]
    public void Champs_de_position_obligatoires_avec_l_espace()
    {
        ValidationError missing = Error(Mutated(root => Combatant(root, 1).Remove("position")));
        Assert.Equal(("$.data.combatants[1].position", ValidationCode.MissingField), (missing.Path, missing.Code));
        ValidationError radius = Error(Mutated(root => Combatant(root, 0).Remove("radius")));
        Assert.Equal(("$.data.combatants[0].radius", ValidationCode.MissingField), (radius.Path, radius.Code));
        Assert.True(Mutated(root => Combatant(root, 0).Remove("facing")).IsValid);   // facultatif : vers le nord
        Assert.True(Mutated(root => Combatant(root, 1).Remove("speed")).IsValid);    // facultatif : celle des règles
    }

    [Fact]
    public void Champs_de_position_refuses_sans_espace()
    {
        ScenarioLoadResult result = Mutated(root =>
        {
            DataOf(root).Remove("space");
            DataOf(root)["commands"] = new JsonArray();
        });
        string[] expected = { "$.data.combatants[0].facing", "$.data.combatants[0].position", "$.data.combatants[0].radius",
            "$.data.combatants[1].facing", "$.data.combatants[1].position", "$.data.combatants[1].radius", "$.data.combatants[1].speed" };
        Assert.Equal(expected, result.Report.Errors.Select(e => e.Path).OrderBy(p => p, StringComparer.Ordinal));
        Assert.All(result.Report.Errors, e => Assert.Equal(ValidationCode.InvalidValue, e.Code));
        ValidationError move = Error(ReadMutated(root => DataOf(root)["commands"] = new JsonArray(new JsonObject
        {
            ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(1, 0),
        })));
        Assert.Equal(("$.data.commands[0].action", ValidationCode.InvalidValue), (move.Path, move.Code));
    }

    [Theory]
    [InlineData("[1]", "$.data.combatants[0].position", ValidationCode.WrongType)]
    [InlineData("[1, 2, 3]", "$.data.combatants[0].position", ValidationCode.WrongType)]
    [InlineData("{\"x\": 1}", "$.data.combatants[0].position", ValidationCode.WrongType)]
    [InlineData("[1, \"2\"]", "$.data.combatants[0].position", ValidationCode.WrongType)]
    [InlineData("[-9.8, 0]", "$.data.combatants[0].position", ValidationCode.InvalidValue)]   // emprise hors de l'aire
    [InlineData("[-4, 0.5]", "$.data.combatants[0].position", ValidationCode.InvalidValue)]   // dans le rocher
    public void Position_mal_formee_ou_impossible(string json, string path, ValidationCode code)
    {
        ValidationError error = Error(Mutated(root => Combatant(root, 0)["position"] = JsonNode.Parse(json)));
        Assert.Equal((path, code), (error.Path, error.Code));
    }

    [Fact]
    public void Vitesse_propre_reservee_aux_assaillants()
    {
        ValidationError error = Error(Mutated(root => Combatant(root, 0)["speed"] = 6));
        Assert.Equal(("$.data.combatants[0].speed", ValidationCode.InvalidValue), (error.Path, error.Code));
        Assert.Contains("assailant", error.Message);
    }

    [Fact]
    public void Commandes_de_deplacement_et_direction()
    {
        ValidationError missing = Error(Mutated(root => Command(root, 0).Remove("direction")));
        Assert.Equal(("$.data.commands[0].direction", ValidationCode.MissingField), (missing.Path, missing.Code));
        ValidationError zero = Error(Mutated(root => Command(root, 0)["direction"] = new JsonArray(0, 0)));
        Assert.Equal(("$.data.commands[0].direction", ValidationCode.InvalidValue), (zero.Path, zero.Code));
        ValidationError onGuard = Error(Mutated(root => Command(root, 5)["direction"] = new JsonArray(1, 0)));
        Assert.Equal(("$.data.commands[5].direction", ValidationCode.InvalidValue), (onGuard.Path, onGuard.Code));
        ValidationError onStop = Error(Mutated(root => Command(root, 4)["direction"] = new JsonArray(1, 0)));
        Assert.Equal(("$.data.commands[4].direction", ValidationCode.InvalidValue), (onStop.Path, onStop.Code));
        ValidationError unknown = Error(Mutated(root => Command(root, 0)["action"] = "jump"));
        Assert.Equal(("$.data.commands[0].action", ValidationCode.UnknownReference), (unknown.Path, unknown.Code));
        Assert.Contains("move, stop", unknown.Message);
        ValidationError target = Error(Mutated(root => Command(root, 0)["target"] = "loup"));
        Assert.Equal(("$.data.commands[0].target", ValidationCode.InvalidValue), (target.Path, target.Code));
    }

    [Fact]
    public void Espace_mal_forme()
    {
        ValidationError bounds = Error(Mutated(root => DataOf(root)["space"]!.AsObject().Remove("bounds")));
        Assert.Equal(("$.data.space.bounds", ValidationCode.MissingField), (bounds.Path, bounds.Code));
        ValidationError max = Error(Mutated(root => DataOf(root)["space"]!["bounds"]!.AsObject().Remove("max")));
        Assert.Equal(("$.data.space.bounds.max", ValidationCode.MissingField), (max.Path, max.Code));
        ValidationError concave = Error(Mutated(root => DataOf(root)["space"]!["obstacles"]![0]!["points"] =
            JsonNode.Parse("[[-4.5, -0.2], [-3.5, -0.2], [-4, 0.5], [-3.5, 2], [-4.5, 2]]")));
        Assert.Equal(("$.data.space.obstacles[0].points", ValidationCode.InvalidValue), (concave.Path, concave.Code));
        ValidationError unknown = Error(Mutated(root => DataOf(root)["space"]!["height"] = 3));
        Assert.Equal(("$.data.space.height", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
        Assert.True(Mutated(root => DataOf(root)["space"]!.AsObject().Remove("obstacles")).IsValid);   // obstacles facultatifs
    }

    [Fact]
    public void Schema_5_et_schema_JSON()
    {
        Assert.Equal(5, ScenarioValidator.CurrentSchemaVersion);
        ValidationError old = Error(Mutated(root => root["schemaVersion"] = 4));
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (old.Path, old.Code));
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")));
        JsonElement root = schema.RootElement, defs = root.GetProperty("$defs");
        Assert.Equal(5, root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
        Assert.Equal("#/$defs/space", root.GetProperty("properties").GetProperty("data").GetProperty("properties").GetProperty("space").GetProperty("$ref").GetString());
        JsonElement combatant = defs.GetProperty("combatant").GetProperty("properties");
        Assert.All(new[] { "position", "facing", "radius", "speed" }, f => Assert.True(combatant.TryGetProperty(f, out _), f));
        Assert.Equal(new[] { "guardStart", "guardStop", "parry", "move", "stop" },
            defs.GetProperty("command").GetProperty("properties").GetProperty("action").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal((5.0, 0.1), (combatant.GetProperty("radius").GetProperty("maximum").GetDouble(), combatant.GetProperty("radius").GetProperty("minimum").GetDouble()));
        Assert.Equal(16, defs.GetProperty("space").GetProperty("properties").GetProperty("obstacles").GetProperty("items").GetProperty("properties")
            .GetProperty("points").GetProperty("maxItems").GetInt32());
    }

    [Fact]
    public void Regles_livrees_deplacement_et_contact()
    {
        CombatRules rules = ShippedData.Rules.Combat;
        Assert.Equal((Fixed.FromInt(5), Fixed.Parse("0.5"), ContactRule.Solid, Fixed.Parse("0.3"), Fixed.One),
            (rules.MovementSpeed, rules.GuardSpeedFactor, rules.PlayerCreatureContact, rules.CrossingSpeedFactor, rules.LargeCreatureRadius));
        RulesLoadResult token = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["contact"]!["playerCreature"] = "push");
        ValidationError e = Assert.Single(token.Report.Errors);
        Assert.Equal("$.data.combat.contact.playerCreature", e.Path);
        RulesLoadResult speed = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["movement"]!["speed"] = 0);
        Assert.Equal("$.data.combat.movement.speed", Assert.Single(speed.Report.Errors).Path);
        RulesLoadResult crossing = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["contact"]!["crossingSpeedFactor"] = 0);
        Assert.Equal("$.data.combat.contact.crossingSpeedFactor", Assert.Single(crossing.Report.Errors).Path);
        RulesLoadResult missing = RulesTestData.ReadMutated(r => r["data"]!["combat"]!.AsObject().Remove("movement"));
        Assert.Contains(missing.Report.Errors, x => x.Path == "$.data.combat.movement.speed" && x.Code == ValidationCode.MissingField);
    }

    [Fact]
    public void Seance_d_entrainement_garde_l_espace_et_les_places()
    {
        CombatSetup scenario = ScenarioJsonReader.ReadFile(ScenarioPath(Passage), ShippedData).Setup!;
        TrainingSession session = TrainingSession.Create(ShippedData, scenario, "build.bastion.contrecoup");
        RealtimeCombat combat = session.Start();
        combat.Advance(0.5);
        CombatSetup completed = combat.CompletedSetup();
        Assert.Same(scenario.Space, completed.Space);
        Assert.Equal(scenario.Combatants.Select(c => c.Placement!.Position), completed.Combatants.Select(c => c.Placement!.Position));
        Assert.Empty(completed.Validate());
    }

    [Fact]
    public void Protocole_du_lot_10_ne_transmet_pas_les_deplacements()
    {
        var move = new TickMessage(new TickCommands(3, new[] { new CombatCommand(150, "bastion", GroundVector.Parse("1", "0")) }), "fnv1a64:0000000000000000");
        ArgumentException refused = Assert.Throws<ArgumentException>(() => MessageCodec.Encode(move));
        Assert.Contains("11.7", refused.Message);
        // Un message bien formé dont la commande devient « move » est refusé à la lecture (action inconnue du protocole).
        var guard = new TickMessage(new TickCommands(3, new[] { new CombatCommand(150, "bastion", CombatAction.GuardStart) }), "fnv1a64:0000000000000000");
        string text = MessageCodec.EncodeText(guard);
        Assert.NotNull(MessageCodec.Decode(System.Text.Encoding.UTF8.GetBytes(text), MessageDirection.ServerToClient, out _));
        byte[] forged = System.Text.Encoding.UTF8.GetBytes(text.Replace("\"guardStart\"", "\"move\"", StringComparison.Ordinal));
        Assert.Null(MessageCodec.Decode(forged, MessageDirection.ServerToClient, out string? error));
        Assert.Contains("action inconnue", error);
    }
}
