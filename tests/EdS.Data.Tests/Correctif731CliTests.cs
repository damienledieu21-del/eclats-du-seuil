using System.Text.Json.Nodes;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 7.3.1 : parcours complets par la CLI des contre-exemples A73-M01 et A73-M04 de l'audit (données modifiées dans
/// <c>data/v0</c> du répertoire du cas, <c>validate</c> lancé depuis ce répertoire, <c>simulate</c> et <c>replay</c> avec
/// <c>--data</c>). Avant le correctif : M01 validé (code 0) puis arrêt <c>overflow</c> (code 2) ; M04 annoncé « majorant prudent
/// 2/2, garanti » puis arrêt <c>queueLimitExceeded</c> au tick 40 (code 2).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class Correctif731CliTests
{
    /// <summary>Scénario de l'annexe de l'audit : mannequin « target » et assaillant « attacker » qui pose une brûlure.</summary>
    private static string Scenario(long intervalMs, bool direct, bool trigger) => $$"""
        { "schemaVersion": 6, "kind": "scenario", "contentVersion": "0.1.0-audit",
          "data": { "seed": 42, "durationMs": 5000, "commands": [],
            "combatants": [
              { "id": "target", "type": "dummy", "team": "blue", "initiative": 0, "armor": 0, "magicResistance": 0 },
              { "id": "attacker", "type": "assailant", "team": "red", "initiative": 0, "armor": 0, "magicResistance": 0,
                "target": "target", "firstAttackMs": 0, "intervalMs": {{intervalMs}},
                {{(trigger ? "\"triggers\": [ { \"on\": \"onSkillCast\", \"effects\": [ { \"type\": \"applyStatus\", \"target\": \"eventTarget\", \"status\": \"burn\", \"coefficient\": 1 } ] } ]," : "")}}
                "attacks": [ { "id": "tap", "rawDamage": 1, "damageType": "physique", "targeting": "enemy", "unblockable": false,
                  "applies": [ {{(direct ? "{ \"status\": \"burn\", \"coefficient\": 1 }" : "")}} ] } ] } ] } }
        """;

    private static T InCase<T>(Action<JsonObject> rules, string scenario, Func<string, string, T> use) => Lot71Files.InTempDirectory(directory =>
    {
        string data = Path.Combine(directory, "data", "v0");
        Directory.CreateDirectory(Path.Combine(data, "classes"));
        File.Copy(Path.Combine(DataDirectory, "classes", "bastion.json"), Path.Combine(data, "classes", "bastion.json"));
        JsonObject root = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "rules.json")))!.AsObject();
        rules(root["data"]!.AsObject());
        File.WriteAllText(Path.Combine(data, "rules.json"), root.ToJsonString());
        string path = Path.Combine(directory, "scenario.json");
        File.WriteAllText(path, scenario);
        return use(directory, path);
    });

    [Theory]
    [InlineData(true, false)]   // brûlure posée par l'attaque
    [InlineData(false, true)]   // brûlure posée par réaction
    public void M01_contre_exemple_refuse_par_validate_et_simulate(bool direct, bool trigger)
    {
        InCase(rules =>
        {
            rules["damage"]!["categoryMultiplier"]!["max"] = 0;
            rules["damage"]!["minimumDamage"] = long.MaxValue;
            rules["stats"]!["base"]!["magicPower"] = 1;
        }, Scenario(10_000, direct, trigger), (directory, path) =>
        {
            var validate = Lot71Files.Cli(directory, "validate", path);
            Assert.NotEqual(0, validate.ExitCode);
            Assert.Contains("altération « burn »", validate.Output + validate.Error);
            var simulate = Lot71Files.Cli(directory, "simulate", path, "--data", Path.Combine(directory, "data", "v0"), "--out", Path.Combine(directory, "j.jsonl"));
            Assert.NotEqual(0, simulate.ExitCode);
            Assert.False(File.Exists(Path.Combine(directory, "j.jsonl")) && File.ReadAllText(Path.Combine(directory, "j.jsonl")).Contains("\"tick\""));
            return 0;
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void M04_scenario_minimal_garanti_va_au_bout(int capacity)
    {
        InCase(rules => rules["combat"]!["limits"]!["maxPendingEvents"] = capacity, Scenario(1000, direct: true, trigger: false), (directory, path) =>
        {
            var validate = Lot71Files.Cli(directory, "validate", path);
            Assert.True(validate.ExitCode == 0, validate.Output + validate.Error);
            Assert.Contains($"majorant prudent 2/{capacity}, garanti", validate.Output);
            string journal = Path.Combine(directory, "j.jsonl"), data = Path.Combine(directory, "data", "v0");
            var simulate = Lot71Files.Cli(directory, "simulate", path, "--data", data, "--out", journal);
            Assert.True(simulate.ExitCode == 0, simulate.Output + simulate.Error);
            string last = File.ReadAllLines(journal)[^1];
            Assert.Contains("\"reason\":\"durationReached\"", last);
            Assert.Contains("\"tick\":99", last);
            Assert.Equal(0, Lot71Files.Cli(directory, "replay", journal, "--data", data).ExitCode);
            return 0;
        });
        // N − 1 : refusé par validate, avant tout journal.
        if (capacity == 2)
            InCase(rules => rules["combat"]!["limits"]!["maxPendingEvents"] = 1, Scenario(1000, direct: true, trigger: false), (directory, path) =>
            {
                Assert.NotEqual(0, Lot71Files.Cli(directory, "validate", path).ExitCode);
                return 0;
            });
    }
}
