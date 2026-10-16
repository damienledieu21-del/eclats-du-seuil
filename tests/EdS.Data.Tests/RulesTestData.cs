using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;

namespace EdS.Data.Tests;

/// <summary>Accès au rules.json livré et petites mutations pour les tests.</summary>
internal static class RulesTestData
{
    public static string ShippedPath => Path.Combine(AppContext.BaseDirectory, "data", "v0", "rules.json");

    public static string ShippedJson => File.ReadAllText(ShippedPath);

    public static JsonObject Shipped() => JsonNode.Parse(ShippedJson)!.AsObject();

    /// <summary>Applique une modification au fichier livré puis le relit.</summary>
    public static RulesLoadResult ReadMutated(Action<JsonObject> mutate)
    {
        JsonObject root = Shipped();
        mutate(root);
        return RulesJsonReader.Read(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Renvoie l'objet parent d'un chemin sous data et le nom de la feuille.</summary>
    public static (JsonObject Parent, string Leaf) Locate(JsonObject root, string dataPath)
    {
        string[] segments = dataPath.Split('.');
        JsonObject node = root["data"]!.AsObject();
        foreach (string segment in segments[..^1]) node = node[segment]!.AsObject();
        return (node, segments[^1]);
    }

    public static void Set(JsonObject root, string dataPath, JsonNode? value)
    {
        var (parent, leaf) = Locate(root, dataPath);
        parent[leaf] = value;
    }

    public static void Remove(JsonObject root, string dataPath)
    {
        var (parent, leaf) = Locate(root, dataPath);
        parent.Remove(leaf);
    }
}
