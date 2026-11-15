using System;
using EdS.Domain.Numerics;

namespace EdS.Domain.Time
{
    /// <summary>Saisons du monde, dans l'ordre de l'année du jeu (mêmes rangs que <c>WorldCalendar.Seasons</c> : printemps 0 … hiver 3).</summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    /// <summary>État de la glace du lac (décision de Damien du 15 novembre 2026, 2 h 27).</summary>
    public enum IcePhase
    {
        /// <summary>Eau libre (du 1er mars au 30 novembre).</summary>
        None = 0,

        /// <summary>La glace se forme, visible mais pas praticable (du 1er au 7 décembre).</summary>
        Forming = 1,

        /// <summary>Glace praticable (du 8 décembre à la fin février, 28 ou 29 février inclus).</summary>
        Walkable = 2,
    }

    /// <summary>
    /// Saisons : état de la saison à un instant local de Paris : saison, saison précédente, premier et dernier jour, jour dans la saison (0 :
    /// premier jour), progression de la transition visuelle (0 au premier jour à 0 h, 1 après <see cref="SeasonCalendar.TransitionDays"/>
    /// jours) et glace du lac. Valeur pure, recalculée de la date fournie.
    /// </summary>
    public readonly struct SeasonState : IEquatable<SeasonState>
    {
        public Season Season { get; }

        /// <summary>Saison d'où vient la transition (celle d'avant).</summary>
        public Season Previous { get; }

        public CivilDate Start { get; }

        /// <summary>Dernier jour de la saison (inclus).</summary>
        public CivilDate End { get; }

        /// <summary>Jour dans la saison, de 0 à <see cref="Length"/> − 1.</summary>
        public int Day { get; }

        /// <summary>Nombre de jours de la saison (90 à 92).</summary>
        public int Length { get; }

        /// <summary>Progression de la transition visuelle, de 0 (tout <see cref="Previous"/>) à 1 (tout <see cref="Season"/>).</summary>
        public Fixed Transition { get; }

        public IcePhase Ice { get; }

        /// <summary>Formation de la glace, de 0 (le 1er décembre à 0 h) à 1 (praticable) ; 0 en eau libre.</summary>
        public Fixed IceFormation { get; }

        internal SeasonState(Season season, CivilDate start, CivilDate end, int day, Fixed transition, IcePhase ice, Fixed iceFormation)
        {
            Season = season;
            Previous = (Season)(((int)season + 3) % 4);
            Start = start;
            End = end;
            Day = day;
            Length = checked((int)(end.DayNumber - start.DayNumber + 1));
            Transition = transition;
            Ice = ice;
            IceFormation = iceFormation;
        }

        public bool InTransition => Transition < Fixed.One;

        public bool Equals(SeasonState o) => Season == o.Season && Start == o.Start && Day == o.Day && Transition == o.Transition && Ice == o.Ice
            && IceFormation == o.IceFormation;
        public override bool Equals(object? obj) => obj is SeasonState s && Equals(s);
        public override int GetHashCode() => unchecked(((int)Season * 397 + Start.GetHashCode()) * 31 + Day) ^ Transition.GetHashCode();
        public override string ToString() => $"{SeasonCalendar.Id(Season)}, jour {Day + 1}/{Length}, transition {Transition}, glace {Ice}";
    }

    /// <summary>
    /// Saisons : calendrier des saisons du monde, déterministe, sans horloge réelle (décisions de Damien du 14 novembre 2026 : « saisons du
    /// monde comme dans la vraie vie » ; du 15 novembre 2026, 2 h 27 : changements le 1er mars, le 1er juin, le 1er septembre et le 1er
    /// décembre, à l'heure de Paris, avec 7 jours de transition visuelle, le serveur faisant foi ; glace du lac praticable du 8 décembre à la
    /// fin février inclus, après une semaine où elle se forme sans être praticable). L'entrée est une date civile fournie de l'extérieur
    /// (l'hôte ou le serveur) ; <see cref="AtUtc"/> part d'un instant UTC et le passe à l'heure de Paris (<see cref="ParisTime"/>).
    /// </summary>
    public static class SeasonCalendar
    {
        /// <summary>Jours de transition visuelle après chaque changement de saison.</summary>
        public const int TransitionDays = 7;

        /// <summary>Jours où la glace se forme sans être praticable (du 1er au 7 décembre).</summary>
        public const int IceFormingDays = 7;

        /// <summary>Premier jour praticable de la glace : le 8 décembre.</summary>
        public const int IceWalkableDecemberDay = 1 + IceFormingDays;

