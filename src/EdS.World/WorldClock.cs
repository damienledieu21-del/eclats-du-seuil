using System;

namespace EdS.World
{
    /// <summary>Moment de la journée du monde (cadrage Q26 : le jour et la nuit ont des effets sur le jeu, avec des règles apprenables).</summary>
    public enum DayPhase
    {
        /// <summary>De 21 h à 5 h.</summary>
        Night = 0,

        /// <summary>De 5 h à 7 h.</summary>
        Dawn = 1,

        /// <summary>De 7 h à 19 h.</summary>
        Day = 2,

        /// <summary>De 19 h à 21 h.</summary>
        Dusk = 3,
    }

    /// <summary>Heure du monde à un tick : jour (à partir de 0), minute du jour (0 à 1439) et moment de la journée.</summary>
    public readonly struct WorldTime : IEquatable<WorldTime>
    {
        public long Day { get; }

        /// <summary>Minute du jour du monde, de 0 (minuit) à 1439 (23 h 59).</summary>
        public int Minute { get; }

        public DayPhase Phase { get; }

        public WorldTime(long day, int minute)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (minute < 0 || minute >= WorldClock.MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            Day = day;
            Minute = minute;
            Phase = PhaseOf(minute);
        }

        public int Hour => Minute / 60;

        public static DayPhase PhaseOf(int minute) =>
            minute < 5 * 60 ? DayPhase.Night : minute < 7 * 60 ? DayPhase.Dawn : minute < 19 * 60 ? DayPhase.Day : minute < 21 * 60 ? DayPhase.Dusk : DayPhase.Night;

        public bool Equals(WorldTime other) => Day == other.Day && Minute == other.Minute;
        public override bool Equals(object? obj) => obj is WorldTime t && Equals(t);
        public override int GetHashCode() => unchecked((int)Day * 1440 + Minute);
        public override string ToString() => $"jour {Day}, {Minute / 60:00} h {Minute % 60:00}";
    }

    /// <summary>
    /// Sous-lot 12.1 (cadrage Q26, ARCHITECTURE_MONDE.md, section 4) : temps du monde tiré des ticks de simulation, jamais de l'horloge
    /// réelle. Une journée du monde dure <see cref="DayLengthMs"/> de jeu (valeur provisoire : 3 h depuis la décision de Damien du 30 octobre
    /// 2026, 2 h avant ; réglable) ; le monde commence à
    /// <see cref="StartMinute"/> du jour 0. Tout est entier : le même tick donne toujours la même heure.
    /// </summary>
    public sealed class WorldClock
    {
        public const int MinutesPerDay = 1440;

        /// <summary>
        /// Durée provisoire d'une journée du monde : 3 h de jeu (décision de Damien du 30 octobre 2026, sujet 32 ; 2 h avant). Cadrage Q26 :
        /// les occasions reviennent, pas d'heure réelle restrictive. Le temps du monde est gardé en ticks : une sauvegarde d'avant reprend au
        /// même tick, donc à une autre heure (et un autre jour) du monde.
        /// </summary>
        public const long DefaultDayLengthMs = 3L * 3600 * 1000;

        /// <summary>Heure provisoire du début du monde : 8 h du matin.</summary>
        public const int DefaultStartMinute = 8 * 60;

        public long TickMilliseconds { get; }
        public long DayLengthMs { get; }
        public int StartMinute { get; }

        public WorldClock(long tickMilliseconds, long dayLengthMs = DefaultDayLengthMs, int startMinute = DefaultStartMinute)
        {
            if (tickMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(tickMilliseconds));
            // Une journée d'au moins une minute de jeu et d'au plus un jour réel, en ticks entiers.
            if (dayLengthMs < 60_000 || dayLengthMs > 86_400_000 || dayLengthMs % tickMilliseconds != 0)
                throw new ArgumentOutOfRangeException(nameof(dayLengthMs), $"durée d'une journée hors de [60 000, 86 400 000] ms, ou non multiple du tick ({tickMilliseconds} ms).");
            if (startMinute < 0 || startMinute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(startMinute));
            TickMilliseconds = tickMilliseconds;
            DayLengthMs = dayLengthMs;
            StartMinute = startMinute;
        }

        /// <summary>Heure du monde au tick <paramref name="worldTick"/> (arrondi à la minute inférieure).</summary>
        public WorldTime At(long worldTick)
        {
            if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
            long ms = checked(worldTick * TickMilliseconds);
            // Minutes du monde écoulées depuis minuit du jour 0 : début, plus le temps de jeu ramené à la journée du monde.
            long dayCount = ms / DayLengthMs;
            long within = ms % DayLengthMs;
            long minutes = checked(StartMinute + within * MinutesPerDay / DayLengthMs);
            return new WorldTime(checked(dayCount + minutes / MinutesPerDay), (int)(minutes % MinutesPerDay));
        }

        /// <summary>Nombre de ticks d'une journée du monde.</summary>
        public long TicksPerDay => DayLengthMs / TickMilliseconds;
    }
}
