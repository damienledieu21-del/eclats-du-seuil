using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.World
{
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V2.5 : événements du monde de la zone (null sans événement).</summary>
        private WorldEvents? _events;

        /// <summary>Sous-lot V2.5 : annonces et faits des événements nés hors d'un tick (faille refermée), rendus au tick suivant.</summary>
        private readonly List<WorldEventNotice> _queuedNotices = new List<WorldEventNotice>();
        private readonly List<string> _eventFacts = new List<string>();

        private void OpenEvents()
        {
            if (Definition is { } zone && zone.Events.Count > 0 && InStoryZone) _events = new WorldEvents(zone, _storyZone ?? zone.Id);
        }

        /// <summary>Sous-lot V2.5 : événements suivis dans la session (annoncés, en cours, qui finissent), pour l'affichage.</summary>
        public IReadOnlyList<WorldEventView> WorldEvents => _events?.Views(Zone) ?? (IReadOnlyList<WorldEventView>)Array.Empty<WorldEventView>();

        /// <summary>Sous-lot V2.5 : registre des événements du personnage (sauvegarde v13).</summary>
        public IReadOnlyList<SavedEvent> EventRegistry => _character.Events;

        /// <summary>Sous-lot V2.5 : la créature <paramref name="combatant"/> vient d'un événement du monde.</summary>
        public bool IsEventCreature(string combatant) => _events?.Of(combatant) != null;

        private Population? PopulationOfCombatant(string combatant) => _populations?.Of(combatant) ?? _events?.Of(combatant);

        internal Population PopulationFor(string creature) =>
            Definition?.PopulationOf(creature) ?? throw new InvalidOperationException($"Population « {creature} » absente de la zone.");

        /// <summary>
        /// Sous-lot V2.5 : conséquences visibles de l'événement <paramref name="eventId"/> : la dernière occurrence réglée pour le personnage
        /// a été manquée, et elle a fini il y a moins de <see cref="WorldEventDefinition.AftermathMinutes"/> minutes du monde (le hameau
        /// pillé : fumée, caisses brisées, villageois qui réparent). Recalculé du registre et du temps du monde.
        /// </summary>
        public bool Aftermath(string eventId)
        {
            if (Definition?.EventOf(eventId) is not WorldEventDefinition e || e.AftermathMinutes == 0 || !HasEventEntry(e)) return false;
            SavedEvent entry = EventEntry(e);
            if (entry.Outcome != EventOutcome.Lost) return false;
            if (!(WorldEventSchedule.OnDay(Definition!.Id, _storyZone ?? Definition.Id, e, entry.Last) is EventOccurrence o)) return false;
            long now = WorldEventSchedule.Absolute(Zone.Time);
            return now >= o.EndAt && now < checked(o.EndAt + e.AftermathMinutes);
        }

        /// <summary>
        /// Sous-lot V2.5 : referme la faille ouverte de l'événement <paramref name="eventId"/> (voir <see cref="World.WorldEvents.CloseRift"/>) ;
        /// la réussite et sa récompense sont annoncées au tick suivant. Refus atomique.
        /// </summary>
        public RiftRefusal CloseRift(string eventId)
        {
            if (_events is null) return RiftRefusal.NoRift;
            var notices = new List<WorldEventNotice>();
            RiftRefusal refusal = _events.CloseRift(this, Zone, eventId ?? throw new ArgumentNullException(nameof(eventId)), _character.Id, notices);
            _queuedNotices.AddRange(notices);
            return refusal;
        }

        internal bool HasEventEntry(WorldEventDefinition e) => _character.Events.Any(x => x.Id == WorldEventSchedule.RegistryKey(Definition!.Id, e.Id));

        /// <summary>Ligne du registre de l'événement (une ligne vide s'il n'en a pas encore).</summary>
        internal SavedEvent EventEntry(WorldEventDefinition e)
        {
            string key = WorldEventSchedule.RegistryKey(Definition!.Id, e.Id);
            return _character.Events.FirstOrDefault(x => x.Id == key) ?? new SavedEvent(key, 0, 0, -1, EventOutcome.None);
        }

        internal void SetEventEntry(WorldEventDefinition e, SavedEvent entry)
        {
            if (_character.Events.Count >= SavedEvent.MaxEntries && !HasEventEntry(e)) return;   // registre plein : rien n'est gardé (borne technique)
            _character = _character.WithEvents(_character.Events.Where(x => x.Id != entry.Id).Concat(new[] { entry }));
        }

        /// <summary>
        /// Règle l'occurrence <paramref name="o"/> pour le personnage, une seule fois (rien si son jour est déjà réglé) : compteurs, issue,
        /// faits ; à la première réussite, la récompense (ou, sac plein, elle est due) ; aux suivantes, les remerciements seulement.
        /// </summary>
        internal void SettleEvent(WorldEventDefinition e, EventOccurrence o, EventOutcome outcome, WorldEventNoticeKind? lostKind, List<WorldEventNotice> notices)
        {
            SavedEvent entry = EventEntry(e);
            if (o.Day <= entry.Last) return;
            bool first = outcome == EventOutcome.Won && entry.Won == 0;
            long won = outcome == EventOutcome.Won ? Math.Min(SavedEvent.MaxCount, checked(entry.Won + 1)) : entry.Won;
            long lost = outcome == EventOutcome.Lost ? Math.Min(SavedEvent.MaxCount, checked(entry.Lost + 1)) : entry.Lost;
            SetEventEntry(e, new SavedEvent(entry.Id, won, lost, Math.Min(SavedEvent.MaxDay, o.Day), outcome, entry.Owed || first));
            if (outcome == EventOutcome.Won)
            {
                notices.Add(new WorldEventNotice(WorldEventNoticeKind.Won, e.Id, o.Group.Id, o.Day));
                if (e.Reward?.Fact is string fact) _eventFacts.Add(fact);
                if (!first) notices.Add(new WorldEventNotice(WorldEventNoticeKind.Thanks, e.Id, o.Group.Id, o.Day));
                // La première récompense est donnée tout de suite si elle tient ; sinon elle reste due (owed) jusqu'à ce qu'elle tienne.
                else GiveEventReward(e, notices);   // sac plein : due, annoncée par le tick (WorldEvents.Step)
            }
            else if (outcome == EventOutcome.Lost)
            {
                notices.Add(new WorldEventNotice(lostKind ?? WorldEventNoticeKind.Lost, e.Id, o.Group.Id, o.Day));
                if (e.LostFact is string fact) _eventFacts.Add(fact);
            }
        }

        /// <summary>
        /// Donne la récompense due de l'événement <paramref name="e"/>, en entier ou pas du tout (objets dans le sac, pièces dans la bourse,
        /// expérience sous le plafond) : réservé sur une copie de l'inventaire avant toute mutation. Faux, sans rien changer, si elle ne tient pas.
        /// </summary>
        internal bool GiveEventReward(WorldEventDefinition e, List<WorldEventNotice> notices)
        {
            SavedEvent entry = EventEntry(e);
            if (!entry.Owed || !(e.Reward is WorldEventReward r)) return false;
            Inventory trial = Inventory.Clone();
            foreach ((string item, int count) in r.Items)
                if (trial.Add(item, count) != InventoryRefusal.None) return false;
            if (!Coins.TryAdd(_character.Money, r.Money, out long money)) return false;
            long xp = Math.Min(SaveValidator.MaxXp, checked(_character.Xp + r.Xp));
            long gained = xp - _character.Xp;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(money).WithXp(xp);
            SetEventEntry(e, new SavedEvent(entry.Id, entry.Won, entry.Lost, entry.Last, entry.Outcome));
            notices.Add(new WorldEventNotice(WorldEventNoticeKind.Rewarded, e.Id, string.Empty, entry.Last, 0, gained, r.Money, r.Items));
            return true;
        }

        /// <summary>Après les populations : événements du monde, puis faits appris (avant l'histoire du tick).</summary>
        private IReadOnlyList<WorldEventNotice> StepEvents(List<StoryEvent> story)
        {
            var notices = new List<WorldEventNotice>(_queuedNotices);
            _queuedNotices.Clear();
            if (_events != null) notices.AddRange(_events.Step(this, Zone, _fights.Sum(f => f.FormsToCome)));
            foreach (string fact in _eventFacts)
                if (Narrative != null) Narrative.Learn(fact, story);
                else if (!_pendingFacts.Contains(fact)) _pendingFacts.Add(fact);
            _eventFacts.Clear();
            return notices;
        }

        /// <summary>Sous-lot V2.5 : place de refuge du PNJ pendant une attaque, ou null.</summary>
        private GroundVector? RefugeOf(string npc) => _events?.RefugeOf(npc);
    }
}
