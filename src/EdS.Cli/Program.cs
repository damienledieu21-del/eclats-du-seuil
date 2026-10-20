using System.Globalization;
using System.Text.Json;
using EdS.Data;
using EdS.Data.Builds;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Tools.Measures;
using EdS.Tools.Network;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using EdS.Simulation.Randomness;

// Outil en ligne de commande du simulateur.

// Code de sortie d'une simulation interrompue (budget d'événements, débordement) : distinct des erreurs d'usage (1).
const int SimulationAbortedExitCode = 2;

// Sous-lot 8.6 : eds report --strict, au moins un objectif provisoire non atteint (les combats sont tous allés au bout).
const int ObjectiveFailedExitCode = 3;

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
    case "report":
        return RunReport(args.AsSpan(1), SimulationAbortedExitCode, ObjectiveFailedExitCode);
    case "reseau":
        return RunNetwork(args.AsSpan(1));
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
        // Lot 7.1 : pas de lecture d'un fichier trop gros ; le validateur de règles le refusera avec FileTooLarge.
        if (new FileInfo(path).Length > InputLimits.MaxDataFileBytes) throw new IOException("fichier trop volumineux");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = InputLimits.MaxJsonDepth });
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
        Warnings(scenario.Report);
        Console.WriteLine($"  combattants    {string.Join(", ", setup.Combatants.Select(c => c.Id))}");
        Console.WriteLine($"  commandes      {setup.Commands.Count}");
        Console.WriteLine($"  durée          {setup.DurationMs} ms, graine {setup.Seed}");
        Console.WriteLine($"  file           {setup.ComputeQueueBound().Describe(setup.Combat.MaxPendingEvents)}");
        return 0;
    }

    if (kind == BuildSetValidator.Kind)
    {
        // Sous-lot 8.5 : builds de référence, validés contre les règles et les classes de data/v0 (sans ses propres builds).
        GameDataLoadResult loadedData = GameDataLoader.Load(DefaultDataDirectory(), builds: false);
        if (loadedData.Data is not { } data)
        {
            foreach (var (file, report) in loadedData.Failures) Fail(file, report);
            return 1;
        }
        BuildSetLoadResult builds = BuildSetJsonReader.ReadFile(path, data);
        if (builds.BuildSet is not { } set) return Fail(path, builds.Report);
        Console.WriteLine($"{path} : valide");
        Console.WriteLine($"  schemaVersion  {set.SchemaVersion}");
        Console.WriteLine($"  contentVersion {set.ContentVersion}");
        data.TryGetClass(set.ClassId, out LoadedClass buildClass);
        Console.WriteLine($"  classe         {buildClass.Definition.Name} ({set.ClassId}), {set.Builds.Count} builds");
        foreach (ReferenceBuild b in set.Builds)
        {
            string trees = string.Join(", ", buildClass.Definition.Trees.Select(t => $"{t.Name} {BuildValidator.PointsInTree(b.Build, t)}"));
            string major = buildClass.Definition.Trees.SelectMany(t => t.Talents).Where(t => t.IsMajor && b.Build.TalentRanks.TryGetValue(t.Id, out int rank) && rank > 0)
                .Select(t => t.Name).FirstOrDefault() ?? "aucun majeur";
            Console.WriteLine($"  {b.Id,-26} {b.Name} ({BuildSetValidator.Id(b.Category)}), niveau {b.Build.Level} : {trees} ; {major} ; " +
                $"{b.Build.ActiveSkills.Count} actives, {(b.Build.Ultimate is null ? "sans ultime" : "ultime")}, {b.Build.PassiveSkills.Count} passifs");
        }
        return 0;
    }

    if (kind != ClassValidator.Kind)
    {
        RulesLoadResult result = RulesJsonReader.ReadFile(path);
        if (result.Rules is not { } rules) return Fail(path, result.Report);
        Valid(path, rules.SchemaVersion, rules.ContentVersion, rules.Fingerprint);
        Warnings(result.Report);
        return 0;
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
    Warnings(classResult.Report);
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
    Console.Error.WriteLine($"{path} : {report.TotalCount} erreur(s)");
    foreach (var error in report.Errors)
        Console.Error.WriteLine("  " + error);
    if (report.OmittedSummary is { } omitted) Console.Error.WriteLine("  " + omitted);
    return 1;
}

