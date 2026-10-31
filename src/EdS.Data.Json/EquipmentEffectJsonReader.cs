using System;
using System.Collections.Generic;
using EdS.Data.Equipment;
using EdS.Data.Json.Text;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot V1.3 : lecture d'un effet d'équipement (forme seulement), dans un scénario ou un journal (<c>id</c>, <c>modifiers</c>,
    /// <c>triggers</c>) et dans le catalogue des objets (<c>text</c>, <c>modifiers</c>, <c>triggers</c> ; la source est l'objet ou le palier
    /// d'ensemble). Les déclencheurs admettent la portée d'un effet, comme ceux des classes.
    /// </summary>
    internal static class EquipmentEffectJsonReader
    {
        public static EquipmentEffectData? Read(JsonInput input, JsonElement element, string path, bool withId, bool withText = true)
        {
            var e = new EquipmentEffectData();
            var fields = new Dictionary<string, Action<JsonElement, string>>
            {
                ["modifiers"] = (v, p) => e.Modifiers = ModifierJsonReader.ReadList(input, v, p),
                ["triggers"] = (v, p) => e.Triggers = TriggerJsonReader.ReadList(input, v, p, allowRange: true),
            };
            if (withId) fields["id"] = (v, p) => e.Id = input.Text(v, p);
            else if (withText) fields["text"] = (v, p) => e.Text = input.Text(v, p);
            bool ok = input.ReadFields(element, path, fields);
            return ok ? e : null;
        }
    }
}
