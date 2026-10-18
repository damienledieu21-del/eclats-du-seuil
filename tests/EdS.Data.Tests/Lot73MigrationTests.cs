using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.3.2 : non-régression du journal au format 4 (provenance). Les journaux des quatre scénarios livrés produits par le moteur
/// 0.8.1 (format 3, figés dans Fixtures/logs-0.8.1, produits au commit c2ddc7d) sont comparés aux journaux actuels : une fois retirés
/// les champs de provenance (<c>eid</c>, <c>cause</c>, <c>root</c>, <c>depth</c>, <c>trigger</c>) et, dans l'en-tête, les versions et
/// l'identité des données, l'égalité doit être exacte, ligne par ligne, ordre des champs et numéros de séquence compris. Le
/// gameplay ne change donc pas. Le graphe causal de chaque journal actuel est reconstruit et vérifié.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Trigger")]
public class Lot73MigrationTests
{
    public static readonly TheoryData<string> Scenarios = new() { "mannequin", "entrainement_garde_parade", "entrainement_rupture", "entrainement_alterations" };

    private static readonly HashSet<string> DiagnosticKinds = new(StringComparer.Ordinal)
    {
        "combatant", "commandRejected", "attackSkipped", "stateBlocked", "simulationAborted", "combatantSummary", "combatEnded",
    };

    private static string[] Historical(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.8.1", name + ".jsonl"));

    private static IReadOnlyList<string> Current(string name)
    {
        var log = new MemoryCombatLog();
        // Sous-lot 8.4 : contenu historique du Bastion (ScenarioTestData.HistoricalData) ; le contenu livré change le gameplay. Sous-lot
        // 8.5 : scénarios figés du 8.4 (passifs « [TEST] »).
        CombatEngine.Run(ScenarioJsonReader.ReadFile(FrozenScenarioPath(name), HistoricalData).Setup!, log);
        return log.Lines;
    }

    private static List<JsonObject> Lines(IEnumerable<string> log) => log.Skip(1).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Journaux_0_8_1_identiques_apres_suppression_des_champs_du_format_4(string name)
    {
        string[] before = Historical(name);
        IReadOnlyList<string> after = Current(name);
        Assert.Equal(before.Length, after.Count);

        JsonObject oldHeader = JsonNode.Parse(before[0])!.AsObject(), newHeader = JsonNode.Parse(after[0])!.AsObject();
        Assert.Equal((3, "0.8.1"), (oldHeader["formatVersion"]!.GetValue<int>(), oldHeader["engineVersion"]!.GetValue<string>()));
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version), (newHeader["formatVersion"]!.GetValue<int>(), newHeader["engineVersion"]!.GetValue<string>()));
        foreach (JsonObject header in new[] { oldHeader, newHeader })
            foreach (string key in new[] { "formatVersion", "engineVersion", "data" }) header.Remove(key);
        Assert.Equal(oldHeader.ToJsonString(), newHeader.ToJsonString());

