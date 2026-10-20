using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EdS.Data.Json.Text;
using EdS.Data.Triggers;
using EdS.Data.Validation;
using EdS.Domain.Numerics;

namespace EdS.Data.Json
{
    /// <summary>
    /// Lecture des déclencheurs (lot 7.3), même forme dans les classes, les règles et les scénarios (forme seulement). Sous-lot 11.4b :
    /// la portée d'un effet (<c>effects[].range</c>) n'est lue que dans les classes (schéma 9) ; ailleurs, elle reste un champ inconnu.
    /// </summary>
    internal static class TriggerJsonReader
    {
        public static List<TriggerData?>? ReadList(JsonInput input, JsonElement element, string path, bool allowRange = false) =>
            input.ReadList(element, path, (e, ip) => Read(input, e, ip, allowRange));

        private static TriggerData? Read(JsonInput input, JsonElement element, string path, bool allowRange)
        {
            var t = new TriggerData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["on"] = (v, p) => t.On = input.Text(v, p),
                ["role"] = (v, p) => t.Role = input.Text(v, p),
                ["filter"] = (v, p) => t.Filter = ReadFilter(input, v, p),
                ["cooldownMs"] = (v, p) => t.CooldownMs = input.Int64(v, p),
                ["effects"] = (v, p) => t.Effects = input.ReadList(v, p, (e, ip) => ReadEffect(input, e, ip, allowRange)),
            });
            return ok ? t : null;
        }

        private static TriggerFilterData? ReadFilter(JsonInput input, JsonElement element, string path)
        {
            var f = new TriggerFilterData();
            List<string?>? Names(JsonElement v, string p) => input.ReadList(v, p, (e, ip) => input.Text(e, ip));
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["skills"] = (v, p) => f.Skills = Names(v, p),
                ["damageType"] = (v, p) => f.DamageType = input.Text(v, p),
                ["defense"] = (v, p) => f.Defense = Names(v, p),
                ["status"] = (v, p) => f.Status = input.Text(v, p),
                ["category"] = (v, p) => f.Category = input.Text(v, p),
                ["resource"] = (v, p) => f.Resource = input.Text(v, p),
                ["renewed"] = (v, p) => f.Renewed = input.Boolean(v, p),
            });
            return ok ? f : null;
        }

        private static TriggerEffectData? ReadEffect(JsonInput input, JsonElement element, string path, bool allowRange)
        {
            var e = new TriggerEffectData();
            var fields = new Dictionary<string, Action<JsonElement, string>>
            {
                ["type"] = (v, p) => e.Type = input.Text(v, p),
                ["target"] = (v, p) => e.Target = input.Text(v, p),
                ["coefficient"] = (v, p) => e.Coefficient = ReadRanked(input, v, p),
                ["powerType"] = (v, p) => e.PowerType = input.Text(v, p),
                ["damageType"] = (v, p) => e.DamageType = input.Text(v, p),
                ["unblockable"] = (v, p) => e.Unblockable = input.Boolean(v, p),
                ["status"] = (v, p) => e.Status = input.Text(v, p),
                ["resource"] = (v, p) => e.Resource = input.Text(v, p),
                ["amount"] = (v, p) => e.Amount = ReadRanked(input, v, p),
            };
            if (allowRange) fields["range"] = (v, p) => e.Range = input.Decimal(v, p);
            bool ok = input.ReadFields(element, path, fields);
            return ok ? e : null;
        }

        /// <summary>Un nombre décimal, ou <c>{ "base": x, "perRank": y }</c> (aussi la valeur <c>add</c> d'un modificateur, sous-lot 8.1).</summary>
        internal static RankedData? ReadRanked(JsonInput input, JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return input.TryReadDecimal(element, path, out Fixed value) ? new RankedData { Base = value } : null;
            var r = new RankedData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["base"] = (v, p) => r.Base = input.Decimal(v, p),
                ["perRank"] = (v, p) => r.PerRank = input.Decimal(v, p),
            });
            return ok ? r : null;
        }
    }
}
