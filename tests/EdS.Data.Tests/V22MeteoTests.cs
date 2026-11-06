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
/// Sous-lot V2.2 (météo) : temps du monde tiré du jour et de la zone (déterminisme, répartition sur beaucoup de jours, durées, transitions
/// douces, brume du matin, éclairs), recalculé après un chargement (sauvegarde inchangée), compatible avec la nuit (V2.1) ; histoire en
/// schéma 8 (condition <c>weather</c>, schéma 7 encore lu) et répliques des villageois ; affichage (lumière, ambiances, flaques, sons de
/// synthèse) et menu d'essai.
/// </summary>
[Trait("Category", "World")]
public sealed class V22MeteoTests : IDisposable
{
    private const string Vallons = "zone.vallons";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v22_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static readonly Lazy<ZoneDefinition> Shipped = new(() => Zone());

    /// <summary>Tick du monde où il est <paramref name="minute"/> au jour <paramref name="day"/> (le monde commence à 8 h du jour 0).</summary>
    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    /// <summary>Première minute (jour, minute de 8 h à 18 h) d'au moins le jour 1 où il fait <paramref name="kind"/>.</summary>
    private static (long Day, int Minute) Find(WeatherKind kind, int from = 8 * 60, int to = 18 * 60)
    {
        for (long day = 1; day < 2000; day++)
            for (int minute = from; minute < to; minute += 10)
                if (WorldWeather.At(Vallons, day, minute).Kind == kind) return (day, minute);
        throw new InvalidOperationException("introuvable : " + kind);
    }

    private static IEnumerable<(long Day, int Minute)> Minutes(long fromDay, long days)
    {
        for (long d = fromDay; d < fromDay + days; d++)
            for (int m = 0; m < WorldClock.MinutesPerDay; m++)
                yield return (d, m);
    }

    // --- Cœur : temps du monde ---------------------------------------------------------------------------------------------

    [Fact]
    public void Meme_jour_meme_temps_sans_horloge_ni_etat()
    {
        for (long day = 0; day < 50; day++)
        {
            Assert.Equal(WorldWeather.Day(Vallons, day), WorldWeather.Day(Vallons, day));
            foreach (int minute in new[] { 0, 333, 720, 1439 })
                Assert.Equal(WorldWeather.At(Vallons, day, minute), WorldWeather.At(Vallons, new WorldTime(day, minute)));
        }
        Assert.Equal("zone.vallons/weather/12", WorldWeather.Key(Vallons, 12));
        // Une autre zone a son propre temps.
        Assert.Contains(Enumerable.Range(1, 60), d => !WorldWeather.Day(Vallons, d).Equals(WorldWeather.Day("zone.autre", d)));
        // Le jour 0 (début du monde) est toujours beau, sans brume.
        WeatherDay first = WorldWeather.Day(Vallons, 0);
        Assert.Equal((WeatherMood.Fair, 0), (first.Mood, first.Mist));
        Assert.All(Enumerable.Range(0, WorldWeather.SlotsPerDay), i => Assert.Equal(WeatherKind.Clear, first.Slot(i)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldWeather.At(Vallons, 0, 1440));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldWeather.At(Vallons, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => first.Slot(8));
    }

    [Fact]
    public void Repartition_sur_deux_mille_jours()
    {
        var counts = new Dictionary<WeatherKind, int>();
        int samples = 0, mistMornings = 0, moods = 0, stormyDays = 0;
        for (long day = 1; day <= 2000; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                WeatherState s = WorldWeather.At(Vallons, day, hour * 60 + 30);
                counts[s.Kind] = counts.TryGetValue(s.Kind, out int c) ? c + 1 : 1;
                samples++;
                // La brume ne vient que le matin.
                if (hour >= 10 || hour < 4) Assert.Equal(0, s.Mist);
                Assert.InRange(s.Clouds, 0, WorldWeather.Scale);
                Assert.InRange(s.Rain, 0, WorldWeather.Scale);
                Assert.InRange(s.Wetness, 0, WorldWeather.Scale);
            }
            if (WorldWeather.At(Vallons, day, 6 * 60).Kind == WeatherKind.Mist) mistMornings++;
            WeatherDay d = WorldWeather.Day(Vallons, day);
            if (d.Mood == WeatherMood.Fair) moods++;
            if (d.Mood == WeatherMood.Stormy) stormyDays++;
        }
        double Share(params WeatherKind[] kinds) => kinds.Sum(k => counts.TryGetValue(k, out int c) ? c : 0) / (double)samples;
        // Surtout du beau temps et des nuages ; de la pluie sans excès ; l'orage est rare.
        Assert.InRange(Share(WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Mist), 0.65, 0.92);
        Assert.InRange(Share(WeatherKind.Drizzle, WeatherKind.Shower, WeatherKind.Storm), 0.08, 0.3);
        Assert.InRange(Share(WeatherKind.Storm), 0.003, 0.03);
        Assert.InRange(Share(WeatherKind.Clear), 0.3, 0.7);
        // Brume du matin à 6 h, un jour sur trois à peu près.
        Assert.InRange(mistMornings / 2000.0, 0.2, 0.5);
        Assert.InRange(moods / 2000.0, 0.45, 0.55);
        Assert.InRange(stormyDays / 2000.0, 0.05, 0.09);
    }

