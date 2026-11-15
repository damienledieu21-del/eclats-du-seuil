using System;
using EdS.Domain.Numerics;
using EdS.Domain.Time;
using Xunit;

namespace EdS.Domain.Tests.Time;

/// <summary>
/// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : calendrier civil, heure de Paris (règle européenne), saisons aux 1er mars,
/// 1er juin, 1er septembre et 1er décembre, 7 jours de transition, glace du lac (formation du 1er au 7 décembre, praticable du 8 décembre à
/// la fin février inclus). Tests aux bornes et de propriétés, avec <see cref="DateTime"/> comme oracle.
/// </summary>
[Trait("Category", "Time")]
public class SaisonsCalendrierTests
{
    private static long Utc(int y, int mo, int d, int h = 0, int mi = 0, int s = 0) =>
        new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.Zero).ToUnixTimeSeconds();

    private static CivilDate D(int y, int m, int d) => new CivilDate(y, m, d);

    [Fact]
    public void Annees_bissextiles_et_longueur_des_mois()
    {
        Assert.True(CivilDate.IsLeapYear(2028));
        Assert.True(CivilDate.IsLeapYear(2000));
        Assert.False(CivilDate.IsLeapYear(2026));
        Assert.False(CivilDate.IsLeapYear(1900));
        Assert.False(CivilDate.IsLeapYear(2100));
        Assert.Equal(29, CivilDate.DaysInMonth(2028, 2));
        Assert.Equal(28, CivilDate.DaysInMonth(2027, 2));
        Assert.Equal(30, CivilDate.DaysInMonth(2026, 11));
        Assert.Equal(31, CivilDate.DaysInMonth(2026, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(2026, 2, 29));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(2026, 13, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(2026, 4, 31));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(10000, 1, 1));
        D(2028, 2, 29);
    }

    [Fact]
    public void Numero_de_jour_et_jour_de_la_semaine_comme_DateTime_de_1970_a_2200()
    {
        DateTime epoch = new DateTime(1970, 1, 1);
        long last = new CivilDate(2200, 12, 31).DayNumber;
        for (long n = -800; n <= last; n++)
        {
            DateTime oracle = epoch.AddDays(n);
            CivilDate d = CivilDate.FromDayNumber(n);
            Assert.Equal((oracle.Year, oracle.Month, oracle.Day), (d.Year, d.Month, d.Day));
            Assert.Equal(n, d.DayNumber);
            Assert.Equal(((int)oracle.DayOfWeek + 6) % 7, d.DayOfWeek);   // 0 : lundi
        }
        Assert.Equal(new DateTime(1, 1, 1).Subtract(epoch).Days, CivilDate.MinDayNumber);
        Assert.Equal(new DateTime(9999, 12, 31).Subtract(epoch).Days, CivilDate.MaxDayNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilDate.FromDayNumber(CivilDate.MaxDayNumber + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilDate.FromDayNumber(CivilDate.MinDayNumber - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => D(9999, 12, 31).AddDays(1));
    }

    [Fact]
    public void Dernier_dimanche_de_mars_et_d_octobre()
    {
        Assert.Equal(D(2026, 3, 29), CivilDate.LastSunday(2026, 3));
        Assert.Equal(D(2026, 10, 25), CivilDate.LastSunday(2026, 10));
        Assert.Equal(D(2027, 3, 28), CivilDate.LastSunday(2027, 3));
        Assert.Equal(D(2027, 10, 31), CivilDate.LastSunday(2027, 10));   // le 31 lui-même
        Assert.Equal(D(2028, 3, 26), CivilDate.LastSunday(2028, 3));
        for (int y = 1996; y <= 2200; y++)
            foreach (int m in new[] { 3, 10 })
            {
                CivilDate s = CivilDate.LastSunday(y, m);
                Assert.Equal(6, s.DayOfWeek);
                Assert.Equal(m, s.Month);
                Assert.True(s.Day + 7 > CivilDate.DaysInMonth(y, m));
            }
    }

    [Fact]
    public void Heure_de_Paris_au_passage_a_l_heure_d_ete_et_d_hiver()
    {
        // Heure d'été 2026 : le 29 mars à 1 h UTC, 2 h de Paris deviennent 3 h.
        Assert.Equal(new CivilMoment(D(2026, 3, 29), 1 * 60 + 59), ParisTime.ToLocal(Utc(2026, 3, 29, 0, 59, 59)));
        Assert.False(ParisTime.IsSummerTime(Utc(2026, 3, 29, 0, 59, 59)));
        Assert.Equal(new CivilMoment(D(2026, 3, 29), 3 * 60), ParisTime.ToLocal(Utc(2026, 3, 29, 1)));
        Assert.True(ParisTime.IsSummerTime(Utc(2026, 3, 29, 1)));
        // Heure d'hiver 2026 : le 25 octobre à 1 h UTC, 3 h de Paris redeviennent 2 h.
        Assert.Equal(new CivilMoment(D(2026, 10, 25), 2 * 60 + 59), ParisTime.ToLocal(Utc(2026, 10, 25, 0, 59, 59)));
        Assert.Equal(new CivilMoment(D(2026, 10, 25), 2 * 60), ParisTime.ToLocal(Utc(2026, 10, 25, 1)));
        Assert.Equal(ParisTime.WinterOffsetMinutes, ParisTime.OffsetMinutes(Utc(2026, 10, 25, 1)));
        Assert.Equal(ParisTime.SummerOffsetMinutes, ParisTime.OffsetMinutes(Utc(2026, 7, 14, 12)));
        Assert.Equal(ParisTime.WinterOffsetMinutes, ParisTime.OffsetMinutes(Utc(2026, 12, 25, 12)));
        // Bornes.
        Assert.Equal(new CivilMoment(D(1970, 1, 1), 60), ParisTime.ToLocal(0));
        Assert.Equal(new CivilMoment(D(9999, 12, 31), 1439), ParisTime.ToLocal(ParisTime.MaxUnixSeconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParisTime.ToLocal(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParisTime.ToLocal(ParisTime.MaxUnixSeconds + 1));
    }

    [Fact]
    public void Heure_de_Paris_comme_le_fuseau_Europe_Paris_du_systeme_quand_il_existe()
    {
        TimeZoneInfo? paris = null;
        try { paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }
        // Sans base des fuseaux sur la machine, les cas explicites du test précédent font foi ; avec elle, chaque heure de 2000 à 2040.
        if (paris is null) return;
        for (long t = Utc(2000, 1, 1); t < Utc(2040, 1, 1); t += 3600)
        {
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(t).UtcDateTime, paris);
            CivilMoment mine = ParisTime.ToLocal(t);
            Assert.Equal((local.Year, local.Month, local.Day, local.Hour * 60 + local.Minute), (mine.Date.Year, mine.Date.Month, mine.Date.Day, mine.Minute));
        }
    }

    [Theory]
    [InlineData(2026, 2, 28, Season.Winter)]
    [InlineData(2026, 3, 1, Season.Spring)]
    [InlineData(2026, 5, 31, Season.Spring)]
    [InlineData(2026, 6, 1, Season.Summer)]
    [InlineData(2026, 8, 31, Season.Summer)]
    [InlineData(2026, 9, 1, Season.Autumn)]
    [InlineData(2026, 11, 30, Season.Autumn)]
    [InlineData(2026, 12, 1, Season.Winter)]
    [InlineData(2028, 2, 29, Season.Winter)]
    [InlineData(2028, 3, 1, Season.Spring)]
    public void Changement_de_saison_aux_bonnes_dates(int y, int m, int d, Season expected)
    {
        Assert.Equal(expected, SeasonCalendar.SeasonOf(D(y, m, d)));
        Assert.Equal(expected, SeasonCalendar.At(D(y, m, d)).Season);
    }

    [Fact]
    public void Changement_de_saison_a_minuit_heure_de_Paris_pas_UTC()
    {
        // Le 28 février 2026 à 22 h 59 UTC il est 23 h 59 à Paris (hiver) ; à 23 h UTC, le 1er mars à 0 h (printemps).
        Assert.Equal(Season.Winter, SeasonCalendar.AtUtc(Utc(2026, 2, 28, 22, 59)).Season);
        SeasonState spring = SeasonCalendar.AtUtc(Utc(2026, 2, 28, 23));
        Assert.Equal(Season.Spring, spring.Season);
        Assert.Equal(0, spring.Day);
        Assert.Equal(Fixed.Zero, spring.Transition);
        // En été (UTC+2) : le 31 mai à 21 h 59 UTC, encore le printemps ; à 22 h UTC, l'été.
        Assert.Equal(Season.Spring, SeasonCalendar.AtUtc(Utc(2026, 5, 31, 21, 59)).Season);
        Assert.Equal(Season.Summer, SeasonCalendar.AtUtc(Utc(2026, 5, 31, 22)).Season);
        Assert.Equal(Season.Autumn, SeasonCalendar.AtUtc(Utc(2026, 8, 31, 22)).Season);
        Assert.Equal(Season.Winter, SeasonCalendar.AtUtc(Utc(2026, 11, 30, 23)).Season);
        Assert.Equal(Season.Autumn, SeasonCalendar.AtUtc(Utc(2026, 11, 30, 22, 59)).Season);
    }

    [Fact]
    public void Debut_fin_longueur_et_jour_des_saisons_avec_le_29_fevrier()
    {
        SeasonState w = SeasonCalendar.At(D(2028, 2, 29));
        Assert.Equal((D(2027, 12, 1), D(2028, 2, 29), 91, 90), (w.Start, w.End, w.Length, w.Day));
        SeasonState w2 = SeasonCalendar.At(D(2027, 2, 28));
        Assert.Equal((D(2026, 12, 1), D(2027, 2, 28), 90, 89), (w2.Start, w2.End, w2.Length, w2.Day));
        SeasonState dec = SeasonCalendar.At(D(2027, 12, 31));
        Assert.Equal((D(2027, 12, 1), D(2028, 2, 29), 30), (dec.Start, dec.End, dec.Day));
        Assert.Equal(92, SeasonCalendar.At(D(2026, 4, 1)).Length);
        Assert.Equal(92, SeasonCalendar.At(D(2026, 7, 1)).Length);
        Assert.Equal(91, SeasonCalendar.At(D(2026, 10, 1)).Length);
        Assert.Equal(Season.Autumn, SeasonCalendar.At(D(2026, 12, 1)).Previous);
        Assert.Equal(Season.Winter, SeasonCalendar.At(D(2026, 3, 1)).Previous);
    }

    [Fact]
    public void Transition_de_sept_jours()
    {
        Assert.Equal(Fixed.Zero, SeasonCalendar.At(D(2026, 9, 1), 0).Transition);
        Assert.Equal(Fixed.FromRatio(1, 2), SeasonCalendar.At(D(2026, 9, 4), 12 * 60).Transition);
        SeasonState last = SeasonCalendar.At(D(2026, 9, 7), 1439);
        Assert.True(last.InTransition);
        Assert.True(last.Transition < Fixed.One);
        Assert.Equal(Fixed.One, SeasonCalendar.At(D(2026, 9, 8), 0).Transition);
        Assert.False(SeasonCalendar.At(D(2026, 9, 8), 0).InTransition);
        Assert.Equal(Fixed.One, SeasonCalendar.At(D(2026, 11, 30), 1439).Transition);
        // Croissante, minute après minute, sur les huit premiers jours.
        Fixed previous = Fixed.FromInt(-1);
        for (int minute = 0; minute < 8 * 1440; minute++)
        {
            Fixed t = SeasonCalendar.At(D(2026, 12, 1).AddDays(minute / 1440), minute % 1440).Transition;
            Assert.True(t >= previous);
            Assert.True(t >= Fixed.Zero && t <= Fixed.One);
            previous = t;
        }
    }

    [Theory]
    [InlineData(2026, 11, 30, IcePhase.None)]
    [InlineData(2026, 12, 1, IcePhase.Forming)]
    [InlineData(2026, 12, 7, IcePhase.Forming)]
    [InlineData(2026, 12, 8, IcePhase.Walkable)]
    [InlineData(2027, 1, 15, IcePhase.Walkable)]
    [InlineData(2027, 2, 28, IcePhase.Walkable)]
    [InlineData(2027, 3, 1, IcePhase.None)]
    [InlineData(2028, 2, 28, IcePhase.Walkable)]
    [InlineData(2028, 2, 29, IcePhase.Walkable)]
    [InlineData(2028, 3, 1, IcePhase.None)]
    [InlineData(2026, 7, 1, IcePhase.None)]
    public void Fenetre_de_glace(int y, int m, int d, IcePhase expected)
    {
        Assert.Equal(expected, SeasonCalendar.IceOf(D(y, m, d)));
        Assert.Equal(expected, SeasonCalendar.At(D(y, m, d), 720).Ice);
    }

    [Fact]
    public void Glace_7_decembre_contre_8_decembre_a_l_heure_de_Paris()
    {
        SeasonState before = SeasonCalendar.AtUtc(Utc(2026, 12, 7, 22, 59));   // 23 h 59 à Paris le 7
        Assert.Equal(IcePhase.Forming, before.Ice);
        Assert.True(before.IceFormation < Fixed.One);
        Assert.True(before.IceFormation > Fixed.FromRatio(99, 100));
        SeasonState after = SeasonCalendar.AtUtc(Utc(2026, 12, 7, 23));   // 0 h à Paris le 8
        Assert.Equal(IcePhase.Walkable, after.Ice);
        Assert.Equal(Fixed.One, after.IceFormation);
        Assert.Equal(Fixed.Zero, SeasonCalendar.At(D(2026, 12, 1), 0).IceFormation);
        Assert.Equal(Fixed.Zero, SeasonCalendar.At(D(2027, 3, 1), 0).IceFormation);
        // Fin février contre 1er mars (fonte à minuit, heure de Paris).
        Assert.Equal(IcePhase.Walkable, SeasonCalendar.AtUtc(Utc(2027, 2, 28, 22, 59)).Ice);
        Assert.Equal(IcePhase.None, SeasonCalendar.AtUtc(Utc(2027, 2, 28, 23)).Ice);
        Assert.Equal(IcePhase.Walkable, SeasonCalendar.AtUtc(Utc(2028, 2, 29, 22, 59)).Ice);
        Assert.Equal(IcePhase.None, SeasonCalendar.AtUtc(Utc(2028, 2, 29, 23)).Ice);
    }

    [Fact]
    public void Proprietes_sur_chaque_jour_de_1996_a_2200()
    {
        CivilDate day = D(1996, 1, 1);
        SeasonState previous = SeasonCalendar.At(day.AddDays(-1));
        while (day.Year <= 2200)
        {
            SeasonState s = SeasonCalendar.At(day);
            Assert.Equal(day.DayNumber - s.Start.DayNumber, s.Day);
            Assert.InRange(s.Day, 0, s.Length - 1);
            Assert.InRange(s.Length, 90, 92);
            Assert.Equal(s.End, s.Start.AddDays(s.Length - 1));
            Assert.Equal(1, s.Start.Day);
            if (s.Day == 0)
            {
                Assert.Equal(s.Previous, previous.Season);
                Assert.Equal(previous.End.AddDays(1), s.Start);
                Assert.Equal(previous.Length - 1, previous.Day);
            }
            else
            {
                Assert.Equal(previous.Season, s.Season);
                Assert.Equal(previous.Day + 1, s.Day);
            }
            // Glace : praticable exactement du 8 décembre à la fin février ; en formation exactement du 1er au 7 décembre.
            bool walkable = day.Month <= 2 || (day.Month == 12 && day.Day >= 8);
            Assert.Equal(walkable, s.Ice == IcePhase.Walkable);
            Assert.Equal(day.Month == 12 && day.Day < 8, s.Ice == IcePhase.Forming);
            Assert.Equal(s.Day >= SeasonCalendar.TransitionDays, s.Transition == Fixed.One);
            previous = s;
            day = day.AddDays(1);
        }
    }

    [Fact]
    public void Identifiants_de_saison()
    {
        foreach (Season s in Enum.GetValues(typeof(Season))) Assert.Equal(s, SeasonCalendar.Parse(SeasonCalendar.Id(s)));
        Assert.Null(SeasonCalendar.Parse("Spring"));
        Assert.Null(SeasonCalendar.Parse(null));
        Assert.Equal("autumn", SeasonCalendar.Id(Season.Autumn));
    }
}
