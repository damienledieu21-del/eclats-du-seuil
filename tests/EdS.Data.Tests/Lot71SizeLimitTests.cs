using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>Fichiers et scénarios générés pour les tests de limites du lot 7.1, dans un dossier temporaire supprimé après usage.</summary>
internal static class Lot71Files
{
    public static T InTempDirectory<T>(Func<string, T> use)
    {
        string directory = Path.Combine(Path.GetTempPath(), "eds-lot71-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            return use(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Texte complété par des espaces finaux (permis après la valeur JSON) jusqu'à <paramref name="bytes"/> octets UTF-8.</summary>
    public static string PadTo(string json, long bytes) => json + new string(' ', checked((int)(bytes - Encoding.UTF8.GetByteCount(json))));

    /// <summary>Fichier creux de <paramref name="bytes"/> octets nuls : il ne serait pas du JSON valide s'il était lu.</summary>
    public static string Sparse(string directory, string name, long bytes)
    {
        string path = Path.Combine(directory, name);
        using (var stream = new FileStream(path, FileMode.CreateNew)) stream.SetLength(bytes);
        return path;
    }

    public static JsonObject Mannequin() => Shipped();

    public static string ToJson(JsonObject root) => root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Mannequin avec le personnage, le mannequin cible et des mannequins supplémentaires jusqu'à <paramref name="count"/> combattants.</summary>
    public static JsonObject WithCombatants(int count)
    {
        JsonObject root = Mannequin();
        JsonArray combatants = DataOf(root)["combatants"]!.AsArray();
        for (int i = combatants.Count; i < count; i++)
            combatants.Add(new JsonObject
            {
                ["id"] = $"mannequin_{i:000}", ["type"] = "dummy", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 100, ["magicResistance"] = 0,
            });
        return root;
    }

    /// <summary>Mannequin avec <paramref name="count"/> commandes, deux par tick (sous la limite par acteur), sur 600 s.</summary>
    public static JsonObject WithCommands(int count)
    {
        JsonObject root = Mannequin();
        DataOf(root)["durationMs"] = 600_000;
        var commands = new JsonArray();
        for (int i = 0; i < count; i++)
            commands.Add(new JsonObject
            {
                ["atMs"] = i / 2 * 50L, ["actor"] = "bastion", ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "mannequin",
            });
        DataOf(root)["commands"] = commands;
        return root;
    }

    /// <summary>Mannequin avec <paramref name="count"/> commandes du même acteur au même tick (500 ms).</summary>
    public static JsonObject WithSimultaneous(int count)
    {
        JsonObject root = Mannequin();
        var commands = new JsonArray();
        for (int i = 0; i < count; i++)
            commands.Add(new JsonObject { ["atMs"] = 500, ["actor"] = "bastion", ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "mannequin" });
        DataOf(root)["commands"] = commands;
        return root;
    }

    public static (int ExitCode, string Output, string Error) Cli(string workingDirectory, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "EdS.Cli.dll"));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error.Result);
    }
}

/// <summary>
/// Lot 7.1, limites des lecteurs : taille d'un fichier (avant lecture) et d'un texte (avant encodage), profondeur JSON,
/// longueur des tableaux du scénario (avant construction des éléments), plafond du rapport d'erreurs, taille d'un
/// journal rejoué. Chaque limite est testée en N − 1, N et N + 1.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Limits")]
public class Lot71SizeLimitTests
{
    private static string RulesJson => File.ReadAllText(Path.Combine(DataDirectory, "rules.json"));

