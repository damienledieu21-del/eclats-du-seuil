using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.5 : une occurrence d'un événement du monde, tirée pour une fenêtre de jours. Les minutes sont absolues : minutes du monde
    /// depuis minuit du jour 0 (<c>jour × 1 440 + minute</c>).
    /// </summary>
    public sealed class EventOccurrence
    {
        public WorldEventDefinition Event { get; }
        public long Window { get; }
        public long Day { get; }
        public WorldEventGroup Group { get; }
        public long AnnounceAt { get; }
        public long StartAt { get; }
        public long EndAt { get; }

        internal EventOccurrence(WorldEventDefinition e, long window, long day, int minute, WorldEventGroup group)
        {
            Event = e;
            Window = window;
            Day = day;
            Group = group;
            StartAt = checked(day * WorldClock.MinutesPerDay + minute);
            AnnounceAt = checked(StartAt - e.AnnounceMinutes);
            EndAt = checked(StartAt + e.DurationMinutes);
        }

        public override string ToString() => $"{Event.Id} jour {Day} {StartAt % WorldClock.MinutesPerDay / 60:00} h {StartAt % 60:00} ({Group.Id})";
    }

    /// <summary>
    /// Sous-lot V2.5 : calendrier des événements du monde, recalculé du temps du monde, sans horloge réelle ni état. Les jours du monde sont
    /// pris par fenêtres de <see cref="WorldEventDefinition.EveryDays"/> jours ; pour la fenêtre <c>k</c>, un <see cref="Pcg32"/> initialisé
    /// par l'empreinte FNV-1a de la clé <c>zone.vallons/event/attaque_hameau/k</c> tire, toujours dans le même ordre : la chance, le jour dans
    /// la fenêtre, la minute de départ, la variante. Un événement qui a son temps (<see cref="WorldEventDefinition.Weather"/>) n'a lieu que si le
    /// temps de la zone, au départ, en est un. La même partie a donc toujours les mêmes événements.
    /// </summary>
    public static class WorldEventSchedule
    {
        /// <summary>Fenêtres examinées au plus pour trouver l'occurrence précédente ou suivante.</summary>
        public const int MaxSearch = 400;

        public static string Key(string zone, string eventId, long window) =>
            zone + "/event/" + eventId + "/" + window.ToString(CultureInfo.InvariantCulture);

        /// <summary>Minute absolue d'une heure du monde.</summary>
        public static long Absolute(WorldTime time) => checked(time.Day * WorldClock.MinutesPerDay + time.Minute);

        /// <summary>Premier tick du monde dont l'heure est au moins la minute absolue <paramref name="minute"/>.</summary>
        public static long TickAt(WorldClock clock, long minute)
        {
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            long since = checked(minute - clock.StartMinute);
            if (since <= 0) return 0;
            long num = checked(since * clock.DayLengthMs), den = checked(clock.TickMilliseconds * WorldClock.MinutesPerDay);
            return checked((num + den - 1) / den);
        }

        /// <summary>Occurrence de la fenêtre <paramref name="window"/>, ou null (pas de chance, avant le premier jour, pas son temps).</summary>
        /// <param name="weatherZone">Zone dont le temps compte (celle de l'histoire, comme pour les populations).</param>
        public static EventOccurrence? InWindow(string zone, string weatherZone, WorldEventDefinition e, long window)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            if (window < 0) return null;
            var random = new Pcg32(Loot.Fnv1a(Key(zone, e.Id, window)), Loot.Fnv1a("event:" + e.Id));
            uint roll = random.NextBounded(100);
            uint offset = random.NextBounded((uint)e.EveryDays);
            uint spread = random.NextBounded(checked((uint)e.SpreadMinutes + 1));
            uint pick = random.NextBounded((uint)e.TotalWeight);
            if (roll >= (uint)e.ChancePercent) return null;
            long day = checked(window * e.EveryDays + offset);
            if (day < e.FirstDay) return null;
            int minute = checked(e.StartMinute + (int)spread);
            // Un départ après minuit (une faille à 1 h) se juge au temps de ce jour-là ; l'occurrence garde le jour de sa fenêtre.
            long at = checked(day * WorldClock.MinutesPerDay + minute);
            if (e.Weather is PopulationWeather w
                && !ZonePopulations.Fits(w, WorldWeather.At(weatherZone, at / WorldClock.MinutesPerDay, (int)(at % WorldClock.MinutesPerDay)).Kind)) return null;
            WorldEventGroup group = e.Groups[e.Groups.Count - 1];
            uint acc = 0;
            foreach (WorldEventGroup g in e.Groups)
            {
                acc = checked(acc + (uint)g.Weight);
                if (pick < acc) { group = g; break; }
            }
            return new EventOccurrence(e, window, day, minute, group);
        }

        /// <summary>Occurrence en cours ou annoncée à la minute absolue <paramref name="now"/> (annonce ≤ maintenant &lt; fin), ou null.</summary>
        public static EventOccurrence? Around(string zone, string weatherZone, WorldEventDefinition e, long now)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            long window = Math.Max(0, now / WorldClock.MinutesPerDay) / e.EveryDays;
            // Une occurrence s'annonce au plus 12 h avant et dure au plus 12 h : la fenêtre en cours, la précédente et la suivante suffisent.
            for (long w = window + 1; w >= window - 1; w--)
                if (InWindow(zone, weatherZone, e, w) is EventOccurrence o && o.AnnounceAt <= now && now < o.EndAt) return o;
            return null;
        }

        /// <summary>Dernière occurrence finie à la minute absolue <paramref name="now"/> (fin ≤ maintenant), ou null.</summary>
        public static EventOccurrence? LastEnded(string zone, string weatherZone, WorldEventDefinition e, long now)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            long window = checked(Math.Max(0, now / WorldClock.MinutesPerDay) / e.EveryDays + 1);
            for (int i = 0; i < MaxSearch && window - i >= 0; i++)
                if (InWindow(zone, weatherZone, e, window - i) is EventOccurrence o && o.EndAt <= now) return o;
            return null;
        }

        /// <summary>Prochaine occurrence qui commence à la minute absolue <paramref name="from"/> ou après, ou null.</summary>
        public static EventOccurrence? Next(string zone, string weatherZone, WorldEventDefinition e, long from)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            long window = Math.Max(0, from / WorldClock.MinutesPerDay) / e.EveryDays;
            for (int i = 0; i < MaxSearch; i++)
                if (InWindow(zone, weatherZone, e, checked(window + i)) is EventOccurrence o && o.StartAt >= from) return o;
            return null;
        }

        /// <summary>Occurrence du jour <paramref name="day"/>, ou null.</summary>
        public static EventOccurrence? OnDay(string zone, string weatherZone, WorldEventDefinition e, long day)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            if (day < 0) return null;
            EventOccurrence? o = InWindow(zone, weatherZone, e, day / e.EveryDays);
            return o != null && o.Day == day ? o : null;
        }

        /// <summary>Clé du registre d'un personnage pour l'événement <paramref name="eventId"/> de la zone <paramref name="zone"/>.</summary>
        public static string RegistryKey(string zone, string eventId) => zone + "/" + eventId;
    }

    /// <summary>Sous-lot V2.5 : ce qu'un événement du monde annonce à un tick.</summary>
    public enum WorldEventNoticeKind
    {
        /// <summary>Annonce à l'avance (« Des feux sur la crête… »).</summary>
        Announced = 0,

        /// <summary>Départ : la cloche du hameau, la faille qui s'ouvre, la meute qui sort du bois.</summary>
        Started = 1,

        /// <summary>Faille : une nouvelle vague d'échos (<see cref="WorldEventNotice.Wave"/>).</summary>
        Wave = 2,

        /// <summary>Réussi (le hameau défendu, la faille refermée).</summary>
        Won = 3,

        /// <summary>Manqué pendant que le personnage était dans la zone.</summary>
        Lost = 4,

        /// <summary>Manqué pendant que le personnage était ailleurs (annoncé à son retour).</summary>
        Missed = 5,

        /// <summary>Récompense de la première réussite donnée (<see cref="WorldEventNotice.Xp"/>, <see cref="WorldEventNotice.Money"/>, objets).</summary>
        Rewarded = 6,

        /// <summary>Récompense de la première réussite qui attend une place dans le sac (annoncé une fois par session).</summary>
        RewardWaiting = 7,

        /// <summary>Une réussite de plus : les remerciements, sans récompense (déjà donnée).</summary>
        Thanks = 8,

        /// <summary>Fin d'une chasse : les bêtes de l'événement s'en vont.</summary>
        Ended = 9,
    }

    public sealed class WorldEventNotice
    {
        public WorldEventNoticeKind Kind { get; }
        public string Event { get; }
        public string Group { get; }
        public long Day { get; }
        public int Wave { get; }
        public long Xp { get; }
        public long Money { get; }
        public IReadOnlyList<(string Item, int Count)> Items { get; }

        internal WorldEventNotice(WorldEventNoticeKind kind, string ev, string group, long day, int wave = 0, long xp = 0, long money = 0,
            IReadOnlyList<(string, int)>? items = null)
        {
            Kind = kind;
            Event = ev;
            Group = group;
            Day = day;
            Wave = wave;
            Xp = xp;
            Money = money;
            Items = items ?? Array.Empty<(string, int)>();
        }
    }

    /// <summary>Sous-lot V2.5 : où en est un événement suivi dans la session.</summary>
    public enum WorldEventPhase
    {
        Announced = 0,
        Running = 1,

        /// <summary>Fin passée : les créatures qui restent s'en vont dès qu'elles sont au calme.</summary>
        Ending = 2,
    }

    /// <summary>Sous-lot V2.5 : un événement suivi dans la session, pour l'affichage (annonce, faille, conséquences).</summary>
    public sealed class WorldEventView
    {
        public WorldEventDefinition Event { get; }
        public EventOccurrence Occurrence { get; }
        public WorldEventPhase Phase { get; }

        /// <summary>Faille ouverte (une faille, en cours, pas encore refermée) : sa place.</summary>
        public GroundVector? Rift { get; }

        /// <summary>Créatures de l'événement debout dans la zone.</summary>
        public IReadOnlyList<string> Creatures { get; }

        /// <summary>Ticks de la session (prochain tick simulé) du départ et de la fin.</summary>
        public long StartTick { get; }
        public long EndTick { get; }

        internal WorldEventView(WorldEventDefinition e, EventOccurrence o, WorldEventPhase phase, GroundVector? rift, IReadOnlyList<string> creatures, long startTick, long endTick)
        {
            Event = e;
            Occurrence = o;
            Phase = phase;
            Rift = rift;
            Creatures = creatures;
            StartTick = startTick;
            EndTick = endTick;
        }
    }

    /// <summary>Sous-lot V2.5 : refus de <see cref="WorldSession.CloseRift"/> (refus atomique : rien ne change).</summary>
    public enum RiftRefusal
    {
        None = 0,

        /// <summary>Aucune faille ouverte de cet événement.</summary>
        NoRift = 1,

        /// <summary>Le personnage n'est pas assez près (<see cref="WorldEvents.CloseRange"/>), ou n'est pas là.</summary>
        TooFar = 2,

        /// <summary>Des échos de la faille sont encore debout.</summary>
        EchoesRemain = 3,
    }

    /// <summary>
    /// Sous-lot V2.5 : les événements du monde d'une session de zone (attaque du hameau, faille du Seuil, chasse de la meute). Tout se décide
    /// du temps du monde (<see cref="WorldEventSchedule"/>) et de l'état du moteur, tick après tick : la même partie donne les mêmes
    /// événements. Les créatures d'un événement prennent le modèle d'une population de la zone (réglée au niveau du joueur) et arrivent sous
    /// une identité nouvelle (<c>bandit_epee3_ev1</c>) ; elles ne prennent jamais les <see cref="ZonePopulations.QuestReserve"/> dernières
    /// places de combattants : la place d'une variante entière est réservée avant la première arrivée (sinon l'occurrence n'a pas lieu, réglée
    /// sans issue). Une occurrence est réglée une seule fois par personnage (registre de la sauvegarde, version 13).
    /// </summary>
    internal sealed class WorldEvents
    {
        /// <summary>Distance au plus entre le bord de l'emprise du personnage et le centre de la faille pour la refermer (provisoire).</summary>
        public static readonly Fixed CloseRange = Fixed.FromInt(3);

        private sealed class Slot
        {
            public WorldEventCreature Creature = null!;
            public Population Like = null!;
            public string? Current;
            public bool Dead;
            public bool Gone;
            public bool Hurt;
            public bool Leashing;

            /// <summary>Partie d'elle-même à la fin de l'événement (pas une victoire).</summary>
            public bool Withdrawn;

            public long? RetryAt;
        }

        private sealed class Run
        {
            public EventOccurrence Occurrence = null!;
            public bool Announced;
            public bool Started;
            public bool Ending;
            public bool Closed;
            public int WavesSpawned;
            public long NextWaveTick;
            public readonly List<Slot> Slots = new List<Slot>();
        }

        private readonly ZoneDefinition _zone;
        private readonly string _weatherZone;
        private readonly Dictionary<string, Run?> _runs = new Dictionary<string, Run?>(StringComparer.Ordinal);
        private readonly Dictionary<string, Population> _byCreature = new Dictionary<string, Population>(StringComparer.Ordinal);
        private readonly HashSet<string> _waitingAnnounced = new HashSet<string>(StringComparer.Ordinal);
        private int _counter;
        private bool _opened;
        private long _minuteSeen = -1;

        public WorldEvents(ZoneDefinition zone, string weatherZone)
        {
            _zone = zone;
            _weatherZone = weatherZone;
            foreach (WorldEventDefinition e in zone.Events) _runs[e.Id] = null;
        }

        /// <summary>Population modèle d'une créature d'événement, ou null.</summary>
        public Population? Of(string combatant) => _byCreature.TryGetValue(combatant, out Population p) ? p : null;

        /// <summary>Le PNJ <paramref name="npc"/> est réfugié (attaque en cours) : sa place de refuge, sinon null.</summary>
        public GroundVector? RefugeOf(string npc)
        {
            foreach (Run? run in _runs.Values)
                if (run is { Started: true } r && r.Occurrence.Event.Refuge is GroundVector refuge && !Settled(r) && r.Occurrence.Event.Villagers.Contains(npc))
                    return refuge;
            return null;
        }

        private static bool Settled(Run r) => r.Closed || (r.Ending && r.Slots.All(s => s.Current is null || s.Dead || s.Gone));

        public IReadOnlyList<WorldEventView> Views(Zone zone)
        {
            var list = new List<WorldEventView>();
            foreach (WorldEventDefinition e in _zone.Events)
            {
                if (!(_runs[e.Id] is Run r)) continue;
                var alive = r.Slots.Where(s => s.Current != null && !s.Dead && !s.Gone).Select(s => s.Current!).ToList();
                WorldEventPhase phase = r.Ending ? WorldEventPhase.Ending : r.Started ? WorldEventPhase.Running : WorldEventPhase.Announced;
                GroundVector? rift = e.Kind == WorldEventKind.Rift && r.Started && !r.Closed && !r.Ending ? r.Occurrence.Group.Position : null;
                list.Add(new WorldEventView(e, r.Occurrence, phase, rift, alive, SessionTick(zone, r.Occurrence.StartAt), SessionTick(zone, r.Occurrence.EndAt)));
            }
            return list;
        }

        private static long SessionTick(Zone zone, long minute) => checked(WorldEventSchedule.TickAt(zone.Clock, minute) - zone.StartWorldTick);

        /// <summary>
        /// Après un tick de la zone (et des populations) : annonces, départs, arrivées, vagues, laisses, issues ; les réglages passent par
        /// <paramref name="world"/> (registre, récompenses, faits).
        /// </summary>
        public IReadOnlyList<WorldEventNotice> Step(WorldSession world, Zone zone, int reserved)
        {
            CombatEngine engine = zone.Engine;
            if (engine.IsFinished) return Array.Empty<WorldEventNotice>();
            var notices = new List<WorldEventNotice>();
            long nowTick = engine.CurrentTick;
            long now = WorldEventSchedule.Absolute(zone.Clock.At(checked(zone.StartWorldTick + nowTick)));
            if (!_opened)
            {
                _opened = true;
                Open(world, now, notices);
            }
            bool minuteChanged = now != _minuteSeen;
            _minuteSeen = now;
            foreach (WorldEventDefinition e in _zone.Events)
            {
                Run? run = _runs[e.Id];
                if (run is null)
                {
                    if (!minuteChanged) continue;
                    if (!(WorldEventSchedule.Around(_zone.Id, _weatherZone, e, now) is EventOccurrence o)) continue;
                    if (e.Settles && world.EventEntry(e).Last >= o.Day) continue;   // déjà réglée pour ce personnage
                    _runs[e.Id] = run = new Run { Occurrence = o };
                }
                StepRun(world, zone, run, now, nowTick, reserved, notices);
                if (Settled(run) || (run.Ending && run.Slots.Count == 0))
                {
                    if (!run.Closed && e.Settles) Settle(world, run, notices);
                    else if (!e.Settles) notices.Add(new WorldEventNotice(WorldEventNoticeKind.Ended, e.Id, run.Occurrence.Group.Id, run.Occurrence.Day));
                    _runs[e.Id] = null;
                }
            }
            // Récompense due (sac plein à la réussite, ou dans une session précédente) : donnée dès qu'elle tient.
            foreach (WorldEventDefinition e in _zone.Events)
                if (e.Settles && world.EventEntry(e).Owed)
                {
                    if (world.GiveEventReward(e, notices)) _waitingAnnounced.Remove(e.Id);
                    else if (_waitingAnnounced.Add(e.Id)) notices.Add(new WorldEventNotice(WorldEventNoticeKind.RewardWaiting, e.Id, string.Empty, world.EventEntry(e).Last));
                }
            return notices;
        }

        /// <summary>
        /// Début de la session : pour chaque événement à issue, une première ligne du registre (aucune occurrence passée ne compte pour un
        /// personnage qui découvre l'événement) ; sinon, la dernière occurrence finie et pas encore réglée est manquée (le personnage était
        /// ailleurs). Une seule, la plus récente.
        /// </summary>
        private void Open(WorldSession world, long now, List<WorldEventNotice> notices)
        {
            foreach (WorldEventDefinition e in _zone.Events)
            {
                if (!e.Settles) continue;
                EventOccurrence? last = WorldEventSchedule.LastEnded(_zone.Id, _weatherZone, e, now);
                if (!world.HasEventEntry(e))
                {
                    world.SetEventEntry(e, new SavedEvent(WorldEventSchedule.RegistryKey(_zone.Id, e.Id), 0, 0, last?.Day ?? -1, EventOutcome.None));
                    continue;
                }
                if (last != null && last.Day > world.EventEntry(e).Last) world.SettleEvent(e, last, EventOutcome.Lost, WorldEventNoticeKind.Missed, notices);
            }
        }

        private void StepRun(WorldSession world, Zone zone, Run run, long now, long nowTick, int reserved, List<WorldEventNotice> notices)
        {
            CombatEngine engine = zone.Engine;
            EventOccurrence o = run.Occurrence;
            WorldEventDefinition e = o.Event;
            long tickMs = engine.TickMilliseconds;
            if (!run.Announced && !run.Started && now < o.StartAt)
            {
                run.Announced = true;
                if (e.AnnounceMinutes > 0) notices.Add(new WorldEventNotice(WorldEventNoticeKind.Announced, e.Id, o.Group.Id, o.Day));
            }
            if (!run.Started && now >= o.StartAt)
            {
                run.Started = true;
                run.Announced = true;
                if (!Reserve(zone, reserved, o.Group.Creatures.Count))
                {
                    // Plus de place hors de la réserve des quêtes : l'occurrence n'a pas lieu, réglée sans issue (ni victoire ni pillage).
                    run.Ending = true;
                    if (e.Settles) world.SettleEvent(e, o, EventOutcome.None, null, notices);
                    run.Closed = true;
                    return;
                }
                notices.Add(new WorldEventNotice(WorldEventNoticeKind.Started, e.Id, o.Group.Id, o.Day, 1));
                AddWave(run, world);
                run.WavesSpawned = 1;
                run.NextWaveTick = checked(nowTick + e.WaveMs / tickMs);
            }
            if (!run.Started) return;
            // Faille : vague suivante, si la faille est encore ouverte et qu'il reste de la place hors de la réserve.
            if (e.Kind == WorldEventKind.Rift && !run.Closed && !run.Ending && run.WavesSpawned < e.Waves && nowTick >= run.NextWaveTick)
            {
                run.WavesSpawned++;
                run.NextWaveTick = checked(nowTick + e.WaveMs / tickMs);
                if (Reserve(zone, reserved, o.Group.Creatures.Count))
                {
                    AddWave(run, world);
                    notices.Add(new WorldEventNotice(WorldEventNoticeKind.Wave, e.Id, o.Group.Id, o.Day, run.WavesSpawned));
                }
            }
            foreach (Slot s in run.Slots) StepSlot(zone, s, nowTick, tickMs, reserved);
            // Attaque : plus aucun assaillant debout (vaincus, ou enfuis faute de place pour revenir), même après la fin si le combat a continué.
            if (e.Kind == WorldEventKind.Raid && run.Slots.All(s => s.Dead || (s.Gone && !s.Withdrawn)) && (!run.Ending || run.Slots.Any(s => s.Dead && s.Hurt)))
            {
                run.Closed = true;
                if (run.Slots.Any(s => s.Hurt)) world.SettleEvent(e, o, EventOutcome.Won, null, notices);
                else world.SettleEvent(e, o, EventOutcome.None, null, notices);
                return;
            }
            if (!run.Ending && now >= o.EndAt) run.Ending = true;
            if (run.Ending)
                foreach (Slot s in run.Slots)
                    if (s.Current != null && !s.Dead && !s.Gone && !s.Leashing)
                    {
                        CombatantView v = engine.View(s.Current);
                        if (v.IsPresent && !HeldByPlayer(engine, v) && TryLeave(zone, s.Current)) s.Gone = s.Withdrawn = true;
                    }
        }

        /// <summary>Retenue par un combat contre un personnage : une fin d'événement ne coupe jamais ce combat.</summary>
        private static bool HeldByPlayer(CombatEngine engine, CombatantView v) =>
            v.Engaged && v.CurrentTarget is string target && engine.View(target).Kind == CombatantKind.Character;

        private void AddWave(Run run, WorldSession world)
        {
            foreach (WorldEventCreature c in run.Occurrence.Group.Creatures)
                run.Slots.Add(new Slot { Creature = c, Like = world.PopulationFor(c.Like), RetryAt = -1 });
        }

        /// <summary>Place pour <paramref name="count"/> créatures de plus, hors de la réserve des quêtes et des places gardées.</summary>
        private static bool Reserve(Zone zone, int reserved, int count) =>
            checked(zone.Engine.CombatantIds.Count + reserved + ZonePopulations.QuestReserve + count) <= zone.Setup.Combat.MaxCombatants;

        private void StepSlot(Zone zone, Slot s, long now, long tickMs, int reserved)
        {
            if (s.Dead || s.Gone) return;
            CombatEngine engine = zone.Engine;
            if (s.Current is string id)
            {
                CombatantView v = engine.View(id);
                bool refused = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick is long at && at < now;
                if (v.IsDead)
                {
                    s.Dead = true;
                    s.Hurt |= v.HurtByCharacter;
                    return;
                }
                if (v.HasLeft || refused)
                {
                    // Laisse (ou arrivée refusée) : une vie calme revient à sa place une seconde plus tard, s'il y a de la place.
                    s.Current = null;
                    s.Leashing = false;
                    s.RetryAt = checked(now + Math.Max(1, 1000 / tickMs));
                    return;
                }
                if (!s.Leashing && v.IsPresent && v.Engaged && v.CurrentTarget is string target && engine.View(target) is CombatantView t
                    && t.IsPresent && !t.IsDead && v.Position is GroundVector a && v.Radius is Fixed ra && t.Position is GroundVector b && t.Radius is Fixed rb
                    && ZonePopulations.Beyond(a, ra, b, rb, s.Like.LeashRange) && TryLeave(zone, id))
                    s.Leashing = true;
                return;
            }
            long due = s.RetryAt ?? -1;
            if (due > now) return;
            if (due >= 0 && !Reserve(zone, reserved, 1))
            {
                s.Gone = true;   // plus de place pour revenir : elle a pris la fuite
                return;
            }
            Join(zone, s, now, tickMs);
        }

        private void Join(Zone zone, Slot s, long now, long tickMs)
        {
            CombatEngine engine = zone.Engine;
            AssailantSetup t = s.Like.Template;
            CombatantPlacement model = t.Placement!;
            var place = new CombatantPlacement(s.Creature.Position, model.Radius, model.Facing, model.Speed);
            if (Occupied(engine, place))
            {
                s.RetryAt = checked(now + Math.Max(1, 1000 / tickMs));
                return;
            }
            var taken = new HashSet<string>(engine.CombatantIds, StringComparer.Ordinal);
            string id;
            do id = $"{s.Like.Creature}_ev{_counter = checked(_counter + 1)}"; while (taken.Contains(id));
            var setup = new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative,
                t.StatusResistance, t.Triggers, t.Health, place, checked(now * tickMs), t.Behavior);
            try
            {
                engine.Join(setup);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                s.RetryAt = checked(now + Math.Max(1, 1000 / tickMs));
                return;
            }
            s.Current = id;
            s.RetryAt = null;
            _byCreature[id] = s.Like;
        }

        private static bool Occupied(CombatEngine engine, CombatantPlacement place)
        {
            foreach (string id in engine.CombatantIds)
            {
                CombatantView v = engine.View(id);
                if (!v.IsPresent || v.IsDead || v.Position is not GroundVector p || v.Radius is not Fixed r) continue;
                if (!ZonePopulations.Beyond(p, r, place.Position, place.Radius, Fixed.Zero)) return true;
            }
            return false;
        }

        private static bool TryLeave(Zone zone, string combatant)
        {
            try
            {
                zone.Leave(combatant);
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return false;
            }
        }

        private static void Settle(WorldSession world, Run run, List<WorldEventNotice> notices) =>
            world.SettleEvent(run.Occurrence.Event, run.Occurrence, EventOutcome.Lost, WorldEventNoticeKind.Lost, notices);

        /// <summary>
        /// Referme la faille de l'événement <paramref name="eventId"/> : le personnage est à <see cref="CloseRange"/> de son centre, et aucun
        /// écho de la faille n'est debout ni n'attend d'arriver. Les vagues suivantes n'ont pas lieu ; l'occurrence est réussie. Refus atomique.
        /// </summary>
        public RiftRefusal CloseRift(WorldSession world, Zone zone, string eventId, string character, List<WorldEventNotice> notices)
        {
            if (!_runs.TryGetValue(eventId, out Run? run) || !(run is { Started: true, Closed: false, Ending: false } r) || r.Occurrence.Event.Kind != WorldEventKind.Rift
                || !(r.Occurrence.Group.Position is GroundVector rift)) return RiftRefusal.NoRift;
            CombatantView me = zone.Engine.View(character);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || !(me.Radius is Fixed radius)
                || ZonePopulations.Beyond(p, radius, rift, Fixed.Zero, CloseRange)) return RiftRefusal.TooFar;
            if (r.Slots.Any(s => !s.Dead && !s.Gone)) return RiftRefusal.EchoesRemain;
            r.Closed = true;
            world.SettleEvent(r.Occurrence.Event, r.Occurrence, EventOutcome.Won, null, notices);
            _runs[eventId] = null;
            return RiftRefusal.None;
        }
    }
}
