using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.14 : les marchands livrés sans ce qui demande la version 6 : la section <c>market</c>, les boutiques (<c>shop</c>), les
/// fournées (<c>bakes</c>, <c>soldOut</c>), les offres fraîches et à la porte, les règles de prix du jour de marché et les marchands du marché
/// et des boutiques ajoutés en V2.14 (<see cref="Added"/>).
/// </summary>
internal static class V214TestData
{
    /// <summary>Marchands ajoutés en V2.14 (boutiques et étals du marché).</summary>
    public static readonly string[] Added = Array.Empty<string>();

    public static void MerchantsWithoutShops(JsonObject root)
    {
        JsonObject data = root["data"]!.AsObject();
        data.Remove("market");
        if (data["prices"] is JsonArray prices)
            foreach (JsonNode? rule in prices.Where(r => r!["market"] != null).ToList()) prices.Remove(rule);
        JsonArray merchants = data["merchants"]!.AsArray();
        foreach (JsonNode? m in merchants.Where(m => Added.Contains((string?)m!["id"])).ToList())
            merchants.Remove(m);
        foreach (JsonNode? m in merchants)
        {
            foreach (string field in new[] { "shop", "bakes", "soldOut" }) m!.AsObject().Remove(field);
            foreach (JsonNode? o in m!["offers"]!.AsArray())
            {
                o!.AsObject().Remove("fresh");
                o.AsObject().Remove("door");
            }
        }
        // Les marchands des autres sections nommés par une règle ou un contrecoup, s'ils viennent d'être retirés.
        var ids = merchants.Select(m => (string)m!["id"]!).ToHashSet();
        foreach (string section in new[] { "prices", "shocks" })
            if (data[section] is JsonArray rules)
                foreach (JsonNode? r in rules)
                    if (r!["merchants"] is JsonArray named)
                        foreach (JsonNode? n in named.Where(n => !ids.Contains((string)n!)).ToList()) named.Remove(n);
    }
}
