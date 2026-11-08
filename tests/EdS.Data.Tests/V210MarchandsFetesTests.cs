using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.10 (marchands vivants et fêtes du bourg) : stock fini qui s'épuise et se regagne chaque jour du monde, objets revendus en
/// vitrine (revendus plus cher), prix qui bougent avec la saison et le temps, contrecoups des événements (le hameau pillé, la faille restée
/// ouverte, la route d'Ambre coupée) ; calendrier des fêtes (jour de marché, foire de printemps, fête des moissons, veillée de l'Aube), le
/// colporteur de passage et son étal ambulant, les invités de la fête sur la place, les concours (Firmin, Aldric, frère Anselme). Formats :
/// histoire en schéma 13, marchands en schéma 5, sauvegarde v16 (anciennes versions encore lues).
/// </summary>
[Trait("Category", "World")]
public class V210MarchandsFetesTests
{
    private const string Herboriste = "merchant.herboriste", Potion = "item.potion_de_soin", Herbe = "item.herbe_de_l_aube", Peau = "item.peau_de_loup";
    private const string Colporteur = "merchant.colporteur";

    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    private static WorldSession Open(double x, double z, SavedCharacter hero, long day, int minute = 9 * 60, MerchantBook? book = null)
    {
        ZoneDefinition zone = Zone(x, z);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, minute) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), market: new Market(book ?? ShippedMerchants));
        session.Step();
        return session;
    }

    private static WorldSession AtHerboriste(SavedCharacter hero, long day, int minute = 9 * 60) => Open(-65.2, -6.4, hero, day, minute);

    private static SavedCharacter Reloaded(WorldSession w) => w.Save().Character!;

    private static GroundVector P(double x, double z) => new GroundVector(EdS.Domain.Numerics.Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        EdS.Domain.Numerics.Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult ReadStory(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    // --- Formats ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Formats_du_sous_lot_et_anciennes_versions_lues()
    {
        Assert.Equal((13, 12, 13), (StoryValidator.CurrentSchemaVersion, StoryValidator.Schema12, Story.Value.SchemaVersion));
        Assert.Equal((5, 4, 5, "0.10.0-provisoire"), (MerchantValidator.CurrentSchemaVersion, MerchantValidator.Schema4, ShippedMerchants.SchemaVersion, ShippedMerchants.ContentVersion));
        Assert.Equal((16, 16), (SaveValidator.CurrentSchemaVersion, SaveValidator.StockSince));
        Assert.Equal(new[] { "festival.moissons", "festival.veillee", "festival.foire", "festival.marche" }, Story.Value.Festivals.Select(f => f.Id));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "merchants.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(MerchantsPath)).RootElement));
        // Histoire en schéma 12 : encore lue sans les fêtes ; les fêtes y sont refusées au chemin précis.
        JsonObject twelve = StoryJson();
        twelve["schemaVersion"] = 12;
        Assert.Contains(ReadStory(twelve).Report.Errors, e => e.Path == "$.data.festivals" && e.Code == ValidationCode.InvalidValue);
        V210TestData.WithoutFestivals(twelve);
        StoryLoadResult old = ReadStory(twelve);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Empty(old.Story!.Festivals);
        // Marchands en schéma 4 : encore lus sans stock vivant (offres sans limite) ; un stock y est refusé au chemin précis.
        JsonObject four = MerchantsJson();
        four["schemaVersion"] = 4;
        Assert.Contains(ReadMerchants(four).Report.Errors, e => e.Path == "$.data.merchants[0].offers[0].stock" && e.Code == ValidationCode.InvalidValue);
        V210TestData.MerchantsWithoutLiving(four);
        MerchantsLoadResult m4 = ReadMerchants(four);
        Assert.True(m4.IsValid, m4.Report.ToString());
        Assert.All(m4.Book!.Merchants.SelectMany(m => m.Offers), o => Assert.Null(o.Stock));
        Assert.Empty(m4.Book.Prices);
    }

    [Theory]
    [InlineData("weekday", "2", "$.data.festivals[0].weekday", ValidationCode.InvalidValue)]   // une fête annuelle à jour de semaine
    [InlineData("to", "\"09:00\"", "$.data.festivals[0].to", ValidationCode.InvalidValue)]
    [InlineData("season", "\"mousson\"", "$.data.festivals[0].season", ValidationCode.UnknownReference)]
    [InlineData("day", "28", "$.data.festivals[0].day", ValidationCode.OutOfRange)]
    [InlineData("from", "\"10h\"", "$.data.festivals[0].from", ValidationCode.InvalidValue)]
    public void Fete_refusee_au_chemin_precis(string field, string json, string path, ValidationCode code)
    {
        JsonObject root = StoryJson();
        root["data"]!["festivals"]![0]![field] = JsonNode.Parse(json);
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Theory]
    [InlineData(3, "weekday", "0", "festival.marche", "festival.moissons")]   // le jour de marché tomberait le jour des moissons, à leurs heures
    [InlineData(1, "day", "7", "festival.veillee", "festival.moissons")]   // la veillée (18 h) le jour des moissons (jusqu'à 22 h)
    [InlineData(1, "from", "\"22:00\"", "festival.veillee", null)]   // la veillée après les moissons, le même jour : heures disjointes
    public void Deux_fetes_ne_se_chevauchent_jamais(int index, string field, string json, string festival, string? clash)
    {
        JsonObject root = StoryJson();
        JsonObject f = root["data"]!["festivals"]![index]!.AsObject();
        f[field] = JsonNode.Parse(json);
        if (field == "day" || field == "from") (f["season"], f["day"]) = ("autumn", 7);
        StoryLoadResult r = ReadStory(root);
        if (clash is null)
        {
            Assert.DoesNotContain(r.Report.Errors, e => e.Message.Contains("chevauche", StringComparison.Ordinal));
            return;
        }
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.festivals[{index}].from" && e.Code == ValidationCode.InvalidValue
            && e.Message.Contains(festival, StringComparison.Ordinal) && e.Message.Contains(clash, StringComparison.Ordinal));
        // Les fêtes livrées ne se chevauchent pas : le jour de marché (6e jour) ne tombe jamais sur une fête annuelle (le 1er de la semaine).
        Assert.True(ReadStory(StoryJson()).IsValid);
    }

    [Fact]
    public void Concours_invites_et_PNJ_de_passage_refuses_au_chemin_precis()
    {
        JsonObject root = StoryJson();
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.ToList().FindIndex(x => (string?)x!["id"] == "dialogue.boulanger");
        JsonArray nodes = dialogues[d]!["nodes"]!.AsArray();
        int n = nodes.ToList().FindIndex(x => (string?)x!["id"] == "v210_moissons");
        // Concourir sans demander que le concours soit ouvert : il se rejouerait sans fin.
        nodes[n]!["choices"]![0]!["conditions"]!.AsArray().RemoveAt(0);
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == $"$.data.dialogues[{d}].nodes[{n}].choices[0].effects[1].id" && e.Code == ValidationCode.InvalidValue);
        // Une fête inconnue, un invité inconnu, un invité de passage, un PNJ de passage dans un intérieur.
        JsonObject other = StoryJson();
        other["data"]!["dialogues"]![d]!["entries"]!.AsArray().First(e => (string?)e!["node"] == "v210_moissons")!["conditions"]![0]!["id"] = "festival.carnaval";
        Assert.Contains(ReadStory(other).Report.Errors, e => e.Path.EndsWith(".conditions[0].id", StringComparison.Ordinal) && e.Code == ValidationCode.UnknownReference);
        JsonObject guest = StoryJson();
        guest["data"]!["festivals"]![0]!["guests"]![0]!["npc"] = "npc.colporteur";
        Assert.Contains(ReadStory(guest).Report.Errors, e => e.Path == "$.data.festivals[0].guests[0].npc" && e.Code == ValidationCode.InvalidValue);
        JsonObject visit = StoryJson();
        JsonArray npcs = visit["data"]!["npcs"]!.AsArray();
        int c = npcs.ToList().FindIndex(x => (string?)x!["id"] == "npc.colporteur");
        npcs[c]!["festivals"]!.AsArray().Add("festival.marche");
        Assert.Contains(ReadStory(visit).Report.Errors, e => e.Path == $"$.data.npcs[{c}].festivals[4]" && e.Code == ValidationCode.DuplicateId);
    }

    [Theory]
    [InlineData("$.data.merchants[1].offers[0].stock", "0", ValidationCode.OutOfRange)]
    [InlineData("$.data.merchants[1].restock", "0", ValidationCode.OutOfRange)]
    [InlineData("$.data.merchants[1].resale", "99", ValidationCode.OutOfRange)]
    [InlineData("$.data.merchants[1].festivals", "[\"festival.carnaval\"]", ValidationCode.UnknownReference)]
    [InlineData("$.data.prices[0].percent", "400", ValidationCode.OutOfRange)]
    [InlineData("$.data.prices[0].season", "\"mousson\"", ValidationCode.UnknownReference)]
    [InlineData("$.data.shocks[0].days", "29", ValidationCode.OutOfRange)]
    [InlineData("$.data.shocks[0].stockPercent", "101", ValidationCode.OutOfRange)]
    [InlineData("$.data.shocks[0].outcome", "\"draw\"", ValidationCode.InvalidValue)]
    public void Stock_prix_et_contrecoups_refuses_au_chemin_precis(string path, string json, ValidationCode code)
    {
        JsonObject root = MerchantsJson();
        string[] parts = path.Substring(2).Split('.');
        JsonNode node = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string p = parts[i];
            int b = p.IndexOf('[');
            node = b < 0 ? node[p]! : node[p.Substring(0, b)]![int.Parse(p.Substring(b + 1, p.Length - b - 2))]!;
        }
        node[parts[^1]] = JsonNode.Parse(json);
        string expected = path.EndsWith(".festivals", StringComparison.Ordinal) ? path + "[0]" : path;
        Assert.Contains(ReadMerchants(root).Report.Errors, e => e.Path == expected && e.Code == code);
        // Un contrecoup a une seule cause.
        JsonObject both = MerchantsJson();
        both["data"]!["shocks"]![0]!["fact"] = "fact.chef_des_bandits.vaincu";
        Assert.Contains(ReadMerchants(both).Report.Errors, e => e.Path == "$.data.shocks[0]" && e.Code == ValidationCode.InvalidValue);
    }

    // --- Calendrier et fêtes ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "spring", 0)]
    [InlineData(27, "spring", 6)]
    [InlineData(28, "summer", 0)]
    [InlineData(63, "autumn", 0)]
    [InlineData(105, "winter", 0)]
    [InlineData(112, "spring", 0)]
    public void Calendrier_du_monde(long day, string season, int weekday)
    {
        Assert.Equal((season, weekday), (WorldCalendar.SeasonOf(day), WorldCalendar.WeekdayOf(day)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCalendar.SeasonOf(-1));
    }

    [Fact]
    public void Les_saisons_du_calendrier_et_celles_des_tenues_vont_ensemble()
    {
        // HumanBodies.GameSeason (V2.9.4) reçoit (WardrobeSeason)WorldCalendar.SeasonIndexOf(jour) : les deux suites ont le même ordre.
        Assert.Equal(new[] { "spring", "summer", "autumn", "winter" }, WorldCalendar.Seasons);
        Assert.Equal(new[] { EdS.Runtime.WardrobeSeason.Spring, EdS.Runtime.WardrobeSeason.Summer, EdS.Runtime.WardrobeSeason.Autumn, EdS.Runtime.WardrobeSeason.Winter },
            new long[] { 0, 28, 56, 84 }.Select(d => (EdS.Runtime.WardrobeSeason)WorldCalendar.SeasonIndexOf(d)));
    }

    [Theory]
    [InlineData(5, 7 * 60, "festival.marche")]
    [InlineData(5, 13 * 60, null)]
    [InlineData(4, 8 * 60, null)]
    [InlineData(12, 8 * 60, "festival.marche")]
    [InlineData(14, 8 * 60, "festival.foire")]
    [InlineData(63, 9 * 60 + 59, null)]
    [InlineData(63, 10 * 60, "festival.moissons")]
    [InlineData(175, 21 * 60 + 59, "festival.moissons")]   // l'année d'après
    [InlineData(105, 18 * 60, "festival.veillee")]
    [InlineData(105, 23 * 60 + 59, null)]
    public void Les_fetes_reviennent_avec_le_calendrier(long day, int minute, string? festival) =>
        Assert.Equal(festival, Story.Value.FestivalAt(day, minute)?.Id);

    [Fact]
    public void L_essai_des_fetes_avance_jusqu_au_bon_jour()
    {
        var clock = new WorldClock(50);
        long? tick = EdS.Runtime.DayTrial.NextDayAt(clock, TickAt(1, 9 * 60), EdS.Runtime.DayTrial.SeasonDay(2, 7), 10 * 60 + 30);
        Assert.Equal(new WorldTime(63, 10 * 60 + 30), clock.At(tick!.Value));
        Assert.Equal(new WorldTime(12, 8 * 60), clock.At(EdS.Runtime.DayTrial.NextDayAt(clock, TickAt(5, 9 * 60), EdS.Runtime.DayTrial.Weekday(5), 8 * 60)!.Value));
        Assert.Null(EdS.Runtime.DayTrial.NextDayAt(clock, 0, _ => false, 0));
    }

    [Fact]
    public void Le_colporteur_ne_vient_qu_aux_fetes()
    {
        Npc anseau = Story.Value.Npc("npc.colporteur");
        WorldSession ordinary = Open(-57, 4.6, Hero(100_000), 1);
        Assert.True(ordinary.NpcAway(anseau));
        Assert.False(ordinary.NpcHere(anseau));
        Assert.Equal(DialogueRefusal.TooFar, ordinary.Talk("npc.colporteur", out _));
        Assert.Equal(TradeRefusal.NotFestival, ordinary.TradeStatus(Colporteur));
        Assert.Equal(TradeRefusal.NotFestival, ordinary.Buy(Colporteur, "item.pierre_a_aiguiser"));
        Assert.Equal(("festival.marche", 5L), (ordinary.NextFestival()!.Value.Festival.Id, ordinary.NextFestival()!.Value.Day));
        // Jour de marché : il est là, sa boutique est ouverte, ses objets rares tournent.
        WorldSession market = Open(-57, 4.6, Hero(100_000), 5, 8 * 60);
        Assert.Equal("festival.marche", market.Festival?.Id);
        Assert.False(market.NpcAway(anseau));
        Assert.True(market.NpcHere(anseau));
        Assert.Equal(DialogueRefusal.None, market.Talk("npc.colporteur", out DialogueView? view));
        Assert.Equal("accueil", view!.Node);
        Assert.Equal(TradeRefusal.None, market.Buy(Colporteur, "item.pierre_a_aiguiser", 2));
        Assert.Equal(4, market.Left(Colporteur, "item.pierre_a_aiguiser"));
        Assert.Equal(5, market.Stock(Colporteur).Count);   // deux offres permanentes, trois tirées
        // La route d'Ambre est coupée tant que la bande de Mâchefer tient le camp : pas de bottes ni de pendentif.
        Assert.Equal((0, 1), (market.Left(Colporteur, "item.bottes_du_renard"),
            Open(-57, 4.6, Hero(100_000).WithStory(new SavedStory(new[] { "fact.chef_des_bandits.vaincu" })), 5, 8 * 60).Left(Colporteur, "item.bottes_du_renard")));
        Assert.Contains(market.ActiveShocks, s => s.Id == "route_coupee");
    }

    [Fact]
    public void Les_invites_se_rassemblent_sur_la_place()
    {
        StoryBook book = Story.Value;
        WorldSession market = Open(-57, 4.6, Hero(), 5, 8 * 60);
        Npc lison = book.Npc("npc.lison");
        Assert.Equal(("colporteur", P(-57.5, 1.5)), (market.NpcActivity(lison), market.NpcPosition(lison)));
        WorldSession moissons = Open(-55, 3.1, Hero(), 63, 10 * 60 + 30);
        Npc firmin = book.Npc("npc.boulanger"), mathurin = book.Npc("npc.aubergiste");
        Assert.Equal(("fete_concours", P(-55, 1.5)), (moissons.NpcActivity(firmin), moissons.NpcPosition(firmin)));
        // Mathurin sort de son auberge pour tenir le tonneau de cidre sur la place.
        Assert.Equal(((string?)null, true, "tonneau"), (moissons.NpcZone(mathurin), moissons.NpcHere(mathurin), moissons.NpcActivity(mathurin)));
        // Hors de la fête, chacun reprend sa journée.
        WorldSession after = Open(-55, 3.1, Hero(), 63, 22 * 60 + 30);
        Assert.Equal("fournil", after.NpcActivity(firmin));
    }

    [Fact]
    public void Le_concours_de_Firmin_une_fois_par_fete_et_la_quete_jamais_bloquee()
    {
        var pain = new Dictionary<string, QuestProgress> { ["quest.pain_du_matin"] = new QuestProgress(QuestProgress.Done) };
        SavedCharacter hero = Hero(0, new ItemStack(Herbe, 8)).WithStory(new SavedStory(new[] { "fact.met.boulanger" }, pain));
        WorldSession w = Open(-55, 3.1, hero, 63, 10 * 60 + 30);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.boulanger", out DialogueView? view));
        Assert.Equal("v210_moissons", view!.Node);
        DialogueOption tourte = view.Options.Single(o => o.Text == "text.v210.boulanger.tourte");
        Assert.Equal(DialogueRefusal.None, w.Choose(tourte.Index, out _, out IReadOnlyList<StoryEvent> events));
        Assert.Contains(events, e => e.Kind == StoryEventKind.ContestEntered && e.Id == "festival.moissons");
        Assert.Equal((150L, 4, 15), (w.Money, w.Inventory.Count(Herbe), w.Narrative!.Reputation("villagers")));
        Assert.True(w.Contested("festival.moissons"));
        // Le concours est fermé jusqu'à la prochaine fête des moissons.
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.boulanger", out view));
        Assert.DoesNotContain(view!.Options, o => o.Text == "text.v210.boulanger.tourte");
        SavedCharacter saved = Reloaded(w);
        Assert.Equal(63L, saved.Story.Festivals["festival.moissons"]);
        WorldSession nextYear = Open(-55, 3.1, saved, 63 + WorldCalendar.DaysPerYear, 10 * 60 + 30);
        Assert.Equal(DialogueRefusal.None, nextYear.Talk("npc.boulanger", out view));
        Assert.Contains(view!.Options, o => o.Text == "text.v210.boulanger.tourte");
        // Une quête en cours passe avant la fête (Firmin parle d'abord du pain du matin).
        var started = new Dictionary<string, QuestProgress> { ["quest.pain_du_matin"] = new QuestProgress(Story.Value.Quest("quest.pain_du_matin").Steps[0].Id) };
        WorldSession busy = Open(-55, 3.1, Hero().WithStory(new SavedStory(null, started)), 63, 10 * 60 + 30);
        Assert.Equal(DialogueRefusal.None, busy.Talk("npc.boulanger", out view));
        Assert.NotEqual("v210_moissons", view!.Node);
    }

    // --- Stock vivant ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_stock_s_epuise_puis_se_regagne_chaque_jour()
    {
        WorldSession w = AtHerboriste(Hero(100_000), 1);
        Assert.Equal(30, w.Left(Herboriste, Potion));
        Assert.Equal(30, w.Stock(Herboriste).Single(o => o.Item == Potion).Left);
        Assert.Equal(TradeRefusal.None, w.Buy(Herboriste, Potion, 25));
        Assert.Equal(5, w.Left(Herboriste, Potion));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.OutOfStock, w.Buy(Herboriste, Potion, 6));
        Assert.Equal(before, Snapshot(w));   // refus atomique : ni la bourse, ni le sac, ni le stock
        Assert.Equal(5, w.Left(Herboriste, Potion));
        Assert.Equal(TradeRefusal.None, w.Buy(Herboriste, Potion, 5));
        Assert.Equal(0, w.Stock(Herboriste).Single(o => o.Item == Potion).Left);
        SavedCharacter saved = Reloaded(w);
        Assert.Equal("merchant.herboriste/item.potion_de_soin 30 0 1", string.Join(",", saved.Stock.Select(s => $"{s.Id} {s.Bought} {s.Resold} {s.Day}")));
        // La moitié du stock plein revient chaque jour (50 %), jusqu'au plein ; la ligne pleine n'est plus écrite.
        Assert.Equal(15, AtHerboriste(saved, 2).Left(Herboriste, Potion));
        WorldSession full = AtHerboriste(saved, 3);
        Assert.Equal(30, full.Left(Herboriste, Potion));
        Assert.Equal(TradeRefusal.None, full.Buy(Herboriste, Herbe, 1));
        Assert.DoesNotContain(Reloaded(full).Stock, s => s.Item == Potion);
    }

    [Fact]
    public void Ce_qu_on_revend_part_en_vitrine_et_se_revend_plus_cher()
    {
        WorldSession w = AtHerboriste(Hero(1_000, new ItemStack(Peau, 10)), 1);
        long unit = w.SellUnit(Herboriste, Peau);
        Assert.Equal(TradeRefusal.None, w.Sell(Herboriste, Peau, 10));
        MerchantOffer resold = w.Stock(Herboriste).Single(o => o.Item == Peau);
        Assert.True(resold.Resold);
        Assert.Equal((10, 52L), (resold.Left, resold.Price));   // 130 % de sa valeur (40)
        Assert.True(resold.Price > unit && resold.Price >= ShippedMerchants.Values[Peau]);
        long money = w.Money;
        Assert.Equal(TradeRefusal.None, w.Buy(Herboriste, Peau, 3));
        Assert.Equal(money - 3 * 52, w.Money);
        // Le rachat reprend de la vitrine ce qu'elle garde encore.
        Assert.Equal(TradeRefusal.None, w.BuyBack(Herboriste, 0));
        Assert.DoesNotContain(w.Stock(Herboriste), o => o.Item == Peau);
        // Sans rachat : l'herboriste écoule la moitié de sa vitrine chaque jour.
        WorldSession v = AtHerboriste(Hero(1_000, new ItemStack(Peau, 8)), 1);
        Assert.Equal(TradeRefusal.None, v.Sell(Herboriste, Peau, 8));
        SavedCharacter saved = Reloaded(v);
        Assert.Equal(4, AtHerboriste(saved, 2).Left(Herboriste, Peau));
        Assert.DoesNotContain(AtHerboriste(saved, 3).Stock(Herboriste), o => o.Item == Peau);
    }

    // --- Prix qui bougent et contrecoups ---------------------------------------------------------------------------------

    [Fact]
    public void Les_prix_suivent_les_saisons_sans_jamais_passer_sous_la_valeur()
    {
        long Price(long day, string item) => AtHerboriste(Hero(), day).Stock(Herboriste).Single(o => o.Item == item).Price;
        Assert.Equal((60L, 22L), (Price(1, Potion), Price(1, Herbe)));   // printemps : prix du fichier
        Assert.Equal(18L, Price(30, Herbe));   // été : les herbes abondent (80 %), jamais sous leur valeur (15)
        Assert.Equal(24L, Price(63 - 7, Herbe));   // automne (110 %)
        Assert.Equal(66L, Price(90, Potion));   // hiver : les potions (110 %)
        // Les fourrures, plus chères l'hiver (125 %, 150 % sous l'orage) ; au printemps, au prix du fichier.
        WorldSession winter = AtHerboriste(Hero(), 90);
        Assert.Equal(winter.Weather.Kind == WeatherKind.Storm ? 150 : 125, winter.MarketPercent("merchant.armurier", "item.peau_d_ours"));
        Assert.Equal(100, AtHerboriste(Hero(), 1).MarketPercent("merchant.armurier", "item.peau_d_ours"));
    }

    [Fact]
    public void L_attaque_perdue_vide_les_etals_et_fait_monter_les_remedes()
    {
        SavedCharacter lost = Hero(100_000).WithEvents(new[] { new SavedEvent("zone.vallons/attaque_hameau", 0, 1, 1, EventOutcome.Lost) });
        WorldSession w = AtHerboriste(lost, 2);
        Assert.Contains(w.ActiveShocks, s => s.Id == "attaque_perdue");
        MerchantOffer potion = w.Stock(Herboriste).Single(o => o.Item == Potion);
        Assert.Equal((9, 78L), (potion.Left, potion.Price));   // 30 % du stock, 130 % du prix
        Assert.Equal(TradeRefusal.OutOfStock, w.Buy(Herboriste, Potion, 10));
        // Trois jours plus tard, tout est revenu.
        WorldSession later = AtHerboriste(lost, 4);
        Assert.DoesNotContain(later.ActiveShocks, s => s.Id == "attaque_perdue");
        Assert.Equal((30, 60L), (later.Left(Herboriste, Potion), later.Stock(Herboriste).Single(o => o.Item == Potion).Price));
        // Le hameau défendu : un geste des herbières (90 %), jamais sous la valeur.
        SavedCharacter won = Hero().WithEvents(new[] { new SavedEvent("zone.vallons/attaque_hameau", 1, 0, 1, EventOutcome.Won) });
        Assert.Equal(54L, AtHerboriste(won, 2).Stock(Herboriste).Single(o => o.Item == Potion).Price);
        // La faille restée ouverte : plus un éclat de Seuil sur les étals.
        SavedCharacter rift = Hero().WithEvents(new[] { new SavedEvent("zone.vallons/faille_du_seuil", 0, 1, 1, EventOutcome.Lost) });
        Assert.Equal(0, AtHerboriste(rift, 2).Left(Herboriste, "item.eclat_de_seuil"));
    }

    /// <summary>
    /// Propriétés, sur plusieurs saisons et contrecoups (graine fixe) : un refus ne change rien ; la somme des pièces et de la valeur de
    /// revente du sac ne croît jamais (acheter, revendre, racheter, en vitrine ou non, ne crée jamais d'argent) ; le stock reste borné.
    /// </summary>
    [Fact]
    public void Proprietes_du_marche_vivant()
    {
        var random = new Pcg32(2010);
        string[] items = { Potion, Herbe, Peau, "item.remede_de_l_aube", "item.eclat_de_seuil", "item.gambison", "item.peau_d_ours" };
        SavedCharacter hero = Hero(5_000, new ItemStack(Peau, 20), new ItemStack("item.peau_d_ours", 6), new ItemStack(Herbe, 10))
            .WithEvents(new[] { new SavedEvent("zone.vallons/attaque_hameau", 0, 1, 29, EventOutcome.Lost) });
        int done = 0;
        foreach (long day in new long[] { 1, 2, 30, 31, 63, 90, 91 })
        {
            var market = new Market(ShippedMerchants);
            WorldSession w = Open(-65.2, -6.4, hero, day, 9 * 60, null);
            long Wealth() => w.Money + w.Inventory.Stacks.Sum(s => new Market(ShippedMerchants).UnitPrice(Herboriste, s.Item) * s.Count);
            long wealth = Wealth();
            for (int i = 0; i < 400; i++)
            {
                string item = items[random.NextBounded((uint)items.Length)];
                int count = 1 + (int)random.NextBounded(i % 40 == 0 ? 80u : 5u);
                string before = Snapshot(w);
                int? left = w.Stock(Herboriste).FirstOrDefault(o => o.Item == item)?.Left;
                TradeRefusal r = random.NextBounded(3) switch
                {
                    0 => w.Buy(Herboriste, item, count),
                    1 => w.Sell(Herboriste, item, count),
                    _ => w.BuyBack(Herboriste, (int)random.NextBounded(4)),
                };
                if (r != TradeRefusal.None) Assert.Equal(before, Snapshot(w));
                else done++;
                if (r == TradeRefusal.OutOfStock) Assert.True(left < count);
                long now = Wealth();
                Assert.True(now <= wealth, $"jour {day}, opération {i} : {wealth} → {now}");
                wealth = now;
                Assert.All(w.Stock(Herboriste), o => Assert.True(o.Left is null || (o.Left >= 0 && o.Left <= 2 * SavedStock.MaxUnits)));
            }
            hero = Reloaded(w);
            Assert.True(hero.Stock.Count <= SavedStock.MaxEntries);
        }
        Assert.True(done > 300, done.ToString());
    }

    [Fact]
    public void Jour_de_fete_la_foule_du_marche_s_entend_toute_la_fete()
    {
        // 20 h : le marché s'est tu d'ordinaire ; un jour de fête, la foule, la rumeur et la taverne s'entendent sur la place.
        double Volume(EdS.Runtime.StyleAmbience a, EdS.Runtime.AmbiencePlace p) => EdS.Runtime.StyleRules.PlaceVolume(a, p, 20 * 60, double.PositiveInfinity, false, false, 0, 0);
        EdS.Runtime.AmbiencePlace town = EdS.Runtime.AmbiencePlace.Town, fete = town | EdS.Runtime.AmbiencePlace.Festival;
        Assert.Equal(0, Volume(EdS.Runtime.StyleAmbience.Market, town));
        Assert.Equal((1.0, 0.8, 0.5), (Volume(EdS.Runtime.StyleAmbience.Market, fete), Volume(EdS.Runtime.StyleAmbience.Village, fete), Volume(EdS.Runtime.StyleAmbience.Tavern, fete)));
        // Hors du bourg, rien.
        Assert.Equal(0, Volume(EdS.Runtime.StyleAmbience.Market, EdS.Runtime.AmbiencePlace.Festival));
    }

    [Fact]
    public void Les_musiques_de_fete_et_leur_repli()
    {
        EdS.Runtime.AmbiencePlace fete = EdS.Runtime.AmbiencePlace.Town | EdS.Runtime.AmbiencePlace.Festival;
        EdS.Runtime.StyleMusic Music(EdS.Runtime.AmbiencePlace p, bool fighting = false, double night = 0) =>
            EdS.Runtime.StyleRules.MusicFor(false, fighting, true, false, false, night, false, false, p);
        Assert.Equal(EdS.Runtime.StyleMusic.Feast, Music(fete | EdS.Runtime.AmbiencePlace.Feast));
        Assert.Equal(EdS.Runtime.StyleMusic.Dance, Music(fete, night: 1));   // la veillée, de nuit
        Assert.Equal(EdS.Runtime.StyleMusic.Combat, Music(fete, fighting: true));
        Assert.Equal(EdS.Runtime.StyleMusic.Village, Music(EdS.Runtime.AmbiencePlace.Town));
        Assert.True(EdS.Runtime.StyleRules.IsFeast("festival.moissons") && EdS.Runtime.StyleRules.IsFeast("festival.foire") && !EdS.Runtime.StyleRules.IsFeast("festival.marche"));
        foreach ((string path, EdS.Runtime.StyleMusic kind) in EdS.Runtime.StyleRules.MusicFiles)
            Assert.Equal(kind, EdS.Runtime.StyleRules.MusicOf("Assets/" + path));
        Assert.Equal(EdS.Runtime.StyleMusic.Village, EdS.Runtime.StyleRules.MusicFallback(EdS.Runtime.StyleMusic.Feast));
        Assert.Equal(EdS.Runtime.StyleMusic.Village, EdS.Runtime.StyleRules.MusicFallback(EdS.Runtime.StyleMusic.Dance));
    }

    // --- Sauvegarde v16 --------------------------------------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_v16_du_stock_et_des_concours_et_v15_encore_lue()
    {
        WorldSession w = AtHerboriste(Hero(100_000, new ItemStack(Peau, 3)), 1);
        Assert.Equal(TradeRefusal.None, w.Buy(Herboriste, Potion, 4));
        Assert.Equal(TradeRefusal.None, w.Sell(Herboriste, Peau, 3));
        WorldSave save = w.Save();
        save = new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed,
            save.Character!.WithStory(save.Character.Story.WithFestivals(new Dictionary<string, long> { ["festival.moissons"] = 63 })));
        string text = SaveJsonReader.Write(save);
        Assert.Contains("\"stock\": [", text, StringComparison.Ordinal);
        Assert.Contains("{\"id\": \"merchant.herboriste/item.peau_de_loup\", \"bought\": 0, \"resold\": 3, \"day\": 1}", text, StringComparison.Ordinal);
        Assert.Contains("\"festivals\": {\"festival.moissons\": 63}", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(text, SaveJsonReader.Write(read.Save!));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // En v15 : ni stock ni concours (champs inconnus) ; sans eux, lue telle quelle (tout est plein, aucun concours).
        string v15 = text.Replace($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", "\"schemaVersion\": 15");
        SaveLoadResult refused = SaveJsonReader.Read(v15);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".stock", StringComparison.Ordinal) && e.Code == ValidationCode.UnknownField);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".festivals", StringComparison.Ordinal) && e.Code == ValidationCode.UnknownField);
        string plain = SaveJsonReader.Write(AtHerboriste(Hero(5), 1).Save()).Replace($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", "\"schemaVersion\": 15");
        SaveLoadResult old = SaveJsonReader.Read(plain);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Empty(old.Save!.Character!.Stock);
        // Formes refusées au chemin précis : ligne vide, hors bornes, désordre.
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"bought\": 0, \"resold\": 3", "\"bought\": 0, \"resold\": 0")).Report.Errors, e => e.Code == ValidationCode.InvalidValue && e.Path.Contains(".stock["));
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"resold\": 3", "\"resold\": 1000")).Report.Errors, e => e.Code == ValidationCode.OutOfRange && e.Path.EndsWith(".resold", StringComparison.Ordinal));
    }
}
