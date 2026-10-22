using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Data.Builds;
using EdS.Domain.Builds;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.Tools.Measures;
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
    public void Build_remplace_sans_effacer_l_arrivee()
    {
        // Revue Codex de la PR #32 (20e tour) : remplacer le build d'un personnage (séance d'entraînement, mesures ; salle réseau dans
        // Lot102ServerTests) garde l'instant de son arrivée : le renfort n'est pas présent dès le tick 0.
        CombatSetup scenario = ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData).Setup!;
        long? Arrival(CombatSetup s, string id) => s.Combatants.Single(c => c.Id == id).ArrivalMs;
        Assert.Equal(3000L, Arrival(scenario, "renfort"));
        Assert.True(ShippedData.TryGetBuild("build.bastion.gardien", out ReferenceBuild gardien));
        TrainingSession session = TrainingSession.Create(ShippedData, scenario, gardien.Id, playerId: "renfort");
        CharacterSetup renfort = session.Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "renfort");
        Assert.Same(gardien.Build, renfort.Build);
        Assert.Equal(3000L, renfort.ArrivalMs);
        var late = new CombatSetup(scenario.Seed, scenario.DurationMs, scenario.Stats, scenario.Damage, scenario.Progression, scenario.Combat,
            scenario.Combatants.Select(c => c is CharacterSetup ch && ch.Id == "bastion" ? new CharacterSetup(ch.Id, ch.Team, ch.Class, ch.Build, ch.Initiative, ch.Placement, 1000) : c),
            scenario.Commands, scenario.Data, scenario.Space);
        CombatSetup measured = Measures.WithBuild(late, gardien);
        Assert.Same(gardien.Build, measured.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "bastion").Build);
        Assert.Equal(1000L, Arrival(measured, "bastion"));
    }

    [Fact]
    public void Aucune_intention_soumise_pour_un_joueur_absent()
    {
        // Revue Codex de la PR #32 (28e tour) : le renfort arrive au tick 60. Avant (et au tick même de son arrivée, où sa vue le dit
        // encore absent), garde tenue, parade et compétence ne produisent aucune commande : ni refus actorAbsent, ni quota consommé, ni
        // parade qui passerait au tick de l'arrivée. Une fois présent, ses intentions partent.
        CombatSetup scenario = ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData).Setup!;
        RealtimeCombat combat = TrainingSession.Create(ShippedData, scenario, "build.bastion.gardien", playerId: "renfort").Start();
        combat.SetGuard(true);
        while (combat.Tick <= 60)
        {
            Assert.False(combat.Player().View.IsPresent, $"tick {combat.Tick}");
            combat.Parry();
            combat.UseSlot(0);
            combat.StepOnce();
        }
        Assert.Empty(combat.SubmittedCommands);
        Assert.True(combat.Player().View.IsPresent);
        combat.Parry();
        combat.StepOnce();
        // Sous-lot 11.6 : dans ce combat positionné, la cible sélectionnée part aussi, une fois le renfort présent.
        Assert.Equal(new[] { CombatAction.GuardStart, CombatAction.Parry, CombatAction.Select }, combat.SubmittedCommands.Select(c => c.Action));
        Assert.All(combat.SubmittedCommands, c => Assert.Equal(61 * 50, c.AtMs));
    }

    [Fact]
    public void Retours_suivis_pour_un_combattant_entre_par_Join()
    {
        // Revue Codex de la PR #32 (29e tour) : le suivi des retours, créé avant l'entrée d'un combattant par Join, le découvre au premier
        // Collect qui suit (il levait KeyNotFoundException) ; il part de compteurs nuls : aucun faux retour, puis ses vrais retours.
        CombatSetup scenario = ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData).Setup!;
        CharacterSetup renfort = scenario.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "renfort");
        var setup = new CombatSetup(scenario.Seed, scenario.DurationMs, scenario.Stats, scenario.Damage, scenario.Progression, scenario.Combat,
            scenario.Combatants.Where(c => c.Id != "renfort"), scenario.Commands.Where(c => c.Actor != "renfort"), scenario.Data, scenario.Space);
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        var tracker = new FeedbackTracker(engine);
        while (engine.CurrentTick < 10)
        {
            engine.Step();
            tracker.Collect(engine);
        }
        tracker.Drain();
        engine.Join(new CharacterSetup(renfort.Id, renfort.Team, renfort.Class, renfort.Build, renfort.Initiative, renfort.Placement,
            engine.CurrentTick * engine.TickMilliseconds));
        engine.Step();
        tracker.Collect(engine);
        Assert.True(engine.View("renfort").IsPresent);
        Assert.DoesNotContain(tracker.Drain(), f => f.CombatantId == "renfort");
        engine.Submit(new CombatCommand(engine.CurrentTick * engine.TickMilliseconds, "renfort", CombatAction.Stop));   // refusé : il ne marche pas
        engine.Step();
        tracker.Collect(engine);
        Assert.Equal(FeedbackKind.Rejected, Assert.Single(tracker.Drain(), f => f.CombatantId == "renfort").Kind);
    }

    [Fact]
    public void En_tete_du_journal_porte_les_arrivees_et_les_departs()
    {
        Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Renfort), ShippedData), out MemoryCombatLog log);
        JsonElement scenario = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario");
        Assert.Equal(3000L, scenario.GetProperty("combatants")[1].GetProperty("arrivalMs").GetInt64());
        Assert.False(scenario.GetProperty("combatants")[0].TryGetProperty("arrivalMs", out _));
        Assert.Contains(scenario.GetProperty("commands").EnumerateArray(), c => c.GetRawText() == "{\"atMs\":5000,\"actor\":\"renfort\",\"action\":\"leave\"}");
        Assert.Equal(CombatEngine.LogFormatVersion, JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("formatVersion").GetInt32());
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
        Assert.Equal(8, ScenarioValidator.CurrentSchemaVersion);   // sous-lot 11.5 : 8 (comportements)
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Error(Mutated(root => root["schemaVersion"] = 6)).Path, Error(Mutated(root => root["schemaVersion"] = 6)).Code));
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(8, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Equal("integer", schema["$defs"]!["combatant"]!["properties"]!["arrivalMs"]!["type"]!.GetValue<string>());
        Assert.Contains("leave", schema["$defs"]!["command"]!["properties"]!["action"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Protocole_2_transmet_le_depart()
    {
        // Sous-lot 11.7 : le protocole 2 transmet le départ (au lot 10, refusé).
        var leave = new TickMessage(new TickCommands(3, new[] { new CombatCommand(150, "bastion", CombatAction.Leave) }), "fnv1a64:0000000000000000");
        var read = (TickMessage?)MessageCodec.Decode(MessageCodec.Encode(leave), MessageDirection.ServerToClient, out string? error);
        Assert.True(read != null, error);
        Assert.Equal(CombatAction.Leave, Assert.Single(read!.Commands.Commands).Action);
    }
}
