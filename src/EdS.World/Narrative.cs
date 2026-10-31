using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Réponse proposée : son rang dans le nœud et la clé de son texte.</summary>
    public sealed class DialogueOption
    {
        public int Index { get; }
        public string Text { get; }

        internal DialogueOption(int index, string text)
        {
            Index = index;
            Text = text;
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
        private (string Npc, Dialogue Dialogue, DialogueNode Node)? _open;

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

        internal Narrative(StoryBook book, SavedStory saved, INarrativeWorld world)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            CheckSaved(book, saved);
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
        internal static void CheckSaved(StoryBook book, SavedStory saved)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (saved is null) throw new ArgumentNullException(nameof(saved));
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
        public bool IsOpen(string quest) => !_quests.ContainsKey(quest) && All(_book.Quest(quest).Requires);

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

        public SavedStory ToSaved() => new SavedStory(_facts,
            _quests.ToDictionary(q => q.Key, q => new QuestProgress(q.Value.Step < 0 ? QuestProgress.Done
                    : q.Value.Step == _book.Quest(q.Key).Steps.Count ? QuestProgress.Reward : _book.Quest(q.Key).Steps[q.Value.Step].Id, q.Value.Progress),
                StringComparer.Ordinal),
            _trust, _discoveries, _tracked);

        // --- Dialogues ---------------------------------------------------------------------------

        /// <summary>Parle à un PNJ : à portée, le personnage présent et vivant ; ouvre le nœud de la première entrée qui convient.</summary>
        public DialogueRefusal Talk(string npc, out DialogueView? view)
        {
            view = null;
            if (npc is null || !_book.HasNpc(npc)) return DialogueRefusal.NothingToSay;
            Npc n = _book.Npc(npc);
            if (!InRange(n)) return DialogueRefusal.TooFar;
            // Sous-lot V1.6 : parler au PNJ remplit les objectifs « parler à » et « livrer à » de lui en cours (avant son dialogue, qui peut
            // dépendre de la livraison).
            Spoke(npc);
            Dialogue d = _book.Dialogue(n.Dialogue);
            DialogueEntry? entry = d.Entries.FirstOrDefault(e => All(e.Conditions));
            if (entry is null) return DialogueRefusal.NothingToSay;
            _open = (npc, d, d.Node(entry.Node));
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
                return DialogueRefusal.TooFar;
            }
            if (index < 0 || index >= o.Node.Choices.Count || !All(o.Node.Choices[index].Conditions)) return DialogueRefusal.NoSuchChoice;
            DialogueChoice choice = o.Node.Choices[index];
            DialogueRefusal applied = Apply(choice.Effects, events);
            if (applied != DialogueRefusal.None) return applied;
            if (choice.Next is null) _open = null;
            else
            {
                _open = (o.Npc, o.Dialogue, o.Dialogue.Node(choice.Next));
                next = View(o.Npc, _open.Value.Node);
            }
            return DialogueRefusal.None;
        }

        public void Close() => _open = null;

        /// <summary>
        /// Réponses proposées : leurs conditions réunies, et (sous-lot V1.6) aucune quête encore fermée à commencer (une réponse qui ouvrirait
        /// une quête avant son niveau n'apparaît pas).
        /// </summary>
        private DialogueView View(string npc, DialogueNode node) =>
            new DialogueView(npc, node.Id, node.Text, node.Choices.Select((c, i) => (c, i)).Where(x => All(x.c.Conditions) && !StartsLocked(x.c.Effects))
                .Select(x => new DialogueOption(x.i, x.c.Text)).ToArray());

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
                return effects.Any(e => e.Kind == EffectKind.StartQuest && !_quests.ContainsKey(e.Id) && !All(_book.Quest(e.Id).Requires));
            }
            finally
            {
                _bonusXp = 0;
            }
        }

        private bool InRange(Npc n) =>
            _world.PlayerPositionIn(n.Zone) is GroundVector p && !ZonePopulations.Beyond(p, EdS.Domain.Numerics.Fixed.Zero, n.Position, EdS.Domain.Numerics.Fixed.Zero, TalkRange);

        /// <summary>
        /// Sous-lot V1.6 : le personnage parle à <paramref name="npc"/> : chaque étape en cours « parler à » ce PNJ est remplie ; chaque étape
        /// « livrer à » ce PNJ l'est aussi si les objets sont dans l'inventaire et libres (pas réservés) : ils lui sont remis d'un coup, en
        /// entier ou pas du tout (ordre ordinal des quêtes). L'étape se franchit au tick suivant, avec ses effets.
        /// </summary>
        private void Spoke(string npc)
        {
            LastTalkFilled = 0;
            foreach (string id in _quests.Keys.ToArray())
            {
                if (!(CurrentStep(id) is QuestStep s) || _quests[id].Progress >= 1) continue;
                Objective o = s.Objective;
                // Relecture de la PR #76 : rien n'est pris en parlant. Les objets à livrer, s'ils sont là et libres, sont promis ; ils sont
                // remis au tick suivant avec les effets de l'étape, en une seule application atomique (tout ou rien : une étape refusée ne garde
                // jamais les objets).
                bool filled = (o.Kind == ObjectiveKind.Talk && o.Id == npc)
                    || (o.Kind == ObjectiveKind.Deliver && o.Npc == npc && _world.Inventory.Available(o.Id) >= o.Value);
                if (!filled) continue;
                _quests[id] = (_quests[id].Step, 1L);
                _queued.Add(new StoryEvent(StoryEventKind.ObjectiveDone, id, s.Id));
                LastTalkFilled++;
                Revision++;
            }
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
            return DialogueRefusal.None;
        }

        // --- Quêtes ------------------------------------------------------------------------------

        /// <summary>Sous-lot 12.7 : fait appris par le monde (victoire sur le chef) ; faux s'il l'était déjà.</summary>
        internal bool Learn(string fact, List<StoryEvent> events)
        {
            if (!_facts.Add(fact)) return false;
            Revision++;
            events.Add(new StoryEvent(StoryEventKind.FactLearned, fact));
            return true;
        }

        /// <summary>
        /// Une créature de cette population a été vaincue (mort attribuée) : compte pour les objectifs « defeat » en cours, et (sous-lot V1.6)
        /// pour les objectifs « defeatFamily » de sa famille. Le compteur s'arrête au nombre demandé.
        /// </summary>
        internal void Defeated(string population, string? family = null)
        {
            foreach (string id in _quests.Keys.ToArray())
                if (CurrentStep(id) is { Objective: { } o }
                    && ((o.Kind == ObjectiveKind.Defeat && o.Id == population) || (o.Kind == ObjectiveKind.DefeatFamily && family != null && o.Id == family))
                    && _quests[id].Progress < o.Value)
                {
                    _quests[id] = (_quests[id].Step, checked(_quests[id].Progress + 1));
                    Revision++;
                }
        }

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
                else if (last) events.Add(new StoryEvent(StoryEventKind.QuestCompleted, id));
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
            _ => _world.IsEquipped(o.Id),
        };

        // --- Conditions et effets ----------------------------------------------------------------

        private bool All(IReadOnlyList<StoryCondition> conditions) => conditions.All(Holds);

        private bool Holds(StoryCondition c) => c.Kind switch
        {
            ConditionKind.Fact => _facts.Contains(c.Id),
            ConditionKind.NotFact => !_facts.Contains(c.Id),
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
            ConditionKind.InPlace => _world.InPlace(c.Id),
            ConditionKind.LevelAtLeast => (_bonusXp > 0 ? _world.LevelWith(_bonusXp) : _world.Level) >= c.Value,   // sous-lot V1.6
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
        /// Applique des effets en entier ou pas du tout : les passages à ouvrir doivent trouver place au registre du monde, les pièces données
        /// dans la bourse (sous-lot V1.1), les objets sont d'abord essayés sur une copie de l'inventaire (retraits puis dons, dans l'ordre) ;
        /// si tout passe, tout est appliqué. Rend le motif d'un refus : <see cref="DialogueRefusal.PurseFull"/> pour la bourse,
        /// <see cref="DialogueRefusal.NoRoom"/> pour le reste.
        /// </summary>
        private DialogueRefusal Apply(IReadOnlyList<StoryEffect> effects, List<StoryEvent> events)
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
                        if (_facts.Add(e.Id)) events.Add(new StoryEvent(StoryEventKind.FactLearned, e.Id));
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
                    default:
                        if (_discoveries.Add(e.Id)) events.Add(new StoryEvent(StoryEventKind.Discovered, e.Id));
                        break;
                }
            return DialogueRefusal.None;
        }
    }
}