    [Fact]
    public void Transitions_douces_minute_par_minute_et_d_un_jour_a_l_autre()
    {
        WeatherState? previous = null;
        foreach ((long day, int minute) in Minutes(1, 40))
        {
            WeatherState s = WorldWeather.At(Vallons, day, minute);
            if (previous is WeatherState p)
            {
                // Un changement de temps prend 45 minutes du monde (plus de 5 minutes de jeu) : jamais de saut, minuit compris.
                Assert.True(Math.Abs(s.Clouds - p.Clouds) <= 34, $"nuages jour {day}, {minute}");
                Assert.True(Math.Abs(s.Rain - p.Rain) <= 34, $"pluie jour {day}, {minute}");
                Assert.True(Math.Abs(s.Storm - p.Storm) <= 34, $"orage jour {day}, {minute}");
                Assert.True(Math.Abs(s.Mist - p.Mist) <= 20, $"brume jour {day}, {minute}");
                Assert.True(Math.Abs(s.Wetness - p.Wetness) <= 101, $"sol jour {day}, {minute}");
            }
            previous = s;
        }
    }

    [Fact]
    public void Durees_credibles_et_sol_qui_seche()
    {
        var runs = new List<int>();
        int run = 0, dryingSeen = 0;
        WeatherState? previous = null;
        foreach ((long day, int minute) in Minutes(1, 400))
        {
            WeatherState s = WorldWeather.At(Vallons, day, minute);
            if (s.Raining) run++;
            else if (run > 0)
            {
                runs.Add(run);
                run = 0;
            }
            // Le sol reste mouillé un moment après la pluie, puis sèche.
            if (previous is WeatherState p && p.Raining && !s.Raining)
            {
                Assert.True(s.Wetness > 0);
                dryingSeen++;
            }
            if (!s.Raining && s.Rain == 0 && s.Wetness > 0) Assert.True(WorldWeather.At(Vallons, (day * 1440 + minute + 180) / 1440, (int)((day * 1440 + minute + 180) % 1440)).Wetness < s.Wetness
                || WorldWeather.At(Vallons, (day * 1440 + minute + 180) / 1440, (int)((day * 1440 + minute + 180) % 1440)).Rain > 0);
            previous = s;
        }
        runs.Sort();
        Assert.True(runs.Count > 20);
        // Une pluie dure en général plus de deux heures du monde (un quart d'heure de jeu) ; jamais une poignée de minutes.
        Assert.True(runs[runs.Count / 2] >= 120, "médiane " + runs[runs.Count / 2]);
        Assert.True(runs[0] >= 20, "plus courte " + runs[0]);
        Assert.True(dryingSeen > 0);
    }

