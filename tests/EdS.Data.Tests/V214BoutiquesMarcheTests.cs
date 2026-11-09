using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Simulation.Randomness;
using EdS.World;
using EdS.Runtime;
using EdS.Domain.Space;
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
        // Relecture de la PR #135 : à la porte, sans limite de stock (un stock fini peut s'épuiser), pour rester toujours trouvables.
        m["offers"]![0]!.AsObject().Remove("stock");
        m["offers"]![1]!.AsObject().Remove("stock");
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
        m["offers"]![2]!["fresh"] = true;   // le remède (stock fini) : l'herbe, à la porte, reste sans limite
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
        // Sans les potions ni l'herbe de l'Aube (utiles à des quêtes, elles demanderaient la porte), ni l'épicerie à horaires qui en vend aussi.
        Merchant(nobody, "merchant.epicier").Remove("shop");
        Merchant(nobody, Herboriste)["offers"]!.AsArray().RemoveAt(1);
        Merchant(nobody, Herboriste)["offers"]!.AsArray().RemoveAt(0);
        MerchantBook closedBook = Valid(nobody);
        Assert.Equal(ShopState.RestDay, Open(-65.2, -6.4, Hero(), 6, 10 * 60, closedBook).ShopStateOf(Herboriste));
        WorldSession shut = Open(-65.2, -6.4, Hero(), 1, 17 * 60, closedBook);
        Assert.Equal(ShopState.ClosedHours, shut.ShopStateOf(Herboriste));
        Assert.Equal(TradeRefusal.Closed, shut.CanTrade(Herboriste));
        Assert.Equal(TradeRefusal.Closed, shut.Buy(Herboriste, Faucille));
    }

    // --- Contenu livré ------------------------------------------------------------------------------------------------

    private const string Boulangerie = "merchant.boulangerie", Pain = "item.pain_de_campagne", Croissant = "item.croissant_au_beurre", Epices = "item.pain_d_epices";
    private const string Fromagere = "merchant.fromagere", Fromage = "item.fromage_des_hautes_pierres";

    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    /// <summary>Au comptoir de la boulangerie (Bastien, le commis), le jour <paramref name="day"/> à <paramref name="minute"/>.</summary>
    private static WorldSession AtBakery(long day, int minute, SavedCharacter? hero = null) =>
        V231TestData.Inside("zone.boulangerie", TickAt(day, minute) - 1, -0.25, -0.2, Story.Value, hero ?? Hero(10_000));

    private static WorldSession AtMarket(long day, int minute, SavedCharacter? hero = null) => Open(-50.5, -3.0, hero ?? Hero(10_000), day, minute);

    private static SavedCharacter Liked(int reputation, long money = 10_000) =>
        Hero(money).WithStory(new SavedStory(reputation: new Dictionary<string, int> { ["villagers"] = reputation }));

    [Fact]
    public void Boutiques_et_marche_livres()
    {
        MerchantBook book = ShippedMerchants;
        Assert.Equal((6, "0.11.0-provisoire"), (book.SchemaVersion, book.ContentVersion));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "merchants.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(MerchantsPath)).RootElement));
        // Les vraies boutiques : genre, maison du bourg, intérieur, horaires et jour de repos (0 : lundi … 6 : dimanche).
        var shops = book.Merchants.Where(m => m.Shop != null).ToDictionary(m => m.Id, m => m.Shop!);
        Assert.Equal(new[] { "merchant.armurier", "merchant.herboriste", "merchant.epicier", Boulangerie, "merchant.auberge", "merchant.mercerie" }, shops.Keys);
        Assert.Equal(("bakery", "maison2", "zone.boulangerie", 330, 1170, DoorService.Clerk), (shops[Boulangerie].Kind, shops[Boulangerie].Building, shops[Boulangerie].Zone,
            shops[Boulangerie].Open, shops[Boulangerie].Close, shops[Boulangerie].Door));
        Assert.Equal(new[] { 0 }, shops[Boulangerie].Rest);
        // Décisions de Damien du 30 octobre 2026 gardées : Garin et Bertille ouvrent de 5 h à 22 h, Sauge jour et nuit (sans jour de repos).
        Assert.Equal((300, 1320), (shops["merchant.armurier"].Open, shops["merchant.armurier"].Close));
        Assert.Equal((300, 1320), (shops["merchant.epicier"].Open, shops["merchant.epicier"].Close));
        Assert.True(shops["merchant.herboriste"].AllDay);
        Assert.Empty(shops["merchant.herboriste"].Rest);
        // L'auberge ferme après minuit (1 h).
        Assert.Equal((360, 60), (shops["merchant.auberge"].Open, shops["merchant.auberge"].Close));
        // Les fournées de Firmin : 5 h 30 (60 %), 11 h (25 %), 16 h 30 (15 %) ; rupture à 19 h.
        Merchant bakery = book.Merchant(Boulangerie);
        Assert.Equal(new[] { (330, 60), (660, 25), (990, 15) }, bakery.Bakes.Select(b => (b.At, b.Percent)));
        Assert.Equal(1140, bakery.SoldOut);
        Assert.All(bakery.Offers.Where(o => o.Item != "item.pain_d_epices"), o => Assert.True(o.Fresh, o.Item));
        // Le grand jour de marché : mercredi et samedi, de 7 h à 17 h, installation dès 6 h, démontage jusqu'à 19 h.
        MarketDay market = book.Market!;
        Assert.Equal(new[] { "festival.marche", "festival.marche_mercredi" }, market.Festivals);
        Assert.Equal((360, 420, 1020, 1140, 14), (market.Setup, market.Open, market.Close, market.Teardown, market.Crowd));
        Assert.Equal(new[] { 5, 2 }, market.Festivals.Select(f => Story.Value.Festivals.Single(x => x.Id == f).Weekday!.Value));
        Assert.Equal(8, market.Stalls.Count);
        Assert.Equal(new[] { Fromagere, "merchant.marchand_epices", "merchant.colporteur" }, market.Stalls.Where(x => x.Merchant != null).Select(x => x.Merchant));
        Assert.Equal(("liked", 85, 35, 20), (market.Haggle!.Standing, market.Haggle.Percent, market.Haggle.Chance, market.Haggle.Bonus));
        Assert.All(market.Cries, c => Assert.True(Texts.Has(c), c));
        // Chaque boutique avec un intérieur : la zone existe, son comptoir est dans la pièce, hors des meubles.
        foreach ((string id, Shop shop) in shops.Where(x => x.Value.Zone != null))
        {
            ZoneDefinition inside = V231TestData.Interior(shop.Zone!, 0, -1.4);
            Assert.True(inside.Indoor, id);
            double cx = shop.Counter.X.Raw / 10000.0, cz = shop.Counter.Z.Raw / 10000.0;
            var space = inside.Setup.Space!;
            Assert.InRange(cx, space.Min.X.Raw / 10000.0, space.Max.X.Raw / 10000.0);
            Assert.InRange(cz, space.Min.Z.Raw / 10000.0, space.Max.Z.Raw / 10000.0);
            foreach (var o in space.Obstacles)
                Assert.False(cx > o.Points.Min(p => p.X.Raw) / 10000.0 && cx < o.Points.Max(p => p.X.Raw) / 10000.0
                    && cz > o.Points.Min(p => p.Z.Raw) / 10000.0 && cz < o.Points.Max(p => p.Z.Raw) / 10000.0, id + " : " + o.Id);
        }
    }

    [Fact]
    public void Le_fournil_de_Firmin_au_fil_de_la_journee()
    {
        // Mardi (jour 1), 5 h : pas encore ouvert ; Bastien répond à la porte, mais rien n'est encore cuit.
        WorldSession early = AtBakery(1, 5 * 60);
        Assert.Equal(ShopState.AtDoor, early.ShopStateOf(Boulangerie));
        Assert.Equal(new[] { Pain, Epices }, early.Stock(Boulangerie).Select(o => o.Item));   // à la porte : le pain et ce qui se garde
        Assert.Equal(0, early.Left(Boulangerie, Pain));
        Assert.Equal(TradeRefusal.OutOfStock, early.Buy(Boulangerie, Pain));
        Assert.Equal(330, early.NextBake(Boulangerie));
        // 6 h : ouvert, la première fournée est chaude ; on achète au comptoir.
        WorldSession morning = AtBakery(1, 6 * 60);
        Assert.Equal(ShopState.Open, morning.ShopStateOf(Boulangerie));
        Assert.Equal(TradeRefusal.None, morning.CanTrade(Boulangerie));
        Assert.True(morning.WarmBread(Boulangerie));
        int first = morning.Left(Boulangerie, Pain)!.Value;
        Assert.Equal(ShopRules.OnShelf(ShippedMerchants.Merchant(Boulangerie).Bakes, 1140, 40, 360), first);
        Assert.InRange(first, 20, 24);
        Assert.Equal(TradeRefusal.None, morning.Buy(Boulangerie, Pain, 3));
        Assert.Equal(first - 3, morning.Left(Boulangerie, Pain));
        // 11 h 05 : la deuxième fournée ; 15 h : plus chaude ; 19 h 15 : rupture du soir ; 20 h : fermé, rien à la porte.
        Assert.True(AtBakery(1, 11 * 60 + 5).WarmBread(Boulangerie));
        Assert.False(AtBakery(1, 15 * 60).WarmBread(Boulangerie));
        WorldSession evening = AtBakery(1, 19 * 60 + 15);
        Assert.Equal(0, evening.Left(Boulangerie, Croissant));
        Assert.Equal(TradeRefusal.OutOfStock, evening.Buy(Boulangerie, Croissant));
        Assert.Equal(ShopState.AtDoor, AtBakery(1, 20 * 60).ShopStateOf(Boulangerie));
        // Lundi (jour 7) : jour de repos ; Bastien répond à la porte ; réouverture mardi à 5 h 30. Relecture de la PR #135 : le four est
        // éteint, rien de frais (pas de pain qui « sort » à la porte), seulement le pain d'épices qui se garde.
        WorldSession monday = AtBakery(7, 10 * 60);
        Assert.Equal(ShopState.AtDoor, monday.ShopStateOf(Boulangerie));
        Assert.Equal(new[] { Pain, Epices }, monday.Stock(Boulangerie).Select(o => o.Item));
        Assert.Equal(0, monday.Left(Boulangerie, Pain));
        Assert.False(monday.WarmBread(Boulangerie));
        Assert.Null(monday.NextBake(Boulangerie));
        Assert.Equal(TradeRefusal.OutOfStock, monday.Buy(Boulangerie, Pain));
        Assert.Equal((8L, 330), monday.NextOpening(Boulangerie));
        Assert.Equal(TradeRefusal.None, monday.Buy(Boulangerie, Epices));
        string before = Snapshot(monday);
        Assert.Equal(TradeRefusal.NotForSale, monday.Buy(Boulangerie, Croissant));
        Assert.Equal(before, Snapshot(monday));
        // Le lendemain, tout est revenu (stock plein regagné chaque jour).
        Assert.Equal(first, AtBakery(8, 6 * 60, monday.Save().Character!).Left(Boulangerie, Pain));
        Assert.Equal(first, AtBakery(15, 6 * 60).Left(Boulangerie, Pain));   // le mardi d'après, après un autre lundi de repos : la fournée normale
    }

    [Fact]
    public void Garin_et_Bertille_ferment_le_dimanche()
    {
        foreach (string merchant in new[] { "merchant.armurier", "merchant.epicier" })
        {
            (double x, double z) = Near(merchant);
            WorldSession sunday = Open(x, z, Hero(1000), 6, 10 * 60);
            Assert.Equal(ShopState.RestDay, sunday.ShopStateOf(merchant));
            Assert.Equal(TradeRefusal.Closed, sunday.CanTrade(merchant));
            Assert.Equal((7L, 300), sunday.NextOpening(merchant));
            Assert.Equal(TradeRefusal.None, Open(x, z, Hero(1000), 5, 10 * 60).CanTrade(merchant));
        }
        // Sauge, ouverte jour et nuit, tous les jours.
        Assert.Equal(ShopState.Open, Open(-65.2, -6.4, Hero(1000), 6, 10 * 60).ShopStateOf(Herboriste));
    }

    [Fact]
    public void Le_grand_jour_de_marche_s_installe_puis_se_demonte()
    {
        Npc gisele = Story.Value.Npc("npc.fromagere");
        // Mercredi (jour 2) : installation à 6 h 30 ; la fromagère est là, mais ne vend pas encore.
        WorldSession setup = AtMarket(2, 6 * 60 + 30);
        Assert.Equal(MarketPhase.Setup, setup.MarketPhase);
        Assert.False(setup.NpcAway(gisele));
        Assert.Equal(TradeRefusal.NotFestival, setup.TradeStatus(Fromagere));
        Assert.Null(setup.MarketCry);
        // 9 h : le marché bat son plein, criées et étals ouverts ; 15 h (après la fête du matin) : encore ouvert.
        WorldSession open = AtMarket(2, 9 * 60);
        Assert.Equal(MarketPhase.Open, open.MarketPhase);
        Assert.Equal(TradeRefusal.None, open.CanTrade(Fromagere));
        Assert.NotNull(open.MarketCry);
        Assert.Equal(TradeRefusal.None, open.Buy(Fromagere, Fromage));
        Assert.Equal(TradeRefusal.None, AtMarket(2, 15 * 60).CanTrade(Fromagere));
        Assert.Null(AtMarket(2, 15 * 60).Festival);   // la fête (animation) est finie, le marché continue
        // 18 h : on démonte ; 20 h : la place est calme, la fromagère repartie.
        WorldSession teardown = AtMarket(2, 18 * 60);
        Assert.Equal(MarketPhase.Teardown, teardown.MarketPhase);
        Assert.False(teardown.NpcAway(gisele));
        Assert.Equal(TradeRefusal.NotFestival, teardown.CanTrade(Fromagere));
        WorldSession night = AtMarket(2, 20 * 60);
        Assert.Equal(MarketPhase.Calm, night.MarketPhase);
        Assert.True(night.NpcAway(gisele));
        // Jeudi : calme ; prochain marché samedi (jour 5), où l'on vend aussi l'après-midi.
        WorldSession thursday = AtMarket(3, 10 * 60);
        Assert.Equal(MarketPhase.Calm, thursday.MarketPhase);
        Assert.True(thursday.NpcAway(gisele));
        Assert.Equal(5L, thursday.NextMarketDay()!.Value.Day);
        Assert.Equal(TradeRefusal.None, AtMarket(5, 14 * 60).CanTrade(Fromagere));
        // Le jour de marché, l'épicerie et l'herboristerie baissent leurs potions (90 %), jamais sous la valeur.
        Assert.Equal(90, open.MarketPercent("merchant.herboriste", Potion));
        Assert.Equal(100, thursday.MarketPercent("merchant.herboriste", Potion));
    }

    [Fact]
    public void Marchander_selon_la_reputation_avec_un_tirage_fixe_du_jour()
    {
        long[] marketDays = Enumerable.Range(0, 400).Select(d => (long)d).Where(d => d % 7 == 2 || d % 7 == 5).ToArray();
        long lucky = marketDays.First(d => ShopRules.HaggleRoll(Fromagere, d) < 35);
        long hard = marketDays.First(d => ShopRules.HaggleRoll(Fromagere, d) >= 35 && ShopRules.HaggleRoll(Fromagere, d) < 55);
        // Neutre : on ne marchande pas ; rien ne change.
        WorldSession neutral = AtMarket(lucky, 9 * 60);
        Assert.Equal(0, neutral.HaggleChance(Fromagere));
        string before = Snapshot(neutral);
        Assert.Equal(HaggleRefusal.StandingTooLow, neutral.Haggle(Fromagere));
        Assert.Equal(before, Snapshot(neutral));
        // Apprécié (35 %) : accepté le jour chanceux, 85 % du prix pour la journée ; une seule fois.
        WorldSession liked = AtMarket(lucky, 9 * 60, Liked(150));
        long full = liked.Stock(Fromagere).Single(o => o.Item == Fromage).Price;
        Assert.Equal(35, liked.HaggleChance(Fromagere));
        Assert.Equal(HaggleRefusal.None, liked.Haggle(Fromagere));
        Assert.Equal(85, liked.HagglePercent(Fromagere));
        long haggled = Math.Max(ShippedMerchants.Values[Fromage], WorldSession.ReputationPrice(WorldSession.ReputationPrice(38, 85), liked.PricePercentOf(Fromagere)));
        Assert.Equal(haggled, liked.Stock(Fromagere).Single(o => o.Item == Fromage).Price);   // marchandé, puis la réputation
        Assert.True(haggled < full);
        Assert.Equal(HaggleRefusal.AlreadyDone, liked.Haggle(Fromagere));
        // Le jour difficile : refusé à « apprécié », accepté à « honoré » (55 %) ; redemander ne change rien.
        WorldSession refused = AtMarket(hard, 9 * 60, Liked(150));
        Assert.Equal(HaggleRefusal.Refused, refused.Haggle(Fromagere));
        Assert.Equal(HaggleRefusal.Refused, refused.Haggle(Fromagere));
        Assert.Equal(100, refused.HagglePercent(Fromagere));
        Assert.Equal(HaggleRefusal.None, AtMarket(hard, 9 * 60, Liked(350)).Haggle(Fromagere));
        // Hors du marché, et pour une boutique : pas de marchandage.
        Assert.Equal(HaggleRefusal.NoMarket, AtMarket(lucky, 20 * 60, Liked(700)).Haggle(Fromagere));
        Assert.Equal(HaggleRefusal.NotAStall, Open(-65.2, -6.4, Liked(700), lucky, 9 * 60).Haggle("merchant.herboriste"));
    }

    [Fact]
    public void Proprietes_de_la_place_sur_deux_semaines()
    {
        // Chaque heure de deux semaines : un marché seulement le mercredi et le samedi, dans l'ordre installation, marché, démontage ; ses
        // marchands de passage vendent seulement pendant le marché ; une boutique à la porte ne vend que ses offres « door ».
        var seen = new Dictionary<long, List<MarketPhase>>();
        for (long day = 1; day < 15; day++)
            for (int hour = 0; hour < 24; hour += 1)
            {
                WorldSession w = AtMarket(day, hour * 60 + 30);
                MarketPhase phase = w.MarketPhase;
                if (day % 7 != 2 && day % 7 != 5) Assert.Equal(MarketPhase.Calm, phase);
                if (!seen.TryGetValue(day, out List<MarketPhase>? list)) seen[day] = list = new List<MarketPhase>();
                if (list.Count == 0 || list[^1] != phase) list.Add(phase);
                Assert.Equal(phase == MarketPhase.Open, w.TradeStatus(Fromagere) == TradeRefusal.None);
            }
        foreach (long day in new long[] { 2, 5, 9, 12 })
            Assert.Equal(new[] { MarketPhase.Calm, MarketPhase.Setup, MarketPhase.Open, MarketPhase.Teardown, MarketPhase.Calm }, seen[day]);
    }

    // --- Affichage (AdventureSession) ---------------------------------------------------------------------------------

    private static AdventureSession Adventure(long day, int minute)
    {
        string save = Path.Combine(Path.GetTempPath(), "eds-v214-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(day, minute) - 1, 0, Array.Empty<string>(), Hero(1000)));
        try
        {
            return V11TestData.Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
    }

    [Fact]
    public void Affichage_des_enseignes_de_la_fumee_et_du_marche()
    {
        AdventureSession a = Adventure(2, 9 * 60);   // mercredi, 9 h : jour de marché
        Assert.Equal(MarketPhase.Open, a.MarketPhaseNow);
        IReadOnlyList<MarketStallView> stalls = a.MarketStalls();
        Assert.Equal(8, stalls.Count);
        Assert.All(stalls, x => Assert.InRange(x.Face, -180.0, 360.0));
        IReadOnlyList<EdS.Runtime.StyleVillager> crowd = a.MarketCrowd();
        Assert.InRange(crowd.Count, 1, 14);
        Assert.Equal(crowd.Select(v => (v.Id, v.X, v.Z)), a.MarketCrowd().Select(v => (v.Id, v.X, v.Z)));   // même foule
        Assert.True(a.MarketCrowdShown);
        Assert.NotNull(a.MarketLine);
        // Les enseignes : cinq maisons du bourg (l'armurerie reste un étal couvert), à 2 m au plus de leur porte.
        IReadOnlyList<WorldMarker> signs = a.ShopSigns();
        Assert.Equal(new[] { "enseigne/merchant.herboriste", "enseigne/merchant.epicier", "enseigne/merchant.boulangerie", "enseigne/merchant.auberge",
            "enseigne/merchant.mercerie" }, signs.Select(x => x.Id));
        Assert.Contains(signs, x => x.Name == "Boulangerie de Firmin · ouverte");
        foreach (WorldMarker sign in signs)
            Assert.Contains(a.Doors(), d => Math.Sqrt((d.X - sign.X) * (d.X - sign.X) + (d.Z - sign.Z) * (d.Z - sign.Z)) < 2.0);
        // La cheminée du fournil fume (le four chauffe de 2 h à 17 h 30), pas à 20 h, ni le lundi (jour de repos).
        Assert.True(Assert.Single(a.Chimneys()).Lit);
        Assert.False(Assert.Single(Adventure(2, 20 * 60).Chimneys()).Lit);
        Assert.False(Assert.Single(Adventure(7, 9 * 60).Chimneys()).Lit);
        // Jeudi : la place est calme.
        AdventureSession calm = Adventure(3, 10 * 60);
        Assert.Equal(MarketPhase.Calm, calm.MarketPhaseNow);
        Assert.False(calm.MarketCrowdShown);
        Assert.Null(calm.MarketLine);
        Assert.StartsWith("Prochain jour de marché : samedi", calm.MarketNotice, StringComparison.Ordinal);
        Assert.Equal("Boulangerie de Firmin · ouverte de 5 h 30 à 19 h 30 · fermée le lundi", calm.ShopHoursText(Boulangerie));
        Assert.Equal("Herboristerie de Sauge · ouverte jour et nuit", calm.ShopHoursText(Herboriste));
        Assert.Equal("Boutique fermée : jour de repos. Réouverture lundi à 5 h.", Adventure(6, 10 * 60).ClosedText("merchant.epicier"));
    }

    // --- Relecture indépendante de la PR #135 et réponses de Damien du 9 novembre 2026 (17 h 58) ----------------------

    [Fact]
    public void Une_offre_fraiche_ou_a_stock_fini_a_la_porte_ne_suffit_pas_a_une_quete()
    {
        // L'herbe de l'Aube (utile à une quête) vendue seulement à la porte d'une boutique, mais fraîche ou à stock fini : refusée.
        StoryBook story = Book(Zone());
        JsonObject root = WithHerbShop();
        V214TestData.WithoutShops(root);   // seule l'herboristerie a une boutique
        root["schemaVersion"] = 6;
        JsonObject herb = Merchant(root, Herboriste);
        herb["shop"] = JsonNode.Parse("{\"kind\":\"herbalist\",\"sign\":\"text.merchant.herboriste.greeting\",\"open\":\"08:00\",\"close\":\"15:00\",\"door\":\"patron\"}");
        foreach (JsonNode? m in root["data"]!["merchants"]!.AsArray())
            if ((string?)m!["id"] != Herboriste)
                foreach (JsonNode? o in m!["offers"]!.AsArray().Where(o => (string?)o!["item"] == Herbe || (string?)o!["item"] == Potion).ToList()) m["offers"]!.AsArray().Remove(o);
        int index = root["data"]!["merchants"]!.AsArray().ToList().FindIndex(m => (string?)m!["id"] == Herboriste);
        foreach (int k in new[] { 0, 1 })
        {
            herb["offers"]![k]!["door"] = true;
            herb["offers"]![k]!.AsObject().Remove("stock");
        }
        Assert.True(ReadMerchants(root).IsValid, ReadMerchants(root).Report.ToString());   // sans limite, pas fraîche : toujours trouvable
        herb["offers"]![1]!["stock"] = 60;   // stock fini : peut s'épuiser
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == $"$.data.merchants[{index}].offers[1].door" && e.Code == ValidationCode.InvalidValue);
        herb["offers"]![1]!["fresh"] = true;   // fraîche (avec des fournées) : à zéro avant la première et après la rupture
        herb["bakes"] = JsonNode.Parse("[{\"at\":\"06:00\",\"percent\":100}]");
        herb["soldOut"] = "19:00";
        Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == $"$.data.merchants[{index}].offers[1].door" && e.Code == ValidationCode.InvalidValue);
        _ = story;
    }

    [Fact]
    public void Apres_minuit_le_jour_de_repos_de_la_veille_compte()
    {
        // L'auberge (6 h à 1 h) fermée le lundi, sans personne à la porte : le mardi à 0 h 30, c'est encore le lundi soir : jour de repos.
        JsonObject root = MerchantsJson();
        JsonObject inn = Merchant(root, "merchant.auberge");
        inn["shop"]!["rest"] = new JsonArray(0);
        inn["shop"]!.AsObject().Remove("door");
        foreach (JsonNode? o in inn["offers"]!.AsArray()) o!.AsObject().Remove("door");
        MerchantBook book = Valid(root);
        WorldSession tuesday = Open(-65.2, -6.4, Hero(), 1, 30, book);
        Assert.Equal(ShopState.RestDay, tuesday.ShopStateOf("merchant.auberge"));
        Assert.Equal((1L, 360), tuesday.NextOpening("merchant.auberge"));
        // Le dimanche soir à 0 h 30 (lundi), l'auberge du dimanche est encore ouverte.
        Assert.Equal(ShopState.Open, Open(-65.2, -6.4, Hero(), 7, 30, book).ShopStateOf("merchant.auberge"));
        // Le mardi à 2 h (après la fermeture de 1 h) : simplement fermée.
        Assert.Equal(ShopState.ClosedHours, Open(-65.2, -6.4, Hero(), 1, 120, book).ShopStateOf("merchant.auberge"));
    }

    [Fact]
    public void Chaque_boutique_a_ses_employes_nommes_avec_leurs_horaires()
    {
        StoryBook story = Story.Value;
        var staff = new Dictionary<string, (string Zone, string Work, int From, int To)>
        {
            ["npc.commis"] = ("zone.boulangerie", "comptoir", 300, 1185),
            ["npc.commis_epicerie"] = ("zone.epicerie", "comptoir", 300, 1320),
            ["npc.commis_herboristerie"] = ("zone.herboristerie", "comptoir", 420, 1260),
            ["npc.commis_mercerie"] = ("zone.mercerie", "couture", 480, 1110),
        };
        foreach ((string id, (string zone, string work, int from, int to)) in staff)
        {
            Npc npc = story.Npc(id);
            Assert.Equal(zone, npc.Zone);
            Assert.True(Texts.Has(npc.Name));
            Assert.Equal(work, npc.SlotAt(from)!.Activity);
            Assert.Equal(work, npc.SlotAt(to - 1)!.Activity);
            Assert.Equal("rangement", npc.SlotAt(to)!.Activity);
            Assert.Equal("rangement", npc.SlotAt(from - 1)!.Activity);
        }
        // Plus de commis de décor anonyme : dans chaque boutique, les corps sont ceux des PNJ qui parlent.
        foreach (string zone in new[] { "zone.boulangerie", "zone.epicerie", "zone.herboristerie", "zone.mercerie" })
        {
            InteriorLayout layout = InteriorRules.Layout(InterieursTests.MapOf(V231TestData.Interior(zone, 0, -1.4)));
            Assert.DoesNotContain(layout.Villagers, v => v.Id == "commis");
        }
    }
}
