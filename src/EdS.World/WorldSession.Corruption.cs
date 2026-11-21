using System;
using System.Collections.Generic;
using EdS.Data.Corruption;
using EdS.Data.Saves;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V2.42.1 : corruption de la zone (null : aucune, ou une autre zone que la sienne).</summary>
        private CorruptionBook? _corruption;

        /// <summary>Paliers vus par la session (annonces), ou null avant le premier tick.</summary>
        private Dictionary<string, int>? _corruptionSeen;

        /// <summary>Purifications faites hors d'un tick, annoncées au tick suivant.</summary>
        private readonly List<CorruptionNotice> _queuedCorruption = new List<CorruptionNotice>();

        private static readonly IReadOnlyList<CorruptionNotice> NoCorruptionNotices = new CorruptionNotice[0];
        private static readonly IReadOnlyList<CorruptionView> NoCorruptionViews = new CorruptionView[0];

        /// <summary>Sous-lot V2.42.1 : la corruption de <paramref name="book"/>, si elle est de cette zone ; sinon aucune.</summary>
        public void UseCorruption(CorruptionBook? book)
        {
            _corruption = book != null && book.ZoneId == Definition?.Id ? book : null;
            _corruptionSeen = null;
            _corruptionViewsDay = -1;
        }

        /// <summary>Sous-lot V2.42.1 : corruption en jeu dans cette session, ou null.</summary>
        public CorruptionBook? CorruptionBook => ActiveCorruption;

        private CorruptionBook? ActiveCorruption => _corruption != null && Definition?.Id == _corruption.ZoneId ? _corruption : null;

        /// <summary>Sous-lot V2.42.1 : autels purifiés du personnage (sauvegarde v31).</summary>
        public SavedCorruption CorruptionState => _character.Corruption;

        /// <summary>Sous-lot V2.42.1 : annonces de la corruption du dernier tick (<see cref="Step"/>).</summary>
        public IReadOnlyList<CorruptionNotice> CorruptionNotices { get; private set; } = NoCorruptionNotices;

        private IReadOnlyList<CorruptionView> _corruptionViews = NoCorruptionViews;
        private SavedCorruption? _corruptionViewsOf;
        private long _corruptionViewsDay = -1;

        /// <summary>
        /// Sous-lot V2.42.1 : la source puis chaque terre, avec son palier d'aujourd'hui (vide sans corruption). Recalculées seulement quand le
        /// jour du monde ou les autels purifiés changent ; sinon la même liste.
        /// </summary>
        public IReadOnlyList<CorruptionView> Corruption
        {
            get
            {
                if (!(ActiveCorruption is CorruptionBook book)) return NoCorruptionViews;
                long day = Today;
                if (day == _corruptionViewsDay && ReferenceEquals(_corruptionViewsOf, _character.Corruption)) return _corruptionViews;
                long[] since = CorruptionRules.CorruptedSince(book, _character.Corruption);
                var views = new List<CorruptionView>(book.Areas.Count + 1)
                {
                    new CorruptionView(book.Source, true, book.MaxStage, book.MaxStage, 0, null, null),
                };
                for (int i = 0; i < book.Areas.Count; i++)
                {
                    CorruptionArea a = book.Areas[i];
                    views.Add(new CorruptionView(a, false, CorruptionRules.StageAt(since[i], book, day), book.MaxStage, since[i],
                        _character.Corruption.DayOf(a.Id), CorruptionRules.NextChangeDay(since[i], book, day)));
                }
                _corruptionViews = views.AsReadOnly();
                _corruptionViewsOf = _character.Corruption;
                _corruptionViewsDay = day;
                return _corruptionViews;
            }
        }

        /// <summary>
        /// Sous-lot V2.42.1 : la terre la plus corrompue qui contient <paramref name="point"/> (à palier égal : la première, la source d'abord),
        /// ou null (aucune, ou toutes propres).
        /// </summary>
        public CorruptionView? CorruptionAt(GroundVector point)
        {
            CorruptionView? best = null;
            foreach (CorruptionView v in Corruption)
                if (v.IsCorrupted && !CorruptionValidator.Beyond(point, v.Position, v.Radius) && (best is null || v.Stage > best.Stage)) best = v;
            return best;
        }

        /// <summary>
        /// Sous-lot V2.42.1 : purifie l'autel de la terre <paramref name="area"/> avec un <see cref="CorruptionBook.PurifyItem"/> (un éclat de
        /// Seuil) : la terre est corrompue aujourd'hui et le personnage est à <see cref="PurifyRange"/> de l'autel. L'objet est pris, le jour
        /// gardé (la terre redevient propre pour <see cref="CorruptionBook.RespiteDays"/> jours, et avec elle les terres qu'elle seule
        /// nourrissait), l'expérience donnée (sous le plafond), le fait appris ; annoncé au tick suivant. Refus atomique : tout est vérifié
        /// avant la moindre mutation.
        /// </summary>
        public CorruptionRefusal PurifyAltar(string area)
        {
            if (area is null) throw new ArgumentNullException(nameof(area));
            if (_defeated) return CorruptionRefusal.Defeated;
            if (!(ActiveCorruption is CorruptionBook book) || !(book.Of(area) is CorruptionArea a)) return CorruptionRefusal.NoArea;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || !(me.Radius is Fixed radius)
                || ZonePopulations.Beyond(p, radius, a.Altar, Fixed.Zero, PurifyRange)) return CorruptionRefusal.TooFar;
            long day = Math.Min(SavedEvent.MaxDay, Today);
            int stage = CorruptionRules.StageAt(CorruptionRules.CorruptedSince(book, _character.Corruption)[a.Index], book, day);
            if (stage < 1) return CorruptionRefusal.NotCorrupted;
            if (Inventory.Available(book.PurifyItem) < 1) return CorruptionRefusal.NoItem;
            // Tout est vérifié : l'objet d'abord (refus atomique de l'inventaire), puis le reste, qui ne peut plus échouer.
            if (Inventory.Remove(book.PurifyItem, 1) != InventoryRefusal.None) return CorruptionRefusal.NoItem;
            _character = _character.WithCorruption(_character.Corruption.With(a.Id, day));
            long xp = Math.Min(SaveValidator.MaxXp, checked(_character.Xp + book.PurifyXp));
            long gained = xp - _character.Xp;
            _character = _character.WithXp(xp);
            _queuedCorruption.Add(new CorruptionNotice(CorruptionNoticeKind.Purified, a.Id, stage, day, gained));
            if (book.PurifiedFact is string fact)
            {
                if (Narrative != null) Narrative.LearnQueued(fact);
                else if (!_pendingFacts.Contains(fact)) _pendingFacts.Add(fact);
            }
            // La terre purifiée n'est pas annoncée « reculée » : elle est notée propre tout de suite (les terres qu'elle nourrissait le seront).
            if (_corruptionSeen != null) _corruptionSeen[a.Id] = 0;
            return CorruptionRefusal.None;
        }

        /// <summary>
        /// Sous-lot V2.42.1, à chaque tick : les purifications faites depuis le tick précédent, puis chaque terre qui change de palier avec les
        /// jours (gagnée, plus forte, ou reculée faute de source), pas au premier tick de la session (les paliers sont seulement notés).
        /// </summary>
        private void StepCorruption()
        {
            if (!(ActiveCorruption is CorruptionBook))
            {
                CorruptionNotices = NoCorruptionNotices;
                _queuedCorruption.Clear();
                return;
            }
            List<CorruptionNotice>? notices = _queuedCorruption.Count == 0 ? null : new List<CorruptionNotice>(_queuedCorruption);
            _queuedCorruption.Clear();
            bool first = _corruptionSeen is null;
            _corruptionSeen ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CorruptionView v in Corruption)
            {
                if (v.IsSource) continue;
                if (!first && _corruptionSeen.TryGetValue(v.Area.Id, out int seen) && seen != v.Stage)
                {
                    CorruptionNoticeKind kind = seen == 0 ? CorruptionNoticeKind.Spread : v.Stage > seen ? CorruptionNoticeKind.Grew : CorruptionNoticeKind.Receded;
                    (notices ??= new List<CorruptionNotice>()).Add(new CorruptionNotice(kind, v.Area.Id, v.Stage, Today));
                }
                _corruptionSeen[v.Area.Id] = v.Stage;
            }
            CorruptionNotices = notices is null ? NoCorruptionNotices : notices.AsReadOnly();
        }
    }
}
