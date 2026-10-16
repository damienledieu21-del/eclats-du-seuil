using EdS.Data.Classes;
using EdS.Data.Rules;
using EdS.Data.Validation;

namespace EdS.Data.Json;

/// <summary>Résultat du chargement d'un dossier de données : les données, ou les rapports des fichiers refusés.</summary>
public sealed class GameDataLoadResult
{
    public GameData? Data { get; }

    /// <summary>Fichiers refusés et leur rapport.</summary>
    public IReadOnlyList<(string Path, ValidationReport Report)> Failures { get; }

    public bool IsValid => Data != null;

    internal GameDataLoadResult(GameData? data, IReadOnlyList<(string, ValidationReport)> failures)
    {
        Data = data;
        Failures = failures;
    }
}

/// <summary>Charge un dossier de données (<c>rules.json</c> et <c>classes/*.json</c>), par exemple <c>data/v0</c>.</summary>
public static class GameDataLoader
{
    public static GameDataLoadResult Load(string directory)
    {
        var failures = new List<(string, ValidationReport)>();
        string rulesPath = Path.Combine(directory, "rules.json");
        RulesLoadResult rules = RulesJsonReader.ReadFile(rulesPath);
        if (rules.Rules is null)
        {
            failures.Add((rulesPath, rules.Report));
            return new GameDataLoadResult(null, failures);
        }

        var classes = new List<LoadedClass>();
        string classDirectory = Path.Combine(directory, "classes");
        string[] files = Directory.Exists(classDirectory) ? Directory.GetFiles(classDirectory, "*.json") : [];
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            ClassLoadResult result = ClassJsonReader.ReadFile(file, rules.Rules.Progression);
            if (result.Class is null) failures.Add((file, result.Report));
            else if (classes.Any(c => c.Definition.Id == result.Class.Definition.Id))
            {
                var report = new ValidationReport();
                report.Add("$.data.id", ValidationCode.DuplicateId, $"classe « {result.Class.Definition.Id} » déjà définie par un autre fichier.");
                failures.Add((file, report));
            }
            else classes.Add(result.Class);
        }
        return failures.Count > 0 ? new GameDataLoadResult(null, failures) : new GameDataLoadResult(new GameData(rules.Rules, classes), failures);
    }
}
