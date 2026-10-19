using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : connexion TCP à messages encadrés (<see cref="Framing"/>), sans bibliothèque. Un fil lit le flux et range les messages
    /// complets ; un autre écrit la file d'envoi. Les deux files sont bornées : un pair qui inonde ou qui ne lit plus est déconnecté au lieu
    /// de faire grossir la mémoire. Un message annoncé au-delà de la limite de réception ferme la connexion avant d'être lu.
    /// </summary>
    public sealed class TcpNetConnection : INetConnection
    {
        /// <summary>Messages au plus en attente de lecture par l'application.</summary>
        public const int MaxPendingReceived = 4096;

        /// <summary>Messages au plus en attente d'envoi.</summary>
        public const int MaxPendingSend = 4096;

        /// <summary>
        /// Octets au plus en attente dans chaque sens (lecture par l'application, envoi au pair) : avec des messages jusqu'à 4 Mio, le
        /// nombre de messages seul ne bornerait pas la mémoire (revue Codex de la PR #18).
        /// </summary>
        public const long MaxPendingBytes = 64L * 1024 * 1024;

        /// <summary>Délai d'écriture : un pair qui ne lit plus est déconnecté.</summary>
        public const int SendTimeoutMs = 10_000;

        private readonly TcpClient _client;
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly int _maxReceive;
        private readonly int _maxSend;
        private readonly ConcurrentQueue<byte[]> _received = new ConcurrentQueue<byte[]>();
        private readonly BlockingCollection<byte[]> _outgoing = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), MaxPendingSend);
        private readonly object _gate = new object();
        private string? _closeReason;
        private long _receivedBytes;
        private long _outgoingBytes;

        /// <param name="maxReceivePayload">Plus gros message accepté de l'autre côté.</param>
        /// <param name="maxSendPayload">Plus gros message que l'autre côté accepte.</param>
        public TcpNetConnection(TcpClient client, int maxReceivePayload, int maxSendPayload)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (maxReceivePayload < 1) throw new ArgumentOutOfRangeException(nameof(maxReceivePayload));
            if (maxSendPayload < 1) throw new ArgumentOutOfRangeException(nameof(maxSendPayload));
            _maxReceive = maxReceivePayload;
            _maxSend = maxSendPayload;
            _client.NoDelay = true;
            _client.SendTimeout = SendTimeoutMs;
            _socket = _client.Client;
            _stream = _client.GetStream();
            RemoteAddress = _client.Client.RemoteEndPoint?.ToString() ?? "?";
            new Thread(ReadLoop) { IsBackground = true, Name = "EdS.Net lecture " + RemoteAddress }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "EdS.Net écriture " + RemoteAddress }.Start();
        }

        /// <summary>Connexion à un serveur, sans bloquer l'appelant (Unity relève la tâche à chaque image).</summary>
        public static Task<TcpNetConnection> ConnectAsync(string host, int port, int maxReceivePayload, int maxSendPayload)
        {
            if (host is null) throw new ArgumentNullException(nameof(host));
            return Task.Run(async () =>
            {
                var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(host, port).ConfigureAwait(false);
                    return new TcpNetConnection(client, maxReceivePayload, maxSendPayload);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            });
        }

        public string RemoteAddress { get; }

        public bool IsOpen
        {
            get { lock (_gate) return _closeReason is null; }
        }

        public string? CloseReason
        {
            get { lock (_gate) return _closeReason; }
        }

        public bool Send(byte[] payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length == 0) throw new ArgumentException("Message vide.", nameof(payload));
            if (!IsOpen) return false;
            if (payload.Length > _maxSend)
            {
                Close($"message de {payload.Length} octets à envoyer, l'autre côté en accepte {_maxSend} au plus.");
                return false;
            }
            if (Interlocked.Add(ref _outgoingBytes, payload.Length) > MaxPendingBytes)
            {
                Interlocked.Add(ref _outgoingBytes, -payload.Length);
                Close("file d'envoi pleine : l'autre côté ne lit plus.");
                return false;
            }
            try
            {
                if (_outgoing.TryAdd(Framing.Frame(payload))) return true;
            }
            catch (InvalidOperationException)
            {
                Interlocked.Add(ref _outgoingBytes, -payload.Length);
                return false;   // file fermée entre-temps
            }
            Interlocked.Add(ref _outgoingBytes, -payload.Length);
            Close("file d'envoi pleine : l'autre côté ne lit plus.");
            return false;
        }

        public bool TryReceive(out byte[] payload)
        {
            if (!_received.TryDequeue(out payload!)) return false;
            Interlocked.Add(ref _receivedBytes, -payload.Length);
            return true;
        }

        public void Close(string reason)
        {
            lock (_gate)
            {
                if (_closeReason != null) return;
                _closeReason = reason ?? "fermée.";
            }
            // Les messages déjà en file partent encore ; le fil d'écriture ferme ensuite la socket.
            _outgoing.CompleteAdding();
        }

        public void Dispose()
        {
            Close("fermée.");
            Shutdown();
        }

        /// <summary>Fermeture immédiate de la socket (débloque le fil de lecture).</summary>
        private void Shutdown()
        {
            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // déjà fermée
            }
        }

        /// <summary>Fermeture vue d'un fil : la raison est gardée seulement si personne n'a fermé avant.</summary>
        private void Fail(string reason)
        {
            Close(reason);
            Shutdown();
        }

        private void ReadLoop()
        {
            var reader = new FrameReader(_maxReceive);
            var buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    int read = _stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        Fail("connexion fermée par l'autre côté.");
                        return;
                    }
                    foreach (byte[] message in reader.Push(buffer, 0, read))
                    {
                        if (_received.Count >= MaxPendingReceived || Interlocked.Read(ref _receivedBytes) + message.Length > MaxPendingBytes)
                        {
                            Fail("trop de messages en attente de lecture.");
                            return;
                        }
                        Interlocked.Add(ref _receivedBytes, message.Length);
                        _received.Enqueue(message);
                    }
                    if (reader.Error != null)
                    {
                        Fail("faute de protocole : " + reader.Error);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                // Aucune exception ne sort d'un fil : elle arrêterait le processus (Unity compris).
                Fail("connexion perdue : " + e.Message);
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (byte[] frame in _outgoing.GetConsumingEnumerable())
                {
                    _stream.Write(frame, 0, frame.Length);
                    Interlocked.Add(ref _outgoingBytes, -(frame.Length - Framing.HeaderBytes));
                }
                // File close et vidée : fin d'envoi propre, puis fermeture.
                try
                {
                    _socket.Shutdown(SocketShutdown.Send);
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    // déjà fermée
                }
                // Laisse au pair le temps de lire avant de libérer la socket ; le fil de lecture s'arrête à la fermeture.
                Thread.Sleep(100);
                Shutdown();
            }
            catch (Exception e)
            {
                // Aucune exception ne sort d'un fil : elle arrêterait le processus (Unity compris).
                Fail("envoi impossible : " + e.Message);
            }
        }
    }

    /// <summary>Écoute TCP : un fil accepte les connexions entrantes et les range ; <see cref="TryAccept"/> ne bloque pas.</summary>
    public sealed class TcpNetListener : INetListener
    {
        /// <summary>Connexions acceptées au plus en attente de prise en charge.</summary>
        public const int MaxPendingAccepted = 64;

        private readonly TcpListener _listener;
        private readonly int _maxReceive;
        private readonly int _maxSend;
        private readonly Queue<TcpNetConnection> _accepted = new Queue<TcpNetConnection>();
        private readonly object _gate = new object();
        private bool _stopped;

        /// <param name="address">Adresse d'écoute (<see cref="IPAddress.Any"/> : tout le réseau local ; <see cref="IPAddress.Loopback"/> : ce poste).</param>
        /// <param name="port">Port ; 0 pour un port libre choisi par le système (tests).</param>
        public TcpNetListener(IPAddress address, int port, int maxReceivePayload = Protocol.MaxClientMessageBytes, int maxSendPayload = Protocol.MaxServerMessageBytes)
        {
            _maxReceive = maxReceivePayload;
            _maxSend = maxSendPayload;
            _listener = new TcpListener(address ?? throw new ArgumentNullException(nameof(address)), port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            new Thread(AcceptLoop) { IsBackground = true, Name = "EdS.Net écoute " + Port }.Start();
        }

        /// <summary>Port effectivement écouté.</summary>
        public int Port { get; }

        public bool TryAccept(out INetConnection connection)
        {
            lock (_gate)
            {
                if (_accepted.Count == 0)
                {
                    connection = null!;
                    return false;
                }
                connection = _accepted.Dequeue();
                return true;
            }
        }

        private void AcceptLoop()
        {
            while (true)
            {
                lock (_gate)
                    if (_stopped) return;
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    return;   // écoute arrêtée (aucune exception ne sort d'un fil)
                }
                TcpNetConnection connection;
                try
                {
                    connection = new TcpNetConnection(client, _maxReceive, _maxSend);
                }
                catch (Exception)
                {
                    client.Dispose();
                    continue;
                }
                // Revue Codex de la PR #18 : l'enregistrement et l'arrêt sont exclusifs ; une connexion arrivée pendant l'arrêt est fermée.
                bool kept;
                lock (_gate)
                {
                    kept = !_stopped && _accepted.Count < MaxPendingAccepted;
                    if (kept) _accepted.Enqueue(connection);
                }
                if (!kept) connection.Dispose();
            }
        }

        /// <summary>Arrête l'écoute : les connexions pas encore prises en charge sont fermées, aucune n'est plus rendue ensuite.</summary>
        public void Dispose()
        {
            TcpNetConnection[] pending;
            lock (_gate)
            {
                if (_stopped) return;
                _stopped = true;
                pending = _accepted.ToArray();
                _accepted.Clear();
            }
            _listener.Stop();
            foreach (TcpNetConnection connection in pending) connection.Dispose();
        }
    }
}
