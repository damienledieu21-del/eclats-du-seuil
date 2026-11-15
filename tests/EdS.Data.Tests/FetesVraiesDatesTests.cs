using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Time;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Fêtes aux vraies dates (décision de Damien du 29 octobre 2026, sujet 12 : fêtes d'Orsédia aux dates des vraies fêtes, de 1 à 2 semaines ;
/// saisons du 15 novembre 2026 : date réelle fournie par l'hôte, heure de Paris) : histoire en schéma 14 (<c>date</c> d'une fête annuelle),
/// fête datée qui suit la date réelle quand elle est fournie, calendrier du jeu sinon ; jour réel d'un jour du monde projeté depuis l'instant
/// fourni.
/// </summary>
[Trait("Category", "World")]
public class FetesVraiesDatesTests
{
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    private static long Utc(int y, int mo, int d, int h = 12) => new DateTimeOffset(y, mo, d, h, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    private static WorldSession Open(long day, int minute)
    {
        ZoneDefinition zone = Zone();
        var hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, minute) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    private static StoryLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);
    }

    private static JsonObject Fest(JsonObject root, string id) =>
        root["data"]!["festivals"]!.AsArray().Select(f => f!.AsObject()).Single(f => (string)f["id"]! == id);

    private static int Index(string id) => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!["data"]!["festivals"]!.AsArray()
        .Select((f, i) => ((string)f!["id"]!, i)).Single(x => x.Item1 == id).i;

    private static string First(StoryLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    [Fact]
    public void Fetes_annuelles_datees_dans_l_histoire_livree()
    {
        StoryBook b = Story.Value;
        Assert.Equal(StoryValidator.CurrentSchemaVersion, b.SchemaVersion);
        Assert.Equal((9, 15, 14), (b.Festival("festival.moissons").Month, b.Festival("festival.moissons").StartDay, b.Festival("festival.moissons").Days));
        Assert.Equal((11, 15, 14), (b.Festival("festival.veillee").Month, b.Festival("festival.veillee").StartDay, b.Festival("festival.veillee").Days));
        Assert.Equal((4, 10, 14), (b.Festival("festival.foire").Month, b.Festival("festival.foire").StartDay, b.Festival("festival.foire").Days));
        Assert.False(b.Festival("festival.marche").HasDate);   // les marchés restent sur la semaine du monde
        Festival moissons = b.Festival("festival.moissons");
        Assert.False(moissons.OnCivil(new CivilDate(2027, 9, 14)));
        Assert.True(moissons.OnCivil(new CivilDate(2027, 9, 15)));
        Assert.True(moissons.OnCivil(new CivilDate(2027, 9, 28)));   // 14e jour
        Assert.False(moissons.OnCivil(new CivilDate(2027, 9, 29)));
        Festival givre = b.Festival("festival.givre");
        Assert.True(givre.CalendarOnly);
        Assert.True(givre.OnCivil(new CivilDate(2027, 12, 31)));
        Assert.False(givre.OnCivil(new CivilDate(2028, 1, 1)));
        Assert.Equal(new CivilDate(2027, 12, 18), givre.OccurrenceOf(new CivilDate(2027, 12, 25)));
        // Le calendrier décidé le 29 octobre 2026 : douze fêtes, une qui commence chaque mois, de 7 à 14 jours, jamais deux à la fois.
        Festival[] dated = b.Festivals.Where(f => f.HasDate).ToArray();
        Assert.Equal(Enumerable.Range(1, 12), dated.Select(f => f.Month!.Value).OrderBy(m => m));
        Assert.All(dated, f => Assert.InRange(f.Days!.Value, 7, 14));
        Assert.Equal(new[] { "festival.foire", "festival.moissons", "festival.veillee" }, dated.Where(f => !f.CalendarOnly).Select(f => f.Id).OrderBy(x => x, StringComparer.Ordinal));
        for (CivilDate d = new CivilDate(2027, 1, 1); d.Year == 2027; d = d.AddDays(1))
            Assert.True(dated.Count(f => f.OnCivil(d)) <= 1, d.ToString());
        Assert.Equal((4, 9, 11), (b.Festival("festival.foire").Month!.Value, moissons.Month!.Value, b.Festival("festival.veillee").Month!.Value));
    }

    [Fact]
    public void Date_refusee_avec_son_chemin()
    {
        string p = $"$.data.festivals[{Index("festival.moissons")}].date";
        string m = $"$.data.festivals[{Index("festival.marche")}].date";
        Assert.Equal(p + ".month", First(Mutated(r => Fest(r, "festival.moissons")["date"]!["month"] = 13)));
        Assert.Equal(p + ".day", First(Mutated(r => Fest(r, "festival.moissons")["date"] = JsonNode.Parse("{\"month\": 2, \"day\": 29, \"days\": 7}"))));   // pas chaque année
        Assert.Equal(p + ".days", First(Mutated(r => Fest(r, "festival.moissons")["date"]!["days"] = 6)));
        Assert.Equal(p + ".days", First(Mutated(r => Fest(r, "festival.moissons")["date"]!["days"] = 15)));
        Assert.Equal(m, First(Mutated(r => Fest(r, "festival.marche")["date"] = JsonNode.Parse("{\"month\": 5, \"day\": 1, \"days\": 7}"))));   // fête de la semaine
        // Deux fêtes datées qui se chevauchent : refusé (une seule fête datée à la fois), même à cheval sur deux années.
        string f = $"$.data.festivals[{Index("festival.foire")}].date";
        Assert.Equal(f, First(Mutated(r => Fest(r, "festival.foire")["date"] = JsonNode.Parse("{\"month\": 11, \"day\": 20, \"days\": 7}"))));   // la veillée
        Assert.Equal(f, First(Mutated(r => Fest(r, "festival.foire")["date"] = JsonNode.Parse("{\"month\": 9, \"day\": 25, \"days\": 7}"))));   // les moissons
        Assert.Equal($"$.data.festivals[{Index("festival.lanternes")}].date",   // à cheval sur deux années : la foire du 28 décembre gêne les Lanternes
            First(Mutated(r => { Fest(r, "festival.givre")["date"] = JsonNode.Parse("{\"month\": 12, \"day\": 1, \"days\": 7}"); Fest(r, "festival.foire")["date"] = JsonNode.Parse("{\"month\": 12, \"day\": 28, \"days\": 7}"); })));
        Assert.True(Mutated(r => Fest(r, "festival.foire")["date"] = JsonNode.Parse("{\"month\": 10, \"day\": 4, \"days\": 7}")).IsValid);
        // Fête du calendrier seulement : son nom et sa date, rien d'autre.
        string g = $"$.data.festivals[{Index("festival.givre")}]";
        Assert.Equal(g + ".calendarOnly", First(Mutated(r => Fest(r, "festival.givre")["from"] = "10:00")));
        Assert.Equal(g + ".calendarOnly", First(Mutated(r => Fest(r, "festival.givre")["guests"] = JsonNode.Parse("[{\"npc\": \"npc.lison\", \"position\": [-10, -5], \"activity\": \"fete\"}]"))));
        Assert.Equal(g + ".date", First(Mutated(r => Fest(r, "festival.givre").Remove("date"))));
        // Une histoire en version 13 ne connaît pas les dates ; sans elles, elle reste lue.
        Assert.Equal(p, First(Mutated(r => r["schemaVersion"] = 13)));
        StoryLoadResult old = Mutated(r =>
        {
            r["schemaVersion"] = 13;
            JsonArray all = r["data"]!["festivals"]!.AsArray();
            for (int i = all.Count - 1; i >= 0; i--)
                if (all[i]!["calendarOnly"] != null) all.RemoveAt(i);
                else all[i]!.AsObject().Remove("date");
        });
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Equal(3, old.Story!.Festivals.Count(x => x.Weekday is null));
    }

    [Fact]
    public void Jour_reel_d_un_jour_du_monde_projete_depuis_l_instant_fourni()
    {
        WorldSession w = Open(10, 12 * 60);
        Assert.Null(w.CivilDateOfDay(10));   // sans date fournie
        w.SetCivilTime(Utc(2027, 9, 20, 10));   // 12 h à Paris (heure d'été), midi du monde : même jour
        Assert.Equal(new CivilDate(2027, 9, 20), w.CivilDateOfDay(10));
        // Une journée du monde dure 3 h : 8 jours du monde font un jour réel.
        Assert.Equal(new CivilDate(2027, 9, 21), w.CivilDateOfDay(18));
        Assert.Equal(new CivilDate(2027, 9, 19), w.CivilDateOfDay(2));
        Assert.Equal(new CivilDate(2027, 10, 20), w.CivilDateOfDay(10 + 30 * 8));
    }

    [Fact]
    public void La_fete_datee_suit_la_date_reelle_et_le_calendrier_du_jeu_sans_date()
    {
        Festival moissons = Story.Value.Festival("festival.moissons");
        // Sans date : le jour du calendrier du jeu (automne, jour 7), comme avant.
        long gameDay = 2 * WorldCalendar.DaysPerSeason + moissons.Day!.Value;
        WorldSession old = Open(gameDay, 12 * 60);
        Assert.Equal("festival.moissons", old.Festival?.Id);
        // Avec une date réelle hors de la fête : pas de fête des moissons ce jour-là.
        old.SetCivilTime(Utc(2027, 6, 1, 10));
        Assert.NotEqual("festival.moissons", old.Festival?.Id);
        // Le 25 septembre : fête des moissons tous les jours du monde, de 10 h à 22 h du monde.
        WorldSession w = Open(3, 12 * 60);
        w.SetCivilTime(Utc(2027, 9, 25, 10));
        Assert.Equal("festival.moissons", w.Festival?.Id);
        Assert.Equal("festival.moissons", w.FestivalToday?.Id);
        for (long day = 3; day < 3 + 16; day++)
            Assert.Equal("festival.moissons", w.FestivalOnDay(day)?.Id);
        // Le 4 octobre (dans 9 jours réels, 72 jours du monde) : finie.
        Assert.NotEqual("festival.moissons", w.FestivalOnDay(3 + 9 * 8)?.Id);
        WorldSession night = Open(3, 23 * 60);
        night.SetCivilTime(Utc(2027, 9, 25, 10));
        Assert.Null(night.Festival);   // hors des heures de la fête
        Assert.Equal("festival.moissons", night.FestivalToday?.Id);
    }

    [Fact]
    public void Prochaine_fete_cherchee_sur_une_annee_reelle()
    {
        WorldSession w = Open(3, 12 * 60);
        w.SetCivilTime(Utc(2027, 9, 1, 10));
        Assert.NotNull(w.NextFestival());
        // Relecture de la PR #174 : le calcul direct donne exactement ce que donne la recherche jour par jour, à plusieurs dates.
        foreach (long utc in new[] { Utc(2027, 9, 1, 10), Utc(2027, 9, 13, 10), Utc(2027, 11, 14, 22), Utc(2028, 1, 2, 3), Utc(2027, 6, 30, 12) })
        {
            WorldSession x = Open(3, 12 * 60);
            x.SetCivilTime(utc);
            (Festival, long)? expected = null;
            for (long d = 3; d < 3 + 3000; d++)
                if (x.FestivalOnDay(d) is Festival f && (d > 3 || 12 * 60 < f.To)) { expected = (f, d); break; }
            Assert.Equal(expected?.Item1.Id, x.NextFestival()?.Festival.Id);
            Assert.Equal(expected?.Item2, x.NextFestival()?.Day);
        }
        // La veillée de l'Aube (rattachée à la Veillée des Contes) : le premier jour du monde où elle tombe est le 15 novembre (heure de Paris).
        long day = 3;
        while (w.FestivalOnDay(day)?.Id != "festival.veillee") day++;
        Assert.Equal(new CivilDate(2027, 11, 15), w.CivilDateOfDay(day));
        Assert.Equal(new CivilDate(2027, 11, 14), w.CivilDateOfDay(day - 1));
    }

    [Fact]
    public void Fete_du_calendrier_seulement_affichee_sans_mettre_le_monde_en_fete()
    {
        WorldSession w = Open(3, 12 * 60);
        w.SetCivilTime(Utc(2027, 1, 3, 11));   // Nuit des Lanternes (contenu à écrire)
        Assert.Equal("festival.lanternes", w.CalendarFestival?.Id);
        Assert.Null(w.Festival);   // ni invités, ni conditions, ni habits de fête
        Assert.Null(w.FestivalToday);
        Assert.NotEqual("festival.lanternes", w.FestivalOnDay(3)?.Id);
        // Une fête qui a son contenu : la fête et le calendrier disent la même.
        w.SetCivilTime(Utc(2027, 9, 20, 10));
        Assert.Equal(("festival.moissons", "festival.moissons"), (w.CalendarFestival?.Id, w.Festival?.Id));
        // Hors de toute fenêtre, ou sans date fournie : rien.
        w.SetCivilTime(Utc(2027, 9, 5, 10));
        Assert.Null(w.CalendarFestival);
        Assert.Null(Open(3, 12 * 60).CalendarFestival);
    }

    [Fact]
    public void Le_marche_de_la_semaine_se_tient_pendant_une_fete_datee()
    {
        // Relecture de la PR #174 : pendant les 14 jours de la fête des moissons, le marché du vendredi et celui du mercredi ont toujours lieu.
        WorldSession w = Open(3, 8 * 60);
        w.SetCivilTime(Utc(2027, 9, 20, 6));   // en pleine fête des moissons
        Assert.Equal("festival.moissons", w.FestivalToday?.Id);
        Assert.NotNull(w.NextMarketDay());
        long friday = 3;
        while (WorldCalendar.WeekdayOf(friday) != Story.Value.Festival("festival.marche").Weekday) friday++;
        Assert.Equal(("festival.marche", "festival.moissons"), (w.NextMarketDay()?.Festival.Id, w.FestivalOnDay(friday)?.Id));
        // Le jour du marché, à 8 h du monde (avant la fête, de 10 h à 22 h) : le marché bat son plein, et c'est bien le marché du jour.
        WorldSession market = Open(friday, 8 * 60);
        market.SetCivilTime(Utc(2027, 9, 20, 6));
        Assert.Equal("festival.marche", market.MarketFestivalToday?.Id);
        Assert.NotEqual(EdS.Data.Merchants.MarketPhase.Calm, market.MarketPhase);
        Assert.Equal("festival.marche", market.Festival?.Id);
        // Décision de Damien du 15 novembre 2026 : à 10 h 30 du monde, la fête des moissons (10 h à 22 h) et le marché (7 h à 13 h) se croisent :
        // la fête datée passe avant le marché, et le marché se tient quand même.
        WorldSession both = Open(friday, 10 * 60 + 30);
        both.SetCivilTime(Utc(2027, 9, 20, 6));
        Assert.Equal("festival.moissons", both.Festival?.Id);
        Assert.Equal("festival.marche", both.MarketFestivalToday?.Id);
        Assert.NotEqual(EdS.Data.Merchants.MarketPhase.Calm, both.MarketPhase);
    }

    [Fact]
    public void La_fete_datee_passe_avant_le_marche_quel_que_soit_l_ordre_des_donnees()
    {
        // Le marché du vendredi déplacé en tête des fêtes : la fête datée passe toujours avant lui quand leurs heures se croisent.
        StoryLoadResult r = Mutated(root =>
        {
            var list = root["data"]!["festivals"]!.AsArray();
            JsonNode marche = list.Single(f => (string)f!["id"]! == "festival.marche")!;
            list.Remove(marche);
            list.Insert(0, marche);
        });
        Assert.True(r.IsValid, r.Report.ToString());
        StoryBook book = r.Story!;
        long friday = 0;
        while (WorldCalendar.WeekdayOf(friday) != book.Festival("festival.marche").Weekday) friday++;
        var sept20 = new CivilDate(2027, 9, 20);
        Assert.Equal("festival.moissons", book.FestivalAt(friday, 10 * 60 + 30, sept20)?.Id);
        Assert.Equal("festival.marche", book.FestivalAt(friday, 8 * 60, sept20)?.Id);
        Assert.Equal("festival.moissons", book.FestivalOn(friday, sept20)?.Id);
    }

    [Fact]
    public void Un_seul_concours_par_occurrence_d_une_fete_datee()
    {
        WorldSession w = Open(100, 12 * 60);
        w.SetCivilTime(Utc(2027, 9, 20, 10));   // jour 100 : le 20 septembre (une journée du monde : 3 h, 8 par jour réel)
        Assert.True(w.ContestOpen("festival.moissons", -1));
        Assert.False(w.ContestOpen("festival.moissons", 100));
        Assert.Equal(new CivilDate(2027, 9, 17), w.CivilDateOfDay(76));
        Assert.False(w.ContestOpen("festival.moissons", 76));   // trois jours réels plus tôt, même occurrence : un seul concours
        Assert.Equal(new CivilDate(2027, 9, 14), w.CivilDateOfDay(52));
        Assert.True(w.ContestOpen("festival.moissons", 52));   // la veille du début : une autre occurrence
        // Sans date fournie : un concours par jour du monde, comme avant.
        WorldSession old = Open(30, 12 * 60);
        Assert.False(old.ContestOpen("festival.moissons", 30));
        Assert.True(old.ContestOpen("festival.moissons", 29));
    }

    [Fact]
    public void Meme_entrees_meme_calendrier_et_frontiere_de_minuit_a_Paris()
    {
        WorldSession a = Open(3, 12 * 60), b = Open(3, 12 * 60);
        a.SetCivilTime(Utc(2027, 9, 10, 9));
        b.SetCivilTime(Utc(2027, 9, 10, 9));
        for (long d = 3; d < 3 + 400; d++) Assert.Equal(a.FestivalOnDay(d)?.Id, b.FestivalOnDay(d)?.Id);
        // La veille du début, 23 h 30 UTC : déjà le 15 septembre à Paris (1 h 30, heure d'été) : la fête a commencé.
        WorldSession late = Open(3, 12 * 60);
        late.SetCivilTime(new DateTimeOffset(2027, 9, 14, 23, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.Equal(new CivilDate(2027, 9, 15), late.CivilDateOfDay(3));
        Assert.Equal("festival.moissons", late.FestivalToday?.Id);
        WorldSession before = Open(3, 12 * 60);
        before.SetCivilTime(new DateTimeOffset(2027, 9, 14, 21, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds());   // 23 h 30 à Paris, le 14
        Assert.Equal(new CivilDate(2027, 9, 14), before.CivilDateOfDay(3));
        Assert.NotEqual("festival.moissons", before.FestivalToday?.Id);
    }

    [Fact]
    public void Rejeu_depuis_la_sauvegarde_meme_calendrier_et_memes_concours()
    {
        // Relecture de la PR #174 : le jour réel d'un jour du monde est pris à midi du monde ; il ne bascule jamais en pleine journée du monde.
        // Une partie sauvegardée puis rouverte, avec l'instant réel correspondant, retrouve le même calendrier, la même fête et les mêmes concours.
        long t0 = new DateTimeOffset(2027, 9, 14, 21, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();   // 23 h 30 à Paris, la veille des moissons
        WorldSession a = Open(3, 12 * 60);
        a.SetCivilTime(t0);
        for (int i = 0; i < 2000; i++) a.Step();   // 100 s réelles : minuit passe à Paris
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(a.Save())).Save!;
        ZoneDefinition zone = Zone();
        var b = new WorldSession(saved, zone, ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        b.SetCivilTime(t0 + 2000 * 50 / 1000);
        for (long d = 0; d < 3 + 200; d++)
        {
            Assert.Equal(a.CivilDateOfDay(d), b.CivilDateOfDay(d));
            Assert.Equal(a.FestivalOnDay(d)?.Id, b.FestivalOnDay(d)?.Id);
            Assert.Equal(a.ContestOpen("festival.moissons", d), b.ContestOpen("festival.moissons", d));
        }
        Assert.Equal((a.Festival?.Id, a.FestivalToday?.Id, a.NextFestival()?.Day), (b.Festival?.Id, b.FestivalToday?.Id, b.NextFestival()?.Day));
        // Dans la journée du monde en cours, le jour réel reste celui de son midi, quel que soit le tick.
        Assert.Equal(new CivilDate(2027, 9, 14), a.CivilDateOfDay(a.Zone.Time.Day));
    }

    [Fact]
    public void Vingt_neuf_fevrier_et_occurrence_commencee_l_annee_d_avant()
    {
        StoryLoadResult r = Mutated(root =>
        {
            Fest(root, "festival.givre")["date"] = JsonNode.Parse("{\"month\": 12, \"day\": 10, \"days\": 14}");
            Fest(root, "festival.lanternes")["date"] = JsonNode.Parse("{\"month\": 12, \"day\": 29, \"days\": 7}");
            Fest(root, "festival.liens")["date"] = JsonNode.Parse("{\"month\": 2, \"day\": 24, \"days\": 7}");
            Fest(root, "festival.carnaval")["date"] = JsonNode.Parse("{\"month\": 3, \"day\": 5, \"days\": 7}");
        });
        Assert.True(r.IsValid, r.Report.ToString());
        Festival lanternes = r.Story!.Festival("festival.lanternes");
        Assert.Equal(new CivilDate(2027, 12, 29), lanternes.OccurrenceOf(new CivilDate(2028, 1, 2)));   // commencée l'année d'avant
        Assert.Equal(new CivilDate(2027, 12, 29), lanternes.OccurrenceOf(new CivilDate(2028, 1, 4)));   // 7e jour
        Assert.Null(lanternes.OccurrenceOf(new CivilDate(2028, 1, 5)));
        Festival liens = r.Story.Festival("festival.liens");
        Assert.True(liens.OnCivil(new CivilDate(2028, 2, 29)));   // année bissextile : le 29 février compte
        Assert.True(liens.OnCivil(new CivilDate(2028, 3, 1)));    // 24 au 1er mars : 7 jours
        Assert.False(liens.OnCivil(new CivilDate(2028, 3, 2)));
        Assert.True(liens.OnCivil(new CivilDate(2027, 3, 2)));    // année ordinaire : jusqu'au 2 mars
        Assert.False(liens.OnCivil(new CivilDate(2027, 3, 3)));
    }
}
