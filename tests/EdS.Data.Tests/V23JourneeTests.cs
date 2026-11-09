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
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.3 (villageois qui suivent leur journée) : histoire en schéma 9 (emplois du temps des PNJ, condition <c>activity</c>, répliques
/// <c>lines</c> d'un nœud selon le moment), schéma 8 encore lu ; refus au chemin précis ; places de chacun selon l'heure et la pluie, bornes,
/// matin comme au début du jeu, nuit devant sa porte (V2.1), déterminisme et sauvegarde inchangée ; répliques selon le lieu ; boutique d'un
/// marchand parti ; allers-retours des villageois du décor ; menu d'essai.
/// </summary>
[Trait("Category", "World")]
public sealed class V23JourneeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v23_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly Lazy<StoryBook> Shipped = new(() => Book(Vallons));

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    private static WorldSession At(long tick, ZoneDefinition? zone = null)
    {
        ZoneDefinition z = zone ?? Vallons;
        var w = new WorldSession(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, Hero(1000)), z, ShippedData, Progression,
            ItemsCatalog, Book(z), market: new Market(ShippedMerchants));
        w.Step();
        return w;
    }

    /// <summary>Premier jour (à partir du jour 1) où il ne pleut pas fort à cette minute (les PNJ ne s'abritent pas).</summary>
    private static long DryDay(int minute)
    {
        for (long day = 1; ; day++)
            if (!WorldSession.Shelters(WorldWeather.At("zone.vallons", day, minute).Kind)) return day;
    }

    private static GroundVector P(double x, double z) => new GroundVector(EdS.Domain.Numerics.Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        EdS.Domain.Numerics.Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    // --- Format : schéma 9 ------------------------------------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Vallons, ItemsCatalog, V16TestData.Interiors);

    private static int NpcIndex(JsonObject root, string npc) =>
        root["data"]!["npcs"]!.AsArray().Select((n, i) => ((string)n!["id"]!, i)).Single(x => x.Item1 == npc).i;

    private static (int Dialogue, int Node) NodeIndex(JsonObject root, string dialogue, string node)
    {
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.Select((x, i) => ((string)x!["id"]!, i)).Single(x => x.Item1 == dialogue).i;
        int n = dialogues[d]!["nodes"]!.AsArray().Select((x, i) => ((string)x!["id"]!, i)).Single(x => x.Item1 == node).i;
        return (d, n);
    }

    private static void Refused(string path, ValidationCode code, Action<JsonObject> mutate)
    {
        JsonObject root = StoryJson();
        mutate(root);
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        // Le lecteur JSON ajoute la ligne au chemin d'une liste trop longue (« … (ligne 1) »).
        Assert.Contains(r.Report.Errors, x => (x.Path == path || x.Path.StartsWith(path + " (", StringComparison.Ordinal)) && x.Code == code);
    }

    [Fact]
    public void Histoire_livree_en_schema_9_avec_les_emplois_du_temps()
    {
        StoryBook book = Shipped.Value;
        Assert.Equal((13, 13, "0.25.0-provisoire"), (book.SchemaVersion, StoryValidator.CurrentSchemaVersion, book.ContentVersion));
        string[] scheduled =
        {
            "npc.boulanger", "npc.odile", "npc.maelle", "npc.apprentie", "npc.archer_garde", "npc.capitaine", "npc.gaspard", "npc.armurier",
            "npc.epiciere", "npc.herboriste", "npc.renart", "npc.lison", "npc.veuve", "npc.fermier", "npc.doyen", "npc.herboriste_hameau", "npc.fermiere",
            // Réponse de Damien (question 6) : on en rajoute, dehors et dans les intérieurs (chacun dans sa zone).
            "npc.ysarn", "npc.deserteur", "npc.thibaut", "npc.aubergiste", "npc.pretre", "npc.albe", "npc.habitant", "npc.maitresse_guilde",
            "npc.intendant_guilde", "npc.archiviste",
            // V2.14 (réponse de Damien du 9 novembre 2026) : les employés des boutiques, à leurs horaires.
            "npc.commis", "npc.commis_epicerie", "npc.commis_herboristerie", "npc.commis_mercerie",
        };
        Assert.Equal(scheduled.OrderBy(x => x, StringComparer.Ordinal), book.Npcs.Where(n => n.Schedule.Count > 0).Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal));
        // Chaque activité a son texte (sous le nom du PNJ, et pour la boutique).
        foreach (Npc npc in book.Npcs)
            foreach (string activity in npc.Activities)
                Assert.True(Texts.Has(StoryValidator.ActivityTextPrefix + activity), activity);
        // Des répliques du moment chez chacun d'eux (et chez Mathurin et Frère Anselme, qui parlent des autres).
        var lines = book.Dialogues.SelectMany(d => d.Nodes.SelectMany(n => n.Lines)).ToList();
        Assert.InRange(lines.Count, 60, 180);   // V2.10 : 160 (répliques de fête) ; V2.7 : 145 (répliques selon la réputation)   // V2.3.1 : 110 (répliques des PNJ qui changent de lieu)
        // V1.9.10 : chaque réplique exige « déjà rencontré » ; hors les retrouvailles, elle porte une activité.
        Assert.All(lines, l => Assert.Contains(l.Conditions, c => c.Kind == ConditionKind.Fact));
        Assert.All(lines, l => Assert.True((l.Text.StartsWith("text.v1910.", StringComparison.Ordinal) || l.Text.StartsWith("text.v1911.", StringComparison.Ordinal)) || l.Conditions.Any(c => c.Kind == ConditionKind.Activity)
            // V2.7 : ou un palier de réputation, ou les retrouvailles d'un PNJ nouveau (Gaut le receleur).
            || l.Text == "text.v27.receleur.retrouvailles"
            // V2.10 : ou une fête (répliques de fête), ou les retrouvailles du colporteur.
            || l.Text == "text.v210.colporteur.retrouvailles" || l.Conditions.Any(c => c.Kind == ConditionKind.Festival)
            // V2.14 : ou les retrouvailles des PNJ des boutiques et du marché, ou un moment de la journée (Colin, le soir : tout est vendu).
            || (l.Text.StartsWith("text.v214.", StringComparison.Ordinal) && (l.Text.EndsWith(".retrouvailles", StringComparison.Ordinal) || l.Conditions.Any(c => c.Kind == ConditionKind.Phase)))
            || (l.Text.StartsWith("text.v27.", StringComparison.Ordinal) && l.Conditions.Any(c => c.Kind is ConditionKind.Standing or ConditionKind.StandingAtLeast or ConditionKind.ReputationBelow))));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Schema_8_encore_lu_sans_emploi_du_temps_et_refuse_au_chemin_precis_avec()
    {
        JsonObject eight = StoryJson();
        eight["schemaVersion"] = 8;
        V261TestData.WithoutSuites(eight);   // V2.6.1 : sans ce qui demande la version 11
        StoryLoadResult refused = Read(eight);
        int firmin = NpcIndex(eight, "npc.boulanger");
        (int d, int n) = NodeIndex(eight, "dialogue.boulanger", "apres");
        Assert.Contains(refused.Report.Errors, x => x.Path == $"$.data.npcs[{firmin}].schedule" && x.Code == ValidationCode.InvalidValue);
        Assert.Contains(refused.Report.Errors, x => x.Path == $"$.data.dialogues[{d}].nodes[{n}].lines" && x.Code == ValidationCode.InvalidValue);
        // La condition activity, hors d'une réplique : refusée aussi en schéma 8.
        JsonObject entry = StoryJson();
        V23TestData.WithoutSchedules(entry);
        entry["schemaVersion"] = 8;
        V261TestData.WithoutSuites(entry);   // V2.6.1 : sans ce qui demande la version 11
        entry["data"]!["dialogues"]![d]!["entries"]![0]!["conditions"]!.AsArray().Add(new JsonObject { ["if"] = "activity", ["id"] = "npc.boulanger", ["step"] = "fournil" });
        Assert.Contains(Read(entry).Report.Errors, x => x.Path == $"$.data.dialogues[{d}].entries[0].conditions[1].if" && x.Code == ValidationCode.InvalidValue);
        V23TestData.WithoutSchedules(eight);
        StoryLoadResult read = Read(eight);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(8, read.Story!.SchemaVersion);
        Assert.All(read.Story.Npcs, x => Assert.Empty(x.Schedule));
        // Une partie sur l'histoire en schéma 8 : les PNJ restent à leur place (devant leur porte la nuit), comme en V2.1.
        Npc firminBook = read.Story.Npc("npc.boulanger");
        Assert.Null(firminBook.SlotAt(12 * 60));
    }

    [Fact]
    public void Emploi_du_temps_refuse_au_chemin_precis()
    {
        JsonObject probe = StoryJson();
        int f = NpcIndex(probe, "npc.boulanger");
        string firmin = $"$.data.npcs[{f}].schedule";
        JsonObject S(JsonObject root, int k) => root["data"]!["npcs"]![f]!["schedule"]![k]!.AsObject();
        Refused(firmin + "[1].at", ValidationCode.InvalidValue, r => S(r, 1)["at"] = "25:00");
        Refused(firmin + "[1].at", ValidationCode.InvalidValue, r => S(r, 1)["at"] = "11h30");
        Refused(firmin + "[1].at", ValidationCode.InvalidValue, r => S(r, 1)["at"] = "11:60");
        Refused(firmin + "[1].at", ValidationCode.InvalidValue, r => S(r, 1)["at"] = "02:00");   // pas après le créneau précédent
        Refused(firmin + "[2].at", ValidationCode.InvalidValue, r => S(r, 2)["at"] = "11:00");
        Refused(firmin + "[0].at", ValidationCode.MissingField, r => S(r, 0).Remove("at"));
        Refused(firmin + "[1].activity", ValidationCode.InvalidValue, r => S(r, 1)["activity"] = "Marché");
        Refused(firmin + "[1].activity", ValidationCode.UnknownReference, r => S(r, 1)["activity"] = "danse");   // pas de texte « text.activity.danse »
        Refused(firmin + "[1].position", ValidationCode.OutOfRange, r => S(r, 1)["position"] = new JsonArray(-500, 0));
        Refused(firmin + "[1].position", ValidationCode.InvalidValue, r => S(r, 1)["position"] = new JsonArray(-42, 10));   // dans la maison du fournil
        Refused(firmin + "[1].rain.position", ValidationCode.InvalidValue, r => S(r, 1)["rain"]!["position"] = new JsonArray(-69, 9));   // dans l'auberge
        Refused(firmin + "[1].rain.activity", ValidationCode.MissingField, r => S(r, 1)["rain"]!.AsObject().Remove("activity"));
        Refused(firmin, ValidationCode.OutOfRange, r => r["data"]!["npcs"]![f]!["schedule"] = new JsonArray());
        Refused(firmin, ValidationCode.LimitExceeded, r =>
        {
            var many = new JsonArray();
            for (int i = 0; i < StoryValidator.MaxScheduleSlots + 1; i++)
                many.Add(new JsonObject { ["at"] = $"{i:00}:00", ["activity"] = "fournil", ["position"] = new JsonArray(-42, 3.5) });
            r["data"]!["npcs"]![f]!["schedule"] = many;
        });
        // Un PNJ d'un intérieur : sa place est dans sa pièce.
        int mathurin = NpcIndex(probe, "npc.aubergiste");
        Refused($"$.data.npcs[{mathurin}].schedule[0].position", ValidationCode.OutOfRange, r => r["data"]!["npcs"]![mathurin]!["schedule"] =
            new JsonArray(new JsonObject { ["at"] = "08:00", ["activity"] = "taverne", ["position"] = new JsonArray(-65, 2.8) }));
    }

    [Fact]
    public void Repliques_et_condition_activity_refusees_au_chemin_precis()
    {
        JsonObject probe = StoryJson();
        (int d, int n) = NodeIndex(probe, "dialogue.boulanger", "apres");
        string lines = $"$.data.dialogues[{d}].nodes[{n}].lines";
        JsonObject L(JsonObject root, int k) => root["data"]!["dialogues"]![d]!["nodes"]![n]!["lines"]![k]!.AsObject();
        JsonObject C(JsonObject root) => L(root, 0)["conditions"]![0]!.AsObject();
        Refused(lines + "[0].conditions", ValidationCode.InvalidValue, r => L(r, 0)["conditions"] = new JsonArray());
        Refused(lines + "[0].text", ValidationCode.UnknownReference, r => L(r, 0)["text"] = "text.v23.inconnu");
        Refused(lines + "[0].conditions[0].id", ValidationCode.UnknownReference, r => C(r)["id"] = "npc.personne");
        Refused(lines + "[0].conditions[0].id", ValidationCode.InvalidValue, r => C(r)["id"] = "npc.ysolde");   // pas d'emploi du temps
        Refused(lines + "[0].conditions[0].step", ValidationCode.UnknownReference, r => C(r)["step"] = "tir");   // une activité d'Aldric
        Refused(lines + "[0].conditions[0].step", ValidationCode.MissingField, r => C(r).Remove("step"));
        Refused(lines + "[0].conditions[0].value", ValidationCode.InvalidValue, r => C(r)["value"] = 1);
        Refused(lines, ValidationCode.LimitExceeded, r =>
        {
            var many = new JsonArray();
            for (int i = 0; i < StoryValidator.MaxLines + 1; i++) many.Add(L(r, 0).DeepClone());
            r["data"]!["dialogues"]![d]!["nodes"]![n]!["lines"] = many;
        });
        // Une condition activity vaut aussi dans une entrée de dialogue (une autre étape que questAt).
        JsonObject ok = StoryJson();
        ok["data"]!["dialogues"]![d]!["entries"]![0]!["conditions"]!.AsArray().Add(new JsonObject { ["if"] = "activity", ["id"] = "npc.boulanger", ["step"] = "abri" });
        Assert.True(Read(ok).IsValid, Read(ok).Report.ToString());
    }

    /// <summary>Relecture indépendante de la PR #103 : une histoire minimale, lisible dans toutes les versions de 4 à 9.</summary>
    private static JsonObject Minimal(int version) => new JsonObject
    {
        ["schemaVersion"] = version, ["kind"] = "story", ["contentVersion"] = "0.1.0",
        ["data"] = new JsonObject
        {
            // Odile d'abord ; puis ceux qui ont une place de nuit dans la zone (la zone les attend dans l'histoire).
            ["npcs"] = new JsonArray(new[] { "npc.odile" }.Concat(Vallons.NightSpots.Select(n => n.Npc).Where(n => n != "npc.odile")).Select(id => (JsonNode)new JsonObject
            {
                ["id"] = id, ["name"] = "text.npc.odile.name", ["dialogue"] = "dialogue.odile",
                ["position"] = new JsonArray(Shipped.Value.Npc(id).Position.X.Raw / 10000.0, Shipped.Value.Npc(id).Position.Z.Raw / 10000.0),
            }).ToArray()),
            ["dialogues"] = new JsonArray(new JsonObject
            {
                ["id"] = "dialogue.odile",
                ["entries"] = new JsonArray(new JsonObject { ["conditions"] = new JsonArray(), ["node"] = "accueil" }),
                ["nodes"] = new JsonArray(new JsonObject { ["id"] = "accueil", ["text"] = "text.odile.accueil", ["choices"] = new JsonArray() }),
            }),
            ["quests"] = new JsonArray(), ["discoveries"] = new JsonArray(),
        },
    };

    private static JsonArray Schedule(params string[] hours) =>
        new JsonArray(hours.Select((h, i) => (JsonNode)new JsonObject { ["at"] = h, ["activity"] = i % 2 == 0 ? "moulin" : "riviere", ["position"] = new JsonArray(-8, 3 - i) }).ToArray());

    private static JsonObject ActivityTerm() => new JsonObject { ["if"] = "activity", ["id"] = "npc.odile", ["step"] = "moulin" };

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Chaque_version_lit_une_histoire_intacte_et_seule_la_9_connait_la_journee(int version)
    {
        StoryLoadResult plain = Read(Minimal(version));
        Assert.True(plain.IsValid, plain.Report.ToString());
        Assert.Equal(version, plain.Story!.SchemaVersion);
        bool known = version >= 9;
        void Check(Action<JsonObject> mutate, string path)
        {
            JsonObject root = Minimal(version);
            root["data"]!["npcs"]![0]!["schedule"] = Schedule("05:00", "12:00");   // la condition activity en a besoin
            if (!known && path != "$.data.npcs[0].schedule") root["data"]!["npcs"]![0]!.AsObject().Remove("schedule");
            mutate(root);
            StoryLoadResult r = Read(root);
            if (known) Assert.True(r.IsValid, r.Report.ToString());
            else Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == ValidationCode.InvalidValue);
        }
        Check(_ => { }, "$.data.npcs[0].schedule");
        Check(r => r["data"]!["dialogues"]![0]!["nodes"]![0]!["lines"] = new JsonArray(new JsonObject
            { ["conditions"] = new JsonArray(new JsonObject { ["if"] = "phase", ["id"] = "dusk" }), ["text"] = "text.odile.accueil" }),
            "$.data.dialogues[0].nodes[0].lines");
        Check(r => r["data"]!["dialogues"]![0]!["entries"]!.AsArray().Insert(0, new JsonObject { ["conditions"] = new JsonArray(ActivityTerm()), ["node"] = "accueil" }),
            "$.data.dialogues[0].entries[0].conditions[0].if");
    }

    [Fact]
    public void Creneaux_limites_un_seul_minuit_et_23_h_59()
    {
        Npc Odile(params string[] hours)
        {
            JsonObject root = Minimal(9);
            root["data"]!["npcs"]![0]!["schedule"] = Schedule(hours);
            StoryLoadResult r = Read(root);
            Assert.True(r.IsValid, r.Report.ToString());
            return r.Story!.Npc("npc.odile");
        }
        // Un seul créneau, à 23 h 59 : il vaut toute la journée (celui de la veille avant lui).
        Npc one = Odile("23:59");
        Assert.All(new[] { 0, 1, 720, 1438, 1439 }, m => Assert.Equal(1439, one.SlotAt(m)!.Minute));
        // 00:00 et 23:59 : la dernière minute seulement au second.
        Npc both = Odile("00:00", "23:59");
        Assert.Equal((0, 0, 1439), (both.SlotAt(0)!.Minute, both.SlotAt(1438)!.Minute, both.SlotAt(1439)!.Minute));
        // Premier créneau tardif : à minuit, c'est encore celui de la veille (le dernier).
        Npc late = Odile("20:00", "22:00");
        Assert.Equal(("riviere", "riviere", "moulin", "riviere"), (late.SlotAt(0)!.Activity, late.SlotAt(19 * 60 + 59)!.Activity, late.SlotAt(20 * 60)!.Activity, late.SlotAt(22 * 60)!.Activity));
        JsonObject bad = Minimal(9);
        bad["data"]!["npcs"]![0]!["schedule"] = Schedule("24:00");
        Assert.Contains(Read(bad).Report.Errors, e => e.Path == "$.data.npcs[0].schedule[0].at" && e.Code == ValidationCode.InvalidValue);
    }

    // --- Où est chacun, selon l'heure ---------------------------------------------------------------------------------------

    [Fact]
    public void Le_creneau_en_cours_est_le_dernier_commence_et_la_journee_boucle()
    {
        Npc firmin = Shipped.Value.Npc("npc.boulanger");
        Assert.Equal("fournil", firmin.SlotAt(0)!.Activity);   // celui de la veille (21 h 30)
        Assert.Equal("fournil", firmin.SlotAt(2 * 60)!.Activity);
        Assert.Equal("fournil", firmin.SlotAt(11 * 60 + 29)!.Activity);
        Assert.Equal("marche", firmin.SlotAt(11 * 60 + 30)!.Activity);
        Assert.Equal("sieste", firmin.SlotAt(14 * 60)!.Activity);
        Assert.Equal("taverne", firmin.SlotAt(18 * 60 + 30)!.Activity);
        Assert.Equal("taverne", firmin.SlotAt(21 * 60 + 29)!.Activity);
        Assert.Equal("fournil", firmin.SlotAt(21 * 60 + 30)!.Activity);
        Assert.Equal("fournil", firmin.SlotAt(1439)!.Activity);
        Assert.Null(Shipped.Value.Npc("npc.ysolde").SlotAt(600));
        Assert.Equal(new[] { "fournil", "marche", "abri", "sieste", "taverne" }, firmin.Activities);
    }

    [Theory]
    [InlineData(11 * 60 + 29, "fournil", -42, 3.5)]
    [InlineData(11 * 60 + 30, "marche", -55.5, -4.5)]
    [InlineData(15 * 60, "sieste", -38.5, 4.8)]
    [InlineData(19 * 60, "taverne", -1.9, 2.0)]   // V2.3.1 : dans la salle de l'auberge (zone de son créneau)
    [InlineData(23 * 60, "fournil", -42, 3.5)]
    public void Firmin_suit_sa_journee(int minute, string activity, double x, double z)
    {
        long day = DryDay(minute);
        WorldSession w = At(TickAt(day, minute));
        Npc firmin = w.Narrative!.Book.Npc("npc.boulanger");
        Assert.Equal(activity, w.NpcActivity(firmin));
        Assert.Equal(P(x, z), w.NpcPosition(firmin));
    }

    [Fact]
    public void Les_boutiques_de_Garin_et_Bertille_sont_a_l_etal_de_5_h_a_22_h_hors_pauses()
    {
        // Décision de Damien du 30 octobre 2026 : Garin et Bertille ouvrent de 5 h à 22 h. Leur emploi du temps les garde à l'étal, sauf le
        // repas (et, pour Bertille, un verre à la taverne avant 21 h 15) : la boutique les suit, et l'étal dit où ils sont.
        foreach (string id in new[] { "npc.armurier", "npc.epiciere" })
        {
            Npc npc = Shipped.Value.Npc(id);
            foreach (int minute in new[] { 5 * 60, 7 * 60, 11 * 60 + 59, 14 * 60, 18 * 60, 21 * 60 + 15, 21 * 60 + 58, 21 * 60 + 59 })
                Assert.True(npc.SlotAt(minute)!.Position == npc.Position, $"{id} à {minute / 60} h {minute % 60}");
        }
    }

    [Fact]
    public void Le_matin_chacun_est_a_sa_place_comme_au_debut_du_jeu()
    {
        // De 7 h 45 à 11 h 29, chaque PNJ est à sa place de l'histoire (le jeu commence à 8 h : l'ouverture et les premières quêtes ne bougent pas).
        foreach (Npc npc in Shipped.Value.Npcs.Where(n => n.Schedule.Count > 0))
            for (int minute = 7 * 60 + 45; minute < 11 * 60 + 30; minute++)
                Assert.True(npc.SlotAt(minute)!.Position == npc.Position, $"{npc.Id} à {minute / 60} h {minute % 60}");
    }

    [Fact]
    public void La_nuit_chacun_reste_devant_sa_porte_ou_a_sa_place()
    {
        // Décision de Damien du 6 novembre 2026 (PR #99) : la nuit, devant sa porte, toujours joignable : l'emploi du temps la garde.
        foreach (Npc npc in Shipped.Value.Npcs.Where(n => n.Schedule.Count > 0))
            foreach (int minute in new[] { 22 * 60 + 30, 0, 3 * 60, 4 * 60 + 59 })
            {
                GroundVector expected = Vallons.NightSpotOf(npc.Id) ?? npc.Position;
                // Gaspard reste au coin du feu (à 1,6 m de son poste) jusqu'à 23 h.
                if (npc.Id == "npc.gaspard" && minute == 22 * 60 + 30) expected = npc.SlotAt(minute)!.Position;
                Assert.True(npc.SlotAt(minute)!.Position == expected, $"{npc.Id} à {minute / 60} h {minute % 60}");
            }
    }

    [Fact]
    public void Sous_l_averse_on_s_abrite_pas_sous_la_pluie_fine()
    {
        Npc firmin = Shipped.Value.Npc("npc.boulanger");
        (long day, int minute)? shower = null, drizzle = null;
        for (long day = 1; day < 600 && (shower is null || drizzle is null); day++)
            for (int minute = 11 * 60 + 30; minute < 14 * 60; minute += 5)
            {
                WeatherKind kind = WorldWeather.At("zone.vallons", day, minute).Kind;
                if (shower is null && kind == WeatherKind.Shower) shower = (day, minute);
                if (drizzle is null && kind == WeatherKind.Drizzle) drizzle = (day, minute);
            }
        WorldSession wet = At(TickAt(shower!.Value.day, shower.Value.minute));
        Assert.Equal("abri", wet.NpcActivity(firmin));
        Assert.Equal(P(-53.5, 4.2), wet.NpcPosition(firmin));
        WorldSession light = At(TickAt(drizzle!.Value.day, drizzle.Value.minute));
        Assert.Equal("marche", light.NpcActivity(firmin));
        Assert.Equal(P(-55.5, -4.5), light.NpcPosition(firmin));
        Assert.True(WorldSession.Shelters(WeatherKind.Storm));
        Assert.False(WorldSession.Shelters(WeatherKind.Mist));
    }

    [Fact]
    public void Meme_heure_meme_place_et_sauvegarde_inchangee()
    {
        long tick = TickAt(DryDay(12 * 60 + 15), 12 * 60 + 15);
        WorldSession a = At(tick), b = At(tick);
        foreach (Npc npc in a.Narrative!.Book.Npcs) Assert.Equal(a.NpcPosition(npc), b.NpcPosition(b.Narrative!.Book.Npc(npc.Id)));
        string text = SaveJsonReader.Write(a.Save());
        Assert.Equal(16, SaveValidator.CurrentSchemaVersion);   /* V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements) */   // rien de l'emploi du temps n'est sauvegardé
        Assert.DoesNotContain("schedule", text, StringComparison.Ordinal);
        var back = new WorldSession(SaveJsonReader.Read(text).Save!, Vallons, ShippedData, Progression, ItemsCatalog, Book(Vallons), market: new Market(ShippedMerchants));
        back.Step();
        foreach (Npc npc in a.Narrative.Book.Npcs) Assert.Equal(a.NpcActivity(npc), back.NpcActivity(back.Narrative!.Book.Npc(npc.Id)));
    }

    // --- Ce qu'ils disent, selon le lieu --------------------------------------------------------------------------------

    [Fact]
    public void Garin_a_la_taverne_le_dit_et_garde_ses_reponses()
    {
        int noon = 12 * 60 + 15;
        long day = DryDay(noon);
        // V2.3.1 : la taverne est dans la salle de l'auberge (Garin n'est plus sur la terrasse).
        Assert.Equal(DialogueRefusal.TooFar, At(TickAt(day, noon), Zone(-64.5, 1.4)).Talk("npc.armurier", out DialogueView? _));
        WorldSession tavern = V231TestData.Inside("zone.auberge", TickAt(day, noon), 3.5, 0.8, Shipped.Value);
        Assert.Equal(DialogueRefusal.None, tavern.Talk("npc.armurier", out DialogueView? _));   // V1.9.10 : la première fois, il se présente
        tavern.Narrative!.Close();
        Assert.Equal(DialogueRefusal.None, tavern.Talk("npc.armurier", out DialogueView? there));
        Assert.Equal("text.v23.garin.taverne", there!.Text);
        // On lui achète là-bas : la boutique est auprès de lui.
        Assert.Equal(TradeRefusal.None, tavern.CanTrade("merchant.armurier"));
        // Le matin, à son étal : son accueil, avec les mêmes réponses.
        WorldSession stall = At(TickAt(day, 9 * 60), Zone(Near("merchant.armurier").X, Near("merchant.armurier").Z));
        Assert.Equal(DialogueRefusal.None, stall.Talk("npc.armurier", out DialogueView? home));
        Assert.Equal("text.armurier.accueil", home!.Text);
        Assert.Equal(home.Options.Select(o => o.Text), there.Options.Select(o => o.Text));
        Assert.Equal(TradeRefusal.TooFar, At(TickAt(day, noon), Zone(Near("merchant.armurier").X, Near("merchant.armurier").Z)).CanTrade("merchant.armurier"));
    }

    [Fact]
    public void Gaspard_au_coin_du_feu_le_soir()
    {
        int evening = 20 * 60;
        long day = DryDay(evening);
        WorldSession w = At(TickAt(day, evening), Zone(25.5, -12.2));
        Assert.Equal("feu", w.NpcActivity(w.Narrative!.Book.Npc("npc.gaspard")));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.gaspard", out DialogueView? _));   // V1.9.10 : d'abord son accueil
        w.Narrative!.Close();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.gaspard", out DialogueView? view));
        Assert.Equal("text.v23.gaspard.feu", view!.Text);
    }

    [Fact]
    public void Chaque_replique_est_sur_un_noeud_d_accueil_et_chaque_activite_est_vecue()
    {
        StoryBook book = Shipped.Value;
        foreach (Dialogue d in book.Dialogues)
            foreach (DialogueNode node in d.Nodes.Where(n => n.Lines.Count > 0))
            {
                // Le nœud est celui d'une entrée de dialogue (accueil, mot d'après, réplique du temps) : la réplique se dit en arrivant.
                Assert.Contains(d.Entries, e => e.Node == node.Id);
                foreach (DialogueLine line in node.Lines)
                    foreach (StoryCondition c in line.Conditions.Where(c => c.Kind == ConditionKind.Activity))
                    {
                        Npc npc = book.Npc(c.Id);
                        // L'activité arrive bien dans la journée (un créneau, ou l'abri d'un créneau).
                        Assert.Contains(npc.Schedule, s => s.Activity == c.Step || s.Rain?.Activity == c.Step);
                    }
            }
        // Aucun abri sans réplique chez un PNJ qui en a d'autres : sous l'averse, il dit qu'il s'abrite (ou parle de la pluie).
        foreach (Npc npc in book.Npcs.Where(n => n.Schedule.Any(s => s.Rain != null)))
        {
            Dialogue d = book.Dialogue(npc.Dialogue);
            bool rainEntry = d.Entries.Any(e => e.Conditions.Any(c => c.Kind == ConditionKind.Weather));
            bool shelterLine = d.Nodes.Any(n => n.Lines.Any(l => l.Conditions.Any(c => c.Kind == ConditionKind.Activity && c.Id == npc.Id && c.Step == "abri")));
            Assert.True(rainEntry || shelterLine, npc.Id);
        }
    }

    // --- La boutique d'un marchand parti -----------------------------------------------------------------------------------

    [Fact]
    public void A_l_etal_d_un_marchand_parti_la_boutique_dit_ou_il_est()
    {
        int noon = 12 * 60 + 15;
        long day = DryDay(noon);
        (string data, string scenarios) = DataCopy(Near("merchant.armurier"));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(day, noon), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.Null(a.NpcInReach);
        Assert.Equal("npc.armurier", a.AwayStallInReach!.Id);
        Assert.Equal("à la taverne", a.ActivityOf("npc.armurier"));
        Assert.Null(a.ActivityOf("npc.ysolde"));
        Assert.False(a.Interact());
        Assert.Equal("Garin, l'armurier n'est pas à son étal à cette heure : on le trouve à la taverne, et sa boutique avec lui.", a.DrainMessages().First());
    }

    [Fact]
    public void A_l_etal_de_Renart_parti_pas_encore_ouvert_la_vraie_raison()
    {
        // Relecture indépendante de la PR #103 : l'étal de Renart (ouvert après son aveu) n'annonce pas « on le trouve au puits » quand on ne
        // peut pas lui acheter : il dit qu'il n'est pas encore ouvert.
        int noon = 12 * 60 + 15;
        (string data, string scenarios) = DataCopy((-47.5, -0.6));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(DryDay(noon), noon), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save, data, scenarios);
        a.DrainMessages();
        Assert.Equal("commerages", a.World.NpcActivity(a.World.Narrative!.Book.Npc("npc.renart")));
        Assert.Equal("npc.renart", a.AwayStallInReach!.Id);
        Assert.Equal(TradeRefusal.NotYetOpen, a.World.TradeStatus("merchant.renart"));
        Assert.Equal(TradeRefusal.TooFar, a.World.CanTrade("merchant.renart"));
        Assert.False(a.Interact());
        Assert.Equal("Cet étal n'est pas encore ouvert pour toi.", a.DrainMessages().First());
    }

    [Fact]
    public void Dans_l_auberge_Mathurin_descend_a_la_cave_et_son_corps_suit_son_repere()
    {
        // Revue Codex de la PR #103 : on reste dans l'auberge au passage de 14 h. Le cœur déplace Mathurin (parler, livrer : à la cave) ;
        // la carte de la pièce garde la place d'entrée de son corps (posé une fois), et l'affichage le fait marcher jusqu'à son repère
        // (WorldMarkers.Walk → ArenaView.MoveSpeaker → SpaceView.MoveSpeaker).
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.auberge", TickAt(1, 13 * 60 + 59), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save);
        Assert.True(a.Indoors);
        (double X, double Z) Mathurin() => a.Npcs().Where(n => n.Id == "npc.aubergiste").Select(n => (n.X, n.Z)).Single();
        Assert.Equal((3.75, 3.55), Mathurin());
        Assert.Contains(("npc.aubergiste", 3.75, 3.55), a.Map.Speakers);
        Assert.Equal("derrière son comptoir", a.ActivityOf("npc.aubergiste"));
        for (int i = 0; i < 400 && a.World.Zone.Time.Minute != 14 * 60 + 1; i++) a.Advance(0.05);
        Assert.Equal((5.8, -1.5), Mathurin());
        Assert.Equal("à la cave", a.ActivityOf("npc.aubergiste"));
        Assert.Contains(("npc.aubergiste", 3.75, 3.55), a.Map.Speakers);   // même session : le corps est posé une fois, puis suit son repère
        string markers = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "WorldMarkers.cs"));
        Assert.Contains("arena.MoveSpeaker(m.Data.Id, here.x, here.z, deltaTime);", markers, StringComparison.Ordinal);
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("_markers.Walk(a, Time.deltaTime, _arena);", game, StringComparison.Ordinal);
        string space = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("_speakers[v.Id] = placed;", space, StringComparison.Ordinal);
        // Rechargée à 14 h 01 (ou une porte repassée) : le corps est posé d'emblée à la cave.
        SaveStore.Write(save, a.World.Save());
        AdventureSession back = Adventure(save);
        Assert.Contains(("npc.aubergiste", 5.8, -1.5), back.Map.Speakers);
    }

    // --- Les villageois du décor --------------------------------------------------------------------------------------------

    private static readonly VillageSpots Spots = new VillageSpots((-69, 4), (0, -1), (-58, -1), 1.2,
        new[] { (-35.0, -15.0, -25.0, -6.0), (-58.0, -24.0, -51.5, -17.5) });

    [Fact]
    public void Les_villageois_du_decor_font_leur_course_puis_reviennent()
    {
        var villagers = Enumerable.Range(0, 400).Select(i => new StyleVillager("v" + i, "paysan", VillagerActivity.Idle, -40, -10, 90)).ToList();
        var counts = villagers.GroupBy(VillageDay.ErrandOf).ToDictionary(g => g.Key, g => g.Count());
        // Réponse de Damien (question 8) : un sur trois pour chaque course ; personne ne reste toute la journée (sauf les marcheurs).
        Assert.False(counts.ContainsKey(VillagerErrand.None));
        foreach (VillagerErrand e in new[] { VillagerErrand.Market, VillagerErrand.Lunch, VillagerErrand.Tavern }) Assert.InRange(counts[e], 100, 170);
        // Les marcheurs gardent leur allée.
        Assert.Equal(VillagerErrand.None, VillageDay.ErrandOf(new StyleVillager("m", "paysan", VillagerActivity.Walk, 0, 0, 0, 5, 5)));
        foreach (StyleVillager v in villagers)
        {
            VillagerErrand errand = VillageDay.ErrandOf(v);
            (int from, int to) = VillageDay.Window(errand, v);
            // Hors de sa course : à son poste, avec sa pose. Toujours la même réponse (déterminisme).
            VillagerGoal early = VillageDay.Goal(v, 6 * 60, Spots);
            Assert.True(early.AtPost && early.X == v.X && early.Z == v.Z && early.Activity == v.Activity);
            Assert.Equal(early, VillageDay.Goal(v, 6 * 60, Spots));
            if (errand == VillagerErrand.None) continue;
            Assert.InRange(to - from, 60, 180);
            Assert.True(VillageDay.Goal(v, from - 1, Spots).AtPost);
            Assert.True(VillageDay.Goal(v, to, Spots).AtPost);
            VillagerGoal away = VillageDay.Goal(v, from, Spots);
            Assert.False(away.AtPost);
            switch (errand)
            {
                case VillagerErrand.Market:
                    double r = Math.Sqrt((away.X + 58) * (away.X + 58) + (away.Z + 1) * (away.Z + 1));
                    Assert.InRange(r, 2.4 - 1e-9, 3.2);   // demi-côté du puits, plus 1,2 à 2 m
                    break;
                case VillagerErrand.Tavern:
                    Assert.True(away.Z < 4 - 1.5 && Math.Abs(away.X + 69) >= 1.8 - 1e-9, $"{away.X}, {away.Z}");   // côté rue, jamais devant la porte
                    break;
                case VillagerErrand.Lunch:
                    Assert.True(away.Indoors);
                    Assert.Equal((-35.0 - 0.6, -10.0), (away.X, away.Z));   // contre le mur de la maison la plus proche, de son côté
                    break;
            }
        }
        // Sans place dans la zone (un intérieur), chacun reste à son poste.
        Assert.True(VillageDay.Goal(villagers[0], 12 * 60 + 30, null).AtPost);
    }

    [Fact]
    public void Sans_place_dans_la_zone_chacun_reste_a_son_poste_et_les_fenetres_tiennent_dans_la_journee()
    {
        var villagers = Enumerable.Range(0, 400).Select(i => new StyleVillager("w" + i, "paysan", VillagerActivity.Talk, 3, 4, 10)).ToList();
        var empty = new VillageSpots(null, null, null, 0, Array.Empty<(double, double, double, double)>());
        foreach (StyleVillager v in villagers)
        {
            VillagerPlan plan = VillageDay.Plan(v, empty);
            Assert.Equal(VillagerErrand.None, plan.Errand);
            foreach (int minute in new[] { 0, 9 * 60 + 30, 12 * 60 + 30, 20 * 60, 1439 })
            {
                Assert.True(VillageDay.Goal(v, minute, empty).AtPost);
                Assert.True(VillageDay.Goal(v, minute, null).AtPost);
            }
            // Avec les places, le plan calculé une fois donne la même chose que le calcul complet, à chaque minute.
            VillagerPlan full = VillageDay.Plan(v, Spots);
            for (int minute = 0; minute < 1440; minute += 7) Assert.Equal(VillageDay.Goal(v, minute, Spots), VillageDay.Goal(full, minute));
            foreach (VillagerErrand e in new[] { VillagerErrand.Market, VillagerErrand.Lunch, VillagerErrand.Tavern })
            {
                (int from, int to) = VillageDay.Window(e, v);
                Assert.True(0 <= from && from < to && to <= 1440, $"{v.Id} {e} {from}-{to}");
            }
        }
        Assert.InRange(villagers.Select(VillageDay.Offset).Distinct().Count(), 30, 2 * VillageDay.Spread + 1);
    }

    [Fact]
    public void Places_du_bourg_trouvees_sur_la_carte()
    {
        (string data, string scenarios) = DataCopy((-58, -6));
        string save = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(0, 9 * 60), 0, Array.Empty<string>(), Hero(100)));
        AdventureSession a = Adventure(save, data, scenarios);
        VillageSpots spots = VillageDay.Spots(a.Map)!;
        Assert.Equal((-69.0, 4.0), spots.Tavern);
        Assert.Equal((0.0, -1.0), spots.TavernOut);   // la porte de l'auberge donne au sud
        Assert.Equal((-58.0, -1.0), spots.Well);
        Assert.InRange(spots.Houses.Count, 5, 20);
        Assert.Contains("Firmin", VillageTrial.Guide(5 * 60 + 25), StringComparison.Ordinal);
        Assert.Contains("taverne", VillageTrial.Guide(19 * 60 + 40), StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_d_essai_de_la_journee_du_village()
    {
        string menu = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsVillageTrial.cs"));
        Assert.Equal(7, menu.Split("[MenuItem(\"Éclats du Seuil/Essai de la journée du village").Length - 1);
        Assert.Contains("DayTrial.MoveSave(SavePath, minute", menu, StringComparison.Ordinal);
        Assert.Contains("WeatherTrial.MoveSave(SavePath, \"shower\"", menu, StringComparison.Ordinal);
        Assert.Contains("isPlayingOrWillChangePlaymode", menu, StringComparison.Ordinal);
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("_arena.Village(a.World.Zone.Time.Minute);", game, StringComparison.Ordinal);
        string markers = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "WorldMarkers.cs"));
        Assert.Contains("adventure.ActivityOf(npc.Id)", markers, StringComparison.Ordinal);
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

