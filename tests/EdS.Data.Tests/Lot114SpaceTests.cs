using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4, première étape du moteur spatial : combat positionné (bloc <c>space</c> du scénario, schéma 5), déplacement par
/// intentions à vitesse bornée (ralenti en garde et par le gel), bords solides, portée des compétences à cible vérifiée au lancement
/// (refus <c>targetOutOfRange</c>), journal au format 10. Données livrées (règles 0.9.0, Bastion 0.6.0).
/// </summary>
[Trait("Category", "Space")]
[Trait("Category", "Data")]
public class Lot114SpaceTests
{
    // --- Construction des scénarios ---------------------------------------------------------------------------------------

    private static string S(string s) => $"skill.bastion.{s}";

    private static JsonObject Point(string x, string z) => new() { ["x"] = JsonValue.Create(decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture)), ["z"] = JsonValue.Create(decimal.Parse(z, System.Globalization.CultureInfo.InvariantCulture)) };

    private static JsonObject Place(JsonObject combatant, string x, string z, string radius, JsonObject? facing = null)
    {
        combatant["position"] = Point(x, z);
        if (facing != null) combatant["facing"] = facing;
        combatant["radius"] = JsonValue.Create(decimal.Parse(radius, System.Globalization.CultureInfo.InvariantCulture));
        return combatant;
    }

    private static JsonObject Hero(string id = "bastion", string team = "joueurs") => new()
    {
        ["id"] = id, ["type"] = "character", ["team"] = team, ["initiative"] = 0, ["class"] = "class.bastion",
        ["build"] = new JsonObject
        {
            ["level"] = 30,
            ["attributes"] = new JsonObject { ["force"] = 30, ["dexterite"] = 10, ["intelligence"] = 6, ["vitalite"] = 30, ["volonte"] = 15, ["endurance"] = 20 },
            ["talents"] = new JsonObject(),
            ["activeSkills"] = new JsonArray(S("frappe_du_rempart"), S("provocation"), S("barriere_d_acier"), S("onde_de_rupture")),
            ["ultimate"] = null,
            ["passiveSkills"] = new JsonArray(),
        },
    };

    private static JsonObject Dummy(string id = "mannequin") => new()
    {
        ["id"] = id, ["type"] = "dummy", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0,
    };

    private static JsonObject Assailant(string target, string id = "assaillant", long firstMs = 0, long intervalMs = 1000, long raw = 0, string? applies = null,
        long? health = null)
    {
        var a = new JsonObject
        {
            ["id"] = id, ["type"] = "assailant", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0, ["target"] = target,
            ["firstAttackMs"] = firstMs, ["intervalMs"] = intervalMs,
            ["attacks"] = new JsonArray(new JsonObject
            {
                ["id"] = "coup", ["rawDamage"] = raw, ["damageType"] = "physique", ["targeting"] = "enemy", ["unblockable"] = true,
                ["applies"] = applies is null ? new JsonArray() : new JsonArray(new JsonObject { ["status"] = applies }),
            }),
        };
        if (health is long h) a["health"] = h;
        return a;
    }

    private static JsonObject Move(long atMs, string x, string z, string actor = "bastion") =>
        new() { ["atMs"] = atMs, ["actor"] = actor, ["action"] = "move", ["direction"] = Point(x, z) };

    private static JsonObject Act(long atMs, string action, string actor = "bastion") => new() { ["atMs"] = atMs, ["actor"] = actor, ["action"] = action };

    private static JsonObject Cast(long atMs, string skill, string? target, string actor = "bastion") =>
        new() { ["atMs"] = atMs, ["actor"] = actor, ["skill"] = S(skill), ["target"] = target };

    private static JsonObject Bounds(int minX = -20, int maxX = 20, int minZ = -20, int maxZ = 20) =>
        new() { ["bounds"] = new JsonObject { ["minX"] = minX, ["maxX"] = maxX, ["minZ"] = minZ, ["maxZ"] = maxZ } };

    private static string Json(IEnumerable<JsonObject> combatants, IEnumerable<JsonObject> commands, long durationMs = 2000, JsonObject? space = null, long seed = 1)
    {
        var data = new JsonObject
        {
            ["seed"] = seed, ["durationMs"] = durationMs,
            ["combatants"] = new JsonArray(combatants.Select(c => (JsonNode?)c).ToArray()),
            ["commands"] = new JsonArray(commands.Select(c => (JsonNode?)c).ToArray()),
        };
        if (space != null) data["space"] = space;
        return new JsonObject { ["schemaVersion"] = 5, ["kind"] = "scenario", ["contentVersion"] = "0.1.0-test", ["data"] = data }.ToJsonString();
    }

    private static ScenarioLoadResult Read(string json) => ScenarioJsonReader.Read(json, ShippedData);

    private static (CombatResult Result, List<JsonElement> Lines, MemoryCombatLog Log) Run(string json)
    {
        ScenarioLoadResult loaded = Read(json);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(loaded.Setup!, log);
        return (result, log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement).ToList(), log);
    }

    private static IEnumerable<JsonElement> Of(List<JsonElement> lines, string kind) => lines.Where(l => l.GetProperty("event").GetString() == kind);

    /// <summary>
    /// Dernière position journalisée de <paramref name="id"/> au tick <paramref name="tick"/> ou avant ; placement de départ (ligne combatant)
    /// sinon, y compris pour un tick négatif (positions vues par les commandes du tick 0).
    /// </summary>
    private static (decimal X, decimal Z) PositionAt(List<JsonElement> lines, string id, long tick)
    {
        (decimal, decimal) position = default;
        foreach (JsonElement l in lines)
        {
            string kind = l.GetProperty("event").GetString()!;
            if (kind != "combatant" && l.GetProperty("tick").GetInt64() > tick) break;   // placement de départ : toujours lu
            if (kind == "combatant" && l.GetProperty("combatant").GetString() == id)
                position = (l.GetProperty("x").GetDecimal(), l.GetProperty("z").GetDecimal());
            else if (kind == "moved")
                foreach (JsonElement p in l.GetProperty("positions").EnumerateArray())
                    if (p.GetProperty("combatant").GetString() == id) position = (p.GetProperty("x").GetDecimal(), p.GetProperty("z").GetDecimal());
        }
        return position;
    }

    private static void AssertError(ValidationReport report, string path, ValidationCode? code = null) =>
        Assert.Contains(report.Errors, e => e.Path == path && (code is null || e.Code == code));

    private static string SpaceScenario(IEnumerable<JsonObject> commands, string dummyX = "10", long durationMs = 2000, JsonObject? space = null) =>
        Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Dummy(), dummyX, "0", "0.6") }, commands, durationMs, space ?? Bounds());

    // --- Scénario livré -----------------------------------------------------------------------------------------------------

    private static string ApprochePath => ScenarioPath("espace/approche");
    private static string ApprocheFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.16.0-contenu-0.6.0", "espace_approche.jsonl");

    [Fact]
    [Trait("Category", "Regression")]
    public void Approche_livree_journal_fige_octet_par_octet_et_rejoue()
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ApprochePath, ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(File.ReadAllText(ApprocheFixture), log.ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(ApprocheFixture), ShippedData).Status);
    }

    [Fact]
    public void Approche_livree_hors_de_portee_puis_a_portee_au_tick_pres()
    {
        string json = File.ReadAllText(ApprochePath);
        var (result, lines, _) = Run(json);
        // Bastion (0, 0), rayon 0,5 ; mannequin (10, 0), rayon 0,6 : bords à 8,9 m ; Frappe du rempart à 2,5 m.
        var rejected = Of(lines, "commandRejected").Select(l => (l.GetProperty("tick").GetInt64(), l.GetProperty("reason").GetString()!)).ToList();
        Assert.Equal(new[] { (0L, "targetOutOfRange"), (25L, "targetOutOfRange"), (180L, "notMoving"), (190L, "targetOutOfRange") }, rejected);
        // Au tick 25, le Bastion est à 6,25 m (25 pas de 0,25 m) : bords à 2,65 m. Au tick 26, à 6,5 m : 2,4 m, la frappe part.
        Assert.Equal((6.25m, 0m), PositionAt(lines, "bastion", 24));
        Assert.Equal((6.5m, 0m), PositionAt(lines, "bastion", 25));
        Assert.Contains(Of(lines, "skillCast"), l => l.GetProperty("tick").GetInt64() == 26 && l.GetProperty("skill").GetString() == S("frappe_du_rempart"));
        // En garde (2 à 4 s), demi-vitesse : 0,125 m par tick, 5 m en 40 ticks ; puis la Provocation (15 m) porte de 7,4 m.
        Assert.Equal((6.375m, 0m), PositionAt(lines, "bastion", 40));
        Assert.Equal((1.5m, 0m), PositionAt(lines, "bastion", 79));
        Assert.Contains(Of(lines, "skillCast"), l => l.GetProperty("tick").GetInt64() == 80 && l.GetProperty("skill").GetString() == S("provocation"));
        // Vers +z, le bord (10 m, rayon 0,5) arrête le Bastion à 9,5 m : plus aucune ligne moved une fois contre le bord.
        Assert.Equal((1.5m, 9.5m), PositionAt(lines, "bastion", 199));
        Assert.Equal(157, Of(lines, "moved").Max(l => l.GetProperty("tick").GetInt64()));
        Assert.Equal((2L, 4L), (result.Combatants.Single(c => c.Id == "bastion").SkillsCast, result.Combatants.Single(c => c.Id == "bastion").CommandsRejected));
    }

    [Fact]
    public void Journal_au_format_10_placement_dans_l_en_tete_et_la_ligne_combatant()
    {
        var (_, lines, log) = Run(SpaceScenario(new[] { Move(0, "1", "0") }, durationMs: 100));
        JsonElement header = JsonDocument.Parse(log.Lines[0]).RootElement;
        Assert.Equal((10, "0.16.0"), (header.GetProperty("formatVersion").GetInt32(), header.GetProperty("engineVersion").GetString()));
        JsonElement scenario = header.GetProperty("scenario");
        Assert.Equal("{\"bounds\":{\"minX\":-20,\"maxX\":20,\"minZ\":-20,\"maxZ\":20}}", scenario.GetProperty("space").GetRawText());
        JsonElement bastion = scenario.GetProperty("combatants")[0];
        Assert.Equal(("{\"x\":0,\"z\":0}", "{\"x\":0,\"z\":1}", 0.5m),
            (bastion.GetProperty("position").GetRawText(), bastion.GetProperty("facing").GetRawText(), bastion.GetProperty("radius").GetDecimal()));
        Assert.Equal("{\"atMs\":0,\"actor\":\"bastion\",\"action\":\"move\",\"direction\":{\"x\":1,\"z\":0}}", scenario.GetProperty("commands")[0].GetRawText());
        JsonElement line = Of(lines, "combatant").First();
        Assert.Equal((0m, 0m, 0m, 1m, 0.5m), (line.GetProperty("x").GetDecimal(), line.GetProperty("z").GetDecimal(),
            line.GetProperty("facingX").GetDecimal(), line.GetProperty("facingZ").GetDecimal(), line.GetProperty("radius").GetDecimal()));
        JsonElement started = Of(lines, "movementStarted").Single();
        Assert.Equal(("command:0", 1m, 0m), (started.GetProperty("root").GetString(), started.GetProperty("directionX").GetDecimal(), started.GetProperty("directionZ").GetDecimal()));
        JsonElement moved = Of(lines, "moved").First();
        Assert.Equal("movement", moved.GetProperty("phase").GetString());
        Assert.False(moved.TryGetProperty("eid", out _));   // enveloppe : ni effet ni provenance
        Assert.Equal("[{\"combatant\":\"bastion\",\"x\":0.25,\"z\":0,\"facingX\":1,\"facingZ\":0}]", moved.GetProperty("positions").GetRawText());
    }

    [Fact]
    [Trait("Category", "Regression")]
    public void Combat_sans_espace_aucun_champ_ni_ligne_de_l_espace()
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(MannequinPath, ShippedData).Setup!, log);
        string text = log.ToString();
        foreach (string absent in new[] { "\"space\"", "\"position\"", "\"radius\"", "\"facingX\"", "\"moved\"", "\"movement", "targetOutOfRange" })
            Assert.DoesNotContain(absent, text, StringComparison.Ordinal);
    }

    // --- Déplacement ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Vitesse_de_base_puis_garde_a_moitie_au_tick_pres()
    {
        var (_, lines, _) = Run(SpaceScenario(new[] { Move(0, "0", "-2"), Act(500, "guardStart"), Act(1000, "moveStop") }));
        // 5 m/s : 0,25 m par tick dès le tick de la commande (ticks 0 à 9) ; en garde dès le tick 10 : 0,125 m.
        Assert.Equal((0m, -0.25m), PositionAt(lines, "bastion", 0));
        Assert.Equal((0m, -2.5m), PositionAt(lines, "bastion", 9));
        Assert.Equal((0m, -2.625m), PositionAt(lines, "bastion", 10));
        Assert.Equal((0m, -3.75m), PositionAt(lines, "bastion", 19));
        Assert.Equal((0m, -3.75m), PositionAt(lines, "bastion", 39));   // arrêt au tick 20, avant le déplacement du tick
        Assert.Equal(20, Of(lines, "moved").Count());
        Assert.All(Of(lines, "moved"), l => Assert.Equal("[0,-1]", $"[{l.GetProperty("positions")[0].GetProperty("facingX")},{l.GetProperty("positions")[0].GetProperty("facingZ")}]"));
    }

    [Fact]
    public void Gel_ralentit_de_30_pourcent()
    {
        // L'assaillant gèle le Bastion à son premier tour (tick 0, après le déplacement du tick) : 0,175 m par tick ensuite.
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Assailant("bastion", applies: "freeze"), "5", "5", "0.6") },
            new[] { Move(0, "1", "0") }, durationMs: 500, space: Bounds()));
        Assert.Contains(Of(lines, "stateApplied"), l => l.GetProperty("state").GetString() == "freeze" && l.GetProperty("tick").GetInt64() == 0);
        Assert.Equal((0.25m, 0m), PositionAt(lines, "bastion", 0));
        Assert.Equal((0.425m, 0m), PositionAt(lines, "bastion", 1));
        Assert.Equal((0.25m + 9 * 0.175m, 0m), PositionAt(lines, "bastion", 9));
    }

    [Fact]
    public void Bords_solides_glissement_le_long_du_bord()
    {
        // Diagonale vers le coin (5, 5) d'un espace de 10 m de côté : le Bastion longe le bord z puis s'arrête dans le coin.
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "3", "3", "0.5"), Place(Dummy(), "-4", "-4", "0.6") },
            new[] { Move(0, "1", "2") }, durationMs: 1500, space: Bounds(-5, 5, -5, 5)));
        Assert.Equal((4.5m, 4.5m), PositionAt(lines, "bastion", 1499 / 50));
        (decimal X, decimal Z)[] all = Of(lines, "moved").Select(l => (l.GetProperty("positions")[0].GetProperty("x").GetDecimal(),
            l.GetProperty("positions")[0].GetProperty("z").GetDecimal())).ToArray();
        Assert.All(all, p => Assert.True(p.X <= 4.5m && p.Z <= 4.5m));
        Assert.Contains(all, p => p.Z == 4.5m && p.X < 4.5m);   // glissement : z au bord, x avance encore
        Assert.Equal(all.Length, all.Distinct().Count());       // une ligne seulement quand la position change
    }

    [Fact]
    public void Mort_arrete_le_deplacement_et_les_commandes_de_deplacement()
    {
        // Un allié mortel est tué au tick 0 (attaque énorme) pendant qu'il marche ; il ne bouge plus ensuite, sa commande suivante est refusée.
        JsonObject ally = Place(Hero("allie"), "0", "2", "0.5");
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), ally, Place(Assailant("allie", raw: 1_000_000), "5", "5", "0.6") },
            new[] { Move(0, "1", "0", "allie"), Move(100, "0", "1", "allie"), Act(150, "moveStop", "allie") }, durationMs: 500, space: Bounds()));
        Assert.Contains(Of(lines, "died"), l => l.GetProperty("combatant").GetString() == "allie" && l.GetProperty("tick").GetInt64() == 0);
        Assert.Equal(new[] { 0L }, Of(lines, "moved").Select(l => l.GetProperty("tick").GetInt64()));
        Assert.Equal(new[] { "actorDead", "actorDead" }, Of(lines, "commandRejected").Select(l => l.GetProperty("reason").GetString()));
    }

    [Fact]
    public void Mannequin_et_assaillant_ne_se_deplacent_pas()
    {
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Dummy(), "4", "0", "0.6"), Place(Assailant("bastion"), "-4", "0", "0.6") },
            new[] { Move(0, "1", "0", "mannequin"), Move(0, "1", "0", "assaillant"), Act(50, "moveStop", "bastion") }, durationMs: 200, space: Bounds()));
        Assert.Equal(new[] { "actorCannotAct", "actorCannotAct", "notMoving" }, Of(lines, "commandRejected").Select(l => l.GetProperty("reason").GetString()));
        Assert.Empty(Of(lines, "moved"));
    }

    [Fact]
    public void Proprietes_sur_deplacements_aleatoires_bornes_vitesse_et_rejeu()
    {
        int moves = 0, casts = 0, outOfRange = 0, walls = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var random = new Random(seed);
            string R(double min, double max) => (min + random.NextDouble() * (max - min)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            var commands = new List<JsonObject>();
            for (long at = 0; at < 3000; at += 50 * random.Next(1, 12))
            {
                int kind = random.Next(6);
                commands.Add(kind switch
                {
                    0 => Act(at, "guardStart"),
                    1 => Act(at, "guardStop"),
                    2 => Act(at, "moveStop"),
                    3 => Cast(at, "frappe_du_rempart", "mannequin"),
                    _ => Move(at, R(-3, 3), R(-3, 3)),
                });
            }
            commands.RemoveAll(c => c["action"]?.GetValue<string>() == "move" && c["direction"]!["x"]!.GetValue<decimal>() == 0 && c["direction"]!["z"]!.GetValue<decimal>() == 0);
            string json = Json(new[] { Place(Hero(), R(-7, 7), R(-7, 7), "0.5"), Place(Dummy(), R(-7, 7), R(-7, 7), "0.6") }, commands, 3000,
                Bounds(-8, 8, -8, 8), seed);
            var (_, lines, log) = Run(json);
            (decimal X, decimal Z) previous = (Of(lines, "combatant").First().GetProperty("x").GetDecimal(), Of(lines, "combatant").First().GetProperty("z").GetDecimal());
            foreach (JsonElement moved in Of(lines, "moved"))
            {
                JsonElement p = moved.GetProperty("positions").EnumerateArray().Single();
                (decimal X, decimal Z) now = (p.GetProperty("x").GetDecimal(), p.GetProperty("z").GetDecimal());
                Assert.InRange(now.X, -7.5m, 7.5m);
                Assert.InRange(now.Z, -7.5m, 7.5m);
                decimal dx = now.X - previous.X, dz = now.Z - previous.Z;
                Assert.True(dx * dx + dz * dz <= 0.0625m, $"graine {seed} : pas de {dx}, {dz} au tick {moved.GetProperty("tick")}");
                if (Math.Abs(now.X) == 7.5m || Math.Abs(now.Z) == 7.5m) walls++;
                moves++;
                previous = now;
            }
            // Portée : chaque frappe acceptée l'a été à 2,5 m au plus entre les bords, avec les positions du tick précédent.
            foreach (JsonElement cast in Of(lines, "skillCast"))
            {
                long tick = cast.GetProperty("tick").GetInt64();
                var (bx, bz) = PositionAt(lines, "bastion", tick - 1);
                var (mx, mz) = PositionAt(lines, "mannequin", tick - 1);
                decimal reach = 2.5m + 0.5m + 0.6m;
                Assert.True((bx - mx) * (bx - mx) + (bz - mz) * (bz - mz) <= reach * reach, $"graine {seed}, tick {tick}");
                casts++;
            }
            foreach (JsonElement refused in Of(lines, "commandRejected").Where(l => l.GetProperty("reason").GetString() == "targetOutOfRange"))
            {
                long tick = refused.GetProperty("tick").GetInt64();
                var (bx, bz) = PositionAt(lines, "bastion", tick - 1);
                var (mx, mz) = PositionAt(lines, "mannequin", tick - 1);
                decimal reach = 2.5m + 0.5m + 0.6m;
                Assert.True((bx - mx) * (bx - mx) + (bz - mz) * (bz - mz) > reach * reach, $"graine {seed}, tick {tick}");
                outOfRange++;
            }
            // Déterminisme : même combat, même journal ; le rejeu le reproduit octet par octet.
            Assert.Equal(log.ToString(), Run(json).Log.ToString());
            Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
        }
        // La propriété n'est pas vide : des pas, des bords atteints, des frappes à portée et des refus de portée.
        Assert.True(moves > 1000 && walls > 0 && casts > 0 && outOfRange > 0, $"pas {moves}, bords {walls}, frappes {casts}, refus {outOfRange}");
    }

    // --- Portée -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Portees_du_Bastion_livrees()
    {
        IReadOnlyDictionary<string, Fixed?> ranges = ClassTestData.Bastion.Skills.Where(s => s.Combat != null).ToDictionary(s => s.Id, s => s.Combat!.Range);
        Assert.Equal(Fixed.Parse("2.5"), ranges[S("frappe_du_rempart")]);
        Assert.Equal(Fixed.Parse("2.5"), ranges[S("heurt_de_bouclier")]);
        Assert.Equal(Fixed.Parse("2.5"), ranges[S("contrecoup")]);
        Assert.Equal(Fixed.FromInt(15), ranges[S("provocation")]);
        Assert.Equal(Fixed.FromInt(15), ranges[S("barriere_d_acier")]);
        Assert.Null(ranges[S("onde_de_rupture")]);   // zone : rayon au prochain pas du 11.4
        Assert.Null(ranges[S("bastion_absolu")]);
    }

    [Fact]
    public void Seules_les_portees_changent_depuis_le_contenu_0_5_0()
    {
        JsonObject shipped = JsonNode.Parse(File.ReadAllText(ClassTestData.BastionPath))!.AsObject();
        JsonObject frozen = JsonNode.Parse(File.ReadAllText(Content050ClassPath))!.AsObject();
        Assert.Equal(("0.6.0-provisoire", "0.5.0-provisoire"), (shipped["contentVersion"]!.GetValue<string>(), frozen["contentVersion"]!.GetValue<string>()));
        int removed = 0;
        foreach (JsonNode? skill in shipped["data"]!["skills"]!.AsArray())
            if (skill!["combat"] is JsonObject combat && combat.Remove("range")) removed++;
        Assert.Equal(5, removed);
        shipped["contentVersion"] = "0.5.0-provisoire";
        Assert.Equal(frozen.ToJsonString(), shipped.ToJsonString());
    }

    [Theory]
    [InlineData("3.6", true)]       // bords à 2,5 m exactement : à portée
    [InlineData("3.6001", false)]   // 2,5001 m : hors de portée
    [InlineData("-3.6", true)]
    public void Portee_a_la_frontiere_exacte(string dummyX, bool inRange)
    {
        var (_, lines, _) = Run(SpaceScenario(new[] { Cast(0, "frappe_du_rempart", "mannequin") }, dummyX, durationMs: 100));
        Assert.Equal(inRange, Of(lines, "skillCast").Any());
        Assert.Equal(!inRange, Of(lines, "commandRejected").Any(l => l.GetProperty("reason").GetString() == "targetOutOfRange"));
    }

    [Fact]
    public void Refus_de_portee_atomique_puis_lancer_au_pas_suivant()
    {
        // À 3,6001 m, refusé au tick 0 sans dépense ni recharge ; un pas de 0,25 m vers la cible, et la frappe part au tick 1.
        var (result, lines, _) = Run(SpaceScenario(new[] { Cast(0, "frappe_du_rempart", "mannequin"), Move(0, "1", "0"), Cast(50, "frappe_du_rempart", "mannequin") },
            "3.6001", durationMs: 200));
        JsonElement refused = Of(lines, "commandRejected").Single();
        Assert.Equal((0L, "targetOutOfRange", 0L), (refused.GetProperty("tick").GetInt64(), refused.GetProperty("reason").GetString(), refused.GetProperty("command").GetInt64()));
        JsonElement cast = Of(lines, "skillCast").Single();
        long maxEndurance = Of(lines, "combatant").First().GetProperty("maxEndurance").GetInt64();
        Assert.Equal((1L, 2L, maxEndurance - 10), (cast.GetProperty("tick").GetInt64(), cast.GetProperty("command").GetInt64(), cast.GetProperty("endurance").GetInt64()));
        Assert.Equal(1, result.Combatants.Single(c => c.Id == "bastion").CommandsRejected);
    }

    [Theory]
    [InlineData("16.0", true)]       // allié à 15 m entre les bords (rayons 0,5 et 0,5) : Barrière d'acier à portée
    [InlineData("16.0001", false)]
    public void Portee_d_une_competence_sur_un_allie(string allyX, bool inRange)
    {
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Hero("allie"), allyX, "0", "0.5"), Place(Dummy(), "0", "10", "0.6") },
            new[] { Cast(0, "barriere_d_acier", "allie") }, durationMs: 100, space: Bounds()));
        Assert.Equal(inRange, Of(lines, "skillCast").Any());
        Assert.Equal(!inRange, Of(lines, "commandRejected").Any(l => l.GetProperty("reason").GetString() == "targetOutOfRange"));
    }

    [Fact]
    public void Ordre_des_motifs_cible_morte_avant_portee_et_soi_toujours_a_portee()
    {
        // Sur soi (Barrière d'acier, allié désigné : le lanceur), la distance est nulle. Sur une cible morte et lointaine : targetDead.
        // Un mannequin lointain garde le combat ouvert après la mort de l'assaillant.
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Assailant("bastion", health: 1, raw: 0), "15", "0", "0.6"),
                Place(Dummy(), "0", "15", "0.6") },
            new[] { Cast(0, "onde_de_rupture", null), Cast(1000, "barriere_d_acier", "bastion"), Cast(2000, "frappe_du_rempart", "assaillant") },
            durationMs: 2500, space: Bounds()));
        Assert.Contains(Of(lines, "died"), l => l.GetProperty("combatant").GetString() == "assaillant");
        Assert.Equal(new[] { S("onde_de_rupture"), S("barriere_d_acier") }, Of(lines, "skillCast").Select(l => l.GetProperty("skill").GetString()));
        Assert.Equal("targetDead", Of(lines, "commandRejected").Single().GetProperty("reason").GetString());
    }

    [Fact]
    public void Competence_de_zone_sans_portee_a_ce_stade()
    {
        // Limite documentée (étape 1 du 11.4) : Onde de rupture frappe encore tous les ennemis vivants, même à 30 m.
        var (_, lines, _) = Run(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Dummy("proche"), "2", "0", "0.6"), Place(Dummy("loin"), "0", "30", "0.6") },
            new[] { Cast(0, "onde_de_rupture", null) }, durationMs: 100, space: Bounds(-40, 40, -40, 40)));
        Assert.Equal(new[] { "loin", "proche" }, Of(lines, "damage").Select(l => l.GetProperty("target").GetString()));
    }

    // --- Validation ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Position_sans_espace_refusee()
    {
        ScenarioLoadResult result = Read(Json(new[] { Place(Hero(), "0", "0", "0.5"), Dummy() }, Array.Empty<JsonObject>()));
        AssertError(result.Report, "$.data.combatants[0].position", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Espace_sans_position_refuse()
    {
        ScenarioLoadResult result = Read(Json(new[] { Place(Hero(), "0", "0", "0.5"), Dummy() }, Array.Empty<JsonObject>(), space: Bounds()));
        AssertError(result.Report, "$.data.combatants[1].position");
        Assert.Single(result.Report.Errors);
    }

    [Fact]
    public void Orientation_ou_rayon_sans_position_refuses()
    {
        JsonObject hero = Hero();
        hero["radius"] = 0.5m;
        hero["facing"] = Point("1", "0");
        ScenarioLoadResult result = Read(Json(new[] { hero, Dummy() }, Array.Empty<JsonObject>()));
        AssertError(result.Report, "$.data.combatants[0].radius", ValidationCode.InvalidValue);
        AssertError(result.Report, "$.data.combatants[0].facing", ValidationCode.InvalidValue);
    }

    [Theory]
    [InlineData("0", "0", "0", "$.data.combatants[0].radius")]          // rayon nul
    [InlineData("0", "0", "50.0001", "$.data.combatants[0].radius")]    // au-delà de 50 m
    [InlineData("19.6", "0", "0.5", "$.data.combatants[0].position")]   // emprise qui dépasse le bord (20 m)
    [InlineData("10001", "0", "0.5", "$.data.combatants[0].position")]  // hors des limites numériques
    public void Placement_invalide_refuse(string x, string z, string radius, string path)
    {
        ScenarioLoadResult result = Read(Json(new[] { Place(Hero(), x, z, radius), Place(Dummy(), "5", "5", "0.6") }, Array.Empty<JsonObject>(), space: Bounds()));
        AssertError(result.Report, path);
    }

    [Fact]
    public void Rayon_absent_et_orientation_nulle_refuses()
    {
        JsonObject hero = Hero();
        hero["position"] = Point("0", "0");
        ScenarioLoadResult missing = Read(Json(new[] { hero, Place(Dummy(), "5", "5", "0.6") }, Array.Empty<JsonObject>(), space: Bounds()));
        AssertError(missing.Report, "$.data.combatants[0].radius", ValidationCode.MissingField);
        ScenarioLoadResult zero = Read(Json(new[] { Place(Hero(), "0", "0", "0.5", Point("0", "0")), Place(Dummy(), "5", "5", "0.6") }, Array.Empty<JsonObject>(), space: Bounds()));
        AssertError(zero.Report, "$.data.combatants[0].facing", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Bornes_invalides_refusees()
    {
        ScenarioLoadResult result = Read(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Dummy(), "5", "5", "0.6") }, Array.Empty<JsonObject>(), space: Bounds(10, 10, -5, 5)));
        AssertError(result.Report, "$.data.space.bounds.minX");
        JsonObject incomplete = Bounds();
        incomplete["bounds"]!.AsObject().Remove("maxZ");
        AssertError(Read(Json(new[] { Place(Hero(), "0", "0", "0.5"), Place(Dummy(), "5", "5", "0.6") }, Array.Empty<JsonObject>(), space: incomplete)).Report,
            "$.data.space.bounds.maxZ", ValidationCode.MissingField);
    }

    [Fact]
    public void Deplacement_dans_un_combat_sans_espace_refuse()
    {
        ScenarioLoadResult result = Read(Json(new[] { Hero(), Dummy() }, new[] { Move(0, "1", "0"), Act(50, "moveStop") }));
        AssertError(result.Report, "$.data.commands[0].action", ValidationCode.InvalidValue);
        AssertError(result.Report, "$.data.commands[1].action", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Commandes_de_deplacement_mal_formees_refusees()
    {
        JsonObject noDirection = Act(0, "move");
        JsonObject withSkill = Move(0, "1", "0");
        withSkill["skill"] = S("frappe_du_rempart");
        JsonObject guardWithDirection = Act(0, "guardStart");
        guardWithDirection["direction"] = Point("1", "0");
        JsonObject skillWithDirection = Cast(0, "frappe_du_rempart", "mannequin");
        skillWithDirection["direction"] = Point("1", "0");
        // Forme (lecture) : direction absente, compétence ou direction mal placées.
        ScenarioLoadResult shape = Read(SpaceScenario(new[] { noDirection, withSkill, guardWithDirection, skillWithDirection }));
        Assert.Equal(4, shape.Report.Errors.Count);
        AssertError(shape.Report, "$.data.commands[0].direction", ValidationCode.MissingField);
        AssertError(shape.Report, "$.data.commands[1].skill", ValidationCode.InvalidValue);
        AssertError(shape.Report, "$.data.commands[2].direction", ValidationCode.InvalidValue);
        AssertError(shape.Report, "$.data.commands[3].direction", ValidationCode.InvalidValue);
        // Cohérence (domaine) : direction nulle, composante hors des limites numériques.
        ScenarioLoadResult domain = Read(SpaceScenario(new[] { Move(0, "0", "0"), Move(50, "10001", "0") }));
        Assert.Equal(2, domain.Report.Errors.Count);
        AssertError(domain.Report, "$.data.commands[0].direction", ValidationCode.InvalidValue);
        AssertError(domain.Report, "$.data.commands[1].direction", ValidationCode.OutOfRange);
        ScenarioLoadResult unknown = Read(SpaceScenario(new[] { Act(0, "dash") }));
        ValidationError error = Assert.Single(unknown.Report.Errors);
        Assert.Contains("move, moveStop", error.Message);
    }

    [Theory]
    [InlineData("onde_de_rupture", "5")]       // zone : portée sans effet
    [InlineData("bastion_absolu", "15")]       // soi
    [InlineData("frappe_du_rempart", "-1")]
    [InlineData("frappe_du_rempart", "1000.0001")]
    public void Portee_de_classe_invalide_refusee(string skill, string range)
    {
        ClassLoadResult result = ClassTestData.ReadMutated(root =>
        {
            JsonObject combat = root["data"]!["skills"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == S(skill))!["combat"]!.AsObject();
            combat["range"] = decimal.Parse(range, System.Globalization.CultureInfo.InvariantCulture);
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path.EndsWith(".combat.range", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("baseSpeed", "0")]
    [InlineData("baseSpeed", "100.0001")]
    [InlineData("guardSpeedRatio", "1.5")]
    [InlineData("guardSpeedRatio", "-0.1")]
    public void Regles_de_deplacement_invalides_refusees(string field, string value)
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["movement"]![field] = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        AssertError(result.Report, "$.data.combat.movement." + field, ValidationCode.InvalidValue);
    }

    [Fact]
    public void Regles_sans_bloc_de_deplacement_refusees()
    {
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["data"]!["combat"]!.AsObject().Remove("movement"));
        AssertError(result.Report, "$.data.combat.movement.baseSpeed", ValidationCode.MissingField);
        Assert.Equal((Fixed.FromInt(5), Fixed.Parse("0.5")), (ShippedData.Rules.Combat.MovementBaseSpeed, ShippedData.Rules.Combat.GuardMovementSpeedRatio));
    }

    // --- Pilotage, temps réel et réseau --------------------------------------------------------------------------------------

    [Fact]
    public void Pilote_deplacement_soumis_et_vue_positionnee()
    {
        CombatSetup setup = Read(SpaceScenario(Array.Empty<JsonObject>())).Setup!;
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(0, "bastion", GroundVector.Zero)));
        engine.Submit(new CombatCommand(0, "bastion", new GroundVector(Fixed.Zero, Fixed.One)));
        engine.Step();
        CombatantView view = engine.View("bastion");
        Assert.Equal((new GroundVector(Fixed.Zero, Fixed.Parse("0.25")), new GroundVector(Fixed.Zero, Fixed.One), Fixed.Parse("0.5")),
            (view.Position!.Value, view.Facing!.Value, view.Radius!.Value));
        Assert.Equal(new GroundVector(Fixed.Zero, Fixed.One), view.Moving);

        // Combat sans espace : aucune position dans la vue, déplacement refusé à la soumission.
        var logical = new CombatEngine(ScenarioJsonReader.ReadFile(MannequinPath, ShippedData).Setup!, new MemoryCombatLog());
        Assert.Null(logical.View("bastion").Position);
        Assert.Throws<ArgumentException>(() => logical.Submit(new CombatCommand(0, "bastion", new GroundVector(Fixed.One, Fixed.Zero))));
        Assert.Throws<ArgumentException>(() => logical.Submit(new CombatCommand(0, "bastion", CombatAction.MoveStop)));
    }

    [Fact]
    public void Combat_complete_garde_l_espace_et_les_placements()
    {
        // Le temps réel et le pilote complètent le combat de départ des commandes soumises : l'espace et les placements sont gardés.
        CombatSetup setup = Read(SpaceScenario(Array.Empty<JsonObject>(), durationMs: 500)).Setup!;
        var realtime = new EdS.Runtime.RealtimeCombat(setup, "bastion");
        CombatSetup completed = realtime.CompletedSetup();
        Assert.Same(setup.Space, completed.Space);
        Assert.All(completed.Combatants, c => Assert.NotNull(c.Placement));
        EdS.Runtime.TrainingSession session = EdS.Runtime.TrainingSession.Create(ShippedData, setup, "build.bastion.rempart");
        Assert.NotNull(session.Setup.Space);
        Assert.Equal(setup.Combatants[0].Placement, session.Setup.Combatants.Single(c => c.Id == "bastion").Placement);
    }

    [Fact]
    public void Protocole_reseau_1_sans_deplacement()
    {
        var commands = new TickCommands(0, new[] { new CombatCommand(0, "bastion", new GroundVector(Fixed.One, Fixed.Zero)) });
        Assert.Throws<NotSupportedException>(() => MessageCodec.EncodeText(new TickMessage(commands, "fnv1a64:0000000000000000")));
        var stop = new TickCommands(0, new[] { new CombatCommand(0, "bastion", CombatAction.MoveStop) });
        Assert.Throws<NotSupportedException>(() => MessageCodec.EncodeText(new TickMessage(stop, "fnv1a64:0000000000000000")));
    }
}
