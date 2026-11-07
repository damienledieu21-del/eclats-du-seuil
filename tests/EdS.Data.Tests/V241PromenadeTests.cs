using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.4.1 (bêtes qui se promènent ; réponse de Damien du 6 novembre 2026 à la question 8 de la PR #104) : moteur 0.29.0, journal au
/// format 23 (<c>wander</c> dans l'en-tête, ligne <c>calmed</c>), scénarios en schéma 17, règles en schéma 16 (contenu 0.17.0, bloc
/// <c>combat.wander</c>), instantané au format 6 (<c>quietSince</c>, <c>calms</c>) ; nouvelle référence figée
/// <c>Fixtures/reference-0.29.0-contenu-0.6.0-regles-0.17.0</c> (corps identiques pour les anciens journaux, et <c>espace_promenade</c>).
/// </summary>
[Trait("Category", "Behavior")]
[Trait("Category", "Regression")]
public class V241PromenadeMoteurTests
{
    private const string Promenade = "espace_promenade";
    private const string Latest = "reference-0.29.0-contenu-0.6.0-regles-0.17.0", Previous = "reference-0.28.0-contenu-0.6.0-regles-0.16.0";
    private static readonly string[] Deer = { "cerf2", "cerf3", "cerf_chef" };

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

    private static CombatSetup Load(string name = Promenade)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    private static List<JsonElement> Events(MemoryCombatLog log) =>
        log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static double Distance(GroundVector a, double x, double z) =>
        Math.Sqrt(Math.Pow(a.X.Raw / 1e4 - x, 2) + Math.Pow(a.Z.Raw / 1e4 - z, 2));

    private static double Distance(GroundVector a, GroundVector b) => Distance(a, b.X.Raw / 1e4, b.Z.Raw / 1e4);

    private static void AssertError(ScenarioLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    private static JsonNode Wander(JsonObject r, int index) => r["data"]!["combatants"]![index]!["behavior"]!["wander"]!;

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.29.0", 23, 17, 6), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((16, "0.17.0-provisoire"), (DataFormat.RulesSchemaVersion, ShippedData.Rules.ContentVersion));
        Assert.Equal((Fixed.FromInt(30), 15_000L), (ShippedData.Rules.Combat.WanderMaxRadius, ShippedData.Rules.Combat.WanderCalmMs));
    }

    [Fact]
    public void Reference_figee_identique_et_rejouee()
    {
        string[] frozen = File.ReadAllLines(Fixture(Latest, Promenade));
        Assert.Equal(frozen, Simulate(Load()).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        // L'en-tête porte l'errance, sous la forme du fichier de scénario.
        JsonElement chief = JsonDocument.Parse(frozen[0]).RootElement.GetProperty("scenario").GetProperty("combatants").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "cerf_chef").GetProperty("behavior").GetProperty("wander");
        Assert.Equal(("graze", "harde", true), (chief.GetProperty("style").GetString(), chief.GetProperty("group").GetString(), chief.GetProperty("lead").GetBoolean()));
        Assert.Equal(3, frozen.Count(l => l.Contains("\"event\":\"calmed\"", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("espace_betes")]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // Sans bête qui erre, le moteur 0.29.0 reproduit octet par octet les lignes d'événements du moteur 0.28.0 (seul l'en-tête change) ;
        // l'ancienne référence reste figée et refusée au rejeu.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(old, ShippedData).Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(Load(name)).Lines);
    }

    [Fact]
    public void Errance_deterministe_sans_tirage_du_combat()
    {
        CombatSetup setup = Load();
        Assert.Equal(Simulate(setup).Lines, Simulate(Load()).Lines);
        // Le générateur de chaque bête n'est jamais touché par l'errance (les tirages viennent d'un Pcg32 neuf par étape).
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        string Random(string id) => engine.Snapshot().Text.Split('\n').Single(l => l.StartsWith($"{{\"fighter\":\"{id}\",\"presence\"", StringComparison.Ordinal))
            .Split("\"random\":")[1].Split(']')[0];
        string before = Random("lievre");
        for (int i = 0; i < 400; i++) engine.Step();
        Assert.Equal(before, Random("lievre"));
        Assert.NotEqual(GroundVector.Parse("0", "-15"), engine.View("lievre").Position);
        // Une autre graine : d'autres pas.
        CombatSetup other = ReadMutated(r => r["data"]!["seed"] = 77, scenario: Promenade).Setup!;
        var moved = new CombatEngine(other, new MemoryCombatLog());
        for (int i = 0; i < 400; i++) moved.Step();
        Assert.NotEqual(engine.View("lievre").Position, moved.View("lievre").Position);
    }

    [Fact]
    public void Chaque_bete_reste_dans_son_aire_a_son_allure()
    {
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        var states = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var last = new Dictionary<string, GroundVector>(StringComparer.Ordinal);
        while (!engine.IsFinished)
        {
            engine.Step();
            foreach (string id in new[] { "lievre", "sanglier", "cerf_chef", "loup_chef", "loup2" })
            {
                CombatantView v = engine.View(id);
                if (v.Wander is string s) (states.TryGetValue(id, out var set) ? set : states[id] = new HashSet<string>()).Add(s);
                if (last.TryGetValue(id, out GroundVector before))
                {
                    // Allure : un pas au plus de vitesse × allure (lièvre 7 × 0,8 ; sanglier 5 × 0,3), à l'arrondi près.
                    double bound = id == "lievre" ? 7 * 0.8 * 0.05 : id == "sanglier" ? 5 * 0.3 * 0.05 : 1;
                    Assert.True(Distance(v.Position!.Value, before) <= bound + 1e-3, $"{id} : pas trop long au tick {engine.CurrentTick}");
                }
                last[id] = v.Position!.Value;
            }
            Assert.True(Distance(engine.View("lievre").Position!.Value, 0, -15) <= 4.5);
            Assert.True(Distance(engine.View("sanglier").Position!.Value, 20, -15) <= 3.5);
            Assert.True(Distance(engine.View("loup_chef").Position!.Value, 20, 15) <= 6.5);
        }
        Assert.Superset(new HashSet<string> { "hop", "crouch", "watch" }, states["lievre"]);
        Assert.Superset(new HashSet<string> { "walk", "root" }, states["sanglier"]);
        Assert.Superset(new HashSet<string> { "walk", "graze" }, states["cerf_chef"]);
        Assert.Superset(new HashSet<string> { "walk", "watch" }, states["loup_chef"]);
        Assert.Contains("walk", states["loup2"]);
    }

    [Fact]
    public void La_meute_suit_son_chef_en_file_et_la_harde_en_grappe()
    {
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        double wolves = 0, deer = 0;
        int samples = 0;
        while (!engine.IsFinished)
        {
            engine.Step();
            if (engine.CurrentTick < 200 || engine.CurrentTick % 20 != 0) continue;
            GroundVector chief = engine.View("loup_chef").Position!.Value;
            double a = Distance(engine.View("loup2").Position!.Value, chief), b = Distance(engine.View("loup3").Position!.Value, chief);
            Assert.True(a < 9 && b < 9, $"meute dispersée au tick {engine.CurrentTick}");
            wolves += a + b;
            if (engine.View("cerf_chef").Wander != null && engine.View("cerf2").Wander != null)
            {
                GroundVector stag = engine.View("cerf_chef").Position!.Value;
                deer += Distance(engine.View("cerf2").Position!.Value, stag) + Distance(engine.View("cerf3").Position!.Value, stag);
            }
            samples++;
        }
        Assert.True(wolves / (2 * samples) < 4.5, $"meute : écart moyen {wolves / (2 * samples):0.0} m");
        Assert.True(deer / (2 * samples) < 5, $"harde : écart moyen {deer / (2 * samples):0.0} m");
    }

    [Fact]
    public void La_harde_effrayee_detale_puis_se_calme_et_rentre()
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(Load(), log);
        while (!engine.IsFinished) engine.Step();
        List<JsonElement> events = Events(log);
        foreach (string id in Deer)
        {
            long engaged = events.First(e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("actor").GetString() == id).GetProperty("tick").GetInt64();
            long calmed = events.Single(e => e.GetProperty("event").GetString() == "calmed" && e.GetProperty("actor").GetString() == id).GetProperty("tick").GetInt64();
            // 15 s au calme au moins (300 ticks) après le départ du Bastion (tick 400).
            Assert.True(calmed > engaged && calmed >= 400 + 300 - 120, $"{id} calmé au tick {calmed}");
            CombatantView v = engine.View(id);
            Assert.False(v.Engaged || v.Noticed);
            Assert.NotNull(v.Wander);
            // Rentrée : la harde broute de nouveau autour de la place de son chef.
            Assert.True(Distance(v.Position!.Value, -20, 10) <= 5 + 2 * 2 + 1, $"{id} pas rentré : {v.Position}");
        }
        Assert.DoesNotContain(events, e => e.GetProperty("event").GetString() == "damage");
    }

    [Fact]
    public void Instantane_garde_le_calme_et_se_reprend_a_l_identique()
    {
        CombatSetup setup = Load();
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 560) engine.Step();
        CombatSnapshot quiet = engine.Snapshot();
        Assert.StartsWith("{\"snapshot\":6,", quiet.Text);
        Assert.Contains("\"quietSince\":", quiet.Text.Split('\n').Single(l => l.StartsWith("{\"fighter\":\"cerf_chef\",\"presence\"", StringComparison.Ordinal)).Replace("\"quietSince\":null", ""));
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), quiet, resumedLog);
        int from = log.Lines.Count;
        while (engine.CurrentTick < 1100)
        {
            engine.Step();
            resumed.Step();
        }
        Assert.Equal(log.Lines.Skip(from), resumedLog.Lines);
        CombatSnapshot after = engine.Snapshot();
        Assert.Equal(after.Text, resumed.Snapshot().Text);
        Assert.Contains("\"calms\":1", after.Text);
        // Retouches refusées : des calmes négatifs, une bête au calme sans être engagée.
        string tampered = after.Text.Replace("\"calms\":1", "\"calms\":-1");
        Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(tampered), new MemoryCombatLog()));
        string[] lines = quiet.Text.Split('\n');
        int hare = Array.FindIndex(lines, l => l.StartsWith("{\"fighter\":\"lievre\",\"presence\"", StringComparison.Ordinal));
        lines[hare] = lines[hare].Replace("\"quietSince\":null", "\"quietSince\":3");
        Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(string.Join("\n", lines)), new MemoryCombatLog()));
    }

    [Fact]
    public void Budget_epuise_au_calme_rien_n_est_a_moitie_fait()
    {
        // Test adversarial : le Bastion démarre au milieu de la harde, puis s'en va ; calme ramené à 1 s ; budget de travail du tick ramené
        // pas à pas : quel que soit l'endroit où le tick s'arrête (budgetExceeded), chaque cerf est engagé si et seulement si sa dernière
        // ligne est « engaged » (jamais calmé sans sa ligne, ni l'inverse), remarqué seulement engagé. Le tick 0 (engagement) étant le plus
        // lourd, l'arrêt y tombe ; la réserve exacte du calme (une unité par bête engagée qui erre, une ligne si elle se calme, avant toute
        // mutation) est vérifiée à chaque tick en Debug (CI).
        CombatSetup baseSetup = ReadMutated(r =>
        {
            r["data"]!["combatants"]![0]!["position"] = new JsonArray(-20, 5);
            r["data"]!["durationMs"] = 12000;
            r["data"]!["commands"] = new JsonArray(
                new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(0.46, -0.89) },
                new JsonObject { ["atMs"] = 3000, ["actor"] = "bastion", ["action"] = "stop" });
        }, scenario: Promenade).Setup!;
        CombatSetup With(int work)
        {
            var rules = (CombatRules)typeof(CombatRules).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(baseSetup.Combat, null)!;
            typeof(CombatRules).GetProperty(nameof(CombatRules.WanderCalmMs))!.SetValue(rules, 1000L);
            if (work > 0)
            {
                typeof(CombatRules).GetProperty(nameof(CombatRules.MaxWorkPerTick))!.SetValue(rules, work);
                typeof(CombatRules).GetProperty(nameof(CombatRules.MaxEventsPerTick))!.SetValue(rules, Math.Min(work, baseSetup.Combat.MaxEventsPerTick));
            }
            return new CombatSetup(baseSetup.Seed, baseSetup.DurationMs, baseSetup.Stats, baseSetup.Damage, baseSetup.Progression, rules,
                baseSetup.Combatants, baseSetup.Commands, baseSetup.Data, baseSetup.Space, baseSetup.Zone, baseSetup.Truces);
        }
        List<JsonElement> full = Events(Simulate(With(0)));
        long calmTick = full.First(e => e.GetProperty("event").GetString() == "calmed").GetProperty("tick").GetInt64();
        bool aborted = false;
        for (int work = 1; work <= 160; work++)
        {
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(With(work), log);
            var engagedBefore = new Dictionary<string, bool>(StringComparer.Ordinal);
            while (!engine.IsFinished)
            {
                foreach (string id in Deer) engagedBefore[id] = engine.View(id).Engaged;
                int lines = log.Lines.Count;
                engine.Step();
                List<JsonElement> now = log.Lines.Skip(lines).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
                foreach (string id in Deer)
                {
                    // Jamais calmé sans sa ligne, ni l'inverse ; remarqué seulement engagé.
                    bool calmed = now.Any(e => e.GetProperty("event").GetString() == "calmed" && e.GetProperty("actor").GetString() == id);
                    bool engaged = now.Any(e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("actor").GetString() == id);
                    Assert.Equal(engaged || (engagedBefore[id] && !calmed), engine.View(id).Engaged);
                    Assert.True(!engine.View(id).Noticed || engine.View(id).Engaged, $"budget {work} : {id} a remarqué sans être engagé");
                }
                aborted |= now.Any(e => e.GetProperty("event").GetString() == "simulationAborted");
            }
        }
        Assert.True(aborted, "aucun arrêt");
        Assert.True(calmTick > 0);
    }

    [Fact]
    public void Formes_refusees_avec_leur_chemin()
    {
        // Indices : 0 bastion, 1 cerf2, 2 cerf3, 3 cerf_chef, 4 lievre, 5 loup2, 6 loup3, 7 loup_chef, 8 sanglier.
        const string Hare = "$.data.combatants[4].behavior.wander";
        AssertError(ReadMutated(r => Wander(r, 4)["radius"] = 31, scenario: Promenade), Hare + ".radius");   // combat.wander.maxRadius 30
        AssertError(ReadMutated(r => Wander(r, 4)["radius"] = 0, scenario: Promenade), Hare + ".radius");
        AssertError(ReadMutated(r => Wander(r, 4)["pace"] = 0, scenario: Promenade), Hare + ".pace");
        AssertError(ReadMutated(r => Wander(r, 4)["pace"] = 1.5, scenario: Promenade), Hare + ".pace");
        AssertError(ReadMutated(r => Wander(r, 4)["legMs"] = 4025, scenario: Promenade), Hare + ".legMs");
        AssertError(ReadMutated(r => Wander(r, 4)["legMs"] = 500, scenario: Promenade), Hare + ".legMs");
        AssertError(ReadMutated(r => Wander(r, 4)["pauseMs"] = new JsonArray(2000, 1000), scenario: Promenade), Hare + ".pauseMs");
        AssertError(ReadMutated(r => Wander(r, 4)["pauseMs"] = new JsonArray(0, 9000), scenario: Promenade), Hare + ".pauseMs");
        AssertError(ReadMutated(r => Wander(r, 4)["pauseMs"] = new JsonArray(1000), scenario: Promenade), Hare + ".pauseMs");
        AssertError(ReadMutated(r => Wander(r, 4).AsObject().Remove("pauseMs"), scenario: Promenade), Hare + ".pauseMs");
        AssertError(ReadMutated(r => Wander(r, 4)["style"] = "nage", scenario: Promenade), Hare + ".style");
        AssertError(ReadMutated(r => Wander(r, 4)["points"] = 9, scenario: Promenade), Hare + ".points");
        AssertError(ReadMutated(r => Wander(r, 4)["spacing"] = 0, scenario: Promenade), Hare + ".spacing");
        AssertError(ReadMutated(r => Wander(r, 4)["lead"] = true, scenario: Promenade), Hare + ".lead");   // un chef sans groupe
        AssertError(ReadMutated(r => Wander(r, 4)["group"] = "Harde", scenario: Promenade), Hare + ".group");
        AssertError(ReadMutated(r => Wander(r, 4)["sens"] = 1, scenario: Promenade), Hare + ".sens");
        // Un groupe d'une seule équipe : le lièvre (gibier) prend « meute_nord » avant les loups.
        AssertError(ReadMutated(r => Wander(r, 4)["group"] = "meute_nord", scenario: Promenade), "$.data.combatants[5].behavior.wander.group");
        // Une bête qui erre attend qu'on l'engage.
        AssertError(ReadMutated(r => r["data"]!["combatants"]![4]!["behavior"]!.AsObject().Remove("aggroRange"), scenario: Promenade), Hare);
        // Schéma 16 refusé (sans migration).
        AssertError(ReadMutated(r => r["schemaVersion"] = 16, scenario: Promenade), "$.schemaVersion");
    }

    [Fact]
    public void Regles_au_schema_15_refusees_et_bloc_wander_obligatoire()
    {
        string rules = File.ReadAllText(Path.Combine(DataDirectory, "rules.json"));
        Assert.False(RulesJsonReader.Read(rules.Replace("\"schemaVersion\": 16", "\"schemaVersion\": 15")).IsValid);
        JsonObject Root() => JsonNode.Parse(rules)!.AsObject();
        JsonObject root = Root();
        root["data"]!["combat"]!.AsObject().Remove("wander");
        Assert.Contains(RulesJsonReader.Read(root.ToJsonString()).Report.Errors, e => e.Path.StartsWith("$.data.combat.wander", StringComparison.Ordinal));
        root = Root();
        root["data"]!["combat"]!["wander"]!["maxRadius"] = 0;
        Assert.Contains(RulesJsonReader.Read(root.ToJsonString()).Report.Errors, e => e.Path == "$.data.combat.wander.maxRadius");
        root = Root();
        root["data"]!["combat"]!["wander"]!["calmMs"] = 1025;
        Assert.Contains(RulesJsonReader.Read(root.ToJsonString()).Report.Errors, e => e.Path == "$.data.combat.wander.calmMs");
    }

    [Fact]
    public void Schemas_JSON_du_sous_lot()
    {
        JsonNode scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(17, scenario["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode wander = scenario["$defs"]!["combatant"]!["properties"]!["behavior"]!["properties"]!["wander"]!;
        Assert.Equal(new[] { "style", "radius", "pace", "legMs", "pauseMs" }, wander["required"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.Equal(new[] { "graze", "hop", "patrol", "root" }, wander["properties"]!["style"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()));
        JsonNode rules = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json")))!;
        Assert.Equal(16, rules["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.NotNull(rules["properties"]!["data"]!["properties"]!["combat"]!["properties"]!["wander"]);
    }

    [Fact]
    public void Lievre_frappe_par_le_joueur_se_calme_garde_le_credit_et_ne_remarque_plus_le_joueur_au_loin()
    {
        // Relecture de la PR #111 : le Bastion frappe le lièvre puis s'en va loin à l'ouest ; le lièvre se calme (crédit du joueur gardé :
        // frappé par un personnage), rentre ; un loup arrivé plus tard (arrivalMs, avec son errance, sans groupe) le dérange : réengagé
        // contre une bête, il ne « remarque » pas le joueur au loin (il ne le vise pas) ; seul un nouveau coup du joueur l'éveillerait.
        CombatSetup setup = ReadMutated(r =>
        {
            JsonArray c = r["data"]!["combatants"]!.AsArray();
            c[0]!["position"] = new JsonArray(-0.9, -15);
            c[4]!["health"] = 500;   // il survit au coup
            JsonObject late = c[5]!.DeepClone().AsObject();
            late["id"] = "loup_tard";
            late["position"] = new JsonArray(0, -5);
            late["arrivalMs"] = 40000;
            late["behavior"]!["wander"]!.AsObject().Remove("group");
            c.Add(late);
            r["data"]!["durationMs"] = 120000;
            r["data"]!["commands"] = new JsonArray(
                new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "lievre" },
                new JsonObject { ["atMs"] = 1500, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(-1, 0) },
                new JsonObject { ["atMs"] = 7000, ["actor"] = "bastion", ["action"] = "stop" });
        }, scenario: Promenade).Setup!;
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        bool calmed = false, reengaged = false, wolfWandered = false;
        while (!engine.IsFinished)
        {
            engine.Step();
            CombatantView hare = engine.View("lievre"), wolf = engine.View("loup_tard");
            if (wolf.IsPresent && wolf.Wander != null) wolfWandered = true;
            if (!calmed && log.Lines.Any(l => l.Contains("\"event\":\"calmed\",\"actor\":\"lievre\"", StringComparison.Ordinal)))
            {
                calmed = true;
                Assert.True(hare.HurtByCharacter);   // le crédit du joueur reste
                Assert.False(hare.Engaged || hare.Noticed);
            }
            if (calmed && hare.Engaged)
            {
                reengaged = true;
                Assert.False(hare.Noticed, $"le lièvre remarque le joueur au loin au tick {engine.CurrentTick}");
                Assert.NotEqual("bastion", hare.CurrentTarget);
                Assert.True(hare.HurtByCharacter);
            }
        }
        Assert.True(calmed && reengaged && wolfWandered, $"calmé {calmed}, réengagé {reengaged}, loup errant {wolfWandered}");
        Assert.Contains(log.Lines, l => l.Contains("\"event\":\"combatantJoined\"", StringComparison.Ordinal) && l.Contains("loup_tard", StringComparison.Ordinal));
    }

    [Fact]
    public void Chef_parti_un_autre_mene_la_file()
    {
        // Relecture de la PR #111 : le chef de la meute quitte le combat (comme mort : il n'erre plus) ; loup2, premier membre qui erre, mène
        // alors, et loup3 le suit en file. Puis le chef engagé (le Bastion approche) : même relève.
        CombatSetup setup = ReadMutated(r =>
            r["data"]!["commands"]!.AsArray().Add(new JsonObject { ["atMs"] = 10000, ["actor"] = "loup_chef", ["action"] = "leave" }), scenario: Promenade).Setup!;
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        double gap = 0;
        int samples = 0;
        while (!engine.IsFinished)
        {
            engine.Step();
            if (engine.CurrentTick < 400 || engine.CurrentTick % 20 != 0) continue;
            Assert.False(engine.View("loup_chef").IsPresent);
            gap += Distance(engine.View("loup3").Position!.Value, engine.View("loup2").Position!.Value);
            samples++;
        }
        Assert.True(gap / samples < 4, $"loup3 à {gap / samples:0.0} m de loup2 en moyenne");
        // loup2 mène : il patrouille autour de sa propre place (22, 17), plus autour de celle du chef.
        Assert.True(Distance(engine.View("loup2").Position!.Value, 22, 17) <= 6.5);

        CombatSetup chased = ReadMutated(r =>
        {
            r["data"]!["combatants"]![0]!["position"] = new JsonArray(20, 23);
            r["data"]!["commands"] = new JsonArray();
        }, scenario: Promenade).Setup!;
        var e2 = new CombatEngine(chased, new MemoryCombatLog());
        for (int i = 0; i < 300; i++) e2.Step();
        Assert.True(e2.View("loup_chef").Engaged);
        CombatantView follower = e2.View("loup3");
        if (!follower.Engaged) Assert.NotNull(follower.Wander);   // une bête de la meute restée au calme erre seule ou derrière le nouveau chef
    }

    [Fact]
    public void Charge_puis_calme_l_instantane_se_relit_a_chaque_tick_et_se_reprend_a_l_identique()
    {
        // Revue Codex de la PR #111 (tour 2) : un sanglier qui erre charge un loup qui erre (seul, sans groupe) ; ils se blessent jusqu'à
        // 1 PV (une bête n'en tue pas une autre), puis se calment. « Chargé » reste vrai après le calme : à chaque tick, l'instantané authentique
        // se relit (tous les contrôles de cohérence), et, après le calme, la reprise vérifiée par rejeu continue à l'identique.
        CombatSetup setup = ReadMutated(r =>
        {
            JsonArray c = r["data"]!["combatants"]!.AsArray();
            c[0]!["position"] = new JsonArray(-35, 25);   // le Bastion loin de tout
            r["data"]!["commands"] = new JsonArray();
            c[8]!["health"] = 40;
            JsonObject wolf = c[5]!.DeepClone().AsObject();
            wolf["id"] = "loup_seul";
            wolf["position"] = new JsonArray(20, -9);
            wolf["health"] = 30;
            wolf["behavior"]!["wander"]!.AsObject().Remove("group");
            c.Add(wolf);
            r["data"]!["durationMs"] = 60000;
        }, scenario: Promenade).Setup!;
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        bool afterCalm = false;
        CombatEngine? copy = null;
        MemoryCombatLog? copyLog = null;
        int from = 0;
        while (!engine.IsFinished)
        {
            CombatSnapshot snapshot = engine.Snapshot();
            CombatEngine.Restore(engine.CompletedSetup(), snapshot, new MemoryCombatLog());   // contrôles de cohérence à chaque tick
            string boar = snapshot.Text.Split('\n').Single(l => l.StartsWith("{\"fighter\":\"sanglier\",\"presence\"", StringComparison.Ordinal));
            if (!afterCalm && boar.Contains("\"charged\":true", StringComparison.Ordinal) && boar.Contains("\"calms\":1", StringComparison.Ordinal))
            {
                afterCalm = true;
                copyLog = new MemoryCombatLog();
                copy = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, copyLog);
                from = log.Lines.Count;
            }
            engine.Step();
            if (copy != null)
            {
                copy.Step();
                if (!engine.IsFinished) Assert.Equal(engine.Snapshot().Text, copy.Snapshot().Text);
            }
        }
        Assert.True(afterCalm, "le sanglier n'a pas chargé puis ne s'est pas calmé");
        Assert.Equal(log.Lines.Skip(from), copyLog!.Lines);
        Assert.Contains(log.Lines, l => l.Contains("\"event\":\"charged\",\"actor\":\"sanglier\"", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, l => l.Contains("\"event\":\"died\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Harde_calmee_puis_reengagee_l_instantane_se_relit_a_chaque_tick()
    {
        // Revue Codex de la PR #111 (tour 2) : la harde effrayée se calme, puis le Bastion revient : un cerf la voit, les autres s'engagent
        // par la meute (un allié dont le tick d'engagement d'avant le calme n'est plus gardé). À chaque tick, l'instantané se relit.
        CombatSetup setup = ReadMutated(r =>
        {
            r["data"]!["combatants"]![0]!["position"] = new JsonArray(-20, 4);
            r["data"]!["durationMs"] = 60000;
            r["data"]!["commands"] = new JsonArray(
                new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(0, -1) },
                new JsonObject { ["atMs"] = 4000, ["actor"] = "bastion", ["action"] = "stop" },
                new JsonObject { ["atMs"] = 35000, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(0, 1) },
                new JsonObject { ["atMs"] = 39000, ["actor"] = "bastion", ["action"] = "stop" });
        }, scenario: Promenade).Setup!;
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (!engine.IsFinished)
        {
            CombatEngine.Restore(engine.CompletedSetup(), engine.Snapshot(), new MemoryCombatLog());
            engine.Step();
        }
        List<JsonElement> events = Events(log);
        Assert.Contains(events, e => e.GetProperty("event").GetString() == "calmed");
        long firstCalm = events.First(e => e.GetProperty("event").GetString() == "calmed").GetProperty("tick").GetInt64();
        Assert.Contains(events, e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("tick").GetInt64() > firstCalm);
    }

    [Fact]
    public void Instantane_au_format_5_refuse()
    {
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        for (int i = 0; i < 20; i++) engine.Step();
        string text = engine.Snapshot().Text;
        Assert.StartsWith("{\"snapshot\":6,", text);
        string old = "{\"snapshot\":5," + text.Substring("{\"snapshot\":6,".Length);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(old), new MemoryCombatLog()));
    }

    [Fact]
    public void Une_creature_d_evenement_n_erre_pas()
    {
        AssailantBehavior shipped = Load().Combatants.OfType<AssailantSetup>().Single(a => a.Id == "loup_chef").Behavior!;
        AssailantBehavior still = shipped.WithoutWander();
        Assert.Null(still.Wander);
        Assert.Equal((shipped.MaxRange, shipped.AggroRange, shipped.Encircle, shipped.Pack, shipped.Flee), (still.MaxRange, still.AggroRange, still.Encircle, still.Pack, still.Flee));
        Assert.Same(still, still.WithoutWander());
    }
}

/// <summary>Sous-lot V2.4.1 : les bêtes des Vallons se promènent (harde, meute, sangliers, lièvres), sans gêner le reste du monde.</summary>
[Trait("Category", "World")]
public class V241PromenadeMondeTests
{
    private static readonly ZoneDefinition Vallons = Zone();

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    private static long DayWith(WeatherKind kind, int minute)
    {
        for (long d = 1; d < 400; d++) if (WorldWeather.At("zone.vallons", d, minute).Kind == kind) return d;
        throw new InvalidOperationException("temps introuvable");
    }

    private static WorldSession At(long tick, ZoneDefinition? zone = null) =>
        new(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, Hero(1000)), zone ?? Vallons, ShippedData, Progression, ItemsCatalog, Book(zone ?? Vallons));

    private static IEnumerable<AssailantSetup> Beasts => Vallons.Setup.Combatants.OfType<AssailantSetup>();

    private static double Dist(GroundVector a, GroundVector b) =>
        Math.Sqrt(Math.Pow((a.X.Raw - b.X.Raw) / 1e4, 2) + Math.Pow((a.Z.Raw - b.Z.Raw) / 1e4, 2));

    /// <summary>
    /// Aires où une bête peut se trouver en errant : son disque autour de sa place ; pour un membre d'un groupe, aussi celui de son chef,
    /// élargi de la formation (écart × rang, ou × racine du rang en grappe) et d'un mètre de retard.
    /// </summary>
    private static List<(GroundVector Center, double Reach)> Reach(AssailantSetup a)
    {
        var discs = new List<(GroundVector, double)>();
        if (a.Behavior?.Wander is not { } w)
        {
            discs.Add((a.Placement!.Position, 0));
            return discs;
        }
        discs.Add((a.Placement!.Position, w.Radius.Raw / 1e4));
        if (w.Group is string group)
        {
            AssailantSetup[] members = Beasts.Where(b => b.Behavior?.Wander?.Group == group).ToArray();
            AssailantSetup lead = members.Single(b => b.Behavior!.Wander!.Lead);
            int n = members.Length - 1;
            double spread = w.Spacing.Raw / 1e4 * (lead.Behavior!.Wander!.Style == WanderStyle.Patrol ? n : Math.Sqrt(n)) + 1;
            discs.Add((lead.Placement!.Position, lead.Behavior.Wander.Radius.Raw / 1e4 + spread));
        }
        return discs;
    }

    [Fact]
    public void Les_betes_des_vallons_se_promenent()
    {
        AssailantSetup Of(string id) => Beasts.Single(b => b.Id == id);
        // Harde du lac et de la lisière, meutes (forêt, nord, est, brume), loups seuls, lièvres, sangliers qui fouillent.
        Assert.Equal(("harde_lac", true, WanderStyle.Graze), (Of("cerf1").Behavior!.Wander!.Group, Of("cerf1").Behavior!.Wander!.Lead, Of("cerf1").Behavior!.Wander!.Style));
        Assert.Equal(("harde_lac", false), (Of("cerf3").Behavior!.Wander!.Group, Of("cerf3").Behavior!.Wander!.Lead));
        Assert.Equal(("meute_brume", WanderStyle.Patrol), (Of("loup_brume2").Behavior!.Wander!.Group, Of("loup_brume2").Behavior!.Wander!.Style));
        Assert.All(Enumerable.Range(1, 4), i => Assert.Equal(WanderStyle.Hop, Of("lievre" + i).Behavior!.Wander!.Style));
        Assert.Equal(WanderStyle.Root, Of("sanglier3").Behavior!.Wander!.Style);
        // Restent à leur place : les échos et les bandits (pas des bêtes), la meute de Croc-Gris au pré (équilibrage du combat de chef), le
        // loup blanc (créature rare, l'étoile des chasses), le sanglier des ruines (au bord du village).
        Assert.All(Beasts.Where(b => b.Team is "echos" or "bandits"), b => Assert.Null(b.Behavior?.Wander));
        Assert.All(new[] { "loup3", "loup4", "loup5", "loup_blanc", "sanglier" }, id => Assert.Null(Of(id).Behavior!.Wander));
        Assert.True(Beasts.Count(b => b.Behavior?.Wander != null) >= 30);
        // Chaque groupe : une seule équipe, un seul chef désigné.
        foreach (IGrouping<string, AssailantSetup> g in Beasts.Where(b => b.Behavior?.Wander?.Group != null).GroupBy(b => b.Behavior!.Wander!.Group!))
        {
            Assert.Single(g.Select(b => b.Team).Distinct());
            Assert.Single(g, b => b.Behavior!.Wander!.Lead);
        }
    }

    [Fact]
    public void Aucune_bete_hostile_ne_s_approche_d_une_autre_en_errant()
    {
        // Pas de bagarre sans raison (sous-lot V2.4), errance comprise : les aires de deux bêtes ennemies restent hors de leur distance
        // d'engagement, à un mètre près.
        AssailantSetup[] beasts = Beasts.ToArray();
        foreach (AssailantSetup a in beasts)
            foreach (AssailantSetup b in beasts)
            {
                if (string.CompareOrdinal(a.Id, b.Id) >= 0 || !Vallons.Setup.Hostile(a.Team, b.Team)) continue;
                double aggro = Math.Max(a.Behavior!.AggroRange!.Value.Raw, b.Behavior!.AggroRange!.Value.Raw) / 1e4;
                foreach (var (ca, ra) in Reach(a))
                    foreach (var (cb, rb) in Reach(b))
                    {
                        double d = Dist(ca, cb) - ra - rb - a.Placement!.Radius.Raw / 1e4 - b.Placement!.Radius.Raw / 1e4;
                        Assert.True(d > aggro + 1, $"{a.Id} et {b.Id} : {d:0.#} m");
                    }
            }
    }

    [Fact]
    public void Aucune_bete_ne_vient_d_elle_meme_sur_un_point_sur()
    {
        // Une bête qui était hors de portée d'un point sûr y reste en errant : le joueur qui arrive, fait halte ou se soigne n'est pas
        // dérangé (la place du village, le camp et le hameau de la carrière, le bourg, la ferme).
        foreach (SafePoint safe in Vallons.SafePoints)
            foreach (AssailantSetup a in Beasts.Where(b => b.Behavior?.Wander != null))
            {
                double aggro = a.Behavior!.AggroRange!.Value.Raw / 1e4, r = a.Placement!.Radius.Raw / 1e4 + 0.5;
                if (Dist(a.Placement.Position, safe.Position) - r <= aggro + 0.5) continue;   // déjà à portée : sa place décide, comme avant
                foreach (var (c, reach) in Reach(a))
                    Assert.True(Dist(c, safe.Position) - reach - r > aggro + 0.5, $"{a.Id} près de {safe.Id}");
            }
    }

    [Fact]
    public void Un_matin_sans_le_joueur_les_betes_se_promenent_sans_se_battre()
    {
        // Le joueur reste au village de 6 h à 7 h d'un jour clair : les bêtes vont et viennent dans leurs aires, sans jamais s'engager, sans
        // mort, sans laisse ni vie de la session consommée.
        long clear = DayWith(WeatherKind.Clear, 6 * 60);
        WorldSession w = At(TickAt(clear, 6 * 60));
        var population = new List<PopulationEvent>();
        var moved = new HashSet<string>(StringComparer.Ordinal);
        var homes = Beasts.ToDictionary(b => b.Id, b => b, StringComparer.Ordinal);
        while (!w.Zone.IsOver && w.Zone.Time.Hour < 7)
        {
            SessionStep step = w.Step();
            population.AddRange(step.Population);
            Assert.Empty(step.Experience);
            if (w.Zone.Engine.CurrentTick % 100 != 0) continue;
            foreach (string id in w.Zone.Engine.CombatantIds)
            {
                CombatantView v = w.Zone.Engine.View(id);
                if (v.Kind != CombatantKind.Assailant || !v.IsPresent) continue;
                string population0 = homes.Keys.Where(k => id == k || id.StartsWith(k + "_v", StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault() ?? id;
                if (!homes.TryGetValue(population0, out AssailantSetup? a) || a.Behavior?.Wander is null) continue;
                Assert.False(v.Engaged, $"{id} engagé à {w.Zone.Time}");
                if (Dist(v.Position!.Value, a.Placement!.Position) > 0.5) moved.Add(population0);
                Assert.True(Reach(a).Any(d => Dist(v.Position.Value, d.Center) <= d.Reach + 0.6), $"{id} hors de son aire : {v.Position}");
            }
        }
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => w.Zone.Engine.View(id).IsDead);
        Assert.DoesNotContain(population, e => e.Kind is PopulationEventKind.Leashed or PopulationEventKind.Exhausted or PopulationEventKind.Respawned);
        Assert.Superset(new HashSet<string> { "cerf1", "cerf2", "loup6", "loup7", "sanglier3" }, moved);
    }

    [Fact]
    public void Un_cerf_effraye_fuit_puis_revient_brouter_chez_lui()
    {
        // Le joueur arrive à 6 m d'un cerf (sud du lac, midi) : la harde détale ; il repart vers l'ouest ; 15 s au calme plus tard, la harde
        // se désengage et revient brouter autour de la place de son chef. La fuite ne bloque jamais le voyage.
        long clear = DayWith(WeatherKind.Clear, 12 * 60);
        WorldSession w = At(TickAt(clear, 12 * 60), Zone(-8, -66));
        CombatEngine e = w.Zone.Engine;
        for (int i = 0; i < 100; i++) w.Step();
        Assert.True(e.View(w.CurrentCreature("cerf3")).Engaged);
        Assert.False(w.InCombat);
        for (int i = 0; i < 160; i++)
        {
            e.Submit(new CombatCommand(e.CurrentTick * 50, "bastion", new GroundVector(Fixed.FromInt(-1), Fixed.Zero)));
            w.Step();
        }
        e.Submit(new CombatCommand(e.CurrentTick * 50, "bastion", CombatAction.Stop));
        bool calm = false;
        for (int i = 0; i < 1600 && !calm; i++)
        {
            w.Step();
            calm = new[] { "cerf1", "cerf2", "cerf3" }.All(id => !e.View(w.CurrentCreature(id)).Engaged);
        }
        Assert.True(calm, "la harde ne s'est pas calmée");
        for (int i = 0; i < 1200; i++) w.Step();
        GroundVector chief = Beasts.Single(b => b.Id == "cerf1").Placement!.Position;
        foreach (string id in new[] { "cerf1", "cerf2", "cerf3" })
        {
            CombatantView v = e.View(w.CurrentCreature(id));
            Assert.Equal(id, v.Id);   // la même vie : aucune n'a été consommée
            Assert.NotNull(v.Wander);
            Assert.True(Dist(v.Position!.Value, chief) <= 4 + 2 * 2 + 1.5, $"{id} pas rentré : {v.Position}");
        }
    }

    [Fact]
    public void Formes_simples_et_essai_des_betes_qui_se_promenent()
    {
        // Sous Unity : la tête d'un cerf tourne autour de sa nuque ; il la baisse pour brouter, la relève pour guetter ; le lièvre se ramasse.
        Assert.True(Wildlife.IsHead("tete") && Wildlife.IsHead("bois_g") && Wildlife.IsHead("oreille_d") && !Wildlife.IsHead("patte_avg"));
        Assert.True(Wildlife.Pose(WildlifeKind.Deer, "graze", 1.0).HeadPitch > 30);
        Assert.True(Wildlife.Pose(WildlifeKind.Deer, "watch", 1.0).HeadPitch < 0);
        Assert.True(Wildlife.Pose(WildlifeKind.Hare, "crouch", 1.0).Crouch > 0);
        Assert.Equal((0.0, 0.0), Wildlife.Pose(WildlifeKind.Deer, null, 1.0));
        Assert.Equal(StyleMotion.Graze, StyleRules.Motion(0, false, false, false, "root"));
        Assert.Equal(StyleMotion.Walk, StyleRules.Motion(1.2, false, false, false, "walk"));
        Assert.Equal(StyleMotion.Idle, StyleRules.Motion(0, false, false, false, "watch"));
        Assert.Equal("Eat_Grass", StyleRules.Clip(new[] { "Idle", "Walk", "Eat_Grass" }, StyleMotion.Graze));
        Assert.Equal("Idle", StyleRules.Clip(new[] { "Idle", "Walk", "Eat_Grass" }, StyleMotion.Idle));
        Assert.Equal("Idle", StyleRules.Clip(new[] { "Idle", "Walk" }, StyleMotion.Graze));
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "global.json"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        string menu = File.ReadAllText(Path.Combine(root, "unity", "Assets", "EdS", "Editor", "EdsWanderTrial.cs"));
        Assert.Contains("Éclats du Seuil/Essai des bêtes qui se promènent : le matin (harde, meute, sangliers)", menu);
        Assert.Contains("Éclats du Seuil/Essai des bêtes qui se promènent : le crépuscule (lièvres)", menu);
        Assert.Contains("Aucune sauvegarde", WildlifeTrial.MoveToWanderMorning(Path.Combine(Path.GetTempPath(), "eds_absente_" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void Essai_avance_la_sauvegarde_au_matin_sans_averse()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eds_v241_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "vallons.json");
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
            string message = WildlifeTrial.MoveToWanderMorning(path, stamp: "essai");
            Assert.Contains("harde broute", message);
            var save = (WorldSave)SaveStore.Read(path).Save!;
            WorldTime at = new WorldClock(50).At(save.WorldTick);
            Assert.Equal(WildlifeTrial.WanderMorningMinute, at.Minute);
            Assert.True(WildlifeTrial.DeerWeather(WorldWeather.At("zone.vallons", at).Kind));
            Assert.Contains("lièvres sautillent", WildlifeTrial.MoveToWanderDusk(path, stamp: "essai2"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
