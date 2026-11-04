using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4c : formats des attaques annoncées. Scénarios en schéma 6 (forme, annonce, dimensions et défenses des attaques,
/// action <c>select</c>), règles en schéma 12 (<c>combat.guard.arcDegrees</c>, <c>combat.telegraph.minReactionMs</c>), journal au
/// format 12 rejoué octet par octet depuis son en-tête ; scénario livré <c>espace_annonces</c> : une sortie de zone évite le coup, au tick près.
/// </summary>
[Trait("Category", "Space")]
[Trait("Category", "Data")]
public class Lot114cAttackDataTests
{
    private const string Annonces = "espace_annonces";

    private static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Annonces);

    private static ValidationError Error(ScenarioLoadResult result)
    {
        Assert.False(result.IsValid);
        return Assert.Single(result.Report.Errors);
    }

    private static JsonObject Attack(JsonObject root, int combatant, int attack) => DataOf(root)["combatants"]![combatant]!["attacks"]![attack]!.AsObject();

    private static List<JsonElement> Simulate(ScenarioLoadResult loaded, out MemoryCombatLog log)
    {
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        return log.Lines.Select(l => JsonDocument.Parse(l).RootElement).Where(e => e.TryGetProperty("event", out _)).ToList();
    }

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Tick(JsonElement e) => e.GetProperty("tick").GetInt64();

    private static string[] Targets(JsonElement landed) => landed.GetProperty("targets").EnumerateArray().Select(t => t.GetString()!).ToArray();

    [Fact]
    public void Scenario_livre_esquive_garde_de_face_et_morsure_de_dos()
    {
        List<JsonElement> events = Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Annonces), ShippedData), out MemoryCombatLog log);
        IEnumerable<JsonElement> Of(string kind) => events.Where(e => S(e, "event") == kind);
        // Tick 20 : l'ogre annonce un cercle de 2 m sur le Bastion (impact au tick 38) ; le Bastion marche au nord de 2,75 m et l'évite.
        JsonElement ecrasement = Assert.Single(Of("attackStarted"), e => S(e, "attack") == "ecrasement");
        Assert.Equal((20L, "circle", 38L, "[-4,0]"), (Tick(ecrasement), S(ecrasement, "shape"), ecrasement.GetProperty("impactTick").GetInt64(), ecrasement.GetProperty("origin").GetRawText()));
        JsonElement esquive = Assert.Single(Of("attackLanded"), e => S(e, "attack") == "ecrasement");
        Assert.Equal(38L, Tick(esquive));
        Assert.Empty(Targets(esquive));
        // Tick 80 : souffle (cône de 8 m, 90°), qui se garde ; le Bastion, en garde et tourné vers l'ogre sélectionné, le garde de face.
        JsonElement souffle = Assert.Single(Of("attackLanded"), e => S(e, "attack") == "souffle");
        Assert.Equal(102L, Tick(souffle));
        Assert.Equal(new[] { "bastion" }, Targets(souffle));
        Assert.Equal("guard", S(Assert.Single(Of("damage"), d => S(d, "attack") == "souffle"), "defense"));
        // Le loup mord de dos : dégâts pleins, deux fois.
        Assert.Equal(new[] { (100L, "behind"), (140L, "behind") }, Of("damage").Where(d => S(d, "attack") == "morsure").Select(d => (Tick(d), S(d, "defense")!)).ToArray());
        // Tick 140 : le coup de l'ogre (2,5 m) ne part pas, le Bastion est trop loin.
        Assert.Equal(("coup", "targetOutOfRange"), (S(Assert.Single(Of("attackSkipped")), "attack"), S(Assert.Single(Of("attackSkipped")), "reason")));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Un_tick_trop_tot_et_le_cercle_frappe()
    {
        // Arrêt à 1600 ms au lieu de 1650 : 10 pas de 0,25 m, le bord de l'emprise est sur le cercle à l'impact (frontière comprise).
        List<JsonElement> events = Simulate(Mutated(root => DataOf(root)["commands"]![2]!["atMs"] = 1600), out _);
        JsonElement landed = Assert.Single(events, e => S(e, "event") == "attackLanded" && S(e, "attack") == "ecrasement");
        Assert.Equal(new[] { "bastion" }, Targets(landed));
    }

    [Fact]
    public void Forme_inconnue_et_dimensions_au_chemin_du_fichier()
    {
        ValidationError shape = Error(Mutated(root => Attack(root, 1, 0)["shape"] = "spirale"));
        Assert.Equal(("$.data.combatants[1].attacks[0].shape", ValidationCode.UnknownReference), (shape.Path, shape.Code));
        ValidationError center = Error(Mutated(root => Attack(root, 1, 0)["center"] = "milieu"));
        Assert.Equal("$.data.combatants[1].attacks[0].center", center.Path);
        Assert.Equal("$.data.combatants[1].attacks[1].angle", Error(Mutated(root => Attack(root, 1, 1).Remove("angle"))).Path);
        Assert.Equal("$.data.combatants[1].attacks[0].telegraphMs", Error(Mutated(root => Attack(root, 1, 0)["telegraphMs"] = 500)).Path);
        Assert.Equal("$.data.combatants[1].attacks[0].parryable", Error(Mutated(root => Attack(root, 1, 0)["parryable"] = true)).Path);
        ValidationError unknown = Error(Mutated(root => Attack(root, 1, 0)["hauteur"] = 3));
        Assert.Equal(("$.data.combatants[1].attacks[0].hauteur", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
        // Sans espace, une forme est refusée.
        ScenarioLoadResult flat = ReadMutated(root => DataOf(root)["combatants"]![1]!["attacks"]![0]!["shape"] = "target", scenario: "entrainement_garde_parade");
        Assert.Equal("$.data.combatants[1].attacks[0].shape", Error(flat).Path);
    }

    [Fact]
    public void Commande_select_lue_et_controlee()
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath(Annonces), ShippedData);
        CombatCommand select = loaded.Setup!.Commands[0];
        Assert.Equal((CombatAction.Select, "ogre", (string?)null), (select.Action, select.Target, select.Skill));
        Assert.True(Mutated(root => DataOf(root)["commands"]![0]!.AsObject().Remove("target")).IsValid);   // efface la sélection
        Assert.Equal("$.data.commands[0].skill", Error(Mutated(root => DataOf(root)["commands"]![0]!["skill"] = "skill.bastion.frappe_du_rempart")).Path);
        Assert.Equal("$.data.commands[0].target", Error(Mutated(root => DataOf(root)["commands"]![0]!["target"] = "inconnu")).Path);
        Assert.Equal("$.data.commands[3].target", Error(Mutated(root => DataOf(root)["commands"]![3]!["target"] = "ogre")).Path);   // guardStart
        // Sans espace : refusée.
        ScenarioLoadResult flat = ReadMutated(root => DataOf(root)["commands"]!.AsArray().Add(JsonNode.Parse("""{ "atMs": 0, "actor": "bastion", "action": "select", "target": "mannequin" }""")));
        Assert.EndsWith(".action", Error(flat).Path);
    }

    [Fact]
    public void Schema_6_et_schemas_JSON()
    {
        Assert.Equal(15, ScenarioValidator.CurrentSchemaVersion);   // V1.8d : 15 (bonus modifié) ; V1.3 : 14 (équipement) ;   // sous-lot 11.4d : 7 (arrivées, départ) ; 11.5 : 8 (comportements) ; 11.9 : 9 ; 12.1 : 10 ; 12.3 : 11 ; 12.6 : 12
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Error(Mutated(root => root["schemaVersion"] = 5)).Path, Error(Mutated(root => root["schemaVersion"] = 5)).Code));
        JsonNode scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(15, scenario["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode attack = scenario["$defs"]!["attack"]!["properties"]!;
        Assert.Equal(new[] { "target", "cone", "circle", "line", "persistent" }, attack["shape"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.All(new[] { "telegraphMs", "range", "radius", "angle", "width", "center", "persistentMs", "tickMs", "guardable", "parryable" }, f => Assert.NotNull(attack[f]));
        JsonNode rules = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json")))!;
        JsonNode combat = rules["properties"]!["data"]!["properties"]!["combat"]!["properties"]!;
        Assert.NotNull(combat["guard"]!["properties"]!["arcDegrees"]);
        Assert.NotNull(combat["telegraph"]!["properties"]!["minReactionMs"]);
    }

    [Fact]
    public void Regles_de_l_arc_et_des_annonces_lues()
    {
        Assert.Equal((180, 300L), (ShippedData.Rules.Combat.GuardArcDegrees, ShippedData.Rules.Combat.TelegraphMinReactionMs));
        RulesLoadResult changed = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["guard"]!["arcDegrees"] = 120);
        Assert.True(changed.IsValid, changed.Report.ToString());
        Assert.Equal(120, changed.Rules!.Combat.GuardArcDegrees);
        Assert.NotEqual(ShippedData.Rules.Fingerprint, changed.Rules.Fingerprint);
        Assert.Contains(RulesTestData.ReadMutated(r => r["data"]!["combat"]!["guard"]!["arcDegrees"] = 0).Report.Errors, e => e.Path == "$.data.combat.guard.arcDegrees");
        Assert.Contains(RulesTestData.ReadMutated(r => r["data"]!["combat"]!.AsObject().Remove("telegraph")).Report.Errors, e => e.Path.StartsWith("$.data.combat.telegraph", StringComparison.Ordinal));
    }

    [Fact]
    public void En_tete_du_journal_porte_les_formes_et_se_rejoue()
    {
        Simulate(ScenarioJsonReader.ReadFile(ScenarioPath(Annonces), ShippedData), out MemoryCombatLog log);
        JsonElement ogre = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario").GetProperty("combatants")[1];
        Assert.Equal("{\"id\":\"ecrasement\",\"rawDamage\":120,\"damageType\":\"physique\",\"targeting\":\"enemy\",\"unblockable\":false,\"applies\":[],\"shape\":\"circle\",\"telegraphMs\":900,\"radius\":2}",
            ogre.GetProperty("attacks")[0].GetRawText());
        Assert.Equal("{\"atMs\":0,\"actor\":\"bastion\",\"action\":\"select\",\"target\":\"ogre\"}",
            JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario").GetProperty("commands")[0].GetRawText());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Protocole_2_transmet_la_selection()
    {
        // Sous-lot 11.7 : le protocole 2 transmet la sélection de cible (au lot 10, refusée), sélection vide comprise.
        var select = new TickMessage(new TickCommands(3, new[] { CombatCommand.SelectTarget(150, "bastion", "ogre"), CombatCommand.SelectTarget(150, "renfort", null) }),
            "fnv1a64:0000000000000000");
        string text = MessageCodec.EncodeText(select);
        var read = (TickMessage)MessageCodec.Decode(System.Text.Encoding.UTF8.GetBytes(text), MessageDirection.ServerToClient, out string? error)!;
        Assert.True(read != null, error);
        Assert.Equal(new[] { (CombatAction.Select, "ogre"), (CombatAction.Select, (string?)null) }, read!.Commands.Commands.Select(c => (c.Action, c.Target)));
        Assert.Equal(text, MessageCodec.EncodeText(read));
    }
}
