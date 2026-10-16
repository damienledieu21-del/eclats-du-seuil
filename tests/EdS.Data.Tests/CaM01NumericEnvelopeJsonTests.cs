using System.Diagnostics;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>Données modifiées pour les tests CA-M01 : une seule occurrence remplacée dans rules.json, sinon échec du test.</summary>
internal static class CaM01Data
{
    public static string RulesWith(string original, string replacement) => CopyData((file, text) =>
    {
        if (file != "rules.json") return text;
        int first = text.IndexOf(original, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(original, first + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException($"« {original} » doit apparaître exactement une fois dans rules.json.");
        return text.Replace(original, replacement, StringComparison.Ordinal);
    });

    public static T WithDirectory<T>(string directory, Func<string, T> use)
    {
        try
        {
            return use(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

/// <summary>
/// Correctif 6.1.1, CA-M01, chemin JSON : les mêmes contre-exemples écrits dans les fichiers de données. Une valeur hors
/// limites des règles est refusée au chargement de rules.json, à son chemin JSON ; une défense de personnage hors limites
/// (cas B) est refusée à la validation du scénario, rattachée au combattant, avec le code OutOfRange.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class CaM01NumericEnvelopeJsonTests
{
    private static void AssertRulesRefused(string original, string replacement, string path) =>
        CaM01Data.WithDirectory(CaM01Data.RulesWith(original, replacement), directory =>
        {
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.False(loaded.IsValid);
            var (_, report) = Assert.Single(loaded.Failures);
            Assert.Contains(report.Errors, e => e.Path == path);
            return 0;
        });

    private static CombatResult AssertLimitAcceptedAndSimulated(string original, string replacement) =>
        CaM01Data.WithDirectory(CaM01Data.RulesWith(original, replacement), directory =>
        {
            GameData data = GameDataLoader.Load(directory).Data!;
            ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(ScenarioPath("entrainement_garde_parade"), data);
            Assert.True(scenario.IsValid, string.Join(" ; ", scenario.Report.Errors));
            CombatResult result = CombatEngine.Run(scenario.Setup!, new MemoryCombatLog());
            Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
            return result;
        });

    // Cas A
    [Theory]
    [InlineData("\"physique\": 1000,", "\"physique\": 1000000000,")]
    [InlineData("\"magique\": 1000", "\"magique\": 1000000000")]
    public void A_constante_de_defense_a_la_limite(string original, string replacement) =>
        AssertLimitAcceptedAndSimulated(original, replacement);

    [Theory]
    [InlineData("1000000001")]
    [InlineData("922337203685477")]
    [InlineData("922337203685478")]
    [InlineData("9223372036854775807")]
    public void A_constante_de_defense_hors_limites(string value)
    {
        AssertRulesRefused("\"physique\": 1000,", $"\"physique\": {value},", "$.data.damage.defenseConstants.physique");
        AssertRulesRefused("\"magique\": 1000", $"\"magique\": {value}", "$.data.damage.defenseConstants.magique");
    }

    // Cas B
    [Theory]
    [InlineData("\"armor\": 0,", "\"armor\": 1000000000,")]
    [InlineData("\"magicResistance\": 0,", "\"magicResistance\": 1000000000,")]
    public void B_defense_de_base_des_personnages_a_la_limite(string original, string replacement) =>
        AssertLimitAcceptedAndSimulated(original, replacement);

    [Theory]
    [InlineData("1000000000.0001")]
    [InlineData("922337203685477")]
    [InlineData("922337203685477.5807")]
    public void B_defense_de_base_des_personnages_hors_limites_refusee_par_le_scenario(string value)
    {
        foreach (string field in new[] { "armor", "magicResistance" })
            CaM01Data.WithDirectory(CaM01Data.RulesWith($"\"{field}\": 0,", $"\"{field}\": {value},"), directory =>
            {
                GameData data = GameDataLoader.Load(directory).Data!;   // les règles seules sont valides…
                ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(ScenarioPath("entrainement_garde_parade"), data);
                Assert.False(scenario.IsValid);                          // …le combat qui les utilise ne l'est pas
                Assert.Contains(scenario.Report.Errors, e => e.Path == "$.data.combatants[0]" && e.Code == ValidationCode.OutOfRange);
                return 0;
            });
    }

    // Cas C
    [Fact]
    public void C_recharge_globale_a_la_limite() =>
        AssertLimitAcceptedAndSimulated("\"globalCooldownMs\": 1000,", "\"globalCooldownMs\": 86400000,");

    [Theory]
    [InlineData("86400050")]                 // première valeur invalide multiple du tick (50 ms)
    [InlineData("9223372036854775800")]
    [InlineData("-50")]
    public void C_recharge_globale_hors_limites(string value) =>
        AssertRulesRefused("\"globalCooldownMs\": 1000,", $"\"globalCooldownMs\": {value},", "$.data.combat.globalCooldownMs");

    // Cas D
    [Fact]
    public void D_delai_de_regeneration_a_la_limite() =>
        AssertLimitAcceptedAndSimulated("\"regenDelayMs\": 1000", "\"regenDelayMs\": 86400000");

    [Theory]
    [InlineData("86400050")]
    [InlineData("9223372036854775800")]
    [InlineData("-50")]
    public void D_delai_de_regeneration_hors_limites(string value) =>
        AssertRulesRefused("\"regenDelayMs\": 1000", $"\"regenDelayMs\": {value}", "$.data.combat.endurance.regenDelayMs");
}

/// <summary>
/// Correctif 6.1.1, CA-M01, chemin CLI : la commande <c>simulate</c> lancée dans un processus séparé. Une configuration
/// invalide sort en code 1 avec le diagnostic localisé, <b>sans créer le fichier journal</b> ; la valeur limite simule
/// normalement (code 0) ; une simulation interrompue sort en code 2 avec le bandeau de diagnostic.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class CliNumericLimitsTests
{
    private static (int ExitCode, string Output, string Error, bool LogWritten) Simulate(string original, string replacement)
    {
        string directory = CaM01Data.RulesWith(original, replacement);
        string logPath = Path.Combine(directory, "journal.jsonl");
        try
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory,
            };
            foreach (string arg in new[] { Path.Combine(AppContext.BaseDirectory, "EdS.Cli.dll"), "simulate",
                ScenarioPath("entrainement_garde_parade"), "--data", directory, "--out", logPath })
                start.ArgumentList.Add(arg);
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return (process.ExitCode, output.Result, error.Result, File.Exists(logPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("\"physique\": 1000,", "\"physique\": 1000000001,", "$.data.damage.defenseConstants.physique")]
    [InlineData("\"armor\": 0,", "\"armor\": 1000000000.0001,", "$.data.combatants[0]")]
    [InlineData("\"globalCooldownMs\": 1000,", "\"globalCooldownMs\": 86400050,", "$.data.combat.globalCooldownMs")]
    [InlineData("\"regenDelayMs\": 1000", "\"regenDelayMs\": 86400050", "$.data.combat.endurance.regenDelayMs")]
    public void Configuration_invalide_refusee_sans_ecrire_de_journal(string original, string replacement, string path)
    {
        var (exitCode, _, error, logWritten) = Simulate(original, replacement);
        Assert.Equal(1, exitCode);
        Assert.Contains(path, error);
        Assert.False(logWritten);
    }

    [Theory]
    [InlineData("\"physique\": 1000,", "\"physique\": 1000000000,")]
    [InlineData("\"armor\": 0,", "\"armor\": 1000000000,")]
    [InlineData("\"globalCooldownMs\": 1000,", "\"globalCooldownMs\": 86400000,")]
    [InlineData("\"regenDelayMs\": 1000", "\"regenDelayMs\": 86400000")]
    public void Valeur_limite_simulee_normalement(string original, string replacement)
    {
        var (exitCode, output, error, logWritten) = Simulate(original, replacement);
        Assert.True(exitCode == 0, error);
        Assert.True(logWritten);
        Assert.DoesNotContain("DIAGNOSTIC", output);
    }

    [Fact]
    public void Simulation_interrompue_code_2_et_bandeau()
    {
        var (exitCode, output, _, logWritten) = Simulate("\"maxEventsPerTick\": 1000", "\"maxEventsPerTick\": 3");
        Assert.Equal(2, exitCode);
        Assert.Contains("DIAGNOSTIC", output);
        Assert.True(logWritten);   // le journal d'une simulation interrompue est conservé pour le diagnostic
    }
}
