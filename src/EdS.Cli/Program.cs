using System.Globalization;
using System.Text.Json;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Validation;
using EdS.Simulation.Randomness;

// Outil en ligne de commande du simulateur.
// Les commandes simulate / report arriveront avec les lots 5 et 8.

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}

switch (args[0])
{
    case "rng":
        return RunRng(args.AsSpan(1));
    case "validate":
        return RunValidate(args.AsSpan(1));
    default:
        Console.Error.WriteLine($"Commande inconnue : {args[0]}");
        PrintHelp();
        return 1;
}

static int RunRng(ReadOnlySpan<string> options)
{
    ulong seed = 42, stream = Pcg32.DefaultStream;
    int count = 6;
    for (int i = 0; i < options.Length; i++)
    {
        string value = i + 1 < options.Length ? options[i + 1] : string.Empty;
        switch (options[i])
        {
            case "--seed": seed = ulong.Parse(value, CultureInfo.InvariantCulture); i++; break;
            case "--stream": stream = ulong.Parse(value, CultureInfo.InvariantCulture); i++; break;
            case "--count": count = int.Parse(value, CultureInfo.InvariantCulture); i++; break;
            default:
                Console.Error.WriteLine($"Option inconnue : {options[i]}");
                return 1;
        }
    }

    var rng = new Pcg32(seed, stream);
    Console.WriteLine($"PCG32 graine={seed} flux={stream}");
    for (int i = 0; i < count; i++)
        Console.WriteLine($"0x{rng.NextUInt():x8}");
    return 0;
}

static int RunValidate(ReadOnlySpan<string> options)
{
    string defaultRules = Path.Combine("data", "v0", "rules.json");
    string? path = null, rulesPath = null;
    for (int i = 0; i < options.Length; i++)
    {
        if (options[i] == "--rules" && i + 1 < options.Length) rulesPath = options[++i];
        else if (path is null && !options[i].StartsWith("--", StringComparison.Ordinal)) path = options[i];
        else
        {
            Console.Error.WriteLine($"Option inconnue : {options[i]}");
            return 1;
        }
    }
    path ??= defaultRules;

    // Le champ « kind » choisit le validateur ; un fichier illisible est confié au validateur de règles, qui l'explique.
    string? kind = null;
    try
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("kind", out JsonElement k) && k.ValueKind == JsonValueKind.String)
            kind = k.GetString();
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }

    if (kind != ClassValidator.Kind)
    {
        RulesLoadResult result = RulesJsonReader.ReadFile(path);
        if (result.Rules is not { } rules) return Fail(path, result.Report);
        return Valid(path, rules.SchemaVersion, rules.ContentVersion, rules.Fingerprint);
    }

    rulesPath ??= defaultRules;
    RulesLoadResult rulesResult = RulesJsonReader.ReadFile(rulesPath);
    if (rulesResult.Rules is not { } progressionSource)
    {
        Console.Error.WriteLine($"Règles de progression illisibles ({rulesPath}) :");
        return Fail(rulesPath, rulesResult.Report);
    }
    ClassLoadResult classResult = ClassJsonReader.ReadFile(path, progressionSource.Progression);
    if (classResult.Class is not { } loaded) return Fail(path, classResult.Report);
    Valid(path, loaded.SchemaVersion, loaded.ContentVersion, loaded.Fingerprint);
    Console.WriteLine($"  classe         {loaded.Definition.Name} ({loaded.Definition.Id}), {loaded.Definition.Trees.Sum(t => t.Talents.Count)} talents, {loaded.Definition.Skills.Count} compétences");
    Console.WriteLine($"  règles         {rulesPath} ({progressionSource.Fingerprint})");
    return 0;
}

static int Valid(string path, int schemaVersion, string contentVersion, string fingerprint)
{
    Console.WriteLine($"{path} : valide");
    Console.WriteLine($"  schemaVersion  {schemaVersion}");
    Console.WriteLine($"  contentVersion {contentVersion}");
    Console.WriteLine($"  empreinte      {fingerprint}");
    return 0;
}

static int Fail(string path, ValidationReport report)
{
    Console.Error.WriteLine($"{path} : {report.Errors.Count} erreur(s)");
    foreach (var error in report.Errors)
        Console.Error.WriteLine("  " + error);
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("Éclats du Seuil — simulateur de combat (v0.1, valeurs provisoires)");
    Console.WriteLine();
    Console.WriteLine("Usage :");
    Console.WriteLine("  eds rng [--seed N] [--stream N] [--count N]   Affiche les premiers tirages PCG32");
    Console.WriteLine("  eds validate [fichier.json] [--rules R]        Valide un fichier de règles ou de classe (défaut : data/v0/rules.json) ;");
    Console.WriteLine("                                                 une classe est vérifiée avec la progression de R (défaut : data/v0/rules.json)");
}