// Lot 7.3 : avertissements (cycles statiques de déclencheurs), sur la sortie d'erreur pour ne pas mêler un journal écrit sur stdout.
static void Warnings(ValidationReport report)
{
    foreach (var warning in report.Warnings)
        Console.Error.WriteLine("  avertissement : " + warning);
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
    Warnings(scenario.Report);

    var log = new MemoryCombatLog();
    CombatResult result = CombatEngine.Run(setup, log);
    TextWriter summary = output is null ? Console.Error : Console.Out;
    if (output is null) Console.Out.Write(log.ToString());
    else File.WriteAllText(output, log.ToString(), new System.Text.UTF8Encoding(false));

    bool aborted = result.EndReason == CombatEndReason.Aborted;
    if (aborted)
    {
        // Correctif 6.1 : un combat interrompu n'a pas de résultat d'équilibrage ; ses statistiques partielles ne servent qu'au diagnostic.
        summary.WriteLine($"DIAGNOSTIC — simulation interrompue ({result.AbortReason}) : statistiques partielles, à ne pas utiliser pour l'équilibrage.");
    }
    summary.WriteLine($"Combat simulé — valeurs provisoires ({setup.DurationMs} ms prévus, graine {setup.Seed})");
    summary.WriteLine($"  fin            {result.EndReason} au tick {result.EndTick} ({result.DurationMs} ms){(result.AbortReason is null ? "" : " : " + result.AbortReason)}");
    // Lot 7.1 : pics de charge mesurés, hors journal, rapportés aux limites configurées.
    CombatRules limits = setup.Combat;
    summary.WriteLine($"  charge         pics : travail {result.Peaks.WorkPerTick}/{limits.MaxWorkPerTick} par tick, lignes {result.Peaks.EventsPerTick}/{limits.MaxEventsPerTick} par tick, file {result.Peaks.PendingEvents}/{limits.MaxPendingEvents}");
    // Sous-lot 7.3 (C12, Q11) : avec déclencheurs, le majorant de la file n'est plus une garantie.
    summary.WriteLine($"  file           {setup.ComputeQueueBound().Describe(limits.MaxPendingEvents)}");
    foreach (CombatantResult c in result.Combatants)
    {
        string dps = (c.DamageDealt * 1000m / result.DurationMs).ToString("0.##", CultureInfo.InvariantCulture);
        summary.WriteLine($"  {c.Id,-14} dégâts infligés {c.DamageDealt} (DPS {dps}), reçus {c.DamageTaken}, coups {c.Hits} dont {c.CriticalHits} critiques, compétences {c.SkillsCast}, refus {c.CommandsRejected}");
        if (c.Resonance is int resonance)
            summary.WriteLine($"  {"",-14} blocages {c.Blocks}, parades {c.PerfectParries} réussies / {c.FailedParries} ratées, ruptures {c.GuardBreaks}, PV {c.Health}, endurance {c.Endurance}, Résonance {resonance}, Charges {c.RampartCharges}");
        if (c.Position is { } position)   // sous-lot 11.4 : combat positionné
            summary.WriteLine($"  {"",-14} position finale {position}");
    }
    if (output != null) summary.WriteLine($"  journal        {output} ({log.Lines.Count} lignes)");
    return aborted ? SimulationAbortedExitCode : 0;
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

// Sous-lot 8.6 : mesures des builds de référence sur les scénarios de mesure, avec le pilote de mesure.
static int RunReport(ReadOnlySpan<string> options, int abortedExitCode, int objectiveFailedExitCode)
{
    string directory = Path.Combine("scenarios", "mesures"), dataDirectory = DefaultDataDirectory();
    string? logs = null;
    bool json = false, strict = false, directorySet = false;
    var buildIds = new List<string>();
    var profiles = new List<PilotProfile>();
    for (int i = 0; i < options.Length; i++)
    {
        string option = options[i];
        bool hasValue = i + 1 < options.Length;
        if (option == "--data" && hasValue) dataDirectory = options[++i];
        else if (option == "--logs" && hasValue) logs = options[++i];
        else if (option == "--build" && hasValue) buildIds.Add(options[++i]);
        else if (option == "--profile" && hasValue)
        {
            if (!Measures.Profiles.TryGetValue(options[++i], out PilotProfile profile))
            {
                Console.Error.WriteLine($"Profil inconnu : {options[i]} (beginner, standard ou expert).");
                return 1;
            }
            if (!profiles.Contains(profile)) profiles.Add(profile);
        }
        else if (option == "--json") json = true;
        else if (option == "--strict") strict = true;
        else if (!directorySet && !option.StartsWith("--", StringComparison.Ordinal))
        {
            directory = option;
            directorySet = true;
        }
        else
        {
            Console.Error.WriteLine($"Option inconnue : {option}");
            return 1;
        }
    }
    if (!Directory.Exists(directory))
    {
        Console.Error.WriteLine($"Dossier de scénarios de mesure introuvable : {directory}");
        return 1;
    }

    GameData? data = LoadData(dataDirectory);
    if (data is null) return 1;
    var failures = new List<(string Path, ScenarioLoadResult Result)>();
    IReadOnlyList<MeasureScenario> scenarios;
    try
    {
        scenarios = Measures.LoadScenarios(directory, data, failures);
    }
    catch (ArgumentException e)
    {
        Console.Error.WriteLine(e.Message);   // numéro de scénario en double
        return 1;
    }
    foreach (var (file, failure) in failures) Fail(file, failure.Report);
    if (failures.Count > 0) return 1;
    if (scenarios.Count == 0)
    {
        Console.Error.WriteLine($"{directory} : aucun scénario de mesure (s<n>_<nom>.json).");
        return 1;
    }

    // Builds : ceux demandés, sinon tous les builds de la classe du personnage mesuré, dans l'ordre de leur fichier.
    var builds = new List<ReferenceBuild>();
    foreach (string id in buildIds)
    {
        if (!data.TryGetBuild(id, out ReferenceBuild build))
        {
            Console.Error.WriteLine($"Build de référence inconnu : {id}");
            return 1;
        }
        if (!builds.Contains(build)) builds.Add(build);
    }
    if (builds.Count == 0)
    {
        var classes = scenarios.Select(s => s.Setup.Combatants.OfType<CharacterSetup>().FirstOrDefault(c => c.Id == Measures.MeasuredId)?.Class.Id).Distinct().ToList();
        if (classes.Count != 1 || classes[0] is null)
        {
            Console.Error.WriteLine($"Chaque scénario de mesure doit avoir un personnage « {Measures.MeasuredId} », de la même classe.");
            return 1;
        }
        builds.AddRange(data.BuildSets.Where(b => b.ClassId == classes[0]).SelectMany(b => b.Builds));
        if (builds.Count == 0)
        {
            Console.Error.WriteLine($"Aucun build de référence pour la classe {classes[0]}.");
            return 1;
        }
    }
    if (profiles.Count == 0) profiles.AddRange(new[] { PilotProfile.Beginner, PilotProfile.Standard, PilotProfile.Expert });

    if (logs != null) Directory.CreateDirectory(logs);
    var written = new List<(string Path, MemoryCombatLog Log)>();
    IReadOnlyList<MeasureResult> results;
    try
    {
        results = Measures.RunAll(scenarios, builds, profiles, (scenario, build, profile) =>
        {
            if (logs is null) return null;
            var log = new MemoryCombatLog();
            written.Add((Path.Combine(logs, MeasureReport.LogFileName(scenario, build, profile)), log));
            return log;
        });
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException)
    {
        // Personnage mesuré absent ou d'une autre classe, scénario complété refusé (CombatSetupException), ou (défaut du pilote)
        // combat piloté non reproductible.
        Console.Error.WriteLine("Mesure impossible : " + e.Message);
        return 1;
    }
    foreach (var (path, log) in written) File.WriteAllText(path, log.ToString(), new System.Text.UTF8Encoding(false));

    IReadOnlyList<ObjectiveResult> objectives = Measures.Objectives(results);
    if (json)
    {
        using Stream stdout = Console.OpenStandardOutput();
        stdout.Write(MeasureReport.ToJson(data, scenarios, builds, results, objectives));
    }
    else MeasureReport.WriteText(Console.Out, data, scenarios, builds, results, objectives);
    if (logs != null) Console.Error.WriteLine($"  journaux       {written.Count} dans {logs} (rejouables avec eds replay)");

    var aborted = results.Where(r => r.Aborted).ToList();
    foreach (MeasureResult r in aborted)
        Console.Error.WriteLine($"DIAGNOSTIC — combat interrompu ({r.Scenario}, {r.Build}, {Measures.IdOf(r.Profile)}) : mesures partielles, à ne pas utiliser pour l'équilibrage.");
    if (aborted.Count > 0) return abortedExitCode;
    return strict && objectives.Any(o => o.Passed == false) ? objectiveFailedExitCode : 0;
}

// Sous-lot 10.5 : mesures du réseau (parades selon la latence, désynchronisations, coupures), rapport Markdown sur la sortie standard.
static int RunNetwork(ReadOnlySpan<string> options)
{
    string dataDirectory = DefaultDataDirectory(), scenarioPath = Path.Combine("scenarios", "coop", "duo_meute.json");
    int seeds = 3;
    for (int i = 0; i < options.Length; i++)
    {
        bool hasValue = i + 1 < options.Length;
        if (options[i] == "--data" && hasValue) dataDirectory = options[++i];
        else if (options[i] == "--scenario" && hasValue) scenarioPath = options[++i];
        else if (options[i] == "--graines" && hasValue && int.TryParse(options[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= NetworkReport.MaxSeeds)
        {
            seeds = n;
            i++;
        }
        else
        {
            Console.Error.WriteLine($"Option inconnue ou invalide : {options[i]}");
            return 1;
        }
    }
    GameData? data = LoadData(dataDirectory);
    if (data is null) return 1;
    ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(scenarioPath, data);
    if (scenario.Setup is null) return Fail(scenarioPath, scenario.Report);
    IReadOnlyList<NetworkReportRow> rows;
    IReadOnlyList<NetworkMeasureResult> cuts;
    try
    {
        rows = NetworkReport.MeasureLinks(data, scenario.Setup, seeds);
        cuts = NetworkReport.MeasureCuts(data, scenario.Setup);
    }
    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
    {
        // Scénario incompatible (pas exactement deux personnages Bastion, combat qui ne commence pas).
        Console.Error.WriteLine($"Mesures impossibles ({scenarioPath}) : {e.Message}");
        return 1;
    }
    NetworkReport.Write(Console.Out, rows, cuts, seeds, Path.GetFileNameWithoutExtension(scenarioPath), scenario.Setup.DurationMs);
    // Revue Codex de la PR #22 : une divergence pendant une coupure fait aussi échouer la commande.
    return rows.All(r => r.HashesMatch && r.Desyncs == 0) && cuts.All(c => c.HashesMatch && c.Desyncs == 0) ? 0 : 3;
}

static void PrintHelp()
{
    Console.WriteLine("Éclats du Seuil — simulateur de combat (v0.1, valeurs provisoires)");
    Console.WriteLine();
    Console.WriteLine("Usage :");
    Console.WriteLine("  eds rng [--seed N] [--stream N] [--count N]   Affiche les premiers tirages PCG32");
    Console.WriteLine("  eds validate [fichier.json] [--rules R]        Valide un fichier de règles ou de classe (défaut : data/v0/rules.json) ;");
    Console.WriteLine("                                                 une classe est vérifiée avec la progression de R (défaut : data/v0/rules.json) ;");
    Console.WriteLine("                                                 un scénario ou des builds de référence avec les données de data/v0");
    Console.WriteLine("  eds simulate scénario.json [--data D] [--out J] Simule un combat ; journal JSON Lines vers J (défaut : sortie standard)");
    Console.WriteLine("                                                  Code de sortie 2 si la simulation est interrompue (statistiques de diagnostic)");
    Console.WriteLine("  eds replay journal.jsonl [--data D]            Vérifie les empreintes puis rejoue et compare octet par octet");
    Console.WriteLine("  eds report [dossier] [--data D] [--build B]... [--profile P]... [--json] [--logs L] [--strict]");
    Console.WriteLine("                                                 Mesure les builds de référence sur les scénarios de mesure (défaut :");
    Console.WriteLine("                                                 scenarios/mesures), profils beginner, standard, expert, avec le pilote ;");
    Console.WriteLine("                                                 journaux des combats vers L ; code 2 si un combat est interrompu,");
    Console.WriteLine("                                                 3 avec --strict si un objectif provisoire n'est pas atteint");
    Console.WriteLine("  eds reseau [--data D] [--scenario S] [--graines N]");
    Console.WriteLine("                                                 Mesure le jeu en réseau sur le réseau simulé (latence 0 à 200 ms,");
    Console.WriteLine("                                                 gigue, coupures) : parades, délais, désynchronisations ; rapport");
    Console.WriteLine("                                                 Markdown ; code 3 si une empreinte diffère ou une divergence survient");
}
