using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Feasts;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot F7, réconfort d'un plat de fête (décision de Damien du 19 novembre 2026 : +10 % de marche hors combat pendant 60 minutes de jeu,
/// aucune statistique de combat) : moteur 0.35.0, journal au format 29, scénarios en schéma 23, instantané au format 10. Scénario de
/// référence <c>scenarios/espace_reconfort.json</c>, figé dans <c>Fixtures/reference-0.35.0-contenu-0.6.0-regles-0.18.0</c> (les autres
/// journaux y ont exactement le même corps qu'en 0.34.0) : le Bastion marche à 5 m/s, est réconforté (5,5 m/s), puis ne l'est plus.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Scenario")]
public class ReconfortReferenceTests
{
    private const string Reconfort = "espace_reconfort";
    private const string Latest = "reference-0.35.0-contenu-0.6.0-regles-0.18.0", Previous = "reference-0.34.0-contenu-0.6.0-regles-0.18.0";

    /// <summary>Empreinte du journal figé du scénario du réconfort (format 29), documentée dans MOTEUR.md.</summary>
    public const string ReconfortFingerprint = "0990255f12ff4cdd9ececf537c419d1024133a8f5b7fc905136fb8132b8aa6ec";

    private static string Fixture(string dir, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", dir, name + ".jsonl");

    private static JsonObject Json() => JsonNode.Parse(File.ReadAllText(ScenarioPath(Reconfort)))!.AsObject();

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
        Assert.Equal(("0.35.0", 29, 23, 10), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal(Fixed.FromRatio(11, 10), ShippedData.Rules.Combat.ComfortSpeedFactor);
        Assert.Equal(("comfort", "comfortEnd"), (CombatActionIds.Of(CombatAction.Comfort), CombatActionIds.Of(CombatAction.ComfortEnd)));
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Fixture(Latest, Reconfort);
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen, Simulate(Load()));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        Assert.Equal(ReconfortFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        JsonElement[] events = Events(frozen);
        Assert.Equal(1.1m, Assert.Single(events, e => Kind(e) == "comforted").GetProperty("factor").GetDecimal());
        Assert.Single(events, e => Kind(e) == "comfortEnded");
        Assert.Equal(new[] { ("comfortEnd", "notComforted"), ("comfort", "alreadyComforted") },
            events.Where(e => Kind(e) == "commandRejected").Select(e => (e.GetProperty("action").GetString()!, e.GetProperty("reason").GetString()!)));
        // À pied : 0,25 m par tick (5 m/s) ; réconforté : 0,275 m (5,5 m/s, +10 %) ; de nouveau 0,25 m après comfortEnd.
        Assert.Equal(0.25m, X(events, 20) - X(events, 19));
        Assert.Equal(0.275m, X(events, 22) - X(events, 21));
        Assert.Equal(0.25m, X(events, 42) - X(events, 41));
    }

    [Fact]
    public void Autres_journaux_de_la_reference_0_34_0_meme_corps()
    {
        // La nouvelle référence n'a changé que l'en-tête (versions) des journaux d'avant : sans réconfort, mêmes lignes d'événements.
        string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", Previous);
        string[] names = Directory.GetFiles(dir, "*.jsonl").Select(f => Path.GetFileNameWithoutExtension(f)!).ToArray();
        Assert.True(names.Length >= 11);
        foreach (string name in names)
        {
            string[] before = File.ReadAllLines(Fixture(Previous, name)), after = File.ReadAllLines(Fixture(Latest, name));
            Assert.Equal(before.Skip(1), after.Skip(1));
            Assert.Contains("\"formatVersion\":29", after[0]);
            Assert.Contains("\"engineVersion\":\"0.35.0\"", after[0]);
            Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(before, ShippedData).Status);   // journal historique : autre moteur
        }
    }

