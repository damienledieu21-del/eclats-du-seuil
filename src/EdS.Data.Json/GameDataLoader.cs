using EdS.Data.Builds;
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

/// <summary>
/// Charge un dossier de données (<c>rules.json</c>, <c>classes/*.json</c> et, depuis le sous-lot 8.5, <c>builds/*.json</c>), par exemple
/// <c>data/v0</c>. Les builds de référence sont validés contre les classes du dossier ; un fichier refusé fait échouer le chargement,
/// comme une classe refusée.
/// </summary>
public static class GameDataLoader
{
    /// <param name="directory">Dossier de données.</param>
    /// <param name="builds">Faux : ignore <c>builds/</c> (validation d'un fichier de builds isolé par <c>eds validate</c>).</param>
    public static GameDataLoadResult Load(string directory, bool builds = true)
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
        if (failures.Count > 0) return new GameDataLoadResult(null, failures);
        var data = new GameData(rules.Rules, classes);
        if (!builds) return new GameDataLoadResult(data, failures);

        var sets = new List<LoadedBuildSet>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string buildDirectory = Path.Combine(directory, "builds");
        string[] buildFiles = Directory.Exists(buildDirectory) ? Directory.GetFiles(buildDirectory, "*.json") : [];
        Array.Sort(buildFiles, StringComparer.Ordinal);
        foreach (string file in buildFiles)
        {
            BuildSetLoadResult result = BuildSetJsonReader.ReadFile(file, data);
            if (result.BuildSet is not { } set)
            {
                failures.Add((file, result.Report));
                continue;
            }
            var report = new ValidationReport();
            for (int i = 0; i < set.Builds.Count; i++)
                if (!ids.Add(set.Builds[i].Id))
                    report.Add($"$.data.builds[{i}].id", ValidationCode.DuplicateId, $"build « {set.Builds[i].Id} » déjà défini par un autre fichier.");
            if (report.IsValid) sets.Add(set);
            else failures.Add((file, report));
        }
        return failures.Count > 0 ? new GameDataLoadResult(null, failures) : new GameDataLoadResult(new GameData(rules.Rules, classes, sets), failures);
    }
}
