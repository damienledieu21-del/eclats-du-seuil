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

        /// <summary>
        /// Fenêtres dont l'occurrence peut toucher le jour <paramref name="day"/> : un départ a lieu au plus 1 j 12 h après le début de son
        /// jour (minute de départ et écart), s'annonce au plus 12 h avant et dure au plus 12 h : les jours de <c>day - 2</c> à <c>day + 1</c>
        /// suffisent (revue Codex de la PR #107, tour 2 : un départ après minuit appartient à la fenêtre précédente).
        /// </summary>
        private static IEnumerable<long> WindowsAround(WorldEventDefinition e, long day)
        {
            long first = Math.Max(0, day - 2) / e.EveryDays, last = Math.Max(0, day + 1) / e.EveryDays;
            for (long w = first; w <= last; w++) yield return w;
        }

        /// <summary>
        /// Occurrences en cours ou annoncées à la minute absolue <paramref name="now"/> (annonce ≤ maintenant &lt; fin), par départ croissant
        /// (deux peuvent se chevaucher : une faille de 1 h et l'annonce de la suivante).
        /// </summary>
        public static IReadOnlyList<EventOccurrence> AllAround(string zone, string weatherZone, WorldEventDefinition e, long now)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            var list = new List<EventOccurrence>();
            foreach (long w in WindowsAround(e, Math.Max(0, now) / WorldClock.MinutesPerDay))
                if (InWindow(zone, weatherZone, e, w) is EventOccurrence o && o.AnnounceAt <= now && now < o.EndAt) list.Add(o);
            list.Sort((a, b) => a.StartAt.CompareTo(b.StartAt));
            return list;
        }

        /// <summary>Occurrence en cours ou annoncée à la minute absolue <paramref name="now"/>, la première par son départ, ou null.</summary>
        public static EventOccurrence? Around(string zone, string weatherZone, WorldEventDefinition e, long now)
        {
            IReadOnlyList<EventOccurrence> all = AllAround(zone, weatherZone, e, now);
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>Dernière occurrence finie à la minute absolue <paramref name="now"/> (fin ≤ maintenant), ou null.</summary>
        public static EventOccurrence? LastEnded(string zone, string weatherZone, WorldEventDefinition e, long now)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            long window = checked(Math.Max(0, now / WorldClock.MinutesPerDay) / e.EveryDays + 1);
            // Revue Codex de la PR #107 (tour 2) : la fin la plus tardive, pas la fenêtre la plus récente (une faille de la fenêtre précédente
            // peut finir après celle d'une fenêtre plus récente) : la fenêtre trouvée et les deux d'avant sont comparées.
            EventOccurrence? best = null;
            long stopAt = -1;
            for (int i = 0; i < MaxSearch && window - i >= 0 && window - i >= stopAt; i++)
                if (InWindow(zone, weatherZone, e, window - i) is EventOccurrence o && o.EndAt <= now)
                {
                    if (best is null) stopAt = window - i - 2;
                    if (best is null || o.EndAt > best.EndAt) best = o;
                }
            return best;
        }

        /// <summary>Prochaine occurrence qui commence à la minute absolue <paramref name="from"/> ou après, ou null.</summary>
        public static EventOccurrence? Next(string zone, string weatherZone, WorldEventDefinition e, long from)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            // Revue Codex de la PR #107 (tour 2) : une occurrence de la fenêtre précédente peut partir après minuit, après le changement de
            // fenêtre : la recherche commence deux jours plus tôt et garde le départ le plus proche (la fenêtre trouvée et la suivante).
            long window = Math.Max(0, from / WorldClock.MinutesPerDay - 2) / e.EveryDays;
            EventOccurrence? best = null;
            long stopAt = long.MaxValue;
            for (int i = 0; i < MaxSearch && checked(window + i) <= stopAt; i++)
                if (InWindow(zone, weatherZone, e, checked(window + i)) is EventOccurrence o && o.StartAt >= from)
                {
                    if (best is null) stopAt = checked(window + i + 1);
                    if (best is null || o.StartAt < best.StartAt) best = o;
                }
            return best;
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
        public static string RegistryKey(string zone, string eventId) => ZoneValidator.EventRegistryKey(zone, eventId);
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

        /// <summary>Sous-lot V2.6 : une faille restée ouverte laisse une cicatrice (<see cref="WorldEventNotice.Wave"/> : son palier).</summary>
        ScarBorn = 10,

        /// <summary>Sous-lot V2.6 : une cicatrice gagne un palier (avec les jours, ou une nouvelle faille restée ouverte à sa place).</summary>
        ScarGrew = 11,

        /// <summary>Sous-lot V2.6 : une faille refermée à sa place fait reculer la cicatrice d'un palier.</summary>
        ScarReceded = 12,

        /// <summary>Sous-lot V2.6 : une faille refermée efface une cicatrice du premier palier.</summary>
        ScarHealed = 13,

        /// <summary>Sous-lot V2.6 : une cicatrice purifiée (un éclat de Seuil) ; <see cref="WorldEventNotice.Xp"/> : l'expérience gagnée.</summary>
        ScarPurified = 14,

        /// <summary>Sous-lot V2.6 : des échos se lèvent autour d'une cicatrice (une fois quand ils reviennent).</summary>
        ScarStirs = 15,

        /// <summary>
        /// Sous-lot V2.6.1 : une cicatrice au palier de propagation en fait naître une au lieu voisin (<see cref="WorldEventNotice.Group"/> :
        /// la nouvelle ; <see cref="WorldEventNotice.From"/> : celle d'où vient le froid ; <see cref="WorldEventNotice.Day"/> : le jour de la
        /// propagation).
        /// </summary>
        ScarSpread = 16,
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

        /// <summary>Sous-lot V2.6.1 : variante d'où vient une propagation (<see cref="WorldEventNoticeKind.ScarSpread"/>), sinon vide.</summary>
        public string From { get; }

        internal WorldEventNotice(WorldEventNoticeKind kind, string ev, string group, long day, int wave = 0, long xp = 0, long money = 0,
            IReadOnlyList<(string, int)>? items = null, string spreadFrom = "")
        {
            From = spreadFrom;
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

        /// <summary>Des échos de la faille sont encore debout, attendent d'arriver, ou ont fui sans être vaincus.</summary>
        EchoesRemain = 3,

        /// <summary>Relecture de la PR #107 : toutes les vagues ne sont pas encore sorties (la faille n'a pas fini de cracher ses échos).</summary>
        WavesRemain = 4,
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

            /// <summary>Sous-lot V2.6 : PV et dégâts, en pour cent de ceux du modèle (cicatrice du Seuil ; 100 sinon).</summary>
            public long Strength = 100;

            /// <summary>Relecture de la PR #110 : écho rôdeur d'une cicatrice.</summary>
            public bool Haunt;
        }

        /// <summary>Sous-lot V2.6 : échos rôdeurs d'une cicatrice (une place par écho rôdeur de la variante) et minute absolue de leur retour.</summary>
        private sealed class Haunt
        {
            public Slot?[] Slots = Array.Empty<Slot?>();
            public long[] ReadyAt = Array.Empty<long>();
            public bool Stirred;
        }

        /// <summary>Sous-lot V2.6 : échos rôdeurs par cicatrice (clé de la sauvegarde), dans l'ordre des clés.</summary>
        private readonly SortedDictionary<string, Haunt> _haunts = new SortedDictionary<string, Haunt>(StringComparer.Ordinal);

        private sealed class Run
        {
            public EventOccurrence Occurrence = null!;
            public bool Announced;
            public bool Started;
            public bool Ending;
            public bool Closed;
            public int WavesSpawned;

            /// <summary>Relecture de la PR #107 : pas de place à son départ : l'occurrence n'a pas eu lieu (ni départ, ni fin annoncés).</summary>
            public bool Refused;
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

        /// <summary>
        /// Relecture de la PR #110 : change dès qu'une créature d'événement ou un écho rôdeur arrive, tombe, part, ou qu'une occurrence
        /// commence ou finit (les vues des cicatrices ne sont recalculées que si elle change).
        /// </summary>
        public int Version { get; private set; }

        private void Changed() => Version = unchecked(Version + 1);

        /// <summary>Relecture de la PR #107 : jour de la dernière occurrence tentée par événement (une occurrence n'est tentée qu'une fois par session).</summary>
        private readonly Dictionary<string, long> _tried = new Dictionary<string, long>(StringComparer.Ordinal);
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

        private static bool Settled(Run r) => r.Closed || (r.Ending && r.Slots.All(s => s.Dead || s.Gone));

        /// <summary>
        /// Relecture de la PR #107 : places réservées et pas encore prises : créatures d'événement sans combattant (pas encore arrivées, place
        /// occupée, arrivée refusée, laisse). Chacune garde sa place jusqu'à son arrivée ou son abandon.
        /// </summary>
        internal int PendingPlaces => checked(_runs.Values.Where(r => r != null).Sum(r => r!.Slots.Count(Pending))
            + _haunts.Values.Sum(h => h.Slots.Count(s => s != null && Pending(s))));

        private static bool Pending(Slot s) => s.Current is null && !s.Dead && !s.Gone;

        /// <summary>Sous-lot V2.6 : une faille de l'événement <paramref name="eventId"/> est ouverte à la variante <paramref name="group"/>.</summary>
        public bool RiftOpenAt(string eventId, string group) =>
            _runs.TryGetValue(eventId, out Run? r) && r is { Started: true, Closed: false, Ending: false, Refused: false } && r.Occurrence.Group.Id == group;

        /// <summary>Sous-lot V2.6 : échos rôdeurs de la cicatrice <paramref name="key"/> debout dans la zone.</summary>
        public IReadOnlyList<string> HauntsOf(string key) =>
            _haunts.TryGetValue(key, out Haunt? h) ? h.Slots.Where(s => s is { Dead: false, Gone: false } && s.Current != null).Select(s => s!.Current!).ToList() : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Sous-lot V2.6 : la cicatrice <paramref name="key"/> a des échos rôdeurs debout ou qui vont arriver (place réservée).</summary>
        public bool HauntsRemain(string key) => _haunts.TryGetValue(key, out Haunt? h) && h.Slots.Any(s => s is { Dead: false, Gone: false });

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
                    // Revue Codex de la PR #107 (tour 2) : la première occurrence en cours ni réglée ni déjà tentée (deux peuvent se chevaucher).
                    EventOccurrence? o = null;
                    foreach (EventOccurrence c in WorldEventSchedule.AllAround(_zone.Id, _weatherZone, e, now))
                    {
                        if (e.Settles && world.EventEntry(e).Last >= c.Day) continue;   // déjà réglée pour ce personnage
                        // Relecture de la PR #107 : une occurrence n'est tentée qu'une fois par session (pas de place, registre plein : rien ne la relance).
                        if (_tried.TryGetValue(e.Id, out long tried) && tried >= c.Day) continue;
                        o = c;
                        break;
                    }
                    if (o is null) continue;
                    _tried[e.Id] = o.Day;
                    if (world.Level < e.MinLevel)
                    {
                        // Choix de Damien du 7 novembre 2026 : sous le niveau minimum, l'occurrence n'a pas lieu pour ce personnage : ni annonce,
                        // ni arrivée, ni issue ; elle est seulement notée au registre (sans issue), pour ne jamais compter plus tard.
                        if (e.Settles) world.SettleEvent(e, o, EventOutcome.None, null, notices);
                        continue;
                    }
                    _runs[e.Id] = run = new Run { Occurrence = o };
                    Changed();
                }
                StepRun(world, zone, run, now, nowTick, reserved, notices);
                if (Settled(run) || (run.Ending && run.Slots.Count == 0))
                {
                    if (!run.Closed && e.Settles) Settle(world, run, notices);
                    else if (!e.Settles && !run.Refused) notices.Add(new WorldEventNotice(WorldEventNoticeKind.Ended, e.Id, run.Occurrence.Group.Id, run.Occurrence.Day));
                    _runs[e.Id] = null;
                    Changed();
                }
            }
            StepHaunts(world, zone, now, nowTick, reserved, minuteChanged, notices);
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
                // Choix de Damien du 7 novembre 2026 : sous le niveau minimum, pas de pillage pendant l'absence (notée sans issue).
                if (last != null && last.Day > world.EventEntry(e).Last)
                    world.SettleEvent(e, last, world.Level < e.MinLevel ? EventOutcome.None : EventOutcome.Lost, WorldEventNoticeKind.Missed, notices);
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
                Changed();
                if (!Reserve(zone, reserved, o.Group.Creatures.Count))
                {
                    // Plus de place hors de la réserve des quêtes : l'occurrence n'a pas lieu, réglée sans issue (ni victoire ni pillage).
                    run.Ending = true;
                    run.Refused = true;
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
            if (!run.Ending && now >= o.EndAt) run.Ending = true;
            // Faille : vague suivante, à son heure ou dès que toute la vague précédente est vaincue, si la faille est encore ouverte et qu'il
            // reste de la place hors de la réserve (sinon la vague ne sort pas : elle compte comme sortie).
            if (e.Kind == WorldEventKind.Rift && !run.Closed && !run.Ending && run.WavesSpawned < e.Waves
                && (nowTick >= run.NextWaveTick || run.Slots.All(s => s.Dead)))
            {
                run.WavesSpawned++;
                run.NextWaveTick = checked(nowTick + e.WaveMs / tickMs);
                if (Reserve(zone, reserved, o.Group.Creatures.Count))
                {
                    AddWave(run, world);
                    notices.Add(new WorldEventNotice(WorldEventNoticeKind.Wave, e.Id, o.Group.Id, o.Day, run.WavesSpawned));
                }
            }
            foreach (Slot s in run.Slots) StepSlot(zone, s, nowTick, tickMs, reserved, run.Ending);
            // Attaque : plus aucun assaillant debout (vaincus, ou enfuis faute de place pour revenir), même après la fin si le combat a continué.
            if (e.Kind == WorldEventKind.Raid && run.Slots.All(s => s.Dead || (s.Gone && !s.Withdrawn)) && (!run.Ending || run.Slots.Any(s => s.Dead && s.Hurt)))
            {
                run.Closed = true;
                if (run.Slots.Any(s => s.Hurt)) world.SettleEvent(e, o, EventOutcome.Won, null, notices);
                else world.SettleEvent(e, o, EventOutcome.None, null, notices);
                return;
            }
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
            // Sous-lot V2.6 : une faille qui s'ouvre sur une cicatrice crache des échos plus forts (palier de la cicatrice à cette vague).
            long strength = world.ScarStrength(run.Occurrence.Event, run.Occurrence.Group.Id);
            foreach (WorldEventCreature c in run.Occurrence.Group.Creatures)
                run.Slots.Add(new Slot { Creature = c, Like = world.PopulationFor(c.Like), RetryAt = -1, Strength = strength });
        }

        /// <summary>
        /// Place pour <paramref name="count"/> créatures de plus, hors de la réserve des quêtes, des places gardées (formes de chef) et des
        /// places déjà réservées par les créatures d'événement qui n'ont pas encore de combattant (<see cref="PendingPlaces"/>).
        /// </summary>
        private bool Reserve(Zone zone, int reserved, int count) =>
            Fits(zone.Engine.PlacesTaken, PendingPlaces, reserved, count, zone.Setup.Combat.MaxCombatants)
            // Sous-lot V2.26 : places recyclées, identités bornées par la session.
            && checked(PendingPlaces + reserved + count) <= zone.Engine.IdentitiesLeft;

        /// <summary>Relecture de la PR #107 : la règle de place, en nombres (testée seule).</summary>
        internal static bool Fits(int combatants, int pending, int reserved, int count, int max) =>
            checked(combatants + pending + reserved + ZonePopulations.QuestReserve + count) <= max;

        private void StepSlot(Zone zone, Slot s, long now, long tickMs, int reserved, bool ending)
        {
            if (s.Dead || s.Gone) return;
            // Relecture de la PR #107 : après la fin, aucune arrivée : une créature qui n'est pas là ne viendra plus.
            if (ending && s.Current is null)
            {
                s.Gone = s.Withdrawn = true;
                return;
            }
            CombatEngine engine = zone.Engine;
            if (s.Current is string id)
            {
                CombatantView v = engine.View(id);
                bool refused = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick is long at && at < now;
                if (v.IsDead)
                {
                    Changed();
                    s.Dead = true;
                    s.Hurt |= v.HurtByCharacter;
                    return;
                }
                if (v.HasLeft || refused)
                {
                    Changed();
                    // Revue Codex de la PR #110 (tour 2) : un écho rôdeur qui a fui (laisse) ne revient pas une seconde plus tard : il est
                    // parti, et revient après le délai des rôdeurs (StepHaunts) ; une arrivée refusée réessaie comme les autres.
                    if (s.Haunt && v.HasLeft && !ending)
                    {
                        s.Leashing = false;
                        s.Gone = true;
                        return;
                    }
                    // Laisse (ou arrivée refusée) : une vie calme revient à sa place une seconde plus tard, si une nouvelle place se réserve
                    // maintenant (relecture de la PR #107 : avant tout retour) ; sinon elle a pris la fuite. Après la fin, elle est partie.
                    s.Leashing = false;
                    if (ending || !Reserve(zone, reserved, 1))
                    {
                        s.Gone = true;
                        s.Withdrawn = ending;
                        return;
                    }
                    s.Current = null;
                    s.RetryAt = checked(now + Math.Max(1, 1000 / tickMs));
                    return;
                }
                if (!s.Leashing && v.IsPresent && v.Engaged && v.CurrentTarget is string target && engine.View(target) is CombatantView t
                    && t.IsPresent && !t.IsDead && v.Position is GroundVector a && v.Radius is Fixed ra && t.Position is GroundVector b && t.Radius is Fixed rb
                    && ZonePopulations.Beyond(a, ra, b, rb, s.Like.LeashRange) && TryLeave(zone, id))
                    s.Leashing = true;
                return;
            }
            // Sa place est déjà réservée (vague, ou retour réservé ci-dessus) : l'arrivée n'en prend pas d'autre.
            if ((s.RetryAt ?? -1) > now) return;
            Join(zone, s, now, tickMs);
        }

        private void Join(Zone zone, Slot s, long now, long tickMs)
        {
            CombatEngine engine = zone.Engine;
            AssailantSetup t = s.Strength > 100 ? ZoneScaling.Strengthen(s.Like.Template, s.Strength) : s.Like.Template;
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
                // Sous-lot V2.4.1 : elle n'erre pas (elle attend à sa place, comme en V2.5 : la faille, l'attaque, la chasse ne changent pas).
                t.StatusResistance, t.Triggers, t.Health, place, checked(now * tickMs), t.Behavior?.WithoutWander());
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
            // Relecture de la PR #110 : un écho rôdeur revient sans fin : sa mort n'est pas inscrite au registre (borné).
            if (s.Haunt) zone.Transient(id);
            Changed();
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

        /// <summary>
        /// Sous-lot V2.6 : échos qui rôdent autour des cicatrices. À partir de <see cref="WorldEventScar.HauntStage"/>, une cicatrice garde
        /// autour d'elle un écho rôdeur de plus par palier (au plus les <c>haunts</c> de sa variante), plus forts selon son palier ; un écho
        /// vaincu ou enfui revient <see cref="WorldEventScar.HauntRespawnMinutes"/> minutes du monde plus tard. Même règle de place que les
        /// événements (réserve des quêtes, places gardées), réservée avant l'arrivée ; sans place, il attend la minute suivante. Pendant qu'une
        /// faille est ouverte à sa place, aucun ne se lève. Une cicatrice effacée laisse partir les siens dès qu'ils sont au calme.
        /// </summary>
        private void StepHaunts(WorldSession world, Zone zone, long now, long nowTick, int reserved, bool minuteChanged, List<WorldEventNotice> notices)
        {
            long tickMs = zone.Engine.TickMilliseconds;
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, WorldEventDefinition e, WorldEventGroup g, int stage) in world.ScarStates())
            {
                live.Add(key);
                WorldEventScar rule = e.Scar!;
                if (g.Haunts.Count == 0) continue;
                if (!_haunts.TryGetValue(key, out Haunt? h))
                    _haunts[key] = h = new Haunt { Slots = new Slot?[g.Haunts.Count], ReadyAt = new long[g.Haunts.Count] };
                int want = RiftOpenAt(e.Id, g.Id) ? 0 : rule.HauntsAt(stage, g.Haunts.Count);
                long strength = rule.StrengthAt(stage);
                bool rose = false, any = false;
                for (int i = 0; i < h.Slots.Length; i++)
                {
                    if (h.Slots[i] is Slot s)
                    {
                        // Relecture de la PR #110 : un écho de trop (la cicatrice a reculé, une faille s'ouvre à sa place) ou plus fort que son
                        // palier s'en va dès qu'il ne se bat plus contre un personnage ; il revient à la minute suivante s'il est encore voulu.
                        bool surplus = i >= want || s.Strength > strength;
                        StepSlot(zone, s, nowTick, tickMs, reserved, surplus);
                        if (surplus && Withdraw(zone, s)) h.ReadyAt[i] = now;
                        else if (s.Dead || s.Gone) h.ReadyAt[i] = checked(now + rule.HauntRespawnMinutes);
                        if (s.Dead || s.Gone) h.Slots[i] = null;
                        else any = true;
                        continue;
                    }
                    if (i >= want || !minuteChanged || now < h.ReadyAt[i] || !Reserve(zone, reserved, 1)) continue;
                    WorldEventCreature c = g.Haunts[i];
                    h.Slots[i] = new Slot { Creature = c, Like = world.PopulationFor(c.Like), RetryAt = -1, Strength = strength, Haunt = true };
                    rose = any = true;
                    Changed();
                }
                // Une annonce quand ils se lèvent (une fois, puis de nouveau après qu'ils ont tous été vaincus).
                if (rose && !h.Stirred) notices.Add(new WorldEventNotice(WorldEventNoticeKind.ScarStirs, e.Id, g.Id, now / WorldClock.MinutesPerDay, stage));
                h.Stirred = any;
            }
            // Cicatrices effacées (purifiées, guéries) : leurs échos s'en vont dès qu'ils ne se battent plus contre un personnage.
            foreach (KeyValuePair<string, Haunt> pair in _haunts)
            {
                if (live.Contains(pair.Key)) continue;
                for (int i = 0; i < pair.Value.Slots.Length; i++)
                {
                    if (!(pair.Value.Slots[i] is Slot s)) continue;
                    StepSlot(zone, s, nowTick, tickMs, reserved, true);
                    Withdraw(zone, s);
                    if (s.Dead || s.Gone) pair.Value.Slots[i] = null;
                }
                pair.Value.Stirred = false;
            }
        }

        /// <summary>
        /// Relecture de la PR #110 : un écho rôdeur qui n'est plus voulu part s'il est au calme (pas encore arrivé : il ne viendra pas) ; vrai
        /// s'il est parti ainsi (pas vaincu).
        /// </summary>
        private bool Withdraw(Zone zone, Slot s)
        {
            if (s.Dead || s.Gone) return s.Withdrawn;
            if (s.Current is null)
            {
                s.Gone = s.Withdrawn = true;
                Changed();
                return true;
            }
            CombatantView v = zone.Engine.View(s.Current);
            if (!v.IsPresent || HeldByPlayer(zone.Engine, v) || !TryLeave(zone, s.Current)) return false;
            s.Gone = s.Withdrawn = true;
            Changed();
            return true;
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
            // Relecture de la PR #107 : toutes les vagues sorties, et chaque écho vaincu (un écho enfui ne l'est pas).
            if (r.Slots.Any(s => !s.Dead)) return RiftRefusal.EchoesRemain;
            if (r.WavesSpawned < r.Occurrence.Event.Waves) return RiftRefusal.WavesRemain;
            r.Closed = true;
            world.SettleEvent(r.Occurrence.Event, r.Occurrence, EventOutcome.Won, null, notices);
            _runs[eventId] = null;
            Changed();
            return RiftRefusal.None;
        }
    }
}
