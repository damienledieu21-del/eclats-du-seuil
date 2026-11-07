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
            string key = WorldScarRules.Key(Definition.Id, e.Id, o.Group.Id);
            SavedScar? old = _character.Scars.FirstOrDefault(x => x.Id == key);
            long day = Math.Min(SavedEvent.MaxDay, Math.Max(o.Day, old?.Since ?? 0));
            if (old is null)
            {
                if (!SetScars(_character.Scars.Concat(new[] { new SavedScar(key, 1, day, day) }))) return;
                notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarBorn, e.Id, o.Group.Id, o.Day, 1));
                if (rule.BornFact is string born) _eventFacts.Add(born);
                return;
            }
            int stage = Math.Min(rule.MaxStage, WorldScarRules.StageAt(old, rule, day) + 1);
            SetScars(_character.Scars.Where(x => x.Id != key).Concat(new[] { new SavedScar(key, stage, day, old.Born) }));
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
            CombatantView me = Zone.Engine.View(_character.Id);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || !(me.Radius is Fixed radius)
                || ZonePopulations.Beyond(p, radius, g.Position!.Value, Fixed.Zero, PurifyRange)) return ScarRefusal.TooFar;
            if (_events?.RiftOpenAt(e.Id, g.Id) == true) return ScarRefusal.RiftOpen;
            if (_events?.HauntsRemain(key) == true) return ScarRefusal.HauntsRemain;
            if (Inventory.Available(rule.PurifyItem) < 1) return ScarRefusal.NoItem;
            // Tout est vérifié : l'objet d'abord (refus atomique de l'inventaire), puis le reste, qui ne peut plus échouer.
            if (Inventory.Remove(rule.PurifyItem, 1) != InventoryRefusal.None) return ScarRefusal.NoItem;
            int stage = WorldScarRules.StageAt(_character.Scars.First(x => x.Id == key), rule, Today);
            _character = _character.WithScars(_character.Scars.Where(x => x.Id != key));
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

        private bool KnowsFact(string fact) => Narrative?.Knows(fact) ?? _pendingFacts.Contains(fact);
    }
}
