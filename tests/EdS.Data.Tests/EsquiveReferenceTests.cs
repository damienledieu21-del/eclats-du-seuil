using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Esquive (décision de Damien du 14 novembre 2026, qui revient sur L11-D4) : moteur 0.32.0, journal au format 26, règles en schéma 17
/// (contenu 0.18.0, bloc <c>combat.dodge</c>), scénarios en schéma 20 (action <c>dodge</c> ; 19 : V2.28c), instantané au format 7. Nouvelle référence
/// figée <c>Fixtures/reference-0.34.0-contenu-0.6.0-regles-0.18.0</c> : les journaux de la référence 0.31.0 y ont exactement le même corps
/// (seul l'en-tête change), plus <c>espace_esquive</c> (coup imparable esquivé, coup reçu sans esquive, bond arrêté par un mur, recharge,
/// mur collé, bord de l'aire, bond vers l'arrière). Les anciennes références restent figées et refusées au rejeu.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Scenario")]
public class EsquiveReferenceTests
{
    private const string Esquive = "espace_esquive";
    private const string Latest = "reference-0.34.0-contenu-0.6.0-regles-0.18.0", Previous = "reference-0.31.0-contenu-0.6.0-regles-0.17.0";

    /// <summary>Empreinte du journal figé du scénario de l'esquive (format 26), documentée dans MOTEUR.md.</summary>
    public const string EsquiveFingerprint = "6f675f4041d4712fde1cffe8ff1cd320fbb5e3a2d33b3ae8900bd5757d13b1b6";

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

