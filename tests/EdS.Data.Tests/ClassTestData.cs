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
}
