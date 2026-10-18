using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EdS.Data.Json.Text;
using EdS.Data.Modifiers;

namespace EdS.Data.Json
{
    /// <summary>Lecture des modificateurs (sous-lot 8.1) d'un talent ou d'une compétence passive (forme seulement).</summary>
    internal static class ModifierJsonReader
    {
        public static List<ModifierData?>? ReadList(JsonInput input, JsonElement element, string path) =>
            input.ReadList(element, path, (e, ip) => Read(input, e, ip));

        private static ModifierData? Read(JsonInput input, JsonElement element, string path)
        {
            var m = new ModifierData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["key"] = (v, p) => m.Key = input.Text(v, p),
                ["add"] = (v, p) => m.Add = TriggerJsonReader.ReadRanked(input, v, p),
                ["conditions"] = (v, p) => m.Conditions = input.ReadList(v, p, (e, ip) => input.Text(e, ip)),
            });
            return ok ? m : null;
        }
    }
}
