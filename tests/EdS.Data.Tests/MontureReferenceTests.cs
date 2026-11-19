using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28d (2), monture : moteur 0.34.0, journal au format 28, scénarios en schéma 22, instantané au format 9. Scénario de référence
/// <c>scenarios/espace_monture.json</c>, figé dans <c>Fixtures/reference-0.34.0-contenu-0.6.0-regles-0.18.0</c> : le Bastion monte en selle,
/// ne peut ni garder ni esquiver en selle, chevauche à 13 m/s, descend, marche à 5 m/s, puis ne peut pas remonter à moins de 12 m de l'ogre.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Scenario")]
public class MontureReferenceTests
{
    private const string Monture = "espace_monture";
    private const string Latest = "reference-0.34.0-contenu-0.6.0-regles-0.18.0";

    /// <summary>Empreinte du journal figé du scénario de la monture (format 28), documentée dans MOTEUR.md.</summary>
    public const string MontureFingerprint = "2ebc100044f566cc750f2a6c4c72bd93292662a994d069736f5c351ee55b753d";

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", Latest, name + ".jsonl");

    private static JsonObject Json() => JsonNode.Parse(File.ReadAllText(ScenarioPath(Monture)))!.AsObject();

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
    public void Versions()
    {
        Assert.Equal(("0.35.0", 29, 23, 10), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((Fixed.FromInt(13), Fixed.FromInt(12)), (MountSetup.HorseSpeed, ShippedData.Rules.Combat.MountSafeDistance));
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Fixture(Monture);
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen.Skip(1), Simulate(Load()).Skip(1));   // réconfort (moteur 0.35.0) : corps seulement
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(frozen, ShippedData).Status);   // réconfort : autre version du moteur
        Assert.Equal(MontureFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        Assert.Contains("\"mount\":{\"speed\":13}", frozen[0]);
        JsonElement[] events = Events(frozen);
        Assert.Equal(13m, Assert.Single(events, e => Kind(e) == "mounted").GetProperty("speed").GetDecimal());
        Assert.Single(events, e => Kind(e) == "dismounted");
        Assert.Equal(new[] { ("guardStart", "mounted"), ("dodge", "mounted"), ("dismount", "notMounted"), ("mount", "enemyNear") },
            events.Where(e => Kind(e) == "commandRejected").Select(e => (e.GetProperty("action").GetString()!, e.GetProperty("reason").GetString()!)));
        // En selle : de −50 à −11 en 60 ticks (13 m/s) ; à pied : de −11 à 1,5 en 50 ticks (5 m/s).
        Assert.Equal((-11m, 1.5m), (X(events, 61), X(events, 114)));
    }

    [Fact]
    public void Validation_de_la_monture_et_des_commandes()
    {
        // Schéma 21 refusé (réservé aux compagnons), schéma 22 lu.
        JsonObject old = Json();
        old["schemaVersion"] = 21;
        Assert.False(Read(old).IsValid);
        // Une monture seulement sur un personnage, à une vitesse dans les bornes techniques.
        JsonObject onOgre = Json();
        onOgre["data"]!["combatants"]![1]!["mount"] = new JsonObject { ["speed"] = 13 };
        Assert.Contains(Read(onOgre).Report.Errors, e => e.Path == "$.data.combatants[1].mount");
        JsonObject fast = Json();
        fast["data"]!["combatants"]![0]!["mount"]!["speed"] = 25;
        Assert.Contains(Read(fast).Report.Errors, e => e.Path.EndsWith(".mount.speed", StringComparison.Ordinal));
        JsonObject noSpeed = Json();
        noSpeed["data"]!["combatants"]![0]!["mount"] = new JsonObject();
        Assert.Contains(Read(noSpeed).Report.Errors, e => e.Path.EndsWith(".mount.speed", StringComparison.Ordinal));
        // Sans monture : ni mount ni dismount, ni arrivée en selle.
        JsonObject walker = Json();
        walker["data"]!["combatants"]![0]!.AsObject().Remove("mount");
        Assert.Contains(Read(walker).Report.Errors, e => e.Path.EndsWith(".actor", StringComparison.Ordinal));
        JsonObject carried = Json();
        carried["data"]!["combatants"]![0]!.AsObject().Remove("mount");
        carried["data"]!["commands"] = new JsonArray();
        carried["data"]!["combatants"]![0]!["arrivalMs"] = 100;
        carried["data"]!["combatants"]![0]!["carried"] = new JsonObject { ["mounted"] = true };
        Assert.Contains(Read(carried).Report.Errors, e => e.Path.EndsWith(".mounted", StringComparison.Ordinal));
        // Une direction seulement pour un déplacement ou une esquive.
        JsonObject direction = Json();
        direction["data"]!["commands"]![0]!["direction"] = new JsonArray(1, 0);
        Assert.False(Read(direction).IsValid);
    }

