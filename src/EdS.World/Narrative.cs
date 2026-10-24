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
    }

    /// <summary>Événement narratif (journal de quête, journal des découvertes, confiance des PNJ).</summary>
    public sealed class StoryEvent
    {
        public StoryEventKind Kind { get; }
        public string Id { get; }

        /// <summary>Étape (quête), ou null.</summary>
        public string? Step { get; }

        /// <summary>Quantité (objets, expérience) ou changement de confiance ; 0 sinon.</summary>
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

        /// <summary>Les objets donnés ne tiennent pas dans l'inventaire, ou ceux demandés manquent : rien n'a changé.</summary>
        NoRoom = 4,
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
                int step = q.Value.IsDone ? -1 : book.Quest(q.Key).StepIndex(q.Value.Step);
                _quests[q.Key] = (step, q.Value.Progress);
            }
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
                if (!q.Value.IsDone && quest.StepIndex(q.Value.Step) < 0) throw new ArgumentException($"Étape « {q.Value.Step} » inconnue de « {q.Key} ».", nameof(saved));
            }
        }

        // Vues en lecture seule : l'histoire ne change que par ses opérations.
        public IReadOnlyCollection<string> Facts => _facts.ToList().AsReadOnly();
        public IReadOnlyCollection<string> Discoveries => _discoveries.ToList().AsReadOnly();
        public int Trust(string npc) => _trust.TryGetValue(npc, out int t) ? t : 0;
        public bool Knows(string fact) => _facts.Contains(fact);
        public bool IsDone(string quest) => _quests.TryGetValue(quest, out var q) && q.Step < 0;
        public bool IsActive(string quest) => _quests.TryGetValue(quest, out var q) && q.Step >= 0;

        /// <summary>Étape en cours d'une quête active ; null sinon.</summary>
        public QuestStep? CurrentStep(string quest) => _quests.TryGetValue(quest, out var q) && q.Step >= 0 ? _book.Quest(quest).Steps[q.Step] : null;

        public long Progress(string quest) => _quests.TryGetValue(quest, out var q) ? q.Progress : 0;

        /// <summary>Dialogue ouvert, ou null.</summary>
        public DialogueView? Open => _open is { } o ? View(o.Npc, o.Node) : null;

        public SavedStory ToSaved() => new SavedStory(_facts,
            _quests.ToDictionary(q => q.Key, q => new QuestProgress(q.Value.Step < 0 ? QuestProgress.Done : _book.Quest(q.Key).Steps[q.Value.Step].Id, q.Value.Progress),
                StringComparer.Ordinal),
            _trust, _discoveries);

        // --- Dialogues ---------------------------------------------------------------------------

        /// <summary>Parle à un PNJ : à portée, le personnage présent et vivant ; ouvre le nœud de la première entrée qui convient.</summary>
        public DialogueRefusal Talk(string npc, out DialogueView? view)
        {
            view = null;
            if (npc is null || !_book.HasNpc(npc)) return DialogueRefusal.NothingToSay;
            Npc n = _book.Npc(npc);
            if (!InRange(n)) return DialogueRefusal.TooFar;
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
            if (!Apply(choice.Effects, events)) return DialogueRefusal.NoRoom;
            if (choice.Next is null) _open = null;
            else
            {
                _open = (o.Npc, o.Dialogue, o.Dialogue.Node(choice.Next));
                next = View(o.Npc, _open.Value.Node);
            }
            return DialogueRefusal.None;
        }

        public void Close() => _open = null;

        private DialogueView View(string npc, DialogueNode node) =>
            new DialogueView(npc, node.Id, node.Text, node.Choices.Select((c, i) => (c, i)).Where(x => All(x.c.Conditions))
                .Select(x => new DialogueOption(x.i, x.c.Text)).ToArray());

        private bool InRange(Npc n) =>
            _world.PlayerPosition is GroundVector p && !ZonePopulations.Beyond(p, EdS.Domain.Numerics.Fixed.Zero, n.Position, EdS.Domain.Numerics.Fixed.Zero, TalkRange);

        // --- Quêtes ------------------------------------------------------------------------------

        /// <summary>Sous-lot 12.7 : fait appris par le monde (victoire sur le chef) ; faux s'il l'était déjà.</summary>
        internal bool Learn(string fact, List<StoryEvent> events)
        {
            if (!_facts.Add(fact)) return false;
            events.Add(new StoryEvent(StoryEventKind.FactLearned, fact));
            return true;
        }

        /// <summary>Une créature de cette population a été vaincue (mort attribuée) : compte pour les objectifs « defeat » en cours.</summary>
        internal void Defeated(string population)
        {
            foreach (string id in _quests.Keys.ToArray())
                if (CurrentStep(id) is { Objective: { Kind: ObjectiveKind.Defeat } o } && o.Id == population)
                    _quests[id] = (_quests[id].Step, checked(_quests[id].Progress + 1));
        }

        /// <summary>Vérifie l'objectif de l'étape en cours de chaque quête (ordre ordinal) ; une étape au plus par quête et par tick.</summary>
        internal void Step(List<StoryEvent> events)
        {
            foreach (string id in _quests.Keys.ToArray())
            {
                (int step, long progress) = _quests[id];
                if (step < 0) continue;
                Quest quest = _book.Quest(id);
                QuestStep s = quest.Steps[step];
                if (!Reached(s.Objective, progress)) continue;
                bool last = step + 1 == quest.Steps.Count;
                var effects = last ? s.Effects.Concat(quest.Effects).ToArray() : s.Effects.ToArray();
                var applied = new List<StoryEvent>();
                if (!Apply(effects, applied))
                {
                    if (_blocked.Add(id + "/" + s.Id)) events.Add(new StoryEvent(StoryEventKind.RewardBlocked, id, s.Id));
                    continue;
                }
                _blocked.Remove(id + "/" + s.Id);
                // L'étape est franchie avant ses effets dans le journal ; un effet qui relance la même quête est sans objet.
                _quests[id] = last ? (-1, 0L) : (step + 1, 0L);
                events.Add(new StoryEvent(StoryEventKind.StepCompleted, id, s.Id));
                events.AddRange(applied);
                if (last) events.Add(new StoryEvent(StoryEventKind.QuestCompleted, id));
            }
        }

        private bool Reached(Objective o, long progress) => o.Kind switch
        {
            ObjectiveKind.Fact => _facts.Contains(o.Id),
            ObjectiveKind.Reach => _world.InPlace(o.Id),
            ObjectiveKind.Defeat => progress >= o.Value,
            ObjectiveKind.Collect => _world.Inventory.Count(o.Id) >= o.Value,
            _ => _world.IsPassageOpen(o.Id),
        };

        // --- Conditions et effets ----------------------------------------------------------------

        private bool All(IReadOnlyList<StoryCondition> conditions) => conditions.All(Holds);

        private bool Holds(StoryCondition c) => c.Kind switch
        {
            ConditionKind.Fact => _facts.Contains(c.Id),
            ConditionKind.NotFact => !_facts.Contains(c.Id),
            ConditionKind.QuestNotStarted => !_quests.ContainsKey(c.Id),
            ConditionKind.QuestActive => IsActive(c.Id),
            ConditionKind.QuestAt => CurrentStep(c.Id)?.Id == c.Step,
            ConditionKind.QuestDone => IsDone(c.Id),
            ConditionKind.TrustAtLeast => Trust(c.Id) >= c.Value,
            ConditionKind.TrustBelow => Trust(c.Id) < c.Value,
            ConditionKind.HasItem => _world.Inventory.Count(c.Id) >= c.Value,
            ConditionKind.Phase => PhaseId(_world.Phase) == c.Id,
            ConditionKind.InPlace => _world.InPlace(c.Id),
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
        /// Applique des effets en entier ou pas du tout : les passages à ouvrir doivent trouver place au registre du monde, les objets sont
        /// d'abord essayés sur une copie de l'inventaire (retraits puis dons, dans l'ordre) ; si tout passe, tout est appliqué.
        /// </summary>
        private bool Apply(IReadOnlyList<StoryEffect> effects, List<StoryEvent> events)
        {
            // Revue Codex de la PR #46 : les passages à ouvrir réservent leur place au registre avant toute mutation (registre plein : rien
            // n'est appliqué, ni retrait d'objets ni autre effet).
            int passages = effects.Where(e => e.Kind == EffectKind.OpenPassage && !_world.IsPassageOpen(e.Id)).Select(e => e.Id).Distinct(StringComparer.Ordinal).Count();
            if (passages > _world.PassageRoom) return false;
            Inventory trial = _world.Inventory.Clone();
            foreach (StoryEffect e in effects)
            {
                if (e.Kind == EffectKind.TakeItem && trial.Remove(e.Id, (int)e.Value) != InventoryRefusal.None) return false;
                if (e.Kind == EffectKind.GiveItem && trial.Add(e.Id, (int)e.Value) != InventoryRefusal.None) return false;
            }
            _world.Inventory.CopyFrom(trial);
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
                    default:
                        if (_discoveries.Add(e.Id)) events.Add(new StoryEvent(StoryEventKind.Discovered, e.Id));
                        break;
                }
            return true;
        }
    }
}
