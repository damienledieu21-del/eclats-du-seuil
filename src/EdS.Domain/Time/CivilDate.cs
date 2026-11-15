using System;
using System.Globalization;

namespace EdS.Domain.Time
{
    /// <summary>
    /// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : date civile du calendrier grégorien (proleptique), de l'an 1 à l'an 9999,
    /// fournie de l'extérieur (hôte ou serveur), jamais lue d'une horloge réelle par le moteur. Années bissextiles exactes (divisible par 4,
    /// sauf les siècles non divisibles par 400). Tout est entier et vérifié.
    /// </summary>
    public readonly struct CivilDate : IEquatable<CivilDate>, IComparable<CivilDate>
    {
        public const int MinYear = 1, MaxYear = 9999;

        public int Year { get; }

        /// <summary>Mois, de 1 (janvier) à 12 (décembre).</summary>
        public int Month { get; }

        /// <summary>Jour du mois, de 1 à <see cref="DaysInMonth(int, int)"/>.</summary>
        public int Day { get; }

        public CivilDate(int year, int month, int day)
        {
            if (year < MinYear || year > MaxYear) throw new ArgumentOutOfRangeException(nameof(year), year, $"année hors de [{MinYear}, {MaxYear}].");
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month), month, "mois hors de [1, 12].");
            if (day < 1 || day > DaysInMonth(year, month)) throw new ArgumentOutOfRangeException(nameof(day), day, $"jour hors de [1, {DaysInMonth(year, month)}].");
            Year = year;
            Month = month;
            Day = day;
        }

        public static bool IsLeapYear(int year) => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);

        public static int DaysInMonth(int year, int month)
        {
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month), month, "mois hors de [1, 12].");
            switch (month)
            {
                case 2: return IsLeapYear(year) ? 29 : 28;
                case 4: case 6: case 9: case 11: return 30;
                default: return 31;
            }
        }

        public static bool IsValid(int year, int month, int day) =>
            year >= MinYear && year <= MaxYear && month >= 1 && month <= 12 && day >= 1 && day <= DaysInMonth(year, month);

        /// <summary>Jours écoulés depuis le 1er janvier 1970 (négatif avant). Algorithme « days from civil » (H. Hinnant), en entiers.</summary>
        public long DayNumber
        {
            get
            {
                long y = Month <= 2 ? Year - 1 : Year;
                long era = (y >= 0 ? y : y - 399) / 400;
                long yoe = y - era * 400;
                long mp = (Month + 9) % 12;   // mars = 0
                long doy = (153 * mp + 2) / 5 + Day - 1;
                long doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
                return checked(era * 146097 + doe - 719468);
            }
        }

        public static readonly long MinDayNumber = new CivilDate(MinYear, 1, 1).DayNumber;
        public static readonly long MaxDayNumber = new CivilDate(MaxYear, 12, 31).DayNumber;

        /// <summary>Date du jour <paramref name="dayNumber"/> (jours depuis le 1er janvier 1970).</summary>
        public static CivilDate FromDayNumber(long dayNumber)
        {
            if (dayNumber < MinDayNumber || dayNumber > MaxDayNumber) throw new ArgumentOutOfRangeException(nameof(dayNumber), dayNumber, "date hors de l'an 1 à l'an 9999.");
            long z = dayNumber + 719468;
            long era = (z >= 0 ? z : z - 146096) / 146097;
            long doe = z - era * 146097;
            long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
            long y = yoe + era * 400;
            long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
            long mp = (5 * doy + 2) / 153;
            long d = doy - (153 * mp + 2) / 5 + 1;
            long m = mp < 10 ? mp + 3 : mp - 9;
            return new CivilDate((int)(m <= 2 ? y + 1 : y), (int)m, (int)d);
        }

        public CivilDate AddDays(long days) => FromDayNumber(checked(DayNumber + days));

        /// <summary>Jour de la semaine, de 0 (lundi) à 6 (dimanche) ; le 1er janvier 1970 était un jeudi (3).</summary>
        public int DayOfWeek => (int)(((DayNumber + 3) % 7 + 7) % 7);

        /// <summary>Dernier dimanche du mois <paramref name="month"/> de <paramref name="year"/> (heure d'été européenne : mars et octobre).</summary>
        public static CivilDate LastSunday(int year, int month)
        {
            var last = new CivilDate(year, month, DaysInMonth(year, month));
            return last.AddDays(-((last.DayOfWeek - 6 + 7) % 7));
        }

        public bool Equals(CivilDate other) => Year == other.Year && Month == other.Month && Day == other.Day;
        public override bool Equals(object? obj) => obj is CivilDate d && Equals(d);
        public override int GetHashCode() => (Year * 13 + Month) * 32 + Day;
        public int CompareTo(CivilDate other) => DayNumber.CompareTo(other.DayNumber);
        public static bool operator ==(CivilDate a, CivilDate b) => a.Equals(b);
        public static bool operator !=(CivilDate a, CivilDate b) => !a.Equals(b);
        public static bool operator <(CivilDate a, CivilDate b) => a.CompareTo(b) < 0;
        public static bool operator >(CivilDate a, CivilDate b) => a.CompareTo(b) > 0;
        public static bool operator <=(CivilDate a, CivilDate b) => a.CompareTo(b) <= 0;
        public static bool operator >=(CivilDate a, CivilDate b) => a.CompareTo(b) >= 0;

        /// <summary>Forme ISO 8601 (<c>2026-12-08</c>).</summary>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0:0000}-{1:00}-{2:00}", Year, Month, Day);
    }

    /// <summary>Saisons : instant local (date civile et minute du jour, de 0 à 1439), par exemple à l'heure de Paris.</summary>
    public readonly struct CivilMoment : IEquatable<CivilMoment>
    {
        public const int MinutesPerDay = 1440;

        public CivilDate Date { get; }

        /// <summary>Minute du jour, de 0 (minuit) à 1439 (23 h 59).</summary>
        public int Minute { get; }

        public CivilMoment(CivilDate date, int minute)
        {
            if (minute < 0 || minute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute), minute, "minute hors de [0, 1439].");
            Date = date;
            Minute = minute;
        }

        public bool Equals(CivilMoment other) => Date.Equals(other.Date) && Minute == other.Minute;
        public override bool Equals(object? obj) => obj is CivilMoment m && Equals(m);
        public override int GetHashCode() => Date.GetHashCode() * 1440 + Minute;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1:00}:{2:00}", Date, Minute / 60, Minute % 60);
    }

    /// <summary>
    /// Saisons (décision de Damien du 15 novembre 2026 : « à l'heure de Paris ») : passage d'un instant UTC (secondes depuis le 1er janvier
    /// 1970, fourni par l'hôte ou le serveur) à l'heure légale de Paris. Règle européenne, appliquée à toutes les années : heure d'été (UTC+2)
    /// du dernier dimanche de mars à 1 h UTC au dernier dimanche d'octobre à 1 h UTC (fin exclue), heure d'hiver (UTC+1) le reste de l'année.
    /// Le fuseau du téléphone n'intervient jamais. Les années antérieures à 1996, où la règle était autre, ne servent pas au jeu.
    /// </summary>
    public static class ParisTime
    {
        public const int WinterOffsetMinutes = 60, SummerOffsetMinutes = 120;

        /// <summary>Premier instant accepté : le 1er janvier 1970 à 0 h UTC.</summary>
        public const long MinUnixSeconds = 0;

        /// <summary>Dernier instant accepté : le 31 décembre 9999 à 23 h 59 min 59 s, heure de Paris (heure d'hiver, UTC+1, en décembre).</summary>
        public static readonly long MaxUnixSeconds = checked((CivilDate.MaxDayNumber + 1) * 86400 - 3600 - 1);

        private static void Check(long unixSeconds)
        {
            if (unixSeconds < MinUnixSeconds || unixSeconds > MaxUnixSeconds)
                throw new ArgumentOutOfRangeException(nameof(unixSeconds), unixSeconds, $"instant UTC hors de [{MinUnixSeconds}, {MaxUnixSeconds}] s.");
        }

        /// <summary>Instant UTC (secondes) du changement d'heure de l'année <paramref name="year"/> : dernier dimanche du mois, à 1 h UTC.</summary>
        public static long ChangeAt(int year, int month) => checked(CivilDate.LastSunday(year, month).DayNumber * 86400 + 3600);

        /// <summary>Paris est à l'heure d'été à l'instant <paramref name="unixSeconds"/>.</summary>
        public static bool IsSummerTime(long unixSeconds)
        {
            Check(unixSeconds);
            int year = CivilDate.FromDayNumber(FloorDiv(unixSeconds, 86400)).Year;
            return unixSeconds >= ChangeAt(year, 3) && unixSeconds < ChangeAt(year, 10);
        }

        /// <summary>Décalage de Paris sur UTC, en minutes (60 ou 120).</summary>
        public static int OffsetMinutes(long unixSeconds) => IsSummerTime(unixSeconds) ? SummerOffsetMinutes : WinterOffsetMinutes;

        /// <summary>Heure légale de Paris (date et minute, secondes tronquées) à l'instant UTC <paramref name="unixSeconds"/>.</summary>
        public static CivilMoment ToLocal(long unixSeconds)
        {
            long local = checked(unixSeconds + OffsetMinutes(unixSeconds) * 60L);
            long day = FloorDiv(local, 86400);
            return new CivilMoment(CivilDate.FromDayNumber(day), (int)((local - day * 86400) / 60));
        }

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