    [Fact]
    public void Validation_des_commandes_de_reconfort()
    {
        JsonObject old = Json();
        old["schemaVersion"] = 22;
        Assert.False(Read(old).IsValid);   // schéma 22 refusé (réservé à la monture)
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
        flat["data"]!["commands"] = new JsonArray(new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "comfort" });
        Assert.Contains(Read(flat).Report.Errors, e => e.Path.EndsWith(".action", StringComparison.Ordinal));
    }

    [Fact]
    public void Instantane_reconforte_repris_a_l_identique_et_retouche_refusee()
    {
        CombatSetup setup = Load();
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 30) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.StartsWith("{\"snapshot\":10,", snapshot.Text);
        Assert.Contains("\"comforted\":true", snapshot.Text);
        Assert.True(engine.View("bastion").Comforted);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        int from = log.Lines.Count;
        while (!engine.IsFinished)
        {
            engine.Step();
            resumed.Step();
        }
        Assert.Equal(log.Lines.Skip(from), resumedLog.Lines);
        // Réconforté sans commande comfort : refusé.
        JsonObject root = Json();
        root["data"]!["commands"] = new JsonArray();
        var plain = new CombatEngine(Load(root), new MemoryCombatLog());
        while (plain.CurrentTick < 5) plain.Step();
        string tampered = plain.Snapshot().Text.Replace("\"comforted\":false", "\"comforted\":true");
        Assert.NotEqual(plain.Snapshot().Text, tampered);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(plain.CompletedSetup(), CombatSnapshot.Parse(tampered), new MemoryCombatLog()));
    }

    [Fact]
    public void Le_monde_veut_le_reconfort_soixante_minutes_hors_combat_seulement()
    {
        ZoneDefinition zone = Zone(-1088.5, -36.2);
        FeastBook book = FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Book(zone), Texts).Book!;
        long start = WorldEventSchedule.TickAt(new WorldClock(50), 15 * 1440 + 12 * 60);
        var w = new WorldSession(new WorldSave("zone.vallons", start, 0, new[] { PlayerCharacters.PlacesKey },
            Hero().WithFeasts(new SavedFeasts(new[] { new SavedFeast("festival.carnaval", dishes: 2) }))),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        w.Step();
        w.UseFeasts(book);
        Assert.False(w.ComfortWanted);
        Assert.Null(w.ComfortUntilMinute);
        Assert.Equal(FeastRefusal.None, w.EatFeastDish("festival.carnaval"));
        long now = w.Zone.Time.Day * 1440 + w.Zone.Time.Minute;
        Assert.Equal(now + WorldSession.ComfortMinutes, w.ComfortUntilMinute);
        Assert.True(w.ComfortWanted);
        // Report sur une autre session du monde (porte, voyage) : hors sauvegarde, mais suit le personnage.
        var other = new WorldSession(new WorldSave("zone.vallons", start, 0, new[] { PlayerCharacters.PlacesKey }, Hero()),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        other.CarryComfort(w.ComfortUntilMinute);
        Assert.Equal(w.ComfortUntilMinute, other.ComfortUntilMinute);
        // En combat : plus voulu (le pont envoie comfortEnd).
        w.Zone.Leave(w.CombatantId);
        w.Step();
        Assert.True(w.InCombat);
        Assert.False(w.ComfortWanted);
        // Rien de cela n'entre dans la sauvegarde.
        Assert.DoesNotContain("comfort", SaveJsonReader.Write(w.Save()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lot_de_plusieurs_ticks_entree_en_combat_au_milieu_le_reconfort_s_arrete_au_tick_pres()
    {
        // Revue Codex de la PR #223 : le réconfort voulu est relu avant chaque tick du lot. Ici la source passe à faux au 11e tick (comme
        // une entrée en combat, WorldSession.ComfortWanted) au milieu d'un seul lot de 30 ticks.
        JsonObject root = Json();
        root["data"]!["commands"] = new JsonArray();
        var combat = new RealtimeCombat(Load(root), "bastion") { MaxTicksPerAdvance = 30 };
        int reads = 0;
        combat.ComfortSource = () => reads++ < 10;
        Assert.Equal(30, combat.Advance(30 * 0.05 + 0.01));
        var sent = combat.SubmittedCommands.Where(c => c.Action == CombatAction.Comfort || c.Action == CombatAction.ComfortEnd).Select(c => (c.Action, c.AtMs / 50)).ToList();
        Assert.Equal(new[] { (CombatAction.Comfort, 0L), (CombatAction.ComfortEnd, 10L) }, sent);
        Assert.False(combat.Comforted);
    }

    [Fact]
    public void Lot_de_plusieurs_ticks_expiration_au_milieu_le_reconfort_s_arrete_a_l_heure()
    {
        // Revue Codex de la PR #223 : la fin du réconfort (60 minutes de jeu) tombe au milieu d'un seul lot de ticks : comfortEnd part au
        // tick où le monde atteint la minute de fin, pas au lot suivant.
        string save = Path.Combine(Path.GetTempPath(), "eds-f7-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", WorldEventSchedule.TickAt(new WorldClock(50), 15 * 1440 + 12 * 60), 0,
            new[] { PlayerCharacters.PlacesKey }, Hero()));
        AdventureSession a;
        try
        {
            a = Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
        long now = a.World.Zone.Time.Day * 1440 + a.World.Zone.Time.Minute;
        a.World.CarryComfort(now + 1);   // réconfort qui finit à la minute suivante
        Assert.True(a.World.ComfortWanted);
        long minuteTicks = a.World.Zone.Clock.DayLengthMs / 1440 / 50;
        int batch = checked((int)(minuteTicks * 2 + 10));
        a.Combat.MaxTicksPerAdvance = batch;
        Assert.Equal(batch, a.Advance(batch * 0.05 + 0.01));
        var sent = a.Combat.SubmittedCommands.Where(c => c.Action == CombatAction.Comfort || c.Action == CombatAction.ComfortEnd).ToList();
        Assert.Equal(new[] { CombatAction.Comfort, CombatAction.ComfortEnd }, sent.Select(c => c.Action));
        long lasted = (sent[1].AtMs - sent[0].AtMs) / 50;
        Assert.InRange(lasted, 1, minuteTicks + 1);   // au plus une minute de jeu (et un tick), jamais tout le lot
        Assert.False(a.World.ComfortWanted);
        Assert.False(a.Combat.Comforted);
    }
}
