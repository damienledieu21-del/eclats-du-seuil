using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.9 : formats des réglages demandés par Damien après l'essai du 22 octobre 2026. Scénarios en schéma 9 (<c>low</c> d'un
/// obstacle, <c>push</c> et <c>holdPosition</c> d'une attaque, <c>pack</c> d'un comportement), journal au format 15 rejoué octet par
/// octet ; Vallons livrés : rivière basse, sanglier qui repousse, sanglier et archer hors meute, pluie de flèches sans arrêt.
/// </summary>
[Trait("Category", "Behavior")]
[Trait("Category", "Data")]
public class Lot119ReglagesDataTests
{
    private const string Meute = "espace_meute";

    private static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();

    private static JsonObject Combatant(JsonObject root, string id) =>
        DataOf(root)["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == id);

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Meute);

    private static ValidationError Error(ScenarioLoadResult result)
    {
        Assert.False(result.IsValid);
        return Assert.Single(result.Report.Errors);
    }

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>La meute avec tous les réglages du sous-lot 11.9.</summary>
    private static void AllSettings(JsonObject root)
    {
        DataOf(root)["space"]!["obstacles"]![0]!.AsObject()["low"] = true;
        Combatant(root, "sanglier")["attacks"]![0]!.AsObject()["push"] = 2;
        Combatant(root, "sanglier")["behavior"]!.AsObject()["pack"] = false;
        Combatant(root, "archer")["attacks"]![1]!.AsObject()["holdPosition"] = false;
    }

    [Fact]
    public void Reglages_lus_depuis_le_scenario()
    {
        ScenarioLoadResult loaded = Mutated(AllSettings);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        CombatSetup setup = loaded.Setup!;
        Assert.True(setup.Space!.Obstacles[0].Low);
        AssailantSetup boar = setup.Combatants.OfType<AssailantSetup>().Single(a => a.Id == "sanglier");
        AssailantSetup archer = setup.Combatants.OfType<AssailantSetup>().Single(a => a.Id == "archer");
        Assert.Equal(Fixed.FromInt(2), boar.Attacks[0].Push);
        Assert.True(boar.Attacks[0].HoldPosition);
        Assert.False(boar.Behavior!.Pack);
        Assert.False(archer.Attacks[1].HoldPosition);
        Assert.Null(archer.Attacks[1].Push);
        Assert.True(archer.Behavior!.Pack);
        // Sans ces champs : les défauts du sous-lot 11.5 (obstacle haut, pas de poussée, arrêt pendant l'annonce, meute).
        CombatSetup plain = ScenarioJsonReader.ReadFile(ScenarioPath(Meute), ShippedData).Setup!;
        Assert.False(plain.Space!.Obstacles[0].Low);
        Assert.All(plain.Combatants.OfType<AssailantSetup>(), a =>
        {
            Assert.True(a.Behavior!.Pack);
            Assert.All(a.Attacks, at => Assert.True(at.HoldPosition && at.Push is null));
        });
    }