        /// <summary>Identifiant de données d'une saison (« spring », « summer », « autumn », « winter »).</summary>
        public static string Id(Season season) => season switch
        {
            Season.Spring => "spring",
            Season.Summer => "summer",
            Season.Autumn => "autumn",
            Season.Winter => "winter",
            _ => throw new ArgumentOutOfRangeException(nameof(season)),
        };

        /// <summary>Saison d'un identifiant de données, ou null.</summary>
        public static Season? Parse(string? id) => id switch
        {
            "spring" => Season.Spring,
            "summer" => Season.Summer,
            "autumn" => Season.Autumn,
            "winter" => Season.Winter,
            _ => (Season?)null,
        };

        /// <summary>Saison du jour <paramref name="date"/> (mars à mai : printemps ; juin à août : été ; septembre à novembre : automne ; sinon hiver).</summary>
        public static Season SeasonOf(CivilDate date) => date.Month switch
        {
            3 or 4 or 5 => Season.Spring,
            6 or 7 or 8 => Season.Summer,
            9 or 10 or 11 => Season.Autumn,
            _ => Season.Winter,
        };

        /// <summary>Premier jour de la saison qui contient <paramref name="date"/> (l'hiver de janvier et février commence le 1er décembre d'avant).</summary>
        public static CivilDate StartOf(CivilDate date)
        {
            switch (SeasonOf(date))
            {
                case Season.Spring: return new CivilDate(date.Year, 3, 1);
                case Season.Summer: return new CivilDate(date.Year, 6, 1);
                case Season.Autumn: return new CivilDate(date.Year, 9, 1);
                default:
                    if (date.Month == 12) return new CivilDate(date.Year, 12, 1);
                    if (date.Year == CivilDate.MinYear) return new CivilDate(CivilDate.MinYear, 1, 1);   // borne basse du calendrier
                    return new CivilDate(date.Year - 1, 12, 1);
            }
        }

        /// <summary>Dernier jour (inclus) de la saison qui contient <paramref name="date"/> (fin février : 28 ou 29 selon l'année).</summary>
        public static CivilDate EndOf(CivilDate date)
        {
            switch (SeasonOf(date))
            {
                case Season.Spring: return new CivilDate(date.Year, 5, 31);
                case Season.Summer: return new CivilDate(date.Year, 8, 31);
                case Season.Autumn: return new CivilDate(date.Year, 11, 30);
                default:
                    int y = date.Month == 12 ? date.Year + 1 : date.Year;
                    if (y > CivilDate.MaxYear) return new CivilDate(CivilDate.MaxYear, 12, 31);   // borne haute du calendrier
                    return new CivilDate(y, 2, CivilDate.DaysInMonth(y, 2));
            }
        }

        /// <summary>Glace du lac le jour <paramref name="date"/> (sans tenir compte de l'heure : elle change à minuit, heure de Paris).</summary>
        public static IcePhase IceOf(CivilDate date)
        {
            if (date.Month == 12) return date.Day < IceWalkableDecemberDay ? IcePhase.Forming : IcePhase.Walkable;
            return date.Month <= 2 ? IcePhase.Walkable : IcePhase.None;
        }

        /// <summary>État de la saison à l'instant local de Paris <paramref name="local"/>.</summary>
        public static SeasonState At(CivilMoment local)
        {
            CivilDate date = local.Date;
            CivilDate start = StartOf(date);
            int day = checked((int)(date.DayNumber - start.DayNumber));
            const long window = TransitionDays * (long)CivilMoment.MinutesPerDay;
            long elapsed = checked(day * (long)CivilMoment.MinutesPerDay + local.Minute);
            Fixed transition = elapsed >= window ? Fixed.One : Fixed.FromRatio(elapsed, window, RoundingMode.Floor);
            IcePhase ice = IceOf(date);
            Fixed formation = ice switch
            {
                IcePhase.Walkable => Fixed.One,
                IcePhase.Forming => Fixed.FromRatio(checked((date.Day - 1) * (long)CivilMoment.MinutesPerDay + local.Minute),
                    IceFormingDays * (long)CivilMoment.MinutesPerDay, RoundingMode.Floor),
                _ => Fixed.Zero,
            };
            return new SeasonState(SeasonOf(date), start, EndOf(date), day, transition, ice, formation);
        }

        /// <summary>État de la saison au jour <paramref name="date"/> à la minute <paramref name="minute"/> (heure de Paris).</summary>
        public static SeasonState At(CivilDate date, int minute = 0) => At(new CivilMoment(date, minute));

        /// <summary>État de la saison à l'instant UTC <paramref name="unixSeconds"/> fourni par l'hôte ou le serveur, ramené à l'heure de Paris.</summary>
        public static SeasonState AtUtc(long unixSeconds) => At(ParisTime.ToLocal(unixSeconds));
    }
}
