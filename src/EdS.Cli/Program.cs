using System.Globalization;
using EdS.Data.Json;
using EdS.Data.Rules;
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
        return RunValidate(args.Length > 1 ? args[1] : Path.Combine("data", "v0", "rules.json"));
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

static int RunValidate(string path)
{
    RulesLoadResult result = RulesJsonReader.ReadFile(path);
    if (result.Rules is { } rules)
    {
        Console.WriteLine($"{path} : valide");
        Console.WriteLine($"  schemaVersion  {rules.SchemaVersion}");
        Console.WriteLine($"  contentVersion {rules.ContentVersion}");
        Console.WriteLine($"  empreinte      {rules.Fingerprint}");
        return 0;
    }

    Console.Error.WriteLine($"{path} : {result.Report.Errors.Count} erreur(s)");
    foreach (var error in result.Report.Errors)
        Console.Error.WriteLine("  " + error);
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("Éclats du Seuil — simulateur de combat (v0.1, valeurs provisoires)");
    Console.WriteLine();
    Console.WriteLine("Usage :");
    Console.WriteLine("  eds rng [--seed N] [--stream N] [--count N]   Affiche les premiers tirages PCG32");
    Console.WriteLine("  eds validate [chemin/rules.json]               Valide un fichier de règles (défaut : data/v0/rules.json)");
}
