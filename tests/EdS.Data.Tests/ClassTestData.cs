using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Domain.Builds;

namespace EdS.Data.Tests;

/// <summary>Accès au bastion.json livré, aux règles de progression livrées et petites mutations pour les tests.</summary>
internal static class ClassTestData
{
    public static string BastionPath => Path.Combine(AppContext.BaseDirectory, "data", "v0", "classes", "bastion.json");

    public static string BastionJson => File.ReadAllText(BastionPath);

    public static ProgressionRules Progression { get; } = RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Progression;

    public static ClassDefinition Bastion { get; } = ClassJsonReader.ReadFile(BastionPath, Progression).Class!.Definition;

    public static JsonObject Shipped() => JsonNode.Parse(BastionJson)!.AsObject();

    public static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();

    public static JsonObject Talent(JsonObject root, int tree, int talent) =>
        DataOf(root)["trees"]![tree]!["talents"]![talent]!.AsObject();

    public static JsonObject Skill(JsonObject root, int skill) => DataOf(root)["skills"]![skill]!.AsObject();

    public static string ToJson(JsonObject root) => root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public static ClassLoadResult ReadMutated(Action<JsonObject> mutate)
    {
        JsonObject root = Shipped();
        mutate(root);
        return ClassJsonReader.Read(ToJson(root), Progression);
    }

    /// <summary>
    /// Sous-lot 8.5 : classe figée du sous-lot 8.4 (<see cref="ScenarioTestData.FrozenClassPath"/>), sans déclencheur ni modificateur,
    /// base des tests de mutation écrits avant le contenu du 8.5 (porteurs « [TEST] » à leurs indices d'origine).
    /// </summary>
    public static string FrozenJson => File.ReadAllText(ScenarioTestData.FrozenClassPath);

    public static JsonObject Frozen() => JsonNode.Parse(FrozenJson)!.AsObject();

    public static ClassDefinition FrozenBastion { get; } = ClassJsonReader.ReadFile(ScenarioTestData.FrozenClassPath, Progression).Class!.Definition;

    public static ClassLoadResult ReadFrozenMutated(Action<JsonObject> mutate)
    {
        JsonObject root = Frozen();
        mutate(root);
        return ClassJsonReader.Read(ToJson(root), Progression);
    }
}
