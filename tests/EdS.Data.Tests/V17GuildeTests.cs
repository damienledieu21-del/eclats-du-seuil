using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Guild;
using EdS.Data.Json;
using EdS.Data.Merchants;
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
/// Sous-lot V1.7, « Guilde des aventuriers » : données de la guilde (schéma 1 : rangs G à SSS, G, F et E ouverts, épreuves, titres,
/// contrats, portes, points), histoire en schéma 4 (objectif <c>guildRank</c>, effet <c>giveGuildPoints</c>, condition
/// <c>guildRankAtLeast</c>), marchands en schéma 2 (boutique et offres qui demandent un rang), sauvegarde en version 10 (carte
/// d'aventurier). Inscription, contrats du jour, épreuves, promotions, titres, portes et boutique ; chaque refus.
/// </summary>
[Trait("Category", "World")]
public class V17GuildeTests : IDisposable
{
    private const string Mistress = "npc.maitresse_guilde", TrialF = "quest.epreuve_rang_f", TrialE = "quest.epreuve_rang_e";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v17_guilde_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());

    private static IReadOnlyList<ZoneDefinition> AllZones => new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList();

    private static ZoneDefinition Inside(string id) => V16TestData.Interiors.Single(z => z.Id == id);

    private static JsonObject GuildJson() => JsonNode.Parse(File.ReadAllText(GuildJsonReader.PathIn(DataDirectory)))!.AsObject();

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryBook ReadStory(JsonObject root)
    {
        StoryLoadResult r = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons.Value, ItemsCatalog, V16TestData.Interiors);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Story!;
    }

    private static readonly Lazy<StoryBook> ShippedStory = new(() => ReadStory(StoryJson()));

    private static GuildLoadResult ReadGuild(JsonObject root, StoryBook? story = null, MerchantBook? merchants = null) =>
        GuildJsonReader.Read(root.ToJsonString(), Texts, story ?? ShippedStory.Value, Vallons.Value, AllZones, ItemsCatalog, merchants ?? ShippedMerchants);

    private static readonly Lazy<GuildBook> ShippedGuild = new(() =>
    {
        GuildLoadResult r = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, ShippedStory.Value, Vallons.Value, AllZones, ItemsCatalog, ShippedMerchants);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    private static GuildBook Guild => ShippedGuild.Value;

    /// <summary>
    /// L'histoire livrée, la maîtresse posée à 1,7 m de la porte de la guilde (où l'on arrive) ; l'intendant juste à côté d'elle.
    /// </summary>
    private static readonly Lazy<StoryBook> NearDoor = new(() =>
    {
        JsonObject root = StoryJson();
        foreach (JsonNode? n in root["data"]!["npcs"]!.AsArray())
        {
            if ((string)n!["id"]! == Mistress) n["position"] = new JsonArray(0, -1.6);
            if ((string)n["id"]! == "npc.intendant_guilde") n["position"] = new JsonArray(1.2, -2.2);
        }
        return ReadStory(root);
    });

    private static SavedCharacter WithCard(SavedCharacter hero, SavedGuildCard? card, IReadOnlyDictionary<string, QuestProgress>? quests = null, params string[] facts) =>
        hero.WithStory(new SavedStory(facts, quests, guild: card));

    private static SavedGuildCard Card(string rank, long points = 0, string? title = null, IReadOnlyDictionary<string, long>? active = null, long day = 0,
        IEnumerable<string>? taken = null, params string[] titles) =>
        new SavedGuildCard(rank, points, 0, titles.Length > 0 ? titles : new[] { "title.guilde.recrue" }, title, active, day, taken);

    /// <summary>Partie ouverte dans la Maison de la Guilde, arrivée par sa porte, un tick joué ; la maîtresse à portée (ou non).</summary>
    private static WorldSession InGuild(SavedCharacter hero, bool near = true, long tick = 0, Market? market = null, GuildBook? guild = null)
    {
        var session = new WorldSession(new WorldSave("zone.guilde", tick, 0, Array.Empty<string>(), hero), Inside("zone.guilde"), ShippedData, Progression,
            ItemsCatalog, near ? NearDoor.Value : ShippedStory.Value, door: "sortie", storyZone: "zone.vallons", market: market, guild: guild ?? Guild);
        session.Step();
        return session;
    }

    /// <summary>Une journée du monde, en ticks (le tableau change).</summary>
    private static long DayTicks => WorldClock.DefaultDayLengthMs / 50;

    private static List<string> Steps(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    // --- Données de la guilde ------------------------------------------------------------------------------------------

    [Fact]
    public void Guilde_livree()
    {
        GuildBook g = Guild;
        Assert.Equal((1, Mistress, 3, 2), (g.SchemaVersion, g.Npc, g.MaxActive, g.Daily));
        // Les dix rangs, dans l'ordre ; G, F et E ouverts dans les Vallons (choix de Damien), les autres fermés, prêts pour les régions suivantes.
        Assert.Equal(GuildRanks.Ids, g.Ranks.Select(r => r.Id));
        Assert.Equal(new[] { "g", "f", "e" }, g.Ranks.Where(r => r.Open).Select(r => r.Id));
        Assert.Equal("e", g.TopOpen.Id);
        Assert.Equal(new long[] { 0, 100, 300, 800, 2000, 4500, 9000, 18000, 36000, 72000 }, g.Ranks.Select(r => r.Points));
        Assert.Equal(new[] { null, TrialF, TrialE, null, null, null, null, null, null, null }, g.Ranks.Select(r => r.Trial));
        Assert.Equal(g.Ranks.Count, g.Titles.Count);
        Assert.All(g.Ranks, r => Assert.True(g.TryGetTitle(r.Title, out _)));
        // Contrats : chasses, livraisons, primes ; chaque rang ouvert en a au moins autant que le tirage du jour ; plus haut, mieux payé.
        Assert.Equal(new[] { 4, 4, 4 }, new[] { "g", "f", "e" }.Select(r => g.Contracts.Count(c => c.Rank == r)));
        Assert.Contains(g.Contracts, c => c.Kind == ContractKind.Hunt);
        Assert.Contains(g.Contracts, c => c.Kind == ContractKind.Delivery && c.Objective.Npc == Mistress);
        GuildContract bounty = Assert.Single(g.Contracts, c => c.Kind == ContractKind.Bounty);
        Assert.Equal((ObjectiveKind.Defeat, "le_borgne"), (bounty.Objective.Kind, bounty.Objective.Id));
        long Pay(string rank) => g.Contracts.Where(c => c.Rank == rank).Max(c => c.Money);
        Assert.True(Pay("g") < Pay("f") && Pay("f") < Pay("e"));
        Assert.True(g.Contracts.Where(c => c.Rank == "g").Max(c => c.Points) < g.Contracts.Where(c => c.Rank == "e").Min(c => c.Points));
        // Récompenses d'un contrat : pièces, expérience, points de guilde (effets de l'histoire).
        Assert.Equal(new[] { EffectKind.GiveMoney, EffectKind.GiveXp, EffectKind.GiveGuildPoints }, bounty.Rewards.Select(e => e.Kind));
        // Accès à des lieux : les archives de la guilde au rang F.
        GuildGate gate = Assert.Single(g.Gates);
        Assert.Equal(("zone.guilde", "archives", "f"), (gate.Zone, gate.Door, gate.Rank));
        Assert.Equal("f", g.GateOf("zone.guilde", "archives"));
        Assert.Null(g.GateOf("zone.guilde", "sortie"));
        // Points des accomplissements.
        Assert.Equal((40L, 20L, 5L), (g.MainQuestPoints, g.SideQuestPoints, g.DiscoveryPoints));
        Assert.Equal(25, g.RarePoints["loup_blanc"]);
        Assert.Equal(60, g.FactPoints["fact.chef.vaincu"]);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "guild.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(GuildJsonReader.PathIn(DataDirectory))).RootElement));
    }

    private static JsonObject Rank(JsonObject root, int i) => root["data"]!["ranks"]![i]!.AsObject();

    private static JsonObject Contract(JsonObject root, int i) => root["data"]!["contracts"]![i]!.AsObject();

    public static IEnumerable<object[]> Mauvaises_guildes()
    {
        object[] Case(string path, ValidationCode code, Action<JsonObject> change) => new object[] { path, code, change };
        yield return Case("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion, r => r["schemaVersion"] = 2);
        yield return Case("$.kind", ValidationCode.WrongKind, r => r["kind"] = "story");
        yield return Case("$.data.npc", ValidationCode.UnknownReference, r => r["data"]!["npc"] = "npc.personne");
        yield return Case("$.data.npc", ValidationCode.MissingField, r => r["data"]!.AsObject().Remove("npc"));
        yield return Case("$.data.maxActive", ValidationCode.OutOfRange, r => r["data"]!["maxActive"] = 9);
        yield return Case("$.data.daily", ValidationCode.OutOfRange, r => r["data"]!["daily"] = 0);
        yield return Case("$.data.extra", ValidationCode.UnknownField, r => r["data"]!["extra"] = 1);
        // Rangs : les dix, dans l'ordre, seuils croissants, ouverts d'abord, une épreuve par rang ouvert au-dessus de G.
        yield return Case("$.data.ranks", ValidationCode.OutOfRange, r => r["data"]!["ranks"]!.AsArray().RemoveAt(9));
        yield return Case("$.data.ranks[1].id", ValidationCode.InvalidValue, r => Rank(r, 1)["id"] = "e");
        yield return Case("$.data.ranks[0].points", ValidationCode.InvalidValue, r => Rank(r, 0)["points"] = 10);
        yield return Case("$.data.ranks[2].points", ValidationCode.InvalidValue, r => Rank(r, 2)["points"] = 100);
        yield return Case("$.data.ranks[3].points", ValidationCode.OutOfRange, r => Rank(r, 3)["points"] = 10_000_001);
        yield return Case("$.data.ranks[0].open", ValidationCode.InvalidValue, r => Rank(r, 0)["open"] = false);
        yield return Case("$.data.ranks[4].open", ValidationCode.InvalidValue, r => Rank(r, 4)["open"] = true);
        yield return Case("$.data.ranks[1].trial", ValidationCode.MissingField, r => Rank(r, 1)["trial"] = null);
        yield return Case("$.data.ranks[1].trial", ValidationCode.MissingField, r => Rank(r, 1).Remove("trial"));
        yield return Case("$.data.ranks[3].trial", ValidationCode.InvalidValue, r => Rank(r, 3)["trial"] = "quest.bete_blanche");
        yield return Case("$.data.ranks[0].trial", ValidationCode.InvalidValue, r => Rank(r, 0)["trial"] = "quest.bete_blanche");
        yield return Case("$.data.ranks[1].trial", ValidationCode.UnknownReference, r => Rank(r, 1)["trial"] = "quest.inconnue");
        yield return Case("$.data.ranks[2].trial", ValidationCode.DuplicateId, r => Rank(r, 2)["trial"] = TrialF);
        // Une épreuve avec une récompense au choix (la promotion vient à sa dernière étape) : refusée.
        yield return Case("$.data.ranks[1].trial", ValidationCode.InvalidValue, r => Rank(r, 1)["trial"] = "quest.peaux_pour_l_auberge");
        yield return Case("$.data.ranks[1].title", ValidationCode.UnknownReference, r => Rank(r, 1)["title"] = "title.inconnu");
        yield return Case("$.data.ranks[1].name", ValidationCode.UnknownReference, r => Rank(r, 1)["name"] = "text.inconnu");
        yield return Case("$.data.titles[1].id", ValidationCode.DuplicateId, r => r["data"]!["titles"]![1]!["id"] = "title.guilde.recrue");
        yield return Case("$.data.titles[0].id", ValidationCode.InvalidValue, r => r["data"]!["titles"]![0]!["id"] = "titre.recrue");
        // Contrats.
        yield return Case("$.data.contracts[0].id", ValidationCode.InvalidValue, r => Contract(r, 0)["id"] = "contrat.loups");
        yield return Case("$.data.contracts[1].id", ValidationCode.DuplicateId, r => Contract(r, 1)["id"] = (string)Contract(r, 0)["id"]!);
        yield return Case("$.data.contracts[0].rank", ValidationCode.UnknownReference, r => Contract(r, 0)["rank"] = "z");
        yield return Case("$.data.contracts[0].rank", ValidationCode.InvalidValue, r => Contract(r, 0)["rank"] = "d");
        yield return Case("$.data.contracts[0].kind", ValidationCode.InvalidValue, r => Contract(r, 0)["kind"] = "escorte");
        yield return Case("$.data.contracts[0].objective.kind", ValidationCode.InvalidValue, r => Contract(r, 0)["kind"] = "delivery");
        yield return Case("$.data.contracts[7].objective.kind", ValidationCode.InvalidValue, r => Contract(r, 7)["objective"]!["kind"] = "defeatFamily");
        yield return Case("$.data.contracts[0].objective.kind", ValidationCode.InvalidValue, r => Contract(r, 0)["objective"]!["kind"] = "collect");
        yield return Case("$.data.contracts[0].objective.id", ValidationCode.UnknownReference, r => Contract(r, 0)["objective"]!["id"] = "dragon");
        yield return Case("$.data.contracts[7].objective.id", ValidationCode.UnknownReference, r => Contract(r, 7)["objective"]!["id"] = "le_sans_nom");
        yield return Case("$.data.contracts[2].objective.id", ValidationCode.UnknownReference, r => Contract(r, 2)["objective"]!["id"] = "item.inconnu");
        yield return Case("$.data.contracts[2].objective.npc", ValidationCode.UnknownReference, r => Contract(r, 2)["objective"]!["npc"] = "npc.personne");
        yield return Case("$.data.contracts[0].objective.npc", ValidationCode.InvalidValue, r => Contract(r, 0)["objective"]!["npc"] = Mistress);
        yield return Case("$.data.contracts[0].objective.step", ValidationCode.InvalidValue, r => Contract(r, 0)["objective"]!["step"] = "x");
        yield return Case("$.data.contracts[0].objective.value", ValidationCode.OutOfRange, r => Contract(r, 0)["objective"]!["value"] = 0);
        yield return Case("$.data.contracts[0].objective.value", ValidationCode.MissingField, r => Contract(r, 0)["objective"]!.AsObject().Remove("value"));
        yield return Case("$.data.contracts[0].money", ValidationCode.OutOfRange, r => Contract(r, 0)["money"] = -1);
        yield return Case("$.data.contracts[0].points", ValidationCode.OutOfRange, r => Contract(r, 0)["points"] = 0);
        yield return Case("$.data.contracts[0].xp", ValidationCode.MissingField, r => Contract(r, 0).Remove("xp"));
        yield return Case("$.data.contracts[0].text", ValidationCode.UnknownReference, r => Contract(r, 0)["text"] = "text.inconnu");
        // Trop peu de contrats d'un rang pour le tirage du jour.
        yield return Case("$.data.contracts", ValidationCode.OutOfRange, r => r["data"]!["daily"] = 5);
        // Portes ouvertes par le rang.
        yield return Case("$.data.gates[0].zone", ValidationCode.UnknownReference, r => r["data"]!["gates"]![0]!["zone"] = "zone.nulle_part");
        yield return Case("$.data.gates[0].door", ValidationCode.UnknownReference, r => r["data"]!["gates"]![0]!["door"] = "cave");
        yield return Case("$.data.gates[0].rank", ValidationCode.InvalidValue, r => r["data"]!["gates"]![0]!["rank"] = "g");
        yield return Case("$.data.gates[0].rank", ValidationCode.InvalidValue, r => r["data"]!["gates"]![0]!["rank"] = "d");
        yield return Case("$.data.gates[1].door", ValidationCode.DuplicateId, r => r["data"]!["gates"]!.AsArray().Add(r["data"]!["gates"]![0]!.DeepClone()));
        // Points.
        yield return Case("$.data.points.mainQuest", ValidationCode.OutOfRange, r => r["data"]!["points"]!["mainQuest"] = 100_001);
        yield return Case("$.data.points.rare[0].creature", ValidationCode.UnknownReference, r => r["data"]!["points"]!["rare"]![0]!["creature"] = "dragon");
        yield return Case("$.data.points.facts[0].fact", ValidationCode.UnknownReference, r => r["data"]!["points"]!["facts"]![0]!["fact"] = "fact.jamais.appris");
        yield return Case("$.data.points.facts[1].fact", ValidationCode.DuplicateId, r => r["data"]!["points"]!["facts"]![1]!["fact"] = "fact.chef.vaincu");
    }

    [Theory]
    [MemberData(nameof(Mauvaises_guildes))]
    public void Guilde_invalide_refusee_avec_son_chemin(string path, ValidationCode code, Action<JsonObject> change)
    {
        JsonObject root = GuildJson();
        change(root);
        GuildLoadResult r = ReadGuild(root);
        Assert.False(r.IsValid);
        Assert.Null(r.Book);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Epreuve_commencee_par_l_histoire_refusee()
    {
        // Une épreuve ne se commence qu'au tableau de la guilde (qui vérifie le rang et les points) : un startQuest de l'histoire est refusé.
        JsonObject story = StoryJson();
        JsonObject anselme = story["data"]!["dialogues"]!.AsArray().Single(d => (string)d!["id"]! == "dialogue.pretre")!.AsObject();
        anselme["nodes"]![0]!["choices"]![0]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "startQuest", ["id"] = TrialF });
        GuildLoadResult r = ReadGuild(GuildJson(), ReadStory(story));
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal(("$.data.ranks[1].trial", ValidationCode.InvalidValue), (e.Path, e.Code));
        Assert.Contains("dialogue.pretre", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boutique_d_un_rang_ferme_refusee_par_la_guilde()
    {
        JsonObject merchants = MerchantsJson();
        JsonObject shop = merchants["data"]!["merchants"]!.AsArray().Single(m => (string)m!["id"]! == "merchant.guilde")!.AsObject();
        shop["offers"]![2]!["rank"] = "d";
        MerchantsLoadResult m = ReadMerchants(merchants);
        Assert.True(m.IsValid, m.Report.ToString());   // le rang D existe : les marchands l'acceptent
        GuildLoadResult r = ReadGuild(GuildJson(), merchants: m.Book);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.ranks" && e.Message.Contains("item.heaume_du_guetteur", StringComparison.Ordinal));
    }

    // --- Histoire en schéma 4, marchands en schéma 2 --------------------------------------------------------------------

    [Fact]
    public void Histoire_en_schema_4_avec_rang_et_points_de_guilde()
    {
        StoryBook book = ShippedStory.Value;
        Assert.Equal(4, book.SchemaVersion);
        Assert.Equal((ObjectiveKind.GuildRank, "e"), (book.Quest("quest.registres_de_l_archiviste").Steps[0].Objective.Kind, book.Quest("quest.registres_de_l_archiviste").Steps[0].Objective.Id));
        Dialogue archiviste = book.Dialogue("dialogue.archiviste");
        Assert.Contains(archiviste.Nodes.SelectMany(n => n.Choices).SelectMany(c => c.Effects), e => e.Kind == EffectKind.GiveGuildPoints && e.Value == 20);
        Assert.Contains(book.Dialogue("dialogue.maitresse_guilde").Entries, e => e.Conditions.Any(c => c.Kind == ConditionKind.GuildRankAtLeast && c.Id == "e"));
        // Les trois PNJ de la guilde, dans leurs pièces ; les épreuves sont des quêtes secondaires.
        Assert.Equal(new[] { ("npc.maitresse_guilde", "zone.guilde"), ("npc.intendant_guilde", "zone.guilde"), ("npc.archiviste", "zone.archives") },
            book.Npcs.Where(n => n.Zone is "zone.guilde" or "zone.archives").Select(n => (n.Id, n.Zone!)));
        Assert.Equal(QuestCategory.Side, book.Quest(TrialF).Category);
        // Schéma 3 refusé ; les nouvelles natures vérifiées avec leur chemin.
        JsonObject old = StoryJson();
        old["schemaVersion"] = 3;
        Assert.Equal("$.schemaVersion", Assert.Single(StoryJsonReader.ReadStory(old.ToJsonString(), Texts, Vallons.Value, ItemsCatalog, V16TestData.Interiors).Report.Errors).Path);
        void Refused(string path, ValidationCode code, Action<JsonObject> change)
        {
            JsonObject root = StoryJson();
            change(root);
            StoryLoadResult r = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons.Value, ItemsCatalog, V16TestData.Interiors);
            Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
        }
        JsonObject Registres(JsonObject r) => r["data"]!["quests"]!.AsArray().Single(q => (string)q!["id"]! == "quest.registres_de_l_archiviste")!.AsObject();
        int index = StoryJson()["data"]!["quests"]!.AsArray().ToList().FindIndex(q => (string)q!["id"]! == "quest.registres_de_l_archiviste");
        Refused($"$.data.quests[{index}].steps[0].objective.id", ValidationCode.UnknownReference, r => Registres(r)["steps"]![0]!["objective"]!["id"] = "z");
        Refused($"$.data.quests[{index}].steps[0].objective.value", ValidationCode.InvalidValue, r => Registres(r)["steps"]![0]!["objective"]!["value"] = 2);
        Refused($"$.data.quests[{index}].effects[0].value", ValidationCode.OutOfRange,
            r => Registres(r)["effects"]![0] = new JsonObject { ["do"] = "giveGuildPoints", ["value"] = GuildRanks.MaxAward + 1 });
        Refused($"$.data.quests[{index}].effects[0].id", ValidationCode.InvalidValue,
            r => Registres(r)["effects"]![0] = new JsonObject { ["do"] = "giveGuildPoints", ["id"] = "x", ["value"] = 1 });
        Refused($"$.data.quests[{index}].requires[0].id", ValidationCode.UnknownReference,
            r => Registres(r)["requires"] = new JsonArray(new JsonObject { ["if"] = "guildRankAtLeast", ["id"] = "rang_f" }));
        Refused($"$.data.quests[{index}].requires[0].value", ValidationCode.InvalidValue,
            r => Registres(r)["requires"] = new JsonArray(new JsonObject { ["if"] = "guildRankAtLeast", ["id"] = "f", ["value"] = 1 }));
    }

    [Fact]
    public void Marchands_en_schema_2_avec_la_reserve_de_la_guilde()
    {
        MerchantBook book = ShippedMerchants;
        Assert.Equal(2, book.SchemaVersion);
        Merchant shop = book.Merchant("merchant.guilde");
        Assert.Equal(("npc.intendant_guilde", "g"), (shop.Npc, shop.Rank));
        Assert.Equal(new string?[] { null, null, "f", "f", "f", "f", "e", "e" }, shop.Offers.Select(o => o.Rank));
        Assert.All(book.Merchants.Where(m => m.Id != "merchant.guilde"), m => Assert.Null(m.Rank));
        // Une potion moins chère qu'ailleurs : un avantage de membre ; jamais sous la valeur de base.
        Assert.True(shop.Offers.Single(o => o.Item == "item.potion_de_soin").Price < book.Merchant("merchant.herboriste").Offers.Single(o => o.Item == "item.potion_de_soin").Price);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "merchants.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(MerchantsPath)).RootElement));
        JsonObject Shop(JsonObject r) => r["data"]!["merchants"]!.AsArray().Single(m => (string)m!["id"]! == "merchant.guilde")!.AsObject();
        int i = MerchantsJson()["data"]!["merchants"]!.AsArray().ToList().FindIndex(m => (string)m!["id"]! == "merchant.guilde");
        void Refused(string path, ValidationCode code, Action<JsonObject> change)
        {
            JsonObject root = MerchantsJson();
            change(root);
            Assert.Contains(Errors(ReadMerchants(root)), e => e.Path == path && e.Code == code);
        }
        Refused($"$.data.merchants[{i}].rank", ValidationCode.UnknownReference, r => Shop(r)["rank"] = "h");
        Refused($"$.data.merchants[{i}].offers[2].rank", ValidationCode.UnknownReference, r => Shop(r)["offers"]![2]!["rank"] = "rang_f");
        Refused($"$.data.merchants[{i}].offers[2].rank", ValidationCode.InvalidValue, r => Shop(r)["offers"]![2]!["rank"] = "g");
        JsonObject old = MerchantsJson();
        old["schemaVersion"] = 1;
        Assert.Equal("$.schemaVersion", Assert.Single(Errors(ReadMerchants(old))).Path);
    }

    // --- Sauvegarde en version 10 -------------------------------------------------------------------------------------

    private static SavedGuildCard FullCard => new SavedGuildCard("f", 140, 3, new[] { "title.guilde.recrue", "title.guilde.eclaireur" }, "title.guilde.eclaireur",
        new Dictionary<string, long> { ["contract.grande_battue"] = 2, ["contract.loups_du_pre"] = 0 }, 4, new[] { "contract.loups_du_pre", "contract.grande_battue" });

    /// <summary>Le héros avec la carte complète et une créature rare déjà vaincue (relecture de la PR #79 : gardée dans l'histoire).</summary>
    private static SavedCharacter FullHero => Hero().WithStory(new SavedStory(guild: FullCard, hunted: new[] { "loup_blanc" }));

    [Fact]
    public void Carte_sauvegardee_en_version_10()
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.guilde", 10, 1, Array.Empty<string>(), FullHero));
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);
        Assert.Equal(SaveValidator.GuildCardSince, 10);   // la carte entre en version 10 ; la version courante est 11 depuis V1.4
        Assert.Contains("        \"tracked\": null,\n        \"hunted\": [\"loup_blanc\"],\n        \"guild\": {\"rank\": \"f\", \"points\": 140, \"contracts\": 3, "
            + "\"titles\": [\"title.guilde.eclaireur\", \"title.guilde.recrue\"], \"title\": \"title.guilde.eclaireur\", \"active\": {\"contract.grande_battue\": 2, "
            + "\"contract.loups_du_pre\": 0}, \"day\": 4, \"taken\": [\"contract.grande_battue\", \"contract.loups_du_pre\"]}\n      },\n", text, StringComparison.Ordinal);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.False(read.Save!.Migrated);
        SavedGuildCard card = read.Save.Character!.Story.Guild!;
        Assert.Equal(("f", 140L, 3L, "title.guilde.eclaireur", 4L), (card.Rank, card.Points, card.Contracts, card.Title, card.Day));
        Assert.Equal(2, card.Active["contract.grande_battue"]);
        Assert.Equal(new[] { "loup_blanc" }, read.Save.Character.Story.Hunted);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        // Créatures rares sans carte (vaincues avant l'inscription) : la ligne « hunted » seule.
        string before = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithStory(new SavedStory(hunted: new[] { "loup_blanc" }))));
        Assert.Contains("        \"tracked\": null,\n        \"hunted\": [\"loup_blanc\"]\n      },\n", before, StringComparison.Ordinal);
        Assert.Equal(before, SaveJsonReader.Write(SaveJsonReader.Read(before).Save!));
        // Sans carte : pas de ligne « guild » (le personnage n'est pas inscrit) ; le reste ne change pas.
        string none = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        Assert.DoesNotContain("guild", none, StringComparison.Ordinal);
        Assert.Null(SaveJsonReader.Read(none).Save!.Character!.Story.Guild);
    }

    [Fact]
    public void Anciennes_sauvegardes_lues_sans_carte()
    {
        // Version 8 (V1.6) et 9 (V1.3) : même forme sans carte, lues et mises à jour ; une carte y est un champ inconnu, avec sa version.
        string none = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        string card = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), WithCard(Hero(), FullCard)));
        string hunted = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithStory(new SavedStory(hunted: new[] { "loup_blanc" }))));
        foreach (int version in new[] { 9, 8 })
        {
            ValidationError beast = Assert.Single(SaveJsonReader.Read(SaveV11.Older(hunted).Replace(SaveVersion.Current, $"\"schemaVersion\": {version}")).Report.Errors);
            Assert.Equal(("$.data.character.story.hunted", ValidationCode.UnknownField), (beast.Path, beast.Code));
            SaveLoadResult old = SaveJsonReader.Read(SaveV11.Older(none).Replace(SaveVersion.Current, $"\"schemaVersion\": {version}"));
            Assert.True(old.IsValid, old.Report.ToString());
            Assert.True(old.Save!.Migrated);
            Assert.Null(old.Save.Character!.Story.Guild);
            Assert.Equal(none, SaveJsonReader.Write(old.Save));
            ValidationError unknown = Assert.Single(SaveJsonReader.Read(SaveV11.Older(card).Replace(SaveVersion.Current, $"\"schemaVersion\": {version}")).Report.Errors);
            Assert.Equal(("$.data.character.story.guild", ValidationCode.UnknownField), (unknown.Path, unknown.Code));
            Assert.Contains("version 10", unknown.Message, StringComparison.Ordinal);
        }
        // Une version suivante est refusée.
        Assert.Equal("$.schemaVersion", Assert.Single(SaveJsonReader.Read(card.Replace(SaveVersion.Current, SaveVersion.Future)).Report.Errors).Path);
    }

    [Theory]
    [InlineData("\"rank\": \"f\"", "\"rank\": \"h\"", "$.data.character.story.guild.rank", ValidationCode.InvalidValue)]
    [InlineData("\"rank\": \"f\", ", "", "$.data.character.story.guild.rank", ValidationCode.MissingField)]
    [InlineData("\"points\": 140", "\"points\": -1", "$.data.character.story.guild.points", ValidationCode.OutOfRange)]
    [InlineData("\"points\": 140", "\"points\": 10000001", "$.data.character.story.guild.points", ValidationCode.OutOfRange)]
    [InlineData("\"contracts\": 3", "\"contracts\": -3", "$.data.character.story.guild.contracts", ValidationCode.OutOfRange)]
    [InlineData("\"title\": \"title.guilde.eclaireur\"", "\"title\": \"title.guilde.lame\"", "$.data.character.story.guild.title", ValidationCode.UnknownReference)]
    [InlineData("\"title\": \"title.guilde.eclaireur\", ", "", "$.data.character.story.guild.title", ValidationCode.MissingField)]
    [InlineData("[\"title.guilde.eclaireur\", \"title.guilde.recrue\"]", "[\"title.guilde.recrue\", \"title.guilde.eclaireur\"]", "$.data.character.story.guild.titles[1]", ValidationCode.InvalidValue)]
    [InlineData("[\"title.guilde.eclaireur\", \"title.guilde.recrue\"]", "[\"titre\", \"title.guilde.recrue\"]", "$.data.character.story.guild.titles[0]", ValidationCode.InvalidValue)]
    [InlineData("\"contract.grande_battue\": 2", "\"contrat.battue\": 2", "$.data.character.story.guild.active.contrat.battue", ValidationCode.InvalidValue)]
    [InlineData("\"contract.grande_battue\": 2", "\"contract.grande_battue\": -2", "$.data.character.story.guild.active.contract.grande_battue", ValidationCode.OutOfRange)]
    [InlineData("\"day\": 4", "\"day\": -1", "$.data.character.story.guild.day", ValidationCode.OutOfRange)]
    [InlineData("[\"loup_blanc\"]", "[\"Loup blanc\"]", "$.data.character.story.hunted[0]", ValidationCode.InvalidValue)]
    [InlineData("[\"loup_blanc\"]", "[]", "$.data.character.story.hunted", ValidationCode.OutOfRange)]
    [InlineData("[\"loup_blanc\"]", "[\"loup_blanc\", \"loup_blanc\"]", "$.data.character.story.hunted[1]", ValidationCode.InvalidValue)]
    [InlineData("\"day\": 4", "\"day\": 4, \"extra\": 1", "$.data.character.story.guild.extra", ValidationCode.UnknownField)]
    [InlineData("\"guild\": {", "\"guild\": null, \"x\": {", "$.data.character.story.guild", ValidationCode.WrongType)]
    public void Carte_invalide_refusee_avec_son_chemin(string from, string to, string path, ValidationCode code)
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.guilde", 10, 1, Array.Empty<string>(), FullHero));
        Assert.Contains(from, text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text.Replace(from, to, StringComparison.Ordinal));
        Assert.False(read.IsValid);
        Assert.Contains(read.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Carte_en_objets_publics_bornee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedGuildCard("g", SavedGuildCard.MaxPoints + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedGuildCard("g", contracts: -1));
        Assert.Throws<ArgumentException>(() => new SavedGuildCard("g", title: "title.guilde.lame"));
        Assert.Equal(new[] { "a", "b" }, new SavedGuildCard("g", taken: new[] { "b", "a", "b" }).Taken);
        // Trop de contrats en cours dans une sauvegarde écrite à la main : refusé, avec la borne.
        var tooMany = new StoryStateData
        {
            Facts = new List<string?>(), Discoveries = new List<string?>(), Quests = new List<(string, QuestProgressData?)>(),
            Trust = new List<KeyValuePair<string, int?>>(), HasTracked = true, HasGuild = true,
            Guild = new GuildCardData
            {
                Rank = "g", Points = 0, Contracts = 0, Titles = new List<string?>(), HasTitle = true, Day = 0, Taken = new List<string?>(),
                Active = Enumerable.Range(0, 9).Select(n => new KeyValuePair<string, long?>("contract.c" + n, 0)).ToList(),
            },
        };
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        var report = new ValidationReport();
        var document = new SaveDocument();
        JsonHeader header = JsonInput.Parse(JsonInput.Utf8(text, report)!, report, (input, element) => document.Data = SaveJsonReader.ReadData(input, element, "$.data"));
        (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
        document.Data!.Character!.Story = tooMany;
        SaveLoadResult r = SaveValidator.Validate(document);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.character.story.guild.active" && e.Code == ValidationCode.LimitExceeded);
    }

    [Fact]
    public void Carte_qui_ne_va_pas_avec_la_guilde_refusee_avant_la_partie()
    {
        // Rang fermé, titre ou contrat inconnus : la sauvegarde ne va pas avec ces données (refus avant toute session).
        Assert.Throws<ArgumentException>(() => InGuild(WithCard(Hero(), new SavedGuildCard("d", titles: new[] { "title.guilde.recrue" }))));
        Assert.Throws<ArgumentException>(() => InGuild(WithCard(Hero(), new SavedGuildCard("g", titles: new[] { "title.inconnu" }))));
        Assert.Throws<ArgumentException>(() => InGuild(WithCard(Hero(), new SavedGuildCard("g", active: new Dictionary<string, long> { ["contract.inconnu"] = 0 }))));
        // Sans guilde chargée, la carte est gardée telle quelle.
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), FullHero), Vallons.Value, ShippedData, Progression,
            ItemsCatalog, ShippedStory.Value);
        Assert.Null(session.Guild);
        SavedGuildCard kept = session.Save().Character!.Story.Guild!;
        Assert.Equal((FullCard.Rank, FullCard.Points, FullCard.Title, FullCard.Active.Count), (kept.Rank, kept.Points, kept.Title, kept.Active.Count));
        Assert.Equal(new[] { "loup_blanc" }, session.Save().Character!.Story.Hunted);
        Assert.Equal(GuildRefusal.NoGuild, session.CanUseGuild());
    }

    // --- Inscription, tableau, contrats ---------------------------------------------------------------------------------

    [Fact]
    public void Inscription_aupres_de_la_maitresse()
    {
        WorldSession far = InGuild(Hero(), near: false);
        Assert.Equal(GuildRefusal.TooFar, far.JoinGuild(out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        Assert.False(far.Narrative!.IsMember);

        WorldSession w = InGuild(Hero());
        Assert.Equal(GuildRefusal.None, w.CanUseGuild());
        Assert.Equal(GuildRefusal.NotMember, w.TakeContract(w.Board()[0].Id, out _));
        Assert.Equal(GuildRefusal.NotMember, w.CanStartTrial());
        Assert.Equal(GuildRefusal.None, w.JoinGuild(out IReadOnlyList<StoryEvent> joined));
        Assert.Equal(new[] { "GuildJoined g" }, joined.Select(e => e.ToString()));
        Narrative n = w.Narrative!;
        Assert.Equal(("g", 0L, "title.guilde.recrue"), (n.GuildRankId, n.GuildPoints, n.ShownTitle));
        Assert.Equal(GuildRefusal.AlreadyMember, w.JoinGuild(out _));
        // Gardée dans la sauvegarde, relue à la session suivante.
        SavedGuildCard card = w.Save().Character!.Story.Guild!;
        Assert.Equal("g", card.Rank);
        Assert.Equal(new[] { "title.guilde.recrue" }, card.Titles);
        WorldSession again = InGuild(w.Save().Character!);
        Assert.True(again.Narrative!.IsMember);
    }

    [Fact]
    public void Tableau_du_jour_deterministe_et_selon_le_rang()
    {
        GuildBook g = Guild;
        // Même jour, même rang : même tableau ; deux contrats par rang ouvert jusqu'au sien, du plus bas au plus haut.
        for (long day = 0; day < 20; day++)
        {
            IReadOnlyList<GuildContract> board = GuildBoard.Of(g, 2, day);
            Assert.Equal(board.Select(c => c.Id), GuildBoard.Of(g, 2, day).Select(c => c.Id));
            Assert.Equal(new[] { "g", "g", "f", "f", "e", "e" }, board.Select(c => c.Rank));
            Assert.Equal(board.Take(2).Select(c => c.Id), GuildBoard.Of(g, 0, day).Select(c => c.Id));   // un rang plus haut garde ceux d'en dessous
            // Dans l'ordre du fichier, sans doublon.
            Assert.Equal(board.Select(c => g.Contracts.ToList().IndexOf(c)).OrderBy(i => i), board.Select(c => g.Contracts.ToList().IndexOf(c)));
        }
        // Le tableau change d'un jour à l'autre (au moins une fois en vingt jours), et chaque contrat d'un rang finit par venir.
        Assert.Contains(Enumerable.Range(1, 19), d => !GuildBoard.Of(g, 0, d).Select(c => c.Id).SequenceEqual(GuildBoard.Of(g, 0, 0).Select(c => c.Id)));
        Assert.All(g.Contracts.Where(c => c.Rank == "g"), c => Assert.Contains(Enumerable.Range(0, 60).SelectMany(d => GuildBoard.Of(g, 0, d)), x => x.Id == c.Id));
        Assert.Equal("guild/f/day/3", GuildBoard.DayKey("f", 3));
        // Dans la partie : le jour du monde, jamais l'horloge réelle.
        WorldSession w = InGuild(WithCard(Hero(), Card("f")));
        Assert.Equal(GuildBoard.Of(g, 1, w.Day).Select(c => c.Id), w.Board().Select(c => c.Id));
        WorldSession later = InGuild(WithCard(Hero(), Card("f")), tick: DayTicks);
        Assert.Equal(w.Day + 1, later.Day);
    }

    [Fact]
    public void Contrats_pris_abandonnes_et_bornes()
    {
        WorldSession w = InGuild(WithCard(Hero(), Card("e", 400, "title.guilde.lame", null, 0, null, "title.guilde.recrue", "title.guilde.eclaireur", "title.guilde.lame")));
        IReadOnlyList<GuildContract> board = w.Board();
        Assert.Equal(6, board.Count);
        Assert.Equal(GuildRefusal.NotOnBoard, w.TakeContract("contract.inconnu", out _));
        string offBoard = Guild.Contracts.First(c => board.All(b => b.Id != c.Id)).Id;
        Assert.Equal(GuildRefusal.NotOnBoard, w.TakeContract(offBoard, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(board[0].Id, out IReadOnlyList<StoryEvent> taken));
        Assert.Equal(new[] { $"ContractTaken {board[0].Id}" }, taken.Select(e => e.ToString()));
        Assert.Equal(GuildRefusal.AlreadyTaken, w.TakeContract(board[0].Id, out _));
        Assert.Equal(GuildRefusal.AlreadyTaken, w.CanTakeContract(board[0].Id));
        Assert.Equal(GuildRefusal.None, w.TakeContract(board[1].Id, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(board[2].Id, out _));
        // Trois en cours au plus (maxActive) : le quatrième attend, et rien ne change.
        string before = SaveJsonReader.Write(w.Save());
        Assert.Equal(GuildRefusal.TooManyContracts, w.CanTakeContract(board[3].Id));
        Assert.Equal(GuildRefusal.TooManyContracts, w.TakeContract(board[3].Id, out IReadOnlyList<StoryEvent> refused));
        Assert.Empty(refused);
        Assert.Equal(before, SaveJsonReader.Write(w.Save()));
        // Abandon (où que l'on soit) : il ne se reprend pas le même jour, mais un autre prend sa place.
        Assert.Equal(GuildRefusal.None, w.AbandonContract(board[0].Id, out IReadOnlyList<StoryEvent> abandoned));
        Assert.Equal(new[] { $"ContractAbandoned {board[0].Id}" }, abandoned.Select(e => e.ToString()));
        Assert.Equal(GuildRefusal.NotActive, w.AbandonContract(board[0].Id, out _));
        Assert.Equal(GuildRefusal.AlreadyTaken, w.TakeContract(board[0].Id, out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract(board[3].Id, out _));
        Assert.Equal(new[] { board[1].Id, board[2].Id, board[3].Id }.OrderBy(i => i, StringComparer.Ordinal), w.Narrative!.ActiveContracts.Select(a => a.Contract));
        // Le lendemain, le contrat abandonné peut revenir au tableau et se reprendre (s'il est tiré).
        SavedCharacter hero = w.Save().Character!;
        Assert.Equal(new[] { board[0].Id, board[1].Id, board[2].Id, board[3].Id }.OrderBy(i => i, StringComparer.Ordinal), hero.Story.Guild!.Taken);
        WorldSession tomorrow = InGuild(hero, tick: DayTicks);
        Assert.Equal(GuildRefusal.None, tomorrow.AbandonContract(board[1].Id, out _));
        string again = tomorrow.Board().Select(c => c.Id).First(id => tomorrow.Narrative!.ActiveContracts.All(a => a.Contract != id));
        Assert.Equal(GuildRefusal.None, tomorrow.TakeContract(again, out _));
        // Relecture de la PR #79 : l'abandon compte comme une prise du jour (il ne se reprend pas avant le lendemain).
        Assert.Equal(new[] { board[1].Id, again }.OrderBy(i => i, StringComparer.Ordinal), tomorrow.Save().Character!.Story.Guild!.Taken);
    }

    [Fact]
    public void Chasse_reussie_avec_ses_recompenses()
    {
        WorldSession w = InGuild(WithCard(Hero(), Card("g", 10)));
        GuildContract hunt = Guild.Contract("contract.loups_du_pre");
        // Pris le jour où il est au tableau (le jour 0 ou un autre).
        long day = Enumerable.Range(0, 60).First(d => GuildBoard.Of(Guild, 0, d).Contains(hunt));
        w = InGuild(WithCard(Hero(), Card("g", 10)), tick: day * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract(hunt.Id, out _));
        w.Narrative!.Defeated("sanglier", "sanglier");   // une autre famille ne compte pas
        w.Narrative.Defeated("loup1", "loup");
        w.Narrative.Defeated("loup2_v3", "loup");
        Assert.Equal(2, w.Narrative.ActiveContracts.Single().Progress);
        Assert.Empty(Steps(w, 2));
        long xp = w.Character.Xp;
        w.Narrative.Defeated("loup3", "loup");
        Assert.Equal(new[] { $"ContractDone {hunt.Id}", "MoneyGiven money 80", "XpGiven xp 40", $"GuildPoints {hunt.Id} 12" }, Steps(w));
        Assert.Equal((80L, xp + 40, 22L, 1L), (w.Money, w.Character.Xp, w.Narrative.GuildPoints, w.Narrative.ContractsDone));
        Assert.Empty(w.Narrative.ActiveContracts);
        // Déjà pris aujourd'hui : il ne revient pas avant demain.
        Assert.Equal(GuildRefusal.AlreadyTaken, w.TakeContract(hunt.Id, out _));
    }

    [Fact]
    public void Livraison_remise_a_la_maitresse_tout_ou_rien()
    {
        GuildContract herbs = Guild.Contract("contract.herbes_pour_sauge");
        long day = Enumerable.Range(0, 60).First(d => GuildBoard.Of(Guild, 0, d).Contains(herbs));
        WorldSession w = InGuild(WithCard(Hero(0, new ItemStack("item.herbe_de_l_aube", 2)), Card("g")), tick: day * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract(herbs.Id, out _));
        // Il en manque une : rien n'est promis.
        Assert.Equal(GuildRefusal.None, w.VisitGuild());
        Assert.Empty(Steps(w));
        Assert.Equal(0, w.Narrative!.ActiveContracts.Single().Progress);
        w.Inventory.Add("item.herbe_de_l_aube", 1);
        // Remises d'un coup au tick suivant, avec les récompenses.
        Assert.Equal(GuildRefusal.None, w.VisitGuild());
        Assert.Equal(1, w.Narrative.ActiveContracts.Single().Progress);
        Assert.Equal(new[] { $"ContractDone {herbs.Id}", "ItemTaken item.herbe_de_l_aube 3", "MoneyGiven money 70", "XpGiven xp 30", $"GuildPoints {herbs.Id} 10" }, Steps(w));
        Assert.Equal(0, w.Inventory.Count("item.herbe_de_l_aube"));
        // Bourse pleine : le contrat attend (annoncé une fois), les herbes restent.
        WorldSession full = InGuild(WithCard(Hero(Coins.Max, new ItemStack("item.herbe_de_l_aube", 3)), Card("g")), tick: day * DayTicks);
        Assert.Equal(GuildRefusal.None, full.TakeContract(herbs.Id, out _));
        full.VisitGuild();
        Assert.Equal(new[] { $"PurseFull {herbs.Id}" }, Steps(full, 4));
        Assert.Equal(3, full.Inventory.Count("item.herbe_de_l_aube"));
        Assert.Single(full.Narrative!.ActiveContracts);
        // Les herbes promises quittent le sac : la livraison est à refaire.
        full.Inventory.Remove("item.herbe_de_l_aube", 1);
        Steps(full, 1);
        Assert.Equal(0, full.Narrative.ActiveContracts.Single().Progress);
    }

    // --- Épreuves, promotion, titres, avantages ---------------------------------------------------------------------------

    [Fact]
    public void Epreuve_ouverte_par_les_points_puis_promotion()
    {
        WorldSession poor = InGuild(WithCard(Hero(), Card("g", 99)));
        Assert.Equal(GuildRefusal.NotEnoughPoints, poor.CanStartTrial());
        Assert.Equal(GuildRefusal.NotEnoughPoints, poor.StartTrial(out _));
        Assert.False(poor.Narrative!.IsOpen(TrialF));   // une épreuve n'est pas ouverte sans les points

        WorldSession w = InGuild(WithCard(Hero(0, new ItemStack("item.peau_de_loup", 3)), Card("g", 100)));
        Assert.True(w.Narrative!.IsOpen(TrialF));
        Assert.Equal(GuildRefusal.None, w.StartTrial(out IReadOnlyList<StoryEvent> started));
        Assert.Equal(new[] { $"QuestStarted {TrialF}/conseil" }, started.Select(e => e.ToString()));
        Assert.Equal(GuildRefusal.TrialActive, w.CanStartTrial());
        // Mission : Gaspard (hors de portée ici : son objectif est rempli comme une quête), quatre loups, trois peaux remises à la maîtresse.
        SavedCharacter hero = w.Save().Character!;
        var progress = new Dictionary<string, QuestProgress> { [TrialF] = new QuestProgress("preuves") };
        w = InGuild(WithCard(hero, hero.Story.Guild, progress));
        Assert.Equal(GuildRefusal.None, w.VisitGuild());
        List<string> events = Steps(w);
        Assert.Equal(new[] { $"ObjectiveDone {TrialF}/preuves", $"StepCompleted {TrialF}/preuves", "ItemTaken item.peau_de_loup 3", "XpGiven xp 150", $"QuestCompleted {TrialF}",
            "GuildPromoted f" }, events);
        Narrative n = w.Narrative!;
        Assert.Equal(("f", "title.guilde.eclaireur"), (n.GuildRankId, n.ShownTitle));
        Assert.Equal(new[] { "title.guilde.eclaireur", "title.guilde.recrue" }, n.Titles);
        Assert.Equal(100, n.GuildPoints);   // les points restent ; l'épreuve n'en donne pas (elle donne le rang)
        Assert.Equal(GuildRefusal.NotEnoughPoints, w.CanStartTrial());   // E demande 300
        Assert.Equal(4, w.Board().Count);   // le tableau du rang F : ceux de G et de F
    }

    [Fact]
    public void Plus_haut_rang_des_Vallons_et_rangs_fermes()
    {
        WorldSession w = InGuild(WithCard(Hero(), Card("e", 5000, null, null, 0, null, "title.guilde.recrue")));
        Assert.Equal(GuildRefusal.TopRank, w.CanStartTrial());
        Assert.Equal("d", w.Narrative!.NextRank!.Id);
        Assert.False(w.Narrative.NextRank.Open);
        Assert.Equal(GuildRefusal.TopRank, w.StartTrial(out _));
        Assert.All(w.Board(), c => Assert.Contains(c.Rank, new[] { "g", "f", "e" }));
    }

    [Fact]
    public void Titre_affiche_au_choix()
    {
        WorldSession w = InGuild(WithCard(Hero(), Card("f", 0, "title.guilde.recrue", null, 0, null, "title.guilde.recrue", "title.guilde.eclaireur")), near: false);
        Assert.Equal(GuildRefusal.None, w.ShowTitle("title.guilde.eclaireur", out IReadOnlyList<StoryEvent> shown));   // loin de la maîtresse aussi
        Assert.Equal(new[] { "TitleShown title.guilde.eclaireur" }, shown.Select(e => e.ToString()));
        Assert.Equal(GuildRefusal.UnknownTitle, w.ShowTitle("title.guilde.lame", out _));
        Assert.Equal("title.guilde.eclaireur", w.Narrative!.ShownTitle);
        Assert.Equal(GuildRefusal.None, w.ShowTitle(null, out _));
        Assert.Null(w.Save().Character!.Story.Guild!.Title);
        Assert.Equal(GuildRefusal.NotMember, InGuild(Hero()).ShowTitle(null, out _));
    }

    [Fact]
    public void Porte_des_archives_ouverte_au_rang_F()
    {
        Assert.Equal("f", InGuild(WithCard(Hero(), Card("g"))).DoorNeedsRank("archives"));
        Assert.Equal("f", InGuild(Hero()).DoorNeedsRank("archives"));
        Assert.Null(InGuild(Hero()).DoorNeedsRank("sortie"));
        Assert.Null(InGuild(WithCard(Hero(), Card("f", 0, null, null, 0, null, "title.guilde.recrue"))).DoorNeedsRank("archives"));
        Assert.Null(InGuild(WithCard(Hero(), Card("e", 0, null, null, 0, null, "title.guilde.recrue"))).DoorNeedsRank("archives"));
    }

    [Fact]
    public void Reserve_de_la_guilde_selon_le_rang()
    {
        var market = new Market(ShippedMerchants);
        // Non inscrit : la réserve reste fermée.
        WorldSession outsider = InGuild(Hero(10_000), market: market);
        Assert.Equal(TradeRefusal.RankTooLow, outsider.CanTrade("merchant.guilde"));
        // Rang G : potions oui, équipement de rang F non (rien ne change).
        WorldSession g = InGuild(WithCard(Hero(10_000), Card("g")), market: market);
        Assert.Equal(TradeRefusal.None, g.CanTrade("merchant.guilde"));
        Assert.Equal(TradeRefusal.None, g.Buy("merchant.guilde", "item.potion_de_soin"));
        Assert.Equal(10_000 - 50, g.Money);
        string before = Snapshot(g);
        Assert.Equal(TradeRefusal.RankTooLow, g.Buy("merchant.guilde", "item.heaume_du_guetteur"));
        Assert.Equal(before, Snapshot(g));
        // Rang F : l'offre de rang F s'achète ; celle de rang E non.
        WorldSession f = InGuild(WithCard(Hero(10_000), Card("f", 0, null, null, 0, null, "title.guilde.recrue")), market: market);
        Assert.Equal(TradeRefusal.None, f.Buy("merchant.guilde", "item.heaume_du_guetteur"));
        Assert.Equal(TradeRefusal.RankTooLow, f.Buy("merchant.guilde", "item.cuirasse_du_veneur"));
        // L'intendant est dans la pièce : de loin, trop loin.
        Assert.Equal(TradeRefusal.TooFar, InGuild(WithCard(Hero(10_000), Card("g")), near: false, market: market).CanTrade("merchant.guilde"));
    }

    // --- Points de guilde ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Points_des_accomplissements_seulement_une_fois_inscrit()
    {
        // Créature rare : la première victoire seulement, une fois inscrit.
        WorldSession w = InGuild(WithCard(Hero(), Card("g")));
        w.Narrative!.Defeated("loup_blanc", "loup_blanc");
        w.Narrative.Defeated("loup_blanc_v2", "loup_blanc");
        w.Narrative.Defeated("loup_blanc", "loup_blanc");
        Assert.Equal(new[] { "GuildPoints loup_blanc 25" }, Steps(w));
        Assert.Equal(new[] { "loup_blanc" }, w.Save().Character!.Story.Hunted);
        // Fait d'un chef, découverte et effet de l'histoire (l'archiviste) ; une quête secondaire finie.
        var list = new List<StoryEvent>();
        w.Narrative.Learn("fact.chef.vaincu", list);
        Assert.Equal(new[] { "FactLearned fact.chef.vaincu", "GuildPoints fact.chef.vaincu 60" }, list.Select(e => e.ToString()));
        Assert.Equal(85, w.Narrative.GuildPoints);
        // Pas inscrit : rien n'est compté. Relecture de la PR #79 (P5) : la créature rare est gardée comme déjà vaincue, et ne rapporte rien
        // non plus après l'inscription ; rien de ce qui est fait avant l'inscription ne compte jamais.
        WorldSession outsider = InGuild(Hero());
        outsider.Narrative!.Defeated("loup_blanc", "loup_blanc");
        outsider.Narrative.Learn("fact.chef.vaincu", new List<StoryEvent>());
        Assert.Empty(Steps(outsider));
        Assert.Null(outsider.Save().Character!.Story.Guild);
        Assert.Equal(new[] { "loup_blanc" }, outsider.Save().Character!.Story.Hunted);
        Assert.Equal(GuildRefusal.None, outsider.JoinGuild(out _));
        outsider.Narrative.Defeated("loup_blanc", "loup_blanc");
        Assert.Empty(Steps(outsider));
        Assert.Equal(0, outsider.Narrative.GuildPoints);
        // Plafond de la carte : le gain s'arrête, jamais de dépassement.
        WorldSession top = InGuild(WithCard(Hero(), Card("g", SavedGuildCard.MaxPoints - 10)));
        top.Narrative!.Learn("fact.chef.vaincu", new List<StoryEvent>());
        Assert.Equal(SavedGuildCard.MaxPoints, top.Narrative.GuildPoints);
    }

    [Fact]
    public void Archives_effet_points_de_guilde_et_objectif_rang()
    {
        // Dans les archives (rang F), l'archiviste : consulter les registres donne 20 points et la découverte (5), et la quête « rang E ».
        StoryBook near = ReadStory(MoveNpc(StoryJson(), "npc.archiviste", -1.4, 0));
        var session = new WorldSession(new WorldSave("zone.archives", 0, 0, Array.Empty<string>(), WithCard(Hero(), Card("f", 0, null, null, 0, null, "title.guilde.recrue"))),
            Inside("zone.archives"), ShippedData, Progression, ItemsCatalog, near, door: "sortie", storyZone: "zone.vallons", guild: Guild);
        session.Step();
        Assert.Equal(DialogueRefusal.None, session.Talk("npc.archiviste", out DialogueView? view));
        Assert.Equal("accueil", view!.Node);
        Assert.Equal(DialogueRefusal.None, session.Choose(0, out DialogueView? registres, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(new[] { "FactLearned fact.archives.consultees", "Discovered discovery.archives_de_la_guilde", "GuildPoints discovery.archives_de_la_guilde 5",
            "GuildPoints guild 20" }, events.Select(e => e.ToString()));
        Assert.Equal(DialogueRefusal.None, session.Choose(0, out _, out IReadOnlyList<StoryEvent> start));
        Assert.Contains("QuestStarted quest.registres_de_l_archiviste/rang_e", start.Select(e => e.ToString()));
        // L'objectif « rang E » attend le rang ; au rang E, il est atteint.
        Assert.DoesNotContain(Steps(session), e => e.StartsWith("StepCompleted", StringComparison.Ordinal));
        SavedCharacter hero = session.Save().Character!;
        SavedStory st = hero.Story;
        var promoted = new SavedStory(st.Facts, st.Quests, st.Trust, st.Discoveries, st.Tracked,
            new SavedGuildCard("e", st.Guild!.Points, 0, new[] { "title.guilde.recrue" }, null, null, 0, null));
        var again = new WorldSession(new WorldSave("zone.archives", 0, 0, Array.Empty<string>(), hero.WithStory(promoted)), Inside("zone.archives"), ShippedData, Progression,
            ItemsCatalog, near, door: "sortie", storyZone: "zone.vallons", guild: Guild);
        Assert.Contains("StepCompleted quest.registres_de_l_archiviste/rang_e", again.Step().Story.Select(e => e.ToString()));
        Assert.Equal("retour", again.Narrative!.CurrentStep("quest.registres_de_l_archiviste")!.Id);
    }

    private static JsonObject MoveNpc(JsonObject root, string npc, double x, double z)
    {
        foreach (JsonNode? n in root["data"]!["npcs"]!.AsArray())
            if ((string)n!["id"]! == npc) n["position"] = new JsonArray(x, z);
        return root;
    }

    [Fact]
    public void Quete_finie_donne_les_points_de_sa_categorie()
    {
        // La quête des peaux (secondaire) finie au choix de sa récompense : 20 points ; une épreuve ne donne pas de points.
        var progress = new Dictionary<string, QuestProgress> { ["quest.peaux_pour_l_auberge"] = new QuestProgress(QuestProgress.Reward) };
        WorldSession w = InGuild(WithCard(Hero(), Card("g"), progress));
        Assert.Equal(DialogueRefusal.None, w.ChooseReward("quest.peaux_pour_l_auberge", 0, out IReadOnlyList<StoryEvent> events));
        Assert.Equal("GuildPoints quest.peaux_pour_l_auberge 20", events.Last().ToString());
        Assert.Equal(20, w.Narrative!.GuildPoints);
    }

    // --- L'aventure -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Aventure_a_la_guilde_tableau_carte_et_sauvegarde()
    {
        // Une partie reprise dans la Maison de la Guilde (à son point sûr, devant le comptoir de la maîtresse).
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.guilde", 0, 1, Array.Empty<string>(), Inventory.Outfit(Hero(1000), ItemsCatalog)));
        AdventureSession a = Adventure(SavePath);
        a.DrainMessages();
        Assert.True(a.Indoors);
        Assert.Equal(Mistress, a.NpcInReach!.Id);
        Assert.True(a.IsGuildmistress(Mistress));
        Assert.False(a.GuildCard().Member);
        Assert.True(a.Interact());   // F : le tableau de la guilde, pas le dialogue
        Assert.True(a.GuildOpen);
        Assert.Null(a.Dialogue);
        Assert.Equal(GuildRefusal.None, a.JoinGuild());
        Assert.Contains("Te voilà inscrit à la Guilde des aventuriers : rang G.", a.DrainMessages());
        GuildCardView card = a.GuildCard();
        Assert.Equal((true, "g", "Rang G", "Recrue de la Guilde"), (card.Member, card.Rank, card.RankName, card.Title));
        Assert.Equal("Points de guilde : 0 / 100 pour le Rang F", card.PointsText);
        Assert.Equal("Recrue de la Guilde", a.ShownTitle);
        IReadOnlyList<ContractEntry> board = a.GuildBoard();
        Assert.Equal(2, board.Count);
        Assert.All(board, c => Assert.Equal(GuildRefusal.None, c.Refusal));
        Assert.Equal(GuildRefusal.None, a.TakeContract(board[0].Id));
        Assert.Equal(GuildRefusal.AlreadyTaken, a.GuildBoard()[0].Refusal);
        ContractEntry active = Assert.Single(a.ActiveContracts());
        Assert.NotNull(active.Progress);
        Assert.Contains("Contrat pris : " + board[0].Name, a.DrainMessages());
        // Un refus s'affiche et ne change rien.
        Assert.Equal(GuildRefusal.AlreadyMember, a.JoinGuild());
        Assert.Contains("Tu es déjà inscrit.", a.DrainMessages());
        // Parler : le dialogue de la maîtresse (membre).
        Assert.True(a.TalkToGuildmistress());
        Assert.False(a.GuildOpen);
        Assert.Equal("text.maitresse.membre", a.Dialogue!.Text);
        a.CloseDialogue();
        // La sauvegarde garde la carte ; une partie reprise la retrouve.
        a.Save();
        AdventureSession again = Adventure(SavePath);
        Assert.Equal("g", again.GuildCard().Rank);
        Assert.Single(again.ActiveContracts());
        // La porte des archives : réservée au rang F (message, rien ne change).
        Assert.Equal("f", again.World.DoorNeedsRank("archives"));
    }

    [Fact]
    public void Objectif_sur_la_carte_vers_les_archives()
    {
        // Revenir voir l'archiviste, depuis les Vallons : l'étoile est sur la porte de la guilde (les archives sont derrière elle).
        var quests = new Dictionary<string, QuestProgress> { ["quest.registres_de_l_archiviste"] = new QuestProgress("retour") };
        SavedCharacter hero = Inventory.Outfit(Hero(), ItemsCatalog);
        hero = hero.WithStory(new SavedStory(new[] { "fact.archives.consultees" }, quests, tracked: "quest.registres_de_l_archiviste",
            guild: new SavedGuildCard("e", 300, 0, new[] { "title.guilde.recrue" })));
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), hero));
        AdventureSession a = Adventure(SavePath);
        Assert.False(a.Indoors);
        MapMarker objective = a.Objective!;
        Door door = Vallons.Value.DoorOf("porte_guilde")!;
        Assert.Equal((door.Position.X.Raw / 10000.0, door.Position.Z.Raw / 10000.0), (objective.X, objective.Z));
    }

    // --- Relecture de la PR #79 : régressions des sondes P1 à P6 ------------------------------------------------------------

    private static int QuestIndex(JsonObject story, string id) => story["data"]!["quests"]!.AsArray().ToList().FindIndex(q => (string)q!["id"]! == id);

    private static JsonObject QuestOf(JsonObject story, string id) => story["data"]!["quests"]![QuestIndex(story, id)]!.AsObject();

    private static GuildLoadResult GuildWithStory(Action<JsonObject> change)
    {
        JsonObject story = StoryJson();
        change(story);
        return ReadGuild(GuildJson(), ReadStory(story));
    }

    [Fact]
    public void P1_rang_ferme_dans_l_histoire_refuse_a_son_chemin()
    {
        // Objectif guildRank d'un rang fermé : jamais atteint. L'histoire seule l'accepte (le rang existe) ; la guilde le refuse.
        JsonObject story = StoryJson();
        int q = QuestIndex(story, "quest.registres_de_l_archiviste");
        GuildLoadResult r = GuildWithStory(s => QuestOf(s, "quest.registres_de_l_archiviste")["steps"]![0]!["objective"]!["id"] = "d");
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal(($"$.data.quests[{q}].steps[0].objective.id", ValidationCode.InvalidValue), (e.Path, e.Code));
        Assert.Contains("story.json", e.Message, StringComparison.Ordinal);
        // Condition guildRankAtLeast d'un rang fermé, dans un dialogue et dans des conditions d'ouverture.
        int d = story["data"]!["dialogues"]!.AsArray().ToList().FindIndex(x => (string)x!["id"]! == "dialogue.maitresse_guilde");
        r = GuildWithStory(s => s["data"]!["dialogues"]![d]!["entries"]![0]!["conditions"]![0]!["id"] = "d");
        Assert.Equal(($"$.data.dialogues[{d}].entries[0].conditions[0].id", ValidationCode.InvalidValue), (Assert.Single(r.Report.Errors).Path, r.Report.Errors[0].Code));
        r = GuildWithStory(s => QuestOf(s, "quest.registres_de_l_archiviste")["requires"] = new JsonArray(new JsonObject { ["if"] = "guildRankAtLeast", ["id"] = "sss" }));
        Assert.Equal($"$.data.quests[{q}].requires[0].id", Assert.Single(r.Report.Errors).Path);
    }

    [Fact]
    public void P2_epreuve_qui_demande_son_propre_rang_refusee()
    {
        // Dans l'épreuve du rang F, un objectif « rang F » (ou plus) ne serait jamais atteint : le rang vient à la fin de l'épreuve.
        int q = QuestIndex(StoryJson(), TrialF);
        GuildLoadResult r = GuildWithStory(s => QuestOf(s, TrialF)["steps"]![0]!["objective"] = new JsonObject { ["kind"] = "guildRank", ["id"] = "f" });
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal(($"$.data.quests[{q}].steps[0].objective.id", ValidationCode.InvalidValue), (e.Path, e.Code));
        Assert.Single(GuildWithStory(s => QuestOf(s, TrialF)["steps"]![1]!["objective"] = new JsonObject { ["kind"] = "guildRank", ["id"] = "e" }).Report.Errors);
        // Un rang en dessous (G) ne bloque rien.
        Assert.True(GuildWithStory(s => QuestOf(s, TrialF)["steps"]![0]!["objective"] = new JsonObject { ["kind"] = "guildRank", ["id"] = "g" }).IsValid);
        // Même règle pour ses conditions d'ouverture : l'épreuve ne s'ouvrirait jamais.
        r = GuildWithStory(s => QuestOf(s, TrialF)["requires"] = new JsonArray(new JsonObject { ["if"] = "guildRankAtLeast", ["id"] = "f" }));
        Assert.Equal(($"$.data.quests[{q}].requires[0].id", ValidationCode.InvalidValue), (Assert.Single(r.Report.Errors).Path, r.Report.Errors[0].Code));
    }

    [Fact]
    public void P3_conditions_d_ouverture_de_l_epreuve_respectees()
    {
        JsonObject story = StoryJson();
        foreach (JsonNode? n in story["data"]!["npcs"]!.AsArray())
            if ((string)n!["id"]! == Mistress) n["position"] = new JsonArray(0, -1.6);
        QuestOf(story, TrialF)["requires"] = new JsonArray(new JsonObject { ["if"] = "levelAtLeast", ["value"] = 99 });
        StoryBook book = ReadStory(story);
        GuildLoadResult g = ReadGuild(GuildJson(), book);
        Assert.True(g.IsValid, g.Report.ToString());
        var session = new WorldSession(new WorldSave("zone.guilde", 0, 0, Array.Empty<string>(), WithCard(Hero(), Card("g", 100))), Inside("zone.guilde"), ShippedData,
            Progression, ItemsCatalog, book, door: "sortie", storyZone: "zone.vallons", guild: g.Book);
        session.Step();
        Assert.False(session.Narrative!.IsOpen(TrialF));
        Assert.Equal(GuildRefusal.Locked, session.Narrative.CanStartTrial());
        Assert.Equal(GuildRefusal.Locked, session.CanStartTrial());
        Assert.Equal(GuildRefusal.Locked, session.StartTrial(out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        Assert.False(session.Narrative.IsActive(TrialF));
        Assert.True(Texts.Has("text.ui.guild.trial_locked"));
    }

    [Fact]
    public void P4_contrat_d_un_autre_jour_abandonne_ne_se_reprend_pas_le_jour_meme()
    {
        GuildBook g = Guild;
        long d = Enumerable.Range(0, 200).First(x => GuildBoard.Of(g, 0, x).Intersect(GuildBoard.Of(g, 0, x + 1)).Any());
        string id = GuildBoard.Of(g, 0, d).Intersect(GuildBoard.Of(g, 0, d + 1)).First().Id;
        WorldSession w = InGuild(WithCard(Hero(), Card("g")), tick: d * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract(id, out _));
        WorldSession next = InGuild(w.Save().Character!, tick: (d + 1) * DayTicks);
        Assert.Equal(GuildRefusal.None, next.AbandonContract(id, out _));
        Assert.Equal(GuildRefusal.AlreadyTaken, next.CanTakeContract(id));
        Assert.Equal(GuildRefusal.AlreadyTaken, next.TakeContract(id, out _));
        SavedGuildCard card = next.Save().Character!.Story.Guild!;
        Assert.Equal(next.Day, card.Day);
        Assert.Equal(new[] { id }, card.Taken);
        Assert.Empty(card.Active);
    }

    [Fact]
    public void P5_creature_rare_avant_inscription_ne_compte_jamais()
    {
        WorldSession w = InGuild(Hero());
        w.Narrative!.Defeated("loup_blanc", "loup_blanc");
        Assert.Empty(Steps(w));
        // Gardée dans la sauvegarde, relue à la session suivante, où l'inscription ne la fait pas compter.
        WorldSession later = InGuild(w.Save().Character!);
        Assert.Equal(new[] { "loup_blanc" }, later.Narrative!.Hunted);
        Assert.Equal(GuildRefusal.None, later.JoinGuild(out _));
        later.Narrative.Defeated("loup_blanc", "loup_blanc");
        later.Narrative.Defeated("le_borgne", "bandit");   // une autre créature rare, vaincue après l'inscription : elle compte
        Assert.Equal(new[] { "GuildPoints le_borgne 20" }, Steps(later));
    }

    [Fact]
    public void P6_le_Borgne_compte_pour_la_prime_et_pour_la_bande()
    {
        // Comportement voulu pour l'instant (question à Damien) : une victoire sur le Borgne avance la prime et la chasse de la famille « bandit ».
        GuildBook g = Guild;
        long d = Enumerable.Range(0, 400).First(x => GuildBoard.Of(g, 2, x).Any(c => c.Id == "contract.bande_du_camp")
            && GuildBoard.Of(g, 2, x).Any(c => c.Id == "contract.prime_du_borgne"));
        WorldSession w = InGuild(WithCard(Hero(), Card("e", 300, null, null, 0, null, "title.guilde.recrue")), tick: d * DayTicks);
        Assert.Equal(GuildRefusal.None, w.TakeContract("contract.bande_du_camp", out _));
        Assert.Equal(GuildRefusal.None, w.TakeContract("contract.prime_du_borgne", out _));
        w.Narrative!.Defeated("le_borgne", "bandit");
        Assert.Equal(1, w.Narrative.ActiveContracts.Single(a => a.Contract == "contract.bande_du_camp").Progress);
        Assert.Contains($"ContractDone contract.prime_du_borgne", Steps(w));
    }
}
