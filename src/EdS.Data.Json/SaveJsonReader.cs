using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Json.Text;
using EdS.Data.Saves;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.1 : lecture stricte et écriture canonique des sauvegardes du monde (<c>kind: "save"</c>). La lecture ne vérifie que la
    /// forme ; le reste est délégué au validateur unique <see cref="SaveValidator"/>. L'écriture donne toujours le même texte pour la même
    /// sauvegarde (une ligne par champ, événements dans l'ordre ordinal), relu à l'identique.
    /// </summary>
    public static class SaveJsonReader
    {
        /// <summary>Version des valeurs d'une sauvegarde écrite par ce programme.</summary>
        public const string ContentVersion = "0.1.0-provisoire";

        /// <summary>Taille maximale d'un fichier de sauvegarde lu (enveloppe de la tranche : 100 000 événements de 200 caractères au plus).</summary>
        public const long MaxBytes = 32L * 1024 * 1024;

        public static SaveLoadResult ReadFile(string path)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report, MaxBytes), report);
        }

        public static SaveLoadResult Read(string json)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report, MaxBytes), report);
        }

        private static SaveLoadResult Read(byte[]? json, ValidationReport report)
        {
            var document = new SaveDocument();
            if (json is null) return SaveValidator.Validate(document, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"), MaxBytes);
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return SaveValidator.Validate(document, report);
        }

        internal static SaveData? ReadData(JsonInput input, JsonElement element, string path)   // interne : tests en objets publics
        {
            var data = new SaveData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["zone"] = (v, p) => data.Zone = input.Text(v, p),
                ["worldTick"] = (v, p) => data.WorldTick = input.Int64(v, p),
                ["sessions"] = (v, p) => data.Sessions = input.Int64(v, p),
                ["consumed"] = (v, p) => data.Consumed = input.ReadLimitedList(v, p, SaveValidator.MaxConsumed,
                    n => $"{n} événements consommés, au plus {SaveValidator.MaxConsumed}.", (e, ip) => input.Text(e, ip)),
                ["character"] = (v, p) =>   // sous-lot 12.2 (version 2) : null ou le personnage
                {
                    data.HasCharacter = true;
                    if (v.ValueKind != JsonValueKind.Null) data.Character = ReadCharacter(input, v, p);
                },
            });
            return ok ? data : null;
        }

        private static CharacterData? ReadCharacter(JsonInput input, JsonElement element, string path)
        {
            var c = new CharacterData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => c.Id = input.Text(v, p),
                ["class"] = (v, p) => c.Class = input.Text(v, p),
                ["xp"] = (v, p) => c.Xp = input.Int64(v, p),
                ["talents"] = (v, p) => c.Talents = ScenarioJsonReader.ReadIntegerMap(input, v, p),
                ["activeSkills"] = (v, p) => c.ActiveSkills = input.ReadLimitedList(v, p, SaveValidator.MaxSkills, n => $"{n} compétences, au plus {SaveValidator.MaxSkills}.", (e, ip) => input.Text(e, ip)),
                ["ultimate"] = (v, p) =>
                {
                    c.HasUltimate = true;
                    c.Ultimate = input.NullableText(v, p);
                },
                ["passiveSkills"] = (v, p) => c.PassiveSkills = input.ReadLimitedList(v, p, SaveValidator.MaxSkills, n => $"{n} compétences, au plus {SaveValidator.MaxSkills}.", (e, ip) => input.Text(e, ip)),
                ["inventory"] = (v, p) =>   // sous-lot 12.3 (version 3)
                {
                    c.Present.Add("inventory");
                    c.Inventory = input.ReadLimitedList(v, p, SaveValidator.MaxInventory, n => $"{n} places, au plus {SaveValidator.MaxInventory}.",
                        (e, ip) => ReadStack(input, e, ip));
                },
                ["equipment"] = (v, p) =>
                {
                    c.Present.Add("equipment");
                    c.Equipment = ReadEquipment(input, v, p, out List<KeyValuePair<string, ItemStackData>>? instances);
                    c.EquipmentInstances = instances;
                },
                ["professions"] = (v, p) =>   // sous-lot 12.6 (version 5)
                {
                    c.Present.Add("professions");
                    if (!input.RequireKind(v, JsonValueKind.Object, p, "objet « métier : expérience »")) return;
                    var list = new List<KeyValuePair<string, long?>>();
                    foreach (JsonProperty property in input.UniqueProperties(v, p))
                    {
                        if (list.Count >= SaveValidator.MaxProfessions) { input.Report.Add(p, ValidationCode.LimitExceeded, $"au plus {SaveValidator.MaxProfessions} métiers."); return; }
                        list.Add(new KeyValuePair<string, long?>(property.Name, input.Int64(property.Value, p + "." + property.Name)));
                    }
                    c.Professions = list;
                },
                ["story"] = (v, p) =>   // sous-lot 12.5 (version 4)
                {
                    c.Present.Add("story");
                    c.Story = ReadStory(input, v, p);
                },
                ["money"] = (v, p) =>   // sous-lot V1.1 (version 6) : bourse en pièces de cuivre
                {
                    c.Present.Add("money");
                    c.Money = input.Int64(v, p);
                },
            });
            return ok ? c : null;
        }

        private static StoryStateData? ReadStory(JsonInput input, JsonElement element, string path)
        {
            var st = new StoryStateData();
            List<string?>? Ids(JsonElement v, string p) => input.ReadLimitedList(v, p, SaveValidator.MaxStoryEntries,
                n => $"{n} entrées, au plus {SaveValidator.MaxStoryEntries}.", (e, ip) => input.Text(e, ip));
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["facts"] = (v, p) => st.Facts = Ids(v, p),
                ["discoveries"] = (v, p) => st.Discoveries = Ids(v, p),
                ["tracked"] = (v, p) =>   // sous-lot V1.6 (version 8)
                {
                    st.HasTracked = true;
                    st.Tracked = input.NullableText(v, p);
                },
                ["trust"] = (v, p) => st.Trust = ScenarioJsonReader.ReadIntegerMap(input, v, p),
                ["hunted"] = (v, p) => st.Hunted = input.ReadLimitedList(v, p, SavedStory.MaxHunted,   // relecture de la PR #79 (version 10)
                    n => $"{n} créatures, au plus {SavedStory.MaxHunted}.", (e, ip) => input.Text(e, ip)),
                ["guild"] = (v, p) =>   // sous-lot V1.7 (version 10) : carte d'aventurier
                {
                    st.HasGuild = true;
                    st.Guild = v.ValueKind == JsonValueKind.Null ? null : ReadGuild(input, v, p);
                },
                ["quests"] = (v, p) =>
                {
                    if (!input.RequireKind(v, JsonValueKind.Object, p, "objet « quête : avancement »")) return;
                    var quests = new List<(string, QuestProgressData?)>();
                    foreach (JsonProperty property in input.UniqueProperties(v, p))
                    {
                        if (quests.Count >= SaveValidator.MaxStoryEntries) { input.Report.Add(p, ValidationCode.LimitExceeded, $"au plus {SaveValidator.MaxStoryEntries} quêtes."); return; }
                        var q = new QuestProgressData();
                        bool qok = input.ReadFields(property.Value, p + "." + property.Name, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["step"] = (qv, qp) => q.Step = input.Text(qv, qp),
                            ["progress"] = (qv, qp) => q.Progress = input.Int64(qv, qp),
                        });
                        quests.Add((property.Name, qok ? q : null));
                    }
                    st.Quests = quests;
                },
            });
            return ok ? st : null;
        }

        /// <summary>Sous-lot V1.7 : carte d'aventurier (<c>story.guild</c>, version 10).</summary>
        private static GuildCardData? ReadGuild(JsonInput input, JsonElement element, string path)
        {
            var g = new GuildCardData();
            List<string?>? Ids(JsonElement v, string p, int max) => input.ReadLimitedList(v, p, max, n => $"{n} entrées, au plus {max}.", (e, ip) => input.Text(e, ip));
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["rank"] = (v, p) => g.Rank = input.Text(v, p),
                ["points"] = (v, p) => g.Points = input.Int64(v, p),
                ["contracts"] = (v, p) => g.Contracts = input.Int64(v, p),
                ["titles"] = (v, p) => g.Titles = Ids(v, p, SavedGuildCard.MaxTitles),
                ["title"] = (v, p) =>
                {
                    g.HasTitle = true;
                    g.Title = input.NullableText(v, p);
                },
                ["active"] = (v, p) =>
                {
                    if (!input.RequireKind(v, JsonValueKind.Object, p, "objet « contrat : avancement »")) return;
                    var list = new List<KeyValuePair<string, long?>>();
                    foreach (JsonProperty property in input.UniqueProperties(v, p))
                    {
                        if (list.Count >= SavedGuildCard.MaxActive) { input.Report.Add(p, ValidationCode.LimitExceeded, $"au plus {SavedGuildCard.MaxActive} contrats en cours."); return; }
                        list.Add(new KeyValuePair<string, long?>(property.Name, input.Int64(property.Value, p + "." + property.Name)));
                    }
                    g.Active = list;
                },
                ["day"] = (v, p) => g.Day = input.Int64(v, p),
                ["taken"] = (v, p) => g.Taken = Ids(v, p, SavedGuildCard.MaxTaken),
            });
            return ok ? g : null;
        }

        /// <summary>
        /// Une place de l'inventaire (<c>item</c>, <c>count</c>) ; sous-lot V1.3 (version 9) : instance d'objet, avec <c>properties</c>
        /// facultatif ; les noms <c>quality</c> et <c>wear</c> (sous-lot V1.4) sont relevés pour être refusés avec leur motif. Avec
        /// <paramref name="withCount"/> faux (objet porté sous sa forme d'instance), pas de <c>count</c>.
        /// </summary>
        private static ItemStackData? ReadStack(JsonInput input, JsonElement element, string path, bool withCount = true)
        {
            var stack = new ItemStackData();
            var fields = new Dictionary<string, Action<JsonElement, string>>
            {
                ["item"] = (v, p) => stack.Item = input.Text(v, p),
                ["properties"] = (v, p) => stack.Properties = input.ReadLimitedList(v, p, SaveValidator.MaxProperties,
                    n => $"{n} propriétés, au plus {SaveValidator.MaxProperties}.", (e, ip) => ReadProperty(input, e, ip)),
                ["quality"] = (v, p) => stack.Reserved.Add("quality"),
                ["wear"] = (v, p) => stack.Reserved.Add("wear"),
            };
            if (withCount) fields["count"] = (v, p) => stack.Count = input.Int32(v, p);
            bool ok = input.ReadFields(element, path, fields);
            return ok ? stack : null;
        }

        private static PropertyRollData? ReadProperty(JsonInput input, JsonElement element, string path)
        {
            var r = new PropertyRollData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => r.Id = input.Text(v, p),
                ["value"] = (v, p) => r.Value = input.Int32(v, p),
            });
            return ok ? r : null;
        }

        /// <summary>
        /// Objet porté par emplacement : l'identifiant de l'objet, ou (sous-lot V1.3, version 9) son instance <c>{ "item": …, "properties": […] }</c>,
        /// gardée à part dans <paramref name="instances"/>.
        /// </summary>
        private static List<KeyValuePair<string, string?>>? ReadEquipment(JsonInput input, JsonElement element, string path,
            out List<KeyValuePair<string, ItemStackData>>? instances)
        {
            instances = null;
            if (!input.RequireKind(element, JsonValueKind.Object, path, "objet « emplacement : objet »")) return null;
            var map = new List<KeyValuePair<string, string?>>();
            foreach (JsonProperty property in input.UniqueProperties(element, path))
            {
                if (map.Count >= SaveValidator.MaxEquipment) { input.Report.Add(path, ValidationCode.LimitExceeded, $"au plus {SaveValidator.MaxEquipment} emplacements."); return null; }
                string p = path + "." + property.Name;
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    ItemStackData? instance = ReadStack(input, property.Value, p, withCount: false);
                    map.Add(new KeyValuePair<string, string?>(property.Name, instance?.Item));
                    if (instance != null) (instances ??= new List<KeyValuePair<string, ItemStackData>>()).Add(new KeyValuePair<string, ItemStackData>(property.Name, instance));
                }
                else map.Add(new KeyValuePair<string, string?>(property.Name, input.Text(property.Value, p)));
            }
            return map;
        }
        /// <summary>Sous-lot V1.7 : la carte d'aventurier sur une ligne, dans l'ordre des champs du schéma.</summary>
        private static string Guild(SavedGuildCard g)
        {
            string List(IEnumerable<string> ids) => "[" + string.Join(", ", ids.Select(Quoted)) + "]";
            return "{\"rank\": " + Quoted(g.Rank) + ", \"points\": " + g.Points.ToString(CultureInfo.InvariantCulture)
                + ", \"contracts\": " + g.Contracts.ToString(CultureInfo.InvariantCulture) + ", \"titles\": " + List(g.Titles)
                + ", \"title\": " + (g.Title is null ? "null" : Quoted(g.Title))
                + ", \"active\": {" + string.Join(", ", g.Active.Select(a => Quoted(a.Key) + ": " + a.Value.ToString(CultureInfo.InvariantCulture))) + "}"
                + ", \"day\": " + g.Day.ToString(CultureInfo.InvariantCulture) + ", \"taken\": " + List(g.Taken) + "}";
        }


        /// <summary>Sous-lot V1.3 : propriétés tirées d'une instance, sur une ligne : <c>[{"id": "property.…", "value": 12}, …]</c>.</summary>
        private static string Properties(IReadOnlyList<RolledProperty> properties)
        {
            if (properties.Any(r => !SaveValidator.IsPropertyId(r.Id) || r.Value < 1 || r.Value > SaveValidator.MaxPropertyValue))
                throw new ArgumentException("Propriétés tirées hors du format des sauvegardes.", nameof(properties));
            return "[" + string.Join(", ", properties.Select(r => "{\"id\": " + Quoted(r.Id) + ", \"value\": " + r.Value.ToString(CultureInfo.InvariantCulture) + "}")) + "]";
        }
        // Identifiants écrits sans échappement : le format de SaveValidator.IsDataId, exigé aussi à la lecture (revue Codex de la PR #43).
        private static string Quoted(string id) =>
            SaveValidator.IsDataId(id) ? "\"" + id + "\"" : throw new ArgumentException($"Identifiant « {id} » hors du format des sauvegardes.", nameof(id));

        /// <summary>Texte canonique d'une sauvegarde (UTF-8, fins de ligne « \n », dernière ligne terminée).</summary>
        public static string Write(WorldSave save)
        {
            if (save is null) throw new ArgumentNullException(nameof(save));
            // Les identifiants et les clés sont déjà validés (ASCII sans guillemet ni barre oblique inverse) : aucun échappement nécessaire.
            if (!SaveValidator.IsZoneId(save.Zone) || save.Consumed.Any(k => !SaveValidator.IsEventKey(k)))
                throw new ArgumentException("Sauvegarde invalide : identifiant de zone ou clé d'événement hors du format.", nameof(save));
            // Revue Codex de la PR #42 : mêmes bornes qu'à la lecture, pour ne jamais écrire une sauvegarde qui ne se relirait pas.
            if (save.WorldTick < 0 || save.WorldTick > SaveValidator.MaxWorldTick || save.Sessions < 0 || save.Sessions > SaveValidator.MaxWorldTick
                || save.Consumed.Count > SaveValidator.MaxConsumed)
                throw new ArgumentException($"Sauvegarde invalide : temps du monde ou sessions hors de [0, {SaveValidator.MaxWorldTick}], ou plus de {SaveValidator.MaxConsumed} événements.", nameof(save));
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"schemaVersion\": ").Append(SaveValidator.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"kind\": \"").Append(SaveValidator.Kind).Append("\",\n");
            b.Append("  \"contentVersion\": \"").Append(ContentVersion).Append("\",\n");
            b.Append("  \"data\": {\n");
            b.Append("    \"zone\": \"").Append(save.Zone).Append("\",\n");
            b.Append("    \"worldTick\": ").Append(save.WorldTick.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("    \"sessions\": ").Append(save.Sessions.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            WriteCharacter(b, save.Character);
            if (save.Consumed.Count == 0) b.Append("    \"consumed\": []\n");
            else
            {
                b.Append("    \"consumed\": [\n");
                for (int i = 0; i < save.Consumed.Count; i++)
                    b.Append("      \"").Append(save.Consumed[i]).Append(i + 1 < save.Consumed.Count ? "\",\n" : "\"\n");
                b.Append("    ]\n");
            }
            b.Append("  }\n}\n");
            string text = b.ToString();
            // Revue Codex de la PR #43 : ce qui est écrit doit se relire (bornes du personnage comprises : expérience, rangs, identifiants),
            // sinon une sauvegarde valide serait remplacée par un fichier illisible.
            SaveLoadResult check = Read(text);
            if (!check.IsValid)
                throw new ArgumentException($"Sauvegarde invalide : {check.Report.Errors[0].Path} : {check.Report.Errors[0].Message}", nameof(save));
            return text;
        }

        private static void WriteCharacter(StringBuilder b, SavedCharacter? c)
        {
            if (c is null)
            {
                b.Append("    \"character\": null,\n");
                return;
            }
            string List(IEnumerable<string> ids) => "[" + string.Join(", ", ids.Select(Quoted)) + "]";
            b.Append("    \"character\": {\n");
            b.Append("      \"id\": ").Append(Quoted(c.Id)).Append(",\n");
            b.Append("      \"class\": ").Append(Quoted(c.ClassId)).Append(",\n");
            b.Append("      \"xp\": ").Append(c.Xp.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("      \"talents\": {").Append(string.Join(", ", c.Talents.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => Quoted(t.Key) + ": " + t.Value.ToString(CultureInfo.InvariantCulture)))).Append("},\n");
            b.Append("      \"activeSkills\": ").Append(List(c.ActiveSkills)).Append(",\n");
            b.Append("      \"ultimate\": ").Append(c.Ultimate is null ? "null" : Quoted(c.Ultimate)).Append(",\n");
            b.Append("      \"passiveSkills\": ").Append(List(c.PassiveSkills)).Append(",\n");
            // Sous-lot 12.3 (version 3) : une place occupée par ligne, dans l'ordre ; équipement dans l'ordre ordinal des emplacements.
            if (c.Inventory.Count == 0) b.Append("      \"inventory\": [],\n");
            else
            {
                b.Append("      \"inventory\": [\n");
                for (int i = 0; i < c.Inventory.Count; i++)
                {
                    ItemStack stack = c.Inventory[i];
                    if (!SaveValidator.IsItemId(stack.Item)) throw new ArgumentException($"Objet « {stack.Item} » hors du format des sauvegardes.", nameof(c));
                    b.Append("        {\"item\": ").Append(Quoted(stack.Item)).Append(", \"count\": ").Append(stack.Count.ToString(CultureInfo.InvariantCulture));
                    if (stack.IsRolled) b.Append(", \"properties\": ").Append(Properties(stack.Properties));   // sous-lot V1.3 (version 9)
                    b.Append(i + 1 < c.Inventory.Count ? "},\n" : "}\n");
                }
                b.Append("      ],\n");
            }
            if (c.Equipment.Any(e => !SaveValidator.IsItemId(e.Value) || !SaveValidator.IsDataId(e.Key)))
                throw new ArgumentException("Équipement hors du format des sauvegardes.", nameof(c));
            // Sous-lot V1.3 (version 9) : un objet porté aux propriétés tirées s'écrit sous sa forme d'instance.
            b.Append("      \"equipment\": {").Append(string.Join(", ", c.Equipment.Select(e => Quoted(e.Key) + ": "
                + (c.EquipmentProperties.TryGetValue(e.Key, out IReadOnlyList<RolledProperty>? rolled)
                    ? "{\"item\": " + Quoted(e.Value) + ", \"properties\": " + Properties(rolled) + "}"
                    : Quoted(e.Value))))).Append("},\n");
            // Sous-lot 12.5 (version 4) : état narratif, sur une ligne par rubrique, dans l'ordre ordinal.
            SavedStory st = c.Story;
            if (st.Facts.Concat(st.Discoveries).Concat(st.Quests.Keys).Concat(st.Trust.Keys).Any(id => !SaveValidator.IsStoryId(id))
                || (st.Tracked != null && !SaveValidator.IsStoryId(st.Tracked))
                || st.Quests.Values.Any(q => !SaveValidator.IsDataId(q.Step) || q.Progress < 0 || q.Progress > SaveValidator.MaxProgress)
                || st.Trust.Values.Any(t => t < -SaveValidator.MaxTrust || t > SaveValidator.MaxTrust))
                throw new ArgumentException("Histoire hors du format des sauvegardes.", nameof(c));
            b.Append("      \"story\": {\n");
            b.Append("        \"facts\": ").Append(List(st.Facts)).Append(",\n");
            b.Append("        \"quests\": {").Append(string.Join(", ", st.Quests.Select(q => Quoted(q.Key) + ": {\"step\": " + Quoted(q.Value.Step)
                + ", \"progress\": " + q.Value.Progress.ToString(CultureInfo.InvariantCulture) + "}"))).Append("},\n");
            b.Append("        \"trust\": {").Append(string.Join(", ", st.Trust.Select(t => Quoted(t.Key) + ": " + t.Value.ToString(CultureInfo.InvariantCulture)))).Append("},\n");
            b.Append("        \"discoveries\": ").Append(List(st.Discoveries)).Append(",\n");
            // Sous-lot V1.6 (version 8) : quête suivie, ou null.
            b.Append("        \"tracked\": ").Append(st.Tracked is null ? "null" : Quoted(st.Tracked)).Append(st.Guild is null && st.Hunted.Count == 0 ? "\n" : ",\n");
            // Sous-lot V1.7 (version 10) : créatures rares déjà vaincues (absent s'il n'y en a aucune), puis la carte d'aventurier, sur une ligne
            // chacune ; la carte est absente tant que le personnage n'est pas inscrit.
            if (st.Hunted.Count > 0)
            {
                if (st.Hunted.Count > SavedStory.MaxHunted || st.Hunted.Any(h => h.Length > 64 || !SaveValidator.IsDataId(h)))
                    throw new ArgumentException("Créatures rares hors du format des sauvegardes.", nameof(c));
                b.Append("        \"hunted\": ").Append(List(st.Hunted)).Append(st.Guild is null ? "\n" : ",\n");
            }
            if (st.Guild is SavedGuildCard g) b.Append("        \"guild\": ").Append(Guild(g)).Append("\n");
            b.Append("      },\n");
            // Sous-lot 12.6 (version 5) : expérience des métiers, sur une ligne.
            if (c.Professions.Any(p => !SaveValidator.IsDataId(p.Key) || p.Value < 1 || p.Value > SaveValidator.MaxXp))
                throw new ArgumentException("Métiers hors du format des sauvegardes.", nameof(c));
            b.Append("      \"professions\": {").Append(string.Join(", ", c.Professions.Select(p => Quoted(p.Key) + ": " + p.Value.ToString(CultureInfo.InvariantCulture)))).Append("},\n");
            // Sous-lot V1.1 (version 6) : bourse en pièces de cuivre, un seul nombre.
            if (!Coins.IsValid(c.Money)) throw new ArgumentException($"Bourse hors de [0, {Coins.Max}] cuivre.", nameof(c));
            b.Append("      \"money\": ").Append(c.Money.ToString(CultureInfo.InvariantCulture)).Append("\n");
            b.Append("    },\n");
        }
    }
}