    // --- Taille d'un fichier : 16 Mio ------------------------------------------------------

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Fichier_de_regles_a_la_limite_de_taille(long delta, bool accepted) =>
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Path.Combine(directory, "rules.json");
            File.WriteAllText(path, Lot71Files.PadTo(RulesJson, InputLimits.MaxDataFileBytes + delta));
            Assert.Equal(InputLimits.MaxDataFileBytes + delta, new FileInfo(path).Length);
            var result = RulesJsonReader.ReadFile(path);
            if (accepted) Assert.True(result.Rules is not null, result.Report.ToString());
            else Assert.Equal(ValidationCode.FileTooLarge, Assert.Single(result.Report.Errors).Code);
            return 0;
        });

    [Fact]
    public void Fichier_trop_gros_refuse_sans_lire_son_contenu() =>
        Lot71Files.InTempDirectory(directory =>
        {
            // 1 Gio d'octets nuls : lu, ce serait InvalidJson ; refusé à sa taille, c'est FileTooLarge, sans allocation du contenu.
            string path = Lot71Files.Sparse(directory, "enorme.json", 1L << 30);
            ValidationError rules = Assert.Single(RulesJsonReader.ReadFile(path).Report.Errors);
            ValidationError scenario = Assert.Single(ScenarioJsonReader.ReadFile(path, ShippedData).Report.Errors);
            ValidationError @class = Assert.Single(ClassJsonReader.ReadFile(path, ShippedData.Rules.Progression).Report.Errors);
            foreach (ValidationError e in new[] { rules, scenario, @class })
            {
                Assert.Equal(ValidationCode.FileTooLarge, e.Code);
                Assert.Contains("au plus 16 Mio", e.Message);
            }
            return 0;
        });

    // --- Taille d'un texte : vérifiée avant encodage -----------------------------------------

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Texte_JSON_a_la_limite_de_taille(long delta, bool accepted)
    {
        string json = Lot71Files.PadTo(MannequinJson, InputLimits.MaxDataFileBytes + delta);
        ScenarioLoadResult result = ScenarioJsonReader.Read(json, ShippedData);
        if (accepted) Assert.True(result.IsValid, result.Report.ToString());
        else Assert.Equal(ValidationCode.FileTooLarge, Assert.Single(result.Report.Errors).Code);
    }

    [Fact]
    public void Texte_mesure_en_octets_UTF8_et_non_en_caracteres()
    {
        // Moins de caractères que la limite, mais plus d'octets : « é » vaut deux octets.
        string json = MannequinJson.Replace("\"seed\": 42,", "\"seed\": 42, \"commentaire\": \"\",");
        int fill = (int)((InputLimits.MaxDataFileBytes - Encoding.UTF8.GetByteCount(json)) / 2) + 1;
        string big = json.Replace("\"commentaire\": \"\"", "\"commentaire\": \"" + new string('é', fill) + "\"");
        Assert.True(big.Length < InputLimits.MaxDataFileBytes);
        Assert.True(Encoding.UTF8.GetByteCount(big) > InputLimits.MaxDataFileBytes);
        Assert.Equal(ValidationCode.FileTooLarge, Assert.Single(ScenarioJsonReader.Read(big, ShippedData).Report.Errors).Code);
    }

    [Fact]
    public void Texte_beaucoup_trop_long_refuse_sans_compter_ses_octets()
    {
        var report = new ValidationReport();
        Assert.Null(JsonInput.Utf8(new string(' ', (int)InputLimits.MaxDataFileBytes + 1), report));
        Assert.Equal(ValidationCode.FileTooLarge, Assert.Single(report.Errors).Code);
    }

    // --- Profondeur JSON : 32 -----------------------------------------------------------------

    private static string Nested(int depth) =>
        // La racine compte pour un niveau ; « x » porte depth − 1 tableaux imbriqués.
        "{\"x\": " + new string('[', depth - 1) + new string(']', depth - 1) + "}";

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void Profondeur_JSON_a_la_limite(int depth, bool accepted)
    {
        var report = new ValidationReport();
        bool read = false;
        JsonInput.ParseDocument(Nested(depth), report, (_, _) => read = true);
        Assert.Equal(accepted, read);
        if (accepted) Assert.True(report.IsValid, report.ToString());
        else Assert.Equal(ValidationCode.InvalidJson, Assert.Single(report.Errors).Code);
    }

    [Fact]
    public void Profondeur_appliquee_aux_fichiers_de_donnees()
    {
        // Le champ inconnu est signalé si la profondeur passe ; sinon, une seule erreur InvalidJson.
        string ok = RulesJson.Replace("\"data\":", "\"extra\": " + new string('[', 31) + new string(']', 31) + ",\n  \"data\":");
        string tooDeep = RulesJson.Replace("\"data\":", "\"extra\": " + new string('[', 32) + new string(']', 32) + ",\n  \"data\":");
        Assert.Equal(ValidationCode.UnknownField, Assert.Single(RulesJsonReader.Read(ok).Report.Errors).Code);
        Assert.Equal(ValidationCode.InvalidJson, Assert.Single(RulesJsonReader.Read(tooDeep).Report.Errors).Code);
    }

    // --- Longueur des tableaux du scénario --------------------------------------------------

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    public void Tableau_de_combattants_jusqu_a_la_limite(int count)
    {
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(Lot71Files.WithCombatants(count)), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal(count, result.Setup!.Combatants.Count);
    }

    [Fact]
    public void Tableau_de_combattants_trop_long_une_seule_erreur_localisee()
    {
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(Lot71Files.WithCombatants(129)), ShippedData);
        ValidationError e = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.data.combatants", ValidationCode.LimitExceeded), (e.Path, e.Code));
        // Règles 0.12.0 (décision de Damien du 29 octobre 2026) : 128 combattants au plus (64 avant).
        Assert.Equal("129 combattants, au plus 128 (combat.limits.maxCombatants).", e.Message);
        Assert.Equal(9, e.Line);   // ligne du nom « combatants » dans le fichier indenté
    }

    [Fact]
    public void Elements_d_un_tableau_trop_long_non_analyses()
    {
        // 300 éléments tous invalides : s'ils étaient lus, chacun donnerait des erreurs. Seule l'erreur de volume apparaît.
        JsonObject root = Lot71Files.Mannequin();
        var combatants = new JsonArray();
        for (int i = 0; i < 300; i++) combatants.Add(new JsonObject { ["inconnu"] = i });
        DataOf(root)["combatants"] = combatants;
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(root), ShippedData);
        Assert.Equal(1, result.Report.TotalCount);
        Assert.Equal(ValidationCode.LimitExceeded, result.Report.Errors[0].Code);
    }

    [Theory]
    [InlineData(19_999)]
    [InlineData(20_000)]
    public void Tableau_de_commandes_jusqu_a_la_limite(int count)
    {
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(Lot71Files.WithCommands(count)), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal(count, result.Setup!.Commands.Count);
    }

    [Fact]
    public void Tableau_de_commandes_trop_long_une_seule_erreur()
    {
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(Lot71Files.WithCommands(20_001)), ShippedData);
        ValidationError e = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.data.commands", ValidationCode.LimitExceeded), (e.Path, e.Code));
        Assert.Equal("20001 commandes, au plus 20000 (combat.limits.maxCommands).", e.Message);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Commandes_simultanees_par_acteur_depuis_le_JSON(int count, bool accepted)
    {
        ScenarioLoadResult result = ScenarioJsonReader.Read(Lot71Files.ToJson(Lot71Files.WithSimultaneous(count)), ShippedData);
        if (accepted) Assert.True(result.IsValid, result.Report.ToString());
        else
        {
            ValidationError e = Assert.Single(result.Report.Errors);
            Assert.Equal(("$.data.commands[4].atMs", ValidationCode.LimitExceeded), (e.Path, e.Code));
        }
    }

    // --- Plafond du rapport : 200 erreurs ------------------------------------------------------

    [Theory]
    [InlineData(199, null)]
    [InlineData(200, null)]
    [InlineData(201, "… et 1 autre erreur")]
    [InlineData(250, "… et 50 autres erreurs")]
    public void Rapport_plafonne_a_200_erreurs(int count, string? summary)
    {
        var report = new ValidationReport();
        for (int i = 0; i < count; i++) report.Add($"$.e{i}", ValidationCode.InvalidValue, "erreur " + i);
        Assert.Equal(Math.Min(count, 200), report.Errors.Count);
        Assert.Equal(count, report.TotalCount);
        Assert.Equal(summary, report.OmittedSummary);
        Assert.Equal(summary ?? "$.e" + (count - 1) + " : erreur " + (count - 1) + " [InvalidValue]", report.ToString().Split('\n')[^1]);
        Assert.True(report.HasErrorAtOrAbove($"$.e{count - 1}.enfant"));   // une erreur non conservée compte encore
    }

    [Fact]
    public void Rapport_plafonne_sur_un_vrai_fichier()
    {
        // 64 combattants avec chacun quatre champs inconnus : 252 erreurs, 200 conservées.
        JsonObject root = Lot71Files.WithCombatants(64);
        foreach (JsonNode? c in DataOf(root)["combatants"]!.AsArray())
            for (int k = 0; k < 4; k++) c!.AsObject()["inconnu" + k] = k;
        ValidationReport report = ScenarioJsonReader.Read(Lot71Files.ToJson(root), ShippedData).Report;
        Assert.Equal(256, report.TotalCount);
        Assert.Equal(200, report.Errors.Count);
        Assert.EndsWith("… et 56 autres erreurs", report.ToString());
    }

    // --- Journal rejoué : 256 Mio ------------------------------------------------------------

    [Theory]
    [InlineData(1, true)]    // limite = taille + 1 : le journal est à N − 1
    [InlineData(0, true)]    // limite = taille : N
    [InlineData(-1, false)]  // limite = taille − 1 : N + 1
    public void Journal_rejoue_a_la_limite_de_taille(long delta, bool accepted) =>
        Lot71Files.InTempDirectory(directory =>
        {
            ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(MannequinPath, ShippedData);
            var log = new MemoryCombatLog();
            CombatEngine.Run(scenario.Setup!, log);
            string path = Path.Combine(directory, "journal.jsonl");
            File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
            ReplayOutcome outcome = CombatLogReplayer.ReplayFile(path, ShippedData, new FileInfo(path).Length + delta);
            if (accepted) Assert.Equal(ReplayStatus.Identical, outcome.Status);
            else
            {
                Assert.Equal(ReplayStatus.Invalid, outcome.Status);
                Assert.Contains("trop volumineux", Assert.Single(outcome.Problems));
            }
            return 0;
        });

    [Fact]
    public void Journal_de_plus_de_256_Mio_refuse_avant_lecture() =>
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Lot71Files.Sparse(directory, "journal.jsonl", InputLimits.MaxLogFileBytes + 1);
            ReplayOutcome outcome = CombatLogReplayer.ReplayFile(path, ShippedData);
            Assert.Equal(ReplayStatus.Invalid, outcome.Status);
            Assert.Contains("au plus 256 Mio", Assert.Single(outcome.Problems));
            return 0;
        });

    [Fact]
    public void Ligne_d_en_tete_trop_longue_refusee()
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay([new string(' ', (int)InputLimits.MaxDataFileBytes + 1)], ShippedData);
        Assert.Equal(ReplayStatus.Invalid, outcome.Status);
        Assert.Contains("FileTooLarge", Assert.Single(outcome.Problems));
    }
}

