using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Guild;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Space;

namespace EdS.World
{
    public enum StoryEventKind
    {
        QuestStarted = 0,
        StepCompleted = 1,
        QuestCompleted = 2,
        FactLearned = 3,
        Discovered = 4,
        TrustChanged = 5,
        PassageOpened = 6,
        ItemGiven = 7,
        ItemTaken = 8,
        XpGiven = 9,

        /// <summary>Objectif atteint, mais ses récompenses ne tiennent pas dans l'inventaire : l'étape attend une place (annoncé une fois).</summary>
        RewardBlocked = 10,

        /// <summary>Sous-lot V1.1 : pièces données à la bourse (valeur en cuivre).</summary>
        MoneyGiven = 11,

        /// <summary>
        /// Sous-lot V1.1 : point sûr découvert (identifiant du point sûr de la zone de la partie) ; le voyage rapide y mène désormais.
        /// </summary>
        SafePointDiscovered = 12,

        /// <summary>
        /// Relecture de la PR #72 : objectif atteint, mais les pièces de ses récompenses dépasseraient la bourse (<see cref="Coins.Max"/>) :
        /// l'étape attend (annoncé une fois, comme <see cref="RewardBlocked"/>).
        /// </summary>
        PurseFull = 13,

        /// <summary>Sous-lot V1.6 : pièces prises à la bourse (valeur en cuivre).</summary>
        MoneyTaken = 14,

        /// <summary>
        /// Sous-lot V1.6 : objectif atteint, mais ses effets prennent plus de pièces que la bourse n'en a : l'étape attend (annoncé une fois,
        /// comme <see cref="RewardBlocked"/>).
        /// </summary>
        MoneyShort = 15,

        /// <summary>
        /// Sous-lot V1.6 : objectif atteint, mais ses effets commencent une quête encore fermée (niveau, autre quête) : l'étape attend (annoncé
        /// une fois).
        /// </summary>
        QuestLocked = 16,

        /// <summary>
        /// Sous-lot V1.6 : toutes les étapes de la quête sont franchies ; elle attend le choix de sa récompense
        /// (<see cref="Narrative.ChooseReward"/>), puis elle est finie.
        /// </summary>
        RewardToChoose = 17,

        /// <summary>Sous-lot V1.6 : objectif « parler à » ou « livrer à » rempli en parlant au PNJ (identifiant de la quête, étape).</summary>
        ObjectiveDone = 18,

        /// <summary>Sous-lot V1.7 : inscription à la Guilde des aventuriers (identifiant : le rang, « g »).</summary>
        GuildJoined = 19,

        /// <summary>
        /// Sous-lot V1.7 : points de guilde gagnés (valeur : points réellement ajoutés ; identifiant : ce qui les donne, une quête, un contrat,
        /// une découverte, une créature, un fait, ou « guild » pour un effet de l'histoire).
        /// </summary>
        GuildPoints = 20,

        /// <summary>Sous-lot V1.7 : épreuve réussie, promotion au rang (identifiant du rang).</summary>
        GuildPromoted = 21,

        /// <summary>Sous-lot V1.7 : contrat pris au tableau (identifiant du contrat).</summary>
        ContractTaken = 22,

        /// <summary>Sous-lot V1.7 : contrat abandonné (identifiant du contrat ; il ne se reprend pas avant le lendemain).</summary>
        ContractAbandoned = 23,

        /// <summary>Sous-lot V1.7 : contrat réussi, récompenses données (identifiant du contrat).</summary>
        ContractDone = 24,

        /// <summary>Sous-lot V1.7 : titre affiché choisi (identifiant du titre ; vide : plus aucun).</summary>
        TitleShown = 25,

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : un contrat pressé pas fini à la fin de la journée du monde où il a été pris est perdu, sans
        /// pénalité (identifiant du contrat).
        /// </summary>
        ContractExpired = 26,

        /// <summary>
        /// Réponses du 31 octobre 2026 : à l'inscription, les exploits d'avant (quêtes finies, découvertes, créatures rares vaincues, faits)
        /// comptent, une seule fois (valeur : points réellement ajoutés). La maîtresse le remarque : « Ta réputation te précède… ».
        /// </summary>
        GuildReputation = 27,

        /// <summary>
        /// Sous-lot V1.9.7 : une quête en cours a été passée (effet <c>skipQuest</c>, « Je sais me battre » chez Odile) : finie sans ses
        /// étapes restantes ni sa récompense.
        /// </summary>
        QuestSkipped = 28,
    }

    /// <summary>Événement narratif (journal de quête, journal des découvertes, confiance des PNJ).</summary>
    public sealed class StoryEvent
    {
        public StoryEventKind Kind { get; }
        public string Id { get; }

        /// <summary>Étape (quête), ou null.</summary>
        public string? Step { get; }

        /// <summary>Quantité (objets, expérience, pièces de cuivre) ou changement de confiance ; 0 sinon.</summary>
        public long Value { get; }

        internal StoryEvent(StoryEventKind kind, string id, string? step = null, long value = 0)
        {
            Kind = kind;
            Id = id;
            Step = step;
            Value = value;
        }

        public override string ToString() => $"{Kind} {Id}{(Step is null ? "" : "/" + Step)}{(Value == 0 ? "" : " " + Value)}";
    }

    public enum DialogueRefusal
    {
        None = 0,

        /// <summary>PNJ inconnu, ou aucune entrée de son dialogue ne convient (il n'a rien à dire).</summary>
        NothingToSay = 1,

        /// <summary>Trop loin du PNJ (ou personnage absent, mort).</summary>
        TooFar = 2,

        /// <summary>Pas de dialogue ouvert, ou réponse hors de la liste proposée.</summary>
        NoSuchChoice = 3,

        /// <summary>
        /// Les objets donnés ne tiennent pas dans l'inventaire, ceux demandés manquent, ou un passage n'a plus sa place au registre : rien n'a
        /// changé.
        /// </summary>
        NoRoom = 4,

        /// <summary>Relecture de la PR #72 : les pièces données dépasseraient la bourse (<see cref="Coins.Max"/>) : rien n'a changé.</summary>
        PurseFull = 5,

        /// <summary>Sous-lot V1.6 : les pièces demandées (<c>takeMoney</c>) manquent dans la bourse : rien n'a changé.</summary>
        NotEnoughMoney = 6,

        /// <summary>
        /// Sous-lot V1.6 : une quête à commencer est encore fermée (ses conditions d'ouverture manquent), ou il n'y a pas de récompense à
        /// choisir : rien n'a changé.
        /// </summary>
        Locked = 7,
    }

    /// <summary>
    /// Réponse proposée : son rang dans le nœud et la clé de son texte. Réponses de Damien du 31 octobre 2026 : une réponse de livraison
    /// (« Voici les peaux ») vient après celles du nœud ; elle dit ce qui est livré (<see cref="Item"/>, <see cref="Count"/>), à montrer avec
    /// son texte (<c>text.dialogue.livrer</c> : {0} le nombre, {1} le nom de l'objet).
    /// </summary>
    public sealed class DialogueOption
    {
        public int Index { get; }
        public string Text { get; }

        /// <summary>Livraison : la quête ou le contrat dont l'objectif <c>deliver</c> serait rempli ; null pour une réponse du nœud.</summary>
        public string? Delivery { get; }

        /// <summary>Livraison : l'objet livré, ou null.</summary>
        public string? Item { get; }

        /// <summary>Livraison : le nombre d'objets livrés ; 0 sinon.</summary>
        public long Count { get; }

        internal DialogueOption(int index, string text, string? delivery = null, string? item = null, long count = 0)
        {
            Index = index;
            Text = text;
            Delivery = delivery;
            Item = item;
            Count = count;
        }
    }

    /// <summary>Dialogue ouvert : PNJ, nœud, texte (clé), réponses dont les conditions sont réunies.</summary>
    public sealed class DialogueView
    {
        public string Npc { get; }
        public string Node { get; }
        public string Text { get; }
        public IReadOnlyList<DialogueOption> Options { get; }

        internal DialogueView(string npc, string node, string text, IReadOnlyList<DialogueOption> options)
        {
            Npc = npc;
            Node = node;
            Text = text;
            Options = (options ?? throw new ArgumentNullException(nameof(options))).ToList().AsReadOnly();
        }
    }

    /// <summary>Ce que l'histoire demande au monde de la partie (inventaire, expérience, passages, position du personnage).</summary>
    internal interface INarrativeWorld
    {
        Inventory Inventory { get; }
        void GiveXp(long xp);
        bool OpenPassage(string passage);
        bool IsPassageOpen(string passage);

        /// <summary>Passages qu'on peut encore ouvrir pour de bon (places libres au registre du monde).</summary>
        int PassageRoom { get; }
        DayPhase Phase { get; }

        /// <summary>Sous-lot V2.2 : le temps qu'il fait dehors (conditions <c>weather</c>).</summary>
        WeatherKind Weather { get; }

        /// <summary>Position du personnage s'il est présent et vivant ; null sinon.</summary>
        GroundVector? PlayerPosition { get; }

        bool InPlace(string place);

