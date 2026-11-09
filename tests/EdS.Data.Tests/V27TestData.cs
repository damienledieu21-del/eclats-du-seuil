using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.7 : l'histoire livrée sans ce qui demande la version 12 (réputation) : les quêtes, nœuds, entrées, répliques et réponses
/// « v27 », les effets <c>reputation</c> et la section <c>reputation</c>.
/// </summary>
internal static class V27TestData
{
    public static void WithoutReputation(JsonObject root)
    {
        V210TestData.WithoutFestivals(root);   // sous-lot V2.10 : les fêtes demandent la version 13
        JsonObject data = root["data"]!.AsObject();
        data.Remove("reputation");
        foreach (JsonNode? n in data["npcs"]!.AsArray()) n!.AsObject().Remove("faction");
        JsonArray quests = data["quests"]!.AsArray();
        foreach (JsonNode? q in quests.Where(q => ((string?)q!["id"])!.StartsWith("quest.v27.", StringComparison.Ordinal)).ToList()) quests.Remove(q);
        foreach (JsonNode? d in data["dialogues"]!.AsArray())
        {
            JsonArray entries = d!["entries"]!.AsArray(), nodes = d["nodes"]!.AsArray();
            foreach (JsonNode? e in entries.Where(e => ((string?)e!["node"])!.StartsWith("v27_", StringComparison.Ordinal)).ToList()) entries.Remove(e);
            foreach (JsonNode? n in nodes.Where(n => ((string?)n!["id"])!.StartsWith("v27_", StringComparison.Ordinal)).ToList()) nodes.Remove(n);
            foreach (JsonNode? n in nodes)
            {
                if (n!["lines"] is JsonArray lines)
                {
                    foreach (JsonNode? l in lines.Where(l => ((string?)l!["text"])!.StartsWith("text.v27.", StringComparison.Ordinal)).ToList()) lines.Remove(l);
                    if (lines.Count == 0) n.AsObject().Remove("lines");
                }
                JsonArray choices = n["choices"]!.AsArray();
                foreach (JsonNode? c in choices.Where(c => ((string?)c!["text"])!.StartsWith("text.v27.", StringComparison.Ordinal)).ToList()) choices.Remove(c);
                foreach (JsonNode? c in choices)
                {
                    JsonArray effects = c!["effects"]!.AsArray();
                    foreach (JsonNode? e in effects.Where(e => (string?)e!["do"] == "reputation").ToList()) effects.Remove(e);
                }
            }
        }
    }
}
