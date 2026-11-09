using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.3 : effets d'équipement, ensembles et propriétés tirées hors du moteur. Catalogue des objets en version 4 (places à effets,
/// objets nommés, effets, ensembles de 2 à 6 pièces, tables des propriétés tirées), tirage déterministe par la clé de la mort, instance
/// d'objet dans l'inventaire et la sauvegarde (version 9), effets portés au combat à l'entrée suivante, vente et rachat fidèles,
/// comparaison et détails de la fiche.
/// </summary>
[Trait("Category", "World")]
public class V13EquipementTests
{
    private static string CatalogPath => ItemJsonReader.PathIn(DataDirectory);

    private static ItemCatalogLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CatalogPath))!.AsObject();
        mutate(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();
    private static JsonObject ItemOf(JsonObject root, string id) => Data(root)["items"]!.AsArray().Single(i => (string)i!["id"]! == id)!.AsObject();
    private static int IndexOf(string id) => ItemsCatalog.Items.ToList().FindIndex(i => i.Id == id);
    private static int SetIndex(string id) => ItemsCatalog.Sets.ToList().FindIndex(s => s.Id == id);

    private static void AssertError(ItemCatalogLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    /// <summary>Bastion au niveau donné, avec ces instances dans le sac et cet équipement.</summary>
    private static SavedCharacter Hero(int level, IEnumerable<ItemStack> bag, IReadOnlyDictionary<string, string>? worn = null,
        IReadOnlyDictionary<string, IReadOnlyList<RolledProperty>>? rolled = null) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[Math.Min(level, Progression.XpToLevel.Count) - 1])
            .WithItems(bag, worn ?? new Dictionary<string, string>(), rolled);

    private static Inventory Bag(int level, IEnumerable<ItemStack> bag, IReadOnlyDictionary<string, string>? worn = null) =>
        Inventory.From(Hero(level, bag, worn), ItemsCatalog, level);

    // --- Catalogue ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Catalogue_en_version_4_avec_objets_nommes_ensembles_et_proprietes()
    {
        ItemCatalog c = ItemsCatalog;
        Assert.Equal((7, "0.16.0-provisoire"), (c.SchemaVersion, c.ContentVersion));   // V2.6.1 : contenu 0.14.0 (chapelet de l'Aube) ; V1.9.9 : contenu 0.13.0 ; V1.9.7 : contenu 0.12.0 ; V1.9.6 : contenu 0.11.0 ; V1.9.4 : contenu 0.10.0 ; V1.9.3 : contenu 0.9.0 ; V1.9.2 : contenu 0.8.0 ; V1.9.1 : contenu 0.7.0 ; sous-lot V1.4 : version 5 (qualité, usure) ; V1.8 : 6 (résistance à l'usure) ; V1.8c : 7 (objets de bonus)
        // Réponses du 31 octobre 2026 : les boucliers aussi (la rondache du chef des bandits).
        Assert.Equal(new[] { "weapon", "shield", "neck", "ring_1", "ring_2", "relic_1", "relic_2" }, c.Places.Where(p => p.Effects).Select(p => p.Id));
        Assert.Equal(new[] { "item.lame_du_seuil", "item.rondache_du_chef_des_bandits", "item.amulette_de_l_aube", "item.talisman_du_seuil", "item.collier_de_croc_gris" }, c.Items.Where(i => i.Effect != null).Select(i => i.Id));   // V1.9.7 : le collier de Croc-Gris (récompense du chef)
        Assert.Equal("Les coups sur un ennemi étourdi infligent 10 % de dégâts en plus.", c.Item("item.lame_du_seuil").Effect!.Text);
        Assert.Equal("item.lame_du_seuil", c.Item("item.lame_du_seuil").Effect!.Effect.Source);
        // Deux ensembles des niveaux 1 à 20 : 4 et 6 pièces, paliers croissants, sources « set.….<pièces> ».
        Assert.Equal(new[] { ("set.tenue_du_veneur", 4, "2,4"), ("set.garde_de_la_tour", 6, "2,4,6") },
            c.Sets.Select(s => (s.Id, s.Pieces.Count, string.Join(",", s.Bonuses.Select(b => b.Pieces)))));
        Assert.All(c.Sets.SelectMany(s => s.Pieces), id => Assert.True(c.Item(id).Named));
        Assert.All(c.Sets.SelectMany(s => s.Pieces), id => Assert.InRange(c.Item(id).RequiredLevel, 1, 20));
        Assert.Equal("set.garde_de_la_tour.6", c.Sets[1].Bonuses[2].Effect!.Source);
        Assert.Equal(new GearBonus(armor: 60), c.Sets[1].Bonuses[0].Bonus);
        // Tables : chaque rareté une fois ; la rareté fixe le nombre et l'échelle, pas le hasard (Q30).
        Assert.Equal(new[] { (ItemRarity.Common, 0, 100), (ItemRarity.Rare, 1, 100), (ItemRarity.VeryRare, 2, 110), (ItemRarity.Epic, 3, 120),
            (ItemRarity.Legendary, 4, 135), (ItemRarity.Mythic, 5, 150) }, c.RarityRolls.Select(r => (r.Rarity, r.Count, r.ScalePercent)));   // 4 et 5 : réponses du 31 octobre 2026
        Assert.Equal(6, c.Properties.Count);
        // Le butin non nommé tire ; l'objet nommé, la pièce d'ensemble, l'Éclat et l'outil jamais.
        Assert.True(c.IsRollable(c.Item("item.bottes_du_renard")));
        Assert.True(c.IsRollable(c.Item("item.epee_de_garde")));
        Assert.False(c.IsRollable(c.Item("item.gambison")));               // commun : aucune propriété
        Assert.False(c.IsRollable(c.Item("item.lame_du_seuil")));          // nommé
        Assert.False(c.IsRollable(c.Item("item.plastron_de_la_tour")));    // pièce d'ensemble
        Assert.False(c.IsRollable(c.Item("item.marteau_de_forge")));       // outil
    }

    [Fact]
    public void Bornes_par_niveau_et_rarete()
    {
        ItemCatalog c = ItemsCatalog;
        Assert.True(c.TryGetProperty("property.vigueur", out PropertyDefinition vigueur));
        // Vigueur : 5 à 10 au niveau 1, +3 à +6 par niveau ; × l'échelle de la rareté.
        Assert.Equal((20, 40), c.Bounds(vigueur, c.Item("item.bottes_du_renard")));     // rare, niveau 6 : (5 + 15, 10 + 30)
        Assert.Equal((70, 141), c.Bounds(vigueur, c.Item("item.cape_du_seuil")));       // épique, niveau 19 : (5 + 54, 10 + 108) × 1,2, arrondi vers le bas
        Assert.True(c.TryGetProperty("property.force", out PropertyDefinition force));
        Assert.False(force.FitsKind("feet"));
        Assert.True(force.FitsKind("weapon"));
        Assert.Equal(new[] { "property.solidite", "property.vigueur", "property.souffle" }, c.PropertiesFor(c.Item("item.bottes_du_renard")).Select(p => p.Id));
    }

    [Fact]
    public void Catalogue_incoherent_refuse_avec_son_chemin()
    {
        int lame = IndexOf("item.lame_du_seuil"), gants = IndexOf("item.gants_de_cuir"), tour = SetIndex("set.garde_de_la_tour");
        // Un effet sur une place qui n'en admet pas (des gants).
        AssertError(Mutated(r => ItemOf(r, "item.gants_de_cuir")["effects"] = ItemOf(r, "item.lame_du_seuil")["effects"]!.DeepClone()), $"$.data.items[{gants}].effects");
        // Un objet à effets ou une pièce d'ensemble non nommés.
        AssertError(Mutated(r => ItemOf(r, "item.lame_du_seuil").Remove("named")), $"$.data.items[{lame}].named");
        AssertError(Mutated(r => ItemOf(r, "item.plastron_de_la_tour").Remove("named")), $"$.data.items[{IndexOf("item.plastron_de_la_tour")}].named");
        // Un effet hors des bornes des règles (la même fonction que la validation du combat).
        AssertError(Mutated(r => ItemOf(r, "item.lame_du_seuil")["effects"]!["modifiers"]![0]!["add"] = 0.3), $"$.data.items[{lame}].effects.modifiers[0].add");
        AssertError(Mutated(r => ItemOf(r, "item.lame_du_seuil")["effects"]!.AsObject().Remove("text")), $"$.data.items[{lame}].effects.text");
        // Ensemble inconnu, palier impossible, trop peu de pièces.
        AssertError(Mutated(r => ItemOf(r, "item.lame_de_la_tour")["set"] = "set.inconnu"), $"$.data.items[{IndexOf("item.lame_de_la_tour")}].set");
        AssertError(Mutated(r => Data(r)["sets"]![tour]!["bonuses"]![0]!["pieces"] = 3), $"$.data.sets[{tour}].bonuses[0].pieces");
        AssertError(Mutated(r => Data(r)["sets"]![0]!["bonuses"]!.AsArray().Add(new JsonObject { ["pieces"] = 6, ["text"] = "Trop", ["bonus"] = new JsonObject { ["armor"] = 1 } })),
            "$.data.sets[0].bonuses[2].pieces");
        AssertError(Mutated(r =>
        {
            foreach (string id in new[] { "item.spalieres_du_veneur", "item.cuirasse_du_veneur", "item.gants_du_veneur" }) ItemOf(r, id).Remove("set");
        }), "$.data.sets[0].id");
        AssertError(Mutated(r => Data(r)["sets"]![tour]!["bonuses"]![1]!["effects"]!["modifiers"]![0]!["add"] = -0.2),
            $"$.data.sets[{tour}].bonuses[1].effects.modifiers[0].add");
        // Tables des propriétés : rareté absente, borne haute sous la basse, sorte de place inconnue, version antérieure.
        AssertError(Mutated(r => Data(r)["properties"]!["rarities"]!.AsArray().RemoveAt(5)), "$.data.properties.rarities");
        AssertError(Mutated(r => Data(r)["properties"]!["list"]![0]!["max"] = 0), "$.data.properties.list[0].max");
        AssertError(Mutated(r => Data(r)["properties"]!["list"]![0]!["slots"] = new JsonArray("shard")), "$.data.properties.list[0].slots[0]");
        AssertError(Mutated(r => Data(r).Remove("properties")), "$.data.properties");
        Assert.False(Mutated(r => r["schemaVersion"] = 3).IsValid);
    }

    [Fact]
    public void Un_objet_qui_tire_a_toujours_assez_de_proprietes_possibles()
    {
        // Les épiques tirent 3 propriétés : une arme épique non nommée n'en a que 2 possibles (Force, Vigueur) : refusé.
        ItemCatalogLoadResult result = Mutated(r =>
        {
            ItemOf(r, "item.lame_du_seuil").Remove("named");
            ItemOf(r, "item.lame_du_seuil").Remove("effects");
        });
        AssertError(result, $"$.data.items[{IndexOf("item.lame_du_seuil")}].rarity");
    }

    // --- Tirage ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Meme_mort_meme_objet_et_toujours_dans_les_bornes()
    {
        const string key = "zone.vallons/3/death/loup2";
        ItemStack first = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, key, 1);
        Assert.Equal(first, ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, key, 1));
        Assert.Single(first.Properties);
        Assert.Null(ItemsCatalog.CheckInstance(first));
        // Un autre rang de la table, une autre mort : un autre tirage (le plus souvent) ; jamais hors des bornes.
        var seen = new HashSet<string>();
        foreach (ItemDefinition item in ItemsCatalog.Items.Where(ItemsCatalog.IsRollable))
            for (int i = 0; i < 40; i++)
            {
                ItemStack rolled = ItemRolls.Roll(ItemsCatalog, item.Id, 1, $"zone.vallons/{i}/death/loup{i % 7}", i % 3);
                Assert.Null(ItemsCatalog.CheckInstance(rolled));
                Assert.Equal(ItemsCatalog.RollCount(item.Rarity), rolled.Properties.Count);
                Assert.Equal(rolled.Properties.Count, rolled.Properties.Select(p => p.Id).Distinct().Count());
                seen.Add(rolled.ToString());
            }
        Assert.True(seen.Count > 100, seen.Count.ToString());
        // Un objet qui ne tire pas reste l'objet de base, à sa quantité.
        Assert.Equal(new ItemStack("item.peau_de_loup", 3), ItemRolls.Roll(ItemsCatalog, "item.peau_de_loup", 3, key, 0));
        Assert.Equal(new ItemStack("item.lame_du_seuil", 1), ItemRolls.Roll(ItemsCatalog, "item.lame_du_seuil", 1, key, 0));
        Assert.Throws<ArgumentException>(() => ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "pas une clé", 0));
    }

    [Fact]
    public void Propriete_toutes_les_valeurs_des_bornes_sortent_et_aucune_autre()
    {
        // Sur beaucoup de morts, chaque valeur tirée d'une propriété des bottes (rare, niveau 6) reste dans ses bornes, et les deux bornes sortent.
        ItemDefinition boots = ItemsCatalog.Item("item.bottes_du_renard");
        var values = new Dictionary<string, HashSet<int>>();
        for (int i = 0; i < 3000; i++)
            foreach (RolledProperty r in ItemRolls.Roll(ItemsCatalog, boots.Id, 1, $"zone.vallons/{i}/death/sanglier", 2).Properties)
                (values.TryGetValue(r.Id, out HashSet<int>? set) ? set : values[r.Id] = new HashSet<int>()).Add(r.Value);
        Assert.Equal(3, values.Count);   // Solidité, Vigueur, Souffle
        foreach ((string id, HashSet<int> seen) in values)
        {
            Assert.True(ItemsCatalog.TryGetProperty(id, out PropertyDefinition p));
            (int low, int high) = ItemsCatalog.Bounds(p, boots);
            Assert.All(seen, v => Assert.InRange(v, low, high));
            Assert.Contains(low, seen);
            Assert.Contains(high, seen);
        }
    }

    [Fact]
    public void Instance_hors_des_bornes_ou_mal_formee_refusee()
    {
        ItemStack ok = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        RolledProperty r = ok.Properties[0];
        Assert.True(ItemsCatalog.TryGetProperty(r.Id, out PropertyDefinition p));
        (int low, int high) = ItemsCatalog.Bounds(p, ItemsCatalog.Item(ok.Item));
        Assert.Null(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty(r.Id, low) })));
        Assert.Null(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty(r.Id, high) })));
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty(r.Id, high + 1) })));
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty(r.Id, low - 1) })));
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 2, ok.Properties)));                                   // quantité
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { r, new RolledProperty("property.souffle", 2) })));   // nombre
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty("property.force", 3) })));         // place
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack(ok.Item, 1, new[] { new RolledProperty("property.inconnue", 3) })));
        Assert.NotNull(ItemsCatalog.CheckInstance(new ItemStack("item.lame_du_seuil", 1, new[] { new RolledProperty("property.force", 5) })));   // nommé
    }

    // --- Inventaire et effets ----------------------------------------------------------------------------------------------

    [Fact]
    public void Proprietes_gardees_quand_on_porte_ote_echange_et_range()
    {
        ItemStack boots = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        Inventory bag = Bag(10, new[] { new ItemStack("item.bottes_du_renard", 1), boots, new ItemStack("item.peau_de_loup", 5) });
        GearBonus before = bag.Gear();
        Assert.Equal(InventoryRefusal.None, bag.Equip(1, 10));   // l'instance aux propriétés, pas la paire de base
        Assert.Equal(boots, bag.WornInstanceAt("feet"));
        Assert.Equal(before + ItemsCatalog.BonusOf(boots), bag.Gear());
        Assert.Equal(InventoryRefusal.None, bag.Unequip("feet"));
        Assert.Equal(boots, bag.Stacks.Last());
        bag.Sort(InventorySort.Type);
        Assert.Equal(new[] { new ItemStack("item.bottes_du_renard", 1), boots, new ItemStack("item.peau_de_loup", 5) }, bag.Stacks);
        // Une instance aux propriétés ne s'empile pas, même avec la même.
        Assert.Equal(InventoryRefusal.None, bag.Add(boots));
        Assert.Equal(4, bag.Stacks.Count);
        Assert.Throws<ArgumentException>(() => bag.Add(new ItemStack("item.bottes_du_renard", 1, new[] { new RolledProperty("property.force", 1) })));
    }

    [Fact]
    public void Effets_et_ensembles_portes_pour_le_prochain_combat()
    {
        var worn = new Dictionary<string, string>
        {
            ["weapon"] = "item.lame_de_la_tour", ["head"] = "item.heaume_de_la_tour", ["chest"] = "item.plastron_de_la_tour", ["legs"] = "item.cuissards_de_la_tour",
            ["neck"] = "item.amulette_de_l_aube", ["shield"] = "item.bouclier_de_la_tour",
        };
        Inventory bag = Bag(20, Array.Empty<ItemStack>(), worn);
        Assert.Equal(5, bag.WornPieces("set.garde_de_la_tour"));
        // Le palier à 2 pièces ne donne qu'un bonus aux statistiques (pas d'effet de combat) ; celui à 4 donne son effet.
        Assert.Equal(new[] { "item.amulette_de_l_aube", "set.garde_de_la_tour.4" }, bag.Effects().Select(e => e.Source));
        GearBonus pieces = worn.Values.Select(id => ItemsCatalog.Item(id).Bonus).Aggregate(GearBonus.None, (a, b) => a + b);
        Assert.Equal(pieces + new GearBonus(armor: 60), bag.Gear());   // palier à 2 pièces : +60 armure
        // La sixième pièce ouvre le palier à 6 ; l'ôter le referme.
        Inventory full = Bag(20, new[] { new ItemStack("item.gantelets_de_la_tour", 1) }, worn);
        Assert.Equal(InventoryRefusal.None, full.Equip(0, 20));
        Assert.Equal(new[] { "item.amulette_de_l_aube", "set.garde_de_la_tour.4", "set.garde_de_la_tour.6" }, full.Effects().Select(e => e.Source));
        Assert.Equal(InventoryRefusal.None, full.Unequip("hands"));
        Assert.Equal(2, full.Effects().Count);
        // Les effets portés passent la validation du combat (la même fonction que le catalogue).
        Assert.Empty(EquipmentEffect.CheckList(full.Effects(), ShippedData.Rules.Combat, "equipment"));
    }

    [Fact]
    public void Effets_de_l_entree_dans_la_session_puis_seulement_a_l_entree_suivante()
    {
        SavedCharacter hero = Hero(10, new[] { new ItemStack("item.lame_du_seuil", 1) }, new Dictionary<string, string> { ["neck"] = "item.amulette_de_l_aube" });
        WorldSession w = Play(Zone(), hero);
        Assert.Equal(new[] { "item.amulette_de_l_aube" }, w.EnteredEffects.Select(e => e.Source));
        Assert.Equal(InventoryRefusal.None, w.Equip(0));   // la Lame du Seuil, hors combat
        Assert.Equal(new[] { "item.amulette_de_l_aube" }, w.EnteredEffects.Select(e => e.Source));   // le combat en cours garde les siens
        WorldSession next = Play(Zone(), w.Save().Character!);
        Assert.Equal(new[] { "item.lame_du_seuil", "item.amulette_de_l_aube" }, next.EnteredEffects.Select(e => e.Source));
    }

    // --- Sauvegarde --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Instances_sauvegardees_en_version_9_et_relues()
    {
        ItemStack boots = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        ItemStack sword = ItemRolls.Roll(ItemsCatalog, "item.epee_de_garde", 1, "zone.vallons/2/death/sanglier1", 2);
        SavedCharacter hero = Hero(10, new[] { boots, new ItemStack("item.peau_de_loup", 4) }, new Dictionary<string, string> { ["weapon"] = sword.Item },
            new Dictionary<string, IReadOnlyList<RolledProperty>> { ["weapon"] = sword.Properties });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), hero));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);
        Assert.Contains($"{{\"item\": \"item.bottes_du_renard\", \"count\": 1, \"properties\": [{{\"id\": \"{boots.Properties[0].Id}\", \"value\": {boots.Properties[0].Value}}}]}}",
            text, StringComparison.Ordinal);
        Assert.Contains($"\"weapon\": {{\"item\": \"item.epee_de_garde\", \"properties\": [", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(boots, read.Save!.Character!.Inventory[0]);
        Assert.Equal(sword.Properties, read.Save.Character.EquipmentProperties["weapon"]);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // La partie reprend avec les mêmes instances, portée comprise.
        WorldSession w = Play(Zone(), read.Save.Character);
        Assert.Equal(sword, w.Inventory.WornInstanceAt("weapon"));
        Assert.Equal(SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), hero)), text);
        // Fusion de main dans V1.7 : une sauvegarde de version 9 (instances, sans carte d'aventurier) se relit, mise à jour, et se réécrit à
        // l'identique en version 10 ; une carte ou des créatures rares y sont des champs inconnus.
        string v9 = SaveV11.Older(text).Replace(SaveVersion.Current, "\"schemaVersion\": 9");
        SaveLoadResult old = SaveJsonReader.Read(v9);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Migrated);
        Assert.Equal(boots, old.Save.Character!.Inventory[0]);
        Assert.Equal(sword.Properties, old.Save.Character.EquipmentProperties["weapon"]);
        Assert.Null(old.Save.Character.Story.Guild);
        Assert.Equal(text, SaveJsonReader.Write(old.Save));
        string withCard = SaveV11.Older(SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(),
            hero.WithStory(new SavedStory(guild: new SavedGuildCard("g", titles: new[] { "title.guilde.recrue" })))))).Replace(SaveVersion.Current, "\"schemaVersion\": 9");
        Assert.Equal(("$.data.character.story.guild", ValidationCode.UnknownField), (Assert.Single(SaveJsonReader.Read(withCard).Report.Errors).Path, SaveJsonReader.Read(withCard).Report.Errors[0].Code));
    }

    [Fact]
    public void Sauvegarde_d_instances_refusee_avec_son_chemin()
    {
        ItemStack boots = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), Hero(10, new[] { boots })));
        string props = $"[{{\"id\": \"{boots.Properties[0].Id}\", \"value\": {boots.Properties[0].Value}}}]";
        ValidationError Only(string json) => Assert.Single(SaveJsonReader.Read(json).Report.Errors);
        // Version antérieure : les propriétés n'existaient pas.
        Assert.Equal("$.data.character.inventory[0].properties", Only(SaveV11.Older(text).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 7")).Path);
        // Quantité, liste vide, doublon, valeur, identifiant.
        Assert.Equal("$.data.character.inventory[0].count", Only(text.Replace("\"count\": 1, \"properties\"", "\"count\": 2, \"properties\"")).Path);
        Assert.Equal("$.data.character.inventory[0].properties", Only(text.Replace(props, "[]")).Path);
        Assert.Equal("$.data.character.inventory[0].properties[0].value", Only(text.Replace(props, $"[{{\"id\": \"{boots.Properties[0].Id}\", \"value\": 0}}]")).Path);
        Assert.Equal("$.data.character.inventory[0].properties[0].id", Only(text.Replace(props, "[{\"id\": \"vigueur\", \"value\": 3}]")).Path);
        Assert.Equal("$.data.character.inventory[0].properties[1].id",
            Only(text.Replace(props, $"[{{\"id\": \"property.souffle\", \"value\": 3}}, {{\"id\": \"property.souffle\", \"value\": 4}}]")).Path);
        // Qualité et usure : réservées au sous-lot V1.3, utilisées depuis le sous-lot V1.4 (version 11) : une qualité inconnue est refusée,
        // et en version 9 le champ est inconnu.
        ValidationError quality = Only(text.Replace("\"count\": 1, \"properties\"", "\"count\": 1, \"quality\": \"superieure\", \"properties\""));
        Assert.Equal(("$.data.character.inventory[0].quality", ValidationCode.InvalidValue), (quality.Path, quality.Code));
        ValidationError old = Only(SaveV11.Older(text).Replace(SaveVersion.Current, "\"schemaVersion\": 9")
            .Replace("\"count\": 1, \"properties\"", "\"count\": 1, \"quality\": \"superior\", \"properties\""));
        Assert.Equal(("$.data.character.inventory[0].quality", ValidationCode.UnknownField), (old.Path, old.Code));
        Assert.Contains("version 11", old.Message, StringComparison.Ordinal);
        // Équipement : la forme d'instance exige ses propriétés.
        string worn = SaveJsonReader.Write(new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), Hero(10, Array.Empty<ItemStack>(),
            new Dictionary<string, string> { ["feet"] = boots.Item }, new Dictionary<string, IReadOnlyList<RolledProperty>> { ["feet"] = boots.Properties })));
        Assert.Equal("$.data.character.equipment.feet.properties", Only(worn.Replace(", \"properties\": " + props, "")).Path);
        // Des propriétés hors des bornes du catalogue : relecture de la PR #78, ramenées dans les bornes à la reprise de la partie (jamais
        // d'exception : voir V13RelectureTests).
        string far = text.Replace(props, $"[{{\"id\": \"{boots.Properties[0].Id}\", \"value\": 9999}}]");
        SaveLoadResult loaded = SaveJsonReader.Read(far);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        Inventory settled = Inventory.From(loaded.Save!.Character!, ItemsCatalog, 10, out IReadOnlyList<string> adjusted);
        Assert.Single(adjusted);
        Assert.True(ItemsCatalog.TryGetProperty(boots.Properties[0].Id, out PropertyDefinition farProperty));
        Assert.Equal(ItemsCatalog.Bounds(farProperty, ItemsCatalog.Item(boots.Item)).Max, settled.Stacks[0].Properties[0].Value);
    }

    // --- Butin, vente et rachat --------------------------------------------------------------------------------------------

    [Fact]
    public void Butin_aux_proprietes_tirees_par_la_cle_de_la_mort()
    {
        Scenarios.ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        // Table d'essai : les bottes du Renard (rares : une propriété) tombent toujours.
        ItemCatalog catalog = Mutated(r => Data(r)["loot"]![0]!["drops"] = new JsonArray(
            new JsonObject { ["item"] = "item.bottes_du_renard", ["count"] = 1, ["chancePercent"] = 100 })).Catalog!;
        IReadOnlyList<LootGained> Kill()
        {
            var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Progression)),
                loaded.Setup!, ShippedData, Progression, catalog, new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")),
                new Dictionary<string, string> { ["loup1"] = "loot.loup" });
            var loot = new List<LootGained>();
            for (int i = 0; i < 600 && loot.Count == 0; i++)
            {
                if (i % 20 == 1)
                    try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                    catch (ArgumentException) { }
                loot.AddRange(session.Step().Loot);
            }
            Assert.Equal(ItemRolls.Roll(catalog, "item.bottes_du_renard", 1, loot[0].Source.Key, 0), session.Inventory.Stacks.Single());
            return loot;
        }
        LootGained boots = Assert.Single(Kill());
        Assert.Equal(("item.bottes_du_renard", 1, true, 1), (boots.Item, boots.Count, boots.Kept, boots.Properties.Count));
        // La même mort (même clé) donne le même objet.
        Assert.Equal(boots.Properties, Assert.Single(Kill()).Properties);
    }

    [Fact]
    public void Vente_d_une_instance_par_sa_place_et_rachat_avec_ses_proprietes()
    {
        ItemStack boots = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        var market = new Market(ShippedMerchants);
        WorldSession w = At("merchant.armurier", V11TestData.Hero(0, new ItemStack("item.bottes_du_renard", 1), boots), market);
        long before = w.Money;
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", 1, 1));
        Assert.Equal(new[] { new ItemStack("item.bottes_du_renard", 1) }, w.Inventory.Stacks);   // l'instance est partie, pas la paire de base
        Assert.True(w.Money > before);
        Assert.Equal(boots, Assert.Single(market.Buyback[0].Stacks));
        Assert.Equal(TradeRefusal.None, w.BuyBack("merchant.armurier", 0));
        Assert.Equal(boots, w.Inventory.Stacks.Last());
        Assert.Equal(before, w.Money);
        Assert.Equal(TradeRefusal.InvalidCount, w.SellPlace("merchant.armurier", 1, 2));
    }

    // --- Comparaison -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Comparaison_avec_proprietes_effets_et_ensembles()
    {
        ItemStack boots = ItemRolls.Roll(ItemsCatalog, "item.bottes_du_renard", 1, "zone.vallons/1/death/loup1", 0);
        var worn = new Dictionary<string, string> { ["feet"] = "item.bottes_de_cuir", ["neck"] = "item.amulette_de_l_aube",
            ["legs"] = "item.jambieres_du_veneur", ["chest"] = "item.cuirasse_du_veneur", ["shoulders"] = "item.spalieres_du_veneur" };
        Inventory bag = Bag(12, new[] { boots, new ItemStack("item.talisman_du_seuil", 1), new ItemStack("item.gants_du_veneur", 1), new ItemStack("item.gants_de_cuir", 1) }, worn);
        // Les propriétés tirées comptent dans les écarts.
        ItemComparison c = ItemComparison.For(bag, bag.Stacks[0])!;
        RolledProperty p = boots.Properties.Single();
        Assert.True(ItemsCatalog.TryGetProperty(p.Id, out PropertyDefinition property));
        GearBonus bonus = ItemsCatalog.BonusOf(boots);
        CompareLine line = c.Lines.Single(l => l.Label == ItemComparison.BonusLabels[property.Stat]);
        Assert.Equal(bonus[property.Stat].ToString(), line.Candidate);
        Assert.StartsWith(property.Name + " : +" + p.Value, Assert.Single(c.Details).Text);
        // Un effet nouveau en vert, l'effet perdu en rouge.
        ItemComparison talisman = ItemComparison.For(bag, bag.Stacks[1])!;
        Assert.Equal(new[] { CompareTrend.Better, CompareTrend.Worse }, talisman.Details.Select(d => d.Trend));
        Assert.StartsWith("Effet perdu : Une parade parfaite", talisman.Details[1].Text);
        // La quatrième pièce du veneur : 3 → 4 pièces, le palier à 4 s'ouvre (vert) ; des gants ordinaires ne changent rien à l'ensemble.
        ItemComparison gants = ItemComparison.For(bag, bag.Stacks[2])!;
        Assert.Equal(new[] { ("Ensemble « Tenue du veneur » : 3 → 4/4 pièces", CompareTrend.Better, true), ("(2) +8 puissance physique.", CompareTrend.Same, true),
            ("(4) " + ItemsCatalog.Sets[0].Bonuses[1].Text, CompareTrend.Better, true) }, gants.Details.Select(d => (d.Text, d.Trend, d.Active)));
        Assert.Empty(ItemComparison.For(bag, bag.Stacks[3])!.Details);
        // Ôter une pièce portée pour une autre : l'ensemble perd une pièce (rouge), le palier à 2 reste.
        ItemComparison legs = ItemComparison.For(Bag(12, new[] { new ItemStack("item.braies_de_toile", 1) }, worn), new ItemStack("item.braies_de_toile", 1))!;
        Assert.Equal(("Ensemble « Tenue du veneur » : 3 → 2/4 pièces", CompareTrend.Worse), (legs.Details[0].Text, legs.Details[0].Trend));
        // Un objet porté se montre tel quel (rien à comparer).
        ItemComparison self = ItemComparison.For(bag, "item.cuirasse_du_veneur", "chest")!;
        Assert.Equal(("Ensemble « Tenue du veneur » : 3/4 pièces", CompareTrend.Same), (self.Details[0].Text, self.Details[0].Trend));
        Assert.False(self.Details.Last().Active);   // palier à 4 : pas encore
    }
}
