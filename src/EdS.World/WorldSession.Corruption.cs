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

        /// <summary>Sous-lot V2.42.1 : autels purifiés du personnage (sauvegarde v32).</summary>
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
                // Sous-lot V2.42.3 : la source est propre pendant le répit de la région (après la défaite de son gardien).
                long source = CorruptionRules.SourceSince(book, _character.Corruption);
                var views = new List<CorruptionView>(book.Areas.Count + 1)
                {
                    new CorruptionView(book.Source, true, day < source ? 0 : book.MaxStage, book.MaxStage, source, _character.Corruption.DayOf(book.Source.Id),
                        day < source ? source : (long?)null),
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
            // Revue Codex de la PR #242 (P1) : la place dans la sauvegarde est vérifiée avant toute mutation (SavedCorruption.With ne peut
            // plus lever après le retrait de l'éclat : terre au format du catalogue, jour dans [0, MaxDay], au plus MaxEntries terres).
            if (_character.Corruption.DayOf(a.Id) is null && _character.Corruption.Purified.Count >= SavedCorruption.MaxEntries) return CorruptionRefusal.Full;
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
                _sourceFight = null;
                return;
            }
            bool silent = StepSourceChallenge();
            List<CorruptionNotice>? notices = _queuedCorruption.Count == 0 ? null : new List<CorruptionNotice>(_queuedCorruption);
            _queuedCorruption.Clear();
            // Sous-lot V2.42.3 : la purification de la source fait reculer toutes les terres d'un coup : une seule annonce, pas une par terre.
            bool first = _corruptionSeen is null || silent;
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

        // --- Sous-lot V2.42.3 : défi de la source -------------------------------------------------------------------------------------

        /// <summary>Essais : autels purifiés du personnage remplacés (sans autre effet).</summary>
        /// <summary>Essais (revue Codex de la PR #252) : appelé juste avant le départ du gardien d'un défi perdu (session, forme).</summary>
        internal Action<Zone, string>? BeforeSourceLeaveForTest { get; set; }

        internal void SetCorruptionForTest(SavedCorruption corruption) => _character = _character.WithCorruption(corruption);

        /// <summary>Sous-lot V2.42.3 : marge au-delà du disque de la source où le défi tient ; plus loin, il est perdu.</summary>
        public static readonly Fixed SourceLeash = Fixed.FromInt(30);

        /// <summary>Sous-lot V2.42.3 : combat du gardien de la source (dans <see cref="_fights"/> tant qu'il vit), ou null.</summary>
        private BossFight? _sourceFight;

        /// <summary>Sous-lot V2.42.3 : la victoire sur le gardien a déjà été réglée (purification ou refus annoncé).</summary>
        private bool _sourceSettled;

        /// <summary>Relecture indépendante de la PR #252 : le personnage était présent et vivant dans la session du gardien à sa défaite.</summary>
        private bool _sourceWonHere;

        /// <summary>Sous-lot V2.42.3 : le défi de la source est en cours (le gardien se bat).</summary>
        public bool SourceChallengeUnderway => _sourceFight != null && !_sourceSettled;

        /// <summary>Sous-lot V2.42.3 : forme du gardien en jeu (combattant), ou null hors du défi.</summary>
        public string? SourceGuardianForm => SourceChallengeUnderway ? _sourceFight!.CurrentForm : null;

        /// <summary>Sous-lot V2.42.3 : autels dans leur répit aujourd'hui (pour l'indice du défi).</summary>
        public int AltarsResting => ActiveCorruption is CorruptionBook book ? CorruptionRules.AltarsResting(book, _character.Corruption, Today) : 0;

        /// <summary>Sous-lot V2.42.3 : jours de répit de la région qui restent aujourd'hui (0 : aucun).</summary>
        public long SourceRestDays => ActiveCorruption is CorruptionBook book ? Math.Max(0, CorruptionRules.SourceSince(book, _character.Corruption) - Today) : 0;

        /// <summary>Sous-lot V2.42.3 : la région respire encore de la dernière purification de la source.</summary>
        public bool SourceResting => ActiveCorruption is CorruptionBook book && CorruptionRules.SourceResting(book, _character.Corruption, Today);

        /// <summary>
        /// Sous-lot V2.42.3 (décision de Damien du 21 novembre 2026) : lance le défi de la source : le gardien se lève au cercle des pierres
        /// levées (sa première forme arrive dans la session du personnage). Conditions : le défi existe, le personnage est debout dans le disque
        /// de la source (et, sur l'hôte des secteurs, dans la session du secteur du gardien), aucun défi n'est en cours, la région n'est plus
        /// dans le répit d'une purification de la source, au moins <see cref="CorruptionChallenge.MinAltars"/> autels sont dans leur répit, la
        /// sauvegarde a la place de la source, et la simulation accepte l'arrivée. Refus atomique : tout est vérifié avant la moindre mutation.
        /// </summary>
        public SourceChallengeRefusal ChallengeSource()
        {
            if (_defeated) return SourceChallengeRefusal.Defeated;
            if (!(ActiveCorruption is CorruptionBook book) || !(book.Challenge is CorruptionChallenge challenge)) return SourceChallengeRefusal.NoChallenge;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || CorruptionValidator.Beyond(p, book.Source.Position, book.Source.Radius))
                return SourceChallengeRefusal.TooFar;
            string? sector = _host?.Map.OwnerOf(challenge.Boss.Forms[0].Placement!.Position);
            if (_host != null && sector != _zoneSector) return SourceChallengeRefusal.TooFar;
            if (SourceChallengeUnderway) return SourceChallengeRefusal.Underway;
            long day = Math.Min(SavedEvent.MaxDay, Today);
            if (CorruptionRules.SourceResting(book, _character.Corruption, day)) return SourceChallengeRefusal.Resting;
            if (CorruptionRules.AltarsResting(book, _character.Corruption, day) < challenge.MinAltars) return SourceChallengeRefusal.TooFewAltars;
            if (_character.Corruption.DayOf(book.Source.Id) is null && _character.Corruption.Purified.Count >= SavedCorruption.MaxEntries) return SourceChallengeRefusal.Full;
            var fight = new BossFight(challenge.Boss) { Sector = _host != null ? sector : null, Session = Zone };
            BossEvent started;
            try
            {
                started = fight.StartAnew(Zone.Engine);   // refus atomique du moteur : rien n'a changé s'il lève
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return SourceChallengeRefusal.NoRoom;
            }
            if (_sourceFight != null) _fights.Remove(_sourceFight);   // le combat réglé d'un défi précédent de la session
            _sourceFight = fight;
            _sourceSettled = false;
            _sourceWonHere = false;
            _fights.Add(fight);   // après les chefs de la zone : ordre stable des événements
            _bossStarts.Add(started);
            _queuedCorruption.Add(new CorruptionNotice(CorruptionNoticeKind.ChallengeStarted, book.Source.Id, 0, day));
            return SourceChallengeRefusal.None;
        }

        /// <summary>
        /// Sous-lot V2.42.3, à chaque tick (après les chefs) : le gardien vaincu, le personnage debout, purifie la source (jour gardé sous
        /// l'identifiant de la source, expérience, annonce) ; le personnage tombé, parti à plus de <see cref="SourceLeash"/> du disque de la source
        /// ou sorti de la session du combat, le gardien vaincu sans lui, ou une forme qui ne peut pas arriver : le défi est perdu, la forme en
        /// jeu s'en va. Un renouvellement de la session (gel, fonte, fin) ne le perd pas : le combat y continue (OpenedSector). Rend vrai si
        /// toutes les terres viennent de reculer (pas d'annonce terre par terre).
        /// </summary>
        private bool StepSourceChallenge()
        {
            if (!(_sourceFight is BossFight f) || _sourceSettled || !(ActiveCorruption is CorruptionBook book) || !(book.Challenge is CorruptionChallenge challenge))
                return false;
            long day = Math.Min(SavedEvent.MaxDay, Today);
            // Relecture indépendante de la PR #252 : la victoire demande la présence (même règle que la récompense de la première victoire).
            if (f.IsDefeated && !_defeated && _sourceWonHere)
            {
                _sourceSettled = true;
                SavedCorruption saved = _character.Corruption;
                if (saved.DayOf(book.Source.Id) is null && saved.Purified.Count >= SavedCorruption.MaxEntries)
                {
                    _queuedCorruption.Add(new CorruptionNotice(CorruptionNoticeKind.SourceBlocked, book.Source.Id, 0, day));
                    return false;
                }
                _character = _character.WithCorruption(saved.With(book.Source.Id, day));
                long xp = Math.Min(SaveValidator.MaxXp, checked(_character.Xp + challenge.Xp));
                long gained = xp - _character.Xp;
                _character = _character.WithXp(xp);
                _queuedCorruption.Add(new CorruptionNotice(CorruptionNoticeKind.SourcePurified, book.Source.Id, challenge.RespiteDays, day, gained));
                return true;
            }
            bool live = Live(f);
            CombatantView me = Zone.Engine.View(CombatantId);
            bool far = !me.IsPresent || !(me.Position is GroundVector p)
                || CorruptionValidator.Beyond(p, book.Source.Position, checked(book.Source.Radius + SourceLeash))
                || !ReferenceEquals(f.Session, Zone);   // relecture de la PR #252 : sorti de la session du combat (autre secteur, autre session)
            // Relecture indépendante de la PR #252 : une forme qui ne peut pas arriver (combat arrêté) est un échec propre.
            if (!_defeated && live && !far && !f.IsDefeated && !f.IsStalled) return false;
            // Défi perdu : la forme en jeu, présente et vivante, quitte sa session ; le combat quitte la liste (aucune forme à venir).
            // Revue Codex de la PR #252 : comme le départ d'un voyageur (Sectors.Leave), un départ refusé par le moteur (session terminée,
            // plafond de commandes ou d'événements : InvalidOperationException ; forme refusée : ArgumentException) laisse la forme sur place,
            // sans combat ; le défi est réglé quand même (échec annoncé), et aucun tick suivant ne retente le départ.
            if (live && f.Session is Zone z)
                BeforeSourceLeaveForTest?.Invoke(z, f.CurrentForm);
            if (live && f.Session is Zone s && !s.IsOver && System.Linq.Enumerable.Contains(s.Engine.CombatantIds, f.CurrentForm))
            {
                CombatantView v = s.Engine.View(f.CurrentForm);
                if (v.IsPresent && !v.IsDead)
                {
                    try { s.Leave(f.CurrentForm); }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
                }
            }
            if (!f.RewardPending) _fights.Remove(f);
            _sourceSettled = true;
            _queuedCorruption.Add(new CorruptionNotice(CorruptionNoticeKind.ChallengeFailed, book.Source.Id, 0, day));
            return false;
        }
    }
}
