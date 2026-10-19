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
        "  --local : écoute ce poste seulement (127.0.0.1) ; sinon tout le réseau local.";

    public static int Main(string[] args)
    {
        string data = Path.Combine("data", "v0"), scenarioPath = Path.Combine("scenarios", "coop", "duo_meute.json");
        int port = Protocol.DefaultPort;
        long? duration = null;
        string? journals = null;
        bool local = false;
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

        var room = new CombatRoom(gameData, scenario.Setup, new RoomOptions { DurationMs = duration });
        room.Trace = message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (journals != null)
        {
            // Revue Codex de la PR #19 : un serveur relancé sur le même dossier continue la numérotation, sans écraser de journal ; une
            // erreur d'écriture est signalée sans arrêter la salle.
            var folder = new JournalFolder(journals);
            room.CombatEnded = lines =>
            {
                try
                {
                    Console.WriteLine($"journal écrit : {folder.Write(lines)}");
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"journal non écrit ({folder.Directory}) : {e.Message}");
                }
            };
        }

        using var listener = new TcpNetListener(local ? IPAddress.Loopback : IPAddress.Any, port);
        using var host = new RoomHost(room, listener);
        host.Start();
        Console.WriteLine($"Serveur Éclats du Seuil : {Path.GetFileNameWithoutExtension(scenarioPath)}, places {string.Join(", ", room.SeatIds)}, " +
            $"port {listener.Port}{(local ? " (ce poste seulement)" : "")}. Ctrl+C pour arrêter.");
        using var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };
        while (!stop.Wait(500))
        {
            if (host.Error is { } error)
            {
                Console.Error.WriteLine("Erreur du serveur : " + error);
                return 1;
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
