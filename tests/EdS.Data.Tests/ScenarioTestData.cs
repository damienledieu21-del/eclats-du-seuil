using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;

namespace EdS.Data.Tests;

/// <summary>Données livrées (data/v0), scénario livré (scenarios/mannequin.json) et mutations pour les tests.</summary>
internal static class ScenarioTestData
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data", "v0");
    public static string MannequinPath => Path.Combine(AppContext.BaseDirectory, "scenarios", "mannequin.json");
    public static string MannequinJson => File.ReadAllText(MannequinPath);
    public static string ScenarioPath(string name) => Path.Combine(AppContext.BaseDirectory, "scenarios", name + ".json");

    public static GameData ShippedData { get; } = GameDataLoader.Load(DataDirectory).Data!;

    public static JsonObject Shipped() => JsonNode.Parse(MannequinJson)!.AsObject();
    public static JsonObject DataOf(JsonObject root) => root["data"]!.AsObject();
    public static JsonObject Combatant(JsonObject root, int index) => DataOf(root)["combatants"]![index]!.AsObject();
    public static JsonObject Command(JsonObject root, int index) => DataOf(root)["commands"]![index]!.AsObject();

    public static ScenarioLoadResult ReadMutated(Action<JsonObject> mutate, GameData? data = null, string scenario = "mannequin")
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath(scenario)))!.AsObject();
        mutate(root);
        return ScenarioJsonReader.Read(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), data ?? ShippedData);
    }

    /// <summary>Copie de data/v0 dans un dossier temporaire, avec une modification, pour simuler des données changées.</summary>
    public static string CopyData(Func<string, string, string> transform)
    {
        string target = Path.Combine(Path.GetTempPath(), "eds-donnees-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(target, "classes"));
        foreach (string file in Directory.GetFiles(DataDirectory, "*.json", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(DataDirectory, file);
            File.WriteAllText(Path.Combine(target, relative), transform(relative.Replace('\\', '/'), File.ReadAllText(file)));
        }
        return target;
    }
}
