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
/// lot 8 (<see cref="Lot8LogFields.Added"/>), l'égalité doit être exacte, ligne par ligne, ordre des champs et numéros de séquence compris.
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

    private static string[] Historical(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.9.1", name + ".jsonl"));

    private static IReadOnlyList<string> Current(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!, log);
        return log.Lines;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Journaux_0_9_1_identiques_au_gameplay_pres_des_champs_ajoutes(string name)
    {
        string[] before = Historical(name);
        IReadOnlyList<string> after = Current(name);
        Assert.Equal(before.Length, after.Count);
        JsonObject oldHeader = JsonNode.Parse(before[0])!.AsObject(), newHeader = JsonNode.Parse(after[0])!.AsObject();
        Assert.Equal((4, "0.9.1"), (oldHeader["formatVersion"]!.GetValue<int>(), oldHeader["engineVersion"]!.GetValue<string>()));
        Assert.Equal((5, "0.10.0"), (newHeader["formatVersion"]!.GetValue<int>(), newHeader["engineVersion"]!.GetValue<string>()));
        Assert.Equal(Lot8LogFields.StableHeader(before[0]), Lot8LogFields.StableHeader(after[0]));
        string baseRules = Lot8LogFields.BaseRulesJson(ShippedData.Rules.Combat);
        for (int i = 1; i < before.Length; i++)
        {
            foreach (string[] added in Lot8LogFields.Added.Values)
                foreach (string key in added)
                    Assert.False(JsonNode.Parse(before[i])!.AsObject().ContainsKey(key), $"journal 0.9.1 sans « {key} »");
            // Données livrées sans modificateur : valeurs neutres des champs ajoutés, puis égalité exacte sans eux.
            Assert.Null(Lot8LogFields.NeutralProblem(after[i], _ => baseRules));
            Assert.Equal(Lot8LogFields.Comparable(before[i]), Lot8LogFields.Comparable(after[i]));
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
