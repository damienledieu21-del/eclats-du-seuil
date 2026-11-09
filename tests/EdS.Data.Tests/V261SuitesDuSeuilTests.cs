using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.6.1 (« suites du Seuil ») : propagation d'une cicatrice au lieu voisin (zones en schéma 12 ; 11 encore lu), quête de Sœur Albe
/// « Les cicatrices du Seuil » (objectif <c>purify</c>, conditions <c>scarsAtLeast</c> et <c>scarsBelow</c> ; histoire en schéma 11, 32
/// répliques par dialogue ; 10 et antérieures encore lues avec 16), répliques de Gaspard, Odile, Aldric et du capitaine, essai. Sauvegarde v14,
/// moteur, règles, journal et journaux figés inchangés.
/// </summary>
[Trait("Category", "World")]
public class V261SuitesDuSeuilTests
{
    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly WorldClock Clock = new(50);
    private const string Rift = "faille_du_seuil", Quest = "quest.cicatrices_du_seuil";
    private const string Ruins = "zone.vallons/faille_du_seuil/ruines", Lake = "zone.vallons/faille_du_seuil/lac", Meadow = "zone.vallons/faille_du_seuil/pre";
    private const string Shard = "item.eclat_de_seuil", Rosary = "item.chapelet_de_l_aube";
    private const string Spread = "fact.event.faille_du_seuil.cicatrice_propagee", Born = "fact.event.faille_du_seuil.cicatrice";

