using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.4 : forge, qualité, usure. Catalogue des objets en version 5 (bonus des qualités, solidité et perte par combat), métiers en
/// version 2 (recettes à qualité, petit jeu de forge, recette au-dessus de son niveau, réparation), sauvegarde en version 11 (qualité,
/// signature, usure d'une instance ; compteur des fabrications), usure hors du moteur à la fin d'un combat, réparation atomique.
/// </summary>
[Trait("Category", "World")]
public class V14ForgeTests
{
    private static ItemCatalog Items => ItemsCatalog;

    /// <summary>Devant la forge de Maëlle (le lieu « forge »).</summary>
    private static (double X, double Z) AtForge => (-19, -5);

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, Items).Book!;

    private static CraftingLoadResult CraftingMutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return CraftingJsonReader.Read(root.ToJsonString(), Texts, Zone(), Items);
    }

    private static ItemCatalogLoadResult ItemsMutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static string FirstPath(ValidationReport report) => Assert.Single(report.Errors.Take(1)).Path;

    private static SavedCharacter Smith(long forgeXp, long money = 0, params ItemStack[] bag)
    {
        SavedCharacter hero = Inventory.Outfit(Hero(money), Items).WithItems(bag, Inventory.Outfit(Hero(money), Items).Equipment
            .ToDictionary(e => e.Key, e => new ItemStack(e.Value, 1)));
        return forgeXp > 0 ? hero.WithProfessions(new Dictionary<string, long> { ["forge"] = forgeXp }) : hero;
    }

    private static WorldSession Open(ZoneDefinition zone, SavedCharacter hero)
    {
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items,
            Book(zone), Crafting(zone), market: new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    private static ItemStack[] Ore(int ore, int ferrures) => new[] { new ItemStack("item.minerai_de_fer", ore), new ItemStack("item.ferrure", ferrures) };

    // --- Catalogue des objets (version 5) ------------------------------------------------------------------------------------

    [Fact]
    public void Catalogue_en_version_5_avec_qualite_et_usure()
    {
        Assert.Equal(7, Items.SchemaVersion);   // sous-lot V1.8c : version 7 (objets de bonus) ; V1.8 : version 6, résistance à l'usure ; 3 points par PV maximaux (V18 : 30 à 40 combats)
        Assert.Equal((10, 20, 100, 3), (Items.SuperiorPercent, Items.MasterworkPercent, Items.WearMax, Items.WearPerFullHealth));
        ItemDefinition sword = Items.Item("item.epee_de_garde");   // puissance physique 22
        Assert.True(Items.TakesQuality(sword));
        Assert.Equal(2, Items.QualityBonus(sword, ItemQuality.Superior).PhysicalPower);     // 10 % de 22, vers le bas
        Assert.Equal(4, Items.QualityBonus(sword, ItemQuality.Masterwork).PhysicalPower);   // 20 % de 22
        Assert.Equal(0, Items.QualityBonus(sword, ItemQuality.Superior).Armor);              // une statistique nulle reste nulle
        Assert.Equal(GearBonus.None, Items.QualityBonus(sword, ItemQuality.Normal));
        // Au moins +1 par statistique non nulle : des gants de 10 d'armure gagnent 1 (10 %), un chef-d'œuvre 2.
        Assert.Equal((1, 2), (Items.QualityBonus(Items.Item("item.gants_de_cuir"), ItemQuality.Superior).Armor,
            Items.QualityBonus(Items.Item("item.gants_de_cuir"), ItemQuality.Masterwork).Armor));
        // Un objet nommé, l'Éclat du Seuil et l'outil de métier ne prennent pas de qualité ; l'Éclat et l'outil ne s'usent pas.
        Assert.False(Items.TakesQuality(Items.Item("item.lame_du_seuil")));
        Assert.True(Items.Wears(Items.Item("item.lame_du_seuil")));
        Assert.False(Items.TakesQuality(Items.Item("item.eclat_du_seuil_endormi")) || Items.Wears(Items.Item("item.eclat_du_seuil_endormi")));
        Assert.False(Items.Wears(Items.Item("item.marteau_de_forge")) || Items.Wears(Items.Item("item.minerai_de_fer")));
        Assert.Equal(26, Items.BonusOf(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "bastion")).PhysicalPower);
    }

    [Fact]
    public void Pire_cumul_compte_le_chef_d_oeuvre_et_reste_dans_la_limite_du_combat()
    {
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long baseBound, long rolled, long sets) = Items.WornBound(stat);
            long plain = Items.Places.Sum(p => (long)Items.Items.Where(i => i.Type == ItemType.Equipment && i.Slot == p.Accepts)
                .Select(i => i.Bonus[stat] + Items.QualityBonus(i, ItemQuality.Masterwork)[stat]).DefaultIfEmpty(0).Max());
            Assert.True(baseBound + rolled >= plain, GearBonus.Fields[stat]);
            Assert.True(baseBound + rolled + sets <= GearBonus.MaxValue);
        }
        // Une part de qualité qui mènerait le pire cumul au-delà de la limite est refusée par le validateur unique : les bonus de base les
        // plus forts portés à 50 000 et un chef-d'œuvre à 100 % dépassent 1 000 000 sur vingt places.
        ItemCatalogLoadResult loud = ItemsMutated(r =>
        {
            foreach (JsonNode? item in r["data"]!["items"]!.AsArray())
                if (item!["type"]!.GetValue<string>() == "equipment" && item["slot"]!.GetValue<string>() is not ("shard" or "tool")) item["bonus"] = new JsonObject { ["armor"] = 50_000 };
            r["data"]!["quality"]!["masterwork"] = 100;
        });
        Assert.False(loud.IsValid);
        Assert.Contains(loud.Report.Errors, e => e.Code == ValidationCode.LimitExceeded && e.Message.Contains("armor"));
    }

    [Theory]
    [InlineData("superior", -1, "$.data.quality.superior", ValidationCode.OutOfRange)]
    [InlineData("superior", 101, "$.data.quality.superior", ValidationCode.OutOfRange)]
    [InlineData("masterwork", 5, "$.data.quality.masterwork", ValidationCode.InvalidValue)]   // sous la qualité supérieure
    [InlineData("max", 0, "$.data.wear.max", ValidationCode.OutOfRange)]
    [InlineData("perFullHealth", 101, "$.data.wear.perFullHealth", ValidationCode.OutOfRange)]   // au-delà de la solidité
    public void Qualite_et_usure_du_catalogue_bornees(string field, int value, string path, ValidationCode code)
    {
        ItemCatalogLoadResult r = ItemsMutated(root => root["data"]![field is "max" or "perFullHealth" ? "wear" : "quality"]![field] = value);
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal((path, code), (e.Path, e.Code));
    }

    [Fact]
    public void Catalogue_sans_qualite_ou_d_une_version_anterieure_refuse()
    {
        Assert.Equal("$.data.quality", Assert.Single(ItemsMutated(r => r["data"]!.AsObject().Remove("quality")).Report.Errors).Path);
        Assert.Equal("$.data.wear", Assert.Single(ItemsMutated(r => r["data"]!.AsObject().Remove("wear")).Report.Errors).Path);
        Assert.Equal("$.schemaVersion", ItemsMutated(r => r["schemaVersion"] = 4).Report.Errors[0].Path);
    }

    // --- Instances : qualité et usure ----------------------------------------------------------------------------------------

    [Fact]
    public void Validateur_unique_des_instances_pour_la_qualite_et_l_usure()
    {
        Assert.Null(Items.CheckInstance(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Superior)));
        Assert.Null(Items.CheckInstance(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "bastion", wear: 0)));
        Assert.Null(Items.CheckInstance(new ItemStack("item.lame_du_seuil", 1, wear: 99)));   // un objet nommé s'use
        Assert.Contains("qualité", Items.CheckInstance(new ItemStack("item.lame_du_seuil", 1, null, ItemQuality.Superior)));
        Assert.Contains("ne s'use pas", Items.CheckInstance(new ItemStack("item.eclat_du_seuil_endormi", 1, wear: 3)));
        Assert.Contains("usure pleine", Items.CheckInstance(new ItemStack("item.epee_de_garde", 1, wear: 100)));
        Assert.Contains("quantité 1", Items.CheckInstance(new ItemStack("item.minerai_de_fer", 2, wear: 3)));
        Assert.Contains("signature", Items.CheckInstance(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "Maëlle")));
        Assert.Throws<ArgumentException>(() => new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork));   // un chef-d'œuvre est signé
        Assert.Throws<ArgumentException>(() => new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Superior, "bastion"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStack("item.epee_de_garde", 1, wear: -1));
    }

    [Fact]
    public void Instance_accordee_au_catalogue_actuel_sans_partie_perdue()
    {
        // Usure au-delà de la solidité actuelle : pleine ; qualité d'un objet devenu nommé : normale (signature retirée avec elle).
        ItemStack worn = Items.Settle(new ItemStack("item.epee_de_garde", 1, wear: 250), out IReadOnlyList<string> notes);
        Assert.Equal((null, false), (worn.Wear, worn.IsUnique));
        Assert.Single(notes);
        ItemStack named = Items.Settle(new ItemStack("item.lame_du_seuil", 1, null, ItemQuality.Masterwork, "bastion", 40), out notes);
        Assert.Equal((ItemQuality.Normal, (string?)null, (int?)40), (named.Quality, named.SignedBy, named.Wear));
        Assert.Single(notes);
        ItemStack fine = new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Superior, null, 3);
        Assert.Same(fine, Items.Settle(fine, out notes));
        Assert.Empty(notes);
    }

    [Fact]
    public void Piece_usee_a_zero_ne_donne_plus_ses_bonus_ni_sa_garde_ni_son_effet_sans_se_casser()
    {
        SavedCharacter hero = Inventory.Outfit(Hero(), Items);
        Inventory fresh = Inventory.From(hero, Items, 20);
        GearBonus before = fresh.Gear();
        Assert.NotNull(fresh.Shield);
        var worn = hero.Equipment.ToDictionary(e => e.Key, e => e.Key == "shield" || e.Key == "weapon" ? new ItemStack(e.Value, 1, wear: 0) : new ItemStack(e.Value, 1));
        Inventory broken = Inventory.From(hero.WithItems(hero.Inventory, worn), Items, 20);
        Assert.Null(broken.Shield);   // la garde des règles
        Assert.Equal(before.PhysicalPower - Items.Item("item.epee_du_novice").Bonus.PhysicalPower, broken.Gear().PhysicalPower);
        Assert.Equal(before.Armor - Items.Item("item.ecu_du_novice").Bonus.Armor, broken.Gear().Armor);
        Assert.Equal("item.ecu_du_novice", broken.WornAt("shield"));   // toujours porté
        // Un objet nommé à effet, usé à zéro : plus d'effet.
        var lame = new Dictionary<string, ItemStack>(worn) { ["weapon"] = new ItemStack("item.lame_du_seuil", 1, wear: 0) };
        Assert.Empty(Inventory.From(hero.WithItems(hero.Inventory, lame), Items, 20).Effects());
        lame["weapon"] = new ItemStack("item.lame_du_seuil", 1, wear: 1);
        Assert.Single(Inventory.From(hero.WithItems(hero.Inventory, lame), Items, 20).Effects());
    }

    [Fact]
    public void Instance_unique_ne_s_empile_pas_et_ne_part_pas_a_la_place_d_une_copie_de_base()
    {
        var inventory = new Inventory(Items);
        Assert.Equal(InventoryRefusal.None, inventory.Add(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Superior)));
        Assert.Equal(InventoryRefusal.None, inventory.Add("item.epee_de_garde", 1));
        Assert.Equal(InventoryRefusal.None, inventory.Remove("item.epee_de_garde", 1, out IReadOnlyList<ItemStack> removed));
        Assert.False(Assert.Single(removed).IsUnique);   // la copie de base part d'abord
        Assert.Equal(ItemQuality.Superior, Assert.Single(inventory.Stacks).Quality);
    }

    // --- Métiers (version 2) ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Metiers_en_version_2_forge_echec_et_reparation()
    {
        CraftingBook book = Crafting(Zone());
        Assert.Equal(3, book.SchemaVersion);   // sous-lot V1.8 : version 3 (expérience d'un échec)
        Assert.Equal(5, book.Recipes.Count(r => r.Quality));
        Assert.All(book.Recipes.Where(r => r.Quality), r => Assert.Equal(("forge", "forge", 1), (r.Profession, r.Place, r.Output.Count)));
        Assert.Equal(3, book.Forge.Strikes);
        Assert.Equal((12, 60), book.Forge.Chances(100, 0));   // frappes parfaites, au niveau requis
        Assert.Equal((0, 15), book.Forge.Chances(0, 0));
        Assert.Equal((2, 35, 25, 50), (book.Failure.MaxLevels, book.Failure.PercentPerLevel, book.Failure.SkillReduction, book.Failure.RefundPercent));
        Assert.Equal((0, 35, 10, 70, 45), (book.Failure.FailPercent(0, 0), book.Failure.FailPercent(1, 0), book.Failure.FailPercent(1, 100),
            book.Failure.FailPercent(2, 0), book.Failure.FailPercent(2, 100)));
        Assert.Equal(new[] { "forge" }, book.Repair.Places);
        Assert.Equal(new[] { "merchant.armurier" }, book.Repair.Merchants);
        // Prix : 30 % de la valeur au prorata de ce qui manque, arrondi vers le haut, au moins 5 cuivre.
        Assert.Equal((0L, 54L, 75L, 5L, 5L), (book.Repair.Price(300, 0, 100), book.Repair.Price(300, 60, 100), book.Repair.Price(250, 100, 100),
            book.Repair.Price(10, 1, 100), book.Repair.Price(0, 50, 100)));
    }

    [Theory]
    [InlineData("forge.strikes", 0, "$.data.forge.strikes")]
    [InlineData("forge.superior.base", 101, "$.data.forge.superior.base")]
    [InlineData("failure.maxLevels", 10, "$.data.failure.maxLevels")]   // le niveau maximal d'un métier est 10
    [InlineData("failure.refundPercent", -5, "$.data.failure.refundPercent")]
    [InlineData("repair.minimum", -1, "$.data.repair.minimum")]
    public void Regles_de_la_forge_bornees(string field, int value, string path)
    {
        string[] parts = field.Split('.');
        CraftingLoadResult r = CraftingMutated(root =>
        {
            JsonNode node = root["data"]!;
            for (int i = 0; i < parts.Length - 1; i++) node = node[parts[i]]!;
            node[parts[^1]] = value;
        });
        Assert.Equal(path, Assert.Single(r.Report.Errors).Path);
    }

    [Fact]
    public void Recette_a_qualite_un_seul_equipement_qui_en_prend()
    {
        JsonObject Recipe(JsonObject root, string id) => root["data"]!["recipes"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["id"]!.GetValue<string>() == id);
        Assert.Equal("$.data.recipes[0].quality", FirstPath(CraftingMutated(r => Recipe(r, "recipe.ferrure")["quality"] = true).Report));   // un matériau
        Assert.Equal("$.data.recipes[3].quality", FirstPath(CraftingMutated(r => Recipe(r, "recipe.epee_de_garde")["output"]!["item"] = "item.lame_du_seuil").Report));
        Assert.Equal("$.data.repair.places[0]", FirstPath(CraftingMutated(r => r["data"]!["repair"]!["places"] = new JsonArray("cave")).Report));
        Assert.Equal("$.data.repair.merchants[0]", FirstPath(CraftingMutated(r => r["data"]!["repair"]!["merchants"] = new JsonArray("armurier")).Report));
        Assert.Equal("$.data.repair", FirstPath(CraftingMutated(r =>
        {
            r["data"]!["repair"]!["places"] = new JsonArray();
            r["data"]!["repair"]!["merchants"] = new JsonArray();
        }).Report));
        Assert.Equal("$.schemaVersion", CraftingMutated(r => r["schemaVersion"] = 1).Report.Errors[0].Path);
        Assert.Equal("$.data.forge", FirstPath(CraftingMutated(r => r["data"]!.AsObject().Remove("forge")).Report));
    }

    // --- Petit jeu de forge, qualité, échec -----------------------------------------------------------------------------------

    /// <summary>Atelier et inventaire de départ pour une forge d'expérience <paramref name="xp"/>, avec ses matériaux.</summary>
    private static (Workshop Shop, Inventory Bag) Bench(long xp, int ore = 10, int ferrures = 3)
    {
        var shop = new Workshop(Crafting(Zone()), xp > 0 ? new Dictionary<string, long> { ["forge"] = xp } : new Dictionary<string, long>());
        var bag = new Inventory(Items);
        bag.Add("item.minerai_de_fer", ore);
        bag.Add("item.ferrure", ferrures);
        return (shop, bag);
    }

    [Fact]
    public void Les_frappes_sont_une_entree_du_joueur_controlee()
    {
        (Workshop shop, Inventory bag) = Bench(40);
        string before = string.Join(",", bag.Stacks);
        Assert.Equal(CraftRefusal.BadStrikes, shop.Craft("recipe.epee_de_garde", true, bag, null, "bastion", 0, 0, out _));
        Assert.Equal(CraftRefusal.BadStrikes, shop.Craft("recipe.epee_de_garde", true, bag, new ForgeStrikes(100, 100), "bastion", 0, 0, out _));
        Assert.Equal(CraftRefusal.BadStrikes, shop.Craft("recipe.epee_de_garde", true, bag, new ForgeStrikes(100, 101, 0), "bastion", 0, 0, out _));
        Assert.Equal(CraftRefusal.BadStrikes, shop.Craft("recipe.epee_de_garde", true, bag, new ForgeStrikes(-1, 50, 0), "bastion", 0, 0, out _));
        Assert.Equal(CraftRefusal.BadStrikes, shop.Craft("recipe.ferrure", true, bag, new ForgeStrikes(50, 50, 50), "bastion", 0, 0, out _));   // sans petit jeu
        Assert.Equal(before, string.Join(",", bag.Stacks));   // rien n'a changé
        Assert.Equal(CraftRefusal.None, shop.Check("recipe.epee_de_garde", true, bag));   // l'atelier juge la recette avant le petit jeu
        Assert.Equal(66, new ForgeStrikes(100, 99, 0).Skill);
    }

    [Fact]
    public void Qualite_tiree_par_la_cle_stable_et_montee_par_l_adresse()
    {
        int Masterworks(int note, out int superiors)
        {
            int masters = 0;
            superiors = 0;
            for (int counter = 0; counter < 300; counter++)
            {
                (Workshop shop, Inventory bag) = Bench(40);
                Assert.Equal(CraftRefusal.None, shop.Craft("recipe.epee_de_garde", true, bag, new ForgeStrikes(note, note, note), "bastion", 2, counter, out CraftEvent? e));
                Assert.Equal(CraftEventKind.Crafted, e!.Kind);
                ItemStack made = bag.Stacks.Single(s => s.Item == "item.epee_de_garde");
                Assert.Equal(e.Quality, made.Quality);
                Assert.Null(Items.CheckInstance(made));
                if (made.Quality == ItemQuality.Masterwork)
                {
                    masters++;
                    Assert.Equal("bastion", made.SignedBy);   // signé du nom du personnage
                }
                if (made.Quality == ItemQuality.Superior) superiors++;
            }
            return masters;
        }
        int clumsy = Masterworks(0, out int clumsySuperior), perfect = Masterworks(100, out int perfectSuperior);
        Assert.Equal(0, clumsy);
        Assert.InRange(perfect, 15, 60);              // 12 % de 300
        Assert.InRange(clumsySuperior, 20, 75);       // 15 %
        Assert.InRange(perfectSuperior, 140, 220);    // 60 %
        // La même entrée donne toujours le même résultat ; la clé change avec la recette, le jour et le compteur.
        ItemQuality Once(long day, long counter)
        {
            (Workshop shop, Inventory bag) = Bench(40);
            shop.Craft("recipe.epee_de_garde", true, bag, new ForgeStrikes(80, 90, 70), "bastion", day, counter, out CraftEvent? e);
            Assert.Equal(Workshop.CraftKey("bastion", "recipe.epee_de_garde", day, counter), e!.Key);
            return e.Quality;
        }
        for (int c = 0; c < 20; c++) Assert.Equal(Once(3, c), Once(3, c));
        Assert.Equal("bastion/craft/recipe.epee_de_garde/day/3/12", Workshop.CraftKey("bastion", "recipe.epee_de_garde", 3, 12));
        Assert.True(SaveValidator.IsEventKey(Workshop.CraftKey("bastion", "recipe.epee_de_garde", 3, 12)));
    }

    [Fact]
    public void Recette_au_dessus_de_son_niveau_peut_rater_et_rend_une_partie_des_materiaux()
    {
        // Forge niveau 1 ; le casque de fer demande le niveau 3 (deux niveaux de plus : 70 % de rater sans adresse) ; les gantelets le 4.
        (Workshop first, Inventory refused) = Bench(0);
        Assert.Equal(CraftRefusal.LevelTooLow, first.Craft("recipe.gantelets_de_fer", true, refused, new ForgeStrikes(100, 100, 100), "bastion", 0, 0, out _));
        Assert.Equal((70, 0, 15), first.Chances(first.Book.Recipe("recipe.casque_de_fer"), 0));
        Assert.Equal(45, first.Chances(first.Book.Recipe("recipe.casque_de_fer"), 100).Fail);
        // Relecture indépendante de la PR #80 : sans petit jeu (herboristerie), l'adresse ne change pas la chance de rater.
        Assert.Equal(first.Chances(first.Book.Recipe("recipe.remede_de_l_aube"), 0), first.Chances(first.Book.Recipe("recipe.remede_de_l_aube"), 100));
        Assert.Equal(70, first.Chances(first.Book.Recipe("recipe.remede_de_l_aube"), 100).Fail);
        bool failed = false, succeeded = false;
        for (int counter = 0; counter < 40 && !(failed && succeeded); counter++)
        {
            (Workshop shop, Inventory bag) = Bench(0, ore: 5, ferrures: 1);
            Assert.Equal(CraftRefusal.None, shop.Craft("recipe.casque_de_fer", true, bag, new ForgeStrikes(0, 0, 0), "bastion", 0, counter, out CraftEvent? e));
            Assert.Equal(70, e!.FailPercent);
            if (e.Kind == CraftEventKind.Failed)
            {
                failed = true;
                // Cinq minerais et une ferrure pris ; la moitié rendue, vers le bas : deux minerais, aucune ferrure. Sous-lot V1.8 (réponse
                // de Damien) : un quart de l'expérience de la recette (60 : 15).
                Assert.Equal(new[] { ("item.minerai_de_fer", 2) }, e.Refunded);
                Assert.Equal((2, 0, 0, 15L), (bag.Count("item.minerai_de_fer"), bag.Count("item.ferrure"), bag.Count("item.casque_de_fer"), shop.Xp("forge")));
                Assert.Equal((0, 15L), (e.Count, e.Xp));
            }
            else
            {
                succeeded = true;
                Assert.Equal((0, 0, 1, 60L), (bag.Count("item.minerai_de_fer"), bag.Count("item.ferrure"), bag.Count("item.casque_de_fer"), shop.Xp("forge")));
            }
        }
        Assert.True(failed && succeeded);
    }

    [Fact]
    public void Forge_dans_la_partie_compteur_garde_et_resultat_reproductible()
    {
        ZoneDefinition zone = Zone(AtForge.X, AtForge.Z);
        WorldSession w = Open(zone, Smith(40, 0, Ore(10, 3)));
        Assert.Equal(0L, w.Character.Crafts);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.epee_de_garde", new ForgeStrikes(100, 100, 100), out CraftEvent? first));
        Assert.Equal(CraftRefusal.BadStrikes, w.Craft("recipe.epee_de_garde", out _));   // sans le petit jeu
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.Equal(2L, w.Character.Crafts);   // seules les tentatives admises comptent
        Assert.Equal(Workshop.CraftKey("bastion", "recipe.epee_de_garde", w.Zone.Time.Day, 0), first!.Key);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal(2L, saved.Character!.Crafts);
        Assert.Equal(first.Quality, saved.Character.Inventory.Single(s => s.Item == "item.epee_de_garde").Quality);
        // Rejouée sur la même sauvegarde de départ, la même forge donne la même pièce.
        WorldSession again = Open(zone, Smith(40, 0, Ore(10, 3)));
        again.Craft("recipe.epee_de_garde", new ForgeStrikes(100, 100, 100), out CraftEvent? replay);
        Assert.Equal((first.Quality, first.Key), (replay!.Quality, replay.Key));
        // Loin de la forge : refusée, rien ne change, le compteur non plus.
        WorldSession away = Open(Zone(), Smith(40, 0, Ore(10, 3)));
        Assert.Equal(CraftRefusal.NotHere, away.Craft("recipe.epee_de_garde", new ForgeStrikes(100, 100, 100), out _));
        Assert.Equal(0L, away.Character.Crafts);
    }

    // --- Usure -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chaque_combat_use_l_equipement_porte_selon_les_degats_recus()
    {
        // Un loup robuste au contact du Bastion, qui le frappe ; le Bastion le frappe jusqu'à sa mort.
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 400;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        SavedCharacter hero = Inventory.Outfit(Hero(), Items);
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), loaded.Setup!, ShippedData, Progression, Items,
            new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        long maxHealth = w.Zone.Engine.View("bastion").MaxHealth!.Value;
        long lowest = maxHealth;
        WearReport? report = null;
        WorldSave? midFight = null;
        for (int i = 0; i < 4000 && report is null; i++)
        {
            if (i % 20 == 1)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
            if (w.Zone.Engine.View("bastion").Health is long h) lowest = Math.Min(lowest, h);
            if (midFight is null && w.PendingDamage > 0) midFight = w.Save();
            report = step.Wear;
        }
        Assert.NotNull(report);
        Assert.True(report!.Damage > 0);
        // Relecture indépendante de la PR #80 : les dégâts sont ceux du compteur du moteur (un soin ne les cache pas).
        Assert.Equal(w.Zone.Engine.View("bastion").Counters.DamageTaken, report.Damage);
        Assert.Equal(0L, w.PendingDamage);
        // Relecture de la PR #83 (M3) : au début de la session, le premier point est arrondi vers le haut.
        Assert.Equal((int)((report.Damage * Items.WearPerFullHealth + maxHealth - 1) / maxHealth), report.Lost);
        // Chaque pièce portée qui s'use a perdu autant ; l'Éclat du Seuil ne s'use pas.
        int expected = Items.WearMax - report.Lost;
        Assert.Equal(report.Lost == 0 ? null : expected, w.Inventory.WornInstanceAt("weapon")!.Wear);
        Assert.Equal(report.Lost == 0 ? null : expected, w.Inventory.WornInstanceAt("shield")!.Wear);
        Assert.Null(w.Inventory.WornInstanceAt("shard")!.Wear);
        // Les bonus du combat en cours ne changent pas (entrée), ni le journal : l'usure est hors du moteur.
        Assert.Equal(w.EnteredGear, Inventory.From(hero, Items, 1).Gear());
        Assert.NotNull(midFight);   // une sauvegarde prise en plein combat compte déjà les dégâts reçus
    }

    [Fact]
    public void Changer_d_equipement_apres_l_entree_n_evite_pas_l_usure()
    {
        // Relecture indépendante de la PR #80 : le combat garde l'équipement d'entrée ; c'est lui que l'usure use. Un loup au contact, un
        // second qui arrive une minute plus tard ; entre les deux combats, l'épée d'entrée part au sac et une hachette la remplace.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            foreach (JsonObject wolf in root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Where(c => c["id"]!.GetValue<string>() is "loup1" or "loup2"))
            {
                wolf["health"] = 120;
                wolf["position"] = new JsonArray(-16, -12);
                if (wolf["id"]!.GetValue<string>() == "loup2") wolf["arrivalMs"] = 60_000;
            }
        }, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        long xp = 0;
        while (Progression.LevelFor(xp) < 2) xp++;   // la hachette demande le niveau 2
        SavedCharacter outfit = Inventory.Outfit(Hero().WithXp(xp), harsh);
        SavedCharacter hero = outfit.WithItems(new[] { new ItemStack("item.hachette_du_bucheron", 1) }, outfit.Equipment.ToDictionary(e => e.Key, e => new ItemStack(e.Value, 1)));
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), loaded.Setup!, ShippedData, Progression, harsh,
            new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        var reports = new List<WearReport>();
        bool swapped = false;
        for (int i = 0; i < 8000 && reports.Count < 2; i++)
        {
            string target = w.Zone.Engine.View("loup1").IsDead ? "loup2" : "loup1";
            if (i % 20 == 1)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", target)); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
            if (w.Step().Wear is WearReport r) reports.Add(r);
            if (reports.Count == 1 && !swapped && !w.InCombat)
            {
                Assert.Equal(InventoryRefusal.None, w.Equip(0));   // la hachette prend la place de l'épée, qui part au sac
                Assert.Equal("item.hachette_du_bucheron", w.Inventory.WornAt("weapon"));
                swapped = true;
            }
        }
        Assert.True(swapped);
        Assert.Equal(2, reports.Count);
        Assert.True(reports[1].Lost > 0);
        // L'épée d'entrée, au sac, a perdu les deux usures ; la hachette, portée seulement après l'entrée, rien.
        ItemStack sword = w.Inventory.Stacks.Single(x => x.Item == "item.epee_du_novice");
        Assert.Equal(Math.Max(0, harsh.WearMax - reports[0].Lost - reports[1].Lost), harsh.WearLeft(sword));
        Assert.Null(w.Inventory.WornInstanceAt("weapon")!.Wear);
        // Le bouclier, resté porté, a perdu les deux.
        Assert.Equal(Math.Max(0, harsh.WearMax - reports[0].Lost - reports[1].Lost), harsh.WearLeft(w.Inventory.WornInstanceAt("shield")!));
    }

    [Fact]
    public void Bouclier_use_a_zero_jamais_meilleur_que_sa_propre_garde()
    {
        // Relecture indépendante de la PR #80 : la garde de repli prend, pour chaque valeur, la plus faible de la sienne et de celle des règles.
        CombatRules rules = ShippedData.Rules.Combat;
        SavedCharacter hero = Inventory.Outfit(Hero(), Items);
        foreach (string shield in Items.Items.Where(i => i.Guard != null).Select(i => i.Id))
        {
            var worn = hero.Equipment.ToDictionary(e => e.Key, e => e.Key == "shield" ? new ItemStack(shield, 1, wear: 0) : new ItemStack(e.Value, 1));
            Inventory inventory = Inventory.From(hero.WithItems(hero.Inventory, worn), Items, 20);
            ShieldGuard own = Items.Item(shield).Guard!, fallback = inventory.ShieldFor(rules)!;
            Assert.True(fallback.DamageReduction <= own.DamageReduction && fallback.DamageReduction <= Fixed.Max(rules.GuardDamageReduction, rules.ShieldMinDamageReduction), shield);
            Assert.True(fallback.CostPerBlock >= own.CostPerBlock, shield);
            Assert.True(fallback.ParryWindowMs <= own.ParryWindowMs, shield);
            Assert.Empty(fallback.Check(rules, "shield"));   // toujours admise par le combat
            var fresh = hero.Equipment.ToDictionary(e => e.Key, e => e.Key == "shield" ? new ItemStack(shield, 1, wear: 1) : new ItemStack(e.Value, 1));
            Assert.Equal(own, Inventory.From(hero.WithItems(hero.Inventory, fresh), Items, 20).ShieldFor(rules));
        }
        // La garde de repli entre au combat.
        var broken = hero.Equipment.ToDictionary(e => e.Key, e => e.Key == "shield" ? new ItemStack(e.Value, 1, wear: 0) : new ItemStack(e.Value, 1));
        WorldSession w = Open(Zone(), hero.WithItems(hero.Inventory, broken));
        Assert.Equal(Inventory.From(hero.WithItems(hero.Inventory, broken), Items, 1).ShieldFor(rules), w.EnteredShield);
    }

    [Fact]
    public void Usure_forte_sauvegardee_et_relue()
    {
        // Le catalogue réglé à 100 points par PV maximaux : une usure visible dès le premier coup.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        var inventory = Inventory.From(Inventory.Outfit(Hero(), harsh), harsh, 1);
        // Relecture de la PR #83 (M4) : les pièces portées s'usent par WearPieces, la seule usure de la partie.
        IReadOnlyList<ItemStack> worn = inventory.Places().Select(p => inventory.WornInstanceAt(p)).OfType<ItemStack>().Where(x => harsh.Wears(harsh.Item(x.Item))).ToList();
        worn = inventory.WearPieces(worn, 150, out IReadOnlyList<string> zero);
        Assert.Equal(new[] { "item.epee_du_novice", "item.ecu_du_novice" }, zero.OrderBy(x => x, StringComparer.Ordinal).Reverse());
        Assert.Equal(0, inventory.WornInstanceAt("weapon")!.Wear);
        inventory.WearPieces(worn, 5, out zero);
        Assert.Empty(zero);   // déjà à zéro : rien de plus, rien ne casse
        WorldSave save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), inventory.SaveInto(Hero()));
        string text = SaveJsonReader.Write(save);
        Assert.Contains("\"weapon\": {\"item\": \"item.epee_du_novice\", \"wear\": 0}", text);
        Assert.Equal(0, SaveJsonReader.Read(text).Save!.Character!.WornInstance("weapon")!.Wear);
    }

    // --- Réparation --------------------------------------------------------------------------------------------------------

    private static SavedCharacter Damaged(long money)
    {
        // Une partie d'avant les réponses de Damien du 31 octobre 2026 : l'épée du milicien et l'écu de chêne (l'équipement du novice vaut
        // 1 cuivre, sa réparation coûte le minimum).
        SavedCharacter hero = Hero(money).WithItems(Array.Empty<ItemStack>(), new Dictionary<string, string>
            { ["weapon"] = "item.epee_du_milicien", ["shield"] = "item.ecu_de_chene", ["shard"] = "item.eclat_du_seuil_endormi" });
        var worn = hero.Equipment.ToDictionary(e => e.Key, e => e.Key switch
        {
            "weapon" => new ItemStack(e.Value, 1, wear: 40),   // 60 points manquent
            "shield" => new ItemStack(e.Value, 1, wear: 0),    // usé à zéro
            _ => new ItemStack(e.Value, 1),
        });
        return hero.WithItems(new[] { new ItemStack("item.gambison", 1, wear: 90), new ItemStack("item.minerai_de_fer", 4) }, worn);
    }

    private static long Expected()
    {
        RepairRules rules = Crafting(Zone()).Repair;
        return rules.Price(ShippedMerchants.Values["item.epee_du_milicien"], 60, 100) + rules.Price(ShippedMerchants.Values["item.ecu_de_chene"], 100, 100)
            + rules.Price(ShippedMerchants.Values["item.gambison"], 10, 100);
    }

    [Fact]
    public void Reparer_la_copie_du_sac_ne_touche_pas_la_piece_portee()
    {
        // Relecture indépendante de la PR #83 (M1) : deux épées identiques (même usure), l'une portée, l'autre dans le sac. Réparer celle du
        // sac ne remet pas à neuf l'entrée de celle qui est portée : au combat suivant, c'est toujours l'épée portée qui s'use.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        ZoneDefinition forge = Zone(AtForge.X, AtForge.Z, s =>
        {
            JsonObject wolf = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 400;
            wolf["position"] = new JsonArray(-15, -4);
            wolf["arrivalMs"] = 20_000;   // après la réparation
        });
        var used = new ItemStack("item.epee_du_novice", 1, wear: 50);
        SavedCharacter outfit = Inventory.Outfit(Hero(10_000), harsh);
        SavedCharacter hero = outfit.WithItems(new[] { used }, outfit.Equipment.ToDictionary(e => e.Key, e => e.Key == "weapon" ? used : new ItemStack(e.Value, 1)));
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), forge, ShippedData, Progression, harsh, Book(forge),
            Crafting(forge), market: new Market(ShippedMerchants));
        w.Step();
        int place = w.Inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        Assert.Equal(RepairRefusal.None, w.RepairPiece(null, null, place, out long paid));
        Assert.True(paid >= 1);
        Assert.Null(w.Inventory.Stacks[place].Wear);   // celle du sac, à neuf
        Assert.Equal(50, w.Inventory.WornInstanceAt("weapon")!.Wear);   // la portée, inchangée
        WearReport? report = null;
        for (int i = 0; i < 6000 && report is null; i++)
        {
            if (i % 20 == 1)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
            report = w.Step().Wear;
        }
        Assert.NotNull(report);
        Assert.True(report!.Lost > 0);
        Assert.Equal(Math.Max(0, 50 - report.Lost), harsh.WearLeft(w.Inventory.WornInstanceAt("weapon")!));   // la portée s'use
        Assert.Null(w.Inventory.Stacks.Single(x => x.Item == "item.epee_du_novice").Wear);   // celle du sac reste à neuf
    }

    /// <summary>Forge, loup robuste qui arrive après 20 s ; le héros porte une épée entamée (50) et a, au besoin, une copie identique au sac.</summary>
    private WorldSession ForgeWithWolf(ItemCatalog catalog, bool copyInBag, int? wear = 50)
    {
        ZoneDefinition forge = Zone(AtForge.X, AtForge.Z, s =>
        {
            JsonObject wolf = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 400;
            wolf["position"] = new JsonArray(-15, -4);
            wolf["arrivalMs"] = 20_000;
        });
        SavedCharacter outfit = Inventory.Outfit(Hero(10_000), catalog);
        ItemStack[] bag = copyInBag ? new[] { new ItemStack("item.epee_du_novice", 1, wear: wear) } : Array.Empty<ItemStack>();
        SavedCharacter hero = outfit.WithItems(bag, outfit.Equipment.ToDictionary(e => e.Key, e => e.Key == "weapon" ? new ItemStack(e.Value, 1, wear: wear) : new ItemStack(e.Value, 1)));
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), forge, ShippedData, Progression, catalog, Book(forge),
            Crafting(forge), market: new Market(ShippedMerchants));
        w.Step();
        return w;
    }

    private static WearReport FightWolf(WorldSession w)
    {
        WearReport? report = null;
        for (int i = 0; i < 6000 && report is null; i++)
        {
            if (i % 20 == 1)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
            report = w.Step().Wear;
        }
        Assert.NotNull(report);
        Assert.True(report!.Lost > 0);
        return report;
    }

    [Fact]
    public void Piece_du_combat_rangee_reparee_au_sac_puis_remise_s_use_encore()
    {
        // Remarque de Codex sur la PR #83 : l'épée du combat rangée dans le sac, réparée là (RepairPiece sans place portée), puis remise :
        // elle reste la pièce du combat et s'use au combat suivant.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        WorldSession w = ForgeWithWolf(harsh, copyInBag: false);
        Assert.Equal(InventoryRefusal.None, w.Unequip("weapon"));
        int place = w.Inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        Assert.Equal(RepairRefusal.None, w.RepairPiece(null, null, place, out _));
        Assert.Null(w.Inventory.Stacks[place].Wear);
        Assert.Equal(InventoryRefusal.None, w.Equip(place));
        Assert.Null(w.Inventory.WornInstanceAt("weapon")!.Wear);
        WearReport report = FightWolf(w);
        Assert.Equal(Math.Max(0, harsh.WearMax - report.Lost), harsh.WearLeft(w.Inventory.WornInstanceAt("weapon")!));
    }

    [Fact]
    public void Copie_identique_au_sac_jamais_prise_pour_la_piece_du_combat()
    {
        // Remarque de Codex sur la PR #83 : l'épée du combat (50) rangée dans le sac après une copie identique (50). Réparée au sac, puis
        // au combat suivant, c'est elle qui s'use ; la copie, jamais suivie, ne change pas.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        WorldSession w = ForgeWithWolf(harsh, copyInBag: true);
        Assert.Equal(InventoryRefusal.None, w.Unequip("weapon"));
        Assert.Equal(new int?[] { 50, 50 }, w.Inventory.Stacks.Where(x => x.Item == "item.epee_du_novice").Select(x => x.Wear));
        int entry = w.Inventory.Stacks.ToList().FindLastIndex(x => x.Item == "item.epee_du_novice");
        int copy = w.Inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        Assert.NotEqual(copy, entry);
        Assert.Equal(RepairRefusal.None, w.RepairPiece(null, null, entry, out _));
        WearReport report = FightWolf(w);
        Assert.Equal(50, w.Inventory.Stacks[copy].Wear);   // la copie n'est pas comptée
        Assert.Equal(Math.Max(0, harsh.WearMax - report.Lost), harsh.WearLeft(w.Inventory.Stacks[entry]));   // la pièce du combat s'use
    }

    [Theory]
    [InlineData(false)]   // pièce neuve (sans usure ni propriété : une pile ordinaire)
    [InlineData(true)]    // pièce entamée, réparée au sac (redevenue ordinaire)
    public void Piece_du_combat_rangee_triee_puis_remise_s_use_encore(bool repairInBag)
    {
        // Remarque de Codex sur la PR #83 (tri) : le tri du sac ne recrée plus les objets ordinaires ; l'épée du combat, rangée puis triée,
        // reste la pièce du combat : remise, elle s'use au combat suivant.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        WorldSession w = ForgeWithWolf(harsh, copyInBag: false, wear: repairInBag ? 50 : null);
        Assert.Equal(InventoryRefusal.None, w.Unequip("weapon"));
        int place = w.Inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        if (repairInBag) Assert.Equal(RepairRefusal.None, w.RepairPiece(null, null, place, out _));
        Assert.Null(w.Inventory.Stacks[place].Wear);
        w.Inventory.Sort(InventorySort.Type);
        place = w.Inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        Assert.Equal(InventoryRefusal.None, w.Equip(place));
        WearReport report = FightWolf(w);
        Assert.Equal(Math.Max(0, harsh.WearMax - report.Lost), harsh.WearLeft(w.Inventory.WornInstanceAt("weapon")!));
    }

    [Fact]
    public void Copie_identique_triee_au_sac_jamais_prise_pour_la_piece_du_combat()
    {
        // Remarque de Codex sur la PR #83 (tri) : deux épées neuves identiques au sac (la copie, puis celle du combat), triées : au combat,
        // seule celle du combat s'use.
        ItemCatalog harsh = ItemsMutated(r => r["data"]!["wear"]!["perFullHealth"] = 100).Catalog!;
        WorldSession w = ForgeWithWolf(harsh, copyInBag: true, wear: null);
        Assert.Equal(InventoryRefusal.None, w.Unequip("weapon"));
        w.Inventory.Sort(InventorySort.Type);
        Assert.Equal(new int?[] { null, null }, w.Inventory.Stacks.Where(x => x.Item == "item.epee_du_novice").Select(x => x.Wear));
        WearReport report = FightWolf(w);
        // Tri stable : la copie (rangée la première) reste devant ; elle n'est pas comptée, l'épée du combat s'use.
        Assert.Equal(new int?[] { null, Math.Max(0, harsh.WearMax - report.Lost) },
            w.Inventory.Stacks.Where(x => x.Item == "item.epee_du_novice").Select(x => x.Wear));
    }

    [Fact]
    public void Les_operations_de_l_inventaire_gardent_l_identite_de_l_equipement()
    {
        // Remarque de Codex sur la PR #83 (tri) : tri, retrait d'une pile entière et rangement d'une instance gardent le même objet pour
        // l'équipement (pile de 1) ; les objets qui s'empilent se regroupent comme avant.
        var inventory = new Inventory(Items);
        var sword = new ItemStack("item.epee_du_novice", 1);
        Assert.Equal(InventoryRefusal.None, inventory.Add(sword));
        Assert.Same(sword, inventory.Stacks.Single(x => x.Item == "item.epee_du_novice"));
        inventory.Add("item.peau_de_loup", 2);
        inventory.Add("item.peau_de_loup", 3);
        inventory.Sort(InventorySort.Type);
        Assert.Same(sword, inventory.Stacks.Single(x => x.Item == "item.epee_du_novice"));
        Assert.Equal(5, inventory.Stacks.Single(x => x.Item == "item.peau_de_loup").Count);   // regroupées
        int place = inventory.Stacks.ToList().FindIndex(x => x.Item == "item.epee_du_novice");
        Assert.Equal(InventoryRefusal.None, inventory.RemoveAt(place, out ItemStack? removed));
        Assert.Same(sword, removed);
        Assert.Equal(InventoryRefusal.None, inventory.Add(removed!));
        Assert.Same(sword, inventory.Stacks.Single(x => x.Item == "item.epee_du_novice"));
        Assert.Equal(InventoryRefusal.None, inventory.Remove("item.epee_du_novice", 1, out IReadOnlyList<ItemStack> taken));
        Assert.Same(sword, Assert.Single(taken));
    }

    [Fact]
    public void Reparation_a_la_forge_contre_de_l_or_tout_ou_rien()
    {
        Assert.Equal(54 + 75, Expected() - Crafting(Zone()).Repair.Price(ShippedMerchants.Values["item.gambison"], 10, 100));
        ZoneDefinition forge = Zone(AtForge.X, AtForge.Z);
        // Pas assez d'or : rien ne change.
        WorldSession poor = Open(forge, Damaged(Expected() - 1));
        Assert.Equal(Expected(), poor.RepairPrice());
        string before = Snapshot(poor) + string.Join(",", poor.Inventory.Damaged().Select(d => d.Stack.ToString()));
        Assert.Equal(RepairRefusal.NotEnoughMoney, poor.Repair(null, out long nothing));
        Assert.Equal(0L, nothing);
        Assert.Equal(before, Snapshot(poor) + string.Join(",", poor.Inventory.Damaged().Select(d => d.Stack.ToString())));
        // Assez d'or : tout est réparé d'un coup, la bourse paie le prix.
        WorldSession rich = Open(forge, Damaged(Expected() + 7));
        Assert.Equal(3, rich.Inventory.Damaged().Count);
        Assert.Equal(RepairRefusal.None, rich.Repair(null, out long paid));
        Assert.Equal((Expected(), 7L), (paid, rich.Money));
        Assert.Empty(rich.Inventory.Damaged());
        Assert.Equal(RepairRefusal.NothingToRepair, rich.Repair(null, out _));
        Assert.Equal(0L, rich.RepairPrice());
        // Réparé, le bouclier retrouve sa garde à la prochaine entrée.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(rich.Save())).Save!;
        Assert.NotNull(Inventory.From(saved.Character!, Items, 1).Shield);
        // Loin de la forge et d'un marchand qui répare : refusé.
        Assert.Equal(RepairRefusal.NotHere, Open(Zone(), Damaged(10_000)).Repair(null, out _));
    }

    [Fact]
    public void Reparation_chez_l_armurier_seulement_parmi_les_marchands()
    {
        (double x, double z) = Near("merchant.armurier");
        WorldSession w = Open(Zone(x, z), Damaged(10_000));
        Assert.Equal(RepairRefusal.Closed, w.Repair("merchant.herboriste", out _));   // l'herboriste ne répare pas
        Assert.Equal(RepairRefusal.NotHere, w.Repair(null, out _));                    // pas à la forge
        Assert.Equal(RepairRefusal.None, w.Repair("merchant.armurier", out long paid));
        Assert.Equal((Expected(), 10_000 - Expected()), (paid, w.Money));
        // Un marchand de réparation absent des marchands est refusé avant l'ouverture de la partie.
        CraftingBook unknown = CraftingJsonReader.Read(JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject()
            .Also(r => r["data"]!["repair"]!["merchants"] = new JsonArray("merchant.forgeron")).ToJsonString(), Texts, Zone(), Items).Book!;
        Assert.Throws<ArgumentException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Damaged(0)), Zone(), ShippedData,
            Progression, Items, Book(Zone()), unknown, market: new Market(ShippedMerchants)));
    }

    // --- Sauvegarde (version 11) -------------------------------------------------------------------------------------------

    private static string Written()
    {
        SavedCharacter hero = Damaged(5).WithCrafts(3);
        hero = hero.WithItems(hero.Inventory.Append(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "bastion"))
            .Append(new ItemStack("item.casque_de_fer", 1, null, ItemQuality.Superior, null, 12)), hero.EquipmentInstances.Count == 0
            ? hero.Equipment.ToDictionary(e => e.Key, e => new ItemStack(e.Value, 1)) : hero.Equipment.ToDictionary(e => e.Key, e => hero.WornInstance(e.Key)!));
        return SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 2, Array.Empty<string>(), hero));
    }

    [Fact]
    public void Sauvegarde_en_version_11_avec_qualite_usure_et_fabrications()
    {
        string text = Written();
        Assert.Contains(SaveVersion.Current + ",", text);
        Assert.Contains("{\"item\": \"item.epee_de_garde\", \"count\": 1, \"quality\": \"masterwork\", \"signedBy\": \"bastion\"}", text);
        Assert.Contains("{\"item\": \"item.casque_de_fer\", \"count\": 1, \"quality\": \"superior\", \"wear\": 12}", text);
        Assert.Contains("\"shield\": {\"item\": \"item.ecu_de_chene\", \"wear\": 0}", text);   // Damaged : une partie d'avant l'équipement du novice
        Assert.Contains("\"crafts\": 3,\n", text);
        SavedCharacter read = SaveJsonReader.Read(text).Save!.Character!;
        Assert.Equal(3L, read.Crafts);
        Assert.Equal((ItemQuality.Masterwork, "bastion"), (read.Inventory[2].Quality, read.Inventory[2].SignedBy));
        Assert.Equal(40, read.WornInstance("weapon")!.Wear);
        Assert.Equal(text, SaveJsonReader.Write(SaveJsonReader.Read(text).Save!));   // forme canonique, relue à l'identique
    }

    [Theory]
    [InlineData("\"quality\": \"masterwork\", \"signedBy\": \"bastion\"", "\"quality\": \"masterwork\"", "$.data.character.inventory[2].signedBy", ValidationCode.MissingField)]
    [InlineData("\"quality\": \"superior\", \"wear\": 12", "\"quality\": \"superior\", \"signedBy\": \"bastion\", \"wear\": 12", "$.data.character.inventory[3].signedBy", ValidationCode.InvalidValue)]
    [InlineData("\"quality\": \"superior\"", "\"quality\": \"normal\"", "$.data.character.inventory[3].quality", ValidationCode.InvalidValue)]
    [InlineData("\"wear\": 12", "\"wear\": -1", "$.data.character.inventory[3].wear", ValidationCode.OutOfRange)]
    [InlineData("\"signedBy\": \"bastion\"", "\"signedBy\": \"Bastion\"", "$.data.character.inventory[2].signedBy", ValidationCode.InvalidValue)]
    [InlineData("\"crafts\": 3,\n", "\"crafts\": -3,\n", "$.data.character.crafts", ValidationCode.OutOfRange)]
    [InlineData(",\n      \"crafts\": 3,\n", ",\n", "$.data.character.crafts", ValidationCode.MissingField)]
    [InlineData("{\"item\": \"item.minerai_de_fer\", \"count\": 4}", "{\"item\": \"item.minerai_de_fer\", \"count\": 4, \"wear\": 3}", "$.data.character.inventory[1].count", ValidationCode.InvalidValue)]
    public void Sauvegarde_de_qualite_ou_d_usure_invalide_refusee_avec_son_chemin(string from, string to, string path, ValidationCode code)
    {
        string text = Written();
        Assert.Contains(from, text);
        ValidationError e = SaveJsonReader.Read(text.Replace(from, to)).Report.Errors[0];
        Assert.Equal((path, code), (e.Path, e.Code));
    }

    [Fact]
    public void Ancienne_sauvegarde_relue_avec_usure_pleine_et_qualite_normale()
    {
        // Une sauvegarde de version 9 (V1.3) : ni qualité, ni usure, ni fabrications ; relue, mise à jour, usure pleine, compteur à 0.
        string current = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 2, Array.Empty<string>(), Inventory.Outfit(Hero(12), Items)));
        string v9 = SaveV11.Older(current).Replace(SaveVersion.Current, "\"schemaVersion\": 9");
        WorldSave old = SaveJsonReader.Read(v9).Save!;
        Assert.True(old.Migrated);
        Assert.Equal(0L, old.Character!.Crafts);
        Assert.All(old.Character.Equipment.Keys, slot => Assert.False(old.Character.WornInstance(slot)!.IsUnique));
        Assert.Equal(current, SaveJsonReader.Write(old));   // réécrite à la version courante
        // Version 9 avec un champ de la version 11 : refusé, champ inconnu.
        ValidationError wear = SaveJsonReader.Read(v9.Replace("\"weapon\": \"item.epee_du_novice\"", "\"weapon\": {\"item\": \"item.epee_du_novice\", \"wear\": 3}")).Report.Errors[0];
        Assert.Equal(("$.data.character.equipment.weapon.wear", ValidationCode.UnknownField), (wear.Path, wear.Code));
        ValidationError crafts = SaveJsonReader.Read(v9.Replace("\"money\": 12\n", "\"money\": 12,\n      \"crafts\": 1\n")).Report.Errors[0];
        Assert.Equal(("$.data.character.crafts", ValidationCode.UnknownField), (crafts.Path, crafts.Code));
    }

    [Fact]
    public void Sauvegarde_de_version_10_avec_sa_carte_de_guilde_relue_en_version_11()
    {
        // Étape de lecture 10 → 11 : une partie de V1.7 (carte d'aventurier, créatures rares) sans qualité, usure ni fabrications se relit,
        // carte comprise, puis se réécrit à la version courante avec un compteur à 0.
        Assert.Equal(SaveValidator.GuildCardSince + 1, SaveValidator.ItemStateSince);
        Assert.Equal(SaveValidator.ItemStateSince + 1, SaveValidator.BoonsSince);   // sous-lot V1.8c : version 12 (bonus actifs)
        Assert.Equal(SaveValidator.SectorsSince, SaveValidator.CurrentSchemaVersion);   // V2.26 : 18 (registre purgé, secteurs) ;   // V2.16 : 17 (aspect du héros) ;   // V2.10 : 16 (stock des marchands, concours des fêtes) ; V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements) ; V1.8c : 12 (bonus)
        SavedCharacter hero = Inventory.Outfit(Hero(12), Items)
            .WithStory(new SavedStory(guild: new SavedGuildCard("g", titles: new[] { "title.guilde.recrue" }), hunted: new[] { "loup_blanc" }));
        string current = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 2, Array.Empty<string>(), hero));
        string v10 = SaveV11.Older(current).Replace(SaveVersion.Current, $"\"schemaVersion\": {SaveValidator.GuildCardSince}");
        Assert.Contains("\"guild\": {", v10, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(v10);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.True(read.Save!.Migrated);
        SavedCharacter old = read.Save.Character!;
        Assert.Equal(("g", 0L), (old.Story.Guild!.Rank, old.Crafts));
        Assert.Equal(new[] { "loup_blanc" }, old.Story.Hunted);
        Assert.All(old.Equipment.Keys, slot => Assert.False(old.WornInstance(slot)!.IsUnique));
        Assert.Equal(current, SaveJsonReader.Write(read.Save));
        // En version 10, les champs de la version 11 sont inconnus.
        ValidationError wear = SaveJsonReader.Read(v10.Replace("\"weapon\": \"item.epee_du_novice\"", "\"weapon\": {\"item\": \"item.epee_du_novice\", \"wear\": 3}")).Report.Errors[0];
        Assert.Equal(("$.data.character.equipment.weapon.wear", ValidationCode.UnknownField), (wear.Path, wear.Code));
    }

    // --- Petit jeu (client) et fiche ---------------------------------------------------------------------------------------

    [Fact]
    public void Petit_jeu_de_forge_note_les_frappes_au_bon_moment()
    {
        Assert.Equal((100, 100, 0, 0), (ForgeGame.NoteAt(0.5), ForgeGame.NoteAt(0.5 + ForgeGame.PerfectHalfWidth), ForgeGame.NoteAt(0), ForgeGame.NoteAt(1)));
        for (double p = 0.5; p < 1; p += 0.05) Assert.True(ForgeGame.NoteAt(p) >= ForgeGame.NoteAt(p + 0.05));
        var game = new ForgeGame(3);
        game.Advance(ForgeGame.FirstSweepSeconds / 2);   // à mi-chemin : sur la cible
        Assert.Equal(100, game.Strike());
        game.Advance(10);   // au plus 10 s d'un coup ; le curseur reste sur la barre
        Assert.InRange(game.Position, 0, 1);
        game.Strike();
        game.Strike();
        Assert.True(game.Done);
        Assert.Null(game.Strike());
        Assert.Equal(3, game.Notes.Count);
        Assert.Equal(new ForgeStrikes(game.Notes).Skill, game.Skill);
        Assert.True(new ForgeStrikes(game.Notes).IsValidFor(3));
    }

    [Fact]
    public void Fiche_de_l_objet_avec_qualite_et_usure()
    {
        var inventory = Inventory.From(Inventory.Outfit(Hero(), Items), Items, 20);
        inventory.Add(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "bastion", 0));
        ItemComparison c = ItemComparison.For(inventory, inventory.Stacks[0])!;
        Assert.Equal("Chef-d'œuvre, signé Bastion : +20 % au bonus de base", c.Details[0].Text);
        Assert.Equal(("Usé : solidité 0 / 100, plus aucun bonus (à réparer)", CompareTrend.Worse), (c.Details[1].Text, c.Details[1].Trend));
        Assert.Empty(ItemComparison.For(inventory, "item.epee_de_garde")!.Details);   // neuve et de qualité normale : rien à dire
        inventory.Add(new ItemStack("item.casque_de_fer", 1, wear: 37));
        Assert.Equal("Solidité 37 / 100 (à réparer)", ItemComparison.For(inventory, inventory.Stacks.Last())!.Details.Single().Text);
    }
    [Fact]
    public void Comparaison_d_une_piece_usee_a_effet_comme_l_inventaire()
    {
        // Remarque de Codex sur la PR #80 : une Lame du Seuil usée à zéro, au sac, ne montre pas son effet comme gagné.
        var inventory = Inventory.From(Inventory.Outfit(Hero(), Items), Items, 20);
        inventory.Add(new ItemStack("item.lame_du_seuil", 1, wear: 0));
        DetailLine effect = ItemComparison.For(inventory, inventory.Stacks.Last())!.Details.Single(d => d.Text.Contains(Items.Item("item.lame_du_seuil").Effect!.Text));
        Assert.Equal((CompareTrend.Same, false), (effect.Trend, effect.Active));
        Assert.StartsWith("Effet (pièce usée, inactif)", effect.Text);
        // Neuve : l'effet s'ajoute (vert). Portée et usée : l'inventaire ne la compte pas, la fiche non plus.
        inventory.Add(new ItemStack("item.lame_du_seuil", 1));
        Assert.Equal(CompareTrend.Better, ItemComparison.For(inventory, inventory.Stacks.Last())!.Details.Single(d => d.Text.StartsWith("Effet :")).Trend);
        Assert.Equal(InventoryRefusal.None, inventory.Equip(inventory.Stacks.Count - 2, 20));   // la lame usée portée
        Assert.Empty(inventory.Effects());
        ItemComparison self = ItemComparison.For(inventory, inventory.WornInstanceAt("weapon")!, "weapon")!;
        Assert.False(self.Details.Single(d => d.Text.Contains("Effet")).Active);
    }

    [Fact]
    public void Comparaison_d_une_piece_d_ensemble_usee_comme_l_inventaire()
    {
        // Remarque de Codex sur la PR #80 : une pièce d'ensemble usée à zéro ne fait pas paraître un palier atteint, au sac comme portée.
        SavedCharacter hero = Inventory.Outfit(Hero(), Items);
        var worn = hero.Equipment.ToDictionary(e => e.Key, e => new ItemStack(e.Value, 1));
        worn["legs"] = new ItemStack("item.jambieres_du_veneur", 1);
        worn["shoulders"] = new ItemStack("item.spalieres_du_veneur", 1);
        worn["chest"] = new ItemStack("item.cuirasse_du_veneur", 1, wear: 0);   // portée, usée à zéro
        Inventory inventory = Inventory.From(hero.WithItems(new[] { new ItemStack("item.gants_du_veneur", 1, wear: 0), new ItemStack("item.gants_du_veneur", 1) }, worn), Items, 20);
        Assert.Equal(2, inventory.WornPieces("set.tenue_du_veneur"));
        // Portée et usée : la fiche compte 2 pièces, comme l'inventaire, et le dit.
        ItemComparison chest = ItemComparison.For(inventory, inventory.WornInstanceAt("chest")!, "chest")!;
        Assert.Contains(chest.Details, d => d.Text == "Ensemble « Tenue du veneur » : 2/4 pièces");
        Assert.Contains(chest.Details, d => d.Text.StartsWith("Pièce usée : ne compte pas"));
        Assert.False(chest.Details.Single(d => d.Text.StartsWith("(4)")).Active);
        // Au sac et usée : la porter ne change pas le compte (2/4) ; neuve, elle le monte à 3/4.
        ItemComparison usedGloves = ItemComparison.For(inventory, inventory.Stacks[0])!;
        DetailLine set = usedGloves.Details.Single(d => d.Text.StartsWith("Ensemble"));
        Assert.Equal(("Ensemble « Tenue du veneur » : 2/4 pièces", CompareTrend.Same), (set.Text, set.Trend));
        Assert.Equal("Ensemble « Tenue du veneur » : 2 → 3/4 pièces", ItemComparison.For(inventory, inventory.Stacks[1])!.Details.Single(d => d.Text.StartsWith("Ensemble")).Text);
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject Also(this JsonObject o, Action<JsonObject> change)
    {
        change(o);
        return o;
    }
}
