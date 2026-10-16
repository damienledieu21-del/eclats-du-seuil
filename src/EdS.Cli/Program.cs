using System.Globalization;
using System.Text.Json;
using EdS.Data;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Randomness;

// Outil en ligne de commande du simulateur.
// La commande report arrivera avec le lot 8.

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
    case "simulate":
        return RunSimulate(args.AsSpan(1));
    case "replay":
        return RunReplay(args.AsSpan(1));
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

    if (kind == ScenarioValidator.Kind)
    {
        GameData? data = LoadData(DefaultDataDirectory());
        if (data is null) return 1;
        ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(path, data);
        if (scenario.Setup is not { } setup) return Fail(path, scenario.Report);
        Console.WriteLine($"{path} : valide");
        Console.WriteLine($"  combattants    {string.Join(", ", setup.Combatants.Select(c => c.Id))}");
        Console.WriteLine($"  commandes      {setup.Commands.Count}");
        Console.WriteLine($"  durée          {setup.DurationMs} ms, graine {setup.Seed}");
        return 0;
    }

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

static string DefaultDataDirectory() => Path.Combine("data", "v0");

static GameData? LoadData(string directory)
{
    GameDataLoadResult loaded = GameDataLoader.Load(directory);
    if (loaded.Data is { } data) return data;
    foreach (var (file, report) in loaded.Failures) Fail(file, report);
    return null;
}

static int RunSimulate(ReadOnlySpan<string> options)
{
    string? scenarioPath = null, output = null, dataDirectory = DefaultDataDirectory();
    for (int i = 0; i < options.Length; i++)
    {
        if (options[i] == "--out" && i + 1 < options.Length) output = options[++i];
        else if (options[i] == "--data" && i + 1 < options.Length) dataDirectory = options[++i];
        else if (scenarioPath is null && !options[i].StartsWith("--", StringComparison.Ordinal)) scenarioPath = options[i];
        else
        {
            Console.Error.WriteLine($"Option inconnue : {options[i]}");
            return 1;
        }
    }
    if (scenarioPath is null)
    {
        Console.Error.WriteLine("Usage : eds simulate scénario.json [--data dossier] [--out journal.jsonl]");
        return 1;
    }

    GameData? data = LoadData(dataDirectory);
    if (data is null) return 1;
    ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(scenarioPath, data);
    if (scenario.Setup is not { } setup) return Fail(scenarioPath, scenario.Report);

    var log = new MemoryCombatLog();
    CombatResult result = CombatEngine.Run(setup, log);
    TextWriter summary = output is null ? Console.Error : Console.Out;
    if (output is null) Console.Out.Write(log.ToString());
    else File.WriteAllText(output, log.ToString(), new System.Text.UTF8Encoding(false));

    summary.WriteLine($"Combat simulé — valeurs provisoires ({setup.DurationMs} ms prévus, graine {setup.Seed})");
    summary.WriteLine($"  fin            {result.EndReason} au tick {result.EndTick} ({result.DurationMs} ms){(result.AbortReason is null ? "" : " : " + result.AbortReason)}");
    foreach (CombatantResult c in result.Combatants)
    {
        string dps = (c.DamageDealt * 1000m / result.DurationMs).ToString("0.##", CultureInfo.InvariantCulture);
        summary.WriteLine($"  {c.Id,-14} dégâts infligés {c.DamageDealt} (DPS {dps}), reçus {c.DamageTaken}, coups {c.Hits} dont {c.CriticalHits} critiques, compétences {c.SkillsCast}, refus {c.CommandsRejected}");
        if (c.Resonance is int resonance)
            summary.WriteLine($"  {"",-14} blocages {c.Blocks}, parades {c.PerfectParries} réussies / {c.FailedParries} ratées, ruptures {c.GuardBreaks}, PV {c.Health}, endurance {c.Endurance}, Résonance {resonance}, Charges {c.RampartCharges}");
    }
    if (output != null) summary.WriteLine($"  journal        {output} ({log.Lines.Count} lignes)");
    return 0;
}

static int RunReplay(ReadOnlySpan<string> options)
{
    string? logPath = null, dataDirectory = DefaultDataDirectory();
    for (int i = 0; i < options.Length; i++)
    {
        if (options[i] == "--data" && i + 1 < options.Length) dataDirectory = options[++i];
        else if (logPath is null && !options[i].StartsWith("--", StringComparison.Ordinal)) logPath = options[i];
        else
        {
            Console.Error.WriteLine($"Option inconnue : {options[i]}");
            return 1;
        }
    }
    if (logPath is null)
    {
        Console.Error.WriteLine("Usage : eds replay journal.jsonl [--data dossier]");
        return 1;
    }

    GameData? data = LoadData(dataDirectory);
    if (data is null) return 1;
    ReplayOutcome outcome = CombatLogReplayer.ReplayFile(logPath, data);
    switch (outcome.Status)
    {
        case ReplayStatus.Identical:
            Console.WriteLine($"{logPath} : rejeu identique, octet par octet.");
            return 0;
        case ReplayStatus.Diverged:
            ReplayComparison c = outcome.Comparison!;
            Console.Error.WriteLine($"{logPath} : rejeu différent à la ligne {c.FirstDifferentLine}.");
            Console.Error.WriteLine($"  journal : {c.Expected ?? "(fin du journal)"}");
            Console.Error.WriteLine($"  rejeu   : {c.Actual ?? "(fin du rejeu)"}");
            return 1;
        default:
            Console.Error.WriteLine(outcome.Status == ReplayStatus.Refused
                ? $"{logPath} : rejeu refusé, le journal n'est pas reproductible avec ces données ou ce moteur :"
                : $"{logPath} : journal illisible :");
            foreach (string problem in outcome.Problems) Console.Error.WriteLine("  " + problem);
            return 1;
    }
}

static void PrintHelp()
{
    Console.WriteLine("Éclats du Seuil — simulateur de combat (v0.1, valeurs provisoires)");
    Console.WriteLine();
    Console.WriteLine("Usage :");
    Console.WriteLine("  eds rng [--seed N] [--stream N] [--count N]   Affiche les premiers tirages PCG32");
    Console.WriteLine("  eds validate [fichier.json] [--rules R]        Valide un fichier de règles ou de classe (défaut : data/v0/rules.json) ;");
    Console.WriteLine("                                                 une classe est vérifiée avec la progression de R (défaut : data/v0/rules.json) ;");
    Console.WriteLine("                                                 un scénario avec les données de data/v0");
    Console.WriteLine("  eds simulate scénario.json [--data D] [--out J] Simule un combat ; journal JSON Lines vers J (défaut : sortie standard)");
    Console.WriteLine("  eds replay journal.jsonl [--data D]            Vérifie les empreintes puis rejoue et compare octet par octet");
}