    [Fact]
    public void Brume_du_matin_et_eclairs_de_l_orage()
    {
        // La brume : montée de 4 h à 5 h 30, tenue jusqu'à 7 h, levée avant 9 h 30.
        Assert.Equal(0, WorldWeather.MistAt(800, 239));
        Assert.Equal(800, WorldWeather.MistAt(800, 330));
        Assert.Equal(800, WorldWeather.MistAt(800, 419));
        Assert.InRange(WorldWeather.MistAt(800, 500), 1, 799);
        Assert.Equal(0, WorldWeather.MistAt(800, 570));
        Assert.Equal(0, WorldWeather.MistAt(0, 360));
        (long day, int _) = Find(WeatherKind.Mist, 5 * 60, 7 * 60);
        Assert.True(WorldWeather.Day(Vallons, day).Mist > 0);
        Assert.Equal(WeatherKind.Mist, WorldWeather.At(Vallons, day, 6 * 60).Kind);
        // Les éclairs : seulement pendant l'orage, à des minutes fixes ; le tonnerre suit, plus vite quand l'orage est là.
        int flashes = 0, stormMinutes = 0;
        foreach ((long d, int m) in Minutes(1, 600))
        {
            WeatherState s = WorldWeather.At(Vallons, d, m);
            if (s.Storm < 300) Assert.False(s.Lightning);
            if (s.Kind == WeatherKind.Storm) stormMinutes++;
            if (!s.Lightning) continue;
            flashes++;
            Assert.InRange(s.ThunderDelayTenths, 4, 49);
            Assert.Equal(s, WorldWeather.At(Vallons, d, m));
        }
        Assert.True(stormMinutes > 0 && flashes > stormMinutes / 6, $"{flashes} éclairs pour {stormMinutes} minutes d'orage");
    }

    [Fact]
    public void Identifiants_du_temps_et_conditions_de_l_histoire()
    {
        Assert.Equal(StoryValidator.Weathers.Take(6), Enum.GetValues<WeatherKind>().Select(WorldWeather.Id));
        Assert.Equal("rain", StoryValidator.Weathers[6]);
        Assert.True(WorldWeather.Matches(WeatherKind.Drizzle, "rain") && WorldWeather.Matches(WeatherKind.Storm, "rain"));
        Assert.False(WorldWeather.Matches(WeatherKind.Mist, "rain") || WorldWeather.Matches(WeatherKind.Storm, "shower"));
        Assert.True(WorldWeather.Matches(WeatherKind.Storm, "storm"));
    }

    // --- Sauvegarde inchangée, temps recalculé ----------------------------------------------------------------------------

    private static WorldSession At(long tick) =>
        new WorldSession(new WorldSave(Vallons, tick, 0, new[] { PlayerCharacters.PlacesKey }, Hero(100)), Shipped.Value, ShippedData, Progression,
            ItemsCatalog, Book(Shipped.Value), market: new Market(ShippedMerchants));

    [Fact]
    public void Apres_un_chargement_le_meme_temps_et_la_sauvegarde_ne_change_pas()
    {
        (long day, int minute) = Find(WeatherKind.Shower);
        WorldSession w = At(TickAt(day, minute));
        for (int i = 0; i < 5; i++) w.Step();
        Assert.Equal(WorldWeather.At(Vallons, w.Zone.Time), w.Weather);
        Assert.True(w.Weather.Raining);
        string text = SaveJsonReader.Write(w.Save());
        Assert.DoesNotContain("weather", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, SaveValidator.CurrentSchemaVersion);   // format de la sauvegarde inchangé
        WorldSave back = SaveJsonReader.Read(text).Save!;
        var next = new WorldSession(back, Shipped.Value, ShippedData, Progression, ItemsCatalog, Book(Shipped.Value), market: new Market(ShippedMerchants));
        Assert.Equal(w.Zone.Time, next.Zone.Time);
        Assert.Equal(w.Weather, next.Weather);
    }