        /// <summary>Sous-lot V1.1 : bourse du personnage, en pièces de cuivre.</summary>
        long Money { get; }

        /// <summary>Sous-lot V1.1 : ajoute des pièces à la bourse ; faux, sans rien changer, si elle dépasserait <see cref="Coins.Max"/>.</summary>
        bool GiveMoney(long copper);

        /// <summary>Sous-lot V1.6 : prend des pièces à la bourse ; faux, sans rien changer, si elle n'en a pas assez.</summary>
        bool TakeMoney(long copper);

        /// <summary>Sous-lot V1.6 : niveau du personnage d'après son expérience.</summary>
        int Level { get; }

        /// <summary>
        /// Relecture de la PR #76 : niveau qu'aurait le personnage avec <paramref name="xp"/> points d'expérience de plus (plafonnés comme un
        /// vrai gain) ; sert à juger l'ouverture d'une quête commencée par les mêmes effets qu'un gain d'expérience.
        /// </summary>
        int LevelWith(long xp);

        /// <summary>Sous-lot V1.6 : l'objet est porté (équipement).</summary>
        bool IsEquipped(string item);

        /// <summary>
        /// Sous-lot V1.6 : position du personnage, présent et vivant, s'il est dans la zone <paramref name="zone"/> (null : la zone de
        /// l'histoire) ; null sinon. Sert à parler aux PNJ, qui vivent dehors ou dans un intérieur.
        /// </summary>
        GroundVector? PlayerPositionIn(string? zone);

        /// <summary>Sous-lot V2.1 : place du PNJ à l'heure du monde (devant sa porte la nuit, s'il rentre chez lui).</summary>
        GroundVector NpcPosition(Npc npc);

        /// <summary>Sous-lot V2.3 : activité du PNJ à l'heure du monde et sous le temps qu'il fait (null : pas d'emploi du temps).</summary>
        string? NpcActivity(Npc npc);

        /// <summary>
        /// Sous-lot V2.3.1 : zone où se tient le PNJ à l'heure du monde (celle de son créneau en cours ; null : la zone de l'histoire), comme
        /// <see cref="Npc.Zone"/> : on lui parle quand on est dans cette zone.
        /// </summary>
        string? NpcZone(Npc npc);

        /// <summary>Réponses du 31 octobre 2026 : jour du monde (fin des contrats pressés).</summary>
        long Day { get; }

        /// <summary>Relecture de la PR #81 : jour du monde du tick qui vient d'être simulé (avant le premier tick : <see cref="Day"/>).</summary>
        long SimulatedDay { get; }
    }

    /// <summary>
    /// Sous-lot 12.5 (cadrage H1, H2, H3, Q2) : l'histoire d'un personnage. État personnel (portée « parcours personnel », H2) : faits
    /// appris, avancement des quêtes, confiance des PNJ, découvertes ; dialogues aux réponses conditionnelles et aux conséquences (confiance,
    /// faits, objets, quêtes, passages) ; quêtes à étapes, dont l'objectif est vérifié à chaque tick. Chaque application d'effets est
    /// atomique : des objets qui ne tiennent pas, ou qui manquent, laissent tout inchangé.
    /// </summary>
    public sealed class Narrative
    {
        /// <summary>Distance de parole : écart entre le personnage et le PNJ, au plus (provisoire).</summary>
        public static readonly EdS.Domain.Numerics.Fixed TalkRange = EdS.Domain.Numerics.Fixed.FromInt(3);

        private readonly StoryBook _book;
        private readonly INarrativeWorld _world;
        private readonly SortedSet<string> _facts;
        private readonly SortedDictionary<string, (int Step, long Progress)> _quests = new SortedDictionary<string, (int, long)>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, int> _trust;
        private readonly SortedSet<string> _discoveries;
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Dialogue ouvert ; réponses du 31 octobre 2026 : nœud null quand le PNJ n'a rien à dire mais qu'on peut lui livrer.</summary>
        private (string Npc, Dialogue Dialogue, DialogueNode? Node)? _open;

        /// <summary>Sous-lot V1.9.10 : fait « déjà rencontré » appris à l'ouverture du dialogue en cours (premier dialogue avec ce PNJ), ou null.</summary>
        private string? _firstMet;

        /// <summary>Réponses du 31 octobre 2026 : texte de la réponse de livraison (« Voici : {1} ×{0}. »).</summary>
        public const string DeliverText = "text.dialogue.livrer";

        /// <summary>Réponses du 31 octobre 2026 : ce que dit un PNJ qui n'a rien d'autre à dire, quand on peut lui livrer.</summary>
        public const string DeliveryOnlyText = "text.dialogue.livraison";

        /// <summary>Sous-lot V1.6 : quête suivie (son objectif sur la carte), ou null.</summary>
        private string? _tracked;

        /// <summary>Sous-lot V1.6 : événements des livraisons et des objectifs « parler à », rendus au tick suivant (<see cref="Step"/>).</summary>
        private readonly List<StoryEvent> _queued = new List<StoryEvent>();

        /// <summary>Relecture de la PR #76 : expérience que donnent les effets jugés (l'ouverture d'une quête se juge après elle).</summary>
        private long _bonusXp;

        /// <summary>
        /// Relecture de la PR #76 : objectifs « parler à » et « livrer à » remplis par le dernier <see cref="Talk"/> (l'interface ne dit pas « rien
        /// à dire » à un PNJ qui vient d'en remplir un).
        /// </summary>
        public int LastTalkFilled { get; private set; }

        /// <summary>
        /// Relecture de la PR #76 : compteur qui change à chaque changement de l'état narratif (l'interface garde son journal tant qu'il ne
        /// bouge pas).
        /// </summary>
        public long Revision { get; private set; }

        public StoryBook Book => _book;

        /// <summary>Sous-lot V1.7 : la Guilde des aventuriers (null : partie sans guilde ; la carte sauvegardée est alors gardée telle quelle).</summary>
        public GuildBook? Guild => _guild;

        private readonly GuildBook? _guild;

        /// <summary>Sous-lot V1.7 : carte d'aventurier (null : pas inscrit, ou partie sans guilde).</summary>
        private GuildCard? _card;

        /// <summary>Sous-lot V1.7 : carte sauvegardée d'une partie sans guilde, rendue telle quelle à la sauvegarde.</summary>
        private readonly SavedGuildCard? _keptCard;

        /// <summary>
        /// Relecture de la PR #79 : créatures rares de la guilde déjà vaincues, inscrit ou non (une victoire d'avant l'inscription ne rapporte
        /// jamais de points plus tard).
        /// </summary>
        private readonly SortedSet<string> _hunted;

        internal Narrative(StoryBook book, SavedStory saved, INarrativeWorld world, GuildBook? guild = null)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            // Sous-lot V1.9.5 : l'avancement de l'ancienne mission est converti à la lecture (sa fin est passée au chapitre 5).
            saved = StoryConversions.Upgrade(book, saved ?? throw new ArgumentNullException(nameof(saved)));
            CheckSaved(book, saved, guild);
            _guild = guild;
            _hunted = new SortedSet<string>(saved.Hunted, StringComparer.Ordinal);
            if (saved.Guild != null)
            {
                if (guild is null) _keptCard = saved.Guild;
                else _card = GuildCard.From(saved.Guild);
            }
            _facts = new SortedSet<string>(saved.Facts, StringComparer.Ordinal);
            _trust = new SortedDictionary<string, int>(saved.Trust.ToDictionary(t => t.Key, t => t.Value), StringComparer.Ordinal);
            _discoveries = new SortedSet<string>(saved.Discoveries, StringComparer.Ordinal);
            foreach (KeyValuePair<string, QuestProgress> q in saved.Quests)
            {
                Quest quest = book.Quest(q.Key);
                // Sous-lot V1.6 : une quête qui attend le choix de sa récompense est après sa dernière étape.
                int step = q.Value.IsDone ? -1 : q.Value.IsChoosingReward ? quest.Steps.Count : quest.StepIndex(q.Value.Step);
                _quests[q.Key] = (step, q.Value.Progress);
            }
            _tracked = saved.Tracked != null && IsActive(saved.Tracked) ? saved.Tracked : null;
        }

