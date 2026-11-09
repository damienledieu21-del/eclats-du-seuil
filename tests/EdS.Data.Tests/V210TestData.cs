using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.10 : l'histoire livrée sans ce qui demande la version 13 (fêtes) : la section <c>festivals</c>, le colporteur et son
/// dialogue, les fêtes des PNJ, les entrées, nœuds et répliques « v210 » et les conditions <c>festival</c> et <c>contestOpen</c> ; et les
/// marchands livrés sans ce qui demande la version 5 (stock vivant, étal ambulant, prix et contrecoups).
/// </summary>
internal static class V210TestData
{
    public static void WithoutFestivals(JsonObject root)
    {
        JsonObject data = root["data"]!.AsObject();
        data.Remove("festivals");
        JsonArray npcs = data["npcs"]!.AsArray();
        foreach (JsonNode? n in npcs.Where(n => (string?)n!["id"] == "npc.colporteur").ToList()) npcs.Remove(n);
        foreach (JsonNode? n in npcs) n!.AsObject().Remove("festivals");
        JsonArray dialogues = data["dialogues"]!.AsArray();
        foreach (JsonNode? d in dialogues.Where(d => (string?)d!["id"] == "dialogue.colporteur").ToList()) dialogues.Remove(d);
        foreach (JsonNode? d in dialogues)
        {
            JsonArray entries = d!["entries"]!.AsArray(), nodes = d["nodes"]!.AsArray();
            foreach (JsonNode? e in entries.Where(e => ((string?)e!["node"])!.StartsWith("v210_", StringComparison.Ordinal)).ToList()) entries.Remove(e);
            foreach (JsonNode? n in nodes.Where(n => ((string?)n!["id"])!.StartsWith("v210_", StringComparison.Ordinal)).ToList()) nodes.Remove(n);
            foreach (JsonNode? n in nodes)
                if (n!["lines"] is JsonArray lines)
                {
                    foreach (JsonNode? l in lines.Where(l => ((string?)l!["text"])!.StartsWith("text.v210.", StringComparison.Ordinal)).ToList()) lines.Remove(l);
                    if (lines.Count == 0) n.AsObject().Remove("lines");
                }
        }
    }

    public static void MerchantsWithoutLiving(JsonObject root)
    {
        V214TestData.MerchantsWithoutShops(root);   // sous-lot V2.14 : d'abord ce qui demande la version 6
        JsonObject data = root["data"]!.AsObject();
        data.Remove("prices");
        data.Remove("shocks");
        JsonArray merchants = data["merchants"]!.AsArray();
        foreach (JsonNode? m in merchants.Where(m => (string?)m!["id"] == "merchant.colporteur").ToList()) merchants.Remove(m);
        foreach (JsonNode? m in merchants)
        {
            foreach (string field in new[] { "restock", "resale", "festivals" }) m!.AsObject().Remove(field);
            foreach (JsonNode? o in m!["offers"]!.AsArray()) o!.AsObject().Remove("stock");
        }
    }
}
