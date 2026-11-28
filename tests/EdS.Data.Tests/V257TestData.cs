using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// V2.57 : l'histoire livrée sans ce qui vient des quêtes et dialogues des Vallons (quêtes « quest.v257 », entrées, nœuds, réponses et
/// répliques « v257 », conditions <c>season</c>, <c>corruptionAtLeast</c> et <c>corruptionBelow</c> de la version 17) : pour les essais
/// des versions plus anciennes, et ceux des données réduites (sans les dessous ni l'Envers).
/// </summary>
internal static class V257TestData
{
    private static bool New(JsonNode? x)
    {
        string json = x!.ToJsonString();
        return json.Contains("v257", StringComparison.Ordinal) || json.Contains("\"if\":\"season\"", StringComparison.Ordinal)
            || json.Contains("\"if\":\"corruptionAtLeast\"", StringComparison.Ordinal) || json.Contains("\"if\":\"corruptionBelow\"", StringComparison.Ordinal);
    }

    private static void RemoveAll(JsonArray a, Func<JsonNode?, bool> f)
    {
        foreach (JsonNode? x in a.Where(f).ToList()) a.Remove(x);
    }

    public static void WithoutQuetes(JsonNode root) => WithoutQuetesIn(root["data"]!);

    public static void WithoutQuetesIn(JsonNode data)
    {
        RemoveAll(data["quests"]!.AsArray(), q => ((string)q!["id"]!).StartsWith("quest.v257.", StringComparison.Ordinal));
        RemoveAll(data["discoveries"]!.AsArray(), x => ((string)x!["id"]!).StartsWith("discovery.v257.", StringComparison.Ordinal));
        foreach (JsonNode? dialogue in data["dialogues"]!.AsArray())
        {
            RemoveAll(dialogue!["entries"]!.AsArray(), New);
            RemoveAll(dialogue["nodes"]!.AsArray(), n => ((string)n!["id"]!).StartsWith("v257_", StringComparison.Ordinal));
            foreach (JsonNode? n in dialogue["nodes"]!.AsArray())
            {
                if (n!["lines"] is JsonArray lines)
                {
                    RemoveAll(lines, New);
                    if (lines.Count == 0) n.AsObject().Remove("lines");
                }
                RemoveAll(n["choices"]!.AsArray(), New);
            }
        }
    }
}
