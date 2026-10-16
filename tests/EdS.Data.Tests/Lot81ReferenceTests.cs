using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.1, étape 1 : préservation des références. Les journaux des cinq scénarios livrés produits par le moteur 0.9.1
/// (format 4, figés dans Fixtures/logs-0.9.1 au commit 5a9a032, avant toute modification du lot 8) sont comparés aux journaux
/// actuels : une fois retirés de l'en-tête la version du moteur et l'identité des données, et des lignes les champs ajoutés par le
/// lot 8 (<see cref="AddedFields"/>), l'égalité doit être exacte, ligne par ligne, ordre des champs et numéros de séquence compris.
/// Le gameplay des scénarios historiques ne change donc pas. Les assertions métier de ces scénarios restent dans leurs propres
/// tests (TrainingScenarioTests, Correctif731TriggerScenarioTests), indépendantes des empreintes.
/// </summary>
[Trait("Category", "Regression")]
public class Lot81ReferenceTests
{
    public static readonly TheoryData<string> Scenarios = new()
    {
        "mannequin", "entrainement_garde_parade", "entrainement_rupture", "entrainement_alterations", "entrainement_declencheurs",
    };

    /// <summary>Champs ajoutés aux lignes par le lot 8, par type d'événement (vide tant qu'aucun n'est ajouté).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> AddedFields = new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>Champs de l'en-tête qui changent légitimement : version du moteur et identité (empreintes) des données.</summary>
    private static readonly string[] HeaderVolatile = { "engineVersion", "data" };

    private static string[] Historical(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.9.1", name + ".jsonl"));

    private static IReadOnlyList<string> Current(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!, log);
        return log.Lines;
    }

    private static string Strip(string line, bool header)
    {
        JsonObject o = JsonNode.Parse(line)!.AsObject();
        if (header)
        {
            foreach (string key in HeaderVolatile) o.Remove(key);
            return o.ToJsonString();
        }
        if (AddedFields.TryGetValue(o["event"]!.GetValue<string>(), out string[]? added))
            foreach (string key in added) o.Remove(key);
        return o.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Journaux_0_9_1_identiques_au_gameplay_pres_des_champs_ajoutes(string name)
    {
        string[] before = Historical(name);
        IReadOnlyList<string> after = Current(name);
        Assert.Equal(before.Length, after.Count);
        JsonObject oldHeader = JsonNode.Parse(before[0])!.AsObject();
        Assert.Equal((4, "0.9.1"), (oldHeader["formatVersion"]!.GetValue<int>(), oldHeader["engineVersion"]!.GetValue<string>()));
        Assert.Equal(Strip(before[0], header: true), Strip(after[0], header: true));
        for (int i = 1; i < before.Length; i++)
        {
            foreach (string[] added in AddedFields.Values)
                foreach (string key in added)
                    Assert.False(JsonNode.Parse(before[i])!.AsObject().ContainsKey(key), $"journal 0.9.1 sans « {key} »");
            Assert.Equal(Strip(before[i], header: false), Strip(after[i], header: false));
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Graphe_causal_des_journaux_figes(string name)
    {
        CausalGraph graph = CausalGraph.Build(Historical(name));
        Assert.All(graph.Nodes.Values, n => Assert.InRange(n.Depth, 0, 4));
    }
}
