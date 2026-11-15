using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Scenarios;
using EdS.Domain.Collections;
using EdS.Domain.Space;
using EdS.Domain.Time;

namespace EdS.Data.Story
{
    /// <summary>
    /// Sous-lot V2.10 (marchands vivants et fêtes du bourg) : le calendrier du monde, recalculé du jour du monde (aucune horloge réelle) :
    /// semaines de <see cref="DaysPerWeek"/> jours, saisons de <see cref="DaysPerSeason"/> jours (printemps, été, automne, hiver), années de
    /// <see cref="DaysPerYear"/> jours ; le jour 0 est le premier jour du printemps et le premier de la semaine. Valeurs provisoires
    /// (proposition de Claude, à régler par Damien ; voir MONDE.md).
    /// </summary>
    public static class WorldCalendar
    {
        public const int DaysPerWeek = 7, DaysPerSeason = 28, SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

        /// <summary>Saisons, dans l'ordre de l'année.</summary>
        public static IReadOnlyList<string> Seasons { get; } = new ReadOnlyCollection<string>(new[] { "spring", "summer", "autumn", "winter" });

        private static long Check(long day) => day >= 0 ? day : throw new ArgumentOutOfRangeException(nameof(day));

        /// <summary>Jour de la semaine (0 à 6).</summary>
        public static int WeekdayOf(long day) => (int)(Check(day) % DaysPerWeek);

        /// <summary>Rang de la saison (0 : printemps … 3 : hiver).</summary>
        public static int SeasonIndexOf(long day) => (int)(Check(day) / DaysPerSeason % SeasonsPerYear);

        public static string SeasonOf(long day) => Seasons[SeasonIndexOf(day)];

        /// <summary>Jour de la saison (0 à 27).</summary>
        public static int DayOfSeason(long day) => (int)(Check(day) % DaysPerSeason);

        /// <summary>Rang d'une saison, ou -1.</summary>
        public static int SeasonIndex(string? season) => season is null ? -1 : Seasons.ToList().IndexOf(season);
    }

    /// <summary>Sous-lot V2.10 : un invité d'une fête : le PNJ, sa place sur la place du bourg et son activité pendant la fête.</summary>
    public sealed class FestivalGuest
    {
        public string Npc { get; }
        public GroundVector Position { get; }
        public string Activity { get; }

        internal FestivalGuest(string npc, GroundVector position, string activity)
        {
            Npc = npc;
            Position = position;
            Activity = activity;
        }
    }

    /// <summary>
    /// Sous-lot V2.10 (histoire en schéma 13) : une fête du bourg : chaque semaine (<see cref="Weekday"/>) ou chaque année (<see cref="Season"/>
    /// et <see cref="Day"/>), de <see cref="From"/> à <see cref="To"/> (minutes du jour, fin exclue). Pendant la fête, ses invités sont à leur
    /// place de fête (dans la zone de l'histoire), les PNJ de passage (<see cref="Npc.Festivals"/>) sont là, les étals ambulants ouverts.
    /// Recalculé du temps du monde : rien n'est sauvegardé, sauf la dernière participation au concours (sauvegarde v16).
    /// </summary>
    public sealed class Festival
    {
        public string Id { get; }

        /// <summary>Clé du nom affiché.</summary>
        public string Name { get; }

        /// <summary>Jour de la semaine (0 à 6) d'une fête hebdomadaire, ou null.</summary>
        public int? Weekday { get; }

        /// <summary>Rang de la saison d'une fête annuelle (<see cref="WorldCalendar.Seasons"/>), ou null.</summary>
        public int? Season { get; }

        /// <summary>Jour de la saison d'une fête annuelle (0 à 27), ou null.</summary>
        public int? Day { get; }

        public int From { get; }
        public int To { get; }
        public IReadOnlyList<FestivalGuest> Guests { get; }

        /// <summary>
        /// Sous-lot V2.10 (décision de Damien du 8 novembre 2026 : un concours à chaque jour de marché, en rotation) : nombre de tours de la
        /// rotation des concours (1 : toujours le même) ; le tour d'une occurrence est son rang (semaine ou année du monde) modulo ce nombre.
        /// </summary>
        public int Rounds { get; private set; } = 1;

        internal Festival WithRounds(int rounds)
        {
            Rounds = rounds;
            return this;
        }

        /// <summary>Tour de la rotation des concours le jour <paramref name="day"/> (0 à <see cref="Rounds"/> − 1).</summary>
        public int RoundOn(long day) =>
            (int)(day / (Weekday != null ? WorldCalendar.DaysPerWeek : WorldCalendar.DaysPerYear) % Rounds);

