using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.6 : soin et métiers. Objets de soin dans le moteur (commande <c>consume</c>, charges, recharge partagée ; moteur 0.24.0,
/// journal 18, scénarios 12, instantané 3) ; potions et remèdes de l'inventaire emportés au combat ; récolte, fabrication et expérience de
/// métier séparée ; sauvegarde en version 5.
/// </summary>
[Trait("Category", "World")]
public class Lot126CraftingTests
{
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;

    private static ZoneDefinition Zone(double x = -18, double z = -12)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, Items).Zone!;
    }

    /// <summary>Essai du 12.9 : on fabrique à la forge de Maëlle (entrée posée devant elle).</summary>
    private static (double X, double Z) Forge => (-19, -5);

    private static CraftingBook Book(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, Items).Book!;

    private static CraftingLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return CraftingJsonReader.Read(root.ToJsonString(), Texts, Zone(), Items);
    }

    private static SavedCharacter Hero(IReadOnlyDictionary<string, long>? professions = null, params ItemStack[] items) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>())
            .WithProfessions(professions ?? new Dictionary<string, long>());

    private static WorldSession Play(ZoneDefinition zone, SavedCharacter hero)
    {
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items, null, Book(zone));
        session.Step();
        return session;
    }

    // --- Moteur : objets de soin -------------------------------------------------------------------

    private static ScenarioLoadResult Potions(Action<JsonObject>? more = null, string scenario = "entrainement_declencheurs") => ReadMutated(root =>
    {
        root["data"]!["combatants"]![0]!["consumables"] = new JsonArray(new JsonObject
        {
            ["id"] = "item.potion_de_soin", ["heal"] = 100, ["charges"] = 2, ["cooldownMs"] = 1000,
        });
        JsonArray commands = root["data"]!["commands"]!.AsArray();
        foreach (long at in new long[] { 5000, 5500, 7000, 9000 })
            commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = "bastion", ["action"] = "consume", ["item"] = "item.potion_de_soin" });
        more?.Invoke(root);
    }, scenario: scenario);

    private static IEnumerable<JsonElement> Lines(MemoryCombatLog log, string kind) =>
        log.Lines.Select(l => JsonDocument.Parse(l).RootElement).Where(e => e.TryGetProperty("event", out JsonElement k) && k.GetString() == kind);

    [Fact]
    public void Metiers_en_double_refuses_dans_une_sauvegarde_en_objets_publics()
    {
        // Même règle que la revue Codex de la PR #44 : un métier en double donne un rapport, pas une exception.
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithProfessions(new Dictionary<string, long> { ["forge"] = 10 })));
        var report = new ValidationReport();
        var document = new SaveDocument();
        JsonHeader header = JsonInput.Parse(JsonInput.Utf8(json, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
        (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
        Assert.True(SaveValidator.Validate(document).IsValid);
        document.Data!.Character!.Professions!.Add(new KeyValuePair<string, long?>("forge", 20));
        Assert.Contains(SaveValidator.Validate(document).Report.Errors, e => e.Path == "$.data.character.professions.forge");
    }

    [Fact]
    public void Metiers_en_objets_publics_bornes_comme_a_la_lecture()
    {
        // Même règle que l'inventaire (revue Codex de la PR #44) et l'histoire : hors du lecteur JSON, des métiers renseignés en version 4
        // sont un champ inconnu, et la liste garde sa borne ; le schéma suit les mêmes noms et la même borne.
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithProfessions(new Dictionary<string, long> { ["forge"] = 10 })));
        SaveDocument Document(string text)
        {
            var report = new ValidationReport();
            var document = new SaveDocument();
            JsonHeader header = JsonInput.Parse(JsonInput.Utf8(text, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
            (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
            return document;
        }
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        root["schemaVersion"] = 4;
        root["data"]!["character"]!.AsObject().Remove("professions");
        root["data"]!["character"]!.AsObject().Remove("money");   // sous-lot V1.1 (version 6)
        root["data"]!["character"]!.AsObject().Remove("crafts");   // sous-lot V1.4 (version 11)
        root["data"]!["character"]!.AsObject().Remove("boons");   // sous-lot V1.8c (version 12)
        root["data"]!["character"]!["story"]!.AsObject().Remove("tracked");   // sous-lot V1.6 (version 7)
        string v4 = root.ToJsonString();
        Assert.True(SaveJsonReader.Read(v4).IsValid);
        SaveDocument old = Document(v4);
        old.Data!.Character!.Professions = new List<KeyValuePair<string, long?>> { new("forge", 10) };
        Assert.Contains(SaveValidator.Validate(old).Report.Errors, e => e.Path == "$.data.character.professions" && e.Code == ValidationCode.UnknownField);
        SaveDocument many = Document(json);
        many.Data!.Character!.Professions = Enumerable.Range(0, SaveValidator.MaxProfessions + 1).Select(i => new KeyValuePair<string, long?>($"m{i:D3}", 1)).ToList();
        Assert.Contains(SaveValidator.Validate(many).Report.Errors, e => e.Path == "$.data.character.professions" && e.Code == ValidationCode.LimitExceeded);
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(json).RootElement));
        Assert.False(schema.IsValid(JsonDocument.Parse(json.Replace("{\"forge\": 10}", "{\"Forge\": 10}")).RootElement));
    }

    [Fact]
    public void Metiers_construits_hors_du_lecteur_bornes_et_sans_exception()
    {
        // Même règle que la revue Codex de la PR #44 : un document en objets publics garde les bornes et les doublons du fichier, et un
        // nœud sans l'une de ses coordonnées donne un champ manquant, pas une exception.
        CraftingDocument Read()
        {
            var report = new ValidationReport();
            CraftingDocument d = CraftingJsonReader.ReadDocument(JsonInput.Utf8(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)), report), report);
            Assert.True(report.IsValid);
            return d;
        }
        ZoneDefinition zone = Zone();
        Assert.True(CraftingValidator.Validate(Read(), Texts, zone, Items).IsValid);
        CraftingDocument many = Read();
        while (many.Data!.Professions!.Count <= CraftingValidator.MaxProfessions) many.Data.Professions.Add(new ProfessionData { Id = $"m{many.Data.Professions.Count}", Name = many.Data.Professions[0]!.Name });
        Assert.Contains(CraftingValidator.Validate(many, Texts, zone, Items).Report.Errors, e => e.Path == "$.data.professions" && e.Code == ValidationCode.LimitExceeded);
        CraftingDocument twice = Read();
        twice.Data!.Professions![0]!.Name = "text.inconnu";
        twice.Data.Professions.Add(new ProfessionData { Id = twice.Data.Professions[0]!.Id, Name = twice.Data.Professions[1]!.Name });
        Assert.Contains(CraftingValidator.Validate(twice, Texts, zone, Items).Report.Errors, e => e.Code == ValidationCode.DuplicateId);
        CraftingDocument point = Read();
        point.Data!.Nodes![0]!.Position = new PointData { X = EdS.Domain.Numerics.Fixed.Zero };
        Assert.Contains(CraftingValidator.Validate(point, Texts, zone, Items).Report.Errors, e => e.Path == "$.data.nodes[0].position" && e.Code == ValidationCode.MissingField);
        CraftingDocument levels = Read();
        levels.Data!.MaxLevel = 0;
        levels.Data.XpToLevel = Enumerable.Range(0, CraftingValidator.MaxLevels + 1).Select(i => (long?)i).ToList();
        Assert.Contains(CraftingValidator.Validate(levels, Texts, zone, Items).Report.Errors, e => e.Path == "$.data.xpToLevel" && e.Code == ValidationCode.LimitExceeded);
        // Le schéma suit les bornes des identifiants et des niveaux.
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "crafting.schema.json"));
        JsonObject root = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["professions"]![0]!["id"] = new string('a', 65);
        Assert.False(schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement));
    }

    [Fact]
    public void Potion_bue_en_combat_charges_et_recharge_partagee()
    {
        ScenarioLoadResult loaded = Potions();
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Contains("\"consumables\":[{\"id\":\"item.potion_de_soin\",\"heal\":100,\"charges\":2,\"cooldownMs\":1000}]", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"action\":\"consume\",\"item\":\"item.potion_de_soin\"", log.Lines[0], StringComparison.Ordinal);
        JsonElement[] consumed = Lines(log, "consumed").ToArray();
        Assert.Equal(2, consumed.Length);
        Assert.Equal((100L, 1, 120L), (consumed[0].GetProperty("healed").GetInt64(), consumed[0].GetProperty("charges").GetInt32(), consumed[0].GetProperty("readyTick").GetInt64()));
        Assert.Equal(0, consumed[1].GetProperty("charges").GetInt32());
        Assert.Equal(new[] { "consumableCooldown", "noCharges" },
            Lines(log, "commandRejected").Where(e => e.GetProperty("action").GetString() == "consume").Select(e => e.GetProperty("reason").GetString()));
        JsonElement summary = Lines(log, "combatantSummary").Single(e => e.GetProperty("combatant").GetString() == "bastion");
        long healed = summary.GetProperty("healed").GetInt64();
        Assert.Equal(consumed.Sum(c => c.GetProperty("healed").GetInt64()), healed);
        Assert.Equal(2, summary.GetProperty("consumablesUsed").GetProperty("item.potion_de_soin").GetInt32());
        // PV de fin = PV max − dégâts subis + soins.
        long max = Lines(log, "combatant").Single(e => e.GetProperty("combatant").GetString() == "bastion").GetProperty("maxHealth").GetInt64();
        Assert.Equal(max - summary.GetProperty("damageTaken").GetInt64() + healed, summary.GetProperty("health").GetInt64());
        // Rejeu à l'identique ; l'assaillant n'a pas de bilan de soin.
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
        Assert.False(Lines(log, "combatantSummary").Single(e => e.GetProperty("combatant").GetString() == "assaillant").TryGetProperty("healed", out _));
    }

    [Fact]
    public void Potion_refusee_a_PV_pleins()
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Potions(scenario: "mannequin").Setup!, log);
        Assert.Empty(Lines(log, "consumed"));
        Assert.All(Lines(log, "commandRejected").Where(e => e.GetProperty("action").GetString() == "consume"),
            e => Assert.Equal("healthFull", e.GetProperty("reason").GetString()));
    }

    [Fact]
    public void Instantane_avec_objets_de_soin_relu_et_continue()
    {
        CombatSetup setup = Potions().Setup!;
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 125) engine.Step();   // après la première potion (tick 100), avant la seconde (tick 140)
        string text = engine.Snapshot().Text;
        Assert.True(text.Contains("\"consumables\":[[\"item.potion_de_soin\",1]],\"consumableReadyTick\":120", StringComparison.Ordinal),
            text.Split('\n').FirstOrDefault(l => l.Contains("consumab", StringComparison.Ordinal)) ?? text[..300]);
        var resumedLog = new MemoryCombatLog();
        CombatEngine copy = CombatEngine.RestoreVerified(engine.CompletedSetup(), CombatSnapshot.Parse(text), resumedLog);
        while (!engine.IsFinished)
        {
            engine.Step();
            copy.Step();
        }
        Assert.Equal(log.Lines.Skip(log.Lines.Count - resumedLog.Lines.Count), resumedLog.Lines);
        // Un instantané retouché (charges impossibles) est refusé.
        Assert.ThrowsAny<Exception>(() => CombatEngine.RestoreVerified(setup, CombatSnapshot.Parse(text.Replace("[\"item.potion_de_soin\",1]", "[\"item.potion_de_soin\",5]")), NullCombatLog.Instance));
    }

    [Theory]
    [InlineData("heal", 0, "$.data.combatants[0].consumables[0].heal")]
    [InlineData("charges", 1000, "$.data.combatants[0].consumables[0].charges")]
    [InlineData("cooldownMs", 1025, "$.data.combatants[0].consumables[0].cooldownMs")]
    public void Objet_de_soin_invalide(string field, long value, string path)
    {
        ScenarioLoadResult loaded = Potions(root => root["data"]!["combatants"]![0]!["consumables"]![0]![field] = value);
        Assert.Equal(path, Assert.Single(loaded.Report.Errors).Path);
    }

    [Fact]
    public void Commandes_de_soin_invalides()
    {
        int last = ReadMutated(_ => { }, scenario: "entrainement_declencheurs").Setup!.Commands.Count + 3;
        Assert.Equal($"$.data.commands[{last}].item", Assert.Single(Potions(r => r["data"]!["commands"]![last]!["item"] = "item.remede_de_l_aube").Report.Errors).Path);
        Assert.Equal($"$.data.commands[{last}].item", Assert.Single(Potions(r => r["data"]!["commands"]![last]!.AsObject().Remove("item")).Report.Errors).Path);
        Assert.Equal("$.data.commands[0].item", Assert.Single(Potions(r => r["data"]!["commands"]![0]!["item"] = "item.potion_de_soin").Report.Errors).Path);
        Assert.Equal($"$.data.commands[{last}].target", Assert.Single(Potions(r => r["data"]!["commands"]![last]!["target"] = "assaillant").Report.Errors).Path);
        Assert.Equal("$.data.combatants[1].consumables",
            Assert.Single(Potions(r => r["data"]!["combatants"]![1]!["consumables"] = new JsonArray()).Report.Errors).Path);
        Assert.Equal("$.data.combatants[0].consumables[1].id", Assert.Single(Potions(r => r["data"]!["combatants"]![0]!["consumables"]!.AsArray()
            .Add(new JsonObject { ["id"] = "item.potion_de_soin", ["heal"] = 1, ["charges"] = 1, ["cooldownMs"] = 0 })).Report.Errors).Path);
    }

    // --- Métiers ---------------------------------------------------------------------------------

    [Fact]
    public void Metiers_livres()
    {
        CraftingLoadResult loaded = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, Zone(), Items);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        CraftingBook book = loaded.Book!;
        Assert.Equal(new[] { "forge", "herboristerie", "minage" }, book.Professions.Select(p => p.Id));
        Assert.Equal((1, 2, 10), (book.LevelFor(39), book.LevelFor(40), book.LevelFor(5000)));
        Assert.Equal(("forge", "forge", 3, "item.ferrure"), (book.Recipe("recipe.ferrure").Profession, book.Recipe("recipe.ferrure").Place,
            book.Recipe("recipe.ferrure").Inputs[0].Count, book.Recipe("recipe.ferrure").Output.Item));
        Assert.Equal(11, book.Nodes.Count);   // Vallons agrandis : herbes de la forêt, du lac, du verger ; filons des hautes pierres et de la tour
        ItemDefinition potion = Items.Item("item.potion_de_soin");
        Assert.Equal((ItemType.Consumable, 300L, 20_000L, 5), (potion.Type, potion.Heal, potion.CooldownMs, potion.Stack));
        MiniJsonSchema Schema(string name) => MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", name));
        Assert.True(Schema("crafting.schema.json").IsValid(JsonDocument.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory))).RootElement));
        Assert.True(Schema("items.schema.json").IsValid(JsonDocument.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Metiers_invalides_refuses_avec_leur_chemin()
    {
        static JsonObject D(JsonObject r) => r["data"]!.AsObject();
        string First(CraftingLoadResult r) { Assert.False(r.IsValid); return r.Report.Errors[0].Path; }
        Assert.Equal("$.data.xpToLevel[2]", First(Mutated(r => D(r)["xpToLevel"]![2] = 40)));
        Assert.Equal("$.data.professions[1].id", First(Mutated(r => D(r)["professions"]![1]!["id"] = "forge")));
        Assert.Equal("$.data.professions[0].name", First(Mutated(r => D(r)["professions"]![0]!["name"] = "text.inconnu")));
        Assert.Equal("$.data.recipes[0].profession", First(Mutated(r => D(r)["recipes"]![0]!["profession"] = "cuisine")));
        Assert.Equal("$.data.recipes[0].place", First(Mutated(r => D(r)["recipes"]![0]!["place"] = "chateau")));
        Assert.Equal("$.data.recipes[0].requiredLevel", First(Mutated(r => D(r)["recipes"]![0]!["requiredLevel"] = 11)));
        Assert.Equal("$.data.recipes[0].inputs[0].item", First(Mutated(r => D(r)["recipes"]![0]!["inputs"]![0]!["item"] = "item.inconnu")));
        Assert.Equal("$.data.recipes[0].output.count", First(Mutated(r => D(r)["recipes"]![0]!["output"]!["count"] = 6)));
        Assert.Equal("$.data.recipes[1].id", First(Mutated(r => D(r)["recipes"]![1]!["id"] = "recipe.ferrure")));
        Assert.Equal("$.data.nodes[0].position", First(Mutated(r => D(r)["nodes"]![0]!["position"] = new JsonArray(150, 0))));   // Vallons agrandis : hors de l'aire (x jusqu'à 140)
        Assert.Equal("$.data.nodes[0].respawnMs", First(Mutated(r => D(r)["nodes"]![0]!["respawnMs"] = 10)));
    }

    [Fact]
    public void Recolte_et_fabrication_avec_leur_experience()
    {
        // Au filon de l'ouest (carrière) : trois récoltes, une par repousse ; l'expérience va au minage seulement.
        WorldSession mine = Play(Zone(31, -12), Hero());
        Assert.Equal(CraftRefusal.NotHere, mine.Gather("herbes_village", out _));
        Assert.Equal(CraftRefusal.Unknown, mine.Gather("filon_nord", out _));
        Assert.Equal(CraftRefusal.None, mine.Gather("filon_ouest", out CraftEvent? first));
        Assert.Equal(("item.minerai_de_fer", 1, "minage", 12L), (first!.Item, first.Count, first.Profession, first.Xp));
        Assert.Equal(CraftRefusal.Depleted, mine.Gather("filon_ouest", out _));
        long now = mine.Zone.Engine.CurrentTick;
        Assert.False(mine.Workshop!.IsReady("filon_ouest", now + 1199));
        Assert.True(mine.Workshop.IsReady("filon_ouest", now + 1200));   // une minute : le filon a repoussé
        Assert.Equal(CraftRefusal.NotHere, mine.Gather("filon_centre", out _));   // trop loin
        Assert.Equal((1, 12L, 0L, 0L), (mine.Inventory.Count("item.minerai_de_fer"), mine.Workshop.Xp("minage"), mine.Workshop.Xp("forge"), mine.Character.Xp));
        // Hors de l'atelier (la forge de Maëlle, depuis l'essai du 12.9), on ne forge pas.
        Assert.Equal(CraftRefusal.NotHere, mine.Craft("recipe.ferrure", out _));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(mine.Save())).Save!;
        Assert.Equal(12L, saved.Character!.Professions["minage"]);

        // À la forge, avec trois minerais : la ferrure, l'expérience de forge, et rien pour le combat.
        WorldSession forge = Play(Zone(Forge.X, Forge.Z), Hero(null, new ItemStack("item.minerai_de_fer", 5)));
        Assert.Equal(CraftRefusal.None, forge.Craft("recipe.ferrure", out CraftEvent? ferrure));
        Assert.Equal(("item.ferrure", 30L, 1, 1), (ferrure!.Item, ferrure.Xp, ferrure.LevelBefore, ferrure.LevelAfter));
        Assert.Equal((2, 1, 0L), (forge.Inventory.Count("item.minerai_de_fer"), forge.Inventory.Count("item.ferrure"), forge.Character.Xp));
        Assert.Equal(CraftRefusal.NotEnough, forge.Craft("recipe.ferrure", out _));
        Assert.Equal(2, forge.Inventory.Count("item.minerai_de_fer"));   // rien retiré
        // Sous-lot V1.4 : une recette jusqu'à deux niveaux au-dessus du métier se tente (elle peut rater) ; ici, il manque les herbes.
        Assert.Equal(CraftRefusal.NotEnough, forge.Craft("recipe.remede_de_l_aube", out _));
        // Une fabrication bien en dessous du niveau du métier forme moins.
        WorldSession master = Play(Zone(Forge.X, Forge.Z), Hero(new Dictionary<string, long> { ["herboristerie"] = 400 }, new ItemStack("item.herbe_de_l_aube", 4)));
        Assert.Equal(6, master.Workshop!.Level("herboristerie"));
        master.Craft("recipe.potion_de_soin", out CraftEvent? trivial);
        Assert.Equal(5L, trivial!.Xp);   // 20 / 4 à 5 niveaux d'écart
        // Inventaire plein : le produit ne tient pas, rien ne change.
        WorldSession full = Play(Zone(Forge.X, Forge.Z), Hero(null, Enumerable.Repeat(new ItemStack("item.gambison", 1), 23).Append(new ItemStack("item.minerai_de_fer", 3)).ToArray()));
        Assert.Equal(CraftRefusal.None, full.Craft("recipe.ferrure", out _));   // les minerais libèrent leur place
        WorldSession packed = Play(Zone(Forge.X, Forge.Z), Hero(null, Enumerable.Repeat(new ItemStack("item.gambison", 1), 22)
            .Append(new ItemStack("item.minerai_de_fer", 20)).Append(new ItemStack("item.minerai_de_fer", 20)).ToArray()));
        Assert.Equal(CraftRefusal.Full, packed.Craft("recipe.ferrure", out _));   // trois minerais de moins ne libèrent aucune place
        Assert.Equal(40, packed.Inventory.Count("item.minerai_de_fer"));
    }

    [Fact]
    public void Potions_de_l_inventaire_emportees_et_bues()
    {
        // Sept potions : une pile (cinq) emportée au combat ; une bue retire une potion de l'inventaire.
        ZoneDefinition zone = Zone();
        WorldSession session = Play(zone, Hero(null, new ItemStack("item.potion_de_soin", 5), new ItemStack("item.potion_de_soin", 2)));
        CombatantView me = session.Zone.Engine.View("bastion");
        Assert.Equal(5, me.ConsumableCharges["item.potion_de_soin"]);
        Assert.Throws<ArgumentException>(() => session.Drink("item.remede_de_l_aube"));
        session.Drink("item.potion_de_soin");   // PV pleins : refusée par le moteur, rien n'est retiré
        session.Step();
        Assert.Equal(7, session.Inventory.Count("item.potion_de_soin"));
    }

    [Fact]
    public void Potions_emportees_reservees_jusqu_a_ce_qu_elles_soient_bues()
    {
        // Revue Codex de la PR #47 : les potions emportées ne se retirent plus de l'inventaire (recette, réponse de dialogue, API) avant
        // d'être bues ; le moteur ne peut donc jamais soigner sans potion payée.
        WorldSession session = Play(Zone(-4, 13), Hero(null, new ItemStack("item.potion_de_soin", 5), new ItemStack("item.potion_de_soin", 2)));
        Assert.Equal((7, 5, 2), (session.Inventory.Count("item.potion_de_soin"), session.Inventory.Reserved("item.potion_de_soin"), session.Inventory.Available("item.potion_de_soin")));
        Assert.Equal(InventoryRefusal.NotEnough, session.Inventory.Remove("item.potion_de_soin", 3));
        Assert.Equal(InventoryRefusal.None, session.Inventory.Remove("item.potion_de_soin", 2));
        Assert.Equal(5, session.Save().Character!.Inventory.Sum(st => st.Item == "item.potion_de_soin" ? st.Count : 0));   // réservées, toujours sauvegardées
        // Le loup voisin blesse le Bastion ; la potion bue sort de la réserve et de l'inventaire.
        for (int i = 0; i < 400 && session.Zone.Engine.View("bastion").Health == session.Zone.Engine.View("bastion").MaxHealth; i++) session.Step();
        Assert.True(session.Zone.Engine.View("bastion").Health < session.Zone.Engine.View("bastion").MaxHealth);
        session.Drink("item.potion_de_soin");
        session.Step();
        session.Step();
        Assert.Equal(4, session.Zone.Engine.View("bastion").ConsumableCharges["item.potion_de_soin"]);
        Assert.Equal((4, 4, 0), (session.Inventory.Count("item.potion_de_soin"), session.Inventory.Reserved("item.potion_de_soin"), session.Inventory.Available("item.potion_de_soin")));
        // Une copie (essai d'une réponse de dialogue) garde la réserve.
        Assert.Equal(InventoryRefusal.NotEnough, session.Inventory.Clone().Remove("item.potion_de_soin", 1));
    }

    [Fact]
    public void Soin_pilote_refuse_s_il_nomme_un_objet_non_emporte()
    {
        // Revue Codex de la PR #47 : le pilote refuse à l'admission (rien ne change) le soin que le scénario complété refuserait, pour que
        // la partie reste rejouable et restaurable.
        WorldSession session = Play(Zone(), Hero(null, new ItemStack("item.potion_de_soin", 1)));
        CombatEngine engine = session.Zone.Engine;
        long at = engine.CurrentTick * engine.TickMilliseconds;
        Assert.Throws<ArgumentException>(() => engine.Submit(CombatCommand.Use(at, "bastion", "item.remede_de_l_aube")));
        Assert.Throws<ArgumentException>(() => engine.Submit(CombatCommand.Use(at, "loup1", "item.potion_de_soin")));
        engine.Submit(CombatCommand.Use(at, "bastion", "item.potion_de_soin"));
        Assert.True(engine.CompletedSetup().Validate().Count == 0);
    }

    [Fact]
    public void Experience_de_metier_hors_bornes_refusee_avant_l_ouverture()
    {
        // Revue Codex de la PR #47 : un personnage construit hors du lecteur JSON (expérience négative, démesurée) ou un état narratif qui
        // ne va pas avec l'histoire est refusé avant l'ouverture de la zone : aucune ligne de journal, aucune opération à moitié faite.
        ZoneDefinition zone = Zone();
        StoryBook story = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), Texts, zone, Items, V16TestData.Interiors).Story!;
        void Refused(SavedCharacter hero)
        {
            var log = new MemoryCombatLog();
            Assert.Throws<ArgumentException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression,
                Items, story, Book(zone), log));
            Assert.Empty(log.Lines);
        }
        Refused(Hero(new Dictionary<string, long> { ["minage"] = -5 }));
        Refused(Hero(new Dictionary<string, long> { ["minage"] = long.MaxValue }));
        Refused(Hero(new Dictionary<string, long> { ["cuisine"] = 10 }));
        Refused(Hero().WithStory(new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.inconnue"] = new QuestProgress("x") })));
        // Au plafond, l'expérience annoncée est celle réellement ajoutée.
        WorldSession capped = Play(Zone(Forge.X, Forge.Z), Hero(new Dictionary<string, long> { ["forge"] = CraftingValidator.MaxXp }, new ItemStack("item.minerai_de_fer", 3)));
        Assert.Equal(CraftRefusal.None, capped.Craft("recipe.ferrure", out CraftEvent? done));
        Assert.Equal(0L, done!.Xp);
        Assert.Equal(CraftingValidator.MaxXp, capped.Workshop!.Xp("forge"));
    }

    // --- Sauvegarde, version 5 ---------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_des_metiers()
    {
        SavedCharacter hero = Hero(new Dictionary<string, long> { ["minage"] = 24, ["forge"] = 30, ["herboristerie"] = 0 });
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), hero));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);   // sous-lot V1.1 : version 6 (bourse) ; V1.2 : 7 (18 places) ; V1.6 : 8 (quête suivie) ; V1.3 : 9 (instances d'objets)
        Assert.Contains("      \"professions\": {\"forge\": 30, \"minage\": 24},\n      \"money\": 0,\n      \"crafts\": 0,\n      \"boons\": []\n    },", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(text, SaveJsonReader.Write(read.Save!));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        string v4 = SaveV11.Older(text).Replace(",\n      \"professions\": {\"forge\": 30, \"minage\": 24},\n      \"money\": 0\n", "\n").Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 4");
        SaveLoadResult migrated = SaveJsonReader.Read(v4);
        Assert.True(migrated.IsValid, migrated.Report + "\n" + v4);
        Assert.Empty(migrated.Save!.Character!.Professions);
        Assert.Equal("$.data.character.professions", Assert.Single(SaveJsonReader.Read(SaveV11.Older(text).Replace(",\n      \"money\": 0\n", "\n").Replace(",\n        \"tracked\": null\n", "\n").Replace(SaveVersion.Current, "\"schemaVersion\": 4")).Report.Errors).Path);
        Assert.Equal("$.data.character.professions.forge", SaveJsonReader.Read(text.Replace("\"forge\": 30", "\"forge\": 0")).Report.Errors[0].Path);
        Assert.Equal("$.data.character.professions.forge", SaveJsonReader.Read(text.Replace("{\"forge\": 30, \"minage\": 24}", "{\"minage\": 24, \"forge\": 30}")).Report.Errors[0].Path);
        // Un métier sauvegardé doit exister dans les données.
        Assert.Throws<ArgumentException>(() => Play(Zone(), Hero(new Dictionary<string, long> { ["cuisine"] = 5 })));
    }
}
