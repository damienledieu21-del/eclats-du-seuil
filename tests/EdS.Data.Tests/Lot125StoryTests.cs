using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.5 : PNJ, dialogues et quêtes. Textes par clés (Q37), histoire validée contre les textes, la zone et le catalogue ; PNJ qui se
/// souviennent (confiance, H1) ; conditions durables et temporaires (H3) ; mission « Les traces sous le moulin » (Q2) ; état narratif
/// personnel (H2) dans la sauvegarde en version 4.
/// </summary>
[Trait("Category", "World")]
public class Lot125StoryTests
{
    private const string Mission = "quest.traces_sous_le_moulin";

    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
    private static string StoryPath => StoryJsonReader.StoryPathIn(DataDirectory);

    /// <summary>Zone des Vallons, point d'entrée déplacé, scénario retouché au besoin (copié dans un dossier temporaire).</summary>
    private static ZoneDefinition Zone(double x = -18, double z = -12, Action<JsonObject>? scenario = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        string directory = ScenariosDirectory;
        string? temporary = null;
        if (scenario != null)
        {
            temporary = directory = Path.Combine(Path.GetTempPath(), "eds_story_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            scenario(s);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));   // sous-lot 12.7 : formes du chef
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);   // sous-lot V1.6 : formes du chef des bandits
        }
        try
        {
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, Items);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            return loaded.Zone!;
        }
        finally
        {
            if (temporary != null) Directory.Delete(temporary, true);
        }
    }

    private static StoryBook Book(ZoneDefinition zone) => StoryJsonReader.ReadStoryFile(StoryPath, Texts, zone, Items, V16TestData.Interiors).Story!;

    private static StoryLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryPath))!.AsObject();
        mutate(root);
        return StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), Items, V16TestData.Interiors);
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();
    private static JsonObject Dialogue(JsonObject root, int i) => Data(root)["dialogues"]![i]!.AsObject();
    private static JsonObject Step(JsonObject root, int i) => Data(root)["quests"]![0]!["steps"]![i]!.AsObject();

    private static string FirstError(StoryLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    private static SavedCharacter Hero(SavedStory? story = null, params ItemStack[] items) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>()).WithStory(story ?? SavedStory.Empty);

    private static SavedStory At(string step, long progress = 0, int odile = 0, params string[] facts) =>
        new(facts, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(step, progress) },
            odile == 0 ? null : new Dictionary<string, int> { ["npc.odile"] = odile });

    /// <summary>Partie ouverte sur la zone, un tick simulé (le personnage est arrivé).</summary>
    private static WorldSession Play(ZoneDefinition zone, SavedCharacter hero)
    {
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items, Book(zone));
        session.Step();
        return session;
    }

    // --- Données -----------------------------------------------------------------------------------

    [Fact]
    public void Recit_en_double_refuse_dans_une_sauvegarde_en_objets_publics()
    {
        // Même règle que la revue Codex de la PR #44 : une quête ou une confiance en double donne un rapport, pas une exception.
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("traces") },
            new Dictionary<string, int> { ["npc.odile"] = 1 });
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithStory(story)));
        SaveDocument Document()
        {
            var report = new ValidationReport();
            var document = new SaveDocument();
            JsonHeader header = JsonInput.Parse(JsonInput.Utf8(json, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
            (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
            return document;
        }
        SaveDocument quests = Document();
        quests.Data!.Character!.Story!.Quests!.Add(("quest.traces_sous_le_moulin", new QuestProgressData { Step = "pre", Progress = 0 }));
        Assert.Contains(SaveValidator.Validate(quests).Report.Errors, e => e.Path == "$.data.character.story.quests.quest.traces_sous_le_moulin");
        SaveDocument trust = Document();
        trust.Data!.Character!.Story!.Trust!.Add(new KeyValuePair<string, int?>("npc.odile", -1));
        Assert.Contains(SaveValidator.Validate(trust).Report.Errors, e => e.Path == "$.data.character.story.trust.npc.odile");
        Assert.True(SaveValidator.Validate(Document()).IsValid);
    }

    [Fact]
    public void Histoire_en_objets_publics_bornee_comme_a_la_lecture()
    {
        // Même règle que la revue Codex de la PR #44 : hors du lecteur JSON, une histoire renseignée en version 3 est un champ inconnu,
        // une histoire absente en version 4 manque (même marquée présente), la confiance garde sa borne ; le schéma suit les mêmes bornes.
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Progression)));
        SaveDocument Document(string text)
        {
            var report = new ValidationReport();
            var document = new SaveDocument();
            JsonHeader header = JsonInput.Parse(JsonInput.Utf8(text, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
            (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
            return document;
        }
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        root["schemaVersion"] = 3;
        root["data"]!["character"]!.AsObject().Remove("story");
        root["data"]!["character"]!.AsObject().Remove("professions");   // sous-lot 12.6 (version 5)
        root["data"]!["character"]!.AsObject().Remove("money");   // sous-lot V1.1 (version 6)
        root["data"]!["character"]!.AsObject().Remove("crafts");   // sous-lot V1.4 (version 11)
        string v3 = root.ToJsonString();
        Assert.True(SaveJsonReader.Read(v3).IsValid);
        SaveDocument old = Document(v3);
        old.Data!.Character!.Story = new StoryStateData { Facts = new List<string?>(), Quests = new List<(string, QuestProgressData?)>(), Trust = new List<KeyValuePair<string, int?>>(), Discoveries = new List<string?>() };
        Assert.Contains(SaveValidator.Validate(old).Report.Errors, e => e.Path == "$.data.character.story" && e.Code == ValidationCode.UnknownField);
        SaveDocument missing = Document(json);
        missing.Data!.Character!.Story = null;
        Assert.Contains(SaveValidator.Validate(missing).Report.Errors, e => e.Path == "$.data.character.story" && e.Code == ValidationCode.MissingField);
        SaveDocument trust = Document(json);
        trust.Data!.Character!.Story!.Trust = Enumerable.Range(0, SaveValidator.MaxStoryEntries + 1).Select(i => new KeyValuePair<string, int?>($"npc.p{i:D5}", 1)).ToList();
        Assert.Contains(SaveValidator.Validate(trust).Report.Errors, e => e.Path == "$.data.character.story.trust" && e.Code == ValidationCode.LimitExceeded);
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(json).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(json.Replace("\"trust\": {}", "\"trust\": {\"odile\": 1}")).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(json.Replace("\"quests\": {}", "\"quests\": {\"traces\": {\"step\": \"pre\", \"progress\": 0}}")).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(json.Replace("\"facts\": []", "\"facts\": [\"fact.a\", \"fact.a\"]")).RootElement));
    }

    [Fact]
    public void Textes_et_histoire_en_objets_publics_refuses_sans_exception()
    {
        // Même règle que la revue Codex de la PR #44 : une clé de texte en double est refusée ; un PNJ dont la position n'a qu'une
        // coordonnée donne un champ manquant, pas une exception.
        var texts = new TextsDocument
        {
            SchemaVersion = TextsValidator.CurrentSchemaVersion, Kind = TextsValidator.Kind, ContentVersion = "0.6.0",
            Data = new TextsData { Language = "fr", Entries = new List<KeyValuePair<string, string?>> { new("text.a", "un"), new("text.a", "deux") } },
        };
        Assert.Contains(TextsValidator.Validate(texts).Report.Errors, e => e.Path == "$.data.entries.text.a" && e.Code == ValidationCode.DuplicateId);
        var report = new ValidationReport();
        StoryDocument story = StoryJsonReader.ReadStoryDocument(JsonInput.Utf8(File.ReadAllText(StoryPath), report), report);
        Assert.True(report.IsValid);
        story.Data!.Npcs![0]!.Position = new EdS.Data.Scenarios.PointData { X = EdS.Domain.Numerics.Fixed.Zero };
        StoryLoadResult result = StoryValidator.Validate(story, Texts, Zone(), Items, null, V16TestData.Interiors);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.npcs[0].position" && e.Code == ValidationCode.MissingField);
    }

    [Fact]
    public void Champ_d_une_autre_nature_refuse_meme_renseigne_hors_du_lecteur()
    {
        // Revue Codex de la PR #46 : une étape, une valeur ou un identifiant que la nature n'admet pas est refusé même sans « Present »
        // (document en objets publics), comme dans le fichier.
        StoryDocument Read()
        {
            var report = new ValidationReport();
            StoryDocument d = StoryJsonReader.ReadStoryDocument(JsonInput.Utf8(File.ReadAllText(StoryPath), report), report);
            Assert.True(report.IsValid);
            return d;
        }
        TermData FirstCondition(StoryDocument d, string kind) => d.Data!.Dialogues!.SelectMany(x => x!.Entries!).SelectMany(e => e!.Conditions!).First(c => c!.Kind == kind)!;
        TermData FirstEffect(StoryDocument d, string kind) =>
            d.Data!.Dialogues!.SelectMany(x => x!.Nodes!).SelectMany(n => n!.Choices!).SelectMany(c => c!.Effects!).First(e => e!.Kind == kind)!;
        ZoneDefinition zone = Zone();
        StoryDocument step = Read();
        FirstCondition(step, "questActive").Step = "retour";
        Assert.Contains(StoryValidator.Validate(step, Texts, zone, Items, null, V16TestData.Interiors).Report.Errors, e => e.Path.EndsWith(".step", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
        StoryDocument value = Read();
        FirstEffect(value, "setFact").Value = 3;
        Assert.Contains(StoryValidator.Validate(value, Texts, zone, Items, null, V16TestData.Interiors).Report.Errors, e => e.Path.EndsWith(".value", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
        StoryDocument id = Read();
        TermData xp = id.Data!.Quests!.SelectMany(q => q!.Steps!).SelectMany(s => s!.Effects!).First(r => r!.Kind == "giveXp")!;
        xp.Id = "fact.x";
        Assert.Contains(StoryValidator.Validate(id, Texts, zone, Items, null, V16TestData.Interiors).Report.Errors, e => e.Path.EndsWith(".id", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
        Assert.True(StoryValidator.Validate(Read(), Texts, zone, Items, null, V16TestData.Interiors).IsValid);
    }

    [Fact]
    public void Tables_et_vues_de_l_histoire_non_modifiables()
    {
        // Les tables globales du validateur et les vues de l'histoire ne se modifient pas par conversion (même règle que la revue Codex de la PR #44).
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ConditionKind>)StoryValidator.Conditions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, EffectKind>)StoryValidator.Effects).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ObjectiveKind>)StoryValidator.Objectives).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)StoryValidator.Phases)[0] = "night");
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)Texts.Entries).Clear());
    }

    [Fact]
    public void Textes_et_histoire_livres()
    {
        TextsLoadResult texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory));
        Assert.True(texts.IsValid, texts.Report.ToString());
        Assert.Equal("fr", texts.Texts!.Language);
        Assert.Equal("Odile, la meunière", texts.Texts.Get("text.npc.odile.name"));
        Assert.Equal("text.inconnu", texts.Texts.Get("text.inconnu"));   // jamais d'exception à l'affichage
        StoryLoadResult story = StoryJsonReader.ReadStoryFile(StoryPath, texts.Texts, Zone(), Items, V16TestData.Interiors);
        Assert.True(story.IsValid, story.Report.ToString());
        StoryBook book = story.Story!;
        // V1.1 : les marchands du bourg ; V1.6 : l'aubergiste, le prêtre et l'habitante, dans leurs intérieurs.
        Assert.Equal(new[] { "npc.odile", "npc.maelle", "npc.gaspard", "npc.armurier", "npc.herboriste", "npc.epiciere", "npc.aubergiste", "npc.pretre", "npc.habitant" },
            book.Npcs.Select(n => n.Id));
        Quest mission = book.Quest(Mission);
        Assert.Equal(new[] { "enquete", "traces", "passage", "pre", "manifestation", "rodeur", "chef", "retour" }, mission.Steps.Select(s => s.Id));   // 12.7 : le chef
        Assert.Equal(4, book.Discoveries.Count);
        // Chaque texte livré sert, et chaque clé employée existe (le validateur le garantit dans ce sens).
        MiniJsonSchema Schema(string name) => MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", name));
        Assert.True(Schema("story.schema.json").IsValid(JsonDocument.Parse(File.ReadAllText(StoryPath)).RootElement));
        Assert.True(Schema("texts.schema.json").IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.TextsPathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Histoire_invalide_refusee_avec_son_chemin()
    {
        Assert.Equal("$.data.npcs[0].name", FirstError(Mutated(r => Data(r)["npcs"]![0]!["name"] = "text.inconnu")));
        Assert.Equal("$.data.npcs[0].dialogue", FirstError(Mutated(r => Data(r)["npcs"]![0]!["dialogue"] = "dialogue.inconnu")));
        Assert.Equal("$.data.npcs[1].id", FirstError(Mutated(r => Data(r)["npcs"]![1]!["id"] = "npc.odile")));
        Assert.Equal("$.data.npcs[0].position", FirstError(Mutated(r => Data(r)["npcs"]![0]!["position"] = new JsonArray(150, 0))));   // hors de l'aire agrandie
        Assert.Equal("$.data.dialogues[0].entries[0].node", FirstError(Mutated(r => Dialogue(r, 0)["entries"]![0]!["node"] = "nulle_part")));
        Assert.Equal("$.data.dialogues[0].nodes[0].choices[0].next", FirstError(Mutated(r => Dialogue(r, 0)["nodes"]![0]!["choices"]![0]!["next"] = "nulle_part")));
        Assert.Equal("$.data.dialogues[0].entries[0].conditions[0].if", FirstError(Mutated(r => Dialogue(r, 0)["entries"]![0]!["conditions"]![0]!["if"] = "chance")));
        Assert.Equal("$.data.dialogues[0].entries[1].conditions[0].step", FirstError(Mutated(r => Dialogue(r, 0)["entries"]![1]!["conditions"]![0]!["step"] = "nulle_part")));
        Assert.Equal("$.data.dialogues[1].entries[0].conditions[1].id", FirstError(Mutated(r => Dialogue(r, 1)["entries"]![0]!["conditions"]![1]!["id"] = "item.inconnu")));
        Assert.Equal("$.data.dialogues[1].entries[0].conditions[1].value", FirstError(Mutated(r => Dialogue(r, 1)["entries"]![0]!["conditions"]![1]!["value"] = 0)));
        Assert.Equal("$.data.dialogues[0].nodes[1].choices[0].effects[2].value",
            FirstError(Mutated(r => Dialogue(r, 0)["nodes"]![1]!["choices"]![0]!["effects"]![2]!["value"] = 101)));
        Assert.Equal("$.data.dialogues[0].nodes[1].choices[0].effects[0].id",
            FirstError(Mutated(r => Dialogue(r, 0)["nodes"]![1]!["choices"]![0]!["effects"]![0]!["id"] = "quest.inconnue")));
        Assert.Equal("$.data.dialogues[0].nodes[1].choices[0].effects[1].value",
            FirstError(Mutated(r => Dialogue(r, 0)["nodes"]![1]!["choices"]![0]!["effects"]![1]!["value"] = 1)));   // setFact sans valeur
        Assert.Equal("$.data.quests[0].steps[1].objective.id", FirstError(Mutated(r => Step(r, 1)["objective"]!["id"] = "foret")));
        Assert.Equal("$.data.quests[0].steps[5].objective.id", FirstError(Mutated(r => Step(r, 5)["objective"]!["id"] = "ours")));
        Assert.Equal("$.data.quests[0].steps[5].objective.value", FirstError(Mutated(r => Step(r, 5)["objective"]!["value"] = 0)));
        Assert.Equal("$.data.quests[0].steps[3].id", FirstError(Mutated(r => Step(r, 3)["id"] = "done")));                 // réservé
        Assert.Equal("$.data.quests[0].steps[1].id", FirstError(Mutated(r => Step(r, 1)["id"] = "enquete")));
        // Un fait attendu doit être appris quelque part.
        Assert.Equal("$.data.quests[0].steps[0].objective.id", FirstError(Mutated(r => Step(r, 0)["objective"]!["id"] = "fact.jamais.appris")));
        Assert.Equal("$.kind", FirstError(Mutated(r => r["kind"] = "texts")));
    }

    // --- Dialogues et confiance (H1) ---------------------------------------------------------------

    [Fact]
    public void Dialogue_d_Odile_et_debut_de_la_mission()
    {
        Assert.Equal(DialogueRefusal.TooFar, Play(Zone(), Hero()).Talk("npc.odile", out _));   // place du village : trop loin
        WorldSession session = Play(Zone(-9, 2), Hero());
        Assert.Equal(DialogueRefusal.NothingToSay, session.Talk("npc.inconnu", out _));
        Assert.Equal(DialogueRefusal.None, session.Talk("npc.odile", out DialogueView? accueil));
        Assert.Equal(("accueil", "text.odile.accueil"), (accueil!.Node, accueil.Text));
        Assert.Equal(DialogueRefusal.NoSuchChoice, session.Choose(3, out _, out _));
        Assert.Equal(DialogueRefusal.None, session.Choose(0, out DialogueView? recit, out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        Assert.Equal(new[] { "text.odile.recit.aider", "text.odile.recit.soupcon" }, recit!.Options.Select(o => o.Text));
        // La réponse soupçonneuse vexe Odile (confiance −1) : la mission commence quand même.
        Assert.Equal(DialogueRefusal.None, session.Choose(1, out DialogueView? end, out IReadOnlyList<StoryEvent> events));
        Assert.Null(end);
        Assert.Null(session.Narrative!.Open);
        Assert.Equal(new[] { "QuestStarted quest.traces_sous_le_moulin/enquete", "FactLearned fact.traces.recit", "FactLearned fact.odile.vexee",
            "TrustChanged npc.odile -1" }, events.Select(e => e.ToString()));
        // Au tick suivant, l'objectif de l'enquête (le récit appris) est rempli : étape suivante.
        Assert.Equal(new[] { "StepCompleted quest.traces_sous_le_moulin/enquete" }, session.Step().Story.Select(e => e.ToString()));
        Assert.Equal("traces", session.Narrative.CurrentStep(Mission)!.Id);
        session.Talk("npc.odile", out DialogueView? again);
        Assert.Equal("en_cours", again!.Node);
        // Tout est gardé dans la sauvegarde (version 4), et relu tel quel.
        SavedStory saved = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!.Character!.Story;
        Assert.Equal(new[] { "fact.odile.vexee", "fact.traces.recit" }, saved.Facts);
        Assert.Equal(new QuestProgress("traces"), saved.Quests[Mission]);
        Assert.Equal(-1, saved.Trust["npc.odile"]);
    }

    [Fact]
    public void Maelle_forge_la_ferrure_et_ouvre_le_passage()
    {
        ZoneDefinition zone = Zone(-19, -5);
        // Sans ferrure, Maëlle dit de la forger ; si Odile a confiance, elle donne en plus un indice (conséquence d'un choix passé, H1).
        Play(zone, Hero(At("passage", odile: -1))).Talk("npc.maelle", out DialogueView? demande);
        Assert.Equal("minerai", demande!.Node);
        Play(zone, Hero(At("passage", odile: 1))).Talk("npc.maelle", out DialogueView? indice);
        Assert.Equal("indice", indice!.Node);
        // Sous-lot 12.6 : avec une ferrure forgée, Maëlle la pose ; le passage du moulin est ouvert pour de bon, l'étape franchie au tick suivant.
        WorldSession session = Play(zone, Hero(At("passage"), new ItemStack("item.ferrure", 1), new ItemStack("item.minerai_de_fer", 1)));
        session.Talk("npc.maelle", out DialogueView? ferrure);
        Assert.Equal("ferrure", ferrure!.Node);
        Assert.Equal(DialogueRefusal.None, session.Choose(0, out DialogueView? faite, out IReadOnlyList<StoryEvent> events));
        Assert.Equal("faite", faite!.Node);
        Assert.Empty(faite.Options);
        Assert.Contains(events, e => e.Kind == StoryEventKind.PassageOpened && e.Id == "moulin");
        Assert.Equal((0, 1), (session.Inventory.Count("item.ferrure"), session.Inventory.Count("item.minerai_de_fer")));
        Assert.True(session.IsPassageOpen("moulin"));
        long xp = session.Character.Xp;
        Assert.Equal(new[] { "StepCompleted quest.traces_sous_le_moulin/passage", "XpGiven xp 60", "MoneyGiven money 50" }, session.Step().Story.Select(e => e.ToString()));   // V1.1 : 50 cuivre
        Assert.Equal(xp + 60, session.Character.Xp);
        Assert.Contains("zone.vallons/passage/moulin", session.Save().Consumed);
    }

    [Fact]
    public void Retour_chez_Odile_selon_sa_confiance()
    {
        ZoneDefinition zone = Zone(-9, 2);
        // Confiance gagnée : l'amulette de l'aube en remerciement.
        WorldSession warm = Play(zone, Hero(At("retour", odile: 1)));
        warm.Talk("npc.odile", out DialogueView? retour);
        Assert.Equal(new[] { "text.odile.retour.confiance" }, retour!.Options.Select(o => o.Text));
        Assert.Equal(DialogueRefusal.None, warm.Choose(retour.Options[0].Index, out _, out IReadOnlyList<StoryEvent> given));
        Assert.Contains(given, e => e.Kind == StoryEventKind.ItemGiven && e.Id == "item.amulette_de_l_aube");
        Assert.Equal(1, warm.Inventory.Count("item.amulette_de_l_aube"));
        Assert.Equal(new[] { "StepCompleted quest.traces_sous_le_moulin/retour", "XpGiven xp 300", "MoneyGiven money 500", "QuestCompleted quest.traces_sous_le_moulin" },   // V1.1 : 5 argent
            warm.Step().Story.Select(e => e.ToString()));
        Assert.True(warm.Narrative!.IsDone(Mission));
        warm.Talk("npc.odile", out DialogueView? apres);
        Assert.Equal("apres", apres!.Node);
        Assert.Equal(QuestProgress.Done, warm.Save().Character!.Story.Quests[Mission].Step);

        // Confiance perdue : un merci sec, pas d'amulette.
        WorldSession cold = Play(zone, Hero(At("retour", odile: -1)));
        cold.Talk("npc.odile", out DialogueView? froid);
        Assert.Equal(new[] { "text.odile.retour.froid" }, froid!.Options.Select(o => o.Text));
        cold.Choose(froid.Options[0].Index, out _, out _);
        Assert.Equal(0, cold.Inventory.Count("item.amulette_de_l_aube"));

        // Inventaire plein : la réponse est refusée en entier (ni fait, ni objet), le dialogue reste ouvert.
        WorldSession full = Play(zone, Hero(At("retour", odile: 1), Enumerable.Repeat(new ItemStack("item.gambison", 1), 24).ToArray()));
        full.Talk("npc.odile", out DialogueView? plein);
        Assert.Equal(DialogueRefusal.NoRoom, full.Choose(plein!.Options[0].Index, out _, out IReadOnlyList<StoryEvent> nothing));
        Assert.Empty(nothing);
        Assert.False(full.Narrative!.Knows("fact.odile.retour"));
        Assert.NotNull(full.Narrative.Open);
    }

    [Fact]
    public void Recompense_de_dialogue_donnee_une_seule_fois_avant_le_tick_suivant()
    {
        // Revue Codex de la PR #46 : l'étape se franchit au tick suivant ; d'ici là, reparler au PNJ ne redonne ni l'amulette, ni la
        // ferrure, ni la confiance de Gaspard.
        WorldSession odile = Play(Zone(-9, 2), Hero(At("retour", odile: 1)));
        odile.Talk("npc.odile", out DialogueView? retour);
        Assert.Equal(DialogueRefusal.None, odile.Choose(retour!.Options[0].Index, out _, out _));
        odile.Talk("npc.odile", out DialogueView? encore);
        Assert.Equal("en_cours", encore!.Node);
        Assert.Equal(1, odile.Inventory.Count("item.amulette_de_l_aube"));

        WorldSession maelle = Play(Zone(-19, -5), Hero(At("passage", odile: 1), new ItemStack("item.ferrure", 2)));
        maelle.Talk("npc.maelle", out DialogueView? ferrure);
        Assert.Equal("ferrure", ferrure!.Node);
        Assert.Equal(DialogueRefusal.None, maelle.Choose(0, out _, out _));
        maelle.Talk("npc.maelle", out DialogueView? apres);
        Assert.Equal("bonjour", apres!.Node);
        Assert.Equal(1, maelle.Inventory.Count("item.ferrure"));

        WorldSession gaspard = Play(Zone(26, -13), Hero(At("rodeur")));
        gaspard.Talk("npc.gaspard", out DialogueView? rodeur);
        Assert.Equal("rodeur", rodeur!.Node);
        Assert.Equal(DialogueRefusal.None, gaspard.Choose(rodeur.Options[0].Index, out _, out _));
        gaspard.Talk("npc.gaspard", out DialogueView? garde);
        Assert.Equal("garde", garde!.Node);
        Assert.Equal(1, gaspard.Narrative!.Trust("npc.gaspard"));
    }

    [Fact]
    public void Passage_sans_place_au_registre_refuse_en_entier()
    {
        // Revue Codex de la PR #46 : registre du monde plein, passage fermé : la ferrure est refusée en entier (ferrure gardée, passage
        // fermé, aucun fait), le dialogue reste ouvert.
        WorldSession session = Play(Zone(-19, -5), Hero(At("passage"), new ItemStack("item.ferrure", 1)));
        for (int i = 0; session.Zone.RegistryRoom > 0; i++) session.Zone.Consume($"zone.vallons/test/k{i}");
        session.Talk("npc.maelle", out DialogueView? ferrure);
        Assert.Equal("ferrure", ferrure!.Node);
        Assert.Equal(DialogueRefusal.NoRoom, session.Choose(0, out _, out IReadOnlyList<StoryEvent> nothing));
        Assert.Empty(nothing);
        Assert.Equal(1, session.Inventory.Count("item.ferrure"));
        Assert.False(session.IsPassageOpen("moulin"));
        Assert.False(session.Narrative!.Knows("fact.moulin.porte_ouverte"));
        Assert.NotNull(session.Narrative.Open);
    }

    // --- Objectifs ---------------------------------------------------------------------------------

    [Fact]
    public void Atteindre_un_lieu_et_vaincre_une_creature()
    {
        // Entrée au moulin, étape « suivre les traces » : atteinte dès l'arrivée, avec sa découverte.
        WorldSession moulin = new(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(At("traces"))), Zone(-8, 5), ShippedData, Progression, Items,
            Book(Zone(-8, 5)));
        List<StoryEvent> events = moulin.Step().Story.ToList();
        Assert.Equal(new[] { "StepCompleted quest.traces_sous_le_moulin/traces", "Discovered discovery.traces_griffues" }, events.Select(e => e.ToString()));

        // Étape « rodeur » : le rôdeur affaibli près de l'entrée ; sa mort compte pour la population « rodeur ».
        ZoneDefinition zone = Zone(-18, -12, s =>
        {
            JsonObject rodeur = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "rodeur");
            rodeur["health"] = 40;
            rodeur["position"] = new JsonArray(-16, -12);
            rodeur["behavior"] = new JsonObject { ["maxRange"] = 0.5, ["aggroRange"] = 9, ["pack"] = false };   // au contact, inoffensif
            foreach (JsonNode? a in rodeur["attacks"]!.AsArray()) a!["rawDamage"] = 1;
        });
        WorldSession hunt = Play(zone, Hero(At("rodeur")));
        var story = new List<StoryEvent>();
        for (int i = 0; i < 600 && story.Count == 0; i++)
        {
            if (i % 20 == 1)
                try { hunt.Zone.Engine.Submit(new CombatCommand(hunt.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "rodeur")); }
                catch (ArgumentException) { }
            story.AddRange(hunt.Step().Story);
        }
        Assert.Equal(new[] { "StepCompleted quest.traces_sous_le_moulin/rodeur", "MoneyGiven money 150" }, story.Select(e => e.ToString()));   // V1.1 : 1 argent 50 cuivre
        Assert.Equal("chef", hunt.Narrative!.CurrentStep(Mission)!.Id);   // sous-lot 12.7 : le chef avant le retour
    }

    // --- Sauvegarde, version 4 ---------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_de_l_histoire()
    {
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.odile.vexee" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("rodeur", 0), ["quest.autre"] = new QuestProgress(QuestProgress.Done) },
            new Dictionary<string, int> { ["npc.odile"] = -1, ["npc.maelle"] = 2, ["npc.gaspard"] = 0 }, new[] { "discovery.manifestation" });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), Hero(story)));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);   // sous-lot V1.1 : version 6 (bourse) ; V1.2 : 7 (18 places) ; V1.6 : 8 (quête suivie) ; V1.3 : 9 (instances d'objets)
        Assert.Contains("      \"story\": {\n        \"facts\": [\"fact.odile.vexee\", \"fact.traces.recit\"],\n"
            + "        \"quests\": {\"quest.autre\": {\"step\": \"done\", \"progress\": 0}, \"quest.traces_sous_le_moulin\": {\"step\": \"rodeur\", \"progress\": 0}},\n"
            + "        \"trust\": {\"npc.maelle\": 2, \"npc.odile\": -1},\n        \"discoveries\": [\"discovery.manifestation\"],\n"
            + "        \"tracked\": null\n      },\n", text, StringComparison.Ordinal);   // sous-lot V1.6 : quête suivie
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(text, SaveJsonReader.Write(read.Save!));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // Version 3 : mise à jour, histoire vierge ; une histoire en version 3 est un champ inconnu.
        static string CutFrom(string t, string field)
        {
            int a = t.IndexOf(",\n      \"" + field + "\"", StringComparison.Ordinal), b = t.IndexOf("\n    },", a, StringComparison.Ordinal);
            return t.Remove(a, b - a);
        }
        string v3 = CutFrom(text, "story").Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 3");
        SaveLoadResult migrated = SaveJsonReader.Read(v3);
        Assert.True(migrated.IsValid, migrated.Report + "\n" + v3);
        Assert.True(migrated.Save!.Migrated);
        Assert.Empty(migrated.Save.Character!.Story.Facts);
        Assert.Equal("$.data.character.story", Assert.Single(SaveJsonReader.Read(CutFrom(text, "professions").Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 3")).Report.Errors).Path);
        // Une histoire sauvegardée doit aller avec les données : quête inconnue refusée à la reprise.
        Assert.Throws<ArgumentException>(() => Play(Zone(), Hero(story)));
    }

    [Theory]
    [InlineData("\"fact.odile.vexee\", \"fact.traces.recit\"", "\"fact.traces.recit\", \"fact.odile.vexee\"", "$.data.character.story.facts[1]")]
    [InlineData("\"fact.odile.vexee\"", "\"odile\"", "$.data.character.story.facts[0]")]
    [InlineData("{\"step\": \"rodeur\", \"progress\": 0}", "{\"step\": \"rodeur\", \"progress\": -1}", "$.data.character.story.quests.quest.traces_sous_le_moulin.progress")]
    [InlineData("\"npc.odile\": -1", "\"npc.odile\": 101", "$.data.character.story.trust.npc.odile")]
    [InlineData("\"npc.odile\": -1", "\"npc.odile\": 0", "$.data.character.story.trust.npc.odile")]
    [InlineData("\"discoveries\": [\"discovery.manifestation\"]", "\"discoveries\": [\"manifestation\"]", "$.data.character.story.discoveries[0]")]
    public void Sauvegarde_d_histoire_invalide(string from, string to, string path)
    {
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.odile.vexee" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("rodeur") }, new Dictionary<string, int> { ["npc.odile"] = -1 },
            new[] { "discovery.manifestation" });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), Hero(story)));
        Assert.Contains(from, text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text.Replace(from, to, StringComparison.Ordinal));
        Assert.False(read.IsValid);
        Assert.Equal(path, read.Report.Errors[0].Path);
    }
}