    private static JsonObject ZoneJson() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static ZoneLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = ZoneJson();
        mutate(root);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
    }

    private static StoryLoadResult ReadStory(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons, ItemsCatalog, V16TestData.Interiors);

    private static JsonObject RiftJson(JsonObject root) => root["data"]!["events"]!.AsArray().Single(e => (string?)e!["id"] == Rift)!.AsObject();

    private static int RiftIndex => ZoneJson()["data"]!["events"]!.AsArray().Select((e, i) => ((string?)e!["id"], i)).Single(x => x.Item1 == Rift).i;

    private static void AssertError(ValidationReport report, string path, ValidationCode? code = null) =>
        Assert.True(report.Errors.Any(e => e.Path == path && (code is null || e.Code == code)), $"{path} attendu ; {report}");

    private static long Noon(long day) => WorldEventSchedule.TickAt(Clock, day * 1440 + 12 * 60);

    private static long At(long day, int hour, int minute = 0) => WorldEventSchedule.TickAt(Clock, day * 1440 + hour * 60 + minute);

    private static WorldSession Session(ZoneDefinition zone, long tick, SavedCharacter hero) =>
        new(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone),
            safePoint: "place_du_village");

    private static List<WorldEventNotice> Run(WorldSession w, int ticks)
    {
        var notices = new List<WorldEventNotice>();
        for (int i = 0; i < ticks; i++) notices.AddRange(w.Step().Events);
        return notices;
    }

    private static SavedCharacter WithScar(SavedCharacter hero, string key, int stage, long since) =>
        hero.WithScars(hero.Scars.Where(x => x.Id != key).Append(new SavedScar(key, stage, since, since)));

    private static string Scars(WorldSession w) => string.Join(";", w.SavedScars.Select(s => $"{s.Id.Substring(s.Id.LastIndexOf('/') + 1)}:{s.Stage}@{s.Since}/{s.Born}"));

    /// <summary>Quêtes de l'ouverture déjà faites (aucune étape en cours ne passe devant les répliques).</summary>
    private static Dictionary<string, QuestProgress> Done(params (string Quest, string Step)[] more)
    {
        var d = new Dictionary<string, QuestProgress>(StringComparer.Ordinal)
        {
            ["quest.reveil"] = new QuestProgress(QuestProgress.Done), ["quest.pain_du_matin"] = new QuestProgress(QuestProgress.Done),
        };
        foreach ((string q, string s) in more) d[q] = new QuestProgress(s);
        return d;
    }

    /// <summary>Partie dans <paramref name="zone"/> (un intérieur : l'histoire reste celle des Vallons), entrée en (x, z), au tick donné.</summary>
    private static WorldSession Near(string zone, double x, double z, long tick, SavedCharacter hero)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition;
        if (outside) definition = Zone(x, z);
        else
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zone)))!.AsObject();
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            definition = loaded.Zone!;
        }
        var w = new WorldSession(new WorldSave(zone, tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), definition, ShippedData, Progression, ItemsCatalog,
            Book(Vallons), storyZone: outside ? null : "zone.vallons");
        w.Step();
        return w;
    }

    private static string Says(WorldSession w, string npc)
    {
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        return view!.Node;
    }

    private static void Pick(WorldSession w, int option = 0)
    {
        Assert.Equal(DialogueRefusal.None, w.Choose(option, out _, out _));
        w.Step();
    }

    // --- Formats ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal((12, 11, 10), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.ScarsSchemaVersion, ZoneValidator.EventsSchemaVersion));
        Assert.Equal((13, 10, 32, 16), (StoryValidator.CurrentSchemaVersion, StoryValidator.Schema10, StoryValidator.MaxEntries, StoryValidator.MaxEntriesBefore11));
        Assert.Equal((12, "0.17.0-provisoire"), (Vallons.SchemaVersion, Vallons.ContentVersion));
        Assert.Equal((13, "0.25.0-provisoire"), (Book(Vallons).SchemaVersion, Book(Vallons).ContentVersion));
        Assert.Equal("0.25.0-provisoire", Texts.ContentVersion);
        Assert.Equal("0.17.0-provisoire", ItemsCatalog.ContentVersion);
        // La sauvegarde ne change pas : une cicatrice propagée est une cicatrice comme les autres (v14 ; V2.7 : v15, la réputation).
        Assert.Equal(16, SaveValidator.CurrentSchemaVersion);
        // Ni le moteur, ni les règles, ni le journal, ni les scénarios (moteur 0.30.0 et journal 24 depuis V2.8, recherche de chemin).
        Assert.Equal(("0.30.0", 24, 17), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion));
    }

    [Fact]
    public void Propagation_livree_selon_la_geographie()
    {
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        WorldEventScar s = rift.Scar!;
        Assert.Equal((true, 4, 2, Spread), (s.Spreads, s.SpreadStage, s.SpreadDays, s.SpreadFact));
        // Les ruines sont au milieu : le lac (36 m) puis le pré (50 m) ; le lac et le pré (78 m l'un de l'autre) ne touchent que les ruines.
        Assert.Equal(new[] { "ruines:lac,pre", "lac:ruines", "pre:ruines" }, rift.Groups.Select(g => g.Id + ":" + string.Join(",", g.SpreadTo)));
        double D2(string a, string b)
        {
            GroundVector p = rift.Groups.Single(g => g.Id == a).Position!.Value, q = rift.Groups.Single(g => g.Id == b).Position!.Value;
            double dx = (p.X.Raw - q.X.Raw) / 10000.0, dz = (p.Z.Raw - q.Z.Raw) / 10000.0;
            return dx * dx + dz * dz;
        }
        Assert.True(D2("ruines", "lac") < D2("ruines", "pre") && D2("ruines", "pre") < D2("lac", "pre"));
        // Les fichiers livrés suivent leurs schémas JSON.
        foreach ((string schema, string file) in new[] { ("zone.schema.json", ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")), ("story.schema.json", StoryJsonReader.StoryPathIn(DataDirectory)) })
            Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", schema)).IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)).RootElement), schema);
    }

    [Fact]
    public void Schema_11_encore_lu_sans_propagation()
    {
        ZoneLoadResult r = Mutated(root =>
        {
            root["schemaVersion"] = 11;
            JsonObject scar = RiftJson(root)["scar"]!.AsObject();
            scar.Remove("spreadStage");
            scar.Remove("spreadDays");
            scar.Remove("spreadFact");
            foreach (JsonNode? g in RiftJson(root)["groups"]!.AsArray()) g!.AsObject().Remove("spreadTo");
        });
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(11, r.Zone!.SchemaVersion);
        Assert.False(r.Zone.EventOf(Rift)!.Scar!.Spreads);
        Assert.All(r.Zone.EventOf(Rift)!.Groups, g => Assert.Empty(g.SpreadTo));
        // En schéma 11, la propagation est refusée à son chemin précis.
        int i = RiftIndex;
        ZoneLoadResult old = Mutated(root => root["schemaVersion"] = 11);
        AssertError(old.Report, $"$.data.events[{i}].scar.spreadStage", ValidationCode.InvalidValue);
        AssertError(old.Report, $"$.data.events[{i}].groups[0].spreadTo", ValidationCode.InvalidValue);
        AssertError(Mutated(root => root["schemaVersion"] = 13).Report, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
    }

    [Theory]
    [InlineData("spreadStage", "0", ".scar.spreadStage", ValidationCode.OutOfRange)]
    [InlineData("spreadStage", "5", ".scar.spreadStage", ValidationCode.OutOfRange)]   // au-delà du palier maximal (4)
    [InlineData("spreadDays", "0", ".scar.spreadDays", ValidationCode.OutOfRange)]
    [InlineData("spreadDays", "31", ".scar.spreadDays", ValidationCode.OutOfRange)]
    [InlineData("spreadFact", "\"propagee\"", ".scar.spreadFact", null)]
    [InlineData("spreadFact", "\"fact.met.npc.albe\"", ".scar.spreadFact", null)]
    public void Propagation_refusee_au_chemin_precis(string field, string json, string path, ValidationCode? code)
    {
        int i = RiftIndex;
        AssertError(Mutated(root => RiftJson(root)["scar"]![field] = JsonNode.Parse(json)).Report, $"$.data.events[{i}]{path}", code);
    }

    [Fact]
    public void Voisines_refusees_au_chemin_precis()
    {
        int i = RiftIndex;
        string G(int g) => $"$.data.events[{i}].groups[{g}].spreadTo";
        JsonArray To(JsonObject root, int g) => RiftJson(root)["groups"]![g]!["spreadTo"]!.AsArray();
        AssertError(Mutated(root => To(root, 0)[0] = "nulle_part").Report, G(0) + "[0]", ValidationCode.UnknownReference);
        AssertError(Mutated(root => To(root, 0)[1] = "ruines").Report, G(0) + "[1]", ValidationCode.InvalidValue);   // elle-même
        AssertError(Mutated(root => To(root, 0)[1] = "lac").Report, G(0) + "[1]", ValidationCode.DuplicateId);
        AssertError(Mutated(root => RiftJson(root)["groups"]![1]!["spreadTo"] = new JsonArray()).Report, G(1), ValidationCode.OutOfRange);
        // Sans palier de propagation, aucune voisine (et sans voisine, une propagation ne fait rien : acceptée).
        ZoneLoadResult r = Mutated(root => RiftJson(root)["scar"]!.AsObject().Remove("spreadStage"));
        AssertError(r.Report, $"$.data.events[{i}].scar.spreadStage", ValidationCode.MissingField);
        AssertError(r.Report, G(0), ValidationCode.InvalidValue);
        ZoneLoadResult alone = Mutated(root => { foreach (JsonNode? g in RiftJson(root)["groups"]!.AsArray()) g!.AsObject().Remove("spreadTo"); });
        Assert.True(alone.IsValid, alone.Report.ToString());
        // Une voisine sur une autre faille, sur une attaque : refusée (des voisines seulement là où la cicatrice se propage).
        ZoneLoadResult raid = Mutated(root =>
        {
            JsonObject attack = root["data"]!["events"]!.AsArray().Single(e => (string?)e!["kind"] == "raid")!.AsObject();
            attack["groups"]![0]!["spreadTo"] = new JsonArray(attack["groups"]![1]!["id"]!.GetValue<string>());
        });
        int raidIndex = ZoneJson()["data"]!["events"]!.AsArray().Select((e, k) => ((string?)e!["kind"], k)).First(x => x.Item1 == "raid").k;
        AssertError(raid.Report, $"$.data.events[{raidIndex}].groups[0].spreadTo", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Histoire_en_schema_11_et_schema_10_encore_lu_avec_seize_repliques()
    {
        StoryBook book = Book(Vallons);
        Assert.Equal((19, 26, 22), (book.Dialogue("dialogue.albe").Entries.Count, book.Dialogue("dialogue.gaspard").Entries.Count, book.Dialogue("dialogue.odile").Entries.Count));
        // Version 10 : 16 répliques au plus ; Gaspard en a 26 (V2.7 : 22 et la piste des rôdeurs ; V2.9.2 : le vieux Brunot) : refusé au chemin précis du dialogue.
        JsonObject ten = StoryJson();
        ten["schemaVersion"] = 10;
        int gaspard = ten["data"]!["dialogues"]!.AsArray().Select((d, k) => ((string?)d!["id"], k)).Single(x => x.Item1 == "dialogue.gaspard").k;
        StoryLoadResult refused = ReadStory(ten);
        AssertError(refused.Report, $"$.data.dialogues[{gaspard}].entries", ValidationCode.OutOfRange);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".kind", StringComparison.Ordinal) && e.Message.Contains("purify"));
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".if", StringComparison.Ordinal) && e.Message.Contains("scarsAtLeast"));
        // Sans ce qui demande la version 11, la version 10 se lit.
        V261TestData.WithoutSuites(ten);
        StoryLoadResult read = ReadStory(ten);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(10, read.Story!.SchemaVersion);
        Assert.All(read.Story.Dialogues, d => Assert.True(d.Entries.Count <= 16, d.Id));
        // Version 11 : 33 répliques, refusé ; 32, accepté.
        JsonObject big = StoryJson();
        JsonArray entries = big["data"]!["dialogues"]![gaspard]!["entries"]!.AsArray();
        while (entries.Count < 32) entries.Insert(0, entries[^1]!.DeepClone());
        Assert.True(ReadStory(big).IsValid);
        entries.Insert(0, entries[^1]!.DeepClone());
        AssertError(ReadStory(big).Report, $"$.data.dialogues[{gaspard}].entries", ValidationCode.LimitExceeded);   // borne du lecteur, la même
        Assert.Equal(12, ZoneValidator.CurrentSchemaVersion);
    }

    [Fact]
    public void Objectif_purify_et_conditions_de_cicatrices_refuses_au_chemin_precis()
    {
        JsonObject Root() => StoryJson();
        int q = Root()["data"]!["quests"]!.AsArray().Select((x, k) => ((string?)x!["id"], k)).Single(x => x.Item1 == Quest).k;
        string Step = $"$.data.quests[{q}].steps[0].objective";
        JsonNode Objective(JsonObject r) => r["data"]!["quests"]![q]!["steps"]![0]!["objective"]!;
        JsonObject a = Root(); Objective(a)["id"] = "attaque_hameau";   // un événement sans cicatrice
        AssertError(ReadStory(a).Report, Step + ".id", ValidationCode.UnknownReference);
        JsonObject b = Root(); Objective(b)["value"] = 0;
        AssertError(ReadStory(b).Report, Step + ".value", ValidationCode.OutOfRange);
        JsonObject c = Root(); Objective(c)["value"] = 33;
        AssertError(ReadStory(c).Report, Step + ".value", ValidationCode.OutOfRange);
        // Conditions : faille à cicatrice, de 0 à 32 cicatrices.
        JsonObject d = Root();
        int albe = d["data"]!["dialogues"]!.AsArray().Select((x, k) => ((string?)x!["id"], k)).Single(x => x.Item1 == "dialogue.albe").k;
        JsonArray albeEntries = d["data"]!["dialogues"]![albe]!["entries"]!.AsArray();
        int guet = albeEntries.Select((x, k) => ((string?)x!["node"], k)).Single(x => x.Item1 == "v261_guet").k;
        albeEntries[guet]!["conditions"]![1]!["value"] = 33;
        AssertError(ReadStory(d).Report, $"$.data.dialogues[{albe}].entries[{guet}].conditions[1].value", ValidationCode.OutOfRange);
        JsonObject e = Root();
        e["data"]!["dialogues"]![albe]!["entries"]![guet]!["conditions"]![1]!["id"] = "nulle_part";
        AssertError(ReadStory(e).Report, $"$.data.dialogues[{albe}].entries[{guet}].conditions[1].id", ValidationCode.UnknownReference);
    }

    // --- Propagation ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Au_palier_4_depuis_deux_jours_le_froid_gagne_le_lac()
    {
        // Ruines au palier 4 depuis le jour 10 : rien au jour 11 ; au jour 12, une cicatrice naît au lac (palier 1), et les ruines repartent du jour 12.
        SavedCharacter hero = WithScar(Hero(), Ruins, 4, 10);
        WorldSession early = Session(Zone(-18, -12), Noon(11), hero);
        Assert.DoesNotContain(Run(early, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        Assert.Equal("ruines:4@10/10", Scars(early));
        WorldSession w = Session(Zone(-18, -12), Noon(12), hero);
        WorldEventNotice spread = Assert.Single(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        Assert.Equal(("lac", "ruines", 12L, 1), (spread.Group, spread.From, spread.Day, spread.Wave));
        Assert.Equal("lac:1@12/12;ruines:4@12/10", Scars(w));
        Assert.Contains(Spread, w.Narrative!.ToSaved().Facts);
        Assert.Equal(new[] { 1, 4 }, w.Scars.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Stage));
        // Rien de plus le même jour ; le pré suit deux jours plus tard (le délai repart de la propagation).
        Assert.DoesNotContain(Run(w, 20), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        WorldSession later = Session(Zone(-18, -12), Noon(14), w.Save().Character!);
        Assert.Single(Run(later, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread && n.Group == "pre");
        Assert.Equal("lac:1@12/12;pre:1@14/14;ruines:4@14/10", Scars(later));
    }

    [Fact]
    public void Une_cicatrice_jeune_attend_le_palier_4_puis_le_delai()
    {
        // Palier 1 au jour 10 : palier 4 au jour 16 (un palier tous les deux jours), propagation au jour 18.
        SavedCharacter hero = WithScar(Hero(), Ruins, 1, 10);
        Assert.Equal(16L, WorldScarRules.SpreadReachDay(new SavedScar(Ruins, 1, 10, 10), Vallons.EventOf(Rift)!.Scar!));
        Assert.DoesNotContain(Run(Session(Zone(-18, -12), Noon(17), hero), 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        WorldSession w = Session(Zone(-18, -12), Noon(18), hero);
        Assert.Contains(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread && n.Day == 18);
        Assert.Equal("lac:1@18/18;ruines:4@18/10", Scars(w));
    }

    [Fact]
    public void Longue_absence_rattrapee_dans_l_ordre_bornee_et_deterministe()
    {
        // Partie reprise au jour 60 : le lac au jour 12, le pré au jour 14 ; plus rien ensuite (une cicatrice au plus par lieu).
        SavedCharacter hero = WithScar(Hero(), Ruins, 4, 10);
        WorldSession w = Session(Zone(-18, -12), Noon(60), hero);
        var spreads = Run(w, 2).Where(n => n.Kind == WorldEventNoticeKind.ScarSpread).Select(n => (n.Group, n.From, n.Day)).ToList();
        Assert.Equal(new[] { ("lac", "ruines", 12L), ("pre", "ruines", 14L) }, spreads);
        Assert.Equal(3, w.SavedScars.Count);
        Assert.All(w.Scars, v => Assert.Equal(4, v.Stage));   // toutes au palier maximal au jour 60
        Assert.DoesNotContain(Run(w, 200), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        // Mêmes entrées, mêmes cicatrices (et le lac né d'une source au lac va vers les ruines seulement).
        WorldSession again = Session(Zone(-18, -12), Noon(60), hero);
        Run(again, 2);
        Assert.Equal(Scars(w), Scars(again));
        WorldSession lake = Session(Zone(-18, -12), Noon(60), WithScar(Hero(), Lake, 4, 10));
        Run(lake, 2);
        Assert.Equal("lac:4@12/10;pre:1@20/20;ruines:4@20/12", Scars(lake));   // le lac gagne les ruines (jour 12) ; elles atteignent le palier 4 au jour 18 et gagnent le pré au jour 20
    }

    [Fact]
    public void Purifier_la_voisine_donne_deux_jours_de_repit()
    {
        // Ruines au palier 4 (depuis longtemps), lac né de lui au jour 12 ; on purifie le lac au jour 13 (à 1,5 m de son cœur).
        SavedCharacter hero = WithScar(WithScar(Hero(0, new ItemStack(Shard, 2)), Ruins, 4, 12), Lake, 1, 12);
        WorldSession w = Session(Zone(15.5, -42), Noon(13), hero);
        Run(w, 2);
        Assert.Equal(ScarRefusal.None, w.PurifyScar(Lake));
        Assert.Equal("ruines:4@13/12", Scars(w));   // la source repart d'aujourd'hui
        Assert.DoesNotContain(Run(w, 20), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        // Le lendemain, toujours rien ; deux jours après, le froid revient au lac (la source n'a pas été purifiée).
        Assert.DoesNotContain(Run(Session(Zone(-18, -12), Noon(14), w.Save().Character!), 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        WorldSession back = Session(Zone(-18, -12), Noon(15), w.Save().Character!);
        Assert.Contains(Run(back, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread && n.Group == "lac" && n.Day == 15);
        // La source purifiée, plus rien ne naît.
        SavedCharacter clean = WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 1, 15);
        WorldSession source = Session(Zone(22.5, -7), Noon(15), clean);
        Run(source, 2);
        Assert.Equal(ScarRefusal.None, source.PurifyScar(Ruins));
        Assert.Empty(Session(Zone(-18, -12), Noon(40), source.Save().Character!).SavedScars);
    }

    /// <summary>Vallons où la cicatrice se propage dès le palier 2 (sous le maximum, 4), un jour après ; <paramref name="spread"/> faux : sans voisines.</summary>
    private static ZoneDefinition EarlySpread(bool spread)
    {
        ZoneLoadResult r = Mutated(root =>
        {
            JsonObject rift = RiftJson(root);
            rift["scar"]!["spreadStage"] = 2;
            rift["scar"]!["spreadDays"] = 1;
            if (!spread) foreach (JsonNode? g in rift["groups"]!.AsArray()) g!.AsObject().Remove("spreadTo");
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray(-18, -12);
        });
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    }

    [Fact]
    public void La_propagation_ne_change_pas_la_croissance_de_la_source()
    {
        // Relecture de la PR #112 : la source (palier 1 au jour 10, un palier tous les deux jours) a le même palier, jour après jour, qu'elle se
        // propage (palier 2 au jour 12, lac au jour 13, pré au jour 13 ou après) ou non.
        SavedCharacter hero = WithScar(Hero(), Ruins, 1, 10);
        ZoneDefinition with = EarlySpread(true), without = EarlySpread(false);
        for (long day = 10; day <= 22; day++)
        {
            WorldSession a = Session(with, Noon(day), hero), b = Session(without, Noon(day), hero);
            Run(a, 2);
            Run(b, 2);
            Assert.Equal(b.Scars.Single(v => v.Key == Ruins).Stage, a.Scars.Single(v => v.Key == Ruins).Stage);
            if (day >= 13) Assert.Equal(new[] { Lake, Meadow, Ruins }, a.SavedScars.Select(x => x.Id));
            else Assert.Single(a.SavedScars);
        }
        // Au palier maximal (la règle livrée), le délai repart du jour même ; en dessous, du dernier palier gagné.
        WorldEventScar rule = with.EventOf(Rift)!.Scar!;
        SavedScar s = new(Ruins, 1, 10, 10);
        Assert.Equal((2, 12L), (WorldScarRules.RestartSpread(s, rule, 13).Stage, WorldScarRules.RestartSpread(s, rule, 13).Since));
        Assert.Equal((4, 40L), (WorldScarRules.RestartSpread(s, rule, 40).Stage, WorldScarRules.RestartSpread(s, rule, 40).Since));
        Assert.Same(s, WorldScarRules.RestartSpread(s, rule, 9));
    }

    [Fact]
    public void Le_repit_ne_vaut_que_pour_les_sources_de_la_place_purifiee()
    {
        // Le pré n'a qu'une source, les ruines : elles repartent du jour 20 ; le lac (palier 4 lui aussi, vers les ruines seulement) garde son délai.
        SavedCharacter hero = WithScar(WithScar(WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 4, 10), Lake, 4, 10), Meadow, 1, 20);   // le pré, né du jour : sans écho
        WorldSession w = Session(Zone(-10.5, 30), Noon(20), hero);
        Run(w, 2);
        Assert.Equal(ScarRefusal.None, w.PurifyScar(Meadow));
        Assert.Equal("lac:4@10/10;ruines:4@20/10", Scars(w));
    }

    [Fact]
    public void Sauvegardee_et_reprise_en_plein_rattrapage_meme_resultat()
    {
        SavedCharacter hero = WithScar(Hero(), Ruins, 4, 10);
        WorldSession direct = Session(Zone(-18, -12), Noon(60), hero);
        Run(direct, 2);
        foreach (long pause in new long[] { 11, 12, 13, 14, 15 })
        {
            WorldSession first = Session(Zone(-18, -12), Noon(pause), hero);
            Assert.Equal(hero.Scars, first.Save().Character!.Scars);   // rien avant le premier tick
            Run(first, 2);
            // (registre des événements vidé : les failles manquées entre-temps creuseraient des cicatrices, ce n'est pas ce qu'on compare)
            SavedCharacter saved = SaveJsonReader.Read(SaveJsonReader.Write(first.Save())).Save!.Character!.WithEvents(Array.Empty<SavedEvent>());
            WorldSession resumed = Session(Zone(-18, -12), Noon(60), saved);
            Run(resumed, 2);
            Assert.Equal(Scars(direct), Scars(resumed));
        }
    }

    [Fact]
    public void Purifier_avant_le_premier_tick_d_une_reprise_garde_les_propagations_passees()
    {
        // Ruines au palier 4 depuis le jour 10, le lac déjà pris (cicatrice du jour, sans écho) ; reprise au jour 30, purification du lac tentée
        // avant le premier tick : le pré, gagné au jour 12 pendant l'absence, n'est jamais perdu ; le répit des ruines part ensuite (jour 30).
        SavedCharacter hero = WithScar(WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 4, 10), Lake, 1, 30);
        WorldSession w = Session(Zone(15.5, -42), Noon(30), hero);
        // Avant le premier tick, le personnage n'est pas encore entré : refusé (trop loin), et rien n'a changé.
        Assert.Equal(ScarRefusal.TooFar, w.PurifyScar(Lake));
        Assert.Equal(hero.Scars, w.SavedScars);
        // Le premier tick rattrape (le pré au jour 12) ; la purification d'ensuite fait partir le répit des ruines d'aujourd'hui.
        Assert.Contains(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread && n.Group == "pre" && n.Day == 12);
        Assert.Equal(ScarRefusal.None, w.PurifyScar(Lake));
        Assert.Equal("pre:1@12/12;ruines:4@30/10", Scars(w));
        Assert.Equal("lac", Assert.Single(Run(w, 1), n => n.Kind == WorldEventNoticeKind.ScarPurified).Group);
        // Le lac revient deux jours plus tard (jour 32), pas avant.
        SavedCharacter after = w.Save().Character!.WithEvents(Array.Empty<SavedEvent>());   // sans failles manquées entre-temps
        Assert.DoesNotContain(Run(Session(Zone(-18, -12), Noon(31), after), 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        Assert.Contains(Run(Session(Zone(-18, -12), Noon(32), after), 2), n => n.Kind == WorldEventNoticeKind.ScarSpread && n.Group == "lac");
    }

    [Fact]
    public void Une_faille_manquee_a_la_source_pendant_l_absence_n_efface_pas_les_propagations_dues()
    {
        // Revue Codex de la PR #112 (tour 2) : ruines au palier 4 depuis le jour 10 (voisines dues aux jours 12 et 14), puis une faille manquée
        // aux ruines plus tard pendant l'absence : réglée au premier tick, elle passe après les propagations de ces jours-là.
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        EventOccurrence o = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, 15 * 1440)!;
        while (o.Group.Id != "ruines") o = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, checked(o.EndAt + 1))!;
        Assert.True(o.Day >= 15);
        SavedCharacter hero = WithScar(Hero().WithEvents(new[] { new SavedEvent(WorldEventSchedule.RegistryKey(Vallons.Id, Rift), 0, 0, -1, EventOutcome.None) }), Ruins, 4, 10);
        WorldSession w = Session(Zone(-18, -12), WorldEventSchedule.TickAt(Clock, checked(o.EndAt + 60)), hero);
        var notices = Run(w, 2);
        Assert.Equal(new[] { ("lac", 12L), ("pre", 14L) }, notices.Where(n => n.Kind == WorldEventNoticeKind.ScarSpread).Select(n => (n.Group, n.Day)));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.ScarGrew && n.Group == "ruines" && n.Wave == 4);
        // Au palier maximal, la faille manquée ne change rien à la source : ni palier, ni délai.
        Assert.Equal("lac:1@12/12;pre:1@14/14;ruines:4@14/10", Scars(w));
        // Ordre des notices : les propagations (jours 12 et 14) avant la faille manquée (plus tard).
        int lastSpread = notices.FindLastIndex(n => n.Kind == WorldEventNoticeKind.ScarSpread), grew = notices.FindIndex(n => n.Kind == WorldEventNoticeKind.ScarGrew);
        Assert.True(lastSpread < grew, $"{lastSpread} < {grew}");
    }

    [Fact]
    public void Une_faille_manquee_creuse_sans_perdre_la_phase()
    {
        // Sous le maximum : un palier d'avance, au même rythme (palier 1 au jour 10, un palier tous les deux jours ; faille manquée au jour 13).
        WorldEventScar rule = Vallons.EventOf(Rift)!.Scar!;
        SavedScar s = new(Ruins, 1, 10, 10), deeper = WorldScarRules.Deepen(s, rule, 13);
        Assert.Equal((3, 12L, 10L), (deeper.Stage, deeper.Since, deeper.Born));
        for (long day = 13; day <= 20; day++)
            Assert.Equal(Math.Min(4, WorldScarRules.StageAt(s, rule, day) + 1), WorldScarRules.StageAt(deeper, rule, day));
        // Au maximum : la même ligne.
        SavedScar max = new(Ruins, 2, 10, 10);
        Assert.Same(max, WorldScarRules.Deepen(max, rule, 30));
        // Un jour d'avant « since » compte comme « since ».
        Assert.Equal((2, 10L), (WorldScarRules.Deepen(s, rule, 5).Stage, WorldScarRules.Deepen(s, rule, 5).Since));
    }

    [Fact]
    public void Une_cicatrice_propagee_vit_comme_les_autres()
    {
        // Née au lac au jour 12, elle grandit de jour en jour (palier 2 au jour 14), recule si une faille s'y referme, et se purifie.
        SavedCharacter hero = WithScar(Hero(), Ruins, 4, 10);
        WorldSession w = Session(Zone(-18, -12), Noon(14), hero);
        Run(w, 2);
        ScarView lake = w.Scars.Single(v => v.Key == Lake);
        Assert.Equal((2, 12L), (lake.Stage, lake.Born));
        // Les cicatrices pleines (borne technique) : rien ne naît, rien ne change (vérifié au chargement des zones reliées).
        SavedCharacter full = hero.WithScars(Enumerable.Range(0, SavedScar.MaxEntries - 1).Select(k => new SavedScar($"zone.ailleurs/faille/v{k:00}", 1, 0, 0))
            .Append(new SavedScar(Ruins, 4, 10, 10)));
        WorldSession crowded = Session(Zone(-18, -12), Noon(30), full);
        Assert.DoesNotContain(Run(crowded, 2), n => n.Kind == WorldEventNoticeKind.ScarSpread);
        Assert.Equal(SavedScar.MaxEntries, crowded.SavedScars.Count);
    }

    [Fact]
    public void Toutes_les_voisines_de_toutes_et_la_propagation_reste_bornee()
    {
        // Chaque lieu voisin des deux autres, propagation au palier 1 et chaque jour : au plus trois cicatrices, en un seul rattrapage.
        ZoneLoadResult r = Mutated(root =>
        {
            JsonObject rift = RiftJson(root);
            rift["scar"]!["spreadStage"] = 1;
            rift["scar"]!["spreadDays"] = 1;
            rift["groups"]![0]!["spreadTo"] = new JsonArray("lac", "pre");
            rift["groups"]![1]!["spreadTo"] = new JsonArray("pre", "ruines");
            rift["groups"]![2]!["spreadTo"] = new JsonArray("ruines", "lac");
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray(-18, -12);
        });
        Assert.True(r.IsValid, r.Report.ToString());
        WorldSession w = Session(r.Zone!, Noon(500), WithScar(Hero(), Meadow, 1, 400));
        Run(w, 2);
        // Pré (source) vers les ruines au jour 401 ; sous le palier maximal, le délai repart du dernier palier gagné (jour 400, la croissance
        // n'est pas touchée) : le pré gagne aussi le lac au jour 401.
        Assert.Equal("lac:1@401/401;pre:1@400/400;ruines:1@401/401", Scars(w));
        Assert.Equal(3, w.SavedScars.Count);
    }

    // --- La quête de Sœur Albe ----------------------------------------------------------------------------------------------------

    /// <summary>Sœur Albe dans sa chapelle (10 h) ou sur le parvis (17 h 30, sa veille du soir).</summary>
    private static WorldSession AtAlbe(long day, bool evening, SavedCharacter hero) => evening
        ? Near("zone.vallons", -70.4, -5.4, At(day, 17, 30), hero)   // entre sa place de veille et son abri (s'il pleut)
        : Near("zone.chapelle", 3.2, 3.2, At(day, 10), hero);

    [Fact]
    public void Albe_propose_la_quete_apres_la_premiere_cicatrice_seulement()
    {
        Assert.NotEqual("v261_offre", Says(AtAlbe(5, false, Hero().WithStory(new SavedStory(null, Done()))), "npc.albe"));
        WorldSession w = AtAlbe(5, false, Hero().WithStory(new SavedStory(new[] { Born }, Done())));
        Assert.Equal("v261_offre", Says(w, "npc.albe"));
        Pick(w, 1);   // plus tard
        Assert.False(w.Narrative!.IsActive(Quest));
        Assert.Equal("v261_offre", Says(w, "npc.albe"));
        Pick(w, 0);
        Assert.True(w.Narrative.IsActive(Quest));
        Assert.Equal("premiere", w.Narrative.CurrentStep(Quest)!.Id);
        // Aucune cicatrice dans les Vallons : elle dit de guetter la prochaine faille ; avec une, de commencer par la plus vieille.
        Assert.Equal("v261_guet", Says(w, "npc.albe"));
        WorldSession scarred = AtAlbe(5, false, WithScar(w.Save().Character!, Lake, 1, 5));
        Assert.Equal("v261_attente", Says(scarred, "npc.albe"));
        Assert.Contains(scarred.Narrative!.Book.Quest(Quest).Steps, s => s.Objective.Kind == ObjectiveKind.Purify);
    }

    [Fact]
    public void La_quete_de_bout_en_bout_et_sa_recompense_unique()
    {
        SavedCharacter hero = Hero(0, new ItemStack(Shard, 1)).WithStory(new SavedStory(new[] { Born }, Done((Quest, "premiere")), tracked: Quest));
        // 1. Une cicatrice aux ruines purifiée : l'étape avance (le compteur suit) ; l'étoile de la carte la montrait.
        WorldSession ruins = Session(Zone(22.5, -7), Noon(6), WithScar(hero, Ruins, 1, 6));
        Run(ruins, 2);
        Assert.Equal(ScarRefusal.None, ruins.PurifyScar(Ruins));
        Run(ruins, 2);
        Assert.Equal("retour", ruins.Narrative!.CurrentStep(Quest)!.Id);
        // 2. Sœur Albe, sur le parvis à 17 h 30 (elle change de lieu : V2.3.1) : l'étape de retour se fait là où elle est ; elle donne un éclat.
        WorldSession parvis = AtAlbe(6, true, ruins.Save().Character!);
        Assert.Equal("zone.vallons", parvis.NpcZone(parvis.Narrative!.Book.Npc("npc.albe")) ?? "zone.vallons");
        Assert.True(parvis.NpcHere(parvis.Narrative.Book.Npc("npc.albe")));
        Assert.Equal("v261_retour", Says(parvis, "npc.albe"));
        Pick(parvis);
        Assert.Equal("toutes", parvis.Narrative.CurrentStep(Quest)!.Id);
        Assert.Equal(1, parvis.Inventory.Count(Shard));
        // 3. Deux autres : le lac, puis le pré (une partie chacune) ; le compteur va de 0 à 2.
        WorldSession lake = Session(Zone(15.5, -42), Noon(7), WithScar(parvis.Save().Character!, Lake, 1, 7).WithItems(
            new[] { new ItemStack(Shard, 2) }, new Dictionary<string, string>()));
        Run(lake, 2);
        Assert.Equal(0, lake.Narrative!.Progress(Quest));
        Assert.Equal(ScarRefusal.None, lake.PurifyScar(Lake));
        Run(lake, 2);
        Assert.Equal((1L, "toutes"), (lake.Narrative.Progress(Quest), lake.Narrative.CurrentStep(Quest)!.Id));
        // (une faille manquée pendant l'absence peut avoir creusé une cicatrice : celle du pré est posée ici, au palier 1, sans écho)
        WorldSession meadow = Session(Zone(-10.5, 30), Noon(7), WithScar(lake.Save().Character!, Meadow, 1, 7));
        Run(meadow, 2);
        Assert.Equal(ScarRefusal.None, meadow.PurifyScar(Meadow));
        Run(meadow, 2);
        Assert.Equal("fin", meadow.Narrative!.CurrentStep(Quest)!.Id);
        // 4. Chez elle, à la chapelle : la récompense, une seule fois (le chapelet de l'Aube), puis son mot d'après, une fois.
        WorldSession end = AtAlbe(8, false, meadow.Save().Character!);
        long xp = end.Character.Xp;
        Assert.Equal("v261_fin", Says(end, "npc.albe"));
        Pick(end);
        Assert.True(end.Narrative!.IsDone(Quest));
        Assert.Equal(1, end.Inventory.Count(Rosary));
        Assert.True(end.Character.Xp > xp);
        Assert.Equal("v261_apres", Says(end, "npc.albe"));
        Pick(end);
        Assert.NotEqual("v261_apres", Says(end, "npc.albe"));
        Assert.Equal(1, end.Inventory.Count(Rosary));
        // La quête finie ne revient pas, même avec une nouvelle cicatrice.
        Assert.NotEqual("v261_offre", Says(AtAlbe(9, false, WithScar(end.Save().Character!, Ruins, 1, 9)), "npc.albe"));
    }

    [Fact]
    public void Une_purification_hors_de_l_etape_ne_compte_pas()
    {
        // Avant la quête, ou à l'étape de retour : le compteur ne bouge pas.
        SavedCharacter hero = Hero(0, new ItemStack(Shard, 1)).WithStory(new SavedStory(new[] { Born }, Done((Quest, "retour"))));
        WorldSession w = Session(Zone(22.5, -7), Noon(6), WithScar(hero, Ruins, 1, 6));
        Run(w, 2);
        Assert.Equal(ScarRefusal.None, w.PurifyScar(Ruins));
        Run(w, 2);
        Assert.Equal((0L, "retour"), (w.Narrative!.Progress(Quest), w.Narrative.CurrentStep(Quest)!.Id));
    }

    [Fact]
    public void Etoile_de_la_carte_vers_la_cicatrice_la_plus_proche_ou_nulle_part()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v261_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "vallons.json");
            SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { Born }, Done((Quest, "premiere")), tracked: Quest));
            SaveStore.Write(path, new WorldSave("zone.vallons", Noon(6), 1, new[] { PlayerCharacters.PlacesKey }, hero));
            Assert.Null(Adventure(path).Objective);   // aucune cicatrice : pas d'étoile, la quête attend
            SaveStore.Write(path, new WorldSave("zone.vallons", Noon(6), 1, new[] { PlayerCharacters.PlacesKey }, WithScar(WithScar(hero, Lake, 1, 6), Meadow, 1, 6)));
            AdventureSession a = Adventure(path);
            Assert.NotNull(a.Objective);
            GroundVector meadow = Vallons.EventOf(Rift)!.Groups.Single(g => g.Id == "pre").Position!.Value;
            Assert.Equal(((float)(meadow.X.Raw / 10000.0), (float)(meadow.Z.Raw / 10000.0)), ((float)a.Objective!.X, (float)a.Objective.Z));   // le pré, plus près du village
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // --- Ce qu'en disent les villageois ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("npc.gaspard", 25.5, -12.0)]
    [InlineData("npc.odile", -6.5, 3.0)]
    public void Gaspard_et_Odile_en_parlent_une_fois_chacun_puis_selon_l_etat(string npc, double x, double z)
    {
        string[] facts = { Born, Spread, "fact.event.faille_du_seuil.restee_ouverte", "fact.event.faille_du_seuil.refermee", "fact.event.faille_du_seuil.cicatrice_purifiee" };
        SavedCharacter hero = WithScar(WithScar(Hero(), Ruins, 1, 3), Lake, 1, 3).WithStory(new SavedStory(facts, Done()));
        WorldSession w = Near("zone.vallons", x, z, At(3, 10), hero);
        var heard = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            string node = Says(w, npc);
            if (!node.StartsWith("v261_", StringComparison.Ordinal) || node == "v261_froid") break;
            heard.Add(node);
            Pick(w);
        }
        string[] expected = npc == "npc.gaspard"
            ? new[] { "v261_propagee", "v261_cicatrice", "v261_refermee" }
            : new[] { "v261_purifiee", "v261_propagee", "v261_cicatrice", "v261_restee" };
        Assert.Equal(expected, heard);
        // Ensuite, tant qu'il y a des cicatrices (deux pour Gaspard, une pour Odile) : un mot selon l'état, chaque fois.
        Assert.Equal("v261_froid", Says(w, npc));
        Pick(w);
        Assert.Equal("v261_froid", Says(w, npc));
        // Sans cicatrice : plus rien de ce froid.
        WorldSession clear = Near("zone.vallons", x, z, At(3, 10), w.Save().Character!.WithScars(Array.Empty<SavedScar>()));
        Assert.NotEqual("v261_froid", Says(clear, npc));
    }

    [Fact]
    public void Pendant_la_quete_Gaspard_donne_un_remede_et_Odile_un_conseil_une_fois()
    {
        SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { Born, "fact.v261.gaspard.cicatrice", "fact.v261.odile.cicatrice" }, Done((Quest, "premiere"))));
        WorldSession g = Near("zone.vallons", 25.5, -12, At(3, 10), hero);
        Assert.Equal("v261_quete", Says(g, "npc.gaspard"));
        Pick(g);
        Assert.Equal(1, g.Inventory.Count("item.remede_de_l_aube"));
        Assert.NotEqual("v261_quete", Says(g, "npc.gaspard"));
        WorldSession o = Near("zone.vallons", -6.5, 3, At(3, 10), hero);
        Assert.Equal("v261_quete", Says(o, "npc.odile"));
    }

    [Theory]
    [InlineData("npc.archer_garde", "fact.event.faille_du_seuil.cicatrice_rode", "v261_rode")]
    [InlineData("npc.capitaine", Spread, "v261_propagee")]
    public void Aldric_et_le_capitaine_en_parlent_une_fois(string npc, string fact, string node)
    {
        Npc who = Book(Vallons).Npc(npc);
        SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { fact }, Done()));
        // À dix heures, chacun à sa place du matin.
        GroundVector p = who.SlotAt(10 * 60)?.Position ?? who.Position;
        WorldSession w = Near("zone.vallons", p.X.Raw / 10000.0 + 1.2, p.Z.Raw / 10000.0, At(3, 10), hero);
        Assert.Equal(node, Says(w, npc));
        Pick(w);
        Assert.NotEqual(node, Says(w, npc));
    }

    // --- Essai ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Essai_des_suites_du_Seuil()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v261_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "vallons.json");
            SaveStore.Write(path, new WorldSave("zone.vallons", Noon(5), 1, new[] { PlayerCharacters.PlacesKey }, WithScar(Hero(), Lake, 2, 4)));
            // Propager : ruines au palier 4 depuis deux jours, le lac effacé ; en entrant, le froid gagne le lac.
            Assert.Contains("le froid gagne au bord du lac", ScarTrial.Propagate(path, DataDirectory, ScenariosDirectory, "a"));
            Assert.Equal((Ruins, 4, 3L), SaveStore.Read(path).Save!.Character!.Scars.Select(s => (s.Id, s.Stage, s.Since)).Single());
            AdventureSession a = Adventure(path);
            Assert.Equal(new[] { Lake, Ruins }, a.Scars.Select(v => v.Key).OrderBy(k => k, StringComparer.Ordinal));
            // Lancer la quête : fait, quête suivie, deux éclats ; une cicatrice déjà là, rien d'ajouté.
            Assert.Contains("Quête « Les cicatrices du Seuil » commencée", ScarTrial.StartQuest(path, DataDirectory, ScenariosDirectory, "b"));
            SavedCharacter hero = SaveStore.Read(path).Save!.Character!;
            Assert.Equal(("premiere", Quest), (hero.Story.Quests[Quest].Step, hero.Story.Tracked));
            Assert.Contains(Born, hero.Story.Facts);
            Assert.Equal(2, hero.Inventory.Where(s => s.Item == Shard).Sum(s => s.Count));
            Assert.Single(hero.Scars);
            // Sans cicatrice : une aux ruines, au palier 1.
            SaveStore.Write(path, new WorldSave("zone.vallons", Noon(5), 1, new[] { PlayerCharacters.PlacesKey }, Hero()));
            Assert.Contains("Une cicatrice t'attend dans les ruines", ScarTrial.StartQuest(path, DataDirectory, ScenariosDirectory, "c"));
            Assert.Equal((Ruins, 1), SaveStore.Read(path).Save!.Character!.Scars.Select(s => (s.Id, s.Stage)).Single());
            Assert.Contains("Aucune sauvegarde", ScarTrial.Propagate(Path.Combine(dir, "absente.json"), DataDirectory, ScenariosDirectory));
            // Les menus Unity de l'essai (compilés par Unity, pas par ces tests) : lus dans le dépôt.
            string menu = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsScarTrial.cs"));
            Assert.Contains("[MenuItem(\"Éclats du Seuil/Essai des suites du Seuil : propager", menu, StringComparison.Ordinal);
            Assert.Contains("[MenuItem(\"Éclats du Seuil/Essai des suites du Seuil : lancer la quête", menu, StringComparison.Ordinal);
            Assert.Contains("ScarTrial.Propagate(", menu, StringComparison.Ordinal);
            Assert.Contains("ScarTrial.StartQuest(", menu, StringComparison.Ordinal);
            Assert.Contains("isPlayingOrWillChangePlaymode", menu, StringComparison.Ordinal);
            // Et le § 14 de l'essai.
            Assert.Contains("## 14. Les suites du Seuil (V2.6.1)", File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "docs", "ESSAI_V1.10.md")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
