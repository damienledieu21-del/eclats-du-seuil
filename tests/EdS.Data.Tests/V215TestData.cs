using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Concours de pâtisserie : l'histoire livrée sans ce qui demande la version 15 (conditions <c>hasSigned</c>, effet <c>present</c>) : les
/// entrées et nœuds « v215 » du concours de Firmin, et « v231 » (concours des Semailles, décision de Damien du 16 novembre 2026).
/// </summary>
internal static class V215TestData
{
    private static bool Pastry(string? id) => id != null && (id.StartsWith("v215_", StringComparison.Ordinal) || id.StartsWith("v231_", StringComparison.Ordinal));

    public static void WithoutPastryContest(JsonObject root)
    {
        V257TestData.WithoutQuetes(root);   // V2.57 : d'abord ce qui demande la version 17
        foreach (JsonNode? d in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = d!["entries"]!.AsArray(), nodes = d["nodes"]!.AsArray();
            foreach (JsonNode? e in entries.Where(e => Pastry((string?)e!["node"])).ToList()) entries.Remove(e);
            foreach (JsonNode? n in nodes.Where(n => Pastry((string?)n!["id"])).ToList()) nodes.Remove(n);
        }
    }
}