/// <summary>Sous-lot V2.3 : aides des tests des histoires d'avant le schéma 9.</summary>
public static class V23TestData
{
    /// <summary>
    /// Déplace un PNJ de l'histoire <paramref name="root"/> (tests qui le rapprochent d'une porte, parfois contre un meuble) : il reste là,
    /// sans emploi du temps ; les répliques qui parlent de ses activités partent avec lui.
    /// </summary>
    public static void Move(JsonNode npc, JsonArray position)
    {
        npc["position"] = position;
        npc.AsObject().Remove("schedule");
        string id = (string)npc["id"]!;
        JsonNode root = npc.Root;
        if (root["data"]?["dialogues"] is not JsonArray dialogues) return;
        foreach (JsonNode? dialogue in dialogues)
            foreach (JsonNode? node in dialogue!["nodes"]!.AsArray())
                if (node!["lines"] is JsonArray lines)
                {
                    for (int k = lines.Count - 1; k >= 0; k--)
                        if (lines[k]!["conditions"]!.AsArray().Any(c => (string?)c!["if"] == "activity" && (string?)c["id"] == id)) lines.RemoveAt(k);
                    if (lines.Count == 0) node.AsObject().Remove("lines");
                }
    }


    /// <summary>
    /// Enlève d'une histoire ce que le schéma 9 apporte (emplois du temps, répliques du moment), pour la relire dans une version plus ancienne
    /// (tests des schémas 4 à 8).
    /// </summary>
    public static void WithoutSchedules(JsonObject root)
    {
        foreach (JsonNode? npc in root["data"]!["npcs"]!.AsArray()) npc!.AsObject().Remove("schedule");
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
            foreach (JsonNode? node in dialogue!["nodes"]!.AsArray()) node!.AsObject().Remove("lines");
    }
}
