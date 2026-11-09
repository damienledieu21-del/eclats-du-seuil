using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.7 (réputation) : quatre factions (villageois, garde, rôdeurs, bandits), sept paliers de « haï » à « héros » ; conditions
/// <c>reputationAtLeast</c>, <c>reputationBelow</c>, <c>standing</c>, <c>standingAtLeast</c> et effet <c>reputation</c> (histoire en schéma
/// 12, section <c>reputation</c> : liens entre factions, gestes du monde, victoires, événements, prix) ; sauvegarde v15
/// (<c>story.reputation</c> ; v14 encore lue). Valeurs provisoires (docs/MONDE.md, « Réputation »).
/// </summary>
[Trait("Category", "World")]
public class V27ReputationTests
{
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    /// <summary>À côté de Colin, dans sa cachette (8 h du matin).</summary>
    private const double ColinX = 66.5, ColinZ = -30;

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    private static SavedCharacter With(SavedCharacter hero, IReadOnlyDictionary<string, int> reputation, IEnumerable<string>? facts = null,
        IReadOnlyDictionary<string, QuestProgress>? quests = null) =>
        hero.WithStory(new SavedStory(facts, quests, reputation: reputation));

    private static List<string> Steps(WorldSession w, int ticks = 2)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    private static void AssertError(ValidationReport report, string path, ValidationCode code) =>
        Assert.Contains(report.Errors, e => e.Path == path && e.Code == code);

