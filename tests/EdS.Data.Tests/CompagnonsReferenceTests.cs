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
using EdS.Simulation.Engine;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Compagnons (décisions de Damien du 14 novembre 2026) : moteur 0.33.0, journal au format 27, scénarios en schéma 21 (<c>companion</c> d'un
/// assaillant, actions <c>order</c> et <c>duo</c>), instantané au format 8 ; règles inchangées (schéma 17, contenu 0.18.0). Nouvelle référence
/// figée <c>Fixtures/reference-0.33.0-contenu-0.6.0-regles-0.18.0</c> : les journaux de la référence 0.32.0 y ont exactement le même corps
/// (seul l'en-tête change), plus <c>espace_compagnon</c> (Nyx, compagnon du Bastion, contre deux loups : ordre « cibler », coups coordonnés,
/// attaque à deux refusée puis lancée, « cibler » fini par la mort de la cible, « protéger », « revenir » qui annule une annonce,
/// « attendre », « suivre »).
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Companion")]
public class CompagnonsReferenceTests
{
    private const string Compagnon = "espace_compagnon";
    private const string Latest = "reference-0.33.0-contenu-0.6.0-regles-0.18.0", Previous = "reference-0.32.0-contenu-0.6.0-regles-0.18.0";

    /// <summary>Empreinte du journal figé du scénario des compagnons (format 27), documentée dans MOTEUR.md.</summary>
    public const string CompagnonFingerprint = "e1d2e0c762701d17048bd909beb0180315dd4a2ef02c0fff61916fae69574bdd";

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
    public void Versions_du_lot()
    {
        Assert.Equal(("0.34.0", 28, 22, 9), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((17, "0.18.0-provisoire"), (DataFormat.RulesSchemaVersion, ShippedData.Rules.ContentVersion));   // règles inchangées
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Fixture(Latest, Compagnon);
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen.Skip(1), Simulate(Compagnon).Skip(1));   // monture (moteur 0.34.0) : corps seulement
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(frozen, ShippedData).Status);   // monture : autre version du moteur
        Assert.Equal(CompagnonFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        JsonElement[] events = Events(frozen);
        string Kind(JsonElement e) => e.GetProperty("event").GetString()!;
        string? S(JsonElement e, string f) => e.GetProperty(f).GetString();
        Assert.Equal(new[] { "target", "protect", "return", "wait", "follow" }, events.Where(e => Kind(e) == "orderGiven").Select(e => S(e, "order")));
        Assert.Equal(new[] { "targetDead", "arrived" }, events.Where(e => Kind(e) == "orderEnded").Select(e => S(e, "reason")));
        Assert.Contains(events, e => Kind(e) == "bondGained" && S(e, "reason") == "coordinated");
        Assert.Contains(events, e => Kind(e) == "bondGained" && S(e, "reason") == "protected");
        Assert.Equal("bondNotFull", S(events.Single(e => Kind(e) == "commandRejected" && S(e, "action") == "duo"), "reason"));
        JsonElement started = events.Single(e => Kind(e) == "duoStarted");
        Assert.Equal(0, started.GetProperty("bond").GetInt64());
        JsonElement landed = events.Single(e => Kind(e) == "duoLanded");
        Assert.Equal(started.GetProperty("impactTick").GetInt64(), landed.GetProperty("tick").GetInt64());
        Assert.Contains(events, e => Kind(e) == "damage" && S(e, "attack") == "contre_jour" && S(e, "source") == "nyx");
        Assert.Contains(events, e => Kind(e) == "attackSkipped" && S(e, "reason") == "duo");
        Assert.Contains(events, e => Kind(e) == "attackCancelled" && S(e, "reason") == "recalled");
        // L'en-tête réécrit le compagnon et les commandes sous la forme du fichier.
        JsonElement header = JsonDocument.Parse(frozen[0]).RootElement.GetProperty("scenario");
        Assert.Equal("bastion", header.GetProperty("combatants")[1].GetProperty("companion").GetProperty("owner").GetString());
    }

    [Theory]
    [InlineData("espace_betes")]
    [InlineData("espace_chemins")]
    [InlineData("espace_esquive")]
    [InlineData("espace_promenade")]
    [InlineData("espace_secteur")]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // Sans compagnon : le moteur 0.33.0 reproduit octet par octet les lignes d'événements du moteur 0.32.0 ; seul l'en-tête change.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(old, ShippedData).Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest.Skip(1), Simulate(name).Skip(1));   // monture (moteur 0.34.0) : corps seulement
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(latest, ShippedData).Status);
        string Header(string line) => line.Replace("\"formatVersion\":26,\"engineVersion\":\"0.32.0\"", "\"formatVersion\":27,\"engineVersion\":\"0.33.0\"", StringComparison.Ordinal);
        Assert.Equal(latest[0], Header(old[0]));
    }

    [Fact]
    public void Evenement_du_hameau_corps_identique()
    {
        string[] old = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.32.0-regles-0.18.0", "evenement_hameau.jsonl"));
        string[] latest = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.33.0-regles-0.18.0", "evenement_hameau.jsonl"));
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest.Skip(1), Simulate("evenement_hameau").Skip(1));   // monture : corps seulement
    }

    // --- Données du scénario (schéma 21) -------------------------------------------------------------------------

    private static void AssertError(ScenarioLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    private static JsonObject Cmd(JsonObject r, int i) => r["data"]!["commands"]![i]!.AsObject();
    private static JsonObject Nyx(JsonObject r) => r["data"]!["combatants"]![1]!["companion"]!.AsObject();

    [Fact]
    public void Compagnon_et_commandes_lus_du_fichier()
    {
        CombatSetup setup = Load(Compagnon);
        var nyx = (AssailantSetup)setup.Combatants[1];
        CompanionSetup c = nyx.Companion!;
        Assert.Equal(("bastion", 70, 10, 5, 3000L), (c.Owner, c.Bond, c.CoordinatedGain, c.ProtectGain, c.BondWindowMs));
        Assert.Equal(("contre_jour", 260L, 800L, 1500L), (c.Duo!.Id, c.Duo.RawDamage, c.Duo.TelegraphMs, c.Duo.RecoveryMs));
        CombatCommand target = setup.Commands.First(x => x.Action == CombatAction.Order);
        Assert.Equal(("nyx", (CompanionOrder?)CompanionOrder.Target, "loup_a"), (target.Companion, target.Order, target.Target));
        CombatCommand duo = setup.Commands.First(x => x.Action == CombatAction.Duo);
        Assert.Equal(("nyx", (CompanionOrder?)null, "loup_b"), (duo.Companion, duo.Order, duo.Target));
    }

    [Fact]
    public void Formes_refusees_avec_chemin_precis()
    {
        AssertError(ReadMutated(r => Nyx(r).Remove("owner"), scenario: Compagnon), "$.data.combatants[1].companion.owner");
        AssertError(ReadMutated(r => Nyx(r)["bond"] = 101, scenario: Compagnon), "$.data.combatants[1].companion.bond");
        AssertError(ReadMutated(r => Nyx(r)["owner"] = "loup_a", scenario: Compagnon), "$.data.combatants[1].companion.owner");
        AssertError(ReadMutated(r => Nyx(r)["duo"]!["damageType"] = "feu", scenario: Compagnon), "$.data.combatants[1].companion.duo.damageType");
        AssertError(ReadMutated(r => Nyx(r)["inconnu"] = 1, scenario: Compagnon), "$.data.combatants[1].companion.inconnu");
        AssertError(ReadMutated(r => r["data"]!["combatants"]![0]!["companion"] = Nyx(r).DeepClone(), scenario: Compagnon), "$.data.combatants[0].companion");
        AssertError(ReadMutated(r => Cmd(r, 2)["order"] = "attaque", scenario: Compagnon), "$.data.commands[2].order");
        AssertError(ReadMutated(r => Cmd(r, 2).Remove("companion"), scenario: Compagnon), "$.data.commands[2].companion");
        AssertError(ReadMutated(r => Cmd(r, 2)["skill"] = "skill.bastion.frappe_du_rempart", scenario: Compagnon), "$.data.commands[2].skill");
        AssertError(ReadMutated(r => Cmd(r, 2).Remove("target"), scenario: Compagnon), "$.data.commands[2].target");
        AssertError(ReadMutated(r => Cmd(r, 5).Remove("target"), scenario: Compagnon), "$.data.commands[5].target");
        AssertError(ReadMutated(r => Cmd(r, 5)["order"] = "wait", scenario: Compagnon), "$.data.commands[5].order");
        AssertError(ReadMutated(r => Cmd(r, 0)["companion"] = "nyx", scenario: Compagnon), "$.data.commands[0].companion");
        AssertError(ReadMutated(r => Cmd(r, 0)["order"] = "wait", scenario: Compagnon), "$.data.commands[0].order");
        AssertError(ReadMutated(r => Cmd(r, 2)["actor"] = "loup_a", scenario: Compagnon), "$.data.commands[2].actor");
        AssertError(ReadMutated(r => r["schemaVersion"] = 20, scenario: Compagnon), "$.schemaVersion");
        // Revue Codex de la PR #169 : une annonce démesurée est refusée avec son chemin, sans exception (aucun débordement).
        AssertError(ReadMutated(r => Nyx(r)["duo"]!["telegraphMs"] = long.MaxValue, scenario: Compagnon), "$.data.combatants[1].companion.duo.telegraphMs");
    }

    [Fact]
    public void Schema_JSON_du_lot()
    {
        JsonNode scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(ScenarioValidator.CurrentSchemaVersion, scenario["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        JsonNode companion = scenario["$defs"]!["combatant"]!["properties"]!["companion"]!;
        Assert.Equal(new[] { "owner", "bond", "followDistance", "leash", "bondWindowMs", "coordinatedGain", "protectGain" },
            companion["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(CompanionOrderIds.All, scenario["$defs"]!["command"]!["properties"]!["order"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()));
        string[] actions = scenario["$defs"]!["command"]!["properties"]!["action"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Contains("order", actions);
        Assert.Contains("duo", actions);
    }
}
