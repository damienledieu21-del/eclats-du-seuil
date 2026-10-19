using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EdS.Data;
using EdS.Domain.Combat;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.4 : une partie en réseau vue du jeu, sans détail de transport : héberger (salle et écoute sur ce poste, avec un
    /// équipier robot si l'on veut essayer seul) ou rejoindre une adresse, puis le client (<see cref="Client"/>). Les connexions s'ouvrent
    /// sans bloquer l'appelant (Unity relève leur résultat à chaque image) ; une coupure pendant un combat fait revenir automatiquement
    /// avec le jeton, une tentative toutes les <see cref="RetrySeconds"/> tant que le serveur garde la place. Aucune exception ne
    /// remonte de <see cref="Advance"/> : les échecs se lisent dans <see cref="Error"/>.
    /// </summary>
    public sealed class NetworkSession : IDisposable
    {
        /// <summary>Délai pour ouvrir une connexion avant d'abandonner la tentative.</summary>
        public const double ConnectTimeoutSeconds = 10;

        /// <summary>Intervalle entre deux tentatives de retour après une coupure.</summary>
        public const double RetrySeconds = 2;

        private readonly GameData _data;
        private readonly Func<Task<INetConnection>> _connect;
        private readonly string _name;
        private readonly BuildChoice _build;
        private readonly Func<Task<INetConnection>>? _connectBot;
        private readonly BuildChoice? _botBuild;
        private Pending? _pending;
        private Pending? _pendingBot;
        private double _nextRetryAt;
        private bool _disposed;

        /// <summary>Connexion en cours d'ouverture : la tâche et l'instant de départ.</summary>
        private sealed class Pending
        {
            public Task<INetConnection> Task = null!;
            public double StartedAt;
        }

        internal NetworkSession(GameData data, Func<Task<INetConnection>> connect, string name, BuildChoice build, RoomHost? host,
            Func<Task<INetConnection>>? connectBot = null, BuildChoice? botBuild = null, string? address = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            if (!MessageCodec.IsText(name, Protocol.MaxNameLength))
                throw new ArgumentException($"Nom de joueur invalide : 1 à {Protocol.MaxNameLength} caractères, sans caractère de contrôle ni espace en bord.", nameof(name));
            _name = name;
            _build = build ?? throw new ArgumentNullException(nameof(build));
            Host = host;
            _connectBot = connectBot;
            _botBuild = botBuild;
            Address = address;
            _pending = Start(_connect);
            if (_connectBot != null) _pendingBot = Start(_connectBot);
        }

        /// <summary>
        /// Héberge une salle sur ce poste pour le scénario donné (deux places pour <c>coop/duo_meute</c>) et la rejoint. Avec
        /// <paramref name="botBuild"/>, un équipier robot prend l'autre place. <paramref name="lan"/> faux : ce poste seulement.
        /// </summary>
        public static NetworkSession HostGame(GameData data, CombatSetup scenario, string name, BuildChoice build, int port = Protocol.DefaultPort,
            bool lan = true, BuildChoice? botBuild = null, RoomOptions? options = null)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (scenario is null) throw new ArgumentNullException(nameof(scenario));
            var room = new CombatRoom(data, scenario, options);
            var listener = new TcpNetListener(lan ? IPAddress.Any : IPAddress.Loopback, port);
            var host = new RoomHost(room, listener);
            try
            {
                host.Start();
                int actual = listener.Port;
                Func<Task<INetConnection>> loopback = () => Connect("127.0.0.1", actual);
                return new NetworkSession(data, loopback, name, build, host, botBuild is null ? null : loopback, botBuild, "127.0.0.1:" + actual);
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        /// <summary>Rejoint la salle hébergée à l'adresse donnée (nom de machine ou adresse IP).</summary>
        public static NetworkSession Join(GameData data, string address, int port, string name, BuildChoice build)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            string host = address.Trim();
            if (host.Length == 0) throw new ArgumentException("Adresse vide.", nameof(address));
            return new NetworkSession(data, () => Connect(host, port), name, build, null, address: host + ":" + port);
        }

        private static async Task<INetConnection> Connect(string host, int port) =>
            await TcpNetConnection.ConnectAsync(host, port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes).ConfigureAwait(false);

        /// <summary>Salle hébergée sur ce poste ; null quand on rejoint.</summary>
        public RoomHost? Host { get; }

        /// <summary>Adresse rejointe (« hôte:port »).</summary>
        public string? Address { get; }

        /// <summary>Client du joueur ; null tant que la connexion n'est pas ouverte.</summary>
        public NetworkClient? Client { get; private set; }

        /// <summary>Équipier robot ; null sans robot ou avant sa connexion.</summary>
        public BotPlayer? Bot { get; private set; }

        /// <summary>Temps écoulé de la session (somme des appels à <see cref="Advance"/>).</summary>
        public double Now { get; private set; }

        /// <summary>Connexion en cours (première ou retour).</summary>
        public bool Connecting => _pending != null;

        /// <summary>Raison d'un échec de connexion, d'un retour impossible ou d'une erreur de la salle hébergée.</summary>
        public string? Error { get; private set; }

        /// <summary>Adresses IPv4 de ce poste sur le réseau local, à donner à l'autre joueur (vide si inconnues).</summary>
        public static IReadOnlyList<string> LocalAddresses()
        {
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString()).Distinct().ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Avance la session : connexions en cours, client du joueur et robot, retour automatique après une coupure.</summary>
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (_disposed) return;
            Now += seconds;
            if (Host?.Error is Exception failure) Error = "salle hébergée arrêtée : " + failure.Message;
            PollPlayer();
            PollBot();
            Client?.Advance(seconds);
            Bot?.Advance(seconds);
            if (Client != null && Client.Status == ClientStatus.Disconnected && _pending is null)
            {
                if (!Client.CanReconnect) Error ??= "retour impossible : le serveur ne garde la place que " + Client.Options.ReconnectSeconds + " s.";
                else if (Now >= _nextRetryAt) _pending = Start(_connect);
            }
        }

        private Pending Start(Func<Task<INetConnection>> connect)
        {
            Task<INetConnection> task;
            try
            {
                task = connect();
            }
            catch (Exception e)
            {
                task = Task.FromException<INetConnection>(e);
            }
            return new Pending { Task = task, StartedAt = Now };
        }

        /// <summary>Résultat d'une connexion en cours : la connexion, null si elle n'est pas encore là, ou l'erreur.</summary>
        private INetConnection? Take(ref Pending? pending, out string? error)
        {
            error = null;
            if (pending is null) return null;
            Task<INetConnection> task = pending.Task;
            if (!task.IsCompleted)
            {
                if (Now - pending.StartedAt < ConnectTimeoutSeconds) return null;
                // Abandonnée : si elle aboutit plus tard, la connexion est refermée aussitôt.
                task.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose();
                }, TaskScheduler.Default);
                pending = null;
                error = $"délai de {ConnectTimeoutSeconds} s dépassé.";
                return null;
            }
            pending = null;
            if (task.Status == TaskStatus.RanToCompletion) return task.Result;
            Exception? e = task.Exception?.GetBaseException();
            error = e?.Message ?? "tentative annulée.";
            return null;
        }

        private void PollPlayer()
        {
            INetConnection? connection = Take(ref _pending, out string? error);
            if (connection is null)
            {
                if (error == null) return;
                if (Client is null) Error = "connexion impossible : " + error;
                else _nextRetryAt = Now + RetrySeconds;   // retour : nouvelle tentative plus tard
                return;
            }
            if (_disposed)
            {
                connection.Dispose();
                return;
            }
            if (Client is null)
            {
                Client = new NetworkClient(_data, connection, _name, _build);
                return;
            }
            if (Client.Status != ClientStatus.Disconnected || !Client.CanReconnect)
            {
                connection.Dispose();
                return;
            }
            Error = null;
            // Revue Codex de la PR #21 (tour 7) : si cette connexion se referme avant l'accueil (serveur qui redémarre, intermédiaire qui
            // accepte puis coupe), la tentative suivante attend aussi RetrySeconds au lieu de repartir à l'image suivante.
            _nextRetryAt = Now + RetrySeconds;
            Client.Reconnect(connection);
        }

        private void PollBot()
        {
            INetConnection? connection = Take(ref _pendingBot, out string? error);
            if (connection is null)
            {
                if (error != null) Error = "robot : connexion impossible : " + error;
                return;
            }
            if (_disposed)
            {
                connection.Dispose();
                return;
            }
            Bot = new BotPlayer(new NetworkClient(_data, connection, "Robot", _botBuild!));
        }

        /// <summary>Quitte la partie : départ du joueur et du robot, arrêt de la salle hébergée.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Client?.Leave();
            Bot?.Client.Leave();
            foreach (Pending? pending in new[] { _pending, _pendingBot })
                pending?.Task.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose();
                }, TaskScheduler.Default);
            _pending = null;
            _pendingBot = null;
            Host?.Dispose();
        }
    }
}
