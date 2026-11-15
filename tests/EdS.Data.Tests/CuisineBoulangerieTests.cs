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
    public void Metier_de_cuisine_treize_recettes_au_four_du_bourg_et_des_noms_en_francais()
    {
        CraftingBook book = Shipped.Value;
        Assert.Equal("0.8.0-provisoire", book.ContentVersion);
        Assert.Contains(book.Professions, p => p.Id == "cuisine");
        Assert.Equal("Cuisine et boulangerie", Texts.Get("text.profession.cuisine"));
        Assert.Equal(13, Cuisine.Count());
        Assert.All(Cuisine, r => Assert.Equal("bourg", r.Place));
        Assert.All(Cuisine, r => Assert.False(string.IsNullOrWhiteSpace(Texts.Get(r.Name))));
        // Du plus simple (levain, pain de campagne) au plus fin (couronne des moissons, matelote) : niveaux 1 à 8, l'expérience suit.
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2, 3, 3, 4, 5, 6, 7, 8 }, Cuisine.Select(r => r.RequiredLevel).OrderBy(l => l));
        Assert.Equal(("item.pain_de_campagne", 3), book.Recipe("recipe.cuisine.pain_de_campagne").Output);
        Assert.Equal(("item.croissant_au_beurre", 4), book.Recipe("recipe.cuisine.croissant_au_beurre").Output);
        // Les pommes du verger se cueillent (expérience de cuisine).
        Assert.Contains(book.Nodes, n => n.Id == "pommes_verger" && n.Profession == "cuisine" && n.Item == "item.pomme_du_verger");
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
        Assert.Equal("0.19.0-provisoire", c.ContentVersion);
        string[] meals = { "item.friture_du_lac", "item.truite_meuniere", "item.quenelles_de_brochet", "item.matelote_de_carpe_doree" };
        Assert.Equal(meals, c.Items.Where(i => i.Boon == BoonKind.Meal).Select(i => i.Id));
        Assert.Equal(new[] { ItemQuality.Normal, ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Masterwork }, meals.Select(m => c.Item(m).BoonQuality));
        Assert.Equal(ItemType.Consumable, c.Item("item.couronne_des_moissons").Type);
        Assert.Equal(ItemType.Consumable, c.Item("item.tarte_renversee_aux_pommes").Type);
        Assert.Equal("Truite meunière", c.Item("item.truite_meuniere").Name);
    }

    [Fact]
    public void Au_four_du_bourg_le_pain_de_campagne_et_l_experience_de_cuisine_hors_du_bourg_rien()
    {
        var stock = new[] { new ItemStack("item.farine_de_ble", 3), new ItemStack("item.levain", 1) };
        WorldSession forge = Play(Zone(-19, -5), null, stock);
        Assert.Equal(CraftRefusal.NotHere, forge.Craft("recipe.cuisine.pain_de_campagne", out _));
        WorldSession bourg = Play(Zone(-40, 0), null, stock);
        Assert.Equal(CraftRefusal.None, bourg.Craft("recipe.cuisine.pain_de_campagne", out CraftEvent? pain));
        Assert.Equal(("item.pain_de_campagne", "cuisine", 20L), (pain!.Item, pain.Profession, pain.Xp));
        Assert.Equal((3, 0, 0), (bourg.Inventory.Count("item.pain_de_campagne"), bourg.Inventory.Count("item.farine_de_ble"), bourg.Inventory.Count("item.levain")));
        Assert.Equal((20L, 0L), (bourg.Workshop!.Xp("cuisine"), bourg.Character.Xp));
        Assert.Equal(CraftRefusal.NotEnough, bourg.Craft("recipe.cuisine.pain_de_campagne", out _));
        // Au-delà de deux niveaux d'écart, la matelote ne se tente pas.
        WorldSession apprentice = Play(Zone(-40, 0), null, new ItemStack("item.carpe_doree", 1), new ItemStack("item.cidre_du_verger", 2),
            new ItemStack("item.beurre_de_baratte", 1), new ItemStack("item.farine_de_ble", 1));
        Assert.NotEqual(CraftRefusal.None, apprentice.Craft("recipe.cuisine.matelote_de_carpe_doree", out _));
        Assert.Equal(1, apprentice.Inventory.Count("item.carpe_doree"));   // rien retiré
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(bourg.Save())).Save!;
        Assert.Equal(20L, saved.Character!.Professions["cuisine"]);
    }
}