    // --- Paliers -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1000, "hated")]
    [InlineData(-600, "hated")]
    [InlineData(-599, "hostile")]
    [InlineData(-300, "hostile")]
    [InlineData(-299, "wary")]
    [InlineData(-100, "wary")]
    [InlineData(-99, "neutral")]
    [InlineData(0, "neutral")]
    [InlineData(99, "neutral")]
    [InlineData(100, "liked")]
    [InlineData(299, "liked")]
    [InlineData(300, "honored")]
    [InlineData(599, "honored")]
    [InlineData(600, "hero")]
    [InlineData(1000, "hero")]
    [InlineData(int.MaxValue, "hero")]
    [InlineData(int.MinValue, "hated")]
    public void Paliers_aux_frontieres(int value, string tier) => Assert.Equal(tier, Reputations.Tiers[Reputations.TierOf(value)]);

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal((13, 11), (StoryValidator.CurrentSchemaVersion, StoryValidator.Schema11));
        Assert.Equal((17, 15, 14), (SaveValidator.CurrentSchemaVersion, SaveValidator.ReputationSince, SaveValidator.ScarsSince));
        Assert.Equal((13, "0.24.0-provisoire"), (Story.Value.SchemaVersion, Story.Value.ContentVersion));
        Assert.Equal(new[] { "villagers", "guard", "rangers", "bandits" }, Reputations.Factions);
        Assert.Equal(7, Reputations.Tiers.Count);
        // Ni le moteur, ni le journal, ni les scénarios.
        Assert.Equal(("0.30.0", 24, 17), (EdS.Simulation.Engine.CombatEngine.Version, EdS.Simulation.Engine.CombatEngine.LogFormatVersion, EdS.Data.Scenarios.ScenarioValidator.CurrentSchemaVersion));
    }

    // --- Données livrées -----------------------------------------------------------------------------------------------

    [Fact]
    public void Regles_livrees_et_chaque_faction_lue_par_une_quete_et_des_repliques()
    {
        StoryBook book = Story.Value;
        ReputationRules r = book.Reputation;
        Assert.Equal(new[] { 130, 120, 110, 100, 95, 90, 80 }, r.Prices);
        Assert.Equal(new[] { "contract", "fence", "questMain", "questSide", "scarPurified" }, r.Acts.Keys);
        Assert.Equal(new[] { ("villagers", -25), ("guard", -50) }, r.LinksOf("bandits").Select(c => (c.Faction, c.Value)));
        Assert.Equal(new[] { ("guard", 3), ("bandits", -4) }, r.DefeatOf("bandit_epee1", "bandit").Select(c => (c.Faction, c.Value)));
        Assert.Equal(new[] { ("villagers", 40), ("guard", 15) }, r.EventOf("attaque_hameau", won: true).Select(c => (c.Faction, c.Value)));
        Assert.Empty(r.EventOf("chasse_de_l_aube", won: true));
        // Chaque faction : au moins une quête qui la demande et des répliques selon son palier.
        static bool Reads(StoryCondition c, string f) => c.Id == f && c.Kind is ConditionKind.Standing or ConditionKind.StandingAtLeast
            or ConditionKind.ReputationAtLeast or ConditionKind.ReputationBelow;
        foreach (string f in Reputations.Factions)
        {
            Assert.Contains(book.Quests, q => q.Id.StartsWith("quest.v27.", StringComparison.Ordinal) && q.Requires.Any(c => Reads(c, f)));
            Assert.True(book.Dialogues.SelectMany(d => d.Nodes).SelectMany(n => n.Lines).Count(l => l.Conditions.Any(c => Reads(c, f))) >= 4, f);
            Assert.True(Texts.Has(Reputations.FactionText(f)), f);
        }
        Assert.All(Reputations.Tiers, t => Assert.True(Texts.Has(Reputations.TierText(t)), t));
        Assert.Equal("Héros", Texts.Get(Reputations.TierText("hero")));
        // Le choix de Colin : l'aider plaît aux bandits, le livrer plaît à la garde.
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    // --- Validation de l'histoire --------------------------------------------------------------------------------------

    [Fact]
    public void Histoire_en_schema_11_encore_lue_sans_la_reputation()
    {
        JsonObject eleven = StoryJson();
        eleven["schemaVersion"] = 11;
        StoryLoadResult refused = Read(eleven);
        Assert.False(refused.IsValid);
        AssertError(refused.Report, "$.data.reputation", ValidationCode.InvalidValue);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".if", StringComparison.Ordinal) && e.Message.Contains("standing"));
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".do", StringComparison.Ordinal) && e.Message.Contains("reputation"));
        V27TestData.WithoutReputation(eleven);
        StoryLoadResult read = Read(eleven);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(11, read.Story!.SchemaVersion);
        Assert.True(read.Story.Reputation.IsEmpty);
        Assert.Equal(100, read.Story.Reputation.PricePercent(0));
    }

    [Theory]
    [InlineData("{\"if\": \"standing\", \"id\": \"pirates\", \"step\": \"hero\"}", ".id", ValidationCode.UnknownReference)]
    [InlineData("{\"if\": \"standing\", \"id\": \"guard\", \"step\": \"roi\"}", ".step", ValidationCode.UnknownReference)]
    [InlineData("{\"if\": \"standing\", \"id\": \"guard\"}", ".step", ValidationCode.MissingField)]
    [InlineData("{\"if\": \"standing\", \"id\": \"guard\", \"step\": \"hero\", \"value\": 3}", ".value", ValidationCode.InvalidValue)]
    [InlineData("{\"if\": \"reputationAtLeast\", \"id\": \"guard\", \"value\": 1001}", ".value", ValidationCode.OutOfRange)]
    [InlineData("{\"if\": \"reputationBelow\", \"id\": \"guard\", \"value\": 10, \"step\": \"hero\"}", ".step", ValidationCode.InvalidValue)]
    [InlineData("{\"if\": \"reputationBelow\", \"id\": \"guard\"}", ".value", ValidationCode.MissingField)]
    public void Condition_de_reputation_refusee_au_chemin_precis(string condition, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        int i = quests.Select((q, k) => ((string?)q!["id"], k)).Single(x => x.Item1 == "quest.v27.ronde_du_capitaine").k;
        quests[i]!["requires"]![0] = JsonNode.Parse(condition);
        AssertError(Read(root).Report, $"$.data.quests[{i}].requires[0]{suffix}", code);
    }

    [Theory]
    [InlineData("{\"do\": \"reputation\", \"id\": \"pirates\", \"value\": 5}", ".id", ValidationCode.UnknownReference)]
    [InlineData("{\"do\": \"reputation\", \"id\": \"guard\", \"value\": 0}", ".value", ValidationCode.InvalidValue)]
    [InlineData("{\"do\": \"reputation\", \"id\": \"guard\", \"value\": -1001}", ".value", ValidationCode.OutOfRange)]
    [InlineData("{\"do\": \"reputation\", \"id\": \"guard\"}", ".value", ValidationCode.MissingField)]
    public void Effet_de_reputation_refuse_au_chemin_precis(string effect, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        int i = quests.Select((q, k) => ((string?)q!["id"], k)).Single(x => x.Item1 == "quest.v27.ronde_du_capitaine").k;
        JsonArray effects = quests[i]!["effects"]!.AsArray();
        effects[effects.Count - 1] = JsonNode.Parse(effect);
        AssertError(Read(root).Report, $"$.data.quests[{i}].effects[{effects.Count - 1}]{suffix}", code);
    }

    [Theory]
    [InlineData("acts", "{\"danse\": {\"villagers\": 5}}", "$.data.reputation.acts.danse", ValidationCode.UnknownReference)]
    [InlineData("acts", "{\"questMain\": {\"pirates\": 5}}", "$.data.reputation.acts.questMain.pirates", ValidationCode.UnknownReference)]
    [InlineData("acts", "{\"questMain\": {\"villagers\": 201}}", "$.data.reputation.acts.questMain.villagers", ValidationCode.OutOfRange)]
    [InlineData("acts", "{\"questMain\": {\"villagers\": 0}}", "$.data.reputation.acts.questMain.villagers", ValidationCode.OutOfRange)]
    [InlineData("acts", "{\"questMain\": {}}", "$.data.reputation.acts.questMain", ValidationCode.OutOfRange)]
    [InlineData("links", "{\"guard\": {\"guard\": 10}}", "$.data.reputation.links.guard.guard", ValidationCode.InvalidValue)]
    [InlineData("links", "{\"guard\": {\"bandits\": -101}}", "$.data.reputation.links.guard.bandits", ValidationCode.OutOfRange)]
    [InlineData("links", "{\"pirates\": {\"guard\": 10}}", "$.data.reputation.links.pirates", ValidationCode.UnknownReference)]
    [InlineData("defeated", "{\"dragon\": {\"rangers\": 1}}", "$.data.reputation.defeated.dragon", ValidationCode.UnknownReference)]
    [InlineData("events", "{\"fete\": {\"won\": {\"villagers\": 1}}}", "$.data.reputation.events.fete", ValidationCode.UnknownReference)]
    [InlineData("events", "{\"attaque_hameau\": {}}", "$.data.reputation.events.attaque_hameau", ValidationCode.MissingField)]
    [InlineData("events", "{\"attaque_hameau\": {\"won\": {\"villagers\": 1}, \"tard\": 3}}", "$.data.reputation.events.attaque_hameau.tard", null)]
    [InlineData("prices", "{\"hated\": 130}", "$.data.reputation.prices.hero", ValidationCode.MissingField)]
    [InlineData("prices", "{\"hated\": 201, \"hostile\": 120, \"wary\": 110, \"neutral\": 100, \"liked\": 95, \"honored\": 90, \"hero\": 80}", "$.data.reputation.prices.hated", ValidationCode.OutOfRange)]
    [InlineData("prices", "{\"roi\": 100, \"hated\": 130, \"hostile\": 120, \"wary\": 110, \"neutral\": 100, \"liked\": 95, \"honored\": 90, \"hero\": 80}", "$.data.reputation.prices.roi", ValidationCode.UnknownReference)]
    public void Section_reputation_refusee_au_chemin_precis(string field, string json, string path, ValidationCode? code)
    {
        JsonObject root = StoryJson();
        root["data"]!["reputation"]![field] = JsonNode.Parse(json);
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    // --- Sauvegarde v15 ------------------------------------------------------------------------------------------------

    private static SavedCharacter Saved() => With(Hero(10), new Dictionary<string, int> { ["villagers"] = 120, ["bandits"] = -40, ["guard"] = 0 });

    [Fact]
    public void Sauvegarde_v15_aller_retour_et_v14_mise_a_jour()
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), Saved()));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);
        Assert.Contains("        \"reputation\": {\"bandits\": -40, \"villagers\": 120},\n", text, StringComparison.Ordinal);   // nulle : pas écrite ; ordre ordinal
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(new Dictionary<string, int> { ["bandits"] = -40, ["villagers"] = 120 }, read.Save!.Character!.Story.Reputation);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // Version 14 : sans réputation (neutre partout), mise à jour ; la réputation y est un champ inconnu.
        string v14 = SaveV11.V14(text).Replace(SaveVersion.Current, "\"schemaVersion\": 14", StringComparison.Ordinal);
        SaveLoadResult old = SaveJsonReader.Read(v14);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Migrated);
        Assert.Empty(old.Save.Character!.Story.Reputation);
        Assert.Contains("        \"reputation\": {},\n", SaveJsonReader.Write(old.Save), StringComparison.Ordinal);
        ValidationError unknown = Assert.Single(SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 14", StringComparison.Ordinal)).Report.Errors);
        Assert.Equal(("$.data.character.story.reputation", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
        // Version 15 sans réputation : champ obligatoire.
        ValidationError missing = Assert.Single(SaveJsonReader.Read(SaveV11.V14(text)).Report.Errors);
        Assert.Equal(("$.data.character.story.reputation", ValidationCode.MissingField), (missing.Path, missing.Code));
    }

    [Theory]
    [InlineData("{\"pirates\": 5}", "$.data.character.story.reputation.pirates", ValidationCode.UnknownReference)]
    [InlineData("{\"guard\": 0}", "$.data.character.story.reputation.guard", ValidationCode.OutOfRange)]
    [InlineData("{\"guard\": 1001}", "$.data.character.story.reputation.guard", ValidationCode.OutOfRange)]
    [InlineData("{\"guard\": -1001}", "$.data.character.story.reputation.guard", ValidationCode.OutOfRange)]
    [InlineData("{\"villagers\": 5, \"guard\": 5}", "$.data.character.story.reputation.guard", ValidationCode.InvalidValue)]
    [InlineData("{\"guard\": \"haut\"}", "$.data.character.story.reputation.guard", null)]
    [InlineData("[]", "$.data.character.story.reputation", null)]
    public void Reputation_sauvegardee_refusee_au_chemin_precis(string json, string path, ValidationCode? code)
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), Saved()));
        string bad = text.Replace("\"reputation\": {\"bandits\": -40, \"villagers\": 120}", "\"reputation\": " + json, StringComparison.Ordinal);
        SaveLoadResult r = SaveJsonReader.Read(bad);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    [Fact]
    public void Ecriture_refusee_hors_des_bornes()
    {
        SavedCharacter hero = With(Hero(), new Dictionary<string, int> { ["guard"] = 5000 });
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), hero)));
        SavedCharacter pirate = With(Hero(), new Dictionary<string, int> { ["pirates"] = 5 });
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), pirate)));
    }

    // --- Effets, liens et paliers en jeu -------------------------------------------------------------------------------

    private static WorldSession AtColin(IReadOnlyDictionary<string, int> reputation, string step = "choix")
    {
        SavedCharacter hero = With(Hero(), reputation, quests: new Dictionary<string, QuestProgress> { ["quest.deserteur"] = new QuestProgress(step) });
        return Play(Zone(ColinX, ColinZ), hero);
    }

    [Fact]
    public void Aider_Colin_plait_aux_bandits_et_fache_la_garde_et_le_village()
    {
        WorldSession w = AtColin(new Dictionary<string, int>());
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.deserteur", out DialogueView? view));
        Assert.Equal("ch4_colin_choix", view!.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out _, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(new[] { "ReputationChanged bandits 50", "ReputationChanged villagers/link -12", "ReputationChanged guard/link -25" },
            events.Where(e => e.Kind == StoryEventKind.ReputationChanged).Select(e => e.ToString()));
        Assert.Equal((50, -25, -12, 0), (w.Narrative!.Reputation("bandits"), w.Narrative.Reputation("guard"), w.Narrative.Reputation("villagers"), w.Narrative.Reputation("rangers")));
        Assert.Equal("neutral", w.Narrative.Standing("bandits"));
        // Sauvegardé, relu : la même réputation.
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal(new Dictionary<string, int> { ["bandits"] = 50, ["guard"] = -25, ["villagers"] = -12 }, save.Character!.Story.Reputation);
    }

    [Fact]
    public void Livrer_Colin_plait_a_la_garde_bornee_et_change_de_palier()
    {
        WorldSession w = AtColin(new Dictionary<string, int> { ["guard"] = 980, ["villagers"] = 95, ["bandits"] = -995 });
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.deserteur", out _));
        Assert.Equal(DialogueRefusal.None, w.Choose(1, out _, out IReadOnlyList<StoryEvent> events));
        // +40 à la garde, borné à 1000 (+20 réels) ; ses liens suivent le changement réel : +5 au village (passe « apprécié »), −10 aux bandits (bornés : −5).
        Assert.Equal(new[] { "ReputationChanged guard 20", "ReputationChanged villagers/link 5", "StandingChanged villagers/liked 1", "ReputationChanged bandits/link -5" },
            events.Where(e => e.Kind is StoryEventKind.ReputationChanged or StoryEventKind.StandingChanged).Select(e => e.ToString()));
        Assert.Equal((1000, 100, -1000), (w.Narrative!.Reputation("guard"), w.Narrative.Reputation("villagers"), w.Narrative.Reputation("bandits")));
        Assert.Equal(("hero", "liked", "hated"), (w.Narrative.Standing("guard"), w.Narrative.Standing("villagers"), w.Narrative.Standing("bandits")));
    }

    [Fact]
    public void Colin_propose_le_mot_de_passe_selon_les_bandits()
    {
        var facts = new[] { "fact.ch4.deserteur.aide", "fact.ch4.deserteur.decide", "fact.met.deserteur" };
        var done = new Dictionary<string, QuestProgress> { ["quest.deserteur"] = new QuestProgress(QuestProgress.Done) };
        // Neutre ou mieux : l'offre.
        WorldSession friend = Play(Zone(ColinX, ColinZ), With(Hero(), new Dictionary<string, int> { ["bandits"] = 50 }, facts, done));
        Assert.Equal(DialogueRefusal.None, friend.Talk("npc.deserteur", out DialogueView? offer));
        Assert.Equal("v27_deserteur_offre", offer!.Node);
        Assert.Equal(DialogueRefusal.None, friend.Choose(0, out _, out IReadOnlyList<StoryEvent> started));
        Assert.Contains(started, e => e.Kind == StoryEventKind.QuestStarted && e.Id == "quest.v27.mot_de_passe");
        // Méfiant : pas d'offre, et Colin le dit.
        WorldSession foe = Play(Zone(ColinX, ColinZ), With(Hero(), new Dictionary<string, int> { ["bandits"] = -200 }, facts, done));
        Assert.Equal(DialogueRefusal.None, foe.Talk("npc.deserteur", out DialogueView? cold));
        Assert.Equal(("ch4_colin_apres_aide", "text.v27.deserteur.aide.wary"), (cold!.Node, cold.Text));
        // Apprécié des bandits : une autre réplique.
        WorldSession liked = Play(Zone(ColinX, ColinZ), With(Hero(), new Dictionary<string, int> { ["bandits"] = 150 },
            facts.Append("fact.v27.deserteur.plus_tard"), done));
        Assert.Equal(DialogueRefusal.None, liked.Talk("npc.deserteur", out DialogueView? warm));
        Assert.Equal(("ch4_colin_apres_aide", "text.v27.deserteur.aide.liked"), (warm!.Node, warm.Text));
        // Remise à plus tard : la réponse « Ton mot de passe… » rouvre l'offre.
        Assert.Contains(warm.Options, o => o.Text == "text.v27.deserteur.encore");
    }

    // --- Gestes du monde -----------------------------------------------------------------------------------------------

    [Fact]
    public void Victoires_evenements_et_purifications_changent_la_reputation()
    {
        WorldSession w = Play(Zone(), Hero());
        w.Narrative!.Defeated("bandit_epee1", "bandit");
        w.Narrative.Defeated("loup_nuit1", "loup");
        w.Narrative.Defeated("cerf1", null);   // ni population ni famille listée : rien
        w.Narrative.EventSettled("attaque_hameau", won: true);
        w.Narrative.EventSettled("faille_du_seuil", won: false);
        w.Narrative.EventSettled("chasse_de_l_aube", won: true);   // sans règle : rien
        w.Narrative.Purified("faille_du_seuil");
        List<string> events = Steps(w).Where(e => e.StartsWith("Reputation", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[]
        {
            "ReputationChanged guard/defeated 3", "ReputationChanged bandits/defeated -4", "ReputationChanged rangers/defeated 1",
            "ReputationChanged villagers/eventWon 40", "ReputationChanged guard/eventWon 15", "ReputationChanged rangers/eventLost -10",
            "ReputationChanged villagers/scarPurified 10", "ReputationChanged rangers/scarPurified 20",
        }, events);
        Assert.Equal((50, 18, 11, -4), (w.Narrative.Reputation("villagers"), w.Narrative.Reputation("guard"), w.Narrative.Reputation("rangers"), w.Narrative.Reputation("bandits")));
        // Annoncés une seule fois.
        Assert.DoesNotContain(Steps(w), e => e.StartsWith("Reputation", StringComparison.Ordinal));
    }

    [Fact]
    public void Une_quete_finie_se_sait_au_village()
    {
        // La quête du capitaine, à son rapport : finie au prochain tick (objectif « parler à » rempli).
        SavedCharacter hero = With(Hero(), new Dictionary<string, int> { ["guard"] = 150 },
            quests: new Dictionary<string, QuestProgress> { ["quest.v27.ronde_du_capitaine"] = new QuestProgress("retour") });
        WorldSession w = Play(Zone(-2, -9.5), hero);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.capitaine", out DialogueView? view));
        Assert.Equal("v27_capitaine_merci", view!.Node);
        List<string> events = Steps(w);
        Assert.Contains("QuestCompleted quest.v27.ronde_du_capitaine", events);
        // Effet de la quête : garde +60 (et ses liens : village +15, bandits −30) ; puis le geste : quête secondaire, village +10.
        Assert.Equal(new[] { "ReputationChanged guard 60", "ReputationChanged villagers/link 15", "ReputationChanged bandits/link -30", "ReputationChanged villagers/questSide 10" },
            events.Where(e => e.StartsWith("Reputation", StringComparison.Ordinal)));
        Assert.Equal((210, 25, -30), (w.Narrative!.Reputation("guard"), w.Narrative.Reputation("villagers"), w.Narrative.Reputation("bandits")));
    }

    [Fact]
    public void Les_quetes_de_faction_restent_fermees_sous_leur_palier()
    {
        StoryBook book = Story.Value;
        Quest ronde = book.Quest("quest.v27.ronde_du_capitaine");
        Assert.Equal(new[] { (ConditionKind.StandingAtLeast, "guard", "liked") }, ronde.Requires.Select(c => (c.Kind, c.Id, c.Step!)));
        // Au poste (8 h), neutre : le capitaine ne propose rien ; apprécié : l'offre.
        WorldSession neutral = Play(Zone(-2, -9.5), Hero());
        Assert.Equal(DialogueRefusal.None, neutral.Talk("npc.capitaine", out DialogueView? plain));
        Assert.NotEqual("v27_capitaine_offre", plain!.Node);
        WorldSession liked = Play(Zone(-2, -9.5), With(Hero(), new Dictionary<string, int> { ["guard"] = 100 }));
        Assert.Equal(DialogueRefusal.None, liked.Talk("npc.capitaine", out DialogueView? offer));
        Assert.Equal("v27_capitaine_offre", offer!.Node);
        // « Une autre fois » : l'offre ne revient pas d'elle-même, une réponse de l'accueil la rouvre.
        Assert.Equal(DialogueRefusal.None, liked.Choose(1, out _, out _));
        liked.Step();
        Assert.Equal(DialogueRefusal.None, liked.Talk("npc.capitaine", out DialogueView? again));
        Assert.NotEqual("v27_capitaine_offre", again!.Node);
        Assert.Contains(again.Options, o => o.Text == "text.v27.capitaine.encore");
    }

    // --- Prix des marchands --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 100)]
    [InlineData(150, 95)]
    [InlineData(450, 90)]
    [InlineData(700, 80)]
    [InlineData(-150, 110)]
    [InlineData(-400, 120)]
    public void Prix_d_achat_selon_les_villageois(int villagers, int percent)
    {
        const string merchant = "merchant.herboriste";
        IReadOnlyList<EdS.Data.Merchants.MerchantOffer> plain = At(merchant, Hero(100_000)).Stock(merchant);
        WorldSession w = At(merchant, With(Hero(100_000), new Dictionary<string, int> { ["villagers"] = villagers }));
        Assert.Equal(percent, w.PricePercent);
        IReadOnlyList<EdS.Data.Merchants.MerchantOffer> stock = w.Stock(merchant);
        Assert.Equal(plain.Select(o => o.Item), stock.Select(o => o.Item));
        Assert.Equal(plain.Select(o => WorldSession.ReputationPrice(o.Price, percent)), stock.Select(o => o.Price));
        // L'achat paie le prix affiché ; la vente ne change pas.
        EdS.Data.Merchants.MerchantOffer first = stock[0];
        long before = w.Money;
        Assert.Equal(TradeRefusal.None, w.Buy(merchant, first.Item));
        Assert.Equal(before - first.Price, w.Money);
    }

    [Theory]
    [InlineData(1, 130, 1)]
    [InlineData(1, 80, 1)]
    [InlineData(10, 95, 10)]   // 9,5 arrondi au plus proche
    [InlineData(25, 90, 23)]   // 22,5
    [InlineData(100, 80, 80)]
    [InlineData(0, 130, 0)]
    public void Prix_arrondi_au_plus_proche_une_piece_au_moins(long price, int percent, long expected) =>
        Assert.Equal(expected, WorldSession.ReputationPrice(price, percent));
}
