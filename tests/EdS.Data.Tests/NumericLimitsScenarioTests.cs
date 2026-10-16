using System.Text.Json.Nodes;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctifs 6.1 M03 et M04 vus depuis les fichiers : une valeur extrême dans un scénario (dégâts, défenses, rangs,
/// niveau, attributs) produit un diagnostic structuré rattaché à son chemin, jamais une exception.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class NumericLimitsScenarioTests
{
    private static ScenarioLoadResult Mutate(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: "entrainement_garde_parade");

    private static void AssertInvalid(ScenarioLoadResult result, string pathPrefix)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path.StartsWith(pathPrefix, StringComparison.Ordinal));
    }

    private static JsonObject Build(JsonObject root) => Combatant(root, 0)["build"]!.AsObject();

    [Theory]
    [InlineData("1000000001")]
    [InlineData("922337203685478")]          // au-delà de Fixed.FromInt
    [InlineData("9223372036854775807")]      // long.MaxValue
    [InlineData("9223372036854775808")]      // hors de long
    [InlineData("-9223372036854775808")]
    [InlineData("1e30")]
    public void Degats_bruts_extremes(string raw) =>
        AssertInvalid(Mutate(r => Combatant(r, 1)["attacks"]![0]!["rawDamage"] = JsonNode.Parse(raw)), "$.data.combatants[1].attacks[0].rawDamage");

    [Theory]
    [InlineData("1000000000.0001")]
    [InlineData("922337203685477.5807")]
    [InlineData("-0.0001")]
    [InlineData("1e300")]
    public void Defenses_extremes(string armor) =>
        AssertInvalid(Mutate(r => Combatant(r, 1)["armor"] = JsonNode.Parse(armor)), "$.data.combatants[1].armor");

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    public void Rangs_extremes(int rank) =>
        AssertInvalid(Mutate(r => Build(r)["talents"]!["talent.bastion.rempart.garde_stable"] = rank), "$.data.combatants[0].build");

    [Fact]
    public void Somme_de_rangs_au_dela_de_32_bits() =>
        AssertInvalid(Mutate(r =>
        {
            var talents = Build(r)["talents"]!.AsObject();
            foreach (string id in talents.Select(t => t.Key).ToList()) talents[id] = int.MaxValue;
        }), "$.data.combatants[0].build");

    [Theory]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    [InlineData("2147483648")]
    public void Niveau_extreme(string level) =>
        AssertInvalid(Mutate(r => Build(r)["level"] = JsonNode.Parse(level)), "$.data.combatants[0].build");

    [Theory]
    [InlineData("2147483647")]
    [InlineData("1000001")]
    public void Attribut_extreme(string value) =>
        AssertInvalid(Mutate(r => Build(r)["attributes"]!["vitalite"] = JsonNode.Parse(value)), "$.data.combatants[0].build");
}
