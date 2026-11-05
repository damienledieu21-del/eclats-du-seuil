using System.Collections.Generic;
using EdS.Data.Scenarios;

namespace EdS.Data.Story
{
    /// <summary>Sous-lot 12.5 : textes d'une langue (<c>kind: "texts"</c>), avant validation.</summary>
    public sealed class TextsDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public TextsData? Data { get; set; }
    }

    public sealed class TextsData
    {
        /// <summary>Langue (« fr », « en »).</summary>
        public string? Language { get; set; }

        /// <summary>Texte par clé (« text.… »), dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, string?>>? Entries { get; set; }
    }

    /// <summary>Sous-lot 12.5 : PNJ, dialogues, quêtes et découvertes (<c>kind: "story"</c>), avant validation.</summary>
    public sealed class StoryDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public StoryData? Data { get; set; }
    }

    public sealed class StoryData
    {
        public List<NpcData?>? Npcs { get; set; }
        public List<DialogueData?>? Dialogues { get; set; }
        public List<QuestData?>? Quests { get; set; }
        public List<DiscoveryData?>? Discoveries { get; set; }
    }

    public sealed class NpcData
    {
        public string? Id { get; set; }

        /// <summary>Clé du nom affiché.</summary>
        public string? Name { get; set; }

        public PointData? Position { get; set; }
        public string? Dialogue { get; set; }

        /// <summary>Sous-lot V1.6 (schéma 3) : zone d'un intérieur où vit le PNJ ; facultatif (absent : la zone de l'histoire).</summary>
        public string? Zone { get; set; }
    }

    public sealed class DialogueData
    {
        public string? Id { get; set; }

        /// <summary>Entrées possibles : la première dont les conditions sont réunies donne le nœud de départ.</summary>
        public List<DialogueEntryData?>? Entries { get; set; }

        public List<DialogueNodeData?>? Nodes { get; set; }
    }

    public sealed class DialogueEntryData
    {
        public List<TermData?>? Conditions { get; set; }
        public string? Node { get; set; }
    }

    public sealed class DialogueNodeData
    {
        public string? Id { get; set; }
        public string? Text { get; set; }
        public List<DialogueChoiceData?>? Choices { get; set; }
    }

    public sealed class DialogueChoiceData
    {
        public string? Text { get; set; }
        public List<TermData?>? Conditions { get; set; }
        public List<TermData?>? Effects { get; set; }

        /// <summary>Nœud suivant ; null : fin du dialogue.</summary>
        public string? Next { get; set; }

        public bool HasNext { get; set; }
    }

    public sealed class QuestData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<QuestStepData?>? Steps { get; set; }

        /// <summary>Effets à la fin de la quête (récompenses).</summary>
        public List<TermData?>? Effects { get; set; }

        /// <summary>Sous-lot V1.6 (schéma 3) : « main » (principale) ou « side » (secondaire) ; obligatoire.</summary>
        public string? Category { get; set; }

        /// <summary>Sous-lot V1.6 (schéma 3) : conditions d'ouverture ; facultatif.</summary>
        public List<TermData?>? Requires { get; set; }

        /// <summary>Sous-lot V1.6 (schéma 3) : récompenses au choix (effets <c>giveItem</c>, deux ou trois) ; facultatif.</summary>
        public List<TermData?>? RewardChoice { get; set; }
    }

    public sealed class QuestStepData
    {
        public string? Id { get; set; }

        /// <summary>Clé du texte du journal de quête pour cette étape.</summary>
        public string? Text { get; set; }

        public TermData? Objective { get; set; }

        /// <summary>Effets quand l'étape est accomplie.</summary>
        public List<TermData?>? Effects { get; set; }
    }

    public sealed class DiscoveryData
    {
        public string? Id { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>Condition (<c>if</c>), effet (<c>do</c>) ou objectif (<c>kind</c>) : une nature, un identifiant, une valeur, une étape.</summary>
    public sealed class TermData
    {
        public string? Kind { get; set; }
        public string? Id { get; set; }
        public long? Value { get; set; }
        public string? Step { get; set; }

        /// <summary>Sous-lot V1.6 : PNJ à qui livrer (objectif <c>deliver</c>).</summary>
        public string? Npc { get; set; }

        /// <summary>Sous-lot V1.9.3b (schéma 5) : qualité minimale (objectif <c>craftQuality</c> : « superior » ou « masterwork »).</summary>
        public string? Quality { get; set; }

        public HashSet<string> Present { get; } = new HashSet<string>();
    }
}
