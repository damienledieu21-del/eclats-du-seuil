using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Seasons;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Time;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.44.2 (saisons comme dans la vraie vie, décisions de Damien du 20 novembre 2026) : moteur 0.36.0 (commandes <c>snow</c> et
/// <c>snowEnd</c>, pas −10 % dans la neige hors des chemins, hors combat), journal 30, scénarios 24, instantané 11 ; saisons en schéma 2
/// (vigne, neige en plaine et au col, créatures de saison) ; vendanges (raisin en septembre). Référence figée
/// <c>Fixtures/reference-0.36.0-contenu-0.6.0-regles-0.18.0/espace_neige.jsonl</c>.
/// </summary>
[Trait("Category", "World")]
public class SaisonsV2442Tests
{
    private const string Neige = "espace_neige";
    private const string Latest = "reference-0.36.0-contenu-0.6.0-regles-0.18.0";

    /// <summary>Empreinte du journal figé du scénario de la neige (format 30), documentée dans MOTEUR.md.</summary>
    public const string NeigeFingerprint = "21c7b69f7e4c975f0da154086f57ae42573aec35d5e68f6c05c3909dcedd6e99";

    private static string ScenariosDir => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;

    private static ZoneDefinition Zone(double x = -18, double z = -12)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDir, ShippedData, Items).Zone!;
    }

    private static SeasonBook Book(ZoneDefinition zone) => SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone).Book!;

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, Items).Book!;

    private static long Utc(int y, int mo, int d, int h = 12) => new DateTimeOffset(y, mo, d, h, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static SeasonLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(SeasonJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return SeasonJsonReader.Read(root.ToJsonString(), Zone());
    }

    private static string FirstError(SeasonLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    private static WorldSession Play(ZoneDefinition zone, bool seasons = true)
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, Items, null, Crafting(zone));
        if (seasons) session.UseSeasons(Book(zone));
        session.Step();
        return session;
    }

    // --- Moteur -------------------------------------------------------------------------------------------------------

    private static JsonObject Json() => JsonNode.Parse(File.ReadAllText(ScenarioPath(Neige)))!.AsObject();

    private static ScenarioLoadResult Read(JsonObject root) => ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);

    private static CombatSetup Load(JsonObject? root = null)
    {
        ScenarioLoadResult result = Read(root ?? Json());
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static string[] Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log.Lines.ToArray();
    }

    private static string Kind(JsonElement e) => e.GetProperty("event").GetString()!;

    private static JsonElement[] Events(string[] lines) => lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement).ToArray();

    private static decimal X(JsonElement[] events, long tick) =>
        events.Single(e => Kind(e) == "moved" && e.GetProperty("tick").GetInt64() == tick).GetProperty("combatants")[0].GetProperty("position")[0].GetDecimal();

    [Fact]
    public void Versions_et_facteur()
    {
        Assert.Equal(("0.36.0", 30, 24, 11), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal(Fixed.FromRatio(9, 10), ShippedData.Rules.Combat.SnowSpeedFactor);
        Assert.Equal(("snow", "snowEnd"), (CombatActionIds.Of(CombatAction.Snow), CombatActionIds.Of(CombatAction.SnowEnd)));
        Assert.Equal((2, 1), (SeasonValidator.CurrentSchemaVersion, SeasonValidator.Schema1));
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", Latest, Neige + ".jsonl");
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen, Simulate(Load()));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        Assert.Equal(NeigeFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        JsonElement[] events = Events(frozen);
        Assert.Equal(0.9m, Assert.Single(events, e => Kind(e) == "snowbound").GetProperty("factor").GetDecimal());
        Assert.Single(events, e => Kind(e) == "snowEnded");
        Assert.Equal(new[] { ("snowEnd", "notSnowbound"), ("snow", "alreadySnowbound") },
            events.Where(e => Kind(e) == "commandRejected").Select(e => (e.GetProperty("action").GetString()!, e.GetProperty("reason").GetString()!)));
        // 5 m/s : 0,25 m par tick ; dans la neige 0,225 ; neige et réconfort 0,2475 (0,275 × 0,9) ; réconfort seul 0,275 ; de nouveau 0,25.
        Assert.Equal(0.25m, X(events, 20) - X(events, 19));
        Assert.Equal(0.225m, X(events, 22) - X(events, 21));
        Assert.Equal(0.2475m, X(events, 32) - X(events, 31));
        Assert.Equal(0.275m, X(events, 42) - X(events, 41));
        Assert.Equal(0.25m, X(events, 55) - X(events, 54));
    }

    [Fact]
    public void Autres_journaux_de_la_reference_0_35_0_meme_corps()
    {
        string previous = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.35.0-contenu-0.6.0-regles-0.18.0");
        string[] names = Directory.GetFiles(previous, "*.jsonl").Select(f => Path.GetFileNameWithoutExtension(f)!).ToArray();
        Assert.True(names.Length >= 12);
        foreach (string name in names)
        {
            string[] before = File.ReadAllLines(Path.Combine(previous, name + ".jsonl"));
            string[] after = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", Latest, name + ".jsonl"));
            Assert.Equal(before.Skip(1), after.Skip(1));
            Assert.Contains("\"formatVersion\":30", after[0]);
            Assert.Contains("\"engineVersion\":\"0.36.0\"", after[0]);
        }
    }

    [Fact]
    public void Validation_des_commandes_de_neige()
    {
        JsonObject old = Json();
        old["schemaVersion"] = 23;
        Assert.False(Read(old).IsValid);   // schéma 23 refusé
        JsonObject onOgre = Json();
        onOgre["data"]!["commands"]![2]!["actor"] = "ogre";
        Assert.Contains(Read(onOgre).Report.Errors, e => e.Path.EndsWith(".actor", StringComparison.Ordinal));
        JsonObject direction = Json();
        direction["data"]!["commands"]![2]!["direction"] = new JsonArray(1, 0);
        Assert.False(Read(direction).IsValid);
        JsonObject flat = Json();
        flat["data"]!.AsObject().Remove("space");
        foreach (JsonNode? c in flat["data"]!["combatants"]!.AsArray())
        {
            c!.AsObject().Remove("position");
            c.AsObject().Remove("facing");
            c.AsObject().Remove("radius");
        }
        flat["data"]!["commands"] = new JsonArray(new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "snow" });
        Assert.Contains(Read(flat).Report.Errors, e => e.Path.EndsWith(".action", StringComparison.Ordinal));
    }

    [Fact]
    public void Instantane_dans_la_neige_repris_a_l_identique_et_retouche_refusee()
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(Load(), log);
        while (engine.CurrentTick < 30) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.StartsWith("{\"snapshot\":11,", snapshot.Text);
        Assert.Contains("\"snowbound\":true", snapshot.Text);
        Assert.True(engine.View("bastion").Snowbound);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        int from = log.Lines.Count;
        while (!engine.IsFinished)
        {
            engine.Step();
            resumed.Step();
        }
        Assert.Equal(log.Lines.Skip(from), resumedLog.Lines);
        JsonObject root = Json();
        root["data"]!["commands"] = new JsonArray();
        var plain = new CombatEngine(Load(root), new MemoryCombatLog());
        while (plain.CurrentTick < 5) plain.Step();
        string tampered = plain.Snapshot().Text.Replace("\"snowbound\":false", "\"snowbound\":true");
        Assert.NotEqual(plain.Snapshot().Text, tampered);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(plain.CompletedSetup(), CombatSnapshot.Parse(tampered), new MemoryCombatLog()));
    }

    [Fact]
    public void La_neige_ne_reste_pas_posee_sur_un_mort()
    {
        // Relecture indépendante de la PR #229 : un personnage qui meurt dans la neige n'y est plus (l'instantané refuse un mort dans la neige).
        JsonObject root = Json();
        JsonObject ogre = root["data"]!["combatants"]![1]!.AsObject();
        ogre["firstAttackMs"] = 100;
        ogre["attacks"]![0]!["rawDamage"] = 100000;
        root["data"]!["combatants"]![0]!["position"] = new JsonArray(9.6, 0);
        root["data"]!["commands"] = new JsonArray(new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "snow" });
        var engine = new CombatEngine(Load(root), new MemoryCombatLog());
        engine.Step();
        Assert.True(engine.View("bastion").Snowbound);
        while (!engine.View("bastion").IsDead && !engine.IsFinished) engine.Step();
        Assert.True(engine.View("bastion").IsDead);
        Assert.False(engine.View("bastion").Snowbound);   // aucune ligne de plus au journal : l'état seul est retiré
    }

    [Fact]
    public void Lot_de_plusieurs_ticks_la_neige_suit_la_source_au_tick_pres()
    {
        JsonObject root = Json();
        root["data"]!["commands"] = new JsonArray();
        var combat = new RealtimeCombat(Load(root), "bastion") { MaxTicksPerAdvance = 30 };
        int reads = 0;
        combat.SnowSource = () => reads++ is >= 5 and < 12;
        Assert.Equal(30, combat.Advance(30 * 0.05 + 0.01));
        var sent = combat.SubmittedCommands.Where(c => c.Action == CombatAction.Snow || c.Action == CombatAction.SnowEnd).Select(c => (c.Action, c.AtMs / 50)).ToList();
        Assert.Equal(new[] { (CombatAction.Snow, 5L), (CombatAction.SnowEnd, 12L) }, sent);
    }

    // --- Saisons en schéma 2 ------------------------------------------------------------------------------------------

    [Fact]
    public void Contenu_livre_vigne_neige_creatures()
    {
        SeasonBook b = Book(Zone());
        Assert.Equal(2, b.SchemaVersion);
        Assert.Equal(new[] { "raisin_vignes_ouest", "raisin_vignes_est" }, b.Vine!.Nodes);
        Assert.Equal(VineState.Harvested, b.VineOn(new CivilDate(2026, 3, 31)));
        Assert.Equal(VineState.Green, b.VineOn(new CivilDate(2026, 4, 1)));
        Assert.Equal(VineState.Green, b.VineOn(new CivilDate(2026, 8, 31)));
        Assert.Equal(VineState.Ripe, b.VineOn(new CivilDate(2026, 9, 1)));    // vendanges : septembre
        Assert.Equal(VineState.Ripe, b.VineOn(new CivilDate(2026, 9, 30)));
        Assert.Equal(VineState.Harvested, b.VineOn(new CivilDate(2026, 10, 1)));
        Assert.Equal(VineState.Harvested, b.VineOn(new CivilDate(2028, 2, 29)));
        Assert.Null(b.VineOn(null));
        // Neige : plaine du 15 décembre au 28 février (et le 29), col du 1er novembre au 15 avril dans son lieu.
        var plain = new GroundVector(Fixed.FromInt(-18), Fixed.FromInt(-12));
        var col = new GroundVector(Fixed.FromInt(2100), Fixed.FromInt(1180));
        Assert.False(b.SnowAt(new CivilDate(2026, 12, 14), plain));
        Assert.True(b.SnowAt(new CivilDate(2026, 12, 15), plain));
        Assert.True(b.SnowAt(new CivilDate(2028, 2, 29), plain));
        Assert.False(b.SnowAt(new CivilDate(2027, 3, 1), plain));
        Assert.True(b.SnowAt(new CivilDate(2026, 11, 1), col));
        Assert.True(b.SnowAt(new CivilDate(2027, 4, 15), col));
        Assert.False(b.SnowAt(new CivilDate(2027, 4, 16), col));
        Assert.False(b.SnowAt(new CivilDate(2026, 10, 31), col));
        Assert.False(b.SnowAt(null, col));
        Assert.Equal(Fixed.FromInt(220), b.Snow!.PassAltitude);   // V2.44.3 : limite de la neige du col à 220 m (relief : col à 320 m)
        // Créatures : les ours hibernent, deux loups de l hiver.
        Assert.False(b.CreatureSeasons["ours"].IsActive(Season.Winter));
        Assert.True(b.CreatureSeasons["ours"].IsActive(Season.Autumn));
        Assert.Equal(2, b.CreatureSeasons.Count(c => c.Key.StartsWith("loup_hiver_", StringComparison.Ordinal) && c.Value.Mask == 1 << (int)Season.Winter));
    }

    [Fact]
    public void Version_1_encore_lue_sans_vigne_ni_neige()
    {
        SeasonLoadResult v1 = Mutated(r =>
        {
            r["schemaVersion"] = 1;
            foreach (string key in new[] { "vine", "snow", "creatures" }) r["data"]!.AsObject().Remove(key);
        });
        Assert.True(v1.IsValid, v1.Report.ToString());
        Assert.Null(v1.Book!.Vine);
        Assert.False(v1.Book.SnowAt(new CivilDate(2027, 1, 10), new GroundVector(Fixed.Zero, Fixed.Zero)));
        Assert.Empty(v1.Book.CreatureSeasons);
        Assert.Equal("$.data.vine", FirstError(Mutated(r => { r["schemaVersion"] = 1; r["data"]!.AsObject().Remove("snow"); r["data"]!.AsObject().Remove("creatures"); })));
        Assert.Equal("$.data.snow", FirstError(Mutated(r => r["data"]!.AsObject().Remove("snow"))));
    }

    [Theory]
    [InlineData("vine.ripe", "{\"month\":2,\"day\":29}", "$.data.vine.ripe")]
    [InlineData("vine.ripe", "{\"month\":13,\"day\":1}", "$.data.vine.ripe")]
    [InlineData("vine.harvested", "{\"month\":8,\"day\":1}", "$.data.vine")]
    [InlineData("vine.rows", "\"rang_inconnu_\"", "$.data.vine.rows")]
    [InlineData("vine.nodes", "[]", "$.data.vine.nodes")]
    [InlineData("vine.nodes", "[\"a\",\"a\"]", "$.data.vine.nodes[1]")]
    [InlineData("snow.pass.places", "[\"lieu_inconnu\"]", "$.data.snow.pass.places[0]")]
    [InlineData("snow.pass.altitude", "501", "$.data.snow.pass.altitude")]
    [InlineData("snow.pass.to", "{\"month\":1,\"day\":31}", "$.data.snow.pass")]
    [InlineData("creatures", "[{\"creature\":\"inconnue\",\"seasons\":[\"winter\"]}]", "$.data.creatures[0].creature")]
    [InlineData("creatures", "[{\"creature\":\"ours\",\"seasons\":[]}]", "$.data.creatures[0].seasons")]
    [InlineData("creatures", "[{\"creature\":\"ours\",\"seasons\":[\"winter\",\"winter\"]}]", "$.data.creatures[0].seasons[1]")]
    [InlineData("creatures", "[{\"creature\":\"loup_blanc\",\"seasons\":[\"winter\"]}]", "$.data.creatures[0].creature")]
    [InlineData("creatures", "[{\"creature\":\"ours\",\"seasons\":[\"hiver\"]}]", "$.data.creatures[0].seasons[0]")]
    public void Refus_avec_chemin_precis(string at, string json, string path)
    {
        SeasonLoadResult r = Mutated(root =>
        {
            string[] keys = at.Split('.');
            JsonNode node = root["data"]!;
            for (int i = 0; i < keys.Length - 1; i++) node = node[keys[i]]!;
            node[keys[^1]] = JsonNode.Parse(json);
        });
        Assert.Equal(path, FirstError(r));
    }

    [Fact]
    public void Noeuds_de_la_vigne_verifies_contre_les_metiers()
    {
        // Revue Codex de la PR #229 : une faute de frappe dans vine.nodes est refusée avec son chemin (sinon le vrai nœud resterait ouvert
        // tout l'automne) ; un nœud de toute l'année aussi.
        ZoneDefinition zone = Zone();
        CraftingBook crafting = Crafting(zone);
        string json = File.ReadAllText(SeasonJsonReader.PathIn(DataDirectory));
        Assert.True(SeasonJsonReader.Read(json, zone, crafting).IsValid);
        SeasonLoadResult typo = SeasonJsonReader.Read(json.Replace("\"raisin_vignes_ouest\"", "\"raisin_vignes_oues\""), zone, crafting);
        Assert.Equal("$.data.vine.nodes[0]", FirstError(typo));
        Assert.Equal(EdS.Data.Validation.ValidationCode.UnknownReference, typo.Report.Errors[0].Code);
        string allYear = crafting.Nodes.First(n => !n.IsSeasonal).Id;
        SeasonLoadResult plain = SeasonJsonReader.Read(json.Replace("\"raisin_vignes_est\"", "\"" + allYear + "\""), zone, crafting);
        Assert.Equal("$.data.vine.nodes[1]", FirstError(plain));
        // Sans catalogue des métiers : contrôle croisé non fait (le contenu saisonnier seul reste lisible).
        Assert.True(SeasonJsonReader.Read(json.Replace("\"raisin_vignes_ouest\"", "\"raisin_vignes_oues\""), zone).IsValid);
        // La partie charge les saisons avec les métiers.
        Assert.Contains("SeasonJsonReader.ReadFile(seasonsPath, definition, craftingBook)",
            File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "src", "EdS.Runtime", "AdventureSession.cs")));
    }

    [Fact]
    public void Sur_un_chemin_au_bord_compris()
    {
        ZoneDefinition zone = Zone();
        Road road = zone.Roads.Single(r => r.Id == "grand_rue");
        // Segment horizontal (-8, 1) → (-16, 1), largeur 4,5 m : à 2,25 m de l'axe on est encore sur le chemin, un dix-millième au-delà non.
        var on = new GroundVector(Fixed.FromInt(-12), Fixed.FromRaw(Fixed.FromInt(1).Raw + road.Width.Raw / 2));
        var off = new GroundVector(Fixed.FromInt(-12), Fixed.FromRaw(Fixed.FromInt(1).Raw + road.Width.Raw / 2 + 1));
        Assert.Equal(new GroundVector(Fixed.FromInt(-16), Fixed.FromInt(1)), road.Points[2]);
        Assert.True(SeasonSnow.OnRoad(new[] { road }, on));
        Assert.False(SeasonSnow.OnRoad(new[] { road }, off));
        Assert.True(SeasonSnow.OnRoad(zone.Roads, road.Points[0]));
        Assert.False(SeasonSnow.OnRoad(Array.Empty<Road>(), on));
        Assert.Throws<ArgumentNullException>(() => SeasonSnow.OnRoad(null!, on));
    }

    private static Road RoadOf(double width, params (double X, double Z)[] points) =>
        (Road)typeof(Road).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single()
            .Invoke(new object?[] { "essai", RoadKind.Dirt, Fixed.FromRaw((long)(width * 10000)),
                points.Select(p => new GroundVector(Fixed.FromRaw((long)(p.X * 10000)), Fixed.FromRaw((long)(p.Z * 10000)))).ToList() });

    private static GroundVector At(decimal x, decimal z) => new GroundVector(Fixed.FromRaw((long)(x * 10000)), Fixed.FromRaw((long)(z * 10000)));

    [Fact]
    public void Sur_un_chemin_aux_bornes_extremes_sans_debordement()
    {
        // Revue Codex de la PR #229 : segment de 8 km et point à 4 km de l'axe (cross² ≈ 10³¹, au-delà d'un decimal) : exact, sans débordement.
        Road far = RoadOf(20, (-4000, 0), (4000, 0));
        Assert.False(SeasonSnow.OnRoad(new[] { far }, At(0, 4000)));
        Assert.True(SeasonSnow.OnRoad(new[] { far }, At(0, 10)));          // bord compris (demi-largeur 10 m)
        Assert.False(SeasonSnow.OnRoad(new[] { far }, At(0, 10.0001m)));   // un dix-millième au-delà
        Assert.True(SeasonSnow.OnRoad(new[] { far }, At(-4010, 0)));       // au bout, dans le disque
        Assert.False(SeasonSnow.OnRoad(new[] { far }, At(-4010.0001m, 0)));
        // Diagonale d'un coin à l'autre de la borne de ±4 096 m.
        Road diagonal = RoadOf(20, (-4096, -4096), (4096, 4096));
        Assert.True(SeasonSnow.OnRoad(new[] { diagonal }, At(0, 0)));
        Assert.True(SeasonSnow.OnRoad(new[] { diagonal }, At(7, -7)));     // à 9,9 m de l'axe
        Assert.False(SeasonSnow.OnRoad(new[] { diagonal }, At(8, -8)));    // à 11,3 m
        Assert.False(SeasonSnow.OnRoad(new[] { diagonal }, At(4096, -4096)));
        Assert.False(SeasonSnow.OnRoad(new[] { diagonal }, At(-4096, 4096)));
        // Déterministe : mêmes réponses à chaque appel.
        Assert.Equal(SeasonSnow.OnRoad(new[] { diagonal }, At(7, -7)), SeasonSnow.OnRoad(new[] { diagonal }, At(7, -7)));
    }

    // --- Monde -------------------------------------------------------------------------------------------------------

    [Fact]
    public void La_neige_ralentit_hors_des_chemins_hors_combat_seulement()
    {
        ZoneDefinition zone = Zone(-18, -20);
        WorldSession w = Play(zone);
        GroundVector at = w.Zone.Engine.View(w.CombatantId).Position!.Value;
        Assert.False(SeasonSnow.OnRoad(zone.Roads, at), "le point de départ du test doit être hors des chemins");
        Assert.False(w.SnowWanted);   // sans date fournie : jamais de neige
        w.SetCivilTime(Utc(2027, 1, 10));
        Assert.True(w.SnowUnderPlayer);
        Assert.True(w.SnowWanted);
        w.SetCivilTime(Utc(2027, 7, 10));
        Assert.False(w.SnowWanted);   // l'été, pas de neige
        // Sur un chemin : aucun effet.
        Road road = zone.Roads.First(r => r.Id == "grand_rue");
        WorldSession onRoad = Play(Zone(road.Points[1].X.Raw / 10000.0, road.Points[1].Z.Raw / 10000.0));
        onRoad.SetCivilTime(Utc(2027, 1, 10));
        Assert.True(onRoad.SnowUnderPlayer);
        Assert.False(onRoad.SnowWanted);
        // En combat : plus voulue (le pont envoie snowEnd).
        w.SetCivilTime(Utc(2027, 1, 10));
        Assert.True(w.SnowWanted);
        w.Zone.Leave(w.CombatantId);
        w.Step();
        Assert.True(w.InCombat);
        Assert.False(w.SnowWanted);
        // Relecture indépendante de la PR #229 : jamais dans un intérieur, jamais dans une autre zone que celle du contenu saisonnier.
        foreach (string other in new[] { "zone.boulangerie", "zone.auberge" })
        {
            ZoneDefinition inside = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, other), ScenariosDir, ShippedData, Items).Zone!;
            Assert.True(inside.Indoor);
            SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
            var room = new WorldSession(new WorldSave(inside.Id, 0, 0, Array.Empty<string>(), hero), inside, ShippedData, Progression, Items);
            room.UseSeasons(Book(zone));
            room.Step();
            room.SetCivilTime(Utc(2027, 1, 10));
            Assert.False(room.SnowUnderPlayer);
            Assert.False(room.SnowWanted);
        }
        JsonObject renamed = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        renamed["data"]!["id"] = "zone.vallons_bis";
        renamed["data"]!["safePoints"]![0]!["position"] = new JsonArray(-18, -20);
        ZoneDefinition elsewhere = ZoneJsonReader.Read(renamed.ToJsonString(), ScenariosDir, ShippedData, Items).Zone!;
        Assert.Equal("zone.vallons_bis", elsewhere.Id);
        SavedCharacter walker = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var away = new WorldSession(new WorldSave(elsewhere.Id, 0, 0, Array.Empty<string>(), walker), elsewhere, ShippedData, Progression, Items);
        away.UseSeasons(Book(zone));
        away.Step();
        away.SetCivilTime(Utc(2027, 1, 10));
        Assert.False(away.SnowWanted);
        Assert.Throws<ArgumentException>(() => Book(zone).ApplyTo(elsewhere));
        // Sans contenu saisonnier : jamais.
        WorldSession bare = Play(zone, seasons: false);
        bare.SetCivilTime(Utc(2027, 1, 10));
        Assert.False(bare.SnowWanted);
    }

    [Fact]
    public void Vendanges_raisin_en_septembre_seulement()
    {
        WorldSession w = Play(Zone());
        Assert.False(w.NodeInSeason("raisin_vignes_ouest"));   // sans date
        w.SetCivilTime(Utc(2026, 8, 31));
        Assert.Equal(VineState.Green, w.VineNow);
        Assert.False(w.NodeInSeason("raisin_vignes_ouest"));
        w.SetCivilTime(Utc(2026, 9, 15));
        Assert.Equal(VineState.Ripe, w.VineNow);
        Assert.True(w.NodeInSeason("raisin_vignes_ouest"));
        Assert.True(w.NodeInSeason("raisin_vignes_est"));
        w.SetCivilTime(Utc(2026, 10, 15));
        Assert.Equal(VineState.Harvested, w.VineNow);
        Assert.False(w.NodeInSeason("raisin_vignes_ouest"));
        Assert.True(w.NodeInSeason("cepes_foret"));   // les cèpes restent tout l'automne
        // Repousse comme les cèpes.
        CraftingBook crafting = Crafting(Zone());
        long cepes = crafting.Nodes.Single(n => n.Id == "cepes_foret").RespawnMs;
        Assert.All(crafting.Nodes.Where(n => n.Id.StartsWith("raisin_", StringComparison.Ordinal)), n => Assert.Equal(cepes, n.RespawnMs));
    }

    [Fact]
    public void Les_ours_hibernent_et_les_loups_de_l_hiver_arrivent()
    {
        ZoneDefinition plain = Zone();
        SeasonBook book = Book(plain);
        ZoneDefinition zone = book.ApplyTo(plain);
        Assert.Null(plain.Populations.Single(p => p.Creature == "ours").Seasons);   // la zone d'origine reste telle quelle
        Population ours = zone.Populations.Single(p => p.Creature == "ourse");
        Population loup = zone.Populations.Single(p => p.Creature == "loup_hiver_foret1");
        Assert.True(ours.Timed && loup.Timed);
        Assert.Same(ours.Seasons, zone.ForLevel(10).Populations.Single(p => p.Creature == "ourse").Seasons);
        WorldSession w = Play(zone);
        w.SetCivilTime(Utc(2027, 1, 10, 13));
        Assert.False(w.IsAvailable(ours));
        Assert.True(w.IsAvailable(loup));
        w.SetCivilTime(Utc(2027, 7, 10, 13));
        Assert.True(w.IsAvailable(ours));
        Assert.False(w.IsAvailable(loup));
        // Au début d'une session : présentes seulement en leur saison.
        Assert.False(ZonePopulationsStart(loup, Season.Summer));
        Assert.True(ZonePopulationsStart(loup, Season.Winter));
        Assert.True(ZonePopulationsStart(loup, null));
        // Une zone qui n'a pas la créature : refusé, rien ne change.
        Assert.Throws<ArgumentException>(() => book.ApplyTo(WithoutWolf()));
    }

    private static bool ZonePopulationsStart(Population p, Season? season) =>
        ZoneSetups.For(Book(Zone()).ApplyTo(Zone()), Array.Empty<string>(), null, null, season).Combatants.Any(c => c.Id == p.Creature);

    private static ZoneDefinition WithoutWolf()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        var pops = root["data"]!["populations"]!.AsArray();
        foreach (var p in pops.Where(p => (string)p!["creature"]! == "loup_hiver_foret1").ToList()) pops.Remove(p);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDir, ShippedData, Items).Zone!;
    }
}
