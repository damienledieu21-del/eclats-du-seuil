using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Simulation.Randomness;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : vraies boutiques dans de vraies maisons (horaires, jour de repos, enseigne,
/// porte où le patron ou un commis répond, fournées du boulanger : rien avant la première, pains chauds le matin, rupture le soir) et grand
/// jour de marché sur la place du bourg (installation, marché, démontage, étals, criées, marchandage selon la réputation). Format : marchands
/// en schéma 6 (schémas 2 à 5 encore lus).
/// </summary>
[Trait("Category", "World")]
public class V214BoutiquesMarcheTests
{
    private const string Herboriste = "merchant.herboriste", Potion = "item.potion_de_soin", Herbe = "item.herbe_de_l_aube", Faucille = "item.faucille_de_l_herboriste";

    internal static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    internal static WorldSession Open(double x, double z, SavedCharacter hero, long day, int minute, MerchantBook? book = null, Market? market = null)
    {
        ZoneDefinition zone = Zone(x, z);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, minute) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), market: market ?? new Market(book ?? ShippedMerchants));
        session.Step();
        Assert.Equal(new WorldTime(day, minute), session.Zone.Time);
        return session;
    }

    private static JsonNode At(JsonObject root, string path)
    {
        JsonNode node = root;
        foreach (string p in path.Substring(2).Split('.'))
        {
            int b = p.IndexOf('[');
            node = b < 0 ? node[p]! : node[p.Substring(0, b)]![int.Parse(p.Substring(b + 1, p.Length - b - 2))]!;
        }
        return node;
    }

    private static JsonObject Merchant(JsonObject root, string id) =>
        root["data"]!["merchants"]!.AsArray().Single(m => (string?)m!["id"] == id)!.AsObject();

    /// <summary>L'herboriste en vraie boutique : de 8 h à 15 h, fermée le dimanche (6), le patron à la porte pour les potions.</summary>
    private static JsonObject WithHerbShop(JsonObject? root = null)
    {
        root ??= MerchantsJson();
        root["schemaVersion"] = 6;
        JsonObject m = Merchant(root, Herboriste);
        m["shop"] = JsonNode.Parse("{\"kind\":\"herbalist\",\"sign\":\"text.merchant.herboriste.greeting\",\"building\":\"maison7\",\"open\":\"08:00\",\"close\":\"15:00\",\"rest\":[6],\"door\":\"patron\"}");
        m["offers"]![0]!["door"] = true;
        m["offers"]![1]!["door"] = true;   // l'herbe de l'Aube sert à une quête : toujours à la porte
        return root;
    }

    private static MerchantBook Valid(JsonObject root)
    {
        MerchantsLoadResult r = ReadMerchants(root);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    }

    // --- Règles pures -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(480, 900, 479, false)]
    [InlineData(480, 900, 480, true)]
    [InlineData(480, 900, 899, true)]
    [InlineData(480, 900, 900, false)]
    [InlineData(1080, 60, 1439, true)]   // l'auberge : de 18 h à 1 h, minuit passé
    [InlineData(1080, 60, 0, true)]
    [InlineData(1080, 60, 59, true)]
    [InlineData(1080, 60, 60, false)]
    [InlineData(1080, 60, 1079, false)]
    [InlineData(600, 600, 600, false)]
    public void Fenetres_d_horaires_aux_frontieres(int from, int to, int minute, bool inside) =>
        Assert.Equal(inside, ShopRules.InWindow(from, to, minute));

    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("23:59", 1439)]
    [InlineData("05:30", 330)]
    [InlineData("24:00", null)]
    [InlineData("7:00", null)]
    [InlineData("07h00", null)]
    [InlineData("07:60", null)]
    public void Heures_HH_MM(string at, int? minute) => Assert.Equal(minute, ShopRules.MinuteOf(at));

    [Fact]
    public void Proprietes_des_horaires_et_de_la_prochaine_ouverture()
    {
        var random = new Pcg32(214, 9);
        for (int n = 0; n < 300; n++)
        {
            int open = (int)random.NextBounded(1440), close = (int)random.NextBounded(1440);
            if (open == close) continue;
            var rest = new List<int>();
            for (int d = 0; d < 7; d++)
                if (random.NextBounded(4) == 0 && rest.Count < 6) rest.Add(d);
            var shop = new Shop("bakery", "text.x", null, open, close, rest, DoorService.None, null);
            long day = random.NextBounded(30);
            int minute = (int)random.NextBounded(1440);
            (long Day, int Minute)? next = ShopRules.NextOpening(shop, day, minute);
            Assert.True(next.HasValue);
            (long nd, int nm) = next!.Value;
            Assert.True(nd > day || (nd == day && nm > minute));
            Assert.True(shop.IsOpen(WorldCalendar.WeekdayOf(nd), nm));
            // La minute d'avant est fermée (c'est bien une ouverture) ; jamais un jour de repos.
            (long pd, int pm) = nm == 0 ? (nd - 1, 1439) : (nd, nm - 1);
            Assert.False(pd >= 0 && shop.IsOpen(WorldCalendar.WeekdayOf(pd), pm));
            Assert.DoesNotContain(shop.OpeningWeekday(WorldCalendar.WeekdayOf(nd), nm), shop.Rest);
        }
    }

    [Fact]
    public void Une_ouverture_apres_minuit_appartient_a_la_veille()
    {
        // L'auberge de 18 h à 1 h, fermée le lundi (0) : le mardi à 0 h 30, c'est encore le lundi soir : fermée ; le lundi à 0 h 30 (dimanche soir) : ouverte.
        var inn = new Shop("inn", "text.x", null, 18 * 60, 60, new[] { 0 }, DoorService.None, null);
        Assert.False(inn.IsOpen(1, 30));
        Assert.True(inn.IsOpen(0, 30));
        Assert.False(inn.IsOpen(0, 19 * 60));
        Assert.True(inn.IsOpen(1, 19 * 60));
    }

    private static readonly Bake[] Firmin = { Bakes(330, 60), Bakes(660, 25), Bakes(990, 15) };

    private static Bake Bakes(int at, int percent) => new Bake(at, percent);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(329, 0)]   // avant la première fournée : rien
    [InlineData(330, 24)]   // 5 h 30 : 60 % de 40
    [InlineData(659, 8)]
    [InlineData(660, 18)]   // 11 h : la deuxième fournée
    [InlineData(990, 8)]    // 16 h 30 : la troisième
    [InlineData(1169, 0)]
    [InlineData(1170, 0)]   // 19 h 30 : rupture du soir
    [InlineData(1439, 0)]
    public void Les_fournees_du_boulanger_et_la_rupture_du_soir(int minute, int shelf) =>
        Assert.Equal(shelf, ShopRules.OnShelf(Firmin, 1170, 40, minute));

    [Fact]
    public void Proprietes_des_fournees()
    {
        var random = new Pcg32(9, 11);
        for (int n = 0; n < 200; n++)
        {
            int count = 1 + (int)random.NextBounded(6), at = (int)random.NextBounded(600);
            // Parts : 100 coupé en « count » morceaux d'au moins 1 %.
            var cuts = new SortedSet<int>();
            while (cuts.Count < count - 1) cuts.Add(1 + (int)random.NextBounded(99));
            var bounds = new List<int> { 0 };
            bounds.AddRange(cuts);
            bounds.Add(100);
            var bakes = new List<Bake>();
            for (int i = 0; i < count; i++)
            {
                bakes.Add(Bakes(at, bounds[i + 1] - bounds[i]));
                at += 1 + (int)random.NextBounded(120);
            }
            at = bakes[^1].At + 1;
            int soldOut = at + (int)random.NextBounded(300);
            if (soldOut >= 1440) continue;
            int full = (int)random.NextBounded(1000);
            Assert.Equal(100, ShopRules.Baked(bakes, 1439));
            int previous = 0;
            for (int m = 0; m < 1440; m++)
            {
                int shelf = ShopRules.OnShelf(bakes, soldOut, full, m);
                Assert.InRange(shelf, 0, full);
                Assert.InRange(ShopRules.Villagers(bakes, soldOut, m), 0, ShopRules.Baked(bakes, m));
                // Le rayon ne remonte qu'à une fournée.
                if (shelf > previous) Assert.Contains(bakes, b => b.At == m);
                if (m >= soldOut || m < bakes[0].At) Assert.Equal(0, shelf);
                previous = shelf;
            }
        }
    }

    [Fact]
    public void Le_tirage_du_marchandage_est_fixe_pour_le_jour()
    {
        var seen = new HashSet<int>();
        for (long day = 0; day < 400; day++)
        {
            int roll = ShopRules.HaggleRoll("merchant.fromager", day);
            Assert.InRange(roll, 0, 99);
            Assert.Equal(roll, ShopRules.HaggleRoll("merchant.fromager", day));
            seen.Add(roll);
        }
        Assert.True(seen.Count > 80, $"{seen.Count} tirages différents sur 400 jours");
        Assert.NotEqual(Enumerable.Range(0, 50).Select(d => ShopRules.HaggleRoll("merchant.a", d)), Enumerable.Range(0, 50).Select(d => ShopRules.HaggleRoll("merchant.b", d)));
    }

    // --- Format -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Boutique_lue_et_version_5_encore_lue_sans_ses_champs()
    {
        MerchantBook book = Valid(WithHerbShop());
        Shop shop = book.Merchant(Herboriste).Shop!;
        Assert.Equal(("herbalist", 480, 900, DoorService.Patron, "maison7"), (shop.Kind, shop.Open, shop.Close, shop.Door, shop.Building));
        Assert.Equal(new[] { 6 }, shop.Rest);
        Assert.True(book.Merchant(Herboriste).Offers[0].Door);
        // En version 5, une boutique est refusée au chemin précis.
        JsonObject five = WithHerbShop();
        five["schemaVersion"] = 5;
        V214TestData.MerchantsWithoutShops(five);
        Merchant(five, Herboriste)["shop"] = JsonNode.Parse("{\"kind\":\"herbalist\",\"sign\":\"text.merchant.herboriste.greeting\",\"open\":\"08:00\",\"close\":\"15:00\"}");
        Assert.Contains(Errors(ReadMerchants(five)), e => e.Path.EndsWith(".shop", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
        JsonObject old = MerchantsJson();
        old["schemaVersion"] = 5;
        V214TestData.MerchantsWithoutShops(old);
        MerchantBook b5 = Valid(old);
        Assert.All(b5.Merchants, m => Assert.Null(m.Shop));
        Assert.Null(b5.Market);
    }

    [Theory]
    [InlineData("shop.kind", "\"patisserie\"", ValidationCode.UnknownReference)]
    [InlineData("shop.sign", "\"text.nulle_part\"", ValidationCode.UnknownReference)]
    [InlineData("shop.open", "\"8h\"", ValidationCode.InvalidValue)]
    [InlineData("shop.close", "\"08:00\"", ValidationCode.InvalidValue)]   // fenêtre vide
    [InlineData("shop.rest", "[0,1,2,3,4,5,6]", ValidationCode.OutOfRange)]
    [InlineData("shop.rest", "[6,6]", ValidationCode.DuplicateId)]
    [InlineData("shop.door", "\"fenetre\"", ValidationCode.InvalidValue)]
    [InlineData("shop.clerk", "\"text.merchant.herboriste.greeting\"", ValidationCode.InvalidValue)]   // un commis sans « clerk »
    [InlineData("shop.building", "\"Maison 7\"", ValidationCode.InvalidValue)]
    [InlineData("bakes", "[{\"at\":\"06:00\",\"percent\":100}]", ValidationCode.InvalidValue)]   // des fournées sans offre fraîche
    [InlineData("soldOut", "\"19:00\"", ValidationCode.InvalidValue)]   // une rupture sans fournées
    [InlineData("offers[1].fresh", "true", ValidationCode.InvalidValue)]   // fraîche sans fournées
    public void Boutique_refusee_au_chemin_precis(string field, string json, ValidationCode code)
    {
        JsonObject root = WithHerbShop();
        int index = root["data"]!["merchants"]!.AsArray().ToList().FindIndex(m => (string?)m!["id"] == Herboriste);
        string path = $"$.data.merchants[{index}].{field}";
        string parent = path.Substring(0, path.LastIndexOf('.'));
        string name = path.Substring(path.LastIndexOf('.') + 1);
        At(root, parent)[name] = JsonNode.Parse(json);
        string expected = field == "bakes" ? path : field.StartsWith("shop.rest", StringComparison.Ordinal) && code == ValidationCode.DuplicateId ? path + "[1]" : path;
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == expected && e.Code == code);
    }

    [Fact]
    public void Fournees_et_jamais_ouverte_refusees_au_chemin_precis()
    {
        JsonObject root = WithHerbShop();
        int index = root["data"]!["merchants"]!.AsArray().ToList().FindIndex(m => (string?)m!["id"] == Herboriste);
        string p = $"$.data.merchants[{index}]";
        JsonObject m = Merchant(root, Herboriste);
        m["offers"]![1]!["fresh"] = true;
        m["soldOut"] = "19:00";
        m["bakes"] = JsonNode.Parse("[{\"at\":\"06:00\",\"percent\":60},{\"at\":\"06:00\",\"percent\":40}]");
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == p + ".bakes[1].at" && e.Code == ValidationCode.InvalidValue);
        m["bakes"] = JsonNode.Parse("[{\"at\":\"06:00\",\"percent\":60},{\"at\":\"11:00\",\"percent\":30}]");
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == p + ".bakes" && e.Code == ValidationCode.InvalidValue);   // 90 %
        m["bakes"] = JsonNode.Parse("[{\"at\":\"06:00\",\"percent\":60},{\"at\":\"11:00\",\"percent\":40}]");
        m["soldOut"] = "10:00";
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == p + ".soldOut" && e.Code == ValidationCode.InvalidValue);
        m["soldOut"] = "19:00";
        Assert.True(ReadMerchants(root).IsValid, ReadMerchants(root).Report.ToString());
        // Ouverte seulement la nuit, mais fermée la nuit : jamais ouverte.
        m["shop"]!["open"] = "23:00";
        m["shop"]!["close"] = "04:00";
        m["closed"] = JsonNode.Parse("[\"night\"]");
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == p + ".shop" && e.Code == ValidationCode.InvalidValue);
        // Quelqu'un à la porte, mais rien à y vendre.
        JsonObject empty = WithHerbShop();
        Merchant(empty, Herboriste)["offers"]![0]!.AsObject().Remove("door");
        Merchant(empty, Herboriste)["offers"]![1]!.AsObject().Remove("door");
        Assert.Contains(Errors(ReadMerchants(empty)), e => e.Path == p + ".shop.door" && e.Code == ValidationCode.InvalidValue);
        // Une offre à la porte sans personne à la porte.
        JsonObject nobody = WithHerbShop();
        Merchant(nobody, Herboriste)["shop"]!.AsObject().Remove("door");
        Assert.Contains(Errors(ReadMerchants(nobody)), e => e.Path == p + ".offers[0].door" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Un_objet_utile_a_l_histoire_se_vend_toujours_a_la_porte()
    {
        // Dans l'histoire, une quête demande de tenir ou de livrer des objets : vendus dans une boutique à horaires, ils se vendent à la porte.
        StoryBook story = Book(Zone());
        var needed = story.Quests.SelectMany(q => q.Steps).Select(s => s.Objective)
            .Where(o => o.Kind == ObjectiveKind.Deliver || o.Kind == ObjectiveKind.Collect).Select(o => o.Id).ToHashSet();
        JsonObject root = WithHerbShop();
        JsonObject m = Merchant(root, Herboriste);
        int index = root["data"]!["merchants"]!.AsArray().ToList().FindIndex(x => (string?)x!["id"] == Herboriste);
        JsonArray offers = m["offers"]!.AsArray();
        int k = offers.ToList().FindIndex(o => needed.Contains((string)o!["item"]!));
        if (k < 0)
        {
            // L'herboriste ne vend rien d'utile à une quête : on lui en fait vendre un.
            string item = needed.First(i => ShippedMerchants.Values.ContainsKey(i));
            offers.Add(JsonNode.Parse($"{{\"item\":\"{item}\",\"price\":{ShippedMerchants.Values[item] * 2},\"always\":true}}"));
            k = offers.Count - 1;
        }
        offers[k]!.AsObject().Remove("door");
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == $"$.data.merchants[{index}].offers[{k}].door" && e.Code == ValidationCode.InvalidValue);
    }

    // --- Dans le monde ------------------------------------------------------------------------------------------------

    [Fact]
    public void Horaires_jour_de_repos_et_patron_a_la_porte()
    {
        MerchantBook book = Valid(WithHerbShop());
        // Jour 1 (mardi), 9 h : ouverte, tout le stock.
        WorldSession w = Open(-65.2, -6.4, Hero(10_000), 1, 9 * 60, book);
        Assert.Equal(ShopState.Open, w.ShopStateOf(Herboriste));
        Assert.Equal(TradeRefusal.None, w.CanTrade(Herboriste));
        Assert.True(w.Stock(Herboriste).Count > 1);
        Assert.Null(w.NextOpening(Herboriste));
        // 17 h : fermée, le patron répond à la porte : seulement les potions et l'herbe, et il n'achète rien.
        WorldSession late = Open(-65.2, -6.4, Hero(10_000, new ItemStack(Herbe, 3)), 1, 17 * 60, book);
        Assert.Equal(ShopState.AtDoor, late.ShopStateOf(Herboriste));
        Assert.Equal(TradeRefusal.None, late.CanTrade(Herboriste));
        Assert.Equal(new[] { Potion, Herbe }, late.Stock(Herboriste).Select(o => o.Item));
        Assert.Equal(TradeRefusal.NotForSale, late.Buy(Herboriste, Faucille));
        string before = Snapshot(late);
        Assert.Equal(TradeRefusal.Closed, late.Sell(Herboriste, Herbe));
        Assert.Equal(before, Snapshot(late));
        Assert.Equal(TradeRefusal.None, late.Buy(Herboriste, Potion));
        Assert.Equal((2L, 480), late.NextOpening(Herboriste));
        // Dimanche (jour 6) : jour de repos, à la porte aussi ; réouverture lundi à 8 h.
        WorldSession sunday = Open(-65.2, -6.4, Hero(10_000), 6, 10 * 60, book);
        Assert.Equal(ShopState.AtDoor, sunday.ShopStateOf(Herboriste));
        Assert.Equal((7L, 480), sunday.NextOpening(Herboriste));
        // Sans personne à la porte : fermée pour de bon (jour de repos, ou hors des heures).
        JsonObject nobody = WithHerbShop();
        Merchant(nobody, Herboriste)["shop"]!.AsObject().Remove("door");
        // Sans les potions ni l'herbe de l'Aube (utiles à des quêtes, elles demanderaient la porte).
        Merchant(nobody, Herboriste)["offers"]!.AsArray().RemoveAt(1);
        Merchant(nobody, Herboriste)["offers"]!.AsArray().RemoveAt(0);
        MerchantBook closedBook = Valid(nobody);
        Assert.Equal(ShopState.RestDay, Open(-65.2, -6.4, Hero(), 6, 10 * 60, closedBook).ShopStateOf(Herboriste));
        WorldSession shut = Open(-65.2, -6.4, Hero(), 1, 17 * 60, closedBook);
        Assert.Equal(ShopState.ClosedHours, shut.ShopStateOf(Herboriste));
        Assert.Equal(TradeRefusal.Closed, shut.CanTrade(Herboriste));
        Assert.Equal(TradeRefusal.Closed, shut.Buy(Herboriste, Faucille));
    }
}
