using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8c : les trois places de bonus (plat, élixir, huile ou pierre d'arme). Catalogue des objets en version 7 (type <c>boon</c>,
/// durée par qualité), métiers (recettes d'élixirs et d'huiles, sans changement de version), sauvegarde en version 12 (bonus actifs et temps
/// qui leur reste), consommation atomique, remplacement d'une même place, temps qui ne baisse que pendant la partie, fin au tick près,
/// bonus gardés quand on tombe, effet en combat dans la limite du combat.
/// </summary>
[Trait("Category", "World")]
public class V18cBonusTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v18c_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly ZoneDefinition Vallons = Zone();

    private const string Vigueur = "item.elixir_de_vigueur", Garde = "item.elixir_de_garde", Seuil = "item.elixir_du_seuil";
    private const string Pierre = "item.pierre_a_aiguiser", Huile = "item.huile_d_eclat";

    /// <summary>
    /// Donnée d'essai clairement marquée (les plats arrivent avec la cuisine, après l'ouverture de l'alpha : aucun plat n'est livré) : un
    /// ragoût de test, ajouté au catalogue livré pour ces tests seulement.
    /// </summary>
    private const string Ragout = "item.test_ragout";

    private static JsonObject ItemsJson() => JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();

    private static ItemCatalogLoadResult ItemsMutated(Action<JsonObject> mutate)
    {
        JsonObject root = ItemsJson();
        mutate(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonArray ItemList(JsonObject root) => root["data"]!["items"]!.AsArray();

    private static int IndexOf(JsonObject root, string id) => ItemList(root).Select((x, i) => (x, i)).Single(p => (string)p.x!["id"]! == id).i;

    private static JsonObject ItemNode(JsonObject root, string id) => ItemList(root)[IndexOf(root, id)]!.AsObject();

    private static void AddRagout(JsonObject root) => ItemList(root).Add(new JsonObject
    {
        ["id"] = Ragout, ["name"] = "Ragoût d'essai", ["type"] = "boon", ["rarity"] = "common", ["stack"] = 5, ["boon"] = "meal", ["quality"] = "superior",
        ["bonus"] = new JsonObject { ["maxEndurance"] = 6 },
    });

    /// <summary>Catalogue livré avec le plat d'essai.</summary>
    private static readonly ItemCatalog WithMeal = ItemsMutated(AddRagout).Catalog!;

    private static ValidationError FirstError(ItemCatalogLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0];
    }

    private static WorldSave SaveOf(SavedCharacter hero, long sessions = 0) =>
        new WorldSave("zone.vallons", 0, sessions, new[] { PlayerCharacters.PlacesKey }, hero);

    /// <summary>Partie ouverte au point sûr du village, un tick simulé (le personnage est arrivé).</summary>
    private static WorldSession Open(SavedCharacter hero, ItemCatalog? items = null, ZoneDefinition? zone = null)
    {
        WorldSession w = Reopen(SaveOf(hero), items, zone);
        w.Step();
        return w;
    }

    /// <summary>Partie ouverte sur une sauvegarde, sans tick.</summary>
    private static WorldSession Reopen(WorldSave save, ItemCatalog? items = null, ZoneDefinition? zone = null) =>
        new WorldSession(save, zone ?? Vallons, ShippedData, Progression, items ?? ItemsCatalog);

    /// <summary>Sauvegarde écrite puis relue (le chemin d'un arrêt du jeu : hors ligne).</summary>
    private static WorldSave Offline(WorldSession w)
    {
        SaveLoadResult r = SaveJsonReader.Read(SaveJsonReader.Write(w.Save()));
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Save!;
    }

    private static string Boons(WorldSession w) => string.Join(",", w.ActiveBoons);

    private static string State(WorldSession w) => Snapshot(w) + "|" + Boons(w);

    // --- Catalogue des objets (version 7) ------------------------------------------------------------------------------------------

    [Fact]
    public void Catalogue_en_version_7_objets_de_bonus_livres()
    {
        ItemCatalog items = ItemsCatalog;
        Assert.Equal((7, 7, "0.17.0-provisoire"), (items.SchemaVersion, ItemValidator.CurrentSchemaVersion, items.ContentVersion));
        Assert.Equal(new[] { 30, 45, 60 }, items.BoonMinutes);
        // La qualité décide de la durée : 30, 45, 60 minutes de jeu, en ticks logiques de 50 ms.
        Assert.Equal((36_000, 54_000, 72_000), (items.BoonTicks(items.Item(Vigueur)), items.BoonTicks(items.Item(Garde)), items.BoonTicks(items.Item(Seuil))));
        Assert.Equal(0, items.BoonTicks(items.Item("item.potion_de_soin")));
        Assert.Equal(BoonKinds.MaxTicks, items.BoonTicks(items.Item(Seuil)));
        var boons = items.Items.Where(i => i.Type == ItemType.Boon).ToList();
        Assert.Equal(new[] { Pierre, Huile, Vigueur, Garde, Seuil }, boons.Select(i => i.Id));
        Assert.Equal(new BoonKind?[] { BoonKind.Weapon, BoonKind.Weapon, BoonKind.Elixir, BoonKind.Elixir, BoonKind.Elixir }, boons.Select(i => i.Boon));
        Assert.Equal(new[] { ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Normal, ItemQuality.Superior, ItemQuality.Masterwork }, boons.Select(i => i.BoonQuality));
        // Plats : la cuisine arrive après l'ouverture de l'alpha ; aucun n'est livré.
        Assert.DoesNotContain(items.Items, i => i.Boon == BoonKind.Meal);
        // Effets modestes, accordés aux statistiques de l'équipement des premiers niveaux.
        Assert.Equal(new GearBonus(physicalPower: 4), items.Item(Pierre).Bonus);
        Assert.Equal(new GearBonus(physicalPower: 7), items.Item(Huile).Bonus);
        Assert.Equal(new GearBonus(maxHealth: 40), items.Item(Vigueur).Bonus);
        Assert.Equal(new GearBonus(armor: 25, statusResistance: 10), items.Item(Garde).Bonus);
        Assert.Equal(new GearBonus(maxHealth: 60, maxEndurance: 10), items.Item(Seuil).Bonus);
        Assert.All(boons, i => Assert.All(Enumerable.Range(0, GearBonus.Fields.Count), s => Assert.True(i.Bonus[s] <= 60)));
        // Le plat d'essai, lui, est admis comme plat.
        Assert.Equal((BoonKind.Meal, 54_000), (WithMeal.Item(Ragout).Boon!.Value, WithMeal.BoonTicks(WithMeal.Item(Ragout))));
    }

    [Fact]
    public void Recettes_des_elixirs_et_des_huiles()
    {
        // Choix le plus petit et cohérent : la forge et l'herboristerie (qui fait déjà les potions) existent : des recettes, pas de marchand.
        CraftingBook book = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, Vallons, ItemsCatalog).Book!;
        Assert.Equal((3, "0.7.0-provisoire"), (book.SchemaVersion, book.ContentVersion));
        foreach (ItemDefinition boon in ItemsCatalog.Items.Where(i => i.Type == ItemType.Boon))
        {
            Recipe r = Assert.Single(book.Recipes, x => x.Output.Item == boon.Id);
            Assert.Equal(boon.Boon == BoonKind.Weapon ? "forge" : "herboristerie", r.Profession);
            Assert.False(r.Quality);   // pas de petit jeu : la qualité est celle de l'objet
            Assert.NotEqual(r.Name, Texts.Get(r.Name));   // nom traduit (une clé absente se rend elle-même)
        }
    }

    [Fact]
    public void Pierre_a_aiguiser_fabriquee_a_la_forge()
    {
        ZoneDefinition forge = Zone(-19, -5);
        CraftingBook book = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, forge, ItemsCatalog).Book!;
        var w = new WorldSession(SaveOf(Hero(0, new ItemStack("item.minerai_de_fer", 2))), forge, ShippedData, Progression, ItemsCatalog, Book(forge), book);
        w.Step();
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.pierre_a_aiguiser", out CraftEvent? done));
        Assert.Equal((Pierre, 1), (done!.Item, done.Count));
        Assert.Equal(1, w.Inventory.Count(Pierre));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Pierre));
        Assert.Equal(36_000, w.BoonAt(BoonKind.Weapon)!.Ticks);
    }

    [Theory]
    [InlineData("boon", null, "$.data.items[{0}].boon", ValidationCode.MissingField)]
    [InlineData("boon", "dessert", "$.data.items[{0}].boon", ValidationCode.UnknownReference)]
    [InlineData("quality", null, "$.data.items[{0}].quality", ValidationCode.MissingField)]
    [InlineData("quality", "legendary", "$.data.items[{0}].quality", ValidationCode.UnknownReference)]
    [InlineData("bonus", null, "$.data.items[{0}].bonus", ValidationCode.MissingField)]
    [InlineData("stack", null, "$.data.items[{0}].stack", ValidationCode.MissingField)]
    public void Objet_de_bonus_mal_forme_refuse_avec_son_chemin(string field, string? value, string path, ValidationCode code)
    {
        int index = IndexOf(ItemsJson(), Vigueur);
        ItemCatalogLoadResult r = ItemsMutated(root =>
        {
            JsonObject item = ItemNode(root, Vigueur);
            if (value is null) item.Remove(field);
            else item[field] = value;
        });
        ValidationError e = FirstError(r);
        Assert.Equal((string.Format(path, index), code), (e.Path, e.Code));
    }

    [Fact]
    public void Objet_de_bonus_sans_effet_ou_au_dela_des_bornes_refuse()
    {
        int index = IndexOf(ItemsJson(), Vigueur);
        ValidationError none = FirstError(ItemsMutated(root => ItemNode(root, Vigueur)["bonus"] = new JsonObject { ["maxHealth"] = 0 }));
        Assert.Equal(($"$.data.items[{index}].bonus", ValidationCode.InvalidValue), (none.Path, none.Code));
        ValidationError empty = FirstError(ItemsMutated(root => ItemNode(root, Vigueur)["bonus"] = new JsonObject()));
        Assert.Equal($"$.data.items[{index}].bonus", empty.Path);
        ValidationError big = FirstError(ItemsMutated(root => ItemNode(root, Vigueur)["bonus"] = new JsonObject { ["maxHealth"] = ItemValidator.MaxItemBonus + 1 }));
        Assert.Equal(($"$.data.items[{index}].bonus.maxHealth", ValidationCode.OutOfRange), (big.Path, big.Code));
        ValidationError unknown = FirstError(ItemsMutated(root => ItemNode(root, Vigueur)["bonus"] = new JsonObject { ["chance"] = 3 }));
        Assert.Equal(($"$.data.items[{index}].bonus.chance", ValidationCode.UnknownReference), (unknown.Path, unknown.Code));
        // À la borne : admis.
        Assert.True(ItemsMutated(root => ItemNode(root, Vigueur)["bonus"] = new JsonObject { ["maxHealth"] = ItemValidator.MaxItemBonus }).IsValid);
    }

    [Theory]
    [InlineData("heal", 10)]
    [InlineData("cooldownMs", 1000)]
    [InlineData("slot", "weapon")]
    [InlineData("requiredLevel", 2)]
    public void Champ_d_un_autre_type_refuse_sur_un_objet_de_bonus(string field, object value)
    {
        int index = IndexOf(ItemsJson(), Vigueur);
        ValidationError e = FirstError(ItemsMutated(root => ItemNode(root, Vigueur)[field] = JsonValue.Create(value)));
        Assert.Equal(($"$.data.items[{index}].{field}", ValidationCode.InvalidValue), (e.Path, e.Code));
    }

    [Theory]
    [InlineData("item.herbe_de_l_aube", "boon", "elixir")]
    [InlineData("item.potion_de_soin", "quality", "normal")]
    [InlineData("item.epee_de_garde", "boon", "weapon")]
    [InlineData("item.epee_de_garde", "quality", "superior")]
    public void Champs_de_bonus_refuses_sur_un_autre_type(string item, string field, string value)
    {
        int index = IndexOf(ItemsJson(), item);
        ValidationError e = FirstError(ItemsMutated(root => ItemNode(root, item)[field] = value));
        Assert.Equal(($"$.data.items[{index}].{field}", ValidationCode.InvalidValue), (e.Path, e.Code));
    }

    [Theory]
    [InlineData("normalMinutes", 29, ValidationCode.OutOfRange)]
    [InlineData("masterworkMinutes", 61, ValidationCode.OutOfRange)]
    [InlineData("superiorMinutes", 29, ValidationCode.OutOfRange)]
    [InlineData("superiorMinutes", 25, ValidationCode.OutOfRange)]
    [InlineData("masterworkMinutes", 40, ValidationCode.InvalidValue)]   // moins que la qualité supérieure (45)
    public void Durees_des_qualites_bornees_et_croissantes(string field, int minutes, ValidationCode code)
    {
        ValidationError e = FirstError(ItemsMutated(root => root["data"]!["boons"]![field] = minutes));
        Assert.Equal(("$.data.boons." + field, code), (e.Path, e.Code));
    }

    [Fact]
    public void Durees_aux_frontieres_et_champs_absents()
    {
        ItemCatalog flat = ItemsMutated(root => root["data"]!["boons"] = new JsonObject { ["normalMinutes"] = 30, ["superiorMinutes"] = 30, ["masterworkMinutes"] = 30 }).Catalog!;
        Assert.Equal(36_000, flat.BoonTicks(flat.Item(Seuil)));
        ItemCatalog longest = ItemsMutated(root => root["data"]!["boons"] = new JsonObject { ["normalMinutes"] = 60, ["superiorMinutes"] = 60, ["masterworkMinutes"] = 60 }).Catalog!;
        Assert.Equal(BoonKinds.MaxTicks, longest.BoonTicks(longest.Item(Vigueur)));
        ValidationError missing = FirstError(ItemsMutated(root => root["data"]!.AsObject().Remove("boons")));
        Assert.Equal(("$.data.boons", ValidationCode.MissingField), (missing.Path, missing.Code));
        ValidationError field = FirstError(ItemsMutated(root => root["data"]!["boons"]!.AsObject().Remove("superiorMinutes")));
        Assert.Equal(("$.data.boons.superiorMinutes", ValidationCode.MissingField), (field.Path, field.Code));
        ValidationError extra = FirstError(ItemsMutated(root => root["data"]!["boons"]!["fineMinutes"] = 40));
        Assert.Equal("$.data.boons.fineMinutes", extra.Path);
        // Une version antérieure du catalogue est refusée (le fichier se met à jour à la main).
        ValidationError old = FirstError(ItemsMutated(root => root["schemaVersion"] = 6));
        Assert.Equal("$.schemaVersion", old.Path);
    }

    // --- Limite du combat ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Pire_cumul_compte_les_trois_places_de_bonus()
    {
        ItemCatalog items = WithMeal;
        int health = GearBonus.FieldIndex("maxHealth"), endurance = GearBonus.FieldIndex("maxEndurance"), physical = GearBonus.FieldIndex("physicalPower");
        // Une place par sorte : le plus fort de chaque place (élixir du Seuil 60 PV ; ragoût 6 + élixir du Seuil 10 d'endurance ; huile 7).
        Assert.Equal((60L, 16L, 7L), (items.BoonBound(health), items.BoonBound(endurance), items.BoonBound(physical)));
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long b, long r, long s) = items.WornBound(stat);
            Assert.True(b + r + s + items.BoonBound(stat) <= GearBonus.MaxValue);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => items.BoonBound(GearBonus.Fields.Count));
    }

    /// <summary>Huit propriétés tirées de <paramref name="value"/> en puissance physique, échelle de 100 % (V13RelectureTests).</summary>
    private static void StrongProperties(JsonObject root, int value)
    {
        JsonObject data = root["data"]!.AsObject();
        foreach (JsonNode? r in data["properties"]!["rarities"]!.AsArray()) r!["scalePercent"] = 100;
        data["properties"]!["rarities"]!.AsArray().Single(r => (string)r!["rarity"]! == "rare")!["count"] = 8;
        JsonArray list = data["properties"]!["list"]!.AsArray();
        for (int k = 0; k < 8; k++)
            list.Add(new JsonObject
            {
                ["id"] = $"property.puissance_{k}", ["name"] = $"Puissance {k}", ["stat"] = "physicalPower",
                ["min"] = value, ["max"] = value, ["minPerLevel"] = 0, ["maxPerLevel"] = 0,
            });
    }

    [Fact]
    public void Catalogue_refuse_quand_les_bonus_font_passer_la_limite_du_combat()
    {
        // Plus forte valeur des propriétés encore admise avec les bonus livrés (recherche par dichotomie : le pire cumul croît avec elle).
        int low = 1, high = SaveValidator.MaxPropertyValue;
        Assert.False(ItemsMutated(root => StrongProperties(root, high)).IsValid);
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (ItemsMutated(root => StrongProperties(root, mid)).IsValid) low = mid;
            else high = mid;
        }
        Assert.True(ItemsMutated(root => StrongProperties(root, low)).IsValid);
        // Juste sous la limite, une huile plus forte la fait passer : refusé, avec les bonus actifs comptés dans le message.
        int huile = IndexOf(ItemsJson(), Huile);
        ItemCatalogLoadResult refused = ItemsMutated(root =>
        {
            StrongProperties(root, low);
            ItemNode(root, Huile)["bonus"] = new JsonObject { ["physicalPower"] = ItemValidator.MaxItemBonus };
        });
        ValidationError e = Assert.Single(refused.Report.Errors);
        Assert.Equal(ValidationCode.LimitExceeded, e.Code);
        Assert.Contains($"bonus actifs {ItemValidator.MaxItemBonus}", e.Message, StringComparison.Ordinal);
        Assert.Contains($"au plus {GearBonus.MaxValue}", e.Message, StringComparison.Ordinal);
        Assert.True(huile >= 0);
    }

    [Fact]
    public void Effet_en_combat_plafonne_a_la_limite_du_combat()
    {
        var boons = new[] { new SavedBoon(BoonKind.Elixir, Vigueur, 10), new SavedBoon(BoonKind.Weapon, Huile, 10) };
        Assert.Equal(new GearBonus(physicalPower: 17, armor: 5, maxHealth: 40), WorldSession.BoonGear(new GearBonus(physicalPower: 10, armor: 5), ItemsCatalog, boons));
        GearBonus top = WorldSession.BoonGear(new GearBonus(physicalPower: GearBonus.MaxValue - 3, maxHealth: GearBonus.MaxValue), ItemsCatalog, boons);
        Assert.Equal((GearBonus.MaxValue, GearBonus.MaxValue), (top.PhysicalPower, top.MaxHealth));
        Assert.True(Inventory.WithinCombatLimit(top));
        // Un objet absent du catalogue ou d'une autre place ne donne rien.
        Assert.Equal(GearBonus.None, ItemsCatalog.BoonBonus(new[] { new SavedBoon(BoonKind.Meal, Vigueur, 5), new SavedBoon(BoonKind.Elixir, "item.inconnu", 5) }));
    }

    // --- Consommer, remplacer -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Consommer_est_atomique_et_les_refus_ne_changent_rien()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 2), new ItemStack("item.herbe_de_l_aube", 3), new ItemStack("item.potion_de_soin", 1)));
        string before = State(w);
        Assert.Equal(BoonRefusal.NotBoon, w.UseBoon("item.herbe_de_l_aube"));
        Assert.Equal(BoonRefusal.NotBoon, w.UseBoon("item.potion_de_soin"));
        Assert.Equal(BoonRefusal.NotBoon, w.UseBoon("item.inconnu"));
        Assert.Equal(BoonRefusal.NotBoon, w.UseBoon(null!));
        Assert.Equal(BoonRefusal.NothingThere, w.UseBoon(Garde));
        Assert.Equal(before, State(w));
        Assert.Equal(BoonRefusal.None, w.CanUseBoon(Vigueur));
        Assert.Equal(before, State(w));   // CanUseBoon ne change rien
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur, out SavedBoon? replaced));
        Assert.Null(replaced);
        Assert.Equal(1, w.Inventory.Count(Vigueur));
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Vigueur, 36_000), Assert.Single(w.ActiveBoons));
        Assert.Null(w.BoonAt(BoonKind.Meal));
        Assert.Null(w.BoonAt((BoonKind)7));
    }

    [Fact]
    public void Refuse_en_plein_combat()
    {
        // Le point sûr posé à 6 m du loup : un ennemi vivant à moins de 12 m, le personnage est en plein combat.
        GroundVector wolf = Vallons.PopulationOf("loup1")!.Template.Placement!.Position;
        ZoneDefinition near = Zone(wolf.X.Raw / 1e4 + 6, wolf.Z.Raw / 1e4);
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 1)), zone: near);
        Assert.True(w.InCombat);
        string before = State(w);
        Assert.Equal(BoonRefusal.InCombat, w.UseBoon(Vigueur));
        Assert.Equal(before, State(w));
    }

    [Fact]
    public void Un_nouveau_bonus_de_la_meme_place_remplace_l_ancien()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 2), new ItemStack(Garde, 1), new ItemStack(Pierre, 1)));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Pierre));
        for (int i = 0; i < 10; i++) w.Step();
        // L'élixir de garde remplace l'élixir de vigueur : l'ancien est perdu avec le temps qui lui restait ; l'huile ne bouge pas.
        Assert.Equal(BoonRefusal.None, w.UseBoon(Garde, out SavedBoon? replaced));
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Vigueur, 36_000 - 10), replaced);
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Garde, 54_000), w.BoonAt(BoonKind.Elixir));
        Assert.Equal(new SavedBoon(BoonKind.Weapon, Pierre, 36_000 - 10), w.BoonAt(BoonKind.Weapon));
        Assert.Equal((1, 0), (w.Inventory.Count(Vigueur), w.Inventory.Count(Garde)));
        // Le même objet repris : la durée repart à plein (même quand l'ancien durait plus longtemps : le nouveau remplace toujours).
        w.Step();
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur, out replaced));
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Garde, 54_000 - 1), replaced);
        Assert.Equal(36_000, w.BoonAt(BoonKind.Elixir)!.Ticks);
        Assert.Equal(2, w.ActiveBoons.Count);
    }

    [Fact]
    public void Trois_places_ensemble_et_effet_jusqu_a_la_prochaine_entree()
    {
        SavedCharacter hero = Inventory.Outfit(Hero(0, new ItemStack(Ragout, 1), new ItemStack(Seuil, 1), new ItemStack(Huile, 1)), WithMeal);
        WorldSession w = Open(hero, WithMeal);
        GearBonus gear = w.EnteredGear;
        Assert.Empty(w.EnteredBoons);
        foreach (string item in new[] { Huile, Ragout, Seuil }) Assert.Equal(BoonRefusal.None, w.UseBoon(item));
        Assert.Equal(new[] { BoonKind.Meal, BoonKind.Elixir, BoonKind.Weapon }, w.ActiveBoons.Select(b => b.Kind));   // ordre des places
        Assert.Equal(new[] { 54_000, 72_000, 54_000 }, w.ActiveBoons.Select(b => b.Ticks));
        // L'en-tête de la session garde le bonus de son entrée ; sous-lot V1.8d : le combat reçoit les bonus tout de suite (bonus modifié,
        // V18dEffetImmediatTests), et la session suivante entre avec eux.
        Assert.Equal(gear, w.EnteredGear);
        Assert.Equal(gear + new GearBonus(physicalPower: 7, maxHealth: 60, maxEndurance: 16), w.CombatGear);
        Assert.Empty(w.EnteredBoons);
        WorldSession next = Reopen(Offline(w), WithMeal);
        Assert.Equal(3, next.EnteredBoons.Count);
        Assert.Equal(gear + new GearBonus(physicalPower: 7, maxHealth: 60, maxEndurance: 16), next.EnteredGear);
        // Les statistiques du combat suivent : PV et endurance maximaux plus hauts qu'à l'entrée précédente.
        Assert.True(next.Zone.Engine.View("bastion").MaxHealth > w.Zone.Engine.View("bastion").MaxHealth);
    }

    // --- Temps, fin, hors ligne, mort -----------------------------------------------------------------------------------------------

    [Fact]
    public void Le_temps_baisse_d_un_tick_par_tick_et_le_bonus_finit_au_tick_pres()
    {
        SavedCharacter hero = Hero().WithBoons(new[] { new SavedBoon(BoonKind.Elixir, Vigueur, 5), new SavedBoon(BoonKind.Weapon, Pierre, 3) });
        WorldSession w = Reopen(SaveOf(hero));
        Assert.Equal(2, w.EnteredBoons.Count);   // présents à l'entrée : leur effet est dans ce combat
        Assert.Equal(w.Inventory.Gear() + new GearBonus(physicalPower: 4, maxHealth: 40), w.EnteredGear);
        SessionStep s1 = w.Step(), s2 = w.Step();
        Assert.Empty(s1.BoonsExpired);
        Assert.Empty(s2.BoonsExpired);
        Assert.Equal((3, 1), (w.BoonAt(BoonKind.Elixir)!.Ticks, w.BoonAt(BoonKind.Weapon)!.Ticks));
        SessionStep s3 = w.Step();   // troisième tick : la pierre finit, exactement
        BoonExpired gone = Assert.Single(s3.BoonsExpired);
        Assert.Equal((BoonKind.Weapon, Pierre), (gone.Kind, gone.Item));
        Assert.False(s3.IsEmpty);
        Assert.Null(w.BoonAt(BoonKind.Weapon));
        Assert.Equal(2, w.BoonAt(BoonKind.Elixir)!.Ticks);
        w.Step();
        SessionStep s5 = w.Step();
        Assert.Equal(Vigueur, Assert.Single(s5.BoonsExpired).Item);
        Assert.Empty(w.ActiveBoons);
        Assert.Empty(w.Step().BoonsExpired);
        // Un bonus fini n'est plus écrit ; la session suivante n'en a plus l'effet.
        WorldSave saved = Offline(w);
        Assert.Empty(saved.Character!.Boons);
        WorldSession next = Reopen(saved);
        Assert.Empty(next.EnteredBoons);
        Assert.Equal(next.Inventory.Gear(), next.EnteredGear);
    }

    [Fact]
    public void Arrete_hors_ligne_la_sauvegarde_garde_le_temps_exact()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Garde, 1)));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Garde));
        for (int i = 0; i < 123; i++) w.Step();
        Assert.Equal(54_000 - 123, w.BoonAt(BoonKind.Elixir)!.Ticks);
        WorldSave saved = Offline(w);
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Garde, 54_000 - 123), Assert.Single(saved.Character!.Boons));
        // Hors ligne, rien n'avance : relue deux fois (ou bien plus tard), la sauvegarde donne le même temps ; seule la partie le fait baisser.
        WorldSession back = Reopen(saved), again = Reopen(SaveJsonReader.Read(SaveJsonReader.Write(saved)).Save!);
        Assert.Equal(Boons(back), Boons(again));
        Assert.Equal(54_000 - 123, back.BoonAt(BoonKind.Elixir)!.Ticks);
        back.Step();
        Assert.Equal(54_000 - 124, back.BoonAt(BoonKind.Elixir)!.Ticks);
    }

    [Fact]
    public void Garde_quand_on_tombe_puis_reprise()
    {
        // Données copiées, un sanglier mortel posté près de l'entrée (comme Lot128AdventureTests).
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        Copy(DataDirectory, data);
        Copy(ScenariosDirectory, scenarios);
        string zone = Path.Combine(scenarios, "zones", "vallons.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        JsonObject boar = root["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
        boar["position"] = new JsonArray(-14, -12);
        foreach (JsonNode? attack in boar["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
        File.WriteAllText(zone, root.ToJsonString());
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SavedCharacter hero = PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression)
            .WithItems(new[] { new ItemStack(Vigueur, 1) }, new Dictionary<string, string>())
            .WithBoons(new[] { new SavedBoon(BoonKind.Elixir, Garde, 20_000), new SavedBoon(BoonKind.Weapon, Huile, 30_000) });
        SaveStore.Write(savePath, SaveOf(hero));
        AdventureSession a = AdventureSession.Load(data, scenarios, savePath);
        Assert.Equal(2, a.World.EnteredBoons.Count);
        for (int i = 0; i < 1200 && !a.World.IsDefeated; i++) a.Advance(0.05);
        Assert.True(a.World.IsDefeated);
        IReadOnlyList<SavedBoon> atDeath = a.World.ActiveBoons;
        Assert.Equal(2, atDeath.Count);
        Assert.True(atDeath[0].Ticks < 20_000);
        Assert.Equal(20_000 - atDeath[0].Ticks, 30_000 - atDeath[1].Ticks);   // le même temps de jeu pour les deux
        // Tombé : on ne consomme rien, et rien ne change.
        Assert.Equal(BoonRefusal.Unavailable, a.World.UseBoon(Vigueur));
        Assert.Equal(0, a.Advance(1));   // tombé : plus rien n'avance avant la reprise, le temps des bonus non plus
        Assert.Equal(atDeath, a.World.ActiveBoons);
        Assert.True(a.Continue());
        // Reprise au point sûr : les bonus sont gardés, avec leur temps exact, et leur effet est dans le nouveau combat.
        Assert.Equal(atDeath, a.World.ActiveBoons);
        Assert.Equal(atDeath, a.World.EnteredBoons);
        Assert.Equal(atDeath, SaveStore.Read(savePath).Save!.Character!.Boons);
    }

    private static void Copy(string from, string to)
    {
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to));
    }

    [Fact]
    public void Aventure_consomme_depuis_le_sac_et_annonce_la_fin()
    {
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SavedCharacter hero = PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression)
            .WithItems(new[] { new ItemStack(Vigueur, 2), new ItemStack("item.herbe_de_l_aube", 1) }, new Dictionary<string, string>())
            .WithBoons(new[] { new SavedBoon(BoonKind.Weapon, Pierre, 2) });
        SaveStore.Write(savePath, SaveOf(hero));
        AdventureSession a = Adventure(savePath);
        a.DrainMessages();
        int place = a.World.Inventory.Stacks.Select((s, i) => (s, i)).First(p => p.s.Item == Vigueur).i;
        Assert.Equal(BoonRefusal.None, a.UseBoon(place));
        Assert.Contains("Élixir de vigueur : bonus actif tout de suite, pendant 30 minutes de jeu.", a.DrainMessages());
        Assert.Equal(BoonRefusal.None, a.UseBoon(place));
        Assert.Contains("Élixir de vigueur : bonus actif tout de suite, pendant 30 minutes de jeu (remplace Élixir de vigueur).", a.DrainMessages());
        int herb = a.World.Inventory.Stacks.Select((s, i) => (s, i)).First(p => p.s.Item == "item.herbe_de_l_aube").i;
        Assert.Equal(BoonRefusal.NotBoon, a.UseBoon(herb));
        Assert.Contains("Cet objet ne donne pas de bonus.", a.DrainMessages());
        Assert.Equal(BoonRefusal.NothingThere, a.UseBoon(99));
        a.Advance(0.2);
        Assert.Contains("Bonus terminé : Pierre à aiguiser.", a.DrainMessages());
        Assert.Null(a.World.BoonAt(BoonKind.Weapon));
    }

    [Fact]
    public void Sauvegarde_automatique_garde_le_temps_qui_baisse()
    {
        // Remarque de Codex sur la PR #84 : un bonus actif, rien d'autre ne se passe ; après un intervalle de sauvegarde automatique, la
        // sauvegarde sur disque porte le temps réduit (et pas celui de la sauvegarde précédente).
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SavedCharacter hero = PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression)
            .WithBoons(new[] { new SavedBoon(BoonKind.Elixir, Garde, 20_000) });
        SaveStore.Write(savePath, SaveOf(hero));
        AdventureSession a = Adventure(savePath);
        a.Save();
        int saved = SaveStore.Read(savePath).Save!.Character!.Boons.Single().Ticks;
        Assert.Equal(a.World.BoonAt(BoonKind.Elixir)!.Ticks, saved);
        // Avant l'intervalle : rien n'est écrit.
        a.Advance(1);
        Assert.Equal(saved, SaveStore.Read(savePath).Save!.Character!.Boons.Single().Ticks);
        for (int i = 0; i < 6 * 20; i++) a.Advance(0.05);
        int now = a.World.BoonAt(BoonKind.Elixir)!.Ticks;
        int onDisk = SaveStore.Read(savePath).Save!.Character!.Boons.Single().Ticks;
        Assert.True(onDisk < saved, $"sauvegarde automatique : {onDisk} ticks, {saved} avant");
        Assert.InRange(onDisk, now, now + (int)(AdventureSession.AutosaveSeconds * 20) + 1);
    }

    // --- Accord avec le catalogue -----------------------------------------------------------------------------------------------

    [Fact]
    public void Bonus_sauvegardes_accordes_au_catalogue_actuel()
    {
        SavedCharacter hero = Hero().WithBoons(new[]
        {
            new SavedBoon(BoonKind.Meal, "item.herbe_de_l_aube", 100),   // plus un objet de bonus
            new SavedBoon(BoonKind.Elixir, Vigueur, BoonKinds.MaxTicks),   // au-delà de sa durée (30 minutes)
            new SavedBoon(BoonKind.Weapon, Vigueur, 50),   // pas un objet de cette place
        });
        WorldSession w = Reopen(SaveOf(hero));
        Assert.Equal(new SavedBoon(BoonKind.Elixir, Vigueur, 36_000), Assert.Single(w.ActiveBoons));
        Assert.Equal(3, w.Adjusted.Count(n => n.StartsWith("bonus « ", StringComparison.Ordinal)));
        Assert.Contains(w.Adjusted, n => n.Contains("ramenés à la durée", StringComparison.Ordinal));
        Assert.Single(w.EnteredBoons);
        // Le plat d'essai relu sans lui dans le catalogue : retiré, sans exception.
        WorldSession meal = Reopen(SaveOf(Hero().WithBoons(new[] { new SavedBoon(BoonKind.Meal, Ragout, 10) })));
        Assert.Empty(meal.ActiveBoons);
    }

    // --- Sauvegarde v12 --------------------------------------------------------------------------------------------------------------

    private static string Written(params SavedBoon[] boons) => SaveJsonReader.Write(SaveOf(Hero().WithBoons(boons)));

    [Fact]
    public void Sauvegarde_en_version_12_ecrite_et_relue_a_l_identique()
    {
        Assert.Equal((17, 12), (SaveValidator.CurrentSchemaVersion, SaveValidator.BoonsSince));   // V2.16 : 17 (aspect du héros) ; V2.10 : 16 (stock) ; V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements)
        string text = Written(new SavedBoon(BoonKind.Weapon, Pierre, 1), new SavedBoon(BoonKind.Meal, Ragout, BoonKinds.MaxTicks));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);   // V2.5 : 13
        Assert.Contains("      \"boons\": [{\"kind\": \"meal\", \"item\": \"item.test_ragout\", \"ticks\": 72000}, {\"kind\": \"weapon\", \"item\": \"item.pierre_a_aiguiser\", \"ticks\": 1}],\n",
            text, StringComparison.Ordinal);
        SaveLoadResult r = SaveJsonReader.Read(text);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.False(r.Save!.Migrated);
        Assert.Equal(new[] { BoonKind.Meal, BoonKind.Weapon }, r.Save.Character!.Boons.Select(b => b.Kind));
        Assert.Equal(text, SaveJsonReader.Write(r.Save));
        Assert.Contains("      \"boons\": [],\n      \"events\": [],\n      \"scars\": []\n", Written(), StringComparison.Ordinal);
    }

    /// <summary>Texte d'une sauvegarde de version 12, une ligne remplacée.</summary>
    private static string WithBoonsLine(string line) =>
        Written().Replace("      \"boons\": [],\n", "      \"boons\": " + line + ",\n", StringComparison.Ordinal);

    [Theory]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"item.elixir_de_vigueur\", \"ticks\": 0}]", "$.data.character.boons[0].ticks", ValidationCode.OutOfRange)]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"item.elixir_de_vigueur\", \"ticks\": 72001}]", "$.data.character.boons[0].ticks", ValidationCode.OutOfRange)]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"item.elixir_de_vigueur\", \"ticks\": -5}]", "$.data.character.boons[0].ticks", ValidationCode.OutOfRange)]
    [InlineData("[{\"kind\": \"dessert\", \"item\": \"item.elixir_de_vigueur\", \"ticks\": 5}]", "$.data.character.boons[0].kind", ValidationCode.InvalidValue)]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"Élixir\", \"ticks\": 5}]", "$.data.character.boons[0].item", ValidationCode.InvalidValue)]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"item.a\", \"ticks\": 5}, {\"kind\": \"elixir\", \"item\": \"item.b\", \"ticks\": 5}]", "$.data.character.boons[1].kind", ValidationCode.InvalidValue)]
    [InlineData("[{\"kind\": \"weapon\", \"item\": \"item.a\", \"ticks\": 5}, {\"kind\": \"meal\", \"item\": \"item.b\", \"ticks\": 5}]", "$.data.character.boons[1].kind", ValidationCode.InvalidValue)]
    [InlineData("[{\"kind\": \"elixir\", \"ticks\": 5}]", "$.data.character.boons[0].item", ValidationCode.MissingField)]
    [InlineData("[{\"kind\": \"elixir\", \"item\": \"item.a\"}]", "$.data.character.boons[0].ticks", ValidationCode.MissingField)]
    [InlineData("[{\"item\": \"item.a\", \"ticks\": 5}]", "$.data.character.boons[0].kind", ValidationCode.MissingField)]
    [InlineData("[null]", "$.data.character.boons[0]", null)]
    [InlineData("[{\"kind\": \"meal\", \"item\": \"item.a\", \"ticks\": 5, \"bonus\": 3}]", "$.data.character.boons[0].bonus", null)]
    [InlineData("{}", "$.data.character.boons", null)]
    [InlineData("[{\"kind\": \"meal\", \"item\": \"item.a\", \"ticks\": 1}, {\"kind\": \"elixir\", \"item\": \"item.b\", \"ticks\": 1}, {\"kind\": \"weapon\", \"item\": \"item.c\", \"ticks\": 1}, {\"kind\": \"weapon\", \"item\": \"item.d\", \"ticks\": 1}]",
        "$.data.character.boons", ValidationCode.LimitExceeded)]
    public void Chargeur_refuse_les_valeurs_corrompues_avec_leur_chemin(string boons, string path, ValidationCode? code)
    {
        SaveLoadResult r = SaveJsonReader.Read(WithBoonsLine(boons));
        Assert.False(r.IsValid);
        ValidationError e = r.Report.Errors[0];
        Assert.Equal(path, e.Path);
        if (code is ValidationCode c) Assert.Equal(c, e.Code);
    }

    [Fact]
    public void Bonus_obligatoires_en_version_12_aux_bornes()
    {
        string text = Written().Replace("      \"boons\": [],\n", "", StringComparison.Ordinal);
        SaveLoadResult r = SaveJsonReader.Read(text);
        Assert.False(r.IsValid);
        Assert.Equal(("$.data.character.boons", ValidationCode.MissingField), (r.Report.Errors[0].Path, r.Report.Errors[0].Code));
        Assert.True(SaveJsonReader.Read(WithBoonsLine("[{\"kind\": \"meal\", \"item\": \"item.a\", \"ticks\": 1}, {\"kind\": \"elixir\", \"item\": \"item.b\", \"ticks\": 72000}, {\"kind\": \"weapon\", \"item\": \"item.c\", \"ticks\": 36000}]")).IsValid);
        // Le modèle refuse aussi ce que le chargeur refuse.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedBoon(BoonKind.Elixir, Vigueur, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedBoon(BoonKind.Elixir, Vigueur, BoonKinds.MaxTicks + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedBoon((BoonKind)3, Vigueur, 1));
        Assert.Throws<ArgumentException>(() => Hero().WithBoons(new[] { new SavedBoon(BoonKind.Elixir, Vigueur, 1), new SavedBoon(BoonKind.Elixir, Garde, 1) }));
        Assert.Equal(BoonKinds.MaxTicks, new SavedBoon(BoonKind.Elixir, Vigueur, BoonKinds.MaxTicks).Ticks);
    }

    [Fact]
    public void Migration_v11_vers_v12_sans_bonus()
    {
        // Une sauvegarde de version 11 (celle de V1.4 à V1.8b) : pas de « boons ».
        string v11 = SaveV11.V14(Written()).Replace(SaveVersion.Current + ",", "\"schemaVersion\": 11,", StringComparison.Ordinal)
            .Replace(",\n      \"boons\": [],\n      \"events\": [],\n      \"scars\": []\n", "\n", StringComparison.Ordinal);
        SaveLoadResult r = SaveJsonReader.Read(v11);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.True(r.Save!.Migrated);
        Assert.Empty(r.Save.Character!.Boons);
        Assert.Contains(SaveVersion.Current + ",", SaveJsonReader.Write(r.Save), StringComparison.Ordinal);
        WorldSession w = Reopen(r.Save);
        Assert.Empty(w.ActiveBoons);
        // Une version 11 n'a pas de bonus : refusés, champ inconnu.
        string withBoons = SaveV11.V12(Written(new SavedBoon(BoonKind.Elixir, Vigueur, 5))).Replace(SaveVersion.Current + ",", "\"schemaVersion\": 11,", StringComparison.Ordinal);
        SaveLoadResult refused = SaveJsonReader.Read(withBoons);
        Assert.False(refused.IsValid);
        Assert.Equal(("$.data.character.boons", ValidationCode.UnknownField), (refused.Report.Errors[0].Path, refused.Report.Errors[0].Code));
        // Une sauvegarde d'une version future est refusée.
        Assert.False(SaveJsonReader.Read(Written().Replace(SaveVersion.Current + ",", SaveVersion.Future + ",", StringComparison.Ordinal)).IsValid);
    }

    // --- Propriété ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Propriete_suites_au_hasard_temps_places_et_hors_ligne()
    {
        string[] pool = { Ragout, Vigueur, Garde, Seuil, Pierre, Huile };
        var random = new Random(20261101);
        for (int run = 0; run < 3; run++)
        {
            var bag = pool.ToDictionary(i => i, i => Math.Min(3, WithMeal.Item(i).Stack));
            // Des bonus déjà presque finis, pour que les fins arrivent pendant la suite.
            var model = new SavedBoon?[BoonKinds.Count];
            string[] first = { Ragout, Vigueur, Pierre };
            for (int k = 0; k < BoonKinds.Count; k++)
                if (random.Next(4) > 0) model[k] = new SavedBoon((BoonKind)k, first[k], random.Next(2, 60));
            SavedCharacter hero = Hero(0, bag.Select(b => new ItemStack(b.Key, b.Value)).ToArray()).WithBoons(model.Where(b => b != null).Select(b => b!));
            WorldSession w = Open(hero, WithMeal);
            for (int k = 0; k < model.Length; k++) model[k] = model[k]?.WithTicks(model[k]!.Ticks - 1);   // le tick de l'arrivée
            int reloads = 0;
            for (int op = 0; op < 400; op++)
            {
                int roll = random.Next(100);
                if (roll < 15)
                {
                    string item = pool[random.Next(pool.Length)];
                    BoonRefusal refusal = w.UseBoon(item, out SavedBoon? replaced);
                    if (bag[item] == 0)
                    {
                        Assert.Equal(BoonRefusal.NothingThere, refusal);
                        continue;
                    }
                    Assert.Equal(BoonRefusal.None, refusal);
                    ItemDefinition d = WithMeal.Item(item);
                    Assert.Equal(model[(int)d.Boon!.Value], replaced);
                    model[(int)d.Boon.Value] = new SavedBoon(d.Boon.Value, item, WithMeal.BoonTicks(d));
                    bag[item]--;
                }
                else if (roll < 18 && reloads < 4)
                {
                    reloads++;
                    w = Reopen(Offline(w), WithMeal);   // hors ligne : rien ne change
                }
                else
                {
                    int jump = random.Next(1, 4);
                    for (int t = 0; t < jump; t++)
                    {
                        SessionStep s = w.Step();
                        var expected = new List<BoonKind>();
                        for (int k = 0; k < model.Length; k++)
                        {
                            if (model[k] is not SavedBoon b) continue;
                            if (b.Ticks == 1) { model[k] = null; expected.Add(b.Kind); }
                            else model[k] = b.WithTicks(b.Ticks - 1);
                        }
                        Assert.Equal(expected, s.BoonsExpired.Select(x => x.Kind));
                    }
                }
                Assert.Equal(model.Where(b => b != null).Select(b => b!), w.ActiveBoons);
                Assert.All(w.ActiveBoons, b => Assert.InRange(b.Ticks, 1, WithMeal.BoonTicks(WithMeal.Item(b.Item))));
                Assert.Equal(w.ActiveBoons.Select(b => b.Kind).Distinct().Count(), w.ActiveBoons.Count);
                foreach (string i in pool) Assert.Equal(bag[i], w.Inventory.Count(i));
            }
        }
    }
}
