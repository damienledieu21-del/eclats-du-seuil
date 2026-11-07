using System;
using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.5 : lecture stricte des textes d'une langue (<c>data/v0/world/texts/fr.json</c>) et de l'histoire
    /// (<c>data/v0/world/story.json</c> : PNJ, dialogues, quêtes, découvertes). La forme seulement ; le reste va aux validateurs uniques
    /// <see cref="TextsValidator"/> et <see cref="StoryValidator"/>.
    /// </summary>
    public static class StoryJsonReader
    {
        public static string TextsPathIn(string dataDirectory, string language = "fr") =>
            System.IO.Path.Combine(dataDirectory, "world", "texts", language + ".json");

        public static string StoryPathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "story.json");

        public static TextsLoadResult ReadTextsFile(string path)
        {
            var report = new ValidationReport();
            return ReadTexts(JsonInput.ReadFile(path, report), report);
        }

        public static TextsLoadResult ReadTexts(string json)
        {
            var report = new ValidationReport();
            return ReadTexts(JsonInput.Utf8(json, report), report);
        }

        private static TextsLoadResult ReadTexts(byte[]? json, ValidationReport report)
        {
            var document = new TextsDocument();
            if (json is null) return TextsValidator.Validate(document, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new TextsData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["language"] = (v, p) => d.Language = input.Text(v, p),
                    ["entries"] = (v, p) => d.Entries = ReadTextMap(input, v, p),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return TextsValidator.Validate(document, report);
        }

        private static List<KeyValuePair<string, string?>>? ReadTextMap(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Object, path, "objet « clé : texte »")) return null;
            var map = new List<KeyValuePair<string, string?>>();
            foreach (JsonProperty property in input.UniqueProperties(element, path))
                map.Add(new KeyValuePair<string, string?>(property.Name, input.Text(property.Value, path + "." + property.Name)));
            return map;
        }

        /// <param name="interiors">Sous-lot V1.6 : autres zones du monde (intérieurs reliés par des portes), où peuvent vivre des PNJ.</param>
        /// <param name="levelCap">Relecture de la PR #82 : plafond de niveau (avertissement des conditions <c>levelAtLeast</c> plus hautes).</param>
        public static StoryLoadResult ReadStoryFile(string path, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, IReadOnlyList<ZoneDefinition>? interiors = null,
            int? levelCap = null)
        {
            var report = new ValidationReport();
            return ReadStory(JsonInput.ReadFile(path, report), texts, zone, items, report, interiors, levelCap);
        }

        public static StoryLoadResult ReadStory(string json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, IReadOnlyList<ZoneDefinition>? interiors = null,
            int? levelCap = null)
        {
            var report = new ValidationReport();
            return ReadStory(JsonInput.Utf8(json, report), texts, zone, items, report, interiors, levelCap);
        }

        private static StoryLoadResult ReadStory(byte[]? json, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport report,
            IReadOnlyList<ZoneDefinition>? interiors, int? levelCap) =>
            StoryValidator.Validate(ReadStoryDocument(json, report), texts, zone, items, report, interiors, levelCap);

        /// <summary>Document lu sans validation (tests : document retouché en objets publics, puis validé).</summary>
        internal static StoryDocument ReadStoryDocument(byte[]? json, ValidationReport report)
        {
            var document = new StoryDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new StoryData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["npcs"] = (v, p) => d.Npcs = List(input, v, p, StoryValidator.MaxNpcs, ReadNpc),
                    ["dialogues"] = (v, p) => d.Dialogues = List(input, v, p, StoryValidator.MaxDialogues, ReadDialogue),
                    ["quests"] = (v, p) => d.Quests = List(input, v, p, StoryValidator.MaxQuests, ReadQuest),
                    ["discoveries"] = (v, p) => d.Discoveries = List(input, v, p, StoryValidator.MaxDiscoveries, ReadDiscovery),
                    ["scenes"] = (v, p) => d.Scenes = List(input, v, p, StoryValidator.MaxScenes, ReadScene),   // sous-lot V1.9.6 (schéma 6)
                    ["reputation"] = (v, p) => d.Reputation = ReadReputation(input, v, p),   // sous-lot V2.7 (schéma 12)
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static List<T?>? List<T>(JsonInput input, JsonElement element, string path, int max, Func<JsonInput, JsonElement, string, T?> read) where T : class =>
            input.ReadLimitedList(element, path, max, n => $"{n} éléments, au plus {max}.", (e, ip) => read(input, e, ip));

        private static NpcData? ReadNpc(JsonInput input, JsonElement element, string path)
        {
            var n = new NpcData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => n.Id = input.Text(v, p),
                ["name"] = (v, p) => n.Name = input.Text(v, p),
                ["position"] = (v, p) => n.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["dialogue"] = (v, p) => n.Dialogue = input.Text(v, p),
                ["zone"] = (v, p) => n.Zone = input.Text(v, p),   // sous-lot V1.6 (schéma 3)
                ["faction"] = (v, p) => n.Faction = input.Text(v, p),   // sous-lot V2.7 (schéma 12)
                ["schedule"] = (v, p) => n.Schedule = List(input, v, p, StoryValidator.MaxScheduleSlots, ReadSlot),   // sous-lot V2.3 (schéma 9)
            }) ? n : null;
        }

        /// <summary>Sous-lot V2.3 (schéma 9) : un créneau de l'emploi du temps d'un PNJ.</summary>
        private static ScheduleSlotData? ReadSlot(JsonInput input, JsonElement element, string path)
        {
            var s = new ScheduleSlotData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["at"] = (v, p) => s.At = input.Text(v, p),
                ["activity"] = (v, p) => s.Activity = input.Text(v, p),
                ["position"] = (v, p) => s.Position = ScenarioJsonReader.ReadPoint(input, v, p),
                ["zone"] = (v, p) => s.Zone = input.Text(v, p),   // sous-lot V2.3.1 (schéma 10)
                ["rain"] = (v, p) => s.Rain = ReadPlace(input, v, p),
            }) ? s : null;
        }

        private static SchedulePlaceData? ReadPlace(JsonInput input, JsonElement element, string path)
        {
            var s = new SchedulePlaceData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["activity"] = (v, p) => s.Activity = input.Text(v, p),
                ["position"] = (v, p) => s.Position = ScenarioJsonReader.ReadPoint(input, v, p),
            }) ? s : null;
        }

        /// <summary>Sous-lot V2.3 (schéma 9) : une réplique d'un nœud selon le moment.</summary>
        private static DialogueLineData? ReadLine(JsonInput input, JsonElement element, string path)
        {
            var l = new DialogueLineData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["conditions"] = (v, p) => l.Conditions = Terms(input, v, p, "if"),
                ["text"] = (v, p) => l.Text = input.Text(v, p),
            }) ? l : null;
        }

        private static DialogueData? ReadDialogue(JsonInput input, JsonElement element, string path)
        {
            var x = new DialogueData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["entries"] = (v, p) => x.Entries = List(input, v, p, StoryValidator.MaxEntries, ReadEntry),
                ["nodes"] = (v, p) => x.Nodes = List(input, v, p, StoryValidator.MaxNodes, ReadNode),
            }) ? x : null;
        }

        private static DialogueEntryData? ReadEntry(JsonInput input, JsonElement element, string path)
        {
            var e = new DialogueEntryData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["conditions"] = (v, p) => e.Conditions = Terms(input, v, p, "if"),
                ["node"] = (v, p) => e.Node = input.Text(v, p),
            }) ? e : null;
        }

        private static DialogueNodeData? ReadNode(JsonInput input, JsonElement element, string path)
        {
            var n = new DialogueNodeData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => n.Id = input.Text(v, p),
                ["text"] = (v, p) => n.Text = input.Text(v, p),
                ["choices"] = (v, p) => n.Choices = List(input, v, p, StoryValidator.MaxChoices, ReadChoice),
                ["lines"] = (v, p) => n.Lines = List(input, v, p, StoryValidator.MaxLines, ReadLine),   // sous-lot V2.3 (schéma 9)
            }) ? n : null;
        }

        private static DialogueChoiceData? ReadChoice(JsonInput input, JsonElement element, string path)
        {
            var c = new DialogueChoiceData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["text"] = (v, p) => c.Text = input.Text(v, p),
                ["conditions"] = (v, p) => c.Conditions = Terms(input, v, p, "if"),
                ["effects"] = (v, p) => c.Effects = Terms(input, v, p, "do"),
                ["next"] = (v, p) =>
                {
                    c.HasNext = true;
                    c.Next = input.NullableText(v, p);
                },
            }) ? c : null;
        }

        private static QuestData? ReadQuest(JsonInput input, JsonElement element, string path)
        {
            var q = new QuestData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => q.Id = input.Text(v, p),
                ["name"] = (v, p) => q.Name = input.Text(v, p),
                ["steps"] = (v, p) => q.Steps = List(input, v, p, StoryValidator.MaxSteps, ReadStep),
                ["effects"] = (v, p) => q.Effects = Terms(input, v, p, "do"),
                // Sous-lot V1.6 (schéma 3) : onglet du journal, conditions d'ouverture, récompenses au choix.
                ["category"] = (v, p) => q.Category = input.Text(v, p),
                ["requires"] = (v, p) => q.Requires = Terms(input, v, p, "if"),
                ["rewardChoice"] = (v, p) => q.RewardChoice = Terms(input, v, p, "do"),
                ["skippable"] = (v, p) => q.Skippable = input.Boolean(v, p),   // sous-lot V1.9.7 (schéma 7)
            }) ? q : null;
        }

        private static QuestStepData? ReadStep(JsonInput input, JsonElement element, string path)
        {
            var s = new QuestStepData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => s.Id = input.Text(v, p),
                ["text"] = (v, p) => s.Text = input.Text(v, p),
                ["objective"] = (v, p) => s.Objective = Term(input, v, p, "kind"),
                ["effects"] = (v, p) => s.Effects = Terms(input, v, p, "do"),
            }) ? s : null;
        }

        private static DiscoveryData? ReadDiscovery(JsonInput input, JsonElement element, string path)
        {
            var x = new DiscoveryData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["text"] = (v, p) => x.Text = input.Text(v, p),
            }) ? x : null;
        }

        private static SceneData? ReadScene(JsonInput input, JsonElement element, string path)
        {
            var x = new SceneData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["zone"] = (v, p) => x.Zone = input.Text(v, p),
                ["actor"] = (v, p) => x.Actor = input.Text(v, p),
                ["start"] = (v, p) => x.Start = input.Text(v, p),
                ["end"] = (v, p) => x.End = input.Text(v, p),
                ["text"] = (v, p) => x.Text = input.Text(v, p),
                ["back"] = (v, p) => x.Back = input.Text(v, p),
            }) ? x : null;
        }

        /// <summary>Sous-lot V2.7 (schéma 12) : section <c>reputation</c> (liens, gestes, victoires, événements, prix).</summary>
        private static ReputationData? ReadReputation(JsonInput input, JsonElement element, string path)
        {
            var r = new ReputationData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["links"] = (v, p) => r.Links = NestedMap(input, v, p),
                ["acts"] = (v, p) => r.Acts = NestedMap(input, v, p),
                ["defeated"] = (v, p) => r.Defeated = NestedMap(input, v, p),
                ["prices"] = (v, p) => r.Prices = ScenarioJsonReader.ReadIntegerMap(input, v, p),
                ["sellPrices"] = (v, p) => r.SellPrices = ScenarioJsonReader.ReadIntegerMap(input, v, p),
                ["shun"] = (v, p) => r.Shun = input.ReadLimitedList(v, p, 4, n => $"{n} factions, au plus 4.", (e, ip) => input.Text(e, ip)),
                ["curfew"] = (v, p) =>
                {
                    var c = new ReputationCurfewData();
                    r.Curfew = input.ReadFields(v, p, new Dictionary<string, Action<JsonElement, string>>
                    {
                        ["faction"] = (w, wp) => c.Faction = input.Text(w, wp),
                        ["place"] = (w, wp) => c.Place = input.Text(w, wp),
                        ["phases"] = (w, wp) => c.Phases = input.ReadLimitedList(w, wp, 4, n => $"{n} moments, au plus 4.", (e, ip) => input.Text(e, ip)),
                        ["safePoint"] = (w, wp) => c.SafePoint = input.Text(w, wp),
                    }) ? c : null;
                },
                ["hidden"] = (v, p) => r.Hidden = input.ReadLimitedList(v, p, 8, n => $"{n} points sûrs secrets, au plus 8.", (e, ip) =>
                {
                    var h = new ReputationHiddenData();
                    return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                    {
                        ["safePoint"] = (w, wp) => h.SafePoint = input.Text(w, wp),
                        ["fact"] = (w, wp) => h.Fact = input.Text(w, wp),
                    }) ? h : null;
                }),
                ["events"] = (v, p) =>
                {
                    if (!input.RequireKind(v, JsonValueKind.Object, p, "objet « événement : réussite, échec »")) return;
                    var events = new List<KeyValuePair<string, ReputationEventData?>>();
                    foreach (JsonProperty e in input.UniqueProperties(v, p))
                    {
                        var x = new ReputationEventData();
                        string ep = p + "." + e.Name;
                        bool ok = input.ReadFields(e.Value, ep, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["won"] = (w, wp) => x.Won = ScenarioJsonReader.ReadIntegerMap(input, w, wp),
                            ["lost"] = (l, lp) => x.Lost = ScenarioJsonReader.ReadIntegerMap(input, l, lp),
                            ["missed"] = (l, lp) => x.Missed = ScenarioJsonReader.ReadIntegerMap(input, l, lp),
                        });
                        events.Add(new KeyValuePair<string, ReputationEventData?>(e.Name, ok ? x : null));
                    }
                    r.Events = events;
                },
            }) ? r : null;
        }

        private static List<KeyValuePair<string, List<KeyValuePair<string, int?>>?>>? NestedMap(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Object, path, "objet « clé : { faction : valeur } »")) return null;
            var map = new List<KeyValuePair<string, List<KeyValuePair<string, int?>>?>>();
            foreach (JsonProperty property in input.UniqueProperties(element, path))
                map.Add(new KeyValuePair<string, List<KeyValuePair<string, int?>>?>(property.Name,
                    ScenarioJsonReader.ReadIntegerMap(input, property.Value, path + "." + property.Name)));
            return map;
        }

        private static List<TermData?>? Terms(JsonInput input, JsonElement element, string path, string kindField) =>
            input.ReadLimitedList(element, path, StoryValidator.MaxTerms, n => $"{n} termes, au plus {StoryValidator.MaxTerms}.",
                (e, ip) => Term(input, e, ip, kindField));

        /// <summary>Condition (<c>if</c>), effet (<c>do</c>) ou objectif (<c>kind</c>), avec <c>id</c>, <c>value</c> et <c>step</c> facultatifs.</summary>
        internal static TermData? Term(JsonInput input, JsonElement element, string path, string kindField)
        {
            var t = new TermData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                [kindField] = (v, p) => t.Kind = input.Text(v, p),
                ["id"] = (v, p) => { t.Present.Add("id"); t.Id = input.Text(v, p); },
                ["value"] = (v, p) => { t.Present.Add("value"); t.Value = input.Int64(v, p); },
                ["step"] = (v, p) => { t.Present.Add("step"); t.Step = input.Text(v, p); },
                ["npc"] = (v, p) => { t.Present.Add("npc"); t.Npc = input.Text(v, p); },   // sous-lot V1.6 : objectif deliver
                ["quality"] = (v, p) => { t.Present.Add("quality"); t.Quality = input.Text(v, p); },   // sous-lot V1.9.3b : objectif craftQuality
            }) ? t : null;
        }
    }
}
