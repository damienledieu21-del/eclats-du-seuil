using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
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

internal static class V24Numbers
{
    public static double ToDouble(this Fixed f) => f.Raw / 10000.0;
}

/// <summary>
/// Sous-lot V2.4 (animaux paisibles et bêtes qui se battent) : moteur 0.28.0 et journal au format 22 (bêtes qui fuient, trêves entre équipes
/// de créatures, coups réduits entre créatures, une créature ne vise une créature qu'à sa distance d'engagement), scénarios en schéma 16,
/// règles en schéma 15 (contenu 0.16.0), instantané au format 5, zones en schéma 9 (populations selon le temps) ; nouvelle référence figée
/// <c>Fixtures/reference-0.30.0-contenu-0.6.0-regles-0.17.0</c> (les quatre journaux d'entraînement, corps identiques, et <c>espace_betes</c>).
/// </summary>
[Trait("Category", "Behavior")]
[Trait("Category", "Regression")]
public class V24AnimauxMoteurTests
{
    private const string Betes = "espace_betes";
    private const string Latest = "reference-0.30.0-contenu-0.6.0-regles-0.17.0", Previous = "reference-0.27.0-contenu-0.6.0-regles-0.15.0";

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

    private static CombatSetup Load(string name = Betes)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static List<JsonElement> Events(MemoryCombatLog log) =>
        log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static MemoryCombatLog Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    private static void AssertError(ScenarioLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.30.0", 24, 17, 6), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((16, "0.17.0-provisoire", "0.17.0-provisoire"), (DataFormat.RulesSchemaVersion, DataFormat.ProvisionalRulesContentVersion, ShippedData.Rules.ContentVersion));
        Assert.Equal(Fixed.Parse("0.5"), ShippedData.Rules.Combat.InfightingDamageReduction);
        Assert.Equal((13, 8, 7), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.PreviousSchemaVersion, ZoneValidator.OldestSchemaVersion));
    }

    [Fact]
    public void Reference_figee_identique_et_rejouee()
    {
        foreach (string name in new[] { Betes, "entrainement_bonus", "entrainement_bouclier", "entrainement_declencheurs", "entrainement_equipement" })
        {
            string[] frozen = File.ReadAllLines(Fixture(Latest, name));
            Assert.Equal(frozen, Simulate(Load(name)).Lines);
            Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        }
        // L'en-tête porte les trêves et la bête qui fuit.
        JsonElement scenario = JsonDocument.Parse(File.ReadLines(Fixture(Latest, Betes)).First()).RootElement.GetProperty("scenario");
        Assert.Equal(2, scenario.GetProperty("truces").GetArrayLength());
        Assert.True(scenario.GetProperty("combatants")[3].GetProperty("behavior").GetProperty("flee").GetBoolean());
    }

    [Theory]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // La référence du sous-lot V1.8d (moteur 0.27.0, format 21, règles 0.15.0) reste figée et refusée au rejeu ; sans créatures de deux
        // équipes ni bête qui fuit, le moteur 0.28.0 en reproduit octet par octet les lignes d'événements.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        ReplayOutcome outcome = CombatLogReplayer.Replay(old, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        JsonElement before = JsonDocument.Parse(old[0]).RootElement, after = JsonDocument.Parse(latest[0]).RootElement;
        Assert.Equal((21, "0.27.0", 24, "0.30.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString(),
            after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Loup_et_sanglier_se_battent_a_coups_reduits_le_bandit_reste_en_treve()
    {
        List<JsonElement> events = Events(Simulate(Load()));
        JsonElement[] damage = events.Where(e => e.GetProperty("event").GetString() == "damage").ToArray();
        Assert.NotEmpty(damage);
        // Morsure de 10, charge de 25 : réduites de moitié entre créatures (combat.infighting.damageReduction 0,5).
        Assert.All(damage.Where(e => e.GetProperty("source").GetString() == "loup"), e => Assert.Equal(("sanglier", 5L), (e.GetProperty("target").GetString(), e.GetProperty("amount").GetInt64())));
        Assert.All(damage.Where(e => e.GetProperty("source").GetString() == "sanglier"), e => Assert.Equal(("loup", 13L), (e.GetProperty("target").GetString(), e.GetProperty("amount").GetInt64())));
        // Le bandit (en trêve avec la meute et le gibier) ne frappe personne et n'est pas frappé ; le cerf ne se bat pas.
        Assert.DoesNotContain(damage, e => e.GetProperty("source").GetString() is "bandit" or "cerf" || e.GetProperty("target").GetString() is "bandit" or "cerf" or "bastion");
        Assert.Contains(events, e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("actor").GetString() == "cerf" && e.GetProperty("target").GetString() == "loup");
        Assert.DoesNotContain(events, e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("actor").GetString() == "bandit");
    }

    [Fact]
    public void Sans_treve_le_bandit_se_mele_au_combat()
    {
        CombatSetup setup = ReadMutated(r => r["data"]!.AsObject().Remove("truces"), scenario: Betes).Setup!;
        Assert.Empty(setup.Truces);
        List<JsonElement> events = Events(Simulate(setup));
        Assert.Contains(events, e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("actor").GetString() == "bandit");
        Assert.Contains(events, e => e.GetProperty("event").GetString() == "damage" && e.GetProperty("source").GetString() == "bandit");
    }

    [Fact]
    public void Le_cerf_fuit_sans_jamais_approcher()
    {
        CombatSetup setup = Load();
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        double previous = -1;
        bool fled = false;
        while (!engine.IsFinished)
        {
            engine.Step();
            CombatantView deer = engine.View("cerf"), wolf = engine.View("loup");
            double d = Distance(deer.Position!.Value, wolf.Position!.Value);
            if (previous >= 0 && d < 14 - 0.7 - 0.6 - 0.01 && deer.MoveDirection is GroundVector move)
            {
                // Engagé et trop près : il s'éloigne (sa marche l'écarte du loup).
                double dx = deer.Position.Value.X.ToDouble() - wolf.Position.Value.X.ToDouble(), dz = deer.Position.Value.Z.ToDouble() - wolf.Position.Value.Z.ToDouble();
                Assert.True(dx * move.X.ToDouble() + dz * move.Z.ToDouble() > 0, "le cerf marche vers le loup");
                fled = true;
            }
            previous = d;
            Assert.True(deer.Flees);
            Assert.Equal(90L, deer.Health);
        }
        Assert.True(fled);
        Assert.True(previous >= 14 - 0.7 - 0.6 - 0.5);   // hors de sa distance de fuite à la fin
    }

    private static double Distance(GroundVector a, GroundVector b) =>
        Math.Sqrt(Math.Pow(a.X.ToDouble() - b.X.ToDouble(), 2) + Math.Pow(a.Z.ToDouble() - b.Z.ToDouble(), 2));

    [Fact]
    public void Une_bete_ne_vise_une_bete_qu_a_sa_distance_d_engagement()
    {
        // Le loup et le sanglier éloignés de 30 m : personne ne s'engage ; un personnage, lui, est visé à toute distance (la laisse décide).
        CombatSetup setup = ReadMutated(r =>
        {
            JsonArray c = r["data"]!["combatants"]!.AsArray();
            c[2]!["position"] = new JsonArray(20, 20);
            c[3]!["position"] = new JsonArray(-10, 20);
        }, scenario: Betes).Setup!;
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        for (int i = 0; i < 60; i++) engine.Step();
        Assert.False(engine.View("loup").Engaged || engine.View("sanglier").Engaged);
        Assert.Equal(("bastion", "bastion"), (engine.View("loup").CurrentTarget, engine.View("sanglier").CurrentTarget));
    }

    [Fact]
    public void Frappee_par_un_personnage_et_gardee_dans_l_instantane()
    {
        // Le Bastion frappe le cerf acculé : seul un coup de personnage marque la bête (le monde ne compte que ses victoires).
        CombatSetup setup = ReadMutated(r =>
        {
            JsonArray c = r["data"]!["combatants"]!.AsArray();
            c[0]!["position"] = new JsonArray(3, 7.5);
            r["data"]!["commands"] = new JsonArray(new JsonObject { ["atMs"] = 100, ["actor"] = "bastion", ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "cerf" });
        }, scenario: Betes).Setup!;
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 40) engine.Step();
        Assert.True(engine.View("cerf").HurtByCharacter);
        Assert.False(engine.View("sanglier").HurtByCharacter);   // mordu par le loup seulement
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.StartsWith("{\"snapshot\":6,", snapshot.Text);
        Assert.Contains("\"hurtByCharacter\":true", snapshot.Text);
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, new MemoryCombatLog());
        Assert.True(resumed.View("cerf").HurtByCharacter);
    }

    [Fact]
    public void Budget_epuise_a_l_engagement_rien_n_est_engage_ni_remarque()
    {
        // Revue Codex de la PR #104 (tour 2), test adversarial : le Bastion à 3 m du loup (il le remarque en s'engageant), budget de travail
        // du tick ramené pas à pas de 1 à 400 : quel que soit l'endroit où le tick s'arrête (budgetExceeded), aucune bête n'a « remarqué » un
        // joueur sans être engagée (refus atomique : rien n'est affecté avant la réservation), et l'arrêt tombe au moins une fois dans la
        // phase des engagements.
        CombatSetup baseSetup = ReadMutated(r => r["data"]!["combatants"]![0]!["position"] = new JsonArray(1, 0), scenario: Betes).Setup!;
        bool inEngagements = false;
        for (int work = 1; work <= 400; work++)
        {
            var rules = (CombatRules)typeof(CombatRules).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(baseSetup.Combat, null)!;
            typeof(CombatRules).GetProperty(nameof(CombatRules.MaxWorkPerTick))!.SetValue(rules, work);
            typeof(CombatRules).GetProperty(nameof(CombatRules.MaxEventsPerTick))!.SetValue(rules, Math.Min(work, baseSetup.Combat.MaxEventsPerTick));
            var setup = new CombatSetup(baseSetup.Seed, baseSetup.DurationMs, baseSetup.Stats, baseSetup.Damage, baseSetup.Progression, rules,
                baseSetup.Combatants, baseSetup.Commands, baseSetup.Data, baseSetup.Space, baseSetup.Zone, baseSetup.Truces);
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(setup, log);
            for (int i = 0; i < 40 && !engine.IsFinished; i++)
            {
                engine.Step();
                foreach (string id in engine.CombatantIds)
                {
                    CombatantView v = engine.View(id);
                    Assert.True(!v.Noticed || v.Engaged, $"budget {work} : {id} a remarqué un joueur sans être engagé");
                }
            }
            // Arrêt au milieu des engagements du tick 0 (cerf, loup, sanglier, dans l'ordre des identifiants) : certains engagés, pas tous.
            List<JsonElement> events = Events(log);
            int engaged = events.Count(e => e.GetProperty("event").GetString() == "engaged" && e.GetProperty("tick").GetInt64() == 0);
            if (engaged is > 0 and < 3 && events.Any(e => e.GetProperty("event").GetString() == "simulationAborted" && e.GetProperty("tick").GetInt64() == 0))
                inEngagements = true;
        }
        Assert.True(inEngagements, "aucun arrêt dans la phase des engagements");
    }

    [Fact]
    public void Formes_refusees_avec_leur_chemin()
    {
        AssertError(ReadMutated(r => r["data"]!["combatants"]![3]!["behavior"]!["minRange"] = 0, scenario: Betes), "$.data.combatants[3].behavior.minRange");
        AssertError(ReadMutated(r => r["data"]!["combatants"]![3]!["behavior"]!["encircle"] = true, scenario: Betes), "$.data.combatants[3].behavior.encircle");
        AssertError(ReadMutated(r => r["data"]!["combatants"]![3]!["behavior"]!["flee"] = "oui", scenario: Betes), "$.data.combatants[3].behavior.flee");
        AssertError(ReadMutated(r => r["data"]!["truces"]![0] = new JsonArray("meute", "meute"), scenario: Betes), "$.data.truces[0]");
        AssertError(ReadMutated(r => r["data"]!["truces"]![1] = new JsonArray("bandits", "meute"), scenario: Betes), "$.data.truces[1]");   // doublon (l'autre sens)
        AssertError(ReadMutated(r => r["data"]!["truces"]![0] = new JsonArray("meute", "joueurs"), scenario: Betes), "$.data.truces[0]");   // équipe d'un personnage
        AssertError(ReadMutated(r => r["data"]!["truces"]![0] = new JsonArray("meute", "Bandits"), scenario: Betes), "$.data.truces[0]");
        AssertError(ReadMutated(r => r["data"]!["truces"]![0] = new JsonArray("meute"), scenario: Betes), "$.data.truces[0]");
        AssertError(ReadMutated(r => r["data"]!["truces"]![0] = new JsonArray("meute", "bandits", "gibier"), scenario: Betes), "$.data.truces[0]");
        // La cible d'un assaillant ne peut pas être en trêve avec lui.
        AssertError(ReadMutated(r => r["data"]!["combatants"]![1]!["target"] = "bandit", scenario: Betes), "$.data.combatants[1].target");
        // Schéma 15 refusé (sans migration).
        AssertError(ReadMutated(r => r["schemaVersion"] = 15, scenario: Betes), "$.schemaVersion");
    }

    [Fact]
    public void Schemas_JSON_du_sous_lot()
    {
        JsonNode scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(17, scenario["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.NotNull(scenario["properties"]!["data"]!["properties"]!["truces"]);
        JsonNode rules = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json")))!;
        Assert.Equal(16, rules["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode zone = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json")))!;
        Assert.Equal(new[] { 7, 8, 9, 10, 11, 12 }, zone["properties"]!["schemaVersion"]!["enum"]!.AsArray().Select(v => v!.GetValue<int>()));
    }
}

