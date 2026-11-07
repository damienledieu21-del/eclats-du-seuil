using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.7, chapitre 1 enrichi, suite (PR A2) : cinq quêtes de plus au village (niveaux 1 à 5), le village vivant (répliques de
/// jour et de nuit, réactions aux choix du joueur) et trois corrections demandées par Damien sur la PR #94 : un seul loup derrière le moulin,
/// « Je sais me battre » qui passe le tutoriel sans ses 30 points (effet <c>skipQuest</c>, histoire en schéma 7), le collier de Croc-Gris.
/// Chaque quête est jouée de bout en bout par un pilote (la partie est sauvegardée, relue et rouverte à chaque déplacement). Contournements :
/// les victoires sont annoncées à l'histoire sans livrer le combat (les combats ont leurs tests) ; une partie des champignons est dans le sac.
/// </summary>
[Trait("Category", "World")]
public class V197VillageTests
{
    private const string Opening = "quest.reveil", Mission = "quest.traces_sous_le_moulin", Chat = "quest.chat_de_rosalie", Puits = "quest.puits_du_moulin",
        Garde = "quest.garde_de_nuit", Champignons = "quest.champignons_de_sauge", Contrat = "quest.premier_contrat", Pain = "quest.pain_du_matin",
        Fleches = "quest.fleches_perdues", Lanterne = "quest.derniere_lanterne", Tour = "quest.tour_silencieuse";

    // Où se tenir (à 1 à 2 m du PNJ, hors de tout obstacle).
    private static readonly (double X, double Z) Odile = (-9, 2), Moulin = (-8, 5), Boulanger = (-42, 2), Archer = (1, -1.5), Gaspard = (26, -13),
        Capitaine = (-2, -9.5), Herboriste = (-65.2, -6.4), Forge = (-19, -5), Epiciere = (-50.8, 4.4), RondEst = (-15, 25.5), RondOuest = (-21, 25.5);
    private static readonly (double X, double Z) Rosalie = (-1.8, -2.4);

    /// <summary>Heures du monde (la journée dure trois heures réelles et commence à 8 h) : 20 h, 22 h 27, 6 h le lendemain.</summary>
    private const long Crepuscule = 108_000, Nuit = 130_000, Aube = 198_000;

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));
    private static readonly Lazy<GuildBook> Guilde = new(() =>
    {
        GuildLoadResult r = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, Story.Value, Vallons.Value,
            new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList(), ItemsCatalog, ShippedMerchants);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static ZoneDefinition Room(string id, (double X, double Z) at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(at.X, at.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    /// <summary>Partie ouverte dans les Vallons ou un intérieur, au point <paramref name="at"/>, à l'instant <paramref name="tick"/> ; l'arrivée et trois ticks joués.</summary>
    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick, IEnumerable<string>? consumed, out List<string> events)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), definition, ShippedData, Progression, ItemsCatalog, Story.Value, outside ? Crafting(definition) : null,
            storyZone: outside ? null : "zone.vallons", market: new Market(ShippedMerchants), guild: Guilde.Value);
        events = session.Step().Story.Select(e => e.ToString()).ToList();
        events.AddRange(Ticks(session));
        return session;
    }

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, long tick = 0, IEnumerable<string>? consumed = null) =>
        Open("zone.vallons", at, hero, tick, consumed, out _);

    private static WorldSession OpenIn(string zone, (double X, double Z) at, SavedCharacter hero, long tick = 0) => Open(zone, at, hero, tick, null, out _);

    private static WorldSession Go(WorldSession from, (double X, double Z) at, string zone = "zone.vallons") => Go(from, at, zone, out _);

    private static WorldSession Go(WorldSession from, (double X, double Z) at, string zone, out List<string> events)
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(zone, at, save.Character!, save.WorldTick, save.Consumed, out events);
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

    private static DialogueView ChooseView(WorldSession w, int index, string next)
    {
        Assert.Equal(DialogueRefusal.None, w.Choose(index, out DialogueView? view, out _));
        Assert.Equal(next, view?.Node);
        return view!;
    }

    /// <summary>Choisit la réponse de texte <paramref name="text"/> parmi celles proposées.</summary>
    private static IReadOnlyList<string> Pick(WorldSession w, DialogueView view, string text, string? next = null) =>
        Choose(w, view.Options.Single(o => o.Text == text).Index, next);

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedCharacter Hero(int level, SavedStory? story = null, params ItemStack[] items) =>
        V11TestData.Hero(0, items).WithStory(story ?? SavedStory.Empty).WithXp(XpForLevel(level));

    private static SavedStory Done(IEnumerable<string> facts, params string[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done)));

    private static SavedStory With(IEnumerable<string> facts, IReadOnlyDictionary<string, int>? trust, params (string Quest, string Step)[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q.Quest, q => new QuestProgress(q.Step)), trust: trust);

    private static SavedCharacter Fresh(params ItemStack[] items) =>
        Inventory.Outfit(PlayerCharacters.Create("bastion", "class.bastion", Progression).WithStory(StoryConversions.NewGame(Story.Value))
            .WithItems(items, new Dictionary<string, string>(StringComparer.Ordinal)), ItemsCatalog);

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    // --- Formats : histoire en schéma 7 (skipQuest) ---------------------------------------------------------------------

    [Fact]
    public void Histoire_en_schema_7_avec_skipQuest()
    {
        Assert.Equal((10, 10), (StoryValidator.CurrentSchemaVersion, Story.Value.SchemaVersion));   // V2.3.1 : schéma 10   // V2.2 : schéma 8 (weather) ; V2.3 : schéma 9
        Assert.Equal(EffectKind.SkipQuest, StoryValidator.Effects["skipQuest"]);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
        // « Je sais me battre » : trois réponses chez Odile (l'Éclat, la vieille porte, le tutoriel en cours), et nulle part ailleurs.
        Dialogue odile = Story.Value.Dialogue("dialogue.odile");
        Assert.Equal(new[] { "reveil_eclat", "reveil_porte", "reveil_en_cours" },
            odile.Nodes.Where(n => n.Choices.Any(c => c.Effects.Any(e => e.Kind == EffectKind.SkipQuest && e.Id == Opening))).Select(n => n.Id));
        Assert.Equal(3, Story.Value.Dialogues.SelectMany(d => d.Nodes).SelectMany(n => n.Choices).Count(c => c.Effects.Any(e => e.Kind == EffectKind.SkipQuest)));
    }

    [Fact]
    public void Histoire_en_schema_6_encore_lue_sans_skipQuest()
    {
        JsonObject root = StoryJson();
        V23TestData.WithoutSchedules(root);   // V2.3 : sans emplois du temps ni répliques du moment (schéma 9)
        root["schemaVersion"] = 6;
        StoryLoadResult refused = Read(root);
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Report.Errors, e => e.Path.StartsWith("$.data.dialogues[0].nodes[", StringComparison.Ordinal)
            && e.Path.EndsWith(".do", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue && e.Message.Contains("skipQuest"));
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        int opening = quests.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == Opening);
        Assert.Contains(refused.Report.Errors, e => e.Path == $"$.data.quests[{opening}].skippable" && e.Code == ValidationCode.InvalidValue);
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
        {
            foreach (JsonNode? node in dialogue!["nodes"]!.AsArray())
            {
                JsonArray choices = node!["choices"]!.AsArray();
                for (int i = choices.Count - 1; i >= 0; i--)
                    if (choices[i]!.ToJsonString().Contains("skipQuest")) choices.RemoveAt(i);
            }
            // Sous-lot V2.2 : sans les répliques du temps (condition weather, schéma 8).
            JsonArray entries = dialogue["entries"]!.AsArray();
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i]!.ToJsonString().Contains("\"weather\"")) entries.RemoveAt(i);
        }
        quests[opening]!.AsObject().Remove("skippable");
        StoryLoadResult read = Read(root);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(6, read.Story!.SchemaVersion);
        root["schemaVersion"] = 11;   // V2.2 : la version 8 est la version courante ; V2.3 : la version 9 ; V2.3.1 : la version 10
        Assert.Contains(Read(root).Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
    }

    [Fact]
    public void SkipQuest_refuse_au_chemin_precis()
    {
        // Une quête inconnue.
        JsonObject root = StoryJson();
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == "dialogue.odile");
        JsonArray nodes = dialogues[d]!["nodes"]!.AsArray();
        int n = nodes.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == "reveil_en_cours");
        JsonArray choices = nodes[n]!["choices"]!.AsArray();
        // V1.9.9 : des réponses « le village se souvient » suivent ; on vise celle qui passe le tutoriel.
        int c = choices.ToList().FindIndex(x => x!["effects"]!.AsArray().Any(e => e!["do"]!.GetValue<string>() == "skipQuest"));
        choices[c]!["effects"]![0]!["id"] = "quest.inconnue";
        string path = $"$.data.dialogues[{d}].nodes[{n}].choices[{c}].effects[0]";
        Assert.Contains(Read(root).Report.Errors, e => e.Path == path + ".id" && e.Code == ValidationCode.UnknownReference);
        // Une quête qui n'est pas marquée « skippable » (la mission) : refusée.
        root = StoryJson();
        root["data"]!["dialogues"]![d]!["nodes"]![n]!["choices"]![c]!["effects"]![0]!["id"] = Mission;
        Assert.Contains(Read(root).Report.Errors, e => e.Path == path + ".id" && e.Code == ValidationCode.InvalidValue && e.Message.Contains("skippable"));
        Assert.Equal(new[] { Opening }, Story.Value.Quests.Where(x => x.Skippable).Select(x => x.Id));
        // Une valeur : refusée.
        root = StoryJson();
        root["data"]!["dialogues"]![d]!["nodes"]![n]!["choices"]![c]!["effects"]![0]!["value"] = 1;
        Assert.Contains(Read(root).Report.Errors, e => e.Path == path + ".value");
        // Dans une quête (étape ou récompense) : refusé, l'effet n'appartient qu'aux réponses de dialogue.
        root = StoryJson();
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        int q = quests.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == Chat);
        quests[q]!["steps"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "skipQuest", ["id"] = Opening });
        quests[q]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "skipQuest", ["id"] = Opening });
        StoryLoadResult r = Read(root);
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.quests[{q}].steps[0].effects[0].do" && e.Code == ValidationCode.InvalidValue);
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.quests[{q}].effects[2].do" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Une_epreuve_de_guilde_ne_se_passe_pas()
    {
        // L'épreuve du rang F marquée « skippable » : l'histoire est valide, la guilde la refuse au chemin de l'épreuve.
        JsonObject root = StoryJson();
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        quests.Single(x => x!["id"]!.GetValue<string>() == "quest.epreuve_rang_f")!["skippable"] = true;
        StoryLoadResult story = Read(root);
        Assert.True(story.IsValid, story.Report.ToString());
        ZoneDefinition zone = Zone();
        GuildLoadResult guild = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, story.Story!, zone,
            new[] { zone }.Concat(V16TestData.Interiors).ToList(), ItemsCatalog, ShippedMerchants);
        Assert.False(guild.IsValid);
        Assert.Contains(guild.Report.Errors, e => e.Path.EndsWith(".trial", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue && e.Message.Contains("skippable"));
    }

    [Fact]
    public void Passer_une_quete_directement()
    {
        // Le chat suivi à une étape, le puits en cours, les champignons au choix de leur récompense, les miches finies, le contrat jamais commencé.
        var story = new SavedStory(Array.Empty<string>(), new Dictionary<string, QuestProgress>
        {
            [Chat] = new QuestProgress("moulin"), [Puits] = new QuestProgress("loup"), [Champignons] = new QuestProgress(QuestProgress.Reward),
            [Pain] = new QuestProgress(QuestProgress.Done),
        }, tracked: Chat);
        WorldSession w = Open(Boulanger, Hero(5, story));
        Narrative n = w.Narrative!;
        var events = new List<StoryEvent>();
        // Pas commencée, ou finie : rien.
        Assert.False(n.Skip(Contrat, events));
        Assert.False(n.Skip(Pain, events));
        Assert.Empty(events);
        Assert.False(n.IsActive(Contrat));
        // Une quête non suivie : passée, la quête suivie le reste.
        long xp = w.Character.Xp, money = w.Money;
        Assert.True(n.Skip(Puits, events));
        Assert.Equal((true, Chat), (n.IsDone(Puits), n.Tracked));
        // Au choix de sa récompense : passée, sans récompense ; le choix n'est plus possible.
        Assert.True(n.Skip(Champignons, events));
        Assert.True(n.IsDone(Champignons));
        Assert.NotEqual(DialogueRefusal.None, w.ChooseReward(Champignons, 0, out _));
        // La quête suivie : passée, plus suivie.
        Assert.True(n.Skip(Chat, events));
        Assert.Null(n.Tracked);
        Assert.Equal(new[] { $"QuestSkipped {Puits}", $"QuestSkipped {Champignons}", $"QuestSkipped {Chat}" }, events.Select(e => e.ToString()));
        // Une seconde fois : rien. Aucune récompense, aucun objet, aucun point.
        Assert.False(n.Skip(Chat, events));
        Assert.DoesNotContain(Ticks(w), e => e.StartsWith("QuestCompleted", StringComparison.Ordinal) || e.StartsWith("XpGiven", StringComparison.Ordinal));
        Assert.Equal((xp, money, 0), (w.Character.Xp, w.Money, w.Inventory.Count("item.remede_de_l_aube")));
        // Aller-retour par la sauvegarde : les trois restent finies, aucune suivie, rien n'est rejoué.
        string text = SaveJsonReader.Write(w.Save());
        w = Go(w, Boulanger, "zone.vallons", out List<string> arrival);
        Assert.All(new[] { Chat, Puits, Champignons }, q => Assert.True(w.Narrative!.IsDone(q), q));
        Assert.Null(w.Narrative!.Tracked);
        Assert.DoesNotContain(arrival, e => e.Contains(Chat) || e.Contains(Puits) || e.Contains(Champignons));
        SavedStory saved = SaveJsonReader.Read(text).Save!.Character!.Story;
        Assert.All(new[] { Chat, Puits, Champignons, Pain }, q => Assert.Equal(QuestProgress.Done, saved.Quests[q].Step));
        Assert.Null(saved.Tracked);
    }

    // --- Corrections de la PR #94 ------------------------------------------------------------------------------------------

    [Fact]
    public void Je_sais_me_battre_passe_le_tutoriel_sans_ses_30_points()
    {
        // Nouveau personnage : Odile, puis « Je sais me battre » : l'ouverture est finie sans récompense, la mission commence.
        WorldSession w = Open(Odile, Fresh(new ItemStack("item.potion_de_soin", 2)));
        long xp = w.Character.Xp;
        Say(w, "npc.odile", "reveil");
        DialogueView eclat = ChooseView(w, 0, "reveil_eclat");
        IReadOnlyList<string> skipped = Pick(w, eclat, "text.ch1.odile.reveil.passer", "reveil_passe");
        Assert.Equal(new[] { $"QuestSkipped {Opening}" }, skipped);
        Assert.True(w.Narrative!.IsDone(Opening));
        Assert.Null(w.Narrative.Tracked);
        Choose(w, 0, "recit");
        Assert.Contains($"QuestStarted {Mission}/enquete", Choose(w, 0));
        Assert.Equal(Mission, w.Narrative.Tracked);
        List<string> after = Ticks(w);
        Assert.DoesNotContain(after, e => e.StartsWith("QuestCompleted quest.reveil", StringComparison.Ordinal) || e == "XpGiven xp 30");
        Assert.DoesNotContain(after, e => e.StartsWith($"StepCompleted {Opening}", StringComparison.Ordinal));
        // Aucun point du tutoriel (l'étape « enquete » de la mission n'en donne pas).
        Assert.Equal(xp, w.Character.Xp);
        // Sauvegardée, relue : l'ouverture reste finie, Odile parle de la mission ; les deux potions restent au sac.
        w = Go(w, Odile);
        Assert.True(w.Narrative!.IsDone(Opening));
        Assert.Equal(2, w.Inventory.Count("item.potion_de_soin"));
        Say(w, "npc.odile", "en_cours");
    }

    [Fact]
    public void Je_sais_me_battre_aussi_au_milieu_du_tutoriel()
    {
        var story = new SavedStory(new[] { "fact.ch1.reveil.porte" }, new Dictionary<string, QuestProgress> { [Opening] = new QuestProgress("parer") },
            tracked: Opening);
        WorldSession w = Open(Odile, Fresh().WithStory(story));
        DialogueView view = Say(w, "npc.odile", "reveil_en_cours");
        Assert.Equal(new[] { $"QuestSkipped {Opening}" }, Pick(w, view, "text.ch1.odile.reveil.passer", "reveil_passe"));
        // Une quête déjà finie ou jamais commencée : l'effet ne fait rien (la réponse n'est plus proposée).
        Assert.True(w.Narrative!.IsDone(Opening));
        Choose(w, 0, "recit");
        Choose(w, 0);
        Assert.DoesNotContain(Ticks(w), e => e.Contains(Opening));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.odile", out DialogueView? again));
        Assert.DoesNotContain(again!.Options, o => o.Text == "text.ch1.odile.reveil.passer");
    }

    [Fact]
    public void Un_seul_loup_derriere_le_moulin_les_deux_autres_au_pre()
    {
        ZoneDefinition zone = Vallons.Value;
        string[] Places(string id) => zone.PlacesAt(zone.Setup.Combatants.Single(c => c.Id == id).Placement!.Position).Select(p => p.Id).ToArray();
        Assert.Contains("moulin", Places("loup1"));
        Assert.Contains("pre_nord", Places("loup2"));
        Assert.Contains("pre_nord", Places("loup3"));
        // Le loup du tutoriel est seul au sud de la haie (le moulin).
        Assert.Equal(new[] { "loup1" }, zone.Setup.Combatants.Where(c => c.Team == "meute" && c.Placement is { } p
            && zone.PlacesAt(p.Position).Any(pl => pl.Id == "moulin")).Select(c => c.Id));
        // Les textes du tutoriel parlent d'un loup.
        Assert.StartsWith("Un loup rôde derrière le moulin", Texts.Get("text.ch1.quest.reveil.frapper"));
        Assert.StartsWith("Le loup est derrière le moulin", Texts.Get("text.ch1.odile.reveil_en_cours"));
    }

    [Fact]
    public void Croc_Gris_donne_son_collier()
    {
        Boss croc = Vallons.Value.Chiefs.Single(c => c.Id == "croc_gris");
        Assert.Equal(("item.collier_de_croc_gris", 1), Assert.Single(croc.Rewards));
        var collier = ItemsCatalog.Item("item.collier_de_croc_gris");
        Assert.Equal(("Collier de Croc-Gris", "neck", 4), (collier.Name, collier.Slot, collier.RequiredLevel));
        Assert.Equal(900, ShippedMerchants.Values["item.collier_de_croc_gris"]);
        // Le croc reste au catalogue (anciennes parties qui l'ont reçu), sans valeur.
        Assert.False(ShippedMerchants.Values.ContainsKey("item.croc_de_croc_gris"));
    }

    // --- Les cinq quêtes du village ------------------------------------------------------------------------------------------

    [Fact]
    public void Cinq_quetes_du_village_livrees()
    {
        StoryBook book = Story.Value;
        Assert.Equal(new[] { Opening, Chat, Puits, Garde, Champignons, Contrat }, book.Quests.TakeLast(6).Select(q => q.Id));   // ajoutées à la fin
        Assert.All(new[] { Chat, Puits, Garde, Champignons, Contrat }, q => Assert.Equal(QuestCategory.Side, book.Quest(q).Category));
        Assert.Equal(new long?[] { null, 2, 3, 4, 5 }, new[] { Chat, Puits, Garde, Champignons, Contrat }
            .Select(q => book.Quest(q).Requires.FirstOrDefault(c => c.Kind == ConditionKind.LevelAtLeast)?.Value));
        // Aucun PNJ nouveau ; seize entrées au plus par dialogue ; Sœur Albe en garde douze, Odile seize.
        Assert.Equal(28, book.Npcs.Count);
        Assert.All(book.Dialogues, d => Assert.True(d.Entries.Count <= 16, d.Id));
        Assert.Equal((13, 16), (book.Dialogue("dialogue.albe").Entries.Count, book.Dialogue("dialogue.odile").Entries.Count));
    }

    [Fact]
    public void Le_chat_de_Rosalie_de_bout_en_bout()
    {
        WorldSession w = OpenIn("zone.maison", Rosalie, Hero(1));
        Say(w, "npc.habitant", "ch1_chat");
        Assert.Contains($"QuestStarted {Chat}/firmin", Choose(w, 0));
        Say(w, "npc.habitant", "ch1_chat_attente");
        // Firmin l'a vu filer vers le moulin.
        w = Go(w, Boulanger);
        Say(w, "npc.boulanger", "ch1_chat");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Chat}/firmin", Ticks(w));
        // Au moulin : le chat, sur la poutre de la vieille porte (indice).
        w = Go(w, Moulin, "zone.vallons", out List<string> arrival);
        Assert.Contains($"StepCompleted {Chat}/moulin", arrival);
        Assert.Contains("Discovered discovery.ch1.chat_au_moulin", arrival);
        Assert.Equal(1, w.Inventory.Count("item.chat_de_rosalie"));
        // Rendu à Rosalie.
        w = Go(w, Rosalie, "zone.maison");
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.habitant", "ch1_chat_livrer");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.habitant"));
        List<string> end = Ticks(w);
        Assert.Contains("TrustChanged npc.habitant 1", end);
        Assert.Contains($"QuestCompleted {Chat}", end);
        Assert.Equal((xp + 50, money + 40, 0), (w.Character.Xp, w.Money, w.Inventory.Count("item.chat_de_rosalie")));
        Say(w, "npc.habitant", "ch1_chat_apres");
        Assert.False(ShippedMerchants.Values.ContainsKey("item.chat_de_rosalie"));
    }

    [Fact]
    public void Le_puits_du_moulin_de_bout_en_bout()
    {
        // Avant le niveau 2 : Firmin propose ses miches, pas le puits.
        Say(Open(Boulanger, Hero(1)), "npc.boulanger", "accueil");
        WorldSession w = Open(Boulanger, Hero(2));
        DialogueView offer = Say(w, "npc.boulanger", "ch1_puits");
        Assert.Contains(offer.Options, o => o.Text == "text.ch1.boulanger.vers_pain");   // les miches restent offertes
        Assert.Contains($"QuestStarted {Puits}/loup", Choose(w, 0));
        Say(w, "npc.boulanger", "ch1_puits_attente");
        // Le loup du puits, c'est celui du moulin (loup1) ; un autre loup ne compte pas.
        w.Narrative!.Defeated("loup2", "loup");
        Assert.Empty(Ticks(w));
        w.Narrative.Defeated("loup1", "loup");
        Assert.Contains($"StepCompleted {Puits}/loup", Ticks(w));
        w = Go(w, Moulin, "zone.vallons", out List<string> arrival);
        Assert.Contains($"StepCompleted {Puits}/puiser", arrival);
        Assert.Contains("Discovered discovery.ch1.puits_tiede", arrival);
        w = Go(w, Boulanger);
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.boulanger"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.seau_d_eau_du_moulin 1", end);
        Assert.Contains($"QuestCompleted {Puits}", end);
        Assert.Equal((xp + 60, money + 50), (w.Character.Xp, w.Money));
        // Les miches pas encore portées : leur offre passe avant le mot d'après le puits.
        Say(w, "npc.boulanger", "accueil");
        w.Narrative!.Close();
        var story = Done(new[] { "fact.ch1.boulanger.merci" }, Pain, Puits);
        Say(Open(Boulanger, Hero(3, story)), "npc.boulanger", "ch1_puits_apres");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void La_garde_de_nuit_de_bout_en_bout(bool taire)
    {
        // Au niveau 3 : l'offre ; la réponse moqueuse la commence aussi, Aldric s'en souvient.
        WorldSession w = Open(Archer, Hero(3));
        Say(w, "npc.archer_garde", "ch1_garde");
        List<string> start = Choose(w, taire ? 0 : 1).ToList();
        Assert.Contains($"QuestStarted {Garde}/nuit", start);
        if (!taire) Assert.Contains("TrustChanged npc.archer_garde -1", start);
        // De jour, rien ; à la tombée du jour (ou la nuit), on veille.
        Say(w, "npc.archer_garde", "ch1_garde_jour");
        WorldSave day = w.Save();
        w = Open("zone.vallons", Archer, day.Character!, taire ? Crepuscule : Nuit, day.Consumed, out _);
        Assert.Equal(taire ? DayPhase.Dusk : DayPhase.Night, w.Zone.Time.Phase);
        Say(w, "npc.archer_garde", "ch1_garde_nuit");
        Assert.Equal(new[] { "FactLearned fact.ch1.garde.veille" }, Choose(w, 0));
        Assert.Contains($"StepCompleted {Garde}/nuit", Ticks(w));
        Say(w, "npc.archer_garde", "ch1_garde_attente");
        w.Narrative!.Close();
        w.Narrative.Defeated("loup4", "loup");
        w.Narrative.Defeated("loup5", "loup");
        Assert.Contains($"StepCompleted {Garde}/loups", Ticks(w));
        // Aldric avoue qu'il a reculé : se taire ou le dire au capitaine.
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.archer_garde", "ch1_garde_choix");
        List<string> chosen = Choose(w, taire ? 0 : 2, taire ? "ch1_garde_merci" : "ch1_garde_froid").ToList();
        Assert.Contains(taire ? "FactLearned fact.ch1.aldric.secret" : "FactLearned fact.ch1.aldric.moque", chosen);
        Assert.Contains(taire ? "TrustChanged npc.archer_garde 1" : "TrustChanged npc.archer_garde -1", chosen);
        Assert.Contains($"QuestCompleted {Garde}", Ticks(w));
        Assert.Equal((xp + 80, money + 70), (w.Character.Xp, w.Money));
        Say(w, "npc.archer_garde", taire ? "ch1_garde_secret" : "ch1_garde_moque");
        // Le capitaine en parle (la nuit, sa réplique de nuit garde la réponse).
        w = Go(w, Capitaine);
        DialogueView cap = Say(w, "npc.capitaine", taire ? "ch4_accueil" : "ch1_nuit");
        Pick(w, cap, "text.ch1.capitaine.vers_aldric", taire ? "ch1_aldric_secret" : "ch1_aldric_moque");
    }

    [Fact]
    public void Les_champignons_de_Sauge_de_bout_en_bout()
    {
        // Niveau 4, mais la porte du moulin fermée : le pré est hors d'atteinte, Sauge ne demande rien.
        WorldSession closed = Open(Herboriste, Hero(4));
        Say(closed, "npc.herboriste", "accueil");
        Assert.False(closed.Narrative!.IsOpen(Champignons));
        string[] passage = { "zone.vallons/passage/moulin" };
        WorldSession w = Open(Herboriste, Hero(4, null, new ItemStack("item.chapeau_de_lune", 2)), consumed: passage);
        Say(w, "npc.herboriste", "ch1_champignons");
        Assert.Contains($"QuestStarted {Champignons}/cueillir", Choose(w, 0));
        Say(w, "npc.herboriste", "ch1_champignons_attente");
        // Les deux ronds du pré (contournement : deux chapeaux déjà au sac ; la récolte a ses tests).
        w = Go(w, RondEst);
        Assert.Equal(CraftRefusal.None, w.Gather("champignons_pre", out _));
        w = Go(w, RondOuest);
        Assert.Equal(CraftRefusal.None, w.Gather("champignons_rond", out _));
        Assert.Contains($"StepCompleted {Champignons}/cueillir", Ticks(w));
        w = Go(w, Herboriste);
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.herboriste"));
        List<string> end = Ticks(w);
        Assert.Contains("Discovered discovery.ch1.rond_de_sorciere", end);
        Assert.Contains($"RewardToChoose {Champignons}", end);
        Assert.Equal((xp + 90, money + 60), (w.Character.Xp, w.Money));
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Champignons, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.remede_de_l_aube 1", $"QuestCompleted {Champignons}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.herboriste", "ch1_champignons_apres");
        Assert.Equal(new[] { "FactLearned fact.ch1.sauge.rond" }, Choose(w, 0));
        Say(w, "npc.herboriste", "accueil");
    }

    [Fact]
    public void Le_premier_contrat_de_bout_en_bout()
    {
        Say(Open(Capitaine, Hero(4)), "npc.capitaine", "ch4_accueil");
        WorldSession w = Open(Capitaine, Hero(5));
        Say(w, "npc.capitaine", "ch1_contrat");
        Assert.Contains($"QuestStarted {Contrat}/loups", Choose(w, 0));
        Say(w, "npc.capitaine", "ch1_contrat_attente");
        w.Narrative!.Close();
        foreach (string wolf in new[] { "loup2", "loup3", "loup4" }) w.Narrative.Defeated(wolf, "loup");
        Assert.Contains($"StepCompleted {Contrat}/loups", Ticks(w));
        w.Narrative.Defeated("sanglier2", "sanglier");   // pas celui des ruines
        Assert.Empty(Ticks(w));
        w.Narrative.Defeated("sanglier", "sanglier");
        Assert.Contains($"StepCompleted {Contrat}/sanglier", Ticks(w));
        w = Go(w, Capitaine);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.capitaine", "ch1_contrat_retour");
        w.Narrative!.Close();
        List<string> end = Ticks(w);
        Assert.Contains($"RewardToChoose {Contrat}", end);
        Assert.Equal((xp + 120, money + 120), (w.Character.Xp, w.Money));
        Say(w, "npc.capitaine", "ch1_contrat_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Contrat, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.elixir_de_vigueur 2", $"QuestCompleted {Contrat}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.capitaine", "ch4_accueil");
    }

    [Fact]
    public void Offres_de_chaque_donneur_encore_accessibles()
    {
        // Leçon de la PR #88 : une offre en masque une autre seulement si elle garde une réponse vers elle.
        WorldSession r = OpenIn("zone.maison", Rosalie, Hero(5));
        Pick(r, Say(r, "npc.habitant", "ch2_lanterne"), "text.ch1.habitant.vers_chat", "ch1_chat");
        WorldSession c = Open(Capitaine, Hero(16));
        Pick(c, Say(c, "npc.capitaine", "ch4_tour"), "text.ch1.capitaine.vers_contrat", "ch1_contrat");
        WorldSession a = Open(Archer, Hero(4));
        Pick(a, Say(a, "npc.archer_garde", "demande"), "text.ch1.archer_garde.vers_garde", "ch1_garde");
        WorldSession f = Open(Boulanger, Hero(2));
        Pick(f, Say(f, "npc.boulanger", "ch1_puits"), "text.ch1.boulanger.vers_pain", "accueil");
        // Les miches en cours : le puits reste offert ; le puits en cours : les miches aussi.
        WorldSession p = Open(Boulanger, Hero(2, With(Array.Empty<string>(), null, (Pain, "odile"))));
        Pick(p, Say(p, "npc.boulanger", "en_cours"), "text.ch1.boulanger.vers_puits", "ch1_puits");
        WorldSession q = Open(Boulanger, Hero(2, With(Array.Empty<string>(), null, (Puits, "loup"))));
        Pick(q, Say(q, "npc.boulanger", "ch1_puits_attente"), "text.ch1.boulanger.vers_pain", "accueil");
    }

    // --- Le village vivant -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Repliques_de_jour_et_de_nuit()
    {
        SavedStory firmin = Done(new[] { "fact.ch1.boulanger.merci", "fact.ch1.boulanger.puits" }, Pain, Puits);
        foreach ((string npc, (double, double) at, SavedStory? story, string day) in new[]
                 {
                     ("npc.gaspard", Gaspard, (SavedStory?)null, "garde"), ("npc.maelle", Forge, null, "bonjour"), ("npc.epiciere", Epiciere, null, "accueil"),
                     ("npc.herboriste", Herboriste, null, "accueil"), ("npc.capitaine", Capitaine, null, "ch4_accueil"), ("npc.archer_garde", Archer, null, "accueil"),
                     ("npc.boulanger", Boulanger, firmin, "apres"),
                 })
        {
            Say(Open(at, Hero(1, story)), npc, day);
            // Sous-lot V2.1 : la nuit, Maëlle et Bertille sont rentrées : on leur parle devant leur porte.
            (double, double) night = npc == "npc.maelle" ? (-22.6, -8) : npc == "npc.epiciere" ? (-46, 3.4) : at;
            Say(Open(night, Hero(1, story), Nuit), npc, "ch1_nuit");
        }
        // Firmin, à l'aube ; Rosalie, la nuit (après le chat, son mot d'après de jour).
        Say(Open(Boulanger, Hero(1, firmin), Aube), "npc.boulanger", "ch1_aube");
        SavedStory chat = Done(Array.Empty<string>(), Chat);
        Say(OpenIn("zone.maison", Rosalie, Hero(1, chat)), "npc.habitant", "ch1_chat_apres");
        Say(OpenIn("zone.maison", Rosalie, Hero(1, chat), Nuit), "npc.habitant", "ch1_nuit");
        // La nuit ne masque aucune offre : Firmin propose encore ses miches.
        Say(Open(Boulanger, Hero(1), Nuit), "npc.boulanger", "accueil");
    }

    [Fact]
    public void Odile_vexee_ou_en_confiance()
    {
        // Vexée (« Vous mentez… ») : une réponse pour s'excuser, une fois ; Gaspard le sait.
        var vexee = With(new[] { "fact.traces.recit", "fact.odile.vexee" }, new Dictionary<string, int> { ["npc.odile"] = -1 }, (Mission, "traces"));
        WorldSession w = Open(Odile, Hero(1, vexee));
        DialogueView view = Say(w, "npc.odile", "en_cours");
        Assert.DoesNotContain(view.Options, o => o.Text == "text.ch1.odile.vers_roue");
        List<string> sorry = Pick(w, view, "text.ch1.odile.excuser", "ch1_excuses").ToList();
        Assert.Equal(new[] { "FactLearned fact.ch1.odile.excuses", "TrustChanged npc.odile 1" }, sorry);
        Assert.DoesNotContain(Say(w, "npc.odile", "en_cours").Options, o => o.Text == "text.ch1.odile.excuser");
        WorldSession g = Open(Gaspard, Hero(1, vexee));
        Pick(g, Say(g, "npc.gaspard", "garde"), "text.ch1.gaspard.vers_odile", "ch1_odile_vexee");
        // Excusé : Gaspard n'en parle plus (confiance revenue à zéro : ni vexée, ni en confiance).
        w = Go(w, Gaspard);
        Assert.DoesNotContain(Say(w, "npc.gaspard", "garde").Options, o => o.Text == "text.ch1.gaspard.vers_odile");
        // En confiance : Odile parle de sa roue (indice), Gaspard la cite ; la nuit aussi.
        var confiance = With(new[] { "fact.traces.recit" }, new Dictionary<string, int> { ["npc.odile"] = 1 }, (Mission, "traces"));
        WorldSession o = Open(Odile, Hero(1, confiance));
        Pick(o, Say(o, "npc.odile", "en_cours"), "text.ch1.odile.vers_roue", "ch1_roue");
        WorldSession n = Open(Gaspard, Hero(1, confiance), Nuit);
        Pick(n, Say(n, "npc.gaspard", "ch1_nuit"), "text.ch1.gaspard.vers_odile", "ch1_odile_confiance");
    }
}
