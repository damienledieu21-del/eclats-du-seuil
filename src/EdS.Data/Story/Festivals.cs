using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Scenarios;
using EdS.Domain.Collections;
using EdS.Domain.Space;

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
    }

    public sealed class FestivalGuestData
    {
        public string? Npc { get; set; }
        public PointData? Position { get; set; }
        public string? Activity { get; set; }
    }
}