    [Fact]
    public void Arrivee_en_selle_et_etat_emporte()
    {
        // L'état emporté d'un cavalier dit qu'il est en selle ; un cavalier qui arrive en selle chevauche à 13 m/s sans commande mount.
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        while (engine.CurrentTick < 10) engine.Step();
        Assert.True(engine.Carried("bastion").Mounted);
        Assert.False(engine.Carried("bastion").IsEmpty);
        JsonObject root = Json();
        JsonObject bastion = root["data"]!["combatants"]![0]!.AsObject();
        bastion["arrivalMs"] = 50;
        bastion["carried"] = new JsonObject { ["mounted"] = true };
        root["data"]!["commands"] = new JsonArray(new JsonObject { ["atMs"] = 100, ["actor"] = "bastion", ["action"] = "move", ["direction"] = new JsonArray(1, 0) },
            new JsonObject { ["atMs"] = 150, ["actor"] = "bastion", ["action"] = "parry" });
        string[] lines = Simulate(Load(root));
        Assert.Contains("\"mounted\":true", lines[0]);
        JsonElement[] events = Events(lines);
        Assert.Equal("mounted", Assert.Single(events, e => Kind(e) == "commandRejected").GetProperty("reason").GetString());
        Assert.Equal(-50m + 13m * 20 * 50 / 1000, X(events, 21));   // 20 pas de 0,65 m
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(lines, ShippedData).Status);
    }

    [Fact]
    public void Instantane_en_selle_repris_a_l_identique_et_retouche_refusee()
    {
        CombatSetup setup = Load();
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 30) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.StartsWith("{\"snapshot\":10,", snapshot.Text);
        Assert.Contains("\"mounted\":true", snapshot.Text);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        int from = log.Lines.Count;
        while (!engine.IsFinished)
        {
            engine.Step();
            resumed.Step();
        }
        Assert.Equal(log.Lines.Skip(from), resumedLog.Lines);
        // En selle sans monture : refusé.
        JsonObject root = Json();
        root["data"]!["combatants"]![0]!.AsObject().Remove("mount");
        root["data"]!["commands"] = new JsonArray();
        var walker = new CombatEngine(Load(root), new MemoryCombatLog());
        while (walker.CurrentTick < 5) walker.Step();
        string tampered = walker.Snapshot().Text.Replace("\"mounted\":false", "\"mounted\":true");
        Assert.NotEqual(walker.Snapshot().Text, tampered);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(walker.CompletedSetup(), CombatSnapshot.Parse(tampered), new MemoryCombatLog()));
    }

    private static JsonObject Command(long atMs, string action) => new() { ["atMs"] = atMs, ["actor"] = "bastion", ["action"] = action };

    private static string[] Reasons(JsonObject root) =>
        Events(Simulate(Load(root))).Where(e => Kind(e) == "commandRejected").Select(e => e.GetProperty("reason").GetString()!).ToArray();

    [Fact]
    public void Refus_pour_monter_dans_l_ordre()
    {
        // Relecture indépendante de la PR #180 : déjà en selle, bond en cours, parade ouverte, absent ; puis rien ne change.
        JsonObject root = Json();
        root["data"]!["commands"] = new JsonArray(Command(0, "mount"), Command(50, "mount"), Command(100, "dismount"), Command(150, "dodge"),
            Command(200, "mount"), Command(500, "parry"), Command(550, "mount"));
        Assert.Equal(new[] { "alreadyMounted", "dodgeActive", "parryActive" }, Reasons(root));
        JsonObject late = Json();
        late["data"]!["combatants"]![0]!["arrivalMs"] = 1000;
        late["data"]!["commands"] = new JsonArray(Command(500, "mount"), Command(1500, "dismount"));
        Assert.Equal(new[] { "actorAbsent", "notMounted" }, Reasons(late));
    }

    [Fact]
    public void Frappe_en_selle_sans_descendre_soin_refuse_et_vue_du_pilote()
    {
        // L'ogre arrive à 2 m après la mise en selle et frappe : le cavalier reste en selle ; son soin est refusé (mounted).
        JsonObject root = Json();
        JsonObject ogre = root["data"]!["combatants"]![1]!.AsObject();
        ogre["position"] = new JsonArray(-48, 0);
        ogre["arrivalMs"] = 200;
        root["data"]!["combatants"]![0]!["consumables"] = new JsonArray(new JsonObject { ["id"] = "item.potion", ["heal"] = 100, ["charges"] = 2, ["cooldownMs"] = 1000 });
        root["data"]!["commands"] = new JsonArray(Command(0, "mount"),
            new JsonObject { ["atMs"] = 2000, ["actor"] = "bastion", ["action"] = "consume", ["item"] = "item.potion" });
        CombatSetup setup = Load(root);
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        while (engine.CurrentTick < 10) engine.Step();
        CombatantView view = engine.View("bastion");
        Assert.True(view.Mounted);
        Assert.Equal(MountSetup.HorseSpeed, view.MountSpeed);
        Assert.False(view.CanCast || view.CanGuard || view.CanParry || view.CanDodge || view.DodgeAvailable);
        string[] lines = Simulate(setup);
        JsonElement[] events = Events(lines);
        Assert.Contains(events, e => Kind(e) == "damage" && e.GetProperty("target").GetString() == "bastion");
        Assert.DoesNotContain(events, e => Kind(e) == "dismounted");
        Assert.Equal("mounted", Assert.Single(events, e => Kind(e) == "commandRejected").GetProperty("reason").GetString());
    }

    [Fact]
    public void Attaque_a_deux_refusee_en_selle()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("espace_compagnon")))!.AsObject();
        JsonObject bastion = root["data"]!["combatants"]!.AsArray().Single(c => (string?)c!["id"] == "bastion")!.AsObject();
        bastion["mount"] = new JsonObject { ["speed"] = 13 };
        bastion["arrivalMs"] = 50;
        bastion["carried"] = new JsonObject { ["mounted"] = true };
        var commands = new JsonArray();
        foreach (JsonNode? c in root["data"]!["commands"]!.AsArray())
            if ((string?)c!["action"] == "duo") commands.Add(c.DeepClone());
        root["data"]!["commands"] = commands;
        string[] reasons = Reasons(root);
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.Equal("mounted", r));
    }

    [Fact]
    public void Assaillant_jamais_en_selle()
    {
        // Relecture indépendante de la PR #180 : un assaillant n'emporte que ses PV, jamais « en selle ».
        JsonObject root = Json();
        JsonObject ogre = root["data"]!["combatants"]![1]!.AsObject();
        ogre["arrivalMs"] = 100;
        ogre["carried"] = new JsonObject { ["mounted"] = true };
        Assert.Contains(Read(root).Report.Errors, e => e.Path == "$.data.combatants[1].carried");
    }
}