    [Fact]
    public void Erreurs_de_reglage_avec_leur_chemin()
    {
        ValidationError circle = Error(Mutated(root => Combatant(root, "archer")["attacks"]![1]!.AsObject()["push"] = 2));
        Assert.Equal("$.data.combatants[6].attacks[1].push", circle.Path);
        Assert.Contains("bande", circle.Message);
        Assert.Equal("$.data.combatants[6].attacks[0].push", Error(Mutated(root => Combatant(root, "archer")["attacks"]![0]!.AsObject()["push"] = 2)).Path);
        // holdPosition sur une attaque sans forme (morsure du loup réduite à une attaque historique) : refusé.
        Assert.Equal("$.data.combatants[1].attacks[0].holdPosition", Error(Mutated(root =>
        {
            JsonObject bite = Combatant(root, "loup1")["attacks"]![0]!.AsObject();
            bite.Remove("shape");
            bite.Remove("range");
            bite["holdPosition"] = false;
        })).Path);
        Assert.Equal("$.data.combatants[5].attacks[0].push", Error(Mutated(root => Combatant(root, "sanglier")["attacks"]![0]!.AsObject()["push"] = 0)).Path);
        Assert.Equal("$.data.combatants[5].attacks[0].push", Error(Mutated(root => Combatant(root, "sanglier")["attacks"]![0]!.AsObject()["push"] = "2")).Path);
        Assert.Equal("$.data.combatants[5].attacks[0].holdPosition",
            Error(Mutated(root => Combatant(root, "sanglier")["attacks"]![0]!.AsObject()["holdPosition"] = "non")).Path);
        Assert.Equal("$.data.combatants[5].behavior.pack", Error(Mutated(root => Combatant(root, "sanglier")["behavior"]!.AsObject()["pack"] = 0)).Path);
        Assert.Equal("$.data.space.obstacles[0].low", Error(Mutated(root => DataOf(root)["space"]!["obstacles"]![0]!.AsObject()["low"] = "oui")).Path);
        // Schéma 8 : refusé sans migration.
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion),
            (Error(Mutated(root => root["schemaVersion"] = 8)).Path, Error(Mutated(root => root["schemaVersion"] = 8)).Code));
    }

    [Fact]
    public void Schema_9_et_schema_JSON()
    {
        Assert.Equal(9, ScenarioValidator.CurrentSchemaVersion);
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(9, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Equal("boolean", schema["$defs"]!["combatant"]!["properties"]!["behavior"]!["properties"]!["pack"]!["type"]!.GetValue<string>());
        Assert.Equal("boolean", schema["$defs"]!["attack"]!["properties"]!["holdPosition"]!["type"]!.GetValue<string>());
        JsonNode push = schema["$defs"]!["attack"]!["properties"]!["push"]!;
        Assert.Equal(0.1, push["minimum"]!.GetValue<double>());
        Assert.Equal(100, push["maximum"]!.GetValue<double>());
        string text = schema.ToJsonString();
        Assert.Contains("\"low\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Meute_reglee_simulee_et_rejouee_a_l_identique()
    {
        // Avec tous les réglages, la meute joue autrement (hors meute, le sanglier attend que le Bastion vienne à sa distance d'engagement,
        // ce qu'il ne fait pas ici ; sans le réglage, la meute l'entraîne) et le journal se rejoue octet par octet depuis son en-tête (format 15).
        ScenarioLoadResult loaded = Mutated(AllSettings);
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Contains("\"low\":true", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"push\":2", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"pack\":false", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"holdPosition\":false", log.Lines[0], StringComparison.Ordinal);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
        List<JsonElement> events = log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement).ToList();
        Assert.DoesNotContain(events, e => S(e, "event") == "engaged" && S(e, "actor") == "sanglier");
        var plain = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(Meute), ShippedData).Setup!, plain);
        Assert.Contains(plain.Lines, l => l.Contains("\"event\":\"engaged\"", StringComparison.Ordinal)
            && l.Contains("\"actor\":\"sanglier\"", StringComparison.Ordinal) && l.Contains("\"reason\":\"pack\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Vallons_livres_avec_les_reglages_de_l_essai()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("vallons_demo")))!.AsObject();
        Assert.Equal(9, root["schemaVersion"]!.GetValue<int>());
        CombatSetup setup = ScenarioJsonReader.ReadFile(ScenarioPath("vallons_demo"), ShippedData).Setup!;
        Assert.Equal(new[] { "riviere_sud", "riviere_nord" }, setup.Space!.Obstacles.Where(o => o.Low).Select(o => o.Id));
        AssailantSetup boar = setup.Combatants.OfType<AssailantSetup>().Single(a => a.Id == "sanglier");
        AssailantSetup archer = setup.Combatants.OfType<AssailantSetup>().Single(a => a.Id == "archer");
        Assert.Equal((Fixed.FromInt(2), true), (boar.Attacks.Single().Push, boar.Attacks.Single().Charge));
        Assert.False(boar.Behavior!.Pack);
        Assert.False(archer.Behavior!.Pack);
        Assert.Equal(new[] { true, false }, archer.Attacks.Select(a => a.HoldPosition));
        Assert.All(setup.Combatants.OfType<AssailantSetup>().Where(a => a.Id.StartsWith("loup", StringComparison.Ordinal)), w => Assert.True(w.Behavior!.Pack));
        // La version à deux joueurs reprend les mêmes réglages.
        CombatSetup duo = ScenarioJsonReader.ReadFile(ScenarioPath("coop/vallons_duo"), ShippedData).Setup!;
        Assert.Equal(new[] { "riviere_sud", "riviere_nord" }, duo.Space!.Obstacles.Where(o => o.Low).Select(o => o.Id));
        Assert.Equal(Fixed.FromInt(2), duo.Combatants.OfType<AssailantSetup>().Single(a => a.Id == "sanglier").Attacks.Single().Push);
    }

    [Fact]
    public void Marche_a_reculons_lue_ecrite_et_rejouee()
    {
        // Sous-lot 11.9 : « backward » sur un move du scénario ; refusé sur un arrêt ou une compétence ; écrit dans l'en-tête et rejoué.
        ScenarioLoadResult loaded = Mutated(root =>
        {
            JsonObject move = DataOf(root)["commands"]!.AsArray().Select(c => c!.AsObject()).First(c => c["action"]?.GetValue<string>() == "move");
            move["backward"] = true;
        });
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        CombatCommand backward = loaded.Setup!.Commands.First(c => c.Action == CombatAction.Move);
        Assert.True(backward.Backward);
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup, log);
        Assert.Contains("\"backward\":true", log.Lines[0], StringComparison.Ordinal);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
        int stop = DataOf(JsonNode.Parse(File.ReadAllText(ScenarioPath(Meute)))!.AsObject())["commands"]!.AsArray()
            .Select((c, k) => (c, k)).First(x => x.c!["action"]?.GetValue<string>() == "stop").k;
        Assert.Equal($"$.data.commands[{stop}].backward", Error(Mutated(root => DataOf(root)["commands"]![stop]!.AsObject()["backward"] = true)).Path);
        int skill = DataOf(JsonNode.Parse(File.ReadAllText(ScenarioPath(Meute)))!.AsObject())["commands"]!.AsArray()
            .Select((c, k) => (c, k)).First(x => x.c!["skill"] != null).k;
        Assert.Equal($"$.data.commands[{skill}].backward", Error(Mutated(root => DataOf(root)["commands"]![skill]!.AsObject()["backward"] = true)).Path);
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Contains("\"backward\"", schema.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pont_temps_reel_a_reculons()
    {
        // Le pont envoie move à reculons ; changer seulement le recul (même cap) renvoie un move ; à l'arrêt, le recul est oublié.
        RealtimeCombat combat = TrainingSession.Load(DataDirectory, ScenarioPath("vallons_demo"), "build.bastion.rempart", durationMs: 20_000).Start();
        combat.SetMove(-1, 0, backward: true);
        for (int i = 0; i < 10; i++) combat.StepOnce();
        CombatCommand first = Assert.Single(combat.SubmittedCommands, c => c.Action == CombatAction.Move);
        Assert.True(first.Backward);
        Assert.True(combat.Player().View.MoveBackward);
        Assert.Equal(new EdS.Domain.Space.GroundVector(Fixed.FromInt(1), Fixed.Zero), combat.Player().View.Facing);
        combat.SetMove(-1, 0);
        combat.StepOnce();
        Assert.False(combat.SubmittedCommands.Last(c => c.Action == CombatAction.Move).Backward);
        Assert.Equal(2, combat.SubmittedCommands.Count(c => c.Action == CombatAction.Move));
        combat.SetMove(0, 0, backward: true);
        combat.StepOnce();
        Assert.Equal(CombatAction.Stop, combat.SubmittedCommands.Last(c => c.Action is CombatAction.Move or CombatAction.Stop).Action);
    }
}
