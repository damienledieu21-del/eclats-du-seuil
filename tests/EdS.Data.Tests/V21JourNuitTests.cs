using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.1 (jour et nuit qui changent le monde) : zones en schéma 8 (heures des populations, places de nuit des PNJ), schéma 7 encore
/// lu ; loups de la nuit et échos qui arrivent à 22 h et s'en vont à l'aube, sangliers qui dorment la nuit ; PNJ qui rentrent chez eux la
/// nuit et restent joignables devant leur porte ; bornes (21 h 59 / 22 h, 4 h 59 / 5 h), transitions, déterminisme et sauvegarde inchangée.
/// </summary>
[Trait("Category", "World")]
public sealed class V21JourNuitTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v21_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly ZoneDefinition Vallons = Zone();

    private static readonly string[] NightWolves = Enumerable.Range(1, 7).Select(i => "loup_nuit" + i).ToArray();
    private static readonly string[] Echoes = Enumerable.Range(1, 4).Select(i => "loup_echo" + i).ToArray();
    private static readonly string[] Sleepers = Enumerable.Range(4, 5).Select(i => "sanglier" + i).ToArray();

    /// <summary>Tick du monde où il est <paramref name="minute"/> au jour <paramref name="day"/> (le monde commence à 8 h du jour 0).</summary>
    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        // Premier tick de cette minute (la minute du monde dure plusieurs ticks).
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    private static WorldSession At(long tick, SavedCharacter? hero = null, ZoneDefinition? zone = null)
    {
        ZoneDefinition z = zone ?? Vallons;
        return new WorldSession(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero ?? Hero(1000)), z, ShippedData, Progression,
            ItemsCatalog, Book(z), market: new Market(ShippedMerchants));
    }

    private static bool Present(WorldSession w, string creature)
    {
        string current = w.CurrentCreature(creature);
        return w.Zone.Engine.CombatantIds.Contains(current) && w.Zone.Engine.View(current).IsPresent;
    }

    private static ZoneLoadResult ZoneMutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        mutate(root);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
    }

    private static JsonObject Population(JsonObject root, string creature) =>
        root["data"]!["populations"]!.AsArray().Select(p => p!.AsObject()).Single(p => (string)p["creature"]! == creature);

    private static int PopulationIndex(JsonObject root, string creature) =>
        root["data"]!["populations"]!.AsArray().Select((p, i) => ((string)p!["creature"]!, i)).Single(x => x.Item1 == creature).i;

    private static ValidationError Error(ZoneLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0];
    }

    // --- Format : schéma 8, schéma 7 encore lu ----------------------------------------------------------------------------------

    [Fact]
    public void Vallons_livres_au_schema_8()
    {
        Assert.Equal((8, 7), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.PreviousSchemaVersion));
        Assert.Equal(8, Vallons.SchemaVersion);
        foreach (string id in NightWolves.Concat(Echoes))
        {
            Population p = Vallons.PopulationOf(id)!;
            Assert.Equal(1, p.Hours!.Mask);   // la nuit seulement
            Assert.True(p.Hours.IsActive((int)DayPhase.Night));
            Assert.False(p.Hours.IsActive((int)DayPhase.Dusk) || p.Hours.IsActive((int)DayPhase.Dawn) || p.Hours.IsActive((int)DayPhase.Day));
            Assert.Null(p.Night);
        }
        // Les loups de la nuit comptent pour les objectifs de famille « loup » (la garde de nuit d'Aldric, les contrats) ; les échos, à part.
        Assert.All(NightWolves, id => Assert.Equal("loup", Vallons.PopulationOf(id)!.Family));
        Assert.All(Echoes, id => Assert.Equal("echo", Vallons.PopulationOf(id)!.Family));
        Assert.Equal("text.zone.loups_nuit.signe", Vallons.PopulationOf("loup_nuit1")!.Hours!.Sign);
        Assert.Equal("text.zone.echos.signe", Vallons.PopulationOf("loup_echo1")!.Hours!.Sign);
        Assert.Null(Vallons.PopulationOf("loup_nuit2")!.Hours!.Sign);
        foreach (string id in Sleepers)
        {
            PopulationHours h = Vallons.PopulationOf(id)!.Hours!;
            Assert.Equal((false, true, true, true), (h.IsActive(0), h.IsActive(1), h.IsActive(2), h.IsActive(3)));
        }
        // Les autres créatures sont toujours là (le loup blanc garde ses nuits rares).
        Assert.Null(Vallons.PopulationOf("loup1")!.Hours);
        Assert.Null(Vallons.PopulationOf("sanglier")!.Hours);
        Assert.Null(Vallons.PopulationOf("loup_blanc")!.Hours);
        Assert.Equal(new[] { "npc.odile", "npc.maelle", "npc.apprentie", "npc.armurier", "npc.epiciere", "npc.doyen",
            "npc.herboriste_hameau", "npc.fermier" }, Vallons.NightSpots.Select(n => n.Npc));
        // Ceux qui veillent restent à leur place : Aldric (la garde de nuit), le capitaine, Lison (le puits la nuit), Sauge (ouverte la nuit),
        // le boulanger (le pain se fait la nuit), Gaspard, Renart (son étal reste ouvert la nuit).
        foreach (string npc in new[] { "npc.archer_garde", "npc.capitaine", "npc.lison", "npc.herboriste", "npc.boulanger", "npc.gaspard", "npc.renart" })
            Assert.Null(Vallons.NightSpotOf(npc));
        // Le schéma livré décrit le fichier livré.
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"));
        Assert.True(schema.IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement));
    }

    [Fact]
    public void Schema_7_encore_lu_sans_heures_ni_places_de_nuit()
    {
        ZoneLoadResult v7 = ZoneMutated(root =>
        {
            root["schemaVersion"] = 7;
            foreach (JsonNode? p in root["data"]!["populations"]!.AsArray()) p!.AsObject().Remove("hours");
            root["data"]!.AsObject().Remove("nightSpots");
        });
        Assert.True(v7.IsValid, v7.Report.ToString());
        Assert.Equal(7, v7.Zone!.SchemaVersion);
        Assert.All(v7.Zone.Populations, p => Assert.Null(p.Hours));
        Assert.Empty(v7.Zone.NightSpots);
        // Une zone de schéma 7 : toutes ses créatures sont là d'emblée, de jour comme de nuit (sauf la créature rare de la nuit).
        Assert.Equal(v7.Zone.Setup.Combatants.Count - 1, ZoneSetups.For(v7.Zone, Array.Empty<string>(), DayPhase.Night).Combatants.Count);
        // Les intérieurs livrés restent au schéma 7.
        Assert.All(V16TestData.Interiors, z => Assert.Equal(ZoneValidator.PreviousSchemaVersion, z.SchemaVersion));

        ValidationError hours = Error(ZoneMutated(root =>
        {
            root["schemaVersion"] = 7;
            root["data"]!.AsObject().Remove("nightSpots");
        }));
        Assert.Equal(($"$.data.populations[{PopulationIndex(JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject(), "sanglier4")}].hours",
            ValidationCode.InvalidValue), (hours.Path, hours.Code));
        ValidationError spots = Error(ZoneMutated(root =>
        {
            root["schemaVersion"] = 7;
            foreach (JsonNode? p in root["data"]!["populations"]!.AsArray()) p!.AsObject().Remove("hours");
        }));
        Assert.Equal(("$.data.nightSpots", ValidationCode.InvalidValue), (spots.Path, spots.Code));
        foreach (int version in new[] { 6, 9 })
        {
            ValidationError e = Error(ZoneMutated(root => root["schemaVersion"] = version));
            Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (e.Path, e.Code));
        }
    }

    public static IEnumerable<object[]> BadHours() => new[]
    {
        new object[] { "[\"midi\"]", null!, ".phases[0]", ValidationCode.InvalidValue },
        new object[] { "[\"night\", \"night\"]", null!, ".phases[1]", ValidationCode.DuplicateId },
        new object[] { "[]", null!, ".phases", ValidationCode.OutOfRange },
        new object[] { "[\"night\", \"dawn\", \"day\", \"dusk\"]", null!, ".phases", ValidationCode.OutOfRange },
        new object[] { "[\"night\"]", "Text.Majuscule", ".sign", ValidationCode.InvalidValue },
    };

    [Theory]
    [MemberData(nameof(BadHours))]
    public void Heures_mal_formees_refusees_au_chemin_precis(string phases, string? sign, string suffix, ValidationCode code)
    {
        JsonObject shipped = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        int i = PopulationIndex(shipped, "loup_nuit2");
        ValidationError e = Error(ZoneMutated(root =>
        {
            var hours = new JsonObject { ["phases"] = JsonNode.Parse(phases) };
            if (sign != null) hours["sign"] = sign;
            Population(root, "loup_nuit2")["hours"] = hours;
        }));
        Assert.Equal(($"$.data.populations[{i}].hours{suffix}", code), (e.Path, e.Code));
    }

    [Fact]
    public void Heures_et_nuit_rare_ne_vont_pas_ensemble()
    {
        JsonObject shipped = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        int i = PopulationIndex(shipped, "loup_blanc");
        ValidationError e = Error(ZoneMutated(root => Population(root, "loup_blanc")["hours"] = new JsonObject { ["phases"] = new JsonArray("night") }));
        Assert.Equal(($"$.data.populations[{i}].hours", ValidationCode.InvalidValue), (e.Path, e.Code));
    }

    [Theory]
    [InlineData("odile", -4, 4.8, ".npc", ValidationCode.InvalidValue)]
    [InlineData("npc.maelle", -4, 4.8, ".npc", ValidationCode.DuplicateId)]
    [InlineData("npc.odile", -3, 9, ".position", ValidationCode.InvalidValue)]      // dans le moulin
    [InlineData("npc.odile", 500, 0, ".position", ValidationCode.OutOfRange)]      // hors de l'aire
    public void Places_de_nuit_mal_formees_refusees(string npc, double x, double z, string suffix, ValidationCode code)
    {
        ValidationError e = Error(ZoneMutated(root =>
        {
            JsonArray spots = root["data"]!["nightSpots"]!.AsArray();
            spots[0] = new JsonObject { ["npc"] = npc, ["position"] = new JsonArray(x, z) };
        }));
        string path = code == ValidationCode.DuplicateId ? "$.data.nightSpots[1]" : "$.data.nightSpots[0]";   // npc.maelle est à l'indice 1
        Assert.Equal((path + suffix, code), (e.Path, e.Code));
    }

    [Theory]
    [InlineData("npc.personne", ValidationCode.UnknownReference)]
    [InlineData("npc.aubergiste", ValidationCode.InvalidValue)]   // il vit dans l'auberge, pas dans les Vallons
    public void L_histoire_verifie_les_PNJ_des_places_de_nuit(string npc, ValidationCode code)
    {
        ZoneLoadResult zone = ZoneMutated(root => root["data"]!["nightSpots"]![0]!["npc"] = npc);
        Assert.True(zone.IsValid, zone.Report.ToString());
        StoryLoadResult story = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), Texts, zone.Zone!, ItemsCatalog, V16TestData.Interiors);
        Assert.False(story.IsValid);
        Assert.Contains(story.Report.Errors, e => e.Path == "zone.vallons:$.data.nightSpots[0].npc" && e.Code == code);
    }

    // --- Créatures à leurs heures : bornes ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 21 * 60 + 59, DayPhase.Dusk)]
    [InlineData(0, 22 * 60, DayPhase.Night)]
    [InlineData(1, 4 * 60 + 59, DayPhase.Night)]
    [InlineData(1, 5 * 60, DayPhase.Dawn)]
    [InlineData(1, 12 * 60, DayPhase.Day)]
    public void Au_debut_de_la_session_chacun_a_ses_heures(long day, int minute, DayPhase phase)
    {
        WorldSession w = At(TickAt(day, minute));
        w.Step();
        Assert.Equal(phase, w.Zone.Time.Phase);
        bool night = phase == DayPhase.Night;
        // D'emblée, sous leur identité de départ (pas de vie suivante) : les loups de la nuit et les échos la nuit, les sangliers hors de la nuit.
        foreach (string id in NightWolves.Concat(Echoes))
        {
            Assert.Equal(night, Present(w, id));
            if (night) Assert.Equal(id, w.CurrentCreature(id));
        }
        foreach (string id in Sleepers) Assert.Equal(!night, Present(w, id));
        Assert.True(Present(w, "loup1") && Present(w, "sanglier"));   // les autres, toujours
        Assert.Equal(night ? 11 : 0, ZoneSetups.For(Vallons, Array.Empty<string>(), phase).Combatants
            .Count(c => c.Id.StartsWith("loup_nuit", StringComparison.Ordinal) || c.Id.StartsWith("loup_echo", StringComparison.Ordinal)));
        Assert.Equal(night ? 0 : 5, ZoneSetups.For(Vallons, Array.Empty<string>(), phase).Combatants.Count(c => Sleepers.Contains(c.Id)));
    }

    // --- Transitions : crépuscule, aube ---------------------------------------------------------------------------------------

    private static List<PopulationEvent> Until(WorldSession w, DayPhase phase, int extraTicks)
    {
        var events = new List<PopulationEvent>();
        for (int i = 0; i < 2000 && w.Zone.Time.Phase != phase; i++) events.AddRange(w.Step().Population);
        Assert.Equal(phase, w.Zone.Time.Phase);
        for (int i = 0; i < extraTicks; i++) events.AddRange(w.Step().Population);
        return events;
    }

    [Fact]
    public void A_22_h_les_loups_hurlent_et_les_sangliers_vont_dormir()
    {
        WorldSession w = At(TickAt(0, 21 * 60 + 59));
        w.Step();
        Assert.All(NightWolves, id => Assert.False(Present(w, id)));
        List<PopulationEvent> events = Until(w, DayPhase.Night, 5);
        // Signes : un seul hurlement et une seule annonce des échos, à 22 h.
        // (Le loup blanc garde ses nuits rares, tirées à part.)
        PopulationEvent[] signs = events.Where(e => e.Kind == PopulationEventKind.Sign && e.Population != "loup_blanc").ToArray();
        Assert.Equal(new[] { ("loup_nuit1", "text.zone.loups_nuit.signe"), ("loup_echo1", "text.zone.echos.signe") },
            signs.Select(e => (e.Population, e.Creature)).OrderBy(x => x.Item1, StringComparer.Ordinal).Reverse().ToArray());
        // Tous arrivent, sous une vie suivante (les identités ne sont jamais réutilisées).
        foreach (string id in NightWolves.Concat(Echoes))
        {
            Assert.Contains(events, e => e.Kind == PopulationEventKind.Respawned && e.Population == id);
            Assert.True(Present(w, id));
            Assert.StartsWith(id + "_v", w.CurrentCreature(id), StringComparison.Ordinal);
        }
        // Les sangliers, au calme, s'en vont dormir : ni mort, ni butin, ni expérience.
        foreach (string id in Sleepers)
        {
            Assert.Contains(events, e => e.Kind == PopulationEventKind.Withdrew && e.Population == id);
            Assert.False(Present(w, id));
            Assert.False(w.Zone.Engine.View(id).IsDead);
        }
        Assert.DoesNotContain(events, e => e.Kind == PopulationEventKind.Exhausted);
    }

    [Fact]
    public void A_l_aube_les_betes_de_la_nuit_s_en_vont_et_les_sangliers_reviennent()
    {
        WorldSession w = At(TickAt(1, 4 * 60 + 59));
        w.Step();
        Assert.All(NightWolves, id => Assert.True(Present(w, id)));
        List<PopulationEvent> events = Until(w, DayPhase.Dawn, 5);
        foreach (string id in NightWolves.Concat(Echoes))
        {
            Assert.Contains(events, e => e.Kind == PopulationEventKind.Withdrew && e.Population == id);
            Assert.False(Present(w, id));
        }
        // Les sangliers se réveillent à leur place, sans annonce.
        foreach (string id in Sleepers)
        {
            Assert.Contains(events, e => e.Kind == PopulationEventKind.Respawned && e.Population == id);
            Assert.True(Present(w, id));
            Assert.Equal(Vallons.PopulationOf(id)!.Template.Placement!.Position, w.Zone.Engine.View(w.CurrentCreature(id)).Position);
        }
        Assert.DoesNotContain(events, e => e.Kind == PopulationEventKind.Sign);
    }

    [Fact]
    public void Une_quete_qui_demande_un_sanglier_precis_le_fait_venir_la_nuit()
    {
        // Le grand sanglier de « Les sangliers des champs » (objectif « vaincre sanglier4 ») : la quête reste jouable la nuit, comme le loup blanc.
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.sangliers_des_champs"] = new QuestProgress("grand") }, null);
        WorldSession w = At(TickAt(0, 23 * 60), Hero(1000).WithStory(story));
        for (int i = 0; i < 3; i++) w.Step();
        Assert.Equal(DayPhase.Night, w.Zone.Time.Phase);
        Assert.True(Present(w, "sanglier4"));
        Assert.False(Present(w, "sanglier5"));   // pas encore demandé : il dort
        Assert.False(Present(w, "sanglier6"));
    }

    [Fact]
    public void Meme_partie_memes_nuits()
    {
        string Run()
        {
            WorldSession w = At(TickAt(0, 21 * 60 + 59));
            var log = new List<string>();
            for (int i = 0; i < 260; i++)
                foreach (PopulationEvent e in w.Step().Population) log.Add($"{e.Tick}:{e.Kind}:{e.Population}:{e.Creature}");
            return string.Join("\n", log) + "\n" + Snapshot(w);
        }
        string first = Run();
        Assert.Contains("Sign:loup_nuit1", first, StringComparison.Ordinal);
        Assert.Equal(first, Run());
    }

    [Fact]
    public void Sauvegarde_inchangee_la_nuit_reprend_la_nuit()
    {
        WorldSession w = At(TickAt(0, 21 * 60 + 59));
        Until(w, DayPhase.Night, 20);
        string text = SaveJsonReader.Write(w.Save());
        Assert.Contains($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", text, StringComparison.Ordinal);
        Assert.Equal(12, SaveValidator.CurrentSchemaVersion);   // format de la sauvegarde inchangé
        WorldSave back = SaveJsonReader.Read(text).Save!;
        // Session suivante, toujours la nuit : les bêtes de la nuit sont là d'emblée, les sangliers dorment.
        var next = new WorldSession(back, Vallons, ShippedData, Progression, ItemsCatalog, Book(Vallons), market: new Market(ShippedMerchants));
        next.Step();
        Assert.Equal(DayPhase.Night, next.Zone.Time.Phase);
        Assert.All(NightWolves, id => Assert.True(Present(next, id)));
        Assert.All(Sleepers, id => Assert.False(Present(next, id)));
    }

    // --- PNJ qui rentrent chez eux, toujours joignables -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 21 * 60 + 59, false)]
    [InlineData(0, 22 * 60, true)]
    [InlineData(1, 4 * 60 + 59, true)]
    [InlineData(1, 5 * 60, false)]
    public void La_nuit_les_villageois_sont_devant_leur_porte(long day, int minute, bool home)
    {
        WorldSession w = At(TickAt(day, minute));
        w.Step();
        StoryBook book = Book(Vallons);
        foreach (NightSpot spot in Vallons.NightSpots)
        {
            Npc npc = book.Npc(spot.Npc);
            Assert.Equal(home ? spot.Position : npc.Position, w.NpcPosition(npc));
        }
        foreach (string id in new[] { "npc.archer_garde", "npc.lison", "npc.herboriste", "npc.boulanger", "npc.aubergiste" })
            Assert.Equal(book.Npc(id).Position, w.NpcPosition(book.Npc(id)));
    }

    [Fact]
    public void La_nuit_la_boutique_fermee_le_dit_a_l_etal_comme_a_la_porte()
    {
        long night = TickAt(0, 23 * 60);
        Npc garin = Book(Vallons).Npc("npc.armurier");
        // À l'étal : Garin n'y est plus, la boutique est fermée.
        WorldSession stall = At(night, zone: Zone(Near("merchant.armurier").X, Near("merchant.armurier").Z));
        stall.Step();
        Assert.Equal(TradeRefusal.Closed, stall.CanTrade("merchant.armurier"));
        // À sa porte : on lui parle (fermé quand même).
        WorldSession door = At(night, zone: Zone(NearAtNight("merchant.armurier").X, NearAtNight("merchant.armurier").Z));
        door.Step();
        Assert.Equal(Vallons.NightSpotOf("npc.armurier"), door.NpcPosition(garin));
        Assert.Equal(TradeRefusal.Closed, door.CanTrade("merchant.armurier"));
        // Ailleurs : trop loin.
        WorldSession far = At(night);
        far.Step();
        Assert.Equal(TradeRefusal.TooFar, far.CanTrade("merchant.armurier"));
    }

    [Fact]
    public void La_nuit_on_parle_a_Odile_devant_sa_porte()
    {
        GroundVector spot = Vallons.NightSpotOf("npc.odile")!.Value;
        (string data, string scenarios) = DataCopy((spot.X.Raw / 10000.0, Math.Round(spot.Z.Raw / 10000.0 - 1.6, 4)));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(0, 23 * 60), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save, data, scenarios);
        Assert.Equal(DayPhase.Night, a.World.Zone.Time.Phase);
        Assert.Equal("npc.odile", a.NpcInReach!.Id);
        WorldMarker marker = a.Npcs().Single(n => n.Id == "npc.odile");
        Assert.Equal((spot.X.Raw / 10000.0, spot.Z.Raw / 10000.0), (marker.X, marker.Z));
        // Relecture de la PR #99 : la place de jour reste celle de l'histoire, même la nuit (l'affichage y prend sa pose de jour).
        WorldMarker day = a.NpcsByDay().Single(n => n.Id == "npc.odile");
        GroundVector story = Book(Vallons).Npc("npc.odile").Position;
        Assert.Equal((story.X.Raw / 10000.0, story.Z.Raw / 10000.0), (day.X, day.Z));
        Assert.True(a.Interact());
        Assert.NotNull(a.Dialogue);
    }

    // --- Affichage : lumière du jour et de la nuit, lanternes, ambiances ------------------------------------------------------

    [Fact]
    public void La_nuit_est_bleue_et_lisible_le_jour_garde_le_catalogue()
    {
        DayLight noon = DayCycle.At(12 * 60);
        Assert.Equal((1.0, 1.0, 1.0, 0.0, 0.0, false), (noon.SunLevel, noon.AmbientLevel, noon.CatalogShare, noon.Lamps, noon.Night, noon.Moon));
        Assert.Equal(StyleTint.White, noon.AmbientTint);
        Assert.Equal(55.0, noon.Height(55));   // le plein jour garde la hauteur du soleil réglée par Damien
        DayLight midnight = DayCycle.At(0);
        Assert.True(midnight.Moon);
        Assert.True(midnight.AmbientLevel >= 0.5, "la nuit reste lisible");
        Assert.True(midnight.AmbientTint.B > midnight.AmbientTint.R && midnight.AmbientTint.B > midnight.AmbientTint.G, "la nuit est bleue");
        Assert.True(midnight.SunLevel > 0.3, "la lune éclaire");
        Assert.Equal((1.0, 1.0), (midnight.Lamps, midnight.Night));
        Assert.Equal(DayCycle.MoonHeight, midnight.Height(55));
        // Relecture de la PR #99 : au relais, la lumière directionnelle est éteinte exactement, de part et d'autre de la bascule.
        foreach (double turn in new[] { DayCycle.MoonSets, DayCycle.MoonRises })
        {
            Assert.Equal(0.0, DayCycle.At(turn).SunLevel);
            Assert.NotEqual(DayCycle.At(turn - 0.01).Moon, DayCycle.At(turn).Moon);
            Assert.True(DayCycle.At(turn - 0.01).SunLevel < 1e-4 && DayCycle.At(turn + 0.01).SunLevel < 1e-4);
        }
        // L'aube est rose, le crépuscule orangé (plus de rouge que de bleu dans la lumière).
        Assert.True(DayCycle.At(6 * 60).SunTint.R > DayCycle.At(6 * 60).SunTint.B);
        Assert.True(DayCycle.At(20 * 60 + 30).SunTint.R > DayCycle.At(20 * 60 + 30).SunTint.B);
        // Bornes de la nuit du monde : 22 h et 5 h ; les lanternes sont toutes allumées avant 22 h.
        Assert.Equal((1.0, 1.0, 1.0), (DayCycle.NightShare(22 * 60), DayCycle.NightShare(4 * 60 + 59), DayCycle.Lamps(21 * 60 + 30)));
        Assert.Equal((0.0, 0.0), (DayCycle.NightShare(7 * 60), DayCycle.Lamps(6 * 60 + 30)));
        // Ramené dans la journée.
        Assert.Equal(DayCycle.At(60).SunLevel, DayCycle.At(60 + 1440).SunLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => DayCycle.At(double.NaN));
    }

    [Fact]
    public void Transitions_douces_sans_saut()
    {
        DayLight previous = DayCycle.At(0);
        for (double m = 0.25; m <= 1440; m += 0.25)
        {
            DayLight l = DayCycle.At(m);
            foreach ((double a, double b) in new[] { (previous.SunLevel, l.SunLevel), (previous.AmbientLevel, l.AmbientLevel), (previous.SkyLevel, l.SkyLevel),
                (previous.Lamps, l.Lamps), (previous.Night, l.Night), (previous.AmbientTint.R, l.AmbientTint.R), (previous.SunTint.G, l.SunTint.G),
                (previous.Height(55), l.Height(55)) })
                Assert.True(Math.Abs(a - b) < (a == previous.Height(55) ? 1.2 : 0.02), $"saut à la minute {m} : {a} → {b}");
            // Relecture de la PR #99 : la lune et le soleil se passent le relais lumière éteinte (aucune ombre ne saute).
            if (l.Moon != previous.Moon) Assert.True(l.SunLevel < 0.001 && previous.SunLevel < 0.001, $"relais visible à la minute {m}");
            Assert.InRange(l.AmbientLevel, DayCycle.NightAmbient, 1);
            previous = l;
        }
    }

    [Fact]
    public void Heure_exacte_d_un_tick()
    {
        var clock = new WorldClock(50);
        Assert.Equal(480.0, DayCycle.ExactMinute(clock, 0));
        foreach (long tick in new[] { 1L, 999, 12_345, clock.TicksPerDay - 1, clock.TicksPerDay * 3 + 7 })
        {
            double exact = DayCycle.ExactMinute(clock, tick);
            Assert.Equal(clock.At(tick).Minute, (int)Math.Floor(exact));
            Assert.True(DayCycle.ExactMinute(clock, tick, 0.5) > exact || exact > 1439);
        }
    }

    [Fact]
    public void Lanternes_fenetres_veilleurs_et_ambiances_de_nuit()
    {
        // Lanterne de porte : devant la façade (tournée vers le sud ici), hors du bâtiment, à côté du milieu (la porte).
        NightLamp door = NightLamps.DoorLantern("maison1", -10, -5, -4, 5, (0, -1));
        Assert.Equal((NightLampKind.DoorLantern, NightLamps.DoorLanternHeight), (door.Kind, door.Y));
        Assert.True(door.Z < -5 && door.Z > -5.5 && door.X > -10 && door.X < -4 && Math.Abs(door.X + 7) > 0.5);
        Assert.Equal(door.X, NightLamps.DoorLantern("maison1", -10, -5, -4, 5, (0, -1)).X);
        Assert.True(NightLamps.HasLantern(StyleBuilding.House) && !NightLamps.HasLantern(StyleBuilding.Stall) && !NightLamps.HasLantern(StyleBuilding.Woods));
        // Réverbères : tous les 14 m le long d'une rue, au bord, en alternant ; rien là où ce n'est pas libre.
        IReadOnlyList<NightLamp> posts = NightLamps.Street("rue", new[] { (0.0, 0.0), (60.0, 0.0) }, 6, (x, z) => true);
        Assert.Equal(new[] { 7.0, 21, 35, 49 }, posts.Select(p => p.X));
        Assert.Equal(new[] { 3.6, -3.6, 3.6, -3.6 }, posts.Select(p => Math.Round(p.Z, 6)));
        Assert.Empty(NightLamps.Street("rue", new[] { (0.0, 0.0), (60.0, 0.0) }, 6, (x, z) => false));
        // Feux follets : dans leur lieu, toujours aux mêmes places.
        IReadOnlyList<NightLamp> wisps = NightLamps.Wisps("lac", -14, -70, 26, -42, 6, (x, z) => true);
        Assert.Equal(6, wisps.Count);
        Assert.All(wisps, w => Assert.True(w.X >= -14 && w.X <= 26 && w.Z >= -70 && w.Z <= -42 && w.Y >= 0.6 && w.Y <= 1.6));
        Assert.Equal(wisps.Select(w => w.X), NightLamps.Wisps("lac", -14, -70, 26, -42, 6, (x, z) => true).Select(w => w.X));
        // Hiboux : un arbre sur cinq environ, toujours les mêmes.
        int owls = Enumerable.Range(0, 200).Count(i => NightLamps.Owl("arbre" + i, i, 0) != null);
        Assert.InRange(owls, 20, 60);
        Assert.Equal(NightLamps.Owl("arbre3", 1, 2).HasValue, NightLamps.Owl("arbre3", 1, 2).HasValue);
        // Villageois d'affichage : les marcheurs rentrent tous ; un sur quatre environ des autres veille.
        Assert.False(NightLamps.AwakeAtNight(new StyleVillager("rue/passant", "paysan", VillagerActivity.Walk, 0, 0, 0, 5, 5)));
        int awake = Enumerable.Range(0, 400).Count(i => NightLamps.AwakeAtNight(new StyleVillager("v" + i, "paysan", VillagerActivity.Talk, 0, 0, 0)));
        Assert.InRange(awake, 60, 140);
        Assert.True(NightLamps.IsEcho("loup_echo2_v3") && !NightLamps.IsEcho("loup_nuit1"));
        // Ambiances : les oiseaux se taisent la nuit, la nappe de nuit chante ; le jour, l'inverse.
        Assert.Equal(0.0, StyleRules.AmbienceVolume(StyleAmbience.Night, double.NaN, false, false, night: 0));
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Night, double.NaN, false, false, night: 1) > 0.5);
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, false, false, night: 1) < 0.2);
        Assert.Equal(1.0, StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, false, false));
        Assert.Equal(StyleAmbience.Night, StyleRules.AmbienceOf("Assets/Style/Ambience/Grillons de nuit.ogg"));
        Assert.Equal(StyleAmbience.Night, StyleRules.AmbienceOf("Assets/Style/Ambience/night_forest_owl.ogg"));
        // Fenêtres : leur fond est à part (allumé la nuit) ; la porte reste dans les ouvertures sombres.
        StyleBuildingShapes house = TownShapes.Building(StyleBuilding.House, new[] { (-5.0, -4.0), (5.0, -4.0), (5.0, 4.0), (-5.0, 4.0) }, "maison1", (0, 1), 2.5, 2, 2, 2);
        Assert.NotEmpty(house.Windows.Triangles);
        Assert.NotEmpty(house.Dark.Triangles);
        Assert.Contains(house.Windows, house.All);
    }

    [Fact]
    public void Essai_de_la_nuit_avance_seulement_le_temps_du_monde()
    {
        var clock = new WorldClock(50);
        foreach (int minute in new[] { 21 * 60 + 55, 4 * 60 + 55, 0, 8 * 60 })
        {
            long tick = DayTrial.NextTickAt(clock, 1234, minute);
            Assert.True(tick >= 1234 && tick - 1234 < clock.TicksPerDay);
            Assert.Equal(minute, clock.At(tick).Minute);
            Assert.True(tick == 1234 || clock.At(tick - 1).Minute != minute);   // le premier tick de cette minute
        }
        string path = Path.Combine(_folder, "essai", "vallons.json");
        Assert.StartsWith("Aucune sauvegarde", DayTrial.MoveSave(path, 0), StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var save = new WorldSave("zone.vallons", 500, 3, new[] { "zone.vallons/safe/place_du_village" }, Hero(42));
        SaveStore.Write(path, save);
        Assert.StartsWith("Partie avancée au jour 0, 21 h 55", DayTrial.MoveSave(path, 21 * 60 + 55), StringComparison.Ordinal);
        WorldSave moved = SaveStore.Read(path).Save!;
        Assert.Equal(new WorldTime(0, 21 * 60 + 55), clock.At(moved.WorldTick));
        Assert.Equal((save.Zone, save.Sessions, save.Character!.Money), (moved.Zone, moved.Sessions, moved.Character!.Money));
        Assert.Equal(save.Consumed, moved.Consumed);
    }

    private (string Data, string Scenarios) DataCopy((double X, double Z) entry)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(ScenariosDirectory, scenarios);
        string zone = ZoneJsonReader.PathIn(data, "zone.vallons");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(entry.X, entry.Z);
        File.WriteAllText(zone, root.ToJsonString());
        return (data, scenarios);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