/// <summary>Sous-lot V2.4 : le premier écosystème des Vallons (zones en schéma 9, populations selon le temps, bêtes paisibles).</summary>
[Trait("Category", "World")]
public class V24AnimauxMondeTests
{
    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly string[] Mist = { "loup_brume1", "loup_brume2", "loup_brume3", "loup_echo5", "loup_echo6" };
    private static readonly string[] Deer = Enumerable.Range(1, 6).Select(i => "cerf" + i).ToArray();
    private static readonly string[] Hares = Enumerable.Range(1, 4).Select(i => "lievre" + i).ToArray();

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    /// <summary>Premier jour (à partir de 1) où il fait <paramref name="kind"/> à <paramref name="minute"/>.</summary>
    private static long DayWith(WeatherKind kind, int minute)
    {
        for (long d = 1; d < 400; d++) if (WorldWeather.At("zone.vallons", d, minute).Kind == kind) return d;
        throw new InvalidOperationException("temps introuvable");
    }

    private static WorldSession At(long tick, ZoneDefinition? zone = null) =>
        new(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, Hero(1000)), zone ?? Vallons, ShippedData, Progression, ItemsCatalog, Book(zone ?? Vallons));

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

    private static int Index(string creature) => Vallons.Populations.Select((p, i) => (p.Creature, i)).Single(x => x.Creature == creature).i;

    private static void AssertError(ZoneLoadResult r, string path, ValidationCode code)
    {
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Populations_livrees_et_leurs_temps()
    {
        Assert.Equal((13, "0.17.0-provisoire"), (Vallons.SchemaVersion, Vallons.ContentVersion));   // V2.25.3 : schéma 13 ; V2.6.1 : schéma 12, contenu 0.15.0 ; V2.6 : schéma 11, contenu 0.14.0 ; V2.5 : schéma 10, contenu 0.13.0
        Assert.All(Mist, id => Assert.Equal(new[] { "mist" }, Vallons.PopulationOf(id)!.Weather!.Kinds));
        Assert.All(Mist.Take(3), id => Assert.Equal(("loup", "loot.loup"), (Vallons.PopulationOf(id)!.Family, Vallons.PopulationOf(id)!.Loot)));
        Assert.All(Mist.Skip(3), id => Assert.Equal(("echo", "loot.echo"), (Vallons.PopulationOf(id)!.Family, Vallons.PopulationOf(id)!.Loot)));
        Assert.All(Deer, id =>
        {
            Population p = Vallons.PopulationOf(id)!;
            Assert.Equal(("cerf", (string?)null), (p.Family, p.Loot));
            Assert.True(p.Hours!.IsActive((int)DayPhase.Dawn) && p.Hours.IsActive((int)DayPhase.Day) && !p.Hours.IsActive((int)DayPhase.Night));
            Assert.Equal(new[] { "clear", "cloudy", "mist", "drizzle" }, p.Weather!.Kinds);
            Assert.True(p.Template.Behavior!.Flee);
        });
        Assert.All(Hares, id => Assert.True(Vallons.PopulationOf(id)!.Hours!.IsActive((int)DayPhase.Night) && Vallons.PopulationOf(id)!.Template.Behavior!.Flee));
        // Une annonce par groupe, des textes du jeu.
        Assert.Equal("La brume monte de la rivière. Des loups gris descendent des bois vers les champs…", Texts.Get(Vallons.PopulationOf("loup_brume1")!.TimedSign!));
        Assert.Null(Vallons.PopulationOf("loup_brume2")!.TimedSign);
        Assert.NotNull(Texts.Get(Vallons.PopulationOf("loup_echo5")!.TimedSign!));
        Assert.NotNull(Texts.Get(Vallons.PopulationOf("cerf1")!.TimedSign!));
        Assert.NotNull(Texts.Get(Vallons.PopulationOf("lievre1")!.TimedSign!));
        // Équipes : la meute (loups), les échos, le gibier (sangliers, cerfs, lièvres), les bandits ; seule la meute et le gibier se battent.
        CombatSetup s = Vallons.Setup;
        string Team(string id) => s.Combatants.Single(c => c.Id == id).Team;
        Assert.Equal(("meute", "echos", "gibier", "gibier", "gibier", "bandits"), (Team("loup_brume1"), Team("loup_echo5"), Team("sanglier"), Team("cerf1"), Team("lievre1"), Team("archer")));
    }

    [Fact]
    public void Aucune_bete_hostile_ne_commence_a_portee_d_une_autre()
    {
        // Les bêtes ennemies (meute et gibier) ne s'engagent pas d'elles-mêmes à leur place : il faut qu'on les mène l'une vers l'autre (une
        // poursuite, une fuite). Sinon elles se battraient à chaque retour et useraient les vies de la session.
        AssailantSetup[] beasts = Vallons.Setup.Combatants.OfType<AssailantSetup>().ToArray();
        foreach (AssailantSetup a in beasts)
            foreach (AssailantSetup b in beasts)
            {
                if (string.CompareOrdinal(a.Id, b.Id) >= 0 || !Vallons.Setup.Hostile(a.Team, b.Team)) continue;
                double d = Math.Sqrt(Math.Pow(a.Placement!.Position.X.ToDouble() - b.Placement!.Position.X.ToDouble(), 2)
                    + Math.Pow(a.Placement.Position.Z.ToDouble() - b.Placement.Position.Z.ToDouble(), 2)) - a.Placement.Radius.ToDouble() - b.Placement.Radius.ToDouble();
                double aggro = Math.Max(a.Behavior?.AggroRange?.ToDouble() ?? 0, b.Behavior?.AggroRange?.ToDouble() ?? 0);
                Assert.True(d > aggro + 1, $"{a.Id} et {b.Id} à {d:0.#} m");
            }
    }

    [Fact]
    public void Brume_du_matin_loups_gris_et_echos()
    {
        long misty = DayWith(WeatherKind.Mist, 6 * 60), clear = DayWith(WeatherKind.Clear, 6 * 60);
        WorldSession w = At(TickAt(misty, 6 * 60));
        w.Step();
        Assert.All(Mist, id => Assert.True(Present(w, id), id));
        Assert.All(Mist, id => Assert.True(w.IsAvailable(Vallons.PopulationOf(id)!)));
        WorldSession c = At(TickAt(clear, 6 * 60));
        c.Step();
        Assert.All(Mist, id => Assert.False(Present(c, id), id));
        Assert.All(Mist, id => Assert.False(c.IsAvailable(Vallons.PopulationOf(id)!)));
        // Sans brume, l'étoile d'un objectif de famille « loup » vise toujours un loup qu'on peut trouver (pas un loup gris absent).
        Assert.All(Vallons.Populations.Where(p => p.Family == "loup" && c.IsAvailable(p)), p => Assert.False(Mist.Contains(p.Creature)));
        Assert.Equal(ZoneSetups.For(Vallons, Array.Empty<string>(), DayPhase.Dawn, WeatherKind.Mist).Combatants.Count,
            ZoneSetups.For(Vallons, Array.Empty<string>(), DayPhase.Dawn, WeatherKind.Clear).Combatants.Count + 5);
    }

    [Fact]
    public void La_brume_monte_puis_se_leve()
    {
        long misty = DayWith(WeatherKind.Mist, 6 * 60);
        int rise = Enumerable.Range(4 * 60, 120).First(m => WorldWeather.At("zone.vallons", misty, m).Kind == WeatherKind.Mist);
        WorldSession w = At(TickAt(misty, rise - 2));
        var events = new List<PopulationEvent>();
        for (int i = 0; i < 600; i++) events.AddRange(w.Step().Population);
        Assert.Contains(events, e => e.Kind == PopulationEventKind.Sign && e.Creature == "text.zone.loups_brume.signe");
        Assert.Contains(events, e => e.Kind == PopulationEventKind.Sign && e.Creature == "text.zone.echos_brume.signe");
        Assert.All(Mist, id => Assert.True(Present(w, id), id));
        // Quand elle se lève, les bêtes de la brume s'en vont au calme (sans mort ni butin).
        int gone = Enumerable.Range(rise, 400).First(m => WorldWeather.At("zone.vallons", misty, m).Kind != WeatherKind.Mist);
        WorldSession later = At(TickAt(misty, gone - 2));
        var after = new List<PopulationEvent>();
        for (int i = 0; i < 600; i++) after.AddRange(later.Step().Population);
        Assert.All(Mist, id => Assert.Contains(after, e => e.Kind == PopulationEventKind.Withdrew && e.Population == id));
        Assert.All(Mist, id => Assert.False(Present(later, id)));
    }

    [Fact]
    public void Cerfs_a_l_aube_et_le_jour_abrites_sous_l_averse_lievres_au_crepuscule_et_la_nuit()
    {
        long clear = DayWith(WeatherKind.Clear, 12 * 60);
        WorldSession noon = At(TickAt(clear, 12 * 60));
        noon.Step();
        Assert.All(Deer, id => Assert.True(Present(noon, id), id));
        Assert.All(Hares, id => Assert.False(Present(noon, id), id));
        long shower = DayWith(WeatherKind.Shower, 12 * 60);
        WorldSession wet = At(TickAt(shower, 12 * 60));
        wet.Step();
        Assert.All(Deer, id => Assert.False(Present(wet, id), id));
        WorldSession night = At(TickAt(clear, 23 * 60));
        night.Step();
        Assert.All(Hares, id => Assert.True(Present(night, id), id));
        Assert.All(Deer, id => Assert.False(Present(night, id), id));
    }

    [Fact]
    public void Une_bete_n_en_tue_jamais_une_autre()
    {
        // Relecture de la PR #104 : un sanglier posé au milieu des loups du pré : ils se battent, il tombe à 1 PV et y reste (les loups ne
        // le visent plus) ; aucune mort, donc aucune vie de la session usée et aucune récompense. C'est au joueur d'achever.
        ZoneDefinition z = Zone(scenario: s =>
        {
            JsonArray c = s["data"]!["combatants"]!.AsArray();
            c.Select(x => x!.AsObject()).Single(x => (string)x["id"]! == "sanglier")["position"] = new JsonArray(-12, 25);
        });
        WorldSession w = At(TickAt(0, 12 * 60), z);
        var experience = new List<ExperienceGained>();
        for (int i = 0; i < 3000; i++) experience.AddRange(w.Step().Experience);
        CombatantView boar = w.Zone.Engine.View("sanglier");
        Assert.False(boar.IsDead);
        Assert.Equal(1L, boar.Health);
        Assert.False(boar.HurtByCharacter);
        Assert.All(w.Zone.Engine.CombatantIds, id => Assert.False(w.Zone.Engine.View(id).IsDead));
        Assert.Empty(experience);
        // Les loups ne le visent plus : plus aucun coup après sa chute à 1 PV.
        long taken = boar.Counters.DamageTaken;
        for (int i = 0; i < 400; i++) w.Step();
        Assert.Equal(taken, w.Zone.Engine.View("sanglier").Counters.DamageTaken);
    }

    [Fact]
    public void Un_cerf_qui_fuit_n_empeche_pas_de_voyager()
    {
        // Revue Codex de la PR #104 (tour 3) : le joueur arrive à 6 m d'un cerf (sud du lac, midi par temps clair) : la harde le remarque et
        // fuit, reste engagée et le vise de loin (pas de laisse pour une bête qui fuit), mais ce n'est pas un combat : il peut voyager.
        long clear = DayWith(WeatherKind.Clear, 12 * 60);
        WorldSession w = At(TickAt(clear, 12 * 60), Zone(-8, -66));
        for (int i = 0; i < 200; i++) w.Step();
        CombatantView deer = w.Zone.Engine.View(w.CurrentCreature("cerf3"));
        Assert.True(deer.Engaged && deer.Noticed && deer.Flees);
        Assert.Equal("bastion", deer.CurrentTarget);
        Assert.False(w.InCombat);
        Assert.NotEqual(TravelRefusal.InCombat, w.CanTravel(Vallons, "place_du_village"));
    }

    [Fact]
    public void Un_loup_gris_engage_a_portee_reste_un_combat()
    {
        // Revue Codex de la PR #104 (tour 3) : un ennemi engagé qui vise le joueur compte à sa laisse (au-delà, la laisse du monde le renvoie).
        long misty = DayWith(WeatherKind.Mist, 6 * 60);
        WorldSession w = At(TickAt(misty, 6 * 60), Zone(-36, -24));   // à quelques mètres des loups gris
        bool engaged = false;
        for (int i = 0; i < 300 && !engaged; i++)
        {
            w.Step();
            engaged = w.Zone.Engine.View(w.CurrentCreature("loup_brume1")).Engaged;
        }
        Assert.True(engaged);
        Assert.True(w.InCombat);   // à portée : c'est un combat
    }

    [Fact]
    public void Longue_session_sans_le_joueur_les_populations_des_quetes_repeuplent_toujours()
    {
        // Relecture de la PR #104 : une session entière (de 4 h à 11 h d'un matin de brume, presque une heure de jeu) où le joueur ne fait rien, avec
        // de la prédation forcée : le sanglier des ruines (contrats) au milieu des loups du pré, deux cerfs et un lièvre à côté d'eux. Les
        // bêtes se battent, fuient, arrivent à l'aube et avec la brume, partent quand elle se lève. Aucune mort sans le joueur (une bête n'en
        // tue jamais une autre), aucune laisse pour une bête qui fuit, les bêtes d'ambiance ne prennent jamais les 64 dernières places : à la
        // fin, une population de quête repeuple encore 60 fois de suite.
        ZoneDefinition z = Zone(scenario: s =>
        {
            JsonArray c = s["data"]!["combatants"]!.AsArray();
            JsonObject Of(string id) => c.Select(x => x!.AsObject()).Single(x => (string)x["id"]! == id);
            Of("sanglier")["position"] = new JsonArray(-12, 25);
            Of("cerf1")["position"] = new JsonArray(-9, 26);
            Of("cerf2")["position"] = new JsonArray(-14, 24);
            Of("lievre1")["position"] = new JsonArray(-7, 27);
        });
        long misty = DayWith(WeatherKind.Mist, 6 * 60);
        WorldSession w = At(TickAt(misty, 4 * 60), z);
        var population = new List<PopulationEvent>();
        var deaths = new List<string>();
        long creatureHits = 0;
        int max = ShippedData.Rules.Combat.MaxCombatants;
        while (!w.Zone.IsOver && w.Zone.Time.Hour < 11)
        {
            SessionStep step = w.Step();
            population.AddRange(step.Population);
            Assert.Empty(step.Experience);
        }
        foreach (string id in w.Zone.Engine.CombatantIds)
        {
            CombatantView v = w.Zone.Engine.View(id);
            if (v.IsDead) deaths.Add(id);
            if (v.Kind == CombatantKind.Assailant) creatureHits += v.Counters.DamageTaken;
        }
        Assert.True(w.Zone.Time.Hour >= 11);
        Assert.True(creatureHits > 0, "les bêtes ne se sont pas battues");
        Assert.Empty(deaths);   // aucune mort sans le joueur
        Assert.DoesNotContain(population, e => e.Kind == PopulationEventKind.Exhausted);
        Assert.DoesNotContain(population, e => e.Kind == PopulationEventKind.Leashed && Vallons.PopulationOf(e.Population)!.Template.Behavior!.Flee);
        Assert.Contains(population, e => e.Kind == PopulationEventKind.Withdrew && Mist.Contains(e.Population));   // la brume s'est levée
        Assert.True(max - w.Zone.Engine.CombatantIds.Count >= ZonePopulations.QuestReserve - 8,
            $"{w.Zone.Engine.CombatantIds.Count} combattants sur {max}");
        // Une population de quête (le loup du moulin, objectifs « loup ») repeuple toujours : 60 départs de suite, 60 retours.
        for (int n = 0; n < 60; n++)
        {
            string current = w.CurrentCreature("loup1");
            w.Zone.Leave(current);
            bool back = false;
            for (int i = 0; i < 40 && !back; i++)
                back = w.Step().Population.Any(e => e.Kind == PopulationEventKind.Respawned && e.Population == "loup1");
            Assert.True(back, $"loup1 n'est pas revenu au départ n° {n + 1}");
        }
    }

    [Fact]
    public void Formats_de_zone_refuses_avec_leur_chemin()
    {
        int brume = Index("loup_brume1");
        string at = $"$.data.populations[{brume}].weather";
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["kinds"] = new JsonArray("brouillard")), at + ".kinds[0]", ValidationCode.InvalidValue);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["kinds"] = new JsonArray("mist", "mist")), at + ".kinds[1]", ValidationCode.DuplicateId);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["kinds"] = new JsonArray("rain", "drizzle")), at + ".kinds[1]", ValidationCode.DuplicateId);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["kinds"] = new JsonArray("clear", "cloudy", "mist", "rain")), at + ".kinds", ValidationCode.OutOfRange);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["kinds"] = new JsonArray()), at + ".kinds", ValidationCode.OutOfRange);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!.AsObject().Remove("kinds")), at + ".kinds", ValidationCode.MissingField);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["weather"]!["sign"] = "brume"), at + ".sign", ValidationCode.InvalidValue);
        AssertError(ZoneMutated(r => Population(r, "loup_brume1")["night"] = new JsonObject { ["chancePercent"] = 10, ["signMs"] = 0, ["sign"] = "text.zone.loup_blanc.signe" }),
            at, ValidationCode.InvalidValue);
        // Schéma 8 : pas de temps ; il est encore lu sans eux.
        AssertError(ZoneMutated(r => r["schemaVersion"] = 8), at, ValidationCode.InvalidValue);
        ZoneLoadResult v8 = ZoneMutated(r =>
        {
            r["schemaVersion"] = 8;
            r["data"]!.AsObject().Remove("region");   // V2.25.3 (schéma 13)
            foreach (JsonNode? p in r["data"]!["populations"]!.AsArray()) p!.AsObject().Remove("weather");
            r["data"]!.AsObject().Remove("events");   // V2.5 (schéma 10)
        });
        Assert.True(v8.IsValid, v8.Report.ToString());
        Assert.All(v8.Zone!.Populations, p => Assert.Null(p.Weather));
        AssertError(ZoneMutated(r => r["schemaVersion"] = 14), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
    }

    [Fact]
    public void Formes_simples_des_betes_paisibles()
    {
        Assert.Equal((WildlifeKind.Deer, WildlifeKind.Hare, WildlifeKind.None, WildlifeKind.None),
            (Wildlife.KindOf("cerf2_v3"), Wildlife.KindOf("lievre1"), Wildlife.KindOf("loup1"), Wildlife.KindOf("sanglier")));
        foreach (WildlifeKind kind in new[] { WildlifeKind.Deer, WildlifeKind.Hare })
        {
            IReadOnlyList<WildlifePart> parts = Wildlife.Parts(kind);
            Assert.Equal(new[] { 1, 2, 3, 4 }, parts.Where(p => p.Leg > 0).Select(p => p.Leg).OrderBy(l => l));
            Assert.All(parts, p => Assert.True(p.Y >= 0 && p.ScaleX > 0 && p.ScaleY > 0 && p.ScaleZ > 0 && p.R >= 0 && p.R <= 1));
            Assert.Equal(parts.Count, parts.Select(p => p.Name).Distinct().Count());
            // À l'arrêt, aucun mouvement ; à la course, borné et reproductible.
            (double still, double[] legs) = Wildlife.Gait(kind, 0, 3.3);
            Assert.Equal(0, still);
            Assert.All(legs, l => Assert.Equal(0, l));
            for (double t = 0; t < 2; t += 0.05)
            {
                (double bob, double[] swing) = Wildlife.Gait(kind, 9, t);
                Assert.InRange(bob, 0, 0.25);
                Assert.All(swing, l => Assert.InRange(l, -40, 40));
                Assert.Equal(swing, Wildlife.Gait(kind, 9, t).Legs);
            }
        }
        Assert.Contains(Wildlife.Parts(WildlifeKind.Deer), p => p.Name.StartsWith("bois", StringComparison.Ordinal));
        Assert.Contains(Wildlife.Parts(WildlifeKind.Hare), p => p.Name.StartsWith("oreille", StringComparison.Ordinal));
        Assert.Empty(Wildlife.Parts(WildlifeKind.None));
        Assert.Equal(("Cerf", "Lièvre", "Loup gris"), (DisplayNames.Of("cerf1_v2"), DisplayNames.Of("lievre3"), DisplayNames.Of("loup_brume2")));
    }

    [Fact]
    public void Essai_des_animaux()
    {
        var clock = new WorldClock(50);
        long dawn = WildlifeTrial.NextDeerDawn(clock, "zone.vallons", 0)!.Value;
        WorldTime t = clock.At(dawn);
        Assert.Equal(WildlifeTrial.DeerMinute, t.Minute);
        Assert.True(WildlifeTrial.DeerWeather(WorldWeather.At("zone.vallons", t).Kind));
        // Les cerfs sont bien là à ce moment (aube, temps à cerfs).
        WorldSession w = At(dawn);
        w.Step();
        Assert.All(Deer, id => Assert.True(Present(w, id), id));
        // Menu de l'éditeur (comme l'essai de la nuit et celui de la météo).
        string menu = File.ReadAllText(Path.Combine(RepositoryRoot(), "unity", "Assets", "EdS", "Editor", "EdsWildlifeTrial.cs"));
        Assert.Contains("Éclats du Seuil/Essai des animaux : l'aube des cerfs", menu);
        Assert.Contains("Éclats du Seuil/Essai des animaux : la brume (loups gris et échos)", menu);
    }

    private static string RepositoryRoot()
    {
        string? d = AppContext.BaseDirectory;
        while (d != null && !File.Exists(Path.Combine(d, "global.json"))) d = Path.GetDirectoryName(d);
        return d ?? throw new InvalidOperationException("dépôt introuvable");
    }
}