    private static CombatSetup Load(string name)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static string[] Simulate(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(name), log);
        return log.Lines.ToArray();
    }

    private static JsonElement[] Events(string[] lines) => lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement).ToArray();

    [Fact]
    public void Versions_et_regles_livrees()
    {
        Assert.Equal(("0.34.0", 28, 22, 9), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((17, "0.18.0-provisoire"), (DataFormat.RulesSchemaVersion, ShippedData.Rules.ContentVersion));
        CombatRules rules = ShippedData.Rules.Combat;
        Assert.Equal((Fixed.FromInt(3), 150L, 300L, 20L, 1000L),
            (rules.DodgeDistance, rules.DodgeDurationMs, rules.DodgeInvulnerableMs, rules.DodgeEnduranceCost, rules.DodgeCooldownMs));
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Fixture(Latest, Esquive);
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen, Simulate(Esquive));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        // Empreinte du journal figé au format 26 (référence 0.32.0, jamais modifiée) ; la monture (format 28) n'en change que l'en-tête.
        string first = Fixture("reference-0.32.0-contenu-0.6.0-regles-0.18.0", Esquive);
        Assert.Equal(EsquiveFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(first))).ToLowerInvariant());
        Assert.Equal(File.ReadAllLines(first).Skip(1), frozen.Skip(1));
        JsonElement[] events = Events(frozen);
        string Kind(JsonElement e) => e.GetProperty("event").GetString()!;
        // Coup imparable esquivé au tick 30, le suivant reçu au tick 70.
        JsonElement evaded = Assert.Single(events, e => Kind(e) == "evaded");
        Assert.Equal(30, evaded.GetProperty("tick").GetInt64());
        Assert.Contains(events, e => Kind(e) == "damage" && e.GetProperty("tick").GetInt64() == 70 && e.GetProperty("target").GetString() == "bastion");
        // Quatre esquives acceptées ; refus de recharge puis de mur.
        Assert.Equal(4, events.Count(e => Kind(e) == "dodgeStarted"));
        Assert.Equal(new[] { "dodgeCooldown", "dodgeBlocked" }, events.Where(e => Kind(e) == "commandRejected").Select(e => e.GetProperty("reason").GetString()));
        // Le bond vers le mur s'arrête avant lui (x ≤ 2,5 : mur à x = 3, rayon 0,5).
        Assert.All(events.Where(e => Kind(e) == "moved"), m =>
            Assert.True(m.GetProperty("combatants")[0].GetProperty("position")[0].GetDecimal() <= 2.5m));
    }

    [Theory]
    [InlineData("espace_betes")]
    [InlineData("espace_chemins")]
    [InlineData("espace_promenade")]
    [InlineData("espace_secteur")]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // Sans esquive : le moteur 0.32.0 reproduit octet par octet les lignes d'événements du moteur 0.31.0 ; seul l'en-tête change.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(old, ShippedData).Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(name));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(latest, ShippedData).Status);
    }

    [Fact]
    public void Evenement_du_hameau_corps_identique()
    {
        string[] old = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.31.0-regles-0.17.0", "evenement_hameau.jsonl"));
        string[] latest = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.34.0-regles-0.18.0", "evenement_hameau.jsonl"));
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate("evenement_hameau"));
    }

    // --- Données ----------------------------------------------------------------------------------------------

    [Fact]
    public void Regles_au_schema_16_refusees_et_bloc_dodge_obligatoire_et_borne()
    {
        string rules = File.ReadAllText(Path.Combine(DataDirectory, "rules.json"));
        Assert.False(RulesJsonReader.Read(rules.Replace("\"schemaVersion\": 17", "\"schemaVersion\": 16")).IsValid);
        JsonObject Root() => JsonNode.Parse(rules)!.AsObject();
        JsonObject root = Root();
        root["data"]!["combat"]!.AsObject().Remove("dodge");
        Assert.Contains(RulesJsonReader.Read(root.ToJsonString()).Report.Errors, e => e.Path.StartsWith("$.data.combat.dodge", StringComparison.Ordinal));
        root = Root();
        root["data"]!["combat"]!["dodge"]!["invulnerableMs"] = 5000;   // plus longue que la recharge
        Assert.False(RulesJsonReader.Read(root.ToJsonString()).IsValid);
        root = Root();
        root["data"]!["combat"]!["dodge"]!["durationMs"] = 100;   // 30 m/s
        Assert.False(RulesJsonReader.Read(root.ToJsonString()).IsValid);
    }

    private static void AssertError(ScenarioLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    private static JsonObject Cmd(JsonObject r, int i) => r["data"]!["commands"]![i]!.AsObject();

    [Fact]
    public void Commande_dodge_lue_et_contraintes()
    {
        CombatSetup setup = Load(Esquive);
        CombatCommand first = setup.Commands[0], last = setup.Commands[^1];
        Assert.Equal((CombatAction.Dodge, (GroundVector?)new GroundVector(Fixed.Zero, Fixed.One)), (first.Action, first.Direction));
        Assert.Equal((CombatAction.Dodge, (GroundVector?)null), (last.Action, last.Direction));
        AssertError(ReadMutated(r => Cmd(r, 0)["backward"] = true, scenario: Esquive), "$.data.commands[0].backward");
        AssertError(ReadMutated(r => Cmd(r, 0)["skill"] = "skill.bastion.frappe_du_rempart", scenario: Esquive), "$.data.commands[0].skill");
        Assert.False(ReadMutated(r => Cmd(r, 0)["actor"] = "ogre", scenario: Esquive).IsValid);   // une créature n'esquive pas
        Assert.False(ReadMutated(r => Cmd(r, 0)["direction"] = new JsonArray(0, 0), scenario: Esquive).IsValid);
        AssertError(ReadMutated(r => r["schemaVersion"] = 19, scenario: Esquive), "$.schemaVersion");
        // Une direction reste réservée à move et dodge.
        AssertError(ReadMutated(r => { Cmd(r, 0)["action"] = "parry"; }, scenario: Esquive), "$.data.commands[0].direction");
    }

    [Fact]
    public void Schemas_JSON_du_sous_lot()
    {
        JsonNode scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        JsonNode rules = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json")))!;
        Assert.Equal(22, scenario["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Equal(17, rules["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Contains("dodge", scenario.ToJsonString());
        JsonNode dodge = rules["properties"]!["data"]!["properties"]!["combat"]!["properties"]!["dodge"]!;
        Assert.Equal(new[] { "distance", "durationMs", "invulnerableMs", "enduranceCost", "cooldownMs" },
            dodge["required"]!.AsArray().Select(n => n!.GetValue<string>()));
    }
}
