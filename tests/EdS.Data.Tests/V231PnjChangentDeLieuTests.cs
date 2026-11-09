using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.3.1 (PNJ qui changent de lieu, accord de Damien du 7 novembre 2026) : histoire en schéma 10 (zone facultative d'un créneau),
/// schéma 9 encore lu ; refus au chemin précis (zone inconnue, porte manquante, zone du PNJ redonnée, pièce d'une scène, place hors de la
/// zone du créneau) ; un PNJ n'est jamais à deux endroits ni perdu ; on lui parle, on lui livre et on commerce là où il est ; il finit la
/// conversation avant de partir ; « déjà rencontré » est par PNJ ; déterminisme et sauvegarde inchangée ; affichage (portes) et menu d'essai.
/// </summary>
[Trait("Category", "World")]
public sealed class V231PnjChangentDeLieuTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v231_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly Lazy<StoryBook> Shipped = new(() => Book(Vallons));

    /// <summary>Les PNJ qui changent de zone dans leur journée (contenu 0.19.0).</summary>
    private static readonly string[] Roamers =
    {
        "npc.maelle", "npc.armurier", "npc.epiciere", "npc.aubergiste", "npc.pretre", "npc.habitant", "npc.intendant_guilde", "npc.boulanger",
        "npc.renart", "npc.fermier", "npc.capitaine", "npc.albe",   // V2.6.1 : la veille du soir de Sœur Albe sur le parvis
    };

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    /// <summary>Premier jour (à partir du jour 1) où il ne pleut pas fort à cette minute (personne ne s'abrite).</summary>
    private static long DryDay(int minute)
    {
        for (long day = 1; ; day++)
            if (!WorldSession.Shelters(WorldWeather.At("zone.vallons", day, minute).Kind)) return day;
    }

    private static GroundVector P(double x, double z) => new GroundVector(EdS.Domain.Numerics.Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        EdS.Domain.Numerics.Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Partie dehors (point d'entrée en <paramref name="x"/>, <paramref name="z"/>), à ce tick.</summary>
    private static WorldSession Outside(long tick, double x, double z, SavedCharacter? hero = null, MerchantBook? merchants = null) => Session(new WorldSave("zone.vallons", tick, 0,
        new[] { PlayerCharacters.PlacesKey }, hero ?? Hero(1000)), Zone(x, z), null, merchants);

    private static WorldSession Inside(string id, long tick, double x, double z, SavedCharacter? hero = null) => V231TestData.Inside(id, tick, x, z, Shipped.Value, hero);

    private static WorldSession Session(WorldSave save, ZoneDefinition zone, string? storyZone, MerchantBook? merchants = null)
    {
        var w = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, Shipped.Value, storyZone: storyZone, market: new Market(merchants ?? ShippedMerchants));
        w.Step();
        return w;
    }

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons, ItemsCatalog, V16TestData.Interiors);

    private static int NpcIndex(JsonObject root, string npc) =>
        root["data"]!["npcs"]!.AsArray().Select((n, i) => ((string)n!["id"]!, i)).Single(x => x.Item1 == npc).i;

    private static int SlotIndex(JsonObject root, string npc, string at) =>
        root["data"]!["npcs"]![NpcIndex(root, npc)]!["schedule"]!.AsArray().Select((s, i) => ((string)s!["at"]!, i)).Single(x => x.Item1 == at).i;

    private static void Refused(string path, ValidationCode code, Action<JsonObject> mutate)
    {
        JsonObject root = StoryJson();
        mutate(root);
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, x => x.Path == path && x.Code == code);
    }

    // --- Format : schéma 10 -----------------------------------------------------------------------------------------------

    [Fact]
    public void Histoire_livree_en_schema_10_avec_les_PNJ_qui_changent_de_lieu()
    {
        StoryBook book = Shipped.Value;
        Assert.Equal((13, 13, 9, "0.25.0-provisoire"), (book.SchemaVersion, StoryValidator.CurrentSchemaVersion, StoryValidator.Schema9, book.ContentVersion));
        Assert.Equal(Roamers.OrderBy(x => x, StringComparer.Ordinal), book.Npcs.Where(n => n.Roams).Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal));
        // La salle de l'auberge se remplit : neuf visiteurs, plus Mathurin qui y vit.
        Assert.Equal(new[] { "npc.maelle", "npc.armurier", "npc.epiciere", "npc.aubergiste", "npc.pretre", "npc.intendant_guilde", "npc.boulanger", "npc.renart", "npc.fermier", "npc.capitaine" },
            book.VisitorsOf("zone.auberge").Select(n => n.Id));
        // Rosalie sort (chapelle, puits, place) ; Mathurin fume sur la terrasse ; Frère Anselme fait sa tournée.
        Npc rosalie = book.Npc("npc.habitant"), mathurin = book.Npc("npc.aubergiste"), anselme = book.Npc("npc.pretre");
        Assert.Equal(new[] { "zone.maison", "zone.chapelle", null }, rosalie.Zones);
        Assert.Equal(("zone.chapelle", (string?)null, (string?)null, "zone.maison"), (rosalie.ZoneAt(6 * 60 + 45), rosalie.ZoneAt(12 * 60 + 15), rosalie.ZoneAt(16 * 60 + 30), rosalie.ZoneAt(14 * 60)));
        Assert.Equal(((string?)null, "zone.auberge", (string?)null), (mathurin.ZoneAt(16 * 60 + 10), mathurin.ZoneAt(18 * 60), mathurin.ZoneAt(21 * 60 + 10)));
        Assert.Equal(((string?)null, "zone.auberge", "zone.chapelle"), (anselme.ZoneAt(16 * 60), anselme.ZoneAt(17 * 60), anselme.ZoneAt(17 * 60 + 15)));
        // L'archer reste en « ronde assise » sur la terrasse (il ne quitte pas la zone de l'histoire).
        Assert.False(book.Npc("npc.archer_garde").Roams);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Schema_9_encore_lu_sans_zone_de_creneau_et_refuse_au_chemin_precis_avec()
    {
        JsonObject nine = StoryJson();
        nine["schemaVersion"] = 9;
        V261TestData.WithoutSuites(nine);   // V2.6.1 : sans ce qui demande la version 11
        int rosalie = NpcIndex(nine, "npc.habitant");
        int messe = SlotIndex(nine, "npc.habitant", "06:30");
        Assert.Contains(Read(nine).Report.Errors, x => x.Path == $"$.data.npcs[{rosalie}].schedule[{messe}].zone" && x.Code == ValidationCode.InvalidValue);
        // Sans zones de créneau (chacun reste chez lui, ses répliques qui en parlent avec lui), la version 9 se lit.
        foreach (JsonNode? npc in nine["data"]!["npcs"]!.AsArray())
            if (npc!["schedule"] is JsonArray slots && slots.Any(s => s!["zone"] != null)) V23TestData.Move(npc, npc["position"]!.DeepClone().AsArray());
        StoryLoadResult read = Read(nine);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(9, read.Story!.SchemaVersion);
        Assert.All(read.Story.Npcs, n => Assert.False(n.Roams));
    }

    [Fact]
    public void Zone_de_creneau_refusee_au_chemin_precis()
    {
        string Slot(JsonObject r, string npc, string at) => $"$.data.npcs[{NpcIndex(r, npc)}].schedule[{SlotIndex(r, npc, at)}]";
        JsonNode SlotNode(JsonObject r, string npc, string at) => r["data"]!["npcs"]![NpcIndex(r, npc)]!["schedule"]![SlotIndex(r, npc, at)]!;
        JsonObject root = StoryJson();
        // Zone inconnue.
        Refused(Slot(root, "npc.boulanger", "18:30") + ".zone", ValidationCode.UnknownReference, r => SlotNode(r, "npc.boulanger", "18:30")["zone"] = "zone.nulle_part");
        // Porte manquante : les archives ne s'ouvrent que sur la guilde (aucune porte des Vallons vers elles).
        Refused(Slot(root, "npc.intendant_guilde", "12:00") + ".zone", ValidationCode.InvalidValue, r =>
        {
            JsonNode slot = SlotNode(r, "npc.intendant_guilde", "12:00");
            slot["zone"] = "zone.archives";
            slot["position"] = new JsonArray(0, 0);
        });
        // Porte manquante du côté de la zone du PNJ : un PNJ des archives ne peut pas sortir dans le bourg.
        Refused(Slot(root, "npc.archiviste", "15:00") + ".zone", ValidationCode.InvalidValue, r =>
        {
            JsonNode slot = SlotNode(r, "npc.archiviste", "15:00");
            slot["zone"] = "zone.vallons";
            slot["position"] = new JsonArray(-58, -6);
        });
        // La zone du PNJ redonnée (un créneau sans zone y reste), dans un intérieur comme dehors.
        Refused(Slot(root, "npc.aubergiste", "18:00") + ".zone", ValidationCode.InvalidValue, r => SlotNode(r, "npc.aubergiste", "18:00")["zone"] = "zone.auberge");
        Refused(Slot(root, "npc.odile", "12:00") + ".zone", ValidationCode.InvalidValue, r => SlotNode(r, "npc.odile", "12:00")["zone"] = "zone.vallons");
        // La pièce d'une scène (le souvenir) : personne n'y va.
        Refused(Slot(root, "npc.odile", "12:00") + ".zone", ValidationCode.InvalidValue, r =>
        {
            JsonNode slot = SlotNode(r, "npc.odile", "12:00");
            slot["zone"] = "zone.souvenir_rupture";
            slot.AsObject().Remove("rain");
            slot["position"] = new JsonArray(0, 0);
        });
        // La place et l'abri se jugent dans la zone du créneau : une place du bourg n'est pas dans la salle de l'auberge.
        Refused(Slot(root, "npc.boulanger", "18:30") + ".position", ValidationCode.OutOfRange, r => SlotNode(r, "npc.boulanger", "18:30")["position"] = new JsonArray(-65.5, 2.8));
        Refused(Slot(root, "npc.habitant", "12:00") + ".rain.position", ValidationCode.OutOfRange, r => SlotNode(r, "npc.habitant", "12:00")["rain"]!["position"] = new JsonArray(9999, 9999));
        // Dans un obstacle de la salle (une table).
        Refused(Slot(root, "npc.boulanger", "18:30") + ".position", ValidationCode.InvalidValue, r => SlotNode(r, "npc.boulanger", "18:30")["position"] = new JsonArray(-3.5, 2.0));
        // Une zone de créneau inconnue n'entraîne pas d'erreur en cascade sur sa place.
        JsonObject unknown = StoryJson();
        SlotNode(unknown, "npc.boulanger", "18:30")["zone"] = "zone.nulle_part";
        Assert.DoesNotContain(Read(unknown).Report.Errors, e => e.Path == Slot(unknown, "npc.boulanger", "18:30") + ".position");
    }

    // --- Où est chacun ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Un_PNJ_n_est_jamais_a_deux_endroits_ni_perdu()
    {
        var zones = new[] { (string?)null }.Concat(V16TestData.Interiors.Select(z => (string?)z.Id)).ToList();
        foreach (Npc npc in Shipped.Value.Npcs)
            for (int minute = 0; minute < 1440; minute++)
            {
                // Une seule zone à chaque minute, une zone connue du monde, et une des siennes.
                string? at = npc.ZoneAt(minute);
                Assert.Contains(at, zones);
                Assert.Contains(at, npc.Zones);
                // Le matin (7 h 45 à 11 h 30), chacun est chez lui, à sa place de l'histoire, comme au début du jeu.
                if (npc.Schedule.Count > 0 && minute >= 7 * 60 + 45 && minute < 11 * 60 + 30) Assert.Equal(npc.Zone, at);
                // La nuit (22 h 30 à 5 h), chacun est rentré dans sa zone.
                if (npc.Schedule.Count > 0 && (minute >= 22 * 60 + 30 || minute < 5 * 60)) Assert.Equal(npc.Zone, at);
            }
        // Dans le cœur, à quelques heures de la journée : chaque PNJ est présent dans exactement une des zones ouvertes.
        foreach (int minute in new[] { 6 * 60 + 45, 12 * 60 + 15, 16 * 60 + 10, 16 * 60 + 45, 19 * 60 + 45, 21 * 60 + 5 })
        {
            long tick = TickAt(DryDay(minute), minute);
            var sessions = new List<WorldSession> { Outside(tick, -18, -12) };
            sessions.AddRange(V16TestData.Interiors.Where(z => z.Id != "zone.souvenir_rupture").Select(z => Inside(z.Id, tick, z.Entry.Position.X.Raw / 10000.0, z.Entry.Position.Z.Raw / 10000.0)));
            // Sous-lot V2.10 : un PNJ de passage (le colporteur) n'est dans le monde que pendant ses fêtes.
            foreach (Npc npc in Shipped.Value.Npcs.Where(n => n.Zone != "zone.souvenir_rupture" && n.Festivals.Count == 0))
            {
                var here = sessions.Where(w => w.NpcHere(w.Narrative!.Book.Npc(npc.Id))).ToList();
                Assert.True(here.Count == 1, $"{npc.Id} à {minute / 60} h {minute % 60} : {here.Count} endroits");
                Assert.Equal(npc.ZoneAt(minute) ?? "zone.vallons", here[0].Definition!.Id);
            }
        }
    }

    [Fact]
    public void Le_soir_la_salle_de_l_auberge_se_remplit()
    {
        int evening = 19 * 60 + 45;
        long tick = TickAt(DryDay(evening), evening);
        WorldSession inn = Inside("zone.auberge", tick, 0, -3.5);
        string[] inside = Shipped.Value.VisitorsOf("zone.auberge").Where(inn.NpcHere).Select(n => n.Id).ToArray();
        Assert.Equal(new[] { "npc.maelle", "npc.epiciere", "npc.aubergiste", "npc.boulanger", "npc.renart", "npc.fermier" }, inside);
        // Dehors, sur la terrasse : seulement Aldric (sa « ronde assise » commence à 16 h 30… et finit à 19 h) ; à 18 h, il y est.
        WorldSession terrace = Outside(TickAt(DryDay(18 * 60), 18 * 60), -69, 1.5);
        Assert.Equal("taverne", terrace.NpcActivity(Shipped.Value.Npc("npc.archer_garde")));
        Assert.True(terrace.NpcHere(Shipped.Value.Npc("npc.archer_garde")));
        Assert.False(terrace.NpcHere(Shipped.Value.Npc("npc.aubergiste")));   // Mathurin sert dans sa salle
    }

    [Fact]
    public void On_parle_a_un_PNJ_la_ou_il_est_et_seulement_la()
    {
        int evening = 19 * 60 + 10;
        long day = DryDay(evening);
        // Firmin est dans la salle (à la table du fond) : de la terrasse, à sa place d'avant, il n'est pas là.
        WorldSession terrace = Outside(TickAt(day, evening), -65.5, 1.6);
        Assert.Equal(DialogueRefusal.TooFar, terrace.Talk("npc.boulanger", out _));
        WorldSession inn = Inside("zone.auberge", TickAt(day, evening), -1.9, 0.8);
        Assert.Equal(P(-1.9, 2.0), inn.NpcPosition(Shipped.Value.Npc("npc.boulanger")));
        Assert.Equal(DialogueRefusal.None, inn.Talk("npc.boulanger", out _));
        Assert.Equal("taverne", inn.NpcActivity(Shipped.Value.Npc("npc.boulanger")));
        // Mathurin sort fumer sur la terrasse à 16 h : on lui parle dehors, plus derrière son comptoir.
        int pipe = 16 * 60 + 10;
        long pipeDay = DryDay(pipe);
        WorldSession counter = Inside("zone.auberge", TickAt(pipeDay, pipe), 3.75, 1.5);
        Assert.Equal(DialogueRefusal.TooFar, counter.Talk("npc.aubergiste", out _));
        WorldSession outside = Outside(TickAt(pipeDay, pipe), -67.6, 2.8);
        Assert.Equal("pipe", outside.NpcActivity(Shipped.Value.Npc("npc.aubergiste")));
        Assert.Equal(DialogueRefusal.None, outside.Talk("npc.aubergiste", out _));
        outside.Narrative!.Close();
        Assert.Equal(DialogueRefusal.None, outside.Talk("npc.aubergiste", out DialogueView? smoke));
        Assert.Equal("text.v231.mathurin.pipe", smoke!.Text);
    }

    [Fact]
    public void La_boutique_suit_son_marchand_dans_la_salle()
    {
        int noon = 12 * 60 + 15;
        long day = DryDay(noon);
        WorldSession inn = Inside("zone.auberge", TickAt(day, noon), 3.5, 0.8);
        Assert.Equal("taverne", inn.NpcActivity(Shipped.Value.Npc("npc.armurier")));
        Assert.Equal(TradeRefusal.None, inn.CanTrade("merchant.armurier"));
        // Sur la terrasse (sa place de taverne d'avant V2.3.1), il n'y est pas.
        Assert.Equal(TradeRefusal.TooFar, Outside(TickAt(day, noon), -64.5, 1.4).CanTrade("merchant.armurier"));
        // Aubin mange à l'auberge : sa réserve (la boutique de la guilde) le suit.
        Assert.Equal("zone.auberge", inn.NpcZone(Shipped.Value.Npc("npc.intendant_guilde")));
        Assert.True(inn.NpcHere(Shipped.Value.Npc("npc.intendant_guilde")));
        WorldSession guild = Inside("zone.guilde", TickAt(day, noon), -4.5, 1.5);
        Assert.False(guild.NpcHere(Shipped.Value.Npc("npc.intendant_guilde")));
        Assert.NotEqual(TradeRefusal.None, guild.CanTrade("merchant.guilde"));
    }

    [Fact]
    public void Devant_un_etal_ferme_la_boutique_le_dit_meme_si_le_marchand_est_a_l_auberge()
    {
        // Relecture indépendante de la PR #109 : l'étal se juge dans la zone où vit le PNJ, le marchand dans celle de son créneau.
        JsonObject root = MerchantsJson();
        foreach (JsonNode? m in root["data"]!["merchants"]!.AsArray())
            if ((string?)m!["id"] == "merchant.epicier") m["closed"] = new JsonArray("dusk", "night");
        MerchantsLoadResult read = ReadMerchants(root);
        Assert.True(read.IsValid, read.Report.ToString());
        int evening = 20 * 60;
        long day = DryDay(evening);
        WorldSession stall = Outside(TickAt(day, evening), -50.8, 7.2, merchants: read.Book);
        Assert.False(stall.NpcHere(Shipped.Value.Npc("npc.epiciere")));   // Bertille boit à l'auberge
        Assert.Equal(TradeRefusal.Closed, stall.CanTrade("merchant.epicier"));
        // Ailleurs dans le bourg : trop loin, comme avant.
        Assert.Equal(TradeRefusal.TooFar, Outside(TickAt(day, evening), -18, -12, merchants: read.Book).CanTrade("merchant.epicier"));
        // Boutique livrée (fermée la nuit seulement), même heure : trop loin de Bertille (la vraie raison est donnée par l'étal, AwayStallInReach).
        Assert.Equal(TradeRefusal.TooFar, Outside(TickAt(day, evening), -50.8, 7.2).CanTrade("merchant.epicier"));
    }

    [Fact]
    public void Le_dernier_creneau_passe_minuit_chez_le_PNJ()
    {
        // Le dernier créneau vaut jusqu'au premier du lendemain : un PNJ ne dort pas ailleurs (refus au chemin précis).
        JsonObject root = StoryJson();
        int mathurin = NpcIndex(root, "npc.aubergiste");
        int last = root["data"]!["npcs"]![mathurin]!["schedule"]!.AsArray().Count - 1;
        Refused($"$.data.npcs[{mathurin}].schedule[{last}].zone", ValidationCode.InvalidValue, r =>
        {
            JsonNode slot = r["data"]!["npcs"]![mathurin]!["schedule"]![last]!;
            slot["zone"] = "zone.vallons";
            slot["position"] = new JsonArray(-67.6, 4.3);
        });
        // Livré : de 23 h 59 à minuit, chacun reste dans sa zone, et la partie qui passe minuit ne déplace personne d'une zone à l'autre.
        foreach (Npc npc in Shipped.Value.Npcs) Assert.Equal((npc.Zone, npc.Zone), (npc.ZoneAt(1439), npc.ZoneAt(0)));
        long day = DryDay(23 * 60 + 59);
        WorldSession w = Outside(TickAt(day, 23 * 60 + 59), -18, -12);
        var before = Shipped.Value.Npcs.Select(n => (n.Id, w.NpcZone(n), w.NpcHere(n))).ToList();
        for (int i = 0; i < 4000 && w.Zone.Time.Minute != 1; i++) w.Step();
        Assert.Equal((day + 1, 1), (w.Zone.Time.Day, w.Zone.Time.Minute));
        Assert.Equal(before, Shipped.Value.Npcs.Select(n => (n.Id, w.NpcZone(n), w.NpcHere(n))).ToList());
    }

    [Fact]
    public void Pendant_une_attaque_un_PNJ_dans_une_autre_zone_ne_court_pas_au_refuge()
    {
        // Relecture indépendante de la PR #109 (comportement figé) : la cloche n'envoie au refuge que les villageois qui sont dans la zone
        // de l'attaque ; Firmin, ajouté aux villageois, reste dans la salle de l'auberge (son créneau) et ne rejoint le refuge qu'en sortant.
        JsonObject zoneRoot = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        JsonObject raid = zoneRoot["data"]!["events"]!.AsArray().Select(e => e!.AsObject()).Single(e => (string?)e["id"] == "attaque_hameau");
        raid["minLevel"] = 1;
        raid["villagers"]!.AsArray().Add("npc.boulanger");
        ZoneLoadResult loaded = ZoneJsonReader.Read(zoneRoot.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition zone = loaded.Zone!;
        WorldEventDefinition e = zone.EventOf("attaque_hameau")!;
        // Une attaque qui commence pendant que Firmin est à l'auberge (18 h 30 à 21 h 30) et dure au-delà de 21 h 30.
        EventOccurrence? o = null;
        for (long from = 1440; o is null; )
        {
            EventOccurrence next = WorldEventSchedule.Next(zone.Id, zone.Id, e, from)!;
            long minute = next.StartAt % WorldClock.MinutesPerDay;
            if (minute >= 18 * 60 + 30 && minute < 21 * 60 && next.EndAt - (next.StartAt - minute) > 21 * 60 + 35) o = next;
            from = next.StartAt + 1;
            Assert.True(from < 1440 * 400, "aucune attaque le soir");
        }
        var clock = new WorldClock(50);
        StoryBook book = Book(zone);
        var w = new WorldSession(new WorldSave("zone.vallons", WorldEventSchedule.TickAt(clock, o.StartAt), 0, new[] { PlayerCharacters.PlacesKey }, Hero(1000)),
            zone, ShippedData, Progression, ItemsCatalog, book, safePoint: "place_du_village");
        for (int i = 0; i < 20; i++) w.Step();
        Npc firmin = book.Npc("npc.boulanger"), doyen = book.Npc("npc.doyen");
        Assert.Equal(WorldSession.RefugeActivity, w.NpcActivity(doyen));
        Assert.Equal(("zone.auberge", "taverne", P(-1.9, 2.0)), (w.NpcZone(firmin), w.NpcActivity(firmin), w.NpcPosition(firmin)));
        Assert.False(w.NpcHere(firmin));
        // À 21 h 30, il sort de l'auberge : l'attaque dure encore, il court au refuge.
        for (int i = 0; i < 200_000 && w.Zone.Time.Minute != 21 * 60 + 31; i++) w.Step();
        Assert.Equal(21 * 60 + 31, w.Zone.Time.Minute);
        Assert.Equal(((string?)null, WorldSession.RefugeActivity), (w.NpcZone(firmin), w.NpcActivity(firmin)));
        Assert.Equal(w.NpcPosition(doyen), w.NpcPosition(firmin));
    }

    [Fact]
    public void Le_PNJ_finit_la_conversation_avant_de_partir()
    {
        // Firmin fait la sieste devant chez lui jusqu'à 18 h 30, puis va à l'auberge. On lui parle à 18 h 29 : tant que le dialogue est
        // ouvert, il reste là (même zone, même place, même activité) ; il part dès qu'il se ferme.
        int before = 18 * 60 + 29;
        long day = DryDay(before);
        WorldSession w = Outside(TickAt(day, before), -38.5, 3.6);
        Npc firmin = Shipped.Value.Npc("npc.boulanger");
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.boulanger", out _));
        for (int i = 0; i < 4000 && w.Zone.Time.Minute < 18 * 60 + 31; i++) w.Step();
        Assert.Equal(18 * 60 + 31, w.Zone.Time.Minute);
        Assert.Equal(((string?)null, "sieste", P(-38.5, 4.8)), (w.NpcZone(firmin), w.NpcActivity(firmin), w.NpcPosition(firmin)));
        Assert.True(w.NpcHere(firmin));
        Assert.NotNull(w.Narrative!.Open);
        // La réponse se choisit encore (il n'est pas « trop loin »).
        Assert.NotEqual(DialogueRefusal.TooFar, w.Choose(0, out _, out _));
        w.Narrative.Close();
        Assert.Equal(("zone.auberge", "taverne"), (w.NpcZone(firmin), w.NpcActivity(firmin)));
        Assert.False(w.NpcHere(firmin));
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.boulanger", out _));
        // Un nouveau dialogue (ou un dialogue ouvert par un autre chemin que WorldSession.Talk) ne réutilise pas ce qu'il gardait.
        Assert.Null(w.Narrative.OpenNpc);
    }

    [Fact]
    public void Deja_rencontre_est_par_PNJ_pas_par_zone()
    {
        // Mathurin rencontré sur la terrasse, sa pipe à la main : le soir, dans sa salle, il ne se présente plus (ses retrouvailles).
        int pipe = 16 * 60 + 10;
        WorldSession terrace = Outside(TickAt(DryDay(pipe), pipe), -67.6, 2.8);
        Assert.Equal(DialogueRefusal.None, terrace.Talk("npc.aubergiste", out DialogueView? first));
        Assert.Equal("text.aubergiste.accueil", first!.Text);   // sa présentation
        terrace.Narrative!.Close();
        SavedCharacter hero = terrace.Save().Character!;
        int evening = 18 * 60 + 30;
        long tick = TickAt(DryDay(evening), evening);
        WorldSession inn = Inside("zone.auberge", tick, 0, 1.4, hero);
        Assert.Equal(DialogueRefusal.None, inn.Talk("npc.aubergiste", out DialogueView? there));
        Assert.Equal("text.v1910.aubergiste.retrouvailles", there!.Text);
        // Jamais rencontré : dans sa salle aussi, il se présente.
        WorldSession stranger = Inside("zone.auberge", tick, 0, 1.4);
        Assert.Equal(DialogueRefusal.None, stranger.Talk("npc.aubergiste", out DialogueView? fresh));
        Assert.Equal("text.aubergiste.accueil", fresh!.Text);
    }

    [Fact]
    public void Meme_heure_meme_lieu_et_sauvegarde_inchangee()
    {
        int minute = 16 * 60 + 45;
        long tick = TickAt(DryDay(minute), minute);
        WorldSession a = Outside(tick, -18, -12), b = Outside(tick, -18, -12);
        foreach (Npc npc in Shipped.Value.Npcs)
            Assert.Equal((a.NpcZone(npc), a.NpcPosition(npc), a.NpcActivity(npc)), (b.NpcZone(npc), b.NpcPosition(npc), b.NpcActivity(npc)));
        string text = SaveJsonReader.Write(a.Save());
        Assert.Equal(17, SaveValidator.CurrentSchemaVersion);   // V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; rien n'est sauvegardé : la zone de chacun se recalcule de l'heure
        Assert.DoesNotContain("zone.auberge", text, StringComparison.Ordinal);
        var back = new WorldSession(SaveJsonReader.Read(text).Save!, Vallons, ShippedData, Progression, ItemsCatalog, Shipped.Value, market: new Market(ShippedMerchants));
        back.Step();
        foreach (Npc npc in Shipped.Value.Npcs) Assert.Equal(a.NpcZone(npc), back.NpcZone(npc));
    }

    // --- Affichage : les portes, la carte des pièces, le menu d'essai --------------------------------------------------------

    [Fact]
    public void Dans_une_piece_le_PNJ_parti_attend_a_la_porte_et_reapparait_en_rentrant()
    {
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        // Rosalie, chez elle à 11 h 59 ; à midi, elle sort au puits.
        SaveStore.Write(save, new WorldSave("zone.maison", TickAt(DryDay(11 * 60 + 59), 11 * 60 + 59), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save);
        Assert.True(a.Indoors);
        WorldMarker Rosalie() => a.NpcStage().Single(n => n.Id == "npc.habitant");
        Assert.True(Rosalie().Present);
        Assert.Contains(a.Npcs(), n => n.Id == "npc.habitant");
        Assert.Contains(a.Map.Speakers, s => s.Id == "npc.habitant");   // son corps est dans la pièce
        for (int i = 0; i < 4000 && a.World.Zone.Time.Minute != 12 * 60 + 1; i++) a.Advance(0.05);
        // Partie : elle n'est plus là pour le cœur (on ne lui parle plus ici) ; l'affichage la mène à la porte et la cache.
        Assert.False(Rosalie().Present);
        Assert.Equal((0.0, -2.3), (Rosalie().X, Rosalie().Z));
        Assert.DoesNotContain(a.Npcs(), n => n.Id == "npc.habitant");
        Assert.Null(a.NpcInReach is Npc n0 && n0.Id == "npc.habitant" ? n0 : null);
        // Dans la salle de l'auberge à 11 h : les visiteurs du soir attendent, cachés, à la porte ; Mathurin est là.
        SaveStore.Write(save, new WorldSave("zone.auberge", TickAt(1, 11 * 60), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession inn = Adventure(save);
        Assert.Equal(new[] { "npc.aubergiste" }, inn.NpcStage().Where(n => n.Present).Select(n => n.Id));
        Assert.All(inn.NpcStage().Where(n => !n.Present), n => Assert.Equal((0.0, -4.3), (n.X, n.Z)));
        Assert.Equal(inn.NpcStage().Select(n => n.Id), inn.Map.Speakers.Select(s => s.Id));   // un corps pour chacun
        // Dehors, les visiteurs de l'intérieur attendent à sa porte (celle de l'auberge pour Mathurin).
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(1, 11 * 60), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession town = Adventure(save);
        WorldMarker m = town.NpcStage().Single(n => n.Id == "npc.aubergiste");
        Assert.Equal((false, -69.0, 4.0), (m.Present, m.X, m.Z));
        Assert.Equal((-73.0, -7.0), (town.NpcStage().Single(n => n.Id == "npc.pretre").X, town.NpcStage().Single(n => n.Id == "npc.pretre").Z));
    }

    [Fact]
    public void Le_corps_d_un_PNJ_qui_parle_n_obeit_qu_au_coeur()
    {
        // Revue Codex de la PR #109 : dans l'auberge sous une averse, Renart et le fermier qui entrent à 19 h restent visibles ; la nuit non
        // plus ne les cache pas (dans une pièce, la nuit du décor ne les remontrerait pas). Seule leur sortie par la porte les cache.
        foreach (bool night in new[] { false, true })
            foreach (bool awake in new[] { false, true })
                foreach (bool rain in new[] { false, true })
                    foreach (bool staysOut in new[] { false, true })
                        foreach (bool away in new[] { false, true })
                        {
                            Assert.True(VillageDay.Shown(true, false, night, awake, rain, staysOut, away));
                            Assert.False(VillageDay.Shown(true, true, night, awake, rain, staysOut, away));
                            // Les passants du décor gardent leurs règles (V2.1 à V2.3).
                            Assert.Equal((!night || awake) && (!rain || staysOut) && !away, VillageDay.Shown(false, false, night, awake, rain, staysOut, away));
                            Assert.False(VillageDay.Shown(false, true, night, awake, rain, staysOut, away));
                        }
        string root = Lot93UnityPackageTests.Root;
        string night2 = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", "SpaceView.Night.cs"));
        Assert.Contains("VillageDay.Shown(v.Speaker, v.Gone, _worldNight", night2, StringComparison.Ordinal);
        string space = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("VillageDay.Plan(v, speaker ? null : _errands)", space, StringComparison.Ordinal);   // pas de course pour un PNJ
        Assert.Contains("Speaker = speaker", space, StringComparison.Ordinal);
        // Les repères des PNJ (dehors) ne sont cachés que par leur départ (WorldMarkers), jamais par la nuit ni la pluie.
        string markers = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", "WorldMarkers.cs"));
        Assert.Contains("m.Body.SetActive(m.Shown);", markers, StringComparison.Ordinal);
        Assert.DoesNotContain("StaysOutInRain", markers, StringComparison.Ordinal);
        Assert.DoesNotContain("AwakeAtNight", markers, StringComparison.Ordinal);
    }

    [Fact]
    public void Affichage_et_menu_d_essai_des_PNJ_qui_changent_de_lieu()
    {
        string root = Lot93UnityPackageTests.Root;
        string markers = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", "WorldMarkers.cs"));
        Assert.Contains("adventure.NpcStage()", markers, StringComparison.Ordinal);
        Assert.Contains("arena.ShowSpeaker(", markers, StringComparison.Ordinal);
        Assert.Contains("arena.MoveSpeaker(m.Data.Id, here.x, here.z, deltaTime);", markers, StringComparison.Ordinal);
        string space = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("public bool ShowSpeaker(string id, bool shown)", space, StringComparison.Ordinal);
        string menu = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Editor", "EdsRoamingTrial.cs"));
        Assert.True(menu.Split("[MenuItem(\"Éclats du Seuil/Essai des PNJ qui changent de lieu").Length - 1 >= 5);
        Assert.Contains("DayTrial.MoveSave(", menu, StringComparison.Ordinal);
        Assert.Contains("isPlayingOrWillChangePlaymode", menu, StringComparison.Ordinal);
        Assert.Contains("Mathurin", RoamingTrial.Guide(16 * 60 + 5), StringComparison.Ordinal);
        Assert.Contains("Rosalie", RoamingTrial.Guide(6 * 60 + 40), StringComparison.Ordinal);
        Assert.Contains("salle", RoamingTrial.Guide(19 * 60 + 40), StringComparison.Ordinal);
        string essai = File.ReadAllText(Path.Combine(root, "docs", "ESSAI_V1.10.md"));
        Assert.Contains("## 13.", essai, StringComparison.Ordinal);
        Assert.Contains("PNJ qui changent de lieu", essai, StringComparison.Ordinal);
    }
}

/// <summary>Sous-lot V2.3.1 : parties dans un intérieur (la salle de l'auberge, la maison de Rosalie…), pour les tests des PNJ qui y passent.</summary>
internal static class V231TestData
{
    /// <summary>Un intérieur livré, point sûr d'entrée déplacé en (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static ZoneDefinition Interior(string id, double x, double z)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    /// <summary>Partie dans l'intérieur <paramref name="id"/> (point d'entrée en <paramref name="x"/>, <paramref name="z"/>), à ce tick, premier tick joué.</summary>
    public static WorldSession Inside(string id, long tick, double x, double z, StoryBook story, SavedCharacter? hero = null)
    {
        var w = new WorldSession(new WorldSave(id, tick, 0, new[] { PlayerCharacters.PlacesKey }, hero ?? Hero(1000)), Interior(id, x, z), ShippedData, Progression,
            ItemsCatalog, story, storyZone: "zone.vallons", market: new Market(ShippedMerchants));
        w.Step();
        return w;
    }
}
