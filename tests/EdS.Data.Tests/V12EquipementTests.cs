using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.2 : équipement complet. Catalogue des objets en version 3 (18 places, garde des boucliers, équipement de départ, quelques
/// dizaines d'objets des niveaux 1 à 20), inventaire aux places obligatoires, échange et rangement, sauvegarde en version 7 (une sauvegarde
/// de version 6 se relit), garde du bouclier portée au combat, une seule règle « hors combat » (<see cref="WorldSession.InCombat"/>) et la
/// comparaison de l'écran.
/// </summary>
[Trait("Category", "World")]
public class V12EquipementTests
{
    private static string CatalogPath => ItemJsonReader.PathIn(DataDirectory);

    private static ItemCatalogLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CatalogPath))!.AsObject();
        mutate(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();
    private static JsonObject Slot(JsonObject root, string id) => Data(root)["slots"]!.AsArray().Single(s => (string)s!["id"]! == id)!.AsObject();
    private static JsonObject ItemOf(JsonObject root, string id) => Data(root)["items"]!.AsArray().Single(i => (string)i!["id"]! == id)!.AsObject();
    private static int IndexOf(string id) => ItemsCatalog.Items.ToList().FindIndex(i => i.Id == id);

    private static void AssertError(ItemCatalogLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    /// <summary>Personnage du Bastion au niveau donné, sans rien (comme avant le sous-lot), avec ces objets dans le sac.</summary>
    private static SavedCharacter Bare(int level, params string[] items)
    {
        long xp = Progression.XpToLevel[level - 1];
        return PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(xp)
            .WithItems(items.Select(i => new ItemStack(i, 1)), new Dictionary<string, string>());
    }

    private static string State(Inventory inventory) =>
        string.Join(",", inventory.Stacks.Select(s => s.ToString())) + "|" + string.Join(",", inventory.Equipped.Select(e => e.Key + "=" + e.Value));

    // --- Catalogue ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Dix_huit_places_et_leurs_regles()
    {
        ItemCatalog c = ItemsCatalog;
        Assert.Equal((7, 18, 24), (c.SchemaVersion, c.Places.Count, c.InventorySize));   // V1.3 : catalogue en version 4 ; V1.4 : 5 ; V1.8 : 6 ; V1.8c : 7
        Assert.Equal(16, c.Places.Count(p => p.Group == PlaceGroup.Combat));   // arme, bouclier, dix pièces, deux anneaux, deux reliques
        Assert.Equal(("shard", PlaceGroup.Shard, true), (c.Places[16].Id, c.Places[16].Group, c.Places[16].Required));
        Assert.Equal(("tool", PlaceGroup.Tool), (c.Places[17].Id, c.Places[17].Group));
        Assert.Equal(new[] { "ring_1", "ring_2" }, c.PlacesFor("ring").Select(p => p.Id));
        Assert.Equal(new[] { "relic_1", "relic_2" }, c.PlacesFor("relic").Select(p => p.Id));
        EquipmentPlace shield = c.GuardPlace!;
        Assert.Equal(("shield", true, false), (shield.Id, shield.IsRequiredFor("class.bastion"), shield.IsRequiredFor("class.autre")));
        // Réponses du 31 octobre 2026 : l'épée et l'écu du novice remplacent l'épée du milicien et l'écu de chêne.
        Assert.Equal(new[] { "item.eclat_du_seuil_endormi", "item.epee_du_novice", "item.ecu_du_novice" }, c.StartingFor("class.bastion"));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "items.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(CatalogPath)).RootElement));
    }

    [Fact]
    public void Catalogue_des_niveaux_1_a_20_avec_plusieurs_bons_choix()
    {
        List<ItemDefinition> gear = ItemsCatalog.Items.Where(i => i.Type == ItemType.Equipment && i.Slot is not ("shard" or "tool")).ToList();
        Assert.InRange(gear.Count, 60, 80);   // quelques dizaines d'objets
        Assert.Equal((1, 20), (gear.Min(i => i.RequiredLevel), gear.Max(i => i.RequiredLevel)));
        // Chaque place du combat a des objets ; chaque tranche de cinq niveaux a plusieurs choix de même rareté pour une même place.
        foreach (string kind in ItemsCatalog.Places.Where(p => p.Group == PlaceGroup.Combat).Select(p => p.Accepts).Distinct())
            Assert.True(gear.Count(i => i.Slot == kind) >= 3, kind);
        foreach (int band in new[] { 1, 6, 11, 16 })
        {
            var inBand = gear.Where(i => i.RequiredLevel >= band && i.RequiredLevel < band + 5).ToList();
            Assert.Contains(inBand.GroupBy(i => (i.Slot, i.Rarity)), g => g.Count() >= 2);
        }
        // Quatre notions distinctes (H5) : trois boucliers de niveau 5, même rareté, gardes et adéquations différentes.
        var five = gear.Where(i => i.Slot == "shield" && i.RequiredLevel == 5 && i.Rarity == ItemRarity.Rare).ToList();
        Assert.Equal(3, five.Count);
        Assert.Equal(3, five.Select(i => i.Guard).Distinct().Count());
        Assert.Equal(3, five.Select(i => i.Fits.Single()).Distinct().Count());
        // La rareté n'est pas la puissance : la lame du Seuil (épique, niveau 8) bat moins fort que l'épée du capitaine (très rare, niveau 15).
        Assert.True(ItemsCatalog.Item("item.lame_du_seuil").Bonus.PhysicalPower < ItemsCatalog.Item("item.epee_du_capitaine").Bonus.PhysicalPower);
        // Tous les boucliers ont une garde jouable, et rien d'autre n'en a.
        Assert.All(gear, i => Assert.Equal(i.Slot == "shield", i.Guard != null));
        Assert.All(gear.Where(i => i.Guard != null), i => Assert.Empty(i.Guard!.Check(ShippedData.Rules.Combat, "guard")));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), ItemsCatalog.Item("item.ecu_de_chene").Guard);   // les valeurs des règles
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.7"), 14, 100), ItemsCatalog.Item("item.pavois_du_moulin").Guard);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.5"), 6, 200), ItemsCatalog.Item("item.rondache_cerclee").Guard);
        // Les outils n'ont aucun bonus de combat ; l'Éclat de départ non plus (il grandira avec l'histoire).
        Assert.All(ItemsCatalog.Items.Where(i => i.Slot is "tool" or "shard"), i => Assert.True(i.Bonus.IsNone, i.Id));
    }

    [Fact]
    public void Prix_des_objets_et_offres_de_Garin()
    {
        var market = new Market(ShippedMerchants);
        // V2.6.1 : sauf le chapelet de l'Aube, récompense unique de la quête de Sœur Albe (réponse de Damien du 7 novembre 2026 : il ne se vend pas).
        foreach (ItemDefinition item in ItemsCatalog.Items.Where(i => i.Type == ItemType.Equipment && i.Slot != "shard" && i.Id != "item.chapelet_de_l_aube"))
            Assert.True(ShippedMerchants.Values.ContainsKey(item.Id), item.Id);
        Assert.False(ShippedMerchants.Values.ContainsKey("item.chapelet_de_l_aube"));
        Assert.False(ShippedMerchants.Values.ContainsKey("item.eclat_du_seuil_endormi"));   // l'Éclat du personnage ne se vend pas
        var garin = ShippedMerchants.Merchant("merchant.armurier");
        foreach (string item in new[] { "item.targe_d_osier", "item.coiffe_de_cuir", "item.bottes_de_cuir", "item.cape_de_laine" })
            Assert.Contains(garin.Offers, o => o.Item == item && o.Always);
        foreach (string item in new[] { "item.ecu_du_garde", "item.casque_de_fer", "item.jambieres_de_mailles", "item.cape_de_voyageur" })
            Assert.Contains(garin.Offers, o => o.Item == item && !o.Always);
        Assert.Equal(2400, garin.Offers.Single(o => o.Item == "item.ecu_du_garde").Price);   // valeur de base × 1,3, arrondie à la dizaine
        Assert.Equal(1850, ShippedMerchants.Values["item.ecu_du_garde"]);   // rare, niveau 5 : 600 + 250 × 5
    }

    [Fact]
    public void Catalogue_invalide_refuse_avec_son_chemin()
    {
        int pavois = IndexOf("item.pavois_du_moulin"), sword = IndexOf("item.epee_du_milicien"), pick = IndexOf("item.pioche_du_carrier");
        AssertError(Mutated(r => ItemOf(r, "item.pavois_du_moulin")["guard"]!["damageReduction"] = 0.75), $"$.data.items[{pavois}].guard.damageReduction");
        AssertError(Mutated(r => ItemOf(r, "item.pavois_du_moulin")["guard"]!["parryWindowMs"] = 250), $"$.data.items[{pavois}].guard.parryWindowMs");
        AssertError(Mutated(r => ItemOf(r, "item.pavois_du_moulin").Remove("guard")), $"$.data.items[{pavois}].guard");
        AssertError(Mutated(r => ItemOf(r, "item.epee_du_milicien")["guard"] = JsonNode.Parse("{\"damageReduction\":0.6,\"costPerBlock\":10,\"parryWindowMs\":150}")),
            $"$.data.items[{sword}].guard");
        AssertError(Mutated(r => ItemOf(r, "item.pioche_du_carrier")["bonus"] = JsonNode.Parse("{\"armor\":1}")), $"$.data.items[{pick}].bonus");
        AssertError(Mutated(r => ItemOf(r, "item.epee_du_milicien")["fits"] = new JsonArray("tree.bastion.inconnu")), $"$.data.items[{sword}].fits[0]");
        AssertError(Mutated(r => ItemOf(r, "item.epee_du_milicien")["slot"] = "ring_1"), $"$.data.items[{sword}].slot");   // une place, pas une sorte
        AssertError(Mutated(r => Slot(r, "shield")["requiredBy"] = new JsonArray("class.inconnue")), "$.data.slots[1].requiredBy[0]");
        AssertError(Mutated(r => Slot(r, "shield")["required"] = true), "$.data.slots[1].requiredBy");
        AssertError(Mutated(r => Slot(r, "head")["guard"] = true), "$.data.slots");                    // une seule place porte la garde
        AssertError(Mutated(r => Slot(r, "shard")["guard"] = true), "$.data.slots[16].guard");
        AssertError(Mutated(r => Slot(r, "ring_2")["group"] = "tool"), "$.data.slots[13].accepts");    // deux places d'une sorte, même groupe
        AssertError(Mutated(r => Slot(r, "tool")["group"] = "outil"), "$.data.slots[17].group");
        AssertError(Mutated(r => ItemOf(r, "item.epee_du_milicien")["icon"] = "Épée"), $"$.data.items[{sword}].icon");
        Assert.Equal("epee_de_garde", ItemsCatalog.Item("item.epee_longue_d_aube").Icon);   // icône provisoire d'un objet voisin
        Assert.Equal("epee_du_milicien", ItemsCatalog.Item("item.epee_du_milicien").Icon);  // sans icône : la fin de l'identifiant
        // L'équipement de départ remplit les places obligatoires, au niveau 1, sans trop d'objets d'une sorte.
        AssertError(Mutated(r => Data(r)["starting"]!.AsArray().RemoveAt(0)), "$.data.starting");
        AssertError(Mutated(r => Data(r)["starting"]!.AsArray().RemoveAt(2)), "$.data.starting");
        AssertError(Mutated(r => Data(r)["starting"]!.AsArray().Add(JsonNode.Parse("{\"item\":\"item.ecu_de_chene\"}"))), "$.data.starting");   // deux boucliers
        AssertError(Mutated(r => Data(r)["starting"]![1]!["item"] = "item.epee_de_garde"), "$.data.starting[1].item");
        AssertError(Mutated(r => Data(r)["starting"]![1]!["item"] = "item.peau_de_loup"), "$.data.starting[1].item");
        AssertError(Mutated(r => Data(r)["starting"]![1]!["class"] = "class.inconnue"), "$.data.starting[1].class");
        AssertError(Mutated(r => Data(r).Remove("starting")), "$.data.starting");
        // Un catalogue de version 2 (quatre emplacements en texte) est refusé sans migration.
        Assert.False(Mutated(r => r["schemaVersion"] = 2).IsValid);
        Assert.True(Mutated(r => Data(r)["starting"]!.AsArray().Add(JsonNode.Parse("{\"item\":\"item.gants_de_cuir\"}"))).IsValid);
    }

    // --- Inventaire --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Anneaux_et_reliques_vont_au_premier_doigt_libre_puis_s_echangent()
    {
        var inv = Inventory.From(Bare(10, "item.anneau_de_cuivre", "item.anneau_de_fer", "item.anneau_de_force"), ItemsCatalog, 10);
        Assert.Equal(InventoryRefusal.None, inv.Equip(0, 10));
        Assert.Equal(InventoryRefusal.None, inv.Equip(0, 10));
        Assert.Equal(("item.anneau_de_cuivre", "item.anneau_de_fer"), (inv.Equipped["ring_1"], inv.Equipped["ring_2"]));
        Assert.Equal("ring_1", inv.PlaceFor("item.anneau_de_force"));   // les deux sont pris : il remplacerait le premier
        Assert.Equal(InventoryRefusal.None, inv.EquipTo(0, "ring_2", 10));
        Assert.Equal(("item.anneau_de_force", "item.anneau_de_fer"), (inv.Equipped["ring_2"], inv.Stacks[0].Item));
        Assert.Equal(InventoryRefusal.None, inv.Swap("ring_1", "ring_2"));
        Assert.Equal(("item.anneau_de_force", "item.anneau_de_cuivre"), (inv.Equipped["ring_1"], inv.Equipped["ring_2"]));
        Assert.Equal(15, inv.Gear().MaxHealth);
        Assert.Equal(8, inv.Gear().PhysicalPower);
        Assert.Equal(InventoryRefusal.None, inv.Unequip("ring_2"));
        Assert.Equal(InventoryRefusal.None, inv.Swap("ring_1", "ring_2"));   // vers une place vide
        Assert.Equal("item.anneau_de_force", inv.Equipped["ring_2"]);
        Assert.False(inv.Equipped.ContainsKey("ring_1"));
    }

    [Fact]
    public void Refus_atomiques_avec_leur_motif()
    {
        SavedCharacter hero = Inventory.Outfit(Bare(3, "item.anneau_de_cuivre", "item.casque_de_fer", "item.peau_de_loup", "item.targe_d_osier"), ItemsCatalog);
        var inv = Inventory.From(hero, ItemsCatalog, 3);
        string before = State(inv);
        Assert.Equal(InventoryRefusal.WrongSlot, inv.EquipTo(0, "neck", 3));          // un anneau au cou
        Assert.Equal(InventoryRefusal.WrongSlot, inv.EquipTo(0, "inconnue", 3));
        Assert.Equal(InventoryRefusal.LevelTooLow, inv.Equip(1, 3));                  // casque de niveau 6
        Assert.Equal(InventoryRefusal.NotEquipment, inv.Equip(2, 3));
        Assert.Equal(InventoryRefusal.NothingThere, inv.Equip(9, 3));
        Assert.Equal(InventoryRefusal.Required, inv.Unequip("shield"));               // le Bastion porte toujours un bouclier
        Assert.Equal(InventoryRefusal.Required, inv.Unequip("shard"));                // l'Éclat du Seuil ne s'ôte pas
        Assert.Equal(InventoryRefusal.NothingThere, inv.Unequip("ring_1"));
        Assert.Equal(InventoryRefusal.WrongSlot, inv.Swap("ring_1", "neck"));
        Assert.Equal(InventoryRefusal.WrongSlot, inv.Swap("ring_1", "ring_1"));
        Assert.Equal(InventoryRefusal.NothingThere, inv.Swap("ring_1", "ring_2"));
        Assert.Equal(before, State(inv));
        // Échanger le bouclier est permis : l'écu revient dans le sac, à la place de la targe.
        Assert.Equal(InventoryRefusal.None, inv.Equip(3, 3));
        Assert.Equal(("item.targe_d_osier", "item.ecu_du_novice"), (inv.Equipped["shield"], inv.Stacks[3].Item));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.5"), 6, 200), inv.Shield);
        // Sac plein : ôter est refusé, rien ne change.
        var full = Inventory.From(Bare(3), ItemsCatalog, 3);
        for (int i = 0; i < ItemsCatalog.InventorySize; i++) full.Add("item.gambison", 1);
        full.Add("item.anneau_de_cuivre", 1);
        Assert.Equal(InventoryRefusal.Full, full.Add("item.anneau_de_cuivre", 1));
        var oneFree = Inventory.From(Bare(3, "item.anneau_de_cuivre"), ItemsCatalog, 3);
        oneFree.Equip(0, 3);
        for (int i = 0; i < ItemsCatalog.InventorySize; i++) oneFree.Add("item.gambison", 1);
        string snapshot = State(oneFree);
        Assert.Equal(InventoryRefusal.Full, oneFree.Unequip("ring_1"));
        Assert.Equal(snapshot, State(oneFree));
    }

    [Fact]
    public void Proprietes_les_operations_gardent_les_objets()
    {
        // Propriété : quelle que soit la suite d'opérations (graine fixe, 400 opérations au hasard), aucun objet n'apparaît ni ne disparaît,
        // les places obligatoires restent occupées et chaque objet porté est à une place de sa sorte.
        var rng = new Simulation.Randomness.Pcg32(2026, 1);
        string[] pool = { "item.anneau_de_cuivre", "item.anneau_de_fer", "item.dent_de_loup", "item.pierre_gravee", "item.targe_d_osier", "item.rondache_cerclee",
            "item.coiffe_de_cuir", "item.casque_de_fer", "item.pioche_du_carrier", "item.peau_de_loup", "item.potion_de_soin" };
        var inv = Inventory.From(Inventory.Outfit(Bare(10, pool), ItemsCatalog), ItemsCatalog, 10);
        int Total() => inv.Stacks.Sum(s => s.Count) + inv.Equipped.Count;
        int total = Total();
        string[] places = ItemsCatalog.Slots.ToArray();
        for (int k = 0; k < 400; k++)
        {
            int op = (int)rng.NextBounded(5);
            string before = State(inv);
            InventoryRefusal r = op switch
            {
                0 => inv.Equip((int)rng.NextBounded(14), 10),
                1 => inv.EquipTo((int)rng.NextBounded(14), places[rng.NextBounded((uint)places.Length)], 10),
                2 => inv.Unequip(places[rng.NextBounded((uint)places.Length)]),
                3 => inv.Swap(places[rng.NextBounded((uint)places.Length)], places[rng.NextBounded((uint)places.Length)]),
                _ => InventoryRefusal.None,
            };
            if (op == 4) inv.Sort((InventorySort)rng.NextBounded(3));
            if (r != InventoryRefusal.None) Assert.Equal(before, State(inv));
            Assert.Equal(total, Total());
            Assert.True(inv.Equipped.ContainsKey("shield") && inv.Equipped.ContainsKey("shard"));
            foreach (KeyValuePair<string, string> e in inv.Equipped)
            {
                Assert.True(ItemsCatalog.TryGetPlace(e.Key, out EquipmentPlace p));
                Assert.Equal(p.Accepts, ItemsCatalog.Item(e.Value).Slot);
            }
        }
    }

    [Fact]
    public void Rangement_et_filtres()
    {
        var inv = Inventory.From(Bare(5), ItemsCatalog, 5);
        foreach ((string item, int count) in new[] { ("item.peau_de_loup", 5), ("item.casque_de_fer", 1), ("item.potion_de_soin", 2), ("item.coiffe_de_cuir", 1),
                     ("item.peau_de_loup", 3), ("item.lame_du_seuil", 1), ("item.minerai_de_fer", 4) })
            inv.Add(item, count);
        inv.Add("item.potion_de_soin", 1);
        Assert.Equal(new[] { 1, 3, 4 }, inv.Filter(InventoryFilter.Equipment, 5));
        Assert.Equal(new[] { 3 }, inv.Filter(InventoryFilter.Wearable, 5));   // le casque (6) et la lame (8) attendent
        Assert.Equal(new[] { 2 }, inv.Filter(InventoryFilter.Consumables, 5));
        Assert.Equal(new[] { 0, 5 }, inv.Filter(InventoryFilter.Materials, 5));
        Assert.Equal(Enumerable.Range(0, 6), inv.Filter(InventoryFilter.All, 5));
        string Order() => string.Join(" ", inv.Stacks.Select(s => s.ToString()));
        inv.Sort(InventorySort.Type);
        Assert.Equal(6, inv.Stacks.Count);
        Assert.Equal(new[] { "item.lame_du_seuil", "item.casque_de_fer", "item.coiffe_de_cuir", "item.potion_de_soin", "item.minerai_de_fer", "item.peau_de_loup" },
            inv.Stacks.Select(s => s.Item));
        Assert.Equal(8, inv.Count("item.peau_de_loup"));
        inv.Sort(InventorySort.Rarity);
        Assert.Equal("item.lame_du_seuil", inv.Stacks[0].Item);   // épique d'abord
        inv.Sort(InventorySort.Level);
        Assert.Equal(new[] { "item.lame_du_seuil", "item.casque_de_fer", "item.coiffe_de_cuir" }, inv.Stacks.Take(3).Select(s => s.Item));
        string sorted = Order();
        inv.Sort(InventorySort.Level);
        Assert.Equal(sorted, Order());   // idempotent
    }

    // --- Nouveau personnage, sauvegarde ------------------------------------------------------------------------------------

    [Fact]
    public void Nouveau_personnage_avec_son_equipement_de_depart()
    {
        SavedCharacter hero = Inventory.Outfit(Bare(1), ItemsCatalog);
        Assert.Equal(new Dictionary<string, string> { ["shard"] = "item.eclat_du_seuil_endormi", ["shield"] = "item.ecu_du_novice", ["weapon"] = "item.epee_du_novice" },
            hero.Equipment);
        Assert.Equal(hero.Equipment, Inventory.Outfit(hero, ItemsCatalog).Equipment);   // déjà équipé : rien de plus
        string temp = Path.Combine(Path.GetTempPath(), "eds_v12_" + Guid.NewGuid().ToString("N"), "partie.json");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        AdventureSession a = Adventure(temp);
        Assert.Equal("item.ecu_du_novice", a.World.Inventory.Equipped["shield"]);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), a.World.EnteredShield);
        Assert.Equal(60, a.World.EnteredGear.Armor);
        Directory.Delete(Path.GetDirectoryName(temp)!, true);
    }

    [Fact]
    public void Sauvegarde_de_version_6_relue_et_mise_a_jour()
    {
        // Une partie de V1.1 : épée et écu de la tranche portés, l'écu ôté puis rangé dans le sac (permis avant le sous-lot), bourse pleine
        // de pièces. Elle se relit, se met à jour (version courante à l'écriture : 7 au sous-lot V1.2, 8 depuis V1.6) et ses places obligatoires sont remplies à la reprise.
        // Remarque de Codex sur la PR #82 : une vraie sauvegarde d'avant V1.8, au niveau 3 des anciens seuils (220 points, convertis au chargement).
        SavedCharacter v11 = Bare(3, "item.peau_de_loup", "item.ecu_de_chene").WithMoney(345).WithXp(220);
        v11 = v11.WithItems(v11.Inventory, new Dictionary<string, string> { ["weapon"] = "item.epee_du_milicien", ["neck"] = "item.amulette_de_l_aube" });
        string text = SaveV11.Older(SaveJsonReader.Write(new WorldSave("zone.vallons", 100, 2, Array.Empty<string>(), v11))).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 6");
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.True(read.Save!.Migrated);
        Assert.Equal(v11.Equipment, read.Save.Character!.Equipment);
        WorldSession w = new(read.Save, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone()));
        Assert.Equal(new[] { "item.ecu_de_chene", "item.eclat_du_seuil_endormi" }, w.Restored);   // l'écu du sac d'abord, puis l'Éclat de départ
        Assert.Equal(new[] { "item.peau_de_loup" }, w.Inventory.Stacks.Select(s => s.Item));
        Assert.Equal(("item.ecu_de_chene", "item.eclat_du_seuil_endormi", "item.amulette_de_l_aube"),
            (w.Inventory.Equipped["shield"], w.Inventory.Equipped["shard"], w.Inventory.Equipped["neck"]));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), w.EnteredShield);
        string rewritten = SaveJsonReader.Write(new WorldSave("zone.vallons", 100, 2, Array.Empty<string>(), w.Inventory.SaveInto(w.Character)));
        Assert.Contains(SaveVersion.Current + ",", rewritten, StringComparison.Ordinal);
        Assert.Contains("\"shard\": \"item.eclat_du_seuil_endormi\"", rewritten, StringComparison.Ordinal);
        // Relue en version courante : plus rien à remplir.
        SaveLoadResult again = SaveJsonReader.Read(rewritten);
        Assert.False(again.Save!.Migrated);
        Assert.Empty(new WorldSession(again.Save, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone())).Restored);
        // Sans bouclier dans le sac, l'écu de départ est donné.
        string bare = SaveV11.Older(SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Bare(1)))).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 6");
        WorldSession given = new(SaveJsonReader.Read(bare).Save!, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone()));
        Assert.Equal(new[] { "item.ecu_du_novice", "item.eclat_du_seuil_endormi" }, given.Restored);
        // Une sauvegarde d'un programme futur (version suivante) est refusée.
        Assert.False(SaveJsonReader.Read(rewritten.Replace(SaveVersion.Current, SaveVersion.Future)).IsValid);
    }

    [Fact]
    public void Sauvegarde_de_version_7_aux_places_obligatoires_vides_reparee()
    {
        // Relecture de la PR #75 (sonde P2) : une sauvegarde de la version courante (7 au sous-lot V1.2, 8 depuis V1.6 ; pas mise à jour) d'un Bastion qui ne porte rien est acceptée ;
        // à la reprise, ses places obligatoires sont remplies comme pour une ancienne sauvegarde (le bouclier du sac d'abord, sinon l'écu
        // de départ ; l'Éclat), au lieu de rester vides.
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Bare(3, "item.peau_de_loup")));
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.False(read.Save!.Migrated);
        var w = new WorldSession(read.Save, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone()));
        Assert.Equal(new[] { "item.ecu_du_novice", "item.eclat_du_seuil_endormi" }, w.Restored);
        Assert.Equal(("item.ecu_du_novice", "item.eclat_du_seuil_endormi"), (w.Inventory.Equipped["shield"], w.Inventory.Equipped["shard"]));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), w.EnteredShield);   // la garde portée au combat, pas « aucune »
        Assert.Equal(new[] { "item.peau_de_loup" }, w.Inventory.Stacks.Select(s => s.Item));
        // Avec un bouclier dans le sac (version courante aussi) : c'est lui qui est mis, il quitte le sac.
        string withTarge = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Bare(3, "item.targe_d_osier")));
        var t = new WorldSession(SaveJsonReader.Read(withTarge).Save!, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone()));
        Assert.Equal(new[] { "item.targe_d_osier", "item.eclat_du_seuil_endormi" }, t.Restored);
        Assert.Empty(t.Inventory.Stacks);
        // Réécrite puis relue : plus rien à remplir.
        string rewritten = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), w.Inventory.SaveInto(w.Character)));
        Assert.Empty(new WorldSession(SaveJsonReader.Read(rewritten).Save!, Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone())).Restored);
    }

    [Fact]
    public void Remplir_les_places_obligatoires_est_idempotent_et_deterministe()
    {
        // Relecture de la PR #75 (sonde P3) : appelée à chaque chargement, FillRequired ne fait rien la seconde fois, et deux inventaires
        // identiques donnent le même résultat.
        SavedCharacter c = Bare(1, "item.ecu_de_chene", "item.targe_d_osier");
        Inventory a = Inventory.From(c, ItemsCatalog, 1), b = Inventory.From(c, ItemsCatalog, 1);
        IReadOnlyList<string> first = a.FillRequired(1);
        Assert.Equal(new[] { "item.ecu_de_chene", "item.eclat_du_seuil_endormi" }, first);   // le premier bouclier du sac, puis l'Éclat
        Assert.Equal(first, b.FillRequired(1));
        Assert.Equal(State(a), State(b));
        string after = State(a);
        Assert.Empty(a.FillRequired(1));
        Assert.Equal(after, State(a));
        Assert.Equal(new[] { "item.targe_d_osier" }, a.Stacks.Select(s => s.Item));
        // Un personnage déjà équipé (nouveau personnage) : rien à faire dès le premier appel.
        Inventory outfitted = Inventory.From(Inventory.Outfit(Bare(1), ItemsCatalog), ItemsCatalog, 1);
        Assert.Empty(outfitted.FillRequired(1));
    }

    [Fact]
    public void Ancienne_sauvegarde_aux_places_inconnues_refusee_a_la_reprise()
    {
        SavedCharacter odd = Bare(3).WithItems(Array.Empty<ItemStack>(), new Dictionary<string, string> { ["ring"] = "item.anneau_de_cuivre" });
        Assert.Contains("inconnu", Assert.Throws<ArgumentException>(() => Inventory.From(odd, ItemsCatalog, 3)).Message);
        SavedCharacter wrong = Bare(3).WithItems(Array.Empty<ItemStack>(), new Dictionary<string, string> { ["ring_1"] = "item.coiffe_de_cuir" });
        Assert.Contains("ne se porte pas", Assert.Throws<ArgumentException>(() => Inventory.From(wrong, ItemsCatalog, 3)).Message);
    }

    // --- Combat ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_bouclier_porte_change_la_garde_a_la_prochaine_entree()
    {
        SavedCharacter hero = Inventory.Outfit(Bare(5, "item.pavois_du_moulin"), ItemsCatalog);
        WorldSession w = Play(Zone(), hero);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), w.EnteredShield);
        Assert.Equal(InventoryRefusal.None, w.Equip(0));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.6"), 10, 150), w.EnteredShield);   // le combat en cours garde l'entrée
        WorldSession next = Play(Zone(), w.Inventory.SaveInto(w.Character));
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.7"), 14, 100), next.EnteredShield);
        V12Kit.CombatantRulesView(next, out long cost, out long window);
        Assert.Equal((14L, 100L), (cost, window));
    }

    [Fact]
    public void Equipement_hors_combat_seulement()
    {
        // Un sanglier à 8 m du point d'entrée : en plein combat (la règle de la halte, des talents et du voyage), rien ne change.
        ZoneDefinition zone = Zone(scenario: s => s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!["position"] = new JsonArray(-10, -12));
        WorldSession w = Play(zone, Inventory.Outfit(Bare(5, "item.pavois_du_moulin", "item.anneau_de_fer"), ItemsCatalog));
        Assert.True(w.InCombat);
        string before = State(w.Inventory);
        Assert.Equal(InventoryRefusal.InCombat, w.Equip(0));
        Assert.Equal(InventoryRefusal.InCombat, w.EquipTo(1, "ring_2"));
        Assert.Equal(InventoryRefusal.InCombat, w.Unequip("weapon"));
        Assert.Equal(InventoryRefusal.InCombat, w.Swap("ring_1", "ring_2"));
        Assert.Equal(before, State(w.Inventory));
        WorldSession calm = Play(Zone(), Inventory.Outfit(Bare(5, "item.pavois_du_moulin"), ItemsCatalog));
        Assert.False(calm.InCombat);
        Assert.Equal(InventoryRefusal.None, calm.Equip(0));
    }

    // --- Comparaison -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Comparaison_verte_ou_rouge_avec_les_chiffres()
    {
        var inv = Inventory.From(Inventory.Outfit(Bare(5, "item.pavois_du_moulin", "item.anneau_de_cuivre", "item.peau_de_loup"), ItemsCatalog), ItemsCatalog, 5);
        ItemComparison pavois = ItemComparison.For(inv, "item.pavois_du_moulin")!;
        Assert.Equal(("shield", "item.ecu_du_novice"), (pavois.Place, pavois.Worn!.Id));
        string Line(ItemComparison c, string label) => c.Lines.Single(l => l.Label == label).ToString();
        Assert.Equal("Armure : 60 → 120 (+60)", Line(pavois, "Armure"));
        Assert.Equal("PV maximaux : 0 → 60 (+60)", Line(pavois, "PV maximaux"));
        Assert.Equal("Part bloquée en garde : 60 % → 70 % (+10 %)", Line(pavois, "Part bloquée en garde"));
        Assert.Equal("Endurance par blocage : 10 → 14 (+4)", Line(pavois, "Endurance par blocage"));
        Assert.Equal("Fenêtre de parade : 150 ms → 100 ms (−50 ms)", Line(pavois, "Fenêtre de parade"));
        Assert.Equal(new[] { CompareTrend.Better, CompareTrend.Better, CompareTrend.Better, CompareTrend.Worse, CompareTrend.Worse }, pavois.Lines.Select(l => l.Trend));
        ItemComparison ring = ItemComparison.For(inv, "item.anneau_de_cuivre")!;
        Assert.Equal(("ring_1", (ItemDefinition?)null), (ring.Place, ring.Worn));
        Assert.Equal("PV maximaux : – → 15 (+15)", ring.Lines.Single().ToString());
        Assert.Null(ItemComparison.For(inv, "item.peau_de_loup"));
        Assert.True(ItemComparison.For(inv, "item.ecu_du_novice", "shield")!.IsWorn);
        Assert.Equal(70, ItemComparison.Percent(Fixed.Parse("0.7")));
    }
}

