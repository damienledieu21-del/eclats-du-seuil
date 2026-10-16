using System.Text.Json.Nodes;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.3.6 (C12, Q11) : <c>validate</c> et <c>simulate</c> indiquent dans leur rapport si le majorant de la file est garanti. Sans
/// déclencheur, il l'est ; avec un déclencheur (ici une riposte d'assaillant de scénario), il ne l'est plus.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Trigger")]
public class Lot73CliTests
{
    private static JsonNode Riposte() => JsonNode.Parse("""
        { "on": "onHitTaken", "effects": [ { "type": "strike", "target": "eventSource", "coefficient": 0.5, "powerType": "physique", "damageType": "physique" } ] }
        """)!;

    private static (string Validate, string Simulate) Reports(Action<JsonObject> mutate) =>
        Lot71Files.InTempDirectory(directory =>
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("entrainement_garde_parade")))!.AsObject();
            mutate(root);
            string path = Path.Combine(directory, "scenario.json");
            File.WriteAllText(path, Lot71Files.ToJson(root));
            var validate = Lot71Files.Cli(AppContext.BaseDirectory, "validate", path);
            Assert.True(validate.ExitCode == 0, validate.Error);
            var simulate = Lot71Files.Cli(directory, "simulate", path, "--data", DataDirectory, "--out", Path.Combine(directory, "journal.jsonl"));
            Assert.True(simulate.ExitCode == 0, simulate.Error);
            return (validate.Output, simulate.Output);
        });

    [Fact]
    public void Majorant_garanti_sans_declencheur()
    {
        var (validate, simulate) = Reports(_ => { });
        foreach (string report in new[] { validate, simulate })
        {
            Assert.Matches(@"file           majorant prudent \d+/100000, garanti", report);
            Assert.DoesNotContain("NON garanti", report);
        }
    }

    [Fact]
    public void Majorant_non_garanti_avec_declencheur()
    {
        var (validate, simulate) = Reports(root => Combatant(root, 1)["triggers"] = new JsonArray(Riposte()));
        foreach (string report in new[] { validate, simulate })
            Assert.Matches(@"file           majorant prudent \d+/100000 dont 20000 transitoires, NON garanti : les altérations posées par des déclencheurs ne sont pas majorées", report);
    }
}
