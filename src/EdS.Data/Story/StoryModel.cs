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

        /// <summary>Sous-lot V1.6 (histoire en schéma 3) : le personnage a au moins ce niveau (d'après son expérience).</summary>
        LevelAtLeast = 12,

        /// <summary>
        /// Relecture de la PR #76 : la quête a franchi toutes ses étapes et attend le choix de sa récompense (elle n'est plus alors
        /// <see cref="QuestActive"/> pour les dialogues).
        /// </summary>
        QuestReward = 13,

        /// <summary>
        /// Sous-lot V1.7 (histoire en schéma 4) : le personnage est inscrit à la Guilde des aventuriers, à ce rang au moins (identifiant du
        /// rang : « g », « f », « e »…). Rang G : inscrit.
        /// </summary>
        GuildRankAtLeast = 14,

        /// <summary>
        /// Sous-lot V2.2 (histoire en schéma 8) : circonstance temporaire : le temps qu'il fait dehors (<c>clear</c>, <c>cloudy</c>,
        /// <c>mist</c>, <c>drizzle</c>, <c>shower</c>, <c>storm</c>, ou <c>rain</c> : n'importe quelle pluie).
        /// </summary>
        Weather = 15,

        /// <summary>
        /// Sous-lot V2.3 (histoire en schéma 9) : circonstance temporaire : le PNJ <c>id</c> est à l'activité <c>step</c> de son emploi du
        /// temps (<see cref="Npc.Schedule"/>), d'après l'heure du monde et le temps qu'il fait (« fournil », « taverne », « abri »…).
        /// </summary>
        Activity = 16,

        /// <summary>
        /// Sous-lot V2.6.1 (histoire en schéma 11) : la faille à cicatrice <c>id</c> de la zone de l'histoire a au moins (<c>scarsAtLeast</c>)
        /// ou moins de (<c>scarsBelow</c>) <c>value</c> cicatrices gardées dans la sauvegarde du personnage.
        /// </summary>
        ScarsAtLeast = 17,
        ScarsBelow = 18,

        /// <summary>
        /// Sous-lot V2.7 (histoire en schéma 12) : la réputation auprès de la faction <c>id</c> (<see cref="Reputations.Factions"/>) est au moins
        /// (<c>reputationAtLeast</c>) ou sous (<c>reputationBelow</c>) <c>value</c>.
        /// </summary>
        ReputationAtLeast = 19,
        ReputationBelow = 20,

        /// <summary>Sous-lot V2.7 (schéma 12) : la faction <c>id</c> est exactement au palier <c>step</c> (<see cref="Reputations.Tiers"/>).</summary>
        Standing = 21,

        /// <summary>Sous-lot V2.7 (schéma 12) : la faction <c>id</c> est au moins au palier <c>step</c>.</summary>
        StandingAtLeast = 22,
    }

    /// <summary>Sous-lot V1.6 (histoire en schéma 3) : onglet du journal où se range une quête.</summary>
    public enum QuestCategory
    {
        /// <summary>Quête principale (« main »).</summary>
        Main = 0,

        /// <summary>Quête secondaire (« side »).</summary>
        Side = 1,
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

        /// <summary>Sous-lot V1.1 (histoire en schéma 2) : donne des pièces (valeur en cuivre) à la bourse du personnage.</summary>
        GiveMoney = 8,

        /// <summary>
        /// Sous-lot V1.6 (histoire en schéma 3) : prend des pièces (valeur en cuivre) à la bourse ; refusé en entier, sans rien changer, si
        /// elle ne suffit pas.
        /// </summary>
        TakeMoney = 9,

        /// <summary>
        /// Sous-lot V1.7 (histoire en schéma 4) : donne des points de guilde à la carte d'aventurier (valeur) ; sans effet tant que le personnage
        /// n'est pas inscrit (jamais refusé : un plafond de la carte arrête le gain).
        /// </summary>
        GiveGuildPoints = 10,

        /// <summary>
        /// Sous-lot V1.9.7 (histoire en schéma 7) : passe la quête <c>id</c> en cours (une étape, ou le choix de sa récompense) : elle est
        /// finie, <b>sans</b> les effets de ses étapes restantes ni sa récompense finale, et n'est plus suivie. Sans effet sur une quête pas
        /// commencée ou déjà finie. Exemple : « Je sais me battre », chez Odile, passe le tutoriel de l'ouverture (sans ses 30 points).
        /// </summary>
        SkipQuest = 11,

        /// <summary>
        /// Sous-lot V2.7 (schéma 12) : change de <c>value</c> (non nul) la réputation auprès de la faction <c>id</c>, bornée ; les factions
        /// liées (<see cref="ReputationRules.Links"/>) en reçoivent leur part. Jamais refusé.
        /// </summary>
        Reputation = 12,
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

        /// <summary>
        /// Sous-lot V1.6 : parler à un PNJ pendant l'étape (à portée, dans sa zone) ; rempli même si son dialogue n'a rien à dire à ce
        /// moment (l'interface annonce alors l'objectif rempli, pas « rien à dire »).
        /// </summary>
        Talk = 5,

        /// <summary>
        /// Sous-lot V1.6 : livrer des objets à un PNJ (<see cref="Objective.Npc"/>) : en lui parlant, s'ils sont dans l'inventaire et libres
        /// (pas réservés), ils sont promis ; ils lui sont remis d'un coup au tick suivant, avec les effets de l'étape, en entier ou pas du tout.
        /// </summary>
        Deliver = 6,

        /// <summary>Sous-lot V1.6 : vaincre des créatures d'une famille (toutes les populations de cette famille, morts attribuées pendant l'étape).</summary>
        DefeatFamily = 7,

        /// <summary>Sous-lot V1.6 : porter un objet (équipement).</summary>
        Equipped = 8,

        /// <summary>Sous-lot V1.7 (histoire en schéma 4) : atteindre un rang de la Guilde des aventuriers (identifiant du rang).</summary>
        GuildRank = 9,

        /// <summary>
        /// Sous-lot V1.9.3b (histoire en schéma 5) : avoir fabriqué un équipement d'une qualité minimale (<see cref="Objective.Quality"/> :
        /// supérieure ou chef-d'œuvre) : au moins <see cref="Objective.Value"/> instances de l'objet à cette qualité ou mieux, dans le sac ou
        /// portées. Seule la forge donne une qualité (butin, marchands et récompenses sont de qualité normale) ; l'objet n'est pas pris.
        /// </summary>
        CraftQuality = 10,

        /// <summary>
        /// Sous-lot V1.9.6 (histoire en schéma 6, tutoriel de l'ouverture) : le joueur fait lui-même un geste de combat pendant l'étape,
        /// <see cref="Objective.Value"/> fois : <c>strike</c> (un coup porté à un ennemi), <c>guard</c> (un coup bloqué par la garde),
        /// <c>parry</c> (une parade parfaite), <c>drink</c> (un objet de soin bu). Compté par la partie d'après les compteurs du moteur.
        /// </summary>
        Practice = 11,

        /// <summary>
        /// Sous-lot V2.6.1 (histoire en schéma 11, quête de Sœur Albe) : purifier <see cref="Objective.Value"/> cicatrices de la faille
        /// <see cref="Objective.Id"/> (un éclat de Seuil posé au cœur) pendant l'étape. Compté par la partie à chaque purification.
        /// </summary>
        Purify = 12,
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

        /// <summary>Sous-lot V1.7 : effet qui commence la quête <paramref name="quest"/> (l'épreuve de promotion de la guilde, prise au tableau).</summary>
        public static StoryEffect StartQuest(string quest) =>
            new StoryEffect(EffectKind.StartQuest, quest ?? throw new ArgumentNullException(nameof(quest)), 0);
    }

    public sealed class Objective
    {
        public ObjectiveKind Kind { get; }
        public string Id { get; }

        /// <summary>Nombre à atteindre (créatures, objets) ; 1 sinon.</summary>
        public long Value { get; }

        /// <summary>Sous-lot V1.6 : PNJ à qui livrer (<see cref="ObjectiveKind.Deliver"/>) ; null sinon.</summary>
        public string? Npc { get; }

        /// <summary>
        /// Relecture de la PR #76 : effet qui remet les objets livrés (<see cref="ObjectiveKind.Deliver"/> : <c>takeItem</c> de l'objet et de
        /// son nombre), appliqué avec les effets de l'étape ; vide pour les autres objectifs.
        /// </summary>
        public IReadOnlyList<StoryEffect> Delivery { get; }

        /// <summary>Sous-lot V1.9.3b : qualité minimale (<see cref="ObjectiveKind.CraftQuality"/>) ; normale pour les autres objectifs.</summary>
        public EdS.Data.Saves.ItemQuality Quality { get; }

        internal Objective(ObjectiveKind kind, string id, long value, string? npc = null,
            EdS.Data.Saves.ItemQuality quality = EdS.Data.Saves.ItemQuality.Normal)
        {
            Kind = kind;
            Id = id;
            Value = value;
            Npc = npc;
            Quality = quality;
            Delivery = Frozen.List(kind == ObjectiveKind.Deliver ? new[] { new StoryEffect(EffectKind.TakeItem, id, value) } : Array.Empty<StoryEffect>(), "delivery");
        }
    }

    public sealed class Npc
    {
        public string Id { get; }

        /// <summary>Clé du nom.</summary>
        public string Name { get; }

        public GroundVector Position { get; }
        public string Dialogue { get; }

        /// <summary>
        /// Sous-lot V1.6 : zone où vit le PNJ, s'il n'est pas dans celle de l'histoire (un intérieur : l'aubergiste, le prêtre, l'habitant) ;
        /// null : la zone de l'histoire. Sa position est dans cette zone.
        /// </summary>
        public string? Zone { get; }

        /// <summary>
        /// Sous-lot V2.3 (histoire en schéma 9) : emploi du temps du PNJ (vide : il reste à sa place, ou devant sa porte la nuit comme en
        /// V2.1). Les créneaux sont rangés par heure de début croissante ; le créneau en cours est le dernier commencé, celui de la veille
        /// avant le premier (la journée boucle à minuit).
        /// </summary>
        public IReadOnlyList<ScheduleSlot> Schedule { get; }

        internal Npc(string id, string name, GroundVector position, string dialogue, string? zone = null, IEnumerable<ScheduleSlot>? schedule = null)
        {
            Id = id;
            Name = name;
            Position = position;
            Dialogue = dialogue;
            Zone = zone;
            Schedule = Frozen.List(schedule ?? Array.Empty<ScheduleSlot>(), nameof(schedule));
            // Relecture indépendante de la PR #109 : calculées une fois (lues à chaque image par l'affichage).
            Zones = new System.Collections.ObjectModel.ReadOnlyCollection<string?>(new[] { Zone }.Concat(Schedule.Select(s => s.Zone)).Distinct().ToArray());
            Roams = Schedule.Any(s => s.Zone != Zone);
        }

        /// <summary>Sous-lot V2.3 : le créneau en cours à la minute <paramref name="minute"/> du jour (null : pas d'emploi du temps).</summary>
        public ScheduleSlot? SlotAt(int minute)
        {
            if (Schedule.Count == 0) return null;
            ScheduleSlot current = Schedule[Schedule.Count - 1];
            foreach (ScheduleSlot slot in Schedule)
                if (slot.Minute <= minute) current = slot;
                else break;
            return current;
        }

        /// <summary>
        /// Sous-lot V2.3.1 (schéma 10) : zone où se tient le PNJ à la minute <paramref name="minute"/> du jour : celle de son créneau en cours
        /// (même convention que <see cref="Zone"/> : null pour la zone de l'histoire), ou sa zone sans emploi du temps.
        /// </summary>
        public string? ZoneAt(int minute) => SlotAt(minute) is ScheduleSlot slot ? slot.Zone : Zone;

        /// <summary>
        /// Sous-lot V2.3.1 : zones où le PNJ peut se tenir dans la journée (la sienne d'abord, puis celles de ses créneaux, sans doublon ; null :
        /// la zone de l'histoire).
        /// </summary>
        public IReadOnlyList<string?> Zones { get; }

        /// <summary>Sous-lot V2.3.1 : le PNJ change de zone dans sa journée (un créneau ailleurs que chez lui).</summary>
        public bool Roams { get; }

        /// <summary>Sous-lot V2.3 : activités que l'emploi du temps peut donner (créneaux et abris), sans doublon, dans l'ordre du fichier.</summary>
        public IEnumerable<string> Activities => Schedule.SelectMany(s => s.Rain is null ? new[] { s.Activity } : new[] { s.Activity, s.Rain.Activity }).Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// Sous-lot V2.3 (histoire en schéma 9) : un créneau de l'emploi du temps d'un PNJ : à partir de <see cref="Minute"/> (minute du jour du
    /// monde, 0 à 1439), il est à <see cref="Position"/> pour l'activité <see cref="Activity"/> ; sous une vraie pluie (averse ou orage),
    /// à l'abri (<see cref="Rain"/>) s'il en a un.
    /// </summary>
    public sealed class ScheduleSlot
    {
        public int Minute { get; }
        public string Activity { get; }
        public GroundVector Position { get; }
        public SchedulePlace? Rain { get; }

        /// <summary>
        /// Sous-lot V2.3.1 (schéma 10) : zone de la place (et de l'abri) du créneau, même convention que <see cref="Npc.Zone"/> (null : la
        /// zone de l'histoire) ; celle du PNJ quand le fichier ne la donne pas.
        /// </summary>
        public string? Zone { get; }

        internal ScheduleSlot(int minute, string activity, GroundVector position, SchedulePlace? rain, string? zone = null)
        {
            Minute = minute;
            Activity = activity;
            Position = position;
            Rain = rain;
            Zone = zone;
        }
    }

    /// <summary>Sous-lot V2.3 : une place et son activité (l'abri d'un créneau sous la pluie).</summary>
    public sealed class SchedulePlace
    {
        public string Activity { get; }
        public GroundVector Position { get; }

        internal SchedulePlace(string activity, GroundVector position)
        {
            Activity = activity;
            Position = position;
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

        /// <summary>
        /// Sous-lot V2.3 (histoire en schéma 9) : répliques selon le moment : la première dont les conditions sont réunies remplace le texte
        /// du nœud (ses réponses restent les mêmes) ; aucune : le texte du nœud.
        /// </summary>
        public IReadOnlyList<DialogueLine> Lines { get; }

        internal DialogueNode(string id, string text, IEnumerable<DialogueChoice> choices, IEnumerable<DialogueLine>? lines = null)
        {
            Id = id;
            Text = text;
            Choices = Frozen.List(choices, nameof(choices));
            Lines = Frozen.List(lines ?? Array.Empty<DialogueLine>(), nameof(lines));
        }
    }

    /// <summary>Sous-lot V2.3 (histoire en schéma 9) : une réplique d'un nœud, dite quand ses conditions (au moins une) sont réunies.</summary>
    public sealed class DialogueLine
    {
        public IReadOnlyList<StoryCondition> Conditions { get; }
        public string Text { get; }

        internal DialogueLine(IEnumerable<StoryCondition> conditions, string text)
        {
            Conditions = Frozen.List(conditions, nameof(conditions));
            Text = text;
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

    /// <summary>Sous-lot V1.6 : une récompense au choix (un objet, en quantité), parmi deux ou trois.</summary>
    public sealed class RewardChoice
    {
        public string Item { get; }
        public int Count { get; }

        internal RewardChoice(string item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    public sealed class Quest
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<QuestStep> Steps { get; }
        public IReadOnlyList<StoryEffect> Effects { get; }

        /// <summary>Sous-lot V1.6 : onglet du journal (principale ou secondaire).</summary>
        public QuestCategory Category { get; }

        /// <summary>
        /// Sous-lot V1.6 : conditions pour que la quête s'ouvre (niveau, autre quête finie…) ; vide : toujours ouverte. Un effet
        /// <c>startQuest</c> sur une quête fermée est refusé ; une réponse de dialogue qui la commencerait n'est pas proposée.
        /// </summary>
        public IReadOnlyList<StoryCondition> Requires { get; }

        /// <summary>
        /// Sous-lot V1.6 : récompenses au choix (deux ou trois objets) ; vide : aucune. Quand la dernière étape est franchie, la quête attend
        /// le choix du joueur (étape réservée <see cref="Saves.QuestProgress.Reward"/>) avant d'être finie.
        /// </summary>
        public IReadOnlyList<RewardChoice> RewardChoices { get; }

        /// <summary>
        /// Sous-lot V1.9.7 (schéma 7) : la quête peut être passée par un effet <c>skipQuest</c> (le tutoriel de l'ouverture). Une quête qui
        /// n'est pas marquée ainsi ne peut pas l'être (refus du validateur), ni une épreuve de la guilde (refus du validateur de la guilde).
        /// </summary>
        public bool Skippable { get; }

        internal Quest(string id, string name, IEnumerable<QuestStep> steps, IEnumerable<StoryEffect> effects, QuestCategory category = QuestCategory.Main,
            IEnumerable<StoryCondition>? requires = null, IEnumerable<RewardChoice>? rewardChoices = null, bool skippable = false)
        {
            Skippable = skippable;
            Id = id;
            Name = name;
            Steps = Frozen.List(steps, nameof(steps));
            Effects = Frozen.List(effects, nameof(effects));
            Category = category;
            Requires = Frozen.List(requires ?? Array.Empty<StoryCondition>(), nameof(requires));
            RewardChoices = Frozen.List(rewardChoices ?? Array.Empty<RewardChoice>(), nameof(rewardChoices));
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

    /// <summary>
    /// Sous-lot V1.9.6 (histoire en schéma 6) : une scène jouée, comme le souvenir de la Rupture. La scène est une petite zone (un intérieur,
    /// <see cref="Zone"/>) où le joueur incarne quelqu'un d'autre (<see cref="Actor"/>, un texte : son nom à l'écran). Elle est <b>à jouer</b>
    /// quand le fait <see cref="Start"/> est appris et pas encore le fait <see cref="End"/> ; les portes qui mènent à sa zone ne s'ouvrent
    /// qu'alors (l'interface y fait entrer elle-même, elles ne se montrent pas). Elle est finie quand <see cref="End"/> est appris : par la
    /// victoire sur le chef de sa zone (qui l'apprend), ou en la passant (le texte <see cref="Text"/>, le repli, s'affiche alors) ; au retour,
    /// le texte <see cref="Back"/>.
    /// </summary>
    public sealed class Scene
    {
        public string Id { get; }
        public string Zone { get; }
        public string Actor { get; }
        public string Start { get; }
        public string End { get; }
        public string Text { get; }
        public string Back { get; }

        internal Scene(string id, string zone, string actor, string start, string end, string text, string back)
        {
            Id = id;
            Zone = zone;
            Actor = actor;
            Start = start;
            End = end;
            Text = text;
            Back = back;
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

        /// <summary>Sous-lot V1.9.6 (schéma 6) : scènes jouées (vide avant).</summary>
        public IReadOnlyList<Scene> Scenes { get; }

        /// <summary>Sous-lot V2.7 (schéma 12) : règles de réputation (<see cref="ReputationRules.None"/> sans section <c>reputation</c>).</summary>
        public ReputationRules Reputation { get; }

        internal StoryBook(int schemaVersion, string contentVersion, IEnumerable<Npc> npcs, IEnumerable<Dialogue> dialogues, IEnumerable<Quest> quests,
            IEnumerable<Discovery> discoveries, IEnumerable<Scene>? scenes = null, ReputationRules? reputation = null)
        {
            Reputation = reputation ?? ReputationRules.None;
            Scenes = Frozen.List(scenes ?? Array.Empty<Scene>(), nameof(scenes));
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

        /// <summary>Sous-lot V1.6 : PNJ qui vivent dans la zone <paramref name="zone"/> (null : la zone de l'histoire), dans l'ordre du fichier.</summary>
        public IEnumerable<Npc> NpcsIn(string? zone) => Npcs.Where(n => n.Zone == zone);

        /// <summary>
        /// Sous-lot V2.3.1 : PNJ qui peuvent se tenir dans la zone <paramref name="zone"/> à un moment de la journée (ceux qui y vivent et ceux
        /// dont un créneau y mène ; null : la zone de l'histoire), dans l'ordre de l'histoire.
        /// </summary>
        public IEnumerable<Npc> VisitorsOf(string? zone) => Npcs.Where(n => n.Zones.Contains(zone));

        /// <summary>Sous-lot V1.9.6 : scène jouée dans la zone <paramref name="zone"/>, ou null.</summary>
        public Scene? SceneIn(string? zone) => Scenes.FirstOrDefault(s => s.Zone == zone);
    }
}
