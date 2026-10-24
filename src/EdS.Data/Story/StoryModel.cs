using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Collections;
using EdS.Domain.Space;

namespace EdS.Data.Story
{
    /// <summary>Conditions d'un dialogue (cadrage H3 : acquis durables et circonstances temporaires).</summary>
    public enum ConditionKind
    {
        /// <summary>Acquis durable : fait appris.</summary>
        Fact = 0,
        NotFact = 1,
        QuestNotStarted = 2,
        QuestActive = 3,

        /// <summary>Quête en cours à cette étape.</summary>
        QuestAt = 4,
        QuestDone = 5,

        /// <summary>Confiance d'un PNJ au moins égale à la valeur (H1).</summary>
        TrustAtLeast = 6,
        TrustBelow = 7,

        /// <summary>Circonstance : objets dans l'inventaire.</summary>
        HasItem = 8,

        /// <summary>Circonstance temporaire : moment de la journée (dawn, day, dusk, night).</summary>
        Phase = 9,

        /// <summary>Circonstance temporaire : le personnage est dans ce lieu.</summary>
        InPlace = 10,

        PassageOpen = 11,
    }

    public enum EffectKind
    {
        SetFact = 0,

        /// <summary>Change la confiance d'un PNJ (valeur signée).</summary>
        Trust = 1,
        StartQuest = 2,
        GiveItem = 3,
        TakeItem = 4,
        GiveXp = 5,
        OpenPassage = 6,

        /// <summary>Inscrit une découverte au journal.</summary>
        Discover = 7,
    }

    public enum ObjectiveKind
    {
        /// <summary>Un fait est appris (souvent par un dialogue).</summary>
        Fact = 0,

        /// <summary>Le personnage atteint un lieu.</summary>
        Reach = 1,

        /// <summary>Vaincre des créatures d'une population (morts attribuées pendant l'étape).</summary>
        Defeat = 2,

        /// <summary>Avoir des objets dans l'inventaire.</summary>
        Collect = 3,

        /// <summary>Un passage est ouvert.</summary>
        Passage = 4,
    }

    public sealed class StoryCondition
    {
        public ConditionKind Kind { get; }
        public string Id { get; }
        public long Value { get; }
        public string? Step { get; }

        internal StoryCondition(ConditionKind kind, string id, long value, string? step)
        {
            Kind = kind;
            Id = id;
            Value = value;
            Step = step;
        }
    }

    public sealed class StoryEffect
    {
        public EffectKind Kind { get; }

        /// <summary>Identifiant visé (vide pour l'expérience).</summary>
        public string Id { get; }

        public long Value { get; }

        internal StoryEffect(EffectKind kind, string id, long value)
        {
            Kind = kind;
            Id = id;
            Value = value;
        }
    }

    public sealed class Objective
    {
        public ObjectiveKind Kind { get; }
        public string Id { get; }

        /// <summary>Nombre à atteindre (créatures, objets) ; 1 sinon.</summary>
        public long Value { get; }

        internal Objective(ObjectiveKind kind, string id, long value)
        {
            Kind = kind;
            Id = id;
            Value = value;
        }
    }

    public sealed class Npc
    {
        public string Id { get; }

        /// <summary>Clé du nom.</summary>
        public string Name { get; }

        public GroundVector Position { get; }
        public string Dialogue { get; }

        internal Npc(string id, string name, GroundVector position, string dialogue)
        {
            Id = id;
            Name = name;
            Position = position;
            Dialogue = dialogue;
        }
    }

    public sealed class DialogueChoice
    {
        public string Text { get; }
        public IReadOnlyList<StoryCondition> Conditions { get; }
        public IReadOnlyList<StoryEffect> Effects { get; }
        public string? Next { get; }

        internal DialogueChoice(string text, IEnumerable<StoryCondition> conditions, IEnumerable<StoryEffect> effects, string? next)
        {
            Text = text;
            Conditions = Frozen.List(conditions, nameof(conditions));
            Effects = Frozen.List(effects, nameof(effects));
            Next = next;
        }
    }

    public sealed class DialogueNode
    {
        public string Id { get; }
        public string Text { get; }
        public IReadOnlyList<DialogueChoice> Choices { get; }

        internal DialogueNode(string id, string text, IEnumerable<DialogueChoice> choices)
        {
            Id = id;
            Text = text;
            Choices = Frozen.List(choices, nameof(choices));
        }
    }

    public sealed class DialogueEntry
    {
        public IReadOnlyList<StoryCondition> Conditions { get; }
        public string Node { get; }

