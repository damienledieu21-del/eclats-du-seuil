using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Relecture indépendante de la PR #78 (sous-lot V1.3), tour 1 : la vente par place retire toujours la place exacte (jamais une instance
/// tirée à la place de l'objet de base) ; un retrait par identifiant (fabrication, objet pris par une quête, livraison, vente par
/// identifiant) prend les copies de base d'abord ; une instance relue est accordée au catalogue actuel (valeur ramenée dans ses bornes,
/// propriété retirée, jamais d'exception) ; l'équipement ne pose que les altérations admises par les règles
/// (<c>combat.equipment.allowedStatuses</c>) ; le tirage des propriétés suit le rang de l'objet dans sa table.
/// </summary>
[Trait("Category", "World")]
public class V13RelectureTests : IDisposable
{
    private const string Boots = "item.bottes_du_renard";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v13_relecture_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ItemStack Rolled(int death = 1) => ItemRolls.Roll(ItemsCatalog, Boots, 1, $"zone.vallons/{death}/death/loup1", 0);

    private static ItemStack Base(string item = Boots, int count = 1) => new ItemStack(item, count);

    private static SavedCharacter Bastion(int level, IEnumerable<ItemStack> bag, IReadOnlyDictionary<string, string>? worn = null,
        IReadOnlyDictionary<string, IReadOnlyList<RolledProperty>>? rolled = null) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[Math.Min(level, Progression.XpToLevel.Count) - 1])
            .WithItems(bag, worn ?? new Dictionary<string, string>(), rolled);

    private static Inventory Bag(params ItemStack[] stacks) => Inventory.From(Bastion(10, stacks), ItemsCatalog, 10);

    // --- Important : vendre une place vend cette place -------------------------------------------------------------------------

    [Fact]
    public void Vendre_la_place_de_l_objet_de_base_garde_l_instance_tiree()
    {
        ItemStack rolled = Rolled();
        var market = new Market(ShippedMerchants);
        // Bottes de base à la place 0, bottes tirées à la place 1 : on vend la place 0.
        WorldSession w = At("merchant.armurier", Hero(0, Base(), rolled), market);
        long price = market.UnitPrice("merchant.armurier", Boots);
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 0, 1));
        Assert.Equal(new[] { rolled }, w.Inventory.Stacks);   // l'instance tirée reste
        Assert.Equal(price, w.Money);
        SoldLot lot = Assert.Single(market.Buyback);
        Assert.Equal((Boots, 1, price, 0), (lot.Item, lot.Count, lot.Paid, lot.Stacks.Count));   // lot d'objets de base
        // Rachat : la paire de base revient, l'instance tirée n'a pas bougé.
        Assert.Equal(TradeRefusal.None, w.BuyBack("merchant.armurier", 0));
        Assert.Equal(new[] { rolled, Base() }, w.Inventory.Stacks);
        // Ordre inverse : on vend la place 1 (la paire de base), l'instance tirée de la place 0 reste.
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 1, 1));
        Assert.Equal(new[] { rolled }, w.Inventory.Stacks);
    }

    [Fact]
    public void Vente_partielle_d_une_pile_de_base_a_cote_d_une_instance_tiree()
    {
        ItemStack rolled = Rolled();
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.armurier", Hero(0, Base("item.peau_de_loup", 5), rolled, Base("item.peau_de_loup", 3), Base()), market);
        long hide = market.UnitPrice("merchant.armurier", "item.peau_de_loup");
        // Deux peaux de la place 0 : la place 0 garde le reste, les autres places ne bougent pas.
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 0, 2));
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), rolled, Base("item.peau_de_loup", 3), Base() }, w.Inventory.Stacks);
        Assert.Equal(2 * hide, w.Money);
        Assert.Equal(("item.peau_de_loup", 2, 2 * hide), (market.Buyback[0].Item, market.Buyback[0].Count, market.Buyback[0].Paid));
        // Au-delà de la pile de cette place (même si l'inventaire en a assez ailleurs), quantité impossible, place vide : refus, rien ne change.
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.NotEnough, w.SellPlace("merchant.armurier", 0, 4));
        Assert.Equal(TradeRefusal.InvalidCount, w.SellPlace("merchant.armurier", 0, 0));
        Assert.Equal(TradeRefusal.InvalidCount, w.SellPlace("merchant.armurier", 1, 2));   // une instance tirée part seule
        Assert.Equal(TradeRefusal.NotEnough, w.SellPlace("merchant.armurier", 4, 1));
        Assert.Equal(before, Snapshot(w));
        // La paire de base de la dernière place se vend elle-même : l'instance tirée reste.
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 3, 1));
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), rolled, Base("item.peau_de_loup", 3) }, w.Inventory.Stacks);
        // Toute la pile de la place 2.
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 2, 3));
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), rolled }, w.Inventory.Stacks);
    }

    [Fact]
    public void Vente_par_place_dans_l_aventure()
    {
        ItemStack rolled = Rolled();
        (string data, string scenarios) = DataCopy(Near("merchant.armurier"));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        SaveStore.Write(save, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(0, Base(), rolled, Base("item.peau_de_loup", 4))));
        AdventureSession a = Adventure(save, data, scenarios);
        Assert.True(a.Interact());
        a.DrainMessages();
        // Clic sur la paire de base (place 0) : elle part, pas l'instance tirée.
        Assert.Equal(TradeRefusal.None, a.SellPlace(0));
        Assert.StartsWith("Vendu : ", Assert.Single(a.DrainMessages()), StringComparison.Ordinal);
        Assert.Equal(new[] { rolled, Base("item.peau_de_loup", 4) }, a.World.Inventory.Stacks);
        // Une partie de la pile de peaux, puis plus que la pile : seulement ce que la place a.
        Assert.Equal(TradeRefusal.None, a.SellPlace(1, 3));
        Assert.Equal(new[] { rolled, Base("item.peau_de_loup", 1) }, a.World.Inventory.Stacks);
        Assert.Equal(TradeRefusal.None, a.SellPlace(1, 50));
        Assert.Equal(new[] { rolled }, a.World.Inventory.Stacks);
        // L'instance tirée elle-même.
        Assert.Equal(TradeRefusal.None, a.SellPlace(0));
        Assert.Empty(a.World.Inventory.Stacks);
        Assert.Equal(rolled, Assert.Single(a.Market.Buyback[0].Stacks));
    }

    // --- Mineur 2 : retrait par identifiant, les copies de base d'abord -------------------------------------------------------------

    [Fact]
    public void Retrait_par_identifiant_prend_les_copies_de_base_d_abord()
    {
        ItemStack first = Rolled(1), second = Rolled(2);
        // Bottes de base devant une instance tirée : avant la relecture, le retrait (des dernières places) prenait l'instance.
        Inventory bag = Bag(Base(), first);
        Assert.Equal(InventoryRefusal.None, bag.Remove(Boots, 1, out IReadOnlyList<ItemStack> removed));
        Assert.Equal(new[] { first }, bag.Stacks);
        Assert.Equal(new[] { Base() }, removed);
        // Instance devant la copie de base : la copie de base part aussi.
        bag = Bag(first, Base("item.peau_de_loup", 2), Base());
        Assert.Equal(InventoryRefusal.None, bag.Remove(Boots, 1));
        Assert.Equal(new[] { first, Base("item.peau_de_loup", 2) }, bag.Stacks);
        // Plus que les copies de base : elles partent toutes, puis les instances (des dernières places), le reste garde ses propriétés.
        bag = Bag(first, Base(), second, Base());
        Assert.Equal(InventoryRefusal.None, bag.Remove(Boots, 3, out removed));
        Assert.Equal(new[] { first }, bag.Stacks);
        Assert.Equal(new[] { Base(), Base(), second }, removed);
        // Seulement des instances : le retrait les prend (aucune copie de base ne suffit).
        bag = Bag(first);
        Assert.Equal(InventoryRefusal.None, bag.Remove(Boots, 1));
        Assert.Empty(bag.Stacks);
        // Pas assez : refus, rien ne change.
        bag = Bag(first, Base());
        Assert.Equal(InventoryRefusal.NotEnough, bag.Remove(Boots, 3));
        Assert.Equal(new[] { first, Base() }, bag.Stacks);
    }

    [Fact]
    public void Vente_par_identifiant_garde_l_instance_tiree_quand_la_base_suffit()
    {
        ItemStack rolled = Rolled();
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.armurier", Hero(0, Base(), rolled), market);
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.armurier", Boots, 1));
        Assert.Equal(new[] { rolled }, w.Inventory.Stacks);
        Assert.Empty(market.Buyback[0].Stacks);
    }

    [Fact]
    public void Retrait_a_une_place_exacte_atomique()
    {
        ItemStack rolled = Rolled();
        Inventory bag = Bag(Base("item.peau_de_loup", 5), rolled, Base("item.peau_de_loup", 3));
        Assert.Equal(InventoryRefusal.None, bag.RemoveAt(0, 2, out ItemStack? taken));
        Assert.Equal(Base("item.peau_de_loup", 2), taken);
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), rolled, Base("item.peau_de_loup", 3) }, bag.Stacks);
        foreach ((int place, int count, InventoryRefusal refusal) in new[] { (0, 4, InventoryRefusal.NotEnough), (0, 0, InventoryRefusal.NotEnough),
            (1, 2, InventoryRefusal.NotEnough), (3, 1, InventoryRefusal.NothingThere), (-1, 1, InventoryRefusal.NothingThere) })
        {
            Assert.Equal(refusal, bag.RemoveAt(place, count, out ItemStack? none));
            Assert.Null(none);
        }
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), rolled, Base("item.peau_de_loup", 3) }, bag.Stacks);
        Assert.Equal(InventoryRefusal.None, bag.RemoveAt(1, 1, out taken));
        Assert.Equal(rolled, taken);
        Assert.Equal(new[] { Base("item.peau_de_loup", 3), Base("item.peau_de_loup", 3) }, bag.Stacks);
    }

    // --- Mineur 3 : instance relue accordée au catalogue actuel ---------------------------------------------------------------------

    private static (int Low, int High) BoundsOf(string property, string item = Boots)
    {
        Assert.True(ItemsCatalog.TryGetProperty(property, out PropertyDefinition p));
        return ItemsCatalog.Bounds(p, ItemsCatalog.Item(item));
    }

    private static ItemStack With(params (string Id, int Value)[] properties) =>
        new ItemStack(Boots, 1, properties.Select(p => new RolledProperty(p.Id, p.Value)));

    [Fact]
    public void Instance_accordee_au_catalogue_valeur_ramenee_ou_propriete_retiree()
    {
        (int low, int high) = BoundsOf("property.solidite");
        // Déjà accordée : rendue telle quelle, sans compte rendu.
        ItemStack ok = With(("property.solidite", low));
        Assert.Same(ok, ItemsCatalog.Settle(ok, out IReadOnlyList<string> none));
        Assert.Empty(none);
        // Au-dessus, au-dessous : ramenée à la borne la plus proche.
        Assert.Equal(With(("property.solidite", high)), ItemsCatalog.Settle(With(("property.solidite", high + 1)), out IReadOnlyList<string> above));
        Assert.Contains($"ramenée de {high + 1} à {high}", Assert.Single(above), StringComparison.Ordinal);
        Assert.Equal(With(("property.solidite", low)), ItemsCatalog.Settle(With(("property.solidite", low - 1)), out _));
        // Propriété inconnue, ou qui ne va plus à cette place : retirée (seule, l'objet redevient de base).
        Assert.Equal(Base(), ItemsCatalog.Settle(With(("property.inconnue", 3)), out IReadOnlyList<string> unknown));
        Assert.Contains("absente du catalogue", Assert.Single(unknown), StringComparison.Ordinal);
        Assert.Equal(Base(), ItemsCatalog.Settle(With(("property.force", 3)), out _));
        // Trop de propriétés pour sa rareté (rare : une) : les dernières retirées ; doublon retiré.
        Assert.Equal(With(("property.solidite", low)), ItemsCatalog.Settle(With(("property.solidite", low), ("property.souffle", 1)), out IReadOnlyList<string> extra));
        Assert.Single(extra);
        Assert.Equal(With(("property.souffle", 1)), ItemsCatalog.Settle(With(("property.inconnue", 2), ("property.souffle", 1)), out _));
        // Objet qui ne tire pas (nommé) : redevient l'objet de base.
        Assert.Equal(Base("item.lame_du_seuil"), ItemsCatalog.Settle(new ItemStack("item.lame_du_seuil", 1, new[] { new RolledProperty("property.force", 5) }), out _));
        // Objet de base : inchangé.
        ItemStack plain = Base();
        Assert.Same(plain, ItemsCatalog.Settle(plain, out IReadOnlyList<string> nothing));
        Assert.Empty(nothing);
    }

    [Fact]
    public void Propriete_toute_instance_relue_accordee_passe_le_validateur()
    {
        var rng = new Random(78);
        foreach (ItemDefinition item in ItemsCatalog.Items.Where(ItemsCatalog.IsRollable))
            for (int n = 0; n < 40; n++)
            {
                var props = new List<RolledProperty>();
                int k = rng.Next(1, SaveValidator.MaxProperties + 1);
                for (int j = 0; j < k; j++)
                {
                    string id = rng.Next(8) == 0 ? "property.inconnue" : ItemsCatalog.Properties[rng.Next(ItemsCatalog.Properties.Count)].Id;
                    props.Add(new RolledProperty(id, rng.Next(1, 400)));
                }
                ItemStack settled = ItemsCatalog.Settle(new ItemStack(item.Id, 1, props), out _);
                Assert.Null(ItemsCatalog.CheckInstance(settled));
                // Accorder deux fois ne change plus rien.
                Assert.Same(settled, ItemsCatalog.Settle(settled, out IReadOnlyList<string> again));
                Assert.Empty(again);
            }
    }

    [Fact]
    public void Reglage_du_catalogue_ne_rend_pas_la_partie_illisible()
    {
        // Instance tirée sur le catalogue livré (Solidité au plus haut), portée et au sac ; puis le catalogue baisse les bornes de la Solidité
        // et renomme la Vigueur (l'ancienne n'existe plus).
        int high = BoundsOf("property.solidite").High;
        ItemStack worn = With(("property.solidite", high)), carried = new ItemStack(Boots, 1, new[] { new RolledProperty("property.vigueur", BoundsOf("property.vigueur").High) });
        SavedCharacter hero = Bastion(10, new[] { carried }, new Dictionary<string, string> { ["feet"] = Boots },
            new Dictionary<string, IReadOnlyList<RolledProperty>> { ["feet"] = worn.Properties });
        string items = File.ReadAllText(ItemJsonReader.PathIn(DataDirectory));
        JsonObject root = JsonNode.Parse(items)!.AsObject();
        JsonArray list = root["data"]!["properties"]!["list"]!.AsArray();
        JsonObject solidite = list.Single(p => (string)p!["id"]! == "property.solidite")!.AsObject();
        solidite["maxPerLevel"] = 2;
        list.Single(p => (string)p!["id"]! == "property.vigueur")!["id"] = "property.vigueur_nouvelle";
        ItemCatalogLoadResult tuned = ItemJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(tuned.IsValid, tuned.Report.ToString());
        ItemCatalog catalog = tuned.Catalog!;
        Assert.Null(Record.Exception(() => Inventory.From(hero, catalog, 10)));
        Inventory bag = Inventory.From(hero, catalog, 10, out IReadOnlyList<string> adjusted);
        Assert.Equal(2, adjusted.Count);
        Assert.Contains("« property.vigueur » retirée", adjusted[0], StringComparison.Ordinal);       // le sac d'abord
        Assert.Contains("Solidité ramenée", adjusted[1], StringComparison.Ordinal);
        Assert.True(catalog.TryGetProperty("property.solidite", out PropertyDefinition p));
        Assert.Equal(new ItemStack(Boots, 1, new[] { new RolledProperty("property.solidite", catalog.Bounds(p, catalog.Item(Boots)).Max) }), bag.WornInstanceAt("feet"));
        Assert.Equal(new[] { Base() }, bag.Stacks);
        // Ce qui est sauvegardé ensuite est accordé, et se relit sans changement.
        Inventory.From(bag.SaveInto(hero), catalog, 10, out IReadOnlyList<string> second);
        Assert.Empty(second);
    }

    [Fact]
    public void Partie_reprise_avec_le_compte_rendu_et_le_message()
    {
        // La sauvegarde garde une valeur au-delà des bornes actuelles (9 999, sous le plafond de forme de la sauvegarde).
        ItemStack far = With(("property.solidite", 9999));
        WorldSession w = Play(Zone(), Bastion(10, new[] { far }));
        string note = Assert.Single(w.Adjusted);
        Assert.Contains("ramenée de 9999", note, StringComparison.Ordinal);
        Assert.Equal(With(("property.solidite", BoundsOf("property.solidite").High)), w.Inventory.Stacks.Single());
        Assert.Empty(Play(Zone(), Bastion(10, new[] { Rolled() })).Adjusted);
        // Dans l'aventure : un message le dit à l'ouverture.
        string save = Path.Combine(_folder, "reprise", "sauvegarde.json");
        SaveStore.Write(save, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Bastion(10, new[] { far })));
        AdventureSession a = Adventure(save);
        Assert.Contains(a.DrainMessages(), m => m.StartsWith("Objets accordés au catalogue : ", StringComparison.Ordinal) && m.Contains("ramenée de 9999", StringComparison.Ordinal));
    }

    // --- Mineur 4 : altérations posées par l'équipement, dans les règles -------------------------------------------------------------

    private static RulesLoadResult Rules(Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "rules.json")))!.AsObject();
        change(root["data"]!["combat"]!["equipment"]!.AsObject());
        return RulesJsonReader.Read(root.ToJsonString());
    }

    [Fact]
    public void Alterations_de_l_equipement_lues_et_verifiees_avec_leur_chemin()
    {
        const string path = "$.data.combat.equipment.allowedStatuses";
        Assert.Empty(ShippedData.Rules.Combat.EquipmentStatuses);
        RulesLoadResult ok = Rules(e => e["allowedStatuses"] = new JsonArray("fragility", "burn"));
        Assert.True(ok.IsValid, ok.Report.ToString());
        Assert.Equal(new[] { "fragility", "burn" }, ok.Rules!.Combat.EquipmentStatuses);
        Assert.NotEqual(ShippedData.Rules.Fingerprint, ok.Rules.Fingerprint);
        Assert.NotEqual(ok.Rules.Fingerprint, Rules(e => e["allowedStatuses"] = new JsonArray("burn", "fragility")).Rules!.Fingerprint);   // l'ordre compte
        ValidationError Only(Action<JsonObject> change) => Assert.Single(Rules(change).Report.Errors);
        ValidationError unknown = Only(e => e["allowedStatuses"] = new JsonArray("inconnue"));
        Assert.Equal((path, ValidationCode.UnknownReference), (unknown.Path, unknown.Code));
        Assert.Equal(path, Only(e => e["allowedStatuses"] = new JsonArray("burn", "burn")).Path);
        Assert.Equal(path, Only(e => e["allowedStatuses"] = new JsonArray("")).Path);
        Assert.Equal(path + "[0]", Only(e => e["allowedStatuses"] = new JsonArray(3)).Path);
        Assert.Equal(path, Only(e => e["allowedStatuses"] = "burn").Path);
        Assert.Equal(path, Only(e => e.Remove("allowedStatuses")).Path);
    }

    // --- Rang du tirage : le rang dans la table ----------------------------------------------------------------------------------------

    [Fact]
    public void Proprietes_tirees_au_rang_de_l_objet_dans_sa_table()
    {
        // Table d'essai : des peaux (rang 0, 50 %) puis les bottes (rang 1, toujours). Que les peaux tombent ou non, les bottes d'une même
        // mort ont les propriétés du rang 1.
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["loot"]![0]!["drops"] = new JsonArray(
            new JsonObject { ["item"] = "item.peau_de_loup", ["count"] = 1, ["chancePercent"] = 50 },
            new JsonObject { ["item"] = Boots, ["count"] = 1, ["chancePercent"] = 100 });
        ItemCatalog catalog = ItemJsonReader.Read(root.ToJsonString(), ShippedData).Catalog!;
        Assert.True(catalog.TryGetLoot((string)root["data"]!["loot"]![0]!["id"]!, out LootTable table));
        bool with = false, without = false;
        for (int i = 0; i < 64; i++)
        {
            string key = $"zone.vallons/{i}/death/loup1";
            IReadOnlyList<LootRoll> rolls = Loot.Roll(table, key);
            LootRoll boots = rolls.Single(r => r.Item == Boots);
            Assert.Equal(1, boots.Rank);
            if (rolls.Count == 2) { with = true; Assert.Equal(0, rolls[0].Rank); } else without = true;
            Assert.Equal(ItemRolls.Roll(catalog, Boots, 1, key, 1), ItemRolls.Roll(catalog, Boots, 1, key, boots.Rank));
        }
        Assert.True(with && without);
    }

    // --- Remarque de Codex (tour 2) : cumul des bonus de l'équipement porté ----------------------------------------------------------

    /// <summary>Catalogue des objets livré, modifié par <paramref name="change"/> sur son JSON, relu par le validateur.</summary>
    private static ItemCatalogLoadResult Items(Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        change(root["data"]!.AsObject());
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    /// <summary>
    /// Le scénario de Codex : les objets rares tirent huit propriétés, et huit propriétés distinctes de puissance physique valent 50 000
    /// chacune : trois objets (une arme, deux anneaux) en portent déjà 1 200 000, au-delà de la limite du combat.
    /// </summary>
    private static void EightStrongProperties(JsonObject data, int value)
    {
        // Échelle de 100 % partout : une propriété peut alors valoir 50 000 (le plafond d'une valeur tirée).
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
    public void Catalogue_au_pire_cumul_trop_fort_refuse_avec_son_chemin()
    {
        ItemCatalogLoadResult refused = Items(d => EightStrongProperties(d, 50_000));
        Assert.False(refused.IsValid);
        ValidationError e = Assert.Single(refused.Report.Errors);
        Assert.Equal(("$.data.properties.list[6].max", ValidationCode.LimitExceeded), (e.Path, e.Code));
        Assert.Contains("physicalPower", e.Message, StringComparison.Ordinal);
        Assert.Contains($"au plus {GearBonus.MaxValue}", e.Message, StringComparison.Ordinal);
        // Les mêmes propriétés, faibles : admises.
        Assert.True(Items(d => EightStrongProperties(d, 10)).IsValid);
        // Des bonus de base trop forts sur toutes les places : refusés à l'objet le plus fort.
        ItemCatalogLoadResult heavy = Items(d =>
        {
            foreach (JsonNode? item in d["items"]!.AsArray())
                if ((string)item!["type"]! == "equipment" && (string?)item["slot"] != "tool") item["bonus"] = new JsonObject { ["armor"] = 50_000 };
            foreach (JsonNode? set in d["sets"]!.AsArray())
                foreach (JsonNode? tier in set!["bonuses"]!.AsArray()) tier!["bonus"] = new JsonObject { ["armor"] = 50_000 };
        });
        ValidationError armor = Assert.Single(heavy.Report.Errors);
        Assert.StartsWith("$.data.items[", armor.Path, StringComparison.Ordinal);
        Assert.EndsWith("].bonus.armor", armor.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Pire_cumul_du_catalogue_livre_dans_la_limite_du_combat()
    {
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long bases, long rolled, long sets) = ItemsCatalog.WornBound(stat);
            Assert.InRange(bases + rolled + sets, 0, GearBonus.MaxValue);
        }
        // La borne est atteinte par l'objet le plus fort de chaque place : elle n'est jamais sous ce que porte un personnage.
        Inventory bag = Bag(ItemsCatalog.Items.Where(i => i.Type == ItemType.Equipment && ItemsCatalog.PlacesFor(i.Slot).Count > 0)
            .GroupBy(i => i.Slot).Select(g => Base(g.OrderByDescending(i => i.Bonus.PhysicalPower).First().Id)).ToArray());
        for (int k = bag.Stacks.Count - 1; k >= 0; k--) bag.Equip(k, 99);
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long bases, long rolled, long sets) = ItemsCatalog.WornBound(stat);
            Assert.True(bag.Gear()[stat] <= bases + rolled + sets, GearBonus.Fields[stat]);
        }
    }

    /// <summary>
    /// Catalogue NON validé (le constructeur interne, par réflexion), pour éprouver les gardes de l'inventaire : la Force tirée vaut 400 000.
    /// Un catalogue validé ne peut pas en arriver là.
    /// </summary>
    private static ItemCatalog Unchecked()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object force = typeof(PropertyDefinition).GetConstructors(flags).Single().Invoke(new object[]
            { "property.force", "Force", GearBonus.FieldIndex("physicalPower"), 400_000, 400_000, 0, 0, new[] { "weapon", "ring" } });
        var properties = ItemsCatalog.Properties.Select(p => p.Id == "property.force" ? (PropertyDefinition)force : p).ToList();
        return (ItemCatalog)typeof(ItemCatalog).GetConstructors(flags).Single().Invoke(new object?[]
        {
            ItemsCatalog.SchemaVersion, ItemsCatalog.ContentVersion, ItemsCatalog.Places, ItemsCatalog.InventorySize, ItemsCatalog.Starting,
            ItemsCatalog.Items, ItemsCatalog.Loot, ItemsCatalog.Sets, properties, ItemsCatalog.RarityRolls,
            ItemsCatalog.SuperiorPercent, ItemsCatalog.MasterworkPercent, ItemsCatalog.WearMax, ItemsCatalog.WearPerFullHealth,   // sous-lot V1.4
        });
    }

    private static ItemStack Strong(string item) => new ItemStack(item, 1, new[] { new RolledProperty("property.force", 400_000) });

    [Fact]
    public void Porter_ne_depasse_jamais_la_limite_du_combat()
    {
        ItemCatalog catalog = Unchecked();
        ItemStack sword = Strong("item.epee_de_garde"), first = Strong("item.anneau_du_guetteur"), second = Strong("item.anneau_de_force");
        Assert.All(new[] { sword, first, second }, x => Assert.Null(catalog.CheckInstance(x)));
        Inventory bag = Inventory.From(Bastion(10, new[] { sword, first, second, Base("item.anneau_de_cuivre") }), catalog, 10);
        Assert.Equal(InventoryRefusal.None, bag.EquipTo(0, "weapon", 10));
        Assert.Equal(InventoryRefusal.None, bag.EquipTo(0, "ring_1", 10));
        Assert.Equal(InventoryRefusal.None, bag.EquipTo(1, "ring_2", 10));   // l'anneau de cuivre
        string Seen(Inventory i) => string.Join(",", i.Stacks) + "|" + string.Join(",", i.Equipped.Select(e => e.Key + "=" + e.Value))
            + "|" + i.WornInstanceAt("ring_1") + "|" + i.WornInstanceAt("ring_2");
        // À la place de l'anneau de cuivre, le troisième objet fort mènerait la puissance physique au-delà de 1 000 000 : refusé, l'échange
        // est défait.
        string before = Seen(bag);
        Assert.Equal(InventoryRefusal.GearLimit, bag.EquipTo(0, "ring_2", 10));
        Assert.Equal(before, Seen(bag));
        // Sur une place libre : refusé aussi, rien ne change.
        Assert.Equal(InventoryRefusal.None, bag.Unequip("ring_2"));
        before = Seen(bag);
        Assert.Equal(InventoryRefusal.GearLimit, bag.EquipTo(0, "ring_2", 10));
        Assert.Equal(InventoryRefusal.GearLimit, bag.Equip(0, 10));
        Assert.Equal(before, Seen(bag));
        Assert.True(bag.GearWithinCombatLimit);
        Assert.True(Inventory.WithinCombatLimit(bag.Gear()));
    }

    [Fact]
    public void Sauvegarde_au_dela_de_la_limite_reprise_sans_refus_du_combat()
    {
        ItemCatalog catalog = Unchecked();
        ItemStack sword = Strong("item.epee_de_garde"), first = Strong("item.anneau_du_guetteur"), second = Strong("item.anneau_de_force");
        SavedCharacter hero = Bastion(10, Array.Empty<ItemStack>(),
            new Dictionary<string, string> { ["weapon"] = sword.Item, ["ring_1"] = first.Item, ["ring_2"] = second.Item },
            new Dictionary<string, IReadOnlyList<RolledProperty>> { ["weapon"] = sword.Properties, ["ring_1"] = first.Properties, ["ring_2"] = second.Properties });
        Inventory bag = Inventory.From(hero, catalog, 10, out IReadOnlyList<string> adjusted);
        Assert.False(bag.GearWithinCombatLimit);
        Assert.Equal(GearBonus.MaxValue, bag.Gear().PhysicalPower);
        Assert.True(Inventory.WithinCombatLimit(bag.Gear()));
        Assert.Contains(adjusted, n => n.Contains("physicalPower", StringComparison.Ordinal) && n.Contains("limite du combat", StringComparison.Ordinal));
        // La partie reprend : le combat admet le personnage (la session entre dans sa zone).
        ZoneDefinition zone = Zone();
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, catalog, Book(zone),
            market: new Market(ShippedMerchants));
        Assert.Equal(GearBonus.MaxValue, session.EnteredGear.PhysicalPower);
        Assert.Contains(session.Adjusted, n => n.Contains("physicalPower", StringComparison.Ordinal));
    }

    // --- Outils ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Copie des données et scénarios livrés, le point d'entrée déplacé en <paramref name="entry"/>.</summary>
    private (string Data, string Scenarios) DataCopy((double X, double Z) entry)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(ScenariosDirectory, scenarios);
        string zone = ZoneJsonReader.PathIn(data, "zone.vallons");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(entry.X, entry.Z);
        File.WriteAllText(zone, root.ToJsonString());
        return (data, scenarios);
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
