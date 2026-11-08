using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.7, réponses de Damien du 7 novembre 2026 (questions 1 à 5 de la PR #116) : chaque marchand suit la réputation de sa faction
/// (marchands en schéma 4 : <c>faction</c>, <c>standing</c>), à l'achat comme à la vente, refuse au palier « haï » ; le marché noir des
/// bandits (Gaut le receleur) ; l'attaque perdue pendant l'absence (baisse plus faible) ; la garde qui ne parle plus à qui elle hait et le
/// chasse du bourg la nuit ; côté bandits, des quêtes en plus et un passage secret (point sûr caché).
/// </summary>
[Trait("Category", "World")]
public class V27ReponsesTests
{
    private const long Nuit = 130_000;   // 22 h 27 le premier jour

    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    private static SavedCharacter With(SavedCharacter hero, IReadOnlyDictionary<string, int> reputation, IEnumerable<string>? facts = null) =>
        hero.WithStory(new SavedStory(facts, reputation: reputation));

    private static WorldSession Open(double x, double z, SavedCharacter hero, long tick = 0)
    {
        ZoneDefinition zone = Zone(x, z);
        var session = new WorldSession(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    // --- Marchands en schéma 4 -----------------------------------------------------------------------------------------

    [Fact]
    public void Marchands_en_schema_4_avec_leur_faction_et_le_marche_noir()
    {
        Assert.Equal((4, 3, "0.8.0-provisoire"), (MerchantValidator.CurrentSchemaVersion, MerchantValidator.Schema3, ShippedMerchants.ContentVersion));
        Assert.Equal(new[] { ("merchant.armurier", "guard"), ("merchant.herboriste", "villagers"), ("merchant.epicier", "villagers"), ("merchant.guilde", "villagers"),
            ("merchant.renart", "villagers"), ("merchant.receleur", "bandits") }, ShippedMerchants.Merchants.Select(m => (m.Id, m.Faction)));
        Assert.Equal("liked", ShippedMerchants.Merchant("merchant.receleur").Standing);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "merchants.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(MerchantsPath)).RootElement));
        // Version 3 : sans faction ni palier, encore lue ; la faction y est un champ inconnu.
        JsonObject three = MerchantsJson();
        three["schemaVersion"] = 3;
        Assert.Contains(ReadMerchants(three).Report.Errors, e => e.Path == "$.data.merchants[0].faction" && e.Code == ValidationCode.InvalidValue);
        JsonArray list = three["data"]!["merchants"]!.AsArray();
        list.RemoveAt(list.Count - 1);
        foreach (JsonNode? m in list) m!.AsObject().Remove("faction");
        MerchantsLoadResult old = ReadMerchants(three);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.All(old.Book!.Merchants, m => Assert.Equal(("villagers", (string?)null), (m.Faction, m.Standing)));
    }

    [Theory]
    [InlineData("faction", "pirates", ValidationCode.UnknownReference)]
    [InlineData("standing", "roi", ValidationCode.UnknownReference)]
    public void Faction_ou_palier_de_marchand_refuse_au_chemin_precis(string field, string value, ValidationCode code)
    {
        JsonObject root = MerchantsJson();
        root["data"]!["merchants"]![1]![field] = value;
        Assert.Contains(ReadMerchants(root).Report.Errors, e => e.Path == $"$.data.merchants[1].{field}" && e.Code == code);
    }

    // --- Prix selon la faction, à l'achat et à la vente ------------------------------------------------------------------

    [Fact]
    public void L_armurier_suit_la_garde_et_les_herbes_suivent_le_village()
    {
        SavedCharacter hero = With(Hero(100_000), new Dictionary<string, int> { ["guard"] = 700, ["villagers"] = -150 });
        WorldSession w = At("merchant.herboriste", hero);
        Assert.Equal((80, 120, 110, 90), (w.PricePercentOf("merchant.armurier"), w.SellPercentOf("merchant.armurier"),
            w.PricePercentOf("merchant.herboriste"), w.SellPercentOf("merchant.herboriste")));
        // Jamais sous la valeur de base à l'achat, jamais au-dessus à la vente : acheter puis revendre ne rapporte toujours rien.
        foreach (MerchantOffer o in w.Stock("merchant.armurier"))
        {
            Assert.True(ShippedMerchants.TryGetValue(o.Item, out long value));
            Assert.True(o.Price >= value, o.Item);
            Assert.True(w.SellUnit("merchant.armurier", o.Item) <= value, o.Item);
        }
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(150, 105)]
    [InlineData(450, 110)]
    [InlineData(-150, 90)]
    [InlineData(-400, 80)]
    public void Prix_de_vente_selon_les_villageois(int villagers, int percent)
    {
        const string merchant = "merchant.herboriste", item = "item.peau_de_loup";
        long unit = At(merchant, Hero()).SellUnit(merchant, item);
        WorldSession w = At(merchant, With(Hero(0, new EdS.Data.Saves.ItemStack(item, 1)), new Dictionary<string, int> { ["villagers"] = villagers }));
        Assert.Equal(percent, w.SellPercentOf(merchant));
        ShippedMerchants.TryGetValue(item, out long value);
        long expected = Math.Min(value, WorldSession.ReputationPrice(unit, percent));
        Assert.Equal(expected, w.SellUnit(merchant, item));
        Assert.Equal(TradeRefusal.None, w.Sell(merchant, item, 1));
        Assert.Equal(expected, w.Money);
    }

    [Fact]
    public void Hai_du_village_le_marchand_refuse_et_rien_ne_change()
    {
        const string merchant = "merchant.herboriste";
        WorldSession w = At(merchant, With(Hero(100_000), new Dictionary<string, int> { ["villagers"] = -700 }));
        Assert.Equal(TradeRefusal.Shunned, w.CanTrade(merchant));
        string before = Snapshot(w);
        Assert.Equal(TradeRefusal.Shunned, w.Buy(merchant, "item.potion_de_soin"));
        Assert.Equal(before, Snapshot(w));
        // Hostile seulement : il vend, plus cher.
        WorldSession hostile = At(merchant, With(Hero(100_000), new Dictionary<string, int> { ["villagers"] = -400 }));
        Assert.Equal(TradeRefusal.None, hostile.CanTrade(merchant));
        Assert.Equal(120, hostile.PricePercentOf(merchant));
    }

    [Theory]
    [InlineData(0, TradeRefusal.NotYetOpen)]
    [InlineData(99, TradeRefusal.NotYetOpen)]
    [InlineData(100, TradeRefusal.None)]
    [InlineData(700, TradeRefusal.None)]
    public void Le_marche_noir_s_ouvre_a_qui_les_bandits_apprecient(int bandits, TradeRefusal expected)
    {
        WorldSession w = Open(69, -27.5, With(Hero(100_000), new Dictionary<string, int> { ["bandits"] = bandits }));
        Assert.Equal(expected, w.CanTrade("merchant.receleur"));
        if (expected != TradeRefusal.None) return;
        // Les remises du camp : ses prix suivent les bandits.
        Assert.Equal(bandits >= 600 ? 80 : 95, w.PricePercentOf("merchant.receleur"));
        Assert.Equal(TradeRefusal.None, w.Buy("merchant.receleur", "item.potion_de_soin"));
    }

    // --- Attaque perdue pendant l'absence ------------------------------------------------------------------------------------

    [Fact]
    public void Attaque_perdue_pendant_l_absence_une_baisse_plus_faible()
    {
        WorldSession w = Play(Zone(), Hero());
        w.Narrative!.EventSettled("attaque_hameau", won: false, missed: true);
        w.Narrative.EventSettled("faille_du_seuil", won: false, missed: true);   // sans règle d'absence : comme un échec
        w.Narrative.EventSettled("attaque_hameau", won: false);
        var events = new List<string>();
        for (int i = 0; i < 2; i++) events.AddRange(w.Step().Story.Where(e => e.Kind == StoryEventKind.ReputationChanged).Select(e => e.ToString()));
        Assert.Equal(new[] { "ReputationChanged villagers/eventMissed -5", "ReputationChanged rangers/eventMissed -10", "ReputationChanged villagers/eventLost -20" }, events);
        Assert.Equal(new[] { ("villagers", -5) }, Story.Value.Reputation.MissedOf("attaque_hameau").Select(c => (c.Faction, c.Value)));
    }

    // --- La garde qui hait ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-600, DialogueRefusal.Shunned)]
    [InlineData(-599, DialogueRefusal.None)]
    [InlineData(0, DialogueRefusal.None)]
    public void Haie_la_garde_ne_parle_plus(int guard, DialogueRefusal expected)
    {
        WorldSession w = Play(Zone(-2, -9.5), With(Hero(), new Dictionary<string, int> { ["guard"] = guard }));
        Assert.Equal(expected, w.Talk("npc.capitaine", out DialogueView? view));
        Assert.Equal(expected == DialogueRefusal.None, view != null);
        Assert.Equal(expected == DialogueRefusal.Shunned, w.Narrative!.IsShunnedBy("npc.archer_garde"));
        Assert.False(w.Narrative.IsShunnedBy("npc.boulanger"));   // le village ne tourne pas le dos (seule la garde, Shun)
        Assert.DoesNotContain("fact.met.capitaine", w.Narrative.Facts.Where(_ => expected == DialogueRefusal.Shunned));
    }

    [Theory]
    [InlineData(-58, -6, Nuit, -700, "place_du_village")]   // au bourg, la nuit, haï de la garde
    [InlineData(-58, -6, 0, -700, null)]                    // le jour
    [InlineData(-18, -12, Nuit, -700, null)]                // au village
    [InlineData(-58, -6, Nuit, -500, null)]                 // hostile seulement
    public void Couvre_feu_du_bourg(double x, double z, long tick, int guard, string? expected)
    {
        WorldSession w = Open(x, z, With(Hero(), new Dictionary<string, int> { ["guard"] = guard }), tick);
        Assert.Equal(expected, w.CurfewSafePoint);
    }

    [Fact]
    public void Garde_mefiante_quand_les_bandits_t_apprecient()
    {
        DialogueNode accueil = Story.Value.Dialogue("dialogue.archer_garde").Nodes.Single(n => n.Id == "accueil");
        Assert.Contains(accueil.Lines, l => l.Text == "text.v27.archer_garde.bandits.liked"
            && l.Conditions.Any(c => c.Kind == ConditionKind.StandingAtLeast && c.Id == "bandits" && c.Step == "liked"));
        Assert.Contains(Story.Value.Dialogue("dialogue.thibaut").Nodes.SelectMany(n => n.Lines), l => l.Text == "text.v27.thibaut.bandits.liked");
        Assert.Equal(new[] { "guard" }, Story.Value.Reputation.Shun);
        Assert.Equal(("guard", "bourg", "place_du_village"), (Story.Value.Reputation.Curfew!.Faction, Story.Value.Reputation.Curfew.Place, Story.Value.Reputation.Curfew.SafePoint));
        Assert.Equal(new[] { "npc.archer_garde", "npc.capitaine", "npc.thibaut" }, Story.Value.Npcs.Where(n => n.Faction == "guard").Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal));
    }

    // --- Remarques de Codex (tour 2) et décisions de Damien du 8 novembre 2026 --------------------------------------------------

    [Fact]
    public void Le_sentier_des_bandits_s_atteint_par_le_recel()
    {
        // Sans geste répétable, les bandits plafonnent : Colin +50, le mot de passe +60, le remède +40 = 150 (apprécié), sous « honoré » (300).
        StoryBook book = Story.Value;
        long once = book.Quest("quest.v27.mot_de_passe").Effects.Concat(book.Quest("quest.v27.remede_du_camp").Effects)
            .Where(e => e.Kind == EffectKind.Reputation && e.Id == "bandits").Sum(e => e.Value) + 50;
        Assert.Equal(150, once);
        Assert.Equal(new[] { ("guard", -1), ("bandits", 2) }, book.Reputation.Act(Reputations.Fence).Select(c => (c.Faction, c.Value)));
        // Le recel : chaque objet acheté au marché noir rapproche du camp ; 75 potions de soin mènent de 150 à 300.
        var facts = new[] { "fact.ch4.deserteur.aide", "fact.ch4.deserteur.decide", "fact.met.deserteur", "fact.met.receleur" };
        var done = new Dictionary<string, QuestProgress>
        {
            ["quest.deserteur"] = new QuestProgress(QuestProgress.Done), ["quest.v27.mot_de_passe"] = new QuestProgress(QuestProgress.Done),
            ["quest.v27.remede_du_camp"] = new QuestProgress(QuestProgress.Done),
        };
        SavedCharacter hero = Hero(100_000).WithStory(new SavedStory(facts, done, reputation: new Dictionary<string, int> { ["bandits"] = (int)once }));
        WorldSession w = Open(69, -27.5, hero);
        Assert.Equal(TradeRefusal.None, w.Buy("merchant.receleur", "item.potion_de_soin", 75));
        List<string> events = w.Step().Story.Select(e => e.ToString()).ToList();
        Assert.Contains("ReputationChanged bandits/fence 150", events);
        Assert.Contains("ReputationChanged guard/fence -75", events);
        Assert.Contains("StandingChanged bandits/honored 1", events);
        Assert.Equal(("honored", 300), (w.Narrative!.Standing("bandits"), w.Narrative.Reputation("bandits")));
        // Un achat ailleurs ne compte pas.
        WorldSession bourg = At("merchant.herboriste", hero);
        Assert.Equal(TradeRefusal.None, bourg.Buy("merchant.herboriste", "item.potion_de_soin"));
        Assert.DoesNotContain(bourg.Step().Story, e => e.Kind == StoryEventKind.ReputationChanged);
        // Colin, à 300 : l'offre du sentier.
        SavedCharacter honored = Hero().WithStory(new SavedStory(facts, done, reputation: new Dictionary<string, int> { ["bandits"] = 300 }));
        WorldSession colin = Open(66.5, -30, honored);
        Assert.Equal(DialogueRefusal.None, colin.Talk("npc.deserteur", out DialogueView? offer));
        Assert.Equal("v27_deserteur_sentier_offre", offer!.Node);
    }

    [Fact]
    public void Une_offre_remise_a_plus_tard_se_reprend_apres_la_tour()
    {
        // Après la tour silencieuse, l'entrée « quête finie » passe avant l'accueil : la réponse de reprise y est aussi.
        SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { "fact.v27.capitaine.plus_tard", "fact.met.capitaine" },
            new Dictionary<string, QuestProgress> { ["quest.tour_silencieuse"] = new QuestProgress(QuestProgress.Done) },
            reputation: new Dictionary<string, int> { ["guard"] = 150 }));
        WorldSession w = Play(Zone(-2, -9.5), hero);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.capitaine", out DialogueView? view));
        Assert.Equal("ch4_tour_apres", view!.Node);
        DialogueOption again = Assert.Single(view.Options, o => o.Text == "text.v27.capitaine.encore");
        Assert.Equal(DialogueRefusal.None, w.Choose(again.Index, out DialogueView? offer, out _));
        Assert.Equal("v27_capitaine_offre", offer!.Node);
        // Chaque offre remise à plus tard a sa reprise depuis toutes les entrées qui peuvent passer après elle.
        foreach (Dialogue d in Story.Value.Dialogues)
            for (int i = 0; i < d.Entries.Count; i++)
                if (d.Entries[i].Node.EndsWith("_offre", StringComparison.Ordinal) && d.Entries[i].Node.StartsWith("v27_", StringComparison.Ordinal))
                {
                    string key = "text.v27." + d.Entries[i].Node[4..^6] + ".encore";
                    foreach (DialogueEntry later in d.Entries.Skip(i + 1).Where(e => !e.Node.StartsWith("v27_", StringComparison.Ordinal)))
                        Assert.True(d.Node(later.Node).Choices.Any(c => c.Text == key), $"{d.Id} / {later.Node} : pas de reprise {key}");
                }
    }

    [Fact]
    public void Haie_la_garde_parle_encore_pour_une_quete_commencee_ou_principale()
    {
        var hated = new Dictionary<string, int> { ["guard"] = -800 };
        // Bérenger : la tour silencieuse en cours (son rapport) : il parle, froidement.
        SavedCharacter tour = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { ["quest.tour_silencieuse"] = new QuestProgress("rapport") },
            reputation: hated));
        WorldSession w = Play(Zone(-2, -9.5), tour);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.capitaine", out DialogueView? view));
        Assert.Equal("ch4_tour_rapport", view!.Node);
        Assert.True(w.Narrative!.LastTalkCold);
        Assert.Equal("{0}, froidement : « Je fais ça pour le bourg, pas pour toi. »", Texts.Get("text.ui.shunned.story"));
        // Thibaut : la quête principale du chapitre 4 (la lettre de Rosalie).
        SavedCharacter lettre = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { ["quest.tour_et_camp"] = new QuestProgress("lettre") },
            reputation: hated));
        Assert.True(Play(Zone(), lettre).Narrative!.SpeaksForQuest("npc.thibaut"));
        // Rien en cours : il se tait ; il ne propose pas de quête secondaire nouvelle.
        WorldSession idle = Play(Zone(-2, -9.5), With(Hero(), hated));
        Assert.Equal(DialogueRefusal.Shunned, idle.Talk("npc.capitaine", out _));
        Assert.False(idle.Narrative!.LastTalkCold);
    }

    /// <summary>Où se tenir, à 8 h, près de chaque PNJ de la garde (à 1,5 m de sa place, hors de tout obstacle).</summary>
    private static readonly IReadOnlyDictionary<string, (double X, double Z)> GuardSpots = new Dictionary<string, (double X, double Z)>
    {
        ["npc.capitaine"] = (-2, -9.5), ["npc.archer_garde"] = (1, -1.5), ["npc.thibaut"] = (118, 54.5),
    };

    /// <summary>
    /// Remarque de Codex (tour 4) : chaque étape de quête principale qui passe par un PNJ d'une faction qui ne parle plus (la garde) : parler
    /// à lui, lui livrer, ou une entrée de son dialogue à cette étape (un choix à faire). Tirées des données, pas de la règle testée.
    /// </summary>
    public static IEnumerable<object[]> MainStepsWithShunningNpcs()
    {
        StoryBook book = Book(Zone());
        foreach (Quest q in book.Quests.Where(q => q.Category == QuestCategory.Main))
            foreach (QuestStep s in q.Steps)
                foreach (Npc n in book.Npcs.Where(n => n.Faction != null && book.Reputation.Shun.Contains(n.Faction)))
                {
                    Objective o = s.Objective;
                    bool direct = (o.Kind == ObjectiveKind.Talk && o.Id == n.Id) || (o.Kind == ObjectiveKind.Deliver && o.Npc == n.Id);
                    bool entry = book.Dialogue(n.Dialogue).Entries.Any(e => e.Conditions.Any(c => c.Kind == ConditionKind.QuestAt && c.Id == q.Id && c.Step == s.Id));
                    if (direct || entry) yield return new object[] { q.Id, s.Id, n.Id };
                }
    }

    [Fact]
    public void Les_etapes_principales_passent_par_la_garde()
    {
        // Le chapitre 4 passe par Thibaut (le choix du sort de Mâchefer, la lettre de Rosalie) : la règle a bien des cas à couvrir.
        List<object[]> cases = MainStepsWithShunningNpcs().ToList();
        Assert.Contains(cases, c => (string)c[0] == "quest.tour_et_camp" && (string)c[1] == "choix" && (string)c[2] == "npc.thibaut");
        Assert.Contains(cases, c => (string)c[0] == "quest.tour_et_camp" && (string)c[1] == "lettre" && (string)c[2] == "npc.thibaut");
    }

    [Theory]
    [MemberData(nameof(MainStepsWithShunningNpcs))]
    public void Haie_la_garde_la_quete_principale_avance_quand_meme(string quest, string step, string npc)
    {
        StoryBook book = Story.Value;
        QuestStep s = book.Quest(quest).Steps.Single(x => x.Id == step);
        Objective o = s.Objective;
        // La garde hait le personnage ; les objets à livrer sont dans le sac.
        var items = o.Kind == ObjectiveKind.Deliver ? new[] { new ItemStack(o.Id, (int)o.Value) } : Array.Empty<ItemStack>();
        SavedCharacter hero = Hero(0, items).WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { [quest] = new QuestProgress(step) },
            reputation: new Dictionary<string, int> { ["guard"] = Reputations.Min }));
        (double x, double z) = GuardSpots[npc];
        WorldSession w = Open(x, z, hero);
        Assert.Equal("hated", w.Narrative!.Standing("guard"));
        // Il parle (froidement) : le dialogue s'ouvre.
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.True(w.Narrative.LastTalkCold);
        // Et la quête avance : livrer, parler, ou faire le choix qui remplit l'objectif.
        if (o.Kind == ObjectiveKind.Deliver)
        {
            DialogueOption delivery = Assert.Single(view!.Options, d => d.Item == o.Id);
            Assert.Equal(DialogueRefusal.None, w.Choose(delivery.Index, out _, out _));
        }
        else if (o.Kind == ObjectiveKind.Fact)
        {
            DialogueOption choice = view!.Options.First(d => d.Item is null
                && w.Narrative.Book.Dialogue(w.Narrative.Book.Npc(npc).Dialogue).Node(view.Node).Choices.Any(c => c.Text == d.Text && c.Effects.Any(e => e.Kind == EffectKind.SetFact && e.Id == o.Id)));
            Assert.Equal(DialogueRefusal.None, w.Choose(choice.Index, out _, out _));
        }
        for (int i = 0; i < 3; i++) w.Step();
        Assert.NotEqual(step, w.Narrative.CurrentStep(quest)?.Id);
    }

    // --- Côté bandits : quêtes en plus et passage secret ---------------------------------------------------------------------

    [Fact]
    public void Quetes_des_bandits_et_le_sentier_qui_revele_la_planque()
    {
        StoryBook book = Story.Value;
        Quest remede = book.Quest("quest.v27.remede_du_camp"), sentier = book.Quest("quest.v27.sentier_des_contrebandiers");
        Assert.Contains(remede.Requires, c => c.Kind == ConditionKind.StandingAtLeast && c.Id == "bandits" && c.Step == "liked");
        Assert.Contains(sentier.Requires, c => c.Kind == ConditionKind.StandingAtLeast && c.Id == "bandits" && c.Step == "honored");
        Assert.Contains(sentier.Effects, e => e.Kind == EffectKind.SetFact && e.Id == "fact.v27.sentier_revele");
        Assert.Equal(new[] { ("planque_des_contrebandiers", "fact.v27.sentier_revele") }, book.Reputation.Hidden);
    }

    [Fact]
    public void La_planque_ne_se_decouvre_qu_avec_le_sentier()
    {
        ZoneDefinition vallons = Zone();
        // Sur place, sans le fait : rien.
        WorldSession blind = Open(84, 58, Hero());
        for (int i = 0; i < 3; i++) blind.Step();
        Assert.False(blind.IsDiscovered(vallons, "planque_des_contrebandiers"));
        // Avec le fait, de loin : découverte, et le voyage rapide y mène.
        WorldSession told = Open(-18, -12, With(Hero(), new Dictionary<string, int>(), new[] { "fact.v27.sentier_revele" }));
        Assert.True(told.IsDiscovered(vallons, "planque_des_contrebandiers"));
        Assert.Equal(TravelRefusal.None, told.CanTravel(vallons, "planque_des_contrebandiers"));
    }

    [Fact]
    public void Pas_de_halte_ni_de_retour_a_la_planque_avant_le_sentier()
    {
        // Remarque de Codex (tour 3) : à côté de la planque sans le fait, aucune halte (sinon « se reposer » rouvrirait la partie au point
        // sûr précédent) ; avec le fait, la planque est un point sûr comme un autre. Même filtre : carte, voyage, retour (IsSecret).
        var near = new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(85), EdS.Domain.Numerics.Fixed.FromInt(59));
        WorldSession blind = Open(84, 52, Hero());
        Assert.True(blind.IsSecret("planque_des_contrebandiers"));
        Assert.Null(blind.RestPointNear(near, EdS.Runtime.AdventureSession.RestRange));
        Assert.Equal(TravelRefusal.NotDiscovered, blind.CanTravel(Zone(), "planque_des_contrebandiers"));
        WorldSession told = Open(84, 52, With(Hero(), new Dictionary<string, int>(), new[] { "fact.v27.sentier_revele" }));
        Assert.False(told.IsSecret("planque_des_contrebandiers"));
        Assert.Equal("planque_des_contrebandiers", told.RestPointNear(near, EdS.Runtime.AdventureSession.RestRange)!.Id);
        // Les autres points sûrs ne changent pas (le point d'entrée du test est à la place du village, déplacée à 84, 52).
        Assert.Equal("place_du_village", blind.RestPointNear(new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(84), EdS.Domain.Numerics.Fixed.FromInt(52)), EdS.Runtime.AdventureSession.RestRange)!.Id);
    }

    // --- Validation des nouvelles règles ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("shun", "[\"pirates\"]", "$.data.reputation.shun[0]", ValidationCode.UnknownReference)]
    [InlineData("shun", "[\"guard\", \"guard\"]", "$.data.reputation.shun[1]", ValidationCode.DuplicateId)]
    [InlineData("curfew", "{\"faction\": \"guard\", \"place\": \"lune\", \"phases\": [\"night\"], \"safePoint\": \"place_du_village\"}", "$.data.reputation.curfew.place", ValidationCode.UnknownReference)]
    [InlineData("curfew", "{\"faction\": \"guard\", \"place\": \"bourg\", \"phases\": [\"minuit\"], \"safePoint\": \"place_du_village\"}", "$.data.reputation.curfew.phases[0]", ValidationCode.UnknownReference)]
    [InlineData("curfew", "{\"faction\": \"guard\", \"place\": \"bourg\", \"phases\": [], \"safePoint\": \"place_du_village\"}", "$.data.reputation.curfew.phases", ValidationCode.MissingField)]
    [InlineData("curfew", "{\"faction\": \"guard\", \"place\": \"bourg\", \"phases\": [\"night\"], \"safePoint\": \"lune\"}", "$.data.reputation.curfew.safePoint", ValidationCode.UnknownReference)]
    [InlineData("hidden", "[{\"safePoint\": \"lune\", \"fact\": \"fact.v27.sentier_revele\"}]", "$.data.reputation.hidden[0].safePoint", ValidationCode.UnknownReference)]
    [InlineData("hidden", "[{\"safePoint\": \"place_du_village\", \"fact\": \"fact.v27.sentier_revele\"}]", "$.data.reputation.hidden[0].safePoint", ValidationCode.InvalidValue)]
    [InlineData("hidden", "[{\"safePoint\": \"planque_des_contrebandiers\", \"fact\": \"fact.jamais.appris\"}]", "$.data.reputation.hidden[0].fact", ValidationCode.UnknownReference)]
    [InlineData("sellPrices", "{\"hated\": 70}", "$.data.reputation.sellPrices.hero", ValidationCode.MissingField)]
    [InlineData("events", "{\"attaque_hameau\": {\"missed\": {\"villagers\": 0}}}", "$.data.reputation.events.attaque_hameau.missed.villagers", ValidationCode.OutOfRange)]
    public void Nouvelles_regles_refusees_au_chemin_precis(string field, string json, string path, ValidationCode code)
    {
        JsonObject root = StoryJson();
        root["data"]!["reputation"]![field] = JsonNode.Parse(json);
        Assert.Contains(Read(root).Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Faction_de_pnj_inconnue_refusee()
    {
        JsonObject root = StoryJson();
        root["data"]!["npcs"]![0]!["faction"] = "pirates";
        Assert.Contains(Read(root).Report.Errors, e => e.Path == "$.data.npcs[0].faction" && e.Code == ValidationCode.UnknownReference);
    }
}