        internal DialogueEntry(IEnumerable<StoryCondition> conditions, string node)
        {
            Conditions = Frozen.List(conditions, nameof(conditions));
            Node = node;
        }
    }

    public sealed class Dialogue
    {
        public string Id { get; }
        public IReadOnlyList<DialogueEntry> Entries { get; }
        public IReadOnlyList<DialogueNode> Nodes { get; }

        internal Dialogue(string id, IEnumerable<DialogueEntry> entries, IEnumerable<DialogueNode> nodes)
        {
            Id = id;
            Entries = Frozen.List(entries, nameof(entries));
            Nodes = Frozen.List(nodes, nameof(nodes));
        }

        public DialogueNode Node(string id) => Nodes.First(n => n.Id == id);
    }

    public sealed class QuestStep
    {
        public string Id { get; }
        public string Text { get; }
        public Objective Objective { get; }
        public IReadOnlyList<StoryEffect> Effects { get; }

        internal QuestStep(string id, string text, Objective objective, IEnumerable<StoryEffect> effects)
        {
            Id = id;
            Text = text;
            Objective = objective;
            Effects = Frozen.List(effects, nameof(effects));
        }
    }

    public sealed class Quest
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<QuestStep> Steps { get; }
        public IReadOnlyList<StoryEffect> Effects { get; }

        internal Quest(string id, string name, IEnumerable<QuestStep> steps, IEnumerable<StoryEffect> effects)
        {
            Id = id;
            Name = name;
            Steps = Frozen.List(steps, nameof(steps));
            Effects = Frozen.List(effects, nameof(effects));
        }

        public int StepIndex(string step)
        {
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Id == step) return i;
            return -1;
        }
    }

    public sealed class Discovery
    {
        public string Id { get; }
        public string Text { get; }

        internal Discovery(string id, string text)
        {
            Id = id;
            Text = text;
        }
    }

    /// <summary>Sous-lot 12.5 (cadrage Q37) : textes d'une langue, par clé.</summary>
    public sealed class TextCatalog
    {
        public string Language { get; }
        public string ContentVersion { get; }
        public IReadOnlyDictionary<string, string> Entries { get; }

        internal TextCatalog(string language, string contentVersion, IReadOnlyDictionary<string, string> entries)
        {
            Language = language;
            ContentVersion = contentVersion;
            Entries = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(entries ?? throw new ArgumentNullException(nameof(entries)), StringComparer.Ordinal));
        }

        public bool Has(string key) => Entries.ContainsKey(key ?? string.Empty);

        /// <summary>Texte de la clé ; la clé elle-même si elle manque (jamais d'exception à l'affichage).</summary>
        public string Get(string key) => key != null && Entries.TryGetValue(key, out string text) ? text : key ?? string.Empty;
    }

    /// <summary>
    /// Sous-lot 12.5 (cadrage H1, H2, H3, Q2) : PNJ, dialogues, quêtes et découvertes validés. Les textes sont des clés (Q37) ; les lieux,
    /// passages et populations sont ceux de la zone ; les objets ceux du catalogue.
    /// </summary>
    public sealed class StoryBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<Npc> Npcs { get; }
        public IReadOnlyList<Dialogue> Dialogues { get; }
        public IReadOnlyList<Quest> Quests { get; }
        public IReadOnlyList<Discovery> Discoveries { get; }

        internal StoryBook(int schemaVersion, string contentVersion, IEnumerable<Npc> npcs, IEnumerable<Dialogue> dialogues, IEnumerable<Quest> quests,
            IEnumerable<Discovery> discoveries)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Npcs = Frozen.List(npcs, nameof(npcs));
            Dialogues = Frozen.List(dialogues, nameof(dialogues));
            Quests = Frozen.List(quests, nameof(quests));
            Discoveries = Frozen.List(discoveries, nameof(discoveries));
        }

        public Npc Npc(string id) => Npcs.FirstOrDefault(n => n.Id == id) ?? throw new ArgumentException($"PNJ « {id} » inconnu.", nameof(id));
        public Dialogue Dialogue(string id) => Dialogues.First(d => d.Id == id);
        public Quest Quest(string id) => Quests.FirstOrDefault(q => q.Id == id) ?? throw new ArgumentException($"Quête « {id} » inconnue.", nameof(id));
        public bool HasQuest(string id) => Quests.Any(q => q.Id == id);
        public bool HasNpc(string id) => Npcs.Any(n => n.Id == id);
    }
}
