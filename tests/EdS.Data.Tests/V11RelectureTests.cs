using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.1, relecture indépendante de la PR #72 (premier tour) : chaque remarque prouvée a son test de non-régression. Découverte du
/// point de départ après l'arrivée du chef (registre), voyage refusé vers le point sûr où l'on est, boutique fermée qui laisse parler au
/// marchand, bourse pleine qui a son propre message, vente de la part libre d'une pile en partie emportée au combat, boutique fermée à
/// chaque nouvelle session, stock d'un jour figé, bourse bornée dès la construction du personnage.
/// </summary>
[Trait("Category", "World")]
public class V11RelectureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v11_relecture_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    // --- Remarque 1 : découverte du point de départ et registre ------------------------------------------------------------

    /// <summary>
    /// Sonde de la relecture, devenue test : le point sûr de départ est découvert après l'arrivée du chef, dont la première forme compte
    /// alors parmi les combattants. Avec juste la place des morts possibles d'un tick, il n'est pas découvert et le premier tick passe ;
    /// avec une place de plus, il l'est et le premier tick passe aussi. Avant la correction, 29 places libres (autant que les combattants
    /// avec le chef) faisaient découvrir le point par le constructeur, puis le premier tick levait « Registre des événements presque plein ».
    /// </summary>
    [Fact]
    public void Decouverte_du_depart_laisse_au_chef_sa_place_au_registre()
    {
        ZoneDefinition zone = Zone();
        Assert.NotNull(zone.Boss);
        var fresh = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        int combatants = fresh.Zone.Engine.CombatantIds.Count;   // chef compris (première forme)
        Assert.Contains(fresh.BossForm!, fresh.Zone.Engine.CombatantIds);
        string safeKey = zone.SafePointKey(zone.Entry.Id);
        for (int room = combatants - 2; room <= combatants + 2; room++)
            foreach (bool known in new[] { false, true })
            {
                int n = SaveValidator.MaxConsumed - room;
                IEnumerable<string> keys = Enumerable.Range(0, known ? n - 1 : n).Select(i => $"zone.vallons/test/k{i}");
                if (known) keys = keys.Append(safeKey);
                var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, keys.ToArray(), Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
                string label = $"place {room}, déjà découvert : {known}";
                // Découvert à l'ouverture seulement s'il reste, après sa clé, la place des morts possibles d'un tick (chef compris).
                Assert.True(w.IsDiscovered(zone, zone.Entry.Id) == (known || room > combatants), label);
                Assert.Equal(known || room <= combatants ? room : room - 1, w.Zone.RegistryRoom);
                if (room >= combatants) w.Step();   // ne lève plus : la place du chef est gardée
                else Assert.Throws<InvalidOperationException>(() => w.Step());   // registre déjà trop plein sans la découverte (inchangé)
            }
    }

    // --- Remarque 2 : le voyage n'est pas une halte gratuite -------------------------------------------------------------------

    [Fact]
    public void Voyage_vers_le_point_ou_l_on_est_refuse()
    {
        // Dans le cœur : la session commence à la place du Bourg ; le voyage vers elle est refusé, vers le village il reste possible.
        ZoneDefinition zone = Zone();
        string[] both = { zone.SafePointKey("place_du_village"), zone.SafePointKey("place_du_bourg") };
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, both, Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone),
            safePoint: "place_du_bourg");
        w.Step();
        Assert.False(w.InCombat);
        Assert.True(w.IsAt(zone, "place_du_bourg"));
        Assert.False(w.IsAt(zone, "place_du_village"));
        Assert.False(w.IsAt(zone, "nulle_part"));
        Assert.Equal(TravelRefusal.AlreadyHere, w.CanTravel(zone, "place_du_bourg"));
        Assert.Equal(TravelRefusal.None, w.CanTravel(zone, "place_du_village"));

        // Dans l'aventure : au point d'entrée, voyager vers lui ne fait rien (ni session, ni PV), un message dit pourquoi.
        AdventureSession a = Adventure(SavePath);
        a.DrainMessages();
        TravelDestination here = Assert.Single(a.TravelDestinations());
        Assert.True(here.Here);
        Assert.Equal(TravelRefusal.AlreadyHere, a.World.CanTravel(a.World.Definition!, "place_du_village"));
        Assert.Equal(TravelRefusal.AlreadyHere, a.CanTravel("zone.vallons", "place_du_village"));
        int opened = a.Opened;
        Assert.Equal(TravelRefusal.AlreadyHere, a.Travel("zone.vallons", "place_du_village"));
        Assert.Equal((opened, 0), (a.Opened, a.Travels));
        Assert.Equal("Tu es déjà à ce point sûr : fais plutôt une halte.", Assert.Single(a.DrainMessages()));
        Assert.False(File.Exists(SavePath));   // rien d'écrit
    }

    // --- Remarques 3 et 6 : boutique fermée, boutique d'une ancienne session ---------------------------------------------------

    [Fact]
    public void Boutique_fermee_le_marchand_parle_quand_meme()
    {
        (string data, string scenarios) = DataCopy((-65.2, -6.4), merchants: root => Merchant(root, "merchant.herboriste")["closed"] = new JsonArray("day"));
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(500)));
        AdventureSession a = Adventure(SavePath, data, scenarios);
        a.DrainMessages();
        Assert.Equal("npc.herboriste", a.NpcInReach!.Id);
        Assert.Equal(TradeRefusal.Closed, a.World.CanTrade("merchant.herboriste"));
        // F : la boutique est fermée (message), le dialogue de l'histoire s'ouvre à la place.
        Assert.True(a.Interact());
        Assert.Null(a.Shop);
        Assert.Equal("Sauge, l'herboriste", a.DialogueNpc);
        Assert.Equal("Boutique fermée à cette heure.", a.DrainMessages().First());
        a.CloseDialogue();
        Assert.False(a.OpenShop("merchant.herboriste"));
        Assert.Null(a.Shop);
    }

    [Fact]
    public void Nouvelle_session_ferme_la_boutique()
    {
        (string data, string scenarios) = DataCopy((-65.2, -6.4));
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(500)));
        AdventureSession a = Adventure(SavePath, data, scenarios);
        Assert.True(a.Interact());
        Assert.Equal("merchant.herboriste", a.Shop);
        // Halte au point sûr d'entrée (à côté de l'herboriste) : nouvelle session (même chemin que la reprise), boutique fermée. Porte et
        // voyage passent par le même endroit (toute session mise en jeu ferme la boutique de la précédente).
        Assert.True(a.CanRest);
        int opened = a.Opened;
        Assert.True(a.Rest());
        Assert.Equal(opened + 1, a.Opened);
        Assert.Null(a.Shop);
        Assert.Equal(TradeRefusal.TooFar, a.Buy("item.potion_de_soin"));   // plus de boutique ouverte
        a.Advance(0.05);   // le personnage arrive dans la nouvelle session
        Assert.True(a.OpenShop("merchant.herboriste"));
    }

    // --- Remarque 4 : bourse pleine, pas sac plein -------------------------------------------------------------------------------

    [Fact]
    public void Bourse_pleine_a_son_propre_message()
    {
        // Récompense de quête qui attend : la bourse déborderait (500 pièces de la mission).
        var story = new SavedStory(new[] { "fact.odile.retour" }, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("retour") });
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(Coins.Max - 499).WithStory(story)));
        AdventureSession a = Adventure(SavePath);
        a.Advance(0.2);
        string[] said = a.DrainMessages().ToArray();
        Assert.Contains("Bourse pleine : dépense des pièces pour recevoir ta récompense.", said);
        Assert.DoesNotContain(said, m => m.StartsWith("Inventaire plein", StringComparison.Ordinal));
        Assert.Equal(Coins.Max - 499, a.Money);

        // Réponse qui donne des pièces : refusée, avec le message de la bourse.
        (string data, string scenarios) = DataCopy((27, -15), story: root =>
        {
            JsonObject merci = root["data"]!["dialogues"]!.AsArray().Single(d => (string)d!["id"]! == "dialogue.gaspard")!["nodes"]![1]!["choices"]![0]!.AsObject();
            merci["effects"]!.AsArray().Add(new JsonObject { ["do"] = "giveMoney", ["value"] = 25 });
        });
        string other = Path.Combine(_folder, "autre", "sauvegarde.json");
        var rodeur = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("rodeur") });
        SaveStore.Write(other, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(Coins.Max - 24).WithStory(rodeur)));
        AdventureSession b = Adventure(other, data, scenarios);
        Assert.Equal("npc.gaspard", b.NpcInReach!.Id);
        Assert.True(b.Interact());
        b.DrainMessages();
        Assert.Equal(DialogueRefusal.PurseFull, b.Choose(0));
        Assert.Equal("Ta bourse est pleine : ces pièces n'y tiendraient pas.", Assert.Single(b.DrainMessages()));
        Assert.Equal(Coins.Max - 24, b.Money);
    }

    // --- Remarque 5 : vendre la part libre d'une pile en partie emportée au combat -------------------------------------------------

    [Fact]
    public void Vendre_la_part_libre_d_une_pile_reservee()
    {
        (string data, string scenarios) = DataCopy((-65.2, -6.4));
        // 8 potions en deux piles : une pile (5) est emportée au combat à l'entrée, 3 restent à vendre.
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(100, new ItemStack("item.potion_de_soin", 5),
            new ItemStack("item.peau_de_loup", 3), new ItemStack("item.potion_de_soin", 3))));
        AdventureSession a = Adventure(SavePath, data, scenarios);
        Inventory bag = a.World.Inventory;
        Assert.Equal((8, 5, 3), (bag.Count("item.potion_de_soin"), bag.Reserved("item.potion_de_soin"), bag.Available("item.potion_de_soin")));
        Assert.True(a.Interact());
        a.DrainMessages();

        // Clic droit sur la première pile (5, toutes « réservées » à l'œil) : la part libre (3) se vend, retirée des dernières places.
        Assert.Equal(TradeRefusal.None, a.SellPlace(0));
        Assert.Equal("Vendu : Potion de soin ×3 pour 30 cuivre.", Assert.Single(a.DrainMessages()));
        Assert.Equal(new[] { "item.potion_de_soin x5", "item.peau_de_loup x3" }, bag.Stacks.Select(s => $"{s.Item} x{s.Count}"));
        Assert.Equal((5, 5, 0, 130L), (bag.Count("item.potion_de_soin"), bag.Reserved("item.potion_de_soin"), bag.Available("item.potion_de_soin"), a.Money));
        Assert.Equal(("item.potion_de_soin", 3, 30L), (a.Market.Buyback[0].Item, a.Market.Buyback[0].Count, a.Market.Buyback[0].Paid));

        // Plus rien de libre : refus, rien ne change (clic et clic droit).
        string before = Snapshot(a.World);
        Assert.Equal(TradeRefusal.Reserved, a.SellPlace(0));
        Assert.Equal(TradeRefusal.Reserved, a.SellPlace(0, 1));
        Assert.Equal(before, Snapshot(a.World));
        Assert.All(a.DrainMessages(), m => Assert.StartsWith("Objet de soin emporté au combat", m, StringComparison.Ordinal));

        // Une potion achetée maintenant n'est pas emportée : elle se vend, même par un clic sur la pile réservée.
        Assert.Equal(TradeRefusal.None, a.Buy("item.potion_de_soin"));
        Assert.Equal(new[] { "item.potion_de_soin x5", "item.peau_de_loup x3", "item.potion_de_soin x1" }, bag.Stacks.Select(s => $"{s.Item} x{s.Count}"));
        Assert.Equal(TradeRefusal.None, a.SellPlace(0));   // toute la pile demandée : seule la potion libre part
        Assert.Equal(new[] { "item.potion_de_soin x5", "item.peau_de_loup x3" }, bag.Stacks.Select(s => $"{s.Item} x{s.Count}"));
        Assert.Equal(5, bag.Reserved("item.potion_de_soin"));

        // Quantités et places impossibles : refus sans rien changer.
        before = Snapshot(a.World);
        Assert.Equal(TradeRefusal.InvalidCount, a.SellPlace(1, 0));
        Assert.Equal(TradeRefusal.NotEnough, a.SellPlace(7));
        Assert.Equal(TradeRefusal.NotEnough, a.SellPlace(-1));
        Assert.Equal(before, Snapshot(a.World));
        // Une pile libre se vend toujours en entier, et pas plus que la pile (2 demandées sur 3 : 2).
        Assert.Equal(TradeRefusal.None, a.SellPlace(1, 2));
        Assert.Equal(1, bag.Count("item.peau_de_loup"));
        Assert.Equal(TradeRefusal.None, a.SellPlace(1, 50));
        Assert.Equal(0, bag.Count("item.peau_de_loup"));
    }

    // --- Remarque 8 : stock d'un jour figé, bourse bornée à la construction -----------------------------------------------------

    [Fact]
    public void Stock_d_un_jour_fige()
    {
        // Le tirage du stock est un contrat (Pcg32 graine FNV-1a de la clé du jour) : ces jours-là, ces offres, pour de bon.
        var market = new Market(ShippedMerchants);
        Assert.Equal(new[] { "item.epee_du_milicien", "item.ecu_de_chene", "item.gambison", "item.rondache_cerclee", "item.marteau_du_carrier" },
            market.Stock("merchant.armurier", 0).Select(o => o.Item));
        Assert.Equal(new[] { "item.epee_du_milicien", "item.ecu_de_chene", "item.gambison", "item.pavois_du_moulin", "item.cotte_de_mailles" },
            market.Stock("merchant.armurier", 3).Select(o => o.Item));
        Assert.Equal(new[] { "item.epee_du_milicien", "item.ecu_de_chene", "item.gambison", "item.epee_de_garde", "item.cotte_de_mailles" },
            market.Stock("merchant.armurier", 4).Select(o => o.Item));
        Assert.Equal(new[] { "item.potion_de_soin", "item.herbe_de_l_aube", "item.remede_de_l_aube" }, market.Stock("merchant.herboriste", 0).Select(o => o.Item));
        Assert.Equal(new[] { "item.potion_de_soin", "item.herbe_de_l_aube", "item.eclat_de_seuil" }, market.Stock("merchant.herboriste", 1).Select(o => o.Item));
    }

    [Fact]
    public void Bourse_bornee_des_la_construction()
    {
        foreach (long money in new[] { -1L, Coins.Max + 1, long.MaxValue, long.MinValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SavedCharacter("bastion", "class.bastion", 0, money: money));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hero().WithMoney(money));
        }
        Assert.Equal((0L, Coins.Max), (Hero(0).Money, Hero(Coins.Max).Money));
        // Une partie ne peut donc pas porter une bourse hors bornes : son affichage ne lève jamais.
        AdventureSession a = Adventure(SavePath);
        Assert.Equal("0 cuivre", a.Purse);
    }

    // --- Outils ------------------------------------------------------------------------------------------------------------------

    private static JsonObject Merchant(JsonObject root, string id) =>
        root["data"]!["merchants"]!.AsArray().Single(m => (string)m!["id"]! == id)!.AsObject();

    /// <summary>Copie des données et scénarios livrés, le point d'entrée déplacé en <paramref name="entry"/>, retouches éventuelles.</summary>
    private (string Data, string Scenarios) DataCopy((double X, double Z) entry, Action<JsonObject>? merchants = null, Action<JsonObject>? story = null)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(ScenariosDirectory, scenarios);
        Edit(ZoneJsonReader.PathIn(data, "zone.vallons"), root => root["data"]!["safePoints"]![0]!["position"] = new JsonArray(entry.X, entry.Z));
        if (merchants != null) Edit(MerchantJsonReader.PathIn(data), merchants);
        if (story != null) Edit(StoryJsonReader.StoryPathIn(data), story);
        return (data, scenarios);
    }

    private static void Edit(string path, Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(root);
        File.WriteAllText(path, root.ToJsonString());
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
