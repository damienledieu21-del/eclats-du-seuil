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

        /// <summary>Sous-lot V1.9.6 (schéma 6) : scènes jouées, facultatives.</summary>
        public List<SceneData?>? Scenes { get; set; }

        /// <summary>Sous-lot V2.7 (schéma 12) : règles de réputation, facultatives.</summary>
        public ReputationData? Reputation { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 13) : fêtes du bourg, facultatives.</summary>
        public List<FestivalData?>? Festivals { get; set; }
    }

    /// <summary>Sous-lot V2.7 (schéma 12) : section <c>reputation</c> de l'histoire (voir <see cref="ReputationRules"/>), avant validation.</summary>
    public sealed class ReputationData
    {
        /// <summary>Faction d'origine : faction liée et part en pour cent.</summary>
        public List<KeyValuePair<string, List<KeyValuePair<string, int?>>?>>? Links { get; set; }

        /// <summary>Geste du monde : faction et changement.</summary>
        public List<KeyValuePair<string, List<KeyValuePair<string, int?>>?>>? Acts { get; set; }

        /// <summary>Population ou famille : faction et changement.</summary>
        public List<KeyValuePair<string, List<KeyValuePair<string, int?>>?>>? Defeated { get; set; }

        /// <summary>Événement du monde : changements à la réussite et à l'échec.</summary>
        public List<KeyValuePair<string, ReputationEventData?>>? Events { get; set; }

        /// <summary>Palier des villageois : prix d'achat en pour cent.</summary>
        public List<KeyValuePair<string, int?>>? Prices { get; set; }

        /// <summary>Palier de la faction du marchand : prix de vente (ce qu'il paie) en pour cent.</summary>
        public List<KeyValuePair<string, int?>>? SellPrices { get; set; }

        /// <summary>Factions dont les PNJ ne parlent plus à qui elles haïssent.</summary>
        public List<string?>? Shun { get; set; }

        /// <summary>Couvre-feu : une faction qui hait le personnage le chasse d'un lieu à certains moments.</summary>
        public ReputationCurfewData? Curfew { get; set; }

        /// <summary>Points sûrs secrets : ni découverts en passant ni proposés au voyage avant un fait de l'histoire.</summary>
        public List<ReputationHiddenData?>? Hidden { get; set; }
    }

    public sealed class ReputationCurfewData
    {
        public string? Faction { get; set; }
        public string? Place { get; set; }
        public List<string?>? Phases { get; set; }
        public string? SafePoint { get; set; }
    }

    public sealed class ReputationHiddenData
    {
        public string? SafePoint { get; set; }
        public string? Fact { get; set; }
    }

    public sealed class ReputationEventData
    {
        public List<KeyValuePair<string, int?>>? Won { get; set; }
        public List<KeyValuePair<string, int?>>? Lost { get; set; }

        /// <summary>Échec pendant l'absence (réponse de Damien du 7 novembre 2026 : plus faible qu'une défaite sur place) ; absent : comme <see cref="Lost"/>.</summary>
        public List<KeyValuePair<string, int?>>? Missed { get; set; }
    }

    /// <summary>Sous-lot V1.9.6 (schéma 6) : scène jouée (voir <see cref="Scene"/>).</summary>
    public sealed class SceneData
    {
        public string? Id { get; set; }
        public string? Zone { get; set; }
        public string? Actor { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
        public string? Text { get; set; }
        public string? Back { get; set; }
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

        /// <summary>Sous-lot V2.3 (schéma 9) : emploi du temps ; facultatif (absent : le PNJ reste à sa place).</summary>
        public List<ScheduleSlotData?>? Schedule { get; set; }

        /// <summary>Sous-lot V2.7 (schéma 12) : faction du PNJ (un garde ne parle plus à qui la garde hait) ; facultative.</summary>
        public string? Faction { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 13) : fêtes où vient ce PNJ de passage ; facultatives (absentes : toujours là).</summary>
        public List<string?>? Festivals { get; set; }
    }

    /// <summary>Sous-lot V2.3 (schéma 9) : un créneau de l'emploi du temps (voir <see cref="ScheduleSlot"/>).</summary>
    public sealed class ScheduleSlotData
    {
        /// <summary>Heure de début, « HH:MM » (00:00 à 23:59).</summary>
        public string? At { get; set; }
        public string? Activity { get; set; }
        public PointData? Position { get; set; }

        /// <summary>
        /// Sous-lot V2.3.1 (schéma 10) : zone de la place du créneau, si ce n'est pas celle où vit le PNJ (un intérieur relié à la zone de
        /// l'histoire, ou la zone de l'histoire pour un PNJ d'un intérieur) ; facultatif (absent : la zone du PNJ).
        /// </summary>
        public string? Zone { get; set; }

        /// <summary>Abri sous une vraie pluie ; facultatif.</summary>
        public SchedulePlaceData? Rain { get; set; }
    }

    /// <summary>Sous-lot V2.3 (schéma 9) : place et activité (l'abri d'un créneau).</summary>
    public sealed class SchedulePlaceData
    {
        public string? Activity { get; set; }
        public PointData? Position { get; set; }
    }

    /// <summary>Sous-lot V2.3 (schéma 9) : réplique d'un nœud selon le moment.</summary>
    public sealed class DialogueLineData
    {
        public List<TermData?>? Conditions { get; set; }
        public string? Text { get; set; }
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

        /// <summary>Sous-lot V2.3 (schéma 9) : répliques selon le moment ; facultatif.</summary>
        public List<DialogueLineData?>? Lines { get; set; }
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

        /// <summary>Sous-lot V1.9.7 (schéma 7) : la quête peut être passée (effet <c>skipQuest</c>) ; facultatif, faux par défaut.</summary>
        public bool? Skippable { get; set; }
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
