using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Vague 4 (2), cuisine et boulangerie (1/n) : le métier de cuisine (sur le modèle de la forge, de l'herboristerie et du minage), ses
/// ingrédients (épicier, fromagère, pommes du verger, poissons de la pêche), ses pains et viennoiseries, ses deux pâtisseries et ses quatre
/// plats (premiers objets de la place de bonus « plat », prête depuis le sous-lot V1.8c).
/// </summary>
[Trait("Category", "World")]
public class CuisineBoulangerieTests
{
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static readonly Lazy<ItemCatalog> Items = new(() => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!);
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;

    private static ZoneDefinition Zone(double x, double z)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, Items.Value).Zone!;
    }

    private static readonly Lazy<CraftingBook> Shipped = new(() => Book(Zone(-18, -12)));

    private static CraftingBook Book(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, Items.Value).Book!;

    private static WorldSession Play(ZoneDefinition zone, IReadOnlyDictionary<string, long>? professions, params ItemStack[] items)
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>())
            .WithProfessions(professions ?? new Dictionary<string, long>());
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items.Value, null, Book(zone));
        session.Step();
        return session;
    }

    private static IEnumerable<Recipe> Cuisine => Shipped.Value.Recipes.Where(r => r.Profession == "cuisine");

    [Fact]
    public void Metier_de_cuisine_quatorze_recettes_au_four_de_Firmin_et_au_feu_de_camp()
    {
        CraftingBook book = Shipped.Value;
        Assert.Equal("0.14.0-provisoire", book.ContentVersion);
        Assert.Contains(book.Professions, p => p.Id == "cuisine");
        Assert.Equal("Cuisine et boulangerie", Texts.Get("text.profession.cuisine"));
        Assert.Equal(14, Cuisine.Count());   // 16 novembre 2026 : la fouace des Semailles
        // Réponse de Damien du 15 novembre 2026 : pains et pâtisseries au four de Firmin, poissons et plats simples au feu de camp.
        Assert.Equal(new[] { "recipe.cuisine.friture_du_lac", "recipe.cuisine.truite_meuniere", "recipe.cuisine.quenelles_de_brochet", "recipe.cuisine.matelote_de_carpe_doree" },
            Cuisine.Where(r => r.Place == "feu_de_camp").Select(r => r.Id));
        Assert.All(Cuisine.Where(r => r.Place != "feu_de_camp"), r => Assert.Equal("four_de_firmin", r.Place));
        Assert.All(Cuisine, r => Assert.False(string.IsNullOrWhiteSpace(Texts.Get(r.Name))));
        // Du plus simple (levain, pain de campagne) au plus fin (couronne des moissons, matelote) : niveaux 1 à 8, l'expérience suit.
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2, 3, 3, 4, 5, 6, 7, 7, 8 }, Cuisine.Select(r => r.RequiredLevel).OrderBy(l => l));
        Assert.Equal(("item.pain_de_campagne", 3), book.Recipe("recipe.cuisine.pain_de_campagne").Output);
        Assert.Equal(("item.croissant_au_beurre", 4), book.Recipe("recipe.cuisine.croissant_au_beurre").Output);
        // Les pommes du verger se cueillent (expérience de cuisine).
        Assert.Contains(book.Nodes, n => n.Id == "pommes_verger" && n.Profession == "cuisine" && n.Item == "item.pomme_du_verger");
        // Relecture de la PR #183 : un pommier près du bourg (les vergers sont au niveau 6, les chaussons au niveau 3 du métier).
        Assert.Contains(book.Nodes, n => n.Id == "pommier_du_hameau" && n.Profession == "cuisine" && n.Item == "item.pomme_du_verger");
    }

    [Fact]
    public void Chaque_ingredient_s_achete_se_cueille_se_peche_ou_se_prepare()
    {
        CraftingBook book = Shipped.Value;
        MerchantBook merchants = ShippedMerchants;
        var sold = new HashSet<string>(merchants.Merchants.SelectMany(m => m.Offers).Select(o => o.Item), StringComparer.Ordinal);
        var gathered = new HashSet<string>(book.Nodes.Select(n => n.Item), StringComparer.Ordinal);
        var made = new HashSet<string>(Cuisine.Select(r => r.Output.Item), StringComparer.Ordinal);
        string[] fish = { "item.gardon", "item.truite", "item.brochet", "item.carpe_doree" };
        foreach (Recipe r in Cuisine)
            foreach ((string item, int _) in r.Inputs)
                Assert.True(sold.Contains(item) || gathered.Contains(item) || made.Contains(item) || fish.Contains(item), $"{r.Id} : « {item} » introuvable");
        // Farines et œufs chez l'épicier, beurre et lait chez la fromagère.
        Assert.Contains(merchants.Merchants.Single(m => m.Id == "merchant.epicier").Offers, o => o.Item == "item.farine_de_ble");
        Assert.Contains(merchants.Merchants.Single(m => m.Id == "merchant.fromagere").Offers, o => o.Item == "item.beurre_de_baratte");
    }

    [Fact]
    public void Quatre_plats_pour_la_place_du_plat_et_deux_patisseries()
    {
        ItemCatalog c = Items.Value;
        Assert.Equal("0.23.0-provisoire", c.ContentVersion);
        string[] meals = { "item.friture_du_lac", "item.truite_meuniere", "item.quenelles_de_brochet", "item.matelote_de_carpe_doree" };
        Assert.Equal(meals, c.Items.Where(i => i.Boon == BoonKind.Meal).Select(i => i.Id));
        Assert.Equal(new[] { ItemQuality.Normal, ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Masterwork }, meals.Select(m => c.Item(m).BoonQuality));
        Assert.Equal(ItemType.Consumable, c.Item("item.couronne_des_moissons").Type);
        Assert.Equal(ItemType.Consumable, c.Item("item.tarte_renversee_aux_pommes").Type);
        Assert.Equal("Truite meunière", c.Item("item.truite_meuniere").Name);
    }

    [Fact]
    public void Au_four_de_Firmin_le_pain_au_feu_de_camp_la_friture_ailleurs_rien()
    {
        var stock = new[] { new ItemStack("item.farine_de_ble", 3), new ItemStack("item.levain", 1) };
        WorldSession forge = Play(Zone(-19, -5), null, stock);
        Assert.Equal(CraftRefusal.NotHere, forge.Craft("recipe.cuisine.pain_de_campagne", out _));
        Assert.Equal(CraftRefusal.NotHere, Play(Zone(-1092, -34), null, stock).Craft("recipe.cuisine.pain_de_campagne", out _));   // ailleurs dans le bourg
        WorldSession bourg = Play(Zone(-1075.5, -31.5), null, stock);
        Assert.Equal(CraftRefusal.None, bourg.Craft("recipe.cuisine.pain_de_campagne", out CraftEvent? pain));
        Assert.Equal(("item.pain_de_campagne", "cuisine", 20L), (pain!.Item, pain.Profession, pain.Xp));
        Assert.Equal((3, 0, 0), (bourg.Inventory.Count("item.pain_de_campagne"), bourg.Inventory.Count("item.farine_de_ble"), bourg.Inventory.Count("item.levain")));
        Assert.Equal((20L, 0L), (bourg.Workshop!.Xp("cuisine"), bourg.Character.Xp));
        Assert.Equal(CraftRefusal.NotEnough, bourg.Craft("recipe.cuisine.pain_de_campagne", out _));
        // Au-delà de deux niveaux d'écart, la matelote ne se tente pas.
        WorldSession apprentice = Play(Zone(26, -13.5), null, new ItemStack("item.carpe_doree", 1), new ItemStack("item.cidre_du_verger", 2),
            new ItemStack("item.beurre_de_baratte", 1), new ItemStack("item.farine_de_ble", 1));
        Assert.Equal(CraftRefusal.LevelTooLow, apprentice.Craft("recipe.cuisine.matelote_de_carpe_doree", out _));   // relecture de la PR #183 : exactement
        Assert.Equal(1, apprentice.Inventory.Count("item.carpe_doree"));   // rien retiré
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(bourg.Save())).Save!;
        Assert.Equal(20L, saved.Character!.Professions["cuisine"]);
        // Au feu de camp de Gaspard : la friture du lac ; le pain ne s'y fait pas.
        WorldSession fire = Play(Zone(26, -13.5), null, new ItemStack("item.gardon", 3), new ItemStack("item.farine_de_ble", 4), new ItemStack("item.levain", 1));
        Assert.Equal(CraftRefusal.NotHere, fire.Craft("recipe.cuisine.pain_de_campagne", out _));
        Assert.Equal(CraftRefusal.None, fire.Craft("recipe.cuisine.friture_du_lac", out CraftEvent? friture));
        Assert.Equal(("item.friture_du_lac", "cuisine"), (friture!.Item, friture.Profession));
    }

    // --- Relecture indépendante de la PR #183 ---------------------------------------------------------------------------------------

    [Fact]
    public void Ingredients_de_base_toujours_en_vente()
    {
        var epicier = ShippedMerchants.Merchants.Single(m => m.Id == "merchant.epicier").Offers;
        foreach (string item in new[] { "item.farine_de_ble", "item.farine_de_seigle", "item.oeufs_frais", "item.sucre_roux" })
            Assert.True(epicier.Single(o => o.Item == item).Always, item);
        var fromagere = ShippedMerchants.Merchants.Single(m => m.Id == "merchant.fromagere").Offers;
        Assert.True(fromagere.Single(o => o.Item == "item.beurre_de_baratte").Always && fromagere.Single(o => o.Item == "item.lait_cru").Always);
    }

    [Fact]
    public void Cueillette_au_pommier_puis_chaussons_au_four_bout_a_bout()
    {
        // Le pommier du bourg : deux pommes, l'expérience de cuisine.
        WorldSession tree = Play(Zone(-27, -19), null);
        Assert.Equal(CraftRefusal.None, tree.Gather("pommier_du_hameau", out CraftEvent? picked));
        Assert.Equal(("item.pomme_du_verger", 2, "cuisine", 6L), (picked!.Item, picked.Count, picked.Profession, picked.Xp));
        // Au four, au niveau 3 du métier : la pâte feuilletée, puis les chaussons avec les pommes cueillies.
        WorldSession oven = Play(Zone(-1075.5, -31.5), new Dictionary<string, long> { ["cuisine"] = 100 },
            new ItemStack("item.farine_de_ble", 2), new ItemStack("item.beurre_de_baratte", 2), new ItemStack("item.sucre_roux", 1),
            new ItemStack("item.pomme_du_verger", picked.Count));
        Assert.Equal(3, oven.Workshop!.Level("cuisine"));
        Assert.Equal(CraftRefusal.None, oven.Craft("recipe.cuisine.pate_feuilletee", out CraftEvent? pate));
        Assert.Equal(CraftEventKind.Crafted, pate!.Kind);
        Assert.Equal(2, oven.Inventory.Count("item.pate_feuilletee"));
        Assert.Equal(CraftRefusal.None, oven.Craft("recipe.cuisine.chausson_aux_pommes", out CraftEvent? chausson));
        Assert.Equal(CraftEventKind.Crafted, chausson!.Kind);
        Assert.Equal((2, 1, 0, 0), (oven.Inventory.Count("item.chausson_aux_pommes"), oven.Inventory.Count("item.pate_feuilletee"),
            oven.Inventory.Count("item.pomme_du_verger"), oven.Inventory.Count("item.sucre_roux")));
    }

    [Fact]
    public void Fabrication_ratee_au_dessus_de_son_niveau_rend_une_partie_des_ingredients()
    {
        // Niveau 1, brioche (niveau 4) refusée ; chaussons (niveau 3, deux au-dessus) tentés jusqu'à un raté : la moitié rendue.
        var stock = new[] { new ItemStack("item.pate_feuilletee", 10), new ItemStack("item.pomme_du_verger", 20), new ItemStack("item.sucre_roux", 10) };
        WorldSession w = Play(Zone(-1075.5, -31.5), null, stock);
        CraftEvent? failed = null;
        for (int k = 0; k < 10 && failed is null; k++)
        {
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.cuisine.chausson_aux_pommes", out CraftEvent? e));
            if (e!.Kind == CraftEventKind.Failed) failed = e;
        }
        Assert.NotNull(failed);
        Assert.True(failed!.FailPercent > 0);
        Assert.NotEmpty(failed.Refunded);
        Assert.All(failed.Refunded, r => Assert.True(r.Count >= 1));
        Assert.True(failed.Refunded.Sum(r => r.Count) < 4);   // une partie seulement (une pâte, deux pommes, un sucre)
    }

    [Fact]
    public void Cout_des_ingredients_au_moins_la_revente_du_produit()
    {
        // Aucune boucle d'achat et de revente : pour chaque recette dont tous les ingrédients s'achètent (ou se préparent d'ingrédients achetés),
        // le coût le plus bas des ingrédients couvre la meilleure revente du produit.
        var market = new Market(ShippedMerchants);
        var buy = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var o in ShippedMerchants.Merchants.SelectMany(m => m.Offers))
            buy[o.Item] = Math.Min(buy.GetValueOrDefault(o.Item, long.MaxValue), o.Price);
        var byOutput = Cuisine.ToDictionary(r => r.Output.Item, r => r);
        double? Cost(string item)
        {
            if (buy.TryGetValue(item, out long p)) return p;
            if (!byOutput.TryGetValue(item, out Recipe? r)) return null;
            double total = 0;
            foreach ((string i, int n) in r.Inputs)
            {
                if (!(Cost(i) is double c)) return null;
                total += c * n;
            }
            return total / r.Output.Count;
        }
        int checkedRecipes = 0;
        foreach (Recipe r in Cuisine)
        {
            double cost = 0;
            bool all = true;
            foreach ((string i, int n) in r.Inputs)
                if (Cost(i) is double c) cost += c * n; else all = false;
            if (!all) continue;
            long resale = ShippedMerchants.Merchants.Max(m => market.UnitPrice(m.Id, r.Output.Item)) * r.Output.Count;
            Assert.True(cost >= resale, $"{r.Id} : ingrédients {cost}, revente {resale}");
            checkedRecipes++;
        }
        Assert.True(checkedRecipes >= 7);
    }
}
