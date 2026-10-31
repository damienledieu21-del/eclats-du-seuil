using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Réponses de Damien du 31 octobre 2026 (vers 18 h 20) aux questions des chantiers V1.2, V1.3, V1.6 et V1.7 : équipement de départ « du
/// novice », quatre pièces du jour chez Garin, effets sur les boucliers et rondache du chef des bandits, propriétés tirées par rareté (0 à 5).
/// </summary>
[Trait("Category", "World")]
public class V1Reponses2Tests
{
    private const string NoviceSword = "item.epee_du_novice", NoviceShield = "item.ecu_du_novice", Shard = "item.eclat_du_seuil_endormi",
        Rondache = "item.rondache_du_chef_des_bandits";

    private static SavedCharacter Level(int level, IEnumerable<ItemStack>? items = null, Dictionary<string, string>? worn = null) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[level - 1])
            .WithItems(items ?? Array.Empty<ItemStack>(), worn ?? new Dictionary<string, string>());

    // --- 1. Équipement de départ « du novice » ---------------------------------------------------------------------------------

    [Fact]
    public void Nouveau_personnage_avec_l_epee_et_l_ecu_du_novice()
    {
        foreach (string id in new[] { NoviceSword, NoviceShield })
        {
            ItemDefinition item = ItemsCatalog.Item(id);
            Assert.Equal((ItemType.Equipment, ItemRarity.Common, 1, false), (item.Type, item.Rarity, item.RequiredLevel, item.Named));
        }
        Assert.Equal(new[] { Shard, NoviceSword, NoviceShield }, ItemsCatalog.StartingFor("class.bastion"));
        SavedCharacter hero = Inventory.Outfit(Hero(), ItemsCatalog);
        Assert.Equal(new Dictionary<string, string> { ["shard"] = Shard, ["shield"] = NoviceShield, ["weapon"] = NoviceSword }, hero.Equipment);
        // L'écu du novice garde la garde des règles : le combat d'un nouveau personnage ne change pas.
        Assert.Equal(ItemsCatalog.Item("item.ecu_de_chene").Guard, ItemsCatalog.Item(NoviceShield).Guard);
    }

    [Fact]
    public void Les_objets_du_novice_se_vendent_une_piece_de_cuivre()
    {
        var market = new Market(ShippedMerchants);
        foreach (Merchant m in ShippedMerchants.Merchants)
            foreach (string id in new[] { NoviceSword, NoviceShield })
            {
                Assert.Equal(1, ShippedMerchants.Values[id]);
                Assert.Equal(1, market.UnitPrice(m.Id, id));
                Assert.DoesNotContain(m.Offers, o => o.Item == id);   // personne ne les vend
            }
        // Vendus pour de vrai chez Garin : une pièce chacun (ôtés d'abord ; l'écu, place obligatoire du Bastion, s'échange avant).
        WorldSession w = At("merchant.armurier", Inventory.Outfit(Hero(0, new ItemStack("item.targe_d_osier", 1)), ItemsCatalog).WithXp(Progression.XpToLevel[1]));
        Assert.Equal(InventoryRefusal.None, w.Unequip("weapon"));
        Assert.Equal(InventoryRefusal.None, w.Equip(w.Inventory.Stacks.ToList().FindIndex(s => s.Item == "item.targe_d_osier")));
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.armurier", NoviceSword, 1));
        Assert.Equal(TradeRefusal.None, w.Sell("merchant.armurier", NoviceShield, 1));
        Assert.Equal(2, w.Money);
    }

    [Fact]
    public void L_Eclat_du_Seuil_ne_s_enleve_pas_et_ne_se_vend_pas()
    {
        Assert.False(ShippedMerchants.Values.ContainsKey(Shard));
        var market = new Market(ShippedMerchants);
        Assert.Equal(0, market.UnitPrice("merchant.armurier", Shard));
        Inventory inv = Inventory.From(Inventory.Outfit(Hero(), ItemsCatalog), ItemsCatalog, 1);
        Assert.Equal(InventoryRefusal.Required, inv.Unequip("shard"));
        Assert.Equal(Shard, inv.Equipped["shard"]);
        // Même dans le sac (sauvegarde écrite à la main), il ne se vend pas : sans valeur.
        WorldSession w = At("merchant.armurier", Inventory.Outfit(Hero(0, new ItemStack(Shard, 1)), ItemsCatalog));
        Assert.Equal(TradeRefusal.NoValue, w.Sell("merchant.armurier", Shard, 1));
    }

    [Fact]
    public void Les_anciennes_parties_gardent_leur_equipement_et_l_ecu_du_novice_remplace_un_bouclier_manquant()
    {
        // Une partie d'avant : l'épée du milicien et l'écu de chêne restent portés.
        SavedCharacter old = Level(1, worn: new Dictionary<string, string> { ["weapon"] = "item.epee_du_milicien", ["shield"] = "item.ecu_de_chene", ["shard"] = Shard });
        WorldSession kept = Play(Zone(), old);
        Assert.Empty(kept.Restored);
        Assert.Equal(("item.epee_du_milicien", "item.ecu_de_chene"), (kept.Inventory.Equipped["weapon"], kept.Inventory.Equipped["shield"]));
        // Un Bastion sans bouclier, ni porté ni dans le sac : FillRequired lui donne l'écu du novice (et l'Éclat).
        WorldSession given = Play(Zone(), Level(1, new[] { new ItemStack("item.peau_de_loup", 1) }));
        Assert.Equal(new[] { NoviceShield, Shard }, given.Restored);
        Assert.Equal(NoviceShield, given.Inventory.Equipped["shield"]);
    }

    // --- 2. Garin : quatre pièces du jour ---------------------------------------------------------------------------------------

    [Fact]
    public void Garin_vend_quatre_pieces_du_jour_sans_profit_a_la_revente()
    {
        Merchant garin = ShippedMerchants.Merchant("merchant.armurier");
        Assert.Equal(4, garin.Daily);
        var market = new Market(ShippedMerchants);
        for (long day = 0; day < 30; day++)
        {
            IReadOnlyList<MerchantOffer> stock = market.Stock(garin.Id, day);
            Assert.Equal(4, stock.Count(o => !o.Always));
            // Pas de profit : ce qu'on achète se revend au plus à son prix, chez n'importe quel marchand.
            foreach (MerchantOffer o in stock)
                foreach (Merchant buyer in ShippedMerchants.Merchants)
                    Assert.True(market.UnitPrice(buyer.Id, o.Item) <= o.Price, $"{buyer.Id} {o.Item}");
        }
    }

    // --- 3. Effets sur les boucliers et rondache du chef des bandits -----------------------------------------------------------

    [Fact]
    public void Les_boucliers_portent_des_effets_et_la_rondache_du_chef_en_a_un()
    {
        Assert.True(ItemsCatalog.Places.Single(p => p.Id == "shield").Effects);
        ItemDefinition rondache = ItemsCatalog.Item(Rondache);
        Assert.Equal(("shield", true, ItemRarity.VeryRare), (rondache.Slot, rondache.Named, rondache.Rarity));
        Assert.NotNull(rondache.Guard);
        ItemEffect effect = rondache.Effect!;
        Assert.Equal(Rondache, effect.Effect.Source);
        Assert.Empty(effect.Effect.Modifiers);
        TriggerDefinition riposte = Assert.Single(effect.Effect.Triggers);
        Assert.Equal(2000, riposte.CooldownMs);
        // Même contrôle que les armes et les bijoux (les bornes de combat.equipment des règles).
        Assert.Empty(effect.Effect.Check(ShippedData.Rules.Combat, "$"));
        // Portée, elle compte parmi les effets de l'équipement, et entre au combat avec sa garde.
        WorldSession w = Play(Zone(), Level(5, worn: new Dictionary<string, string> { ["shield"] = Rondache, ["shard"] = Shard }));
        Assert.Contains(w.EnteredEffects, e => e.Source == Rondache);
        Assert.Equal(rondache.Guard, w.EnteredShield);
        for (int i = 0; i < 40; i++) w.Step();
        // Un bouclier sans effet reste sans effet ; une pièce d'armure n'en porte toujours pas.
        Assert.Null(ItemsCatalog.Item("item.ecu_de_chene").Effect);
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        JsonObject gambison = root["data"]!["items"]!.AsArray().Select(i => i!.AsObject()).Single(i => (string)i["id"]! == "item.gambison");
        gambison["named"] = true;
        gambison["effects"] = JsonNode.Parse("{\"text\":\"essai\",\"modifiers\":[{\"key\":\"damageTaken\",\"add\":-0.05}]}");
        ItemCatalogLoadResult refused = ItemJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".effects", StringComparison.Ordinal) && e.Message.Contains("armes, boucliers et bijoux", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_effet_de_bouclier_hors_des_bornes_est_refuse_comme_pour_une_arme()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        JsonArray items = root["data"]!["items"]!.AsArray();
        int index = items.Select(i => (string)i!["id"]!).ToList().IndexOf(Rondache);
        items[index]!["effects"]!["triggers"]![0]!["effects"]![0]!["coefficient"] = 1.5;   // au-dessus de combat.equipment.maxStrikeCoefficient (1)
        ItemCatalogLoadResult refused = ItemJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Report.Errors, e => e.Path == $"$.data.items[{index}].effects.triggers[0].effects[0].coefficient");
    }

    [Fact]
    public void Le_chef_des_bandits_donne_sa_rondache_a_sa_premiere_defaite()
    {
        Boss chief = Zone().Bosses.Single(b => b.Id == "chef_des_bandits");
        Assert.Equal(new[] { (Rondache, 1) }, chief.Rewards);
        Assert.Equal("zone.vallons/boss/chef_des_bandits", Zone().VictoryKeyOf(chief));   // la clé de registre de V1.6 : une seule fois
        Assert.True(ShippedMerchants.Values[Rondache] > 0);
    }

    // --- 4. Le loup blanc devient rare ------------------------------------------------------------------------------------------

    private const string WhiteWolf = "loup_blanc";

    /// <summary>Tick du monde au jour <paramref name="day"/>, à <paramref name="hour"/> h <paramref name="minute"/> (le monde commence à 8 h du jour 0).</summary>
    private static long Tick(long day, int hour, int minute = 0) =>
        (day * WorldClock.MinutesPerDay + hour * 60 + minute - WorldClock.DefaultStartMinute) * (new WorldClock(50).TicksPerDay / WorldClock.MinutesPerDay);

    /// <summary>Partie ouverte dans les Vallons au tick du monde donné, un tick joué ; les événements de ce premier tick sont rendus.</summary>
    private static WorldSession VallonsAt(long worldTick, SavedCharacter hero, out SessionStep first, IEnumerable<string>? consumed = null)
    {
        ZoneDefinition z = Zone();
        var w = new WorldSession(new WorldSave("zone.vallons", worldTick, 0, consumed ?? Array.Empty<string>(), hero), z, ShippedData, Progression, ItemsCatalog, Book(z));
        first = w.Step();
        return w;
    }

    private static bool WolfHere(WorldSession w) => w.Zone.Engine.CombatantIds.Any(id => id.StartsWith(WhiteWolf, StringComparison.Ordinal) && w.Zone.Engine.View(id).IsPresent);

    /// <summary>Premières nuits (numéros) où le tirage fait venir le loup blanc, et où il ne le fait pas.</summary>
    private static (long Yes, long No) Nights()
    {
        Population wolf = Zone().PopulationOf(WhiteWolf)!;
        long yes = -1, no = -1;
        for (long n = 1; n < 100 && (yes < 0 || no < 0); n++)
        {
            if (NightCreatures.Appears("zone.vallons", n, wolf)) { if (yes < 0) yes = n; }
            else if (no < 0) no = n;
        }
        return (yes, no);
    }

    [Fact]
    public void Le_loup_blanc_est_une_creature_rare_de_la_nuit()
    {
        ZoneDefinition z = Zone();
        Population wolf = z.PopulationOf(WhiteWolf)!;
        NightSpawn night = wolf.Night!;
        Assert.Equal((35, 15000L, "text.zone.loup_blanc.signe"), (night.ChancePercent, night.SignMs, night.Sign));
        Assert.Equal("Un hurlement glacé monte du pré…", Texts.Get(night.Sign));
        Assert.Equal(6, z.SchemaVersion);
        // Il est dans le scénario de zone (son modèle), mais pas au début d'une session : il vient plus tard, après son signe.
        Assert.Contains(z.Setup.Combatants, c => c.Id == WhiteWolf);
        Assert.DoesNotContain(ZoneSetups.For(z, Array.Empty<string>()).Combatants, c => c.Id == WhiteWolf);
        Assert.Equal(z.Setup.Combatants.Count - 1, ZoneSetups.For(z, Array.Empty<string>()).Combatants.Count);
        // Seul le loup blanc est une créature de la nuit.
        Assert.Equal(new[] { WhiteWolf }, z.Populations.Where(p => p.Night != null).Select(p => p.Creature));
    }

    [Fact]
    public void Chaque_nuit_a_son_numero_et_son_tirage_deterministe()
    {
        var clock = new WorldClock(50);
        Assert.Null(NightCreatures.NightOf(clock.At(0)));                      // 8 h du jour 0
        Assert.Null(NightCreatures.NightOf(clock.At(Tick(0, 21, 59))));
        Assert.Equal(0L, NightCreatures.NightOf(clock.At(Tick(0, 22))));
        Assert.Equal(0L, NightCreatures.NightOf(clock.At(Tick(1, 4, 59))));      // la nuit du jour 0 se finit le jour 1 à 5 h
        Assert.Null(NightCreatures.NightOf(clock.At(Tick(1, 5))));
        Assert.Equal(3L, NightCreatures.NightOf(clock.At(Tick(3, 23, 30))));
        Assert.Equal("zone.vallons/night/3/loup_blanc", NightCreatures.Key("zone.vallons", 3, WhiteWolf));
        Assert.True(SaveValidator.IsEventKey(NightCreatures.Key("zone.vallons", 3, WhiteWolf)));
        // Même nuit, même tirage ; environ une nuit sur trois (35 %) sur 1 000 nuits.
        Population wolf = Zone().PopulationOf(WhiteWolf)!;
        bool[] first = Enumerable.Range(0, 1000).Select(n => NightCreatures.Appears("zone.vallons", n, wolf)).ToArray();
        Assert.Equal(first, Enumerable.Range(0, 1000).Select(n => NightCreatures.Appears("zone.vallons", n, wolf)));
        int count = first.Count(b => b);
        Assert.InRange(count, 300, 400);
        Assert.False(NightCreatures.Appears("zone.vallons", 1, Zone().PopulationOf("loup1")!));   // une créature ordinaire n'a pas de tirage
    }

    [Fact]
    public void Le_jour_et_les_nuits_sans_tirage_le_loup_blanc_ne_vient_pas()
    {
        var (_, no) = Nights();
        foreach (long tick in new[] { Tick(no, 12), Tick(no, 23) })
        {
            WorldSession w = VallonsAt(tick, Hero(), out SessionStep first);
            var events = new List<PopulationEvent>(first.Population);
            for (int i = 0; i < 400; i++) events.AddRange(w.Step().Population);
            Assert.DoesNotContain(events, e => e.Kind == PopulationEventKind.Sign);
            Assert.False(WolfHere(w));
        }
    }

    [Fact]
    public void Une_nuit_tiree_le_loup_blanc_vient_apres_son_signe_et_part_a_l_aube()
    {
        var (yes, _) = Nights();
        // 4 h 55 : le signe d'abord, l'arrivée 15 s plus tard (300 ticks), puis l'aube à 5 h : au calme, il s'en va.
        WorldSession w = VallonsAt(Tick(yes + 1, 4, 55), Hero(), out SessionStep first);
        PopulationEvent sign = Assert.Single(first.Population, e => e.Kind == PopulationEventKind.Sign);
        Assert.Equal((WhiteWolf, "text.zone.loup_blanc.signe"), (sign.Population, sign.Creature));
        Assert.False(WolfHere(w));
        var events = new List<PopulationEvent>();
        for (int i = 0; i < 298; i++) events.AddRange(w.Step().Population);
        Assert.False(WolfHere(w));
        for (int i = 0; i < 4; i++) events.AddRange(w.Step().Population);
        PopulationEvent came = Assert.Single(events, e => e.Kind == PopulationEventKind.Respawned && e.Population == WhiteWolf);
        Assert.Equal("loup_blanc_v2", came.Creature);
        Assert.True(WolfHere(w));
        Assert.DoesNotContain(events, e => e.Kind == PopulationEventKind.Sign);   // un seul signe
        // L'aube (5 h) : il quitte la zone, sans mort ni butin.
        while (w.Zone.Time.Phase == DayPhase.Night) events.AddRange(w.Step().Population);
        for (int i = 0; i < 3; i++) events.AddRange(w.Step().Population);
        Assert.Single(events, e => e.Kind == PopulationEventKind.Withdrew && e.Population == WhiteWolf);
        Assert.False(WolfHere(w));
        Assert.DoesNotContain(w.Zone.Consumed, k => k.Contains("/night/", StringComparison.Ordinal));
        // Le même moment donne la même chose (tirage par la clé de la nuit, jamais par l'horloge réelle).
        WorldSession again = VallonsAt(Tick(yes + 1, 4, 55), Hero(), out SessionStep same);
        Assert.Equal(first.Population.Select(e => e.Kind + e.Creature), same.Population.Select(e => e.Kind + e.Creature));
    }

    [Fact]
    public void Vaincu_cette_nuit_il_ne_revient_pas_avant_la_nuit_suivante()
    {
        var (yes, _) = Nights();
        string key = NightCreatures.Key("zone.vallons", yes, WhiteWolf);
        // La victoire de la nuit garde la clé de la nuit (vaincu de jour, rien n'est gardé).
        WorldSession w = VallonsAt(Tick(yes, 23), Hero(), out _);
        Population wolf = w.Definition!.PopulationOf(WhiteWolf)!;
        w.NightDefeated(wolf, 0);
        Assert.True(w.Zone.IsConsumed(key));
        WorldSession day = VallonsAt(Tick(yes, 12), Hero(), out _);
        day.NightDefeated(wolf, 0);
        Assert.DoesNotContain(day.Zone.Consumed, k => k.Contains("/night/", StringComparison.Ordinal));
        // Une halte (nouvelle session) la même nuit : la clé au registre, il ne revient pas.
        WorldSession after = VallonsAt(Tick(yes, 23, 30), Hero(), out SessionStep first, new[] { key });
        var events = new List<PopulationEvent>(first.Population);
        for (int i = 0; i < 400; i++) events.AddRange(after.Step().Population);
        Assert.DoesNotContain(events, e => e.Kind == PopulationEventKind.Sign);
        Assert.False(WolfHere(after));
    }

    [Fact]
    public void Une_quete_ou_l_epreuve_en_cours_le_fait_venir_a_coup_sur()
    {
        // Frère Anselme : « La bête blanche », à l'étape de la chasse ; l'épreuve E de la guilde, à l'étape du loup blanc. En plein jour.
        foreach ((string quest, string step) in new[] { ("quest.bete_blanche", "chasse"), ("quest.epreuve_rang_e", "loup_blanc") })
        {
            SavedCharacter hero = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { [quest] = new QuestProgress(step) }));
            WorldSession w = VallonsAt(Tick(2, 12), hero, out SessionStep first);
            Assert.Single(first.Population, e => e.Kind == PopulationEventKind.Sign);
            for (int i = 0; i < 302; i++) w.Step();
            Assert.True(WolfHere(w), quest);
        }
        // Une fois la chasse faite (compteur au bout), il redevient rare.
        SavedCharacter done = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { ["quest.bete_blanche"] = new QuestProgress("chasse", 1) }));
        VallonsAt(Tick(2, 12), done, out SessionStep none);
        Assert.DoesNotContain(none.Population, e => e.Kind == PopulationEventKind.Sign);
    }

    // --- 6. Propriétés tirées par rareté : 0, 1, 2, 3, 4, 5 --------------------------------------------------------------------

    [Fact]
    public void Proprietes_tirees_de_zero_a_cinq_selon_la_rarete()
    {
        ItemRarity[] order = { ItemRarity.Common, ItemRarity.Rare, ItemRarity.VeryRare, ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Mythic };
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, order.Select(r => ItemsCatalog.RollCount(r)));
        // L'échelle reste de 100 à 150 %.
        Assert.Equal(new[] { 100, 100, 110, 120, 135, 150 }, order.Select(r => ItemsCatalog.RarityRolls.Single(x => x.Rarity == r).ScalePercent));
        // Le pire cumul de l'équipement porté (WornBound) reste sous la limite du combat : aucune borne à ajuster.
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long b, long rolled, long sets) = ItemsCatalog.WornBound(stat);
            Assert.True(b + rolled + sets <= GearBonus.MaxValue, GearBonus.Fields[stat]);
        }
    }
}