    [Fact]
    public void La_meteo_ne_change_ni_la_nuit_ni_les_betes()
    {
        // Une nuit d'orage reste la nuit : mêmes bêtes, même heure ; la lumière reste bleue et lisible.
        (long day, int minute) = Find(WeatherKind.Storm, 12 * 60, 22 * 60);
        DayLight night = DayCycle.At(23 * 60);
        WeatherState storm = WorldWeather.At(Vallons, day, minute);
        WeatherLook look = WeatherCycle.Look(storm, night);
        Assert.True(night.AmbientLevel * look.Ambient >= DayCycle.NightAmbient * WeatherCycle.NightFloor - 1e-9);
        Assert.True(look.Sun < 1 && look.FogEnd < 1 && look.Rain > 0.5);
        // Par temps clair, rien ne change : le catalogue (jour) et la nuit de V2.1 restent tels quels.
        WeatherState clear = WorldWeather.At(Vallons, 0, 12 * 60);
        Assert.Equal(WeatherKind.Clear, clear.Kind);
        foreach (int m in new[] { 0, 6 * 60, 12 * 60, 20 * 60 + 30 })
        {
            WeatherLook c = WeatherCycle.Look(clear, DayCycle.At(m));
            Assert.Equal((1.0, 1.0, 1.0, 1.0, 1.0, 0.0, 0.0), (c.Sun, c.Ambient, c.Sky, c.FogStart, c.FogEnd, c.Rain, c.Wetness));
            Assert.Equal(StyleTint.White, c.AmbientTint);
        }
        // Sous l'orage, le jour : moins de soleil, ambiance plus basse mais jamais sombre, lanternes allumées plus tôt.
        WeatherLook day0 = WeatherCycle.Look(storm, DayCycle.At(14 * 60));
        Assert.InRange(day0.Sun, 0.05, 0.3);
        Assert.InRange(day0.Ambient, 0.55, 0.8);
        Assert.True(day0.Lamps > 0.5);
        // La brume du matin rapproche beaucoup la brume du catalogue.
        (long md, int _) = Find(WeatherKind.Mist, 5 * 60 + 30, 7 * 60);
        WeatherLook misty = WeatherCycle.Look(WorldWeather.At(Vallons, md, 6 * 60), DayCycle.At(6 * 60));
        Assert.True(misty.FogEnd < 0.5 && misty.Mist > 0.5);
        // Les bêtes de la nuit ne dépendent pas du temps (même partie, même nuit, avec ou sans pluie).
        WorldSession w = At(TickAt(day, 23 * 60));
        Assert.Equal(DayPhase.Night, w.Zone.Time.Phase);
        Assert.Equal(WorldWeather.At(Vallons, day, 23 * 60), w.Weather);
    }

    // --- Histoire en schéma 8 : condition weather et répliques ------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Shipped.Value, ItemsCatalog, V16TestData.Interiors);

