using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V2.6 : distance au plus entre le bord de l'emprise du personnage et le cœur d'une cicatrice pour la purifier (provisoire).</summary>
        public static readonly Fixed PurifyRange = Fixed.FromInt(3);

        /// <summary>Sous-lot V2.6 : paliers des cicatrices vus par la session (annonce d'un palier gagné avec les jours), ou null avant le premier tick.</summary>
        private Dictionary<string, int>? _scarStagesSeen;

        /// <summary>Sous-lot V2.6 : cicatrices du personnage (sauvegarde v14), toutes zones confondues.</summary>
        public IReadOnlyList<SavedScar> SavedScars => _character.Scars;

        /// <summary>Jour du monde de la zone.</summary>
        private long Today => Zone.Time.Day;

        /// <summary>
        /// Sous-lot V2.6 : cicatrices de cette zone (failles à cicatrice, variante connue), avec leur palier d'aujourd'hui, dans l'ordre des clés.
        /// Une cicatrice d'une autre zone, ou d'une faille ou d'une variante que la zone n'a plus, reste gardée telle quelle sans rien faire ici.
        /// </summary>
        internal IEnumerable<(string Key, WorldEventDefinition Event, WorldEventGroup Group, int Stage)> ScarStates()
        {
            if (!(Definition is ZoneDefinition zone) || !InStoryZone) yield break;
            foreach (SavedScar scar in _character.Scars)
                if (ScarOf(zone, scar.Id) is (WorldEventDefinition e, WorldEventGroup g))
                    yield return (scar.Id, e, g, WorldScarRules.StageAt(scar, e.Scar!, Today));
        }

        private static (WorldEventDefinition, WorldEventGroup)? ScarOf(ZoneDefinition zone, string key)
        {
            foreach (WorldEventDefinition e in zone.Events)
            {
                if (e.Scar is null) continue;
                foreach (WorldEventGroup g in e.Groups)
                    if (WorldScarRules.Key(zone.Id, e.Id, g.Id) == key) return (e, g);
            }
            return null;
        }

        private static readonly IReadOnlyList<ScarView> NoScars = new ScarView[0];

        /// <summary>Relecture de la PR #110 : vues gardées tant que les cicatrices, le jour et les échos ne changent pas (lues plusieurs fois par image).</summary>
        private IReadOnlyList<ScarView> _scarViews = NoScars;
        private IReadOnlyList<SavedScar>? _scarViewsOf;
        private long _scarViewsDay = -1;
        private int _scarViewsVersion = -1;

        /// <summary>
        /// Sous-lot V2.6 : cicatrices de la zone, pour l'affichage (palier, rayon, échos rôdeurs, faille ouverte à leur place). Relecture de la
        /// PR #110 : recalculées seulement quand les cicatrices du personnage, le jour du monde ou les échos changent ; sinon la même liste.
        /// </summary>
        public IReadOnlyList<ScarView> Scars
        {
            get
            {
                IReadOnlyList<SavedScar> saved = _character.Scars;
                if (saved.Count == 0) return NoScars;
                long today = Today;
                int version = _events?.Version ?? 0;
                if (ReferenceEquals(saved, _scarViewsOf) && today == _scarViewsDay && version == _scarViewsVersion) return _scarViews;
                var list = new List<ScarView>(saved.Count);
                if (Definition is ZoneDefinition zone && InStoryZone)
                    for (int i = 0; i < saved.Count; i++)
                    {
                        SavedScar s = saved[i];
                        if (!(ScarOf(zone, s.Id) is (WorldEventDefinition e, WorldEventGroup g))) continue;
                        int stage = WorldScarRules.StageAt(s, e.Scar!, today);
                        list.Add(new ScarView(s.Id, e, g, g.Position!.Value, stage, e.Scar!.RadiusAt(stage), s.Born, WorldScarRules.NextStageDay(s, e.Scar, today),
                            _events?.HauntsOf(s.Id) ?? Array.Empty<string>(), _events?.RiftOpenAt(e.Id, g.Id) ?? false));
                    }
                _scarViews = list.Count == 0 ? NoScars : list;
                _scarViewsOf = saved;
                _scarViewsDay = today;
                _scarViewsVersion = version;
                return _scarViews;
            }
        }

        /// <summary>Sous-lot V2.6 : palier d'aujourd'hui de la cicatrice de la variante <paramref name="group"/> de <paramref name="e"/>, 0 sans cicatrice.</summary>
        public int ScarStage(WorldEventDefinition e, string group)
        {
            if (e?.Scar is null || Definition is null) return 0;
            string key = WorldScarRules.Key(Definition.Id, e.Id, group);
            return _character.Scars.FirstOrDefault(x => x.Id == key) is SavedScar s ? WorldScarRules.StageAt(s, e.Scar, Today) : 0;
        }

        /// <summary>Sous-lot V2.6 : force (pour cent) des échos d'une faille qui s'ouvre sur une cicatrice ; 100 sans cicatrice.</summary>
        internal long ScarStrength(WorldEventDefinition e, string group) => e.Scar is WorldEventScar rule ? rule.StrengthAt(Math.Max(1, ScarStage(e, group))) : 100;

        /// <summary>Écrit les cicatrices ; faux, sans rien changer, si elles ne tiennent pas (borne technique).</summary>
        private bool SetScars(IEnumerable<SavedScar> scars)
        {
            var list = scars.ToList();
            if (list.Count > SavedScar.MaxEntries) return false;
            _character = _character.WithScars(list);
            return true;
        }

        /// <summary>
        /// Sous-lot V2.6 : l'occurrence <paramref name="o"/> de la faille <paramref name="e"/> est restée ouverte (le personnage était là ou
        /// ailleurs) : une cicatrice naît à sa place (palier 1), ou celle qui y est gagne un palier (au plus le maximum) ; elle grandit ensuite à
        /// partir du jour de l'occurrence. Rien si les cicatrices sont pleines (borne technique, vérifiée au chargement des zones).
        /// </summary>
        private void ScarFromLost(WorldEventDefinition e, EventOccurrence o, List<WorldEventNotice> notices)
        {
            if (!(e.Scar is WorldEventScar rule) || Definition is null) return;
            // Revue Codex de la PR #112 (tour 2) : les propagations dues jusqu'au jour de l'occurrence sont faites d'abord (ordre des jours) :
            // recreuser la source avant les aurait effacées (une reprise après une absence règle la faille avant le rattrapage du tick).
            SpreadScars(notices, o.Day);
            string key = WorldScarRules.Key(Definition.Id, e.Id, o.Group.Id);
            SavedScar? old = FindScar(key);
            long day = Math.Min(SavedEvent.MaxDay, Math.Max(o.Day, old?.Since ?? 0));
            if (old is null)
            {
                if (!SetScars(_character.Scars.Concat(new[] { new SavedScar(key, 1, day, day) }))) return;
                notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarBorn, e.Id, o.Group.Id, o.Day, 1));
                if (rule.BornFact is string born) _eventFacts.Add(born);
                return;
            }
            SavedScar deeper = WorldScarRules.Deepen(old, rule, day);
            SetScars(_character.Scars.Where(x => x.Id != key).Concat(new[] { deeper }));
            int stage = WorldScarRules.StageAt(deeper, rule, day);
            notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarGrew, e.Id, o.Group.Id, o.Day, stage));
            if (_scarStagesSeen != null) _scarStagesSeen[key] = stage;
        }

        /// <summary>
        /// Sous-lot V2.6 : une faille de <paramref name="e"/> refermée à la variante <paramref name="group"/> : la cicatrice qui y est recule d'un
        /// palier (effacée au palier 1) ; les autres cicatrices de la faille s'arrêtent de grandir (leur palier d'aujourd'hui repart d'aujourd'hui).
        /// </summary>
        internal void ScarRecede(WorldEventDefinition e, string group, long occurrenceDay, List<WorldEventNotice> notices)
        {
            if (!(e.Scar is WorldEventScar rule) || Definition is null) return;
            SpreadScars(notices);   // revue Codex de la PR #112 (tour 2) : les propagations dues d'abord, puis le recul
            string key = WorldScarRules.Key(Definition.Id, e.Id, group);
            long today = Math.Min(SavedEvent.MaxDay, Today);
            string prefix = WorldScarRules.Key(Definition.Id, e.Id, string.Empty);
            var next = new List<SavedScar>();
            foreach (SavedScar s in _character.Scars)
            {
                if (!s.Id.StartsWith(prefix, StringComparison.Ordinal) || today < s.Since) { next.Add(s); continue; }
                int stage = WorldScarRules.StageAt(s, rule, today);
                if (s.Id != key) next.Add(new SavedScar(s.Id, stage, today, s.Born));
                else if (stage > 1)
                {
                    next.Add(new SavedScar(s.Id, stage - 1, today, s.Born));
                    notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarReceded, e.Id, group, occurrenceDay, stage - 1));
                }
                else notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarHealed, e.Id, group, occurrenceDay, 0));
            }
            SetScars(next);
            _scarStagesSeen = null;   // revu au tick suivant, sans annonce
        }

        /// <summary>
        /// Sous-lot V2.6 : purifie la cicatrice <paramref name="key"/> avec un <see cref="WorldEventScar.PurifyItem"/> (un éclat de Seuil) : le
        /// personnage est à <see cref="PurifyRange"/> de son cœur, aucune faille n'est ouverte à sa place, aucun de ses échos rôdeurs n'est
        /// debout. L'objet est pris, la cicatrice effacée, l'expérience donnée (sous le plafond), le fait appris ; annoncé au tick suivant.
        /// Refus atomique : tout est vérifié avant la moindre mutation.
        /// </summary>
        public ScarRefusal PurifyScar(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (_defeated) return ScarRefusal.Defeated;
            if (!(Definition is ZoneDefinition zone) || !InStoryZone || !(ScarOf(zone, key) is (WorldEventDefinition e, WorldEventGroup g))
                || !_character.Scars.Any(x => x.Id == key)) return ScarRefusal.NoScar;
            WorldEventScar rule = e.Scar!;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || !(me.Radius is Fixed radius)
                || ZonePopulations.Beyond(p, radius, g.Position!.Value, Fixed.Zero, PurifyRange)) return ScarRefusal.TooFar;
            if (_events?.RiftOpenAt(e.Id, g.Id) == true) return ScarRefusal.RiftOpen;
            if (_events?.HauntsRemain(key) == true) return ScarRefusal.HauntsRemain;
            if (Inventory.Available(rule.PurifyItem) < 1) return ScarRefusal.NoItem;
            // Tout est vérifié : l'objet d'abord (refus atomique de l'inventaire), puis le reste, qui ne peut plus échouer.
            if (Inventory.Remove(rule.PurifyItem, 1) != InventoryRefusal.None) return ScarRefusal.NoItem;
            // Relecture de la PR #112 : les propagations dues (une session reprise après une absence, avant son premier tick) sont faites
            // d'abord, à leurs jours ; le répit ne part qu'ensuite (sinon il effacerait des propagations passées). Aucune ne vise cette place.
            SpreadScars(_queuedNotices);
            int stage = WorldScarRules.StageAt(_character.Scars.First(x => x.Id == key), rule, Today);
            _character = _character.WithScars(_character.Scars.Where(x => x.Id != key));
            RebaseSpreaders(e, g.Id, Math.Min(SavedEvent.MaxDay, Today));   // sous-lot V2.6.1 : répit avant que la voisine ne la rouvre
            Narrative?.Purified(e.Id);   // sous-lot V2.6.1 : objectif « purify » (quête de Sœur Albe)
            long xp = Math.Min(SaveValidator.MaxXp, checked(_character.Xp + rule.PurifyXp));
            long gained = xp - _character.Xp;
            _character = _character.WithXp(xp);
            _queuedNotices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarPurified, e.Id, g.Id, Today, stage, gained));
            // Relecture de la PR #110 : le fait est appris tout de suite, avec l'expérience et l'effacement (une sauvegarde le garde).
            if (rule.PurifiedFact is string fact)
            {
                if (Narrative != null) Narrative.LearnQueued(fact);
                else if (!_pendingFacts.Contains(fact)) _pendingFacts.Add(fact);
            }
            _scarStagesSeen = null;
            return ScarRefusal.None;
        }

        /// <summary>
        /// Sous-lot V2.6, à chaque tick après les événements : une cicatrice qui gagne un palier avec les jours est annoncée (pas au premier
        /// tick de la session : les paliers sont seulement notés), et la première fois qu'une cicatrice atteint le palier de ses échos rôdeurs,
        /// le fait « profonde » est appris (les PNJ en parlent).
        /// </summary>
        private void StepScars(List<WorldEventNotice> notices)
        {
            if (_character.Scars.Count == 0) return;
            SpreadScars(notices);   // sous-lot V2.6.1 : avant les paliers, pour qu'une cicatrice née d'une autre soit notée sans annonce de croissance
            bool first = _scarStagesSeen is null;
            _scarStagesSeen ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((string key, WorldEventDefinition e, WorldEventGroup g, int stage) in ScarStates())
            {
                if (!first && _scarStagesSeen.TryGetValue(key, out int seen) && stage > seen)
                    notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarGrew, e.Id, g.Id, Today, stage));
                _scarStagesSeen[key] = stage;
                if (stage >= e.Scar!.HauntStage && e.Scar.DeepFact is string deep && !KnowsFact(deep) && !_eventFacts.Contains(deep)) _eventFacts.Add(deep);
            }
        }

        /// <summary>
        /// Sous-lot V2.6.1 : propagation des cicatrices au lieu voisin, recalculée du temps du monde (le même résultat que la partie ait été
        /// jouée ou non pendant ces jours). Une cicatrice qui a atteint le palier de propagation depuis <see cref="WorldEventScar.SpreadDays"/>
        /// jours fait naître une cicatrice (palier 1, née ce jour-là) chez la première de ses voisines (<see cref="WorldEventGroup.SpreadTo"/>)
        /// qui n'en a pas ; son délai repart de ce jour pour la suivante. Les propagations dues sont faites dans l'ordre de leur jour (à égalité :
        /// ordre des variantes, puis des voisines), chacune pouvant en permettre une autre plus tard dans le même rattrapage. <b>Bornée</b> : une
        /// propagation ne naît que là où il n'y a pas de cicatrice, au plus une par variante : au plus autant de tours que de variantes, aucune
        /// boucle possible. Rien si les cicatrices sont pleines (borne technique, vérifiée au chargement des zones).
        /// </summary>
        /// <param name="until">Revue Codex de la PR #112 (tour 2) : jour jusqu'auquel rattraper (le jour d'une occurrence réglée avant le tick) ;
        /// absent : aujourd'hui.</param>
        private void SpreadScars(List<WorldEventNotice> notices, long? until = null)
        {
            if (!(Definition is ZoneDefinition zone) || !InStoryZone) return;
            long today = Math.Min(SavedEvent.MaxDay, Math.Min(Today, until ?? long.MaxValue));
            foreach (WorldEventDefinition e in zone.Events)
            {
                if (!(e.Scar is WorldEventScar rule) || !rule.Spreads) continue;
                for (int round = 0; round < e.Groups.Count; round++)
                {
                    SavedScar? source = null;
                    WorldEventGroup? from = null, to = null;
                    long best = long.MaxValue;
                    foreach (WorldEventGroup g in e.Groups)
                    {
                        if (g.SpreadTo.Count == 0 || !(FindScar(WorldScarRules.Key(zone.Id, e.Id, g.Id)) is SavedScar s)) continue;
                        if (!(WorldScarRules.SpreadReachDay(s, rule) is long reach)) continue;
                        long day = checked(reach + rule.SpreadDays);
                        if (day > today || day >= best) continue;
                        string? free = g.SpreadTo.FirstOrDefault(n => FindScar(WorldScarRules.Key(zone.Id, e.Id, n)) is null);
                        if (free is null) continue;
                        (best, source, from, to) = (day, s, g, e.Groups.First(x => x.Id == free));
                    }
                    if (source is null || from is null || to is null) break;
                    string born = WorldScarRules.Key(zone.Id, e.Id, to.Id);
                    SavedScar rebased = WorldScarRules.RestartSpread(source, rule, best);
                    if (!SetScars(_character.Scars.Where(x => x.Id != source.Id).Concat(new[] { rebased, new SavedScar(born, 1, best, best) }))) break;
                    notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarSpread, e.Id, to.Id, best, 1, spreadFrom: from.Id));
                    if (rule.SpreadFact is string fact && !KnowsFact(fact) && !_eventFacts.Contains(fact)) _eventFacts.Add(fact);
                }
            }
        }

        private SavedScar? FindScar(string key)
        {
            IReadOnlyList<SavedScar> scars = _character.Scars;
            for (int i = 0; i < scars.Count; i++)
                if (scars[i].Id == key) return scars[i];
            return null;
        }

        /// <summary>
        /// Sous-lot V2.6.1 : après la purification de la variante <paramref name="purified"/>, les cicatrices qui se propagent vers elle
        /// (<see cref="WorldEventGroup.SpreadTo"/>) et ont déjà atteint le palier de propagation font repartir leur délai d'aujourd'hui
        /// (<see cref="WorldScarRules.RestartSpread"/> : leur croissance n'est pas touchée) : la place purifiée a
        /// <see cref="WorldEventScar.SpreadDays"/> jours de répit avant que le froid ne revienne (sans cela, une voisine au palier 4 depuis
        /// longtemps la rouvrirait au tick suivant). Relecture de la PR #112 : seules les sources de cette place, pas toutes les cicatrices de la faille.
        /// </summary>
        private void RebaseSpreaders(WorldEventDefinition e, string purified, long today)
        {
            if (!(e.Scar is WorldEventScar rule) || !rule.Spreads || Definition is null) return;
            var sources = new HashSet<string>(e.Groups.Where(g => g.SpreadTo.Contains(purified)).Select(g => WorldScarRules.Key(Definition.Id, e.Id, g.Id)),
                StringComparer.Ordinal);
            var next = new List<SavedScar>(_character.Scars.Count);
            bool changed = false;
            foreach (SavedScar s in _character.Scars)
            {
                if (sources.Contains(s.Id) && today >= s.Since && WorldScarRules.StageAt(s, rule, today) >= rule.SpreadStage
                    && WorldScarRules.RestartSpread(s, rule, today) is SavedScar r && (r.Since != s.Since || r.Stage != s.Stage))
                {
                    next.Add(r);
                    changed = true;
                }
                else next.Add(s);
            }
            if (changed) SetScars(next);
        }

        /// <summary>
        /// Sous-lot V2.6.1 : cicatrices gardées de la faille <paramref name="eventId"/> de la zone de l'histoire (conditions <c>scarsAtLeast</c>
        /// et <c>scarsBelow</c> des dialogues : lues aussi depuis un intérieur, la chapelle de Sœur Albe).
        /// </summary>
        int INarrativeWorld.ScarCount(string eventId)
        {
            string? zone = _storyZone ?? Definition?.Id;
            if (zone is null) return 0;
            string prefix = WorldScarRules.Key(zone, eventId, string.Empty);
            int n = 0;
            foreach (SavedScar s in _character.Scars)
                if (s.Id.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        private bool KnowsFact(string fact) => Narrative?.Knows(fact) ?? _pendingFacts.Contains(fact);
    }
}