        int gameplay = 0;
        for (int i = 1; i < before.Length; i++)
        {
            // Sous-lot 8.1 : champs ajoutés au format 5, comparés à part (Lot81ReferenceTests, Lot8LogFields).
            JsonObject oldLine = JsonNode.Parse(before[i])!.AsObject(), newLine = EdS.TestSupport.Lot8LogFields.RemoveAdded(JsonNode.Parse(after[i])!.AsObject());
            foreach (string key in CausalGraph.ProvenanceFields) Assert.False(oldLine.ContainsKey(key), $"format 3 sans « {key} »");
            string kind = newLine["event"]!.GetValue<string>();
            // Les diagnostics et l'enveloppe ne reçoivent aucun champ de provenance ; combatEnded reçoit deferredDropped (0 sans
            // déclencheur) ; chaque ligne de jeu reçoit eid et cause.
            if (kind == "combatEnded")
            {
                Assert.Equal(0, newLine["deferredDropped"]!.GetValue<long>());
                newLine.Remove("deferredDropped");
            }
            else if (DiagnosticKinds.Contains(kind)) Assert.Equal(EdS.TestSupport.Lot8LogFields.Comparable(before[i]), EdS.TestSupport.Lot8LogFields.Comparable(after[i]));
            else
            {
                gameplay++;
                Assert.True(newLine.ContainsKey("eid") && newLine.ContainsKey("cause"), after[i]);
                foreach (string key in CausalGraph.ProvenanceFields) newLine.Remove(key);
            }
            Assert.Equal(oldLine.ToJsonString(), newLine.ToJsonString());
        }
        Assert.True(gameplay > 0);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Graphe_causal_reconstruit_depuis_le_journal(string name)
    {
        IReadOnlyList<string> log = Current(name);
        CausalGraph graph = CausalGraph.Build(log);
        List<JsonObject> lines = Lines(log);
        Assert.Equal(lines.Count, graph.Nodes.Count + graph.Diagnostics);
        // Sans déclencheur, tout effet est de profondeur 0, et les eid sont consécutifs à partir de 1.
        Assert.All(graph.Nodes.Values, n => Assert.Equal(0, n.Depth));
        Assert.Equal(Enumerable.Range(1, graph.Nodes.Count).Select(i => (long)i), graph.Nodes.Keys.OrderBy(k => k));

        // Chaque racine suit le tableau du plan (§2.1) et chaque effet remonte à l'effet racine de sa racine.
        string[] prefixes = { "command:", "assailantTurn:", "cooldown:", "periodic:", "regeneration:" };
        Assert.All(graph.Roots.Keys, r => Assert.Contains(prefixes, p => r.StartsWith(p, StringComparison.Ordinal)));
        foreach (CausalGraph.Node node in graph.Nodes.Values)
        {
            CausalGraph.Node top = node.Cause is null ? node : graph.Ancestors(node.Eid).Last();
            Assert.Same(graph.Roots[node.Root], top);
        }
    }

    [Fact]
    public void Provenance_heritee_des_alterations_dans_le_scenario_livre()
    {
        List<JsonObject> lines = Lines(Current("entrainement_alterations"));
        JsonObject Line(long eid) => lines.Single(l => l["eid"]?.GetValue<long>() == eid);
        long Cause(JsonObject l) => l["cause"]!.GetValue<long>();
        IEnumerable<JsonObject> Kind(string kind) => lines.Where(l => l["event"]!.GetValue<string>() == kind);
        JsonObject Applied(long instance) => Kind("stateApplied").Single(l => l["instance"]!.GetValue<long>() == instance && l["generation"]!.GetValue<long>() == 1);

        // A1 : une application par une attaque a pour cause l'impact.
        foreach (JsonObject applied in Kind("stateApplied").Where(l => l["state"]!.GetValue<string>() != "stunImmunity"))
            Assert.Equal("damage", Line(Cause(applied))["event"]!.GetValue<string>());
        // A3 : chaque tick de DOT a pour cause l'application de son instance.
        Assert.NotEmpty(Kind("dotDamage"));
        foreach (JsonObject dot in Kind("dotDamage")) Assert.Equal(Applied(dot["instance"]!.GetValue<long>())["eid"]!.GetValue<long>(), Cause(dot));
        // A4 : chaque expiration a pour cause l'application de la génération courante (aucun renouvellement dans ce scénario).
        foreach (JsonObject expired in Kind("stateExpired")) Assert.Equal(Applied(expired["instance"]!.GetValue<long>())["eid"]!.GetValue<long>(), Cause(expired));
        // A5 : l'immunité accordée a pour cause l'expiration de l'étourdissement.
        JsonObject immunity = Kind("stateApplied").Single(l => l["state"]!.GetValue<string>() == "stunImmunity");
        JsonObject stunExpired = Line(Cause(immunity));
        Assert.Equal(("stateExpired", "stun"), (stunExpired["event"]!.GetValue<string>(), stunExpired["state"]!.GetValue<string>()));
        // La garde interrompue par l'étourdissement a pour cause son application.
        JsonObject interrupted = Kind("guardStopped").Single(l => l["reason"]?.GetValue<string>() == "interrupted");
        Assert.Equal("stun", Line(Cause(interrupted))["state"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_alterations")]
    public void Journal_au_format_3_refuse_au_rejeu_sans_migration(string name)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Historical(name), ShippedData);
        Assert.NotEqual(ReplayStatus.Identical, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("format de journal 3") && p.Contains("sans migration"));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Journal_au_format_4_rejoue_octet_par_octet(string name)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Current(name).ToArray(), HistoricalData);
        Assert.Equal(ReplayStatus.Identical, outcome.Status);
    }
}
