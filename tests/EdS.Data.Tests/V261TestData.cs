using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.6.1 : l'histoire livrée sans ce qui demande la version 11 (quête « Les cicatrices du Seuil », répliques « v261_… » et leurs
/// nœuds, veille du soir de Sœur Albe) : elle tient alors dans les 16 répliques des versions 10 et antérieures, pour les tests qui relisent
/// l'histoire livrée dans une ancienne version du format.
/// </summary>
internal static class V261TestData
{
    public const string Quest = "quest.cicatrices_du_seuil";

    public static void WithoutSuites(JsonObject root)
    {
        V27TestData.WithoutReputation(root);   // sous-lot V2.7 : la réputation demande la version 12
        JsonObject data = root["data"]!.AsObject();
        JsonArray quests = data["quests"]!.AsArray();
        foreach (JsonNode? q in quests.Where(q => (string?)q!["id"] == Quest).ToList()) quests.Remove(q);
        foreach (JsonNode? d in data["dialogues"]!.AsArray())
        {
            JsonArray entries = d!["entries"]!.AsArray(), nodes = d["nodes"]!.AsArray();
            // Sous-lot V2.9.2 : sans les deux répliques de Gaspard sur le vieux Brunot (32 répliques au plus ne vient qu'avec la version 11).
            foreach (JsonNode? e in entries.Where(e => ((string?)e!["node"])!.StartsWith("v261_", StringComparison.Ordinal)
                || ((string?)e!["node"])!.StartsWith("v292_", StringComparison.Ordinal)).ToList()) entries.Remove(e);
            foreach (JsonNode? n in nodes.Where(n => ((string?)n!["id"])!.StartsWith("v261_", StringComparison.Ordinal)
                || ((string?)n!["id"])!.StartsWith("v292_", StringComparison.Ordinal)).ToList()) nodes.Remove(n);
            foreach (JsonNode? n in nodes)
                if (n!["lines"] is JsonArray lines)
                {
                    foreach (JsonNode? l in lines.Where(l => ((string?)l!["text"])!.StartsWith("text.v261.", StringComparison.Ordinal)).ToList()) lines.Remove(l);
                    if (lines.Count == 0) n.AsObject().Remove("lines");
                }
        }
        JsonNode albe = data["npcs"]!.AsArray().Single(n => (string?)n!["id"] == "npc.albe")!;
        if (albe["schedule"] is JsonArray slots)
            foreach (JsonNode? s in slots.Where(s => (string?)s!["at"] == "17:00" || (string?)s!["at"] == "18:00").ToList()) slots.Remove(s);
    }
}
