using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.6, « Quêtes : le moteur » : histoire en schéma 3 (catégories, conditions d'ouverture, récompenses au choix, objectifs talk,
/// deliver, defeatFamily, equipped, effet takeMoney, condition levelAtLeast, PNJ des intérieurs), sauvegarde en version 8 (quête suivie,
/// récompense en attente ; la version 7 est celle du sous-lot V1.2), zones en schéma 5 (chefs, familles). Chaque objectif, chaque refus.
/// </summary>
[Trait("Category", "World")]
public class V16QuetesTests
{
    private const string Peaux = "quest.peaux_pour_l_auberge", Bete = "quest.bete_blanche";

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root, IReadOnlyList<ZoneDefinition>? interiors = null) =>
        StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, interiors ?? V16TestData.Interiors);

    private static StoryBook Shipped => Read(StoryJson()).Story!;

    private static ZoneDefinition Inside(string id) => V16TestData.Interiors.Single(z => z.Id == id);

    private static JsonObject Npc(JsonObject root, string id) => root["data"]!["npcs"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    private static JsonObject Quest(JsonObject root, string id) => root["data"]!["quests"]!.AsArray().Single(q => (string)q!["id"]! == id)!.AsObject();

    /// <summary>L'histoire livrée, le PNJ <paramref name="npc"/> posé à 1,5 m de la porte de sa pièce (où l'on arrive).</summary>
    private static StoryBook NearDoor(string npc, Action<JsonObject>? more = null)
    {
        JsonObject root = StoryJson();
        JsonObject n = Npc(root, npc);
        Door door = Inside((string)n["zone"]!).Doors[0];
        n["position"] = new JsonArray(Math.Round(door.Position.X.Raw / 10000.0, 4), Math.Round(door.Position.Z.Raw / 10000.0 + 1.5, 4));
        more?.Invoke(root);
        StoryLoadResult r = Read(root);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Story!;
    }

    /// <summary>Partie ouverte dans l'intérieur <paramref name="zone"/>, arrivée par sa porte, un tick joué.</summary>
    private static WorldSession InRoom(string zone, StoryBook book, SavedCharacter hero, bool step = true)
    {
        var session = new WorldSession(new WorldSave(zone, 0, 0, Array.Empty<string>(), hero), Inside(zone), ShippedData, Progression, ItemsCatalog, book,
            door: "sortie", storyZone: "zone.vallons");
        if (step) session.Step();
        return session;
    }

    private static SavedCharacter WithQuest(SavedCharacter hero, string quest, string step, long progress = 0, string? tracked = null, params string[] facts) =>
        hero.WithStory(new SavedStory(facts, new Dictionary<string, QuestProgress> { [quest] = new QuestProgress(step, progress) }, null, null, tracked));

    private static long XpForLevel(int level) => Enumerable.Range(0, 1_000_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static List<string> Steps(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    // --- Format de l'histoire ------------------------------------------------------------------------------------------

    [Fact]
    public void Histoire_livree_en_schema_3()
    {
        StoryBook book = Shipped;
        Assert.Equal(4, book.SchemaVersion);   // sous-lot V1.7 : schéma 4 (rang et points de guilde), formes du schéma 3 inchangées
        Assert.Equal(QuestCategory.Main, book.Quest("quest.traces_sous_le_moulin").Category);
        Assert.Equal(QuestCategory.Side, book.Quest(Peaux).Category);
        Assert.Equal(new[] { ("item.potion_de_soin", 3), ("item.remede_de_l_aube", 1) }, book.Quest(Peaux).RewardChoices.Select(c => (c.Item, c.Count)));
        Assert.Equal(3, book.Quest(Bete).RewardChoices.Count);
        Assert.Equal(ConditionKind.LevelAtLeast, Assert.Single(book.Quest(Bete).Requires).Kind);
        Assert.Equal(new[] { ObjectiveKind.Talk, ObjectiveKind.Equipped, ObjectiveKind.Defeat, ObjectiveKind.Talk }, book.Quest(Bete).Steps.Select(s => s.Objective.Kind));
        Objective deliver = book.Quest(Peaux).Steps[1].Objective;
        Assert.Equal((ObjectiveKind.Deliver, "item.peau_de_loup", 3L, "npc.aubergiste"), (deliver.Kind, deliver.Id, deliver.Value, deliver.Npc));
        Assert.Equal((ObjectiveKind.DefeatFamily, "loup"), (book.Quest(Peaux).Steps[0].Objective.Kind, book.Quest(Peaux).Steps[0].Objective.Id));
        // Les PNJ des intérieurs : chacun dans sa pièce ; ceux du dehors sans zone.
        Assert.Equal(new[] { ("npc.aubergiste", "zone.auberge"), ("npc.pretre", "zone.chapelle"), ("npc.habitant", "zone.maison") },
            book.Npcs.Where(n => n.Zone != null).Select(n => (n.Id, n.Zone!)).Take(3));   // sous-lot V1.7 : puis ceux de la guilde
        Assert.Equal(6, book.NpcsIn(null).Count());
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Histoire_de_schema_2_refusee_et_interieurs_necessaires()
    {
        JsonObject root = StoryJson();
        root["schemaVersion"] = 2;
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(Read(root).Report.Errors).Path, Read(root).Report.Errors[0].Code));
        // Sans les intérieurs, leurs PNJ vivent dans une zone inconnue.
        StoryLoadResult alone = Read(StoryJson(), Array.Empty<ZoneDefinition>());
        Assert.Equal(new[] { "$.data.npcs[6].zone", "$.data.npcs[7].zone", "$.data.npcs[8].zone", "$.data.npcs[9].zone", "$.data.npcs[10].zone", "$.data.npcs[11].zone" },
            alone.Report.Errors.Select(e => e.Path));   // sous-lot V1.7 : la maîtresse et l'intendant de la guilde, l'archiviste
        Assert.All(alone.Report.Errors, e => Assert.Equal(ValidationCode.UnknownReference, e.Code));
    }

    public static IEnumerable<object[]> Mauvaises_histoires()
    {
        object[] Case(string path, ValidationCode code, Action<JsonObject> change) => new object[] { path, code, change };
        // Quêtes : catégorie, ouverture, récompenses au choix, étape réservée.
        yield return Case("$.data.quests[1].category", ValidationCode.MissingField, r => Quest(r, Peaux).Remove("category"));
        yield return Case("$.data.quests[1].category", ValidationCode.InvalidValue, r => Quest(r, Peaux)["category"] = "chapitre");
        yield return Case("$.data.quests[1].rewardChoice", ValidationCode.OutOfRange, r => Quest(r, Peaux)["rewardChoice"]!.AsArray().RemoveAt(1));
        yield return Case("$.data.quests[1].rewardChoice[1].do", ValidationCode.InvalidValue,
            r => Quest(r, Peaux)["rewardChoice"]![1] = new JsonObject { ["do"] = "giveXp", ["value"] = 10 });
        yield return Case("$.data.quests[1].rewardChoice[1].id", ValidationCode.DuplicateId,
            r => Quest(r, Peaux)["rewardChoice"]![1] = new JsonObject { ["do"] = "giveItem", ["id"] = "item.potion_de_soin", ["value"] = 1 });
        yield return Case("$.data.quests[2].requires[0].value", ValidationCode.OutOfRange, r => Quest(r, Bete)["requires"]![0]!["value"] = 0);
        yield return Case("$.data.quests[2].requires[0].id", ValidationCode.InvalidValue, r => Quest(r, Bete)["requires"]![0]!["id"] = "quest.x");
        yield return Case("$.data.quests[1].steps[0].id", ValidationCode.InvalidValue, r => Quest(r, Peaux)["steps"]![0]!["id"] = "reward");
        // Objectifs.
        yield return Case("$.data.quests[2].steps[0].objective.id", ValidationCode.UnknownReference, r => Quest(r, Bete)["steps"]![0]!["objective"]!["id"] = "npc.personne");
        yield return Case("$.data.quests[2].steps[0].objective.value", ValidationCode.InvalidValue, r => Quest(r, Bete)["steps"]![0]!["objective"]!["value"] = 2);
        yield return Case("$.data.quests[1].steps[1].objective.npc", ValidationCode.MissingField, r => Quest(r, Peaux)["steps"]![1]!["objective"]!.AsObject().Remove("npc"));
        yield return Case("$.data.quests[1].steps[1].objective.npc", ValidationCode.UnknownReference, r => Quest(r, Peaux)["steps"]![1]!["objective"]!["npc"] = "npc.personne");
        yield return Case("$.data.quests[1].steps[1].objective.value", ValidationCode.OutOfRange, r => Quest(r, Peaux)["steps"]![1]!["objective"]!["value"] = 0);
        yield return Case("$.data.quests[2].steps[0].objective.npc", ValidationCode.InvalidValue, r => Quest(r, Bete)["steps"]![0]!["objective"]!["npc"] = "npc.odile");
        yield return Case("$.data.quests[1].steps[0].objective.id", ValidationCode.UnknownReference, r => Quest(r, Peaux)["steps"]![0]!["objective"]!["id"] = "dragon");
        yield return Case("$.data.quests[2].steps[1].objective.id", ValidationCode.InvalidValue, r => Quest(r, Bete)["steps"]![1]!["objective"]!["id"] = "item.peau_de_loup");
        yield return Case("$.data.quests[2].steps[1].objective.value", ValidationCode.InvalidValue, r => Quest(r, Bete)["steps"]![1]!["objective"]!["value"] = 1);
        // Effets et conditions.
        yield return Case("$.data.dialogues[7].nodes[0].choices[0].effects[0].value", ValidationCode.OutOfRange,
            r => r["data"]!["dialogues"]![7]!["nodes"]![0]!["choices"]![0]!["effects"]![0]!["value"] = 0);
        yield return Case("$.data.dialogues[7].nodes[0].choices[0].effects[0].id", ValidationCode.InvalidValue,
            r => r["data"]!["dialogues"]![7]!["nodes"]![0]!["choices"]![0]!["effects"]![0]!["id"] = "npc.pretre");
        yield return Case("$.data.dialogues[7].entries[4].conditions[1].value", ValidationCode.MissingField,
            r => r["data"]!["dialogues"]![7]!["entries"]![4]!["conditions"]![1]!.AsObject().Remove("value"));
        // PNJ des intérieurs.
        yield return Case("$.data.npcs[6].zone", ValidationCode.UnknownReference, r => Npc(r, "npc.aubergiste")["zone"] = "zone.cave");
        yield return Case("$.data.npcs[6].zone", ValidationCode.InvalidValue, r => Npc(r, "npc.aubergiste")["zone"] = "zone.vallons");
        yield return Case("$.data.npcs[6].position", ValidationCode.OutOfRange, r => Npc(r, "npc.aubergiste")["position"] = new JsonArray(-8, 3));
    }

    [Theory]
    [MemberData(nameof(Mauvaises_histoires))]
    public void Histoire_invalide_refusee_avec_son_chemin(string path, ValidationCode code, Action<JsonObject> change)
    {
        JsonObject root = StoryJson();
        change(root);
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    // --- Objectifs -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Parler_a_un_PNJ_d_interieur_remplit_l_objectif()
    {
        StoryBook book = NearDoor("npc.habitant");
        WorldSession w = InRoom("zone.maison", book, WithQuest(Hero(), Bete, "temoin"));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.habitant", out DialogueView? view));
        Assert.Equal("text.habitant.temoin", view!.Text);   // l'entrée de l'étape : Rosalie raconte la bête
        Assert.Equal(new[] { $"ObjectiveDone {Bete}/temoin", $"StepCompleted {Bete}/temoin" }, Steps(w));
        Assert.Equal("armure", w.Narrative!.CurrentStep(Bete)!.Id);
        // Un PNJ d'une autre pièce, ou de dehors, est trop loin.
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.aubergiste", out _));
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.odile", out _));
    }

    [Fact]
    public void Parler_trop_loin_ne_remplit_rien()
    {
        // Mathurin à sa place livrée (derrière son comptoir) : depuis la porte de l'auberge, trop loin ; rien n'est remis.
        WorldSession w = InRoom("zone.auberge", Shipped, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.aubergiste", out _));
        Assert.Empty(Steps(w));
        Assert.Equal((0L, 3), (w.Narrative!.Progress(Peaux), w.Inventory.Count("item.peau_de_loup")));
    }

    [Fact]
    public void PNJ_des_interieurs_a_portee_depuis_le_sol()
    {
        // Données livrées : chaque PNJ d'intérieur se parle depuis une place libre de sa pièce (devant le comptoir, l'autel, le feu).
        foreach ((string npc, double x, double z) in new[] { ("npc.aubergiste", 3.75, 1.6), ("npc.pretre", -2.0, 3.6), ("npc.habitant", -1.8, -2.4) })
        {
            Npc n = Shipped.Npc(npc);
            ZoneDefinition room = Inside(n.Zone!);
            double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
            Assert.True(Math.Sqrt((nx - x) * (nx - x) + (nz - z) * (nz - z)) < 3, npc);
            Assert.All(room.Setup.Space!.Obstacles, o => Assert.False(
                o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < x && x < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < z && z < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, $"{npc} : {o.Id}"));
        }
    }

    [Fact]
    public void Porter_un_objet()
    {
        WorldSession w = InRoom("zone.maison", Shipped, WithQuest(Hero(0, new ItemStack("item.gambison", 1)), Bete, "armure"));
        Assert.Empty(Steps(w));   // dans le sac, pas porté
        int place = w.Inventory.Stacks.ToList().FindIndex(s => s.Item == "item.gambison");
        Assert.Equal(InventoryRefusal.None, w.Equip(place));
        Assert.Equal(new[] { $"StepCompleted {Bete}/armure" }, Steps(w));
        Assert.Equal("chasse", w.Narrative!.CurrentStep(Bete)!.Id);
    }

    [Fact]
    public void Vaincre_une_creature_precise_ou_une_famille()
    {
        WorldSession bete = InRoom("zone.maison", Shipped, WithQuest(Hero(), Bete, "chasse"));
        bete.Narrative!.Defeated("loup1", "loup");   // un loup ordinaire ne compte pas pour le loup blanc
        Assert.Empty(Steps(bete));
        bete.Narrative.Defeated("loup_blanc", "loup_blanc");
        Assert.Equal(new[] { $"StepCompleted {Bete}/chasse" }, Steps(bete));

        WorldSession peaux = InRoom("zone.maison", Shipped, WithQuest(Hero(), Peaux, "chasse"));
        peaux.Narrative!.Defeated("sanglier", "sanglier");
        peaux.Narrative.Defeated("loup_blanc", "loup_blanc");   // une autre famille
        peaux.Narrative.Defeated("loup3", "loup");
        peaux.Narrative.Defeated("loup7_v2", "loup");
        Assert.Empty(Steps(peaux, 1));
        Assert.Equal(2, peaux.Narrative.Progress(Peaux));
        peaux.Narrative.Defeated("loup12", "loup");
        Assert.Equal(new[] { $"StepCompleted {Peaux}/chasse" }, Steps(peaux, 1));
        Assert.Equal("peaux", peaux.Narrative.CurrentStep(Peaux)!.Id);
    }

    [Fact]
    public void Familles_des_populations_livrees()
    {
        ZoneDefinition z = Zone();
        Assert.Equal("loup", z.PopulationOf("loup12")!.Family);   // par défaut : sans les chiffres finaux
        Assert.Equal("sanglier", z.PopulationOf("sanglier")!.Family);
        Assert.Equal(new[] { "bandit_arc1", "bandit_arc2", "bandit_epee1", "bandit_epee2", "le_borgne" },   // sous-lot V1.7 : le Borgne
            z.Populations.Where(p => p.Family == "bandit").Select(p => p.Creature).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal("loup_blanc", z.PopulationOf("loup_blanc")!.Family);
        Assert.Equal(12, z.Populations.Count(p => p.Family == "loup"));
    }

    [Fact]
    public void Livrer_a_un_PNJ_en_entier_ou_pas_du_tout()
    {
        StoryBook book = NearDoor("npc.aubergiste");
        // Deux peaux seulement : la réponse « Voici : … » n'est pas proposée (réponses du 31 octobre 2026), rien n'est remis, l'étape attend.
        WorldSession short_ = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 2)), Peaux, "peaux"));
        Assert.Equal(DialogueRefusal.NoSuchChoice, Deliver(short_, "npc.aubergiste"));
        Assert.Empty(Steps(short_));
        Assert.Equal(2, short_.Inventory.Count("item.peau_de_loup"));
        // Quatre : trois remises d'un coup, la dernière étape franchie, l'expérience et les pièces données, la récompense attend le choix.
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 4)), Peaux, "peaux", 0, Peaux));
        long xp = w.Character.Xp;
        // Réponses du 31 octobre 2026 : parler ne livre plus ; la réponse « Voici : Peau de loup ×3. » promet les peaux.
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.aubergiste", out _));
        Assert.Equal(0, w.Narrative!.Progress(Peaux));
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.aubergiste"));
        // Relecture de la PR #76 : promises, les peaux sont prises au tick suivant avec les effets de l'étape (tout ou rien).
        Assert.Equal(4, w.Inventory.Count("item.peau_de_loup"));
        Assert.Equal(new[] { $"ObjectiveDone {Peaux}/peaux", $"StepCompleted {Peaux}/peaux", "ItemTaken item.peau_de_loup 3", "XpGiven xp 60",
            "MoneyGiven money 150", $"RewardToChoose {Peaux}" }, Steps(w));
        Assert.Equal(1, w.Inventory.Count("item.peau_de_loup"));
        Assert.Equal((xp + 60, 150L), (w.Character.Xp, w.Money));
        Assert.True(w.Narrative!.IsActive(Peaux) && w.Narrative.IsChoosingReward(Peaux) && !w.Narrative.IsDone(Peaux));
        Assert.Null(w.Narrative.CurrentStep(Peaux));
        Assert.Equal(Peaux, w.Narrative.Tracked);   // toujours suivie tant que le choix attend
        // Parler encore ne remet rien de plus.
        w.Talk("npc.aubergiste", out _);
        Assert.Empty(Steps(w));
        Assert.Equal(1, w.Inventory.Count("item.peau_de_loup"));
    }

    // --- Récompenses au choix -----------------------------------------------------------------------------------------

    private static WorldSession Choosing(params ItemStack[] items) =>
        InRoom("zone.maison", Shipped, WithQuest(Hero(0, items), Peaux, QuestProgress.Reward, 0, Peaux));

    [Fact]
    public void Recompense_au_choix_donnee_une_fois()
    {
        WorldSession w = Choosing();
        Assert.True(w.Narrative!.IsChoosingReward(Peaux));
        Assert.Equal(DialogueRefusal.NoSuchChoice, w.ChooseReward(Peaux, 2, out IReadOnlyList<StoryEvent> none));
        Assert.Equal(DialogueRefusal.NoSuchChoice, w.ChooseReward(Peaux, -1, out _));
        Assert.Equal(DialogueRefusal.Locked, w.ChooseReward(Bete, 0, out _));
        Assert.Equal(DialogueRefusal.Locked, w.ChooseReward("quest.inconnue", 0, out _));
        Assert.Empty(none);
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Peaux, 1, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(new[] { "ItemGiven item.remede_de_l_aube 1", $"QuestCompleted {Peaux}" }, events.Select(e => e.ToString()));
        Assert.Equal(1, w.Inventory.Count("item.remede_de_l_aube"));
        Assert.True(w.Narrative.IsDone(Peaux));
        Assert.Null(w.Narrative.Tracked);   // finie : plus suivie
        Assert.Equal(DialogueRefusal.Locked, w.ChooseReward(Peaux, 0, out _));   // une seule fois
        Assert.Equal(1, w.Inventory.Count("item.remede_de_l_aube"));
    }

    [Fact]
    public void Recompense_au_choix_sac_plein_rien_ne_change()
    {
        // Inventaire plein d'objets qui ne s'empilent pas avec la potion.
        int size = ItemsCatalog.InventorySize;
        WorldSession w = Choosing(Enumerable.Range(0, size).Select(_ => new ItemStack("item.gambison", 1)).ToArray());
        string before = Snapshot(w);
        Assert.Equal(DialogueRefusal.NoRoom, w.ChooseReward(Peaux, 0, out IReadOnlyList<StoryEvent> events));
        Assert.Empty(events);
        Assert.Equal(before, Snapshot(w));
        Assert.True(w.Narrative!.IsChoosingReward(Peaux));
    }

    [Fact]
    public void Recompense_en_attente_gardee_dans_la_sauvegarde()
    {
        WorldSession w = Choosing();
        string text = SaveJsonReader.Write(w.Save());
        Assert.Contains($"\"{Peaux}\": {{\"step\": \"reward\", \"progress\": 0}}", text, StringComparison.Ordinal);
        Assert.Contains($"\"tracked\": \"{Peaux}\"", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        WorldSession again = InRoom("zone.maison", Shipped, read.Save!.Character!);
        Assert.True(again.Narrative!.IsChoosingReward(Peaux));
        Assert.Equal(Peaux, again.Narrative.Tracked);
        // Une quête sans récompense au choix ne peut pas être en attente de choix : la sauvegarde ne va pas avec l'histoire.
        Assert.Throws<ArgumentException>(() => InRoom("zone.maison", Shipped, WithQuest(Hero(), "quest.traces_sous_le_moulin", QuestProgress.Reward)));
    }

    // --- Prendre de l'or ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(250, true)]
    public void Offrande_au_pretre_atomique(long money, bool paid)
    {
        WorldSession w = InRoom("zone.chapelle", NearDoor("npc.pretre"), Hero(money));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.pretre", out DialogueView? view));
        Assert.Equal("text.pretre.accueil.offrande", view!.Options[0].Text);
        string before = Snapshot(w);
        DialogueRefusal refusal = w.Choose(0, out DialogueView? next, out IReadOnlyList<StoryEvent> events);
        if (!paid)
        {
            Assert.Equal(DialogueRefusal.NotEnoughMoney, refusal);
            Assert.Empty(events);
            Assert.Equal(before, Snapshot(w));
            Assert.False(w.Narrative!.Knows("fact.pretre.offrande"));
            Assert.Equal(0, w.Narrative.Trust("npc.pretre"));
            Assert.NotNull(w.Narrative.Open);   // le dialogue reste ouvert
            return;
        }
        Assert.Equal(DialogueRefusal.None, refusal);
        Assert.Equal("text.pretre.merci", next!.Text);
        Assert.Equal(new[] { "MoneyTaken money 10", "FactLearned fact.pretre.offrande", "TrustChanged npc.pretre 1" }, events.Select(e => e.ToString()));
        Assert.Equal(money - 10, w.Money);
        // La réponse ne se propose plus (son fait est appris) : on ne paie qu'une fois.
        w.Talk("npc.pretre", out DialogueView? again);
        Assert.DoesNotContain(again!.Options, o => o.Text == "text.pretre.accueil.offrande");
    }

    [Fact]
    public void Prendre_et_donner_des_pieces_ensemble()
    {
        // Une réponse retouchée prend 10 cuivre et en donne 4 : la bourse de 10 suffit (prises d'abord), celle de 9 non.
        StoryBook book = NearDoor("npc.pretre", root =>
            root["data"]!["dialogues"]![7]!["nodes"]![0]!["choices"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "giveMoney", ["value"] = 4 }));
        WorldSession w = InRoom("zone.chapelle", book, Hero(10));
        w.Talk("npc.pretre", out _);
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out _, out _));
        Assert.Equal(4, w.Money);
        WorldSession poor = InRoom("zone.chapelle", book, Hero(9));
        poor.Talk("npc.pretre", out _);
        Assert.Equal(DialogueRefusal.NotEnoughMoney, poor.Choose(0, out _, out _));
        Assert.Equal(9, poor.Money);
    }

    [Fact]
    public void Etape_qui_prend_trop_de_pieces_attend()
    {
        StoryBook book = NearDoor("npc.habitant", root =>
            Quest(root, Bete)["steps"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "takeMoney", ["value"] = 50 }));
        WorldSession w = InRoom("zone.maison", book, WithQuest(Hero(49), Bete, "temoin"));
        w.Talk("npc.habitant", out _);
        Assert.Equal(new[] { $"ObjectiveDone {Bete}/temoin", $"MoneyShort {Bete}/temoin" }, Steps(w, 5));   // annoncé une fois
        Assert.Equal(("temoin", 49L), (w.Narrative!.CurrentStep(Bete)!.Id, w.Money));
    }

    // --- Quêtes qui s'ouvrent ---------------------------------------------------------------------------------------------

    [Fact]
    public void Quete_ouverte_au_niveau_3()
    {
        StoryBook book = NearDoor("npc.pretre");
        WorldSession low = InRoom("zone.chapelle", book, Hero());
        Assert.False(low.Narrative!.IsOpen(Bete));
        low.Talk("npc.pretre", out DialogueView? greeting);
        Assert.Equal("text.pretre.accueil", greeting!.Text);
        WorldSession high = InRoom("zone.chapelle", book, Hero().WithXp(XpForLevel(3)));
        Assert.True(high.Narrative!.IsOpen(Bete));
        high.Talk("npc.pretre", out DialogueView? ask);
        Assert.Equal("text.pretre.demande", ask!.Text);
        Assert.Equal(DialogueRefusal.None, high.Choose(0, out _, out IReadOnlyList<StoryEvent> events));
        Assert.Contains($"QuestStarted {Bete}/temoin", events.Select(e => e.ToString()));
        Assert.Equal(Bete, high.Narrative.Tracked);   // sans quête suivie, la nouvelle l'est
        Assert.False(high.Narrative.IsOpen(Bete));   // commencée : plus à ouvrir
    }

    [Fact]
    public void Reponse_qui_ouvrirait_une_quete_fermee_cachee_et_etape_qui_attend()
    {
        // L'accueil de l'aubergiste retouché : sa réponse commencerait aussi « La bête blanche ». Au niveau 1, elle n'est pas proposée.
        StoryBook book = NearDoor("npc.aubergiste", root =>
        {
            root["data"]!["dialogues"]![6]!["nodes"]![0]!["choices"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "startQuest", ["id"] = Bete });
            Quest(root, Peaux)["steps"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "startQuest", ["id"] = Bete });
        });
        WorldSession w = InRoom("zone.auberge", book, Hero());
        w.Talk("npc.aubergiste", out DialogueView? view);
        Assert.Equal(new[] { "text.aubergiste.accueil.plus_tard" }, view!.Options.Select(o => o.Text));
        // Une étape dont les effets commencent une quête fermée attend (annoncé une fois).
        WorldSession step = InRoom("zone.auberge", book, WithQuest(Hero(), Peaux, "chasse", 3), step: false);
        Assert.Equal(new[] { $"QuestLocked {Peaux}/chasse" }, Steps(step, 4));
        Assert.Equal("chasse", step.Narrative!.CurrentStep(Peaux)!.Id);
    }

    // --- Quête suivie -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Suivre_une_quete()
    {
        var story = new SavedStory(null, new Dictionary<string, QuestProgress>
        {
            [Peaux] = new QuestProgress("chasse"), ["quest.traces_sous_le_moulin"] = new QuestProgress("traces"), [Bete] = new QuestProgress(QuestProgress.Done),
        });
        WorldSession w = InRoom("zone.maison", Shipped, Hero().WithStory(story));
        Assert.Null(w.Narrative!.Tracked);
        Assert.True(w.Track(Peaux));
        Assert.False(w.Track(Bete));   // finie
        Assert.False(w.Track("quest.inconnue"));
        Assert.Equal(Peaux, w.Narrative.Tracked);
        Assert.Equal(Peaux, w.Save().Character!.Story.Tracked);
        Assert.True(w.Track(null));
        Assert.Null(w.Narrative.Tracked);
    }

    // --- Sauvegarde en version 8 ----------------------------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_version_8_et_lecture_des_versions_7_et_6()
    {
        // Fusion de main après la PR #75 : la version 7 est celle du sous-lot V1.2 (18 places) ; la quête suivie et l'étape « reward »
        // passent en version 8.
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), WithQuest(Hero(), Peaux, "chasse", 2, Peaux)));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);
        Assert.Equal(11, SaveValidator.CurrentSchemaVersion);   // V1.3 : 9 (instances d'objets) ; V1.7 : 10 (guilde) ; V1.4 : 11 (qualité, usure)
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        Assert.Equal(text, SaveJsonReader.Write(SaveJsonReader.Read(text).Save!));
        // Sous-lot V1.3 : une sauvegarde de version 8 (même forme, sans instance aux propriétés) est lue, mise à jour et réécrite en version 9,
        // quête suivie comprise.
        SaveLoadResult v8 = SaveJsonReader.Read(SaveV11.Older(text).Replace(SaveVersion.Current, "\"schemaVersion\": 8"));
        Assert.True(v8.IsValid, v8.Report.ToString());
        Assert.True(v8.Save!.Migrated);
        Assert.Equal(text, SaveJsonReader.Write(v8.Save));
        // Versions 7 et 6 : sans quête suivie, mises à jour (réécrites en version 8, quête suivie nulle).
        string untracked = text.Replace($",\n        \"tracked\": \"{Peaux}\"\n", "\n");
        foreach (int version in new[] { 7, 6 })
        {
            string old = SaveV11.Older(untracked).Replace(SaveVersion.Current, $"\"schemaVersion\": {version}");
            SaveLoadResult read = SaveJsonReader.Read(old);
            Assert.True(read.IsValid, read.Report.ToString());
            Assert.True(read.Save!.Migrated);
            Assert.Null(read.Save.Character!.Story.Tracked);
            Assert.Equal(2, read.Save.Character.Story.Quests[Peaux].Progress);
            string rewritten = SaveJsonReader.Write(read.Save);
            Assert.Contains(SaveVersion.Current + ",", rewritten, StringComparison.Ordinal);
            Assert.Contains("\"tracked\": null\n", rewritten, StringComparison.Ordinal);
            // Quête suivie dans une version 7 ou 6 : champ inconnu, avec la version qui l'ajoute.
            ValidationError unknown = Assert.Single(SaveJsonReader.Read(SaveV11.Older(text).Replace(SaveVersion.Current, $"\"schemaVersion\": {version}")).Report.Errors);
            Assert.Equal(("$.data.character.story.tracked", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
            Assert.Contains("version 8", unknown.Message, StringComparison.Ordinal);
            // Étape réservée « reward » dans une version 7 ou 6 : refusée à l'étape de la quête.
            ValidationError reward = Assert.Single(SaveJsonReader.Read(old.Replace("{\"step\": \"chasse\", \"progress\": 2}", "{\"step\": \"reward\", \"progress\": 0}")).Report.Errors);
            Assert.Equal(($"$.data.character.story.quests.{Peaux}.step", ValidationCode.InvalidValue), (reward.Path, reward.Code));
        }
        // En version 8 : l'étape « reward » est lue ; quête suivie absente : manquante ; pas en cours : refusée ; version 10 refusée.
        SaveLoadResult choosing = SaveJsonReader.Read(untracked.Replace("{\"step\": \"chasse\", \"progress\": 2}", "{\"step\": \"reward\", \"progress\": 0}")
            .Replace("\"discoveries\": []\n", "\"discoveries\": [],\n        \"tracked\": null\n"));
        Assert.True(choosing.IsValid, choosing.Report.ToString());
        Assert.Equal(QuestProgress.Reward, choosing.Save!.Character!.Story.Quests[Peaux].Step);
        ValidationError missing = Assert.Single(SaveJsonReader.Read(untracked).Report.Errors);
        Assert.Equal(("$.data.character.story.tracked", ValidationCode.MissingField), (missing.Path, missing.Code));
        ValidationError absent = Assert.Single(SaveJsonReader.Read(text.Replace($"\"tracked\": \"{Peaux}\"", $"\"tracked\": \"{Bete}\"")).Report.Errors);
        Assert.Equal(("$.data.character.story.tracked", ValidationCode.UnknownReference), (absent.Path, absent.Code));
        ValidationError bad = Assert.Single(SaveJsonReader.Read(text.Replace($"\"tracked\": \"{Peaux}\"", "\"tracked\": \"Peaux\"")).Report.Errors);
        Assert.Equal(("$.data.character.story.tracked", ValidationCode.InvalidValue), (bad.Path, bad.Code));
        string done = text.Replace("{\"step\": \"chasse\", \"progress\": 2}", "{\"step\": \"done\", \"progress\": 0}");
        Assert.Equal("$.data.character.story.tracked", Assert.Single(SaveJsonReader.Read(done).Report.Errors).Path);
        ValidationError future = Assert.Single(SaveJsonReader.Read(text.Replace(SaveVersion.Current, SaveVersion.Future)).Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (future.Path, future.Code));
    }

    [Fact]
    public void Sauvegarde_de_version_7_reprise_sans_quete_suivie_et_places_remplies()
    {
        // Une partie de V1.2 (version 7) avec une quête en cours : reprise sans quête suivie (l'étoile de la carte revient à la première
        // principale en cours, comme avant) ; ses places obligatoires sont remplies (FillRequired, à chaque chargement) ; on peut suivre.
        string current = SaveJsonReader.Write(new WorldSave("zone.maison", 0, 0, Array.Empty<string>(), WithQuest(Hero(), Peaux, "chasse", 1)));
        string v7 = SaveV11.Older(current).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 7");
        SaveLoadResult read = SaveJsonReader.Read(v7);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.True(read.Save!.Migrated);
        var w = new WorldSession(read.Save, Inside("zone.maison"), ShippedData, Progression, ItemsCatalog, Shipped, door: "sortie", storyZone: "zone.vallons");
        Assert.Null(w.Narrative!.Tracked);
        Assert.True(w.Narrative.IsActive(Peaux));
        Assert.Contains("item.eclat_du_seuil_endormi", w.Restored);
        Assert.True(w.Track(Peaux));
        string rewritten = SaveJsonReader.Write(w.Save());
        Assert.Contains(SaveVersion.Current + ",", rewritten, StringComparison.Ordinal);
        Assert.Contains($"\"tracked\": \"{Peaux}\"\n", rewritten, StringComparison.Ordinal);
        Assert.True(SaveJsonReader.Read(rewritten).IsValid);
    }

    // --- Zones : chefs et familles -----------------------------------------------------------------------------------------

    [Fact]
    public void Chef_des_bandits_livre_a_deux_formes()
    {
        ZoneDefinition z = Zone();
        Assert.Equal(6, z.SchemaVersion);   // 5 au sous-lot V1.6 ; 6 depuis les réponses du 31 octobre 2026 (créature de la nuit)
        Boss chef = Assert.Single(z.Chiefs);
        Assert.Equal(("chef_des_bandits", "fact.chef_des_bandits.vaincu", "loot.chef_des_bandits"), (chef.Id, chef.Fact, chef.Loot!));
        Assert.Equal(new[] { "chef_des_bandits", "chef_des_bandits_furieux" }, chef.Forms.Select(f => f.Id));
        Assert.All(chef.Forms, f => Assert.Contains(f.Attacks, a => a.TelegraphMs > 0));   // chaque forme a une attaque annoncée
        Assert.Equal(new[] { "gardien_du_seuil", "chef_des_bandits" }, z.Bosses.Select(b => b.Id));
        Assert.Equal("zone.vallons/boss/chef_des_bandits", z.VictoryKeyOf(chef));
        Assert.Equal(z.VictoryKey, z.VictoryKeyOf(z.Boss!));
        // Les deux chefs arrivent avec la session.
        WorldSession w = Play(z, Hero());
        Assert.Equal(new[] { "gardien_du_seuil", "chef_des_bandits" }, w.BossForms);
        Assert.True(w.IsBossForm("chef_des_bandits_furieux") && w.IsBossForm("gardien_eveille") && !w.IsBossForm("bandit_epee1"));
        Assert.True(w.Zone.Engine.View("chef_des_bandits").IsPresent);
        // La victoire sur le chef des bandits apprend son fait (l'histoire peut l'attendre).
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement));
    }

    [Fact]
    public void Nouveaux_ennemis_annonces_et_butin()
    {
        ZoneDefinition z = Zone();
        foreach (string id in new[] { "bandit_epee1", "bandit_epee2", "bandit_arc1", "bandit_arc2", "loup_blanc" })
        {
            Population p = z.PopulationOf(id)!;
            Assert.NotNull(p.Template.Health);
            Assert.Contains(p.Template.Attacks, a => a.TelegraphMs > 0);
            Assert.True(ItemsCatalog.TryGetLoot(p.Loot!, out _), id);
        }
        Assert.True(z.PopulationOf("loup_blanc")!.Template.Health > z.PopulationOf("loup1")!.Template.Health * 2);   // une bête élite
    }

    [Fact]
    public void Nouveaux_ennemis_sans_bouclier_avec_les_regles_0_13()
    {
        // Fusion de main après la PR #75 (règles 0.13.0, garde des boucliers) : les bandits, leur chef et le loup blanc n'ont pas de
        // bouclier ; leur garde est celle des règles (Shield nul), et le Bastion entre avec la garde de son écu.
        // Fusion de main dans V1.3 : règles 0.14.0 (effets d'équipement) ; ces créatures n'ont pas d'équipement.
        // Sous-lot V1.8 : règles 0.15.0 (barre), rien de changé pour ces créatures.
        Assert.Equal("0.15.0-provisoire", ShippedData.Rules.ContentVersion);
        WorldSession w = Play(Zone(), Hero());
        Assert.NotNull(w.EnteredShield);
        string[] creatures = { "bandit_epee1", "bandit_epee2", "bandit_arc1", "bandit_arc2", "chef_des_bandits", "loup_blanc" };
        // Réponses du 31 octobre 2026 : le loup blanc n'est pas là au début d'une session (créature rare de la nuit).
        bool Present(string c) => w.Zone.Engine.CombatantIds.Contains(c) && w.Zone.Engine.View(c).IsPresent;
        foreach (string id in creatures.Where(Present))
            Assert.Null(w.Zone.Engine.View(id).Rules?.Shield);
        Assert.Contains(creatures, Present);
        // Quelques secondes de jeu : la session avance sans erreur (aucune garde de bouclier à contrôler pour eux).
        for (int i = 0; i < 200; i++) w.Step();
    }

    private static ZoneLoadResult ZoneWith(Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        change(root);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
    }

    [Fact]
    public void Chefs_et_familles_invalides_refuses_avec_leur_chemin()
    {
        JsonObject Chief(JsonObject r) => r["data"]!["chiefs"]![0]!.AsObject();
        void Refused(string path, ValidationCode code, Action<JsonObject> change)
        {
            ZoneLoadResult r = ZoneWith(change);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
        }
        Refused("$.data.chiefs[0].id", ValidationCode.DuplicateId, r => Chief(r)["id"] = "gardien_du_seuil");
        Refused("$.data.chiefs[0].forms[1]", ValidationCode.DuplicateId, r => Chief(r)["forms"]![1] = "chef_des_bandits");
        Refused("$.data.chiefs[0].forms[1]", ValidationCode.UnknownReference, r => Chief(r)["forms"]![1] = "chef_absent");
        Refused("$.data.chiefs[0].scenario", ValidationCode.InvalidValue, r => Chief(r)["scenario"] = "zones/absent");
        Refused("$.data.chiefs[0].fact", ValidationCode.InvalidValue, r => Chief(r)["fact"] = "chef");
        Refused("$.data.chiefs", ValidationCode.LimitExceeded, r =>
        {
            var many = new JsonArray();
            for (int i = 0; i < ZoneValidator.MaxChiefs + 1; i++) many.Add(Chief(r).DeepClone());
            r["data"]!["chiefs"] = many;
        });
        Refused("$.data.populations[27].family", ValidationCode.InvalidValue, r => r["data"]!["populations"]![27]!["family"] = "Bandit");
        Assert.Equal("bandit_epee1", (string)JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!["data"]!["populations"]![27]!["creature"]!);
        // Ancien schéma refusé.
        Refused("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion, r => r["schemaVersion"] = 4);
    }
}
