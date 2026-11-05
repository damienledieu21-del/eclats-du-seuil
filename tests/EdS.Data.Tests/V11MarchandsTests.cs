using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.Simulation.Randomness;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.1 : les marchands en données (<c>merchants.json</c>, schéma 1), leur stock tiré chaque jour du monde de façon déterministe,
/// l'achat, la vente et le rachat atomiques avec leurs motifs de refus, et la boutique dans l'aventure (F près du marchand).
/// </summary>
[Trait("Category", "World")]
public class V11MarchandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v11_marchands_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static JsonObject MerchantJson(JsonObject root, int index) => root["data"]!["merchants"]![index]!.AsObject();

    // --- Données -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Marchands_livres_au_bourg()
    {
        MerchantsLoadResult loaded = MerchantJsonReader.ReadFile(MerchantsPath, Texts, Book(Zone()), ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        MerchantBook book = loaded.Book!;
        Assert.Equal(MerchantValidator.CurrentSchemaVersion, book.SchemaVersion);
        // Sous-lot V1.7 : et la réserve de la guilde, tenue par l'intendant dans la Maison de la Guilde.
        Assert.Equal(new[] { "merchant.armurier", "merchant.herboriste", "merchant.epicier", "merchant.guilde", "merchant.renart" }, book.Merchants.Select(m => m.Id));   // V1.9.3 : l'étal de Renart
        Assert.Equal(new[] { "npc.armurier", "npc.herboriste", "npc.epiciere", "npc.intendant_guilde", "npc.renart" }, book.Merchants.Select(m => m.Npc));
        // Les trois du dehors au bourg d'Aube, chacun à un PNJ de l'histoire ; celui de la guilde dans sa pièce.
        ZoneDefinition zone = Zone();
        StoryBook story = Book(zone);
        Assert.All(book.Merchants.Where(m => story.Npc(m.Npc).Zone is null), m => Assert.Contains(zone.PlacesAt(story.Npc(m.Npc).Position), p => p.Id == "bourg"));
        Assert.Equal("zone.guilde", story.Npc(book.Merchant("merchant.guilde").Npc).Zone);
        // L'herboriste vend toujours des potions de soin.
        Assert.Contains(book.Merchant("merchant.herboriste").Offers, o => o.Item == "item.potion_de_soin" && o.Always);
        // Contrat de l'économie : rien sous sa valeur de base, revente au plus la valeur : acheter puis revendre ne rapporte jamais.
        var market = new Market(book);
        foreach (Merchant m in book.Merchants)
            foreach (MerchantOffer o in m.Offers)
            {
                Assert.True(o.Price >= book.Values[o.Item], $"{m.Id} {o.Item}");
                foreach (Merchant buyer in book.Merchants) Assert.True(market.UnitPrice(buyer.Id, o.Item) <= o.Price, $"{buyer.Id} {o.Item}");
            }
        // Tous les objets ont une valeur, sauf la ferrure (objet de quête) et l'Éclat du Seuil du personnage (sous-lot V1.2) : ils ne se vendent pas.
        Assert.Equal(new[] { "item.eclat_du_seuil_endormi", "item.ferrure", "item.miche_du_matin", "item.bourse_d_eclats", "item.bourse_de_renart", "item.corde_de_cloche", "item.huile_de_lampe", "item.pierre_benie", "item.sac_d_eclats", "item.lettre_mouillee", "item.sac_de_pommes" }, ItemsCatalog.Items.Select(i => i.Id).Where(i => !book.Values.ContainsKey(i)));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "merchants.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(MerchantsPath)).RootElement));
        // Les textes des marchands existent (noms, accueils, motifs de refus).
        Assert.All(book.Merchants, m => Assert.True(Texts.Has(m.Greeting), m.Greeting));
        foreach (TradeRefusal r in Enum.GetValues<TradeRefusal>()) Assert.True(Texts.Has("text.ui.trade." + r.ToString().ToLowerInvariant()), r.ToString());
        foreach (TravelRefusal r in Enum.GetValues<TravelRefusal>()) Assert.True(Texts.Has("text.ui.travel." + r.ToString().ToLowerInvariant()), r.ToString());
    }

    [Theory]
    [InlineData("id", "\"armurier\"", "$.data.merchants[0].id", ValidationCode.InvalidValue)]
    [InlineData("id", "\"merchant.armurier\"", "$.data.merchants[1].id", ValidationCode.DuplicateId)]
    [InlineData("npc", "\"npc.inconnu\"", "$.data.merchants[0].npc", ValidationCode.UnknownReference)]
    [InlineData("npc", "\"npc.armurier\"", "$.data.merchants[1].npc", ValidationCode.DuplicateId)]
    [InlineData("greeting", "\"text.absent\"", "$.data.merchants[0].greeting", ValidationCode.UnknownReference)]
    [InlineData("sellPercent", "0", "$.data.merchants[0].sellPercent", ValidationCode.OutOfRange)]
    [InlineData("sellPercent", "101", "$.data.merchants[0].sellPercent", ValidationCode.OutOfRange)]
    [InlineData("sellPercent", "25.5", "$.data.merchants[0].sellPercent", ValidationCode.InvalidNumber)]
    [InlineData("closed", "[\"midi\"]", "$.data.merchants[0].closed[0]", ValidationCode.UnknownReference)]
    [InlineData("closed", "[\"night\", \"night\"]", "$.data.merchants[0].closed[1]", ValidationCode.DuplicateId)]
    [InlineData("closed", "[\"night\", \"dawn\", \"day\", \"dusk\"]", "$.data.merchants[0].closed", ValidationCode.InvalidValue)]
    [InlineData("daily", "12", "$.data.merchants[0].daily", ValidationCode.OutOfRange)]   // V1.2 : onze offres tournantes chez Garin
    [InlineData("daily", "-1", "$.data.merchants[0].daily", ValidationCode.OutOfRange)]
    [InlineData("offers", "[]", "$.data.merchants[0].offers", ValidationCode.OutOfRange)]
    [InlineData("offers", "[{\"item\": \"item.potion_de_soin\", \"price\": 60}]", "$.data.merchants[0].offers[0].always", ValidationCode.MissingField)]
    [InlineData("offers", "[{\"item\": \"item.potion_de_soin\", \"price\": 39, \"always\": true}]", "$.data.merchants[0].offers[0].price", ValidationCode.InvalidValue)]
    [InlineData("offers", "[{\"item\": \"item.potion_de_soin\", \"price\": 0, \"always\": true}]", "$.data.merchants[0].offers[0].price", ValidationCode.OutOfRange)]
    [InlineData("offers", "[{\"item\": \"item.ferrure\", \"price\": 60, \"always\": true}]", "$.data.merchants[0].offers[0].item", ValidationCode.UnknownReference)]
    [InlineData("offers", "[{\"item\": \"item.epee\", \"price\": 60, \"always\": true}]", "$.data.merchants[0].offers[0].item", ValidationCode.UnknownReference)]
    [InlineData("offers", "[{\"item\": \"item.potion_de_soin\", \"price\": 60, \"always\": true}, {\"item\": \"item.potion_de_soin\", \"price\": 70, \"always\": true}]",
        "$.data.merchants[0].offers[1].item", ValidationCode.DuplicateId)]
    [InlineData("offers", "[{\"item\": \"item.potion_de_soin\", \"price\": 60, \"always\": false}]", "$.data.merchants[0].daily", ValidationCode.OutOfRange)]
    [InlineData("extra", "1", "$.data.merchants[0].extra", ValidationCode.UnknownField)]
    public void Marchand_invalide_refuse_avec_son_chemin(string field, string json, string path, ValidationCode code)
    {
        JsonObject root = MerchantsJson();
        MerchantJson(root, path.Contains("merchants[1]") ? 1 : 0)[field] = JsonNode.Parse(json);
        MerchantsLoadResult result = ReadMerchants(root);
        Assert.False(result.IsValid);
        Assert.Contains(Errors(result), e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Valeurs_et_en_tete_invalides_refusees()
    {
        JsonObject Values(Action<JsonArray> change)
        {
            JsonObject root = MerchantsJson();
            change(root["data"]!["values"]!.AsArray());
            return root;
        }
        Assert.Contains(Errors(ReadMerchants(Values(v => v[0]!["value"] = 0))), e => e.Path == "$.data.values[0].value" && e.Code == ValidationCode.OutOfRange);
        Assert.Contains(Errors(ReadMerchants(Values(v => v[0]!["value"] = 100_000_001))), e => e.Path == "$.data.values[0].value" && e.Code == ValidationCode.OutOfRange);
        Assert.Contains(Errors(ReadMerchants(Values(v => v[1]!["item"] = "item.epee_du_milicien"))), e => e.Path == "$.data.values[1].item" && e.Code == ValidationCode.DuplicateId);
        Assert.Contains(Errors(ReadMerchants(Values(v => v[0]!["item"] = "item.inconnu"))), e => e.Path == "$.data.values[0].item" && e.Code == ValidationCode.UnknownReference);
        // Une offre dont l'objet a perdu sa valeur est refusée (on ne vend pas ce qui ne se revend pas).
        Assert.Contains(Errors(ReadMerchants(Values(v => v.RemoveAt(0)))), e => e.Path == "$.data.merchants[0].offers[0].item" && e.Code == ValidationCode.UnknownReference);
        JsonObject none = MerchantsJson();
        none["data"]!["merchants"] = new JsonArray();
        Assert.Contains(Errors(ReadMerchants(none)), e => e.Path == "$.data.merchants" && e.Code == ValidationCode.OutOfRange);
        JsonObject many = MerchantsJson();
        JsonArray offers = MerchantJson(many, 0)["offers"]!.AsArray();
        while (offers.Count <= MerchantValidator.MaxOffers) offers.Add(offers[0]!.DeepClone());
        Assert.Contains(Errors(ReadMerchants(many)), e => e.Path == "$.data.merchants[0].offers" && e.Code == ValidationCode.LimitExceeded);
        JsonObject future = MerchantsJson();
        future["schemaVersion"] = MerchantValidator.CurrentSchemaVersion + 1;   // sous-lot V1.7 : version 2 courante (rang de la boutique)
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(Errors(ReadMerchants(future))).Path, Errors(ReadMerchants(future))[0].Code));
        JsonObject kind = MerchantsJson();
        kind["kind"] = "story";
        Assert.Contains(Errors(ReadMerchants(kind)), e => e.Path == "$.kind");
        JsonObject missing = MerchantsJson();
        missing["data"]!.AsObject().Remove("values");
        Assert.Contains(Errors(ReadMerchants(missing)), e => e.Path == "$.data.values" && e.Code == ValidationCode.MissingField);
    }

    // --- Stock du jour -------------------------------------------------------------------------------------------------

    [Fact]
    public void Stock_du_jour_deterministe()
    {
        var a = new Market(ShippedMerchants);
        var b = new Market(ShippedMerchants);
        foreach (Merchant m in ShippedMerchants.Merchants)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stocks = new HashSet<string>(StringComparer.Ordinal);
            for (long day = 0; day < 200; day++)
            {
                IReadOnlyList<MerchantOffer> stock = a.Stock(m.Id, day);
                // Même jour, même stock (autre instance, autre ordre d'appel) ; l'ordre est celui du fichier.
                b.Stock(m.Id, 199 - day);
                Assert.Equal(stock.Select(o => o.Item), b.Stock(m.Id, day).Select(o => o.Item));
                Assert.Equal(m.Offers.Where(o => stock.Contains(o)).Select(o => o.Item), stock.Select(o => o.Item));
                Assert.All(m.Offers.Where(o => o.Always), o => Assert.Contains(o, stock));
                Assert.Equal(m.Daily, stock.Count(o => !o.Always));
                foreach (MerchantOffer o in stock) seen.Add(o.Item);
                stocks.Add(string.Join(",", stock.Select(o => o.Item)));
            }
            // Le stock change d'un jour à l'autre, et toute offre finit par revenir.
            Assert.Equal(m.Offers.Select(o => o.Item).OrderBy(i => i, StringComparer.Ordinal), seen.OrderBy(i => i, StringComparer.Ordinal));
            Assert.True(stocks.Count > 1, m.Id);
        }
        Assert.Equal("merchant.armurier/day/3", Market.DayKey("merchant.armurier", 3));
        Assert.Throws<ArgumentException>(() => a.Stock("merchant.inconnu", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Stock("merchant.armurier", -1));
    }

    [Fact]
    public void Stock_suit_le_jour_du_monde()
    {
        // Le jour du monde vient des ticks (une journée = 2 h de jeu) : une session qui commence au jour 3 a le stock du jour 3.
        long ticksPerDay = new WorldClock(50).TicksPerDay;
        (double x, double z) = Near("merchant.armurier");
        ZoneDefinition zone = Zone(x, z);
        var market = new Market(ShippedMerchants);
        var session = new WorldSession(new WorldSave("zone.vallons", 3 * ticksPerDay + 10, 0, Array.Empty<string>(), Hero()), zone, ShippedData, Progression, ItemsCatalog,
            Book(zone), market: market);
        Assert.Equal(3, session.Zone.Time.Day);
        Assert.Equal(market.Stock("merchant.armurier", 3).Select(o => o.Item), session.Stock("merchant.armurier").Select(o => o.Item));
    }

    // --- Achat, vente, rachat ------------------------------------------------------------------------------------------

    [Fact]
    public void Acheter_une_potion_chez_l_herboriste()
    {
        WorldSession w = At("merchant.herboriste", Hero(100));
        Assert.Equal(TradeRefusal.None, w.CanTrade("merchant.herboriste"));
        Assert.Equal(TradeRefusal.None, w.Buy("merchant.herboriste", "item.potion_de_soin"));
        Assert.Equal((40L, 1), (w.Money, w.Inventory.Count("item.potion_de_soin")));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.NotEnoughMoney, w.Buy("merchant.herboriste", "item.potion_de_soin"));   // 40 cuivre pour 60
        Assert.Equal(before, Snapshot(w));
        Assert.Equal(TradeRefusal.NotForSale, w.Buy("merchant.herboriste", "item.lame_du_seuil"));
        Assert.Equal(TradeRefusal.UnknownItem, w.Buy("merchant.herboriste", "item.inconnu"));
        Assert.Equal(TradeRefusal.UnknownItem, w.Buy("merchant.herboriste", null!));
        Assert.Equal(TradeRefusal.InvalidCount, w.Buy("merchant.herboriste", "item.potion_de_soin", 0));
        Assert.Equal(TradeRefusal.InvalidCount, w.Buy("merchant.herboriste", "item.potion_de_soin", Market.MaxCount + 1));
        Assert.Equal(TradeRefusal.TooFar, w.Buy("merchant.armurier", "item.gambison"));   // l'armurier est loin
        Assert.Equal(before, Snapshot(w));
        Assert.Throws<ArgumentException>(() => w.Buy("merchant.inconnu", "item.potion_de_soin"));
        // Plusieurs à la fois : le prix est multiplié, vérifié.
        WorldSession rich = At("merchant.herboriste", Hero(Coins.Max));
        Assert.Equal(TradeRefusal.None, rich.Buy("merchant.herboriste", "item.herbe_de_l_aube", 20));
        Assert.Equal(Coins.Max - 20 * 22, rich.Money);
        // La bourse est sauvegardée avec le personnage.
        Assert.Equal(40, w.Save().Character!.Money);
        Assert.Equal(40, SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!.Character!.Money);
    }

    [Fact]
    public void Inventaire_plein_rien_ne_change()
    {
        ItemStack[] bag = Enumerable.Range(0, ItemsCatalog.InventorySize).Select(_ => new ItemStack("item.gambison", 1)).ToArray();
        WorldSession w = At("merchant.herboriste", Hero(1000, bag));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.InventoryFull, w.Buy("merchant.herboriste", "item.potion_de_soin"));
        Assert.Equal(before, Snapshot(w));
        // Vendre libère une place : l'achat passe ; le rachat, lui, ne tient plus.
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.herboriste", "item.gambison"));
        Assert.Equal(TradeRefusal.None, w.Buy("merchant.herboriste", "item.potion_de_soin"));
        before = Snapshot(w);
        Assert.Equal(TradeRefusal.InventoryFull, w.BuyBack("merchant.herboriste", 0));
        Assert.Equal(before, Snapshot(w));
    }

    [Fact]
    public void Vendre_puis_racheter_au_meme_prix()
    {
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.epicier", Hero(0, new ItemStack("item.peau_de_loup", 3), new ItemStack("item.defense_de_sanglier", 1)), market);
        // L'épicière rachète à 30 % de la valeur : peau de loup (40) → 12 cuivre.
        Assert.Equal(12, market.UnitPrice("merchant.epicier", "item.peau_de_loup"));
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.epicier", "item.peau_de_loup", 2));
        Assert.Equal((24L, 1), (w.Money, w.Inventory.Count("item.peau_de_loup")));
        SoldLot lot = Assert.Single(market.Buyback);
        Assert.Equal(("item.peau_de_loup", 2, 24L), (lot.Item, lot.Count, lot.Paid));
        Assert.Equal(TradeRefusal.None, w.BuyBack("merchant.epicier", 0));
        Assert.Equal((0L, 3), (w.Money, w.Inventory.Count("item.peau_de_loup")));
        Assert.Empty(market.Buyback);
        Assert.Equal(TradeRefusal.NothingToBuyBack, w.BuyBack("merchant.epicier", 0));
        Assert.Equal(TradeRefusal.NothingToBuyBack, w.BuyBack("merchant.epicier", -1));
        // Plusieurs lots d'un coup (le même objet deux fois compte en entier) ; tout ou rien.
        Assert.Equal(TradeRefusal.NotEnough, w.Sell("merchant.epicier", new[] { ("item.peau_de_loup", 2), ("item.peau_de_loup", 2) }));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.NoValue, w.Sell("merchant.epicier", new[] { ("item.peau_de_loup", 1), ("item.ferrure", 1) }));
        Assert.Equal(before, Snapshot(w));
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.epicier", new[] { ("item.peau_de_loup", 1), ("item.defense_de_sanglier", 1), ("item.peau_de_loup", 2) }));
        Assert.Equal(12 + 15 + 24, w.Money);
        Assert.Equal(new[] { ("item.peau_de_loup", 2), ("item.defense_de_sanglier", 1), ("item.peau_de_loup", 1) }, market.Buyback.Select(l => (l.Item, l.Count)));
        Assert.Equal(TradeRefusal.InvalidCount, w.Sell("merchant.epicier", Array.Empty<(string, int)>()));
        Assert.Equal(TradeRefusal.InvalidCount, w.Sell("merchant.epicier", "item.peau_de_loup", 0));
        Assert.Equal(TradeRefusal.UnknownItem, w.Sell("merchant.epicier", "item.inconnu"));
    }

    [Fact]
    public void Rachat_borne_et_garde_d_une_session_a_l_autre()
    {
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.epicier", Hero(0, new ItemStack("item.fleche_brisee", 20)), market);
        for (int i = 0; i < Market.MaxBuyback + 3; i++) Assert.Equal(TradeRefusal.None, w.Sell("merchant.epicier", "item.fleche_brisee"));
        Assert.Equal(Market.MaxBuyback, market.Buyback.Count);   // les plus anciens oubliés
        // Une nouvelle session (halte) avec la même instance : les lots restent rachetables.
        WorldSession next = Play(Zone(Near("merchant.epicier").X, Near("merchant.epicier").Z), w.Save().Character!, market);
        Assert.Equal(TradeRefusal.None, next.BuyBack("merchant.epicier", 0));
        Assert.Equal(Market.MaxBuyback - 1, market.Buyback.Count);
        Assert.Equal(20 - Market.MaxBuyback - 3 + 1, next.Inventory.Count("item.fleche_brisee"));
        // Or insuffisant pour racheter : rien ne change.
        WorldSession poor = At("merchant.epicier", Hero(0), market);
        string before = Snapshot(poor);
        Assert.Equal(TradeRefusal.NotEnoughMoney, poor.BuyBack("merchant.epicier", 0));
        Assert.Equal(before, Snapshot(poor));
    }

    [Fact]
    public void Objet_porte_reserve_ou_absent_ne_se_vend_pas()
    {
        SavedCharacter hero = Hero(0, new ItemStack("item.potion_de_soin", 2), new ItemStack("item.peau_de_loup", 1))
            .WithItems(new[] { new ItemStack("item.potion_de_soin", 2), new ItemStack("item.peau_de_loup", 1) },
                new Dictionary<string, string> { ["weapon"] = "item.epee_du_milicien" });
        WorldSession w = At("merchant.armurier", hero);
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.Equipped, w.Sell("merchant.armurier", "item.epee_du_milicien"));
        Assert.Equal(TradeRefusal.Reserved, w.Sell("merchant.armurier", "item.potion_de_soin"));   // emportées au combat à l'entrée
        Assert.Equal(2, w.Inventory.Reserved("item.potion_de_soin"));
        Assert.Equal(TradeRefusal.NotEnough, w.Sell("merchant.armurier", "item.peau_de_loup", 2));
        Assert.Equal(TradeRefusal.NotEnough, w.Sell("merchant.armurier", "item.gambison"));
        Assert.Equal(TradeRefusal.NoValue, w.Sell("merchant.armurier", "item.ferrure"));
        Assert.Equal(before, Snapshot(w));
        // Retirée, l'épée se vend.
        Assert.Equal(InventoryRefusal.None, w.Inventory.Unequip("weapon"));
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.armurier", "item.epee_du_milicien"));
        Assert.Equal(75, w.Money);   // 25 % de 300
    }

    [Fact]
    public void Bourse_pleine_la_vente_est_refusee()
    {
        WorldSession w = At("merchant.epicier", Hero(Coins.Max - 11, new ItemStack("item.peau_de_loup", 2)));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.PurseFull, w.Sell("merchant.epicier", "item.peau_de_loup"));
        Assert.Equal(before, Snapshot(w));
        WorldSession exact = At("merchant.epicier", Hero(Coins.Max - 12, new ItemStack("item.peau_de_loup", 2)));
        Assert.Equal(TradeRefusal.None, exact.Sell("merchant.epicier", "item.peau_de_loup"));
        Assert.Equal(Coins.Max, exact.Money);
    }

    [Fact]
    public void Marchand_trop_loin_ou_ferme()
    {
        // Trop loin (à l'entrée du village) : tout est refusé, rien ne change.
        WorldSession far = Play(Zone(), Hero(1000, new ItemStack("item.peau_de_loup", 1)));
        string before = Snapshot(far);
        Assert.Equal(TradeRefusal.TooFar, far.CanTrade("merchant.herboriste"));
        Assert.Equal(TradeRefusal.TooFar, far.Buy("merchant.herboriste", "item.potion_de_soin"));
        Assert.Equal(TradeRefusal.TooFar, far.Sell("merchant.herboriste", "item.peau_de_loup"));
        Assert.Equal(TradeRefusal.TooFar, far.BuyBack("merchant.herboriste", 0));
        Assert.Equal(before, Snapshot(far));
        // Fermé : un herboriste qui ferme le jour (le monde commence à 8 h).
        JsonObject root = MerchantsJson();
        MerchantJson(root, 1)["closed"] = new JsonArray("day");
        MerchantsLoadResult closedBook = ReadMerchants(root);
        Assert.True(closedBook.IsValid, closedBook.Report.ToString());
        WorldSession shut = At("merchant.herboriste", Hero(1000), new Market(closedBook.Book!));
        Assert.Equal(DayPhase.Day, shut.Zone.Time.Phase);
        before = Snapshot(shut);
        Assert.Equal(TradeRefusal.Closed, shut.CanTrade("merchant.herboriste"));
        Assert.Equal(TradeRefusal.Closed, shut.Buy("merchant.herboriste", "item.potion_de_soin"));
        Assert.Equal(before, Snapshot(shut));
        // Sans histoire, pas de commerce ; un commerce dont un PNJ manque à l'histoire est refusé.
        Assert.Throws<ArgumentException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()), Zone(), ShippedData, Progression,
            ItemsCatalog, market: new Market(ShippedMerchants)));
        Assert.Throws<InvalidOperationException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()), Zone(), ShippedData,
            Progression, ItemsCatalog, Book(Zone())).Buy("merchant.herboriste", "item.potion_de_soin"));
    }

    /// <summary>
    /// Propriétés, sur 3 000 opérations tirées au hasard (graine fixe) : un refus ne change rien ; une réussite change la bourse et
    /// l'inventaire ensemble, du montant exact ; la bourse reste dans ses bornes ; la somme des pièces et de la valeur de revente du sac ne
    /// croît jamais (aucune opération ne crée d'argent : vendre puis racheter rend la même somme).
    /// </summary>
    [Fact]
    public void Proprietes_des_echanges()
    {
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.herboriste", Hero(2_000, new ItemStack("item.peau_de_loup", 20), new ItemStack("item.herbe_de_l_aube", 15),
            new ItemStack("item.gambison", 1), new ItemStack("item.potion_de_soin", 2)), market);
        const string m = "merchant.herboriste";
        string[] items = { "item.potion_de_soin", "item.herbe_de_l_aube", "item.peau_de_loup", "item.gambison", "item.remede_de_l_aube", "item.eclat_de_seuil",
            "item.ferrure", "item.inconnu" };
        var random = new Pcg32(42);
        long Wealth() => w.Money + w.Inventory.Stacks.Sum(s => market.UnitPrice(m, s.Item) * s.Count);
        long wealth = Wealth();
        int done = 0;
        for (int i = 0; i < 3000; i++)
        {
            string item = items[random.NextBounded((uint)items.Length)];
            int count = 1 + (int)random.NextBounded(i % 50 == 0 ? 1200u : 4u);
            string before = Snapshot(w);
            long money = w.Money;
            int had = w.Inventory.Count(item);
            TradeRefusal r;
            switch (random.NextBounded(3))
            {
                case 0:
                    r = w.Buy(m, item, count);
                    if (r == TradeRefusal.None)
                    {
                        long price = w.Stock(m).Single(o => o.Item == item).Price;
                        Assert.Equal(money - price * count, w.Money);
                        Assert.Equal(had + count, w.Inventory.Count(item));
                    }
                    break;
                case 1:
                    r = w.Sell(m, item, count);
                    if (r == TradeRefusal.None)
                    {
                        Assert.Equal(money + market.UnitPrice(m, item) * count, w.Money);
                        Assert.Equal(had - count, w.Inventory.Count(item));
                    }
                    break;
                default:
                    int index = (int)random.NextBounded(14) - 1;
                    SoldLot? lot = index >= 0 && index < market.Buyback.Count ? market.Buyback[index] : null;
                    r = w.BuyBack(m, index);
                    if (r == TradeRefusal.None)
                    {
                        Assert.Equal(money - lot!.Paid, w.Money);
                        Assert.DoesNotContain(lot, market.Buyback);
                    }
                    break;
            }
            if (r != TradeRefusal.None) Assert.Equal(before, Snapshot(w));
            else done++;
            Assert.InRange(w.Money, 0, Coins.Max);
            long now = Wealth();
            Assert.True(now <= wealth, $"opération {i} : {wealth} → {now}");
            wealth = now;
        }
        Assert.True(done > 100, done.ToString());
    }

    // --- Boutique dans l'aventure ---------------------------------------------------------------------------------------

    [Fact]
    public void Parler_au_marchand_ouvre_sa_boutique()
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(ScenariosDirectory, scenarios);
        string zonePath = ZoneJsonReader.PathIn(data, "zone.vallons");
        JsonObject zone = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
        zone["data"]!["safePoints"]![0]!["position"] = new JsonArray(-65.2, -6.4);   // l'entrée près de l'herboriste
        File.WriteAllText(zonePath, zone.ToJsonString());
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(130, new ItemStack("item.peau_de_loup", 3))));
        AdventureSession a = Adventure(savePath, data, scenarios);
        a.DrainMessages();
        Assert.Equal("npc.herboriste", a.NpcInReach!.Id);
        Assert.True(a.IsMerchant("npc.herboriste"));
        Assert.False(a.IsMerchant("npc.odile"));
        Assert.Equal("1 argent 30 cuivre", a.Purse);
        Assert.True(a.Interact());   // F : la boutique, pas le dialogue
        Assert.Equal("merchant.herboriste", a.Shop);
        Assert.Null(a.Dialogue);
        Assert.Equal("Sauge, l'herboriste", a.ShopName);
        Assert.StartsWith("Des potions de soin, toujours.", a.ShopGreeting, StringComparison.Ordinal);
        ShopOffer potion = a.ShopOffers().First();
        Assert.Equal(("item.potion_de_soin", "Potion de soin", 60L, "60 cuivre", TradeRefusal.None), (potion.Item, potion.Name, potion.Price, potion.PriceText, potion.Refusal));
        Assert.Equal(TradeRefusal.None, a.Buy("item.potion_de_soin"));
        Assert.Equal(TradeRefusal.None, a.Buy("item.potion_de_soin"));
        Assert.Equal(TradeRefusal.NotEnoughMoney, a.ShopOffers().First().Refusal);
        Assert.Equal(TradeRefusal.NotEnoughMoney, a.Buy("item.potion_de_soin"));
        Assert.Equal(10, a.SellPrice("item.peau_de_loup"));
        Assert.Equal(TradeRefusal.None, a.SellPlace(0));   // toute la pile de peaux
        Assert.Equal(TradeRefusal.NoValue, a.Sell("item.ferrure"));
        string[] said = a.DrainMessages().ToArray();
        Assert.Equal(new[] { "Acheté : Potion de soin ×1 pour 60 cuivre.", "Acheté : Potion de soin ×1 pour 60 cuivre.", "Or insuffisant.",
            "Vendu : Peau de loup ×3 pour 30 cuivre.", "Le marchand n'en veut pas." }, said);
        ShopLot lot = Assert.Single(a.Buyback());
        Assert.Equal(("Peau de loup", 3, "30 cuivre"), (lot.Name, lot.Count, lot.PaidText));
        Assert.Equal(TradeRefusal.None, a.BuyBack(0));
        Assert.Equal("Racheté : Peau de loup ×3 pour 30 cuivre.", Assert.Single(a.DrainMessages()));
        Assert.Equal(10, a.Money);
        Assert.True(a.LootAnnounced >= 4);   // le bruit des pièces
        // Sauvegarde : bourse et objets ensemble.
        a.Save();
        SavedCharacter saved = SaveStore.Read(savePath).Save!.Character!;
        Assert.Equal((10L, 2, 3), (saved.Money, saved.Inventory.Where(s => s.Item == "item.potion_de_soin").Sum(s => s.Count),
            saved.Inventory.Where(s => s.Item == "item.peau_de_loup").Sum(s => s.Count)));
        // Parler : la boutique se ferme, le dialogue de l'herboriste s'ouvre.
        Assert.True(a.TalkToShopkeeper());
        Assert.Null(a.Shop);
        Assert.Equal("Sauge, l'herboriste", a.DialogueNpc);
        a.CloseDialogue();
        // Sans boutique ouverte, rien ne s'achète.
        Assert.Equal(TradeRefusal.TooFar, a.Buy("item.potion_de_soin"));
        Assert.Empty(a.ShopOffers());
        Assert.True(a.OpenShop("merchant.herboriste"));
        a.CloseShop();
        Assert.False(a.OpenShop("merchant.armurier"));   // trop loin
        Assert.Equal("Trop loin du marchand.", a.DrainMessages().Last());
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
