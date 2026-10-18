using System.Text.Json.Nodes;
using EdS.Domain.Combat;

namespace EdS.TestSupport;

/// <summary>
/// Champs ajoutés au journal par le lot 8, source unique des comparaisons avec les journaux figés des moteurs précédents
/// (Lot81ReferenceTests, Lot81EngineBaselineTests et les tests de migration plus anciens). Partagé par les deux projets de tests.
/// </summary>
public static class Lot8LogFields
{
    /// <summary>
    /// Champs ajoutés aux lignes, par type d'événement. Sous-lot 8.1 (format 5) : résistance aux altérations et règles effectives sur
    /// la ligne <c>combatant</c>, Charges consommées sur <c>guardBroken</c>. Sous-lot 8.2 (format 6) : absorption par les boucliers
    /// sur <c>damage</c> et <c>dotDamage</c>, dégâts absorbés sur <c>combatantSummary</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Added = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["combatant"] = new[] { "statusResistance", "rules" },
        ["guardBroken"] = new[] { "rampartChargesSpent" },
        ["damage"] = new[] { "absorbed", "absorbedBy" },
        ["dotDamage"] = new[] { "absorbed", "absorbedBy" },
        ["combatantSummary"] = new[] { "damageAbsorbed" },
    };

    /// <summary>Champs de l'en-tête qui changent légitimement : version du format, version du moteur et identité (empreintes) des données.</summary>
    public static readonly string[] HeaderVolatile = { "formatVersion", "engineVersion", "data" };

    /// <summary>Retire d'une ligne les champs ajoutés par le lot 8 ; renvoie la même ligne.</summary>
    public static JsonObject RemoveAdded(JsonObject line)
    {
        if (line["event"]?.GetValue<string>() is { } kind && Added.TryGetValue(kind, out string[]? added))
            foreach (string key in added) line.Remove(key);
        return line;
    }

    /// <summary>
    /// Forme comparable d'une ligne : inchangée (octet par octet) si son événement n'a reçu aucun champ au lot 8, sinon relue et
    /// réécrite sans ces champs. À appliquer aux deux journaux comparés.
    /// </summary>
    public static string Comparable(string line)
    {
        JsonObject o = JsonNode.Parse(line)!.AsObject();
        return o["event"]?.GetValue<string>() is { } kind && Added.ContainsKey(kind) ? RemoveAdded(o).ToJsonString() : line;
    }

    /// <summary>En-tête sans ses champs volatils.</summary>
    public static string StableHeader(string header)
    {
        JsonObject o = JsonNode.Parse(header)!.AsObject();
        foreach (string key in HeaderVolatile) o.Remove(key);
        return o.ToJsonString();
    }

    /// <summary>
    /// Objet <c>rules</c> attendu pour un personnage sans modificateur : chaque clé de règle scalaire à la valeur des règles de base,
    /// aucune clé paramétrée, aucun conditionnel. Écrit ici indépendamment du moteur, depuis <see cref="CombatRules"/>.
    /// </summary>
    public static string BaseRulesJson(CombatRules r) =>
        "{" + string.Join(",", new (string Key, string Value)[]
        {
            ("guard.damageReduction", r.GuardDamageReduction.ToString()), ("guard.upkeepPerSecond", r.GuardUpkeepPerSecond.ToString()),
            ("guard.costPerBlock", r.GuardCostPerBlock.ToString()), ("guard.noUpkeepBreak", "false"), ("guard.reductionAtZeroEndurance", "0"),
            ("guard.breakShortenPerCharge", "0"), ("parry.windowMs", r.ParryWindowMs.ToString()),
            ("parry.failureRecoveryMs", r.ParryFailureRecoveryMs.ToString()), ("rampartCharges.max", r.RampartChargesMax.ToString()),
            ("resonance.perDirectHit", r.ResonancePerDirectHit.ToString()), ("endurance.regenPerSecond", r.EnduranceRegenPerSecond.ToString()),
            ("damageDealt", "0"), ("damageTaken", "0"), ("shield.potencyDealt", "0"), ("conditional", "[]"),
        }.Select(p => $"\"{p.Key}\":{p.Value}")) + "}";

    /// <summary>
    /// Valeurs neutres des champs ajoutés sans modificateur : aucune Charge consommée par une rupture, et, sur la ligne d'un
    /// personnage, des règles effectives égales aux règles de base (<paramref name="baseRules"/>, objet <c>rules</c> attendu) ;
    /// <c>rules</c> nul pour un mannequin ou un assaillant. Sous-lot 8.2 : sans bouclier, rien d'absorbé. Renvoie un message d'écart, ou null.
    /// </summary>
    public static string? NeutralProblem(string line, Func<string, string?> baseRulesOf)
    {
        JsonObject o = JsonNode.Parse(line)!.AsObject();
        switch (o["event"]?.GetValue<string>())
        {
            case "guardBroken" when o["rampartChargesSpent"]?.GetValue<long>() != 0:
                return $"rampartChargesSpent non nul sans modificateur : {line}";
            case "damage" or "dotDamage" when o["absorbed"]?.GetValue<long>() != 0 || o["absorbedBy"]?.ToJsonString() != "[]":
                return $"absorption sans bouclier : {line}";
            case "combatantSummary" when o["damageAbsorbed"]?.GetValue<long>() != 0:
                return $"damageAbsorbed non nul sans bouclier : {line}";
            case "combatant":
                string type = o["type"]!.GetValue<string>();
                JsonNode? rules = o["rules"];
                if (type != "character") return rules is null ? null : $"rules non nul pour un {type} : {line}";
                string? expected = baseRulesOf(o["combatant"]!.GetValue<string>());
                return expected is null || rules?.ToJsonString() == expected ? null : $"rules {rules?.ToJsonString()} au lieu de {expected}";
            default:
                return null;
        }
    }
}
