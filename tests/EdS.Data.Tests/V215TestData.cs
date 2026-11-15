using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Concours de pâtisserie : l'histoire livrée sans ce qui demande la version 15 (conditions <c>hasSigned</c>, effet <c>present</c>) : les
/// entrées et nœuds « v215 » du concours de Firmin.
/// </summary>
internal static class V215TestData
{
    public static void WithoutPastryContest(JsonObject root)
    {
        foreach (JsonNode? d in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = d!["entries"]!.AsArray(), nodes = d["nodes"]!.AsArray();
            foreach (JsonNode? e in entries.Where(e => ((string?)e!["node"])!.StartsWith("v215_", StringComparison.Ordinal)).ToList()) entries.Remove(e);
            foreach (JsonNode? n in nodes.Where(n => ((string?)n!["id"])!.StartsWith("v215_", StringComparison.Ordinal)).ToList()) nodes.Remove(n);
        }
    }
}
