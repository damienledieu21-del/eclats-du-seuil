using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Pâtisseries de Firmin (dossier de Damien du 19 novembre 2026, 16 modèles Tripo) : marchands en schéma 7 (jours et saisons d'une offre
/// permanente), offres de la boulangerie selon le jour et la saison, refus du validateur (atomiques), tirage du jour inchangé, niveau de
/// réussite d'une pièce (modèle, icône, texte), modèles du lot C (catégorie <c>09_Firmin</c>) et étal du fournil.
/// </summary>
[Trait("Category", "World")]
public class FirminPatisseriesTests
{
    private const string Bakery = FirminPastries.Bakery;
    private const string Couronne = "item.couronne_des_moissons", Fouace = "item.fouace_des_semailles", Tarte = "item.tarte_renversee_aux_pommes";

    private static readonly string Peintes = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Interface", "Resources", "EdsInterface", "Peintes");

    private static int BakeryIndex(JsonObject root) =>
        root["data"]!["merchants"]!.AsArray().Select((m, i) => (m, i)).Single(x => (string?)x.m!["id"] == Bakery).i;

    private static JsonObject BakeryOffer(JsonObject root, string item) =>
        root["data"]!["merchants"]![BakeryIndex(root)]!["offers"]!.AsArray().Single(o => (string?)o!["item"] == item)!.AsObject();

    private static string OfferPath(JsonObject root, string item)
    {
        JsonArray offers = root["data"]!["merchants"]![BakeryIndex(root)]!["offers"]!.AsArray();
        int k = offers.Select((o, i) => (o, i)).Single(x => (string?)x.o!["item"] == item).i;
        return $"$.data.merchants[{BakeryIndex(root)}].offers[{k}]";
    }

    private static long FirstDay(int weekday, string season) =>
        Enumerable.Range(0, 4 * 400).First(d => WorldCalendar.WeekdayOf(d) == weekday && WorldCalendar.SeasonOf(d) == season);

    [Fact]
    public void Schema_7_et_offres_de_la_boulangerie()
    {
        Assert.Equal((7, 6, 7), (MerchantValidator.CurrentSchemaVersion, MerchantValidator.Schema6, ShippedMerchants.SchemaVersion));
        Merchant bakery = ShippedMerchants.Merchant(Bakery);
        MerchantOffer O(string item) => bakery.Offers.Single(o => o.Item == item);
        Assert.Equal(new[] { 6 }, O(Tarte).Weekdays);
        Assert.Empty(O(Tarte).Seasons);
        Assert.Equal(new[] { 5, 6 }, O(Fouace).Weekdays);
        Assert.Equal(new[] { "spring" }, O(Fouace).Seasons);
        Assert.Equal(new[] { 5, 6 }, O(Couronne).Weekdays);
        Assert.Equal(new[] { "autumn" }, O(Couronne).Seasons);
        Assert.False(O("item.levain").Seasonal);
        Assert.False(O("item.pate_feuilletee").Seasonal);
        // Toute pièce vendue vaut au moins sa valeur de base (contrat de l'économie, revérifié).
        foreach (MerchantOffer o in bakery.Offers) Assert.True(o.Price >= ShippedMerchants.Values[o.Item], o.Item);
    }

    [Fact]
    public void Les_pieces_de_fete_suivent_le_jour_et_la_saison()
    {
        var market = new Market(ShippedMerchants);
        IReadOnlyList<string> On(long day) => market.Stock(Bakery, day).Select(o => o.Item).ToList();
        long springSaturday = FirstDay(5, "spring"), springTuesday = FirstDay(1, "spring"), autumnSunday = FirstDay(6, "autumn"), summerSunday = FirstDay(6, "summer");
        Assert.Contains(Fouace, On(springSaturday));
        Assert.DoesNotContain(Couronne, On(springSaturday));
        Assert.DoesNotContain(Tarte, On(springSaturday));   // le dimanche seulement
        Assert.DoesNotContain(Fouace, On(springTuesday));
        Assert.Contains(Couronne, On(autumnSunday));
        Assert.Contains(Tarte, On(autumnSunday));
        Assert.DoesNotContain(Fouace, On(autumnSunday));
        Assert.Equal(new[] { Tarte }, On(summerSunday).Where(i => i == Tarte || i == Fouace || i == Couronne));
        for (long d = 0; d < 120; d++)
        {
            Assert.Contains("item.levain", On(d));
            Assert.Equal(On(d), On(d));   // même jour, même étal
        }
    }

    [Fact]
    public void Le_tirage_du_jour_ne_change_pas()
    {
        // Non-régression : sans les cinq offres nouvelles (schéma 6), les offres tirées et permanentes de chaque jour restent les mêmes.
        JsonObject root = MerchantsJson();
        root["schemaVersion"] = 6;
        JsonArray offers = root["data"]!["merchants"]![BakeryIndex(root)]!["offers"]!.AsArray();
        foreach (string item in new[] { "item.levain", "item.pate_feuilletee", Tarte, Fouace, Couronne })
            offers.Remove(offers.Single(o => (string?)o!["item"] == item));
        MerchantsLoadResult old = ReadMerchants(root);
        Assert.True(old.Book != null, old.Report.ToString());
        var before = new Market(old.Book!);
        var after = new Market(ShippedMerchants);
        string[] added = { "item.levain", "item.pate_feuilletee", Tarte, Fouace, Couronne };
        for (long d = 0; d < 200; d++)
            foreach (Merchant m in ShippedMerchants.Merchants)
                Assert.Equal(before.Stock(m.Id, d).Select(o => o.Item), after.Stock(m.Id, d).Select(o => o.Item).Where(i => m.Id != Bakery || !added.Contains(i)));
    }

    [Theory]
    [InlineData("weekdays", "[7]", ".weekdays[0]", ValidationCode.OutOfRange)]
    [InlineData("weekdays", "[-1]", ".weekdays[0]", ValidationCode.OutOfRange)]
    [InlineData("weekdays", "[5, 5]", ".weekdays[1]", ValidationCode.DuplicateId)]
    [InlineData("weekdays", "[]", ".weekdays", ValidationCode.OutOfRange)]
    [InlineData("weekdays", "[0, 1, 2, 3, 4, 5, 6]", ".weekdays", ValidationCode.OutOfRange)]
    [InlineData("weekdays", "[0]", ".weekdays", ValidationCode.InvalidValue)]   // le lundi, jour de repos de la boulangerie
    [InlineData("seasons", "[\"printemps\"]", ".seasons[0]", ValidationCode.UnknownReference)]
    [InlineData("seasons", "[\"spring\", \"spring\"]", ".seasons[1]", ValidationCode.DuplicateId)]
    [InlineData("seasons", "[\"spring\", \"summer\", \"autumn\", \"winter\"]", ".seasons", ValidationCode.OutOfRange)]
    public void Refus_des_jours_et_saisons_mal_formes(string field, string value, string suffix, ValidationCode code)
    {
        JsonObject root = MerchantsJson();
        BakeryOffer(root, Fouace)[field] = JsonNode.Parse(value);
        MerchantsLoadResult result = ReadMerchants(root);
        Assert.Null(result.Book);   // refus atomique : aucun livre de marchands
        Assert.Contains(Errors(result), e => e.Path == OfferPath(root, Fouace) + suffix && e.Code == code);
    }

    [Fact]
    public void Jours_d_une_offre_tiree_ou_en_version_6_refuses()
    {
        JsonObject root = MerchantsJson();
        BakeryOffer(root, "item.chausson_aux_pommes")["weekdays"] = JsonNode.Parse("[6]");
        MerchantsLoadResult rotating = ReadMerchants(root);
        Assert.Null(rotating.Book);
        Assert.Contains(Errors(rotating), e => e.Path == OfferPath(root, "item.chausson_aux_pommes") + ".weekdays" && e.Code == ValidationCode.InvalidValue);

        JsonObject v6 = MerchantsJson();
        v6["schemaVersion"] = 6;
        MerchantsLoadResult old = ReadMerchants(v6);
        Assert.Null(old.Book);
        Assert.Contains(Errors(old), e => e.Path == OfferPath(v6, Fouace) + ".weekdays" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Niveau_de_reussite_modele_icone_et_texte()
    {
        Assert.Equal(10, FirminPastries.All.Count);
        Assert.Equal((1, 2, 3, 3), (FirminPastries.Level(ItemQuality.Normal, true), FirminPastries.Level(ItemQuality.Superior, true),
            FirminPastries.Level(ItemQuality.Masterwork, true), FirminPastries.Level(ItemQuality.Normal, false)));
        Assert.Equal("FIR01_N1", FirminPastries.Model(Couronne, ItemQuality.Normal, true));
        Assert.Equal("FIR03_N2", FirminPastries.Model(Fouace, ItemQuality.Superior, true));
        Assert.Equal("FIR02_N3", FirminPastries.Model(Tarte, ItemQuality.Normal, false));
        Assert.Equal("FIR08", FirminPastries.Model("item.croissant_au_beurre", ItemQuality.Normal, true));
        Assert.Null(FirminPastries.Model("item.potion_de_soin", ItemQuality.Normal, true));
        Assert.Equal("couronne_des_moissons_n1", FirminPastries.Icon(Couronne, "couronne_des_moissons", ItemQuality.Normal, true));
        Assert.Equal("couronne_des_moissons", FirminPastries.Icon(Couronne, "couronne_des_moissons", ItemQuality.Masterwork, true));
        Assert.Equal("brioche_doree", FirminPastries.Icon("item.brioche_doree", "brioche_doree", ItemQuality.Normal, true));
        Assert.Equal("sac", FirminPastries.Icon("item.potion_de_soin", "sac", ItemQuality.Normal, true));
        Assert.Equal("text.firmin.tarte_renversee_aux_pommes.n2", FirminPastries.TextKey(Tarte, ItemQuality.Superior, true));
        Assert.Equal("text.firmin.levain", FirminPastries.TextKey("item.levain", ItemQuality.Normal, false));
        Assert.Equal(ItemQuality.Masterwork, FirminPastries.QualityOf("masterwork"));
        Assert.Null(FirminPastries.QualityOf("legendary"));
    }

    [Fact]
    public void Chaque_produit_a_son_objet_son_icone_et_ses_textes()
    {
        foreach (FirminPastry p in FirminPastries.All)
        {
            Assert.True(ItemsCatalog.TryGetItem(p.Item, out var item), p.Item);
            Assert.Equal(p.Short, item!.Icon);
            Assert.Equal(p.Levels, item.CraftQuality);   // les trois pièces de concours, et elles seules, prennent une qualité
            Assert.Empty(ItemIcons.Keywords(p.Item));   // l'icône du dossier passe devant le pack de l'Asset Store
            foreach (ItemQuality q in new[] { ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Masterwork })
                foreach (bool signed in new[] { true, false })
                {
                    Assert.True(File.Exists(Path.Combine(Peintes, FirminPastries.Icon(p.Item, item.Icon, q, signed) + ".png")), p.Item + " " + q);
                    Assert.True(Texts.Has(FirminPastries.TextKey(p.Item, q, signed)!), p.Item + " " + q);
                }
        }
        Assert.NotEmpty(ItemIcons.Keywords("item.miche_du_matin"));   // les autres objets gardent le pack
    }

    [Fact]
    public void Seize_modeles_du_lot_C_relies_par_qualite()
    {
        LotCModel[] firmin = LotCModels.All.Where(m => m.Category == LotCModels.FirminCategory).ToArray();
        Assert.Equal(16, firmin.Length);
        Assert.All(firmin, m => Assert.True(m.Linked, m.Code));
        foreach (FirminPastry p in FirminPastries.All)
        {
            Assert.Equal(p.ModelCode(3), LotCModels.ForItem(p.Item));
            foreach (ItemQuality q in new[] { ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Masterwork })
            {
                string name = q == ItemQuality.Normal ? "normal" : q == ItemQuality.Superior ? "superior" : "masterwork";
                Assert.Equal(FirminPastries.Model(p.Item, q, true), LotCModels.ForQuality(p.Item, name));
            }
        }
        Assert.Equal("FIR05", LotCModels.CodeOf("Assets/EdS/Modeles/LotC/09_Firmin/FIR05/FIR05.fbx"));
        Assert.Null(LotCModels.CodeOf("Assets/EdS/Modeles/LotC/08_Recoltes/FIR05/FIR05.fbx"));
    }

    [Fact]
    public void Etal_du_fournil_dans_l_ordre_et_borne()
    {
        Assert.Equal(new[] { "FIR01_N3", "FIR10", "FIR05", "FIR04" },
            FirminPastries.Showcase(new[] { "item.levain", "item.pain_de_campagne", "item.potion_de_soin", "item.brioche_doree", Couronne }));
        Assert.Equal(FirminPastries.ShowcaseSlots, FirminPastries.Showcase(FirminPastries.All.Select(p => p.Item)).Count);
        Assert.Empty(FirminPastries.Showcase(null));
        // Un dimanche d'automne, l'étal du fournil montre la couronne et la tarte de Firmin (niveau maîtrisé).
        var market = new Market(ShippedMerchants);
        IReadOnlyList<string> codes = FirminPastries.Showcase(market.Stock(Bakery, FirstDay(6, "autumn")).Select(o => o.Item));
        Assert.Equal(new[] { "FIR01_N3", "FIR02_N3" }, codes.Take(2));
    }
}
