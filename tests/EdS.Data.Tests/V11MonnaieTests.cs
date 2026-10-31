using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Simulation.Randomness;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.1 : la monnaie (cuivre, argent, or : choix de Damien du 29 octobre 2026), gardée comme un seul nombre de pièces de cuivre,
/// bornée et vérifiée ; la sauvegarde en version 6 (bourse) et la lecture des anciennes ; l'effet <c>giveMoney</c> de l'histoire en schéma
/// 2, atomique avec les autres récompenses.
/// </summary>
[Trait("Category", "World")]
public class V11MonnaieTests
{
    // --- Pièces ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Cuivre_argent_or()
    {
        Assert.Equal((3L, 45, 12), Coins.Split(34512));
        Assert.Equal("3 or 45 argent 12 cuivre", Coins.Format(34512));
        Assert.Equal("0 cuivre", Coins.Format(0));
        Assert.Equal("5 argent", Coins.Format(500));
        Assert.Equal("1 or", Coins.Format(10_000));
        Assert.Equal("1 or 1 cuivre", Coins.Format(10_001));
        Assert.Equal("999999 or 99 argent 99 cuivre", Coins.Format(Coins.Max));
        Assert.Equal(34512, Coins.Of(3, 45, 12));
        Assert.Equal(250, Coins.Of(0, 2, 50));
        Assert.Equal("2 G 50 C", Coins.Format(20_050, "{0} G", "{0} S", "{0} C"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Split(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Split(Coins.Max + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Format(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Of(1_000_000, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Of(long.MaxValue, 0, 0));   // débordement refusé, jamais tronqué
        Assert.Throws<ArgumentOutOfRangeException>(() => Coins.Of(0, -1, 0));
    }

    [Fact]
    public void Bourse_bornee_sans_debordement()
    {
        Assert.True(Coins.TryAdd(0, Coins.Max, out long full));
        Assert.Equal(Coins.Max, full);
        Assert.False(Coins.TryAdd(Coins.Max, 1, out long same));
        Assert.Equal(Coins.Max, same);
        Assert.False(Coins.TryAdd(0, -1, out long empty));
        Assert.Equal(0, empty);
        Assert.False(Coins.TryAdd(5, long.MinValue, out _));
        Assert.False(Coins.TryAdd(5, long.MaxValue, out _));
        Assert.False(Coins.TryAdd(-1, 1, out _));
        Assert.False(Coins.TryAdd(Coins.Max + 1, -2, out _));
        Assert.True(Coins.TryAdd(Coins.Max, -Coins.Max, out long none));
        Assert.Equal(0, none);
    }

    /// <summary>Propriétés, sur 20 000 tirages : découpage réversible, texte stable, ajout borné qui ne déborde jamais.</summary>
    [Fact]
    public void Proprietes_des_pieces()
    {
        var random = new Pcg32(20261030);
        long Next(long bound) => (long)(((ulong)random.NextUInt() << 32 | random.NextUInt()) % (ulong)bound);
        for (int i = 0; i < 20_000; i++)
        {
            long purse = i % 10 == 0 ? Coins.Max - Next(100) : Next(Coins.Max + 1);
            (long g, int s, int c) = Coins.Split(purse);
            Assert.InRange(s, 0, 99);
            Assert.InRange(c, 0, 99);
            Assert.Equal(purse, Coins.Of(g, s, c));
            Assert.Equal(purse == 0 ? "0 cuivre" : Coins.Format(purse), Coins.Format(purse));
            long change = (i % 3 == 0 ? -1 : 1) * Next(i % 7 == 0 ? Coins.Max * 2 : 100_000);
            bool ok = Coins.TryAdd(purse, change, out long after);
            long exact = purse + change;   // sans débordement : |change| < 2 × 10^10
            Assert.Equal(exact >= 0 && exact <= Coins.Max && Math.Abs(change) <= Coins.Max, ok);
            Assert.Equal(ok ? exact : purse, after);
        }
    }

    // --- Sauvegarde, version 6 ---------------------------------------------------------------------------------------

    private static string SaveWith(long money) => SaveJsonReader.Write(new WorldSave("zone.vallons", 120, 2, new[] { "zone.vallons/safe/place_du_bourg" }, Hero(money)));

    [Fact]
    public void Bourse_sauvegardee_en_version_6()
    {
        string text = SaveWith(34512);
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);
        Assert.Contains("      \"professions\": {},\n      \"money\": 34512,\n      \"crafts\": 0\n    },", text, StringComparison.Ordinal);
        Assert.Contains("\"consumed\": [\n      \"zone.vallons/safe/place_du_bourg\"\n    ]", text, StringComparison.Ordinal);   // point sûr découvert
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(34512, read.Save!.Character!.Money);
        Assert.False(read.Save.Migrated);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        Assert.Equal(Coins.Max, SaveJsonReader.Read(SaveWith(Coins.Max)).Save!.Character!.Money);
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(text).RootElement));
        Assert.True(schema.IsValid(JsonDocument.Parse(SaveWith(Coins.Max)).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(text.Replace("\"money\": 34512", "\"money\": 10000000000")).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(text.Replace("      \"money\": 34512,\n", "      \"z\": 0,\n")).RootElement));
        // Les copies du personnage gardent la bourse.
        SavedCharacter hero = Hero(77);
        Assert.Equal(77, hero.WithXp(5).WithTalents(new Dictionary<string, int>()).WithStory(SavedStory.Empty).WithProfessions(new Dictionary<string, long>())
            .WithItems(Array.Empty<ItemStack>(), new Dictionary<string, string>()).WithSkills(hero.ActiveSkills, null, hero.PassiveSkills).Money);
    }

    [Theory]
    [InlineData("\"money\": 34512", "\"money\": -1", "$.data.character.money", ValidationCode.OutOfRange)]
    [InlineData("\"money\": 34512", "\"money\": 10000000000", "$.data.character.money", ValidationCode.OutOfRange)]
    [InlineData("\"money\": 34512", "\"money\": 99999999999999999999", "$.data.character.money", ValidationCode.OutOfRange)]
    [InlineData("\"money\": 34512", "\"money\": 1.5", "$.data.character.money", ValidationCode.InvalidNumber)]
    [InlineData("\"money\": 34512", "\"money\": \"3 or\"", "$.data.character.money", ValidationCode.WrongType)]
    [InlineData("\"money\": 34512", "\"money\": null", "$.data.character.money", ValidationCode.WrongType)]
    [InlineData("\n      \"money\": 34512,\n", "\n", "$.data.character.money", ValidationCode.MissingField)]
    [InlineData("\"schemaVersion\": {current}", "\"schemaVersion\": {future}", "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion)]
    public void Bourse_invalide_refusee_avec_son_chemin(string from, string to, string path, ValidationCode code)
    {
        (from, to) = (SaveVersion.Expand(from), SaveVersion.Expand(to));
        string text = SaveWith(34512);
        Assert.Contains(from, text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text.Replace(from, to, StringComparison.Ordinal));
        Assert.False(read.IsValid);
        Assert.Contains(read.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Ancienne_sauvegarde_lue_avec_une_bourse_vide()
    {
        string text = SaveWith(34512);
        string v5 = SaveV11.Older(text).Replace(",\n      \"money\": 34512\n", "\n").Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 5");
        SaveLoadResult read = SaveJsonReader.Read(v5);
        Assert.True(read.IsValid, read.Report + "\n" + v5);
        Assert.True(read.Save!.Migrated);
        Assert.Equal(0, read.Save.Character!.Money);
        Assert.Equal(new[] { "zone.vallons/safe/place_du_bourg" }, read.Save.Consumed);
        // Réécrite à la version courante, avec la bourse.
        string rewritten = SaveJsonReader.Write(read.Save);
        Assert.Contains(SaveVersion.Current + ",", rewritten, StringComparison.Ordinal);
        Assert.Contains("\"money\": 0,\n", rewritten, StringComparison.Ordinal);
        // Une bourse dans une sauvegarde de version 5 n'existe pas encore : champ inconnu (en JSON comme en objets publics).
        Assert.Equal(("$.data.character.money", ValidationCode.UnknownField),
            (Assert.Single(SaveJsonReader.Read(SaveV11.Older(text).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 5")).Report.Errors).Path,
             Assert.Single(SaveJsonReader.Read(SaveV11.Older(text).Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 5")).Report.Errors).Code));
        var report = new ValidationReport();
        var document = new SaveDocument();
        JsonHeader header = JsonInput.Parse(JsonInput.Utf8(v5, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
        (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
        document.Data!.Character!.Money = 5;
        Assert.Contains(SaveValidator.Validate(document).Report.Errors, e => e.Path == "$.data.character.money" && e.Code == ValidationCode.UnknownField);
        // Une version 1 (sans personnage) se lit toujours.
        SaveLoadResult v1 = SaveJsonReader.Read("{\"schemaVersion\": 1, \"kind\": \"save\", \"contentVersion\": \"0.1.0\", \"data\": {\"zone\": \"zone.vallons\", \"worldTick\": 0, \"sessions\": 0, \"consumed\": []}}");
        Assert.True(v1.IsValid && v1.Save!.Migrated && v1.Save.Character is null);
    }

    [Fact]
    public void Bourse_hors_bornes_jamais_ecrite()
    {
        // Relecture de la PR #72 : refusée dès la construction du personnage (ArgumentOutOfRangeException), donc jamais écrite.
        foreach (long money in new[] { -1L, Coins.Max + 1, long.MaxValue, long.MinValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(money))));
    }

    // --- Histoire, schéma 2 : giveMoney ---------------------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult ReadStory(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    private static JsonObject Effect(JsonObject root, string step, int index) =>
        root["data"]!["quests"]![0]!["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!["effects"]![index]!.AsObject();

    [Fact]
    public void Recompenses_en_pieces_de_la_mission()
    {
        StoryLoadResult story = ReadStory(StoryJson());
        Assert.True(story.IsValid, story.Report.ToString());
        Assert.Equal(StoryValidator.CurrentSchemaVersion, story.Story!.SchemaVersion);
        Quest mission = story.Story.Quest("quest.traces_sous_le_moulin");
        long Money(IEnumerable<StoryEffect> effects) => effects.Where(e => e.Kind == EffectKind.GiveMoney).Sum(e => e.Value);
        Assert.Equal(50, Money(mission.Steps.Single(s => s.Id == "passage").Effects));
        Assert.Equal(150, Money(mission.Steps.Single(s => s.Id == "rodeur").Effects));
        Assert.Equal(500, Money(mission.Effects));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Theory]
    [InlineData("value", "0", ".value", ValidationCode.OutOfRange)]
    [InlineData("value", "100000001", ".value", ValidationCode.OutOfRange)]
    [InlineData("id", "\"money\"", ".id", ValidationCode.InvalidValue)]
    [InlineData("step", "\"x\"", ".step", ValidationCode.InvalidValue)]
    [InlineData("value", null, ".value", ValidationCode.MissingField)]
    public void Pieces_invalides_refusees_avec_leur_chemin(string field, string? json, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        JsonObject effect = Effect(root, "passage", 1);
        Assert.Equal("giveMoney", (string)effect["do"]!);
        if (json is null) effect.Remove(field);
        else effect[field] = JsonNode.Parse(json);
        StoryLoadResult result = ReadStory(root);
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.quests[0].steps[2].effects[1]" + suffix && e.Code == code);
    }

    [Fact]
    public void Histoire_de_schema_1_refusee()
    {
        JsonObject root = StoryJson();
        root["schemaVersion"] = 1;
        StoryLoadResult result = ReadStory(root);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (Assert.Single(result.Report.Errors).Path, result.Report.Errors[0].Code));
    }

    /// <summary>Partie au retour chez Odile (le fait du retour appris) : la récompense finale se donne au prochain tick.</summary>
    private static WorldSession Return(long money)
    {
        var story = new SavedStory(new[] { "fact.odile.retour" }, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("retour") });
        ZoneDefinition zone = Zone();
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(money).WithStory(story)), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        return session;
    }

    [Fact]
    public void Pieces_de_quete_atomiques_avec_les_autres_recompenses()
    {
        // Bourse qui déborderait : ni pièces, ni expérience, l'étape attend (annoncé une fois).
        WorldSession full = Return(Coins.Max - 499);
        long xp = full.Character.Xp;
        var events = new List<StoryEvent>();
        for (int i = 0; i < 5; i++) events.AddRange(full.Step().Story);
        // Relecture de la PR #72 : la bourse pleine a son propre événement (pas « inventaire plein »).
        Assert.Equal(new[] { "PurseFull quest.traces_sous_le_moulin/retour" }, events.Select(e => e.ToString()));
        Assert.Equal((Coins.Max - 499, xp), (full.Money, full.Character.Xp));
        Assert.Equal("retour", full.Narrative!.CurrentStep("quest.traces_sous_le_moulin")!.Id);

        // Juste la place : tout est donné, la bourse est pleine.
        WorldSession exact = Return(Coins.Max - 500);
        Assert.Contains("MoneyGiven money 500", exact.Step().Story.Select(e => e.ToString()));
        Assert.Equal(Coins.Max, exact.Money);
        Assert.Equal(Coins.Max, exact.Save().Character!.Money);
        Assert.True(exact.Narrative!.IsDone("quest.traces_sous_le_moulin"));
    }

    [Fact]
    public void Reponse_qui_donne_des_pieces_atomique()
    {
        // Une réponse de Gaspard retouchée : des pièces et un objet. Bourse pleine : rien ne change (ni fait, ni confiance, ni objet).
        JsonObject root = StoryJson();
        JsonObject merci = root["data"]!["dialogues"]!.AsArray().Single(d => (string)d!["id"]! == "dialogue.gaspard")!["nodes"]![1]!["choices"]![0]!.AsObject();
        merci["effects"]!.AsArray().Add(new JsonObject { ["do"] = "giveMoney", ["value"] = 25 });
        merci["effects"]!.AsArray().Add(new JsonObject { ["do"] = "giveItem", ["id"] = "item.potion_de_soin", ["value"] = 1 });
        ZoneDefinition zone = Zone(27, -15);
        StoryBook book = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, zone, ItemsCatalog, V16TestData.Interiors).Story!;
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("rodeur") });
        WorldSession Talk(long money)
        {
            var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(money).WithStory(story)), zone, ShippedData, Progression, ItemsCatalog, book);
            session.Step();
            Assert.Equal(DialogueRefusal.None, session.Talk("npc.gaspard", out DialogueView? view));
            Assert.Equal("rodeur", view!.Node);
            return session;
        }
        WorldSession full = Talk(Coins.Max - 24);
        Assert.Equal(DialogueRefusal.PurseFull, full.Choose(0, out _, out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        Assert.Equal((Coins.Max - 24, 0, false, 0), (full.Money, full.Inventory.Count("item.potion_de_soin"), full.Narrative!.Knows("fact.gaspard.merci"),
            full.Narrative.Trust("npc.gaspard")));
        WorldSession room = Talk(100);
        Assert.Equal(DialogueRefusal.None, room.Choose(0, out _, out IReadOnlyList<StoryEvent> given));
        Assert.Contains("MoneyGiven money 25", given.Select(e => e.ToString()));
        Assert.Equal((125L, 1, true), (room.Money, room.Inventory.Count("item.potion_de_soin"), room.Narrative!.Knows("fact.gaspard.merci")));
    }
}
