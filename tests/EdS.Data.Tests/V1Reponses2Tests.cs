using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Guild;
using EdS.Data.Merchants;
using EdS.Data.Story;
using EdS.Data.Validation;
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

    // --- Fusion de main après la PR #80 (V1.4 : forge, qualité, usure) -------------------------------------------------------

    [Fact]
    public void L_epee_du_novice_s_use_et_se_repare()
    {
        Inventory inv = Inventory.From(Inventory.Outfit(Hero(), ItemsCatalog), ItemsCatalog, 1);
        IReadOnlyList<ItemStack> worn = inv.Places().Select(p => inv.WornInstanceAt(p)).OfType<ItemStack>().Where(x => ItemsCatalog.Wears(ItemsCatalog.Item(x.Item))).ToList();
        inv.WearPieces(worn, 30, out IReadOnlyList<string> zero);
        Assert.Empty(zero);   // entamée, pas usée à zéro (relecture de la PR #83 : WearPieces, la seule usure de la partie)
        Assert.True(ItemsCatalog.WearLeft(inv.WornInstanceAt("weapon")!) < ItemsCatalog.WearMax);
        // À la forge : réparée contre de l'or (le prix minimum : elle vaut 1 cuivre), solidité pleine.
        ZoneDefinition forge = Zone(-19, -5);
        CraftingBook crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, forge, ItemsCatalog).Book!;
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, inv.SaveInto(Hero(1000))), forge, ShippedData, Progression,
            ItemsCatalog, Book(forge), crafting, market: new Market(ShippedMerchants));
        w.Step();
        long price = w.RepairPrice();
        Assert.Equal(crafting.Repair.Price(1, ItemsCatalog.WearMax - ItemsCatalog.WearLeft(inv.WornInstanceAt("weapon")!), ItemsCatalog.WearMax)
            + crafting.Repair.Price(1, ItemsCatalog.WearMax - ItemsCatalog.WearLeft(inv.WornInstanceAt("shield")!), ItemsCatalog.WearMax), price);
        Assert.True(price > 0);
        Assert.Equal(RepairRefusal.None, w.Repair(null, out long paid));
        Assert.Equal((price, 1000 - price), (paid, w.Money));
        Assert.Equal(ItemsCatalog.WearMax, ItemsCatalog.WearLeft(w.Inventory.WornInstanceAt("weapon")!));
    }

    [Fact]
    public void La_rondache_usee_a_zero_perd_son_effet()
    {
        Dictionary<string, ItemStack> Worn(int wear) => new() { ["shield"] = new ItemStack(Rondache, 1, wear: wear), ["shard"] = new ItemStack(Shard, 1) };
        SavedCharacter hero = Level(5);
        Assert.DoesNotContain(Inventory.From(hero.WithItems(hero.Inventory, Worn(0)), ItemsCatalog, 5).Effects(), e => e.Source == Rondache);
        Assert.Contains(Inventory.From(hero.WithItems(hero.Inventory, Worn(1)), ItemsCatalog, 5).Effects(), e => e.Source == Rondache);
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
        var w = new WorldSession(new WorldSave("zone.vallons", worldTick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey).Distinct(StringComparer.Ordinal), hero), z, ShippedData, Progression, ItemsCatalog, Book(z));
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
        Assert.Equal(11, z.SchemaVersion);   // V2.6 : cicatrices ; V2.5 : événements ; V2.4 : temps ; V2.1 : jour et nuit ; V1.8 : zones en mélange
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

    // --- Relecture de la PR #81 : loup blanc ---------------------------------------------------------------------------------

    private static string? WolfId(WorldSession w) =>
        w.Zone.Engine.CombatantIds.LastOrDefault(id => id.StartsWith(WhiteWolf + "_v", StringComparison.Ordinal));

    [Fact]
    public void Une_laisse_quand_il_n_est_plus_voulu_vaut_un_depart()
    {
        // De jour, pour « La bête blanche » : il vient ; puis l'objectif est rempli au moment où il lâche sa cible (laisse).
        SavedCharacter hero = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { ["quest.bete_blanche"] = new QuestProgress("chasse") }));
        WorldSession w = VallonsAt(Tick(2, 12), hero, out _);
        for (int i = 0; i < 302; i++) w.Step();
        string wolf = WolfId(w)!;
        Assert.True(w.Zone.Engine.View(wolf).IsPresent);
        w.Narrative!.Defeated(WhiteWolf, WhiteWolf);   // plus voulu
        w.Zone.Leave(wolf);                           // il part comme sur une laisse
        var events = new List<PopulationEvent>();
        for (int i = 0; i < 400; i++) events.AddRange(w.Step().Population);
        Assert.DoesNotContain(events, e => e.Population == WhiteWolf && e.Kind == PopulationEventKind.Respawned);
        Assert.False(WolfHere(w));
    }

    [Fact]
    public void A_l_aube_il_reste_tant_qu_il_se_bat()
    {
        var (yes, _) = Nights();
        // Le Bastion attend près de sa place (à 4 m) : le loup arrive à 4 h 57 et l'engage ; l'aube passe pendant le combat.
        ZoneDefinition z = Zone(-70, 86);
        var w = new WorldSession(new WorldSave("zone.vallons", Tick(yes + 1, 4, 55), 0, Array.Empty<string>(), Hero().WithXp(1620)), z, ShippedData,
            Progression, ItemsCatalog, Book(z));
        bool engagedAtDawn = false;
        for (int i = 0; i < 1200; i++)
        {
            SessionStep step = w.Step();
            string? wolf = WolfId(w);
            bool engaged = wolf != null && w.Zone.Engine.View(wolf).IsPresent && w.Zone.Engine.View(wolf).Engaged;
            if (w.Zone.Time.Phase != DayPhase.Night && engaged) engagedAtDawn = true;
            // Jamais de départ tant qu'il est engagé (le départ part d'un loup au calme).
            // (Sous-lot V2.1 : les bêtes de la nuit s'en vont aussi à l'aube ; seul le départ du loup blanc compte ici.)
            if (step.Population.Any(e => e.Kind == PopulationEventKind.Withdrew && e.Population == "loup_blanc")) Assert.False(engaged);
        }
        Assert.True(engagedAtDawn);   // il était encore là, engagé, après 5 h
    }

    /// <summary>
    /// Une nuit tirée, le Bastion (niveau 10) à 2 m de la place du loup blanc, loup à 1 PV : frappé dès qu'il est là. Rend la partie, et le
    /// nombre de combattants du moteur au tick de sa mort (0 s'il n'est pas mort).
    /// </summary>
    private static (WorldSession Session, int Combatants) KillWolf(long night, Func<ZoneDefinition, IEnumerable<string>> consumed)
    {
        ZoneDefinition z = Zone(-70, 88, scenario: s =>
            s["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == WhiteWolf)!["health"] = 1);
        var w = new WorldSession(new WorldSave("zone.vallons", Tick(night, 23), 0, consumed(z).Append(PlayerCharacters.PlacesKey).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            Hero().WithXp(1620)), z, ShippedData, Progression, ItemsCatalog, Book(z));
        for (int i = 0; i < 900; i++)
        {
            if (WolfId(w) is string wolf && w.Zone.Engine.View(wolf).IsPresent && !w.Zone.Engine.View(wolf).IsDead && i % 10 == 0)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", wolf)); }
                catch (ArgumentException) { }
            SessionStep step = w.Step();
            if (step.Experience.Any(g => g.Source.Combatant.StartsWith(WhiteWolf, StringComparison.Ordinal))) return (w, w.Zone.Engine.CombatantIds.Count);
        }
        return (w, 0);
    }

    [Fact]
    public void Vaincu_la_nuit_la_cle_de_la_nuit_est_ecrite_par_la_recompense()
    {
        var (yes, _) = Nights();
        string key = NightCreatures.Key("zone.vallons", yes, WhiteWolf);
        (WorldSession w, int combatants) = KillWolf(yes, z => new[] { z.SafePointKey(z.Entry.Id) });
        Assert.True(combatants > 0);
        Assert.True(w.Zone.IsConsumed(key));
        Assert.Contains(key, w.Save().Consumed);
        // Registre presque plein : la clé de la nuit n'est gardée que s'il reste la place des morts possibles d'un tick après elle.
        IEnumerable<string> Probes(ZoneDefinition z, int free) =>
            Enumerable.Range(0, SaveValidator.MaxConsumed - free - 1).Select(i => $"probe/k{i:D6}").Append(z.SafePointKey(z.Entry.Id));
        // Les compétences gagnées données à l'entrée (niveau 10) prennent aussi leurs places au registre.
        int given = w.Zone.Consumed.Count(k => k.StartsWith("character/", StringComparison.Ordinal));
        (WorldSession tight, int c1) = KillWolf(yes, z => Probes(z, given + combatants + 1));   // la mort prend une place, il en reste C : pas la clé
        Assert.Equal(combatants, c1);
        Assert.False(tight.Zone.IsConsumed(key));
        (WorldSession roomy, int c2) = KillWolf(yes, z => Probes(z, given + combatants + 2));   // juste la place
        Assert.Equal(combatants, c2);
        Assert.True(roomy.Zone.IsConsumed(key));
    }

    [Fact]
    public void Creature_de_la_nuit_invalide_refusee_avec_son_chemin()
    {
        JsonObject Root() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        int index = Root()["data"]!["populations"]!.AsArray().Select(p => (string)p!["creature"]!).ToList().IndexOf(WhiteWolf);
        string path = $"$.data.populations[{index}].night";
        void Refused(string at, ValidationCode code, Action<JsonObject> change)
        {
            JsonObject root = Root();
            change(root["data"]!["populations"]![index]!["night"]!.AsObject());
            ZoneLoadResult r = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path == at && e.Code == code);
        }
        Refused(path + ".chancePercent", ValidationCode.MissingField, n => n.Remove("chancePercent"));
        Refused(path + ".signMs", ValidationCode.MissingField, n => n.Remove("signMs"));
        Refused(path + ".sign", ValidationCode.MissingField, n => n.Remove("sign"));
        Refused(path + ".chancePercent", ValidationCode.OutOfRange, n => n["chancePercent"] = 0);
        Refused(path + ".chancePercent", ValidationCode.OutOfRange, n => n["chancePercent"] = 101);
        Refused(path + ".signMs", ValidationCode.InvalidValue, n => n["signMs"] = 15010);   // pas un multiple du tick (50 ms)
        Refused(path + ".signMs", ValidationCode.OutOfRange, n => n["signMs"] = ZoneValidator.MaxSignMs + 50);
        Refused(path + ".sign", ValidationCode.InvalidValue, n => n["sign"] = "Un hurlement");
        // Un fichier de schéma 5 est refusé (créature de la nuit au schéma 6).
        JsonObject old = Root();
        old["schemaVersion"] = 5;
        ZoneLoadResult five = ZoneJsonReader.Read(old.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.Contains(five.Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
        // Un signe bien formé mais absent des textes : refusé par l'histoire (qui a les textes), au chemin de la zone.
        JsonObject missing = Root();
        missing["data"]!["populations"]![index]!["night"]!["sign"] = "text.zone.loup_blanc.absent";
        ZoneLoadResult zone = ZoneJsonReader.Read(missing.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(zone.IsValid, zone.Report.ToString());
        StoryLoadResult story = StoryJsonReader.ReadStory(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)), Texts, zone.Zone!, ItemsCatalog, V16TestData.Interiors);
        Assert.False(story.IsValid);
        Assert.Contains(story.Report.Errors, e => e.Path == $"zone.vallons:{path}.sign" && e.Code == ValidationCode.UnknownReference);
    }

    [Fact]
    public void La_riposte_de_la_rondache_part_sur_un_coup_bloque()
    {
        // Le scénario d'entraînement à l'équipement : le Bastion équipé porte la rondache (à la place de l'amulette) et garde contre les coups.
        JsonNode effect = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!
            ["data"]!["items"]!.AsArray().Single(i => (string)i!["id"]! == Rondache)!["effects"]!["triggers"]))!;
        EdS.Data.Scenarios.ScenarioLoadResult loaded = ReadMutated(r =>
            Combatant(r, 0)["equipment"]![1] = new JsonObject { ["id"] = Rondache, ["triggers"] = effect }, scenario: "entrainement_equipement");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new EdS.Simulation.Engine.MemoryCombatLog();
        EdS.Simulation.Engine.CombatEngine.Run(loaded.Setup!, log);
        string[] riposte = log.Lines.Where(l => l.Contains($"\"trigger\":\"{Rondache}/0@bastion_equipe#0\"", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(riposte);
        Assert.Contains(riposte, l => l.Contains("\"event\":\"damage\"", StringComparison.Ordinal) && l.Contains("assaillant_equipe", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, l => l.Contains(Rondache + "/0@bastion_nu", StringComparison.Ordinal));
    }

    // --- 6. Propriétés tirées par rareté : 0, 1, 2, 3, 4, 5 --------------------------------------------------------------------

    [Fact]
    public void Proprietes_tirees_de_zero_a_cinq_selon_la_rarete()
    {
        ItemRarity[] order = { ItemRarity.Common, ItemRarity.Rare, ItemRarity.VeryRare, ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Mythic };
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, order.Select(r => ItemsCatalog.RollCount(r)));
        // L'échelle reste de 100 à 150 %.
        Assert.Equal(new[] { 100, 100, 110, 120, 135, 150 }, order.Select(r => ItemsCatalog.RarityRolls.Single(x => x.Rarity == r).ScalePercent));
        // Le pire cumul de l'équipement porté (WornBound : chef-d'œuvre de V1.4 et propriétés de 0 à 5 compris) reste sous la limite du combat :
        // aucune borne à ajuster.
        Assert.True(ItemsCatalog.MasterworkPercent > 0);
        for (int stat = 0; stat < GearBonus.Fields.Count; stat++)
        {
            (long b, long rolled, long sets) = ItemsCatalog.WornBound(stat);
            Assert.True(b + rolled + sets <= GearBonus.MaxValue, GearBonus.Fields[stat]);
        }
    }
}

/// <summary>
/// Réponses de Damien du 31 octobre 2026 (vers 18 h 20), guilde : les exploits d'avant l'inscription comptent à l'inscription, une fois ; des
/// contrats pressés, mieux payés, à finir avant la fin de la journée du monde ; le loup blanc vient pour un contrat en cours.
/// </summary>
[Trait("Category", "World")]
public class V1Reponses2GuildeTests
{
    private const string Mistress = "npc.maitresse_guilde";

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());

    private static IReadOnlyList<ZoneDefinition> AllZones => new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList();

    private static ZoneDefinition Inside(string id) => V16TestData.Interiors.Single(z => z.Id == id);

    private static JsonObject GuildJson() => JsonNode.Parse(File.ReadAllText(GuildJsonReader.PathIn(DataDirectory)))!.AsObject();

    private static readonly Lazy<StoryBook> ShippedStory = new(() => Book(Vallons.Value));

    /// <summary>L'histoire livrée, la maîtresse à 1,6 m de la porte de la guilde (où l'on arrive).</summary>
    private static readonly Lazy<StoryBook> NearDoor = new(() =>
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();
        foreach (JsonNode? n in root["data"]!["npcs"]!.AsArray())
            if ((string)n!["id"]! == Mistress) V23TestData.Move(n!, new JsonArray(0, -1.6));
        StoryLoadResult r = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons.Value, ItemsCatalog, V16TestData.Interiors);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Story!;
    });

    private static GuildLoadResult ReadGuild(JsonObject root) =>
        GuildJsonReader.Read(root.ToJsonString(), Texts, ShippedStory.Value, Vallons.Value, AllZones, ItemsCatalog, ShippedMerchants);

    private static readonly Lazy<GuildBook> ShippedGuild = new(() =>
    {
        GuildLoadResult r = ReadGuild(GuildJson());
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    private static GuildBook Guild => ShippedGuild.Value;

    private static long DayTicks => WorldClock.DefaultDayLengthMs / 50;

    private static SavedGuildCard Card(string rank, IReadOnlyDictionary<string, long>? active = null, long day = 0, IEnumerable<string>? taken = null) =>
        new SavedGuildCard(rank, 0, 0, new[] { "title.guilde.recrue" }, null, active, day, taken);

    private static WorldSession InGuild(SavedCharacter hero, long tick = 0)
    {
        var session = new WorldSession(new WorldSave("zone.guilde", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), Inside("zone.guilde"), ShippedData, Progression,
            ItemsCatalog, NearDoor.Value, door: "sortie", storyZone: "zone.vallons", guild: Guild);
        session.Step();
        return session;
    }

    private static List<string> Steps(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    // --- 7. Les exploits d'avant l'inscription ------------------------------------------------------------------------------

    [Fact]
    public void A_l_inscription_les_exploits_d_avant_comptent_une_seule_fois()
    {
        var quests = new Dictionary<string, QuestProgress>
        {
            ["quest.traces_sous_le_moulin"] = new QuestProgress(QuestProgress.Done),   // principale : 40
            ["quest.peaux_pour_l_auberge"] = new QuestProgress(QuestProgress.Done),    // secondaire : 20
            ["quest.bete_blanche"] = new QuestProgress("temoin"),                      // en cours : rien
        };
        SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { "fact.chef_des_bandits.vaincu", "fact.traces.recit" }, quests,
            discoveries: new[] { "discovery.traces_griffues", "discovery.eclats_dans_le_pre" }, hunted: new[] { "loup_blanc" }));
        WorldSession w = InGuild(hero);
        long expected = 40 + 20 + 2 * Guild.DiscoveryPoints + Guild.RarePoints["loup_blanc"] + Guild.FactPoints["fact.chef_des_bandits.vaincu"];
        Assert.Equal(expected, w.Narrative!.ReputationPoints());
        Assert.Equal(GuildRefusal.None, w.JoinGuild(out IReadOnlyList<StoryEvent> joined));
        Assert.Equal(new[] { "GuildJoined g", $"GuildReputation g {expected}" }, joined.Select(e => e.ToString()));
        Assert.Equal(expected, w.Narrative.GuildPoints);
        // Une seule fois : pas de seconde inscription ; les mêmes exploits ne se refont pas (fait déjà su, créature déjà vaincue).
        Assert.Equal(GuildRefusal.AlreadyMember, w.JoinGuild(out IReadOnlyList<StoryEvent> again));
        Assert.Empty(again);
        w.Narrative.Learn("fact.chef_des_bandits.vaincu", new List<StoryEvent>());
        w.Narrative.Defeated("loup_blanc", "loup_blanc");
        Assert.Empty(Steps(w));
        Assert.Equal(expected, w.Narrative.GuildPoints);
        // Gardés dans la carte : relus à la session suivante, sans rien compter de nouveau.
        WorldSession later = InGuild(w.Save().Character!);
        Assert.Equal(expected, later.Narrative!.GuildPoints);
        Assert.Empty(Steps(later));
        // Une épreuve finie ne compte pas (elle donne un rang), une nouvelle recrue sans exploit n'a pas de message.
        var trial = new Dictionary<string, QuestProgress> { ["quest.epreuve_rang_f"] = new QuestProgress(QuestProgress.Done) };
        Assert.Equal(0, InGuild(Hero().WithStory(new SavedStory(quests: trial))).Narrative!.ReputationPoints());
        WorldSession fresh = InGuild(Hero());
        Assert.Equal(GuildRefusal.None, fresh.JoinGuild(out IReadOnlyList<StoryEvent> plain));
        Assert.Equal(new[] { "GuildJoined g" }, plain.Select(e => e.ToString()));
        // La réplique de la maîtresse, à relire.
        Assert.Contains("Ta réputation te précède", Texts.Get("text.ui.guild.reputation"), StringComparison.Ordinal);
    }

    // --- 8. Contrats pressés ------------------------------------------------------------------------------------------------

    [Fact]
    public void Contrats_presses_tires_chaque_jour_et_mieux_payes()
    {
        Assert.Equal((2, 2, 1), (Guild.SchemaVersion, Guild.Daily, Guild.RushDaily));
        foreach (string rank in new[] { "g", "f", "e" })
        {
            List<GuildContract> normal = Guild.Contracts.Where(c => c.Rank == rank && !c.Rush).ToList();
            List<GuildContract> rush = Guild.Contracts.Where(c => c.Rank == rank && c.Rush).ToList();
            Assert.Equal(2, rush.Count);
            Assert.All(rush, c => Assert.True(c.Money > normal.Max(n => n.Money) && c.Points > normal.Max(n => n.Points), c.Id));
        }
        Assert.Equal("guild/e/rush/day/3", GuildBoard.RushKey("e", 3));
        for (long day = 0; day < 40; day++)
        {
            IReadOnlyList<GuildContract> board = GuildBoard.Of(Guild, 2, day);
            Assert.Equal(new[] { "g", "f", "e" }, board.Where(c => c.Rush).Select(c => c.Rank));
            Assert.Equal(board.Select(c => c.Id), GuildBoard.Of(Guild, 2, day).Select(c => c.Id));   // même jour, même tableau
        }
        // Chaque contrat pressé finit par venir.
        Assert.All(Guild.Contracts.Where(c => c.Rush), c => Assert.Contains(Enumerable.Range(0, 60).SelectMany(d => GuildBoard.Of(Guild, 2, d)), x => x.Id == c.Id));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "guild.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(GuildJsonReader.PathIn(DataDirectory))).RootElement));
        foreach (GuildContract c in Guild.Contracts.Where(c => c.Rush)) Assert.True(Texts.Has(c.Name) && Texts.Has(c.Text), c.Id);
    }

    [Fact]
    public void Un_contrat_presse_non_fini_a_minuit_est_perdu_sans_penalite()
    {
        long day = Enumerable.Range(1, 60).First(d => GuildBoard.Of(Guild, 0, d).Any(c => c.Rush && c.Kind == ContractKind.Hunt));
        GuildContract rush = GuildBoard.Of(Guild, 0, day).First(c => c.Rush);
        WorldSession w = InGuild(Hero().WithStory(new SavedStory(guild: Card("g"))).WithMoney(100), day * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract(rush.Id, out _));
        w.Narrative!.Defeated("loup1", "loup");   // une seule créature : pas fini
        SavedCharacter hero = w.Save().Character!;
        // Le lendemain : perdu au premier tick, sans pénalité (bourse, points, contrats réussis inchangés), et rien d'autre.
        WorldSession next = new(new WorldSave("zone.guilde", (day + 1) * DayTicks, 0, new[] { PlayerCharacters.PlacesKey }, hero), Inside("zone.guilde"), ShippedData,
            Progression, ItemsCatalog, NearDoor.Value, door: "sortie", storyZone: "zone.vallons", guild: Guild);
        Assert.Equal(new[] { $"ContractExpired {rush.Id}" }, next.Step().Story.Select(e => e.ToString()));
        Assert.Empty(next.Narrative!.ActiveContracts);
        Assert.Equal((100L, 0L, 0L), (next.Money, next.Narrative.GuildPoints, next.Narrative.ContractsDone));
        Assert.Empty(Steps(next));
        // Il ne s'abandonne pas une fois la journée finie (rien à abandonner).
        WorldSession late = new(new WorldSave("zone.guilde", (day + 1) * DayTicks, 0, new[] { PlayerCharacters.PlacesKey }, hero), Inside("zone.guilde"), ShippedData,
            Progression, ItemsCatalog, NearDoor.Value, door: "sortie", storyZone: "zone.vallons", guild: Guild);
        Assert.Equal(GuildRefusal.NotActive, late.AbandonContract(rush.Id, out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        // Fini le même jour : réussi comme un autre contrat, avec ses récompenses.
        WorldSession done = InGuild(Hero().WithStory(new SavedStory(guild: Card("g"))), day * DayTicks);
        Assert.Equal(GuildRefusal.None, done.TakeContract(rush.Id, out _));
        for (int i = 0; i < rush.Objective.Value; i++) done.Narrative!.Defeated("loup" + (i + 1), "loup");
        Assert.Contains($"ContractDone {rush.Id}", Steps(done));
        Assert.Equal(rush.Money, done.Money);
    }

    [Fact]
    public void Trois_contrats_en_cours_au_plus_presses_compris()
    {
        long day = Enumerable.Range(0, 60).First(d => GuildBoard.Of(Guild, 0, d).Any(c => c.Rush));
        IReadOnlyList<GuildContract> board = GuildBoard.Of(Guild, 1, day);
        WorldSession w = InGuild(Hero().WithStory(new SavedStory(guild: Card("f"))), day * DayTicks);
        List<GuildContract> normal = board.Where(c => !c.Rush).Take(2).ToList();
        List<GuildContract> rush = board.Where(c => c.Rush).ToList();
        Assert.Equal(GuildRefusal.None, w.TakeContract(normal[0].Id, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(rush[0].Id, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(normal[1].Id, out _));
        Assert.Equal(GuildRefusal.TooManyContracts, w.CanTakeContract(rush[1].Id));
        Assert.Equal(GuildRefusal.TooManyContracts, w.TakeContract(rush[1].Id, out IReadOnlyList<StoryEvent> refused));
        Assert.Empty(refused);
        Assert.Equal(3, w.Narrative!.ActiveContracts.Count);
    }

    [Fact]
    public void Guilde_au_schema_2_refusee_avec_son_chemin()
    {
        void Refused(string path, ValidationCode code, Action<JsonObject> change)
        {
            JsonObject root = GuildJson();
            change(root);
            GuildLoadResult r = ReadGuild(root);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
        }
        Refused("$.data.rushDaily", ValidationCode.MissingField, r => r["data"]!.AsObject().Remove("rushDaily"));
        Refused("$.data.rushDaily", ValidationCode.OutOfRange, r => r["data"]!["rushDaily"] = 9);
        Refused("$.data.rushDaily", ValidationCode.OutOfRange, r => r["data"]!["rushDaily"] = -1);
        Refused("$.data.contracts", ValidationCode.OutOfRange, r => r["data"]!["rushDaily"] = 3);   // deux contrats pressés par rang seulement
        Refused("$.data.contracts", ValidationCode.OutOfRange, r =>
        {
            foreach (JsonNode? c in r["data"]!["contracts"]!.AsArray())
                if ((string)c!["rank"]! == "g" && c["rush"] is null) c["rush"] = true;   // plus de contrat normal au rang G
        });
        Refused("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion, r => r["schemaVersion"] = 1);
        // Sans contrat pressé (rushDaily 0) : valide, le tableau n'en tire pas.
        JsonObject none = GuildJson();
        none["data"]!["rushDaily"] = 0;
        GuildBook plain = ReadGuild(none).Book!;
        Assert.DoesNotContain(GuildBoard.Of(plain, 2, 3), c => c.Rush);
    }

    // --- Relecture de la PR #81 --------------------------------------------------------------------------------------------

    [Fact]
    public void Deux_livraisons_du_meme_objet_ne_promettent_pas_les_memes_objets()
    {
        // Les herbes pour Sauge (3, contrat normal) et les herbes du jour (2, contrat pressé), tous deux à la maîtresse, le même jour.
        GuildContract normal = Guild.Contract("contract.herbes_pour_sauge"), rush = Guild.Contract("contract.presse_herbes_du_jour");
        long day = Enumerable.Range(0, 200).First(d => GuildBoard.Of(Guild, 0, d).Contains(normal) && GuildBoard.Of(Guild, 0, d).Contains(rush));
        WorldSession w = InGuild(Hero(0, new ItemStack("item.herbe_de_l_aube", 3)).WithStory(new SavedStory(guild: Card("g"))), day * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract(normal.Id, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(rush.Id, out _));
        Assert.Equal(DialogueRefusal.None, w.Talk(Mistress, out DialogueView? view));
        Assert.Equal(new[] { normal.Id, rush.Id }, view!.Options.Where(o => o.Delivery != null).Select(o => o.Delivery));
        // Le joueur choisit d'abord le contrat pressé (2 herbes) : il n'en reste qu'une de libre, le contrat normal n'est plus proposé.
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Delivery == rush.Id).Index, out DialogueView? next, out _));
        Assert.DoesNotContain(next!.Options, o => o.Delivery != null);
        Assert.Equal(new[] { (normal.Id, 0L), (rush.Id, 1L) }, w.Narrative!.ActiveContracts);
        // Deux herbes de plus : il est de nouveau proposé (5 dans le sac, 2 promises).
        w.Inventory.Add("item.herbe_de_l_aube", 2);
        Assert.Equal(DialogueRefusal.None, w.Talk(Mistress, out DialogueView? more));
        Assert.Equal(new[] { normal.Id }, more!.Options.Where(o => o.Delivery != null).Select(o => o.Delivery));
        // Les deux sont remises au tick suivant, chacune avec ses herbes ; aucune promesse ne disparaît.
        Assert.Equal(DialogueRefusal.None, w.Choose(more.Options.Single(o => o.Delivery == normal.Id).Index, out _, out _));
        List<string> events = Steps(w);
        Assert.Contains($"ContractDone {normal.Id}", events);
        Assert.Contains($"ContractDone {rush.Id}", events);
        Assert.Equal(0, w.Inventory.Count("item.herbe_de_l_aube"));
        Assert.Empty(w.Narrative.ActiveContracts);
        // Le choix du joueur est gardé : avec 3 herbes, le pressé promis est remis, le normal attend (rien de promis pour lui).
        WorldSession few = InGuild(Hero(0, new ItemStack("item.herbe_de_l_aube", 3)).WithStory(new SavedStory(guild: Card("g"))), day * DayTicks);
        few.TakeContract(normal.Id, out _);
        few.TakeContract(rush.Id, out _);
        few.Talk(Mistress, out DialogueView? v);
        few.Choose(v!.Options.Single(o => o.Delivery == rush.Id).Index, out _, out _);
        Assert.Contains($"ContractDone {rush.Id}", Steps(few));
        Assert.Equal((1, 0L), (few.Inventory.Count("item.herbe_de_l_aube"), few.Narrative!.ActiveContracts.Single().Progress));
    }

    /// <summary>Tick du monde à minuit, à la fin du jour <paramref name="day"/> (le monde commence à 8 h du jour 0).</summary>
    private static long Midnight(long day) => day * DayTicks + DayTicks * 16 / 24;

    [Fact]
    public void Un_contrat_presse_fini_au_dernier_tick_avant_minuit_est_reussi()
    {
        GuildContract rush = Guild.Contract("contract.presse_loups_du_soir");
        long day = Enumerable.Range(0, 200).First(d => GuildBoard.Of(Guild, 0, d).Contains(rush));
        // Le premier tick finit une avant minuit ; le suivant est le dernier du jour.
        WorldSession w = InGuild(Hero().WithStory(new SavedStory(guild: Card("g"))), Midnight(day) - 2);
        Assert.Equal(day, w.Day);
        Assert.Equal(GuildRefusal.None, w.TakeContract(rush.Id, out _));
        w.Narrative!.Defeated("loup1", "loup");
        w.Narrative.Defeated("loup2", "loup");
        SessionStep last = w.Step();
        Assert.Equal(day + 1, w.Day);   // minuit passé après ce tick, mais il a été joué le jour du contrat
        Assert.Contains($"ContractDone {rush.Id}", last.Story.Select(e => e.ToString()));
        Assert.DoesNotContain(last.Story, e => e.Kind == StoryEventKind.ContractExpired);
        // Un tick plus tard (minuit) : perdu.
        WorldSession late = InGuild(Hero().WithStory(new SavedStory(guild: Card("g"))), Midnight(day) - 2);
        late.TakeContract(rush.Id, out _);
        late.Narrative!.Defeated("loup1", "loup");
        late.Step();
        late.Narrative.Defeated("loup2", "loup");
        Assert.Equal(new[] { $"ContractExpired {rush.Id}" }, late.Step().Story.Select(e => e.ToString()));
    }

    [Fact]
    public void Prendre_et_pouvoir_prendre_disent_la_meme_chose()
    {
        // Trois contrats en cours, dont un pressé pris la veille, pas encore retiré (la session commence au dernier tick du jour) : il est
        // perdu, il ne compte plus dans la limite.
        GuildContract rush = Guild.Contract("contract.presse_loups_du_soir");
        long day = Enumerable.Range(0, 200).First(d => GuildBoard.Of(Guild, 0, d).Contains(rush));
        List<GuildContract> normal = Guild.Contracts.Where(c => c.Rank == "g" && !c.Rush).Take(2).ToList();
        var active = new Dictionary<string, long> { [rush.Id] = 0, [normal[0].Id] = 0, [normal[1].Id] = 0 };
        WorldSession w = InGuild(Hero().WithStory(new SavedStory(guild: Card("g", active, day, new[] { rush.Id }))), Midnight(day) - 1);
        Assert.Equal(day + 1, w.Day);
        Assert.Equal(3, w.Narrative!.ActiveContracts.Count);   // pas encore retiré
        string next = w.Board().First(c => !active.ContainsKey(c.Id)).Id;
        Assert.Equal(GuildRefusal.None, w.CanTakeContract(next));
        Assert.Equal(GuildRefusal.None, w.TakeContract(next, out IReadOnlyList<StoryEvent> taken));
        Assert.Equal(new[] { $"ContractExpired {rush.Id}", $"ContractTaken {next}" }, taken.Select(e => e.ToString()));
        // Pour chaque contrat du tableau, la session (à portée de la maîtresse) rend le refus de la même vérification que la prise.
        foreach (GuildContract c in w.Board()) Assert.Equal(w.Narrative.CanTakeContract(c.Id, w.Day), w.CanTakeContract(c.Id));
        Assert.Equal(GuildRefusal.TooManyContracts, w.CanTakeContract(w.Board().First(c => w.Narrative.ActiveContracts.All(a => a.Contract != c.Id) && !w.Narrative.TakenOn(c.Id, w.Day)).Id));
    }

    // --- 4. Le loup blanc vient pour un contrat en cours -------------------------------------------------------------------

    [Fact]
    public void Le_contrat_du_loup_blanc_le_fait_venir()
    {
        var active = new Dictionary<string, long> { ["contract.loup_blanc"] = 0 };
        SavedCharacter hero = Hero().WithStory(new SavedStory(guild: Card("e", active)));
        ZoneDefinition z = Vallons.Value;
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), z, ShippedData, Progression, ItemsCatalog,
            ShippedStory.Value, guild: Guild);
        Assert.Single(w.Step().Population, e => e.Kind == PopulationEventKind.Sign);
        // Sans le contrat, à 8 h du jour 0 : rien.
        var plain = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithStory(new SavedStory(guild: Card("e")))), z,
            ShippedData, Progression, ItemsCatalog, ShippedStory.Value, guild: Guild);
        Assert.DoesNotContain(plain.Step().Population, e => e.Kind == PopulationEventKind.Sign);
    }
}