/// <summary>
/// Lot 7.1, chemin CLI : un scénario refusé pour son volume sort en code 1 avec le diagnostic, sans fichier journal ;
/// la valeur limite simule normalement.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Limits")]
public class Lot71CliLimitTests
{
    private static (int ExitCode, string Error, bool LogWritten) Simulate(string scenarioJson) =>
        Lot71Files.InTempDirectory(directory =>
        {
            string scenario = Path.Combine(directory, "scenario.json");
            string logPath = Path.Combine(directory, "journal.jsonl");
            File.WriteAllText(scenario, scenarioJson);
            var (exitCode, _, error) = Lot71Files.Cli(directory, "simulate", scenario, "--data", DataDirectory, "--out", logPath);
            return (exitCode, error, File.Exists(logPath));
        });

    public static TheoryData<string, string, string> Refused => new()
    {
        { Lot71Files.ToJson(Lot71Files.WithCombatants(129)), "$.data.combatants (ligne 9)", "129 combattants, au plus 128" },   // 64 avant le 29 octobre 2026
        { Lot71Files.ToJson(Lot71Files.WithCommands(20_001)), "$.data.commands (ligne", "20001 commandes, au plus 20000" },
        { Lot71Files.ToJson(Lot71Files.WithSimultaneous(5)), "$.data.commands[4].atMs", "5ᵉ commande de « bastion »" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Scenario_hors_limites_refuse_sans_journal(string json, string path, string message)
    {
        var (exitCode, error, logWritten) = Simulate(json);
        Assert.Equal(1, exitCode);
        Assert.Contains(path, error);
        Assert.Contains(message, error);
        Assert.Contains("[LimitExceeded]", error);
        Assert.False(logWritten);
    }

    public static TheoryData<string> AtLimit => new()
    {
        Lot71Files.ToJson(Lot71Files.WithCombatants(64)),
        Lot71Files.ToJson(Lot71Files.WithCommands(20_000)),
        Lot71Files.ToJson(Lot71Files.WithSimultaneous(4)),
    };

    [Theory]
    [MemberData(nameof(AtLimit))]
    public void Scenario_a_la_limite_simule(string json)
    {
        var (exitCode, error, logWritten) = Simulate(json);
        Assert.True(exitCode == 0, error);
        Assert.True(logWritten);
    }

    /// <summary>Simulation du scénario de rupture, avec des règles modifiées (remplacements exacts dans rules.json).</summary>
    private static (int ExitCode, string Output, bool LogWritten, string Log) SimulateWithRules(Action<JsonObject> mutateScenario,
        params (string Original, string Replacement)[] rules)
    {
        string directory = CopyData((file, text) =>
        {
            if (file != "rules.json") return text;
            foreach (var (original, replacement) in rules)
            {
                Assert.Equal(1, text.Split(original).Length - 1);
                text = text.Replace(original, replacement, StringComparison.Ordinal);
            }
            return text;
        });
        try
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("entrainement_rupture")))!.AsObject();
            mutateScenario(root);
            string scenario = Path.Combine(directory, "scenario.json");
            string logPath = Path.Combine(directory, "journal.jsonl");
            File.WriteAllText(scenario, Lot71Files.ToJson(root));
            var (exitCode, output, error) = Lot71Files.Cli(directory, "simulate", scenario, "--data", directory, "--out", logPath);
            bool written = File.Exists(logPath);
            return (exitCode, output + error, written, written ? File.ReadAllText(logPath) : string.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Budget_de_travail_depasse_code_2_et_bandeau()
    {
        // 6 unités par tick suffisent aux ticks ordinaires (2 combattants × 3 passages), pas à ceux des attaques.
        var (exitCode, output, logWritten, log) = SimulateWithRules(_ => { },
            ("\"maxWorkPerTick\": 10000", "\"maxWorkPerTick\": 6"), ("\"maxEventsPerTick\": 1000", "\"maxEventsPerTick\": 6"));
        Assert.Equal(2, exitCode);
        Assert.Contains("DIAGNOSTIC — simulation interrompue (workBudgetExceeded)", output);
        Assert.Contains("charge         pics : travail 6/6 par tick", output);
        Assert.True(logWritten);
        Assert.Contains("\"event\":\"simulationAborted\",\"reason\":\"workBudgetExceeded\",\"limit\":6", log);
    }

    [Fact]
    public void Capacite_de_la_file_insuffisante_refusee_avant_journal()
    {
        // Correctif 7.2 : l'assaillant frappe à chaque tick avec Fragilité ; le majorant prudent (échéances vivantes pendant
        // 6 s) dépasse une capacité de 20, le scénario est refusé à la validation (code 1), sans journal. L'arrêt
        // queueLimitExceeded reste un filet de sécurité, testé dans EdS.Simulation.Tests sans le majorant.
        var (exitCode, output, logWritten, _) = SimulateWithRules(root =>
        {
            JsonObject assailant = DataOf(root)["combatants"]![1]!.AsObject();
            assailant["intervalMs"] = 50;
            assailant["attacks"]![0]!["applies"] = new JsonArray(new JsonObject { ["status"] = "fragility" });
            assailant["attacks"]![0]!["rawDamage"] = 1;
            DataOf(root)["commands"] = new JsonArray();
        }, ("\"maxPendingEvents\": 100000", "\"maxPendingEvents\": 20"));
        Assert.Equal(1, exitCode);
        Assert.Contains("majorant prudent de la file", output);
        Assert.Contains("capacité 20", output);
        Assert.False(logWritten);
    }

    [Fact]
    public void Fichier_trop_gros_refuse_par_simulate_et_validate() =>
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Lot71Files.Sparse(directory, "scenario.json", InputLimits.MaxDataFileBytes + 1);
            string logPath = Path.Combine(directory, "journal.jsonl");
            var simulate = Lot71Files.Cli(directory, "simulate", path, "--data", DataDirectory, "--out", logPath);
            Assert.Equal(1, simulate.ExitCode);
            Assert.Contains("[FileTooLarge]", simulate.Error);
            Assert.False(File.Exists(logPath));
            var validate = Lot71Files.Cli(directory, "validate", path);
            Assert.Equal(1, validate.ExitCode);
            Assert.Contains("[FileTooLarge]", validate.Error);
            return 0;
        });
}
