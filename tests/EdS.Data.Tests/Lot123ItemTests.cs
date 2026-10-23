using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.3 : objets, équipement, inventaire. Catalogue en données (emplacements, inventaire de 24 places, raretés, bonus, tables de
/// butin), bonus d'équipement compté par le moteur (moteur 0.23.0, journal au format 17, scénarios en schéma 11), inventaire et équipement
/// atomiques, butin tiré par la clé de l'événement et attribué une seule fois, sauvegarde en version 3.
/// </summary>
[Trait("Category", "World")]
public class Lot123ItemTests
{
    private static string CatalogPath => ItemJsonReader.PathIn(DataDirectory);

    private static ItemCatalog Catalog => ItemJsonReader.ReadFile(CatalogPath, ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static ItemCatalogLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CatalogPath))!.AsObject();
        mutate(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();

    private static JsonObject ItemAt(JsonObject root, int i) => Data(root)["items"]![i]!.AsObject();

    private static string FirstError(ItemCatalogLoadResult result)
    {
        Assert.False(result.IsValid);
        return result.Report.Errors[0].Path;
    }

    // --- Catalogue ---------------------------------------------------------------------------------

    [Fact]
    public void Tables_des_raretes_et_des_types_non_modifiables()
    {
        // Revue Codex de la PR #44 (2e tour) : les tables globales ne se modifient pas par conversion.
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ItemRarity>)ItemValidator.Rarities).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ItemType>)ItemValidator.Types)["equipment"] = ItemType.Material);
        Assert.Equal(ItemRarity.Epic, ItemValidator.Rarities["epic"]);
        // L'inventaire ne se modifie que par ses opérations : ses vues sont en lecture seule.
        var inventory = new Inventory(ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!);
        Assert.Throws<NotSupportedException>(() => ((IList<ItemStack>)inventory.Stacks).Add(new ItemStack("item.gambison", 1)));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)inventory.Equipped).Add("weapon", "item.gambison"));
    }

    [Fact]
    public void Champs_du_bonus_d_equipement_non_modifiables()
    {
        // Revue Codex de la PR #44 : la liste des champs ne peut pas être réordonnée par un appelant ; nom, rang et indexeur restent alignés.
        Assert.Throws<NotSupportedException>(() => ((IList<string>)GearBonus.Fields)[0] = "armor");
        Assert.IsNotType<string[]>(GearBonus.Fields);
        for (int i = 0; i < GearBonus.Fields.Count; i++)
        {
            int[] values = new int[GearBonus.Fields.Count];
            values[i] = 7;
            Assert.Equal(i, GearBonus.FieldIndex(GearBonus.Fields[i]));
            Assert.Equal(7, GearBonus.FromValues(values)[i]);
        }
        Assert.Equal(-1, GearBonus.FieldIndex("inconnu"));
        Assert.Equal(new GearBonus(armor: 7), GearBonus.FromValues(new[] { 0, 0, 0, 7, 0, 0, 0, 0 }));
    }

    [Fact]
    public void Catalogue_livre()
    {
        ItemCatalogLoadResult loaded = ItemJsonReader.ReadFile(CatalogPath, ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ItemCatalog c = loaded.Catalog!;
        Assert.Equal(new[] { "weapon", "shield", "chest", "neck" }, c.Slots);   // quatre emplacements dont le bouclier (L11-D10)
        Assert.Equal(24, c.InventorySize);                                       // Q5
        Assert.Equal(19, c.Items.Count);   // sous-lot 12.6 : herbe, ferrure, potion, remède
        ItemDefinition shield = c.Item("item.ecu_de_chene");
        Assert.Equal((ItemType.Equipment, "shield", 1, ItemRarity.Common, 1, 60), (shield.Type, shield.Slot, shield.RequiredLevel, shield.Rarity, shield.Stack, shield.Bonus.Armor));
        // H5 : deux boucliers de même niveau, d'orientations différentes.
        ItemDefinition pavois = c.Item("item.pavois_du_moulin"), rondache = c.Item("item.rondache_cerclee");
        Assert.Equal(pavois.RequiredLevel, rondache.RequiredLevel);
        Assert.True(pavois.Bonus.MaxHealth > 0 && rondache.Bonus.MaxHealth == 0 && rondache.Bonus.StatusResistance > 0);
        ItemDefinition pelt = c.Item("item.peau_de_loup");
        Assert.Equal((ItemType.Material, (string?)null, 20, true), (pelt.Type, pelt.Slot, pelt.Stack, pelt.Bonus.IsNone));
        Assert.True(c.TryGetLoot("loot.loup", out LootTable wolf));
        Assert.Equal(("item.peau_de_loup", 1, 60), (wolf.Drops[0].Item, wolf.Drops[0].Count, wolf.Drops[0].ChancePercent));
        Assert.Equal(new[] { "common", "rare", "very_rare", "epic", "legendary", "mythic" }, ItemValidator.Rarities.Keys);
        Assert.Throws<ArgumentException>(() => c.Item("item.inconnu"));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "items.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(CatalogPath)).RootElement));
    }

    [Fact]
    public void Catalogue_invalide_refuse_avec_son_chemin()
    {
        Assert.Equal("$.data.slots[1]", FirstError(Mutated(r => Data(r)["slots"]![1] = "weapon")));
        Assert.Equal("$.data.slots[0]", FirstError(Mutated(r => Data(r)["slots"]![0] = "Arme")));
        Assert.Equal("$.data.inventorySize", FirstError(Mutated(r => Data(r)["inventorySize"] = 0)));
        Assert.Equal("$.data.items[1].id", FirstError(Mutated(r => ItemAt(r, 1)["id"] = "item.epee_du_milicien")));
        Assert.Equal("$.data.items[0].id", FirstError(Mutated(r => ItemAt(r, 0)["id"] = "epee")));
        Assert.Equal("$.data.items[0].rarity", FirstError(Mutated(r => ItemAt(r, 0)["rarity"] = "secret")));   // Q30 : une origine, pas un rang
        Assert.Equal("$.data.items[0].type", FirstError(Mutated(r => ItemAt(r, 0)["type"] = "potion")));
        Assert.Equal("$.data.items[0].slot", FirstError(Mutated(r => ItemAt(r, 0)["slot"] = "feet")));
        Assert.Equal("$.data.items[0].requiredLevel", FirstError(Mutated(r => ItemAt(r, 0)["requiredLevel"] = 31)));
        Assert.Equal("$.data.items[0].bonus.chance", FirstError(Mutated(r => ItemAt(r, 0)["bonus"]!["chance"] = 1)));
        Assert.Equal("$.data.items[0].bonus.physicalPower", FirstError(Mutated(r => ItemAt(r, 0)["bonus"]!["physicalPower"] = ItemValidator.MaxItemBonus + 1)));
        Assert.Equal("$.data.items[0].stack", FirstError(Mutated(r => ItemAt(r, 0)["stack"] = 2)));        // un équipement ne s'empile pas
        Assert.Equal("$.data.items[10].slot", FirstError(Mutated(r => ItemAt(r, 10)["slot"] = "neck")));  // un matériau ne se porte pas
        Assert.Equal("$.data.items[10].stack", FirstError(Mutated(r => ItemAt(r, 10)["stack"] = 1000)));
        Assert.Equal("$.data.loot[0].drops[0].item", FirstError(Mutated(r => Data(r)["loot"]![0]!["drops"]![0]!["item"] = "item.inconnu")));
        Assert.Equal("$.data.loot[0].drops[0].count", FirstError(Mutated(r => Data(r)["loot"]![0]!["drops"]![0]!["count"] = 21)));   // plus d'une pile
        Assert.Equal("$.data.loot[0].drops[1].chancePercent", FirstError(Mutated(r => Data(r)["loot"]![0]!["drops"]![1]!["chancePercent"] = 0)));
        Assert.Equal("$.data.loot[1].id", FirstError(Mutated(r => Data(r)["loot"]![1]!["id"] = "loot.loup")));
        Assert.Equal("$.kind", FirstError(Mutated(r => r["kind"] = "progression")));
    }

    // --- Inventaire et équipement ------------------------------------------------------------------

    [Fact]
    public void Inventaire_empile_et_refuse_sans_rien_changer()
    {
        var inventory = new Inventory(Catalog);
        Assert.Equal(InventoryRefusal.None, inventory.Add("item.peau_de_loup", 25));   // une pile de 20, puis 5
        Assert.Equal(new[] { new ItemStack("item.peau_de_loup", 20), new ItemStack("item.peau_de_loup", 5) }, inventory.Stacks);
        Assert.Equal(InventoryRefusal.None, inventory.Add("item.peau_de_loup", 3));    // pile entamée d'abord
        Assert.Equal(8, inventory.Stacks[1].Count);
        for (int i = 0; i < 22; i++) Assert.Equal(InventoryRefusal.None, inventory.Add("item.gambison", 1));
        Assert.Equal(0, inventory.FreePlaces);
        // Plein : un objet de plus est refusé, rien ne bouge ; ce qui tient encore dans une pile entamée est accepté.
        ItemStack[] before = inventory.Stacks.ToArray();
        Assert.Equal(InventoryRefusal.Full, inventory.Add("item.ecu_de_chene", 1));
        Assert.Equal(InventoryRefusal.Full, inventory.Add("item.peau_de_loup", 13));   // 12 places dans la pile entamée, pas 13
        Assert.Equal(before, inventory.Stacks);
        Assert.Equal(InventoryRefusal.None, inventory.Add("item.peau_de_loup", 12));
        Assert.Equal(40, inventory.Count("item.peau_de_loup"));
        // Retirer : en entier ou rien.
        Assert.Equal(InventoryRefusal.NotEnough, inventory.Remove("item.peau_de_loup", 41));
        Assert.Equal(InventoryRefusal.None, inventory.Remove("item.peau_de_loup", 21));
        Assert.Equal((19, 23), (inventory.Count("item.peau_de_loup"), inventory.Stacks.Count));
        Assert.Throws<ArgumentException>(() => inventory.Add("item.inconnu", 1));
        Assert.Throws<ArgumentException>(() => inventory.Add("item.peau_de_loup", 0));
    }

    [Fact]
    public void Equiper_echange_et_respecte_le_niveau()
    {
        var inventory = new Inventory(Catalog);
        inventory.Add("item.ecu_de_chene", 1);
        inventory.Add("item.pavois_du_moulin", 1);
        inventory.Add("item.peau_de_loup", 2);
        Assert.Equal(InventoryRefusal.None, inventory.Equip(0, level: 1));
        Assert.Equal("item.ecu_de_chene", inventory.Equipped["shield"]);
        Assert.Equal(60, inventory.Gear().Armor);
        // Le pavois demande le niveau 5 ; un matériau ne se porte pas ; une place vide non plus.
        Assert.Equal(InventoryRefusal.LevelTooLow, inventory.Equip(0, level: 4));
        Assert.Equal(InventoryRefusal.NotEquipment, inventory.Equip(1, level: 10));
        Assert.Equal(InventoryRefusal.NothingThere, inventory.Equip(5, level: 10));
        // Au niveau 5 : échange, l'écu reprend la place du pavois.
        Assert.Equal(InventoryRefusal.None, inventory.Equip(0, level: 5));
        Assert.Equal(("item.pavois_du_moulin", "item.ecu_de_chene"), (inventory.Equipped["shield"], inventory.Stacks[0].Item));
        inventory.Add("item.gambison", 1);
        Assert.Equal(InventoryRefusal.None, inventory.Equip(2, level: 5));
        Assert.Equal(new GearBonus(armor: 160, maxHealth: 100), inventory.Gear());
        // Retirer : il faut une place libre.
        for (int i = inventory.FreePlaces; i > 0; i--) inventory.Add("item.epee_du_milicien", 1);
        Assert.Equal(InventoryRefusal.Full, inventory.Unequip("chest"));
        Assert.Equal("item.gambison", inventory.Equipped["chest"]);
        inventory.Remove("item.epee_du_milicien", 1);
        Assert.Equal(InventoryRefusal.None, inventory.Unequip("chest"));
        Assert.Equal(InventoryRefusal.NothingThere, inventory.Unequip("chest"));
        Assert.Equal("item.gambison", inventory.Stacks[^1].Item);
    }

    [Fact]
    public void Inventaire_sauvegarde_verifie_contre_le_catalogue()
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        SavedCharacter With(ItemStack[] stacks, Dictionary<string, string>? equipment = null) => hero.WithItems(stacks, equipment ?? new Dictionary<string, string>());
        Inventory ok = Inventory.From(With(new[] { new ItemStack("item.peau_de_loup", 3) }, new Dictionary<string, string> { ["shield"] = "item.ecu_de_chene" }), Catalog, 1);
        Assert.Equal((3, 60), (ok.Count("item.peau_de_loup"), ok.Gear().Armor));
        Assert.Contains("absent du catalogue", Assert.Throws<ArgumentException>(() => Inventory.From(With(new[] { new ItemStack("item.inconnu", 1) }), Catalog, 1)).Message);
        Assert.Contains("pile", Assert.Throws<ArgumentException>(() => Inventory.From(With(new[] { new ItemStack("item.peau_de_loup", 21) }), Catalog, 1)).Message);
        Assert.Throws<ArgumentException>(() => Inventory.From(With(Enumerable.Repeat(new ItemStack("item.gambison", 1), 25).ToArray()), Catalog, 1));
        Assert.Contains("niveau 5", Assert.Throws<ArgumentException>(() => Inventory.From(With(Array.Empty<ItemStack>(),
            new Dictionary<string, string> { ["shield"] = "item.pavois_du_moulin" }), Catalog, 4)).Message);
        Assert.Throws<ArgumentException>(() => Inventory.From(With(Array.Empty<ItemStack>(), new Dictionary<string, string> { ["weapon"] = "item.ecu_de_chene" }), Catalog, 1));
        Assert.Throws<ArgumentException>(() => Inventory.From(With(Array.Empty<ItemStack>(), new Dictionary<string, string> { ["feet"] = "item.ecu_de_chene" }), Catalog, 1));
    }

    // --- Butin -------------------------------------------------------------------------------------

    [Fact]
    public void Butin_determine_par_la_cle_de_l_evenement()
    {
        Catalog.TryGetLoot("loot.archer", out LootTable archer);
        string Roll(string key) => string.Join(",", Loot.Roll(archer, key).Select(r => $"{r.Item}x{r.Count}"));
        Assert.Equal(Roll("zone.vallons/1/death/archer"), Roll("zone.vallons/1/death/archer"));
        // Sur 2 000 morts différentes, chaque objet tombe à peu près selon sa chance (70 %, 5 %, 2 %).
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < 2000; i++)
            foreach (LootRoll r in Loot.Roll(archer, $"zone.vallons/{i}/death/archer"))
                counts[r.Item] = counts.GetValueOrDefault(r.Item) + 1;
        Assert.InRange(counts["item.fleche_brisee"], 1300, 1500);
        Assert.InRange(counts["item.amulette_de_l_aube"], 60, 140);
        Assert.InRange(counts["item.eclat_de_seuil"], 15, 65);
        Assert.Throws<ArgumentException>(() => Loot.Roll(archer, "pas une clé"));
    }

    // --- Sauvegarde, version 3 ---------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_de_l_inventaire_et_de_l_equipement()
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(
            new[] { new ItemStack("item.peau_de_loup", 12), new ItemStack("item.gambison", 1) },
            new Dictionary<string, string> { ["weapon"] = "item.epee_du_milicien", ["shield"] = "item.ecu_de_chene" });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), hero));
        Assert.Contains("\"schemaVersion\": 5,", text, StringComparison.Ordinal);   // sous-lot 12.6 : version 5 (métiers)
        Assert.Contains("      \"inventory\": [\n        {\"item\": \"item.peau_de_loup\", \"count\": 12},\n        {\"item\": \"item.gambison\", \"count\": 1}\n      ],\n"
            + "      \"equipment\": {\"shield\": \"item.ecu_de_chene\", \"weapon\": \"item.epee_du_milicien\"},\n", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(hero.Inventory, read.Save!.Character!.Inventory);
        Assert.Equal(hero.Equipment, read.Save.Character.Equipment);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        Assert.Contains("\"inventory\": [],\n      \"equipment\": {},\n",
            SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Progression))), StringComparison.Ordinal);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            hero.WithItems(new[] { new ItemStack("Épée", 1) }, new Dictionary<string, string>()))));

        // Version 2 : mise à jour, inventaire et équipement vides ; un inventaire en version 2 est un champ inconnu.
        static string CutFrom(string t, string field)
        {
            int a = t.IndexOf(",\n      \"" + field + "\"", StringComparison.Ordinal), b = t.IndexOf("\n    },", a, StringComparison.Ordinal);
            return t.Remove(a, b - a);
        }
        string v2 = CutFrom(text, "inventory").Replace("\"schemaVersion\": 5", "\"schemaVersion\": 2");
        SaveLoadResult migrated = SaveJsonReader.Read(v2);
        Assert.True(migrated.IsValid, migrated.Report.ToString() + "\n" + v2);
        Assert.True(migrated.Save!.Migrated);
        Assert.Empty(migrated.Save.Character!.Inventory);
        Assert.Empty(migrated.Save.Character.Equipment);
        Assert.Equal("$.data.character.inventory",
            Assert.Single(SaveJsonReader.Read(CutFrom(text, "equipment").Replace("\"schemaVersion\": 5", "\"schemaVersion\": 2")).Report.Errors).Path);
    }

    [Theory]
    [InlineData("\"item\": \"item.peau_de_loup\"", "\"item\": \"peau\"", "$.data.character.inventory[0].item")]
    [InlineData("\"count\": 12", "\"count\": 0", "$.data.character.inventory[0].count")]
    [InlineData("\"count\": 12", "\"count\": 1000", "$.data.character.inventory[0].count")]
    [InlineData("{\"shield\": \"item.ecu_de_chene\", \"weapon\"", "{\"weapon\": \"item.ecu_de_chene\", \"shield\"", "$.data.character.equipment.shield")]   // ordre canonique
    [InlineData("\"shield\": \"item.ecu_de_chene\"", "\"shield\": \"ecu\"", "$.data.character.equipment.shield")]
    [InlineData("      \"equipment\"", "      \"equipmen\"", "$.data.character.equipmen")]
    public void Sauvegarde_d_inventaire_invalide(string from, string to, string path)
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(new[] { new ItemStack("item.peau_de_loup", 12) },
            new Dictionary<string, string> { ["weapon"] = "item.epee_du_milicien", ["shield"] = "item.ecu_de_chene" });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), hero));
        Assert.Contains(from, text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text.Replace(from, to, StringComparison.Ordinal));
        Assert.False(read.IsValid);
        Assert.Equal(path, read.Report.Errors[0].Path);
    }

    // --- Moteur : bonus d'équipement ---------------------------------------------------------------

    private static ScenarioLoadResult WithGear(Action<JsonObject> gear, int combatant = 0) => ReadMutated(root =>
    {
        var g = new JsonObject();
        gear(g);
        root["data"]!["combatants"]![combatant]!["gear"] = g;
    });

    [Fact]
    public void Bonus_d_equipement_compte_et_ecrit_dans_l_en_tete()
    {
        ScenarioLoadResult plain = ScenarioJsonReader.ReadFile(ScenarioPath("mannequin"), ShippedData);
        ScenarioLoadResult geared = WithGear(g => { g["maxHealth"] = 40; g["physicalPower"] = 10; g["armor"] = 0; });
        Assert.True(geared.IsValid, geared.Report.ToString());
        CharacterSetup bastion = geared.Setup!.Combatants.OfType<CharacterSetup>().Single();
        Assert.Equal(new GearBonus(physicalPower: 10, maxHealth: 40), bastion.Gear);
        var plainLog = new MemoryCombatLog();
        var gearedLog = new MemoryCombatLog();
        CombatEngine.Run(plain.Setup!, plainLog);
        CombatEngine.Run(geared.Setup!, gearedLog);
        // En-tête : seulement les champs non nuls, après le build ; sans équipement, pas de champ.
        Assert.DoesNotContain("\"gear\"", plainLog.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"gear\":{\"physicalPower\":10,\"maxHealth\":40}", gearedLog.Lines[0], StringComparison.Ordinal);
        long MaxHealth(MemoryCombatLog log) => JsonDocument.Parse(log.Lines.First(l => l.Contains("\"event\":\"combatant\"", StringComparison.Ordinal)
            && l.Contains("\"combatant\":\"bastion\"", StringComparison.Ordinal))).RootElement.GetProperty("maxHealth").GetInt64();
        Assert.Equal(MaxHealth(plainLog) + 40, MaxHealth(gearedLog));
        // Plus de puissance, plus de dégâts au mannequin.
        long Dealt(MemoryCombatLog log) => log.Lines.Where(l => l.Contains("\"event\":\"damage\"", StringComparison.Ordinal)
            && l.Contains("\"source\":\"bastion\"", StringComparison.Ordinal)).Sum(l => JsonDocument.Parse(l).RootElement.GetProperty("amount").GetInt64());
        Assert.True(Dealt(gearedLog) > Dealt(plainLog));
        // Le journal équipé se rejoue à l'identique (l'équipement est dans l'en-tête).
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(gearedLog.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Bonus_d_equipement_invalide_refuse()
    {
        Assert.Equal("$.data.combatants[0].gear.armor", Assert.Single(WithGear(g => g["armor"] = GearBonus.MaxValue + 1).Report.Errors).Path);
        Assert.Equal("$.data.combatants[0].gear.armor", Assert.Single(WithGear(g => g["armor"] = -1).Report.Errors).Path);
        Assert.Equal("$.data.combatants[0].gear.chance", Assert.Single(WithGear(g => g["chance"] = 1).Report.Errors).Path);
        Assert.Equal("$.data.combatants[1].gear", Assert.Single(WithGear(g => g["armor"] = 1, combatant: 1).Report.Errors).Path);   // mannequin
        Assert.True(ItemValidator.MaxItemBonus * ItemValidator.MaxSlots <= GearBonus.MaxValue);   // douze objets portés tiennent toujours
    }

    // --- Partie en solo : butin et équipement ------------------------------------------------------

    [Fact]
    public void Butin_range_une_fois_et_equipement_a_l_entree()
    {
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        // Table d'essai : la peau de loup tombe toujours.
        ItemCatalog catalog = Mutated(r => Data(r)["loot"]![0]!["drops"]![0]!["chancePercent"] = 100).Catalog!;
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(Array.Empty<ItemStack>(),
            new Dictionary<string, string> { ["chest"] = "item.gambison" });
        var placement = new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5"));
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), loaded.Setup!, ShippedData, Progression, catalog, placement,
            new Dictionary<string, string> { ["loup1"] = "loot.loup" });
        Assert.Equal(new GearBonus(armor: 40, maxHealth: 40), session.EnteredGear);
        session.Step();
        long withGambison = session.Zone.Engine.View("bastion").MaxHealth!.Value;
        var loot = new List<LootGained>();
        for (int i = 0; i < 600 && loot.Count == 0; i++)
        {
            if (i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            loot.AddRange(session.Step().Loot);
        }
        LootGained pelt = Assert.Single(loot, l => l.Item == "item.peau_de_loup");
        Assert.Equal(("zone.vallons/1/death/loup1", 1, true), (pelt.Source.Key, pelt.Count, pelt.Kept));
        Assert.Equal(1, session.Inventory.Count("item.peau_de_loup"));
        // Retirer le gambison change le bonus de la prochaine entrée, pas du combat en cours.
        Assert.Equal(InventoryRefusal.None, session.Inventory.Unequip("chest"));
        Assert.Equal(withGambison, session.Zone.Engine.View("bastion").MaxHealth);
        WorldSave after = session.Save();
        Assert.Equal(new[] { new ItemStack("item.peau_de_loup", 1), new ItemStack("item.gambison", 1) }, after.Character!.Inventory);
        Assert.Empty(after.Character.Equipment);
        var next = new WorldSession(SaveJsonReader.Read(SaveJsonReader.Write(after)).Save!, loaded.Setup!, ShippedData, Progression, catalog, placement,
            new Dictionary<string, string> { ["loup1"] = "loot.loup" });
        next.Step();
        Assert.Equal(GearBonus.None, next.EnteredGear);
        Assert.Equal(withGambison - 40, next.Zone.Engine.View("bastion").MaxHealth);
        // La mort de la session précédente reste consommée : elle ne redonne rien (nouvelle session, nouvelle clé).
        Assert.Contains("zone.vallons/1/death/loup1", after.Consumed);
        // Table inconnue : refusée à l'ouverture de la partie.
        Assert.Throws<ArgumentException>(() => new WorldSession(after, loaded.Setup!, ShippedData, Progression, catalog, placement,
            new Dictionary<string, string> { ["loup1"] = "loot.inconnu" }));
    }

    [Fact]
    public void Butin_perdu_quand_l_inventaire_est_plein()
    {
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        ItemCatalog catalog = Mutated(r => Data(r)["loot"]![0]!["drops"]![0]!["chancePercent"] = 100).Catalog!;
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression)
            .WithItems(Enumerable.Repeat(new ItemStack("item.gambison", 1), 24), new Dictionary<string, string>());
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), loaded.Setup!, ShippedData, Progression, catalog,
            new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")), new Dictionary<string, string> { ["loup1"] = "loot.loup" });
        var loot = new List<LootGained>();
        for (int i = 0; i < 600 && loot.Count == 0; i++)
        {
            if (i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            loot.AddRange(session.Step().Loot);
        }
        Assert.False(Assert.Single(loot, l => l.Item == "item.peau_de_loup").Kept);
        Assert.Equal(0, session.Inventory.Count("item.peau_de_loup"));
        Assert.Equal(24, session.Inventory.Stacks.Count);
    }
}