    [Fact]
    public void Histoire_livree_en_schema_8_avec_les_repliques_du_temps()
    {
        StoryBook book = Book(Shipped.Value);
        Assert.Equal((8, StoryValidator.CurrentSchemaVersion, "0.14.0-provisoire"), (book.SchemaVersion, StoryValidator.CurrentSchemaVersion, book.ContentVersion));
        JsonObject root = StoryJson();
        int weatherEntries = 0;
        var speakers = new HashSet<string>();
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = dialogue!["entries"]!.AsArray();
            Assert.InRange(entries.Count, 1, StoryValidator.MaxEntries);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i]!.ToJsonString().Contains("\"weather\""))
                {
                    weatherEntries++;
                    speakers.Add(dialogue["id"]!.GetValue<string>());
                    // Une réplique du temps ne passe jamais avant une quête : elle vient juste avant l'accueil sans condition.
                    Assert.Empty(entries[entries.Count - 1]!["conditions"]!.AsArray());
                    Assert.True(i >= entries.Count - 3);
                }
        }
        Assert.Equal(16, weatherEntries);
        Assert.Equal(11, speakers.Count);
        Assert.DoesNotContain("dialogue.odile", speakers);
        Assert.DoesNotContain("dialogue.gaspard", speakers);
        Assert.Contains("Mets-toi sous l'auvent", Texts.Get("text.v22.boulanger.pluie"), StringComparison.Ordinal);
    }

    [Fact]
    public void Condition_weather_refusee_au_chemin_precis_et_schema_7_encore_lu()
    {
        JsonObject root = StoryJson();
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == "dialogue.fermier");
        JsonArray entries = dialogues[d]!["entries"]!.AsArray();
        int e = entries.ToList().FindIndex(x => x!.ToJsonString().Contains("\"weather\""));
        string path = $"$.data.dialogues[{d}].entries[{e}].conditions[0]";

        void Refused(string suffix, ValidationCode code, Action<JsonObject> change)
        {
            JsonObject copy = StoryJson();
            change(copy["data"]!["dialogues"]![d]!["entries"]![e]!["conditions"]![0]!.AsObject());
            StoryLoadResult r = Read(copy);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, x => x.Path == path + suffix && x.Code == code);
        }
        Refused(".id", ValidationCode.UnknownReference, c => c["id"] = "neige");
        Refused(".value", ValidationCode.InvalidValue, c => c["value"] = 2);
        Refused(".step", ValidationCode.InvalidValue, c => c["step"] = "x");

        // En schéma 7, la condition weather est inconnue (au chemin précis) ; sans elle, le fichier est lu.
        JsonObject seven = StoryJson();
        seven["schemaVersion"] = 7;
        StoryLoadResult refused = Read(seven);
        Assert.Contains(refused.Report.Errors, x => x.Path == path + ".if" && x.Code == ValidationCode.InvalidValue && x.Message.Contains("weather"));
        foreach (JsonNode? dialogue in seven["data"]!["dialogues"]!.AsArray())
        {
            JsonArray list = dialogue!["entries"]!.AsArray();
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i]!.ToJsonString().Contains("\"weather\"")) list.RemoveAt(i);
        }
        StoryLoadResult read = Read(seven);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(7, read.Story!.SchemaVersion);
        seven["schemaVersion"] = 9;
        Assert.Contains(Read(seven).Report.Errors, x => x.Path == "$.schemaVersion" && x.Code == ValidationCode.UnsupportedSchemaVersion);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Theory]
    [InlineData(WeatherKind.Shower, "v22_pluie")]
    [InlineData(WeatherKind.Drizzle, "v22_pluie")]
    [InlineData(WeatherKind.Clear, "ch3_accueil")]
    public void Sous_la_pluie_Garance_parle_du_temps(WeatherKind kind, string node)
    {
        GroundVector at = Book(Shipped.Value).Npc("npc.fermiere").Position;
        (long day, int minute) = Find(kind);
        ZoneDefinition zone = Zone(at.X.Raw / 10000.0, at.Z.Raw / 10000.0 - 1.5);
        var w = new WorldSession(new WorldSave(Vallons, TickAt(day, minute), 0, new[] { PlayerCharacters.PlacesKey }, Hero(100)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        w.Step();
        Assert.Equal(kind, w.Weather.Kind);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.fermiere", out DialogueView? view));
        Assert.Equal(node, view!.Node);
        // La réplique garde les réponses de l'accueil (rien n'est perdu : « partir »).
        Assert.NotEmpty(view.Options);
    }

    [Fact]
    public void Repliques_du_temps_apres_toute_quete_et_avec_les_reponses_de_l_accueil()
    {
        // Relecture de la PR #101 : une réplique du temps ne passe qu'après toutes les entrées conditionnées (quêtes, livraisons, offres,
        // nuit) : seules d'autres répliques du temps et l'accueil sans condition la suivent ; son nœud garde exactement les réponses de l'accueil.
        JsonObject root = StoryJson();
        int checkedNodes = 0;
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = dialogue!["entries"]!.AsArray();
            JsonArray nodes = dialogue["nodes"]!.AsArray();
            int first = entries.ToList().FindIndex(x => x!.ToJsonString().Contains("\"weather\""));
            if (first < 0) continue;
            for (int i = first; i < entries.Count - 1; i++)
            {
                JsonArray conditions = entries[i]!["conditions"]!.AsArray();
                Assert.Single(conditions);
                Assert.Equal("weather", conditions[0]!["if"]!.GetValue<string>());
            }
            JsonObject last = entries[entries.Count - 1]!.AsObject();
            Assert.Empty(last["conditions"]!.AsArray());
            string fallback = nodes.Single(n => n!["id"]!.GetValue<string>() == last["node"]!.GetValue<string>())!["choices"]!.ToJsonString();
            for (int i = first; i < entries.Count - 1; i++)
            {
                string node = entries[i]!["node"]!.GetValue<string>();
                Assert.StartsWith("v22_", node, StringComparison.Ordinal);
                Assert.Equal(fallback, nodes.Single(n => n!["id"]!.GetValue<string>() == node)!["choices"]!.ToJsonString());
                checkedNodes++;
            }
        }
        Assert.Equal(16, checkedNodes);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Condition_weather_refusee_avant_le_schema_8(int version)
    {
        JsonObject root = StoryJson();
        root["schemaVersion"] = version;
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.ToList().FindIndex(x => x!["id"]!.GetValue<string>() == "dialogue.fermier");
        int e = dialogues[d]!["entries"]!.AsArray().ToList().FindIndex(x => x!.ToJsonString().Contains("\"weather\""));
        Assert.Contains(Read(root).Report.Errors, x => x.Path == $"$.data.dialogues[{d}].entries[{e}].conditions[0].if" && x.Code == ValidationCode.InvalidValue
            && x.Message.Contains("weather", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("npc.renart", "Renart, pour vous servir")]
    [InlineData("npc.capitaine", "La garde des Vallons, c'est moi")]
    public void Premier_contact_sous_la_pluie_la_presentation_est_dite(string npc, string presentation)
    {
        // Relecture de la PR #101 : aucun fait « déjà rencontré » (et un nœud ne pose pas de fait) : la réplique de pluie de Renart et du
        // capitaine reprend leur présentation, pour qu'un premier contact sous la pluie la donne quand même.
        GroundVector at = Book(Shipped.Value).Npc(npc).Position;
        (long day, int minute) = Find(WeatherKind.Shower);
        ZoneDefinition zone = Zone(at.X.Raw / 10000.0, at.Z.Raw / 10000.0 - 1.5);
        var w = new WorldSession(new WorldSave(Vallons, TickAt(day, minute), 0, new[] { PlayerCharacters.PlacesKey }, Hero(100)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.Equal("v22_pluie", view!.Node);
        Assert.Contains(presentation, Texts.Get(view.Text), StringComparison.Ordinal);
        Assert.Contains(presentation, Texts.Get("text.v22.capitaine.orage") + Texts.Get("text.v22.renart.pluie"), StringComparison.Ordinal);
    }

    // --- Affichage : ambiances, sons, flaques, villageois, essai ---------------------------------------------------------

    [Theory]
    [InlineData("Assets/Style/Ambience/pluie.ogg", StyleAmbience.Rain)]
    [InlineData("Assets/Style/Ambience/Rain loop.wav", StyleAmbience.Rain)]
    [InlineData("Assets/Style/Ambience/thunderstorm_rain.ogg", StyleAmbience.Rain)]
    [InlineData("Assets/Style/Ambience/averse.ogg", StyleAmbience.Rain)]
    [InlineData("Assets/Style/Ambience/orage.ogg", StyleAmbience.Thunder)]
    [InlineData("Assets/Style/Ambience/thunder_01.wav", StyleAmbience.Thunder)]
    [InlineData("Assets/Style/Ambience/Tonnerre lointain.ogg", StyleAmbience.Thunder)]
    [InlineData("Assets/Style/Ambience/Grillons de nuit.ogg", StyleAmbience.Night)]
    public void Les_sons_du_temps_sont_reconnus_par_leur_nom(string path, StyleAmbience kind) => Assert.Equal(kind, StyleRules.AmbienceOf(path));

    [Fact]
    public void Volumes_des_ambiances_sous_la_pluie()
    {
        Assert.Equal(0.0, StyleRules.AmbienceVolume(StyleAmbience.Rain, double.NaN, false, false));
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Rain, double.NaN, false, false, rain: 1) > 0.9);
        Assert.InRange(StyleRules.AmbienceVolume(StyleAmbience.Rain, double.NaN, false, false, indoors: true, rain: 1), 0.2, 0.4);   // sur le toit
        Assert.Equal(0.0, StyleRules.AmbienceVolume(StyleAmbience.Thunder, double.NaN, false, false, rain: 1));   // joué après l'éclair
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, false, false, rain: 1) < 0.2);
        Assert.Equal(1.0, StyleRules.AmbienceVolume(StyleAmbience.Birds, double.NaN, false, false));   // sans pluie, comme avant
    }

    [Fact]
    public void Sons_de_synthese_bornes_et_reproductibles()
    {
        float[] thunder = WeatherCycle.Thunder(22050, 4, 7);
        Assert.Equal(22050 * 4, thunder.Length);
        Assert.Equal(thunder, WeatherCycle.Thunder(22050, 4, 7));
        Assert.All(thunder, v => Assert.InRange(v, -1f, 1f));
        Assert.True(thunder.Max(Math.Abs) > 0.85f);
        Assert.True(Math.Abs(thunder[^1]) < 0.05f);   // il s'éteint
        float[] rain = WeatherCycle.Rain(22050, 6, 3);
        Assert.Equal(22050 * 6, rain.Length);
        Assert.All(rain, v => Assert.InRange(v, -1f, 1f));
        // La boucle ne claque pas : le passage de la fin au début n'est pas plus brusque qu'un pas ordinaire du son.
        float step = Enumerable.Range(1, rain.Length - 1).Max(i => Math.Abs(rain[i] - rain[i - 1]));
        Assert.True(Math.Abs(rain[^1] - rain[0]) <= step);
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherCycle.Thunder(1000, 4, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherCycle.Rain(22050, 0.5, 1));
    }

    [Fact]
    public void Flaques_et_villageois_qui_s_abritent()
    {
        var roads = new List<StyleRoad>
        {
            new StyleRoad("route", new List<(double X, double Z)> { (0, 0), (60, 0), (60, 40) }, 3, false),
            new StyleRoad("rue", new List<(double X, double Z)> { (-20, 5), (-20, 45) }, 4, true),
        };
        IReadOnlyList<Puddle> puddles = WeatherCycle.Puddles(roads, (x, z) => x > -30, 40);
        Assert.Equal(puddles.Select(p => (p.Id, p.X, p.Z)), WeatherCycle.Puddles(roads, (x, z) => x > -30, 40).Select(p => (p.Id, p.X, p.Z)));
        Assert.InRange(puddles.Count, 4, 40);
        Assert.Equal(puddles.Count, puddles.Select(p => p.Id).Distinct().Count());
        Assert.All(puddles, p => Assert.InRange(p.Radius, 0.45, 1.05));
        Assert.Empty(WeatherCycle.Puddles(roads, (x, z) => false, 40));
        Assert.Equal(3, WeatherCycle.Puddles(roads, (x, z) => true, 3).Count);
        Assert.Equal(0.0, WeatherCycle.PuddleSize(0.1));
        Assert.Equal(1.0, WeatherCycle.PuddleSize(0.9));
        // Sous l'averse, la plupart des villageois du décor rentrent ; un sur trois reste dehors ; les marcheurs rentrent tous.
        var idle = Enumerable.Range(0, 300).Select(i => new StyleVillager("v" + i, "paysan", VillagerActivity.Idle, 0, 0, 0)).ToList();
        Assert.InRange(idle.Count(WeatherCycle.StaysOutInRain), 60, 140);
        Assert.DoesNotContain(Enumerable.Range(0, 50).Select(i => new StyleVillager("m" + i, "paysan", VillagerActivity.Walk, 0, 0, 0, 5, 5)), WeatherCycle.StaysOutInRain);
    }

    [Fact]
    public void Essai_de_la_meteo_avance_seulement_le_temps_du_monde()
    {
        var clock = new WorldClock(50);
        foreach (WeatherKind kind in Enum.GetValues<WeatherKind>())
        {
            long tick = WeatherTrial.NextTickWith(clock, Vallons, 1234, kind)!.Value;
            Assert.True(tick >= 1234);
            Assert.Equal(kind, WorldWeather.At(Vallons, clock.At(tick)).Kind);
        }
        string path = Path.Combine(_folder, "essai", "vallons.json");
        Assert.StartsWith("Aucune sauvegarde", WeatherTrial.MoveSave(path, WeatherKind.Storm), StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var save = new WorldSave(Vallons, 500, 3, new[] { "zone.vallons/safe/place_du_village" }, Hero(42));
        SaveStore.Write(path, save);
        Assert.StartsWith("Partie avancée au jour", WeatherTrial.MoveSave(path, WeatherKind.Storm, stamp: "20261106-120000"), StringComparison.Ordinal);
        // Relecture de la PR #101 : une copie d'avant l'essai, jamais écrasée (la deuxième prend un numéro), et le drapeau de migration gardé.
        string copy = path + ".essai-20261106-120000.bak";
        Assert.Equal(save.WorldTick, SaveStore.Read(copy).Save!.WorldTick);
        WeatherTrial.MoveSave(path, WeatherKind.Clear, stamp: "20261106-120000");
        Assert.True(File.Exists(path + ".essai-20261106-120000-2.bak"));
        Assert.Equal(save.WorldTick, SaveStore.Read(copy).Save!.WorldTick);
        Assert.Contains("Copie d'avant l'essai", DayTrial.MoveSave(path, 0), StringComparison.Ordinal);
        Assert.True(File.Exists(path + ".essai-copie.bak"));
        Assert.True(DayTrial.Advanced(new WorldSave(Vallons, 5, 1, Array.Empty<string>(), Hero(1), migrated: true), 9).Migrated);
        Assert.Equal(9, DayTrial.Advanced(save, 9).WorldTick);
        SaveStore.Write(path, save);
        WeatherTrial.MoveSave(path, WeatherKind.Storm);
        WorldSave moved = SaveStore.Read(path).Save!;
        Assert.Equal(WeatherKind.Storm, WorldWeather.At(Vallons, clock.At(moved.WorldTick)).Kind);
        Assert.True(moved.WorldTick > save.WorldTick);
        Assert.Equal((save.Zone, save.Sessions, save.Character!.Money), (moved.Zone, moved.Sessions, moved.Character!.Money));
        Assert.Equal(save.Consumed, moved.Consumed);
    }
}
