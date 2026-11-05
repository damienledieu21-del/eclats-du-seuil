using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
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
/// Sous-lot V1.9.3b, suites du chapitre 3 (réponses de Damien du 5 novembre 2026, docs/MONDE.md) : l'objectif de quête
/// <c>craftQuality</c> (histoire en schéma 5) et la fin de « L'apprentie de Maëlle » par une épée de garde de qualité supérieure ; l'étal
/// de Renart ouvert après son aveu (marchands en schéma 3, champ <c>fact</c>) ; la lettre gardée montrée à Sœur Albe. Formats : lecture,
/// refus avec chemin précis, anciennes versions encore lues ; pilotes de bout en bout (partie sauvegardée, relue et rouverte à chaque
/// déplacement), anciennes parties qui ne se bloquent pas.
/// </summary>
[Trait("Category", "World")]
public class V193bSuitesTests
{
    private const string Mission = "quest.traces_sous_le_moulin", Bourg = "quest.bourg_d_aube", Hameau = "quest.champs_et_hameau",
        Apprentie = "quest.apprentie_de_maelle", Lettre = "quest.lettre_au_fond_du_lac", Aveu = "fact.ch2.renart.aveu";

    private static readonly (double X, double Z) Renart = (-47.5, -3.3), Forge = (-19, -5), Colombe = (-22, -4.8), Albe = (3.2, 3.2);