        internal Festival(string id, string name, int? weekday, int? season, int? day, int from, int to, IEnumerable<FestivalGuest> guests)
        {
            Id = id;
            Name = name;
            Weekday = weekday;
            Season = season;
            Day = day;
            From = from;
            To = to;
            Guests = Frozen.List(guests, nameof(guests));
        }

        /// <summary>La fête a lieu le jour <paramref name="day"/> du monde (à ses heures).</summary>
        public bool OnDay(long day) => Weekday is int w ? WorldCalendar.WeekdayOf(day) == w
            : WorldCalendar.SeasonIndexOf(day) == Season && WorldCalendar.DayOfSeason(day) == Day;

        /// <summary>La fête bat son plein au jour <paramref name="day"/>, à la minute <paramref name="minute"/>.</summary>
        public bool At(long day, int minute) => OnDay(day) && minute >= From && minute < To;

        /// <summary>Fêtes aux vraies dates (histoire en version 14) : mois et jour du premier jour réel, et durée en jours réels ; null sans date.</summary>
        public int? Month { get; private set; }
        public int? StartDay { get; private set; }
        public int? Days { get; private set; }

        public bool HasDate => Month != null;

        internal Festival WithDate(int? month, int? day, int? days)
        {
            if (month is null || day is null || days is null) return this;
            Month = month;
            StartDay = day;
            Days = days;
            return this;
        }

        /// <summary>Premier jour réel de la fête de l'année <paramref name="year"/>.</summary>
        public CivilDate StartIn(int year) => new CivilDate(year, Month ?? throw new InvalidOperationException("Fête sans date réelle."), StartDay!.Value);

        /// <summary>Premier jour réel de l'occurrence qui contient <paramref name="date"/> (elle peut avoir commencé l'année d'avant), ou null.</summary>
        public CivilDate? OccurrenceOf(CivilDate date)
        {
            if (!HasDate) return null;
            foreach (int year in new[] { date.Year, date.Year - 1 })
            {
                if (year < CivilDate.MinYear) continue;
                CivilDate start = StartIn(year);
                if (date >= start && date.DayNumber < checked(start.DayNumber + Days!.Value)) return start;
            }
            return null;
        }

        /// <summary>La fête datée a lieu le jour réel <paramref name="date"/> (heure de Paris).</summary>
        public bool OnCivil(CivilDate date) => OccurrenceOf(date) != null;

        /// <summary>
        /// Fêtes aux vraies dates : la fête a lieu le jour du monde <paramref name="day"/>, dont le jour réel (heure de Paris, à midi du monde)
        /// est <paramref name="civil"/> : une fête datée suit la date réelle quand elle est connue ; sinon (ou fête de la semaine, ou fête
        /// sans date), le calendrier du jeu.
        /// </summary>
        public bool OnDay(long day, CivilDate? civil) => Weekday is null && HasDate && civil is CivilDate c ? OnCivil(c) : OnDay(day);

        public bool At(long day, int minute, CivilDate? civil) => OnDay(day, civil) && minute >= From && minute < To;

        /// <summary>Tour de la rotation des concours : par année réelle pour une fête datée dont la date est connue.</summary>
        public int RoundOn(long day, CivilDate? civil) =>
            Weekday is null && HasDate && civil is CivilDate c && OccurrenceOf(c) is CivilDate start ? start.Year % Rounds : RoundOn(day);

        /// <summary>Deux fêtes datées partagent au moins un jour réel (sur une année non bissextile et à cheval sur deux années).</summary>
        public static bool DatesOverlap(Festival a, Festival b)
        {
            for (int ya = 2027; ya <= 2028; ya++)
                for (int yb = 2027; yb <= 2028; yb++)
                {
                    long sa = a.StartIn(ya).DayNumber, sb = b.StartIn(yb).DayNumber;
                    if (sa < sb + b.Days!.Value && sb < sa + a.Days!.Value) return true;
                }
            return false;
        }


        /// <summary>Invité <paramref name="npc"/>, ou null.</summary>
        public FestivalGuest? Guest(string npc) => Guests.FirstOrDefault(g => g.Npc == npc);
    }

    /// <summary>Sous-lot V2.10 (schéma 13) : une fête telle que lue, avant validation.</summary>
    public sealed class FestivalData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Weekday { get; set; }
        public string? Season { get; set; }
        public int? Day { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? Rounds { get; set; }
        public List<FestivalGuestData?>? Guests { get; set; }

        /// <summary>Fêtes aux vraies dates (version 14) : date réelle facultative d'une fête annuelle.</summary>
        public FestivalDateData? Date { get; set; }
    }

    public sealed class FestivalDateData
    {
        public int? Month { get; set; }
        public int? Day { get; set; }
        public int? Days { get; set; }
    }

    public sealed class FestivalGuestData
    {
        public string? Npc { get; set; }
        public PointData? Position { get; set; }
        public string? Activity { get; set; }
    }
}