        /// <summary>
        /// L'état narratif sauvegardé va avec cette histoire : chaque quête existe, à une étape connue. Contrôlé avant l'ouverture de la
        /// session (revue Codex de la PR #47 : une session refusée n'a rien écrit).
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde qui ne va pas avec ces données.</exception>
        internal static void CheckSaved(StoryBook book, SavedStory saved, GuildBook? guild = null)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            saved = StoryConversions.Upgrade(book, saved);   // sous-lot V1.9.5 : contrôlé tel qu'il sera lu
            // Sous-lot V1.7 : la carte d'aventurier va avec la guilde chargée : rang ouvert, titres et contrats connus.
            if (guild != null && saved.Guild is SavedGuildCard card)
            {
                if (!guild.Rank(card.Rank).Open) throw new ArgumentException($"Carte d'aventurier au rang fermé « {card.Rank} ».", nameof(saved));
                foreach (string title in card.Titles)
                    if (!guild.TryGetTitle(title, out _)) throw new ArgumentException($"Titre sauvegardé « {title} » absent de la guilde.", nameof(saved));
                foreach (string contract in card.Active.Keys.Concat(card.Taken))
                    if (!guild.TryGetContract(contract, out _)) throw new ArgumentException($"Contrat sauvegardé « {contract} » absent de la guilde.", nameof(saved));
            }
            foreach (KeyValuePair<string, QuestProgress> q in saved.Quests)
            {
                // Une quête sauvegardée doit exister, à une étape connue : sinon la sauvegarde ne va pas avec ces données.
                Quest quest = book.HasQuest(q.Key) ? book.Quest(q.Key) : throw new ArgumentException($"Quête sauvegardée « {q.Key} » absente de l'histoire.", nameof(saved));
                if (q.Value.IsChoosingReward)
                {
                    if (quest.RewardChoices.Count == 0) throw new ArgumentException($"« {q.Key} » n'a pas de récompense au choix.", nameof(saved));
                }
                else if (!q.Value.IsDone && quest.StepIndex(q.Value.Step) < 0) throw new ArgumentException($"Étape « {q.Value.Step} » inconnue de « {q.Key} ».", nameof(saved));
            }
            // Sous-lot V1.6 : la quête suivie est en cours.
            if (saved.Tracked != null && !(saved.Quests.TryGetValue(saved.Tracked, out QuestProgress tracked) && !tracked.IsDone))
                throw new ArgumentException($"Quête suivie « {saved.Tracked} » pas en cours.", nameof(saved));
        }

        // Vues en lecture seule : l'histoire ne change que par ses opérations.
        public IReadOnlyCollection<string> Facts => _facts.ToList().AsReadOnly();
        public IReadOnlyCollection<string> Discoveries => _discoveries.ToList().AsReadOnly();
        public int Trust(string npc) => _trust.TryGetValue(npc, out int t) ? t : 0;
        public bool Knows(string fact) => _facts.Contains(fact);
        public bool IsDone(string quest) => _quests.TryGetValue(quest, out var q) && q.Step < 0;
        public bool IsActive(string quest) => _quests.TryGetValue(quest, out var q) && q.Step >= 0;

        /// <summary>Étape en cours d'une quête active ; null sinon (et pour une quête qui attend le choix de sa récompense).</summary>
        public QuestStep? CurrentStep(string quest) =>
            _quests.TryGetValue(quest, out var q) && q.Step >= 0 && q.Step < _book.Quest(quest).Steps.Count ? _book.Quest(quest).Steps[q.Step] : null;

        /// <summary>Sous-lot V1.6 : la quête a franchi toutes ses étapes et attend le choix de sa récompense.</summary>
        public bool IsChoosingReward(string quest) => _quests.TryGetValue(quest, out var q) && q.Step >= 0 && q.Step == _book.Quest(quest).Steps.Count;

        /// <summary>
        /// Sous-lot V1.6 : la quête peut commencer : pas encore commencée, et ses conditions d'ouverture réunies (niveau, autre quête…).
        /// </summary>
        public bool IsOpen(string quest) => !_quests.ContainsKey(quest) && All(_book.Quest(quest).Requires) && TrialAllows(quest);

        /// <summary>
        /// Sous-lot V1.7 : une épreuve de promotion ne s'ouvre que pour un inscrit du rang juste en dessous, avec assez de points (une autre
        /// quête n'est pas une épreuve : toujours vrai).
        /// </summary>
        private bool TrialAllows(string quest) => !(_guild?.TrialOf(quest) is GuildRank rank) || TrialOpenFor(rank);

        private bool TrialOpenFor(GuildRank rank) => _card != null && rank.Open && _card.Rank + 1 == rank.Index && _card.Points >= rank.Points;

        /// <summary>Sous-lot V1.6 : quête suivie (son objectif sur la carte), ou null.</summary>
        public string? Tracked => _tracked;

        /// <summary>
        /// Sous-lot V1.6 : suit la quête <paramref name="quest"/> (en cours), ou plus aucune (null). Faux, sans rien changer, pour une quête qui
        /// n'est pas en cours.
        /// </summary>
        public bool Track(string? quest)
        {
            if (quest != null && !IsActive(quest)) return false;
            _tracked = quest;
            Revision++;
            return true;
        }

        public long Progress(string quest) => _quests.TryGetValue(quest, out var q) ? q.Progress : 0;

        /// <summary>Dialogue ouvert, ou null.</summary>
        public DialogueView? Open => _open is { } o ? View(o.Npc, o.Node) : null;

        /// <summary>Sous-lot V2.3.1 : PNJ du dialogue ouvert, ou null.</summary>
        public string? OpenNpc => _open?.Npc;

        /// <summary>
        /// Sous-lot V2.3.1 : nombre de dialogues ouverts depuis le chargement (chaque Talk accepté en ouvre un neuf) : distingue deux dialogues
        /// de suite avec le même PNJ. Jamais sauvegardé.
        /// </summary>
        public long Opened { get; private set; }

        public SavedStory ToSaved() => new SavedStory(_facts,
            _quests.ToDictionary(q => q.Key, q => new QuestProgress(q.Value.Step < 0 ? QuestProgress.Done
                    : q.Value.Step == _book.Quest(q.Key).Steps.Count ? QuestProgress.Reward : _book.Quest(q.Key).Steps[q.Value.Step].Id, q.Value.Progress),
                StringComparer.Ordinal),
            _trust, _discoveries, _tracked, _card?.ToSaved() ?? _keptCard, _hunted);

        // --- Dialogues ---------------------------------------------------------------------------

        /// <summary>Parle à un PNJ : à portée, le personnage présent et vivant ; ouvre le nœud de la première entrée qui convient.</summary>
        public DialogueRefusal Talk(string npc, out DialogueView? view)
        {
            // V1.9.10 : un nouveau Talk accepté (même PNJ, sans Close) repart d'un dialogue neuf ; un Talk refusé laisse le dialogue ouvert
            // (et son marqueur de premier accueil) tel qu'il était.
            string? previous = _firstMet;
            _firstMet = null;
            DialogueRefusal refusal = TalkCore(npc, out view);
            if (refusal != DialogueRefusal.None) _firstMet = previous;
            return refusal;
        }

        private DialogueRefusal TalkCore(string npc, out DialogueView? view)
        {
            view = null;
            if (npc is null || !_book.HasNpc(npc)) return DialogueRefusal.NothingToSay;
            Npc n = _book.Npc(npc);
            if (!InRange(n)) return DialogueRefusal.TooFar;
            // Sous-lot V1.6 : parler au PNJ remplit les objectifs « parler à » de lui en cours (avant son dialogue, qui peut en dépendre).
            // Réponses du 31 octobre 2026 : la livraison n'est plus automatique ; c'est une réponse du dialogue.
            Spoke(npc);
            Dialogue d = _book.Dialogue(n.Dialogue);
            DialogueEntry? entry = d.Entries.FirstOrDefault(e => All(e.Conditions));
            if (entry is null)
            {
                // Rien à dire, mais des objets à lui livrer : le dialogue s'ouvre sur les seules réponses de livraison.
                if (Deliveries(npc).Count == 0) return DialogueRefusal.NothingToSay;
                _open = (npc, d, null);
            }
            else _open = (npc, d, d.Node(entry.Node));
            Opened++;
            // Sous-lot V1.9.10 : premier dialogue avec ce PNJ ? Son fait « déjà rencontré » est appris maintenant (rien d'autre ne change : la
            // sauvegarde garde un fait de plus), mais reste « inconnu » pour les conditions tant que ce dialogue est ouvert (Known).
            string met = StoryValidator.MetFact(npc);
            _firstMet = _facts.Add(met) ? met : null;
            if (_firstMet != null) Revision++;
            view = View(npc, _open.Value.Node);
            return DialogueRefusal.None;
        }

        /// <summary>
        /// Choisit la réponse de rang <paramref name="index"/> (parmi les réponses proposées) : ses effets s'appliquent en entier ou pas du tout,
        /// puis le dialogue passe au nœud suivant, ou se ferme. Trop loin du PNJ : le dialogue se ferme, rien ne change.
        /// </summary>
        public DialogueRefusal Choose(int index, List<StoryEvent> events, out DialogueView? next)
        {
            next = null;
            if (events is null) throw new ArgumentNullException(nameof(events));
            if (_open is not { } o) return DialogueRefusal.NoSuchChoice;
            if (!InRange(_book.Npc(o.Npc)))
            {
                _open = null;
                _firstMet = null;
                return DialogueRefusal.TooFar;
            }
            int own = o.Node?.Choices.Count ?? 0;
            if (index >= own) return ChooseDelivery(o.Npc, index - own, out next);
            if (index < 0 || !All(o.Node!.Choices[index].Conditions)) return DialogueRefusal.NoSuchChoice;
            DialogueChoice choice = o.Node.Choices[index];
            DialogueRefusal applied = Apply(choice.Effects, events);
            if (applied != DialogueRefusal.None) return applied;
            if (choice.Next is null) { _open = null; _firstMet = null; }
            else
            {
                _open = (o.Npc, o.Dialogue, o.Dialogue.Node(choice.Next));
                next = View(o.Npc, _open.Value.Node);
            }
            return DialogueRefusal.None;
        }

        public void Close()
        {
            _open = null;
            _firstMet = null;
        }

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : choisit la réponse de livraison de rang <paramref name="k"/>. Les objets, toujours dans le
        /// sac et libres, sont promis ; ils sont remis au tick suivant avec les effets de l'étape (ou les récompenses du contrat), en une seule
        /// application atomique, comme avant : tout ou rien, les objets de base d'abord, jamais les potions réservées. Le dialogue reste ouvert
        /// (sans cette réponse) ; s'il n'avait que des livraisons et qu'il n'en reste plus, il se ferme.
        /// </summary>
        private DialogueRefusal ChooseDelivery(string npc, int k, out DialogueView? next)
        {
            next = null;
            IReadOnlyList<(string Id, bool Contract, Objective Objective)> list = Deliveries(npc);
            if (k < 0 || k >= list.Count) return DialogueRefusal.NoSuchChoice;
            (string id, bool contract, Objective objective) = list[k];
            // Relecture de la PR #81 : jamais plus d'objets promis que d'objets libres (deux livraisons du même objet, une quête et un contrat
            // ou un contrat normal et un pressé) ; sinon, rien n'est promis.
            if (checked(Promised(objective.Id) + objective.Value) > _world.Inventory.Available(objective.Id)) return DialogueRefusal.NoRoom;
            if (contract) _card!.Active[id] = 1;
            else
            {
                _quests[id] = (_quests[id].Step, 1L);
                _queued.Add(new StoryEvent(StoryEventKind.ObjectiveDone, id, CurrentStep(id)!.Id));
            }
            Revision++;
            (string Npc, Dialogue Dialogue, DialogueNode? Node) o = _open!.Value;
            if (o.Node is null && Deliveries(npc).Count == 0) { _open = null; _firstMet = null; }
            else next = View(npc, o.Node);
            return DialogueRefusal.None;
        }

        /// <summary>
        /// Réponses du 31 octobre 2026 : livraisons qu'on peut faire maintenant à <paramref name="npc"/> : étapes en cours « livrer à » lui (ordre
        /// ordinal des quêtes), puis contrats de livraison en cours à lui (ordre ordinal), pas encore promises, les objets dans le sac et libres
        /// (pas réservés pour le combat). Relecture de la PR #81 : les objets déjà promis à une autre livraison (à n'importe quel PNJ) ne sont plus
        /// libres pour celle-ci (<see cref="Promised"/>) : deux livraisons du même objet ne se promettent pas les mêmes objets.
        /// </summary>
        internal IReadOnlyList<(string Id, bool Contract, Objective Objective)> Deliveries(string npc)
        {
            var list = new List<(string, bool, Objective)>();
            bool Free(Objective o) => _world.Inventory.Available(o.Id) - Promised(o.Id) >= o.Value;
            foreach (KeyValuePair<string, (int Step, long Progress)> q in _quests)
                if (CurrentStep(q.Key) is { Objective: { Kind: ObjectiveKind.Deliver } o } && o.Npc == npc && q.Value.Progress < 1 && Free(o))
                    list.Add((q.Key, false, o));
            if (_card != null && _guild != null)
                foreach (KeyValuePair<string, long> a in _card.Active)
                    if (_guild.Contract(a.Key).Objective is { Kind: ObjectiveKind.Deliver } o && o.Npc == npc && a.Value < 1 && Free(o) && !Expired(a.Key, _world.Day))
                        list.Add((a.Key, true, o));
            return list;
        }

        /// <summary>
        /// Relecture de la PR #81 : objets <paramref name="item"/> déjà promis à des livraisons en cours (étapes « livrer à » et contrats de
        /// livraison remplis, pas encore remis), remis au tick suivant.
        /// </summary>
        private long Promised(string item)
        {
            long n = 0;
            foreach (KeyValuePair<string, (int Step, long Progress)> q in _quests)
                if (q.Value.Progress >= 1 && CurrentStep(q.Key) is { Objective: { Kind: ObjectiveKind.Deliver } o } && o.Id == item) n = checked(n + o.Value);
            if (_card != null && _guild != null)
                foreach (KeyValuePair<string, long> a in _card.Active)
                    if (a.Value >= 1 && _guild.Contract(a.Key).Objective is { Kind: ObjectiveKind.Deliver } o && o.Id == item && !Expired(a.Key, _world.Day))
                        n = checked(n + o.Value);
            return n;
        }

        /// <summary>
        /// Réponses proposées : leurs conditions réunies, et (sous-lot V1.6) aucune quête encore fermée à commencer (une réponse qui ouvrirait
        /// une quête avant son niveau n'apparaît pas). Réponses du 31 octobre 2026 : puis une réponse par livraison possible à ce PNJ
        /// (<see cref="Deliveries"/>), de rang après celles du nœud. Sans nœud (rien d'autre à dire), les seules livraisons.
        /// </summary>
        private DialogueView View(string npc, DialogueNode? node)
        {
            var options = new List<DialogueOption>();
            if (node != null)
                options.AddRange(node.Choices.Select((c, i) => (c, i)).Where(x => All(x.c.Conditions) && !StartsLocked(x.c.Effects))
                    .Select(x => new DialogueOption(x.i, x.c.Text)));
            int own = node?.Choices.Count ?? 0;
            IReadOnlyList<(string Id, bool Contract, Objective Objective)> deliveries = Deliveries(npc);
            for (int k = 0; k < deliveries.Count; k++)
                options.Add(new DialogueOption(own + k, DeliverText, deliveries[k].Id, deliveries[k].Objective.Id, deliveries[k].Objective.Value));
            // Sous-lot V2.3 : la première réplique du moment dont les conditions sont réunies remplace le texte du nœud.
            string text = node is null ? DeliveryOnlyText : node.Lines.FirstOrDefault(l => All(l.Conditions))?.Text ?? node.Text;
            return new DialogueView(npc, node?.Id ?? string.Empty, text, options);
        }

        /// <summary>
        /// Une quête à commencer par ces effets est encore fermée. Relecture de la PR #76 : jugé après l'expérience que donnent ces mêmes effets
        /// (une réponse qui donne le niveau 3 et commence une quête du niveau 3 passe).
        /// </summary>
        private bool StartsLocked(IReadOnlyList<StoryEffect> effects)
        {
            if (!effects.Any(e => e.Kind == EffectKind.StartQuest)) return false;
            long xp = 0;
            foreach (StoryEffect e in effects)
                if (e.Kind == EffectKind.GiveXp) xp = checked(xp + e.Value);   // au plus huit effets bornés par le validateur
            _bonusXp = xp;
            try
            {
                return effects.Any(e => e.Kind == EffectKind.StartQuest && !_quests.ContainsKey(e.Id) && (!All(_book.Quest(e.Id).Requires) || !TrialAllows(e.Id)));
            }
            finally
            {
                _bonusXp = 0;
            }
        }

        private bool InRange(Npc n) =>
            _world.PlayerPositionIn(_world.NpcZone(n)) is GroundVector p && !ZonePopulations.Beyond(p, EdS.Domain.Numerics.Fixed.Zero, _world.NpcPosition(n), EdS.Domain.Numerics.Fixed.Zero, TalkRange);

        /// <summary>
        /// Sous-lot V1.6 : le personnage parle à <paramref name="npc"/> : chaque étape en cours « parler à » ce PNJ est remplie (ordre ordinal
        /// des quêtes) ; l'étape se franchit au tick suivant, avec ses effets. Réponses de Damien du 31 octobre 2026 : les étapes « livrer à »
        /// et les contrats de livraison ne se remplissent plus en parlant, mais par une réponse du dialogue (<see cref="Deliveries"/>).
        /// </summary>
        private void Spoke(string npc)
        {
            LastTalkFilled = 0;
            foreach (string id in _quests.Keys.ToArray())
            {
                if (!(CurrentStep(id) is QuestStep s) || _quests[id].Progress >= 1) continue;
                if (s.Objective.Kind != ObjectiveKind.Talk || s.Objective.Id != npc) continue;
                _quests[id] = (_quests[id].Step, 1L);
                _queued.Add(new StoryEvent(StoryEventKind.ObjectiveDone, id, s.Id));
                LastTalkFilled++;
                Revision++;
            }
        }

        /// <summary>
        /// Sous-lot V1.7 : le personnage s'adresse à <paramref name="npc"/> sans ouvrir son dialogue (le tableau de la guilde chez la maîtresse) :
        /// ses objectifs « parler à » sont remplis comme en lui parlant (<see cref="Talk"/>) ; les livraisons se font par son dialogue (réponses du
        /// 31 octobre 2026). Rend le nombre d'objectifs remplis.
        /// </summary>
        internal int Visit(string npc)
        {
            if (npc is null || !_book.HasNpc(npc) || !InRange(_book.Npc(npc))) return 0;
            Spoke(npc);
            return LastTalkFilled;
        }

        /// <summary>
        /// Sous-lot V1.6 : choisit la récompense de rang <paramref name="index"/> d'une quête qui l'attend : l'objet doit tenir dans
        /// l'inventaire (sinon <see cref="DialogueRefusal.NoRoom"/>, rien ne change), puis la quête est finie. Pas de choix en attente, ou rang
        /// hors de la liste : <see cref="DialogueRefusal.Locked"/> ou <see cref="DialogueRefusal.NoSuchChoice"/>.
        /// </summary>
        public DialogueRefusal ChooseReward(string quest, int index, List<StoryEvent> events)
        {
            if (events is null) throw new ArgumentNullException(nameof(events));
            if (quest is null || !_book.HasQuest(quest) || !IsChoosingReward(quest)) return DialogueRefusal.Locked;
            IReadOnlyList<RewardChoice> choices = _book.Quest(quest).RewardChoices;
            if (index < 0 || index >= choices.Count) return DialogueRefusal.NoSuchChoice;
            RewardChoice c = choices[index];
            if (_world.Inventory.Add(c.Item, c.Count) != InventoryRefusal.None) return DialogueRefusal.NoRoom;
            _quests[quest] = (-1, 0L);
            Revision++;
            if (_tracked == quest) _tracked = null;
            events.Add(new StoryEvent(StoryEventKind.ItemGiven, c.Item, null, c.Count));
            events.Add(new StoryEvent(StoryEventKind.QuestCompleted, quest));
            QuestFinished(quest, events);   // sous-lot V1.7 : points de guilde
            return DialogueRefusal.None;
        }

        // --- Quêtes ------------------------------------------------------------------------------

        /// <summary>Sous-lot 12.7 : fait appris par le monde (victoire sur le chef) ; faux s'il l'était déjà.</summary>
        internal bool Learn(string fact, List<StoryEvent> events)
        {
            if (!_facts.Add(fact)) return false;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.FactLearned, fact));
            if (_guild != null && _guild.FactPoints.TryGetValue(fact, out long points)) Award(points, fact, events);   // sous-lot V1.7
            return true;
        }

        /// <summary>
        /// Une créature de cette population a été vaincue (mort attribuée) : compte pour les objectifs « defeat » en cours, et (sous-lot V1.6)
        /// pour les objectifs « defeatFamily » de sa famille. Le compteur s'arrête au nombre demandé.
        /// </summary>
        internal void Defeated(string population, string? family = null)
        {
            bool Counts(Objective o) => (o.Kind == ObjectiveKind.Defeat && o.Id == population) || (o.Kind == ObjectiveKind.DefeatFamily && family != null && o.Id == family);
            foreach (string id in _quests.Keys.ToArray())
                if (CurrentStep(id) is { Objective: { } o } && Counts(o) && _quests[id].Progress < o.Value)
                {
                    _quests[id] = (_quests[id].Step, checked(_quests[id].Progress + 1));
                    Revision++;
                }
            // Sous-lot V1.7 : la première victoire sur une créature rare de la guilde est gardée, inscrit ou non (relecture de la PR #79) ; une fois
            // inscrit, elle donne ses points (annoncés au tick) ; avant l'inscription, rien sur le moment : elle comptera une fois, à l'inscription
            // (réponses de Damien du 31 octobre 2026, ReputationPoints).
            if (_guild is null) return;
            if (_guild.RarePoints.TryGetValue(population, out long rare) && _hunted.Count < SavedStory.MaxHunted && _hunted.Add(population))
            {
                Revision++;
                Award(rare, population, _queued);
            }
            // Les contrats de chasse et les primes en cours comptent comme les quêtes.
            if (_card is null) return;
            foreach (string id in _card.Active.Keys.ToArray())
            {
                Objective o = _guild.Contract(id).Objective;
                if (Counts(o) && _card.Active[id] < o.Value)
                {
                    _card.Active[id] = checked(_card.Active[id] + 1);
                    Revision++;
                }
            }
        }

        /// <summary>
        /// Sous-lot V1.9.6 (tutoriel de l'ouverture) : le joueur a fait <paramref name="count"/> fois le geste <paramref name="action"/>
        /// (<c>strike</c>, <c>guard</c>, <c>parry</c>, <c>drink</c>) : compte pour les objectifs « practice » en cours de ce geste, jusqu'au
        /// nombre demandé.
        /// </summary>
        internal void Practiced(string action, long count)
        {
            if (count <= 0) return;
            foreach (string id in _quests.Keys.ToArray())
                if (CurrentStep(id) is { Objective: { Kind: ObjectiveKind.Practice } o } && o.Id == action && _quests[id].Progress < o.Value)
                {
                    _quests[id] = (_quests[id].Step, Math.Min(o.Value, checked(_quests[id].Progress + count)));
                    Revision++;
                }
        }

        /// <summary>Relecture de la PR #94 : une quête en cours demande encore le geste <paramref name="action"/> à son étape actuelle.</summary>
        internal bool WantsPractice(string action) =>
            _quests.Any(q => CurrentStep(q.Key) is { Objective: { Kind: ObjectiveKind.Practice } o } && o.Id == action && q.Value.Progress < o.Value);

        /// <summary>
        /// Sous-lot V1.9.6 : la scène à jouer (son fait de début appris, pas encore celui de fin), la première dans l'ordre du fichier ; null
        /// s'il n'y en a pas.
        /// </summary>
        public Scene? PendingScene => _book.Scenes.FirstOrDefault(sc => _facts.Contains(sc.Start) && !_facts.Contains(sc.End));

        /// <summary>
        /// Sous-lot V1.9.6 : passe la scène <paramref name="scene"/> à jouer : son fait de fin est appris (annoncé au tick suivant), comme si
        /// elle avait été jouée. Faux, sans rien changer, si elle n'est pas à jouer.
        /// </summary>
        internal bool SkipScene(string scene)
        {
            if (!(PendingScene is Scene s) || s.Id != scene) return false;
            return Learn(s.End, _queued);
        }

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : une quête en cours (étape actuelle) ou un contrat en cours demande encore de vaincre cette
        /// créature (<c>defeat</c>) ou une créature de sa famille (<c>defeatFamily</c>). Une créature rare de la nuit vient alors à coup sûr.
        /// </summary>
        internal bool Hunts(string population, string family)
        {
            bool Wants(Objective o, long progress) => progress < o.Value
                && ((o.Kind == ObjectiveKind.Defeat && o.Id == population) || (o.Kind == ObjectiveKind.DefeatFamily && o.Id == family));
            foreach (KeyValuePair<string, (int Step, long Progress)> q in _quests)
                if (CurrentStep(q.Key) is { Objective: { } o } && Wants(o, q.Value.Progress)) return true;
            if (_card is null || _guild is null) return false;
            foreach (KeyValuePair<string, long> a in _card.Active)
                if (Wants(_guild.Contract(a.Key).Objective, a.Value)) return true;
            return false;
        }

        /// <summary>Relecture de la PR #79 : créatures rares de la guilde déjà vaincues (ordre ordinal).</summary>
        public IReadOnlyList<string> Hunted => _hunted.ToList().AsReadOnly();

        /// <summary>Vérifie l'objectif de l'étape en cours de chaque quête (ordre ordinal) ; une étape au plus par quête et par tick.</summary>
        internal void Step(List<StoryEvent> events)
        {
            // Sous-lot V1.6 : livraisons et objectifs « parler à » remplis depuis le tick précédent.
            events.AddRange(_queued);
            _queued.Clear();
            foreach (string id in _quests.Keys.ToArray())
            {
                (int step, long progress) = _quests[id];
                Quest quest = _book.Quest(id);
                if (step < 0 || step >= quest.Steps.Count) continue;
                QuestStep s = quest.Steps[step];
                // Relecture de la PR #76 : des objets promis qui ne sont plus là (vendus, réservés depuis) : la livraison est à refaire.
                if (s.Objective.Kind == ObjectiveKind.Deliver && progress >= 1 && _world.Inventory.Available(s.Objective.Id) < s.Objective.Value)
                {
                    _quests[id] = (step, 0L);
                    Revision++;
                    continue;
                }
                if (!Reached(s.Objective, progress)) continue;
                bool last = step + 1 == quest.Steps.Count;
                bool choose = last && quest.RewardChoices.Count > 0;
                // Relecture de la PR #76 : les objets livrés sont pris avec les effets de l'étape, dans la même application atomique.
                var effects = s.Objective.Delivery.Concat(s.Effects).Concat(last ? quest.Effects : Array.Empty<StoryEffect>()).ToArray();
                var applied = new List<StoryEvent>();
                // Relecture de la PR #76 : les effets se jugent dans l'état d'après l'étape (une quête qui en commence une autre ouverte « après
                // elle », questDone, la commence) ; refusés, la quête revient à son état.
                // Revue Codex de la PR #76 : avec une récompense au choix, l'état d'après est l'attente du choix (questReward vrai, questDone
                // faux), pas la quête finie.
                _quests[id] = choose ? (quest.Steps.Count, 0L) : last ? (-1, 0L) : (step + 1, 0L);
                DialogueRefusal blocked = Apply(effects, applied);
                _quests[id] = (step, progress);
                if (blocked != DialogueRefusal.None)
                {
                    // Relecture de la PR #72 : une bourse pleine a son propre message (pas « inventaire plein ») ; sous-lot V1.6 : une bourse trop
                    // légère et une quête encore fermée aussi.
                    StoryEventKind kind = blocked switch
                    {
                        DialogueRefusal.PurseFull => StoryEventKind.PurseFull,
                        DialogueRefusal.NotEnoughMoney => StoryEventKind.MoneyShort,
                        DialogueRefusal.Locked => StoryEventKind.QuestLocked,
                        _ => StoryEventKind.RewardBlocked,
                    };
                    if (_blocked.Add(id + "/" + s.Id)) events.Add(new StoryEvent(kind, id, s.Id));
                    continue;
                }
                _blocked.Remove(id + "/" + s.Id);
                // L'étape est franchie avant ses effets dans le journal ; un effet qui relance la même quête est sans objet.
                // Sous-lot V1.6 : après sa dernière étape, une quête à récompense au choix attend ce choix (elle reste en cours).
                Revision++;
                _quests[id] = choose ? (quest.Steps.Count, 0L) : last ? (-1, 0L) : (step + 1, 0L);
                if (last && !choose && _tracked == id) _tracked = null;
                events.Add(new StoryEvent(StoryEventKind.StepCompleted, id, s.Id));
                events.AddRange(applied);
                if (choose) events.Add(new StoryEvent(StoryEventKind.RewardToChoose, id));
                else if (last)
                {
                    events.Add(new StoryEvent(StoryEventKind.QuestCompleted, id));
                    QuestFinished(id, events);   // sous-lot V1.7 : promotion (épreuve) ou points de guilde
                }
            }
            StepContracts(events);
        }

        // --- Guilde des aventuriers (sous-lot V1.7) -----------------------------------------------------------------

        /// <summary>Le personnage est inscrit à la guilde.</summary>
        public bool IsMember => _card != null;

        /// <summary>Rang de la carte (« g », « f »…), ou null sans carte.</summary>
        public string? GuildRankId => _card is null ? null : GuildRanks.Ids[_card.Rank];

        /// <summary>Indice du rang de la carte (0 : G), ou -1 sans carte.</summary>
        public int GuildRankIndex => _card?.Rank ?? -1;

        public long GuildPoints => _card?.Points ?? 0;
        public long ContractsDone => _card?.Contracts ?? 0;

        /// <summary>Titres gagnés (ordre ordinal), vide sans carte.</summary>
        public IReadOnlyList<string> Titles => _card is null ? Array.Empty<string>() : (IReadOnlyList<string>)_card.Titles.ToList().AsReadOnly();

        /// <summary>Titre affiché, ou null.</summary>
        public string? ShownTitle => _card?.Title;

        /// <summary>Contrats en cours et leur avancement (ordre ordinal), vide sans carte.</summary>
        public IReadOnlyList<(string Contract, long Progress)> ActiveContracts =>
            _card is null ? Array.Empty<(string, long)>() : (IReadOnlyList<(string, long)>)_card.Active.Select(a => (a.Key, a.Value)).ToList().AsReadOnly();

        /// <summary>Le contrat a déjà été pris le jour <paramref name="day"/> (en cours, réussi ou abandonné).</summary>
        public bool TakenOn(string contract, long day) => _card != null && _card.Day == day && _card.Taken.Contains(contract);

        /// <summary>Carte telle qu'elle sera sauvegardée (null sans carte).</summary>
        public SavedGuildCard? Card => _card?.ToSaved() ?? _keptCard;

        /// <summary>Rang suivant (ouvert ou non), ou null au plus haut ; null sans carte ni guilde.</summary>
        public GuildRank? NextRank => _card != null && _guild != null && _card.Rank + 1 < _guild.Ranks.Count ? _guild.Ranks[_card.Rank + 1] : null;

        /// <summary>
        /// Pourquoi l'épreuve du rang suivant ne peut pas commencer maintenant (<see cref="GuildRefusal.None"/> : elle le peut) : sans carte,
        /// au plus haut rang ouvert, déjà en cours, pas assez de points.
        /// </summary>
        public GuildRefusal CanStartTrial()
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card is null) return GuildRefusal.NotMember;
            if (!(NextRank is GuildRank next) || !next.Open || next.Trial is null) return GuildRefusal.TopRank;
            if (_quests.ContainsKey(next.Trial)) return IsDone(next.Trial) ? GuildRefusal.Locked : GuildRefusal.TrialActive;
            if (_card.Points < next.Points) return GuildRefusal.NotEnoughPoints;
            // Relecture de la PR #79 : les conditions d'ouverture de la quête de l'épreuve comptent aussi (niveau, autre quête…).
            if (!All(_book.Quest(next.Trial).Requires)) return GuildRefusal.Locked;
            return GuildRefusal.None;
        }

        /// <summary>
        /// Inscription : la carte au rang G, avec le titre de ce rang (affiché). Refusée sans guilde, ou déjà inscrit. Réponses de Damien du
        /// 31 octobre 2026 : les exploits d'avant l'inscription comptent, comme s'ils avaient été faits après (<see cref="ReputationPoints"/>),
        /// en un seul événement (<see cref="StoryEventKind.GuildReputation"/>) ; ils ne comptent qu'une fois : l'inscription n'a lieu qu'une fois,
        /// et ces exploits ne se refont pas (une quête finie, une découverte, une créature rare ou un fait ne rapportent qu'à leur première fois).
        /// </summary>
        internal GuildRefusal Join(List<StoryEvent> events)
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card != null) return GuildRefusal.AlreadyMember;
            GuildRank g = _guild.Ranks[0];
            long reputation = ReputationPoints();
            _card = new GuildCard { Rank = 0, Title = g.Title };
            _card.Titles.Add(g.Title);
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.GuildJoined, g.Id));
            long added = _card.Add(reputation);
            if (added > 0) events.Add(new StoryEvent(StoryEventKind.GuildReputation, g.Id, null, added));
            return GuildRefusal.None;
        }

        /// <summary>
        /// Réponses du 31 octobre 2026 : points de guilde des exploits déjà faits : chaque quête finie (sauf une épreuve, qui donne un rang)
        /// selon sa catégorie, chaque découverte, chaque créature rare de la guilde déjà vaincue (<c>story.hunted</c>), chaque fait qui rapporte
        /// des points. 0 sans guilde. Chaque gain est borné par le validateur et les listes par la sauvegarde : la somme ne déborde pas.
        /// </summary>
        public long ReputationPoints()
        {
            if (_guild is null) return 0;
            long total = 0;
            foreach (KeyValuePair<string, (int Step, long Progress)> q in _quests)
                if (q.Value.Step < 0 && _guild.TrialOf(q.Key) is null)
                    total = checked(total + (_book.Quest(q.Key).Category == QuestCategory.Main ? _guild.MainQuestPoints : _guild.SideQuestPoints));
            total = checked(total + _discoveries.Count * _guild.DiscoveryPoints);
            foreach (string creature in _hunted)
                if (_guild.RarePoints.TryGetValue(creature, out long rare)) total = checked(total + rare);
            foreach (string fact in _facts)
                if (_guild.FactPoints.TryGetValue(fact, out long points)) total = checked(total + points);
            return total;
        }

        /// <summary>
        /// Réponses du 31 octobre 2026 : le contrat en cours <paramref name="contract"/> est un contrat pressé d'un jour passé (pris le jour de la
        /// carte, avant <paramref name="day"/>) : il est perdu, même s'il n'a pas encore été retiré.
        /// </summary>
        private bool Expired(string contract, long day) => _card != null && _guild != null && day > _card.Day && _guild.Contract(contract).Rush;

        /// <summary>
        /// Réponses du 31 octobre 2026 : un contrat pressé est à finir avant la fin de la journée du monde où il a été pris. Il a toujours été
        /// pris le jour de la carte (<see cref="GuildCard.Day"/>) : la carte ne passe à un autre jour qu'après ce retrait. Le jour venu, chaque
        /// contrat pressé en cours est perdu, sans pénalité (rien n'est pris ; ses objets promis restent dans le sac).
        /// </summary>
        private void ExpireRush(long day, List<StoryEvent> events)
        {
            if (_card is null || _guild is null) return;
            foreach (string id in _card.Active.Keys.ToArray())
            {
                if (!Expired(id, day)) continue;
                _card.Active.Remove(id);
                _blocked.Remove("contract/" + id);
                Revision++;
                events.Add(new StoryEvent(StoryEventKind.ContractExpired, id));
            }
        }

        /// <summary>
        /// Relecture de la PR #81 : refus qu'aurait la prise du contrat <paramref name="contract"/> le jour <paramref name="day"/>, sans rien
        /// changer (la même vérification que <see cref="TakeContract"/>) : inscrit ; contrat au tableau ; pas déjà pris ce jour ; moins de
        /// contrats en cours que la limite (tous confondus, sans les contrats pressés d'un jour passé, déjà perdus) ; place dans la liste du jour.
        /// </summary>
        internal GuildRefusal CanTakeContract(string contract, long day)
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card is null) return GuildRefusal.NotMember;
            if (contract is null || GuildBoard.Of(_guild, _card.Rank, day).All(c => c.Id != contract)) return GuildRefusal.NotOnBoard;
            if ((_card.Active.ContainsKey(contract) && !Expired(contract, day)) || TakenOn(contract, day)) return GuildRefusal.AlreadyTaken;
            if (_card.Active.Keys.Count(a => !Expired(a, day)) >= Math.Min(_guild.MaxActive, SavedGuildCard.MaxActive)) return GuildRefusal.TooManyContracts;
            if (day == _card.Day && _card.Taken.Count >= SavedGuildCard.MaxTaken) return GuildRefusal.TooManyContracts;   // MarkTaken refuserait
            return GuildRefusal.None;
        }

        /// <summary>
        /// Prend le contrat <paramref name="contract"/> du tableau du jour <paramref name="day"/> : inscrit ; contrat au tableau ; pas déjà pris ce
        /// jour ; moins de <see cref="GuildBook.MaxActive"/> contrats en cours. Son avancement part de zéro. Tout ou rien.
        /// </summary>
        internal GuildRefusal TakeContract(string contract, long day, List<StoryEvent> events)
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card is null) return GuildRefusal.NotMember;
            GuildRefusal can = CanTakeContract(contract, day);
            if (can != GuildRefusal.None) return can;
            ExpireRush(day, events);
            if (!_card.MarkTaken(contract, day)) return GuildRefusal.TooManyContracts;
            _card.Active[contract] = 0;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.ContractTaken, contract));
            return GuildRefusal.None;
        }

        /// <summary>
        /// Abandonne un contrat en cours (sans perte ; il ne se reprend pas avant le lendemain). Relecture de la PR #79 : l'abandon compte comme
        /// une prise du jour <paramref name="day"/>, même pour un contrat pris un autre jour (sinon il se reprendrait aussitôt) ; si la liste du
        /// jour est pleine, l'abandon est refusé sans rien changer.
        /// </summary>
        internal GuildRefusal AbandonContract(string contract, long day, List<StoryEvent> events)
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card is null) return GuildRefusal.NotMember;
            if (contract is null || !_card.Active.ContainsKey(contract) || Expired(contract, day)) return GuildRefusal.NotActive;   // pressé d'hier : perdu
            if (day == _card.Day && !_card.Taken.Contains(contract) && _card.Taken.Count >= SavedGuildCard.MaxTaken) return GuildRefusal.TooManyContracts;
            ExpireRush(day, events);
            if (!_card.MarkTaken(contract, day)) return GuildRefusal.TooManyContracts;
            _card.Active.Remove(contract);
            _blocked.Remove("contract/" + contract);
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.ContractAbandoned, contract));
            return GuildRefusal.None;
        }

        /// <summary>Commence l'épreuve du rang suivant (sa quête), si <see cref="CanStartTrial"/> le permet ; tout ou rien.</summary>
        internal GuildRefusal StartTrial(List<StoryEvent> events)
        {
            GuildRefusal why = CanStartTrial();
            if (why != GuildRefusal.None) return why;
            var applied = new List<StoryEvent>();
            if (Apply(new[] { StoryEffect.StartQuest(NextRank!.Trial!) }, applied) != DialogueRefusal.None) return GuildRefusal.Locked;
            events.AddRange(applied);
            return GuildRefusal.None;
        }

        /// <summary>Affiche le titre <paramref name="title"/> (un titre gagné), ou plus aucun (null).</summary>
        internal GuildRefusal ShowTitle(string? title, List<StoryEvent> events)
        {
            if (_guild is null) return GuildRefusal.NoGuild;
            if (_card is null) return GuildRefusal.NotMember;
            if (title != null && !_card.Titles.Contains(title)) return GuildRefusal.UnknownTitle;
            if (_card.Title == title) return GuildRefusal.None;
            _card.Title = title;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.TitleShown, title ?? string.Empty));
            return GuildRefusal.None;
        }

        /// <summary>Ajoute des points de guilde à la carte (sans effet sans carte) et l'annonce ; arrêtés au plafond de la carte.</summary>
        private void Award(long points, string source, List<StoryEvent> events)
        {
            if (_card is null || points <= 0) return;
            long added = _card.Add(points);
            if (added <= 0) return;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.GuildPoints, source, null, added));
        }

        /// <summary>
        /// Une quête est finie : une épreuve de promotion donne le rang (et son titre, affiché) ; une autre quête donne les points de guilde de
        /// sa catégorie.
        /// </summary>
        private void QuestFinished(string quest, List<StoryEvent> events)
        {
            if (_guild is null || _card is null) return;
            if (_guild.TrialOf(quest) is GuildRank rank)
            {
                if (_card.Rank + 1 != rank.Index) return;   // déjà au-dessus (ne peut arriver : l'épreuve ne s'ouvre qu'au rang juste en dessous)
                _card.Rank = rank.Index;
                _card.Titles.Add(rank.Title);
                _card.Title = rank.Title;
                Revision++;
                events.Add(new StoryEvent(StoryEventKind.GuildPromoted, rank.Id));
                return;
            }
            Award(_book.Quest(quest).Category == QuestCategory.Main ? _guild.MainQuestPoints : _guild.SideQuestPoints, quest, events);
        }

        /// <summary>
        /// Sous-lot V1.7 : chaque contrat en cours dont l'objectif est atteint est réussi (ordre ordinal) : la remise des objets livrés et ses
        /// récompenses (pièces, expérience, points de guilde) en une seule application atomique ; refusée (bourse pleine, sac plein), le contrat
        /// attend, annoncé une fois. Des objets promis qui ont quitté le sac : la livraison est à refaire.
        /// </summary>
        private void StepContracts(List<StoryEvent> events)
        {
            if (_card is null || _guild is null) return;
            // Réponses du 31 octobre 2026 : les contrats pressés d'un jour passé sont perdus avant toute réussite. Relecture de la PR #81 : le
            // jour est celui du tick qui vient d'être simulé (un contrat fini au dernier tick avant minuit est réussi).
            ExpireRush(_world.SimulatedDay, events);
            foreach (string id in _card.Active.Keys.ToArray())
            {
                GuildContract contract = _guild.Contract(id);
                Objective o = contract.Objective;
                long progress = _card.Active[id];
                if (o.Kind == ObjectiveKind.Deliver && progress >= 1 && _world.Inventory.Available(o.Id) < o.Value)
                {
                    _card.Active[id] = 0;
                    Revision++;
                    continue;
                }
                if (!Reached(o, progress)) continue;
                var applied = new List<StoryEvent>();
                DialogueRefusal blocked = Apply(o.Delivery.Concat(contract.Rewards).ToArray(), applied, id);
                if (blocked != DialogueRefusal.None)
                {
                    StoryEventKind kind = blocked == DialogueRefusal.PurseFull ? StoryEventKind.PurseFull : StoryEventKind.RewardBlocked;
                    if (_blocked.Add("contract/" + id)) events.Add(new StoryEvent(kind, id));
                    continue;
                }
                _blocked.Remove("contract/" + id);
                _card.Active.Remove(id);
                _card.Contracts = Math.Min(SavedGuildCard.MaxContracts, checked(_card.Contracts + 1));
                Revision++;
                events.Add(new StoryEvent(StoryEventKind.ContractDone, id));
                events.AddRange(applied);
            }
        }

        private bool Reached(Objective o, long progress) => o.Kind switch
        {
            ObjectiveKind.Fact => _facts.Contains(o.Id),
            ObjectiveKind.Reach => _world.InPlace(o.Id),
            ObjectiveKind.Defeat => progress >= o.Value,
            ObjectiveKind.Collect => _world.Inventory.Count(o.Id) >= o.Value,
            ObjectiveKind.Passage => _world.IsPassageOpen(o.Id),
            // Sous-lot V1.6.
            ObjectiveKind.Talk or ObjectiveKind.Deliver => progress >= 1,
            ObjectiveKind.DefeatFamily => progress >= o.Value,
            ObjectiveKind.GuildRank => _card != null && _card.Rank >= GuildRanks.IndexOf(o.Id),   // sous-lot V1.7
            ObjectiveKind.CraftQuality => _world.Inventory.CountAtLeast(o.Id, o.Quality) >= o.Value,   // sous-lot V1.9.3b
            ObjectiveKind.Practice => progress >= o.Value,   // sous-lot V1.9.6
            _ => _world.IsEquipped(o.Id),
        };

        // --- Conditions et effets ----------------------------------------------------------------

        /// <summary>Fait appris, sauf le « déjà rencontré » du premier dialogue encore ouvert (V1.9.10) : le premier accueil dure tout ce dialogue.</summary>
        private bool Known(string fact) => _facts.Contains(fact) && !(_open != null && fact == _firstMet);

        private bool All(IReadOnlyList<StoryCondition> conditions) => conditions.All(Holds);

        private bool Holds(StoryCondition c) => c.Kind switch
        {
            ConditionKind.Fact => Known(c.Id),
            ConditionKind.NotFact => !Known(c.Id),
            ConditionKind.QuestNotStarted => !_quests.ContainsKey(c.Id),
            // Relecture de la PR #76 : une quête qui attend le choix de sa récompense n'est plus « en cours » pour les dialogues (questReward).
            ConditionKind.QuestActive => IsActive(c.Id) && !IsChoosingReward(c.Id),
            ConditionKind.QuestReward => IsChoosingReward(c.Id),
            ConditionKind.QuestAt => CurrentStep(c.Id)?.Id == c.Step,
            ConditionKind.QuestDone => IsDone(c.Id),
            ConditionKind.TrustAtLeast => Trust(c.Id) >= c.Value,
            ConditionKind.TrustBelow => Trust(c.Id) < c.Value,
            ConditionKind.HasItem => _world.Inventory.Count(c.Id) >= c.Value,
            ConditionKind.Phase => PhaseId(_world.Phase) == c.Id,
            ConditionKind.Weather => WorldWeather.Matches(_world.Weather, c.Id),   // sous-lot V2.2
            ConditionKind.Activity => _book.HasNpc(c.Id) && _world.NpcActivity(_book.Npc(c.Id)) == c.Step,   // sous-lot V2.3
            ConditionKind.InPlace => _world.InPlace(c.Id),
            ConditionKind.LevelAtLeast => (_bonusXp > 0 ? _world.LevelWith(_bonusXp) : _world.Level) >= c.Value,   // sous-lot V1.6
            ConditionKind.GuildRankAtLeast => _card != null && _card.Rank >= GuildRanks.IndexOf(c.Id),   // sous-lot V1.7
            _ => _world.IsPassageOpen(c.Id),
        };

        public static string PhaseId(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => "dawn",
            DayPhase.Day => "day",
            DayPhase.Dusk => "dusk",
            _ => "night",
        };

        /// <summary>
        /// Sous-lot V1.9.7 (effet <c>skipQuest</c>) : la quête en cours (à une étape, ou au choix de sa récompense) est finie, sans les effets de
        /// ses étapes restantes ni sa récompense ; elle n'est plus suivie si elle l'était (une autre quête suivie le reste). Pas commencée ou
        /// déjà finie : sans effet (rend faux). Jamais refusé (rien à réserver). Les points de guilde d'une quête finie ne sont pas donnés.
        /// </summary>
        internal bool Skip(string quest, List<StoryEvent> events)
        {
            if (!_quests.TryGetValue(quest, out (int Step, long Progress) skipped) || skipped.Step < 0) return false;
            _quests[quest] = (-1, 0L);
            if (_tracked == quest) _tracked = null;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.QuestSkipped, quest));
            return true;
        }

        /// <summary>
        /// Applique des effets en entier ou pas du tout : les passages à ouvrir doivent trouver place au registre du monde, les pièces données
        /// dans la bourse (sous-lot V1.1), les objets sont d'abord essayés sur une copie de l'inventaire (retraits puis dons, dans l'ordre) ;
        /// si tout passe, tout est appliqué. Rend le motif d'un refus : <see cref="DialogueRefusal.PurseFull"/> pour la bourse,
        /// <see cref="DialogueRefusal.NoRoom"/> pour le reste.
        /// </summary>
        /// <param name="source">Sous-lot V1.7 : ce qui donne les points de guilde d'un effet <c>giveGuildPoints</c> (un contrat), « guild » par défaut.</param>
        private DialogueRefusal Apply(IReadOnlyList<StoryEffect> effects, List<StoryEvent> events, string source = "guild")
        {
            // Revue Codex de la PR #46 : les passages à ouvrir réservent leur place au registre avant toute mutation (registre plein : rien
            // n'est appliqué, ni retrait d'objets ni autre effet).
            int passages = effects.Where(e => e.Kind == EffectKind.OpenPassage && !_world.IsPassageOpen(e.Id)).Select(e => e.Id).Distinct(StringComparer.Ordinal).Count();
            if (passages > _world.PassageRoom) return DialogueRefusal.NoRoom;
            // Sous-lot V1.1 : les pièces données doivent tenir dans la bourse, toutes ensemble (sinon rien n'est appliqué). Chaque effet est
            // borné par le validateur (au plus StoryValidator.MaxMoney, huit effets) : la somme ne déborde pas.
            long money = 0;
            foreach (StoryEffect e in effects)
                if (e.Kind == EffectKind.GiveMoney) money = checked(money + e.Value);
            // Sous-lot V1.6 : les pièces prises doivent être dans la bourse, toutes ensemble ; prises avant les dons (une bourse pleine qui paie
            // puis reçoit garde sa place).
            long take = 0;
            foreach (StoryEffect e in effects)
                if (e.Kind == EffectKind.TakeMoney) take = checked(take + e.Value);
            if (take > _world.Money) return DialogueRefusal.NotEnoughMoney;
            if (money > 0 && !Coins.TryAdd(checked(_world.Money - take), money, out _)) return DialogueRefusal.PurseFull;
            // Sous-lot V1.6 : une quête à commencer doit être ouverte (ses conditions réunies) ; sinon rien n'est appliqué.
            if (StartsLocked(effects)) return DialogueRefusal.Locked;
            Inventory trial = _world.Inventory.Clone();
            foreach (StoryEffect e in effects)
            {
                if (e.Kind == EffectKind.TakeItem && trial.Remove(e.Id, (int)e.Value) != InventoryRefusal.None) return DialogueRefusal.NoRoom;
                if (e.Kind == EffectKind.GiveItem && trial.Add(e.Id, (int)e.Value) != InventoryRefusal.None) return DialogueRefusal.NoRoom;
            }
            _world.Inventory.CopyFrom(trial);
            Revision++;
            if (take > 0)
            {
                // Vérifiée plus haut : la bourse a ces pièces.
                if (!_world.TakeMoney(take)) throw new InvalidOperationException("Bourse trop légère après vérification.");
            }
            foreach (StoryEffect e in effects)
                switch (e.Kind)
                {
                    case EffectKind.SetFact:
                        if (_facts.Add(e.Id))
                        {
                            events.Add(new StoryEvent(StoryEventKind.FactLearned, e.Id));
                            if (_guild != null && _guild.FactPoints.TryGetValue(e.Id, out long factPoints)) Award(factPoints, e.Id, events);   // sous-lot V1.7
                        }
                        break;
                    case EffectKind.GiveGuildPoints:   // sous-lot V1.7 : jamais refusé ; sans carte, sans effet
                        Award(e.Value, source, events);
                        break;
                    case EffectKind.Trust:
                        int before = Trust(e.Id);
                        int after = (int)Math.Max(-SaveValidator.MaxTrust, Math.Min(SaveValidator.MaxTrust, before + e.Value));
                        if (after == 0) _trust.Remove(e.Id);
                        else _trust[e.Id] = after;
                        if (after != before) events.Add(new StoryEvent(StoryEventKind.TrustChanged, e.Id, null, after - before));
                        break;
                    case EffectKind.StartQuest:
                        if (!_quests.ContainsKey(e.Id))
                        {
                            _quests[e.Id] = (0, 0L);
                            // Sous-lot V1.6 : sans quête suivie, la nouvelle l'est.
                            if (_tracked is null) _tracked = e.Id;
                            events.Add(new StoryEvent(StoryEventKind.QuestStarted, e.Id, _book.Quest(e.Id).Steps[0].Id));
                        }
                        break;
                    case EffectKind.GiveItem:
                        events.Add(new StoryEvent(StoryEventKind.ItemGiven, e.Id, null, e.Value));
                        break;
                    case EffectKind.TakeItem:
                        events.Add(new StoryEvent(StoryEventKind.ItemTaken, e.Id, null, e.Value));
                        break;
                    case EffectKind.GiveXp:
                        _world.GiveXp(e.Value);
                        events.Add(new StoryEvent(StoryEventKind.XpGiven, "xp", null, e.Value));
                        break;
                    case EffectKind.OpenPassage:
                        if (_world.OpenPassage(e.Id)) events.Add(new StoryEvent(StoryEventKind.PassageOpened, e.Id));
                        break;
                    case EffectKind.GiveMoney:
                        // Place vérifiée plus haut pour la somme de tous les dons : celui-ci tient.
                        if (!_world.GiveMoney(e.Value)) throw new InvalidOperationException("Bourse pleine après vérification.");
                        events.Add(new StoryEvent(StoryEventKind.MoneyGiven, "money", null, e.Value));
                        break;
                    case EffectKind.TakeMoney:   // sous-lot V1.6 : déjà prises, toutes ensemble, plus haut
                        events.Add(new StoryEvent(StoryEventKind.MoneyTaken, "money", null, e.Value));
                        break;
                    case EffectKind.SkipQuest:
                        Skip(e.Id, events);
                        break;
                    default:
                        if (_discoveries.Add(e.Id))
                        {
                            events.Add(new StoryEvent(StoryEventKind.Discovered, e.Id));
                            if (_guild != null) Award(_guild.DiscoveryPoints, e.Id, events);   // sous-lot V1.7
                        }
                        break;
                }
            return DialogueRefusal.None;
        }
    }
}
