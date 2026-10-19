using System.Net;
using EdS.Data;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Net;

namespace EdS.Server;

/// <summary>
/// Sous-lot 10.2 : serveur dédié d'Éclats du Seuil. Une salle de combat autoritaire (scénario de coopération par défaut) écoute sur le
/// réseau local ; chaque combat terminé peut être écrit en journal rejouable (<c>eds replay</c>).
/// </summary>
public static class Program
{
    private const string Usage =
        "Usage : eds-server [--data data/v0] [--scenario scenarios/coop/duo_meute.json] [--port 7777] [--duration ms] [--journaux dossier] [--local]\n" +
        "                  [--robot [build.bastion.gardien]]\n" +
        "  --local : écoute ce poste seulement (127.0.0.1) ; sinon tout le réseau local.\n" +
        "  --robot : un équipier robot prend une place (pour essayer la coopération seul).";

    public static int Main(string[] args)
    {
        string data = Path.Combine("data", "v0"), scenarioPath = Path.Combine("scenarios", "coop", "duo_meute.json");
        int port = Protocol.DefaultPort;
        long? duration = null;
        string? journals = null;
        bool local = false;
        string? robot = null;
        for (int i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (args[i])
            {
                case "--data": data = Next() ?? ""; break;
                case "--scenario": scenarioPath = Next() ?? ""; break;
                case "--port":
                    if (!int.TryParse(Next(), out port) || port < 0 || port > 65535) return Fail("port invalide.");
                    break;
                case "--duration":
                    if (!long.TryParse(Next(), out long ms) || ms <= 0) return Fail("durée invalide.");
                    duration = ms;
                    break;
                case "--journaux": journals = Next(); break;
                case "--local": local = true; break;
                case "--robot":
                    robot = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "build.bastion.gardien";
                    break;
                case "-h":
                case "--help":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    return Fail($"option inconnue « {args[i]} ».");
            }
        }

        GameDataLoadResult loaded = GameDataLoader.Load(data);
        if (loaded.Data is not { } gameData)
        {
            Console.Error.WriteLine($"Données refusées ({data}) :");
            foreach (var failure in loaded.Failures) Console.Error.WriteLine($"  {failure.Path} : {failure.Report}");
            return 1;
        }
        ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(scenarioPath, gameData);
        if (scenario.Setup is null)
        {
            Console.Error.WriteLine($"Scénario refusé ({scenarioPath}) : {scenario.Report}");
            return 1;
        }

        if (robot != null && !gameData.TryGetBuild(robot, out _)) return Fail($"build du robot inconnu : « {robot} ».");
        var room = new CombatRoom(gameData, scenario.Setup, new RoomOptions { DurationMs = duration });
        room.Trace = message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        int combats = 0;
        if (journals != null)
        {
            Directory.CreateDirectory(journals);
            room.CombatEnded = lines =>
            {
                string file = Path.Combine(journals, $"combat-{++combats:000}.jsonl");
                File.WriteAllText(file, string.Concat(lines.Select(l => l + "\n")));
                Console.WriteLine($"journal écrit : {file}");
            };
        }

        using var listener = new TcpNetListener(local ? IPAddress.Loopback : IPAddress.Any, port);
        using var host = new RoomHost(room, listener);
        host.Start();
        Console.WriteLine($"Serveur Éclats du Seuil : {Path.GetFileNameWithoutExtension(scenarioPath)}, places {string.Join(", ", room.SeatIds)}, " +
            $"port {listener.Port}{(local ? " (ce poste seulement)" : "")}. Ctrl+C pour arrêter.");
        // Sous-lot 10.4 : équipier robot, un client ordinaire relié à ce serveur par le poste lui-même.
        BotPlayer? bot = null;
        if (robot != null)
        {
            TcpNetConnection connection = TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes)
                .GetAwaiter().GetResult();
            bot = new BotPlayer(new NetworkClient(gameData, connection, "Robot", new BuildChoice(robot)));
            Console.WriteLine($"Équipier robot connecté ({robot}).");
        }
        using var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double last = 0;
        bool botStopped = false;
        while (!stop.Wait(bot is null ? 500 : 10))
        {
            if (host.Error is { } error)
            {
                Console.Error.WriteLine("Erreur du serveur : " + error);
                return 1;
            }
            if (bot is null) continue;
            double now = clock.Elapsed.TotalSeconds;
            bot.Advance(now - last);
            last = now;
            if (!botStopped && (bot.Client.Status == ClientStatus.Failed || bot.Client.Status == ClientStatus.Left))
            {
                botStopped = true;
                Console.Error.WriteLine("Équipier robot arrêté : " + bot.Client.Error);
            }
        }
        Console.WriteLine("Arrêt du serveur.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("Erreur : " + message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