    private static readonly string[] AfterChapter2 = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree",
        Aveu, "fact.ch2.vers_les_champs" };

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));
    private static readonly Lazy<GuildBook> Guilde = new(() =>
    {
        GuildLoadResult r = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, Story.Value, Vallons.Value,
            new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList(), ItemsCatalog, ShippedMerchants);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    // --- Histoire en schéma 5 : objectif craftQuality --------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult ReadStory(JsonObject root) =>
        StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons.Value, ItemsCatalog, V16TestData.Interiors);

    private static int ApprentieIndex(JsonObject root) =>
        root["data"]!["quests"]!.AsArray().Select((q, i) => (q, i)).Single(x => x.q!["id"]!.GetValue<string>() == Apprentie).i;

    /// <summary>L'objectif de la dernière étape (l'épée) de l'apprentie, et son chemin dans le fichier.</summary>
    private static (JsonObject Objective, string Path) EpeeObjective(JsonObject root)
    {
        int q = ApprentieIndex(root);
        JsonArray steps = root["data"]!["quests"]![q]!["steps"]!.AsArray();
        int s = steps.Count - 1;
        Assert.Equal("epee", steps[s]!["id"]!.GetValue<string>());
        return (steps[s]!["objective"]!.AsObject(), $"$.data.quests[{q}].steps[{s}].objective");
    }

    [Fact]
    public void Histoire_en_schema_5_l_apprentie_finit_par_une_epee_de_qualite_superieure()
    {
        StoryBook book = Story.Value;
        Assert.Equal((7, 7), (StoryValidator.CurrentSchemaVersion, book.SchemaVersion));   // V1.9.6 : schéma 6 (practice, scènes) ; V1.9.7 : 7 (skipQuest) ; craftQuality inchangé
        Assert.Equal(new[] { "colombe", "minerai", "forger", "montrer", "epee" }, book.Quest(Apprentie).Steps.Select(s => s.Id));
        Objective o = book.Quest(Apprentie).Steps[^1].Objective;
        Assert.Equal((ObjectiveKind.CraftQuality, "item.epee_de_garde", ItemQuality.Superior, 1L), (o.Kind, o.Id, o.Quality, o.Value));
        Assert.Empty(o.Delivery);   // l'épée n'est pas prise
        Assert.True(ItemsCatalog.TakesQuality(ItemsCatalog.Item(o.Id)));
        // Les autres objectifs restent de qualité normale.
        Assert.All(book.Quests.SelectMany(q => q.Steps).Where(s => s.Objective.Kind != ObjectiveKind.CraftQuality), s => Assert.Equal(ItemQuality.Normal, s.Objective.Quality));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
        // Le chef-d'œuvre se demande aussi.
        JsonObject root = StoryJson();
        EpeeObjective(root).Objective["quality"] = "masterwork";
        StoryLoadResult r = ReadStory(root);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(ItemQuality.Masterwork, r.Story!.Quest(Apprentie).Steps[^1].Objective.Quality);
    }

    [Fact]
    public void Histoire_en_schema_4_encore_lue_sans_craftQuality()
    {
        JsonObject root = StoryJson();
        root["schemaVersion"] = 4;
        (JsonObject o, string path) = EpeeObjective(root);
        // Un fichier en version 4 qui demande craftQuality est refusé, au chemin de l'objectif.
        StoryLoadResult refused = ReadStory(root);
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Report.Errors, e => e.Path == path + ".kind" && e.Code == ValidationCode.InvalidValue);
        // Sans lui (formes du schéma 4), il est lu ; sous-lot V1.9.6 : sans l'ouverture ni la scène non plus (formes du schéma 6).
        o["kind"] = "collect";
        o.Remove("quality");
        root["data"]!.AsObject().Remove("scenes");
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        quests.Remove(quests.Single(q => q!["id"]!.GetValue<string>() == "quest.reveil"));
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = dialogue!["entries"]!.AsArray();
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i]!.ToJsonString().Contains("quest.reveil")) entries.RemoveAt(i);
            // Sous-lot V1.9.7 : sans « Je sais me battre » (skipQuest, schéma 7).
            foreach (JsonNode? node in dialogue["nodes"]!.AsArray())
            {
                JsonArray choices = node!["choices"]!.AsArray();
                for (int i = choices.Count - 1; i >= 0; i--)
                    if (choices[i]!.ToJsonString().Contains("skipQuest")) choices.RemoveAt(i);
            }
        }
        StoryLoadResult read = ReadStory(root);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(4, read.Story!.SchemaVersion);
        // Plus ancien ou plus récent : refusé.
        foreach (int version in new[] { 3, 8 })
        {
            root["schemaVersion"] = version;
            Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
        }
        // Revue Codex de la PR #91 : version absente ou mal typée : refus au chemin précis, sans exception.
        root.Remove("schemaVersion");
        StoryLoadResult missing = ReadStory(root);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Report.Errors, e => e.Path == "$.schemaVersion");
        root["schemaVersion"] = "cinq";
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == "$.schemaVersion");
    }

    [Theory]
    [InlineData("quality", "\"legendaire\"", ".quality", ValidationCode.UnknownReference)]   // qualité inconnue
    [InlineData("quality", "\"normal\"", ".quality", ValidationCode.UnknownReference)]       // la qualité normale ne se demande pas
    [InlineData("quality", null, ".quality", ValidationCode.MissingField)]
    [InlineData("id", "\"item.ferrure\"", ".id", ValidationCode.InvalidValue)]              // objet sans qualité (pas un équipement)
    [InlineData("id", "\"item.potion_de_soin\"", ".id", ValidationCode.InvalidValue)]       // ne se porte pas
    [InlineData("id", "\"item.inconnu\"", ".id", ValidationCode.UnknownReference)]
    [InlineData("value", "0", ".value", ValidationCode.OutOfRange)]
    [InlineData("value", "1000", ".value", ValidationCode.OutOfRange)]
    [InlineData("value", null, ".value", ValidationCode.MissingField)]
    [InlineData("npc", "\"npc.maelle\"", ".npc", ValidationCode.InvalidValue)]
    public void Objectif_craftQuality_refuse_au_chemin_precis(string field, string? json, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        (JsonObject o, string path) = EpeeObjective(root);
        if (json is null) o.Remove(field);
        else o[field] = JsonNode.Parse(json);
        StoryLoadResult r = ReadStory(root);
        Assert.False(r.IsValid);
        Assert.Null(r.Story);
        Assert.Contains(r.Report.Errors, e => e.Path == path + suffix && e.Code == code);
    }

    [Fact]
    public void La_qualite_n_appartient_qu_a_craftQuality()
    {
        // Sur un autre objectif (le minerai de l'apprentie), une condition ou un effet : refusée, au chemin du champ.
        JsonObject root = StoryJson();
        int q = ApprentieIndex(root);
        root["data"]!["quests"]![q]!["steps"]![1]!["objective"]!["quality"] = "superior";
        root["data"]!["quests"]![q]!["requires"]![0]!["quality"] = "superior";
        root["data"]!["quests"]![q]!["effects"]![0]!["quality"] = "superior";
        StoryLoadResult r = ReadStory(root);
        Assert.False(r.IsValid);
        foreach (string path in new[] { $"$.data.quests[{q}].steps[1].objective.quality", $"$.data.quests[{q}].requires[0].quality", $"$.data.quests[{q}].effects[0].quality" })
            Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == ValidationCode.InvalidValue);
    }

    // --- Marchands en schéma 3 : l'étal de Renart après l'aveu -----------------------------------------------------------

    private static (JsonObject Root, JsonObject Renart, string Path) RenartJson()
    {
        JsonObject root = MerchantsJson();
        JsonArray list = root["data"]!["merchants"]!.AsArray();
        int i = list.Select((m, k) => (m, k)).Single(x => x.m!["id"]!.GetValue<string>() == "merchant.renart").k;
        return (root, list[i]!.AsObject(), $"$.data.merchants[{i}]");
    }

    [Fact]
    public void Marchands_en_schema_3_l_etal_de_Renart_demande_l_aveu()
    {
        Assert.Equal((3, 3), (MerchantValidator.CurrentSchemaVersion, ShippedMerchants.SchemaVersion));
        Assert.Equal(Aveu, ShippedMerchants.Merchant("merchant.renart").Fact);
        Assert.All(ShippedMerchants.Merchants.Where(m => m.Id != "merchant.renart"), m => Assert.Null(m.Fact));
        // Le fait vient de l'aveu (une réponse du dialogue de Renart), et c'est l'objectif de la dernière étape du chapitre 2.
        Assert.Equal(Aveu, Story.Value.Quest(Bourg).Steps[^1].Objective.Id);
    }

    [Theory]
    [InlineData("\"fact.jamais.pose\"", ValidationCode.UnknownReference)]   // jamais posé par l'histoire : jamais ouvert
    [InlineData("\"aveu\"", ValidationCode.InvalidValue)]                    // mal formé
    [InlineData("3", ValidationCode.WrongType)]
    public void Fait_d_ouverture_refuse_au_chemin_precis(string json, ValidationCode code)
    {
        (JsonObject root, JsonObject renart, string path) = RenartJson();
        renart["fact"] = JsonNode.Parse(json);
        MerchantsLoadResult r = ReadMerchants(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path + ".fact" && e.Code == code);
    }

    [Fact]
    public void Marchands_en_schema_2_encore_lus_sans_fait()
    {
        (JsonObject root, JsonObject renart, string path) = RenartJson();
        root["schemaVersion"] = 2;
        MerchantsLoadResult refused = ReadMerchants(root);
        Assert.Contains(refused.Report.Errors, e => e.Path == path + ".fact" && e.Code == ValidationCode.InvalidValue);
        renart.Remove("fact");
        MerchantsLoadResult read = ReadMerchants(root);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal((2, null), (read.Book!.SchemaVersion, read.Book.Merchant("merchant.renart").Fact));
        root["schemaVersion"] = 1;
        Assert.Contains(ReadMerchants(root).Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
        // Revue Codex de la PR #91 : version absente ou mal typée : refus au chemin précis, sans exception.
        root.Remove("schemaVersion");
        MerchantsLoadResult missing = ReadMerchants(root);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Report.Errors, e => e.Path == "$.schemaVersion");
        root["schemaVersion"] = "trois";
        Assert.Contains(ReadMerchants(root).Report.Errors, e => e.Path == "$.schemaVersion");
    }

    // --- Pilotes ---------------------------------------------------------------------------------------------------------

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static ZoneDefinition Room(string id, (double X, double Z) at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(at.X, at.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick, IEnumerable<string>? consumed)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), definition, ShippedData, Progression, ItemsCatalog, Story.Value, outside ? Crafting(definition) : null,
            storyZone: outside ? null : "zone.vallons", market: new Market(ShippedMerchants), guild: Guilde.Value);
        session.Step();
        Ticks(session);
        return session;
    }

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero) => Open("zone.vallons", at, hero, 0, null);

    /// <summary>Le pilote se rend ailleurs : la partie est sauvegardée, relue, et rouverte au nouveau point, au même instant du monde.</summary>
    private static WorldSession Go(WorldSession from, (double X, double Z) at, string zone = "zone.vallons")
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(zone, at, save.Character!, save.WorldTick, save.Consumed);
    }

    private static List<string> Ticks(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    private static DialogueView Say(WorldSession w, string npc, string node)
    {
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.Equal(node, view!.Node);
        return view;
    }

    private static IReadOnlyList<string> Choose(WorldSession w, int index, string? next = null)
    {
        Assert.Equal(DialogueRefusal.None, w.Choose(index, out DialogueView? view, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(next, view?.Node);
        return events.Select(e => e.ToString()).ToList();
    }

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedCharacter Hero(int level, SavedStory story, long money = 0, params ItemStack[] items) =>
        V11TestData.Hero(money, items).WithStory(story).WithXp(XpForLevel(level));

    private static SavedStory Story_(IEnumerable<string> facts, IEnumerable<string> done, params (string Quest, string Step)[] at) =>
        new SavedStory(facts, done.Select(q => (q, QuestProgress.Done)).Concat(at).ToDictionary(x => x.Item1, x => new QuestProgress(x.Item2)));

    private static readonly string[] ApprentieFacts = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.odile.retour" };

    [Fact]
    public void Une_partie_d_avant_a_l_etape_des_ferrures_continue_jusqu_a_l_epee()
    {
        // Sauvegarde d'avant V1.9.3b, à la dernière étape d'alors (livrer les ferrures) : la livraison mène à l'épée, sans blocage.
        SavedStory story = Story_(ApprentieFacts, new[] { Mission, "quest.ferrure_de_rechange" }, (Apprentie, "montrer"));
        WorldSession w = Open(Forge, Hero(8, story, 0, new ItemStack("item.ferrure", 2), new ItemStack("item.minerai_de_fer", 28)));
        Say(w, "npc.maelle", "ch2_apprentie_attente");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.maelle"));
        List<string> delivered = Ticks(w);
        Assert.Contains($"StepCompleted {Apprentie}/montrer", delivered);
        Assert.DoesNotContain($"RewardToChoose {Apprentie}", delivered);
        Assert.Equal("epee", w.Narrative!.CurrentStep(Apprentie)!.Id);
        Say(w, "npc.maelle", "ch2_apprentie_epee");
        w = Go(w, Colombe);
        Say(w, "npc.apprentie", "ch2_epee");
        w = Go(w, Forge);
        // Une ferrure et quatre minerais par épée ; trois frappes parfaites ; on recommence tant que l'épée sort de qualité normale.
        long xp = w.Character.Xp, money = w.Money;
        var end = new List<string>();
        int swords = 0;
        while (!end.Contains($"RewardToChoose {Apprentie}"))
        {
            Assert.True(swords < 4, "quatre épées sans qualité supérieure");
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.epee_de_garde", new ForgeStrikes(100, 100, 100), out CraftEvent? sword));
            swords++;
            List<string> after = Ticks(w);
            Assert.Equal(sword!.Quality >= ItemQuality.Superior, after.Contains($"StepCompleted {Apprentie}/epee"));
            end.AddRange(after);
            w = Go(w, Forge);   // sauvegardée et relue entre deux essais
        }
        // L'épée reste au personnage ; la quête paie comme avant (220 points, 150 cuivre) et attend le choix de la récompense.
        Assert.True(w.Inventory.CountAtLeast("item.epee_de_garde", ItemQuality.Superior) >= 1);
        Assert.Equal((xp + 220, money + 150), (w.Character.Xp, w.Money));
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Apprentie, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.gantelets_de_fer 1", $"QuestCompleted {Apprentie}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.maelle", "ch2_apprentie_apres");
    }

    [Fact]
    public void L_epee_deja_forgee_ou_portee_remplit_l_etape_et_la_qualite_normale_non()
    {
        SavedStory story = Story_(ApprentieFacts, new[] { Mission, "quest.ferrure_de_rechange" }, (Apprentie, "epee"));
        // Une épée de qualité normale ne suffit pas.
        WorldSession normal = Open(Forge, Hero(8, story, 0, new ItemStack("item.epee_de_garde", 1)));
        Assert.DoesNotContain($"StepCompleted {Apprentie}/epee", Ticks(normal));
        Assert.Equal("epee", normal.Narrative!.CurrentStep(Apprentie)!.Id);
        // Un chef-d'œuvre (au-dessus du minimum) suffit, même porté (le pilote le reçoit et le porte entre deux ticks).
        WorldSession worn = normal;
        Assert.Equal(InventoryRefusal.None, worn.Inventory.Add(new ItemStack("item.epee_de_garde", 1, null, ItemQuality.Masterwork, "bastion")));
        int place = worn.Inventory.Stacks.Select((s, i) => (s, i)).Single(x => x.s.Item == "item.epee_de_garde" && x.s.Quality == ItemQuality.Masterwork).i;
        Assert.Equal(InventoryRefusal.None, worn.Inventory.Equip(place, worn.Level));
        Assert.Equal(1, worn.Inventory.Count("item.epee_de_garde"));   // la normale, dans le sac
        Assert.Equal(1, worn.Inventory.CountAtLeast("item.epee_de_garde", ItemQuality.Superior));
        Assert.Contains($"RewardToChoose {Apprentie}", Ticks(worn));
    }

    [Fact]
    public void Une_partie_d_avant_qui_attend_sa_recompense_ne_se_bloque_pas()
    {
        SavedStory story = Story_(ApprentieFacts, new[] { Mission, "quest.ferrure_de_rechange" }, (Apprentie, QuestProgress.Reward));
        WorldSession w = Open(Forge, Hero(8, story));
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Apprentie, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Contains($"QuestCompleted {Apprentie}", chosen.Select(e => e.ToString()));
        Say(w, "npc.maelle", "ch2_apprentie_apres");
    }

    [Fact]
    public void L_etal_de_Renart_s_ouvre_a_son_aveu()
    {
        // À l'étape de l'aveu : l'étal est fermé, refus sans rien changer ; Renart parle (son aveu).
        string[] before = AfterChapter2.Where(f => f != Aveu && f != "fact.ch2.vers_les_champs").ToArray();
        WorldSession w = Open(Renart, Hero(10, Story_(before.Append("fact.ch2.renart.piste"), new[] { Mission }, (Bourg, "aveu")), 1000));
        Assert.Equal(TradeRefusal.NotYetOpen, w.CanTrade("merchant.renart"));
        Assert.Equal(TradeRefusal.NotYetOpen, w.Buy("merchant.renart", "item.potion_de_soin"));
        Assert.Equal((1000, 0), (w.Money, w.Inventory.Count("item.potion_de_soin")));
        Assert.True(Texts.Has("text.ui.trade.notyetopen"));   // chaque motif de refus a son texte ; l'interface ne l'affiche pas : Renart parle comme un autre PNJ
        Say(w, "npc.renart", "ch2_aveu");
        Assert.Contains($"FactLearned {Aveu}", Choose(w, 0, "ch2_aveu_fin"));
        Choose(w, 0);
        Ticks(w);
        // Après l'aveu : l'étal est ouvert, aussi une fois la partie relue.
        Assert.Equal(TradeRefusal.None, w.CanTrade("merchant.renart"));
        w = Go(w, Renart);
        long purse = w.Money;
        Assert.Equal(TradeRefusal.None, w.Buy("merchant.renart", "item.potion_de_soin"));
        Assert.Equal((purse - 80, 1), (w.Money, w.Inventory.Count("item.potion_de_soin")));
        // Une partie d'avant V1.9.3b, l'aveu déjà fait : l'étal est ouvert.
        WorldSession old = Open(Renart, Hero(12, Story_(AfterChapter2, new[] { Mission, Bourg }), 1000));
        Assert.Equal(TradeRefusal.None, old.CanTrade("merchant.renart"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_lettre_gardee_se_montre_a_Soeur_Albe(bool gardee)
    {
        string[] facts = AfterChapter2.Concat(new[] { "fact.ch3.lettre.trouvee", "fact.ch3.lettre.decidee", gardee ? "fact.ch3.lettre.gardee" : "fact.ch3.lettre.rendue" }).ToArray();
        SavedStory story = Story_(facts, new[] { Mission, Bourg, Lettre, "quest.herbes_pour_la_chapelle" }, (Hameau, "defendre"));
        ItemStack[] bag = gardee ? new[] { new ItemStack("item.lettre_mouillee", 1) } : Array.Empty<ItemStack>();
        WorldSession w = Open("zone.chapelle", Albe, Hero(12, story, 0, bag), 0, null);
        DialogueView view = Say(w, "npc.albe", "ch2_apres");
        if (!gardee)
        {
            Assert.DoesNotContain(view.Options, o => o.Text == "text.ch3.albe.lettre.montrer");
            return;
        }
        Choose(w, Assert.Single(view.Options, o => o.Text == "text.ch3.albe.lettre.montrer").Index, "ch3_lettre");
        List<string> read = Choose(w, 0).ToList();
        Assert.Contains("FactLearned fact.ch3.lettre.montree", read);
        Assert.Contains("ItemGiven item.remede_de_l_aube 1", read);
        // La lettre reste au personnage ; la réponse ne revient pas (une fois relue, la partie s'en souvient).
        w = Go(w, Albe, "zone.chapelle");
        Assert.Equal((1, 1), (w.Inventory.Count("item.lettre_mouillee"), w.Inventory.Count("item.remede_de_l_aube")));
        Assert.DoesNotContain(Say(w, "npc.albe", "ch2_apres").Options, o => o.Text == "text.ch3.albe.lettre.montrer");
    }

    [Fact]
    public void Albe_garde_seize_entrees_et_la_lettre_passe_par_ses_noeuds()
    {
        Dialogue albe = Story.Value.Dialogue("dialogue.albe");
        // Sous-lot V1.9.5 : douze entrées (les quatre des anciennes étapes de la mission sont parties : parties converties à la lecture).
        Assert.Equal(12, albe.Entries.Count);
        Assert.Equal(new[] { "ch2_accueil", "ch2_en_cours", "ch2_apres", "ch2_herbes_attente" },
            albe.Nodes.Where(n => n.Choices.Any(c => c.Next == "ch3_lettre")).Select(n => n.Id));
        // Le mot d'après le chapitre 2 garde ses réponses d'avant, dans l'ordre ; la lettre vient après.
        // V1.9.4 : les réponses du chapitre 4 viennent avant la lettre (fusion de main dans la PR #92).
        Assert.Equal(new[] { "text.ch2.albe.apres.partir", "text.ch3.albe.vers_tour", "text.ch4.albe.ancienne", "text.ch4.albe.vers_sans_repos", "text.ch4.albe.vers_apres",
            "text.ch3.albe.lettre.montrer", "text.ch5.albe.vers_voix", "text.ch5.albe.ancienne" }, albe.Node("ch2_apres").Choices.Select(c => c.Text));   // V1.9.5 : le chapitre 5 après
        foreach (string key in new[] { "text.ch2.quest.apprentie.epee", "text.ch2.maelle.apprentie_epee", "text.ch2.apprentie.epee", "text.ch3.albe.lettre",
            "text.ch3.albe.lettre.montrer", "text.ch3.albe.lettre.partir" })
            Assert.True(Texts.Has(key), key);
    }
}