/// <summary>Sous-lot V1.2 : ce qui se voit sur le Bastion (arme, bouclier, cape ; pas les pièces d'armure).</summary>
[Trait("Category", "Runtime")]
public class V12EquipementVisibleTests
{
    [Fact]
    public void Arme_bouclier_et_cape_visibles()
    {
        var inv = Inventory.From(Inventory.Outfit(PlayerCharacters.Create("bastion", "class.bastion", Progression)
            .WithXp(Progression.XpToLevel[6]).WithItems(new[] { new ItemStack("item.cape_de_voyageur", 1), new ItemStack("item.marteau_du_carrier", 1),
                new ItemStack("item.pavois_du_moulin", 1), new ItemStack("item.casque_de_fer", 1) }, new Dictionary<string, string>()), ItemsCatalog), ItemsCatalog, 7);
        Assert.Equal(new VisibleGear(WeaponLook.Sword, ItemRarity.Common, ShieldLook.Heater, ItemRarity.Common, false, ItemRarity.Common), GearLooks.For(inv));
        // Relecture de la PR #75 : appelé à chaque image avec le précédent, il le rend tel quel tant que rien ne change (aucune allocation).
        VisibleGear shown = GearLooks.For(inv);
        Assert.Same(shown, GearLooks.For(inv, shown));
        Assert.Equal("item.ecu_du_novice", inv.WornAt("shield"));
        Assert.Null(inv.WornAt("cloak"));
        foreach (string id in new[] { "item.cape_de_voyageur", "item.marteau_du_carrier", "item.pavois_du_moulin", "item.casque_de_fer" })
            Assert.Equal(InventoryRefusal.None, inv.Equip(inv.Stacks.ToList().FindIndex(st => st.Item == id), 7));
        Assert.Equal(new VisibleGear(WeaponLook.Hammer, ItemRarity.Rare, ShieldLook.Tower, ItemRarity.Rare, true, ItemRarity.Rare), GearLooks.For(inv));
        Assert.NotSame(shown, GearLooks.For(inv, shown));   // l'équipement a changé : un nouvel objet
        Assert.Equal(GearLooks.For(inv), GearLooks.For(inv, shown));
        // Le casque porté ne change rien à ce qui se voit.
        Assert.True(inv.Equipped.ContainsKey("head"));
        foreach ((string id, WeaponLook look) in new[] { ("item.masse_benite", WeaponLook.Mace), ("item.fleau_du_hameau", WeaponLook.Mace),
                     ("item.hachette_du_bucheron", WeaponLook.Axe), ("item.lame_de_la_tour", WeaponLook.Sword), ("item.epee_du_capitaine", WeaponLook.Sword) })
            Assert.Equal(look, GearLooks.WeaponOf(id));
        foreach ((string id, ShieldLook look) in new[] { ("item.targe_d_osier", ShieldLook.Round), ("item.rondache_du_lac", ShieldLook.Round),
                     ("item.bouclier_de_la_tour", ShieldLook.Tower), ("item.ecu_de_l_aube", ShieldLook.Heater), ("item.bouclier_a_pointes", ShieldLook.Heater) })
            Assert.Equal(look, GearLooks.ShieldOf(id));
    }
}

/// <summary>Accès aux règles effectives du personnage entré dans une session (pour les tests).</summary>
internal static class V12Kit
{
    public static void CombatantRulesView(WorldSession w, out long costPerBlock, out long parryWindowMs)
    {
        CombatantRules rules = w.Zone.Engine.View("bastion").Rules!;
        (costPerBlock, parryWindowMs) = (rules.GuardCostPerBlock, rules.ParryWindowMs);
    }
}
